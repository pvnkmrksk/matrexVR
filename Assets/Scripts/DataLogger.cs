using UnityEngine;
using System;
using System.IO;

public class DataLogger : MonoBehaviour
{
    private string logPath;
    private StreamWriter logFile;

    void Start()
    {
        InitLog();
    }

    void InitLog()
    {
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string time = DateTime.Now.ToString("HH-mm-ss");
        string rootDirectoryPath = Application.dataPath; // Root directory of the project or build
        string directoryPath = Path.Combine(rootDirectoryPath, "data", date);
        Directory.CreateDirectory(directoryPath); // Creates the directory if it doesn't exist
        logPath = Path.Combine(directoryPath, $"{date}_{time}.csv");
        logFile = new StreamWriter(logPath);
        logFile.WriteLine(
            "Current Time,DrumPosX,DrumPosY,DrumPosZ,DrumRotX,DrumRotY,DrumRotZ,SensPosX,SensPosY,SensPosZ,SensRotX,SensRotY,SensRotZ"
        );
        Debug.Log("Writing data to: " + logPath);
    }

    void Update()
    {
        // Collect the necessary information
        string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        GameObject gratingDrum = GameObject.Find("GratingDrum"); // Replace "GratingDrum" with the actual name of the drum object
        Vector3 drumPosition = gratingDrum.transform.position;
        Quaternion drumRotation = gratingDrum.transform.rotation;
        Vector3 sensPosition = FindObjectOfType<ZmqListener>().positionToUpdate;
        Quaternion sensRotation = FindObjectOfType<ZmqListener>().rotationToUpdate;

        // Log the data
        string line =
            $"{currentTime},{drumPosition.x},{drumPosition.y},{drumPosition.z},{drumRotation.eulerAngles.x},"
            + $"{drumRotation.eulerAngles.y},{drumRotation.eulerAngles.z},{sensPosition.x},{sensPosition.y},"
            + $"{sensPosition.z},{sensRotation.eulerAngles.x},{sensRotation.eulerAngles.y},{sensRotation.eulerAngles.z}";

        logFile.WriteLine(line);
        logFile.Flush();
    }

    void OnDestroy()
    {
        logFile.Close();
    }
}
