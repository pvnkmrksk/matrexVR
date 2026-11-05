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
using Object = UnityEngine.Object;

/// <summary>Real Choice_desync rig expansion and rendered intertrial sky for saved four-rig protocols.</summary>
public static class DynamicChoiceCameraValidation
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> checks = new();
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks.Add(message); }

    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY")))
            throw new Exception("Use a marked disposable project.");
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../validation-output/dynamic-cameras"));
        Directory.CreateDirectory(output);
        string failure = null;
        try
        {
            foreach (int count in new[] { 5, 8, 16 })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/Choice_desync.unity");
                var template = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,
                    "Experiments/PathAnglePilot/system.preview.json")))["configs"][0];
                var configs = new Dictionary<string, SystemConfig>();
                for (int index = 1; index <= count; index++)
                {
                    var rig = (JObject)template.DeepClone();
                    rig["vrId"] = "VR" + index;
                    rig["desktopPreview"] = false;
                    rig["startCol"] = (index - 1) % 4 * 128;
                    rig["startRow"] = (index - 1) / 4 * 128;
                    configs.Add("VR" + index, rig.ToObject<SystemConfig>());
                }
                RuntimeRigRegistry.Configure(scene, configs);
                var rigs = RuntimeRigRegistry.Rigs(scene);
                Check(rigs.Length == count, "Real scene expands to " + count + " rigs");
                var controller = Object.FindFirstObjectByType<DynamicSequenceController>();
                typeof(DynamicSequenceController).GetMethod("Awake", Hidden).Invoke(controller, null);
                typeof(DynamicSequenceController).GetMethod("CachePlayers", Hidden).Invoke(controller, null);
                var apply = typeof(DynamicSequenceController).GetMethod("ApplyCameraSettings", Hidden);
                Check(RenderSettings.skybox != null, "Authored intertrial sky material exists at " + count + " rigs");

                foreach (string protocol in new[] { "PathAnglePilot", "HistoricalReplicationPreview" })
                {
                    var design = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,
                        "Experiments", protocol, "design.json")));
                    var intertrial = design["intertrial"]["camera"].ToObject<DynamicSequenceController.CameraSpec[]>();
                    foreach (var rig in rigs)
                    {
                        string id = "VR" + RuntimeRigRegistry.ParseIndex(rig.name);
                        var cameras = rig.GetComponentsInChildren<Camera>(true);
                        Check(cameras.Length == 6, protocol + " " + id + " retains six camera faces at count " + count);
                        foreach (var camera in cameras) camera.clearFlags = CameraClearFlags.SolidColor;
                        apply.Invoke(controller, new object[] { id, intertrial });
                        Check(cameras.All(camera => camera.clearFlags == CameraClearFlags.Skybox),
                            protocol + " intertrial sky reaches every face of " + id + " at count " + count);
                        var front = cameras.Single(camera => camera.name == "Main Camera F");
                        if (id == "VR1" || id == "VR5" || id == "VR16")
                        {
                            var sky = Render(front);
                            front.clearFlags = CameraClearFlags.SolidColor;
                            var solid = Render(front);
                            front.clearFlags = CameraClearFlags.Skybox;
                            Check(sky.GetPixels32().Zip(solid.GetPixels32(), (a, b) => !a.Equals(b)).Count(different => different) > 100,
                                protocol + " " + id + " renders sky pixels, not the solid background, at count " + count);
                            File.WriteAllBytes(Path.Combine(output, protocol + "-" + count + "-" + id + "-intertrial.png"), sky.EncodeToPNG());
                            Object.DestroyImmediate(sky); Object.DestroyImmediate(solid);
                        }
                        foreach (var step in design["steps"])
                        {
                            var specs = step["camera"].ToObject<DynamicSequenceController.CameraSpec[]>();
                            apply.Invoke(controller, new object[] { id, specs });
                            Check(cameras.All(camera => camera.clearFlags == specs[0].clearFlags),
                                protocol + " " + step["name"] + " background reaches " + id + " at count " + count);
                            apply.Invoke(controller, new object[] { id, intertrial });
                            Check(cameras.All(camera => camera.clearFlags == CameraClearFlags.Skybox),
                                protocol + " returns " + id + " to the sky after " + step["name"] + " at count " + count);
                        }
                    }
                    var vr5 = rigs.Single(rig => RuntimeRigRegistry.ParseIndex(rig.name) == 5);
                    foreach (var camera in vr5.GetComponentsInChildren<Camera>(true)) camera.clearFlags = CameraClearFlags.SolidColor;
                    var replay = controller.RecreateRecordedStep("VR5", (JObject)design["intertrial"], controller.transform, vr5.transform);
                    Check(vr5.GetComponentsInChildren<Camera>(true).All(camera => camera.clearFlags == CameraClearFlags.Skybox),
                        protocol + " original-controller replay applies intertrial sky to VR5 at count " + count);
                    Object.DestroyImmediate(replay.gameObject);
                }
            }
        }
        catch (Exception error) { failure = error.GetBaseException().ToString(); }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        File.WriteAllText(Path.Combine(output, "results.json"), JsonConvert.SerializeObject(new {
            passed = failure == null, failure, checks, unity = Application.unityVersion,
            scope = "Actual scene, rig cloning, cameras and rendered sky at 5/8/16 rigs; saved pilot/historical protocols and replay reconstruction. No tracker or physical display hardware."
        }, Formatting.Indented));
        if (failure != null) throw new Exception(failure);
    }

    private static Texture2D Render(Camera camera)
    {
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var previousRect = camera.rect;
        int previousMask = camera.cullingMask;
        var target = new RenderTexture(128, 128, 24);
        try
        {
            camera.targetTexture = target;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.cullingMask = 0; // Isolate the real scene's sky from stimulus geometry.
            camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(128, 128, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply();
            return image;
        }
        finally
        {
            camera.targetTexture = previousTarget; camera.rect = previousRect; camera.cullingMask = previousMask;
            RenderTexture.active = previousActive; target.Release(); Object.DestroyImmediate(target);
        }
    }
}
#endif
