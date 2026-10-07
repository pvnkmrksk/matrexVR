#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PhaseAdapterValidation
{
    private const string Key = "PhaseAdapterValidation.Active";
    private static readonly string[] scenes = { "Swarm", "Kannadi", "Optomotor", "Choice_desync", "Choice" };
    private static readonly string[] templates = { "swarm", "kannadi", "optomotor", "dynamic-choice", "choice-band" };
    private static readonly List<string> errors = new List<string>();
    private static int checks, index, stage, nextFrame, handle;
    private static double deadline;
    private static ExperimentPhases runner;
    private static ClosedLoop[] rigs;
    private static int[] ids;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static PhaseAdapterValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 100; stage = 0; index = 0; nextFrame = Time.frameCount + 3;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
            SceneManager.sceneLoaded += DisableInputs;
        };
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (message == "No backup data file found." || stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        errors.Add(message);
    }
    private static void DisableInputs(Scene scene, LoadSceneMode mode)
    {
        foreach (var input in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) input.enabled = false;
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable copy only.");
        var system = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Examples/System/fictrac-four-cameras.example.json")));
        system["telemetry"]["enabled"] = false; system["overheadCamera"]["enabled"] = false;
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "system_config.json"), system.ToString());
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json"), "{\"autoStart\":false,\"loop\":false,\"sequences\":[]}");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    private static JObject Payload(string template)
    {
        var p = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Templates/" + template + ".template.json")));
        p["enabled"] = true; p["durationSeconds"] = .6;
        if (template == "swarm" || template == "kannadi")
        {
            p["closedLoopPosition"] = 0; p["closedLoopOrientation"] = 0; p["autopilotEnabled"] = false;
            p["nightSky"] = null; p["headingReference"] = null; p["resetPositionOnStart"] = false; p["resetRotationOnStart"] = false;
            if (template == "swarm") p["numberOfLocusts"] = 2;
            else p["numberOfRings"] = 1;
            foreach (JObject vr in p["vrConfigs"] ?? new JArray()) { vr["closedLoopPosition"] = 0; vr["closedLoopOrientation"] = 0; }
        }
        if (template == "choice-band")
        {
            p["resetPositionOnStart"] = false; p["resetRotationOnStart"] = false;
            p["closedLoopPosition"] = false; p["closedLoopOrientation"] = false;
        }
        if (template == "optomotor")
        {
            p["resetPositionOnStart"] = false; p["resetRotationOnStart"] = false;
            foreach (var stimulus in p["stimuli"]) { stimulus["duration"] = 100; stimulus["closedLoopPosition"] = false; stimulus["closedLoopOrientation"] = false; }
        }
        if (template == "dynamic-choice")
        {
            p["adaptiveDecision"] = null; p["intertrial"] = null; p["repetitions"] = 1;
            var step = (JObject)p["steps"][0].DeepClone();
            step["trigger"] = JObject.FromObject(new { type = "time", seconds = 100 });
            step["resetPositionOnStart"] = false; step["resetRotationOnStart"] = false;
            step["closedLoopPosition"] = false; step["closedLoopOrientation"] = false;
            p["steps"] = new JArray(step);
        }
        return p;
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Adapter " + index + " stage " + stage);
            if (Time.frameCount < nextFrame) return;
            nextFrame = Time.frameCount + 1;
            if (stage == 0) { SceneManager.LoadScene("ControlScene"); stage = 1; nextFrame += 3; return; }
            if (stage == 1) { SceneManager.LoadScene(scenes[index]); stage = 2; nextFrame += 3; return; }
            if (stage == 2)
            {
                rigs = Object.FindObjectsByType<ClosedLoop>(FindObjectsSortMode.None).OrderBy(r => r.name).ToArray();
                ids = rigs.Select(r => r.GetInstanceID()).ToArray(); handle = SceneManager.GetActiveScene().handle;
                Check(rigs.Length == 4, scenes[index] + " loads four rigs");
                for (int i = 0; i < rigs.Length; i++) rigs[i].transform.SetPositionAndRotation(new Vector3(i,1,i), Quaternion.Euler(0, 20 + i * 30, 0));
                if (index == 4)
                {
                    // Exercise the optional compact writer without adding a fake VR to
                    // DynamicSequenceController, which discovers players through DataLogger.
                    var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    probe.name = "Color logging probe";
                    var drift = probe.AddComponent<ColorDrift>(); drift.enabled = false;
                    new GameObject("Color CSV validation").AddComponent<ColorDriftLogger>();
                    foreach (var logger in Object.FindObjectsByType<ColorDriftLogger>(FindObjectsSortMode.None))
                        typeof(ColorDriftLogger).GetField("colorDrifts", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(logger, new List<ColorDrift> { drift });
                }
                var pre = Payload(templates[index]);
                pre["headingReference"] = JObject.FromObject(new { enabled = true, windowSeconds = .4, startOffsetSeconds = .1 });
                var stimulus = Payload(templates[index]);
                if (index < 2) stimulus["useHeadingReference"] = true;
                if (index == 3) foreach (var obj in stimulus["steps"][0]["objects"]) obj["useHeadingReference"] = true;
                var post = Payload(templates[index]);
                string file = "adapter-validation.json";
                File.WriteAllText(Path.Combine(Application.streamingAssetsPath, file), new JObject { ["preStimulus"] = pre, ["stimulus"] = stimulus, ["postStimulus"] = post }.ToString());
                var controller = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<InSceneSequence.IInSceneSequencer>().First();
                var main = MainController.Instance;
                main.enabled = false; // The fixture advances the phases directly, not an outer sequence.
                runner = main.gameObject.AddComponent<ExperimentPhases>();
                typeof(MainController).GetField("phaseRunner", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, runner);
                typeof(MainController).GetField("sequenceStarted", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, true);
                Check(runner.TryBegin(new Dictionary<string,object> { [index == 3 ? "design" : "configFile"] = file }, controller.AdvanceStep, e => errors.Add(e.ToString())), scenes[index] + " accepts nested payload");
                Check(runner.CurrentPhase == "preStimulus", scenes[index] + " enters pre-stimulus");
                stage = 3; return;
            }
            if (stage == 3)
            {
                if (runner.CurrentPhase == "preStimulus") return;
                Check(runner.CurrentPhase == "stimulus", scenes[index] + " enters stimulus");
                Check(ids.SequenceEqual(rigs.Select(r => r.GetInstanceID())) && handle == SceneManager.GetActiveScene().handle, scenes[index] + " retains rigs and scene");
                for (int i = 0; i < rigs.Length; i++)
                {
                    Check(Vector3.Distance(rigs[i].transform.position, new Vector3(i,1,i)) < .001f && Mathf.Abs(Mathf.DeltaAngle(rigs[i].transform.eulerAngles.y,20+i*30)) < .02f, scenes[index] + " preserves both pose components " + i);
                    Check(rigs[i].GetComponent<StimulusHeadingReference>().Result != null, scenes[index] + " retains computed reference " + i);
                }
                if (index == 3) Check(GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name.StartsWith("Rig_VR") && t.name.EndsWith("_Objects")) == 4, "Dynamic phase replaces old per-rig containers");
                stage = 4; return;
            }
            if (stage == 4)
            {
                if (runner.CurrentPhase == "stimulus") return;
                Check(runner.CurrentPhase == "postStimulus", scenes[index] + " enters post-stimulus");
                stage = 5; return;
            }
            if (stage == 5)
            {
                if (!runner.Finished) return;
                Check(errors.Count == 0, scenes[index] + " no runtime errors: " + string.Join("; ", errors));
                runner.Cancel(); Object.Destroy(runner); index++;
                if (index == scenes.Length) { SceneManager.LoadScene("ControlScene"); stage = 6; nextFrame += 3; }
                else stage = 1;
                return;
            }
            if (stage == 6) { ValidateRecordedRows(); Check(errors.Count == 0, "Clean shutdown"); Finish(null); }
        }
        catch (Exception error) { Finish(error.ToString()); }
    }
    private static void ValidateRecordedRows()
    {
        var phasesByKind = new Dictionary<string, HashSet<string>>();
        string directory = MasterDataLogger.Instance.directoryPath;
        foreach (string file in Directory.GetFiles(directory, "*.csv*").Where(f => f.EndsWith(".csv") || f.EndsWith(".csv.gz")))
        {
            string text;
            if (file.EndsWith(".gz"))
            {
                using (var stream = new StreamReader(new GZipStream(File.OpenRead(file), CompressionMode.Decompress))) text = stream.ReadToEnd();
            }
            else text = File.ReadAllText(file);
            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Check(lines.Length > 0, "Recorded file contains a header");
            // Swarm and band retain historical Key:Value metadata after the data columns.
            string[] header = lines[0].Split(',').TakeWhile(h => !h.Contains(":")).ToArray();
            int phaseIndex = Array.IndexOf(header, "experimentPhase");
            Check(phaseIndex >= 0, "Phase column exists in " + Path.GetFileName(file));
            string kind = header.Contains("CloneIndex") ? "Kannadi" : header.Contains("VisibilityPhase") ? "Band" :
                header.Contains("StimulusIndex") ? "Optomotor" : header.Contains("CylinderName") ? "ColorDrift" :
                header.Contains("CurrentTrial") ? "Rig" : "Swarm";
            if (!phasesByKind.ContainsKey(kind)) phasesByKind[kind] = new HashSet<string>();
            foreach (string line in lines.Skip(1))
            {
                string[] cells = line.Split(',');
                Check(cells.Length == header.Length, "Column alignment in " + Path.GetFileName(file));
                Check(new[] { "preStimulus", "stimulus", "postStimulus", "idle", "assessment", "error" }.Contains(cells[phaseIndex]), "Recognized phase on every recorded row");
                phasesByKind[kind].Add(cells[phaseIndex]);
            }
        }
        foreach (string kind in new[] { "Rig", "Swarm", "Kannadi", "Optomotor", "Band", "ColorDrift" })
            Check(phasesByKind.ContainsKey(kind) && new[] { "preStimulus", "stimulus", "postStimulus" }.All(phasesByKind[kind].Contains), "All three phases survive disk buffering in " + kind);
        Check(phasesByKind.ContainsKey("ColorDrift"), "Compact color-drift header contains the phase column");
        MainController.Instance.StopSequence();
        Check(MainController.RecordedExperimentPhase == "idle", "Stopped experiments are labeled idle");
    }
    private static void Finish(string failure)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog; SceneManager.sceneLoaded -= DisableInputs;
        SessionState.SetBool(Key,false);
        File.WriteAllText(Path.Combine(Application.dataPath,"../phase-adapter-validation.json"),JsonConvert.SerializeObject(new {checks,failure,errors,unity=Application.unityVersion},Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
#endif
