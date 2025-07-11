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

    private SubscriberSocket subscriber;
    private string message; // The message received from the socket
    public Pose pose { get; private set; }

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
        subscriber.Dispose();
    }

    private void UpdatePose(ZmqMessage zmqMessage)
    {
        // Transform the position
        Vector3 position = new Vector3(zmqMessage.x, zmqMessage.y, zmqMessage.z);

        // Transform the rotation
        Quaternion rotation = Quaternion.Euler(zmqMessage.pitch, zmqMessage.yaw, zmqMessage.roll);

        // Update the pose
        pose = new Pose(position, rotation);
    }
}
