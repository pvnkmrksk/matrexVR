#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Distribution, moving-volume, native raster and recording checks. Disposable project only.</summary>
[InitializeOnLoad]
public static class GeneratedSwarmValidation
{
    private const string Key="GeneratedSwarmValidation.Active";
    private const string Recipe="Examples/Swarm/generated-bogong-speed-distribution.example.json";
    private static int checks, stage, nextFrame;
    private static double deadline;
    private static readonly List<string> errors=new List<string>();
    private static object distribution, visibility, encounters, performance;
    private static void Check(bool ok,string message) {checks++;if(!ok) throw new Exception(message);}
    static GeneratedSwarmValidation()
    {
        EditorApplication.playModeStateChanged+=state=> {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false)) return;
            checks=SessionState.GetInt(Key+".checks",0);
            var prior=JObject.Parse(SessionState.GetString(Key+".results","{}"));
            distribution=prior["distribution"];visibility=prior["visibility"];encounters=prior["encounters"];
            deadline=EditorApplication.timeSinceStartup+150;
            Application.logMessageReceived+=OnLog;SceneManager.sceneLoaded+=DisableInputs;EditorApplication.update+=Tick;
        };
    }
    private static void OnLog(string message,string stack,LogType type)
    {
        if(type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert) return;
        if(message=="No backup data file found." || stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        errors.Add(message+"\n"+stack);
    }
    private static void DisableInputs(Scene scene,LoadSceneMode mode)
    {
        foreach(var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) listener.enabled=false;
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable project only.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        try {DistributionAndVolume();NativeVisibility();} catch(Exception error) {Finish(error.ToString());return;}
        var system=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Templates/system_config.template.json")));
        system["telemetry"]["enabled"]=false;system["overheadCamera"]["enabled"]=false;
        foreach(var rig in system["configs"]) {rig["ledPanelWidth"]=64;rig["ledPanelHeight"]=64;rig["displayOrder"]="RBLFDU";}
        system["configs"][0]["displayOrder"]="RBLF";
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),system.ToString());
        var recipe=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,Recipe)));
        recipe["preStimulus"]["durationSeconds"]=.1;recipe["preStimulus"]["headingReference"]["windowSeconds"]=.1;
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,Recipe),recipe.ToString());
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),new JObject {
            ["autoStart"]=false,["loop"]=false,["randomise"]=false,["sequences"]=new JArray(new JObject {
                ["sceneName"]="Choice_Selwyn",["duration"]=120,["gain"]=1,["autoTrim"]=false,["reloadScene"]=true,
                ["parameters"]=new JObject {["configFile"]=Recipe}})}.ToString());
        SessionState.SetInt(Key+".checks",checks);
        SessionState.SetString(Key+".results",JsonConvert.SerializeObject(new {distribution,visibility,encounters}));
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    private static SwarmConfig Config()
    {
        var doc=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,Recipe)));
        return doc["stimulus"]["swarm"].ToObject<SwarmConfig>();
    }
    private static void DistributionAndVolume()
    {
        UnityEngine.Random.InitState(137);
        double sum=0,sum2=0;int count=100000;
        for(int i=0;i<count;i++) {float speed=GeneratedSwarmMotion.SampleSpeed(6.17f,1.24f);sum+=speed;sum2+=speed*speed;Check(speed>=0,"Sampled physical speed is nonnegative.");}
        double mean=sum/count,sd=Math.Sqrt(sum2/count-mean*mean);
        Check(Math.Abs(mean-6.17)<.015 && Math.Abs(sd-1.24)<.015,"100k generated speeds recover the requested mean and standard deviation.");
        var before=UnityEngine.Random.state;Check(GeneratedSwarmMotion.SampleSpeed(7,0)==7 && UnityEngine.Random.state.Equals(before),"Zero deviation preserves exact legacy speed and RNG state.");
        bool invalidSpeed=false;try {GeneratedSwarmMotion.SampleSpeed(-100,1);} catch(ArgumentException) {invalidSpeed=true;}
        Check(invalidSpeed,"Invalid Inspector speed settings fail promptly instead of hanging in rejection sampling.");
        distribution=new {samples=count,meanMetresPerSecond=mean,standardDeviationMetresPerSecond=sd};
        foreach(string field in new[] {"locustSpeedStdDev","volumeHalfExtentMetres"})
        {
            var json=JObject.FromObject(Config());json[field]=-1;bool rejected=false;
            try {json.ToObject<SwarmConfig>().ValidateSwarm();} catch(ArgumentException) {rejected=true;}
            Check(rejected,"Invalid "+field+" cannot silently enter movement.");
        }
        foreach(float scale in new[] {1f,100f})
        {
            var animal=new GameObject("Observer");animal.transform.position=new Vector3(17,100,23)*scale;
            var host=new GameObject("Generated swarm");var spawner=host.AddComponent<LocustSpawner>();spawner.layerName="SimulatedLocustsVR1";
            spawner.boundaryManager=host.AddComponent<BoundaryManager>();
            try
            {
                var config=Config();config.worldUnitsPerMetre=scale;config.locustSpeed*=scale;config.locustSpeedStdDev*=scale;
                var rig=animal.AddComponent<ClosedLoop>();
                SwarmConfiguration.Apply(spawner,config,rig,scale);
                Check(spawner.Spawned.Count==64 && spawner.GetComponent<SwarmTrajectoryPlayback>()==null,"Blank trajectory selects exactly 64 generated agents.");
                Check(spawner.spawnVolumeSize==Vector3.one*4*scale && spawner.boundaryManager.boundaryHeight==4*scale,"Physical cube scales correctly in metre and centimetre worlds.");
                Check(spawner.bogongVisual.shape=="Moth" && spawner.bogongVisual.sizeMode=="World","Generated moths retain physical perspective and subpixel rendering.");
                var driver=host.GetComponent<GeneratedSwarmMotion>();
                Check(driver!=null && spawner.Spawned.All(a=>!a.GetComponent<LocustMover>().enabled && a.GetComponentsInChildren<Animator>().Length==0),"One shared motion callback and GPU wings, without per-moth Animators.");
                var agent=spawner.Spawned[0];var mover=agent.GetComponent<LocustMover>();
                agent.transform.SetPositionAndRotation(animal.transform.position,Quaternion.identity);mover.speed=6.17f*scale;
                animal.transform.position+=Vector3.forward*6.17f*scale*.1f;driver.Advance(.1f);
                Check(Vector3.Distance(agent.transform.position,animal.transform.position)<.0001f*scale,"Matching observer and moth velocity preserves their relative position.");
                agent.transform.SetPositionAndRotation(animal.transform.position,Quaternion.identity);mover.speed=7.17f*scale;
                animal.transform.position+=Vector3.forward*6.17f*scale*.1f;driver.Advance(.1f);
                Check(Vector3.Distance(agent.transform.position-animal.transform.position,Vector3.forward*.1f*scale)<.0001f*scale,"Relative translation is moth velocity minus observer velocity, not doubled motion.");
                agent.transform.position=animal.transform.position;driver.Advance(3);
                Vector3 expected=Vector3.forward*(Mathf.Repeat(7.17f*3+2,4)-2)*scale;
                Check(Vector3.Distance(agent.transform.position-animal.transform.position,expected)<.001f*scale,"Multi-boundary travel preserves its overshoot rather than snapping to an edge.");
                animal.transform.position+=new Vector3(271,-19,83)*scale;driver.Advance(0);
                Check(spawner.Spawned.All(a=>InCube(spawner,a.transform.position,2.001f*scale)),"Moving or teleporting the observer keeps the volume centered on the current animal.");
                if(scale==1)
                {
                    // Natural encounters: no forced close-pass trajectory or enlargement.
                    UnityEngine.Random.InitState(703);SwarmConfiguration.Apply(spawner,config,rig,scale);driver=host.GetComponent<GeneratedSwarmMotion>();
                    var inside=new bool[64];int passes10=0,passes25=0;double nearby=0;float nearest=float.MaxValue;
                    var inside25=new bool[64];
                    for(int frame=0;frame<12000;frame++)
                    {
                        animal.transform.position+=Vector3.forward*6.17f*.05f;driver.Advance(.05f);
                        for(int i=0;i<64;i++)
                        {
                            float distance=Vector3.Distance(spawner.Spawned[i].transform.position,animal.transform.position);
                            nearest=Mathf.Min(nearest,distance);if(distance<1) nearby++;
                            bool now=distance<.1f,now25=distance<.25f;
                            if(now && !inside[i]) passes10++;if(now25 && !inside25[i]) passes25++;inside[i]=now;inside25[i]=now25;
                        }
                    }
                    Check(passes10>0 && nearest<.1f,"The unforced distribution produces close flybys in a 10-minute co-moving realization.");
                    Check(nearby/12000>2 && nearby/12000<7,"Density retains several physical moths within 1m on average.");
                    encounters=new {durationSeconds=600,agents=64,seed=703,observerMetresPerSecond=6.17,within10cmEntries=passes10,within25cmEntries=passes25,nearestMetres=nearest,meanAgentsWithin1m=nearby/12000,
                        note="One deterministic stochastic realization, sampled every 50ms; not a promised encounter rate."};
                }
            }
            finally {Object.DestroyImmediate(host);Object.DestroyImmediate(animal);}
        }
    }
    private static bool InCube(LocustSpawner spawner,Vector3 position,float half)
    {
        Vector3 p=Quaternion.Inverse(spawner.boundaryManager.referenceRotation)*(position-spawner.boundaryManager.Center);
        return Mathf.Abs(p.x)<=half && Mathf.Abs(p.y)<=half && Mathf.Abs(p.z)<=half;
    }
    private static Color32[] Pixels(Camera camera)
    {
        camera.Render();var previous=RenderTexture.active;RenderTexture.active=camera.targetTexture;
        var texture=new Texture2D(64,64,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,64,64),0,0);texture.Apply();
        var pixels=texture.GetPixels32();Object.DestroyImmediate(texture);RenderTexture.active=previous;return pixels;
    }
    private static void NativeVisibility()
    {
        var cameraRoot=new GameObject("Native panel");var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;
        camera.fieldOfView=90;camera.aspect=1;camera.nearClipPlane=.001f;camera.farClipPlane=20;
        camera.allowHDR=false;camera.allowMSAA=false;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.12f,.12f,.12f,1);camera.cullingMask=1<<29;
        var target=new RenderTexture(64,64,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
        var host=new GameObject("Physical moth") {layer=29};var agent=new GameObject("Moth");
        try
        {
            var visual=Config().bogongVisual;var batch=host.AddComponent<BogongSwarmRenderer>();batch.Configure(new[]{agent},visual,1);
            var material=host.GetComponent<MeshRenderer>().sharedMaterial;material.SetFloat("_ManualAnimation",1);
            ((float[])typeof(BogongSwarmRenderer).GetField("agentPhases",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(batch))[0]=0;
            agent.SetActive(false);batch.RefreshCenters();int gray=Pixels(camera)[0].r;
            int full=gray-Mathf.RoundToInt(visual.color.r*255);Check(full>0,"Actual native sky and moth have measurable contrast.");
            var samples=new List<object>();double halfDistance=0;int maxAt2=0;
            agent.SetActive(true);Color32[] up=null,down=null,frontUp=null,frontDown=null;
            foreach(float distance in new[]{.05f,.1f,.15f,.2f,.3f,.5f,.75f,1f,1.5f,2f,3f})
            foreach(float yaw in new[]{0f,90f})
            foreach(float phase in new[]{0f,.25f,.75f})
            foreach(float offset in new[]{0f,.5f})
            {
                float halfPixel=distance/32f*offset;
                agent.transform.SetPositionAndRotation(new Vector3(halfPixel,0,distance),Quaternion.Euler(0,yaw,0));batch.RefreshCenters();
                material.SetFloat("_AnimationTime",phase/30f);var pixels=Pixels(camera);
                int peak=pixels.Max(p=>Math.Max(0,gray-p.r)),total=pixels.Sum(p=>Math.Max(0,gray-p.r));
                if(peak>=.5*full) halfDistance=Math.Max(halfDistance,distance);
                if(distance==2) maxAt2=Math.Max(maxAt2,peak);
                samples.Add(new {distanceMetres=distance,yawDegrees=yaw,flapPhase=phase,pixelOffset=offset,peakContrastBytes=peak,totalContrastBytes=total,peakFractionOfFullContrast=(double)peak/full});
                if(distance==.1f && yaw==90 && offset==0) {if(phase==.25f) up=pixels;if(phase==.75f) down=pixels;}
                if(distance==.1f && yaw==0 && offset==0) {if(phase==.25f) frontUp=pixels;if(phase==.75f) frontDown=pixels;}
            }
            Check(up!=null && down!=null && !up.SequenceEqual(down),"The single wing pair visibly changes pose at a 10cm broadside pass.");
            Check(frontUp!=null && frontDown!=null && frontUp.Zip(frontDown,(a,b)=>Math.Abs(a.r-b.r)).Sum()>20,
                "The same wing pair has measurable flap motion at a 10cm head-on pass, without extra quads.");
            Check(BogongMothMesh.VerticesPerAgent==44 && BogongMothMesh.IndicesPerAgent==162,"Thin wing edges reuse the existing mesh and draw count.");
            Check(halfDistance>0 && halfDistance<2,"50%-of-full pixel contrast falls below threshold before 2m for the native tested poses.");
            Check(maxAt2>0 && maxAt2<full/2,"At 2m a faint residual pixel can remain; it is not forcibly clipped or enlarged.");
            visibility=new {panelWidth=64,panelHeight=64,fovDegrees=90,skyByte=gray,fullContrastBytes=full,
                furthestSampleAtHalfContrastMetres=halfDistance,selectedVolumeHalfExtentMetres=Math.Max(2,halfDistance),maxPeakContrastAt2mBytes=maxAt2,samples,
                note="Rendered pixel contrast, not calibrated moth acuity. Half threshold varies with orientation, phase and pixel position. The cube corner lies 3.46m away."};
            string folder=Path.Combine(Application.dataPath,"../generated-swarm-captures");Directory.CreateDirectory(folder);
            foreach(var capture in new[]{new {name="wings-up-10cm",pixels=up},new {name="wings-down-10cm",pixels=down},
                new {name="head-on-wings-up-10cm",pixels=frontUp},new {name="head-on-wings-down-10cm",pixels=frontDown}})
            {
                var texture=new Texture2D(64,64,TextureFormat.RGB24,false);texture.SetPixels32(capture.pixels);texture.Apply();
                File.WriteAllBytes(Path.Combine(folder,capture.name+".png"),texture.EncodeToPNG());Object.DestroyImmediate(texture);
            }
        }
        finally {camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(host);Object.DestroyImmediate(agent);Object.DestroyImmediate(cameraRoot);}
    }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Generated swarm stage "+stage);
            if(Time.frameCount<nextFrame) return;
            if(stage==0) {SceneManager.LoadScene("ControlScene");stage=1;nextFrame=Time.frameCount+4;return;}
            var main=MainController.Instance;
            if(main==null) return;
            if(stage==1 && SceneManager.GetActiveScene().name=="ControlScene")
            {
                Check(Object.FindObjectsByType<UnityEngine.UI.InputField>(FindObjectsSortMode.None).Length>0,
                    "Original ControlScene metadata inputs remain present.");
                SwarmHeadingSequenceValidation.Validate(JsonConvert.DeserializeObject<SequenceConfig>(
                    File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"))));
                var startButton = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).FirstOrDefault(button =>
                    Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(i => button.onClick.GetPersistentMethodName(i) == "StartSequence"));
                Check(startButton != null, "Original metadata Save/Start button is wired to the sequence controller.");
                startButton.onClick.Invoke();stage=2;nextFrame=Time.frameCount+4;return;
            }
            if(SceneManager.GetActiveScene().name!="Choice_Selwyn") return;
            if(main.SequenceError!=null) throw new Exception(main.SequenceError);
            if(main.GetComponent<ExperimentPhases>()?.CurrentPhase!="stimulus") return;
            var spawners=Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None);
            if(spawners.Length!=4 || spawners.Any(s=>s.GetComponent<SwarmLogger>()?.LogPath==null)) return;
            foreach(var spawner in spawners)
            {
                var rig=Object.FindObjectsByType<ClosedLoop>(FindObjectsSortMode.None).Single(r=>Kannadi.ParseVRIndex(r.name)==Kannadi.ParseVRIndex(spawner.name));
                Check(spawner.Spawned.Count==64 && spawner.locustSpeed==6.17f && spawner.locustSpeedStdDev==1.24f,"ControlScene loads nested distribution settings on every rig.");
                Check(Vector3.Distance(spawner.boundaryManager.Center,rig.transform.position)<.001,"Every moving volume is centered at its observer, without the former 50m offset.");
                Check(spawner.Spawned.All(a=>InCube(spawner,a.transform.position,2.001f)),"Actual movement and logging run inside the physical 4m cube.");
                var reference=rig.GetComponent<StimulusHeadingReference>().Result;
                Check(reference!=null && Mathf.Abs(Mathf.DeltaAngle(spawner.mu,(float)reference.meanHeadingDegrees))<.01f && spawner.kappa==1000,"The population aligns at 0 degrees to the frozen animal baseline.");
                Check(rig.GetComponentsInChildren<Camera>().Where(c=>c.name.StartsWith("Main Camera ")).All(c=>Mathf.Abs(c.nearClipPlane-.001f)<1e-6),"Near plane supports a 1cm pass on every enabled panel.");
                if(Kannadi.ParseVRIndex(rig.name)==1)
                {
                    var panels=rig.GetComponentsInChildren<Camera>(true).Where(c=>c.name.StartsWith("Main Camera ")).ToArray();
                    Check(panels.Count(c=>c.enabled && c.gameObject.activeInHierarchy)==4 && panels.Where(c=>c.name.EndsWith("U") || c.name.EndsWith("D")).All(c=>!c.enabled),
                        "Omitted U/D camera letters remain disabled and do not render: "+string.Join(", ",panels.Select(c=>c.name+"="+c.enabled+"/"+c.gameObject.activeInHierarchy)));
                }
            }
            Check((bool)JObject.Parse(File.ReadAllText(MasterDataLogger.Instance.ConfigArchive.ManifestPath))["complete"],"Run has an intact sequence, system and generated-experiment archive.");
            performance=Benchmark(spawners);
            Check(errors.Count==0,"No unexpected Unity errors: "+string.Join("; ",errors));Finish(null);
        }
        catch(Exception error) {Finish(error.ToString());}
    }
    internal static object Benchmark(LocustSpawner[] spawners)
    {
        var cameras=new List<Camera>();var targets=new List<RenderTexture>();var roots=new List<GameObject>();
        var results=new List<object>();var drivers=spawners.Select(s=>s.GetComponent<GeneratedSwarmMotion>()).ToArray();
        var loggers=spawners.Select(s=>s.GetComponent<SwarmLogger>()).ToArray();
        var batches=spawners.Select(s=>s.GetComponentInChildren<BogongSwarmRenderer>()).ToArray();
        foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.enabled=false;
        foreach(var logger in loggers) logger.enabled=false;
        try
        {
            for(int i=0;i<spawners.Length;i++) foreach(Quaternion rotation in new[]{Quaternion.identity,Quaternion.Euler(0,90,0),Quaternion.Euler(0,180,0),Quaternion.Euler(0,270,0),Quaternion.Euler(-90,0,0),Quaternion.Euler(90,0,0)})
            {
                var root=new GameObject("Benchmark panel");roots.Add(root);var camera=root.AddComponent<Camera>();camera.enabled=false;
                camera.transform.SetPositionAndRotation(spawners[i].boundaryManager.Center,rotation);camera.fieldOfView=90;camera.aspect=1;camera.nearClipPlane=.001f;camera.farClipPlane=10;
                camera.cullingMask=LayerMask.GetMask(spawners[i].layerName);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.12f,.12f,1);
                camera.allowHDR=false;camera.allowMSAA=false;var rt=new RenderTexture(64,64,24);rt.Create();targets.Add(rt);camera.targetTexture=rt;cameras.Add(camera);
            }
            foreach(bool logging in new[]{false,true})
            {
                var movement=new List<double>();var submission=new List<double>();var captures=new List<double>();
                for(int frame=0;frame<180;frame++)
                {
                    long start=System.Diagnostics.Stopwatch.GetTimestamp();foreach(var driver in drivers) driver.Advance(1f/120);foreach(var batch in batches) batch.RefreshCenters();
                    long moved=System.Diagnostics.Stopwatch.GetTimestamp();
                    if(logging) foreach(var logger in loggers) logger.CaptureFrame();
                    long captured=System.Diagnostics.Stopwatch.GetTimestamp();foreach(var camera in cameras) camera.Render();long end=System.Diagnostics.Stopwatch.GetTimestamp();
                    if(frame>=30) {movement.Add(Ms(moved-start));captures.Add(Ms(captured-moved));submission.Add(Ms(end-start));}
                }
                movement.Sort();captures.Sort();submission.Sort();
                results.Add(new {logging,agents=spawners.Sum(s=>s.Spawned.Count),rigs=spawners.Length,cameras=cameras.Count,movementAndUploadMedianMilliseconds=movement[75],captureMedianMilliseconds=captures[75],
                    refreshRecordRenderMedianMilliseconds=submission[75],refreshRecordRenderP95Milliseconds=submission[142]});
            }
            long bytes=0;
            for(int frame=0;frame<100;frame++)
            {
                long before=GC.GetAllocatedBytesForCurrentThread();
                foreach(var driver in drivers) driver.Advance(1f/120);foreach(var batch in batches) batch.RefreshCenters();foreach(var logger in loggers) logger.CaptureFrame();
                bytes+=GC.GetAllocatedBytesForCurrentThread()-before;
                // Allow the real disk worker to run at a realistic presentation cadence.
                foreach(var camera in cameras) camera.Render();System.Threading.Thread.Sleep(8);
            }
            AsyncSwarmWriter.DrainAll();
            var saved=new List<object>();
            foreach(var logger in loggers)
            {
                Check(logger.DroppedFrames==0 && logger.Failure==null && logger.PendingFrames==0,"All coordinate snapshots drain without backlog drops or writer errors.");
                string[] lines;using(var file=File.OpenRead(logger.LogPath)) using(var gzip=new GZipStream(file,CompressionMode.Decompress)) using(var reader=new StreamReader(gzip)) lines=reader.ReadToEnd().Split('\n').Where(l=>l.Length>0).ToArray();
                var spawner=logger.GetComponent<LocustSpawner>();int population=spawner.Spawned.Count;
                Check(lines.Length-1==logger.WrittenRows && logger.WrittenRows>=280*population,"Gzip contains all submitted rows, including the allocation interval.");
                var final=lines.Skip(lines.Length-population).Select(l=>l.Split(',')).ToDictionary(c=>c[1]);
                foreach(var agent in spawner.Spawned)
                {
                    var row=final[agent.name];var position=new Vector3(float.Parse(row[3],System.Globalization.CultureInfo.InvariantCulture),float.Parse(row[4],System.Globalization.CultureInfo.InvariantCulture),float.Parse(row[5],System.Globalization.CultureInfo.InvariantCulture));
                    Check(Vector3.Distance(position,agent.transform.position)<.0001f,"Saved coordinates match the final presented position of "+agent.name);
                }
                saved.Add(new {rig=spawner.name,rows=logger.WrittenRows,droppedFrames=logger.DroppedFrames,pendingFrames=logger.PendingFrames});
            }
            Check(bytes==0,"Warmed movement, upload and pooled logger capture allocate no managed bytes on the main thread.");
            return new {results,mainThreadManagedBytesPerFrame=bytes/100.0,saved,
                note="Isolated CPU submission timings include actual generated motion, native cameras and asynchronous recording. Excludes GPU completion, terrain drawing, real tracking and LED display hardware; not a guaranteed workstation FPS."};
        }
        finally {foreach(var camera in cameras) camera.targetTexture=null;foreach(var rt in targets) {rt.Release();Object.DestroyImmediate(rt);}foreach(var root in roots) Object.DestroyImmediate(root);}
    }
    private static double Ms(long ticks)=>1000.0*ticks/System.Diagnostics.Stopwatch.Frequency;
    private static void Finish(string failure)
    {
        EditorApplication.update-=Tick;SceneManager.sceneLoaded-=DisableInputs;Application.logMessageReceived-=OnLog;SessionState.SetBool(Key,false);
        File.WriteAllText(Path.Combine(Application.dataPath,"../generated-swarm-validation.json"),JsonConvert.SerializeObject(new {checks,failure,errors,distribution,visibility,encounters,performance,unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName},Formatting.Indented));
        EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
