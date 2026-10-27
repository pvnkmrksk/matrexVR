using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in, standalone workload isolation. Never installed during ordinary experiments.</summary>
[DefaultExecutionOrder(10000)]
public sealed class RuntimePerformanceProbe : MonoBehaviour
{
    private sealed class Series
    {
        public readonly List<double> values=new List<double>(16384);
        public void Add(double value){if(!double.IsNaN(value)&&!double.IsInfinity(value)&&value>=0)values.Add(value);}
        public object Summary()
        {
            if(values.Count==0)return null;
            values.Sort();int n=values.Count;
            return new {samples=n,mean=values.Average(),median=values[n/2],p95=values[Math.Min(n-1,(int)(n*.95))],maximum=values[n-1]};
        }
    }
    private sealed class Counter : IDisposable
    {
        public string name,unit;public ProfilerRecorder recorder;public readonly Series samples=new Series();
        public Counter(ProfilerCategory category,string name)
        {this.name=name;try{recorder=ProfilerRecorder.StartNew(category,name,1);unit=recorder.UnitType.ToString();}catch{}}
        public void Capture(){if(recorder.Valid&&recorder.Count>0)samples.Add(recorder.LastValue);}
        public object Report()=>new {name,available=recorder.Valid,unit,statistics=samples.Summary()};
        public void Dispose(){recorder.Dispose();}
    }
    private static string Arg(string key,string fallback=null)
    {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if(Application.isEditor||string.IsNullOrEmpty(Arg("--performance-probe")))return;
        if(string.IsNullOrWhiteSpace(Arg("--config-dir"))||string.IsNullOrWhiteSpace(Arg("--data-dir")))
        {Debug.LogError("Performance profiling requires explicit disposable --config-dir and --data-dir paths.");Application.Quit(2);return;}
        var host=new GameObject("Explicit standalone performance probe");DontDestroyOnLoad(host);host.AddComponent<RuntimePerformanceProbe>();
    }
    private readonly List<object> results=new List<object>();
    private readonly List<string> failures=new List<string>();
    private string output,scene;
    private bool measuring;
    private double previous;
    private Series intervals,cpuMain,cpuRender,gpu,present;
    private List<Counter> counters;
    private readonly FrameTiming[] timing=new FrameTiming[1];
    private ulong lastTiming;
    private bool allocationCounterAvailable;
    private IEnumerator Start()
    {
        output=Path.GetFullPath(Arg("--performance-probe"));scene=Arg("--performance-scene","Choice");
        Directory.CreateDirectory(output);
        long allocationBefore=GC.GetAllocatedBytesForCurrentThread();var allocationProbe=new byte[1024];
        allocationCounterAvailable=GC.GetAllocatedBytesForCurrentThread()>allocationBefore;GC.KeepAlive(allocationProbe);
        Application.logMessageReceived+=Log;
        while(MainController.Instance==null||MasterDataLogger.Instance==null)yield return null;
        yield return new WaitForSecondsRealtime(1);
        var main=MainController.Instance;var logger=MasterDataLogger.Instance;
        var finishing=logger.FinalizeSessionAsync("performance-probe-setup");while(!finishing.IsCompleted)yield return null;
        int rigs=int.Parse(Arg("--performance-rigs","1"));bool desktop=Arg("--performance-layout","desktop")=="desktop";
        var system=JObject.Parse(File.ReadAllText(Path.Combine(RuntimePaths.ConfigRoot,"Templates/system_config.template.json")));
        var template=(JObject)system["configs"][0];var configured=new JArray();
        for(int i=1;i<=rigs;i++)
        {
            var rig=(JObject)template.DeepClone();rig["vrId"]="VR"+i;rig["closedLoopMode"]="Rekkefly";
            rig["zmqAddress"]="127.0.0.1";rig["zmqPort"]=30000+i;rig["targetDisplay"]=0;
            rig["displayOrder"]=desktop?"F":"DRBLFU";rig["desktopPreview"]=desktop;
            rig["ledPanelWidth"]=128;rig["ledPanelHeight"]=128;rig["startRow"]=(i-1)/2;rig["startCol"]=((i-1)%2)*6;rig["tracker"]=new JObject{["saveVideo"]=false};configured.Add(rig);
        }
        system.Remove("desktopQuickStart");system["configs"]=configured;system["targetDisplay"]=0;system["targetFrameRate"]=60;system["vSyncCount"]=0;
        system["telemetry"]=new JObject{["enabled"]=true,["port"]=30988,["rateHz"]=20};
        system["statusOverlay"]=new JObject{["visible"]=true};system["overheadCamera"]=new JObject{["enabled"]=false};
        string experiment="PerformanceProbe/empty.json";
        var stimulus=new JObject{["objects"]=new JArray(),["closedLoopPosition"]=false,["closedLoopOrientation"]=true,
            ["autopilotEnabled"]=false,["resetPositionOnStart"]=true,["resetRotationOnStart"]=true,
            ["initialPosition"]=new JObject{["x"]=0,["y"]=1,["z"]=0},["uniformSkyColor"]=new JObject{["r"]=.3,["g"]=.3,["b"]=.3,["a"]=1},
            ["autoTrimSettings"]=new JObject{["flightCheckEnabled"]=false}};
        if(scene.IndexOf("Terrain",StringComparison.OrdinalIgnoreCase)>=0)
            stimulus["rugosity"]=new JObject{["enabled"]=true,["visualSurface"]="RoughSmooth",["collisionSurface"]="Flat",["heightMode"]="AboveGround",["orientationMode"]="WorldUp"};
        WriteConfig(experiment,stimulus);WriteConfig("PerformanceProbe/system.json",system);
        WriteConfig("PerformanceProbe/sequence.json",new JObject{["autoStart"]=false,["loop"]=false,["returnToSetup"]=false,["sequences"]=new JArray(new JObject{["sceneName"]=scene,["duration"]=3600,["parameters"]=new JObject{["configFile"]=experiment}})});
        var ui=ConfigurationUI.ConfigWorkbench.Instance;if(ui!=null){ui.Document.enabled=false;ui.enabled=false;}
        main.PrepareConfiguredRun("PerformanceProbe/system.json","PerformanceProbe/sequence.json");main.StartSequence();
        while(SceneManager.GetActiveScene().name!=scene&&main.SequenceError==null)yield return null;
        if(main.SequenceError!=null){failures.Add(main.SequenceError);Finish();yield break;}
        yield return new WaitForSecondsRealtime(3);
        var listeners=FindObjectsByType<ZmqListener>(FindObjectsSortMode.None);
        var loops=FindObjectsByType<ClosedLoop>(FindObjectsSortMode.None);
        var loggers=FindObjectsByType<DataLogger>(FindObjectsSortMode.None);
        var telemetry=FindObjectsByType<ExperimentTelemetry>(FindObjectsSortMode.None);
        var cameras=FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.enabled).ToArray();
        if(cameras.Length==0||loops.Length==0){failures.Add("No active stimulus camera or rig: this is not a valid rendering benchmark.");Finish();yield break;}
        if(scene=="Choice"||scene.StartsWith("TerrainChoice_",StringComparison.Ordinal))
        {
            if(cameras.Length!=rigs*(desktop?1:6))failures.Add("Configured stimulus camera count does not match the apparatus.");
            foreach(var camera in cameras)
            {
                if(camera.targetDisplay!=0)failures.Add("Stimulus camera selected a different output display.");
                if(!desktop&&(Mathf.Abs(camera.pixelWidth-128)>.25f||Mathf.Abs(camera.pixelHeight-128)>.25f))failures.Add("LED stimulus camera is not 128 by 128 pixels: "+camera.name);
                if(camera.GetComponentInParent<ViewportSetter>()==null)failures.Add("Stimulus camera bypasses configured viewport routing.");
            }
            if(failures.Count>0){Finish();yield break;}
        }
        float measureSeconds=float.Parse(Arg("--performance-seconds","5"),System.Globalization.CultureInfo.InvariantCulture);
        string[] phases={"all-60","all-120","all-uncapped","tracking-off","logging-off","telemetry-off","overlays-off","minimal-rendering","no-cameras","offscreen-128","all-uncapped-repeat","vsync-1","all-60-repeat"};
        string requestedPhases=Arg("--performance-phases");
        if(!string.IsNullOrWhiteSpace(requestedPhases))
        {
            var selected=requestedPhases.Split(',');
            if(selected.Any(value=>!phases.Contains(value))){failures.Add("Unknown requested performance phase.");Finish();yield break;}
            phases=selected;
        }
        foreach(string phase in phases)
        {
            bool minimal=phase=="minimal-rendering"||phase=="no-cameras"||phase=="offscreen-128";
            bool tracking=!minimal&&phase!="tracking-off",logging=!minimal&&phase!="logging-off",publish=!minimal&&phase!="telemetry-off";
            foreach(var item in listeners)item.enabled=tracking;
            foreach(var item in loops)item.enabled=tracking;
            foreach(var item in loggers)item.enabled=logging;
            foreach(var item in telemetry)item.enabled=publish;
            OperatorOverlayVisibility.SetHidden(minimal||phase=="overlays-off");
            var textures=new List<RenderTexture>();
            foreach(var camera in cameras)
            {
                camera.enabled=phase!="no-cameras";
                if(phase=="offscreen-128")
                {var texture=new RenderTexture(128,128,24);texture.Create();textures.Add(texture);camera.targetTexture=texture;}
            }
            QualitySettings.vSyncCount=phase=="vsync-1"?1:0;Application.targetFrameRate=phase=="all-60"||phase=="all-60-repeat"?60:phase=="all-120"?120:-1;
            yield return new WaitForSecondsRealtime(2);
            intervals=new Series();cpuMain=new Series();cpuRender=new Series();gpu=new Series();present=new Series();lastTiming=0;
            counters=new List<Counter>{new Counter(ProfilerCategory.Internal,"Main Thread"),new Counter(ProfilerCategory.Internal,"Render Thread"),
                new Counter(ProfilerCategory.Internal,"WaitForTargetFPS"),new Counter(ProfilerCategory.Render,"Gfx.WaitForPresentOnGfxThread"),
                new Counter(ProfilerCategory.Scripts,"BehaviourUpdate"),new Counter(ProfilerCategory.Scripts,"BehaviourLateUpdate"),
                new Counter(ProfilerCategory.Memory,"GC Allocated In Frame"),new Counter(ProfilerCategory.Render,"Batches Count"),new Counter(ProfilerCategory.Render,"Triangles Count")};
            double cpuStarted=System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;
            long allocatedStarted=GC.GetAllocatedBytesForCurrentThread();
            int[] collectionsStarted={GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
            previous=Time.realtimeSinceStartupAsDouble;double started=previous;measuring=true;
            yield return new WaitForSecondsRealtime(measureSeconds);measuring=false;
            double seconds=Time.realtimeSinceStartupAsDouble-started;
            double processCpuMs=System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds-cpuStarted;
            long allocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocatedStarted;
            results.Add(new {phase,scene,requestedRigs=rigs,observedRigs=loops.Length,observedDataLoggers=loggers.Length,layout=desktop?"desktop":"LED-six-128",seconds,frames=intervals.values.Count,
                fps=intervals.values.Count/seconds,frameMs=intervals.Summary(),cpuMainMs=cpuMain.Summary(),cpuRenderMs=cpuRender.Summary(),gpuMs=gpu.Summary(),presentWaitMs=present.Summary(),
                processCpuMs,processCpuPercentOfOneCore=processCpuMs/(seconds*10),processCpuMsPerFrame=processCpuMs/Math.Max(1,intervals.values.Count),
                allocationCounterAvailable,mainThreadAllocatedBytes=allocationCounterAvailable?(long?)allocatedBytes:null,
                mainThreadAllocatedBytesPerFrame=allocationCounterAvailable?(double?)(allocatedBytes/(double)Math.Max(1,intervals.values.Count)):null,
                collections=Enumerable.Range(0,3).Select(i=>GC.CollectionCount(i)-collectionsStarted[i]).ToArray(),focused=Application.isFocused,
                target=Application.targetFrameRate,vsync=QualitySettings.vSyncCount,frameTimingAvailable=FrameTimingManager.IsFeatureEnabled(),
                cameras=cameras.Select(c=>new {c.name,enabled=c.enabled,rect=new[]{c.rect.x,c.rect.y,c.rect.width,c.rect.height},c.targetDisplay,offscreen=c.targetTexture!=null,c.pixelWidth,c.pixelHeight}).ToArray(),
                listenersEnabled=tracking,trackingFresh=listeners.Select(l=>l.HasFreshPose()).ToArray(),trackingSequences=listeners.Select(l=>l.ReadRawSample().sequence).ToArray(),
                recordingEnabled=logging,telemetryEnabled=publish,overlayHidden=OperatorOverlayVisibility.Hidden,counters=counters.Select(c=>c.Report()).ToArray()});
            foreach(var counter in counters)counter.Dispose();counters=null;
            foreach(var camera in cameras)camera.targetTexture=null;
            foreach(var texture in textures){texture.Release();Destroy(texture);}
            Save();Debug.Log("[Performance probe] "+phase+" "+(intervals.values.Count/seconds).ToString("F1")+" FPS");
        }
        main.StopSequence();while(logger.IsFinalizing)yield return null;Finish();
    }
    private static void WriteConfig(string relative,JObject data)
    {string path=Path.Combine(RuntimePaths.ConfigRoot,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,data.ToString(Formatting.Indented));}
    private void LateUpdate()
    {
        if(!measuring)return;
        double now=Time.realtimeSinceStartupAsDouble;intervals.Add((now-previous)*1000);previous=now;
        foreach(var counter in counters)counter.Capture();
        FrameTimingManager.CaptureFrameTimings();
        if(FrameTimingManager.GetLatestTimings(1,timing)==0||timing[0].frameStartTimestamp==lastTiming)return;
        var frame=timing[0];lastTiming=frame.frameStartTimestamp;
        cpuMain.Add(frame.cpuMainThreadFrameTime);cpuRender.Add(frame.cpuRenderThreadFrameTime);gpu.Add(frame.gpuFrameTime);present.Add(frame.cpuMainThreadPresentWaitTime);
    }
    private void Log(string text,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error)failures.Add(text);}
    private void Save()
    {File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(new {unity=Application.unityVersion,os=SystemInfo.operatingSystem,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,
        width=Screen.width,height=Screen.height,quality=QualitySettings.names[QualitySettings.GetQualityLevel()],failures,results,
        note="Explicit standalone software probe; no animal measurement. On-screen Metal presentation, CPU callbacks, target-FPS pacing and GPU workload are different limits. Unavailable counters remain explicit; this is not a guarantee of LED timing."},Formatting.Indented));}
    private void Finish(){Save();Application.logMessageReceived-=Log;Application.Quit(failures.Count==0?0:1);}
}
