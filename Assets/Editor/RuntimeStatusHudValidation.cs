#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>Real production HUD text, clocks and overlay geometry with isolated no-socket input fixtures.</summary>
[InitializeOnLoad]
public static class RuntimeStatusHudValidation
{
    private const string Key="RuntimeStatusHudValidation.Active";
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly List<string> checks=new List<string>(),errors=new List<string>();
    private static string Output=>Path.Combine(Application.dataPath,"../runtime-status-hud-validation");
    private static double deadline;
    private static bool finished;
    static RuntimeStatusHudValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Watchdog;Application.logMessageReceived+=Log;
            var hud=Object.FindFirstObjectByType<RuntimeStatusOverlay>();
            if(hud==null){Finish("Production HUD did not initialize.");return;}hud.StartCoroutine(Guard(Exercise(hud)));
        };
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Use a marked disposable project.");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),"{\"configs\":[{\"vrId\":\"VR1\",\"targetDisplay\":0,\"closedLoopMode\":\"Hejjetrac\",\"displayOrder\":\"DRBLFU\",\"ledPanelWidth\":128,\"ledPanelHeight\":128}],\"statusOverlay\":{\"visible\":true,\"targetDisplay\":0},\"overheadCamera\":{\"enabled\":false},\"telemetry\":{\"enabled\":false},\"targetFrameRate\":60}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),"{\"autoStart\":false,\"loop\":false,\"sequences\":[{\"sceneName\":\"Choice\",\"duration\":30}]}");
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");
        var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();
        typeof(OperatorOverlayValidation).GetMethod("ConfigureGameView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{view});
        EditorApplication.isPlaying=true;
    }
    private static void Watchdog(){if(!finished&&EditorApplication.timeSinceStartup>deadline)Finish("HUD validation timed out.");}
    private static void Log(string value,string trace,LogType type)
    {if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&value!="No backup data file found."&&!trace.Contains("UnityEditor.Search.SearchDatabase"))errors.Add(value);}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
    private static void Put(object target,string field,object value)=>target.GetType().GetField(field,Hidden).SetValue(target,value);
    private static T Field<T>(object target,string field)=>(T)target.GetType().GetField(field,Hidden).GetValue(target);
    private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Hidden).Invoke(target,args);
    private static IEnumerator Guard(IEnumerator routine)
    {while(true){object next;try{if(!routine.MoveNext())yield break;next=routine.Current;}catch(Exception error){Finish(error.ToString());yield break;}yield return next;}}
    private static IEnumerator Exercise(RuntimeStatusOverlay hud)
    {
        for(int i=0;i<20;i++)yield return null;
        var main=MainController.Instance;Check(main!=null,"production experiment controller exists");
        if(ConfigurationUI.ConfigWorkbench.Instance!=null)ConfigurationUI.ConfigWorkbench.Instance.gameObject.SetActive(false);
        main.enabled=false;foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))camera.enabled=false;
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(canvas.GetComponentInParent<RuntimeStatusOverlay>()==null&&canvas.GetComponentInParent<fps>()==null)canvas.gameObject.SetActive(false);
        var scene=SceneManager.CreateScene("TerrainChoice_RoughLeft_SmoothRight");SceneManager.SetActiveScene(scene);
        main.sequenceSteps.Clear();main.executionOrder.Clear();for(int i=0;i<4;i++){main.sequenceSteps.Add(new SequenceStep("TerrainChoice_RoughLeft_SmoothRight",30,null));main.executionOrder.Add(i);}
        main.currentTrial=2;main.currentStep=1;Put(main,"sequenceStarted",true);Put(main,"timer",17.5f);
        var configs=(Dictionary<string,SystemConfig>)main.SystemConfigs;configs.Clear();var loops=(Dictionary<string,ClosedLoop>)main.VRClosedLoops;loops.Clear();
        var background=new GameObject("HUD validation background").AddComponent<Camera>();background.clearFlags=CameraClearFlags.SolidColor;background.backgroundColor=new Color(.07f,.10f,.14f);background.cullingMask=0;background.depth=-100;
        for(int i=0;i<4;i++)
        {
            string id="VR"+(i+1);var config=new SystemConfig{vrId=id,targetDisplay=0,closedLoopMode=ClosedLoopMode.Hejjetrac,displayOrder="DRBLFU",ledPanelWidth=128,ledPanelHeight=128,startRow=i,zmqAddress="127.0.0.1",zmqPort=9871+i};configs.Add(id,config);
            var host=new GameObject(id);host.SetActive(false);var listener=host.AddComponent<ZmqListener>();var rig=host.AddComponent<ClosedLoop>();
            Put(rig,"currentMode",ClosedLoopMode.Hejjetrac);Put(rig,"useLocustGains",true);loops.Add(id,rig);
            typeof(ZmqListener).GetProperty("Endpoint").SetValue(listener,"tcp://127.0.0.1:"+(9871+i));Put(listener,"workerState","waiting for packets");Put(listener,"hasPose",true);Put(listener,"lastPoseTicksUtc",DateTime.UtcNow.Ticks);
            for(int face=0;face<6;face++){var camera=new GameObject("Protected LED "+id+" "+face).AddComponent<Camera>();camera.rect=ViewportSetter.PanelRect(face,config,Screen.width,Screen.height);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.HSVToRGB((i*6+face)*.137f%1,.7f,.7f);camera.cullingMask=0;camera.depth=face+i*6;}
        }
        hud.SetVisible(true);OperatorOverlayVisibility.SetHidden(false);OperatorOverlayLayout.Invalidate();
        string text=hud.BuildText();
        Check(text.Contains(scene.name),"basic HUD identifies the actual active scene");Check(text.Contains("Trial 3 · Step 2 / 4"),"basic HUD reports execution step and current trial cycle");
        Check(text.Contains("Elapsed 12.5 / 30.0 s · Remaining 17.5 s"),"basic HUD reports elapsed, total and remaining step seconds");
        foreach(string id in loops.Keys)Check(text.Contains("<b>"+id+"</b> · HejjéTrac ·")&&text.Contains("tcp://127.0.0.1:"+configs[id].zmqPort),"basic HUD includes rig, backend and actual endpoint for "+id);
        Check(!text.Contains("AUTO TRIM UNAVAILABLE"),"walking basic HUD omits irrelevant flight tuning");
        var first=loops["VR1"].GetComponent<ZmqListener>();typeof(ZmqListener).GetProperty("Endpoint").SetValue(first,"tcp://127.0.0.1:19871");
        Check(hud.BuildText().Contains("tcp://127.0.0.1:19871"),"live listener endpoint wins over stale configured port");typeof(ZmqListener).GetProperty("Endpoint").SetValue(first,"tcp://127.0.0.1:9871");
        var stale=loops["VR2"].GetComponent<ZmqListener>();Put(stale,"lastPoseTicksUtc",DateTime.UtcNow.AddSeconds(-5).Ticks);Check(hud.BuildText().Contains("stale"),"basic HUD exposes stale input");
        Put(loops["VR3"].GetComponent<ZmqListener>(),"hasPose",false);Check(hud.BuildText().Contains("no pose received"),"basic HUD distinguishes absent input from fresh input");
        var fourth=loops["VR4"];loops.Remove("VR4");Check(hud.BuildText().Contains("INACTIVE · configured input")&&hud.BuildText().Contains("9874"),"inactive rigs retain their configured input port");loops.Add("VR4",fourth);
        Check(Math.Abs(main.CurrentStepDurationSeconds-30)<.001&&Math.Abs(main.CurrentStepElapsedSeconds-12.5)<.001,"unphased timing getters follow the actual countdown");
        var runner=main.gameObject.AddComponent<ExperimentPhases>();runner.enabled=false;
        Put(runner,"phases",ExperimentPhase.Parse(JObject.Parse("{\"preStimulus\":{\"enabled\":true,\"durationSeconds\":2},\"stimulus\":{\"durationSeconds\":3},\"postStimulus\":{\"enabled\":true,\"durationSeconds\":5}}")));Put(runner,"index",1);Put(runner,"phaseStarted",Time.timeAsDouble-1);Put(main,"phaseRunner",runner);
        Check(Math.Abs(main.CurrentStepDurationSeconds-10)<.001&&Math.Abs(main.CurrentStepElapsedSeconds-3)<.05,"phased timing uses actual phase total rather than sequence fallback duration");Put(main,"phaseRunner",null);Object.Destroy(runner);
        for(int i=0;i<5;i++)yield return null;
        Call(hud,"Update");yield return new WaitForEndOfFrame();
        Check(Screen.width==1920&&Screen.height==1080,"real Game view framebuffer is Full HD");
        var panel=Field<GameObject>(hud,"panel");var hudCanvas=panel.GetComponentInParent<Canvas>();var rect=panel.GetComponent<RectTransform>();
        var corners=new Vector3[4];rect.GetWorldCorners(corners);var actual=Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
        Check(hud.ShouldDisplay&&panel.activeInHierarchy,"basic status panel is visibly enabled");
        Check(hudCanvas.renderMode==RenderMode.ScreenSpaceOverlay,"capture uses the unchanged production overlay canvas");
        Check(actual.xMin>Screen.width*.5f&&actual.yMin<30,"status prefers bottom-right free space");
        Check(actual.width<Screen.width*.4f&&actual.height<Screen.height*.5f,"four-rig basic HUD remains compact at Full HD");
        Check(!OperatorOverlayLayout.ProtectedRects(0,Screen.width,Screen.height).Any(actual.Overlaps),"status never overlaps calibrated LED rectangles");
        Check(!Field<Button>(hud,"autoTrimButton").gameObject.activeSelf&&!Field<Button>(hud,"flightCheckButton").gameObject.activeSelf,"basic walking HUD has no disabled flight-control clutter");
        var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(Output,"four-rig-basic-full-hd.png"),image.EncodeToPNG());Object.Destroy(image);
        File.WriteAllText(Path.Combine(Output,"basic-status.txt"),hud.BuildText());
        Put(hud,"operatorVisibility",false);typeof(MainController).GetProperty("StatusOverlaySettings").SetValue(main,new StatusOverlayConfig{visible=false,targetDisplay=0});Call(hud,"Configure");
        Check(!hud.Visible,"an explicit false statusOverlay.visible remains respected");
        typeof(MainController).GetProperty("StatusOverlaySettings").SetValue(main,new StatusOverlayConfig{visible=true,targetDisplay=0});Call(hud,"Configure");Check(hud.Visible,"enabling statusOverlay.visible restores diagnostics");
        Put(hud,"showDetails",true);Check(hud.BuildText().Contains("Input SUB:"),"More details retains listener diagnostics");
        Finish(null);
    }
    private static void Finish(string failure)
    {
        if(finished)return;finished=true;SessionState.SetBool(Key,false);EditorApplication.update-=Watchdog;Application.logMessageReceived-=Log;
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,failure,errors,passed=checks,scope="Production HUD and real Full HD Game-view overlay; disabled no-socket listener fixtures and controlled controller clocks, no camera acquisition or physical LED timing"},Formatting.Indented));
        EditorApplication.Exit(failure==null&&errors.Count==0?0:1);
    }
}
#endif
