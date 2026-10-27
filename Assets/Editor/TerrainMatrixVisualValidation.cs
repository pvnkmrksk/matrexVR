using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Actual textured camera renders of all terrain scene/treatment pairs, plus independently sampled geometry.</summary>
public static class TerrainMatrixVisualValidation
{
    private static readonly string[] Modes = { "matched-terrain", "visual-terrain_navigation-flat", "visual-flat_navigation-terrain", "matched-flat" };
    private static readonly List<object> Records = new List<object>();
    private static string output;
    private static int checks;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new InvalidOperationException(message); }
    private static object Vec(Vector3 value) => new { x = value.x, y = value.y, z = value.z };

    public static void Run()
    {
        if (!File.Exists("KANNADI_VALIDATION_COPY")) throw new InvalidOperationException("Disposable project only.");
        output = Path.GetFullPath("terrain-matrix-visual-validation");
        string[] args = Environment.GetCommandLineArgs();
        int arg = Array.IndexOf(args, "-terrainMatrixOutput");
        if (arg >= 0 && arg + 1 < args.Length) output = Path.GetFullPath(args[arg + 1]);
        Directory.CreateDirectory(output);
        try
        {
            foreach (string mode in Modes)
            {
                var sequence = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Examples/Rugosity/sequence-treatment-" + mode + ".json")));
                int sceneIndex = 0;
                foreach (JObject row in sequence["sequences"])
                {
                    string scene = (string)row["sceneName"];
                    EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity");
                    var world = Object.FindFirstObjectByType<RugosityWorld>();
                    var rig = Object.FindFirstObjectByType<RugosityRig>();
                    Check(world != null && rig != null, "World and actual configured rig exist: " + scene);
                    string reference = (string)row["parameters"]["configFile"];
                    var config = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, reference)))["rugosity"].ToObject<RugosityConfig>();
                    var patch = (JObject)row["parameters"]["rugosity"];
                    world.ApplyConfiguration(config, patch);
                    Check(world.IsReady, "Applied real treatment: " + mode + "/" + scene);
                    string visual = (string)patch["visualSurface"], navigation = (string)patch["collisionSurface"];
                    Check(world.settings.visualSurface == visual && world.settings.collisionSurface == navigation, "Applied roles equal sequence overrides");
                    Check(world.surfaces.All(s => s.renderers.All(r => r.enabled == (s.surfaceId == visual)) && s.colliders.All(c => c.enabled == (s.surfaceId == navigation))), "Actual renderer and collider enablement");
                    var eye = rig.GetComponentsInChildren<Camera>(true).Single(c => c.name == "Main Camera F");
                    foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) camera.enabled = false;
                    string name = (Array.IndexOf(Modes, mode) + 1).ToString("00") + "-" + (sceneIndex + 1).ToString("00") + "-" + mode + "-" + TerrainChoiceBuilder.Patterns[sceneIndex];
                    string directory = Path.Combine(output, name); Directory.CreateDirectory(directory);
                    var probes = new List<object>();
                    foreach (int side in new[] { -1, 1 })
                    {
                        rig.transform.SetPositionAndRotation(new Vector3(side * 53.125f, 1, 28.125f), Quaternion.Euler(0, side < 0 ? 55 : -55, 0));
                        Check(rig.Conform(0, true), "Actual rig conforms at common probe");
                        Check(world.surfaces.Single(s => s.surfaceId == visual).Sample(rig.transform.position, out float visibleHeight, out var visibleNormal), "Visible geometry covers probe");
                        string image = side < 0 ? "animal-left.png" : "animal-right.png";
                        var largeProjection = Render(eye, 768, 768, Path.Combine(directory, image));
                        string ledImage = side < 0 ? "animal-left-led128.png" : "animal-right-led128.png";
                        var ledProjection = Render(eye, 128, 128, Path.Combine(directory, ledImage));
                        Check(largeProjection == ledProjection, "Actual standard F camera keeps the same projection at 768px and 128px");
                        probes.Add(new { side = side < 0 ? "left" : "right", position = Vec(rig.transform.position), up = Vec(rig.transform.up),
                            headingDegrees = side < 0 ? 55 : -55, cameraPosition = Vec(eye.transform.position), cameraRotation = Vec(eye.transform.eulerAngles),
                            navigationGround = rig.GroundHeight, navigationTiltDegrees = Vector3.Angle(rig.transform.up, Vector3.up),
                            visibleHeight, visibleTiltDegrees = Vector3.Angle(visibleNormal, Vector3.up), image, ledImage });
                    }
                    var overviewObject = new GameObject("Validation fixed overview");
                    var overview = overviewObject.AddComponent<Camera>();
                    overview.enabled = false; overview.clearFlags = CameraClearFlags.SolidColor; overview.backgroundColor = eye.backgroundColor;
                    overview.nearClipPlane = .01f; overview.farClipPlane = 1000; overview.fieldOfView = 68;
                    overview.transform.position = new Vector3(0, 42, -125); overview.transform.LookAt(new Vector3(0, 0, 20));
                    Render(overview, 1280, 720, Path.Combine(directory, "visible-overview.png"));
                    // Supplemental navigation view: this changes only the diagnostic render,
                    // after the true textured stimulus frames above have been saved.
                    foreach (var surface in world.surfaces) surface.SetRoles(surface.surfaceId == navigation, surface.surfaceId == navigation);
                    Render(overview, 1280, 720, Path.Combine(directory, "navigation-overview.png"));
                    world.ApplyLiveSettings();
                    RenderClay(world, visual, navigation, overview, directory);
                    var sectionX = new List<float>(); var sectionVisual = new List<float>(); var sectionNavigation = new List<float>();
                    var visualSurface = world.surfaces.Single(surface => surface.surfaceId == visual);
                    for (float x = -90; x <= 90; x += .5f)
                    {
                        bool visibleHit = visualSurface.Sample(new Vector3(x, 0, 28.125f), out float visibleY, out _);
                        bool navigationHit = world.HeightSurface.Sample(new Vector3(x, 0, 28.125f), out float navigationY, out _);
                        Check(visibleHit && navigationHit, "Cross-section covers both sides");
                        sectionX.Add(x); sectionVisual.Add(visibleY); sectionNavigation.Add(navigationY);
                    }
                    var surfaces = world.surfaces.Select(surface => new { id = surface.surfaceId,
                        left = SampleRegion(surface, -90, -5), right = SampleRegion(surface, 5, 90),
                        renderers = surface.renderers.Select(r => new { name = r.name, enabled = r.enabled,
                            material = r.sharedMaterial != null ? AssetDatabase.GetAssetPath(r.sharedMaterial) : null,
                            texture = r.sharedMaterial != null && r.sharedMaterial.mainTexture != null ? AssetDatabase.GetAssetPath(r.sharedMaterial.mainTexture) : null }).ToArray()
                    }).ToArray();
                    Records.Add(new { name, mode, scene, pattern = TerrainChoiceBuilder.Patterns[sceneIndex], visual, navigation,
                        camera = new { source = "Actual authored VR1 / Main Camera F component, not a replacement diagnostic eye camera", sourcePrefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(eye.gameObject),
                            eyeFieldOfView = eye.fieldOfView, eyeNear = eye.nearClipPlane, eyeFar = eye.farClipPlane, highResolutionPixels = new[] { 768, 768 }, ledPixels = new[] { 128, 128 },
                            projectionIdenticalAtBothResolutions = true, viewportAndRenderTargetRestored = true, overviewPosition = Vec(overview.transform.position), overviewRotation = Vec(overview.transform.eulerAngles),
                            geometryScaleChanged = false, stimulusMaterialChanged = false }, probes, surfaces,
                        crossSection = new { z = 28.125f, x = sectionX, visual = sectionVisual, navigation = sectionNavigation }, directory });
                    Object.DestroyImmediate(overviewObject);
                    sceneIndex++;
                }
            }
            Check(Records.Count == 16, "All sixteen scene/treatment combinations rendered");
            Write(null); EditorApplication.Exit(0);
        }
        catch (Exception error) { Write(error.ToString()); Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static object SampleRegion(RugositySurface surface, float xmin, float xmax)
    {
        var heights = new List<float>(); float maximumTilt = 0;
        for (float x = xmin; x <= xmax + .001f; x += 2.5f)
        for (float z = -90; z <= 90; z += 2.5f)
            if (surface.Sample(new Vector3(x, 0, z), out float height, out var normal))
            { heights.Add(height); maximumTilt = Mathf.Max(maximumTilt, Vector3.Angle(normal, Vector3.up)); }
        Check(heights.Count > 100, "Surface region sampled independently: " + surface.surfaceId);
        float mean = heights.Average();
        return new { count = heights.Count, minimum = heights.Min(), maximum = heights.Max(), standardDeviation = Mathf.Sqrt(heights.Average(height => (height - mean) * (height - mean))), maximumTiltDegrees = maximumTilt };
    }

    private static Matrix4x4 Render(Camera camera, int width, int height, string path)
    {
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        target.Create(); var previous = camera.targetTexture; var previousRect = camera.rect; var active = RenderTexture.active;
        camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1);
        Check(camera.pixelWidth == width && camera.pixelHeight == height, "Actual camera pixel dimensions equal target");
        var projection = camera.projectionMatrix; camera.Render();
        RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        camera.targetTexture = previous; camera.rect = previousRect; RenderTexture.active = active;
        Object.DestroyImmediate(texture); target.Release(); Object.DestroyImmediate(target);
        return projection;
    }

    private static void RenderClay(RugosityWorld world, string visual, string navigation, Camera camera, string directory)
    {
        var renderers = world.surfaces.SelectMany(surface => surface.renderers).Distinct().ToArray();
        var originals = renderers.ToDictionary(renderer => renderer, renderer => renderer.sharedMaterials);
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToDictionary(light => light, light => light.enabled);
        var ambientMode = RenderSettings.ambientMode; var ambient = RenderSettings.ambientLight;
        var clay = new Material(Shader.Find("Standard")) { name = "Diagnostic neutral clay only", color = new Color(.65f, .65f, .65f) };
        clay.SetFloat("_Glossiness", 0); clay.SetFloat("_Metallic", 0);
        var lampObject = new GameObject("Diagnostic common low-angle light only");
        var lamp = lampObject.AddComponent<Light>(); lamp.type = LightType.Directional; lamp.intensity = 1.3f; lamp.shadows = LightShadows.None;
        lamp.transform.rotation = Quaternion.Euler(20, -35, 0);
        try
        {
            foreach (var pair in lights) pair.Key.enabled = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.12f, .12f, .12f);
            foreach (var renderer in renderers) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => clay).ToArray();
            foreach (var surface in world.surfaces) surface.SetRoles(surface.surfaceId == visual, surface.surfaceId == navigation);
            Render(camera, 1280, 720, Path.Combine(directory, "visual-clay-debug.png"));
            foreach (var surface in world.surfaces) surface.SetRoles(surface.surfaceId == navigation, surface.surfaceId == navigation);
            Render(camera, 1280, 720, Path.Combine(directory, "navigation-clay-debug.png"));
        }
        finally
        {
            foreach (var pair in originals) pair.Key.sharedMaterials = pair.Value;
            foreach (var pair in lights) pair.Key.enabled = pair.Value;
            RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambient;
            Object.DestroyImmediate(lampObject); Object.DestroyImmediate(clay); world.ApplyLiveSettings();
            Check(originals.All(pair => pair.Key.sharedMaterials.SequenceEqual(pair.Value)), "Diagnostic materials restored exactly");
        }
    }
    private static void Write(string failure)
    {
        File.WriteAllText(Path.Combine(output, "results.json"), JsonConvert.SerializeObject(new { checks, failure, unity = Application.unityVersion, cases = Records,
            scope = "Disposable Unity Edit-mode Camera.Render with authored scene geometry/materials/lights and actual RugosityRig.Conform. Eye frames use the real authored VR1/Main Camera F at both 768x768 and 128x128 with verified identical projection; only targetTexture and viewport are temporarily changed and restored. The 128px frames verify rendering at panel resolution, not physical LED timing, native display presentation, tracking calibration, or the full MainController startup path. Navigation overview temporarily renders the selected collision mesh. Additional explicitly named clay-debug renders replace only the ground material and lighting with common neutral clay and one low-angle light, then restore both exactly; geometry and scale are unchanged. Clay-debug frames are never stimulus frames." }, Formatting.Indented));
    }
}
