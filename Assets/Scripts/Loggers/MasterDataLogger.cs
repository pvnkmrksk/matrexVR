using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;

// Manages all data loggers in the scene
[DefaultExecutionOrder(-10000)]
public class MasterDataLogger : MonoBehaviour
{
    // Singleton instance
    public static MasterDataLogger Instance { get; private set; }

    // Path to the directory where the log files will be saved
    public string directoryPath { get; private set; }
    public RunConfigArchive ConfigArchive { get; private set; }

    private StreamWriter runtimeTraceWriter;
    private readonly object runtimeTraceLock = new object();
    private RecordingSessionFinalizer recording;
    private CompanionMediaArchiveSession mediaSession;
    private bool traceSealed, quitWhenFinished, quitAllowed;
    private string traceFailure;
    public Task<JObject> FinalizationTask => recording?.FinalizationTask;
    public bool IsFinalizing => FinalizationTask != null && !FinalizationTask.IsCompleted;
    public string FinalizationError => recording?.Error;

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

        string root=RuntimePaths.RunDataRoot;
        string baseTimestamp=System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        int collision=0;
        do
        {
            timestamp=baseTimestamp+(collision==0?"":"_"+collision.ToString("D3"));
            directoryPath=Path.Combine(root,timestamp);collision++;
        } while(Directory.Exists(directoryPath) || File.Exists(Path.Combine(root,timestamp+".zip")));
        Directory.CreateDirectory(directoryPath);
        BeginRecordingEvidence();

