using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.IO.Compression;

// Manages all data loggers in the scene
[DefaultExecutionOrder(-10000)]
public class MasterDataLogger : MonoBehaviour
{
    // Singleton instance
    public static MasterDataLogger Instance { get; private set; }

    // Path to the directory where the log files will be saved
    public string directoryPath { get; private set; }
    public RunConfigArchive ConfigArchive { get; private set; }

    // List of all DataLogger instances in the scene
    private List<DataLogger> dataLoggers;
    private StreamWriter runtimeTraceWriter;
    private readonly object runtimeTraceLock = new object();

    // Create timestamp variable that can be publicly accessed but not changed with get methods only
    public string timestamp { get; private set; }

    void Awake()
    {
        string startingScene=gameObject.scene.name;
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

        string root=Path.Combine(Application.dataPath,"RunData");
        string baseTimestamp=System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        int collision=0;
        do
        {
            timestamp=baseTimestamp+(collision==0?"":"_"+collision.ToString("D3"));
            directoryPath=Path.Combine(root,timestamp);collision++;
        } while(Directory.Exists(directoryPath) || File.Exists(Path.Combine(root,timestamp+".zip")));
        Directory.CreateDirectory(directoryPath);

        Application.logMessageReceivedThreaded -= HandleLogMessage;
        Application.logMessageReceivedThreaded += HandleLogMessage;
        ConfigArchive=new RunConfigArchive(directoryPath,Application.streamingAssetsPath,timestamp,Application.unityVersion);
        var main=MainController.Instance!=null?MainController.Instance:FindFirstObjectByType<MainController>();
        try {ConfigArchive.CaptureStartup(startingScene,main!=null?main.SystemConfigurationFileName:"system_config.json");}
        catch(Exception error) {Debug.LogError("Cannot initialize run config archive: "+error.Message);}
    }
    // d at the start of the scene
    void Start()
    {
        dataLoggers = new List<DataLogger>();
    }

    public void BeginConfiguredSession(string systemReference, string sequenceReference)
    {
        if (MainController.Instance != null && MainController.Instance.SequenceRunning)
            throw new InvalidOperationException("Cannot rotate the data session during an experiment.");
        AsyncSwarmWriter.DrainAll();
        lock (runtimeTraceLock)
        {
            runtimeTraceWriter?.Flush(); runtimeTraceWriter?.Dispose(); runtimeTraceWriter = null;
            ZipDataFolder();
            string root = Path.Combine(Application.dataPath, "RunData");
            string baseTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss"); int collision = 0;
            do
            {
                timestamp = baseTimestamp + (collision == 0 ? "" : "_" + collision.ToString("D3")); collision++;
                directoryPath = Path.Combine(root, timestamp);
            } while (Directory.Exists(directoryPath) || File.Exists(Path.Combine(root, timestamp + ".zip")));
            Directory.CreateDirectory(directoryPath);
            ConfigArchive = new RunConfigArchive(directoryPath, Application.streamingAssetsPath, timestamp, Application.unityVersion);
            ConfigArchive.CaptureStartup("ControlScene", systemReference, sequenceReference);
            ConfigArchive.RequireComplete();
        }
    }


    // Zips the data folder
    public void ZipDataFolder()
    {
        // Finish gzip trailers before packaging the session, including when Play mode stops.
        AsyncSwarmWriter.DrainAll();
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
            try {ZipDataFolder();}
            catch(Exception error) {Debug.LogError("Could not zip run folder; original data and config snapshots remain at "+directoryPath+": "+error.Message);}
            finally {Instance = null;}
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
