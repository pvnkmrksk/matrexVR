using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.IO.Compression;

// Manages all data loggers in the scene
public class MasterDataLogger : MonoBehaviour
{
    // Singleton instance
    public static MasterDataLogger Instance { get; private set; }

    // Path to the directory where the log files will be saved
    public string directoryPath { get; private set; }

    // List of all DataLogger instances in the scene
    private List<DataLogger> dataLoggers;

    // Create timestamp variable that can be publicly accessed but not changed with get methods only
    public string timestamp { get; private set; }

    void Awake()
    {
        /*          the Awake() method sets Instance to this only if Instance is null. 
        If Instance is not null, it remains the same. This ensures that only one instance 
        of MasterDataLogger exists in your game at any time.
         */
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }
    // d at the start of the scene
    void Start()
    {
        // Get the current timestamp
        timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

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
        string zipPath = Application.dataPath + $"/RunData/{timestamp}.zip";
        // Create the zip file from the directory
        ZipFile.CreateFromDirectory(directoryPath, zipPath);
    }

    void OnDestroy()
    {
        ZipDataFolder();
    }
}