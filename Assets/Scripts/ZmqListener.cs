using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System.Threading;
using System;
using Newtonsoft.Json;

public class ZmqListener : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The ip address of the socket to connect to")]
    public string address = "localhost"; // Replace with your socket address

    [Tooltip("The port of the socket to connect to")]
    [SerializeField]
    public int port = 9872; // Replace with your port number

    private Thread listenerThread;
    private volatile bool isRunning;
    private volatile string workerState = "not started";
    private volatile string wireFormat;
    public string Endpoint { get; private set; }
    public string WireFormat => wireFormat;
    public string State => workerState != "waiting for packets" ? workerState :
        !HasPose ? "waiting for packets" : HasFreshPose() ? "receiving" : "stale";
    // Pose is a multi-word struct. Publish/copy it under one lock so a render frame
    // cannot combine position or quaternion components from different network packets.
    private readonly object poseLock = new object();
    private Pose latestPose;
    private Vector3 latestRawRotation;
    // Preserve wire values for Bogong/Kinefly. Do not recover signed radians from a quaternion.
    public struct RawSample
    {
        public Vector3 position;
        public Vector3 rotation;
        public Quaternion convertedRotation;
        public long sequence, receivedUtcTicks;
        public bool hasPose;
    }
    public RawSample ReadRawSample()
    {
        lock (poseLock) return new RawSample { position = latestPose.position, rotation = latestRawRotation, convertedRotation = latestPose.rotation,
            sequence = receivedSequence, receivedUtcTicks = lastPoseTicksUtc, hasPose = hasPose };
    }
    private bool hasPose;
    public Pose pose
    {
        get { lock (poseLock) return latestPose; }
        private set { lock (poseLock) latestPose = value; }
    }
    public bool HasPose
    {
        get { lock (poseLock) return hasPose; }
        private set { lock (poseLock) hasPose = value; }
    }
    public double SecondsSinceLastPose
    {
        get
        {
            lock (poseLock)
            {
                if (!hasPose || lastPoseTicksUtc == 0) return double.PositiveInfinity;
                return TimeSpan.FromTicks(DateTime.UtcNow.Ticks - lastPoseTicksUtc).TotalSeconds;
            }
        }
    }

    private long lastPoseTicksUtc, receivedSequence;

    // Keep legacy missing-field defaults (zero) for trackers that only send yaw.
    private class ZmqMessage
    {
        public float x;
        public float y;
        public float z;
        public float roll;
        public float pitch;
        public float yaw;
    }

    void Start()
    {
        ApplySystemConfig();
        string endpoint = $"tcp://{address}:{port}";
        Endpoint = endpoint;
        workerState = "starting";
        isRunning = true;
        listenerThread = new Thread(() => Listen(endpoint)) { IsBackground = true, Name = name + "_ZmqListener" };
        listenerThread.Start();
    }

    private void Listen(string endpoint)
    {
        // Create, use and dispose on one thread. Main-thread shutdown only sets a flag.
        try
        {
            using (var socket = new SubscriberSocket())
            {
                socket.Options.Linger = TimeSpan.Zero;
                socket.Connect(endpoint);
                socket.SubscribeToAnyTopic();
                workerState = "waiting for packets";
                while (isRunning)
                {
                    NetMQMessage frames = new NetMQMessage();
                    if (!socket.TryReceiveMultipartMessage(TimeSpan.FromMilliseconds(100), ref frames)) continue;
                    if (!isRunning) break;
                    try
                    {
                        string json;
                        string format;
                        if (frames.FrameCount == 1)
                        {
                            json = frames[0].ConvertToString().TrimStart();
                            // Kinefly's ROS bridge sends a single JSON frame, with no topic.
                            // Older publishers may send the topic as a separate message.
                            // Do not discard alternate JSON samples as the old paired reads did.
                            if (!json.StartsWith("{", StringComparison.Ordinal)) continue;
                            format = "single JSON";
                        }
                        else if (frames.FrameCount == 2)
                        {
                            json = frames[1].ConvertToString();
                            format = "topic + JSON";
                        }
                        else throw new FormatException("Expected a JSON pose frame or topic + JSON pose frames.");
                        UpdatePose(JsonConvert.DeserializeObject<ZmqMessage>(json));
                        if (wireFormat == null) Debug.Log("Tracking input received on " + endpoint + " (" + format + ").");
                        wireFormat = format;
                    }
                    catch (Exception ex)
                    {
                        // Limit repeated malformed-packet reports to once per second.
                        long now = DateTime.UtcNow.Ticks;
                        if (now - lastPacketErrorTicks >= TimeSpan.TicksPerSecond)
                        {
                            lastPacketErrorTicks = now;
                            Debug.LogError("Invalid tracking packet on " + endpoint + ": " + ex.Message);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (isRunning)
            {
                workerState = "error";
                Debug.LogError("ZMQ listener failed on " + endpoint + ": " + ex);
            }
        }
    }
    private long lastPacketErrorTicks;

    private void ApplySystemConfig()
    {
        // Find the MainController
        MainController mainController = FindObjectOfType<MainController>();
        if (mainController != null)
        {
            // Get config values based on GameObject name
            if (!mainController.TryGetSystemConfigForGameObject(gameObject, out SystemConfig config))
            {
                Debug.LogError($"No tracking config found for {gameObject.name}; retaining component endpoint tcp://{address}:{port}. Check vrId in system_config.json.");
                return;
            }

            // Apply config values directly
            address = config.zmqAddress;
            port = config.zmqPort;

            Debug.Log($"Applied system config to {gameObject.name}: ZMQ={address}:{port}");
        }
    }

    void OnDestroy()
    {
        StopListener();
    }

    private void UpdatePose(ZmqMessage zmqMessage)
    {
        if (zmqMessage == null || !Finite(zmqMessage.x) || !Finite(zmqMessage.y) || !Finite(zmqMessage.z) ||
            !Finite(zmqMessage.pitch) || !Finite(zmqMessage.yaw) || !Finite(zmqMessage.roll))
            throw new FormatException("Tracking pose must contain finite numbers.");
        // Transform the position
        Vector3 position = new Vector3(zmqMessage.x, zmqMessage.y, zmqMessage.z);

        // Transform the rotation
        Quaternion rotation = Quaternion.Euler(zmqMessage.pitch, zmqMessage.yaw, zmqMessage.roll);

        // Update the pose
        lock (poseLock)
        {
            latestRawRotation = new Vector3(zmqMessage.pitch, zmqMessage.yaw, zmqMessage.roll);
            latestPose = new Pose(position, rotation);
            hasPose = true;
            lastPoseTicksUtc = DateTime.UtcNow.Ticks;
            receivedSequence++;
        }
    }

    private void PublishPose(Pose value)
    {
        lock (poseLock)
        {
            latestPose = value;
            hasPose = true;
            lastPoseTicksUtc = DateTime.UtcNow.Ticks;
            receivedSequence++;
        }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public bool HasFreshPose(double maxAgeSeconds = 1.0)
    {
        return HasPose && SecondsSinceLastPose <= maxAgeSeconds;
    }

    private void StopListener()
    {
        isRunning = false;

        if (listenerThread != null && listenerThread.IsAlive)
        {
            if (!listenerThread.Join(500))
            {
                Debugger.Log($"ZMQ listener thread for {gameObject.name} did not stop within 500 ms.", 2);
            }
        }

        listenerThread = null;
        workerState = "stopped";
    }
}
