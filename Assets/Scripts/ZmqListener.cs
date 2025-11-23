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
    
    // Three data types: position (invariant), raw rotation (radians), quaternion (Unity)
    public Vector3 position { get; private set; }  // Position data (invariant units)
    public Vector3 rawRotation { get; private set; }  // Raw rotation in radians
    public Quaternion quaternion { get; private set; }  // Unity quaternion (converted from radians)

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
        // 1. Position data (invariant units) - log raw values as they come in, no fudging
        //    For kinefly mode: x = left_angle (radians), y = right_angle (radians), z = 0
        //    For FicTrac mode: x, y, z are actual position coordinates
        position = new Vector3(zmqMessage.x, zmqMessage.y, zmqMessage.z);
        
        // 2. Raw rotation data (in radians) - preserve raw values, no conversion
        //    For kinefly mode: yaw = left_angle - right_angle (radians)
        rawRotation = new Vector3(zmqMessage.pitch, zmqMessage.yaw, zmqMessage.roll);
        
        // 3. Unity quaternion (converted from radians to degrees for Quaternion.Euler)
        //    This is only for Unity's internal use, raw radians are preserved above
        quaternion = Quaternion.Euler(
            zmqMessage.pitch * Mathf.Rad2Deg, 
            zmqMessage.yaw * Mathf.Rad2Deg, 
            zmqMessage.roll * Mathf.Rad2Deg
        );
    }
}
