using System;
using System.Globalization;
using System.Text;
using System.IO;
using System.IO.Compression;
using UnityEngine;

[DefaultExecutionOrder(1200)]
public class BandLogger : MonoBehaviour
{
    public LayerMask targetLayerMask; // Renamed from locustLayerMask
    private string directoryPath;
    private string logPath;
    private StreamWriter logFile;
    private bool closedForArchive;
    private BandSpawner bandSpawner;

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

        // Construct the base filename without the counter
        string baseFileName = $"{date}_{time}_SimulatedData_{prefabName}_{bandName}";
        string filenameSuffix = $"{layerNames}_{bandSpawner.numberOfInstances}_{bandSpawner.spawnLengthX}_{bandSpawner.spawnLengthZ}_{bandSpawner.mu}_{bandSpawner.kappa}_{bandSpawner.speed}.csv.gz";

        // Ensure unique file name
        string uniqueFileName = GetUniqueFileName(directoryPath, baseFileName, filenameSuffix);

        logPath = Path.Combine(directoryPath, uniqueFileName);

        logFile = new StreamWriter(
            new GZipStream(File.Create(logPath), System.IO.Compression.CompressionLevel.Optimal)
        );

        // Write header
        logFile.WriteLine(FormattableString.Invariant($"Timestamp,Name,Layer,X,Y,Z,RotationX,RotationY,RotationZ,Speed,VisibilityPhase,experimentPhase,Visible,UnityFrame,SpawnLengthX:{bandSpawner.spawnLengthX},SpawnLengthZ:{bandSpawner.spawnLengthZ},GridType:{bandSpawner.gridType},Mu:{bandSpawner.mu},Kappa:{bandSpawner.kappa},Speed:{bandSpawner.speed},VisibleOffDuration:{bandSpawner.visibleOffDuration},VisibleOnDuration:{bandSpawner.visibleOnDuration},BoundaryLengthX:{bandSpawner.boundaryLengthX},BoundaryLengthZ:{bandSpawner.boundaryLengthZ},RotationAngle:{bandSpawner.rotationAngle},VisibilityPhaseClock:local-timer"));

        LogAllInstances();
    }

    void LateUpdate()
    {
        LogAllInstances();
    }

    void LogAllInstances()
    {
        if (bandSpawner == null || logFile == null) return;
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fffffff",CultureInfo.InvariantCulture);
        // Unlocked agents are spawned without a parent, so transform children omit them.
        foreach (GameObject instance in bandSpawner.Instances)
        {
            if (instance == null || !instance.activeInHierarchy) continue;
            if (targetLayerMask == (targetLayerMask | (1 << instance.layer)))
            {
                LogInstanceData(timestamp, instance);
            }
        }
    }

    void LogInstanceData(string timestamp, GameObject instance)
    {
        Vector3 position = instance.transform.position;
        Vector3 rotation = instance.transform.eulerAngles;
        
        DirectionalMovement movement = instance.GetComponent<DirectionalMovement>();
        float speed = movement ? movement.GetSpeed() : 0f;

        VisibilityScript visibility = instance.GetComponent<VisibilityScript>();
        float visibilityPhase = visibility ? visibility.GetCurrentPhase() : 0f;

        var data=new StringBuilder(192);
        object[] values={timestamp,instance.name,RuntimeRigRegistry.LayerLabel(instance.layer),position.x,position.y,position.z,rotation.x,rotation.y,rotation.z,speed,visibilityPhase,MainController.RecordedExperimentPhase,visibility==null||visibility.IsVisible,Time.frameCount};
        for(int i=0;i<values.Length;i++){if(i>0)data.Append(',');RecordedCsv.Append(data,values[i]);}
        logFile.WriteLine(data.ToString());
    }

    string GetLayerNames(LayerMask layerMask)
    {
        string layerNames = "";
        for (int i = 0; i < 32; i++)
        {
            if ((layerMask.value & (1 << i)) != 0)
            {
                layerNames += RuntimeRigRegistry.LayerLabel(i) + "_";
            }
        }
        return layerNames.TrimEnd('_');
    }

    private string GetUniqueFileName(string directory, string baseFileName, string suffix)
    {
        int counter = 0;
        string fileName;
        do
        {
            fileName = $"{baseFileName}_{counter}_{suffix}";
            counter++;
        } while (File.Exists(Path.Combine(directory, fileName)));

        return fileName;
    }

    void OnDestroy() => CloseForArchive();

    public void CloseForArchive()
    {
        closedForArchive = true;
        logFile?.Dispose();
        logFile = null;
    }
}
