using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System.Threading;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

// using UnityEngine.SceneManagement;

public class ZmqListener : MonoBehaviour
{
    private readonly string address = "tcp://localhost:9872"; // Replace with your socket address
    private SubscriberSocket subscriber;
    private string message; // The message received from the socket
    private Vector3 positionToUpdate;
    private Quaternion rotationToUpdate;
    private string logPath;
    private StreamWriter logFile;

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
                    float posx = float.Parse(values[0].Split(':')[1]) * 5;
                    float posy = float.Parse(values[1].Split(':')[1]) * 5;
                    positionToUpdate = new Vector3(posx, 0.0f, posy);

                    // Transform the rotation
                    float heading = float.Parse(values[2].Split(':')[1]) * Mathf.Rad2Deg;
                    rotationToUpdate = Quaternion.Euler(0.0f, heading, 0.0f);
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

    void InitLog()
    {
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string time = DateTime.Now.ToString("HH-mm-ss");
        string directoryPath = Path.Combine(Application.persistentDataPath, "data", date);
        Directory.CreateDirectory(directoryPath); // Creates the directory if it doesn't exist
        logPath = Path.Combine(directoryPath, $"{date}_{time}.csv");
        logFile = new StreamWriter(logPath);
        logFile.WriteLine(
            "Current Time,DrumPosX, DrumPosY, DrumPosZ, DrumRotX,DrumRotY,DrumRotZ,SensPosX, SensPosY, SensPosZ, SensRotX,SensRotY,SensRotZ"
        );
        Debug.Log("Writing data to: " + logPath);
    }

    void Update()
    {
        // //get the transform of the drum
        // drumTransform = GameObject.Find("GratingDrum").transform;

        // // Check if 'values' array is populated before updating the position and rotation
        // if (positionToUpdate != null)
        // {
        //     transform.position = positionToUpdate;
        //     transform.rotation = rotationToUpdate;
        // }

        // string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        // float DrumPosX = drumTransform.position.x;
        // float DrumPosY = drumTransform.position.y;
        // float DrumPosZ = drumTransform.position.z;
        // float DrumRotX = drumTransform.rotation.x;
        // float DrumRotY = drumTransform.rotation.y;
        // float DrumRotZ = drumTransform.rotation.z;
        // float SensPosX = positionToUpdate[0];
        // float SensPosY = positionToUpdate[1];
        // float SensPosZ = positionToUpdate[2];
        // float SensRotX = rotationToUpdate[0];
        // float SensRotY = rotationToUpdate[1];
        // float SensRotZ = rotationToUpdate[2];

        // string line =
        //     $"{currentTime},{DrumPosX},{DrumPosY},{DrumPosZ},{DrumRotX},{DrumRotY},{DrumRotZ},{SensPosX},{SensPosY},{SensPosZ},{SensRotX},{SensRotY},{SensRotZ}";

        // // string line =
        // //     $"{currentTime},{xPos},{zPos},{yRot},{stripeStartTime},{angularSpeed},{sceneName}";
        // logFile.WriteLine(line);
        // Debug.Log(line);

        // // print the message received from the socket
        // Debug.Log(positionToUpdate);
        // Debug.Log(rotationToUpdate);
    }

    void OnDestroy()
    {
        subscriber.Dispose();
    }
}
