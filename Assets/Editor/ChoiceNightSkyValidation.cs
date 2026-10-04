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

/// <summary>Choice sky-only configuration, actual rendering, audit and transition regression.</summary>
[InitializeOnLoad]
public static class ChoiceNightSkyValidation
{
    private const string Key = "ChoiceNightSkyValidation.Active";
    private const string Fixture = "choice-night-sky-validation.json";
    private static int checks, frames;
    private static double deadline;
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }

    static ChoiceNightSkyValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            {
                deadline = EditorApplication.timeSinceStartup + 90;
                EditorApplication.update += Validate;
            }
        };
        SceneManager.sceneLoaded += (scene, mode) => {
            if (!SessionState.GetBool(Key, false)) return;
            foreach (var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) listener.enabled = false;
        };
    }

    public static void Run() => Start("Choice", Fixture);
    // Optional smoke check for the local terrain scene; the generic regression needs no terrain assets.
    public static void RunSelwyn() => Start("Choice_Selwyn", "Kannadi/selwyn-night-sky.json");

    private static void Start(string scene, string configFile)
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY")))
            throw new InvalidOperationException("Use a disposable project copy with KANNADI_VALIDATION_COPY.");
        if (configFile == Fixture)
            File.WriteAllText(Path.Combine(Application.streamingAssetsPath, Fixture), JsonConvert.SerializeObject(new {
                nightSky = new NightSkyConfig { latitude = -35.894409, longitude = 148.453948,
                    utcOffsetHours = 11, exposure = .5f, faintDetailCutoff = .15f }, aglHeight = 1000
            })); // Deliberately omit objects, as in the original Selwyn config.
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json"), JsonConvert.SerializeObject(new {
            autoStart = true, loop = false, sequences = new[] { new {
                sceneName = scene, duration = 3600, parameters = new { configFile }
            } }
        }));
        File.Copy(Path.Combine(Application.streamingAssetsPath, "Templates/system-config-six-cameras.template.json"),
            Path.Combine(Application.streamingAssetsPath, "system_config.json"), true);
        SessionState.SetString(Key + ".scene", scene);
        SessionState.SetString(Key + ".config", configFile);
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");
        EditorApplication.isPlaying = true;
    }

    private static Dictionary<string, object> Parameters(string file) => new Dictionary<string, object> { { "configFile", file } };

    private static void Validate()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Choice night-sky startup timed out.");
            if (SceneManager.GetActiveScene().name != SessionState.GetString(Key + ".scene", "")) return;
            if (++frames < 20) return;
            string configFile = SessionState.GetString(Key + ".config", "");
            var config = JsonConvert.DeserializeObject<SceneConfig>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, configFile)));
            var controller = Object.FindFirstObjectByType<ChoiceController>();
            var sky = controller.GetComponent<NightSkyController>();
            Check(sky != null && NightSkyController.CurrentId != "", "Choice generates the sky when objects is omitted.");
            string first = NightSkyController.CurrentId;
            var sample = DateTimeOffset.Parse(NightSkyController.CurrentSampleUtc);
            Check(Math.Abs((DateTimeOffset.UtcNow - sample).TotalSeconds) < 60, "Sky uses the current instant.");
            string archive = Path.Combine(MasterDataLogger.Instance.directoryPath, "Skyboxes");
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(archive, first + ".json")));
            var settings = manifest["description"]["settings"];
            Check((double)settings["latitude"] == config.nightSky.latitude && (double)settings["longitude"] == config.nightSky.longitude, "Observer coordinates are applied and archived.");
            Check((double)settings["utcOffsetHours"] == 11 && (float)settings["exposure"] == .5f, "UTC offset and brightness survive Choice deserialization.");
            Check((int)manifest["width"] == 4096 && (int)manifest["height"] == 2048, "4K equirectangular image is archived.");
            Check(File.Exists(Path.Combine(archive, first + ".png")), "Panorama exists in RunData.");
            var rigs = Object.FindObjectsByType<ViewportSetter>(FindObjectsSortMode.None).OrderBy(r => r.name).ToArray();
            Check(rigs.Length == 4, "Four rigs load.");
            var sheet = new Texture2D(768, 512, TextureFormat.RGB24, false);
            int row = 0;
            foreach (var rig in rigs)
            {
                var cameras = rig.GetComponentsInChildren<Camera>();
                Check(cameras.Length == 6 && cameras.All(c => c.enabled), rig.name + " enables six cameras.");
                int col = 0;
                foreach (char face in "DRBLFU")
                {
                    var camera = cameras.Single(c => c.name == "Main Camera " + face);
                    Check(camera.clearFlags == CameraClearFlags.Skybox, camera.name + " uses skybox clear flags.");
                    var local = camera.GetComponent<Skybox>();
                    Check(local == null || !local.enabled || local.material == null, camera.name + " uses the generated sky.");
                    var target = RenderTexture.GetTemporary(128, 128, 24);
                    var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
                    var rect = camera.rect; float aspect = camera.aspect;
                    var capture = new Texture2D(128, 128, TextureFormat.RGB24, false);
                    try
                    {
                        camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1); camera.aspect = 1;
                        camera.Render(); RenderTexture.active = target;
                        capture.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); capture.Apply();
                        var pixels = capture.GetPixels();
                        sheet.SetPixels(col * 128, (3 - row) * 128, 128, 128, pixels);
                        if (face == 'U') Check(pixels.Max(p => p.maxColorComponent) - pixels.Min(p => p.maxColorComponent) > .01f, rig.name + " renders visible star detail above the terrain.");
                    }
                    finally
                    {
                        camera.targetTexture = previousTarget; camera.rect = rect; camera.aspect = aspect;
                        RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.Destroy(capture);
                    }
                    col++;
                }
                row++;
            }
            sheet.Apply(); File.WriteAllBytes(Path.Combine(Application.dataPath, "../choice-night-sky-six-faces.png"), sheet.EncodeToPNG()); Object.Destroy(sheet);
            typeof(NightSkyController).GetField("startRealtime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sky, Time.realtimeSinceStartupAsDouble - 1801);
            typeof(NightSkyController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sky, null);
            Check(NightSkyController.CurrentId != first && DateTimeOffset.Parse(NightSkyController.CurrentSampleUtc) == sample.AddMinutes(30), "Choice regenerates after 30 minutes.");
            Check(File.ReadAllLines(Path.Combine(archive, "loads.csv")).Length == 3, "Both activations are logged.");
            // File precedence and transitions must clear generated IDs and restore local overrides.
            string transition = "choice-night-sky-transition-validation.json";
            File.WriteAllText(Path.Combine(Application.streamingAssetsPath, transition), "{\"skyboxPath\":\"Photosphere/adaminaby.png\",\"nightSky\":{\"enabled\":true}}");
            controller.AdvanceStep(Parameters(transition));
            Check(NightSkyController.CurrentId == "" && RenderSettings.skybox.mainTexture.width == 3024, "Legacy explicit image wins and clears the generated ID.");
            controller.AdvanceStep(Parameters(configFile));
            Check(NightSkyController.CurrentId != "" && RenderSettings.skybox.mainTexture.width == 4096, "Generated sky can follow a legacy image.");
            File.WriteAllText(Path.Combine(Application.streamingAssetsPath, transition), "{\"objects\":null,\"nightSky\":{\"enabled\":false}}");
            controller.AdvanceStep(Parameters(transition));
            Check(NightSkyController.CurrentId == "", "Disabled sky works with explicit null objects.");
            Check(rigs.SelectMany(r => r.GetComponentsInChildren<Skybox>()).Any(s => s.enabled), "Clearing generated sky restores local overrides.");
            controller.AdvanceStep(Parameters(configFile));
            Object.DestroyImmediate(controller);
            Check(NightSkyController.CurrentId == "", "Destroying Choice clears the generated sky and ID.");
            Finish(null);
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void Finish(string failure)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Validate;
        File.WriteAllText(Path.Combine(Application.dataPath, "../choice-night-sky-validation.json"),
            JsonConvert.SerializeObject(new { checks, failure, scene = SessionState.GetString(Key + ".scene", ""), unity = Application.unityVersion }, Formatting.Indented));
        if (failure != null) Debug.LogError(failure);
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
#endif
