#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Unity.Profiling;
using Object = UnityEngine.Object;

/// <summary>CPU and allocation isolation; does not mistake CPU submission time for display FPS.</summary>
public static class DiagnosticsPerformanceValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<object> results = new List<object>();
    private static readonly List<GameObject> roots = new List<GameObject>();
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY")))
            throw new Exception("Disposable project only.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string failure = null;
        try
        {
            var main = Root("Performance main", false).AddComponent<MainController>();
            typeof(MainController).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { main });
            var configs = (Dictionary<string, SystemConfig>)typeof(MainController).GetField("systemConfigs", Private).GetValue(main);
            var loops = (Dictionary<string, ClosedLoop>)typeof(MainController).GetField("vrClosedLoops", Private).GetValue(main);
            var loggers = new PerformanceDataLogger[4];
            for (int i = 0; i < 4; i++)
            {
                string id = "VR" + (i + 1); var root = Root(id, false);
                root.AddComponent<ZmqListener>(); var loop = root.AddComponent<ClosedLoop>();
                loop.SetYawGain(2.5f); loop.ConfigureAutoTrim(true, new AutoTrimConfig { flightCheckEnabled = false });
                loop.AutoTrim.Tick(0, 0, .75, true, true, true, 2.5f, 0, out _);
                configs[id] = new SystemConfig { vrId = id }; loops[id] = loop;
                loggers[i] = root.AddComponent<PerformanceDataLogger>(); loggers[i].Setup(main);
            }
            Measure("four CSV rows: capture and format", 1000, () => { foreach (var logger in loggers) logger.Format(); });
            var telemetry = Root("Performance telemetry", false).AddComponent<ExperimentTelemetry>();
            long sequence = 0; object snapshot = telemetry.Capture(0);
            Measure("telemetry: four-rig capture", 500, () => snapshot = telemetry.Capture(++sequence));
            Measure("telemetry: JSON serialization", 500, () => JsonConvert.SerializeObject(snapshot));
            var overlay = Root("Performance dashboard", true).AddComponent<RuntimeStatusOverlay>();
            typeof(RuntimeStatusOverlay).GetMethod("Start", Private).Invoke(overlay, null);
            var label = (Text)typeof(RuntimeStatusOverlay).GetField("label", Private).GetValue(overlay);
            var panel = (GameObject)typeof(RuntimeStatusOverlay).GetField("panel", Private).GetValue(overlay);
            Measure("dashboard: format", 200, () => overlay.BuildText());
            int refresh = 0;
            Measure("dashboard: repeat full layout (reference)", 200, () => {
                loops["VR1"].SetYawDCOffset((refresh++ % 91) * .0001f);
                label.text = overlay.BuildText();
                panel.GetComponent<RectTransform>().sizeDelta = new Vector2(720, Mathf.Clamp(label.preferredHeight + 62, 180, 820));
                Canvas.ForceUpdateCanvases();
            });
            var refreshLayout = (Action)Delegate.CreateDelegate(typeof(Action), overlay, typeof(RuntimeStatusOverlay).GetMethod("RefreshLayout", Private));
            Measure("dashboard: cached panel layout and canvas rebuild", 200, () => {
                loops["VR1"].SetYawDCOffset((refresh++ % 91) * .0001f);
                label.text = overlay.BuildText(); refreshLayout(); Canvas.ForceUpdateCanvases();
            });
            ValidateMedian();
            MeasureTrim(false); MeasureTrim(true);
        }
        catch (Exception error) { failure = error.ToString(); Debug.LogError(failure); }
        finally { foreach (var root in roots) Object.DestroyImmediate(root); }
        File.WriteAllText(Path.Combine(Application.dataPath, "../diagnostics-performance.json"), JsonConvert.SerializeObject(new {
            failure, unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName, results,
            note = "Unity Editor, warmed callbacks, four Kinefly rigs. CPU time and managed allocations only, not end-to-end GPU/display FPS. CSV measure excludes disk. Dashboard includes rich text layout and Canvas.ForceUpdateCanvases. Trim runs at 120 frames/s with distinct samples and 120s precision windows." }, Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
    private static GameObject Root(string name, bool active) { var root = new GameObject(name); root.SetActive(active); roots.Add(root); return root; }
    private static void Measure(string name, int count, Action action, int allocationCount = 20)
    {
        for (int i = 0; i < 30; i++) action();
        var times = new double[count];
        for (int i = 0; i < count; i++) {
            long start = System.Diagnostics.Stopwatch.GetTimestamp(); action();
            times[i] = 1000.0 * (System.Diagnostics.Stopwatch.GetTimestamp() - start) / System.Diagnostics.Stopwatch.Frequency;
        }
        using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 65536,
            ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
        for (int i = 0; i < allocationCount; i++) action();
        allocations.Stop();
        long events = allocations.ToArray().Sum(sample => sample.Count); Array.Sort(times);
        results.Add(new { name, calls = count, medianMs = times[count / 2], p95Ms = times[(int)(count * .95)], p99Ms = times[(int)(count * .99)], maximumMs = times[count - 1],
            allocationEventsPerCall = allocations.Valid && allocations.Count < allocations.Capacity ? (double?)(events / (double)allocationCount) : null,
            allocationMarkerUnit = allocations.UnitType.ToString(),
            allocationSamples = allocations.Count, allocationSamplesOverflowed = allocations.Count == allocations.Capacity });
    }
    private static void MeasureTrim(bool enabled)
    {
        var trims = new KineflyAutoTrim[4];
        for (int i = 0; i < 4; i++) { trims[i] = new KineflyAutoTrim(); trims[i].Configure(enabled, new AutoTrimConfig { flightCheckEnabled = false, windowSeconds = 120, maxWindowSeconds = 120 }); }
        var random = new System.Random(47); var yaw = new double[48000];
        for (int i = 0; i < yaw.Length; i++) yaw[i] = .2 * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
        int frame = 0;
        // Warm beyond a full 120-second window; no correction is needed in this centered noisy case.
        for (; frame < 15000; frame++) foreach (var trim in trims) trim.Tick(frame / 120.0, frame, yaw[frame], true, true, true, 2.5f, 0, out _);
        Measure(enabled ? "four auto trim ticks: on, full 120s windows" : "four auto trim ticks: off", 30000,
            () => { foreach (var trim in trims) trim.Tick(frame / 120.0, frame, yaw[frame], true, true, true, 2.5f, 0, out _); frame++; }, 2400);
    }
    private static void ValidateMedian()
    {
        var select = typeof(KineflyAutoTrim).GetMethod("SelectMedian", BindingFlags.Static | BindingFlags.NonPublic);
        var random = new System.Random(81); int checks = 0;
        foreach (int count in new[] { 5, 6, 49, 50, 749, 750, 6000, 15000 })
        for (int shape = 0; shape < 10; shape++)
        {
            var values = new double[count + 100];
            for (int i = 0; i < count; i++) values[i] = shape == 0 ? 3 : shape == 1 ? i : shape == 2 ? -i : shape == 3 ? i % 3 :
                shape == 4 ? (i % 2 == 0 ? -1e12 : 1e12) : random.NextDouble() * 4 - 2;
            // Unused scratch capacity must never enter the median.
            for (int i = count; i < values.Length; i++) values[i] = -1e15;
            var sorted = values.Take(count).OrderBy(value => value).ToArray();
            double expected = count % 2 == 0 ? .5 * (sorted[count / 2 - 1] + sorted[count / 2]) : sorted[count / 2];
            double actual = (double)select.Invoke(null, new object[] { values, count });
            if (actual != expected) throw new Exception("Median differs from full-sort truth, count=" + count + " shape=" + shape);
            checks++;
        }
        results.Add(new { name = "median selection truth checks", checks });
    }
}

public sealed class PerformanceDataLogger : DataLogger
{
    public void Setup(MainController main)
    {
        mainController = main; zmq = GetComponent<ZmqListener>();
        AddColumns("stepIndex", "stepName", "loopIndex", "cumulativeStep", "swapElapsedSec", "swapWallClock", "grayAtTrialStart", "blackSideAtTrialStart", "skyboxId", "skyboxSampleUtc", "SensRotXRad", "SensRotYRad", "SensRotZRad", "trackingImplementation", "trackingMode", "trackingInputUnits", "wireYaw", "yawGain", "yawDCOffsetRadians", "yawOutputDegPerSecond", "autoTrimEnabled", "autoTrimState", "flightDetected", "flightVarianceRadiansSquared", "autoTrimMedianDegPerSecond", "autoTrimPasses", "flightCheckEnabled", "flightThresholdRadiansSquared", "flightSamples", "yawDCOffsetDegrees", "yawDCCompensationDegPerSecond", "autoTrimMeanDegPerSecond", "autoTrimUncertaintyDegPerSecond", "autoTrimMeasurementSeconds", "experimentPhase");
    }
    public void Format() => PrepareLogData();
}
#endif
