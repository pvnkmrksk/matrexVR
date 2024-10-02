using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

public class BandLogger : MonoBehaviour
{
    public LayerMask targetLayerMask; // Renamed from locustLayerMask
    private string directoryPath;
    private string logPath;
    private StreamWriter logFile;
    private BandSpawner bandSpawner;

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

        // Get a reference to the BandSpawner script
        bandSpawner = GetComponent<BandSpawner>();
        if (bandSpawner == null)
        {
            Debugger.Log("BandSpawner script not found on this GameObject.", 1);
            return;
        }

        // Initialize the log file
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string time = DateTime.Now.ToString("HH-mm-ss");
        string layerNames = GetLayerNames(targetLayerMask);

        string prefabName = bandSpawner.instancePrefab.name;
        string bandName = gameObject.name; // Get the name of the GameObject this script is attached to

        logPath = Path.Combine(
            directoryPath,
            $"{date}_{time}_SimulatedData_{prefabName}_{bandName}_{layerNames}_{bandSpawner.numberOfInstances}_{bandSpawner.spawnWidth}_{bandSpawner.spawnLength}_{bandSpawner.mu}_{bandSpawner.kappa}_{bandSpawner.speed}.csv.gz"
        );

        logFile = new StreamWriter(
            new GZipStream(File.Create(logPath), System.IO.Compression.CompressionLevel.Optimal)
        );

        // Write header
        logFile.WriteLine(
            $"Timestamp,Name,Layer,X,Y,Z,Rotation,Speed,VisibilityPhase," +
            $"PrefabName:{prefabName}," +
            $"BandName:{bandName}," + // Add the band name to the header
            $"NumberOfInstances:{bandSpawner.numberOfInstances}," +
            $"SpawnWidth:{bandSpawner.spawnWidth}," +
            $"SpawnLength:{bandSpawner.spawnLength}," +
            $"GridType:{bandSpawner.gridType}," +
            $"Mu:{bandSpawner.mu}," +
            $"Kappa:{bandSpawner.kappa}," +
            $"Speed:{bandSpawner.speed}," +
            $"VisibleOffDuration:{bandSpawner.visibleOffDuration}," +
            $"VisibleOnDuration:{bandSpawner.visibleOnDuration}," +
            $"BoundaryWidth:{bandSpawner.boundaryWidth}," +
            $"BoundaryLength:{bandSpawner.boundaryLength}"
        );

        LogSpawnData();
    }

    string GetLayerNames(LayerMask layerMask)
    {
        string layerNames = "";
        for (int i = 0; i < 32; i++)
        {
            if ((layerMask.value & (1 << i)) != 0)
            {
                layerNames += LayerMask.LayerToName(i) + "_";
            }
        }
        return layerNames.TrimEnd('_');
    }

    void LogSpawnData()
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        foreach (Transform child in bandSpawner.transform)
        {
            GameObject instance = child.gameObject;
            if (targetLayerMask == (targetLayerMask | (1 << instance.layer)))
            {
                Vector3 position = instance.transform.position;
                float rotation = instance.transform.eulerAngles.y;
                
                DirectionalMovement movement = instance.GetComponent<DirectionalMovement>();
                float speed = movement ? movement.GetSpeed() : 0f;

                VisibilityScript visibility = instance.GetComponent<VisibilityScript>();
                float visibilityPhase = visibility ? visibility.phaseOffset : 0f;

                string data = $"{timestamp},{instance.name},{LayerMask.LayerToName(instance.layer)}," +
                              $"{position.x},{position.y},{position.z},{rotation},{speed},{visibilityPhase}";
                logFile.WriteLine(data);
            }
        }
    }

    void Update()
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        foreach (Transform child in bandSpawner.transform)
        {
            GameObject instance = child.gameObject;
            if (targetLayerMask == (targetLayerMask | (1 << instance.layer)))
            {
                Vector3 position = instance.transform.position;
                string data = $"{timestamp},{instance.name},{LayerMask.LayerToName(instance.layer)}," +
                              $"{position.x},{position.y},{position.z}";
                logFile.WriteLine(data);
            }
        }
    }

    void OnDestroy()
    {
        logFile?.Dispose();
    }
}
