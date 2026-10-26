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
[DefaultExecutionOrder(200)]
public class KannadiLogger : MonoBehaviour
{
    private string directoryPath;
    private string logPath;
    private StreamWriter logFile;
    private bool closedForArchive;
    private Kannadi kannadi;
    private int vrIndex = 0; // Will be extracted from GameObject name (VR1, VR2, etc.)

    void Start()
    {
        if (closedForArchive) return;
        // Access directoryPath from MasterDataLogger
        MasterDataLogger masterDataLogger = FindObjectOfType<MasterDataLogger>();
        if (masterDataLogger != null)
        {
            if (!masterDataLogger.AcceptingWrites) { closedForArchive = true; return; }
            directoryPath = masterDataLogger.directoryPath;
        }
        else
        {
            Debugger.Log("MasterDataLogger not found.", 1);
            return;
        }

        if (string.IsNullOrEmpty(directoryPath)) return;

        // Get Kannadi component
        kannadi = GetComponent<Kannadi>();
        if (kannadi == null)
        {
            Debugger.Log("Kannadi script not found on this GameObject.", 1);
            return;
        }

        // Extract VR index from GameObject name (e.g., "VR1 Kannadi" -> 1)
        vrIndex = RuntimeRigRegistry.ParseIndex(gameObject.name);

        // Initialize the log file
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string time = DateTime.Now.ToString("HH-mm-ss-fffffff");
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
            "Timestamp,VRIndex,CloneIndex,CloneName,PositionX,PositionY,PositionZ,RotationX,RotationY,RotationZ,NumberOfRings,hexRadius,skyboxId,skyboxSampleUtc,experimentPhase"
        );
    }

    void LateUpdate()
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

                string data = FormattableString.Invariant($"{timestamp},{vrIndex},{i},{clones[i].name},{position.x},{position.y},{position.z},{rotation.x},{rotation.y},{rotation.z},{kannadi.numberOfRings},{kannadi.hexRadius},{NightSkyController.CurrentId},{NightSkyController.CurrentSampleUtc},{MainController.RecordedExperimentPhase}");

                logFile.WriteLine(data);
            }
        }
    }

    void OnDestroy() => CloseForArchive();

    public void CloseForArchive()
    {
        closedForArchive = true;
        if (logFile != null)
        {
            logFile.Flush();
            logFile.Dispose();
            logFile = null;
        }
    }
}
