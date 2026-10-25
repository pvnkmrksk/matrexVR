#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ReleaseReadinessValidation
{
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY")))
            throw new Exception("Run only on a disposable project copy.");
        var checks = new List<string>();
        void Check(bool success, string text) { if (!success) throw new Exception(text); checks.Add(text); }
        foreach (int[] ids in new[] { new[] {1}, new[] {1, 2}, new[] {1, 2, 3, 4}, new[] {1, 2, 3, 4, 5, 6}, new[] {1, 10} })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Swarm.unity");
            var configs = ids.ToDictionary(i => "VR" + i, i => new SystemConfig { vrId = "VR" + i, speciesName = "Bogong moth", displayOrder = "RBLF", targetDisplay = 0 });
            RuntimeRigRegistry.Configure(scene, configs);
            var rigs = RuntimeRigRegistry.Rigs(scene);
            Check(rigs.Select(r => RuntimeRigRegistry.ParseIndex(r.name)).SequenceEqual(ids), "Configured rigs: " + string.Join(",", ids));
            Check(ids.Select(RuntimeRigRegistry.StimulusLayer).Distinct().Count() == ids.Length, "Independent stimulus layers for " + string.Join(",", ids));
            foreach (var rig in rigs)
            {
                int index = RuntimeRigRegistry.ParseIndex(rig.name);
                var spawner = rig.GetComponent<LocustSpawner>();
                Check(spawner != null && spawner.ResolvedLayer == RuntimeRigRegistry.StimulusLayer(index), "Swarm layer follows VR" + index);
                foreach (SwarmLogger logger in rig.GetComponentsInChildren<SwarmLogger>(true))
                    Check(logger.locustLayerMask.value == (1 << spawner.ResolvedLayer), "Swarm recording follows VR" + index);
                if (index > 4)
                    foreach (Camera camera in rig.GetComponentsInChildren<Camera>(true))
                        Check((camera.cullingMask & RuntimeRigRegistry.StimulusMask) == (1 << spawner.ResolvedLayer), "Cloned camera isolates VR" + index + ": " + camera.name);
            }
        }
        Check(RuntimeRigRegistry.ParseIndex("VR10 Kannadi") == 10 && RuntimeRigRegistry.ParseIndex("VR1") == 1, "VR10 never aliases VR1");
        var authored = EditorSceneManager.OpenScene("Assets/Scenes/Choice.unity");
        RuntimeRigRegistry.Configure(authored, new Dictionary<string, SystemConfig> { ["VR2"] = new SystemConfig { vrId = "VR2" } });
        Check(RuntimeRigRegistry.Rigs(authored).Length == 1 && RuntimeRigRegistry.Rigs(authored)[0].name.Contains("VR2"), "Choice disables omitted animal rigs");
        var asynchronous = EditorSceneManager.OpenScene("Assets/Scenes/Choice_desync.unity");
        RuntimeRigRegistry.Configure(asynchronous, Enumerable.Range(1, 6).ToDictionary(i => "VR" + i, i => new SystemConfig { vrId = "VR" + i }));
        int legacyChoiceMask = LayerMask.GetMask("ChoiceVR1", "ChoiceVR2", "ChoiceVR3", "ChoiceVR4");
        foreach (var rig in RuntimeRigRegistry.Rigs(asynchronous).Where(r => RuntimeRigRegistry.ParseIndex(r.name) > 4))
            foreach (Camera camera in rig.GetComponentsInChildren<Camera>(true))
                Check((camera.cullingMask & legacyChoiceMask) == 0, "Additional asynchronous camera excludes authored private rigs: " + rig.name + "/" + camera.name);
        Check(Directory.Exists(RuntimePaths.ConfigRoot), "Editor configuration folder remains available");
        var output = Path.Combine(Application.dataPath, "../release-readiness-results.json");
        File.WriteAllText(output, JsonConvert.SerializeObject(new { passed = true, checks }, Formatting.Indented));
        Debug.Log("PASS release readiness: " + checks.Count + " checks");
    }
}
#endif
