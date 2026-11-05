#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityGLTF;
using Object = UnityEngine.Object;

/// <summary>Re-export the actual bundled locust, including its skin and animation, as a portable example.</summary>
public static class BundledModelExamplesBuilder
{
    [MenuItem("Tools/MATREX VR/Export bundled locust GLB example")]
    public static void Export()
    {
        const string source = "Assets/Prefabs/Locust/SimulatedLocust.prefab";
        string output = Path.Combine(Application.streamingAssetsPath, "Models/Examples");
        Directory.CreateDirectory(output);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source);
        if (prefab == null) throw new FileNotFoundException("Bundled locust prefab is missing.", source);
        GameObject visual = null; GLTFSettings settings = null;
        try
        {
            visual = Object.Instantiate(prefab); visual.name = "SimulatedLocust";
            visual.transform.position = Vector3.zero; visual.transform.rotation = Quaternion.identity;
            // Export only asset data. Experiment/tracking scripts are not GLB content.
            foreach (var script in visual.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
            foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera);
            foreach (var light in visual.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light);
            settings = ScriptableObject.CreateInstance<GLTFSettings>();
            settings.ExportAnimations = true; settings.ExportDisabledGameObjects = true;
            settings.TryExportTexturesFromDisk = false;
            var exporter = new GLTFSceneExporter(new[] { visual.transform }, new ExportContext(settings));
            exporter.SaveGLB(output, "SimulatedLocust");
            byte[] bytes = File.ReadAllBytes(Path.Combine(output, "SimulatedLocust.glb"));
            ExternalModelLibrary.ValidateEmbeddedGlb("SimulatedLocust.glb", bytes);
            int jsonLength = (int)BitConverter.ToUInt32(bytes, 12);
            var document = JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes, 20, jsonLength).TrimEnd('\0', ' '));
            if (((document["skins"] as JArray)?.Count ?? 0) < 1 || ((document["animations"] as JArray)?.Count ?? 0) < 1)
                throw new InvalidDataException("The exported locust must retain its skeleton and animation clips.");
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            File.WriteAllText(Path.Combine(output, "SimulatedLocust.provenance.json"), new JObject {
                ["file"]="SimulatedLocust.glb",["sourcePrefab"]=source,["sha256"]=RunConfigArchive.Hash(bytes),
                ["bytes"]=bytes.Length,["unity"]=Application.unityVersion,["exporter"]="pinned UnityGLTF GLTFSceneExporter",
                ["skins"]=(document["skins"] as JArray)?.Count ?? 0,
                ["animations"]=new JArray((document["animations"] as JArray ?? new JArray()).Select(animation=>(string)animation["name"] ?? "")),
                ["boundsSize"]=new JArray(bounds.size.x,bounds.size.y,bounds.size.z),
                ["sourceDependencies"]=new JArray(AssetDatabase.GetDependencies(source,true).Where(File.Exists).Select(path=>new JObject{["path"]=path,["sha256"]=RunConfigArchive.Hash(File.ReadAllBytes(path))})),
                ["rights"]="Derived from the existing bundled locust asset. Original asset rights and notices continue to apply; this export grants no additional rights."
            }.ToString(Formatting.Indented)+"\n");
            Debug.Log("Exported embedded locust example: " + bytes.Length + " bytes, " + (document["animations"] as JArray)?.Count + " animation clips.");
        }
        finally { if (visual != null) Object.DestroyImmediate(visual); if (settings != null) Object.DestroyImmediate(settings); }
        AssetDatabase.Refresh();
    }
}
#endif
