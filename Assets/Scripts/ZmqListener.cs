using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System.Threading;
using System;
using System.IO;

public class ZmqListener : MonoBehaviour
{
    // Receives sensor data from ZMQ and exposes a normalized runtime view:
    // position (raw units), raw rotation (radians), and Unity quaternion.
    [SerializeField]
    [Tooltip("The ip address of the socket to connect to")]
    public string address = "localhost"; // Replace with your socket address

    [Tooltip("The port of the socket to connect to")]
    [SerializeField]
    public int port = 9872; // Replace with your port number

    private SubscriberSocket subscriber;
    private string message; // The message received from the socket
    
    // Three data types: position (invariant), raw rotation (radians), quaternion (Unity)
    public Vector3 position { get; private set; }  // Position data (invariant units)
    public Vector3 rawRotation { get; private set; }  // Raw rotation in radians
    public Quaternion quaternion { get; private set; }  // Unity quaternion
    public bool hasReceivedPose { get; private set; }  // True after first parsed message

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
        if (ReplaySessionContext.IsActive)
        {
            enabled = false;
            return;
        }

        // Apply system config at start
        ApplySystemConfig();

        subscriber = new SubscriberSocket();
        subscriber.Connect($"tcp://{address}:{port}");
        subscriber.SubscribeToAnyTopic(); // Subscribe to all topics

        // Start listening for messages on a separate thread
        new Thread(() =>
        {
            while (true)
            {
                try
                {
                    string topic = subscriber.ReceiveFrameString();
                    message = subscriber.ReceiveFrameString();

                    // Update the pose based on the received values
                    ZmqMessage zmqMessage = JsonUtility.FromJson<ZmqMessage>(message);
                    UpdatePose(zmqMessage);
                }
                catch (NetMQException ex)
                {
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
            }
        }).Start();
    }

    private void ApplySystemConfig()
    {
        // Find the MainController
        SystemConfig config = null;
        if (ReplaySessionContext.IsActive && ReplayExperimentHost.Instance != null)
            config = ReplayExperimentHost.Instance.GetSystemConfigForGameObject(gameObject);

        MainController mainController = FindObjectOfType<MainController>();
        if (config == null && mainController != null)
            config = mainController.GetSystemConfigForGameObject(gameObject);

        if (config != null)
        {

            // Apply config values directly
            address = config.zmqAddress;
            port = config.zmqPort;

            Debug.Log($"Applied system config to {gameObject.name}: ZMQ={address}:{port}");
        }
    }

    void OnDestroy()
    {
        subscriber?.Dispose();
    }

    private void UpdatePose(ZmqMessage zmqMessage)
    {
        // Keep data semantics aligned with the historical origin/main behavior.
        position = new Vector3(zmqMessage.x, zmqMessage.y, zmqMessage.z);

        quaternion = Quaternion.Euler(zmqMessage.pitch, zmqMessage.yaw, zmqMessage.roll);
        rawRotation = new Vector3(
            quaternion.eulerAngles.x * Mathf.Deg2Rad,
            quaternion.eulerAngles.y * Mathf.Deg2Rad,
            quaternion.eulerAngles.z * Mathf.Deg2Rad
        );
        hasReceivedPose = true;
    }
}
