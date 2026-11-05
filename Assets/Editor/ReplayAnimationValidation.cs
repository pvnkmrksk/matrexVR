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

/// <summary>Actual original-controller deformation, reverse seeks, GLB clips and immutable CSV checks.</summary>
public static class ReplayAnimationValidation
{
    private static readonly List<string> checks = new List<string>();
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks.Add(message); }
    private static float Difference(Vector3[] a, Vector3[] b) => a.Length != b.Length ? float.PositiveInfinity : a.Zip(b, Vector3.Distance).DefaultIfEmpty(0).Max();
    private static Vector3[] Deformation(GameObject root)
    {
        var points = new List<Vector3>();
        foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var baked = new Mesh(); skin.BakeMesh(baked);
            foreach (var vertex in baked.vertices) points.Add(root.transform.InverseTransformPoint(skin.transform.TransformPoint(vertex)));
            Object.DestroyImmediate(baked);
        }
        foreach (var bone in root.GetComponentsInChildren<Transform>(true))
        { points.Add(bone.localPosition); points.Add(bone.localEulerAngles); }
        return points.ToArray();
    }
    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    public static async void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Use a marked disposable project.");
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../replay-animation-validation")); Directory.CreateDirectory(output);
        string failure = null;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog")?.Find("SimulatedLocust");
            Check(prefab != null, "Original locust prefab is present in the built catalog");
            var source = Object.Instantiate(prefab);
            var animator = source.GetComponentInChildren<Animator>(true);
            Check(animator != null && animator.runtimeAnimatorController != null, "Original locust uses its original Animator controller");
            animator.enabled = true; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.Rebind(); animator.Update(0);
            int state = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            Check(state != 0, "Original controller resolves its actual default state");
            var capture = new RecordedAnimationCapture(source);
            animator.Play(state, 0, .13f); animator.Update(0);
            string a = capture.Capture(); Vector3[] verticesA = Deformation(source);
            animator.Play(state, 0, .47f); animator.Update(0);
            string b = capture.Capture(); Vector3[] verticesB = Deformation(source);
            // Reusing serializer/parameter storage must preserve all evidence,
            // including fields that are temporarily present during transitions.
            foreach (float phase in new[] { 0f, .15f, .5f, .99f, 1.4f, .15f })
            {
                animator.Play(state, 0, phase); animator.Update(0);
                if (phase == .5f) { animator.CrossFade(state, .25f, 0, .7f); animator.Update(.05f); }
                string cached = capture.Capture();
                var payload = (RecordedAnimationState)typeof(RecordedAnimationCapture).GetField("evidence", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(capture);
                string canonical = JsonConvert.SerializeObject(payload, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                Check(JToken.DeepEquals(JToken.Parse(cached), JToken.Parse(canonical)), "Reused animation JSON preserves the full standard serializer contract at " + phase);
                Check((JObject.Parse(cached)["realizedPose"] != null) == animator.IsInTransition(0), "Reused animation evidence enters/leaves actual transitions without stale pose data at " + phase);
            }

            Check(Difference(verticesA, verticesB) > .0001f, "Original prefab visibly deforms at two recorded animation times");
            var replay = Object.Instantiate(prefab); ReplayEnvironmentLoader.FreezeVisual(replay);
            var driver = replay.AddComponent<ReplayAnimationDriver>();
            driver.Sample(a, 0); Check(Difference(verticesA, Deformation(replay)) < .001f, "Replay reproduces first original deformed mesh/bone pose");
            driver.Sample(b, 1); Check(Difference(verticesB, Deformation(replay)) < .001f, "Forward seek reproduces second original deformed pose");
            driver.Sample(a, 0); Check(Difference(verticesA, Deformation(replay)) < .001f, "Backward seek restores original deformed pose");
            driver.Sample(a, 0); Check(Difference(verticesA, Deformation(replay)) < .001f && !replay.GetComponentInChildren<Animator>().enabled, "Paused replay holds exact recorded phase without real-time advancement");
            string csv = Path.Combine(output, "population.csv");
            File.WriteAllText(csv, "Timestamp,Name,Layer,X,Y,Z,RotationX,RotationY,RotationZ,AnimationState\n" +
                "2026-10-29 10:00:00.000,agent,AgentsVR1,0,0,0,0,0,0," + Csv(a) + "\n" +
                "2026-10-29 10:00:01.000,agent,AgentsVR1,1,0,0,0,90,0," + Csv(b) + "\n");
            string before = RunConfigArchive.Hash(File.ReadAllBytes(csv));
            ReplaySessionArchive.TryTimestamp("2026-10-29 10:00:00.000", out long origin);
            using (var stream = ReplayAgentStream.TryOpen(csv, Path.Combine(output, "cache"), origin))
            {
                Check(stream.HasAnimationState && stream.UsesMemoryCache, "Small CSV converts once to an indexed in-memory binary cache");
                Check(stream.Sample(0)[0].animationState == a && stream.Sample(1)[0].animationState == b && stream.Sample(0)[0].animationState == a, "Quoted JSON animation evidence survives CSV/cache and reverse seeking");
            }
            Check(before == RunConfigArchive.Hash(File.ReadAllBytes(csv)), "Replaying and disposing caches never modifies source recording bytes");
            Check(!Directory.GetFiles(Path.Combine(output, "cache")).Any(), "Processed replay cache is separate and disposable");
            var moth = new GameObject("Original procedural moth");
            var renderer = moth.AddComponent<BogongSwarmRenderer>(); renderer.Configure(new[] { moth }, new BogongVisualConfig { shape = "Moth", flapFrequencyHz = 7, flickerFrequencyHz = 3 });
            renderer.SetRecordedAnimation(.35f, .17f);
            string mothState = new RecordedAnimationCapture(moth).Capture(renderer, 0);
            var mothReplay = new GameObject("Replayed procedural moth");
            var restored = mothReplay.AddComponent<BogongSwarmRenderer>(); restored.Configure(new[] { mothReplay }, new BogongVisualConfig { shape = "Moth", flapFrequencyHz = 7, flickerFrequencyHz = 3 });
            mothReplay.AddComponent<ReplayAnimationDriver>().Sample(mothState, 123);
            var material = restored.GetComponent<MeshRenderer>().sharedMaterial;
            Check(Mathf.Abs(restored.AnimationClock - .35f) < .00001f && Mathf.Abs(restored.AnimationPhase(0) - .17f) < .00001f, "Procedural replay restores original shader clock and per-agent wing phase");
            Check(material.GetFloat("_FlapFrequency") == 7 && material.GetFloat("_FlickerFrequency") == 3 && material.GetFloat("_ManualAnimation") == 1, "Original procedural flap and flicker remain enabled on the recording clock");
            byte[] glb = RigWorldValidation.AnimatedTriangle(); string hash = RunConfigArchive.Hash(glb);
            await ExternalModelLibrary.PreloadAsync("replay-animated.glb", glb);
            var originalModel = ExternalModelLibrary.InstantiatePrefab(ExternalModelLibrary.GetPrefab("replay-animated.glb"));
            ExternalModelLibrary.ConfigureAnimation(originalModel, "Move", 1);
            var legacy = originalModel.GetComponentInChildren<Animation>(); legacy["Move"].time = .7f; legacy.Sample();
            string legacyState = new RecordedAnimationCapture(originalModel).Capture();
            var model = ExternalModelLibrary.InstantiatePrefab(ExternalModelLibrary.GetPrefab("replay-animated.glb"));
            ExternalModelLibrary.ConfigureAnimation(model, "Move", 1); ReplayEnvironmentLoader.FreezeVisual(model);
            var modelDriver = model.AddComponent<ReplayAnimationDriver>(); modelDriver.Sample(legacyState, 0);
            Check(Mathf.Abs(model.transform.Find("Triangle").localPosition.y - .7f) < .001f, "Actual imported GLB Legacy Animation restores its recorded clip time");
            modelDriver.Sample(null, .2); Check(Mathf.Abs(model.transform.Find("Triangle").localPosition.y - .2f) < .001f, "Declared legacy clip can be sampled on a deterministic replay condition clock");
            modelDriver.Sample(legacyState, 0); Check(Mathf.Abs(model.transform.Find("Triangle").localPosition.y - .7f) < .001f, "Reverse seek restores imported GLB deformation");
            Check(hash == RunConfigArchive.Hash(glb), "Imported GLB source bytes remain unchanged during replay");
            var loader = new GameObject("Replay visibility").AddComponent<ReplayEnvironmentLoader>();
            var hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ReplayEnvironmentLoader).GetField("importedName", hidden).SetValue(loader, "Kannadi");
            typeof(ReplayEnvironmentLoader).GetField("currentConfig", hidden).SetValue(loader, JObject.Parse("{\"populationMode\":\"Mirror\"}"));
            foreach (int count in new[] { 1, 2, 4, 8, 16 }) foreach (int focal in Enumerable.Range(1, count))
            {
                int mask = loader.RecordedAnimalCameraMask("VR" + focal, ReplayController.AnimalCameraMask("VR" + focal));
                Check((mask & (1 << RuntimeRigRegistry.StimulusLayer(focal))) == 0, "Replay VR" + focal + " hides own body at count " + count);
                foreach (int peer in Enumerable.Range(1, count).Where(id => id != focal)) Check((mask & (1 << RuntimeRigRegistry.StimulusLayer(peer))) != 0, "Replay VR" + focal + " sees peer " + peer + " at count " + count);
            }
            Object.DestroyImmediate(source); Object.DestroyImmediate(replay); Object.DestroyImmediate(moth); Object.DestroyImmediate(mothReplay);
            Object.DestroyImmediate(originalModel); Object.DestroyImmediate(model); Object.DestroyImmediate(loader.gameObject);
        }
        catch (Exception error) { failure = error.ToString(); Debug.LogError(failure); }
        File.WriteAllText(Path.Combine(output, "results.json"), JsonConvert.SerializeObject(new { checks = checks.Count, passed = checks, failure, unity = Application.unityVersion,
            scope = "Original Unity controllers and deformed geometry, procedural material clock/phase, imported animated GLB, CSV cache and immutable source hashes. No physical display or native pointer claim." }, Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
#endif
