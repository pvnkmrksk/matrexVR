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
        try { DataAndPlayback(); ContinuingTracks(); IncomingTracks(); CompactBatch(); CompactRasterParity(); AuthoringAndUnits(); Rendering(); Performance(); }
        catch (Exception error) { failure = error.ToString(); Debug.LogError(failure); }
        File.WriteAllText(Path.Combine(Application.dataPath, "../swarm-trajectory-validation.json"),
            JsonConvert.SerializeObject(new { checks, failure, captures, timings, unity = Application.unityVersion,
                timingNote = "Isolated CPU timings: four rigs, six 64px cameras each, 256 agents per rig. Not hardware-display FPS." }, Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
    private static void CompactBatch()
    {
        var host=new GameObject("Compact batch");
        var roots=new[] {new GameObject("First"),new GameObject("Second"),new GameObject("Third")};
        try
        {
            var batch=host.AddComponent<BogongSwarmRenderer>();batch.Configure(roots,new BogongVisualConfig {shape="Moth"},1);
            var mesh=host.GetComponent<MeshFilter>().sharedMesh;float secondPhase=mesh.uv2[BogongMothMesh.VerticesPerAgent].x;
            Check(mesh.GetIndexCount(0)==3*BogongMothMesh.IndicesPerAgent,"Compact draw range includes every body, wing and subpixel proxy triangle.");
            roots[0].SetActive(false);batch.RefreshCenters();
            Check(batch.ActiveAgentCount==2 && batch.UploadedVertexCount==88 && mesh.GetIndexCount(0)==2*BogongMothMesh.IndicesPerAgent,
                "Inactive slots neither upload pose vertices nor issue geometry.");
            Check(mesh.uv2[0].x==secondPhase,"Compaction preserves each moth's wing phase when an earlier slot retires.");
            foreach(var root in roots) root.SetActive(false);batch.RefreshCenters();
            Check(batch.UploadedVertexCount==0 && mesh.GetIndexCount(0)==0 && !host.GetComponent<MeshRenderer>().enabled,
                "An empty population skips GPU uploads and rendering entirely.");
            roots[1].SetActive(true);batch.RefreshCenters();
            Check(batch.ActiveAgentCount==1 && mesh.uv2[0].x==secondPhase,"Pool reactivation restores only the live moth with its original phase.");
        }
        finally {Object.DestroyImmediate(host);foreach(var root in roots) Object.DestroyImmediate(root);}
    }
    private static void IncomingTracks()
    {
        var data=SwarmTrajectoryData.Parse(new[] {"ID,X,Y,Z,time","A,0,0,0,0","A,1,0,0,1","B,5,0,0,5","B,6,0,0,6"});
        var animal=new GameObject("Incoming animal"); var host=new GameObject("Incoming host");
        var roots=new List<GameObject> {new GameObject("Incoming_ID_pool"),new GameObject("Incoming_ID_pool")};
        var settings=new SwarmPlaybackConfig {continueAfterTrack=true,periodicBoundary=false,loop=false,
            maxGapSeconds=2,preRollSeconds=2,continuationSpeedMetresPerSecond=6};
        var replay=host.AddComponent<SwarmTrajectoryPlayback>();
        Func<GameObject> grow=()=> {var root=new GameObject("Incoming_ID_pool");roots.Add(root);return root;};
        try
        {
            replay.Configure(data,settings,roots,animal.transform,null,Vector3.zero,Quaternion.identity,1,grow);
            replay.Advance(2.99); Check(replay.TotalBirths==1,"An incoming track is not spawned before its configured lead-in.");
            replay.Advance(3); var b=roots.Single(r=>r.name.EndsWith("_ID_B"));
            Vector3 anchor=animal.transform.position-data.Centroid;
            Check(Vector3.Distance(b.transform.position,anchor+new Vector3(-7,0,0))<1e-5,"Incoming line uses first-sample direction at 6m/s, two seconds before the recorded start.");
            animal.transform.position=Vector3.up*10; replay.Advance(4);
            Check(Vector3.Distance(b.transform.position,anchor+new Vector3(-1,0,0))<1e-5,"An incoming moth is already world anchored and cannot follow later animal motion.");
            replay.Advance(5.5);
            Check(Vector3.Distance(b.transform.position,anchor+new Vector3(5.5f,0,0))<1e-5,"Lead-in preserves exact recorded interpolation rather than stretching the samples.");
            replay.Advance(6.5);
            Check(Vector3.Distance(b.transform.position,anchor+new Vector3(9,0,0))<1e-5,"The same moth continues forward after its recorded end.");
            settings.loop=true; animal.transform.position=Vector3.zero;
            replay.Configure(data,settings,roots,animal.transform,null,Vector3.zero,Quaternion.identity,1,grow);
            replay.Advance(4.01);
            Check(roots.Any(r=>r.name.EndsWith("_ID_A_C1_S0")),"Incoming tracks of the next loop begin before the loop boundary.");
            long births=replay.TotalBirths; replay.Advance(4.01);
            Check(replay.TotalBirths==births,"Repeated calls at the same clock time never duplicate cursor births.");
            replay.Advance(3.5);
            Check(replay.TotalBirths==1,"Seeking resets the cursor and initializes the current incoming instance.");

            var distant=SwarmTrajectoryData.Parse(new[] {"ID,X,Y,Z,time","A,0,0,0,0","A,1,0,0,1","B,0,0,0,80","B,1,0,0,81"});
            settings.loop=false;settings.preRollSeconds=30;
            replay.Configure(distant,settings,roots,animal.transform,null,Vector3.zero,Quaternion.identity,1,grow);
            replay.Advance(50); b=roots.Single(r=>r.name.EndsWith("_ID_B"));
            Check(!b.activeSelf && replay.TotalBirths==2,"A long incoming line outside 100m remains pending without rendering.");
            replay.Advance(65);
            Check(b.activeSelf && Vector3.Distance(b.transform.position,animal.transform.position)<=100,"Pending incoming moth becomes visible when it enters the cutoff.");
            replay.Advance(80);
            Check(Vector3.Distance(b.transform.position,new Vector3(-.5f,0,0))<1e-5,"Distance clipping never discards a pending recorded arrival.");
            settings.preRollSeconds=-1; bool rejected=false;
            try {settings.Validate();} catch(ArgumentException) {rejected=true;}
            Check(rejected,"Negative incoming extension is rejected.");
            settings.preRollSeconds=1;settings.continueAfterTrack=false;rejected=false;
            try {settings.Validate();} catch(ArgumentException) {rejected=true;}
            Check(rejected,"Incoming extension cannot silently affect legacy recorded-lifetime mode.");
        }
        finally {Object.DestroyImmediate(host);foreach(var root in roots) Object.DestroyImmediate(root);Object.DestroyImmediate(animal);}
    }
    private static void CompactRasterParity()
    {
        var root=new GameObject("Compact reference") {layer=29};var oldRoot=new GameObject("Full slot reference") {layer=29};
        var cameraRoot=new GameObject("Parity camera");var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;
        camera.fieldOfView=90;camera.aspect=1;camera.nearClipPlane=.001f;camera.farClipPlane=100;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;camera.cullingMask=1<<29;
        camera.allowHDR=false;camera.allowMSAA=false;
        var rt=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;
        var agents=Enumerable.Range(0,4).Select(i=>new GameObject("Parity moth "+i)).ToArray();
        var reference=BogongMothMesh.Create(4);
        try
        {
            var batch=root.AddComponent<BogongSwarmRenderer>();batch.Configure(agents,new BogongVisualConfig {shape="Moth"},1);
            var renderer=root.GetComponent<MeshRenderer>();var material=renderer.sharedMaterial;material.SetFloat("_ManualAnimation",1);
            oldRoot.AddComponent<MeshFilter>().sharedMesh=reference;var old=oldRoot.AddComponent<MeshRenderer>();old.sharedMaterial=material;
            float[] phases=(float[])typeof(BogongSwarmRenderer).GetField("agentPhases",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(batch);
            foreach(float distance in new[] {.177f,.5f,1f,3f})
            {
                // Swarm hosts are children of moving/rotating rigs. Poses remain world-space.
                root.transform.SetPositionAndRotation(new Vector3(13,7,-11),Quaternion.Euler(20,130,10));
                root.transform.localScale=Vector3.one*.7f;
                var positions=new Vector3[reference.vertexCount];var rotations=new Vector4[reference.vertexCount];var visible=new Vector2[reference.vertexCount];
                for(int i=0;i<4;i++)
                {
                    agents[i].SetActive(i==1 || i==3);agents[i].transform.SetPositionAndRotation(new Vector3((i-2)*distance*.08f,0,distance),Quaternion.Euler(0,90+i*17,0));
                    Quaternion q=agents[i].transform.rotation;
                    for(int v=0;v<BogongMothMesh.VerticesPerAgent;v++)
                    {
                        int index=i*BogongMothMesh.VerticesPerAgent+v;positions[index]=agents[i].transform.position;
                        rotations[index]=new Vector4(q.x,q.y,q.z,q.w);visible[index]=new Vector2(phases[i],agents[i].activeSelf?1:0);
                    }
                }
                reference.SetVertices(positions);reference.SetTangents(rotations);reference.uv2=visible;
                old.bounds=new Bounds(Vector3.forward*distance,Vector3.one*100);
                foreach(float time in new[] {.001f,.007f})
                {
                    material.SetFloat("_AnimationTime",time);batch.RefreshCenters();old.enabled=false;var compact=Capture(camera);
                    renderer.enabled=false;old.enabled=true;var full=Capture(camera);
                    Check(compact.SequenceEqual(full),"Live-only world poses ignore the host transform and match the full-slot shader at "+distance+"m, time "+time+".");
                }
            }
        }
        finally
        {
            camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(reference);
            Object.DestroyImmediate(root);Object.DestroyImmediate(oldRoot);Object.DestroyImmediate(cameraRoot);foreach(var agent in agents) Object.DestroyImmediate(agent);
        }
    }
    private static void ContinuingTracks()
    {
        var data = SwarmTrajectoryData.Parse(new[] { "ID,X,Y,Z,time", "A,0,5,0,0", "A,1,5,0,1", "B,0,5,0,2", "B,1,5,0,3" });
        var animal = new GameObject("Stream animal"); animal.transform.position = new Vector3(10,5,20);
        var host = new GameObject("Stream host");
        var roots = new List<GameObject> { new GameObject("Stream_ID_A"), new GameObject("Stream_ID_B") };
        var settings = new SwarmPlaybackConfig { continueAfterTrack = true, periodicBoundary = false,
            loop = false, maxGapSeconds = 2, terminalVelocityWindowSeconds = .1f, despawnDistanceMetres = 100 };
        var replay = host.AddComponent<SwarmTrajectoryPlayback>();
        int revisions = 0;
        Func<GameObject> grow = () => { var root = new GameObject("Stream_ID_pool"); roots.Add(root); return root; };
        try
        {
            replay.Configure(data,settings,roots,animal.transform,null,Vector3.zero,Quaternion.identity,1,grow,()=>revisions++);
            var a = roots.Single(r=>r.activeSelf); Vector3 anchor = a.transform.position - data.Tracks[0].samples[0].position;
            Check(Vector3.Distance(anchor + data.Centroid,animal.transform.position) < 1e-6,"Sample centroid aligns with the animal exactly at birth.");
            animal.transform.position += Vector3.right * 10; replay.Advance(.5);
            Check(Vector3.Distance(a.transform.position,anchor + new Vector3(.5f,5,0)) < 1e-6,"Animal translation cannot drag a live moth.");
            animal.transform.rotation = Quaternion.Euler(0,180,0); replay.Advance(1.5);
            Check(a.activeSelf && Vector3.Distance(a.transform.position,anchor + new Vector3(1.5f,5,0)) < 1e-6,"Recorded endpoint continues at final velocity without steering with the animal.");
            replay.Advance(2.1); var b = roots.Single(r=>r.activeSelf && r!=a);
            Check(Vector3.Distance(b.transform.position - new Vector3(.1f,5,0) + data.Centroid,animal.transform.position) < .00001,"A later birth centres its volume on the animal's new position.");
            replay.Advance(6);
            Check(a.activeSelf && b.activeSelf && Vector3.Distance(a.transform.position,anchor + new Vector3(6,5,0)) < .00001,"Tail keeps flying after the entire nonlooping CSV ends.");
            animal.transform.position = anchor + new Vector3(6,5,0); replay.Advance(7); animal.transform.position += Vector3.right;
            Check(a.activeSelf && Vector3.Distance(a.transform.position,animal.transform.position) < 1e-5,"A co-moving animal can remain beside the continuing moth.");
            animal.transform.position += Vector3.back * 200; replay.Advance(7.1);
            Check(replay.ActiveCount==0 && roots.All(r=>!r.activeSelf),"Crossing 100m from the current animal retires all distant moths.");
            animal.transform.position = Vector3.zero; replay.Advance(8);
            Check(replay.ActiveCount==0,"Retired moths cannot reappear without another recorded birth.");

            settings.loop=true; replay.Configure(data,settings,roots,animal.transform,null,Vector3.zero,Quaternion.identity,1,grow,()=>revisions++);
            var first = roots.Single(r=>r.activeSelf); replay.Advance(3.01);
            Check(first.activeSelf && replay.ActiveCount==3 && roots.Count==3,"Loop births are independent; previous tails survive and the pool can grow.");
            Check(roots.Any(r=>r.name.EndsWith("_ID_A_C1_S0")),"Repeated source IDs have distinguishable log identities.");
            var batch=host.AddComponent<BogongSwarmRenderer>(); batch.Configure(roots,new BogongVisualConfig {shape="Moth"},1);
            int oldVertices=host.GetComponent<MeshFilter>().sharedMesh.vertexCount;
            float phase=host.GetComponent<MeshFilter>().sharedMesh.uv2[0].x;
            replay.Advance(6.01); batch.RefreshCenters();
            Check(replay.ActiveCount==5 && roots.Count==5 && host.GetComponent<MeshFilter>().sharedMesh.vertexCount>oldVertices,"Overlapping loop populations grow the shared mesh without per-agent renderers.");
            Check(host.GetComponent<MeshFilter>().sharedMesh.uv2[0].x==phase && host.GetComponents<MeshRenderer>().Length==1,"Pool growth preserves existing wing phases and one renderer.");
            replay.Advance(.25);
            Check(replay.ActiveCount==1,"Backward seeking clears tails and starts only tracks observed at that time.");

            settings.loop=false; replay.Configure(data,settings,roots,animal.transform,null,Vector3.zero,Quaternion.Euler(0,90,0),100,grow,()=>revisions++);
            first=roots.Single(r=>r.activeSelf); var start=first.transform.position;
            replay.Advance(2);
            Check(Vector3.Distance(first.transform.position-start,Quaternion.Euler(0,90,0)*new Vector3(200,0,0))<.001,"Continuation preserves CSV speed and yaw in centimetre scenes.");
            animal.transform.position += Vector3.up*10001; replay.Advance(2.01);
            Check(replay.ActiveCount==0,"100m cutoff becomes 10,000 world units in a centimetre scene.");
            Check(revisions>0,"Pool births update logger identity caches.");

            var gaps=SwarmTrajectoryData.Parse(new[] {"ID,X,Y,Z,time","G,0,0,0,0","G,1,0,0,.1","G,2,0,0,1","G,3,0,0,1.1"});
            settings.maxGapSeconds=.2f; animal.transform.position=Vector3.zero;
            replay.Configure(gaps,settings,roots,animal.transform,null,Vector3.zero,Quaternion.identity,1,grow,()=>revisions++);
            replay.Advance(1.01);
            Check(replay.ActiveCount==2 && roots.Any(r=>r.activeSelf && r.name.EndsWith("_ID_G_C0_S1")),"Missing-data gaps split independent recorded segments and never interpolate a false bridge.");
            settings.periodicBoundary=true; bool rejected=false;
            try { settings.Validate(); } catch(ArgumentException) { rejected=true; }
            Check(rejected,"Continuing tracks cannot silently recycle through a periodic volume.");

            settings.periodicBoundary=false; settings.loop=false; settings.continuationSpeedMetresPerSecond=6;
            settings.maxGapSeconds=2; animal.transform.position=Vector3.zero;
            replay.Configure(data,settings,roots,animal.transform,null,Vector3.zero,Quaternion.identity,1,grow,()=>revisions++);
            first=roots.Single(r=>r.activeSelf); start=first.transform.position; replay.Advance(.5);
            Check(Vector3.Distance(first.transform.position-start,Vector3.right*.5f)<1e-5,"A tail-speed override preserves recorded positions and times.");
            replay.Advance(1.5);
            Check(Vector3.Distance(first.transform.position-start,Vector3.right*4)<1e-5,"Tail alone continues at 6m/s after the recorded endpoint.");
            start=first.transform.position; replay.Advance(1.75);
            Check(Vector3.Distance(first.transform.position-start,Vector3.right*1.5f)<1e-5,"Continuing velocity is exactly 6m/s in a metre scene.");
            Check(replay.TotalBirths==1,"Birth counts describe arrivals, not the allocated root pool.");
            settings.dataUnitsPerMetre=100;
            replay.Configure(data,settings,roots,animal.transform,null,Vector3.zero,Quaternion.identity,100,grow,()=>revisions++);
            first=roots.Single(r=>r.activeSelf); replay.Advance(1.5); start=first.transform.position; replay.Advance(1.75);
            Check(Vector3.Distance(first.transform.position-start,Vector3.right*150)<.001,"6m/s becomes 600cm/s for centimetre CSV and scene units.");
            settings.continuationSpeedMetresPerSecond=-1; rejected=false;
            try { settings.Validate(); } catch(ArgumentException) { rejected=true; }
            Check(rejected,"Invalid physical tail speeds are rejected.");
        }
        finally { Object.DestroyImmediate(host); foreach(var root in roots) Object.DestroyImmediate(root); Object.DestroyImmediate(animal); }
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
        float wingPhase=root.GetComponent<MeshFilter>().sharedMesh.uv2[0].x;
        captures.Add(new { graphics = SystemInfo.graphicsDeviceName, camera = camera.projectionMatrix.ToString(), bodyLength = material.GetFloat("_BodyLength"), bodyDiameter = material.GetFloat("_BodyDiameter") });
        try
        {
            float previous = float.MaxValue;
            foreach (float distance in new[] { .01f, .1f, 1f, 2f })
            {
                agent.transform.position = new Vector3(0,0,distance); batch.RefreshCenters();
                Color32[] pixels = Capture(camera);
                Check(pixels.All(p=>p.a==255), "Composited camera output stays opaque at " + distance + "m; previews do not apply contrast twice.");
                float contrast = pixels.Sum(p => (255 - p.r) / 255f);
                int changed = pixels.Count(p => p.r < 255);
                captures.Add(new { distanceMetres = distance, contrast, changedPixels = changed });
                Check(changed > 0, "Moth remains visible at " + distance + "m, including 1cm near field.");
                Check(distance == .01f || contrast < previous, "World size / subpixel contrast decreases with distance (" + distance + ").");
                if (distance >= 1) Check(pixels.Where(p => p.r < 255).All(p => p.r > 0), "Subpixel animal is grey rather than a full black pixel.");
                previous = contrast;
            }
            agent.transform.position = new Vector3(0,0,.1f); batch.RefreshCenters();
            material.SetFloat("_AnimationTime", (.25f-wingPhase) / 30f); var up = Capture(camera);
            material.SetFloat("_AnimationTime", (.75f-wingPhase) / 30f); var down = Capture(camera);
            Check(!up.SequenceEqual(down), "GPU wings produce different upper/lower flap poses.");
            File.WriteAllBytes(Path.Combine(Application.dataPath, "../bogong-moth-preview.png"), Encode(camera));
            var preview = new RenderTexture(512,512,24); preview.Create(); camera.targetTexture = preview;
            File.WriteAllBytes(Path.Combine(Application.dataPath, "../bogong-moth-close-preview.png"), Encode(camera));
            camera.targetTexture = target; preview.Release(); Object.DestroyImmediate(preview);
            agent.transform.position = new Vector3(0,0,1); batch.RefreshCenters();
            material.SetFloat("_AnimationTime", (.25f-wingPhase) / 30f); up = Capture(camera);
            material.SetFloat("_AnimationTime", (.75f-wingPhase) / 30f); down = Capture(camera);
            Check(!up.SequenceEqual(down), "Subpixel proxy retains faint flap motion.");
            var grayTarget=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32); grayTarget.Create();
            try
            {
                camera.targetTexture=grayTarget; camera.backgroundColor=Color.gray; material.SetFloat("_WingOpacity",0);
                // Center on a pixel rather than on the four-pixel boundary at image center.
                float pixelMetres=6f/128f;
                agent.transform.position=new Vector3(.5f*pixelMetres,.5f*pixelMetres,3); batch.RefreshCenters();
                var gray=Capture(camera); int blank=gray[0].r;
                int peak=gray.Max(p=>blank-p.r), sum=gray.Sum(p=>blank-p.r);
                captures.Add(new {mode="gray128-first",blank,peak,total=sum,colorSpace=QualitySettings.activeColorSpace.ToString()});
                float expected=Mathf.PI*config.bodyLengthMetres*config.bodyDiameterMetres*.25f*Mathf.Pow(64f/3,2)*blank;
                Check(peak>=expected*.65f && Math.Abs(sum-expected)<=3,
                    "A broadside 3m subpixel moth concentrates its real projected area without increasing total contrast.");
                var energy=new List<int>();
                foreach(float phaseOffset in new[] {0f,.25f,.5f,.75f})
                {
                    agent.transform.position=new Vector3((.5f+phaseOffset)*pixelMetres,(.5f+phaseOffset)*pixelMetres,3);
                    batch.RefreshCenters(); gray=Capture(camera); energy.Add(gray.Sum(p=>blank-p.r));
                }
                Check(energy.All(v=>v>=2) && energy.Max()-energy.Min()<=3,"Fractional pixel movement retains subpixel contrast rather than blinking out.");
                captures.Add(new {mode="gray128",distanceMetres=3,peakContrastBytes=peak,totalContrastBytes=sum,fractionalPixelContrastBytes=energy});
                agent.transform.rotation=Quaternion.identity;
                agent.transform.position=new Vector3(.5f*pixelMetres,.5f*pixelMetres,3); batch.RefreshCenters(); gray=Capture(camera);
                Check(gray.Max(p=>blank-p.r)>=3,"A head-on moth at 3m remains detectable in a 128px gray-sky panel.");
            }
            finally
            {
                camera.targetTexture=target; camera.backgroundColor=Color.white; material.SetFloat("_WingOpacity",config.wingOpacity);
                grayTarget.Release(); Object.DestroyImmediate(grayTarget);
            }
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
        var bodyMesh=BogongMothMesh.Create(1); var local=new List<Vector3>(); bodyMesh.GetUVs(2,local);
        Check(Math.Abs(local.Take(32).Max(v=>v.x)-local.Take(32).Min(v=>v.x)-2)<1e-6 &&
            Math.Abs(local.Take(32).Max(v=>v.y)-local.Take(32).Min(v=>v.y)-2)<1e-6,
            "Low-poly body reaches the configured physical diameter rather than 86.6 percent of it.");
        Object.DestroyImmediate(bodyMesh);
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
