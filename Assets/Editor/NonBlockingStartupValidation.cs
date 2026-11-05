#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Actual Main/scene startup with unavailable optional native tracking. Disposable projects only.</summary>
[InitializeOnLoad]
public static class NonBlockingStartupValidation
{
    const string Key="NonBlockingStartupValidation.Active";
    static string output;
    static readonly List<string> checks=new List<string>(),errors=new List<string>();
    static readonly List<object> runs=new List<object>();
    static readonly List<JObject> clearFrames=new List<JObject>();
    static NonBlockingStartupValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
            {output=SessionState.GetString(Key+".Output","");_=ValidateAsync();}
        };
    }
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new InvalidOperationException("Disposable project only.");
        output=Path.GetFullPath(Path.Combine(Application.dataPath,"../nonblocking-startup-validation"));Directory.CreateDirectory(output);
        PlayerSettings.companyName="MATREX Validation";PlayerSettings.productName="NonBlockingStartup-"+Guid.NewGuid().ToString("N");
        string root=Application.streamingAssetsPath;
        var system=SystemDocument("idle");((JObject)system["configs"][0])["tracker"]["saveVideo"]=false;
        File.WriteAllText(Path.Combine(root,"system_config.json"),system.ToString());
        File.WriteAllText(Path.Combine(root,"__startup-world.json"),new JObject{["closedLoopPosition"]=false,["closedLoopOrientation"]=false,["objects"]=new JArray(),["autoTrimSettings"]=new JObject{["flightCheckEnabled"]=false}}.ToString());
        var sequence=new JObject{["title"]="Unavailable tracker startup",["autoStart"]=false,["loop"]=false,["returnToSetup"]=false,
            ["sequences"]=new JArray(new JObject{["sceneName"]="Choice",["duration"]=600,["parameters"]=new JObject{["configFile"]="__startup-world.json"}})};
        File.WriteAllText(Path.Combine(root,"sequenceConfig.json"),sequence.ToString());File.WriteAllText(Path.Combine(root,"__startup-sequence.json"),sequence.ToString());
        SessionState.SetString(Key+".Output",output);SessionState.SetBool(Key,true);
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    static JObject SystemDocument(string name)
    {
        var root=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Templates/system_config.template.json")));
        var rig=(JObject)root["configs"][0].DeepClone();
        rig["vrId"]="VR1";rig["closedLoopMode"]="Rekkefly";rig["zmqAddress"]=name=="invalid-endpoint"?"invalid address/":"127.0.0.1";rig["zmqPort"]=39871;
        rig["targetDisplay"]=0;rig["displayOrder"]="F";rig["desktopPreview"]=true;rig["ledPanelWidth"]=64;rig["ledPanelHeight"]=64;
        rig["manualControls"]=new JObject{["enabled"]=true,["translateSpeed"]=1,["rotateSpeed"]=30};
        string native=Path.Combine(output,"native-config.toml");File.WriteAllText(native,"[output]\nenabled = false\n");
        rig["tracker"]=new JObject{["executablePath"]=name=="missing-executable"?Path.Combine(output,"missing-native"):"/usr/bin/false",
            ["configFile"]=name=="missing-config"?Path.Combine(output,"missing-native.toml"):native,["saveVideo"]=name!="video-disabled",["recordingControl"]="Experiment"};
        root["configs"]=new JArray(rig);root["targetDisplay"]=0;root["telemetry"]=new JObject{["enabled"]=false};root["statusOverlay"]=new JObject{["visible"]=false};root["overheadCamera"]=new JObject{["enabled"]=false};
        return root;
    }
    static async Task Until(Func<bool> condition,string label,double seconds=15)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();while(!condition()){if(timer.Elapsed.TotalSeconds>seconds)throw new TimeoutException(label);await Task.Delay(10);}
    }
    static void Log(string message,string stack,LogType type)
    {
        if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
        if(stack.Contains("UnityEditor.Search.SearchDatabase"))return;errors.Add(message+"\n"+stack);
    }
    static void ClearContract(MainController main)
    {
        var previous=RenderTexture.active;var sentinel=new RenderTexture(8,8,24,RenderTextureFormat.ARGB32);sentinel.Create();
        var pixels=new Texture2D(8,8,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=sentinel;GL.Clear(true,true,Color.magenta);
            typeof(MainController).GetMethod("ClearScreenToBlack",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(main,null);
            Check(RenderTexture.active==sentinel,"Startup manual screen clear restores the caller's active RenderTexture");
            pixels.ReadPixels(new Rect(0,0,8,8),0,0);pixels.Apply();var color=pixels.GetPixel(4,4);
            Check(color.r>.99f&&color.g<.01f&&color.b>.99f,"Actual GPU sentinel texture pixels survive startup screen clear");
            var cameras=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c.name=="TempClearCamera").ToArray();
            Check(cameras.Length>0&&cameras.All(c=>!c.enabled),"Temporary startup clear cameras are disabled before deferred destruction");
            Check(cameras.All(c=>c.targetTexture==null),"Startup manual clear explicitly targets the screen rather than a preview/export texture");
        }
        finally{RenderTexture.active=previous;sentinel.Release();UnityEngine.Object.Destroy(sentinel);UnityEngine.Object.Destroy(pixels);}
    }
    static void InspectSceneClear(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!="Choice"||mode!=LoadSceneMode.Single)return;
        var cameras=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c.name=="TempClearCamera").ToArray();
        clearFrames.Add(new JObject{["scene"]=scene.name,["frame"]=Time.frameCount,["cameraCount"]=cameras.Length,["allDisabled"]=cameras.All(c=>!c.enabled),["screenOnly"]=cameras.All(c=>c.targetTexture==null)});
    }
    static async Task ValidateAsync()
    {
        string failure=null;Application.logMessageReceived+=Log;
        try
        {
            Check(Application.persistentDataPath.Contains("NonBlockingStartup-"),"Native ownership state is isolated from the user's profile");
            await Until(()=>MainController.Instance!=null&&MasterDataLogger.Instance!=null,"ControlScene initialization");
            var main=MainController.Instance;await MasterDataLogger.Instance.FinalizeSessionAsync("validation-idle");
            ClearContract(main);SceneManager.sceneLoaded+=InspectSceneClear;
            foreach(string name in new[]{"missing-config","missing-executable","failed-process","invalid-endpoint","video-disabled"})
            {
                var system=SystemDocument(name);string path=Path.Combine(Application.streamingAssetsPath,"__startup-"+name+".json");File.WriteAllText(path,system.ToString());
                var rig=system["configs"][0].ToObject<SystemConfig>();string launchFailure=null;
                if(name=="missing-config"||name=="missing-executable"||name=="failed-process")
                {
                    try{TrackerProcessManager.Instance.Launch(rig);}catch(Exception error){launchFailure=error.GetBaseException().Message;}
                    if(name=="failed-process")await Until(()=>!TrackerProcessManager.IsRunning("VR1"),"Owned failing executable exit");
                    else Check(launchFailure!=null,name+": actual launch reports its missing optional native dependency");
                }
                main.PrepareConfiguredRun(path,"__startup-sequence.json");MasterDataLogger.Instance.ConfigArchive.RequireComplete();
                Check(MasterDataLogger.Instance.AcceptingWrites,name+": archive opens despite optional tracker problems");
                if(launchFailure!=null)main.AddRecordingWarning("VR1: optional tracker launch unavailable; VR continues. "+launchFailure);
                main.StartSequence();await Until(()=>main.SequenceRunning||main.SequenceError!=null,name+": actual Main starts");
                Check(main.SequenceRunning&&main.SequenceError==null,name+": VR starts without a native recorder");
                await Until(()=>SceneManager.GetActiveScene().name=="Choice"&&main.VRClosedLoops.Count==1,name+": scene and rig activation");
                await Task.Delay(150);
                var loop=main.VRClosedLoops["VR1"];var listener=loop.GetComponent<ZmqListener>();
                Check(loop.enabled&&loop.GetComponent<Keyboard>()?.enabled==true,name+": live rig and manual controls remain enabled");
                Check(!listener.HasPose,name+": no tracking samples or recorder acknowledgement are fabricated");
                if(name=="invalid-endpoint")
                {
                    await Until(()=>listener.State=="error"&&main.RecordingWarnings.Any(w=>w.Contains("Tracking input unavailable")),"Invalid endpoint warning reaches Main");
                    Check(!string.IsNullOrEmpty(listener.Failure),"Invalid endpoint retains its failure cause after worker cleanup");
                    Check(main.SequenceRunning&&main.SequenceError==null,"Dead listener leaves the actual VR experiment running");
                }
                if(name!="video-disabled")Check(main.RecordingWarnings.Count>0,name+": startup warning is visible on Main");
                var state=TrackerProcessManager.RecordingState("VR1");Check((bool?)state["acknowledged"]!=true,name+": unavailable tracker never claims recording acknowledged");
                string directory=MasterDataLogger.Instance.directoryPath;await main.StopSequenceAsync("stopped");
                var manifest=JObject.Parse(File.ReadAllText(Path.Combine(directory,"recording_manifest.json")));
                Check((string)manifest["lifecycle"]?["status"]=="finalized"&&main.RecordingTransitionError==null,name+": original VR recording finalizes without optional-video failure");
                string trace=File.Exists(Path.Combine(directory,"runtime_trace.log"))?File.ReadAllText(Path.Combine(directory,"runtime_trace.log")):string.Join("\n",Directory.EnumerateFiles(directory,"*trace*").Select(File.ReadAllText));
                if(name!="video-disabled")Check(trace.Contains("VR continues"),name+": warning is retained in this run's trace");
                runs.Add(new{fixture=name,directory,launchFailure,listenerState=listener.State,listenerFailure=listener.Failure,warnings=main.RecordingWarnings.ToArray(),nativeState=state,lifecycle=manifest["lifecycle"]});
                SceneManager.LoadScene("ControlScene");await Task.Delay(30);
            }
            Check(clearFrames.Count==5,"All five actual experiment scene loads expose their temporary clear camera to the post-Main probe");
            Check(clearFrames.All(frame=>(int)frame["cameraCount"]>0&&(bool)frame["allDisabled"]&&(bool)frame["screenOnly"]),"Actual sceneLoaded clear cameras cannot perform an additional automatic black render");
            Check(errors.Count==0,"Optional tracking failures produce no Unity Error/Exception: "+string.Join("; ",errors));
        }
        catch(Exception error){failure=error.ToString();Debug.LogError(failure);}
        finally
        {
            Application.logMessageReceived-=Log;SceneManager.sceneLoaded-=InspectSceneClear;SessionState.SetBool(Key,false);
            File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,passed=checks,failure,errors,runs,clearFrames,unity=Application.unityVersion,scope="Actual Main startup, Choice scene and manual rig, unavailable native dependencies/failing executable/invalid NetMQ endpoint, warning evidence, honest recorder state and complete VR stop. No physical camera or acquisition."},Formatting.Indented));
            EditorApplication.Exit(failure==null?0:1);
        }
    }
}
#endif
