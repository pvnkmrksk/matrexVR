#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ConfigurationUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

/// <summary>Native layout snapshots, exact controls, runtime heading caps and real HUD input/clear checks.</summary>
[InitializeOnLoad]
public static class ConfigurationUXValidation
{
    private const string Key="ConfigurationUXValidation.Active";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly List<string> checks=new List<string>(),errors=new List<string>();
    private static int stage,frame,screen;
    private static double deadline,runStarted;
    private static ConfigDocument experiment;
    private static RenderTexture target;
    private static DisplayFrameClear wash;
    private static HeadingReferenceResult observed;
    private static UnityEngine.InputSystem.Keyboard keyboard;
    private static int requestsBeforeTab;
    private static readonly List<object> layouts=new List<object>();
    private static object tabDiagnostics;
    private static void Check(bool value,string text) {if(!value) throw new Exception(text);checks.Add(text);}
    private static void Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Private).Invoke(obj,args);
    private static void Put(object obj,string name,object value)=>obj.GetType().GetField(name,Private).SetValue(obj,value);
    private static void Submit(Button button) {using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=button;button.SendEvent(evt);}}
    static ConfigurationUXValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false)) return;
            deadline=EditorApplication.timeSinceStartup+180;
            Application.logMessageReceived+=Log;SceneManager.sceneLoaded+=NoTracking;EditorApplication.update+=Tick;
        };
    }
    private static void Log(string text,string stack,LogType type)
    {if((type==LogType.Error || type==LogType.Exception || type==LogType.Assert) && text!="No backup data file found." && !stack.Contains("UnityEditor.Search.SearchDatabase")) errors.Add(text);}
    private static void NoTracking(Scene scene,LoadSceneMode mode)
    {foreach(var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) listener.enabled=false;}
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable copy only.");
        ConfigurationSchemaBuilder.Generate();
        var limited=new HeadingReferenceConfig {enabled=true,windowSeconds=180,startOffsetSeconds=2}.WithinDuration(10,out string warning);
        Check(limited.windowSeconds==8 && warning.Contains("180") && warning.Contains("8"),"Offset plus window is bounded by scene duration.");
        var phases=ExperimentPhase.Parse(JObject.Parse("{'preStimulus':{'enabled':true,'durationSeconds':2,'headingReference':{'enabled':true,'windowSeconds':30}},'stimulus':{'durationSeconds':1}}"));
        Check(phases[0].Duration==2 && phases[0].Heading.windowSeconds==30,"Phase clock wins; configured window stays unchanged.");
        foreach(var test in new[]{new[]{"1.12345678901234567890123456789","0.1","1.22345678901234567890123456789"},new[]{"99999999999999999999999999","1","100000000000000000000000000"},new[]{"-0.01","0.1","0.09"}})
            Check(JsonNumberStepper.Step(new JRaw(test[0]),test[1],1).ToString()==test[2],"Exact spin arithmetic: "+test[0]);
        var system=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Templates/system_config.template.json")));
        system["targetDisplay"]=0;system["telemetry"]["enabled"]=false;system["overheadCamera"]["enabled"]=false;
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),system.ToString());
        string payload="{\"objects\":[],\"closedLoopOrientation\":false,\"closedLoopPosition\":false,\"resetPositionOnStart\":false,\"resetRotationOnStart\":false,\"autopilotEnabled\":true,\"autopilotSpeed\":7,\"uniformSkyColor\":{\"r\":0.12,\"g\":0.12,\"b\":0.12,\"extension\":\"retain\"},\"nightSky\":{\"enabled\":false},\"headingReference\":{\"enabled\":true,\"windowSeconds\":30,\"startOffsetSeconds\":0,\"measureDuringPresentation\":false,\"saveAs\":\"short-observation\"},\"futureSetting\":{\"precise\":1.2345678901234567890123456789,\"enabled\":true}}";
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"ux-test.json"),payload);
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),"{\"autoStart\":false,\"loop\":true,\"randomise\":false,\"sequences\":[{\"sceneName\":\"Choice_Selwyn\",\"duration\":0.7,\"gain\":1,\"reloadScene\":false,\"parameters\":{\"configFile\":\"ux-test.json\"}}]}");
        SessionState.SetBool(Key,true);SessionState.SetString(Key+".checks",JsonConvert.SerializeObject(checks));
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    public static void CompilePlayer()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable copy only.");
        string output="ux-player-assemblies";Directory.CreateDirectory(Path.Combine(Application.dataPath,"../",output));
        UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(new UnityEditor.Build.Player.ScriptCompilationSettings {
            target=BuildTarget.StandaloneOSX,group=BuildTargetGroup.Standalone },output);
        bool complete=Directory.GetFiles(Path.Combine(Application.dataPath,"../",output),"Assembly-CSharp.dll",SearchOption.AllDirectories).Length>0;
        File.WriteAllText(Path.Combine(Application.dataPath,"../ux-player-compile.json"),JsonConvert.SerializeObject(new {complete,target="StandaloneOSX",editorColorFieldExcluded=true}));
        EditorApplication.Exit(complete?0:1);
    }
    private static void Render(ConfigWorkbench ui,int width,int height)
    {
        if(target!=null){ui.Document.panelSettings.targetTexture=null;target.Release();Object.Destroy(target);}
        target=new RenderTexture(width,height,24);target.Create();ui.Document.panelSettings.targetTexture=target;
        frame=Time.frameCount+5;
    }
    private static void Capture(string name)
    {
        RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();RenderTexture.active=null;
        string folder=Path.Combine(Application.dataPath,"../ux-captures");Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());Object.Destroy(image);
    }
    private static void CaptureHud(RuntimeStatusOverlay hud)
    {
        var label=(UnityEngine.UI.Text)typeof(RuntimeStatusOverlay).GetField("label",Private).GetValue(hud);
        label.text=hud.BuildText();Call(hud,"RefreshLayout");
        var canvas=label.GetComponentInParent<Canvas>();var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;
        var root=new GameObject("HUD capture camera");var camera=root.AddComponent<Camera>();camera.enabled=false;
        var elements=canvas.GetComponentsInChildren<Transform>(true);var layers=elements.Select(e=>e.gameObject.layer).ToArray();foreach(var element in elements) element.gameObject.layer=31;
        camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.035f,.05f);
        var imageTarget=new RenderTexture(1440,900,24);imageTarget.Create();camera.targetTexture=imageTarget;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.Render();
        var previous=target;target=imageTarget;Capture("flight-hud");target=previous;
        canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;camera.targetTexture=null;imageTarget.Release();Object.Destroy(imageTarget);Object.Destroy(root);
        for(int i=0;i<elements.Length;i++) elements[i].gameObject.layer=layers[i];
    }
    private static void CheckLayout(ConfigWorkbench ui)
    {
        var root=ui.Document.rootVisualElement;
        var inputs=root.Query<TextField>().ToList().Where(t=>t.name.StartsWith("number-")).ToArray();
        Check(inputs.Length>0 && inputs.All(t=>t.resolvedStyle.width<=160.1f),"Numeric boxes are compact at "+target.width+" px.");
        Check(inputs.All(t=>t.worldBound.x>=0 && t.worldBound.xMax<=target.width+1),"Visible numeric controls fit horizontally at "+target.width+" px.");
        var titles=root.Query<Label>(className:"setting-title").ToList();
        Check(titles.All(t=>t.worldBound.width>35),"Labels keep readable space at "+target.width+" px.");
        var start=root.Q<VisualElement>(className:"footer").Q<Button>(className:"primary");
        Check(start.worldBound.xMax<=target.width+1,"Start remains fully visible at "+target.width+" px.");
        if(target.width<1220) Check(root.Q<VisualElement>(className:"inspector").resolvedStyle.display==DisplayStyle.None,"Secondary overview makes room for controls in a compact window.");
        layouts.Add(new {width=target.width,height=target.height,numericControls=inputs.Length,maxNumericWidth=inputs.Max(t=>t.resolvedStyle.width),compact=root.ClassListContains("compact-layout"),narrow=root.ClassListContains("narrow-layout")});
    }
    private static void Tick()
    {
        if(Time.frameCount<frame) return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("UX timeout at stage "+stage);
            var ui=ConfigWorkbench.Instance;
            if(stage==0)
            {
                if(ui?.Document==null) return;
                InputSystem.settings=Object.Instantiate(InputSystem.settings);
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard=InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();InputSystem.EnableDevice(keyboard);
                checks.AddRange(JsonConvert.DeserializeObject<List<string>>(SessionState.GetString(Key+".checks","[]")));
                experiment=ui.Workspace.Documents.Single(d=>d.Path.EndsWith("ux-test.json"));
                string before=experiment.Root.ToString(Formatting.None);Call(ui,"Select",experiment);Put(ui,"settingsTab","Motion");Call(ui,"BuildContent");
                Check(experiment.Root.ToString(Formatting.None)==before,"Opening compact controls leaves every JSON value unchanged.");
                Render(ui,1440,900);stage=1;return;
            }
            if(stage==1)
            {
                CheckLayout(ui);Capture("motion-1440");
                Check(ui.Document.rootVisualElement.Q<VisualElement>("heading-duration-warning")!=null,"Oversized heading gets a small nonblocking warning.");
                var field=ui.Document.rootVisualElement.Q<TextField>("number-autopilotSpeed");
                field.value="7.12345678901234567890123456789";
                Submit(ui.Document.rootVisualElement.Q<Button>("spin-up-autopilotSpeed"));
                Check(experiment.Root["autopilotSpeed"].ToString()=="7.22345678901234567890123456789","Mouse spin preserves all exact decimal digits.");
                experiment.Undo();Check(experiment.Root["autopilotSpeed"].ToString()=="7.12345678901234567890123456789","Undo restores the exact value before spin.");
                Call(ui,"BuildContent");field=ui.Document.rootVisualElement.Q<TextField>("number-autopilotSpeed");
                using(var key=KeyDownEvent.GetPooled(new Event {type=EventType.KeyDown,keyCode=KeyCode.UpArrow,modifiers=EventModifiers.Shift})) {key.target=field;field.SendEvent(key);}
                Check(experiment.Root["autopilotSpeed"].ToString()=="8.12345678901234567890123456789","Keyboard Shift-Up steps precisely, just like the mouse spin control.");experiment.Undo();
                experiment.Undo();Call(ui,"BuildContent");
                Render(ui,1024,720);stage=2;return;
            }
            if(stage==2)
            {
                CheckLayout(ui);Capture("motion-1024");Render(ui,1920,1080);stage=3;return;
            }
            if(stage==3)
            {
                CheckLayout(ui);Capture("motion-1920");Put(ui,"settingsTab","Sky");Call(ui,"BuildContent");Render(ui,1440,900);stage=4;return;
            }
            if(stage==4)
            {
                Capture("sky-native-picker");
                var color=ui.Document.rootVisualElement.Q<UnityEditor.UIElements.ColorField>("color-uniformSkyColor");
                Check(color!=null && !color.showAlpha,"Editor uses the native color picker and does not invent a missing alpha.");
                string old=experiment.Root.ToString(Formatting.None);color.value=new Color(.2f,.3f,.4f,1);
                Check((string)experiment.Root["uniformSkyColor"]["extension"]=="retain" && experiment.Root["uniformSkyColor"]["a"]==null,"Native picker retains color extensions and absent keys.");
                experiment.Undo();Check(experiment.Root.ToString(Formatting.None)==old,"Native color edits are exactly reversible.");
                Put(ui,"settingsTab","All parameters");Call(ui,"BuildContent");Render(ui,1440,900);stage=5;return;
            }
            if(stage==5)
            {
                Capture("all-parameters");Check(ui.Document.rootVisualElement.Q<VisualElement>("setting-futureSetting")!=null,"Future unknown parameters remain visible and editable.");
                Call(ui,"ShowParameterDialog",experiment,Array.Empty<string>());
                ui.Document.rootVisualElement.Q<TextField>("new-parameter-key").value="laterHardware";
                ui.Document.rootVisualElement.Q<TextField>("new-parameter-value").value="{\"precision\":1.2345678901234567890123456789}";
                var modal=ui.Document.rootVisualElement.Q<VisualElement>(className:"modal-shade");Submit(modal.Query<Button>().ToList().Single(b=>b.text=="Add parameter"));
                Check(experiment.Root["laterHardware"]["precision"].ToString()=="1.2345678901234567890123456789","New unassigned extensions retain exact JSON numbers.");experiment.Undo();Call(ui,"BuildContent");
                Check(ui.Workspace.Validate().Count==0,"Capped heading is a warning, not a blocked run.");
                ui.StartExperiment();runStarted=Time.timeAsDouble;stage=6;frame=Time.frameCount+2;return;
            }
            if(stage==6)
            {
                var main=MainController.Instance;if(main==null || SceneManager.GetActiveScene().name!="Choice_Selwyn") return;
                var rigs=main.VRClosedLoops.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>pair.Value).Where(r=>r!=null).ToArray();
                var reference=rigs.Select(r=>r.GetComponent<StimulusHeadingReference>()).First(r=>r!=null);
                if(reference.Result!=null) observed=reference.Result;
                if(observed==null) return;
                Check(Math.Abs(observed.windowSeconds-.7)<1e-6 && observed.requestedWindowSeconds==30,"Runtime caps 30-second observation to 0.7-second scene without changing config.");
                Check(Time.timeAsDouble-runStarted<2,"Observation cannot turn a short scene into a 30-second wait.");
                var hud=Object.FindFirstObjectByType<RuntimeStatusOverlay>();hud.SetVisible(true);
                typeof(KineflyAutoTrim).GetProperty("Flying").SetValue(rigs[0].AutoTrim,true);
                typeof(KineflyAutoTrim).GetProperty("FlightReady").SetValue(rigs[0].AutoTrim,true);
                string text=hud.BuildText();Check(text.IndexOf("FLYING",StringComparison.Ordinal)<text.IndexOf("DC ",StringComparison.Ordinal),"Flying state is the first information tier, above DC and tuning.");
                CaptureHud(hud);
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                requestsBeforeTab=DisplayFrameClear.Requests;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Tab));stage=61;frame=Time.frameCount+2;return;
            }
            if(stage==61)
            {
                var main=MainController.Instance;var hud=Object.FindFirstObjectByType<RuntimeStatusOverlay>();
                var action=(InputAction)typeof(RuntimeStatusOverlay).GetField("dashboardToggle",Private).GetValue(hud);
                tabDiagnostics=new {visible=hud.Visible,actionEnabled=action.enabled,phase=action.phase.ToString(),controls=action.controls.Count,keyPressed=keyboard.tabKey.isPressed,keyboardEnabled=keyboard.enabled,lastToggleFrame=typeof(RuntimeStatusOverlay).GetField("lastToggleFrame",Private).GetValue(hud),guiOpen=ConfigWorkbench.IsOpen,running=main.SequenceRunning,hudEnabled=hud.enabled};
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                Check(!hud.Visible,"Actual Tab input hides the HUD rather than stopping only its text updates.");
                Check(DisplayFrameClear.Requests==requestsBeforeTab+1,"Actual Tab requests exactly one HUD washout.");
                wash=DisplayFrameClear.ClearOnce(0);
                var black=new RenderTexture(64,64,24);black.Create();wash.ClearCamera.targetTexture=black;wash.ClearCamera.Render();
                RenderTexture.active=black;var pixels=new Texture2D(64,64,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();RenderTexture.active=null;
                Check(pixels.GetPixels32().All(p=>p.r==0 && p.g==0 && p.b==0),"The actual clear pass writes a completely black frame.");
                Check(!wash.ClearCamera.enabled && wash.PresentedFrames==1,"Washout camera disables after its first actual render.");
                wash.ClearCamera.targetTexture=null;black.Release();Object.Destroy(black);Object.Destroy(pixels);
                main.StopSequence();stage=7;frame=Time.frameCount+4;return;
            }
            if(stage==7)
            {
                Check(Object.FindObjectsByType<DisplayFrameClear>(FindObjectsSortMode.None).Length==0,"Hidden HUD has no persistent clearing camera or black overlay.");
                Check(!Object.FindFirstObjectByType<RuntimeStatusOverlay>().Visible,"The HUD stays hidden after its clear pass.");
                var original=LosslessJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"ux-test.json")));
                Check((double)original["headingReference"]["windowSeconds"]==30 && original["futureSetting"]["precise"].ToString()=="1.2345678901234567890123456789","Saved experiment retains requested window and unknown values after runtime capping.");
                Check(errors.Count==0,"No unexpected Unity errors: "+string.Join("; ",errors));Finish(null);
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
    private static void Finish(string failure)
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SceneManager.sceneLoaded-=NoTracking;SessionState.SetBool(Key,false);
        if(target!=null){target.Release();Object.Destroy(target);}
        if(keyboard!=null) InputSystem.RemoveDevice(keyboard);
        File.WriteAllText(Path.Combine(Application.dataPath,"../configuration-ux-validation.json"),JsonConvert.SerializeObject(new {checks=checks.Count,failure,errors,layouts,observed,tabDiagnostics,details=checks,unity=Application.unityVersion},Formatting.Indented));
        EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
