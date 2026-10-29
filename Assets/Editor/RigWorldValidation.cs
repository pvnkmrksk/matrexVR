#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Exercise real prefab expansion, visibility, GLB import and immutable model snapshots.</summary>
public static class RigWorldValidation
{
    private static readonly List<string> checks = new List<string>();
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks.Add(message); }
    public static async void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Use a marked disposable copy.");
        string failure = null;
        try
        {
            foreach (int count in new[] { 1, 2, 4, 8, 16 }) CheckScene("Assets/Scenes/Kannadi.unity", count, true);
            foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled && !s.path.EndsWith("ControlScene.unity") && !s.path.EndsWith("ReplayScene.unity") && !s.path.EndsWith("Kannadi.unity")))
                CheckScene(entry.path, 16, false);
            Reject(() => RuntimeRigRegistry.PeerStimulusMask(17), "VR17 is explicitly rejected");
            Check(SceneCompatibility.Resolve("Swarm") == "Kannadi", "Historical Swarm selects Kannadi");
            var legacy = SceneCompatibility.Parameters("Swarm", new Dictionary<string, object>());
            Check((string)legacy["populationMode"] == "Swarm" && (float)legacy["worldUnitsPerMetre"] == 100, "Historical Swarm preserves population mode and centimetres");
            var phase = KannadiConfig.Load(new Dictionary<string, object> { ["__experimentPhase"] = JObject.Parse("{\"swarm\":{\"agentCount\":3}}"), ["populationMode"] = "Swarm", ["worldUnitsPerMetre"] = 100f });
            Check(phase.populationMode == "Swarm" && phase.EffectiveAgentCount == 3 && phase.worldUnitsPerMetre == 100, "Historical phased swarm flattens population with preserved units");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            new GameObject("A sun").AddComponent<Light>().type = LightType.Directional;
            new GameObject("B sun").AddComponent<Light>().type = LightType.Directional;
            ExperimentSun.Apply(scene, JObject.Parse("{\"sun\":{\"enabled\":true,\"intensity\":2.5,\"color\":{\"r\":0.4,\"g\":0.5,\"b\":0.6},\"rotation\":{\"x\":35,\"y\":120,\"z\":0}}}"));
            Check(Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.enabled && l.type == LightType.Directional) == 1, "One directional sun owns scene illumination");
            Check(Mathf.Abs(RenderSettings.sun.intensity - 2.5f) < .001f && Mathf.Abs(RenderSettings.sun.color.g - .5f) < .001f && Quaternion.Angle(RenderSettings.sun.transform.rotation, Quaternion.Euler(35, 120, 0)) < .01f, "Sun color, intensity and orientation apply exactly");
            ExperimentSun.Apply(scene, JObject.Parse("{\"sun\":{\"enabled\":false}}"));
            Check(!RenderSettings.sun.enabled, "Sun can be disabled");
            byte[] glb = AnimatedTriangle();
            ExternalModelShaderRetention.Ensure();
            await ExternalModelLibrary.PreloadAsync("animated.glb", glb);
            var template = ExternalModelLibrary.GetPrefab("animated.glb");
            Check(!template.activeSelf, "Preloaded external model stays inactive");
            var instance = ExternalModelLibrary.InstantiatePrefab(template);
            Check(instance.activeSelf && instance.GetComponentInChildren<MeshRenderer>().sharedMaterial != null, "GLB mesh and material instantiate without rebuilding");
            Material importedMaterial = instance.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Check(importedMaterial.shader != null && importedMaterial.shader.isSupported && importedMaterial.shader.name != "Hidden/InternalErrorShader", "Imported PBR material has a supported real shader");
            foreach (string name in new[] { "UnityGLTF/PBRGraph", "UnityGLTF/UnlitGraph" })
                Check(Shader.Find(name) != null && Shader.Find(name).isSupported, "Runtime model shader is supported: " + name);
            string nativeFixture = Path.Combine(Application.dataPath, "../rig-world-animated.glb"); File.WriteAllBytes(nativeFixture, glb);
            ExternalModelLibrary.ConfigureAnimation(instance, "Move", 1);
            var animation = instance.GetComponentInChildren<Animation>();
            Check(animation != null && animation["Move"] != null && animation["Move"].time == 0, "Embedded animation starts at zero with selected clip");
            animation["Move"].time = .5f; animation.Sample();
            Transform animatedNode = instance.transform.Find("Triangle");
            Check(animatedNode != null && Math.Abs(animatedNode.localPosition.y - .5f) < .001f, "Actual GLB animation samples the embedded movement");
            Object.DestroyImmediate(instance);
            string output = Path.Combine(Application.dataPath, "../rig-world-validation-assets"); Directory.CreateDirectory(output);
            string source = Path.Combine(output, "source"); Directory.CreateDirectory(source);
            string model = Path.Combine(source, "animated.glb"); File.WriteAllBytes(model, glb);
            string recording = Path.Combine(output, "saved-run");
            var archive = new RunConfigArchive(recording, source, "validation", Application.unityVersion);
            byte[] read = archive.ReadBytes("animated.glb", "model", "Kannadi"); archive.RequireComplete();
            Check(read.SequenceEqual(glb), "Acquisition consumes exactly the model bytes it archives");
            File.Delete(model);
            var replay = new ReplaySessionArchive(recording); string saved = replay.Resolve("animated.glb");
            Check(saved != null && File.ReadAllBytes(saved).SequenceEqual(glb), "Replay resolves hashed model after original file is removed");
            string before = RunConfigArchive.Hash(File.ReadAllBytes(saved));
            await ExternalModelLibrary.PreloadAsync(saved, File.ReadAllBytes(saved));
            Check(ExternalModelLibrary.GetPrefab(saved).GetComponentInChildren<MeshFilter>(true) != null && before == RunConfigArchive.Hash(File.ReadAllBytes(saved)), "Archived GLB imports without modifying recorded bytes");
            Check(!ExternalModelLibrary.CollectReferences(JObject.Parse("{\"objects\":[{\"enabled\":false,\"modelPath\":\"missing.glb\"}]}")).Any(), "Disabled custom objects do not load missing models");
            Reject(() => ExternalModelLibrary.ValidateEmbeddedGlb("broken.glb", new byte[20]), "Invalid GLB header is rejected before import");
            Reject(() => ExternalModelLibrary.ValidateEmbeddedGlb("model.fbx", glb), "Unsupported raw model formats report an explicit GLB requirement");
        }
        catch (Exception error) { failure = error.ToString(); Debug.LogError(failure); }
        File.WriteAllText(Path.Combine(Application.dataPath, "../rig-world-validation.json"), JsonConvert.SerializeObject(new { checks = checks.Count, passed = checks, failure, unity = Application.unityVersion }, Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
    private static void CheckScene(string path, int count, bool mirror)
    {
        var scene = EditorSceneManager.OpenScene(path);
        var configs = Enumerable.Range(1, count).ToDictionary(i => "VR" + i, i => new SystemConfig { vrId = "VR" + i, targetDisplay = 0, displayOrder = "DRBLFU", ledPanelWidth = 16, ledPanelHeight = 16 });
        RuntimeRigRegistry.Configure(scene, configs);
        var active = RuntimeRigRegistry.Rigs(scene);
        Check(active.Length == count, path + " enables exactly " + count + " rigs");
        Check(active.Select(r => RuntimeRigRegistry.StimulusLayer(RuntimeRigRegistry.ParseIndex(r.name))).Distinct().Count() == count, path + " reserves unique layers for " + count + " rigs");
        foreach (var rig in active)
        {
            int id = RuntimeRigRegistry.ParseIndex(rig.name);
            rig.GetComponent<ViewportSetter>().ApplyConfiguration(configs["VR" + id]);
            Camera[] cameras = rig.GetComponentsInChildren<Camera>(true);
            Check(cameras.Length == 6 && cameras.All(c => c.enabled && Math.Abs(c.fieldOfView - 90) < .001f), path + " VR" + id + " enables six 90 degree cameras");
            foreach (char face in "FBRLUD") Check(cameras.Count(c => c.name == "Main Camera " + face) == 1, path + " VR" + id + " has one " + face + " face");
            if (mirror)
            {
                var kannadi = rig.GetComponent<Kannadi>();
                typeof(Kannadi).GetMethod("ConfigureCameras", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(kannadi, null);
                Check(cameras.All(c => (c.cullingMask & (1 << RuntimeRigRegistry.StimulusLayer(id))) == 0), "VR" + id + " hides its own body at count " + count);
                foreach (int peer in Enumerable.Range(1, count).Where(i => i != id))
                    Check(cameras.All(c => (c.cullingMask & (1 << RuntimeRigRegistry.StimulusLayer(peer))) != 0), "VR" + id + " sees peer VR" + peer + " at count " + count);
            }
        }
    }
    private static void Reject(Action action, string text)
    {
        try { action(); } catch (ArgumentException) { checks.Add(text); return; } catch (InvalidDataException) { checks.Add(text); return; }
        throw new Exception(text);
    }
    public static byte[] AnimatedTriangle()
    {
        var json = JObject.Parse("{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"byteLength\":68}],\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":36},{\"buffer\":0,\"byteOffset\":36,\"byteLength\":8},{\"buffer\":0,\"byteOffset\":44,\"byteLength\":24}],\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\",\"min\":[0,0,0],\"max\":[1,1,0]},{\"bufferView\":1,\"componentType\":5126,\"count\":2,\"type\":\"SCALAR\",\"min\":[0],\"max\":[1]},{\"bufferView\":2,\"componentType\":5126,\"count\":2,\"type\":\"VEC3\"}],\"materials\":[{\"pbrMetallicRoughness\":{\"baseColorFactor\":[0.4,0.6,0.2,1],\"metallicFactor\":0}}],\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0},\"material\":0}]}],\"nodes\":[{\"name\":\"Triangle\",\"mesh\":0}],\"scenes\":[{\"nodes\":[0]}],\"scene\":0,\"animations\":[{\"name\":\"Move\",\"samplers\":[{\"input\":1,\"output\":2,\"interpolation\":\"LINEAR\"}],\"channels\":[{\"sampler\":0,\"target\":{\"node\":0,\"path\":\"translation\"}}]}]}");
        byte[] text = Encoding.UTF8.GetBytes(json.ToString(Formatting.None)); int padded = (text.Length + 3) / 4 * 4;
        using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
        {
            writer.Write(0x46546c67u); writer.Write(2u); writer.Write((uint)(12 + 8 + padded + 8 + 68));
            writer.Write((uint)padded); writer.Write(0x4e4f534au); writer.Write(text);
            for (int i = text.Length; i < padded; i++) writer.Write((byte)' ');
            writer.Write(68u); writer.Write(0x004e4942u);
            foreach (float value in new float[] { 0,0,0, 1,0,0, 0,1,0, 0,1, 0,0,0, 0,1,0 }) writer.Write(value);
            return stream.ToArray();
        }
    }
}
#endif
