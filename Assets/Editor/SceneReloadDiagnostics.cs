#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Instrumented two-second full-scene reload investigation. Writes every observation immediately; disposable copy only.</summary>
[InitializeOnLoad]
public static class SceneReloadDiagnostics
{
    private const string ActiveKey="SceneReloadDiagnostics.Active";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static string output,sourceSha;
    private static string[] scenes;
    private static int targetLoads,basePort,stage,loads,lastFrame,loadedFrame,activeSamples,coverageFailures;
    private static double duration,runStarted,loadedAt,deadline,nextSample,finishedAt;
    private static bool forcedRender,closing,cleanupSampled;
    private static int sceneHandle,previousHandle;
    private static long forcedCameraRenders,naturalCameraRenders,loadForcedStart,loadNaturalStart;
    private static StreamWriter events,metrics,errors,warnings;
    private static readonly List<string> runtimeErrors=new List<string>();
    private static readonly List<object> rows=new List<object>();
    private static readonly Dictionary<int,long> receivedAtFirstSample=new Dictionary<int,long>();
    private static readonly Dictionary<Camera,RenderTexture> panelTargets=new Dictionary<Camera,RenderTexture>();
    private static readonly HashSet<int> renderedFaceIds=new HashSet<int>();
    private static Process process;

    static SceneReloadDiagnostics()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(ActiveKey,false))return;
            InitializeRuntime();
        };
    }
    private static string Argument(string name,string fallback=null)
    {
        string[] args=Environment.GetCommandLineArgs();
        for(int i=0;i+1<args.Length;i++)if(args[i]==name)return args[i+1];
        return fallback;
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new InvalidOperationException("Disposable project copy required.");
        string directory=Argument("-soak-output");if(string.IsNullOrWhiteSpace(directory))throw new ArgumentException("Supply -soak-output with an empty diagnostics output folder.");
        Directory.CreateDirectory(directory);
        int count=int.Parse(Argument("-soak-count","200"));double seconds=double.Parse(Argument("-soak-duration","2"),System.Globalization.CultureInfo.InvariantCulture);
        string[] names=Argument("-soak-scenes","Choice_desync,Swarm").Split(',');
        if(count<1 || seconds<=0 || names.Any(n=>!File.Exists("Assets/Scenes/"+n+".unity")))throw new ArgumentException("Invalid soak count, duration, or scene.");
        string root=Application.streamingAssetsPath;
        string backup=Path.Combine(directory,"original-configs");Directory.CreateDirectory(backup);
        foreach(string name in new[]{"system_config.json","sequenceConfig.json"})
        {string path=Path.Combine(root,name);if(File.Exists(path))File.Copy(path,Path.Combine(backup,name),true);}
        string profile=Argument("-soak-profile","standard");
        if(profile!="standard" && profile!="terrain-night-sky")throw new ArgumentException("Unknown soak profile.");
        string backend=Argument("-soak-backend","Kinefly");
        if(!TrackerBackend.TryParse(backend,out _))throw new ArgumentException("Unknown diagnostic tracker backend.");
        bool sharedInput=Argument("-soak-shared-input","false")=="true";
        string inputDescription=Argument("-soak-input-description","Four external120Hz PUB endpoints, topic+JSON, synthetic continuous translation and yaw; no live animal/tracker endpoints.");
        WriteConfigurations(root,names,count,seconds,int.Parse(Argument("-soak-base-port","29871")),profile,backend,sharedInput);
        File.WriteAllText(Path.Combine(directory,"fixture.json"),JsonConvert.SerializeObject(new{unity=Application.unityVersion,count,durationSeconds=seconds,scenes=names,profile,reloadScene=true,configurationDirectory=root,
            graphics="Natural display cameras plus explicit128x128 offscreen renders of every authored face camera each Unity frame; instrument-owned targets are reported separately.",
            input=inputDescription,backend,sharedInput},Formatting.Indented));
        SessionState.SetString(ActiveKey+".Output",Path.GetFullPath(directory));SessionState.SetBool(ActiveKey,true);
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");
        Type gameView=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if(gameView!=null){var window=EditorWindow.GetWindow(gameView);window.Show();window.Focus();}
        EditorApplication.isPlaying=true;
    }
    private static void InitializeRuntime()
    {
        output=SessionState.GetString(ActiveKey+".Output","");sourceSha=Argument("-soak-source-sha","cd207862");
        targetLoads=int.Parse(Argument("-soak-count","200"));duration=double.Parse(Argument("-soak-duration","2"),System.Globalization.CultureInfo.InvariantCulture);
        scenes=Argument("-soak-scenes","Choice_desync,Swarm").Split(',');basePort=int.Parse(Argument("-soak-base-port","29871"));
        forcedRender=SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null;
        events=Writer("events.jsonl");metrics=Writer("metrics.jsonl");errors=Writer("errors.jsonl");warnings=Writer("warnings.jsonl");process=Process.GetCurrentProcess();
        stage=loads=activeSamples=coverageFailures=0;lastFrame=-1;previousHandle=-1;runStarted=EditorApplication.timeSinceStartup;deadline=runStarted+targetLoads*(duration+8)+180;
        forcedCameraRenders=naturalCameraRenders=0;closing=false;cleanupSampled=false;runtimeErrors.Clear();rows.Clear();
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Time.timeScale=1;
        Application.logMessageReceivedThreaded+=OnLog;SceneManager.sceneLoaded+=Loaded;SceneManager.sceneUnloaded+=Unloaded;Camera.onPostRender+=Rendered;
        EditorApplication.update+=Tick;
        Debugger.CurrentLogLevel=2; // Preserve shutdown/timeout warnings, including on older archived sources.
        Write(events,new{kind="begin",utc=DateTimeOffset.UtcNow,pid=process.Id,sourceSha,unity=Application.unityVersion,os=SystemInfo.operatingSystem,gpu=SystemInfo.graphicsDeviceName,
            graphicsApi=SystemInfo.graphicsDeviceType.ToString(),targetLoads,durationSeconds=duration,forcedFullSceneReload=true,basePort,forcedRender});
        Capture("idle-before-run",0);
    }
    private static StreamWriter Writer(string name)=>new StreamWriter(new FileStream(Path.Combine(output,name),FileMode.Create,FileAccess.Write,FileShare.ReadWrite),new System.Text.UTF8Encoding(false),65536){AutoFlush=true};
    private static readonly object logGate=new object();
    private static void Write(StreamWriter stream,object row){lock(logGate){stream?.WriteLine(JsonConvert.SerializeObject(row));}}
    private static void OnLog(string message,string trace,LogType type)
    {
        if(type==LogType.Warning){Write(warnings,new{utc=DateTimeOffset.UtcNow,message,trace,load=loads,stage,duringExperiment=stage==1 && !closing});return;}
        if(type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert)return;
        bool editorIndex=trace!=null && trace.Contains("UnityEditor.Search.SearchDatabase");
        Write(errors,new{utc=DateTimeOffset.UtcNow,message,trace,type=type.ToString(),editorIndex,load=loads,stage,duringExperiment=stage==1 && !closing});
        if(!editorIndex)lock(logGate)runtimeErrors.Add(message);
    }
    private static void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(!scenes.Contains(scene.name) || stage<1 || closing)return;
        double now=EditorApplication.timeSinceStartup;
        if(loads>0)
        {
            double actual=now-loadedAt;
            rows.Add(new{load=loads,sceneHandle,actualSceneSeconds=actual,activeSamples,renderedFaces=renderedFaceIds.Count,forcedCameraRenders=forcedCameraRenders-loadForcedStart,naturalCameraRenders=naturalCameraRenders-loadNaturalStart});
            if(activeSamples<2)coverageFailures++;
        }
        loads++;previousHandle=sceneHandle;sceneHandle=scene.handle;loadedAt=now;loadedFrame=Time.frameCount;activeSamples=0;nextSample=.55;
        loadForcedStart=forcedCameraRenders;loadNaturalStart=naturalCameraRenders;renderedFaceIds.Clear();receivedAtFirstSample.Clear();
        var row=MainController.Instance?.GetCurrentSequenceStep();
        Write(events,new{kind="scene-loaded-before-Start",utc=DateTimeOffset.UtcNow,load=loads,scene=scene.name,sceneHandle,previousHandle,frame=loadedFrame,
            requestedDuration=row?.duration,reloadScene=row?.reloadScene,note="Not an active worker/resource sample; active samples follow after0.55s and1.55s."});
        if(row==null || row.reloadScene!=true || Math.Abs(row.duration-duration)>.001 || (loads>1 && sceneHandle==previousHandle))
            throw new InvalidOperationException("The diagnostic requires a new scene handle and reloadScene:true for every two-second row.");
    }
    private static void Unloaded(Scene scene)
    {
        ReleaseTargets();
        Write(events,new{kind="scene-unloaded",utc=DateTimeOffset.UtcNow,scene=scene.name,handle=scene.handle,frame=Time.frameCount,note="End-of-frame cleanup sampled separately; no pre-Start zero counters are used as leak evidence."});
    }
    private static void Rendered(Camera camera)
    {
        if(camera==null || camera.gameObject.scene.handle!=sceneHandle || camera.GetComponentInParent<ViewportSetter>()==null)return;
        renderedFaceIds.Add(camera.GetInstanceID());if(inForcedRender)forcedCameraRenders++;else naturalCameraRenders++;
    }
    private static bool inForcedRender;
    private static void RenderPanels()
    {
        if(!forcedRender)return;
        foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if(!camera.enabled || camera.gameObject.scene.handle!=sceneHandle || camera.GetComponentInParent<ViewportSetter>()==null)continue;
            if(!panelTargets.TryGetValue(camera,out var target))
            {
                target=new RenderTexture(128,128,16){name="DIAG panel target "+camera.GetInstanceID()};target.Create();panelTargets.Add(camera,target);
            }
            var original=camera.targetTexture;var rect=camera.rect;
            try{inForcedRender=true;camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.Render();}
            finally{camera.targetTexture=original;camera.rect=rect;inForcedRender=false;}
        }
    }
    private static void ReleaseTargets()
    {
        foreach(var target in panelTargets.Values)if(target!=null){target.Release();Object.Destroy(target);}panelTargets.Clear();
    }
    private static void Tick()
    {
        try
        {
            double now=EditorApplication.timeSinceStartup;
            if(now>deadline)throw new TimeoutException("Reload diagnostic deadline: stage"+stage+", loads"+loads);
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            if(stage==0)
            {
                var main=MainController.Instance;if(main==null || main.sequenceSteps.Count!=targetLoads)return;
                stage=1;main.StartSequence();return;
            }
            if(stage==1)
            {
                if(loads>0)
                {
                    RenderPanels();double elapsed=now-loadedAt;
                    if(activeSamples<2 && elapsed>=nextSample && Time.frameCount>=loadedFrame+4)
                    {
                        Capture("active-after-Start",elapsed);activeSamples++;nextSample=1.55;
                        if(loads==1 && coverageFailures>0)throw new InvalidOperationException("First active sample failed input/render/stimulus coverage; inspect metrics.jsonl before interpreting the soak.");
                    }
                    if(loads>=targetLoads && elapsed>=duration)
                    {
                        rows.Add(new{load=loads,sceneHandle,actualSceneSeconds=elapsed,activeSamples,renderedFaces=renderedFaceIds.Count,forcedCameraRenders=forcedCameraRenders-loadForcedStart,naturalCameraRenders=naturalCameraRenders-loadNaturalStart});
                        if(activeSamples<2)coverageFailures++;
                        closing=true;MainController.Instance.StopSequence();MainController.Instance.enabled=false;
                        Capture("last-scene-before-teardown",elapsed);
                        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/ControlScene.unity",new LoadSceneParameters(LoadSceneMode.Single));
                        stage=2;finishedAt=now;return;
                    }
                }
                string sequenceError=MainController.Instance?.GetType().GetProperty("SequenceError")?.GetValue(MainController.Instance) as string;
                if(sequenceError!=null)throw new Exception(sequenceError);
            }
            else if(stage==2 && now-finishedAt>=1)
            {
                // Control UI remains alive by design. An empty scene removes all scene-owned rigs, cameras and loggers.
                SceneManager.CreateScene("Diagnostics teardown");var empty=SceneManager.GetSceneByName("Diagnostics teardown");
                SceneManager.SetActiveScene(empty);var control=SceneManager.GetSceneByName("ControlScene");if(control.IsValid())SceneManager.UnloadSceneAsync(control);
                stage=3;finishedAt=now;
            }
            else if(stage==3 && now-finishedAt>=2)
            {
                Capture("after-scene-teardown-no-forced-GC",now-finishedAt);cleanupSampled=true;
                GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
                Resources.UnloadUnusedAssets();stage=4;finishedAt=now;
            }
            else if(stage==4 && now-finishedAt>=3)
            {
                Capture("after-explicit-GC-and-UnloadUnusedAssets",now-finishedAt);Finish(null);
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
    private static object TypeMemory<T>() where T:Object
    {
        T[] all=Resources.FindObjectsOfTypeAll<T>();var transient=all.Where(o=>o!=null && !EditorUtility.IsPersistent(o)).ToArray();
        return new{count=all.Length,nonPersistentCount=transient.Length,bytes=all.Sum(o=>o==null?0:Profiler.GetRuntimeMemorySizeLong(o)),
            nonPersistentBytes=transient.Sum(o=>Profiler.GetRuntimeMemorySizeLong(o)),names=transient.GroupBy(o=>o.name??"").OrderByDescending(g=>g.Count()).Take(10).Select(g=>new{name=g.Key,count=g.Count()}).ToArray()};
    }
    private static object StaticProperty(string typeName,string property)
    {
        Type type=typeof(MainController).Assembly.GetType(typeName);return type?.GetProperty(property,BindingFlags.Public|BindingFlags.Static)?.GetValue(null);
    }
    private static int? ThreadCount(){try{process.Refresh();return process.Threads.Count;}catch{return null;}}
    private static int? Descriptors(){try{return Directory.GetFileSystemEntries(Directory.Exists("/proc/self/fd")?"/proc/self/fd":"/dev/fd").Length;}catch{return null;}}
    private static object Property(object target,string name)=>target?.GetType().GetProperty(name)?.GetValue(target);
    private static object ConsoleEntries()
    {
        try{return typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries")?.GetMethod("GetCount",BindingFlags.Static|BindingFlags.Public)?.Invoke(null,null);}catch{return null;}
    }
    private static void Capture(string kind,double elapsed)
    {
        long started=System.Diagnostics.Stopwatch.GetTimestamp();
        var inputs=Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None).Where(z=>z.isActiveAndEnabled && z.gameObject.scene.handle==sceneHandle).OrderBy(z=>z.name).ToArray();
        var received=new List<object>();bool coverage=true;
        foreach(var input in inputs)
        {
            object sample=input.ReadRawSample();
            Type sampleType=sample.GetType();
            long sequence=sampleType.GetField("sequence")?.GetValue(sample) is long value?value:
                (long)(typeof(ZmqListener).GetField("lastPoseTicksUtc",Private)?.GetValue(input)??0L);
            bool hasPose=input.HasPose;bool progressing=true;
            if(kind=="active-after-Start")
            {
                if(activeSamples==0)receivedAtFirstSample[input.GetInstanceID()]=sequence;
                else progressing=receivedAtFirstSample.TryGetValue(input.GetInstanceID(),out long before) && sequence>before;
            }
            coverage&=hasPose && input.SecondsSinceLastPose<.25 && progressing;
            received.Add(new{name=input.name,endpoint="tcp://"+input.address+":"+input.port,state=typeof(ZmqListener).GetProperty("State")?.GetValue(input)??(hasPose?"receiving":"waiting"),hasPose,sequence,ageSeconds=input.SecondsSinceLastPose,progressing,
                sequenceCounter=sampleType.GetField("sequence")!=null?"packet-count":"last-received-UTC-ticks"});
        }
        var cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.enabled && c.gameObject.scene.handle==sceneHandle && c.GetComponentInParent<ViewportSetter>()!=null).ToArray();
        var writerState=Object.FindObjectsByType<SwarmLogger>(FindObjectsSortMode.None).Where(w=>w.isActiveAndEnabled && w.gameObject.scene.handle==sceneHandle)
            .Select(w=>new{name=w.name,pending=Property(w,"PendingFrames"),dropped=Property(w,"DroppedFrames"),writtenRows=Property(w,"WrittenRows"),failure=Property(w,"Failure")}).ToArray();
        int expectedCameras=Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(c=>c.gameObject.activeInHierarchy && c.gameObject.scene.handle==sceneHandle && c.GetComponentInParent<ViewportSetter>()!=null);
        string skyId=StaticProperty("NightSkyController","CurrentId") as string;
        Material skyMaterial=RenderSettings.skybox;
        Texture skyImage=skyMaterial!=null && skyMaterial.HasProperty("_MainTex")?skyMaterial.GetTexture("_MainTex"):null;
        int terrains=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).Count(t=>t.gameObject.scene.handle==sceneHandle);
        if(kind=="active-after-Start")
        {
            bool valid=inputs.Length==4 && coverage && expectedCameras>=4 && cameras.Length==expectedCameras && (!forcedRender || renderedFaceIds.Count==expectedCameras);
            if(Argument("-soak-profile","standard")=="terrain-night-sky")valid&=terrains>0 && !string.IsNullOrEmpty(skyId) && skyImage!=null && skyImage.width==4096;
            if(!valid)coverageFailures++;
        }
        long rss=0;try{process.Refresh();rss=process.WorkingSet64;}catch{}
        Write(metrics,new{kind,utc=DateTimeOffset.UtcNow,load=loads,scene=SceneManager.GetActiveScene().name,sceneHandle,frame=Time.frameCount,elapsedInSceneSeconds=elapsed,elapsedTotalSeconds=EditorApplication.timeSinceStartup-runStarted,
            renderedFramesSinceLoad=Time.frameCount-loadedFrame,expectedPanelCameras=expectedCameras,sourcePanelCameras=cameras.Length,renderedFaceCameraIds=renderedFaceIds.Count,forcedCameraRenders=forcedCameraRenders-loadForcedStart,naturalCameraRenders=naturalCameraRenders-loadNaturalStart,
            rssBytes=rss,managedBytes=GC.GetTotalMemory(false),monoUsedBytes=Profiler.GetMonoUsedSizeLong(),monoHeapBytes=Profiler.GetMonoHeapSizeLong(),unityAllocatedBytes=Profiler.GetTotalAllocatedMemoryLong(),unityReservedBytes=Profiler.GetTotalReservedMemoryLong(),unityUnusedReservedBytes=Profiler.GetTotalUnusedReservedMemoryLong(),graphicsDriverBytes=Profiler.GetAllocatedMemoryForGraphicsDriver(),
            dotnetReportedThreads=ThreadCount(),fileDescriptors=Descriptors(),editorConsoleEntries=ConsoleEntries(),activeTrackingWorkers=StaticProperty("ZmqListener","ActiveWorkerCount"),activeAsyncWriters=StaticProperty("AsyncSwarmWriter","ActiveWriterCount"),ownedTrialMaterials=StaticProperty("RuntimeMaterialOwner","ActiveMaterialCount"),diagnosticRenderTargets=panelTargets.Count,writerState,
            gameObjects=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,terrainComponents=terrains,nightSkyId=skyId,skyImageWidth=skyImage==null?0:skyImage.width,skyImageHeight=skyImage==null?0:skyImage.height,materials=TypeMemory<Material>(),meshes=TypeMemory<Mesh>(),textures=TypeMemory<Texture>(),renderTextures=TypeMemory<RenderTexture>(),received,
            instrumentationMilliseconds=1000.0*(System.Diagnostics.Stopwatch.GetTimestamp()-started)/System.Diagnostics.Stopwatch.Frequency});
    }
    private static void Finish(string failure)
    {
        if(events==null)return;
        EditorApplication.update-=Tick;Application.logMessageReceivedThreaded-=OnLog;SceneManager.sceneLoaded-=Loaded;SceneManager.sceneUnloaded-=Unloaded;Camera.onPostRender-=Rendered;SessionState.SetBool(ActiveKey,false);ReleaseTargets();
        lock(logGate)
        {
            File.WriteAllText(Path.Combine(output,"summary.json"),JsonConvert.SerializeObject(new{completed=failure==null,passed=failure==null && runtimeErrors.Count==0 && coverageFailures==0 && loads==targetLoads,sourceSha,unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),loads,targetLoads,experimentDurationSeconds=duration,forcedFullReload=true,coverageFailures,cleanupSampled,elapsedSeconds=EditorApplication.timeSinceStartup-runStarted,failure,runtimeErrors,steps=rows,
                input=Argument("-soak-input-description","Four explicit synthetic120Hz PUBs; no camera or animal measurement."),
                backend=Argument("-soak-backend","Kinefly"),sharedInput=Argument("-soak-shared-input","false")=="true",
                note="Full reloads with four progressing SUB listeners. Active samples are taken after Start, not at sceneLoaded. Explicit offscreen renders supplement natural display rendering; diagnostic targets are counted separately. A completed non-crashing run does not identify the historical crash cause."},Formatting.Indented));
            events.Dispose();metrics.Dispose();errors.Dispose();warnings.Dispose();events=metrics=errors=warnings=null;
        }
        process?.Dispose();
        string original=Path.Combine(output,"original-configs");
        foreach(string name in new[]{"system_config.json","sequenceConfig.json"}){string path=Path.Combine(original,name);if(File.Exists(path))File.Copy(path,Path.Combine(Application.streamingAssetsPath,name),true);}
        foreach(string name in new[]{"__reload-diagnostic-choice.json","__reload-diagnostic-swarm.json","__reload-diagnostic-design.json"})
        {string path=Path.Combine(Application.streamingAssetsPath,name);if(File.Exists(path))File.Delete(path);if(File.Exists(path+".meta"))File.Delete(path+".meta");}
        EditorApplication.Exit(failure==null?0:1);
    }
    private static void WriteConfigurations(string root,string[] names,int count,double seconds,int port,string profile,string backend,bool sharedInput)
    {
        var system=JObject.Parse(File.ReadAllText(Path.Combine(root,"Templates/system_config.template.json")));
        system["targetDisplay"]=0;system["targetFrameRate"]=60;system["vSyncCount"]=0;
        system["telemetry"]=JObject.FromObject(new{enabled=true,bindAddress="127.0.0.1",port=port+9,rateHz=20,topic="matrex.telemetry.v2"});
        system["overheadCamera"]=JObject.FromObject(new{enabled=true,targetDisplay=0,resolution=512});system["statusOverlay"]=JObject.FromObject(new{visible=true,targetDisplay=0});
        var rigs=(JArray)system["configs"];while(rigs.Count>4)rigs.RemoveAt(rigs.Count-1);
        for(int i=0;i<4;i++)
        {
            if(i>=rigs.Count)rigs.Add(rigs[0].DeepClone());var rig=(JObject)rigs[i];rig["vrId"]="VR"+(i+1);rig["zmqAddress"]="127.0.0.1";rig["zmqPort"]=sharedInput?port:port+i;rig["closedLoopMode"]=backend;
            rig["targetDisplay"]=0;rig["startRow"]=i;rig["startCol"]=0;rig["displayOrder"]="DRBLFU";rig["ledPanelWidth"]=128;rig["ledPanelHeight"]=128;
        }
        File.WriteAllText(Path.Combine(root,"system_config.json"),system.ToString());
        var choice=JObject.Parse(File.ReadAllText(Path.Combine(root,"Templates/choice.template.json")));
        choice["objects"]=new JArray(new JObject{["type"]="ScalingCylinder",["position"]=JObject.FromObject(new{radius=60,angle=-25,height=0}),["scale"]=JObject.FromObject(new{x=7,y=70,z=7}),["material"]="Black",["visualAngleDegrees"]=10},
            new JObject{["type"]="ScalingCylinder",["position"]=JObject.FromObject(new{radius=60,angle=25,height=0}),["scale"]=JObject.FromObject(new{x=7,y=70,z=7}),["material"]="Black",["visualAngleDegrees"]=10});
        choice["closedLoopPosition"]=true;choice["closedLoopOrientation"]=true;choice["autopilotEnabled"]=false;choice["headingReference"]=null;choice["nightSky"]=null;
        if(profile=="terrain-night-sky")
        {
            choice["objects"]=new JArray();choice["closedLoopPosition"]=false;
            choice["initialPosition"]=JObject.FromObject(new{x=0,y=100,z=0});choice["resetPositionOnStart"]=true;
            choice["nightSky"]=JObject.FromObject(new{enabled=true,latitude=-35.894409,longitude=148.453948,utcOffsetHours=11,date="2026-10-25",localTime="22:00:00",advanceWithRealTime=true,updateIntervalMinutes=30,roundToInterval=false,northYawDegrees=0,exposure=.5,faintDetailCutoff=.15,maskBelowHorizon=false,imageWidth=4096});
        }
        File.WriteAllText(Path.Combine(root,"__reload-diagnostic-choice.json"),choice.ToString());
        var swarm=JObject.Parse(File.ReadAllText(Path.Combine(root,"Templates/swarm.template.json")));
        swarm["numberOfLocusts"]=64;swarm["agentCount"]=64;swarm["agentVisual"]="Bogong";swarm["bogongVisual"]["shape"]="Moth";swarm["worldUnitsPerMetre"]=100;
        swarm["headingReference"]=null;swarm["nightSky"]=null;swarm["autopilotEnabled"]=false;swarm["closedLoopPosition"]=1;swarm["closedLoopOrientation"]=1;
        File.WriteAllText(Path.Combine(root,"__reload-diagnostic-swarm.json"),swarm.ToString());
        var design=new JObject{["seed"]=1,["repetitions"]=1,["sync"]=false,["steps"]=new JArray(new JObject{["name"]="two-second-reload-materials",["trigger"]=JObject.FromObject(new{type="time",seconds=60}),
            ["closedLoopOrientation"]=true,["closedLoopPosition"]=true,["swapAfterSeconds"]=.8,["objects"]=new JArray(new JObject{["type"]="ScalingCylinder",["polar"]=JObject.FromObject(new{radius=60,angle=-25,height=0}),["scale"]=JObject.FromObject(new{x=7,y=70,z=7}),["material"]="Black",["color"]=new JArray(.1,.1,.1,1),["swapColor"]=new JArray(.8,.8,.8,1),["visualAngleDegrees"]=10},
            new JObject{["type"]="ScalingCylinder",["polar"]=JObject.FromObject(new{radius=60,angle=25,height=0}),["scale"]=JObject.FromObject(new{x=7,y=70,z=7}),["material"]="Black",["color"]=new JArray(.8,.8,.8,1),["swapColor"]=new JArray(.1,.1,.1,1),["visualAngleDegrees"]=10})})};
        File.WriteAllText(Path.Combine(root,"__reload-diagnostic-design.json"),design.ToString());
        var sequence=new JArray();for(int i=0;i<count;i++){string scene=names[i%names.Length];sequence.Add(new JObject{["sceneName"]=scene,["duration"]=seconds,["reloadScene"]=true,["gain"]=1,["parameters"]=new JObject{[scene=="Choice_desync"?"design":"configFile"]=scene=="Swarm"?"__reload-diagnostic-swarm.json":scene=="Choice_desync"?"__reload-diagnostic-design.json":"__reload-diagnostic-choice.json"}});}
        File.WriteAllText(Path.Combine(root,"sequenceConfig.json"),new JObject{["autoStart"]=false,["loop"]=false,["randomise"]=false,["sequences"]=sequence}.ToString());
    }
}
#endif
