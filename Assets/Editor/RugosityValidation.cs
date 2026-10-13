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

[InitializeOnLoad]
public static class RugosityValidation
{
    private const string Key = "RugosityValidation.Active";
    private static double started;
    private static int checks;
    private static JObject report;
    private static int conditionIndex, travelSample, lastFrame = -1;
    private static bool travelPending;
    private static string[] conditionFiles;
    private static readonly List<object> temporalRecords = new List<object>();
    private static float maxTravelTilt, minTravelY = float.MaxValue, maxTravelY = float.MinValue;
    private static readonly List<string> errors = new List<string>();
    static RugosityValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + ".keep", false))
                EditorApplication.delayCall += VerifyKeptSettings;
        };
        if (SessionState.GetBool(Key, false))
        {
            EditorApplication.update += Tick;
            SceneManager.sceneLoaded += DisableTracking;
            Application.logMessageReceived += CaptureError;
        }
    }
    private static void CaptureError(string message, string trace, LogType kind)
    { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message); }
    private static void DisableTracking(Scene scene, LoadSceneMode mode)
    { foreach (var root in scene.GetRootGameObjects()) foreach (var listener in root.GetComponentsInChildren<ZmqListener>(true)) listener.enabled = false; }
    public static void Run()
    {
        if (!File.Exists("KANNADI_VALIDATION_COPY")) throw new Exception("Use a disposable project copy.");
        RugosityLabBuilder.Build();
        EditorSceneManager.OpenScene(RugosityLabBuilder.ScenePath);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (started == 0) { started = EditorApplication.timeSinceStartup; return; }
        if (EditorApplication.timeSinceStartup - started < 2) return;
        EditorApplication.update -= Tick;
        try
        {
            var world = Object.FindFirstObjectByType<RugosityWorld>();
            var rig = Object.FindFirstObjectByType<RugosityRig>();
            var choice = Object.FindFirstObjectByType<ChoiceController>();
            Check(world != null && rig != null && choice != null, "Lab initialization");
            Check(world.IsReady, "World surface setup");
            var root = Path.Combine(Application.streamingAssetsPath, "Examples/Rugosity");
            var sequence = JObject.Parse(File.ReadAllText(Path.Combine(root, "sequence.json")));
            var records = new List<object>();
            foreach (JObject row in sequence["sequences"])
            {
                string file = row["parameters"]["configFile"].ToString();
                choice.InitializeScene(new Dictionary<string, object> { ["configFile"] = file });
                var input = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, file)))["rugosity"].ToObject<RugositySettings>();
                Check(world.settings.visualSurface == input.visualSurface && world.settings.collisionSurface == input.collisionSurface, file + " roles");
                foreach (var surface in world.surfaces)
                {
                    Check(surface.renderers.All(r => r.enabled == (surface.surfaceId == input.visualSurface)), file + " renderer selection");
                    Check(surface.colliders.All(c => c.enabled == (surface.surfaceId == input.collisionSurface)), file + " collider selection");
                }
                float min = float.MaxValue, max = float.MinValue, maxTilt = 0;
                foreach (float x in new[] { -40f, -30, -20, 20, 30, 40 })
                foreach (float z in new[] { -50f, -30, -10, 10, 30 })
                {
                    rig.transform.SetPositionAndRotation(new Vector3(x, -999, z), Quaternion.Euler(0, 31, 0));
                    Check(rig.Conform(.016f, true), file + " valid height sample");
                    Check(world.HeightSurface.Sample(rig.transform.position, out float y, out _), file + " explicit height reference");
                    float expected = input.heightMode == RugosityHeightMode.AboveGround ? y + input.clearanceCm / input.centimetersPerUnit : input.absoluteHeight;
                    Check(Mathf.Abs(rig.transform.position.y - expected) < .0001f, file + " clearance");
                    Check(Mathf.Abs(Mathf.DeltaAngle(rig.transform.eulerAngles.y, 31)) < .01f, file + " heading preserved");
                    if (input.orientationMode == RugosityOrientationMode.WorldUp)
                        Check(Vector3.Angle(rig.transform.up, Vector3.up) < .01f, file + " level horizon");
                    else Check(Vector3.Angle(rig.transform.up, rig.GroundNormal) < .05f, file + " normal follows selected surface");
                    min = Mathf.Min(min, rig.transform.position.y); max = Mathf.Max(max, rig.transform.position.y);
                    maxTilt = Mathf.Max(maxTilt, Vector3.Angle(rig.transform.up, Vector3.up));
                }
                if (file.Contains("visual-rs-level-agl")) Check(max - min > 1 && maxTilt < .01f, "Bumpy visible AGL varies height without tilt on flat collision geometry");
                if (file.Contains("visual-flat-hidden-rs")) Check(max - min > 1 && maxTilt > 5, "Flat visible plane, hidden rough height and tilt");
                if (file.Contains("tilt-only")) Check(max - min < .0001f && maxTilt > 5, "Fixed height with changing horizon orientation");
                if (file.Contains("flat-control")) Check(max - min < .0001f && maxTilt < .01f, "Flat control");
                records.Add(new { file, minimumY = min, maximumY = max, maximumTiltDegrees = maxTilt });
                rig.transform.SetPositionAndRotation(new Vector3(-25, 0, -60), Quaternion.identity); rig.Conform(0, true);
                Render(rig.GetComponent<Camera>(), Path.GetFileNameWithoutExtension(file));
            }
            // Inspector edits apply immediately and stay until the next explicitly requested trial config.
            world.settings.heightMode = RugosityHeightMode.AboveGround;
            world.settings.clearanceCm = 2.75f;
            world.ApplyLiveSettings(); rig.Conform(0, true);
            Check(Mathf.Abs(rig.transform.position.y - rig.GroundHeight - 2.75f) < .001f, "Live Inspector clearance");
            world.configurationPolicy = RugosityWorld.ConfigurationPolicy.InspectorOnly;
            world.ApplyConfiguration(new RugosityConfig { clearanceCm = 9 });
            Check(world.settings.clearanceCm == 2.75f, "Inspector-only blocks config overwrite");
            world.configurationPolicy = RugosityWorld.ConfigurationPolicy.ApplyAtTrialStart;
            world.ApplyConfiguration(new RugosityConfig { clearanceCm = 2 }, JObject.Parse("{\"clearanceCm\":3}"));
            Check(world.settings.clearanceCm == 3, "Sequence override wins");
            world.ApplyConfiguration(null);
            Check(world.settings.clearanceCm == 1, "Omitted config restores authored baseline");
            var snapshot = JsonConvert.SerializeObject(world.settings);
            try { world.ApplyConfiguration(new RugosityConfig { visualSurface = "missing" }); Check(false, "Invalid surface must throw"); }
            catch (ArgumentException) { Check(JsonConvert.SerializeObject(world.settings) == snapshot, "Invalid patch is atomic"); }
            world.settings.centimetersPerUnit = 100;
            world.ApplyLiveSettings(); rig.Conform(0, true);
            Check(Mathf.Abs(rig.transform.position.y - rig.GroundHeight - .01f) < .0001f, "Meter scene conversion");
            Vector3 valid = rig.transform.position;
            rig.transform.position = new Vector3(200, 0, 0);
            Check(!rig.Conform(0, true) && rig.transform.position == valid, "Boundary rejection");
            ValidateTerrainSampling();
            ValidateTransformedMesh();
            Check(errors.Count == 0, "Runtime errors: " + string.Join(";", errors));
            Directory.CreateDirectory("docs");
            report = JObject.FromObject(new { passed = true, checks, unity = Application.unityVersion, records,
                scope = "Disposable Unity Editor Play mode, Choice config loading, numerical geometry checks, frame-by-frame movement through LateUpdate, native camera renders. No hardware." });
            conditionFiles = sequence["sequences"].Select(row => row["parameters"]["configFile"].ToString()).ToArray();
            EditorApplication.update += Traverse;
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    private static void Traverse()
    {
        if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            var world = Object.FindFirstObjectByType<RugosityWorld>();
            var rig = Object.FindFirstObjectByType<RugosityRig>();
            if (!travelPending)
            {
                Object.FindFirstObjectByType<ChoiceController>().InitializeScene(new Dictionary<string, object> { ["configFile"] = conditionFiles[conditionIndex] });
                rig.transform.SetPositionAndRotation(new Vector3(-75, 99, 0), Quaternion.identity);
                travelPending = true;
                return;
            }
            var s = world.settings;
            Check(world.HeightSurface.Sample(rig.transform.position, out float ground, out _), "Live frame sample");
            float y = s.heightMode == RugosityHeightMode.AboveGround ? ground + s.clearanceCm / s.centimetersPerUnit : s.absoluteHeight;
            Check(Mathf.Abs(rig.transform.position.y - y) < .001f, "LateUpdate applies height during traversal");
            Vector3 normal = s.orientationMode == RugosityOrientationMode.WorldUp ? Vector3.up : rig.GroundNormal;
            Check(Vector3.Angle(rig.transform.up, normal) < .05f, "LateUpdate applies orientation during traversal");
            Check(Mathf.Abs(Mathf.DeltaAngle(rig.transform.eulerAngles.y, 0)) < .01f, "No heading drift during traversal");
            minTravelY = Mathf.Min(minTravelY, rig.transform.position.y); maxTravelY = Mathf.Max(maxTravelY, rig.transform.position.y);
            maxTravelTilt = Mathf.Max(maxTravelTilt, Vector3.Angle(rig.transform.up, Vector3.up));
            travelSample++;
            if (travelSample == 60)
            {
                temporalRecords.Add(new { condition = conditionFiles[conditionIndex], frames = travelSample, minY = minTravelY, maxY = maxTravelY, maxTilt = maxTravelTilt });
                conditionIndex++; travelSample = 0; travelPending = false;
                minTravelY = float.MaxValue; maxTravelY = float.MinValue; maxTravelTilt = 0;
                if (conditionIndex == conditionFiles.Length)
                {
                    Check(errors.Count == 0, "Frame-by-frame runtime errors: " + string.Join(";", errors));
                    report["checks"] = checks; report["traversals"] = JArray.FromObject(temporalRecords);
                    File.WriteAllText("docs/rugosity-validation-results.json", report.ToString(Formatting.Indented));
                    world.settings.clearanceCm = 2.25f;
                    RugosityWorldEditor.KeepAfterStop(world);
                    SessionState.SetBool(Key + ".keep", true);
                    EditorApplication.update -= Traverse;
                    EditorApplication.isPlaying = false;
                }
                return;
            }
            rig.transform.position = new Vector3(-75 + 150f * travelSample / 59, rig.transform.position.y, 0);
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }

    private static void VerifyKeptSettings()
    {
        SessionState.SetBool(Key + ".keep", false);
        try
        {
            var world = Object.FindFirstObjectByType<RugosityWorld>();
            if (world == null || Mathf.Abs(world.settings.clearanceCm - 2.25f) > .001f)
                throw new Exception("Keep Play-mode settings failed to restore scene tuning after Stop.");
            var result = JObject.Parse(File.ReadAllText("docs/rugosity-validation-results.json"));
            result["keepPlaySettingsAfterStop"] = true;
            result["checks"] = result.Value<int>("checks") + 1;
            File.WriteAllText("docs/rugosity-validation-results.json", result.ToString(Formatting.Indented));
            Debug.Log("RUGOSITY VALIDATION PASSED: " + result["checks"]);
            Finish(0);
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }

    private static void ValidateTerrainSampling()
    {
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(20, 10, 20) };
        var heights = new float[33, 33];
        for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) heights[z, x] = x / 64f;
        data.SetHeights(0, 0, heights);
        var obj = Terrain.CreateTerrainGameObject(data); obj.transform.position = new Vector3(300, 7, 300);
        var surface = obj.AddComponent<RugositySurface>(); surface.terrains = new[] { obj.GetComponent<Terrain>() };
        obj.GetComponent<Terrain>().enabled = false; obj.GetComponent<TerrainCollider>().enabled = false;
        Check(surface.Sample(new Vector3(310, -500, 310), out float y, out var n) && Mathf.Abs(y - 9.5f) < .01f && n.x < 0, "Hidden TerrainData sample with disabled collider");
        Check(!surface.Sample(Vector3.zero, out _, out _), "Terrain bounds");
        Object.DestroyImmediate(obj); Object.DestroyImmediate(data);
    }
    private static void ValidateTransformedMesh()
    {
        var obj = new GameObject("Transformed geometry test");
        var mesh = new Mesh { vertices = new[] { new Vector3(-1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 1, 1), new Vector3(1, 1, -1) }, triangles = new[] { 0, 1, 2, 0, 2, 3 } };
        mesh.RecalculateNormals();
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        obj.transform.SetPositionAndRotation(new Vector3(50, 8, 50), Quaternion.Euler(10, 25, 5)); obj.transform.localScale = new Vector3(2, 3, 4);
        var surface = obj.AddComponent<RugositySurface>(); surface.meshes = new[] { obj.GetComponent<MeshFilter>() };
        Vector3 p = obj.transform.TransformPoint(new Vector3(0, .5f, 0));
        Check(surface.Sample(p, out float y, out _) && Mathf.Abs(y - p.y) < .0001f, "Rotated nonuniform mesh height");
        obj.transform.position += Vector3.up * 3;
        Check(surface.Sample(p, out y, out _) && Mathf.Abs(y - p.y - 3) < .0001f, "Transform changes invalidate mesh cache");
        Object.DestroyImmediate(obj); Object.DestroyImmediate(mesh);
    }
    private static void Render(Camera camera, string name)
    {
        Directory.CreateDirectory("docs/rugosity-renders");
        var target = new RenderTexture(960, 540, 24); var previous = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
        File.WriteAllBytes("docs/rugosity-renders/" + name + ".png", image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous; target.Release();
        Object.DestroyImmediate(target); Object.DestroyImmediate(image);
    }
    private static void Finish(int code)
    {
        EditorApplication.update -= Traverse;
        SessionState.SetBool(Key, false); Application.logMessageReceived -= CaptureError;
        EditorApplication.Exit(code);
    }
}
