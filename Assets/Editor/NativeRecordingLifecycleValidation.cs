#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Real MainController → owned native GUI → drained, run-bound media. Disposable projects only.</summary>
[InitializeOnLoad]
public static class NativeRecordingLifecycleValidation
{
    const string Key = "NativeRecordingLifecycleValidation.Active";
    static string output;
    static readonly List<string> checks = new List<string>();
    static readonly List<JObject> runs = new List<JObject>();
    static readonly List<string> errors = new List<string>();
    static readonly Dictionary<string, NativeRecorderClient> clients = new Dictionary<string, NativeRecorderClient>();
    static SystemConfig[] rigs;
    static NativeRecordingLifecycleValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            { output = SessionState.GetString(Key + ".Output", ""); _ = ValidateAsync(); }
        };
    }
    static string Arg(string name) { var args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1]; throw new ArgumentException(name + " is required"); }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new InvalidOperationException("Use an explicitly marked disposable project.");
        output = Path.GetFullPath(Arg("-native-output")); Directory.CreateDirectory(output);
        PlayerSettings.companyName = "MATREX Validation";
        PlayerSettings.productName = "NativeRecordingValidation-" + Guid.NewGuid().ToString("N");
        SessionState.SetString(Key + ".Output", output); SessionState.SetBool(Key, true);
        var catalogRoot = Path.GetFullPath(Arg("-native-companions"));
        var entries = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(catalogRoot, "catalog.json")))["entries"];
        var inputs = Path.Combine(output, "Inputs"); Directory.CreateDirectory(inputs);
        var template = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Templates/system_config.template.json")));
        var configurations = new JArray(); int index = 0;
        foreach (string backend in new[] { "rekkefly", "hejjetrac" })
        {
            var entry = entries.OfType<JObject>().Single(e => (string)e["backend"] == backend && (string)e["platform"] == "macos-arm64");
            if ((bool?)entry["supportsRecordingControl"] != true) throw new InvalidDataException("Use the capable local adapter payload.");
            string package = Path.Combine(catalogRoot, (string)entry["packageRoot"]), id = "VR" + (++index), config = Path.Combine(inputs, id + (backend == "rekkefly" ? ".toml" : ".txt"));
            int port; var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            string text = File.ReadAllText(Path.Combine(package, (string)entry["demoConfig"]));
            if (backend == "rekkefly")
            {
                text = System.Text.RegularExpressions.Regex.Replace(text, "(?m)^autosave = .*", "autosave = false");
                text = System.Text.RegularExpressions.Regex.Replace(text, "(?m)^instance = .*", "instance = \"" + id + "-native-validation\"");
                int start = text.IndexOf("[output]", StringComparison.Ordinal), end = text.IndexOf("[recording]", StringComparison.Ordinal);
                text = text.Substring(0, start) + System.Text.RegularExpressions.Regex.Replace(text.Substring(start, end - start), "(?m)^enabled = true", "enabled = false") + text.Substring(end);
                text = System.Text.RegularExpressions.Regex.Replace(text, "(?m)^zmq_enabled = .*", "zmq_enabled = true");
                text = System.Text.RegularExpressions.Regex.Replace(text, "(?m)^zmq_bind = .*", "zmq_bind = \"tcp://127.0.0.1:" + port + "\"");
            }
            else
            {
                text = Field(text, "src_fn", Path.Combine(package, (string)entry["sampleVideo"]));
                text = Field(text, "src_fps", "10"); text = Field(text, "do_display", "y");
                text = Field(text, "save_raw", "n"); text = Field(text, "save_debug", "n");
                text = Field(text, "sock_port", "-1"); text = Field(text, "zmq_host", "127.0.0.1"); text = Field(text, "zmq_port", port.ToString());
                text = Field(text, "vid_codec", "mjpg"); text = Field(text, "output_fn", Path.Combine(inputs, "native-validation"));
            }
            File.WriteAllText(config, text);
            var rig = (JObject)template["configs"][0].DeepClone();
            rig["vrId"] = id; rig["closedLoopMode"] = backend == "rekkefly" ? "Rekkefly" : "Hejjetrac";
            rig["zmqPort"] = port; rig["zmqAddress"] = "127.0.0.1"; rig["targetDisplay"] = 0; rig["displayOrder"] = "F";
            rig["ledPanelWidth"] = 32; rig["ledPanelHeight"] = 32; rig["startCol"] = index - 1; rig["startRow"] = 0;
            rig["tracker"] = new JObject { ["executablePath"] = Path.Combine(package, (string)entry["executable"]), ["configFile"] = config, ["saveVideo"] = true, ["recordingControl"] = "Experiment" };
            configurations.Add(rig);
        }
        template["configs"] = configurations; template["telemetry"] = new JObject { ["enabled"] = false };
        template["overheadCamera"] = new JObject { ["enabled"] = false }; template["statusOverlay"] = new JObject { ["visible"] = false };
        var root = Application.streamingAssetsPath;
        File.WriteAllText(Path.Combine(root, "__native-system.json"), template.ToString());
        // Idle startup has no automatic video request or acquisition side effects.
        var idle = (JObject)template.DeepClone(); foreach (var rig in idle["configs"].OfType<JObject>()) rig["tracker"]["saveVideo"] = false;
        File.WriteAllText(Path.Combine(root, "system_config.json"), idle.ToString());
        File.WriteAllText(Path.Combine(root, "__native-choice.json"), new JObject { ["closedLoopPosition"] = false, ["closedLoopOrientation"] = true, ["objects"] = new JArray(), ["autoTrimSettings"] = new JObject { ["flightCheckEnabled"] = false } }.ToString());
        var sequence = new JObject { ["title"] = "Native recording lifecycle validation", ["autoStart"] = false, ["loop"] = false, ["returnToSetup"] = false,
            ["sequences"] = new JArray(new JObject { ["sceneName"] = "Choice", ["duration"] = 60, ["reloadScene"] = true, ["parameters"] = new JObject { ["configFile"] = "__native-choice.json" } }) };
        File.WriteAllText(Path.Combine(root, "__native-sequence.json"), sequence.ToString());
        File.WriteAllText(Path.Combine(root, "sequenceConfig.json"), sequence.ToString());
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity"); EditorApplication.isPlaying = true;
    }
    static string Field(string text, string key, string value)
    {
        string pattern = "(?m)^(\\s*" + key + "\\s*:\\s*)[^\\r\\n]*";
        return System.Text.RegularExpressions.Regex.IsMatch(text, pattern) ? System.Text.RegularExpressions.Regex.Replace(text, pattern, m => m.Groups[1].Value + value) : text.TrimEnd() + "\n" + key + " : " + value + "\n";
    }
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks.Add(label); }
    static async Task Until(Func<bool> condition, string label, double seconds = 30)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!condition()) { if (timer.Elapsed.TotalSeconds > seconds) throw new TimeoutException(label); await Task.Delay(5); }
    }
    static async Task PauseInputs() { foreach (var client in clients.Values) await client.CallAsync("pause"); }
    static void CaptureLog(string message,string trace,LogType type)
    {
        if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
        if(trace.Contains("UnityEditor.Search.SearchDatabase"))return;
        if(message.StartsWith("Tracker recording stop:",StringComparison.Ordinal)&&message.Contains("outside the matching VR stop request"))return;
        errors.Add(message+"\n"+trace);
    }
    static async Task ValidateAsync()
    {
        string failure = null;
        Application.logMessageReceived+=CaptureLog;
        try
        {
            Check(Application.persistentDataPath.Contains("NativeRecordingValidation-"), "Native ownership receipts use a fresh validation-only application profile");
            await Until(() => MainController.Instance != null && MasterDataLogger.Instance != null, "ControlScene startup");
            var main = MainController.Instance; var logger = MasterDataLogger.Instance;
            await logger.FinalizeSessionAsync("validation-idle");
            rigs = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "__native-system.json")))["configs"].ToObject<SystemConfig[]>();
            foreach (var rig in rigs) TrackerProcessManager.Instance.Launch(rig);
            foreach (var rig in rigs)
            {
                string root = Path.Combine(Application.persistentDataPath, "Companions/processes");
                var receipt = Directory.EnumerateFiles(root, "*.json").Select(path => JObject.Parse(File.ReadAllText(path))).Single(value => (string)value["vrId"] == rig.vrId);
                var client = new NativeRecorderClient((string)receipt["controlDirectory"], (string)receipt["controlInstance"], rig.closedLoopMode == ClosedLoopMode.Rekkefly ? "rekkefly" : "hejjetrac", (int)receipt["processId"]);
                clients[rig.vrId] = client; await client.WaitReadyAsync(); await client.CallAsync("pause");
                Check(!NativeRecorderClient.Recording(NativeRecorderClient.State(await client.CallAsync("status"))), rig.vrId + " initialization opens the actual native GUI without recording");
            }
            for (int take = 0; take < 2; take++) await RunCase("normal-" + take, false, false);
            await RunCase("cancelled-preparation", true, false);
            await RunCase("manual-pause", false, true);
            Check(typeof(TrackerProcessManager).GetField("recordingRuns", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) is System.Collections.IDictionary retained && retained.Count == 0, "Settled takes release run/CTS structures between repetitions");
        }
        catch (Exception error) { failure = error.ToString(); }
        finally
        {
            try { if (MainController.Instance != null && (MainController.Instance.SequenceRunning || MainController.Instance.SequencePreparing || MainController.Instance.SequenceStopping)) await MainController.Instance.StopSequenceAsync("cancelled-validation-cleanup"); }
            catch (Exception error) { errors.Add("VR cleanup: " + error); }
            if (rigs != null) foreach (var rig in rigs) try { TrackerProcessManager.Instance.Stop(rig.vrId); } catch (Exception error) { errors.Add("Owned native cleanup: " + error); }
            Application.logMessageReceived-=CaptureLog;
            SessionState.SetBool(Key, false);
            File.WriteAllText(Path.Combine(output, "results.json"), new JObject { ["passed"] = failure == null && errors.Count == 0, ["checks"] = new JArray(checks), ["runs"] = new JArray(runs), ["failure"] = failure, ["cleanupErrors"] = new JArray(errors), ["persistentDataPath"] = Application.persistentDataPath, ["scope"] = "Actual Editor MainController start/stop, two owned native GUI engines on generated/sample input, native receipt proof and run-local archive; no physical camera/LED claim." }.ToString(Formatting.Indented));
            EditorApplication.Exit(failure == null && errors.Count == 0 ? 0 : 1);
        }
    }
    static async Task RunCase(string label, bool cancelPreparation, bool manualPause)
    {
        await PauseInputs();
        var main = MainController.Instance;
        main.PrepareConfiguredRun("__native-system.json", "__native-sequence.json");
        string directory = MasterDataLogger.Instance.directoryPath;
        main.StartSequence();
        Check(main.SequencePreparing && !main.SequenceRunning, label + ": stimulus waits for native first-frame acknowledgement");
        if (cancelPreparation)
        {
            await Until(() => (string)TrackerProcessManager.RecordingState("VR1")["state"] == "recording" || !main.SequencePreparing, "first native take preparing");
            Check(main.SequencePreparing && !main.SequenceRunning, "Cancellation exercises an acknowledged first take while the second still prepares");
        }
        else
        {
            await Until(() => main.SequenceRunning || main.SequenceError != null, label + " first-frame gate");
            Check(main.SequenceError == null, label + ": start succeeded: " + main.SequenceError);
            foreach (var rig in rigs) Check((bool?)TrackerProcessManager.RecordingState(rig.vrId)["acknowledged"] == true, label + ": " + rig.vrId + " first raw frame acknowledged");
            await Task.Delay(900);
            if (manualPause)
            {
                await clients["VR1"].CallAsync("pause");
                await Until(() => (string)TrackerProcessManager.RecordingState("VR1")["state"] == "override", "manual native pause detection");
                Check((bool?)TrackerProcessManager.RecordingState("VR1")["acknowledged"] == false, "Native manual pause latches override without automatic restart");
            }
        }
        var stop = main.StopSequenceAsync(cancelPreparation ? "cancelled-preparation" : "stopped");
        Check(!MasterDataLogger.Instance.AcceptingWrites, label + ": Stop seals VR writers before waiting for native drain");
        await stop;
        var manifest = JObject.Parse(File.ReadAllText(Path.Combine(directory, "recording_manifest.json")));
        Check(!main.SequencePreparing && !main.SequenceStopping && main.NativeRecordingStopSettled, label + ": lifecycle gate settles before another Start");
        Check((string)manifest["lifecycle"]?["status"] == (manualPause ? "error" : "finalized"), label + ": finalization status is honest: " + manifest["lifecycle"]);
        Check(File.Exists(directory + ".zip") == !manualPause, label + ": only successful/drained artifacts produce a complete ZIP");
        if (cancelPreparation)
        {
            Check((string)manifest["lifecycle"]?["sequenceOutcome"] == "cancelled", "Canceled preparation is never labeled a completed experiment");
            Check(File.Exists(Path.Combine(directory, "native_unpresented_takes.json")), "Zero-pose cancellation retains its closed native artifacts without fabricated alignment");
        }
        else
        {
            var media = JObject.Parse(File.ReadAllText(Path.Combine(directory, "media_manifest.json")));
            Check(media["clips"].Count() == 2, label + ": both exact native takes are archived into this run");
            foreach (var rig in rigs)
            {
                var state = TrackerProcessManager.RecordingState(rig.vrId);
                Check((string)state["runId"] == (string)JObject.Parse(File.ReadAllText(Path.Combine(directory, "native_recording_run.json")))["runId"], label + ": captured immutable GUID binds " + rig.vrId + " to this run");
                Check((string)state["native"]?["state"] == "complete", label + ": native physical closure retained for " + rig.vrId);
                Check(TrackerProcessManager.IsRunning(rig.vrId), label + ": tracking remains independently running after recorder Stop");
            }
        }
        runs.Add(new JObject { ["case"] = label, ["directory"] = directory, ["manifest"] = manifest, ["recordingError"] = main.RecordingTransitionError });
        await PauseInputs();
    }
}
#endif
