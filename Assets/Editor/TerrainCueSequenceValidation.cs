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

// Exercise ControlScene, config archives and real in-place sequence transitions.
[InitializeOnLoad]
public static class TerrainCueSequenceValidation
{
    private const string Key = "TerrainCueSequenceValidation.Active";
    private static double deadline;
    private static int checks;
    private static readonly HashSet<string> seen = new HashSet<string>();
    private static readonly List<string> errors = new List<string>();
    static TerrainCueSequenceValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
            Application.logMessageReceived += (message, trace, kind) => {
                if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message);
            };
        };
        SceneManager.sceneLoaded += (scene, mode) => {
            if (!SessionState.GetBool(Key, false)) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var listener in root.GetComponentsInChildren<ZmqListener>(true)) listener.enabled = false;
        };
    }
    public static void Run()
    {
        if (!File.Exists("KANNADI_VALIDATION_COPY")) throw new Exception("Use a disposable project copy.");
        var path = Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json");
        var sequence = JObject.Parse(File.ReadAllText(path));
        if (sequence["sequences"].Count() != 7 || sequence["sequences"].Any(s => s.Value<string>("sceneName") != "TerrainCueLab"))
            throw new Exception("Active config must be the seven-condition TerrainCueLab sequence.");
        foreach (var row in sequence["sequences"]) row["duration"] = 1.2f;
        sequence["loop"] = false;
        File.WriteAllText(path, sequence.ToString()); // Only the marked disposable copy.
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");
        EditorApplication.isPlaying = true;
    }
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Terrain cue sequence timed out.");
            var main = MainController.Instance;
            if (main == null) return;
            if (main.SequenceError != null) throw new Exception("Sequence error: " + main.SequenceError);
            if (!main.SequenceRunning || main.RemainingStepSeconds > .9f) return;
            var world = Object.FindFirstObjectByType<RugosityWorld>();
            var rig = Object.FindFirstObjectByType<RugosityRig>();
            if (world == null || rig == null) return;
            string file = main.GetCurrentSequenceStep().parameters["configFile"].ToString();
            if (seen.Contains(file)) return;
            var config = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, file)))["rugosity"].ToObject<RugositySettings>();
            Check(world.IsReady, "Loaded world is ready");
            Check(world.settings.visualSurface == config.visualSurface && world.settings.collisionSurface == config.collisionSurface, "Sequence applies mesh assignments");
            Check(rig.GetComponent<Keyboard>().AutopilotEnabled && rig.transform.position.x > -59.8f, "Play starts actual left-to-right movement");
            world.HeightSurface.Sample(rig.transform.position, out float ground, out _);
            float y = config.heightMode == RugosityHeightMode.AboveGround ? ground + 1 : config.absoluteHeight;
            Check(Mathf.Abs(rig.transform.position.y - y) < .001f, "Sequence LateUpdate follows configured height");
            Check(Mathf.Abs(Mathf.DeltaAngle(rig.transform.eulerAngles.y, 90)) < .01f, "Sequence preserves left-to-right heading");
            Check(world.surfaces.All(s => s.renderers.All(r => r.enabled == (s.surfaceId == config.visualSurface)) &&
                s.colliders.All(c => c.enabled == (s.surfaceId == config.collisionSurface))), "Sequence selects actual renderer and collider roles");
            seen.Add(file);
            if (seen.Count != 7) return;
            Check(errors.Count == 0, "Runtime errors: " + string.Join(";", errors));
            Directory.CreateDirectory("docs");
            File.WriteAllText("docs/terrain-cue-sequence-validation.json", JsonConvert.SerializeObject(new {
                passed = true, checks, unity = Application.unityVersion, conditions = seen.OrderBy(s => s),
                scope = "Actual ControlScene auto-start and seven in-place transitions; only durations and loop shortened in disposable copy. No hardware."
            }, Formatting.Indented));
            Debug.Log("TERRAIN CUE SEQUENCE PASSED: " + checks);
            Finish(0);
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }
    private static void Finish(int code)
    { SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.Exit(code); }
}
