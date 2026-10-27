using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Actual camera texture-scale comparisons and material/geometry preservation.</summary>
public static class TerrainTextureScaleValidation
{
    public static void Run()
    {
        if (!File.Exists("KANNADI_VALIDATION_COPY")) throw new InvalidOperationException("Disposable project only.");
        int checks = 0; var observations = new List<object>();
        void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
        string output = Path.GetFullPath("terrain-texture-scale-validation"); Directory.CreateDirectory(output);
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/TerrainChoice_RoughLeft_SmoothRight.unity");
            // Exercise the production wire decoder and motion function with a
            // controlled, inactive no-socket listener: one sphere radian is r cm.
            var numeric = Enumerable.Repeat("0", 25).ToArray(); numeric[0] = "1"; numeric[14] = "1"; numeric[22] = "1"; numeric[24] = "10";
            var decoded = TrackerWireDecoder.Decode("FT, " + string.Join(", ", numeric), ClosedLoopMode.Hejjetrac);
            Check(decoded.ficTracRow && decoded.position.x == 1, "FT column15 decodes one sphere-radian forward displacement.");
            var unitObject = new GameObject("Inactive walking-unit fixture"); unitObject.SetActive(false);
            var input = unitObject.AddComponent<ZmqListener>(); var motion = unitObject.AddComponent<ClosedLoop>(); motion.SetSphereDiameter(2.6f);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var packetType = typeof(ZmqListener).GetNestedType("ZmqMessage", System.Reflection.BindingFlags.NonPublic);
            var packet = Activator.CreateInstance(packetType); packetType.GetField("x").SetValue(packet, decoded.position.x); packetType.GetField("y").SetValue(packet, decoded.position.y);
            typeof(ZmqListener).GetMethod("UpdatePose", flags).Invoke(input, new[] { packet });
            typeof(ClosedLoop).GetField("_zmqListener", flags).SetValue(motion, input); typeof(ClosedLoop).GetField("_ficTracRotationOffset", flags).SetValue(motion, Quaternion.identity);
            foreach (float gain in new[] { 1f, .01f })
            {
                motion.SetLocustGains(gain, 1); unitObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                typeof(ClosedLoop).GetField("_lastFicTracData", flags).SetValue(motion, Vector3.zero);
                typeof(ClosedLoop).GetMethod("AdvanceTracking", flags).Invoke(motion, new object[] { 1f });
                Check(Mathf.Abs(unitObject.transform.position.z - 1.3f * gain) < .00001f, "Production walking conversion uses the sphere radius in centimeters and the explicit gain.");
            }
            Object.DestroyImmediate(unitObject);
            var world = Object.FindFirstObjectByType<RugosityWorld>();
            Check(world.settings.textureScale == 1, "Existing authored scene inherits multiplier one.");
            Check(JsonConvert.DeserializeObject<RugosityConfig>("{}").textureScale == null, "An omitted JSON multiplier retains scene tuning.");
            var example = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Examples/Rugosity/terrain-treatment.json")));
            Check((float)example["rugosity"]["textureScale"] == 4, "The shared example opts into four-times tiling explicitly.");
            var rig = Object.FindFirstObjectByType<RugosityRig>();
            var camera = rig.GetComponentsInChildren<Camera>(true).Single(c => c.name == "Main Camera F");
            foreach (var item in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) item.enabled = false;
            var ground = world.surfaces.Single(s => s.surfaceId == "RoughSmooth").renderers.First();
            var material = ground.sharedMaterial; Vector2 authored = material.mainTextureScale;
            string materialPath = AssetDatabase.GetAssetPath(material); byte[] originalMaterial = File.ReadAllBytes(materialPath);
            var original = new MaterialPropertyBlock(); ground.GetPropertyBlock(original);
            var sentinel = new MaterialPropertyBlock(); ground.GetPropertyBlock(sentinel); sentinel.SetFloat("_TextureScaleValidationSentinel", 17); ground.SetPropertyBlock(sentinel);
            Vector3 originalPosition = default; Quaternion originalRotation = default; string baseline = null;
            foreach (float multiplier in new[] { 1f, 2f, 4f, 8f, 1f })
            {
                world.ApplyConfiguration(new RugosityConfig { textureScale = multiplier });
                rig.transform.SetPositionAndRotation(new Vector3(-53.125f, 1, 28.125f), Quaternion.Euler(0, 55, 0));
                Check(rig.Conform(0, true), "Actual rig follows the navigation surface.");
                Check(Mathf.Abs(rig.transform.position.y - rig.GroundHeight - 1) < .0001f, "Eye remains exactly one centimeter above the selected navigation surface.");
                if (observations.Count == 0) { originalPosition = rig.transform.position; originalRotation = rig.transform.rotation; }
                Check(Vector3.Distance(originalPosition, rig.transform.position) < .00001f && Quaternion.Angle(originalRotation, rig.transform.rotation) < .00001f, "Texture scale does not change animal height or orientation.");
                var block = new MaterialPropertyBlock(); ground.GetPropertyBlock(block);
                Check(block.GetFloat("_TextureScaleValidationSentinel") == 17, "Unrelated property-block values are preserved.");
                if (multiplier != 1) { var vector = block.GetVector("_MainTex_ST"); Check(Mathf.Abs(vector.x - authored.x * multiplier) < .0001f && Mathf.Abs(vector.y - authored.y * multiplier) < .0001f, "Texture repetition multiplies the authored tiling."); }
                string name = multiplier == 1 && observations.Count > 0 ? "texture-1-restored.png" : "texture-" + multiplier + ".png";
                byte[] bytes = Render(camera, 768, 768); File.WriteAllBytes(Path.Combine(output, name), bytes);
                File.WriteAllBytes(Path.Combine(output, name.Replace(".png", "-led128.png")), Render(camera, 128, 128));
                string digest = Hash(bytes);
                if (observations.Count == 0) baseline = digest;
                else if (multiplier == 1) Check(digest == baseline, "Returning to one restores exact original camera pixels.");
                else Check(digest != baseline, "Actual camera pixels change with the requested texture scale.");
                Check(material == ground.sharedMaterial && material.mainTextureScale == authored, "Shared material instance and authored scale remain unchanged.");
                observations.Add(new { multiplier, image = name, sha256 = digest, position = new[] { rig.transform.position.x, rig.transform.position.y, rig.transform.position.z }, tiltDegrees = Vector3.Angle(Vector3.up, rig.transform.up), fov = camera.fieldOfView, clearanceCm = 1 });
            }
            world.ApplyConfiguration(new RugosityConfig { textureScale = 4 });
            world.enabled = false;
            // Edit-mode harnesses do not receive MonoBehaviour disable callbacks.
            // Exercise the exact production callback rather than pretending Play ran.
            typeof(RugosityWorld).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(world, null);
            var restored = new MaterialPropertyBlock(); ground.GetPropertyBlock(restored);
            Check(restored.GetFloat("_TextureScaleValidationSentinel") == 17 && !restored.HasVector("_MainTex_ST"), "Disabling restores the original property block.");
            world.ApplyReplayTextureScale(4);
            Check(!world.enabled && !world.IsReady, "Replay tiling does not activate the frozen world or pose controller.");
            Check(Hash(Render(camera, 768, 768)) == observations.Select(value => Newtonsoft.Json.Linq.JObject.FromObject(value)).Single(value => (float)value["multiplier"] == 4)["sha256"].ToString(), "Frozen replay world restores exact four-times camera pixels.");
            world.ApplyReplayTextureScale(null);
            Check(Hash(Render(camera, 768, 768)) == baseline, "Seeking back to an omitted legacy multiplier restores exact authored one-times pixels.");
            var replayObject = new GameObject("Inactive replay-restore fixture"); replayObject.SetActive(false);
            var replay = replayObject.AddComponent<ReplayEnvironmentLoader>();
            var replayScene = typeof(ReplayEnvironmentLoader).GetField("importedScene", flags);
            var restore = typeof(ReplayEnvironmentLoader).GetMethod("RestoreRugosity", flags);
            try
            {
                replayScene.SetValue(replay, world.gameObject.scene);
                restore.Invoke(replay, new object[] { Newtonsoft.Json.Linq.JObject.Parse("{\"rugosity\":{\"textureScale\":4}}") });
                Check(Hash(Render(camera, 768, 768)) == observations.Select(value => Newtonsoft.Json.Linq.JObject.FromObject(value)).Single(value => (float)value["multiplier"] == 4)["sha256"].ToString(), "Production replay restores tiling when visualSurface is inherited from the scene.");
                Check(!world.enabled && !world.IsReady && world.surfaces.SelectMany(surface => surface.colliders).All(collider => !collider.enabled), "Production replay keeps all terrain collision and live pose controls disabled.");
                restore.Invoke(replay, new object[] { Newtonsoft.Json.Linq.JObject.Parse("{\"rugosity\":{}}") });
                Check(Hash(Render(camera, 768, 768)) == baseline, "Production replay restores authored pixels for a legacy omitted multiplier.");
            }
            finally { replayScene.SetValue(replay, default(UnityEngine.SceneManagement.Scene)); Object.DestroyImmediate(replayObject); }
            ground.SetPropertyBlock(original);
            Check(File.ReadAllBytes(materialPath).SequenceEqual(originalMaterial), "Material asset bytes are unchanged.");
            foreach (float value in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                bool rejected = false; try { world.Validate(new RugositySettings { textureScale = value }); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Invalid scale is rejected before application.");
            }
            ConfigurationSchemaBuilder.Generate();
            File.WriteAllText(Path.Combine(output, "results.json"), JsonConvert.SerializeObject(new { checks, failure = (string)null, observations, scope = "Actual authored front camera in Unity Editor at fixed steep probe. Only albedo tiling changes; one-centimeter clearance, geometry, material asset and original property block restoration verified. Not a physical calibration claim." }, Formatting.Indented));
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "results.json"), JsonConvert.SerializeObject(new { checks, failure = error.ToString(), observations }, Formatting.Indented));
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
    private static string Hash(byte[] bytes)
    { using (var hash = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    private static byte[] Render(Camera camera, int width, int height)
    {
        var texture = new RenderTexture(width, height, 24); texture.Create();
        var previous = camera.targetTexture; var rect = camera.rect; var active = RenderTexture.active;
        try
        {
            camera.targetTexture = texture; camera.rect = new Rect(0, 0, 1, 1); camera.Render(); RenderTexture.active = texture;
            var output = new Texture2D(width, height, TextureFormat.RGB24, false); output.ReadPixels(new Rect(0, 0, width, height), 0, 0); output.Apply();
            byte[] bytes = output.EncodeToPNG(); Object.DestroyImmediate(output); return bytes;
        }
        finally { camera.targetTexture = previous; camera.rect = rect; RenderTexture.active = active; texture.Release(); Object.DestroyImmediate(texture); }
    }
}
