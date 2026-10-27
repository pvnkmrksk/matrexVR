using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Exercise all sixteen treatment/pattern assignments on the one real scene.</summary>
public static class TerrainTreatmentValidation
{
    public static void Run()
    {
        if (!File.Exists("KANNADI_VALIDATION_COPY")) throw new Exception("Use a disposable project copy.");
        int checks = 0;
        Action<bool, string> check = (ok, message) => { checks++; if (!ok) throw new Exception(message); };
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/TerrainCueLab.unity");
            var world = Object.FindFirstObjectByType<RugosityWorld>();
            var rig = Object.FindFirstObjectByType<RugosityRig>();
            check(world != null && rig != null, "Scene must have a world and navigation rig.");
            string folder = Path.Combine(Application.streamingAssetsPath, "Examples/Rugosity");
            var baseConfig = JObject.Parse(File.ReadAllText(Path.Combine(folder, "terrain-treatment.json")))["rugosity"].ToObject<RugosityConfig>();
            string[] modes = { "matched-terrain", "visual-terrain_navigation-flat", "visual-flat_navigation-terrain", "matched-flat" };
            int trials = 0;
            foreach (string mode in modes)
            {
                var sequence = JObject.Parse(File.ReadAllText(Path.Combine(folder, "sequence-treatment-" + mode + ".json")));
                foreach (var row in sequence["sequences"])
                {
                    var patch = (JObject)row["parameters"]["rugosity"];
                    world.ApplyConfiguration(baseConfig, patch);
                    check(world.IsReady, "Applied treatment is ready: " + mode);
                    string visual = (string)patch["visualSurface"], collision = (string)patch["collisionSurface"];
                    check(world.HeightSurface.surfaceId == collision && world.NormalSurface.surfaceId == collision, "Both navigation references follow collision.");
                    check(world.surfaces.All(s => s.renderers.All(r => r.enabled == (s.surfaceId == visual)) && s.colliders.All(c => c.enabled == (s.surfaceId == collision))), "Renderer and collider roles follow treatment.");
                    foreach (float x in new[] { -80f, -30f, 0f, 30f, 80f })
                    {
                        rig.transform.position = new Vector3(x, 10, 0);
                        rig.transform.rotation = Quaternion.Euler(0, 90, 0);
                        check(world.HeightSurface.Sample(rig.transform.position, out float ground, out _), "Selected navigation mesh covers test position.");
                        rig.Conform(0, true);
                        check(Mathf.Abs(rig.transform.position.y - ground - 1) < .001f, "Rig follows selected navigation mesh at 1cm clearance.");
                        if (collision == "Flat")
                            check(Mathf.Abs(rig.transform.position.y - 1) < .001f && Vector3.Angle(rig.transform.up, Vector3.up) < .01f, "Flat navigation stays level even with visible bumps.");
                    }
                    trials++;
                }
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/TerrainCueLab/Ground.mat");
            var original = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TerrainCueLab/ForestFloorAlbedo.jpg");
            check(original != null && material.mainTexture == original, "Ground uses recovered original albedo.");
            check(original.width == 4096 && original.height == 4096, "Recovered albedo imports at original resolution.");
            check(!material.IsKeywordEnabled("_NORMALMAP"), "No normal-map roughness is added to flat visual treatments.");
            Directory.CreateDirectory("docs");
            File.WriteAllText("docs/terrain-treatment-validation-results.json", JsonConvert.SerializeObject(new { passed = true, checks, trials, unity = Application.unityVersion, scope = "Sixteen real mesh/renderer/collider/pose assignments in one scene; Edit mode, no hardware." }, Formatting.Indented));
            Debug.Log("TERRAIN TREATMENTS PASSED: " + checks + " checks across " + trials + " trials.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
