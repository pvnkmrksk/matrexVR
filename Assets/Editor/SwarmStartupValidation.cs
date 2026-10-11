#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Exercise the committed ControlScene startup recipe in a disposable project.</summary>
[InitializeOnLoad]
public static class SwarmStartupValidation
{
    private const string Key = "SwarmStartupValidation.Active";
    private static readonly List<string> errors = new List<string>();
    private static int checks, stage, nextFrame;
    private static double deadline;
    private static object encounterAudit;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }

    static SwarmStartupValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 90;
            SceneManager.sceneLoaded += DisableInputs;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        };
    }
    private static void DisableInputs(Scene scene, LoadSceneMode mode)
    {
        foreach (var input in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) input.enabled = false;
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (stack.Contains("UnityEditor.Search.SearchDatabase") || message == "No backup data file found.") return;
        errors.Add(message + "\n" + stack);
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY")))
            throw new Exception("Disposable project only.");
        var system = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Templates/system_config.template.json")));
        system["telemetry"]["enabled"] = false;
        system["overheadCamera"]["enabled"] = false;
        system["configs"][0]["displayOrder"] = "RBLF";
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "system_config.json"), system.ToString());
        // Leave sequenceConfig and its referenced experiment untouched: this tests the actual startup files.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Startup stage " + stage);
            if (Time.frameCount < nextFrame) return;
            if (stage == 0)
            {
                SceneManager.LoadScene("ControlScene"); stage = 1; nextFrame = Time.frameCount + 4; return;
            }
            var main = MainController.Instance;
            var populations = Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None);
            if (SceneManager.GetActiveScene().name != "Swarm" || main == null || populations.Length != 4 ||
                populations.Any(s => s.Spawned.Count != 430)) return;
            Check(main.VRClosedLoops.Count == 4 && main.SequenceError == null, "ControlScene auto-starts Swarm with four configured rigs.");
            var recipe = KannadiConfig.Load(new Dictionary<string, object> { ["configFile"] = "Examples/Swarm/selwyn-trajectory-playback.example.json" });
            Check(recipe.mu == 0 && recipe.playback.continueAfterTrack && !recipe.playback.periodicBoundary && recipe.playback.continuationSpeedMetresPerSecond==6,
                "North recipe enables world-space continuation without wrapping.");
            var data = SwarmTrajectoryData.Load(Path.Combine(Application.streamingAssetsPath, recipe.trajectoryFile));
            Vector3 displacement = Vector3.zero;
            foreach (var track in data.Tracks) displacement += track.samples.Last().position - track.samples.First().position;
            var yaw = Quaternion.Euler(0, recipe.playback.yawOffsetDegrees, 0);
            Vector3 north = yaw * displacement;
            Check(north.z > 0 && Math.Abs(Mathf.Atan2(north.x, north.z) * Mathf.Rad2Deg) < .001,
                "Aggregate measured trajectory displacement is north 0 degrees after presentation yaw.");
            Check(RenderSettings.skybox != null && RenderSettings.skybox.shader.name == "NightSky/Panorama",
                "Uniform sky replaces the authored scene skybox.");
            var texture = (Texture2D)RenderSettings.skybox.mainTexture;
            Check(texture.width == 2 && texture.GetPixels().All(c => Math.Abs(c.r - .5f) < .01f &&
                Math.Abs(c.g - .5f) < .01f && Math.Abs(c.b - .5f) < .01f && c.a == 1), "Sky is opaque uniform gray.");
            foreach (var population in populations)
            {
                var rig = population.GetComponent<ClosedLoop>();
                int index = Kannadi.ParseVRIndex(rig.name);
                Check(population.worldUnitsPerMetre == 1 && population.Spawned.All(a => !a.GetComponent<LocustMover>().enabled),
                    "VR" + index + " uses metre CSV playback instead of generated movers.");
                Check(Mathf.Abs(Mathf.DeltaAngle(rig.transform.eulerAngles.y, 0)) < .001 && Mathf.Abs(rig.transform.position.y - 5) < .001,
                    "VR" + index + " starts at north 0 degrees and height 5m.");
                var keyboard = rig.GetComponent<Keyboard>();
                Check(keyboard.AutopilotEnabled == recipe.autopilotEnabled && keyboard.AutopilotSpeed == recipe.autopilotSpeed,
                    "VR" + index + " observer motion respects the active experiment recipe.");
                keyboard.enabled = false;
                var cameras = rig.GetComponentsInChildren<Camera>(true).Where(c => c.name.StartsWith("Main Camera ")).ToArray();
                int all = LayerMask.GetMask("SimulatedLocustsVR1", "SimulatedLocustsVR2", "SimulatedLocustsVR3", "SimulatedLocustsVR4");
                int own = LayerMask.GetMask("SimulatedLocustsVR" + index);
                Check(cameras.All(c => (c.cullingMask & all) == own && Math.Abs(c.nearClipPlane - .001f) < 1e-6),
                    "VR" + index + " sees only its own moths and reaches the 1mm near field.");
                Check(cameras.Where(c => c.name.EndsWith("U") || c.name.EndsWith("D")).All(c => c.enabled == (index != 1)),
                    "VR" + index + " respects absent system camera letters.");
                var replay = population.GetComponent<SwarmTrajectoryPlayback>(); replay.enabled = false; replay.Advance(0);
                Check(Vector3.Distance(replay.SpawnVolumeCentroid, rig.transform.position) < .00001,
                    "VR" + index + " birth-volume centroid equals the current animal position.");
                var track = data.Tracks.Single(t => t.samples[0].time == 0);
                var first = population.Spawned.Single(a => a.activeSelf && a.name.EndsWith("_ID_" + track.id));
                Vector3 origin = rig.transform.position;
                Check(Vector3.Distance(first.transform.position, origin + yaw * (track.samples[0].position - data.Centroid)) < .00001,
                    "VR" + index + " initial birth uses the centered measured position.");
                Vector3 before = first.transform.position;
                Check(track.TrySample(.01, .1, out Vector3 p1, out _), "First recorded track interpolates at 10ms.");
                rig.transform.position += Vector3.right * 10; replay.Advance(.01);
                Check(Vector3.Distance(first.transform.position - before, yaw * (p1 - track.samples[0].position)) < .0001,
                    "VR" + index + " existing moth stays anchored when the animal moves.");
                var batches = population.GetComponentsInChildren<BogongSwarmRenderer>();
                Check(batches.Length == 1 && batches[0].GetComponents<MeshRenderer>().Length == 1,
                    "VR" + index + " uses one shared moth renderer.");
            }
            encounterAudit=SwarmEncounterAudit.Run(populations.Single(p=>p.layerName=="SimulatedLocustsVR1"),data,Check);
            Check(errors.Count == 0, "No startup runtime errors: " + string.Join("; ", errors));
            Finish(null);
        }
        catch (Exception error) { Finish(error.ToString()); }
    }
    private static void Finish(string failure)
    {
        EditorApplication.update -= Tick; SceneManager.sceneLoaded -= DisableInputs; Application.logMessageReceived -= OnLog;
        SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Application.dataPath, "../swarm-startup-validation.json"),
            JsonConvert.SerializeObject(new { checks, failure, errors, encounterAudit, unity = Application.unityVersion }, Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
#endif
