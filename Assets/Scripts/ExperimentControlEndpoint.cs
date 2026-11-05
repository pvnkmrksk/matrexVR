using System;
using System.IO;
using System.Linq;
using ConfigurationUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in local file RPC. Requests execute on Unity's main thread, never on an acquisition worker.</summary>
public sealed partial class ExperimentControlEndpoint : MonoBehaviour
{
    public string ControlDirectory { get; private set; }
    private float nextPoll;
    private FileStream ownerLock;
    private readonly string controlInstance=Guid.NewGuid().ToString("N");
    public string ControlInstance => controlInstance;
    private static bool installed;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() => installed = false;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (installed) return;
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--control-dir");
        if (index < 0) return;
        if (index + 1 >= args.Length) { Debug.LogError("--control-dir requires an absolute path."); return; }
        try
        {
            var host = new GameObject("Optional local experiment control");
            host.AddComponent<ExperimentControlEndpoint>().ConfigureDirectory(args[index + 1]);
            DontDestroyOnLoad(host); installed = true;
        }
        catch (Exception error) { Debug.LogError("Local control unavailable: " + error.Message); }
    }
    public void ConfigureDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new ArgumentException("Control directory must be an absolute path.");
        string full = Path.GetFullPath(path);
        // The command mailbox must never live inside an immutable recording.
        for (string current = full; current != null; current = Path.GetDirectoryName(current))
            if (File.Exists(Path.Combine(current, "config_manifest.json")) || File.Exists(Path.Combine(current, "recording_manifest.json")) ||
                File.Exists(Path.Combine(current, "recording_summary.json")))
                throw new ArgumentException("Choose a separate command directory outside saved recordings.");
        if (Directory.Exists(full) && ReplaySessionArchive.CsvFiles(full).Length > 0)
            throw new ArgumentException("Choose a command directory without recording CSV files.");
        if(ControlDirectory!=null)throw new InvalidOperationException("This endpoint is already configured.");
        Directory.CreateDirectory(full);
        ownerLock=new FileStream(Path.Combine(full,"owner.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        try
        {
            ownerLock.Lock(0,1);
            var descriptor=new JObject{["protocolVersion"]=2,["instance"]=controlInstance,["processId"]=System.Diagnostics.Process.GetCurrentProcess().Id,
                ["methods"]=new JArray(ControlMethods),["configRoot"]=RuntimePaths.ConfigRoot,["runDataRoot"]=RuntimePaths.RunDataRoot};
            string temporary=Path.Combine(full,"endpoint."+controlInstance+".tmp"),destination=Path.Combine(full,"endpoint.json");
            File.WriteAllText(temporary,descriptor.ToString());
            if(File.Exists(destination))File.Delete(destination);File.Move(temporary,destination);ControlDirectory=full;
        }
        catch{ownerLock.Dispose();ownerLock=null;throw;}
    }
    private void Update()
    {
        if (ControlDirectory == null || Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + .05f; Poll();
    }
    public void Poll()
    {
        if (ControlDirectory == null) return;
        foreach (string file in Directory.GetFiles(ControlDirectory, "*.request.json").OrderBy(path => path, StringComparer.Ordinal).Take(4))
        {
            string id = Path.GetFileName(file).Replace(".request.json", "");
            if (!Guid.TryParseExact(id, "N", out _)) continue;
            string response = Path.Combine(ControlDirectory, id + ".response.json");
            if (File.Exists(response)) { File.Delete(file); continue; }
            JObject result;
            try
            {
                if (new FileInfo(file).Length > 1048576) throw new ArgumentException("Request exceeds 1 MiB.");
                var request = JObject.Parse(File.ReadAllText(file));
                if((string)request["instance"]!=controlInstance)throw new ArgumentException("Application instance changed; the request was not executed.");
                if ((string)request["id"] != id) throw new ArgumentException("Request id does not match its file.");
                if (!DateTimeOffset.TryParse((string)request["expiresUtc"], out var expiry) || expiry < DateTimeOffset.UtcNow)
                    throw new ArgumentException("Request expired; it was not executed.");
                result = new JObject { ["id"] = id, ["instance"]=controlInstance, ["ok"] = true, ["result"] = Execute((string)request["method"], request["arguments"] as JObject ?? new JObject()) };
            }
            catch (Exception error) { result = new JObject { ["id"] = id, ["instance"]=controlInstance, ["ok"] = false, ["error"] = error.GetBaseException().Message, ["errorCode"]=error is ArgumentException?"invalid_argument":error is IOException?"io_error":"invalid_state" }; }
            string temporary = response + ".tmp";
            File.WriteAllText(temporary, result.ToString(Formatting.None)); File.Move(temporary, response); File.Delete(file);
        }
    }
    private void OnDestroy()
    {
        try
        {
            if(ControlDirectory!=null)
            {
                string path=Path.Combine(ControlDirectory,"endpoint.json");
                if(File.Exists(path)&&(string)JObject.Parse(File.ReadAllText(path))["instance"]==controlInstance)File.Delete(path);
            }
        }
        catch(IOException){}catch(UnauthorizedAccessException){}
        finally{ownerLock?.Dispose();ownerLock=null;}
    }
    private static string RequiredPath(JObject args, string name)
    {
        string path = (string)args[name];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new ArgumentException(name + " must be an absolute path.");
        return Path.GetFullPath(path);
    }
    private static void RequireIdle()
    {
        var main = MainController.Instance;
        if (main?.SequenceRunning == true || main?.SequencePreparing == true || main?.SequenceStopping == true || ConfigWorkbench.Instance?.IsPreparing == true)
            throw new InvalidOperationException("Return to idle setup before changing experiment configuration.");
    }
    public JObject Execute(string method, JObject args)
    {
        var main = MainController.Instance; var gui = ConfigWorkbench.Instance;
        var replay = FindFirstObjectByType<ReplayController>();
        switch (method)
        {
            case "status": return new JObject {
                ["protocolVersion"] = 2, ["scene"] = SceneManager.GetActiveScene().name,
                ["sequenceRunning"] = main?.SequenceRunning ?? false, ["sequencePreparing"] = main?.SequencePreparing ?? false,
                ["sequenceStopping"] = main?.SequenceStopping ?? false, ["recordingError"] = main?.RecordingTransitionError,
                ["recordingWarnings"] = main == null ? new JArray() : JArray.FromObject(main.RecordingWarnings),
                ["sequenceTitle"] = main?.SequenceTitle, ["phase"] = MainController.RecordedExperimentPhase, ["sequenceError"] = main?.SequenceError,
                ["setupOpen"] = gui != null, ["unsavedDraft"] = gui?.ControlHasUnsavedDraft ?? false, ["setupModal"] = gui?.ControlHasModal ?? false,
                ["replayReady"] = replay?.Ready ?? false, ["replayLoading"]=replay?.Loading??false,["replayError"]=replay?.LoadError, ["replayTime"] = replay?.ReplayTime ?? 0, ["replayDuration"] = replay?.Duration ?? 0,
                ["replayPlaying"] = replay?.IsPlaying ?? false, ["diagnostics"]=DetailedStatus() };
            case "validate":
                RequireIdle();
                var workspace = new ConfigWorkspace(RequiredPath(args, "systemFile"), RequiredPath(args, "sequenceFile"), ConfigSchema.Load());
                var errors = gui!=null?gui.ValidateSetupForStart(workspace):workspace.Validate(); return new JObject { ["valid"] = errors.Count == 0, ["errors"] = new JArray(errors) };
            case "start_current":
                RequireIdle(); if (gui == null) throw new InvalidOperationException("Open setup before starting the current draft.");
                gui.StartCurrentFromControl(); return new JObject { ["accepted"] = true, ["note"] = "Start uses the GUI workflow; poll status for asynchronous preparation or errors." };
            case "start_files":
                RequireIdle(); if (gui == null) throw new InvalidOperationException("Open setup before selecting saved configuration files.");
                string system = RequiredPath(args, "systemFile"), sequence = RequiredPath(args, "sequenceFile");
                var validation = Execute("validate", args);
                if (!(bool)validation["valid"]) throw new ArgumentException("Setup validation failed: " + validation["errors"]);
                gui.StartFilesFromControl(system, sequence); return new JObject { ["accepted"] = true, ["note"] = "Start uses the GUI workflow; poll status for asynchronous preparation or errors." };
            case "stop":
                if (main == null) throw new InvalidOperationException("No experiment controller is available.");
                bool returnToSetup = SceneManager.GetActiveScene().name != "ControlScene" || main.SequencePreparing || gui?.IsPreparing == true;
                main.StopSequence("local-control-stop");
                if (returnToSetup) SceneManager.LoadScene("ControlScene");
                return new JObject { ["accepted"] = true, ["recordersFinishing"] = main.SequenceStopping };
            case "replay_open":
                if (gui?.ControlHasUnsavedDraft == true || gui?.ControlHasModal == true) throw new InvalidOperationException("Save/discard the GUI draft and close its dialog before opening replay.");
                string folder = RequiredPath(args, "directory");
                if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
                ReplayController.OpenSession(folder); return new JObject { ["accepted"] = true };
            case "replay_play":
                if (replay == null || !replay.Ready || replay.Loading) throw new InvalidOperationException("A recording must finish loading before playback control.");
                if (!(args["playing"] is JValue flag) || flag.Type != JTokenType.Boolean) throw new ArgumentException("playing must be a boolean.");
                replay.SetPlaying((bool)flag); return new JObject { ["playing"] = replay.IsPlaying };
            case "replay_seek":
                if (replay == null || !replay.Ready || replay.Loading) throw new InvalidOperationException("A recording must finish loading before seeking.");
                double seconds = (double?)args["seconds"] ?? double.NaN;
                if (double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentException("seconds must be finite.");
                replay.SeekSeconds(seconds); return new JObject { ["seconds"] = replay.ReplayTime };
            default: return ExecuteExtended(method,args);
        }
    }
}
