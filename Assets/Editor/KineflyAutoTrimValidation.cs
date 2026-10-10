#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class KineflyAutoTrimValidation
{
    private const string Key = "KineflyAutoTrimValidation.Active";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks, phase;
    private static double deadline, phaseTime;
    private static readonly Dictionary<string, float> offsets = new Dictionary<string, float>();
    private static readonly List<double> captureTimes = new List<double>();
    private static readonly List<double> legacyTimes = new List<double>();
    private static int maxQueue;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
    private static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Flags).GetValue(obj);
    private static double Wave(int i) => i % 4 == 0 ? -1.2 : i % 4 == 2 ? 1.2 : 0;
    private static AutoTrimConfig Fast() => new AutoTrimConfig { windowSeconds = 1, flightWindowSeconds = .2,
        flightVarianceThreshold = .3, settleSeconds = .2, aggressiveness = .5, maxStepRadians = .1, toleranceDegPerSecond = 1 };

    static KineflyAutoTrimValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            checks = SessionState.GetInt(Key + ".Checks", 0);
            deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update += Tick;
        };
        SceneManager.sceneLoaded += (scene, mode) => {
            if (!SessionState.GetBool(Key, false)) return;
            foreach (var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) listener.enabled = false;
        };
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Use a disposable validation copy.");
        PureChecks(); WriterChecks(); Prepare();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetInt(Key + ".Checks", checks);
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    private static void PureChecks()
    {
        Check(!JsonConvert.DeserializeObject<SequenceItem>("{}").autoTrim, "Legacy rows default auto trim off");
        foreach (float gain in new[] { -10f, 1f, 10f })
        {
            var trim = new KineflyAutoTrim(); var config = Fast(); trim.Configure(true, config);
            float offset = 0;
            for (int i = 0; i < 1500; i++)
            {
                double yaw = .4 + Wave(i);
                if (trim.Tick(i * .04, i, yaw, true, true, true, gain, offset, out float next))
                { Check(Math.Abs(next - offset) <= config.maxStepRadians + 1e-6, "Bounded adjustment"); offset = next; }
            }
            Check(trim.Flying && trim.Passes > 1, "Flying and multiple adaptive passes");
            Check(Math.Abs(gain * (.4 - offset) * Mathf.Rad2Deg) <= 1.001, "Effective median converges with signed gain " + gain);
            Check(trim.State == "CENTERED", "Centered indicator");
            var lastChange = trim.LastAdjustment;
            Check(lastChange != null && lastChange.toRadians == offset && lastChange.deltaRadians == lastChange.toRadians - lastChange.fromRadians,
                "Adjustment history matches the last offset actually returned");
            float held = offset; trim.Configure(false, config);
            for (int i = 0; i < 100; i++) Check(!trim.Tick(61 + i * .04, i, 2 + Wave(i), true, true, true, gain, held, out _), "Off keeps learned offset");
            Check(trim.Flying, "Flight indicator works when trim is off");
            Check(ReferenceEquals(trim.LastAdjustment, lastChange), "Turning trim off retains the last adjustment for the dashboard");
        }
        foreach (string gate in new[] { "nonflight", "stale", "walking", "open", "zero", "duplicates" })
        {
            var trim = new KineflyAutoTrim(); trim.Configure(true, Fast());
            for (int i = 0; i < 200; i++)
                Check(!trim.Tick(i * .04, gate == "duplicates" ? 1 : i, gate == "nonflight" ? 5 : .4 + Wave(i),
                    gate != "stale", gate != "walking", gate != "open", gate == "zero" ? 0 : 10, .12f, out _), "Gate preserves offset: " + gate);
            if (gate == "nonflight") Check(trim.FlightReady && !trim.Flying, "Constant biased signal is not flight");
            if (gate == "duplicates") Check(!trim.FlightReady, "Held packets cannot fill a variance window");
        }
        var restarted = new KineflyAutoTrim(); restarted.Configure(true, Fast());
        for (int i = 0; i < 25; i++) restarted.Tick(i * .04, i, .4 + Wave(i), true, true, true, 1, 0, out _);
        restarted.Tick(1, 26, .4, false, true, true, 1, 0, out _);
        Check(!restarted.FlightReady && restarted.WindowProgress == 0, "Stale input clears old evidence");
        bool invalid = false; try { new AutoTrimConfig { aggressiveness = double.NaN }.Validate(); } catch (ArgumentException) { invalid = true; }
        Check(invalid, "Reject nonfinite settings");
        var hysteresis = new KineflyAutoTrim(); hysteresis.Configure(false, Fast());
        int sample = 0;
        Action<double, int> signal = (amplitude, count) => {
            for (int n = 0; n < count; n++, sample++)
                hysteresis.Tick(sample * .04, sample, amplitude * Wave(sample) / 1.2, true, true, true, 1, 0, out _);
        };
        signal(1, 90); Check(hysteresis.Flying, "Confirmed variance enters flight");
        signal(.7, 40); Check(hysteresis.Flying, "Variance between entry and exit thresholds retains flight");
        signal(.4, 40); Check(!hysteresis.Flying, "Low variance exits flight");
        signal(.7, 40); Check(!hysteresis.Flying, "Intermediate variance cannot re-enter flight");
        signal(1, 12); Check(!hysteresis.Flying, "Short above-threshold interval awaits confirmation");
        signal(1, 50); Check(hysteresis.Flying, "Sustained renewed variance confirms flight");
        // Regression: real recorded variance is below the former .3 default, but well above .01.
        foreach (int sign in new[] { -1, 1 })
        {
            var defaults = new KineflyAutoTrim(); defaults.Configure(true, null);
            float defaultOffset = 0;
            for (int i = 0; i < 6000; i++)
                if (defaults.Tick(i * .04, i, sign + .2 * Wave(i), true, true, true, 2.5f, defaultOffset, out float next)) defaultOffset = next;
            Check(defaults.Flying && defaults.Passes >= 8 && defaults.State == "CENTERED" && Math.Abs(2.5 * (sign - defaultOffset) * Mathf.Rad2Deg) <= 1,
                "Set-and-forget defaults center a signed unit bias with variance below .3 within 240s");
            Check(defaults.FlightThreshold == .01, "Omitted settings use the recording-informed threshold");
        }
        var rawVariance = new KineflyAutoTrim(); rawVariance.Configure(false, null);
        for (int i = 0; i < 250; i++) rawVariance.Tick(i * .04, i, 1.4 + .2 * Wave(i), true, true, true, 50, -2, out _);
        var trailing = Enumerable.Range(0, 250).Where(i => 249 * .04 - i * .04 <= 2).Select(i => 1.4 + .2 * Wave(i)).ToArray();
        double mean = trailing.Average(), variance = trailing.Average(yaw => Math.Pow(yaw - mean, 2));
        Check(rawVariance.FlightSampleCount == trailing.Length && Math.Abs(rawVariance.FlightVariance - variance) < 1e-10,
            "Flight uses trailing population variance of raw radians, unaffected by gain or DC offset");
        var bypass = new KineflyAutoTrim(); bypass.Configure(true, Fast());
        for (int i = 0; i < 100; i++) Check(!bypass.Tick(i * .04, i, 1, true, true, true, 1, 0, out _), "Flight gate holds a constant biased input");
        bypass.SetFlightCheckEnabled(false);
        float bypassOffset = 0;
        for (int i = 100; i < 200; i++) if (bypass.Tick(i * .04, i, 1, true, true, true, 1, bypassOffset, out float next)) bypassOffset = next;
        Check(bypassOffset > 0 && !bypass.Flying && bypass.FlightReady && !bypass.FlightCheckEnabled, "Bypass trims without fabricating flying status");
        foreach (string gate in new[] { "stale", "walking", "open", "zero", "duplicates" })
        {
            var guarded = new KineflyAutoTrim(); var config = Fast(); config.flightCheckEnabled = false; guarded.Configure(true, config);
            for (int i = 0; i < 200; i++) Check(!guarded.Tick(i * .04, gate == "duplicates" ? 1 : i, 1,
                gate != "stale", gate != "walking", gate != "open", gate == "zero" ? 0 : 1, 0, out _), "Bypass retains independent safety gate " + gate);
        }
        bypass.SetFlightCheckEnabled(true);
        for (int i = 200; i < 250; i++) Check(!bypass.Tick(i * .04, i, 1, true, true, true, 1, bypassOffset, out _), "Restoring flight check holds learned offset");
        bypass.SetFlightCheckEnabled(false); bypass.Disable();
        Check(!bypass.Enabled && !bypass.FlightCheckEnabled && bypass.FlightThreshold == .3, "Disabling preserves the gate settings displayed on the idle dashboard");
        for (int i = 250; i < 300; i++) Check(!bypass.Tick(i * .04, i, 1, true, true, true, 1, bypassOffset, out _), "Disabled bypass cannot change offsets");
        foreach (int sign in new[] { -1, 1 })
        {
            var bounded = new KineflyAutoTrim(); var config = Fast(); config.flightCheckEnabled = false; bounded.Configure(true, config);
            float offset = 0;
            for (int i = 0; i < 2000; i++)
                if (bounded.Tick(i * .04, i, sign * 3, true, true, true, 1, offset, out float next))
                { Check(Math.Abs(next) <= 2 && Math.Abs(next - offset) <= .100001, "Absolute and per-pass limits both respected"); offset = next; }
            Check(offset == sign * 2 && bounded.State == "OFFSET LIMIT", "Unreachable median is reported at the signed offset limit");
        }
    }
    private static void WriterChecks()
    {
        string path = Path.Combine(Application.dataPath, "../async-swarm-test.csv.gz");
        var writer = new AsyncSwarmWriter(path, "Timestamp,Name,Layer,X,Y,Z,skyboxId,skyboxSampleUtc,experimentPhase");
        for (int f = 0; f < 120; f++)
        {
            var frame = writer.Rent(64); frame.timestamp = DateTime.UtcNow; frame.phase = f < 60 ? "pre" : "post";
            frame.skyboxId = "test"; frame.skyboxSampleUtc = "utc"; frame.count = 64;
            for (int a = 0; a < 64; a++) frame.agents[a] = new AsyncSwarmWriter.Agent { name = "agent" + a, layer = "VR1", x = f, y = a, z = 3.25f };
            Check(writer.Submit(frame), "Writer accepts full frame snapshot");
        }
        writer.WaitForCompletion();
        Check(writer.Failure == null && writer.DroppedFrames == 0 && writer.WrittenRows == 7680 && writer.PendingFrames == 0, "Worker drains all rows");
        using (var input = new StreamReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress)))
        {
            string[] rows = input.ReadToEnd().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Check(rows.Length == 7681, "Valid gzip with every row and header");
            Check(rows[0].StartsWith("Timestamp,Name,Layer"), "CSV header remains compatible");
            Check(rows[1].Contains(",agent0,VR1,0,0,3.25,test,utc,pre"), "Snapshot coordinates/metadata preserved");
            Check(rows[rows.Length - 1].Contains(",agent63,VR1,119,63,3.25,test,utc,post"), "FIFO and sample-time phase preserved");
        }
    }
    private static void Prepare()
    {
        var system = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Templates/system_config.template.json")));
        system["telemetry"]["enabled"] = false; system["overheadCamera"]["enabled"] = false;
        system["targetFrameRate"] = 120;
        system["configs"][3]["closedLoopMode"] = "FicTrac";
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "system_config.json"), system.ToString());
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "auto-trim-test.json"), JsonConvert.SerializeObject(new {
            numberOfLocusts = 64, agentVisual = "Bogong", locustSpeed = 3, closedLoopPosition = 0, closedLoopOrientation = 1, autopilotEnabled = false,
            autoTrimSettings = Fast()
        }));
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json"), JsonConvert.SerializeObject(new {
            autoStart = true, loop = false, sequences = new[] {
                new { sceneName = "Swarm", duration = 1000, gain = 10.0, autoTrim = true, reloadScene = true, parameters = new { configFile = "auto-trim-test.json" } },
                new { sceneName = "Swarm", duration = 1000, gain = -2.5, autoTrim = false, reloadScene = false, parameters = new { configFile = "auto-trim-test.json" } },
                new { sceneName = "Swarm", duration = 1000, gain = 1.0, autoTrim = false, reloadScene = true, parameters = new { configFile = "auto-trim-test.json" } }
            }
        }));
    }
    private static void Feed(ClosedLoop rig, int index, double bias = .4, bool varied = true)
    {
        var listener = rig.GetComponent<ZmqListener>();
        Type packetType = typeof(ZmqListener).GetNestedType("ZmqMessage", BindingFlags.NonPublic);
        var packet = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(new { yaw = bias + (varied ? Wave(index) : 0) }), packetType);
        Call(listener, "UpdatePose", packet);
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Auto trim integration timeout at phase " + phase);
            if (phase == 0) { SceneManager.LoadScene("ControlScene"); phase = 1; return; }
            var main = MainController.Instance;
            if (main == null || main.VRClosedLoops.Count != 4 || SceneManager.GetActiveScene().name != "Swarm") return;
            var rigs = main.VRClosedLoops.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToArray();
            int tick = (int)(Time.realtimeSinceStartupAsDouble * 25);
            foreach (var rig in rigs) Feed(rig, tick, phase >= 4 ? (rig == rigs[2] ? -.9 : .9) : (rig == rigs[1] ? -.4 : .4), phase < 5);
            if (phase == 1)
            {
                if (rigs.Any(r => r.GetYawGain() != 10)) return;
                Check(rigs.Take(3).All(r => r.AutoTrim.Enabled && r.SupportsAutoTrim), "Row enables flight rigs");
                Check(rigs.Take(3).All(r => r.AutoTrim.FlightThreshold == .3), "Experiment tuning reaches every rig without sequence settings");
                Check(!rigs[3].SupportsAutoTrim, "Walking rig ignores auto trim");
                foreach (var rig in rigs.Take(3))
                {
                    // Controlled time exercises production integration/persistence without wall-clock waits.
                    var trim = rig.AutoTrim; float offset = 0;
                    double bias = rig == rigs[1] ? -.4 : .4;
                    for (int i = 0; i < 600; i++)
                        if (trim.Tick(i * .04, i, bias + Wave(i), true, true, true, 10, offset, out float next)) { offset = next; rig.SetYawDCOffset(offset); }
                    Check(Math.Abs(offset) > .3 && trim.Passes > 1, "Each flight rig learns its own offset");
                    offsets[rig.name] = offset;
                }
                offsets[rigs[3].name] = rigs[3].GetYawDCOffset();
                var hud = Object.FindFirstObjectByType<RuntimeStatusOverlay>().BuildText();
                Check(hud.Contains("FLIGHT DASHBOARD") && hud.Contains("YAW CLOSED") && hud.Contains("AUTO TRIM") && hud.Contains("FLYING"), "Compact dashboard status");
                Check(hud.Contains("DC +") && hud.Contains("DC -") && hud.Contains("LAST AUTO: DC UP") && hud.Contains("LAST AUTO: DC DOWN"),
                    "Dashboard shows signed DC offsets and each rig's actual adjustment direction");
                phaseTime = EditorApplication.timeSinceStartup; phase = 2; return;
            }
            if (phase == 2)
            {
                foreach (var logger in Object.FindObjectsByType<SwarmLogger>(FindObjectsSortMode.None))
                {
                    if (logger.CaptureMilliseconds > 0) captureTimes.Add(logger.CaptureMilliseconds);
                    maxQueue = Math.Max(maxQueue, logger.PendingFrames);
                    Check(logger.DroppedFrames == 0 && logger.Failure == null, "64 agents per rig log without loss");
                }
                if (EditorApplication.timeSinceStartup - phaseTime < 3) return;
                BenchmarkLegacy();
                CaptureDashboard();
                Get<UnityEngine.UI.Button>(Object.FindFirstObjectByType<RuntimeStatusOverlay>(), "autoTrimButton").onClick.Invoke();
                Check(rigs.All(r => !r.AutoTrim.Enabled), "Dashboard toggle disables current row");
                var pausedHud = Object.FindFirstObjectByType<RuntimeStatusOverlay>().BuildText();
                Check(pausedHud.Contains("DC +") && pausedHud.Contains("DC -") && pausedHud.Contains("LAST AUTO:"), "Offsets and last automatic changes remain visible with trim off");
                Set(main, "timer", -1f); Call(main, "ManageTimerAndTransitions");
                Check(rigs.All(r => !r.AutoTrim.Enabled && r.GetYawGain() == -2.5f), "In-place row turns trim off and changes gain");
                foreach (var rig in rigs) Check(rig.GetYawDCOffset() == offsets[rig.name], "In-place transition retains offsets");
                Set(main, "timer", -1f); Call(main, "ManageTimerAndTransitions"); phase = 3; return;
            }
            if (phase == 3)
            {
                if (main.currentStep != 2 || rigs.Any(r => r.GetYawGain() != 1)) return;
                foreach (var rig in rigs) Check(!rig.AutoTrim.Enabled && rig.GetYawDCOffset() == offsets[rig.name], "Reloaded scene retains automatic offsets with trim off");
                main.SetCurrentAutoTrim(true); Check(rigs.All(r => r.AutoTrim.Enabled), "Button enables the row flag");
                phaseTime = EditorApplication.timeSinceStartup; phase = 4; return;
            }
            if (phase == 4)
            {
                if (rigs.Take(3).Any(r => r.AutoTrim.Passes < 2)) return;
                foreach (var rig in rigs.Take(3))
                {
                    Check(rig == rigs[2] ? rig.GetYawDCOffset() < offsets[rig.name] - .1 : rig.GetYawDCOffset() > offsets[rig.name] + .1,
                        "Production Update performs multiple live passes in each direction");
                    offsets[rig.name] = rig.GetYawDCOffset();
                }
                CaptureDashboard();
                rigs[0].SetClosedLoopOrientation(false); rigs[1].SetYawGain(0);
                phaseTime = EditorApplication.timeSinceStartup; phase = 5; return;
            }
            if (phase == 5)
            {
                if (EditorApplication.timeSinceStartup - phaseTime < 1) return;
                foreach (var rig in rigs.Take(3)) Check(rig.GetYawDCOffset() == offsets[rig.name], "Live open-loop/zero-gain/nonflight gates hold offsets");
                Check(!rigs[2].AutoTrim.Flying, "Live nonflight indicator");
                var overlay = Object.FindFirstObjectByType<RuntimeStatusOverlay>();
                Get<UnityEngine.UI.Button>(overlay, "flightCheckButton").onClick.Invoke();
                Check(rigs.All(r => !r.AutoTrim.FlightCheckEnabled), "Actual dashboard button bypasses the flight gate");
                Check(overlay.BuildText().Contains("NOT FLYING") && overlay.BuildText().Contains("FLIGHT BYPASSED"), "HUD distinguishes measured nonflight from a bypass");
                phaseTime = EditorApplication.timeSinceStartup; phase = 6; return;
            }
            if (phase == 6)
            {
                if (EditorApplication.timeSinceStartup - phaseTime < 2) return;
                Check(rigs[2].GetYawDCOffset() != offsets[rigs[2].name] && !rigs[2].AutoTrim.Flying, "Live Update adjusts constant biased input under bypass");
                Check(rigs[0].GetYawDCOffset() == offsets[rigs[0].name] && rigs[1].GetYawDCOffset() == offsets[rigs[1].name], "Bypass keeps live open-loop and zero-gain offsets held");
                CaptureDashboard();
                Get<UnityEngine.UI.Button>(Object.FindFirstObjectByType<RuntimeStatusOverlay>(), "flightCheckButton").onClick.Invoke();
                Check(rigs.All(r => r.AutoTrim.FlightCheckEnabled), "Dashboard restores flight checking");
                Call(main, "FailExperiment", new InvalidOperationException("Expected validation failure"));
                Check(rigs.All(r => !r.AutoTrim.Enabled), "Experiment failure disables trim");
                main.StopSequence(); Check(rigs.All(r => !r.AutoTrim.Enabled), "Stopping disables automatic corrections");
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error.ToString()); }
    }
    private static void CaptureDashboard()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        // Exclude only the known Editor Search indexing error from this operator-preview fixture.
        var errorQueue = (Queue<RuntimeStatusOverlay.ErrorEntry>)typeof(RuntimeStatusOverlay).GetField("errors", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var retained = errorQueue.Where(e => e.stackTrace == null || !e.stackTrace.Contains("UnityEditor.Search.SearchDatabase")).ToArray();
        errorQueue.Clear(); foreach (var error in retained) errorQueue.Enqueue(error);
        var overlay = Object.FindFirstObjectByType<RuntimeStatusOverlay>();
        overlay.SetVisible(true); Set(overlay, "nextRefresh", 0.0); Call(overlay, "Update");
        var canvas = Get<Canvas>(overlay, "canvas");
        var cameraRoot = new GameObject("Dashboard preview"); var camera = cameraRoot.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .15f, .2f);
        camera.cullingMask = 1 << 31;
        var transforms = canvas.GetComponentsInChildren<Transform>(true); var layers = transforms.Select(t => t.gameObject.layer).ToArray();
        for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = 31;
        var target = new RenderTexture(1600, 900, 24); target.Create(); camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases(); camera.Render();
        var prior = RenderTexture.active; RenderTexture.active = target;
        var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../auto-trim-dashboard.png"), pixels.EncodeToPNG());
        RenderTexture.active = prior; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
        for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
        camera.targetTexture = null; target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(cameraRoot);
    }
    private static void BenchmarkLegacy()
    {
        var agents = Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None).First().Spawned;
        string path = Path.Combine(Application.dataPath, "../legacy-swarm-benchmark.csv.gz");
        using (var writer = new StreamWriter(new GZipStream(File.Create(path), System.IO.Compression.CompressionLevel.Optimal)))
            for (int f = 0; f < 240; f++)
            {
                var timer = Stopwatch.StartNew();
                var all = GameObject.FindGameObjectsWithTag("SimulatedLocust");
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                foreach (var agent in all)
                    if (agent.layer == agents[0].layer)
                    { Vector3 p = agent.transform.position; writer.WriteLine(FormattableString.Invariant($"{timestamp},{agent.name},{LayerMask.LayerToName(agent.layer)},{p.x},{p.y},{p.z},{NightSkyController.CurrentId},{NightSkyController.CurrentSampleUtc},{MainController.RecordedExperimentPhase}")); }
                legacyTimes.Add(timer.Elapsed.TotalMilliseconds);
            }
    }
    private static object Timing(List<double> values)
    {
        values.Sort(); return new { samples = values.Count, medianMs = values.Count > 0 ? values[values.Count / 2] : 0,
            p95Ms = values.Count > 0 ? values[(int)((values.Count - 1) * .95)] : 0 };
    }
    private static void Finish(string failure)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
        AsyncSwarmWriter.DrainAll();
        File.WriteAllText(Path.Combine(Application.dataPath, "../auto-trim-validation.json"), JsonConvert.SerializeObject(new {
            checks, failure, unity = Application.unityVersion, agentsPerRig = 64, rigs = 4,
            asyncCapture = Timing(captureTimes), legacySynchronousLogging = Timing(legacyTimes), maxQueue
        }, Formatting.Indented));
        if (failure != null) Debug.LogError(failure);
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
#endif
