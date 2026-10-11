#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Disposable-copy render and playback tests; no live inputs or recording session.</summary>
public static class SwarmTrajectoryValidation
{
    private static int checks;
    private static readonly List<object> captures = new List<object>();
    private static readonly List<object> timings = new List<object>();
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable project only.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string failure = null;
        try { DataAndPlayback(); AuthoringAndUnits(); Rendering(); Performance(); }
        catch (Exception error) { failure = error.ToString(); Debug.LogError(failure); }
        File.WriteAllText(Path.Combine(Application.dataPath, "../swarm-trajectory-validation.json"),
            JsonConvert.SerializeObject(new { checks, failure, captures, timings, unity = Application.unityVersion,
                timingNote = "Isolated CPU timings: four rigs, six 64px cameras each, 256 agents per rig. Not hardware-display FPS." }, Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
    private static void DataAndPlayback()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "SwarmData/selwyn-oct7-unity.csv");
        var data = SwarmTrajectoryData.Load(path);
        Check(data.Tracks.Length == 430 && Math.Abs(data.Duration - 599.46) < 1e-6, "Dataset preserves 430 IDs and TimeExact duration.");
        Check(ReferenceEquals(data, SwarmTrajectoryData.Load(path)), "Four rigs share parsed immutable data.");
        var first = data.Tracks.Single(t => t.id == "1");
        Check(Vector3.Distance(first.samples[0].position, new Vector3(-1.761669f,5.314589f,-1.114818f)) < .00001,
            "Canonical metres: west is negative east, Z origin is 5m, source X is north.");
        var fixture = SwarmTrajectoryData.Parse(new[] { "ID,X,Y,Z,time", "A,0,5,0,0", "A,2,5,-2,1", "B,0,5,0,2", "B,1,5,0,3" });
        Check(fixture.Tracks[0].TrySample(.5, 2, out Vector3 p, out Vector3 velocity) && p == new Vector3(1,5,-1), "Time interpolation.");
        Check(velocity == new Vector3(2,0,-2), "Heading derives from trajectory velocity.");
        Check(!fixture.Tracks[0].TrySample(.5,.1,out _,out _), "Missing samples stay invisible across a long gap.");
        var animal = new GameObject("Animal");
        var host = new GameObject("Playback");
        var agents = new[] { new GameObject("A"), new GameObject("B") };
        var settings = new SwarmPlaybackConfig { loop = true, centerOnAnimal = true, periodicBoundary = false, maxGapSeconds = 2 };
        var replay = host.AddComponent<SwarmTrajectoryPlayback>();
        replay.Configure(fixture, settings, agents, animal.transform, null, Vector3.zero, Quaternion.identity, 1);
        Check(agents[0].activeSelf && !agents[1].activeSelf, "Only recorded track lifetimes are visible.");
        Vector3 initial = agents[0].transform.position;
        animal.transform.position = Vector3.right * 100;
        replay.Advance(.5);
        Check(Vector3.Distance(agents[0].transform.position - initial, new Vector3(1,0,-1)) < .0001,
            "An existing track stays world anchored when the animal moves.");
        replay.Advance(2.5);
        Check(!agents[0].activeSelf && agents[1].activeSelf && agents[1].transform.position.x > 99, "New IDs use the animal's current origin.");
        replay.Advance(3.5);
        Check(agents[0].activeSelf && agents[0].transform.position.x > 99, "Loop resets anchors and replays lifetimes.");
        settings.loop = false;
        replay.Advance(4);
        Check(agents.All(a => !a.activeSelf), "Nonlooping playback hides tracks after their last observation.");
        settings.loop = true;
        replay.Configure(fixture, settings, agents, null, null, Vector3.zero, Quaternion.identity, 100);
        replay.Advance(0); initial = agents[0].transform.position; replay.Advance(.5);
        Check(Vector3.Distance(agents[0].transform.position - initial, new Vector3(100,0,-100)) < .001, "Metre data scales correctly in a centimetre scene.");
        var boundary = host.AddComponent<BoundaryManager>(); boundary.boundaryLengthX = boundary.boundaryLengthZ = 10;
        boundary.boundaryHeight = 6; boundary.wrapY = true;
        Check(Vector3.Distance(SwarmTrajectoryPlayback.Wrap(new Vector3(26,10,-26), boundary), new Vector3(-4,-2,4)) < .0001,
            "3D periodic boundary preserves overshoot.");
        var spawner = host.AddComponent<LocustSpawner>(); spawner.agentVisual = "Bogong";
        spawner.trajectoryFile = "SwarmData/selwyn-oct7-unity.csv"; spawner.worldUnitsPerMetre = 1;
        spawner.playbackSettings = new SwarmPlaybackConfig(); spawner.playbackTarget = animal.transform;
        spawner.boundaryManager = boundary; spawner.bogongVisual = new BogongVisualConfig { shape = "Moth" };
        spawner.Rebuild();
        Check(spawner.Spawned.Count == 430 && spawner.Spawned.All(a => !a.GetComponent<LocustMover>().enabled), "Actual spawner replaces random motion with pooled playback.");
        Check(host.GetComponentsInChildren<BogongSwarmRenderer>().Length == 1, "Moth population uses one batch renderer.");
        spawner.PrepareObservation();
        Check(spawner.Spawned.Count == 0, "Observation transition cleans agents and replay.");
        foreach (var agent in agents) Object.DestroyImmediate(agent);
        Object.DestroyImmediate(host); Object.DestroyImmediate(animal);
    }
    private static void Rendering()
    {
        var cameraRoot = new GameObject("64px camera");
        var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false;
        camera.fieldOfView = 90; camera.aspect = 1; camera.nearClipPlane = .001f; camera.farClipPlane = 100;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
        camera.allowHDR = false; camera.allowMSAA = false; camera.cullingMask = 1 << 29;
        var target = new RenderTexture(64,64,24,RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
        camera.rect = new Rect(0,0,1,1);
        captures.Add(new { blank = Capture(camera)[0].ToString(), rect = camera.pixelRect.ToString(), color = camera.backgroundColor.ToString() });
        var root = new GameObject("Moth batch"); root.layer = 29;
        var agent = new GameObject("Moth"); agent.layer = 29;
        agent.transform.rotation = Quaternion.Euler(0,90,0);
        var batch = root.AddComponent<BogongSwarmRenderer>();
        var config = new BogongVisualConfig { shape = "Moth", sizeMode = "World", color = new ColorConfig { r = 0, g = 0, b = 0, a = 1 } };
        batch.Configure(new[] { agent }, config, 1);
        var material = root.GetComponent<MeshRenderer>().sharedMaterial;
        Check(material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader), "Moth shader compiles.");
        material.SetFloat("_ManualAnimation", 1);
        captures.Add(new { graphics = SystemInfo.graphicsDeviceName, camera = camera.projectionMatrix.ToString(), bodyLength = material.GetFloat("_BodyLength"), bodyDiameter = material.GetFloat("_BodyDiameter") });
        try
        {
            float previous = float.MaxValue;
            foreach (float distance in new[] { .01f, .1f, 1f, 2f })
            {
                agent.transform.position = new Vector3(0,0,distance); batch.RefreshCenters();
                Color32[] pixels = Capture(camera);
                float contrast = pixels.Sum(p => (255 - p.r) / 255f);
                int changed = pixels.Count(p => p.r < 255);
                captures.Add(new { distanceMetres = distance, contrast, changedPixels = changed });
                Check(changed > 0, "Moth remains visible at " + distance + "m, including 1cm near field.");
                Check(distance == .01f || contrast < previous, "World size / subpixel contrast decreases with distance (" + distance + ").");
                if (distance >= 1) Check(pixels.Where(p => p.r < 255).All(p => p.r > 0), "Subpixel animal is grey rather than a full black pixel.");
                previous = contrast;
            }
            agent.transform.position = new Vector3(0,0,.1f); batch.RefreshCenters();
            material.SetFloat("_AnimationTime", .25f / 30f); var up = Capture(camera);
            material.SetFloat("_AnimationTime", .75f / 30f); var down = Capture(camera);
            Check(!up.SequenceEqual(down), "GPU wings produce different upper/lower flap poses.");
            File.WriteAllBytes(Path.Combine(Application.dataPath, "../bogong-moth-preview.png"), Encode(camera));
            var preview = new RenderTexture(512,512,24); preview.Create(); camera.targetTexture = preview;
            File.WriteAllBytes(Path.Combine(Application.dataPath, "../bogong-moth-close-preview.png"), Encode(camera));
            camera.targetTexture = target; preview.Release(); Object.DestroyImmediate(preview);
            agent.transform.position = new Vector3(0,0,1); batch.RefreshCenters();
            material.SetFloat("_AnimationTime", .25f / 30f); up = Capture(camera);
            material.SetFloat("_AnimationTime", .75f / 30f); down = Capture(camera);
            Check(!up.SequenceEqual(down), "Subpixel proxy retains faint flap motion.");
            agent.SetActive(false); batch.RefreshCenters();
            Check(Capture(camera).All(p => p.r == 255), "Inactive pooled tracks render no ghost moths.");
            Check(agent.GetComponentsInChildren<Animator>().Length == 0, "Moth does not use Animator callbacks.");
        }
        finally { camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(root); Object.DestroyImmediate(agent); Object.DestroyImmediate(cameraRoot); }
    }
    private static Color32[] Capture(Camera camera)
    {
        camera.Render(); var previous = RenderTexture.active; RenderTexture.active = camera.targetTexture;
        int width = camera.targetTexture.width, height = camera.targetTexture.height;
        var texture = new Texture2D(width,height,TextureFormat.RGBA32,false); texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
        Color32[] result = texture.GetPixels32(); Object.DestroyImmediate(texture); RenderTexture.active = previous; return result;
    }
    private static byte[] Encode(Camera camera)
    {
        var texture = new Texture2D(camera.targetTexture.width,camera.targetTexture.height,TextureFormat.RGBA32,false); texture.SetPixels32(Capture(camera)); texture.Apply();
        byte[] bytes = texture.EncodeToPNG(); Object.DestroyImmediate(texture); return bytes;
    }
    private static void AuthoringAndUnits()
    {
        var root = new GameObject("Bogong moth");
        var moth = root.AddComponent<BogongMoth>();
        Check(root.GetComponentsInChildren<BogongSwarmRenderer>().Length == 1, "Editor asset builds one procedural visual.");
        string path = "Assets/Resources/Bogong/Moth.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        var copy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        Check(copy.GetComponent<BogongMoth>().visual.bodyLengthMetres == .0254f && copy.GetComponentsInChildren<BogongSwarmRenderer>().Length == 1,
            "Saved moth prefab regenerates its configured geometry without duplicated visuals.");
        Object.DestroyImmediate(copy);

