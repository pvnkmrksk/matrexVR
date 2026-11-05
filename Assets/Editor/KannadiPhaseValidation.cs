#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Actual sixteen-animal MainController phase transitions and recorded alias evidence.</summary>
[InitializeOnLoad]
public static class KannadiPhaseValidation
{
    private const string Key = "KannadiPhaseValidation.Active";
    private static readonly List<string> checks = new List<string>(), errors = new List<string>();
    private static int stage, experimentLoads, sceneHandle;
    private static double deadline;
    private static string recording;
    private static int[] instances, mirrorInstances;
    private static Vector3[] continued;
    private static void Check(bool value, string text) { if (!value) throw new Exception(text); checks.Add(text); }
    static KannadiPhaseValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            stage = experimentLoads = 0; deadline = EditorApplication.timeSinceStartup + 90;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.logMessageReceived += OnLog; EditorApplication.update += Tick;
        };
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Use a marked disposable copy.");
        string root = Application.streamingAssetsPath;
        var configs = new JArray();
        for (int i = 1; i <= 16; i++) configs.Add(new JObject {
            ["vrId"] = "VR" + i, ["closedLoopMode"] = "Hejjetrac", ["zmqAddress"] = "127.0.0.1", ["zmqPort"] = 59000 + i,
            ["sphereDiameter"] = 2.6, ["displayOrder"] = "", ["targetDisplay"] = 0,
            ["tracker"] = new JObject { ["saveVideo"] = false, ["recordingControl"] = "Manual" }
        });
        File.WriteAllText(Path.Combine(root, "system_config.json"), new JObject {
            ["configs"] = configs, ["targetDisplay"] = 0, ["targetFrameRate"] = 60,
            ["telemetry"] = new JObject { ["enabled"] = false }, ["overheadCamera"] = new JObject { ["enabled"] = false },
            ["statusOverlay"] = new JObject { ["visible"] = false }
        }.ToString());
        JObject Phase(string mode, bool reset) => new JObject {
            ["enabled"] = true, ["durationSeconds"] = 1, ["populationMode"] = mode,
            ["agentCount"] = 3, ["agentSpeed"] = 0, ["numberOfRings"] = 0, ["periodicBoundary"] = false,
            ["closedLoopPosition"] = 0, ["closedLoopOrientation"] = 0, ["autopilotEnabled"] = false,
            ["resetPositionOnStart"] = reset, ["resetRotationOnStart"] = reset,
            ["initialPosition"] = new JObject { ["x"] = 1, ["y"] = 0, ["z"] = 2 }
        };
        File.WriteAllText(Path.Combine(root, "__kannadi-phases.json"), new JObject {
            ["title"] = "Sixteen animal phase validation", ["preStimulus"] = Phase("Swarm", true),
            ["stimulus"] = Phase("Mirror", false), ["postStimulus"] = Phase("Mirror", false)
        }.ToString());
        var reset = Phase("Mirror", true); reset.Remove("enabled"); reset.Remove("durationSeconds");
        reset["initialPosition"] = new JObject { ["x"] = 3, ["y"] = 0, ["z"] = 4 };
        File.WriteAllText(Path.Combine(root, "__kannadi-reset.json"), reset.ToString());
        File.WriteAllText(Path.Combine(root, "sequenceConfig.json"), new JObject {
            ["title"] = "Alias and continuity validation", ["autoStart"] = false, ["loop"] = false,
            ["randomise"] = false, ["randomSeedMode"] = "Fixed", ["randomSeed"] = 345,
            ["sequences"] = new JArray(
                new JObject { ["sceneName"] = "Swarm", ["duration"] = 3, ["reloadScene"] = true, ["parameters"] = new JObject { ["configFile"] = "__kannadi-phases.json" } },
                new JObject { ["sceneName"] = "Kannadi", ["duration"] = 1, ["reloadScene"] = false, ["parameters"] = new JObject { ["configFile"] = "__kannadi-reset.json" } })
        }.ToString());
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var host = new GameObject("Phase validation MainController"); host.AddComponent<MainController>();
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    private static void OnLog(string text, string trace, LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !trace.Contains("UnityEditor.Search.SearchDatabase")) errors.Add(text);
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Kannadi") experimentLoads++;
        // sceneLoaded is before Start, including registry-created VR5 through VR16.
        foreach (var root in scene.GetRootGameObjects())
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour is ZmqListener || behaviour is Keyboard || behaviour.GetType().Name.IndexOf("Terrain", StringComparison.OrdinalIgnoreCase) >= 0)
                    behaviour.enabled = false;
    }
    private static Kannadi[] Rigs() => Object.FindObjectsByType<Kannadi>(FindObjectsSortMode.None).Where(r => r.gameObject.activeInHierarchy).OrderBy(r => r.VRIndex).ToArray();
    private static void CheckContinuous(Kannadi[] rigs, string label)
    {
        Check(SceneManager.GetActiveScene().handle == sceneHandle && experimentLoads == 1, label + " retains the same loaded scene");
        Check(rigs.Select(r => r.GetInstanceID()).SequenceEqual(instances), label + " retains all sixteen rig instances");
        for (int i = 0; i < rigs.Length; i++) Check(Vector3.Distance(rigs[i].transform.position, continued[i]) < .001f, label + " retains VR" + (i + 1) + " pose with reset false");
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Kannadi phase stage " + stage);
            var main = MainController.Instance; if (main == null || Time.frameCount < 3) return;
            if (main.SequenceError != null) throw new Exception("MainController sequence error in stage " + stage + ": " + main.SequenceError);
            if (ZmqListener.ActiveWorkerCount != 0) throw new Exception("A hardware tracking worker started in stage " + stage);
            if (stage == 0)
            {
                typeof(MainController).GetField("<ReturnToSetupAfterSequence>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, true);
                main.StartSequence(); recording = MasterDataLogger.Instance.directoryPath; stage = 1; return;
            }
            var rigs = Rigs();
            if (stage == 1 && main.SequenceRunning && main.ExperimentPhase == "preStimulus" && rigs.Length == 16)
            {
                Check(SceneManager.GetActiveScene().name == "Kannadi", "Historical Swarm row loads the unified Kannadi scene");
                Check(rigs.All(r => r.IsSwarm && r.GetComponent<LocustSpawner>().Spawned.Count == 3), "All sixteen rigs enter a three-agent pre-stimulus swarm");
                sceneHandle = SceneManager.GetActiveScene().handle; instances = rigs.Select(r => r.GetInstanceID()).ToArray();
                continued = rigs.Select((r, i) => new Vector3(5 + i, 0, 9 + i)).ToArray();
                for (int i = 0; i < rigs.Length; i++) rigs[i].transform.position = continued[i];
                stage = 2; return;
            }
            if (stage == 2 && main.ExperimentPhase == "stimulus" && main.currentStep == 0)
            {
                CheckContinuous(rigs, "Stimulus");
                Check(rigs.All(r => !r.IsSwarm && r.Clones.Length == 1), "All sixteen rigs switch to one mirrored individual");
                mirrorInstances = rigs.Select(r => r.Clones[0].GetInstanceID()).ToArray(); stage = 3; return;
            }
            if (stage == 3 && main.ExperimentPhase == "postStimulus")
            {
                CheckContinuous(rigs, "Post-stimulus");
                Check(rigs.Select(r => r.Clones[0].GetInstanceID()).SequenceEqual(mirrorInstances), "Identical post-stimulus population retains animated instances");
                stage = 4; return;
            }
            if (stage == 4 && main.currentStep == 1)
            {
                Check(SceneManager.GetActiveScene().handle == sceneHandle && rigs.Select(r => r.GetInstanceID()).SequenceEqual(instances), "Explicit reset row keeps the same scene and tracked rigs");
                Check(rigs.All(r => Vector3.Distance(r.transform.position, new Vector3(3, 0, 4)) < .001f), "Reset true restores every configured initial pose");
                stage = 5; return;
            }
            if (stage == 5 && !main.SequenceRunning && !MasterDataLogger.Instance.IsFinalizing)
            {
                Check(MasterDataLogger.Instance.FinalizationError == null, "Saved run finishes without a finalization error");
                var archive = new ReplaySessionArchive(recording); archive.LoadRigSamples();
                Check(archive.Rigs.Count == 16 && Enumerable.Range(1, 16).All(i => archive.Rigs.ContainsKey("VR" + i)), "Saved run contains all sixteen canonical rig pose streams");
                foreach (var pair in archive.Rigs)
                    foreach (string phase in new[] { "preStimulus", "stimulus", "postStimulus" })
                        Check(pair.Value.Any(sample => sample.phase == phase && sample.sequenceStep == 0 && sample.scene == "Kannadi"), pair.Key + " records actual Kannadi poses during " + phase);
                bool alias = false;
                foreach (string path in Directory.GetFiles(recording, "*.csv"))
                {
                    using (var reader = ReplaySessionArchive.OpenCsv(path))
                    {
                        var header = ReplaySessionArchive.Csv(ReplaySessionArchive.ReadCsvRecord(reader) ?? "");
                        int actual = Array.IndexOf(header, "Scene"), original = Array.IndexOf(header, "sourceSequenceScene");
                        if (actual < 0 || original < 0) continue;
                        string line;
                        while ((line = ReplaySessionArchive.ReadCsvRecord(reader)) != null)
                        { var row = ReplaySessionArchive.Csv(line); if (row.Length > Math.Max(actual, original) && row[actual] == "Kannadi" && row[original] == "Swarm") { alias = true; break; } }
                    }
                }
                Check(alias, "Canonical scene logging also preserves the original Swarm sequence label");
                Check(main.SequenceError == null, "MainController completed without a sequence error");
                Check(ZmqListener.ActiveWorkerCount == 0, "No hardware tracking workers started");
                Check(errors.Count == 0, "No Unity errors during acquisition and phase transitions: " + string.Join("; ", errors));
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error.ToString()); }
    }
    private static void Finish(string failure)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; SceneManager.sceneLoaded -= OnSceneLoaded; Application.logMessageReceived -= OnLog;
        File.WriteAllText(Path.Combine(Application.dataPath, "../kannadi-phase-validation.json"), JsonConvert.SerializeObject(new { checks = checks.Count, passed = checks, failure, errors, recording, unity = Application.unityVersion }, Formatting.Indented));
        if (failure != null) Debug.LogError(failure);
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
#endif
