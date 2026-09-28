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
    private StreamWriter runtimeTraceWriter;
    private readonly object runtimeTraceLock = new object();

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
            return;
        }

        timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        directoryPath = Application.dataPath + "/RunData/" + timestamp;
        Directory.CreateDirectory(directoryPath);

        Application.logMessageReceivedThreaded -= HandleLogMessage;
        Application.logMessageReceivedThreaded += HandleLogMessage;
    }
    // d at the start of the scene
    void Start()
    {
        dataLoggers = new List<DataLogger>();
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
        Application.logMessageReceivedThreaded -= HandleLogMessage;

        lock (runtimeTraceLock)
        {
            runtimeTraceWriter?.Flush();
            runtimeTraceWriter?.Dispose();
            runtimeTraceWriter = null;
        }

        if (Instance == this)
        {
            ZipDataFolder();
            Instance = null;
        }
    }

    private void HandleLogMessage(string condition, string stackTrace, LogType type)
    {
        if (string.IsNullOrEmpty(directoryPath))
        {
            return;
        }

        lock (runtimeTraceLock)
        {
            if (runtimeTraceWriter == null)
            {
                string tracePath = Path.Combine(directoryPath, "runtime_trace.log");
                runtimeTraceWriter = new StreamWriter(
                    new FileStream(tracePath, FileMode.Append, FileAccess.Write, FileShare.Read)
                );
            }

            runtimeTraceWriter.WriteLine(
                $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{type}] {condition}"
            );

            if (!string.IsNullOrEmpty(stackTrace))
            {
                runtimeTraceWriter.WriteLine(stackTrace);
            }

            runtimeTraceWriter.Flush();
        }
    }
}
