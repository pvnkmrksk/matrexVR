using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.IO.Compression;

// Manages all data loggers in the scene
public class MasterDataLogger : MonoBehaviour
{
    // Path to the directory where the log files will be saved
    public string directoryPath { get; private set; }

    // List of all DataLogger instances in the scene
    private List<DataLogger> dataLoggers;

    // Called at the start of the scene
    void Start()
    {
        // Logger.Log("MasterDataLogger.Start()");
        // Get the current timestamp
        string timestamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");

        // Set the directory path
        directoryPath = Application.dataPath + "/RunData/" + timestamp;

        // Create the directory
        Directory.CreateDirectory(directoryPath);

        // Find all DataLogger instances in the scene
        dataLoggers = new List<DataLogger>();
        foreach (var logger in FindObjectsOfType<DataLogger>())
        {
            if (logger.gameObject.scene == gameObject.scene)
            {
                dataLoggers.Add(logger);
            }
        }

        // Initialize all DataLogger instances
        foreach (var logger in dataLoggers)
        {
            logger.InitLog();
        }
    }

    // Zips the data folder
    public void ZipDataFolder()
    {
        // Set the path to the zip file
        string zipPath = Application.dataPath + "/RunData/" + System.DateTime.Now.ToString("yyyyMMddHHmmss") + ".zip";

        // Create the zip file from the directory
        ZipFile.CreateFromDirectory(directoryPath, zipPath);
    }

    void OnDestroy()
    {
        Logger.Log("MasterDataLogger.OnDestroy()");
        // ZipDataFolder();
    }
}