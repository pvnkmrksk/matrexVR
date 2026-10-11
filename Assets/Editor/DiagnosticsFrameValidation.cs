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
using Unity.Profiling;
using Object = UnityEngine.Object;

/// <summary>Same real scene, isolated diagnostic consumers, then all together.</summary>
[InitializeOnLoad]
public static class DiagnosticsFrameValidation
{
    private const string Key = "DiagnosticsFrameValidation.Active";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly string[] Cases = { "none", "dashboard", "telemetry", "CSV logging", "auto trim", "all", "none repeat" };
    private static readonly List<object> results = new List<object>();
    private static readonly List<double> frames = new List<double>(), cpu = new List<double>(), gc = new List<double>(), render = new List<double>();
    private static Camera[] cameras;
    private static ProfilerRecorder playerLoop, allocations;
    private static ClosedLoop[] rigs; private static ZmqListener[] listeners;
    private static DataLogger[] loggers; private static RuntimeStatusOverlay overlay; private static ExperimentTelemetry telemetry;
    private static object packet; private static FieldInfo yaw; private static MethodInfo publish;
    private static int phase = -1, lastFrame = -1, sample;
    private static double started, deadline;
    static DiagnosticsFrameValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            phase = -1; deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Tick;
        };
        SceneManager.sceneLoaded += (scene, mode) => {
            if (!SessionState.GetBool(Key, false)) return;
            foreach (var input in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) input.enabled = false;
        };
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable project only.");
        var system = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Templates/system_config.template.json")));
        system["telemetry"]["port"] = 19885; system["overheadCamera"]["enabled"] = false;
        system["targetFrameRate"] = -1; system["vSyncCount"] = 0;
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "system_config.json"), system.ToString());
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json"), JsonConvert.SerializeObject(new {
            autoStart = true, loop = false, sequences = new[] { new { sceneName = "Choice_Selwyn", duration = 1000, gain = 2.5, autoTrim = true,
                reloadScene = true, parameters = new { configFile = "Examples/Flight/auto-trim-calibration.example.json" } } } }));
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    private static void ConfigureCase()
    {
        string name = Cases[phase]; bool all = name == "all";
        overlay.SetVisible(all || name == "dashboard"); telemetry.enabled = all || name == "telemetry";
        if (telemetry.enabled) telemetry.Configure();
        foreach (var logger in loggers) logger.enabled = all || name == "CSV logging";
        foreach (var rig in rigs)
        {
            rig.SetYawDCOffset(0);
            rig.ConfigureAutoTrim(all || name == "auto trim", new AutoTrimConfig { windowSeconds = 120, maxWindowSeconds = 120 });
            double now = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i <= 3250; i++) rig.AutoTrim.Tick(now - 130 + i * .04, i, i % 2 == 0 ? .2 : -.2, true, true, true, 2.5f, 0, out _);
        }
        frames.Clear(); cpu.Clear(); gc.Clear(); render.Clear(); started = EditorApplication.timeSinceStartup;
        Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Frame benchmark timed out.");
            if (phase < 0)
            {
                var main = MainController.Instance;
                if (main == null || main.VRClosedLoops.Count != 4 || SceneManager.GetActiveScene().name != "Choice_Selwyn") return;
                rigs = main.VRClosedLoops.OrderBy(entry => entry.Key).Select(entry => entry.Value).ToArray();
                listeners = rigs.Select(rig => rig.GetComponent<ZmqListener>()).ToArray();
                loggers = rigs.Select(rig => rig.GetComponent<DataLogger>()).ToArray();
                overlay = Object.FindFirstObjectByType<RuntimeStatusOverlay>(); telemetry = Object.FindFirstObjectByType<ExperimentTelemetry>();
                cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(camera => camera.enabled).ToArray();
                var type = typeof(ZmqListener).GetNestedType("ZmqMessage", BindingFlags.NonPublic);
                packet = Activator.CreateInstance(type); yaw = type.GetField("yaw"); publish = typeof(ZmqListener).GetMethod("UpdatePose", Private);
                playerLoop = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "PlayerLoop");
                allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                phase = 0; ConfigureCase();
            }
            if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
            yaw.SetValue(packet, sample++ % 2 == 0 ? .2f : -.2f);
            foreach (var listener in listeners) publish.Invoke(listener, new[] { packet });
            // A batch Editor has no automatically rendered Game view. Submit the actual cameras
            // explicitly so this experiment includes scene rendering instead of reporting fictitious FPS.
            long renderStart = System.Diagnostics.Stopwatch.GetTimestamp();
            foreach (var camera in cameras) camera.Render();
            double renderMs = 1000.0 * (System.Diagnostics.Stopwatch.GetTimestamp() - renderStart) / System.Diagnostics.Stopwatch.Frequency;
            double elapsed = EditorApplication.timeSinceStartup - started;
            if (elapsed > 2)
            {
                frames.Add(Time.unscaledDeltaTime * 1000);
                render.Add(renderMs);
                if (playerLoop.Valid) cpu.Add(playerLoop.LastValue * 1e-6);
                if (allocations.Valid) gc.Add(allocations.LastValue);
            }
            if (elapsed < 6) return;
            results.Add(new { name = Cases[phase], frames = frames.Count, frameMilliseconds = Stats(frames), playerLoopMilliseconds = Stats(cpu), cameraSubmissionMilliseconds = Stats(render), gcBytesPerFrame = Stats(gc) });
            if (++phase == Cases.Length) { Finish(null); return; }
            ConfigureCase();
        }
        catch (Exception error) { Debug.LogError(error); Finish(error.ToString()); }
    }
    private static object Stats(List<double> values)
    {
        if (values.Count == 0) return null; var sorted = values.OrderBy(v => v).ToArray();
        return new { median = sorted[sorted.Length / 2], p95 = sorted[(int)(sorted.Length * .95)], p99 = sorted[(int)(sorted.Length * .99)], maximum = sorted[sorted.Length - 1], mean = sorted.Average() };
    }
    private static void Finish(string failure)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Key, false);
        playerLoop.Dispose(); allocations.Dispose();
        File.WriteAllText(Path.Combine(Application.dataPath, "../diagnostics-frames.json"), JsonConvert.SerializeObject(new {
            failure, results, unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName,
            cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(camera => camera.enabled),
            note = "Isolated batch Editor Play mode; same Selwyn MW sky and four rigs/six 128px cameras each. No swarm. Cameras explicitly rendered because batch mode has no automatically rendered Game view; camera timings measure CPU submission, not completed GPU time. PlayerLoop measures game callbacks separately. Two seconds settling and four seconds measured per case, synthetic fresh wing samples. These are not LED workstation/display FPS. CSV coroutines can drain their preceding case during warmup." }, Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
#endif
