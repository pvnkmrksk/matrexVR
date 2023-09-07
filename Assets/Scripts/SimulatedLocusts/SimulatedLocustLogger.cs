using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Text;
using System.IO.Compression;

public class SimulatedLocustLogger : MonoBehaviour
{
    private string directoryPath;
    private string logPath;
    private StreamWriter logFile;

    void Start()
    {
        // Access directoryPath from MasterDataLogger
        MasterDataLogger masterDataLogger = FindObjectOfType<MasterDataLogger>();
        if (masterDataLogger != null)
        {
            directoryPath = masterDataLogger.directoryPath;
        }
        else
        {
            Debug.LogError("MasterDataLogger not found.");
            return;
        }

        // Initialize the log file
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string time = DateTime.Now.ToString("HH-mm-ss");
        logPath = Path.Combine(directoryPath, $"SimulatedLocustData_{date}_{time}.csv.gz");
        logFile = new StreamWriter(
            new GZipStream(File.Create(logPath), System.IO.Compression.CompressionLevel.Optimal)
        );

        // Write header
        logFile.WriteLine("Timestamp,Name,Layer,X,Y,Z");
    }

    void Update()
    {
        GameObject[] locusts = GameObject.FindGameObjectsWithTag("SimulatedLocust");
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        foreach (GameObject locust in locusts)
        {
            Vector3 position = locust.transform.position;
            string data = $"{timestamp},{locust.name},{LayerMask.LayerToName(locust.layer)},{position.x},{position.y},{position.z}";
            logFile.WriteLine(data);
        }
    }

    void OnDestroy()
    {
        logFile?.Dispose();
    }
}
