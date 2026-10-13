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

/// <summary>Exercises the original ControlScene and four-block sequence in an isolated project.</summary>
[InitializeOnLoad]
public static class SelwynSwarmCycleValidation
{
    private const string Key = "SelwynSwarmCycleValidation.Active";
    private const string Folder = "Examples/Swarm/";
    private static readonly string[] Files = {
        "selwyn-cycle-01-setup-full.example.json", "selwyn-cycle-02-milky-way-full.example.json",
        "selwyn-cycle-03-milky-way-swarm.example.json", "selwyn-cycle-04-swarm-only.example.json" };
    private static readonly float[] Headings = { 0, 41, 123, 275 };
    private static int checks, stage, nextFrame;
    private static double deadline, observationStarted;
    private static float remainingAtObservation;
    private static ClosedLoop[] rigs;
    private static int[] rigIds;
    private static HeadingReferenceResult[] baseline;
    private static readonly List<string> errors = new List<string>();
    private static object directions, population, performance;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }

    static SelwynSwarmCycleValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            checks = SessionState.GetInt(Key + ".checks", 0);
            var prior = JObject.Parse(SessionState.GetString(Key + ".results", "{}"));
            directions = prior["directions"]; population = prior["population"];
            deadline = EditorApplication.timeSinceStartup + 300;
            Application.logMessageReceived += OnLog;
            SceneManager.sceneLoaded += DisableInputs;
            EditorApplication.update += Tick;
        };
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (message == "No backup data file found." || stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        errors.Add(message + "\n" + stack);
    }
    private static void DisableInputs(Scene scene, LoadSceneMode mode)
    {
        foreach (var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) listener.enabled = false;
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable project only.");
        File.Copy(Path.Combine(Application.streamingAssetsPath, Folder + "selwyn-milky-way-swarm-cycle.sequence.json"),
            Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json"), true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try { ConfigsAndDistributions(); Population(); } catch (Exception e) { Finish(e.ToString()); return; }
        var system = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Templates/system_config.template.json")));
        system["telemetry"]["enabled"] = false; system["overheadCamera"]["enabled"] = false;
        foreach (var rig in system["configs"]) { rig["ledPanelWidth"] = 64; rig["ledPanelHeight"] = 64; rig["displayOrder"] = "RBLFDU"; }
        system["configs"][0]["displayOrder"] = "RBLF";
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "system_config.json"), system.ToString());
        // Keep the production sequence's order/durations; advance explicitly after checks.
        // Only shorten the observation in the disposable copy to exercise completion.
        string measurement = Path.Combine(Application.streamingAssetsPath, Folder + Files[1]);
        var document = JObject.Parse(File.ReadAllText(measurement));
        document["headingReference"]["windowSeconds"] = .4;
        File.WriteAllText(measurement, document.ToString());
        SessionState.SetInt(Key + ".checks", checks);
        SessionState.SetString(Key + ".results", JsonConvert.SerializeObject(new { directions, population }));
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    private static double EffectiveDuration(JObject document, double duration)
    {
        var heading = document["headingReference"]?.ToObject<HeadingReferenceConfig>();
        if (heading?.enabled != true) return duration;
        double window = heading.startOffsetSeconds + heading.windowSeconds;
        return heading.measureDuringPresentation ? Math.Max(duration, window) : duration + window;
    }
    private static JObject Read(string file) => JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, Folder + file)));
    private static void ConfigsAndDistributions()
    {
        var seq = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json")));
        Check(!(bool)seq["randomise"] && (bool)seq["loop"] && !(bool)seq["autoStart"], "Manual start, fixed block order, repeating cycle.");
        Check((int)seq["loopStartIndex"] == 1, "Setup runs once; subsequent loops start at the Milky Way block.");
        seq.ToObject<SequenceConfig>().ValidateLoop();
        Check(new SequenceConfig().loopStartIndex == 0, "Older sequences still repeat from row zero by default.");
        var steps = (JArray)seq["sequences"];
        Check(steps.Count == 4, "Four separate experiment configs, without parameters embedded in sequence rows.");
        for (int i = 0; i < 4; i++)
        {
            Check((string)steps[i]["sceneName"] == "Choice_Selwyn" && !(bool)steps[i]["reloadScene"], "All blocks mutate the same Selwyn scene.");
            Check((double)steps[i]["duration"] == (i == 0 ? 360 : 240) && (bool)steps[i]["autoTrim"] == (i == 0), "Correct duration and setup-only trimming.");
            var doc = Read(Files[i]); var config = doc.ToObject<SceneConfig>();
            Check(config.autopilotEnabled && config.autopilotSpeed == 7 && !config.closedLoopPosition && config.closedLoopOrientation, "Constant 7 m/s translation; animal steers.");
            Check(config.resetPositionOnStart == (i == 0) && config.resetRotationOnStart == (i == 0), "Only setup resets pose.");
            Check(EffectiveDuration(doc, i == 0 ? 360 : 240) == (i == 0 ? 360 : 240), "Setup UI reports the actual block duration, without doubling measurement.");
            Check((config.swarm != null) == (i >= 2) && (config.uniformSkyColor != null) == (i == 3), "Moths and uniform sky appear in their requested blocks.");
            if (i < 3) { config.nightSky.Validate(); Check(config.nightSky.localTime == "now" && config.nightSky.timeZoneId == "Australia/Sydney" && config.nightSky.northYawDegrees == 0, "Current local Selwyn sky with north zero."); }
        }
        var legacy = new JObject { ["headingReference"] = new JObject { ["enabled"] = true, ["windowSeconds"] = 10 } };
        Check(EffectiveDuration(legacy, 30) == 40, "Legacy observation still precedes presentation.");
        Check(EffectiveDuration(new JObject { ["headingReference"] = JValue.CreateNull() }, 30) == 30, "Null reference does not break the setup duration display.");
        var cfg = Read(Files[2])["swarm"].ToObject<SwarmConfig>(); cfg.ValidateSwarm();
        Check(cfg.directionDistribution == "VonMises" && cfg.kappa >= 100 && cfg.kappaElevation >= 100, "Opt-in high normalized yaw and elevation concentrations, retaining configured tuning.");
        UnityEngine.Random.InitState(863);
        double sum = 0, sum2 = 0, yawR = 0, order = 0; const int n = 100000;
        for (int i = 0; i < n; i++)
        {
            float yaw = CircularDirectionDistribution.Sample(0, cfg.kappa.Value), pitch = CircularDirectionDistribution.Sample(0, cfg.kappaElevation.Value);
            sum += yaw; sum2 += yaw * yaw; yawR += Math.Cos(yaw * Math.PI / 180);
            order += Math.Cos(yaw * Math.PI / 180) * Math.Cos(pitch * Math.PI / 180);
        }
        double sd = Math.Sqrt(sum2 / n - Math.Pow(sum / n, 2));
        Check(order / n > .988 && order / n < 1 && sd > 0 && sd < 6.5 && Math.Abs(sum / n) < .1, "At least approximately 99% 3D order, with real spread and unbiased mean.");
        directions = new { samples = n, kappaYaw = cfg.kappa.Value, kappaElevation = cfg.kappaElevation.Value, meanYawDegrees = sum / n, yawStdDevDegrees = sd, yawResultant = yawR / n, threeDimensionalOrder = order / n };
        bool bad = false; try { CircularDirectionDistribution.Sample(0, -1); } catch (ArgumentException) { bad = true; }
        Check(bad, "Invalid concentration is rejected.");
        var window = new CircularHeadingWindow(); window.Add(0, 359, 10); window.Add(1, 1, 10);
        Check(Math.Abs(Mathf.DeltaAngle((float)window.Read().meanHeadingDegrees, 0)) < .001, "359 and 1 degrees average north, rather than south.");
        var host = new GameObject("Legacy direction"); var spawner = host.AddComponent<LocustSpawner>();
        try {
            var method = typeof(LocustSpawner).GetMethod("GenerateVanMisesRotation", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(Mathf.Abs(Mathf.DeltaAngle(((Quaternion)method.Invoke(spawner, new object[] { 37f, 10000f })).eulerAngles.y, 37)) < .001, "Existing legacy point-motion cutoff remains unchanged.");
            spawner.directionDistribution = "VonMises";
            var samples = Enumerable.Range(0, 100).Select(_ => ((Quaternion)method.Invoke(spawner, new object[] { 37f, 10000f })).eulerAngles.y).ToArray();
            Check(samples.Max() - samples.Min() > .1, "Corrected high kappa retains spread above the legacy cutoff.");
        } finally { Object.DestroyImmediate(host); }
    }
    private static void Population()
    {
        var host = new GameObject("Population check"); var observer = new GameObject("Observer");
        var spawner = host.AddComponent<LocustSpawner>(); spawner.boundaryManager = host.AddComponent<BoundaryManager>(); spawner.layerName = "SimulatedLocustsVR1";
        try {
            UnityEngine.Random.InitState(703);
            var cfg = Read(Files[2])["swarm"].ToObject<SwarmConfig>(); cfg.headingReferenceId = ""; cfg.useHeadingReference = false;
            var rig = observer.AddComponent<ClosedLoop>(); SwarmConfiguration.Apply(spawner, cfg, rig, 1);
            Check(spawner.Spawned.Count == 150 && spawner.spawnVolumeSize == Vector3.one * 4, "150 moths in the physical 4 m cube.");
            var driver = host.GetComponent<GeneratedSwarmMotion>(); var sum = new double[6]; int withinRange = 0, close = 0; float nearest = float.MaxValue;
            var rotations = new[] { Quaternion.identity, Quaternion.Euler(0,90,0), Quaternion.Euler(0,180,0), Quaternion.Euler(0,270,0), Quaternion.Euler(-90,0,0), Quaternion.Euler(90,0,0) };
            const int frames = 3600;
            for (int frame = 0; frame < frames; frame++) {
                observer.transform.position += Vector3.forward * (7f / 30); driver.Advance(1f / 30);
                int[] counts = new int[6];
                foreach (var agent in spawner.Spawned) {
                    Vector3 relative = agent.transform.position - observer.transform.position;
                    float distance = relative.magnitude; nearest = Mathf.Min(nearest, distance); if (distance < .1) close++;
                    for (int camera = 0; camera < 6; camera++) {
                        Vector3 p = Quaternion.Inverse(rotations[camera]) * relative;
                        if (p.z > .001 && Mathf.Abs(p.x) <= p.z && Mathf.Abs(p.y) <= p.z) counts[camera]++;
                    }
                }
                for (int camera = 0; camera < 6; camera++) { sum[camera] += counts[camera]; if (counts[camera] >= 20 && counts[camera] <= 30) withinRange++; }
            }
            var means = sum.Select(v => v / frames).ToArray();
            Check(means.All(v => v > 21 && v < 29) && close > 0, "Each square 90 degree camera averages about 25 moths, with unforced close passes.");
            var speeds = spawner.Spawned.Select(a => (double)a.GetComponent<LocustMover>().speed).ToArray();
            Check(speeds.Distinct().Count() > 140, "Individual speeds differ and are sampled once, avoiding synchronous procession.");
            population = new { agentsPerRig = 150, densityPerCubicMetre = 150 / 64.0, durationSeconds = 120, observerMetresPerSecond = 7, meanMothsPerCamera = means,
                fractionCameraFramesWith20To30 = withinRange / (6.0 * frames), nearestMetres = nearest, within10cmSampleCount = close,
                note = "Geometric FOV counts; tiny/subpixel moths are still contrast attenuated. Random counts fluctuate and are not clamped to 20-30." };
        } finally { Object.DestroyImmediate(host); Object.DestroyImmediate(observer); }
    }
    private static void Advance(MainController main)
    {
        typeof(MainController).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, 0f);
        typeof(MainController).GetMethod("ManageTimerAndTransitions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(main, null);
        nextFrame = Time.frameCount + 4;
    }
    private static void CheckCommon(MainController main)
    {
        Check(main.SequenceError == null && rigs.All(r => r != null) && rigs.Select(r => r.GetInstanceID()).SequenceEqual(rigIds), "Same rigs survive block transitions.");
        Check(rigs.All(r => !r.AutoTrim.Enabled), "Trimming stays off after setup.");
        for (int i = 0; i < rigs.Length; i++) Check(Math.Abs(rigs[i].GetYawDCOffset() - (.01f * (i + 1))) < .00001, "Calibrated DC offset survives transitions.");
    }
    private static void CheckSwarms(MainController main)
    {
        var spawners = Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None);
        Check(spawners.Length == 4, "Exactly one swarm per configured rig.");
        foreach (var spawner in spawners) {
            int i = Array.FindIndex(rigs, r => Kannadi.ParseVRIndex(r.name) == Kannadi.ParseVRIndex(spawner.name));
            Check(i >= 0 && spawner.Spawned.Count == 150 && spawner.bogongVisual.shape == "Moth", "Every rig renders 150 physical Bogong moths.");
            Check(Math.Abs(Mathf.DeltaAngle(spawner.mu, (float)baseline[i].meanHeadingDegrees)) < .001, "Both swarm blocks reuse that rig's measured baseline, despite subsequent steering.");
            Check(ReferenceEquals(main.ResolveHeadingReference(rigs[i].gameObject, "milky-way-baseline"), baseline[i]), "Reference is frozen, rather than resampled during swarm.");
            Check(Vector3.Distance(spawner.boundaryManager.Center, rigs[i].transform.position) < .001, "Swarm cube follows its moving animal.");
            Check(spawner.GetComponent<SwarmLogger>()?.LogPath != null, "Embedded swarm coordinate recorder is active.");
        }
    }
    private static void Tick()
    {
        try {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Cycle validation stage " + stage);
            if (Time.frameCount < nextFrame) return;
            if (stage == 0) { SceneManager.LoadScene("ControlScene"); stage = 1; nextFrame = Time.frameCount + 4; return; }
            var main = MainController.Instance; if (main == null) return;
            if (stage == 1 && SceneManager.GetActiveScene().name == "ControlScene") {
                Check(Object.FindObjectsByType<UnityEngine.UI.InputField>(FindObjectsSortMode.None).Length > 0,
                    "Original ControlScene metadata interface is retained.");
                var sequence = JsonConvert.DeserializeObject<SequenceConfig>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json")));
                SwarmHeadingSequenceValidation.Validate(sequence);
                sequence.randomise = true;
                bool shuffled = false; try { SwarmHeadingSequenceValidation.Validate(sequence); } catch (ArgumentException) { shuffled = true; }
                Check(shuffled, "Core validation rejects shuffled heading-dependent blocks without any new GUI.");
                sequence.randomise = false;
                var original = sequence.sequences; sequence.sequences = new[] { original[0], original[2], original[3], original[1] };
                bool misordered = false; try { SwarmHeadingSequenceValidation.Validate(sequence); } catch (ArgumentException) { misordered = true; }
                Check(misordered, "Core validation rejects a swarm before its measurement.");
                sequence.sequences = original; SwarmHeadingSequenceValidation.Validate(sequence);
                var startButton = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).FirstOrDefault(button =>
                    Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(i => button.onClick.GetPersistentMethodName(i) == "StartSequence"));
                Check(startButton != null, "Original metadata Save/Start button is wired to the sequence controller.");
                startButton.onClick.Invoke(); stage = 2; nextFrame = Time.frameCount + 10; return;
            }
            if (SceneManager.GetActiveScene().name != "Choice_Selwyn") return;
            if (main.SequenceError != null) throw new Exception(main.SequenceError);
            if (stage == 2) {
                rigs = Object.FindObjectsByType<ClosedLoop>(FindObjectsSortMode.None).OrderBy(r => Kannadi.ParseVRIndex(r.name)).ToArray();
                rigIds = rigs.Select(r => r.GetInstanceID()).ToArray();
                Check(rigs.Length == 4 && rigs.All(r => r.AutoTrim.Enabled) && main.currentStep == 0, "Setup enables trimming on all four rigs.");
                Check(Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None).Length == 0 && NightSkyController.CurrentSampleUtc != "", "Setup has astronomical sky and no conspecifics.");
                for (int i = 0; i < 4; i++) { rigs[i].SetYawDCOffset(.01f * (i + 1)); rigs[i].transform.rotation = Quaternion.Euler(0, Headings[i], 0); }
                stage = 3; Advance(main); observationStarted = Time.timeAsDouble; remainingAtObservation = main.RemainingStepSeconds; return;
            }
            if (stage == 3) {
                CheckCommon(main);
                Check(main.currentStep == 1 && Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None).Length == 0, "Second block has no moths.");
                for (int i = 0; i < 4; i++) rigs[i].transform.rotation = Quaternion.Euler(0, i == 0 ? (Time.frameCount % 2 == 0 ? 359 : 1) : Headings[i], 0);
                if (main.AssessmentPending) {
                    Check(main.RemainingStepSeconds < remainingAtObservation, "Concurrent heading assessment does not pause the 240-second block timer.");
                    return;
                }
                Check(Time.timeAsDouble - observationStarted < 10, "Measurement completes without a second presentation period.");
                baseline = rigs.Select(r => main.ResolveHeadingReference(r.gameObject, "milky-way-baseline")).ToArray();
                for (int i = 0; i < 4; i++) Check(Math.Abs(Mathf.DeltaAngle((float)baseline[i].meanHeadingDegrees, Headings[i])) < 8, "Per-rig circular heading is independently measured.");
                for (int i = 0; i < 4; i++) rigs[i].transform.rotation = Quaternion.Euler(0, Headings[i] + 80, 0);
                stage = 4; Advance(main); return;
            }
            if (stage == 4) {
                CheckCommon(main); CheckSwarms(main);
                Check(main.currentStep == 2 && NightSkyController.CurrentSampleUtc != "", "Third block combines live astronomical sky and swarm.");
                stage = 5; Advance(main); return;
            }
            if (stage == 5) {
                CheckCommon(main); CheckSwarms(main);
                Check(main.currentStep == 3 && NightSkyController.CurrentSampleUtc == "" && NightSkyController.CurrentId != "", "Fourth block has archived uniform gray sky, without Milky Way.");
                Check((bool)JObject.Parse(File.ReadAllText(MasterDataLogger.Instance.ConfigArchive.ManifestPath))["complete"], "System, sequence and all four experiment configs are archived before presentation.");
                performance = GeneratedSwarmValidation.Benchmark(Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None));
                stage = 6; Advance(main); return;
            }
            if (stage == 6) {
                Check(main.currentStep == 1 && main.currentTrial == 1 && rigs.All(r => !r.AutoTrim.Enabled), "Cycle repeats from Milky Way, without re-enabling setup trimming.");
                Check(Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None).Length == 0, "Old swarm is fully destroyed at loop restart.");
                bool missing = false; try { main.ResolveHeadingReference(rigs[0].gameObject, "milky-way-baseline"); } catch (InvalidOperationException) { missing = true; }
                Check(missing, "Previous cycle's reference cannot leak into a new cycle.");
                Check(main.AssessmentPending, "Each repeat measures a fresh Milky Way mean while trimming stays off.");
                main.StopSequence();
                Check(errors.Count == 0, "No unexpected Unity errors: " + string.Join("; ", errors)); Finish(null);
            }
        } catch (Exception e) { Finish(e.ToString()); }
    }
    private static void Finish(string failure)
    {
        EditorApplication.update -= Tick; SceneManager.sceneLoaded -= DisableInputs; Application.logMessageReceived -= OnLog; SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Application.dataPath, "../selwyn-swarm-cycle-validation.json"), JsonConvert.SerializeObject(new { checks, failure, errors, directions, population, performance, unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName }, Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
#endif