        Application.logMessageReceivedThreaded -= HandleLogMessage;
        Application.logMessageReceivedThreaded += HandleLogMessage;
        Application.wantsToQuit -= WantsToQuit;
        Application.wantsToQuit += WantsToQuit;
        ConfigArchive=new RunConfigArchive(directoryPath,RuntimePaths.ConfigRoot,timestamp,Application.unityVersion);
        var main=MainController.Instance!=null?MainController.Instance:FindFirstObjectByType<MainController>();
        try {
            string systemReference=main!=null?main.SystemConfigurationFileName:"system_config.json";
            ConfigArchive.CaptureStartup(startingScene,systemReference);
            CaptureNativeCompanions(systemReference,startingScene);
        }
        catch(Exception error) {Debug.LogError("Cannot initialize run config archive: "+error.Message);}
    }
    public void BeginConfiguredSession(string systemReference, string sequenceReference)
    {
        if (MainController.Instance != null && MainController.Instance.SequenceRunning)
            throw new InvalidOperationException("Cannot rotate the data session during an experiment.");
        if (recording != null && (FinalizationTask == null || !FinalizationTask.IsCompleted))
            throw new InvalidOperationException("The previous recording is still finishing. Wait for its finalization before starting another run.");
        if (!string.IsNullOrEmpty(directoryPath)) AsyncSwarmWriter.CompletionsForDirectory(directoryPath, true);
        lock (runtimeTraceLock)
        {
            runtimeTraceWriter?.Flush(); runtimeTraceWriter?.Dispose(); runtimeTraceWriter = null;
            string root = RuntimePaths.RunDataRoot;
            string baseTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss"); int collision = 0;
            do
            {
                timestamp = baseTimestamp + (collision == 0 ? "" : "_" + collision.ToString("D3")); collision++;
                directoryPath = Path.Combine(root, timestamp);
            } while (Directory.Exists(directoryPath) || File.Exists(Path.Combine(root, timestamp + ".zip")));
            Directory.CreateDirectory(directoryPath);
            traceSealed = false;
            traceFailure = null;
            BeginRecordingEvidence();
            ConfigArchive = new RunConfigArchive(directoryPath, RuntimePaths.ConfigRoot, timestamp, Application.unityVersion);
            ConfigArchive.CaptureStartup("ControlScene", systemReference, sequenceReference);
            CaptureNativeCompanions(systemReference,"ControlScene");
            ConfigArchive.RequireComplete();
        }
    }

    private void BeginRecordingEvidence()
    {
        recording = new RecordingSessionFinalizer(directoryPath, Path.Combine(RuntimePaths.RunDataRoot, timestamp + ".zip"), PlayerBuildIdentity.Capture());
        mediaSession = CompanionMediaArchive.BeginSession(directoryPath, timestamp);
    }

    private void CaptureNativeCompanions(string systemReference,string scene)
    {
        try
        {
            // Read the selected archived document: Main's runtime rigs are loaded
            // after BeginConfiguredSession and may still describe the previous run.
            var system=JObject.Parse(ConfigArchive.ReadText(systemReference,"system",scene));
            mediaSession?.RetainRigs((system["configs"] as JArray ?? new JArray()).OfType<JObject>().Select(rig => (string)rig["vrId"]).Where(id => !string.IsNullOrWhiteSpace(id)));
            var launches=new List<JObject>();
            foreach(var rig in (system["configs"] as JArray??new JArray()).OfType<JObject>())
            {
                string id=(string)rig["vrId"];if(string.IsNullOrWhiteSpace(id))continue;
                var evidence=TrackerProcessManager.RecordingProvenance(id);
                string backend=TrackerBackend.TryParse(rig["closedLoopMode"]?.ToString()??"FicTrac",out var mode)?TrackerBackend.DisplayName(mode):"unknown";
                int port=(int?)rig["zmqPort"]??9872;
                string address=(string)rig["zmqAddress"]??"localhost";
                bool local=address=="127.0.0.1"||address=="::1"||address=="[::1]"||string.Equals(address,"localhost",StringComparison.OrdinalIgnoreCase);
                bool? requestedDemo=(bool?)system["desktopQuickStart"]?["demo"];
                evidence["configuredBackend"]=backend;evidence["configuredPort"]=port;evidence["configuredAddress"]=address;
                evidence["configuredDemoRequested"]=requestedDemo;
                evidence["matchesConfiguredRig"]=local&&(string)evidence["backend"]==backend&&(int?)evidence["port"]==port&&
                    (!requestedDemo.HasValue||(bool?)evidence["demoRequested"]==requestedDemo.Value);
                launches.Add(evidence);
            }
            ConfigArchive.CaptureCompanions(launches,scene);
        }
        catch(Exception error) {Debug.LogWarning("Native tracker provenance is unavailable for this run: "+error.Message);}
    }


    /// <summary>Call on Unity's main thread. Closing scene writers is immediate; drain, analysis, media and ZIP run off-thread.</summary>
    public Task<JObject> FinalizeSessionAsync(string endReason)
    {
        if (recording == null) return Task.FromResult<JObject>(null);
        if (FinalizationTask != null) return FinalizationTask;
        var errors = CloseWriters();
        // Sealing the selected archive prevents idle setup/replay reads from appending to a finalized experiment.
        try { ConfigArchive?.RequireComplete(); } catch (Exception error) { errors.Add(error.Message); }
        ConfigArchive = null;
        var session = recording; var media = mediaSession; string directory = directoryPath;
        return session.FinalizeAsync(endReason,
            token => { AsyncSwarmWriter.DrainAll(); return Task.CompletedTask; },
            async token => (await media.FinalizeAsync(token).ConfigureAwait(false)).ToJson(),
            () => WriterEvidence(directory), media.ObservePose, errors.ToArray());
    }

    private List<string> CloseWriters()
    {
        var errors = new List<string>();
        lock (runtimeTraceLock)
        {
            if (traceFailure != null) errors.Add("Runtime trace: " + traceFailure);
            traceSealed = true;
            try { runtimeTraceWriter?.Flush(); runtimeTraceWriter?.Dispose(); }
            catch (Exception error) { errors.Add("Runtime trace: " + error.Message); }
            finally { runtimeTraceWriter = null; }
        }
        void Close(Action close) { try { close(); } catch (Exception error) { errors.Add(error.Message); } }
        foreach (var logger in FindObjectsByType<DataLogger>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Close(logger.CloseForArchive);
        foreach (var logger in FindObjectsByType<BandLogger>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Close(logger.CloseForArchive);
        foreach (var logger in FindObjectsByType<KannadiLogger>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Close(logger.CloseForArchive);
        foreach (var logger in FindObjectsByType<SwarmLogger>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Close(logger.CloseForArchive);
        return errors;
    }

    private static JArray WriterEvidence(string directory)
    {
        var result = new JArray();
        foreach (var item in AsyncSwarmWriter.CompletionsForDirectory(directory))
            result.Add(new JObject { ["relativePath"] = Path.GetFileName(item.Path), ["writtenRows"] = item.WrittenRows,
                ["droppedFrames"] = item.DroppedFrames, ["failure"] = item.Failure });
        return result;
    }

    // Compatibility entry point. UI callers should await FinalizeSessionAsync instead.
    public void ZipDataFolder()
    {
        FinalizeSessionAsync("session-rotation").GetAwaiter().GetResult();
    }

    private bool WantsToQuit()
    {
        if (quitAllowed || recording == null) return true;
        quitWhenFinished = true;
        MainController.Instance?.StopSequence("application-quit");
        FinalizeSessionAsync("application-quit");
        return FinalizationTask.IsCompleted;
    }

    private void Update()
    {
        if (!quitWhenFinished || IsFinalizing) return;
        quitAllowed = true; Application.Quit();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Application.logMessageReceivedThreaded -= HandleLogMessage;
        Application.wantsToQuit -= WantsToQuit;
        try
        {
            if (recording != null && (FinalizationTask == null || !FinalizationTask.IsCompleted))
            {
                var errors = CloseWriters(); AsyncSwarmWriter.DrainAll();
                recording.MarkShutdown(errors.Count == 0, mediaSession.PendingReceipt("The player stopped before camera media finalization completed.").ToJson(),
                    errors.Count > 0 ? string.Join("; ", errors) : null, WriterEvidence(directoryPath));
            }
        }
        catch (Exception error) { Debug.LogError("Recording shutdown could not finish; original files remain at " + directoryPath + ": " + error.Message); }
        finally { Instance = null; }
    }

    private void HandleLogMessage(string condition, string stackTrace, LogType type)
    {
        if (string.IsNullOrEmpty(directoryPath))
        {
            return;
        }

        lock (runtimeTraceLock)
        {
            if (traceSealed) return;
            try
            {
                if (runtimeTraceWriter == null)
                {
                    string tracePath = Path.Combine(directoryPath, "runtime_trace.log");
                    runtimeTraceWriter = new StreamWriter(new FileStream(tracePath, FileMode.Append, FileAccess.Write, FileShare.Read));
                }
                runtimeTraceWriter.WriteLine(
                    $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{type}] {condition}"
                );
                if (!string.IsNullOrEmpty(stackTrace)) runtimeTraceWriter.WriteLine(stackTrace);
                runtimeTraceWriter.Flush();
            }
            catch (Exception error)
            {
                // Logging a logging failure recursively can crash the threaded callback.
                traceFailure = error.Message; traceSealed = true;
                try { runtimeTraceWriter?.Dispose(); } catch { }
                runtimeTraceWriter = null;
            }
        }
    }
}
