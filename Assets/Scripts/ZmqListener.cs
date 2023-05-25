using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System.Threading;
using System;
using System.IO;

public class ZmqListener : MonoBehaviour
{
    private readonly string address = "tcp://localhost:9872"; // Replace with your socket address
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
        subscriber = new SubscriberSocket();
        subscriber.Connect(address);
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
                    Debug.Log("NetMQException: " + ex.ToString());
                    Thread.Sleep(100);
                    continue;
                }
            }
        }).Start();
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
