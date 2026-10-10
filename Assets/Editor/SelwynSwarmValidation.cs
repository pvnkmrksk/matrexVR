#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>Reference raster comparisons, four-rig render timings and recorded-input trim replay.</summary>
public static class SelwynSwarmValidation
{
    private static int checks;
    private static readonly List<object> renders = new List<object>();
    private static readonly List<object> replays = new List<object>();
    private static readonly List<object> tuning = new List<object>();
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Use a disposable project.");
        string failure = null;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try
        {
            Replay();
            foreach (int count in new[] { 64, 256 }) RenderComparison(count);
        }
        catch (Exception error) { failure = error.ToString(); Debug.LogError(failure); }
        File.WriteAllText(Path.Combine(Application.dataPath, "../selwyn-swarm-validation.json"),
            JsonConvert.SerializeObject(new { checks, failure, renders, replays, tuning, unity = Application.unityVersion,
                graphics = SystemInfo.graphicsDeviceName, note = "Isolated Camera.Render CPU timings, four rigs/six 128px cameras each. Not LED workstation FPS. Recorded replay cannot model animal adaptation." }, Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }

    public static void TuneReplay()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Use a disposable project.");
        foreach (double aggression in new[] { .25, .35, .5, .65 })
        foreach (double settle in new[] { 3.0, 5.0 })
        {
            replays.Clear();
            Replay(new AutoTrimConfig { flightVarianceThreshold = .01, aggressiveness = aggression,
                settleSeconds = settle, maxStepRadians = .04 });
            tuning.Add(new { aggression, settle, replays = replays.ToArray() });
        }
        File.WriteAllText(Path.Combine(Application.dataPath, "../auto-trim-tuning.json"), JsonConvert.SerializeObject(tuning, Formatting.Indented));
        EditorApplication.Exit(0);
    }

    private static void Replay(AutoTrimConfig overrideSettings = null)
    {
        var sequence = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Examples/Sequences/kinefly-auto-trim.example.json")));
        var row = sequence["sequences"][0];
        var settings = overrideSettings ?? row["autoTrimSettings"].ToObject<AutoTrimConfig>();
        float gain = (float)row["gain"];
        foreach (string file in Directory.GetFiles(Path.Combine(Application.dataPath, "../recording-replay"), "*.csv"))
        {
            var trim = new KineflyAutoTrim(); trim.Configure(true, settings);
            float offset = 0; int sequenceId = 0; double start = -1, end = 0, flyingSeconds = 0, previous = 0;
            var tail = new List<double>(); var corrections = new List<object>();
            foreach (string line in File.ReadLines(file).Skip(1))
            {
                string[] fields = line.Split(',');
                if (fields[2] != "preStimulus") continue;
                double originalTime = double.Parse(fields[0], CultureInfo.InvariantCulture);
                if (start < 0) start = originalTime;
                double time = originalTime - start;
                if (time > 240) break;
                double yaw = double.Parse(fields[1], CultureInfo.InvariantCulture);
                if (trim.Tick(time, sequenceId++, yaw, true, true, true, gain, offset, out float next))
                {
                    Check(Math.Abs(next - offset) <= settings.maxStepRadians + 1e-6, "Replay step remains bounded");
                    corrections.Add(new { time, from = offset, to = next, median = trim.EffectiveMedianDegPerSecond });
                    offset = next;
                }
                if (trim.Flying) flyingSeconds += time - previous;
                if (time >= 220 && trim.Flying) tail.Add(yaw);
                previous = end = time;
            }
            Check(end > 239, "Complete 240-second star calibration recording available");
            tail.Sort();
            double median = tail.Count % 2 == 0 ? .5 * (tail[tail.Count / 2 - 1] + tail[tail.Count / 2]) : tail[tail.Count / 2];
            replays.Add(new { source = Path.GetFileName(file), duration = end, offset, passes = trim.Passes, flyingSeconds,
                final20SecondEffectiveMedianDegPerSecond = gain * (median - offset) * Mathf.Rad2Deg,
                corrections, state = trim.State });
            // No flight labels: validate mechanics/coverage, not an invented accuracy score.
            Check(flyingSeconds > 180, "Recorded active signal permits calibration rather than permanently pausing");
            Check(trim.Passes >= 1, "Historical input drives a real correction");
        }
        var stopped = new KineflyAutoTrim(); stopped.Configure(true, settings);
        for (int i = 0; i < 12000; i++)
            Check(!stopped.Tick(i * .02, i, 1 + .01 * Math.Sin(i), true, true, true, gain, .1f, out _), "Low-variance biased nonflight cannot trim");
        Check(stopped.FlightReady && !stopped.Flying, "Low-motion status remains not flying");
    }

