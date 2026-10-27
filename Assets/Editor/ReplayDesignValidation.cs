#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

/// <summary>Actual Full HD replay composition from an isolated copy of a recorded native-video fixture.</summary>
[InitializeOnLoad]
public static class ReplayDesignValidation
{
    private const string Key="ReplayDesignValidation.Active";
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly List<string> checks=new List<string>(),errors=new List<string>();
    private static string Output=>Path.Combine(Application.dataPath,"../replay-design-validation");
    private static double deadline;private static bool finished;
    static ReplayDesignValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            deadline=EditorApplication.timeSinceStartup+150;EditorApplication.update+=Watchdog;Application.logMessageReceived+=Log;
            var replay=Object.FindFirstObjectByType<ReplayController>();
            if(replay==null){Finish("Replay controller did not initialize.");return;}
            replay.StartCoroutine(Guard(Exercise(replay)));
        };
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Use a marked disposable project.");
        Directory.CreateDirectory(Output);
        string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-replayDesignSource");
        if(index<0||index+1>=args.Length)throw new Exception("Supply -replayDesignSource saved-run path.");
        string source=Path.GetFullPath(args[index+1]),copy=Path.Combine(Output,"copied-run");
        if(Directory.Exists(copy))throw new Exception("Use a fresh validation output.");CopyTree(source,copy);
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),"{\"configs\":[{\"vrId\":\"VR1\",\"closedLoopMode\":\"Hejjetrac\",\"displayOrder\":\"F\",\"desktopPreview\":true}],\"statusOverlay\":{\"visible\":true},\"telemetry\":{\"enabled\":false},\"targetFrameRate\":60}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),"{\"autoStart\":false,\"sequences\":[]}");
        EditorSceneManager.OpenScene("Assets/Scenes/ReplayScene.unity");
        var replay=Object.FindFirstObjectByType<ReplayController>();replay.absoluteSessionPath=copy;replay.loop=false;
        SessionState.SetBool(Key,true);
        var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();
        typeof(OperatorOverlayValidation).GetMethod("ConfigureGameView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{view});
        EditorApplication.isPlaying=true;
    }
    private static void CopyTree(string source,string target)
    {
        Directory.CreateDirectory(target);foreach(string file in Directory.GetFiles(source))File.Copy(file,Path.Combine(target,Path.GetFileName(file)));
        foreach(string directory in Directory.GetDirectories(source))CopyTree(directory,Path.Combine(target,Path.GetFileName(directory)));
    }
    private static void Watchdog(){if(!finished&&EditorApplication.timeSinceStartup>deadline)Finish("Replay design validation timed out.");}
    private static void Log(string text,string trace,LogType type)
    {if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!trace.Contains("UnityEditor.Search.SearchDatabase"))errors.Add(text);}
    private static void Check(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);}
    private static T Field<T>(object target,string name)=>target.GetType().GetField(name,Hidden).GetValue(target) is T value?value:default;
    private static void Put(object target,string name,object value)=>target.GetType().GetField(name,Hidden).SetValue(target,value);
    private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Hidden).Invoke(target,args);
    private static IEnumerator Guard(IEnumerator routine)
    {while(true){object next;try{if(!routine.MoveNext())yield break;next=routine.Current;}catch(Exception error){Finish(error.ToString());yield break;}yield return next;}}
    private static IEnumerator Capture(string name)
    {
        for(int i=0;i<5;i++)yield return null;yield return new WaitForEndOfFrame();
        var image=ScreenCapture.CaptureScreenshotAsTexture();Check(image.width==1920&&image.height==1080,"Full HD framebuffer: "+name);
        File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());Object.Destroy(image);
    }
    private static IEnumerator Exercise(ReplayController replay)
    {
        while(!replay.Ready||replay.Loading||replay.environmentLoader.Loading)yield return null;
        replay.SetPlaying(false);replay.SeekSeconds(2.8);
        for(int i=0;i<20;i++)yield return null;
        var media=Field<ReplayMediaPanel>(replay,"mediaPanel");Check(media!=null,"Recorded media panel exists");
        for(int i=0;i<180&&media.VisibleVideoTexture==null;i++)yield return null;
        Check(media.VisibleVideoTexture!=null,"Actual saved tracker video is visible in redesigned panel");
        Check(replay.OverviewVisible,"Default replay retains God’s-eye view");
        Check(Screen.width==1920&&Screen.height==1080,"Full HD game view uses real framebuffer dimensions");
        var overview=Field<Camera>(replay,"overview");var viewport=overview.pixelRect;
        var toolbar=(Rect)typeof(ReplayController).GetMethod("ReplayToolbarRect",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{1920f,1080f,1f});
        var header=(Rect)typeof(ReplayController).GetMethod("ReplayHeaderRect",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{1920f,1080f,1f});
        Check(viewport.yMin>=Screen.height-toolbar.y,"Main camera stays above the transport dock");
        Check(!toolbar.Overlaps(media.ScreenRect),"Media card stays clear of the timeline and transport");Check(!header.Overlaps(media.ScreenRect),"Session header stays clear of camera evidence");
        Check(toolbar.height<=Screen.height*.16f,"Transport dock uses at most sixteen percent at Full HD");
        Check(Field<GUIStyle>(replay,"replaySession").font!=null,"Replay uses packaged interface typography");
        var hud=Object.FindFirstObjectByType<RuntimeStatusOverlay>();if(hud!=null)Check(!hud.ShouldDisplay,"Live experiment HUD is suspended during replay");
        var task=Capture("01-loaded-full-hd");while(task.MoveNext())yield return task.Current;
        Put(replay,"showDetails",true);task=Capture("02-fidelity-full-hd");while(task.MoveNext())yield return task.Current;Put(replay,"showDetails",false);
        Put(replay,"showReplayHelp",true);task=Capture("03-controls-full-hd");while(task.MoveNext())yield return task.Current;Put(replay,"showReplayHelp",false);
        Put(media,"showMediaTools",true);task=Capture("04-media-tools-full-hd");while(task.MoveNext())yield return task.Current;
        Check(!toolbar.Overlaps(media.ScreenRect),"Expanded media tools stay above transport dock");
        Put(media,"offsetForm",true);Put(media,"formOpen",true);task=Capture("05-video-offset-full-hd");while(task.MoveNext())yield return task.Current;
        Check(media.HasModal,"Alignment dialog retains modal input gate");Check(media.CancelModal(),"Alignment dialog remains cancellable");
        Put(replay,"showRunPicker",true);Put(media,"showMediaTools",false);task=Capture("06-open-run-full-hd");while(task.MoveNext())yield return task.Current;
        Put(replay,"showRunPicker",false);
        var mouse=InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("ReplayDesignHoverMouse");
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(toolbar.x+57,Screen.height-(toolbar.y+28))});
        task=Capture("07-play-hover-full-hd");while(task.MoveNext())yield return task.Current;
        InputSystem.RemoveDevice(mouse);
        Check(!replay.IsPlaying,"Inspecting read-only replay evidence leaves paused playback unchanged");Check(errors.Count==0,"No replay rendering or media errors");Finish(null);
    }
    private static void Finish(string failure)
    {
        if(finished)return;finished=true;SessionState.SetBool(Key,false);EditorApplication.update-=Watchdog;Application.logMessageReceived-=Log;
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,failure,errors,passed=checks,scope="Actual Full HD Game-view composition using an isolated copy of previously recorded tracker footage; programmatic UI-state inspection, no native pointer or physical camera acquisition claim"},Formatting.Indented));
        EditorApplication.Exit(failure==null&&errors.Count==0?0:1);
    }
}
#endif
