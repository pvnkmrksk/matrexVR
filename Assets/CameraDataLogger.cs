using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraDataLogger : MonoBehaviour
{
    public float angularSpeed = 0.0f; // Set this value in the Inspector
    public float stripeStartTime = 0.0f; // Set this value in the Inspector
    private string logPath;
    private StreamWriter logFile;

    void Start()
    {
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string time = DateTime.Now.ToString("HH-mm-ss");
        string directoryPath = Path.Combine(Application.persistentDataPath, "data", date);
        Directory.CreateDirectory(directoryPath); // Creates the directory if it doesn't exist
        logPath = Path.Combine(directoryPath, $"{date}_{time}.csv");
        logFile = new StreamWriter(logPath);
        logFile.WriteLine("Current Time,X Position,Z Position,Y Rotation,Stripe Start Time,Angular Speed,Scene Name");
        Debug.Log("Writing data to: " + logPath);
    }

    void Update()
    {
        string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        float xPos = transform.position.x;
        float zPos = transform.position.z;
        float yRot = transform.eulerAngles.y;
        string sceneName = SceneManager.GetActiveScene().name;

        string line = $"{currentTime},{xPos},{zPos},{yRot},{stripeStartTime},{angularSpeed},{sceneName}";
        logFile.WriteLine(line);
    }

    void OnDestroy()
    {
        // Make sure to close the file
    {
        // Make sure to close the file when the game ends
        logFile.Close();
    }
}
}