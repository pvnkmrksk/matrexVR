using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Text;
using System.IO.Compression;

/// <summary>
/// Logs positions and orientations of Kannadi clones with VR index information.
/// </summary>
public class KannadiLogger : MonoBehaviour
{
    private string directoryPath;
    private string logPath;
    private StreamWriter logFile;
    private Kannadi kannadi;
    private int vrIndex = 0; // Will be extracted from GameObject name (VR1, VR2, etc.)

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
            Debugger.Log("MasterDataLogger not found.", 1);
            return;
        }

        // Get Kannadi component
        kannadi = GetComponent<Kannadi>();
        if (kannadi == null)
        {
            Debugger.Log("Kannadi script not found on this GameObject.", 1);
            return;
        }

        // Extract VR index from GameObject name (e.g., "VR1 Kannadi" -> 1)
        string objName = gameObject.name;
        if (objName.Contains("VR1"))
            vrIndex = 1;
        else if (objName.Contains("VR2"))
            vrIndex = 2;
        else if (objName.Contains("VR3"))
            vrIndex = 3;
        else if (objName.Contains("VR4"))
            vrIndex = 4;

        // Initialize the log file
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string time = DateTime.Now.ToString("HH-mm-ss");
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        logPath = Path.Combine(
            directoryPath,
            $"{date}_{time}_{sceneName}_Kannadi_VR{vrIndex}_Clones.csv.gz"
        );

        logFile = new StreamWriter(
            new GZipStream(File.Create(logPath), System.IO.Compression.CompressionLevel.Optimal)
        );

        // Write header
        logFile.WriteLine(
            "Timestamp,VRIndex,CloneIndex,CloneName,PositionX,PositionY,PositionZ,RotationX,RotationY,RotationZ,NumberOfRings,Spacing"
        );
    }

    void Update()
    {
        if (kannadi == null || logFile == null)
            return;

        GameObject[] clones = kannadi.Clones;
        if (clones == null)
            return;

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        for (int i = 0; i < clones.Length; i++)
        {
            if (clones[i] != null)
            {
                Vector3 position = clones[i].transform.position;
                Vector3 rotation = clones[i].transform.rotation.eulerAngles;

                string data = $"{timestamp},{vrIndex},{i},{clones[i].name}," +
                             $"{position.x},{position.y},{position.z}," +
                             $"{rotation.x},{rotation.y},{rotation.z}," +
                             $"{kannadi.numberOfRings},{kannadi.spacing}";

                logFile.WriteLine(data);
            }
        }
    }

    void OnDestroy()
    {
        if (logFile != null)
        {
            logFile.Flush();
            logFile.Dispose();
        }
    }
}