    private static void RenderComparison(int count)
    {
        var cameras = new List<Camera>(); var targets = new List<RenderTexture>();
        var legacy = new List<Bogong>(); var batches = new List<BogongSwarmRenderer>();
        var agents = new List<GameObject>(); var allRoots = new List<GameObject>();
        var config = new BogongVisualConfig { sizeMode = "Angular", angularSizeDegrees = 2,
            color = new ColorConfig { r = .01f, g = .01f, b = .01f, a = 1 } };
        var random = new System.Random(521);
        for (int rig = 0; rig < 4; rig++)
        {
            int layer = 25 + rig;
            var own = new List<GameObject>();
            Vector3 origin = new Vector3(rig * 500, 100, 0);
            for (int i = 0; i < count; i++)
            {
                var agent = GameObject.CreatePrimitive(PrimitiveType.Quad); agent.layer = layer;
                agent.transform.position = origin + new Vector3((float)random.NextDouble() * 120 - 60,
                    (float)random.NextDouble() * 40 + 30, (float)random.NextDouble() * 120 - 60);
                agent.transform.rotation = Quaternion.Euler(5, 270, 0);
                var dot = agent.AddComponent<Bogong>(); dot.Configure(config);
                dot.SendMessage("OnDisable"); dot.SendMessage("OnEnable");
                legacy.Add(dot); own.Add(agent); agents.Add(agent); allRoots.Add(agent);
            }
            var host = new GameObject("Batch rig " + rig); host.layer = layer;
            // Prove host transforms cannot move the world-coordinate stimulus centers.
            host.transform.SetPositionAndRotation(new Vector3(33, 40, 50), Quaternion.Euler(23, 45, 6));
            host.transform.localScale = Vector3.one * 2;
            var batch = host.AddComponent<BogongSwarmRenderer>(); batch.Configure(own, config);
            batch.SendMessage("OnDisable"); batch.gameObject.SetActive(false);
            batches.Add(batch); allRoots.Add(host);
            foreach (Quaternion rotation in new[] { Quaternion.identity, Quaternion.Euler(0,90,0), Quaternion.Euler(0,180,0),
                Quaternion.Euler(0,270,0), Quaternion.Euler(-90,0,0), Quaternion.Euler(90,0,0) })
            {
                var root = new GameObject("Rig camera"); var camera = root.AddComponent<Camera>();
                camera.enabled = false; camera.transform.SetPositionAndRotation(origin, rotation);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
                camera.fieldOfView = 90; camera.aspect = 1; camera.nearClipPlane = .01f; camera.farClipPlane = 2000;
                camera.cullingMask = 1 << layer; camera.allowHDR = false; camera.allowMSAA = false;
                var target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
                cameras.Add(camera); targets.Add(target); allRoots.Add(root);
            }
        }
        try
        {
            var expected = cameras.Select(Capture).ToArray();
            foreach (var dot in legacy) dot.SendMessage("OnDisable");
            foreach (var batch in batches) { batch.gameObject.SetActive(true); batch.SendMessage("OnDisable"); batch.SendMessage("OnEnable"); }
            int compared = 0;
            for (int c = 0; c < cameras.Count; c++)
            {
                var actual = Capture(cameras[c]);
                for (int pixel = 0; pixel < actual.Length; pixel++)
                {
                    Check(actual[pixel].Equals(expected[c][pixel]), "Batch raster differs: " + count + " agents camera " + c + " pixel " + pixel);
                    compared++;
                }
            }
            double[] batchTimes = Measure(cameras, agents, batches);
            foreach (var batch in batches) { batch.SendMessage("OnDisable"); batch.gameObject.SetActive(false); }
            foreach (var dot in legacy) dot.SendMessage("OnEnable");
            double[] legacyTimes = Measure(cameras, agents, null);
            foreach (var dot in legacy) dot.SendMessage("OnDisable");
            renders.Add(new { agentsPerRig = count, rigs = 4, cameras = cameras.Count, comparedPixels = compared,
                legacyMedianMs = Median(legacyTimes), batchedMedianMs = Median(batchTimes),
                legacyP95Ms = Percentile(legacyTimes, .95), batchedP95Ms = Percentile(batchTimes, .95),
                legacyMaterials = legacy.Count, batchedMaterials = batches.Count,
                legacyCameraCallbacks = legacy.Count, batchedCameraCallbacks = batches.Count });
            Check(Median(batchTimes) < Median(legacyTimes), "Batching reduces measured render CPU time");
        }
        finally
        {
            foreach (var camera in cameras) camera.targetTexture = null;
            foreach (var target in targets) { target.Release(); Object.DestroyImmediate(target); }
            foreach (var root in allRoots) Object.DestroyImmediate(root);
        }
    }

    private static Color32[] Capture(Camera camera)
    {
        camera.Render(); var previous = RenderTexture.active; RenderTexture.active = camera.targetTexture;
        var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); pixels.Apply();
        var result = pixels.GetPixels32(); Object.DestroyImmediate(pixels); RenderTexture.active = previous; return result;
    }
    private static double[] Measure(List<Camera> cameras, List<GameObject> agents, List<BogongSwarmRenderer> batches)
    {
        var times = new List<double>();
        for (int frame = 0; frame < 42; frame++)
        {
            var stopwatch = Stopwatch.StartNew();
            foreach (var agent in agents) agent.transform.position += new Vector3(.001f, 0, 0);
            if (batches != null) foreach (var batch in batches) batch.RefreshCenters();
            foreach (var camera in cameras) camera.Render();
            stopwatch.Stop(); if (frame >= 12) times.Add(stopwatch.Elapsed.TotalMilliseconds);
        }
        return times.OrderBy(t => t).ToArray();
    }
    private static double Median(double[] values) => .5 * (values[values.Length / 2 - 1] + values[values.Length / 2]);
    private static double Percentile(double[] values, double p) => values[(int)Math.Floor((values.Length - 1) * p)];
}
#endif
