using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConfigurationUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real MainController runs with configured LED/desktop rigs, all sixteen roles and saved pose evidence.</summary>
[InitializeOnLoad]
public static class TerrainChoiceRuntimeValidation
{
    private const string Key = "TerrainChoiceRuntimeValidation.Active";
    private static readonly string[] Modes = { "matched-terrain", "visual-terrain_navigation-flat", "visual-flat_navigation-terrain", "matched-flat" };
    private static readonly List<string> checks = new List<string>(), errors = new List<string>();
    private static readonly List<object> cases = new List<object>();
    private static readonly HashSet<int> seen = new HashSet<int>();
    private static string output, directory;
    private static int stage, caseIndex, nextFrame, physicalPixelChecks, skippedPixelChecks;
    private static double deadline;
    static TerrainChoiceRuntimeValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            output = SessionState.GetString(Key + ".Output", "");
            deadline = EditorApplication.timeSinceStartup + 300;
            nextFrame = Time.frameCount + 10;
            SceneManager.sceneLoaded += Loaded;
            Application.logMessageReceived += Log;
            EditorApplication.update += Tick;
        };
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new InvalidOperationException("Disposable project only.");
        output = Path.GetFullPath(Path.Combine(Application.dataPath, "../terrain-choice-runtime-validation"));
        Directory.CreateDirectory(output);
        File.Delete(Path.Combine(output, "results.json"));
        SessionState.SetString(Key + ".Output", output);
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");
        EditorApplication.isPlaying = true;
    }
    private static void Loaded(Scene scene, LoadSceneMode mode)
    { foreach (var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) listener.enabled = false; }
    private static void Log(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (message.StartsWith("ArgumentOutOfRangeException: Index was out of range.") && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        errors.Add(message);
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks.Add(message); }
    private static void Next(int value, int frames = 3) { stage = value; nextFrame = Time.frameCount + frames; }
    private static int RigCount => caseIndex == 1 || caseIndex == 2 ? 4 : 1;
    private static bool Preview => caseIndex == 3;
    private static string CaseName => caseIndex == 0 ? "one-rig-led" : caseIndex == 1 ? "four-rig-led-horizontal" : caseIndex == 2 ? "four-rig-led-vertical" : "one-rig-desktop";

    private static void BeginCase(MainController main)
    {
        var system = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Templates/system_config.template.json")));
        var configs = new JArray();
        for (int i = 0; i < RigCount; i++)
            configs.Add(new JObject {
                ["vrId"] = "VR" + (i + 1), ["closedLoopMode"] = "FicTrac", ["sphereDiameter"] = 2.6,
                ["displayOrder"] = Preview ? "F" : i % 2 == 0 ? "RBLF" : "DRBLFU", ["desktopPreview"] = Preview,
                ["ledPanelWidth"] = 128, ["ledPanelHeight"] = 128,
                ["startRow"] = caseIndex == 2 ? 0 : i + 1, ["startCol"] = caseIndex == 2 ? i + 1 : 1,
                ["horizontal"] = caseIndex != 2, ["targetDisplay"] = 0,
                ["zmqAddress"] = "127.0.0.1", ["zmqPort"] = 29700 + i,
                ["manualControls"] = new JObject { ["enabled"] = false }
            });
        system["configs"] = configs;
        system["targetDisplay"] = 0;
        system["telemetry"] = new JObject { ["enabled"] = false };
        system["statusOverlay"] = new JObject { ["visible"] = false };
        system["overheadCamera"] = new JObject { ["enabled"] = false };
        system["desktopQuickStart"] = new JObject { ["demo"] = true };
        string systemFile = "Validation/terrain-" + CaseName + ".system.json", sequenceFile = "Validation/terrain-" + CaseName + ".sequence.json";
        string testConfig = "Validation/terrain-stationary-demo.json";
        var config = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Examples/Rugosity/terrain-treatment.json")));
        config["closedLoopPosition"] = false; config["closedLoopOrientation"] = false; config["autopilotEnabled"] = false;
        var rows = new JArray();
        foreach (string mode in Modes)
        {
            var treatment = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Examples/Rugosity/sequence-treatment-" + mode + ".json")));
            foreach (var row in treatment["sequences"])
            {
                var copy = (JObject)row.DeepClone(); copy["duration"] = 1.2f;
                copy["parameters"]["configFile"] = testConfig;
                rows.Add(copy);
            }
        }
        Directory.CreateDirectory(Path.Combine(Application.streamingAssetsPath, "Validation"));
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, systemFile), system.ToString());
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, sequenceFile), new JObject { ["autoStart"] = false, ["loop"] = false, ["randomise"] = false, ["sequences"] = rows }.ToString());
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, testConfig), config.ToString());
        main.PrepareConfiguredRun(systemFile, sequenceFile);
        directory = MasterDataLogger.Instance.directoryPath;
        seen.Clear();
        main.StartSequence();
    }

    private static void InspectTrial(MainController main)
    {
        var step = main.GetCurrentSequenceStep();
        if (step == null || !TerrainChoiceBuilder.Scenes.Contains(SceneManager.GetActiveScene().name) || main.SceneTransitionPending || main.RemainingStepSeconds > .7f) return;
        var patch = step.parameters["rugosity"] as JObject ?? JObject.FromObject(step.parameters["rugosity"]);
        string visual = (string)patch["visualSurface"], navigation = (string)patch["collisionSurface"];
        int identity = Array.IndexOf(TerrainChoiceBuilder.Scenes, step.sceneName) + 4 * Array.FindIndex(Modes, mode => {
            bool v = visual != "Flat", n = navigation != "Flat";
            return mode == (v ? n ? "matched-terrain" : "visual-terrain_navigation-flat" : n ? "visual-flat_navigation-terrain" : "matched-flat");
        });
        if (seen.Contains(identity)) return;
        var world = Object.FindFirstObjectByType<RugosityWorld>();
        Check(world != null && world.IsReady, CaseName + ": world ready for " + identity);
        Check(world.settings.visualSurface == visual && world.settings.collisionSurface == navigation, "Runtime config roles match " + identity);
        Check(world.HeightSurface.surfaceId == navigation && world.NormalSurface.surfaceId == navigation, "Height and normal follow navigation " + identity);
        Check(world.surfaces.All(s => s.renderers.All(r => r.enabled == (s.surfaceId == visual)) && s.colliders.All(c => c.enabled == (s.surfaceId == navigation))), "Real renderer/collider selection " + identity);
        var left = GameObject.Find("Left choice target"); var right = GameObject.Find("Right choice target");
        Check(left != null && right != null, "Both shared placeholder trees exist " + identity);
        Check(Mathf.Abs(left.transform.position.x + right.transform.position.x) < .001f && Mathf.Abs(left.transform.position.z - right.transform.position.z) < .001f && Mathf.Abs(left.transform.position.magnitude - right.transform.position.magnitude) < .001f, "Choice targets are mirrored and equidistant " + identity);
        var rigs = RuntimeRigRegistry.Rigs(world.gameObject.scene);
        Check(rigs.Length == RigCount, "Actual configured rig count " + identity);
        foreach (var rig in rigs)
        {
            var config = main.GetSystemConfigForGameObject(rig.gameObject);
            Check(rig.GetComponent<DataLogger>() != null, "Standard pose logger exists: " + rig.name);
            var follower = rig.GetComponent<RugosityRig>();
            Check(follower != null && follower.ControlsPose && follower.world == world, "Every cloned rig follows selected world: " + rig.name);
            world.HeightSurface.Sample(rig.transform.position, out float ground, out _);
            Check(Mathf.Abs(rig.transform.position.y - ground - 1) < .001f, "Runtime rig has configured 1cm clearance: " + rig.name);
            var setter = rig.GetComponent<ViewportSetter>();
            Check(setter != null, "Standard viewport routing exists: " + rig.name);
            var cameras = rig.GetComponentsInChildren<Camera>(true);
            string order = ViewportSetter.NormalizeDisplayOrder(config.displayOrder);
            Check(cameras.Count(c => c.enabled && c.gameObject.activeInHierarchy) == order.Length, "Only configured faces enabled: " + rig.name);
            for (int slot = 0; slot < order.Length; slot++)
            {
                var camera = cameras.Single(c => c.name == "Main Camera " + order[slot]);
                var expected = ViewportSetter.PanelRect(slot, config, Screen.width, Screen.height);
                Check(camera.targetDisplay == config.targetDisplay && Vector4.Distance(new Vector4(camera.rect.x, camera.rect.y, camera.rect.width, camera.rect.height), new Vector4(expected.x, expected.y, expected.width, expected.height)) < .00001f, "Runtime face order/pixels/offset/display correct: " + rig.name + "/" + order[slot]);
                if (!Preview && expected.xMin >= 0 && expected.yMin >= 0 && expected.xMax <= 1 && expected.yMax <= 1)
                {
                    Check(Mathf.Abs(camera.pixelRect.width - 128) < .01f && Mathf.Abs(camera.pixelRect.height - 128) < .01f, "Contained LED output remains exactly 128px square: " + rig.name + "/" + order[slot] + " actual=" + camera.pixelRect);
                    physicalPixelChecks++;
                }
                else if (!Preview) skippedPixelChecks++; // Batch editor may inherit a short Game view; standalone test owns full-panel coverage.
            }
            // Unavailable outputs must disable rendering, never silently fall back fullscreen.
            int display = config.targetDisplay; config.targetDisplay = Display.displays.Length;
            setter.ApplyConfiguration(config);
            Check(cameras.All(c => !c.enabled), "Unavailable display disables this rig safely: " + rig.name);
            config.targetDisplay = display; setter.ApplyConfiguration(config);
        }
        if (caseIndex == 0)
        {
            var camera = rigs[0].GetComponentsInChildren<Camera>(true).Single(c => c.name == "Main Camera F");
            var previousTarget = camera.targetTexture; var previousRect = camera.rect; var previousActive = RenderTexture.active;
            var target = new RenderTexture(640, 640, 24); target.Create();
            var image = new Texture2D(640, 640, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1); camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, "world-" + identity.ToString("D2") + ".png"), image.EncodeToPNG());
            }
            finally { camera.targetTexture = previousTarget; camera.rect = previousRect; RenderTexture.active = previousActive; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
        }
        seen.Add(identity);
    }

    private static void CheckRecording()
    {
        var archive = new ReplaySessionArchive(directory); archive.LoadRigSamples();
        Check(archive.Rigs.Count == RigCount, CaseName + ": recorded playable CSV exists for every configured animal");
        foreach (var pair in archive.Rigs)
        {
            Check(pair.Value.Count > 16 && pair.Value.Select(s => s.scene).Distinct().Count() == 4, "All four actual scene contexts recorded: " + pair.Key);
            Check(pair.Value.Select(s => s.sequenceStep).Distinct().Count() == 16, "Every treatment/pattern has playable pose rows: " + pair.Key);
            foreach (var sample in pair.Value.Where(s => s.unityFrame >= 0).GroupBy(s => s.sequenceStep).Select(g => g.Last()))
            {
                var row = archive.Sequence["sequences"][sample.sequenceStep];
                string navigation = (string)row["parameters"]["rugosity"]["collisionSurface"];
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/TerrainCueLab/" + navigation + ".asset");
                var go = new GameObject("Saved pose independent terrain sample");
                try
                {
                    var filter = go.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
                    var surface = go.AddComponent<RugositySurface>(); surface.meshes = new[] { filter };
                    Check(surface.Sample(sample.position, out float y, out var normal), "Saved position lies over selected navigation surface");
                    Check(Mathf.Abs(sample.position.y - y - 1) < .002f, "Saved CSV pose includes terrain conformance");
                    Check(Vector3.Angle(Quaternion.Euler(sample.rotation) * Vector3.up, normal) < .1f, "Saved CSV orientation follows selected navigation normal");
                    var config = archive.ExperimentFor(sample);
                    Check((string)config["rugosity"]["visualSurface"] == (string)row["parameters"]["rugosity"]["visualSurface"] && (string)config["rugosity"]["collisionSurface"] == navigation, "Saved replay reconstructs sequence surface overrides");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }
        Check(File.Exists(Path.Combine(directory, "rugosity_states.jsonl")), "Effective surface states archived with run");
        Check(JObject.Parse(File.ReadAllText(Path.Combine(directory, "config_manifest.json"))).Value<bool>("complete"), "Saved configuration archive is complete");
        cases.Add(new { name = CaseName, rigCount = RigCount, treatments = seen.Count, directory, playableRows = archive.Rigs.ToDictionary(p => p.Key, p => p.Value.Count), warnings = archive.Warnings });
    }

    private static void Tick()
    {
        if (Time.frameCount < nextFrame) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Terrain runtime timeout at stage " + stage + " case " + caseIndex);
            var main = MainController.Instance; var logger = MasterDataLogger.Instance;
            if (main == null || logger == null) return;
            if (main.SequenceError != null) throw new Exception(main.SequenceError);
            if (stage == 0) { _ = logger.FinalizeSessionAsync("terrain-validation-setup"); Next(1); return; }
            if (stage == 1)
            {
                if (logger.FinalizationTask == null || !logger.FinalizationTask.IsCompleted) return;
                BeginCase(main); Next(2); return;
            }
            if (stage == 2)
            {
                if (main.SequenceRunning) { InspectTrial(main); return; }
                Check(seen.Count == 16, "All sixteen runtime treatments inspected: " + CaseName);
                Next(3); return;
            }
            if (stage == 3)
            {
                if (logger.FinalizationTask == null || !logger.FinalizationTask.IsCompleted) return;
                Check(logger.FinalizationError == null, "Recording finalized without error: " + CaseName);
                CheckRecording(); caseIndex++;
                if (caseIndex < 4) { Next(1); return; }
                Check(errors.Count == 0, "No runtime errors: " + string.Join("; ", errors)); Finish(null);
            }
        }
        catch (Exception error) { Finish(error.ToString()); }
    }
    private static void Finish(string failure)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; SceneManager.sceneLoaded -= Loaded; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.Combine(output, "results.json"), JsonConvert.SerializeObject(new { passed = failure == null, failure, checks = checks.Count, unity = Application.unityVersion,
            scope = "Actual MainController/runtime normalized camera routing/desktop preview, all sixteen surface-role combinations across four shared-prefab scenes, real per-animal CSV recording and saved-config replay reconstruction. Batch editor inherits its actual Game view extent; full-panel physical pixel coverage requires standalone native validation. Synthetic stationary demo; no camera or animal acquisition.", actualGameView = new { width = Screen.width, height = Screen.height }, physicalPixelChecks, skippedPixelChecks, fullHdPhysicalCoverage = "separate standalone test required", cases, results = checks, errors }, Formatting.Indented));
        if (failure != null) Debug.LogError(failure); else Debug.Log("TERRAIN RUNTIME PASSED: " + checks.Count);
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