        var rigRoot = new GameObject("Unit scale rig"); rigRoot.SetActive(false);
        var rig = rigRoot.AddComponent<ClosedLoop>();
        var cameraRoot = new GameObject("Main Camera F"); cameraRoot.transform.SetParent(rigRoot.transform);
        var camera = cameraRoot.AddComponent<Camera>(); camera.nearClipPlane = .3f; camera.enabled = false;
        var spawner = rigRoot.AddComponent<LocustSpawner>(); spawner.boundaryManager = rigRoot.AddComponent<BoundaryManager>();
        var config = new SwarmConfig { agentVisual = "Bogong", numberOfLocusts = 1, bogongVisual = new BogongVisualConfig { shape = "moth" } };
        SwarmConfiguration.Apply(spawner, config, rig);
        Check(spawner.worldUnitsPerMetre == 100 && Math.Abs(camera.nearClipPlane - .1f) < .000001, "Walking scene uses centimetres; 1mm near plane is .1 world units.");
        spawner.PrepareObservation();
        Check(Math.Abs(camera.nearClipPlane - .3f) < .000001, "Clearing the experiment restores its previous near plane.");
        var terrainData = new TerrainData(); var terrain = Terrain.CreateTerrainGameObject(terrainData);
        SwarmConfiguration.Apply(spawner, config, rig);
        Check(spawner.worldUnitsPerMetre == 1 && Math.Abs(camera.nearClipPlane - .001f) < .000001,
            "Terrain scene defaults to metres; 1mm near plane is .001 world units.");
        var material = rigRoot.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
        Check(Math.Abs(material.GetFloat("_BodyLength") - .0254f) < .000001, "One-inch body is .0254 in a metre scene.");
        spawner.PrepareObservation();
        Object.DestroyImmediate(rigRoot); Object.DestroyImmediate(terrain); Object.DestroyImmediate(terrainData);
    }
    private static void Performance()
    {
        foreach (string shape in new[] { "Dot", "Moth" })
        {
            var roots = new List<GameObject>(); var batches = new List<BogongSwarmRenderer>();
            var cameras = new List<Camera>(); var targets = new List<RenderTexture>();
            var random = new System.Random(64);
            var config = new BogongVisualConfig { shape = shape, sizeMode = "World", size = .0254f };
            for (int rig = 0; rig < 4; rig++)
            {
                Vector3 origin = Vector3.right * rig * 100;
                int layer = 25 + rig;
                var own = new List<GameObject>();
                for (int i = 0; i < 256; i++)
                {
                    var agent = new GameObject("Agent"); roots.Add(agent); own.Add(agent);
                    agent.transform.position = origin + new Vector3((float)random.NextDouble()*6-3,(float)random.NextDouble()*4-2,(float)random.NextDouble()*6-3);
                    agent.transform.rotation = Quaternion.Euler(0,135,0);
                }
                var root = new GameObject("Batch") { layer = layer }; roots.Add(root);
                var batch = root.AddComponent<BogongSwarmRenderer>(); batch.Configure(own,config,1); batches.Add(batch);
                foreach (Quaternion rotation in new[] { Quaternion.identity, Quaternion.Euler(0,90,0), Quaternion.Euler(0,180,0),
                    Quaternion.Euler(0,270,0), Quaternion.Euler(-90,0,0), Quaternion.Euler(90,0,0) })
                {
                    var cameraRoot = new GameObject("Panel"); roots.Add(cameraRoot);
                    var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false;
                    camera.transform.SetPositionAndRotation(origin,rotation); camera.fieldOfView=90; camera.aspect=1;
                    camera.nearClipPlane=.001f; camera.farClipPlane=20; camera.allowHDR=false; camera.allowMSAA=false;
                    camera.cullingMask=1<<layer; camera.backgroundColor=Color.gray; camera.clearFlags=CameraClearFlags.SolidColor;
                    var target = new RenderTexture(64,64,24); target.Create(); targets.Add(target); camera.targetTexture=target;
                    cameras.Add(camera);
                }
            }
            try
            {
                var milliseconds = new List<double>();
                for (int frame = 0; frame < 40; frame++)
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    foreach (var batch in batches) batch.RefreshCenters();
                    foreach (var camera in cameras) camera.Render();
                    stopwatch.Stop(); if (frame >= 10) milliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);
                }
                milliseconds.Sort();
                timings.Add(new { shape, medianCpuMilliseconds=milliseconds[milliseconds.Count/2], p95CpuMilliseconds=milliseconds[(int)(milliseconds.Count*.95)],
                    agents=1024, rigs=4, cameras=24, materials=batches.Count });
                Check(batches.Count == 4 && roots.All(r => r.GetComponent<Animator>() == null), "Four batched " + shape + " populations avoid per-agent renderers/Animators.");
            }
            finally
            {
                foreach (var camera in cameras) camera.targetTexture=null;
                foreach (var target in targets) { target.Release(); Object.DestroyImmediate(target); }
                foreach (var root in roots) Object.DestroyImmediate(root);
            }
        }
    }
}
#endif
