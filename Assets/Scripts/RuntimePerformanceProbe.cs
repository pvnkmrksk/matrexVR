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
            var sorted=values.OrderBy(value=>value).ToArray();int n=sorted.Length;double mean=values.Average();
            return new {samples=n,mean=values.Average(),minimum=sorted[0],median=sorted[n/2],p95=sorted[Math.Min(n-1,(int)(n*.95))],p99=sorted[Math.Min(n-1,(int)(n*.99))],maximum=sorted[n-1],standardDeviation=Math.Sqrt(values.Select(value=>Math.Pow(value-mean,2)).Average())};
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
    private double previous,previousFrameStart;
    private long previousAllocated;
    private int gc0,gc1,gc2;
    private readonly List<FrameSample> rawSamples=new List<FrameSample>(16384);
    private struct FrameSample
    {
        public int frame,gc0,gc1,gc2;
        public double frameStartMs,wallLateUpdateMs;
        public long mainThreadAllocatedBytes;
        // Recorder.LastValue is the last completed profiler frame, not necessarily this callback's frame.
        public long lastProfilerMainNs,lastProfilerWaitNs,lastProfilerPresentNs,lastProfilerScriptsNs;
        public ulong timingFrameStartTimestamp;
        public double timingCpuMainMs,timingCpuRenderMs,timingGpuMs,timingPresentWaitMs;
    }
    private Counter mainCounter,waitCounter,presentCounter,scriptsCounter;
    private Series intervals,cpuMain,cpuRender,gpu,present;
    private Series populationCapture,worldCapture;
    private SwarmLogger[] populationLoggers;
    private KannadiLogger[] mirrorLoggers;
    private RecordedWorldStateWriter[] worldLoggers;
    private string sourceExperiment,sourcePhase;
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
        sourceExperiment=Arg("--performance-experiment");
        if(!string.IsNullOrWhiteSpace(sourceExperiment))
        {
            var configuredExperiment=JObject.Parse(ExperimentConfigFiles.ReadAllText(sourceExperiment));
            sourcePhase=Arg("--performance-experiment-phase","stimulus");
            stimulus=(JObject)(configuredExperiment[sourcePhase] is JObject phase ? phase.DeepClone() : configuredExperiment.DeepClone());
            foreach(string key in new[]{"populationMode","worldUnitsPerMetre"})
                if(stimulus[key]==null&&configuredExperiment[key]!=null)stimulus[key]=configuredExperiment[key].DeepClone();
            foreach(string key in new[]{"preStimulus","stimulus","postStimulus"})stimulus.Remove(key);
            // Profile the selected presentation itself, not an observation wait.
            stimulus.Remove("headingReference");
            if(stimulus["swarm"] is JObject)stimulus["populationMode"]="Swarm";
            if(Arg("--performance-agents") is string count)
            {
                var population=stimulus["swarm"] as JObject??stimulus;
                population["numberOfLocusts"]=int.Parse(count);population["density"]=null;
            }
            if(Arg("--performance-visual") is string visual)
            {
                var population=stimulus["swarm"] as JObject??stimulus;
                population["agentVisual"]=visual;
                if(visual=="ScenePrefab")stimulus["kannadiTilePrefab"]="SimulatedLocust";
            }
        }
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
        populationLoggers=FindObjectsByType<SwarmLogger>(FindObjectsSortMode.None);
        mirrorLoggers=FindObjectsByType<KannadiLogger>(FindObjectsSortMode.None);
        worldLoggers=FindObjectsByType<RecordedWorldStateWriter>(FindObjectsSortMode.None);
        var populationEnabled=populationLoggers.Select(item=>item.enabled).ToArray();
        var mirrorEnabled=mirrorLoggers.Select(item=>item.enabled).ToArray();
        var animators=FindObjectsByType<Animator>(FindObjectsSortMode.None);
        var animatorEnabled=animators.Select(item=>item.enabled).ToArray();
        var telemetry=FindObjectsByType<ExperimentTelemetry>(FindObjectsSortMode.None);
        var cameras=FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.enabled).ToArray();
        if(cameras.Length==0||loops.Length==0){failures.Add("No active stimulus camera or rig: this is not a valid rendering benchmark.");Finish();yield break;}
        if(scene=="Choice"||scene=="Kannadi"||scene.StartsWith("TerrainChoice_",StringComparison.Ordinal))
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
        string[] phases={"all-60","all-120","all-uncapped","tracking-off","listeners-off","logging-off","population-logging-off","world-logging-off","all-evidence-off","animation-off","telemetry-off","overlays-off","minimal-rendering","no-cameras","offscreen-128","all-uncapped-repeat","vsync-1","vsync-2","all-60-repeat"};
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
            foreach(var item in listeners)item.enabled=tracking&&phase!="listeners-off";
            foreach(var item in loops)item.enabled=tracking;
            foreach(var item in loggers)item.enabled=logging;
            for(int i=0;i<populationLoggers.Length;i++)populationLoggers[i].enabled=populationEnabled[i]&&phase!="population-logging-off"&&phase!="all-evidence-off";
            for(int i=0;i<mirrorLoggers.Length;i++)mirrorLoggers[i].enabled=mirrorEnabled[i]&&phase!="population-logging-off"&&phase!="all-evidence-off";
            foreach(var item in worldLoggers)item.enabled=phase!="world-logging-off"&&phase!="all-evidence-off";
            if(phase=="all-evidence-off")foreach(var item in loggers)item.enabled=false;
            for(int i=0;i<animators.Length;i++)if(animators[i]!=null)animators[i].enabled=phase!="animation-off"&&animatorEnabled[i];
            foreach(var item in telemetry)item.enabled=publish;
            OperatorOverlayVisibility.SetHidden(minimal||phase=="overlays-off");
            var textures=new List<RenderTexture>();
            foreach(var camera in cameras)
            {
                camera.enabled=phase!="no-cameras";
                if(phase=="offscreen-128")
                {var texture=new RenderTexture(128,128,24);texture.Create();textures.Add(texture);camera.targetTexture=texture;}
            }
            QualitySettings.vSyncCount=phase=="vsync-1"?1:phase=="vsync-2"?2:0;Application.targetFrameRate=phase=="all-60"||phase=="all-60-repeat"?60:phase=="all-120"?120:-1;
            // Explicit probe-only cap allows genuine identical capped workload comparisons.
            string cap=Arg("--performance-cap");
            if(cap!=null&&phase!="vsync-1"&&phase!="vsync-2")Application.targetFrameRate=int.Parse(cap);
            yield return new WaitForSecondsRealtime(2);
            intervals=new Series();cpuMain=new Series();cpuRender=new Series();gpu=new Series();present=new Series();populationCapture=new Series();worldCapture=new Series();lastTiming=0;
            counters=new List<Counter>{(mainCounter=new Counter(ProfilerCategory.Internal,"Main Thread")),new Counter(ProfilerCategory.Internal,"Render Thread"),
                (waitCounter=new Counter(ProfilerCategory.Internal,"WaitForTargetFPS")),(presentCounter=new Counter(ProfilerCategory.Render,"Gfx.WaitForPresentOnGfxThread")),
                (scriptsCounter=new Counter(ProfilerCategory.Scripts,"BehaviourUpdate")),new Counter(ProfilerCategory.Scripts,"BehaviourLateUpdate"),
                new Counter(ProfilerCategory.Memory,"GC Allocated In Frame"),new Counter(ProfilerCategory.Render,"Batches Count"),new Counter(ProfilerCategory.Render,"Triangles Count")};
            double cpuStarted=System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;
            long allocatedStarted=GC.GetAllocatedBytesForCurrentThread();
            int[] collectionsStarted={GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
            previous=0;previousFrameStart=0;rawSamples.Clear();
            previousAllocated=GC.GetAllocatedBytesForCurrentThread();gc0=GC.CollectionCount(0);gc1=GC.CollectionCount(1);gc2=GC.CollectionCount(2);
            double started=Time.realtimeSinceStartupAsDouble;measuring=true;
            yield return new WaitForSecondsRealtime(measureSeconds);measuring=false;
            int endedGc0=GC.CollectionCount(0),endedGc1=GC.CollectionCount(1),endedGc2=GC.CollectionCount(2);
            double seconds=Time.realtimeSinceStartupAsDouble-started;
            double processCpuMs=System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds-cpuStarted;
            long allocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocatedStarted;
            results.Add(new {phase,scene,sourceExperiment,sourcePhase,requestedRigs=rigs,observedRigs=loops.Length,observedDataLoggers=loggers.Length,observedPopulation=FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None).Select(item=>new{item.numberOfLocusts,item.agentVisual,spawned=item.Spawned.Count}).ToArray(),observedAnimators=animators.Length,layout=desktop?"desktop":"LED-six-128",seconds,frames=intervals.values.Count,
                fps=1000*intervals.values.Count/intervals.values.Sum(),phaseBoundaryFps=intervals.values.Count/seconds,completeIntervalSeconds=intervals.values.Sum()/1000,rawFrameMs=intervals.values.ToArray(),rawSamples=rawSamples.ToArray(),frameStartMs=SummarizeFrameStarts(),frameMs=intervals.Summary(),cpuMainMs=cpuMain.Summary(),cpuRenderMs=cpuRender.Summary(),gpuMs=gpu.Summary(),presentWaitMs=present.Summary(),
                populationCaptureMs=populationCapture.Summary(),worldCaptureMs=worldCapture.Summary(),
                processCpuMs,processCpuPercentOfOneCore=processCpuMs/(seconds*10),processCpuMsPerFrame=processCpuMs/Math.Max(1,intervals.values.Count),
                allocationCounterAvailable,mainThreadAllocatedBytes=allocationCounterAvailable?(long?)allocatedBytes:null,
                mainThreadAllocatedBytesPerFrame=allocationCounterAvailable?(double?)(allocatedBytes/(double)Math.Max(1,intervals.values.Count)):null,
                collections=new[]{endedGc0-collectionsStarted[0],endedGc1-collectionsStarted[1],endedGc2-collectionsStarted[2]},focused=Application.isFocused,
                target=Application.targetFrameRate,vsync=QualitySettings.vSyncCount,refreshNumerator=Screen.currentResolution.refreshRateRatio.numerator,refreshDenominator=Screen.currentResolution.refreshRateRatio.denominator,fullScreen=Screen.fullScreenMode.ToString(),metalDisplayLink=Environment.GetEnvironmentVariable("UNITY_USE_METAL_DISPLAY_LINK"),frameTimingAvailable=FrameTimingManager.IsFeatureEnabled(),
                cameras=cameras.Select(c=>new {c.name,enabled=c.enabled,rect=new[]{c.rect.x,c.rect.y,c.rect.width,c.rect.height},c.targetDisplay,offscreen=c.targetTexture!=null,c.pixelWidth,c.pixelHeight}).ToArray(),
                listenersEnabled=tracking&&phase!="listeners-off",closedLoopsEnabled=tracking,trackingFresh=listeners.Select(l=>l.HasFreshPose()).ToArray(),trackingSequences=listeners.Select(l=>l.ReadRawSample().sequence).ToArray(),
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
        double now=Time.realtimeSinceStartupAsDouble,frameStart=Time.unscaledTimeAsDouble;
        long allocated=GC.GetAllocatedBytesForCurrentThread();int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);
        if(previous==0){previous=now;previousFrameStart=frameStart;previousAllocated=allocated;gc0=g0;gc1=g1;gc2=g2;return;}
        var sample=new FrameSample{frame=Time.frameCount,frameStartMs=(frameStart-previousFrameStart)*1000,wallLateUpdateMs=(now-previous)*1000,
            mainThreadAllocatedBytes=allocationCounterAvailable?allocated-previousAllocated:-1,gc0=g0-gc0,gc1=g1-gc1,gc2=g2-gc2,
            lastProfilerMainNs=Last(mainCounter),lastProfilerWaitNs=Last(waitCounter),lastProfilerPresentNs=Last(presentCounter),lastProfilerScriptsNs=Last(scriptsCounter)};
        intervals.Add(sample.wallLateUpdateMs);previous=now;previousFrameStart=frameStart;previousAllocated=allocated;gc0=g0;gc1=g1;gc2=g2;
        foreach(var counter in counters)counter.Capture();
        double populationMilliseconds=0,worldMilliseconds=0;
        foreach(var item in populationLoggers)if(item!=null&&item.enabled)populationMilliseconds+=item.CaptureMilliseconds;
        foreach(var item in mirrorLoggers)if(item!=null&&item.enabled)populationMilliseconds+=item.CaptureMilliseconds;
        foreach(var item in worldLoggers)if(item!=null&&item.enabled)worldMilliseconds+=item.CaptureMilliseconds;
        populationCapture.Add(populationMilliseconds);worldCapture.Add(worldMilliseconds);
        FrameTimingManager.CaptureFrameTimings();
        if(FrameTimingManager.GetLatestTimings(1,timing)==0||timing[0].frameStartTimestamp==lastTiming){rawSamples.Add(sample);return;}
        var frame=timing[0];lastTiming=frame.frameStartTimestamp;
        sample.timingFrameStartTimestamp=frame.frameStartTimestamp;sample.timingCpuMainMs=frame.cpuMainThreadFrameTime;
        sample.timingCpuRenderMs=frame.cpuRenderThreadFrameTime;sample.timingGpuMs=frame.gpuFrameTime;sample.timingPresentWaitMs=frame.cpuMainThreadPresentWaitTime;
        rawSamples.Add(sample);
        cpuMain.Add(frame.cpuMainThreadFrameTime);cpuRender.Add(frame.cpuRenderThreadFrameTime);gpu.Add(frame.gpuFrameTime);present.Add(frame.cpuMainThreadPresentWaitTime);
    }
    private static long Last(Counter counter)=>counter.recorder.Valid&&counter.recorder.Count>0?counter.recorder.LastValue:0;
    private object SummarizeFrameStarts(){var series=new Series();foreach(var sample in rawSamples)series.Add(sample.frameStartMs);return series.Summary();}
    private void Log(string text,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error)failures.Add(text);}
    private void Save()
    {File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(new {unity=Application.unityVersion,os=SystemInfo.operatingSystem,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,
        width=Screen.width,height=Screen.height,quality=QualitySettings.names[QualitySettings.GetQualityLevel()],qualitySettings=new{QualitySettings.shadows,QualitySettings.shadowDistance,QualitySettings.skinWeights,QualitySettings.pixelLightCount,QualitySettings.antiAliasing},failures,results,
        note="Explicit standalone software probe; no animal measurement. On-screen Metal presentation, CPU callbacks, target-FPS pacing and GPU workload are different limits. Raw profiler LastValue belongs to the last completed profiler frame; FrameTiming timestamps identify delayed timing samples. Zero timing timestamp means unavailable/duplicate, not zero work. Raw samples retain frame-start, wall intervals, allocations and GC deltas. Unavailable counters remain explicit; this is not a guarantee of LED timing."},Formatting.Indented));}
    private void Finish(){Save();Application.logMessageReceived-=Log;Application.Quit(failures.Count==0?0:1);}
}
