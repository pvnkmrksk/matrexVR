using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System.Threading;

public class ZmqListener : MonoBehaviour
{
    private readonly string address = "tcp://localhost:9872"; // Replace with your socket address
    private SubscriberSocket subscriber;
    private string message; // The message received from the socket
    private Vector3 positionToUpdate;
    private Quaternion rotationToUpdate;

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

                    // Update the position based on the received values
                    string[] values = message.Split(',');

                    // Transform the position
                    float posx = float.Parse(values[0].Split(':')[1])*5;
                    float posy = float.Parse(values[1].Split(':')[1])*5;
                    positionToUpdate = new Vector3(posx, 0.0f, posy);

                    // Transform the rotation
                    float heading = float.Parse(values[2].Split(':')[1])* Mathf.Rad2Deg;
                    rotationToUpdate = Quaternion.Euler(0.0f, heading , 0.0f);
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

    void Update()
    {
        // Check if 'values' array is populated before updating the position and rotation
        if (positionToUpdate != null)
        {
            transform.position = positionToUpdate;
            transform.rotation = rotationToUpdate;
        }
    }

    void OnDestroy()
    {
        subscriber.Dispose();
    }
}
