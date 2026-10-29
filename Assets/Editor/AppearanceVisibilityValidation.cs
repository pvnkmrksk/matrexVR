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
using Object = UnityEngine.Object;

/// <summary>Actual camera masks and GPU panorama orientation, in a disposable project only.</summary>
public static class AppearanceVisibilityValidation
{
    private static readonly List<string> checks = new List<string>();
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); checks.Add(label); }
    private static void Reject(Action action, string label)
    {
        bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, label);
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable project only.");
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../appearance-visibility-validation"));
        Directory.CreateDirectory(output); checks.Clear(); string failure = null;
        try { VisibilityMasks(); VisibilityPixels(); Panoramas(); }
        catch (Exception error) { failure = error.ToString(); Debug.LogError(failure); }
        File.WriteAllText(Path.Combine(output, "results.json"), JsonConvert.SerializeObject(new {
            checks = checks.Count, passed = checks, failure, unity = Application.unityVersion,
            scope = "Actual 1/2/4/8/16 rig camera masks, matrix enable/disable and phase restoration, rendered body visibility, and GPU rotation against Unity Skybox/Panoramic."
        }, Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
    private static Camera[] Rig(int index)
    {
        var root = new GameObject("VR" + index); root.AddComponent<ClosedLoop>();
        var cameras = new List<Camera>();
        foreach (char face in "FRBLUD")
        {
            var child = new GameObject("Main Camera " + face); child.transform.SetParent(root.transform, false);
            var camera = child.AddComponent<Camera>(); camera.enabled = false;
            camera.cullingMask = 1 | (1 << 31) | (1 << RuntimeRigRegistry.StimulusLayer(index)); cameras.Add(camera);
        }
        return cameras.ToArray();
    }
    private static void VisibilityMasks()
    {
        foreach (int count in new[] { 1, 2, 4, 8, 16 })
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameras = Enumerable.Range(1, count).Select(Rig).ToArray();
            var eye = new GameObject("Unparented overview").AddComponent<Camera>(); eye.enabled = false;
            int overviewMask = eye.cullingMask;
            var configuration = new JObject { [ExperimentVisibility.Field] = ExperimentVisibility.Default() };
            configuration[ExperimentVisibility.Field]["enabled"] = true;
            var rows = (JArray)configuration[ExperimentVisibility.Field]["rows"];
            for (int row = 0; row < 16; row++) for (int column = 0; column < 16; column++) rows[row][column] = row != column && (row + column) % 3 == 0;
            ExperimentVisibility.Apply(scene, configuration);
            for (int index = 0; index < count; index++)
            {
                int expected = 1 | (1 << 31);
                for (int column = 0; column < 16; column++) if ((bool)rows[index][column]) expected |= 1 << RuntimeRigRegistry.StimulusLayer(column + 1);
                Check(cameras[index].All(camera => camera.cullingMask == expected), count + " rigs: all six faces use VR" + (index + 1) + " row without changing other layers");
            }
            Check(eye.cullingMask == overviewMask, count + " rigs: observer matrix never changes the overhead camera");
            ExperimentVisibility.Apply(scene, new JObject());
            for (int index = 0; index < count; index++)
                Check(cameras[index].All(camera => camera.cullingMask == (1 | (1 << 31) | (1 << RuntimeRigRegistry.StimulusLayer(index + 1)))), count + " rigs: disabling restores VR" + (index + 1) + " authored population mask");
            ExperimentVisibility.Apply(scene, configuration);
            ExperimentVisibility.Restore(scene); // Main calls this before the next original controller.
            foreach (var camera in cameras[0]) camera.cullingMask = 1 | (1 << RuntimeRigRegistry.StimulusLayer(4));
            ExperimentVisibility.Apply(scene, (JObject)configuration.DeepClone());
            ExperimentVisibility.Restore(scene);
            Check(cameras[0].All(camera => camera.cullingMask == (1 | (1 << RuntimeRigRegistry.StimulusLayer(4)))), count + " rigs: next controller's authored mask survives a matrix phase");
        }
        var small = new JObject { [ExperimentVisibility.Field] = ExperimentVisibility.Default(2) }; small[ExperimentVisibility.Field]["enabled"] = true;
        Reject(() => ExperimentVisibility.Validate(small, 4), "Sparse VR IDs require the corresponding matrix row");
        small[ExperimentVisibility.Field]["rows"][0] = new JArray(true);
        Reject(() => ExperimentVisibility.Validate(small), "Nonsquare matrix fails before acquisition");
        small[ExperimentVisibility.Field]["rows"] = new JArray(new JArray(1));
        Reject(() => ExperimentVisibility.Validate(small), "Numeric matrix flags are rejected in favor of explicit booleans");
        small[ExperimentVisibility.Field]["enabled"] = false;
        ExperimentVisibility.Validate(small, 16); Check(true, "Disabled matrix preserves unused values without applying them");
    }
    private static Color[] Render(Camera camera, int size = 64)
    {
        var oldTarget = camera.targetTexture; var previous = RenderTexture.active;
        var target = new RenderTexture(size, size, 24); var image = new Texture2D(size, size, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, size, size), 0, 0); image.Apply(); return image.GetPixels();
        }
        finally { camera.targetTexture = oldTarget; RenderTexture.active = previous; Object.DestroyImmediate(image); Object.DestroyImmediate(target); }
    }
    private static void VisibilityPixels()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = Rig(1)[0]; Rig(2); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.fieldOfView = 70;
        var red = GameObject.CreatePrimitive(PrimitiveType.Cube); var green = GameObject.CreatePrimitive(PrimitiveType.Cube);
        red.layer = RuntimeRigRegistry.StimulusLayer(1); green.layer = RuntimeRigRegistry.StimulusLayer(2);
        red.transform.position = new Vector3(-.6f, 0, 3); green.transform.position = new Vector3(.6f, 0, 3);
        red.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = Color.red };
        green.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = Color.green };
        var configuration = new JObject { [ExperimentVisibility.Field] = new JObject { ["enabled"] = true, ["rows"] = new JArray(new JArray(false, true), new JArray(true, false)) } };
        ExperimentVisibility.Apply(scene, configuration); var pixels = Render(camera);
        Check(pixels.Any(color => color.g > .5f) && !pixels.Any(color => color.r > .5f), "Actual observer render shows its selected peer and hides its own layer");
        ExperimentVisibility.Apply(scene, new JObject()); pixels = Render(camera);
        Check(pixels.Any(color => color.r > .5f) && !pixels.Any(color => color.g > .5f), "Actual render returns to its authored condition after disabling matrix");
        Object.DestroyImmediate(red.GetComponent<Renderer>().sharedMaterial); Object.DestroyImmediate(green.GetComponent<Renderer>().sharedMaterial);
    }
    private static int Channels(Color color)
    {
        float peak = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        return (color.r > peak * .6f ? 1 : 0) | (color.g > peak * .6f ? 2 : 0) | (color.b > peak * .6f ? 4 : 0);
    }
    private static void Panoramas()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Check(ExperimentSky.Rotation(null) == 0 && ExperimentSky.Rotation(new JObject { [ExperimentSky.RotationField] = 360 }) == 0, "Omitted panorama rotation is zero and one turn normalizes to zero");
        foreach (double value in new[] { -1, 361, double.NaN, double.PositiveInfinity })
            Reject(() => ExperimentSky.Rotation(new JObject { [ExperimentSky.RotationField] = value }), "Invalid panorama rotation rejected: " + value);
        Reject(() => ExperimentSky.Rotation(new JObject { [ExperimentSky.RotationField] = "90" }), "Panorama rotation uses a numeric control, not text coercion");
        var camera = new GameObject("Panorama validation camera").AddComponent<Camera>(); camera.enabled = false; camera.clearFlags = CameraClearFlags.Skybox; camera.cullingMask = 0;
        var texture = new Texture2D(64, 32, TextureFormat.RGBA32, false); texture.wrapModeU = TextureWrapMode.Repeat; texture.wrapModeV = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Point;
        var colors = new[] { Color.red, Color.green, Color.blue, Color.yellow };
        for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++) texture.SetPixel(x, y, colors[((x + 8) / 16) % 4]); texture.Apply();
        var stock = new Material(Shader.Find("Skybox/Panoramic")); stock.mainTexture = texture;
        var actual = new Material(Resources.Load<Shader>("NightSky/Panorama")); actual.mainTexture = texture;
        var previous = RenderSettings.skybox;
        try
        {
            foreach (int angle in new[] { 0, 90, 180, 270, 360 }) foreach (int yaw in new[] { 0, 90, 180, 270 })
            {
                camera.transform.rotation = Quaternion.Euler(0, yaw, 0);
                var configuration = new JObject { ["skyboxPath"] = "fixture.png", [ExperimentSky.RotationField] = angle };
                RenderSettings.skybox = stock; ExperimentSky.Apply(configuration); var expected = Render(camera, 16)[8 * 16 + 8];
                RenderSettings.skybox = actual; ExperimentSky.Apply(configuration); var rendered = Render(camera, 16)[8 * 16 + 8];
                Check(Channels(expected) == Channels(rendered), "GPU panorama direction matches Unity at rotation " + angle + ", view " + yaw);
            }
        }
        finally { RenderSettings.skybox = previous; Object.DestroyImmediate(stock); Object.DestroyImmediate(actual); Object.DestroyImmediate(texture); }
    }
}
#endif
