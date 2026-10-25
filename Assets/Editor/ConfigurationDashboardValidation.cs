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

/// <summary>Native Full HD dashboard, navigation, default inheritance and runtime hover regression checks.</summary>
[InitializeOnLoad]
public static class ConfigurationDashboardValidation
{
    private const string Key="ConfigurationDashboardValidation.Active";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly List<string> checks=new List<string>(),errors=new List<string>();
    private static int stage,frame;
    private static double deadline;
    private static RenderTexture target;
    private static Rect navigation;
    private static string originalSystem,originalSequence,originalExperiment;
    private static ConfigDocument experiment;
    private static void Check(bool ok,string text) { if(!ok) throw new Exception(text); checks.Add(text); }
    private static void Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,Private).Invoke(obj,args);
    private static void Put(object obj,string field,object value)=>obj.GetType().GetField(field,Private).SetValue(obj,value);
    private static void Submit(Button button) { using(var evt=NavigationSubmitEvent.GetPooled()) { evt.target=button; button.SendEvent(evt); } }
    static ConfigurationDashboardValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false)) return;
            deadline=EditorApplication.timeSinceStartup+180;
            Application.logMessageReceived+=Log; SceneManager.sceneLoaded+=Loaded; EditorApplication.update+=Tick;
        };
    }
    private static void Log(string text,string stack,LogType type)
    { if((type==LogType.Error || type==LogType.Exception || type==LogType.Assert) && text!="No backup data file found." && !stack.Contains("UnityEditor.Search.SearchDatabase")) errors.Add(text); }
    private static void Loaded(Scene scene,LoadSceneMode mode)
    { foreach(var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) listener.enabled=false; }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable project only.");
        ConfigurationSchemaBuilder.Generate();
        var system=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Templates/system_config.template.json")));
        system["targetDisplay"]=0; system.Remove("telemetry"); system.Remove("statusOverlay");
        var animals=new AnimalData {ExperimentName="Bogong night sky",ExperimenterName="Validation",Comments="Synthetic preview session"};
        int animalNumber=0;
        foreach(var rig in ((JArray)system["configs"]).OfType<JObject>())
        {
            rig["speciesName"]="Agrotis infusa";
            animals.Animals.Add(new AnimalMetadata {VR=(string)rig["vrId"],AnimalID="Animal "+(++animalNumber).ToString("00"),SpeciesName="Agrotis infusa",Sex="Unspecified",AgeDays=""});
        }
        string backup=Path.Combine(RuntimePaths.RunDataRoot,"Backup");Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(backup,"AnimalMetadata.json"),JsonConvert.SerializeObject(animals,Formatting.Indented));
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),system.ToString());
        var sequence=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Examples/Swarm/selwyn-milky-way-swarm-cycle.sequence.json")));
        sequence["autoStart"]=false;
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),sequence.ToString());
        SessionState.SetBool(Key,true); EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity"); EditorApplication.isPlaying=true;
    }
    private static void Next(int next,int wait=5) { stage=next;frame=Time.frameCount+wait; }
    private static void Capture(string name)
    {
        RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();RenderTexture.active=null;
        var folder=Path.Combine(Application.dataPath,"../dashboard-captures");Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());Object.Destroy(image);
    }
    private static void Layout(ConfigWorkbench ui,string page)
    {
        var root=ui.Document.rootVisualElement; var nav=root.Q<VisualElement>("setup-navigation");
        Check(nav.resolvedStyle.display==DisplayStyle.Flex && Mathf.Abs(nav.worldBound.x-navigation.x)<.1f && Mathf.Abs(nav.worldBound.width-navigation.width)<.1f,"Fixed navigation position on "+page);
        foreach(string name in new[]{"back-to-session","open-sequence","open-system","open-experiments"})
            Check(root.Query<Button>(name).ToList().Count==1 && nav.Contains(root.Q<Button>(name)),"One left-side "+name+" link on "+page);
        Check(root.Q<Label>("setup-frame-rate").worldBound.xMax<=target.width && root.Q<Label>("setup-frame-rate").text.Contains("FPS"),"Frame rate integrated into header on "+page);
        Check(Object.FindFirstObjectByType<fps>()?.OverlayVisible==false,"Separate FPS canvas suspended on "+page);
        foreach(var setting in root.Query<VisualElement>(className:"studio-setting").ToList().Where(e=>e.worldBound.width>0))
        {
            Check(setting.worldBound.x>=navigation.xMax-1 && setting.worldBound.xMax<=target.width+1,"Control stays inside workspace: "+setting.name);
            Check(setting.tooltip.Contains("JSON:") && setting.tooltip.Contains("Omitted default:"),"Parameter has semantic help/default: "+setting.name);
            if(setting.ClassListContains("boolean-setting") && !setting.GetFirstAncestorOfType<ScrollView>().ClassListContains("inspector"))
                Check(setting.worldBound.width<=286.1f,"Boolean has compact width: "+setting.name);
        }
    }
    private static void Tick()
    {
        if(Time.frameCount<frame) return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Dashboard timeout at "+stage);
            var ui=ConfigWorkbench.Instance;if(ui?.Document==null)return;var root=ui.Document.rootVisualElement;
            if(stage==0)
            {
                target=new RenderTexture(1920,1080,24);target.Create();ui.Document.panelSettings.targetTexture=target;
                originalSystem=ui.Workspace.System.Root.ToString(Formatting.None);originalSequence=ui.Workspace.Sequence.Root.ToString(Formatting.None);Next(1,90);
            }
            else if(stage==1)
            {
                navigation=root.Q<VisualElement>("setup-navigation").worldBound;Layout(ui,"Session");
                Capture("session-1920");
                Check(root.Query<VisualElement>(className:"subject-card").ToList().Select(e=>e.worldBound.y).Distinct().Count()==1,"All four subject cards fit on one Full HD row");
                Check(root.Query<VisualElement>(className:"subject-heading").ToList().All(heading=>heading.Q<Label>(className:"pill").resolvedStyle.whiteSpace==WhiteSpace.NoWrap),"Animal input-mode badges remain on one line");
                Check(root.Q<ListView>("session-plan").worldBound.yMax<root.Q<VisualElement>(className:"footer").worldBound.y,"Complete four-block plan fits above the footer in Full HD");
                foreach(var card in root.Query<VisualElement>(className:"subject-card").ToList())
                    foreach(var input in card.Query<TextField>().ToList())
                        Check(input.Q<VisualElement>(className:"unity-base-field__input").worldBound.yMax<=input.worldBound.yMax+.1f,"Subject input not clipped: "+input.name+" (field "+input.worldBound+", input "+input.Q<VisualElement>(className:"unity-base-field__input").worldBound+")");
                Check(root.Q<TextField>("subject-id-VR1").label=="Animal ID","Real animal ID uses neutral terminology");
                Check(root.Q<VisualElement>("subject-VR1").Q<Label>(className:"pill").text=="Rekkefly","Legacy flight config displays the modern backend name");
                foreach(string icon in new[]{"house","user","play","monitor","sliders-horizontal","square","file-text","trash"})
                    Check(Resources.Load<Texture2D>("ConfigurationUI/Icons/"+icon)!=null,"Bundled Lucide icon loads: "+icon);
                var species=root.Q<TextField>("subject-species-VR1");
                Check(species!=null && species.value=="Agrotis infusa","Species from system JSON appears on the animal card");
                species.value="Agrotis infusa / pilot";
                Check((string)ui.Workspace.System.Root["configs"][0]["speciesName"]==species.value,"Species field edits the shared system JSON document");
                ui.Workspace.System.Undo();species.SetValueWithoutNotify("Agrotis infusa");Call(ui,"EnsureMetadata");
                Check(ui.Workspace.System.Root.ToString(Formatting.None)==originalSystem,"Species edit Undo restores the exact system document");
                root.Q<TextField>("subject-id-VR1").value="Animal A / β 001";Call(ui,"SaveMetadata");
                var saved=JObject.Parse(File.ReadAllText(Path.Combine(RuntimePaths.RunDataRoot,"Backup/AnimalMetadata.json")));
                Check((string)saved["Animals"][0]["AnimalID"]=="Animal A / β 001" && (string)saved["Animals"][0]["SpeciesName"]=="Agrotis infusa","Actual session controls save canonical animal ID and species");
                Check(JToken.DeepEquals(saved["Animals"],saved["Flies"]) && (string)saved["Flies"][0]["FlyID"]=="Animal A / β 001","Actual session save retains equivalent legacy metadata aliases");
                Check(MainController.Instance.CurrentAnimalMetadata.Animals[0].AnimalID=="Animal A / β 001","Saved animal metadata is immediately available to context heartbeats");
                Capture("session-1920"); Submit(root.Q<Button>("open-sequence"));Next(2);
            }
            else if(stage==2)
            {
                Layout(ui,"Sequence");Check(root.Q<Button>("open-sequence").ClassListContains("nav-active"),"Sequence selected highlight");Capture("sequence-1920");
                Submit(root.Q<Button>("open-system"));Next(3);
            }
            else if(stage==3)
            {
                Layout(ui,"System");
                var backend=root.Q<DropdownField>("tracker-backend-configs-0-closedLoopMode");
                Check(backend.choices.Contains("HejjéTrac · walking") && backend.choices.Contains("Rekkefly · flight") && backend.value.Contains("legacy Kinefly"),"Backend picker offers modern modes and preserves current legacy mode");
                backend.value="HejjéTrac · walking";
                Check((string)ui.Workspace.System.Root["configs"][0]["closedLoopMode"]=="Hejjetrac","Explicit backend selection writes its canonical JSON name");
                ui.Workspace.System.Undo();Call(ui,"BuildContent");
                Check(ui.Workspace.System.Root.ToString(Formatting.None)==originalSystem,"Backend selection Undo restores exact legacy JSON");
                Submit(root.Q<VisualElement>("setting-configs-0-tracker").Q<Button>());
                Check(ui.Workspace.System.Root["configs"][0]["tracker"] is JObject,"Optional tracker launch fields can be added through setup");
                var tracker=root.Q<VisualElement>("setting-configs-0-tracker");tracker.Q<Foldout>().value=true;
                root.Q<VisualElement>("setting-configs-0-tracker-executablePath").Q<TextField>().value="/never/launched/tracker";Call(ui,"BuildContent");
                Check(root.Q<Button>("tracker-launch-VR1")!=null && !TrackerProcessManager.IsRunning("VR1"),"Configured tracker offers an explicit Launch button without starting a process");
                ui.Workspace.System.Undo();ui.Workspace.System.Undo();Call(ui,"BuildContent");
                Check(ui.Workspace.System.Root.ToString(Formatting.None)==originalSystem,"Optional tracker setup is fully undoable");
                Check(root.Q<Toggle>("flag-telemetry-enabled").value && root.Q<Toggle>("flag-statusOverlay-visible").value,"Omitted telemetry and status show ON defaults");
                var telemetry=root.Q<VisualElement>("setting-telemetry"); telemetry.Q<Foldout>().value=true;
                root.Q<VisualElement>("setting-statusOverlay").Q<Foldout>().value=true;Next(4);
            }
            else if(stage==4)
            {
                Check(root.Q<VisualElement>("setting-telemetry").Q<TextField>("number-port")?.value=="9880","Omitted telemetry port visibly inherits 9880");
                Check(root.Q<VisualElement>("setting-statusOverlay").Q<TextField>("number-targetDisplay")?.value=="0","Omitted HUD display visibly inherits 0");
                Check(ui.Workspace.System.Root.ToString(Formatting.None)==originalSystem,"Viewing default monitoring controls does not materialize JSON");
                var panelOrder=root.Q<VisualElement>("setting-configs-0-displayOrder");
                Check(panelOrder.tooltip.Contains("R=Right") && panelOrder.tooltip.Contains("disable"),"Camera order explains allow-list and disabled faces");
                Check(root.Q<VisualElement>("setting-configs-0-ledPanelWidth").tooltip.Contains("pixels"),"LED width describes pixels");
                Capture("system-1920");
                var scroll=root.Q<ScrollView>(className:"settings-scroll");scroll.scrollOffset=new Vector2(0,500);Next(5);
            }
            else if(stage==5)
            {
                Capture("monitoring-defaults-1920");
                Submit(root.Q<Button>("open-experiments"));Next(6);
            }
            else if(stage==6)
            {
                Layout(ui,"Experiment library");Capture("experiments-1920");
                var step=ui.Workspace.Sequence.Root["sequences"][2];experiment=ui.Workspace.Open((string)step["parameters"]["configFile"],"choice");
                originalExperiment=experiment.Root.ToString(Formatting.None);
                Call(ui,"Select",experiment);Put(ui,"settingsTab","Motion");Call(ui,"BuildContent");Next(7);
            }
            else if(stage==7)
            {
                Layout(ui,"Motion");
                Check(root.Q<VisualElement>("setting-closedLoopOrientation").Q<Label>(className:"setting-title").text=="Closed loop orientation","Uses community orientation terminology");
                Check(root.Q<VisualElement>("setting-closedLoopPosition").tooltip.Contains("Orientation remains independent"),"Position tooltip explains independent orientation control");
                Capture("motion-1920");
                var setting=root.Q<VisualElement>("setting-closedLoopOrientation");
                using(var evt=PointerMoveEvent.GetPooled(new Event {type=EventType.MouseMove,mousePosition=setting.worldBound.center})) { evt.target=setting;setting.SendEvent(evt); }
                Next(8,80);
            }
            else if(stage==8)
            {
                Check(root.Q<VisualElement>("runtime-hover-help").resolvedStyle.display==DisplayStyle.Flex,"Actual hover event displays runtime help");Capture("hover-help-1920");
                Check(ui.Workspace.Sequence.Root.ToString(Formatting.None)==originalSequence,"Navigation leaves sequence unchanged");
                var toggle=root.Q<VisualElement>("setting-closedLoopPosition").Q<Toggle>();bool old=toggle.value;toggle.value=!old;experiment.Undo();Call(ui,"BuildContent");
                Submit(root.Q<Button>("open-system"));root.Q<Toggle>("flag-telemetry-enabled").value=false;
                Check((bool)ui.Workspace.System.Root["telemetry"]["enabled"]==false && ui.Workspace.System.Root["telemetry"]["port"]==null,"Editing inherited flag writes only selected field");
                ui.Workspace.System.Undo();Check(ui.Workspace.System.Root.ToString(Formatting.None)==originalSystem,"Inherited edit Undo restores exact original JSON");
                Call(ui,"Select",experiment);Put(ui,"jsonMode",true);Call(ui,"BuildContent");
                var draft=(JObject)experiment.Root.DeepClone();draft["futureExactData"]=new JRaw("1.2345678901234567890123456789");
                root.Q<TextField>(className:"raw-json").value=draft.ToString(Formatting.Indented);Next(9);
            }
            else if(stage==9)
            {
                float footer=root.Q<VisualElement>(className:"footer").worldBound.y;
                foreach(string action in new[]{"apply-raw-json","discard-raw-json"})
                {
                    var button=root.Q<Button>(action);
                    Check(button.worldBound.height>0 && button.worldBound.y>=64 && button.worldBound.yMax<=footer,"Long JSON keeps its action visible above the footer: "+action);
                }
                Check(root.Q<TextField>("raw-json-editor").verticalScrollerVisibility==ScrollerVisibility.Auto,"Raw JSON scrolls inside a bounded editor");
                string draft=root.Q<TextField>("raw-json-editor").value;
                Submit(root.Query<Button>().ToList().First(button=>button.text=="Reload disk (undoable)"));
                Check(root.Q<TextField>("raw-json-editor").value==draft && experiment.Root["futureExactData"]==null,"Disk reload cannot discard an unapplied JSON text draft");
                Capture("raw-json-1920");
                Submit(root.Query<Button>().ToList().First(button=>button.text=="Apply JSON"));
                Check(experiment.Root["futureExactData"].ToString()=="1.2345678901234567890123456789","Actual raw JSON Apply preserves unknown exact numeric text");
                Put(ui,"jsonMode",false);Put(ui,"advancedMode",true);Call(ui,"BuildContent");
                var rows=(List<string>)typeof(ConfigWorkbench).GetField("rows",Private).GetValue(ui);
                Check(rows.Contains("futureExactData"),"All fields exposes extensions added in raw JSON");
                root.Q<ListView>(className:"fields").ScrollToItem(rows.IndexOf("futureExactData"));Next(10);
            }
            else if(stage==10)
            {
                Capture("all-fields-1920");Put(ui,"advancedMode",false);Call(ui,"BuildContent");Call(ui,"ShowEditorTools");
                Submit(root.Query<Button>().ToList().First(button=>button.Query<Label>().ToList().Any(label=>label.text=="Revert to saved JSON")));
                Check(experiment.Root["futureExactData"]==null,"Actual Revert tool restores saved JSON");
                Submit(root.Query<Button>().ToList().First(button=>button.text=="Undo"));
                Check(experiment.Root["futureExactData"].ToString()=="1.2345678901234567890123456789","Actual Undo restores the reverted exact JSON draft");
                Submit(root.Query<Button>().ToList().First(button=>button.text=="Undo"));
                Check(experiment.Root.ToString(Formatting.None)==originalExperiment,"Raw JSON and Revert round trip leaves original experiment unchanged");
                Next(11);
            }
            else if(stage==11)
            {
                Check(root.Q<Button>("open-recorded-replay")!=null,"Recorded-run replay is visible in setup navigation");Call(ui,"ShowReplayLauncher");
                Check(root.Query<Button>().ToList().Any(button=>button.Query<Label>().ToList().Any(label=>label.text=="Save setup and open replay")),"Replay navigation offers save-before-leaving");
                Next(12);
            }
            else if(stage==12)
            {
                Capture("replay-launcher-1920");
                Submit(root.Query<Button>().ToList().First(button=>button.Query<Label>().ToList().Any(label=>label.text=="Keep editing")));
                Check(SceneManager.GetActiveScene().name=="ControlScene" && !MainController.Instance.SequenceRunning,"Cancel replay launch keeps setup open");
                foreach(var schema in ui.Workspace.Schemas.Catalog.Properties()) HelpCoverage((JObject)schema.Value,new string[0]);Finish(null);
            }
        }
        catch(Exception error) { Finish(error.ToString()); }
    }
    private static void HelpCoverage(JObject schema,string[] path)
    {
        foreach(var property in (schema["properties"] as JObject??new JObject()).Properties())
        {
            var parts=path.Concat(new[]{property.Name}).ToArray();var child=(JObject)property.Value;
            var help=ConfigParameterHelp.Description(property.Name,parts,child);
            Check(!help.StartsWith("Configuration parameter for this experiment"),"Described schema parameter "+string.Join(".",parts));
            HelpCoverage(child,parts); if(child["items"] is JObject item) HelpCoverage(item,parts);
        }
    }
    private static void Finish(string failure)
    {
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;
        File.WriteAllText(Path.Combine(Application.dataPath,"../config-ui-dashboard-validation.json"),JsonConvert.SerializeObject(new {checks=checks.Count,failure,errors,unity=Application.unityVersion,passed=checks},Formatting.Indented));
        Application.logMessageReceived-=Log;SceneManager.sceneLoaded-=Loaded;EditorApplication.Exit(failure==null && errors.Count==0?0:1);
    }
}
#endif
