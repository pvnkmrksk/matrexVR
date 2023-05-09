using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PlayerDataLogger : MonoBehaviour
{
    public GenerateStripes stripeGenerator;
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
        logFile.WriteLine("Time,Position,Heading,AngularSpeed,StripeStartTime");
        Debug.Log("Writing data to: " + logPath);
    }

    void Update()
    {
        Vector3 position = transform.position;
        Vector3 forward = transform.forward;
        float angularSpeed = stripeGenerator.GetAngularSpeed(); // This method does not exist yet, you'll need to add it to your GenerateStripes script
        float stripeStartTime = stripeGenerator.GetStripeStartTime(); // This method does not exist yet, you'll need to add it to your GenerateStripes script

        string line = $"{Time.time},{position},{forward},{angularSpeed},{stripeStartTime}";
        logFile.WriteLine(line);
    }

    void OnDestroy()
    {
        // Make sure to close the file when the game ends
        logFile.Close();
    }
}
