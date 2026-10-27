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
using Object=UnityEngine.Object;

/// <summary>Native apparatus/daily-workflow interactions. Disposable project only; no tracker, camera or real experiment starts.</summary>
[InitializeOnLoad]
public static class ApparatusInteractionValidation
{
    private const string Key="ApparatusInteractionValidation.Active";
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly List<string> checks=new List<string>(),errors=new List<string>(),editorDiagnostics=new List<string>();
    private static int stage,nextFrame,caseIndex,prepared,launched,started;
    private static double deadline;
    private static RenderTexture target;
    private static string ownedSystem,originalDemo,importedFile;
    private static string preflightDirectory;
    private static Dictionary<string,string> demoFiles;
    private static float scrollPosition;
    private static List<Tuple<int,int,int,string,bool>> cases;
    private static string Output => Path.Combine(Application.dataPath,"../apparatus-interaction-validation");
    static ApparatusInteractionValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            deadline=EditorApplication.timeSinceStartup+150;Application.logMessageReceived+=Log;SceneManager.sceneLoaded+=Loaded;EditorApplication.update+=Tick;
        };
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Use a marked disposable project.");
        Directory.CreateDirectory(Output);File.Delete(Path.Combine(Output,"results.json"));ConfigurationSchemaBuilder.Generate();
        string profile=DesktopQuickStart.EnsureProfile(Application.streamingAssetsPath,"flying","night-sky",true);
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),File.ReadAllText(Path.Combine(profile,"system.json")));
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),File.ReadAllText(Path.Combine(profile,"sequence.json")));
        string apparatus=Path.Combine(Application.streamingAssetsPath,"Apparatus");
        if(Directory.Exists(apparatus))Directory.Move(apparatus,Path.Combine(Output,"previous-apparatus-"+Guid.NewGuid().ToString("N")));
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    private static void Loaded(Scene scene,LoadSceneMode mode)
    {foreach(var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None))listener.enabled=false;}
    private static void Log(string text,string stack,LogType type)
    {
        if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
        if(text.StartsWith("ArgumentOutOfRangeException: Index was out of range.",StringComparison.Ordinal)&&stack.Contains("UnityEditor.Search.SearchDatabase")&&stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
        {editorDiagnostics.Add(text+"\n"+stack);return;}
        if(text!="No backup data file found.")errors.Add(text);
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
    private static object Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Hidden).Invoke(owner,args);
    private static void Put(object owner,string name,object value)=>owner.GetType().GetField(name,Hidden).SetValue(owner,value);
    private static void Submit(Button button){Check(button!=null,"requested native button exists");using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=button;button.SendEvent(evt);}}
    private static void KeyPress(ConfigWorkbench ui,KeyCode key,EventModifiers modifiers=EventModifiers.None,VisualElement target=null)
    {target=target??ui.Document.rootVisualElement;using(var evt=KeyDownEvent.GetPooled(new Event {type=EventType.KeyDown,keyCode=key,modifiers=modifiers})){evt.target=target;target.SendEvent(evt);}}
    private static void Next(int value,int wait=4){stage=value;nextFrame=Time.frameCount+wait;}
    private static void Capture(string name)
    {
        var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();RenderTexture.active=previous;
        File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());Object.Destroy(image);
    }
    private static void ShowSystem(ConfigWorkbench ui)=>Call(ui,"Select",ui.Workspace.System);
    private static void ShowHome(ConfigWorkbench ui)=>Call(ui,"ShowSessionHome");
    private static JObject FreshLed(ConfigWorkbench ui,int count,int faces,int pixels,string movement,bool horizontal)
    {
        var source=JObject.Parse(originalDemo);DesktopApparatusProfile.Begin(source,"led",true);DesktopApparatusProfile.ResizeRigs(source,count);DesktopApparatusProfile.SetMovement(source,movement);
        int n=0;foreach(var rig in DesktopApparatusProfile.Rigs(source)){rig["displayOrder"]=faces==4?"RBLF":"DRBLFU";rig["ledPanelWidth"]=pixels;rig["ledPanelHeight"]=pixels;rig["horizontal"]=horizontal;rig["startRow"]=horizontal?n:0;rig["startCol"]=horizontal?0:n;rig["speciesName"]="Validation species";n++;}
        source["unknownApparatusExtension"]=new JObject {["retained"]=true};return source;
    }
    private static void Tick()
    {
        if(Time.frameCount<nextFrame)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Apparatus UI timeout at "+stage+" case "+caseIndex);
            var ui=ConfigWorkbench.Instance;if(ui?.Document==null)return;var root=ui.Document.rootVisualElement;
            switch(stage)
            {
                case 0:
                    target=new RenderTexture(1920,1080,24);target.Create();ui.Document.panelSettings.scale=1;ui.Document.panelSettings.targetTexture=target;
                    originalDemo=ui.Workspace.System.Root.ToString(Formatting.None);
                    demoFiles=new[]{ui.Workspace.System.Path,ui.Workspace.Sequence.Path,ExperimentConfigFiles.Resolve((string)ui.Workspace.Sequence.Root["sequences"][0]["parameters"]["configFile"])}.ToDictionary(path=>path,path=>Convert.ToBase64String(File.ReadAllBytes(path)));
                    Next(1,60);break;
                case 1:
                    Check(DesktopQuickStart.IsDemo(ui.Workspace)&&!DesktopApparatusProfile.IsConfigured(ui.Workspace.System.Root),"unset hardware opens an explicit demo");
                    Check(root.Q<Button>("quickstart-mode-flying")==null&&root.Q<DropdownField>("quickstart-input")==null,"daily landing does not expose hardware movement/input controls");
                    Check(root.Q<Button>("quickstart-world-night-sky")!=null&&root.Q<Button>("quickstart-world-forest")!=null,"unconfigured demo has real starter worlds");
                    Check(root.Q<Button>("start-experiment").Q<Label>().text=="Start demo","unset hardware Start is clearly a demo");
                    Check(!root.Q<Foldout>("demo-options").value,"secondary demo controls are progressively disclosed");
                    Check(!Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Any(c=>c.gameObject.scene.name=="ControlScene"&&c.gameObject.name=="Canvas"),"authored legacy ControlScene canvas is absent");
                    Submit(root.Q<Button>("toggle-navigation"));Check(root.ClassListContains("navigation-collapsed")&&root.Q<Button>("open-system").tooltip.Contains("Configure"),"collapsed navigation preserves a labelled Configure control");
                    Submit(root.Q<Button>("toggle-navigation"));Check(!root.ClassListContains("navigation-collapsed"),"navigation expands back to icon and text");
                    Capture("demo-home");Submit(root.Q<Button>("open-system"));Next(2);break;
                case 2:
                    Check(root.Q<ScrollView>("apparatus-scroll")!=null,"Configure has a stable scroll identity");
                    var led=root.Query<Button>().ToList().FirstOrDefault(b=>b.Q<Label>()?.text=="Configure LED panels");Submit(led);Next(3);break;
                case 3:
                    ownedSystem=ui.Workspace.System.Path;
                    Check(ownedSystem.Replace('\\','/').EndsWith("/Apparatus/system.json"),"new hardware uses its own system JSON");
                    Check(!DesktopApparatusProfile.IsConfigured(ui.Workspace.System.Root),"draft apparatus is not prematurely completed");
                    Check(root.Q<Button>("start-experiment").Q<Label>().text=="Use configuration","draft footer completes configuration instead of offering Start");
                    Check(ui.Workspace.Documents.Any(d=>d.Root.ToString(Formatting.None)==originalDemo),"creating apparatus retains the exact source demo document");
                    Check(root.Q<IntegerField>("apparatus-vr-count")!=null&&root.Q<DropdownField>("apparatus-mode-VR1")!=null,"VR count and movement belong in Configure");
                    Check(root.Q<TextField>("apparatus-species-VR1")!=null,"species belongs in Configure");
                    Check(ui.Workspace.Sequence.Path.Replace('\\','/').EndsWith("/Apparatus/sequence.json")&&((string)ui.Workspace.Sequence.Root["sequences"][0]["parameters"]["configFile"]).StartsWith("Apparatus/Experiments/"),"hardware creation forks sequence and experiment away from demo files");
                    root.Q<IntegerField>("apparatus-vr-count").value=4;Check(((JArray)ui.Workspace.System.Root["configs"]).Count==4,"native VR-count control adds four rigs");
                    root.Q<IntegerField>("apparatus-vr-count").value=1;Check(((JArray)ui.Workspace.System.Root["configs"]).Count==1,"native VR-count control removes rigs reversibly");
                    root.Q<DropdownField>("apparatus-mode-VR1").value="Walking · HejjéTrac";Submit(root.Q<Button>("start-experiment"));Next(103);break;
                case 103:
                    Check(!MainController.Instance.SequenceRunning,"draft footer saves configuration without starting an experiment");
                    string walkingFile=(string)ui.Workspace.Sequence.Root["sequences"][0]["parameters"]["configFile"];
                    var walking=ui.Workspace.Open(walkingFile,"choice").Root;
                    Check((bool)walking["closedLoopPosition"]&&!(bool)walking["autopilotEnabled"]&&Mathf.Abs((float)walking["walkingPositionGain"]-.01f)<.00001f,"walking hardware receives ready closed-loop walking starter motion");
                    Check(demoFiles.All(pair=>Convert.ToBase64String(File.ReadAllBytes(pair.Key))==pair.Value),"saving apparatus leaves every original demo file byte-identical");
                    ShowSystem(ui);
                    cases=new List<Tuple<int,int,int,string,bool>>();
                    foreach(int count in new[]{1,4,8})foreach(int faces in new[]{4,6})foreach(int pixels in new[]{64,128})foreach(string movement in new[]{"flying","walking"})foreach(bool horizontal in new[]{true,false})cases.Add(Tuple.Create(count,faces,pixels,movement,horizontal));
                    caseIndex=0;Next(4);break;
                case 4:
                    if(caseIndex>=cases.Count){ui.Workspace.System.ApplyJson(FreshLed(ui,4,6,64,"flying",true).ToString());ShowSystem(ui);Next(6);break;}
                    var c=cases[caseIndex];ui.Workspace.System.ApplyJson(FreshLed(ui,c.Item1,c.Item2,c.Item3,c.Item4,c.Item5).ToString());ShowSystem(ui);Next(5,3);break;
                case 5:
                    var example=cases[caseIndex];string description=example.Item1+" VR / "+example.Item2+" faces / "+example.Item3+"px / "+example.Item4+" / "+(example.Item5?"horizontal":"vertical");
                    Check(root.Query<Foldout>(className:"apparatus-rig").ToList().Count==example.Item1,"rig editor count: "+description);
                    Check(root.Query<Button>(className:"apparatus-face").ToList().Count==example.Item1*6,"complete cube-net controls: "+description);
                    Check(root.Query<Button>(className:"apparatus-face").ToList().Count(b=>b.ClassListContains("is-enabled"))==example.Item1*example.Item2,"graphical face enablement: "+description);
                    Check(root.Query<Label>(className:"apparatus-pixel-cell").ToList().Count==example.Item1*example.Item2,"display-map cells: "+description);
                    var first=root.Q<Label>("apparatus-pixel-VR1-0");Check(first.tooltip.Contains(example.Item3+" × "+example.Item3+" px at (0, 0)"),"native preview uses exact pixel geometry: "+description);
                    Check((string)ui.Workspace.System.Root["configs"][0]["closedLoopMode"]==(example.Item4=="walking"?"Hejjetrac":"Rekkefly"),"selected movement matches system JSON: "+description);
                    Check((bool)ui.Workspace.System.Root["unknownApparatusExtension"]["retained"],"unknown apparatus extension retained: "+description);
                    if(caseIndex==0)Capture("led-one-vr-four-faces");if(example.Item1==4&&example.Item2==6&&example.Item3==64&&example.Item4=="flying"&&example.Item5)Capture("led-four-vr-six-faces");
                    caseIndex++;Next(4,1);break;
                case 6:
                    root.Q<Foldout>("apparatus-rig-VR2").value=true;Next(7);break;
                case 7:
                    var scroll=root.Q<ScrollView>("apparatus-scroll");scroll.scrollOffset=new Vector2(0,Mathf.Min(450,scroll.verticalScroller.highValue));scrollPosition=scroll.scrollOffset.y;
                    Check(scrollPosition>50,"apparatus viewport actually scrolls before structural edit");
                    root.Q<DropdownField>("apparatus-mode-VR2").value="Walking · HejjéTrac";Next(8,8);break;
                case 8:
                    Check(root.Q<Foldout>("apparatus-rig-VR2").value,"VR2 foldout remains open after its mode changes");
                    Check(Mathf.Abs(root.Q<ScrollView>("apparatus-scroll").scrollOffset.y-scrollPosition)<2,"changing VR2 mode retains operator scroll position");
                    Check((string)ui.Workspace.System.Root["configs"][1]["closedLoopMode"]=="Hejjetrac","VR2 movement change reaches authoritative JSON");
                    root.Q<IntegerField>("apparatus-width-VR2").value=96;Next(9,6);break;
                case 9:
                    Check((int)ui.Workspace.System.Root["configs"][1]["ledPanelWidth"]==96,"manual non-preset panel dimensions remain exact");
                    Check(root.Q<Foldout>("apparatus-rig-VR2").value&&Mathf.Abs(root.Q<ScrollView>("apparatus-scroll").scrollOffset.y-scrollPosition)<2,"pixel-size changes retain rig expansion and scroll");
                    Capture("configure-scrolled-vr2");Submit(root.Q<Button>("apparatus-complete"));Next(10);break;
                case 10:
                    Check(DesktopApparatusProfile.IsConfigured(ui.Workspace.System.Root),"explicit completion persists apparatus state");
                    Check(root.Q<Label>("configured-apparatus-summary")!=null,"configured home has a compact apparatus summary");
                    Check(root.Q<Button>("quickstart-world-night-sky")==null&&root.Q<DropdownField>("quickstart-demo-mode")==null,"configured daily home hides demo/hardware choices");
                    Check(root.Q<Button>("session-edit-sequence")!=null,"sequence editing is a primary everyday action");
                    Check(root.Q<TextField>("subject-species-VR1")==null,"daily animal metadata does not edit model species");
                    Check(root.Q<Button>("start-experiment").Q<Label>().text=="Start experiment","configured Start is an experiment");Capture("configured-daily-home");
                    Submit(root.Q<Button>("open-sequence"));Next(11);break;
                case 11:
                    var slider=root.Q<Slider>("slider-gain");Check(slider!=null&&slider.lowValue==0&&slider.highValue==50,"gain slider covers zero through50");
                    root.Q<TextField>("number-gain").value="-12.5";Call(ui,"FlushNumbers");
                    Check(ui.Workspace.Sequence.Root["sequences"][0]["gain"].ToString()=="-12.5","manual negative gain retained exactly");
                    root.Q<TextField>("number-gain").value="250";Call(ui,"FlushNumbers");
                    Check(ui.Workspace.Sequence.Root["sequences"][0]["gain"].ToString()=="250","manual gain above slider range retained exactly");Capture("gain-manual-override");
                    root.Q<TextField>("number-gain").value="35";Call(ui,"FlushNumbers");ShowHome(ui);Next(12);break;
                case 12:
                    ui.Workspace.System.Edit(r=>r["configs"][0]["targetDisplay"]=99);
                    Check(((IEnumerable<string>)Call(ui,"ConnectedOutputErrors")).Any(e=>e.Contains("not connected")),"actual Start rejects disconnected output displays");
                    ui.Workspace.System.Edit(r=>{r["configs"][0]["targetDisplay"]=0;r["configs"][0]["startCol"]=100000;});
                    Check(((IEnumerable<string>)Call(ui,"ConnectedOutputErrors")).Any(e=>e.Contains("LED layout needs")),"actual Start rejects LED rectangles beyond connected framebuffer bounds");
                    ui.Workspace.System.Edit(r=>r["configs"][0]["startCol"]=0);
                    prepared=launched=started=0;
                    Put(ui,"prepareCompanionForValidation",new Func<SystemConfig,bool,System.Threading.Tasks.Task<CompanionCatalog.Prepared>>((rig,demo)=>{prepared++;Check(!demo,"configured tracker launch never claims demo input");return System.Threading.Tasks.Task.FromResult(new CompanionCatalog.Prepared{rig=rig});}));
                    Put(ui,"launchCompanionForValidation",new Action<CompanionCatalog.Prepared>(_=>launched++));Put(ui,"startExperimentForValidation",new Action(()=>started++));
                    Check(root.Q<Button>("initialize-tracking")!=null,"daily home exposes tracking initialization before Start experiment");
                    preflightDirectory=MasterDataLogger.Instance.directoryPath;
                    Submit(root.Q<Button>("initialize-tracking"));Next(120,12);break;
                case 120:
                    Check(prepared==4&&launched==4&&started==0,"Initialize tracking prepares all eligible inputs without starting the experiment");
                    Check(!MainController.Instance.SequenceRunning&&!ui.IsPreparing,"tracking preflight returns to idle setup without an experimental scene");
                    Check(MasterDataLogger.Instance.directoryPath==preflightDirectory,"tracking initialization does not allocate a new experiment run directory");
                    Check(root.Q<Button>("start-experiment").enabledSelf,"Start experiment stays available after tracking initialization");
                    prepared=launched=started=0;
                    ui.StartExperiment();Next(13,12);break;
                case 13:
                    Check(prepared==4&&launched==4&&started==1,"configured Start prepares each eligible rig once and starts once via safe callbacks");
                    Check(!MainController.Instance.SequenceRunning,"native interaction suite never starts an experimental scene");
                    Put(ui,"prepareCompanionForValidation",null);Put(ui,"launchCompanionForValidation",null);Put(ui,"startExperimentForValidation",null);
                    importedFile=Path.Combine(Application.streamingAssetsPath,"apparatus-imported-system.json");var imported=FreshLed(ui,1,4,128,"walking",true);imported.Remove(DesktopApparatusProfile.MarkerName);imported["configs"][0]["tracker"]=new JObject{["configFile"]="retain-native-calibration.txt",["extension"]=17};imported["configs"][0]["ledPanelWidth"]=96;
                    File.WriteAllText(importedFile,imported.ToString());ui.Workspace.SelectPrimary("system",importedFile);ShowHome(ui);Next(14);break;
                case 14:
                    Check(DesktopQuickStart.IsDemo(ui.Workspace),"unaccepted imported hardware remains a demo on Start");Submit(root.Q<Button>("open-system"));Next(15);break;
                case 15:
                    Check(root.Q<Button>("apparatus-review-existing")!=null,"existing system JSON can be reviewed without reseeding");Submit(root.Q<Button>("apparatus-review-existing"));Next(16);break;
                case 16:
                    Check(ui.Workspace.System.Path==Path.GetFullPath(importedFile)&&(int)ui.Workspace.System.Root["configs"][0]["ledPanelWidth"]==96,"reviewing imported geometry does not replace it with128px defaults");
                    Check((string)ui.Workspace.System.Root["configs"][0]["tracker"]["configFile"]=="retain-native-calibration.txt"&&(int)ui.Workspace.System.Root["configs"][0]["tracker"]["extension"]==17,"imported native calibration and unknown data survive review");
                    Check(!DesktopApparatusProfile.ShouldPrepareCompanions(ui.Workspace.System.Root),"imported/manual native workflow is not silently auto-launched");
                    Check(!MainController.Instance.SequenceRunning,"all apparatus cases complete without scene or camera acquisition");
                    KeyPress(ui,KeyCode.F1);Next(17);break;
                case 17:
                    Check(root.Q<VisualElement>(className:"modal-shade")!=null,"F1 opens keyboard help in the native UI");
                    Check(ui.TryHandleSetupEscape()&&root.Q<VisualElement>(className:"modal-shade")==null,"Escape closes setup dialogs without quitting");
                    KeyPress(ui,KeyCode.Alpha2,EventModifiers.Alt);Next(18);break;
                case 18:
                    Check(root.Q<ListView>(className:"block-list")!=null,"Alt+2 opens the sequence using the native event path");
                    KeyPress(ui,KeyCode.J,EventModifiers.Control|EventModifiers.Shift);Next(19);break;
                case 19:
                    var raw=root.Q<TextField>("raw-json-editor");Check(raw!=null,"Ctrl+Shift+J opens the current JSON editor");
                    raw.value=raw.value+" ";Check(ui.TryHandleSetupEscape()&&root.Q<TextField>("raw-json-editor")!=null,"Escape keeps an unapplied JSON draft visible");
                    Submit(root.Q<Button>("discard-raw-json"));
                    string inherited=Path.Combine(Application.streamingAssetsPath,"apparatus-effective-invalid.json");File.WriteAllText(inherited,"{\"autopilotEnabled\":true,\"autopilotSpeed\":\"invalid\"}");
                    var doc=ui.Workspace.Open(inherited,"choice");Call(ui,"Select",doc);Put(ui,"settingsTab","Motion");Call(ui,"BuildContent");Next(20);break;
                case 20:
                    var current=(ConfigDocument)typeof(ConfigWorkbench).GetField("selected",Hidden).GetValue(ui);
                    var speed=root.Q<TextField>("number-autopilotSpeed");
                    string fallback=ConfigSchema.EffectiveValue(current.Root["autopilotSpeed"],ConfigSchema.Child(ui.Workspace.Schemas.Get("choice"),"autopilotSpeed")).ToString();
                    Check(speed!=null&&speed.value==fallback,"invalid numeric JSON displays the runtime default in its numeric control");
                    Check((string)current.Root["autopilotSpeed"]=="invalid","viewing the fallback leaves invalid source available for repair");
                    Call(ui,"ShowEffectiveConfiguration");Next(21);break;
                case 21:
                    Check(root.Q<TextField>("effective-json-preview")?.isReadOnly==true,"effective JSON is inspectable without editing the source");
                    var preview=root.Q<TextField>("effective-json-preview");preview.Focus();KeyPress(ui,KeyCode.Tab,EventModifiers.None,preview);
                    var focus=root.panel.focusController.focusedElement as VisualElement;
                    Check(focus!=preview&&!preview.Contains(focus)&&root.Q<VisualElement>(className:"modal-shade").Contains(focus),"Tab advances to the next action while retaining modal focus");
                    Check(ui.TryHandleSetupEscape(),"effective JSON dialog closes by Escape");
                    string blankPath=Path.Combine(Application.streamingAssetsPath,"apparatus-effective-blank.json");File.WriteAllText(blankPath," \n");
                    var blank=ui.Workspace.Open(blankPath,"choice");Call(ui,"Select",blank);Put(ui,"settingsTab","Motion");Call(ui,"BuildContent");Next(22);break;
                case 22:
                    Check(root.Q<TextField>("number-autopilotSpeed")!=null,"blank JSON immediately exposes inherited numeric controls");
                    var blankCurrent=(ConfigDocument)typeof(ConfigWorkbench).GetField("selected",Hidden).GetValue(ui);
                    Check(File.ReadAllText(blankCurrent.Path)==" \n","showing blank-file defaults leaves source bytes unchanged");
                    string brokenPath=Path.Combine(Application.streamingAssetsPath,"apparatus-effective-broken.json");File.WriteAllText(brokenPath,"{broken");
                    var broken=ui.Workspace.Open(brokenPath,"choice");Call(ui,"Select",broken);Put(ui,"settingsTab","Motion");Call(ui,"BuildContent");Next(23);break;
                case 23:
                    var damaged=(ConfigDocument)typeof(ConfigWorkbench).GetField("selected",Hidden).GetValue(ui);
                    Check(damaged.LoadError!=null&&root.Q<TextField>("number-autopilotSpeed")!=null,"malformed JSON keeps inherited default controls inspectable");
                    Check(File.ReadAllText(damaged.Path)=="{broken","opening malformed JSON does not change source bytes");
                    KeyPress(ui,KeyCode.J,EventModifiers.Control|EventModifiers.Shift);Next(24);break;
                case 24:
                    var repair=root.Q<TextField>("raw-json-editor");Check(repair.value=="{broken","JSON editor preserves the exact malformed source for repair");
                    repair.value="{\"autopilotEnabled\":true,\"autopilotSpeed\":5}";Submit(root.Q<Button>("apply-raw-json"));
                    KeyPress(ui,KeyCode.S,EventModifiers.Control);Next(25);break;
                case 25:
                    var repaired=(ConfigDocument)typeof(ConfigWorkbench).GetField("selected",Hidden).GetValue(ui);
                    Check(repaired.LoadError==null&&!repaired.Dirty&&(double)JObject.Parse(File.ReadAllText(repaired.Path))["autopilotSpeed"]==5,"Apply JSON then Ctrl+S repairs and saves the same authoritative document");
                    Check(File.ReadAllText(repaired.Path+".bak")=="{broken","repair keeps original malformed input as a recoverable backup");
                    Check(!MainController.Instance.SequenceRunning,"keyboard and inherited-default checks leave experiment idle");Finish(null);break;
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
    private static void Finish(string failure)
    {
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SceneManager.sceneLoaded-=Loaded;
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,failure,errors,editorDiagnostics,unity=Application.unityVersion,passed=checks,scope="Native offscreen UI Toolkit interactions using synthetic configs and injected no-launch callbacks; no hardware, camera, native tracker or experiment scene started"},Formatting.Indented));
        EditorApplication.Exit(failure==null&&errors.Count==0?0:1);
    }
}
#endif
