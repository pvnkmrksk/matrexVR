using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System.Threading;
using System;
using System.IO;

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
    public Pose pose { get; private set; }
    public bool HasPose { get; private set; }
    public double SecondsSinceLastPose
    {
        get
        {
            if (!HasPose || lastPoseTicksUtc == 0)
            {
                return double.PositiveInfinity;
            }

            return TimeSpan.FromTicks(DateTime.UtcNow.Ticks - lastPoseTicksUtc).TotalSeconds;
        }
    }

    private long lastPoseTicksUtc;

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
        // Apply system config at start
        ApplySystemConfig();

        string endpoint = $"tcp://{address}:{port}";

        // Start listening for messages on a separate thread
        isRunning = true;
        listenerThread = new Thread(() =>
        {
            // NetMQ sockets must be created, read and disposed on the same thread.
            // A bounded receive lets scene unload stop the listener without disposing
            // a socket concurrently with its blocking receive.
            try
            {
                using (var subscriber = new SubscriberSocket())
                {
                    subscriber.Options.Linger = TimeSpan.Zero;
                    subscriber.Connect(endpoint);
                    subscriber.SubscribeToAnyTopic();
                    bool waitingForPayload = false;
                    while (isRunning)
                    {
                        try
                        {
                            if (!subscriber.TryReceiveFrameString(TimeSpan.FromMilliseconds(50), out string message)) continue;
                            if (!waitingForPayload)
                            {
                                waitingForPayload = true; // Preserve the existing topic + JSON frame protocol.
                                continue;
                            }
                            waitingForPayload = false;

                            if (!isRunning)
                            {
                                break;
                            }

                            // Update the pose based on the received values
                            ZmqMessage zmqMessage = JsonUtility.FromJson<ZmqMessage>(message);
                            UpdatePose(zmqMessage);
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }
                        catch (NetMQException ex)
                        {
                            if (!isRunning)
                            {
                                break;
                            }

                            // Change error level from 1 (error) to 3 (info) for socket exceptions
                            string errorMessage = ex.ToString();

                            // Handle common socket messages that shouldn't be treated as errors
                            if (errorMessage.Contains("connection reset by peer") ||
                                errorMessage.Contains("non-blocking socket would block"))
                            {
                                Debugger.Log("NetMQ socket info: " + errorMessage, 3);
                            }
                            else
                            {
                                // For other NetMQ exceptions, still log as warnings
                                Debugger.Log("NetMQException: " + errorMessage, 2);
                            }

                            Thread.Sleep(100);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            if (!isRunning)
                            {
                                break;
                            }

                            Debugger.Log("Unhandled ZMQ listener exception: " + ex, 1);
                            Thread.Sleep(100);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (isRunning) Debug.LogError("ZMQ listener failed: " + ex);
            }
        })
        {
            IsBackground = true,
            Name = $"{gameObject.name}_ZmqListener"
        };
        listenerThread.Start();
    }

    private void ApplySystemConfig()
    {
        // Find the MainController
        MainController mainController = FindObjectOfType<MainController>();
        if (mainController != null)
        {
            // Get config values based on GameObject name
            SystemConfig config = mainController.GetSystemConfigForGameObject(gameObject);

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
        // Transform the position
        Vector3 position = new Vector3(zmqMessage.x, zmqMessage.y, zmqMessage.z);

        // Transform the rotation
        Quaternion rotation = Quaternion.Euler(zmqMessage.pitch, zmqMessage.yaw, zmqMessage.roll);

        // Update the pose
        pose = new Pose(position, rotation);
        HasPose = true;
        lastPoseTicksUtc = DateTime.UtcNow.Ticks;
    }

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
    }
}
