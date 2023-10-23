using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Text;
using System.IO.Compression;

public class SimulatedLocustLogger : MonoBehaviour
{
    public LayerMask locustLayerMask;  // Public LayerMask variable
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
            Logger.Log("MasterDataLogger not found.", 1);
            return;
        }

        // Get a reference to the LocustSpawner script
        LocustSpawner locustSpawner = GetComponent<LocustSpawner>();
        if (locustSpawner == null)
        {
            Logger.Log("LocustSpawner script not found on this GameObject.", 1);
            return;
        }

        // Initialize the log file
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string time = DateTime.Now.ToString("HH-mm-ss");
        // Correctly extract layer names from the LayerMask
        string layerNames = "";
        int layerMaskValue = locustLayerMask.value;
        for (int i = 0; i < 32; i++)  // Unity supports up to 32 layers
        {
            if ((layerMaskValue & (1 << i)) != 0)
            {
                layerNames += LayerMask.LayerToName(i) + "_";
            }
        }
        layerNames = layerNames.TrimEnd('_');  // Remove trailing underscore

        // Include metadata in the file name
        logPath = Path.Combine(directoryPath, $"{date}_{time}_SimulatedLocustData_{layerNames}_{locustSpawner.numberOfLocusts}_{locustSpawner.spawnAreaSize}_{locustSpawner.mu}_{locustSpawner.kappa}_{locustSpawner.locustSpeed}.csv.gz");

        logFile = new StreamWriter(
            new GZipStream(File.Create(logPath), System.IO.Compression.CompressionLevel.Optimal)
        );

        // Write header with parameters from LocustSpawner
        logFile.WriteLine($"Timestamp,Name,Layer,X,Y,Z,NumberOfLocusts:{locustSpawner.numberOfLocusts},SpawnAreaSize:{locustSpawner.spawnAreaSize},Mu:{locustSpawner.mu},Kappa:{locustSpawner.kappa},LocustSpeed:{locustSpawner.locustSpeed}");

    }

    void Update()
    {
        GameObject[] locusts = GameObject.FindGameObjectsWithTag("SimulatedLocust");
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        foreach (GameObject locust in locusts)
        {
            if (locustLayerMask == (locustLayerMask | (1 << locust.layer)))  // Check if the locust's layer is in the mask
            {
                Vector3 position = locust.transform.position;
                string data = $"{timestamp},{locust.name},{LayerMask.LayerToName(locust.layer)},{position.x},{position.y},{position.z}";
                logFile.WriteLine(data);
            }
        }
    }

    void OnDestroy()
    {
        logFile?.Dispose();
    }
}
