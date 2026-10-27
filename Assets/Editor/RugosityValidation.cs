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
    private static bool legacy;
    private static float expectedTravelX;
    private static string ReportPath => legacy ? "docs/rugosity-legacy-validation-results.json" : "docs/rugosity-validation-results.json";
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
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var listener in root.GetComponentsInChildren<ZmqListener>(true)) listener.enabled = false;
            // Geometry-only runner opens the lab directly, without a recording session.
            // TerrainChoiceRuntimeValidation separately tests the standard loggers through MainController.
            foreach (var logger in root.GetComponentsInChildren<DataLogger>(true)) logger.enabled = false;
        }
    }
    public static void Run()
    {
        if (!File.Exists("KANNADI_VALIDATION_COPY")) throw new Exception("Use a disposable project copy.");
        RugosityLabBuilder.Build();
        SessionState.SetBool(Key + ".legacy", false);
        EditorSceneManager.OpenScene(RugosityLabBuilder.ScenePath);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    public static void RunLegacy()
    {
        if (!File.Exists("KANNADI_VALIDATION_COPY")) throw new Exception("Use a disposable project copy.");
        SessionState.SetBool(Key + ".legacy", true);
        EditorSceneManager.OpenScene(RugosityLabBuilder.LegacyScenePath);
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
            legacy = SessionState.GetBool(Key + ".legacy", false);
            var world = Object.FindFirstObjectByType<RugosityWorld>();
            var rig = Object.FindFirstObjectByType<RugosityRig>();
            var choice = Object.FindFirstObjectByType<ChoiceController>();
            Check(world != null && rig != null && choice != null, "Lab initialization");
            Check(world.IsReady, "World surface setup");
            var root = Path.Combine(Application.streamingAssetsPath, "Examples/Rugosity");
            var sequence = JObject.Parse(File.ReadAllText(Path.Combine(root, legacy ? "sequence-legacy-rugosity.json" : "sequence.json")));
            var records = new List<object>();
            foreach (JObject row in sequence["sequences"])
            {
                string file = row["parameters"]["configFile"].ToString();
                choice.InitializeScene(new Dictionary<string, object> { ["configFile"] = file });
                rig.GetComponent<Keyboard>().SetAutopilotMode(false);
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
                if (legacy && file.Contains("tilt-only")) Check(max - min < .0001f && maxTilt > 5, "Fixed height with changing horizon orientation");
                if (file.Contains("flat-control")) Check(max - min < .0001f && maxTilt < .01f, "Flat control");
                if (!legacy)
                {
                    if (file.Contains("navigation-flat") || file.Contains("matched-flat"))
                        Check(max - min < .0001f && maxTilt < .01f, "Flat navigation has neither height nor tilt variation");
                    if (file.Contains("navigation-terrain") || file.Contains("matched-terrain"))
                        Check(max - min > .2f && maxTilt > 2, "Terrain navigation has height and tilt variation");
                    if (file.Contains("height-only")) Check(max - min > .2f && maxTilt < .01f, "Height-only control");
                    if (file.Contains("tilt-only")) Check(max - min < .0001f && maxTilt > 2, "Tilt-only control");
                    ValidateAgainstColliders(world, rig, file);
                }
                records.Add(new { file, minimumY = min, maximumY = max, maximumTiltDegrees = maxTilt });
                rig.transform.SetPositionAndRotation(new Vector3(-25, 0, -60), Quaternion.Euler(0, legacy ? 0 : 90, 0)); rig.Conform(0, true);
                Render(rig.GetComponent<Camera>() ?? rig.GetComponentsInChildren<Camera>(true).First(c => c.name == "Main Camera F"), Path.GetFileNameWithoutExtension(file));
            }
            if (!legacy)
            {
                ExportCrossSections(world, rig, choice);
                ValidateProfiles(world);
                ValidateSmoothedHeading(world, rig);
                ValidateDisabledWorld(world, rig);
                ValidateTrackingFrame(world, rig);
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
                var keyboard = rig.GetComponent<Keyboard>();
                keyboard.enabled = false;
                keyboard.SetAutopilotMode(false);
                rig.transform.SetPositionAndRotation(new Vector3(-75, 99, 0), Quaternion.Euler(0, legacy ? 0 : 90, 0));
                expectedTravelX = -75;
                travelPending = true;
                return;
            }
            if (!legacy) Check(Mathf.Abs(rig.transform.position.x - expectedTravelX) < .001f, "Autopilot moves horizontally without slope-dependent speed changes");
            var s = world.settings;
            Check(world.HeightSurface.Sample(rig.transform.position, out float ground, out _), "Live frame sample");
            float y = s.heightMode == RugosityHeightMode.AboveGround ? ground + s.clearanceCm / s.centimetersPerUnit : s.absoluteHeight;
            Check(Mathf.Abs(rig.transform.position.y - y) < .001f, "LateUpdate applies height during traversal");
            Vector3 normal = s.orientationMode == RugosityOrientationMode.WorldUp ? Vector3.up : rig.GroundNormal;
            Check(Vector3.Angle(rig.transform.up, normal) < .05f, "LateUpdate applies orientation during traversal");
            Check(Mathf.Abs(Mathf.DeltaAngle(rig.transform.eulerAngles.y, legacy ? 0 : 90)) < .01f, "No heading drift during traversal");
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
                    File.WriteAllText(ReportPath, report.ToString(Formatting.Indented));
                    world.settings.clearanceCm = 2.25f;
                    RugosityWorldEditor.KeepAfterStop(world);
                    SessionState.SetBool(Key + ".keep", true);
                    EditorApplication.update -= Traverse;
                    EditorApplication.isPlaying = false;
                }
                return;
            }
            if (legacy) rig.transform.position = new Vector3(-75 + 150f * travelSample / 59, rig.transform.position.y, 0);
            else
            {
                var keyboard = rig.GetComponent<Keyboard>();
                keyboard.SetAutopilotMode(true); keyboard.SetAutopilotSpeed(5);
                keyboard.ApplyInput(.5f);
                expectedTravelX += 2.5f;
            }
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
            var result = JObject.Parse(File.ReadAllText(ReportPath));
            result["keepPlaySettingsAfterStop"] = true;
            result["checks"] = result.Value<int>("checks") + 1;
            File.WriteAllText(ReportPath, result.ToString(Formatting.Indented));
            Debug.Log("RUGOSITY VALIDATION PASSED: " + result["checks"]);
            Finish(0);
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }

    private static void ValidateAgainstColliders(RugosityWorld world, RugosityRig rig, string file)
    {
        Physics.SyncTransforms();
        var collider = world.HeightSurface.colliders.Single();
        bool original = collider.enabled;
        collider.enabled = true;
        Physics.SyncTransforms();
        for (int i = 0; i < 41; i++)
        {
            var p = new Vector3(-70 + i * 3.5f, 20, 1.37f);
            Check(collider.Raycast(new Ray(p, Vector3.down), out var hit, 100), "Independent collider ray hits selected navigation geometry");
            world.HeightSurface.Sample(p, out float sampled, out _);
            Check(Mathf.Abs(sampled - hit.point.y) < .0001f, "Sample height agrees with Unity MeshCollider");
            rig.transform.SetPositionAndRotation(p, Quaternion.Euler(0, 90, 0));
            rig.Conform(0, true);
            if (world.settings.heightMode == RugosityHeightMode.AboveGround)
                Check(Mathf.Abs(rig.transform.position.y - hit.point.y - 1) < .0001f, "Agent clearance above independent collider is exactly 1 cm");
            world.surfaces.Single(s => s.surfaceId == world.settings.visualSurface).Sample(p, out float visible, out _);
            Check(rig.transform.position.y > visible + .1f, file + " camera stays above visible geometry");
        }
        collider.enabled = original;
    }

    private static void ValidateProfiles(RugosityWorld world)
    {
        foreach (var surface in world.surfaces)
        {
            float[] ranges = new float[2];
            for (int side = 0; side < 2; side++)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                for (int i = 0; i < 100; i++)
                {
                    surface.Sample(new Vector3((side == 0 ? -70 : 20) + i * .5f, 0, 0), out float h, out _);
                    lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
                }
                ranges[side] = hi - lo;
                bool rough = surface.surfaceId == "RoughRough" || (surface.surfaceId == "RoughSmooth" && side == 0) || (surface.surfaceId == "SmoothRough" && side == 1);
                Check(rough ? ranges[side] > .3f : ranges[side] < .03f, surface.surfaceId + " left/right spatial roughness");
            }
        }
    }

    private static void ExportCrossSections(RugosityWorld world, RugosityRig rig, ChoiceController choice)
    {
        var panels = new JArray();
        foreach (string file in new[] { "matched-flat", "matched-terrain", "visual-terrain_navigation-flat", "visual-flat_navigation-terrain" })
        {
            choice.InitializeScene(new Dictionary<string, object> { ["configFile"] = "Examples/Rugosity/" + file + ".json" });
            rig.GetComponent<Keyboard>().SetAutopilotMode(false);
            var visible = world.surfaces.Single(s => s.surfaceId == world.settings.visualSurface);
            var samples = new JArray();
            for (int i = 0; i <= 400; i++)
            {
                float x = -20 + i * .1f;
                var p = new Vector3(x, 0, 0);
                rig.transform.SetPositionAndRotation(p, Quaternion.Euler(0, 90, 0)); rig.Conform(0, true);
                visible.Sample(p, out float view, out _);
                world.HeightSurface.Sample(p, out float nav, out _);
                samples.Add(new JObject { ["x"] = x, ["visual"] = view, ["navigation"] = nav,
                    ["eye"] = rig.transform.position.y, ["forwardX"] = rig.transform.forward.x, ["forwardY"] = rig.transform.forward.y });
            }
            panels.Add(new JObject { ["condition"] = file, ["samples"] = samples });
        }
        Directory.CreateDirectory("docs");
        File.WriteAllText("docs/terrain-cue-cross-sections.json", panels.ToString(Formatting.Indented));
    }

    private static void ValidateSmoothedHeading(RugosityWorld world, RugosityRig rig)
    {
        world.ApplyConfiguration(new RugosityConfig { tiltResponse = 4 });
        rig.transform.rotation = Quaternion.Euler(15, 37, -25);
        for (int i = 0; i < 120; i++)
        {
            rig.transform.position = new Vector3(-65 + i * .9f, 5, i % 5);
            Check(rig.Conform(.016f), "Smoothed sample exists");
            Check(Mathf.Abs(Mathf.DeltaAngle(rig.transform.eulerAngles.y, 37)) < .01f, "Tilt smoothing preserves horizontal heading");
        }
        world.ApplyConfiguration(null);
    }

    private static void ValidateDisabledWorld(RugosityWorld world, RugosityRig rig)
    {
        world.enabled = false;
        var rendererStates = world.surfaces.SelectMany(s => s.renderers).Select(r => r.enabled).ToArray();
        RugosityWorld.Apply(world.gameObject.scene, null);
        world.ApplyLiveSettings();
        Check(rendererStates.SequenceEqual(world.surfaces.SelectMany(s => s.renderers).Select(r => r.enabled)), "Disabled world does not change authored renderer roles");
        Check(!rig.ControlsPose, "Disabled world releases camera pose");
        world.enabled = true; world.ApplyLiveSettings();
    }

    private static void ValidateTrackingFrame(RugosityWorld world, RugosityRig rig)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var loop = rig.GetComponent<ClosedLoop>();
        var type = typeof(ClosedLoop);
        var offset = Quaternion.Euler(30, 60, 20);
        var packetType = typeof(ZmqListener).GetNestedType("ZmqMessage", System.Reflection.BindingFlags.NonPublic);
        var packet = Activator.CreateInstance(packetType);
        packetType.GetField("x").SetValue(packet, 1f);
        typeof(ZmqListener).GetMethod("UpdatePose", flags).Invoke(rig.GetComponent<ZmqListener>(), new[] { packet });
        loop.SetSphereDiameter(1);
        loop.SetClosedLoopPosition(true); loop.SetClosedLoopOrientation(false);
        foreach (bool controlled in new[] { true, false })
        {
            world.enabled = controlled;
            if (controlled) world.ApplyLiveSettings();
            type.GetField("_ficTracRotationOffset", flags).SetValue(loop, offset);
            type.GetField("_lastFicTracData", flags).SetValue(loop, Vector3.zero);
            Vector3 before = new Vector3(-30, 2, 0); rig.transform.position = before;
            type.GetMethod("AdvanceTracking", flags).Invoke(loop, new object[] { .016f });
            var movementFrame = controlled ? Quaternion.Euler(0, 60, 0) : offset;
            var expected = movementFrame * (Vector3.forward * .5f);
            Check(Vector3.Distance(rig.transform.position - before, expected) < .0001f,
                controlled ? "Terrain tracking ignores initial tilt without shrinking horizontal stride" : "Non-terrain tracking retains legacy displacement arithmetic");
        }
        loop.SetClosedLoopPosition(false);
        world.enabled = true; world.ApplyLiveSettings();
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
        Check(surface.Sample(p, out float y, out var normal) && Mathf.Abs(y - p.y) < .0001f, "Rotated nonuniform mesh height");
        var expectedNormal = obj.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(new Vector3(-.5f, 1, 0)).normalized;
        Check(Vector3.Angle(normal, expectedNormal) < .05f, "Sample normal agrees with analytic transformed sloping plane");
        obj.transform.position += Vector3.up * 3;
        Check(surface.Sample(p, out y, out _) && Mathf.Abs(y - p.y - 3) < .0001f, "Transform changes invalidate mesh cache");
        Object.DestroyImmediate(obj); Object.DestroyImmediate(mesh);
    }
    private static void Render(Camera camera, string name)
    {
        string folder = legacy ? "docs/rugosity-legacy-renders" : "docs/rugosity-renders";
        Directory.CreateDirectory(folder);
        var target = new RenderTexture(960, 540, 24); var previous = RenderTexture.active;
        var previousTarget = camera.targetTexture; var previousRect = camera.rect;
        camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
        File.WriteAllBytes(folder + "/" + name + ".png", image.EncodeToPNG());
        camera.targetTexture = previousTarget; camera.rect = previousRect; RenderTexture.active = previous; target.Release();
        Object.DestroyImmediate(target); Object.DestroyImmediate(image);
    }
    private static void Finish(int code)
    {
        EditorApplication.update -= Traverse;
        SessionState.SetBool(Key, false); Application.logMessageReceived -= CaptureError;
        EditorApplication.Exit(code);
    }
}
