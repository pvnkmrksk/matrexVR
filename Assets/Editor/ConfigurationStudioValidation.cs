#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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

[InitializeOnLoad]
public static class ConfigurationStudioValidation
{
    private const string Key="ConfigurationStudioValidation.Active";
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly List<string> checks=new List<string>(),errors=new List<string>();
    private static readonly Dictionary<string,double> timings=new Dictionary<string,double>();
    private static int stage,frame,runs;
    private static double deadline;
    private static RenderTexture target;
    private static List<ExperimentPreset> library;
    private static void Check(bool value,string label) { if(!value) throw new Exception(label); checks.Add(label); }
    private static void Call(ConfigWorkbench ui,string name,params object[] args) => typeof(ConfigWorkbench).GetMethod(name,Private).Invoke(ui,args);
    private static void Put(ConfigWorkbench ui,string key,object value) => typeof(ConfigWorkbench).GetField(key,Private).SetValue(ui,value);
    private static void Submit(Button button) { using(var e=NavigationSubmitEvent.GetPooled()) { e.target=button; button.SendEvent(e); } }
    static ConfigurationStudioValidation()
    {
        EditorApplication.playModeStateChanged+=s=>{
            if(s!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false)) return;
            stage=0; frame=Time.frameCount+10; deadline=EditorApplication.timeSinceStartup+150;
            EditorApplication.update+=Tick; Application.logMessageReceived+=Log; SceneManager.sceneLoaded+=NoTracking;
        };
    }
    private static void Log(string message,string stack,LogType type)
    { if((type==LogType.Exception || type==LogType.Error || type==LogType.Assert) && !stack.Contains("UnityEditor.Search.SearchDatabase")) errors.Add(message); }
    private static void NoTracking(Scene scene,LoadSceneMode mode)
    { foreach(var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) listener.enabled=false; }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable validation copy only.");
        ConfigurationSchemaBuilder.Generate();
        typeof(ConfigurationUIValidation).GetMethod("UnitChecks",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        library=ExperimentBlocks.Catalog(Application.streamingAssetsPath);
        Check(library.Any(p=>p.Reference=="Templates/experiment.template.json") && library.Any(p=>p.Schema=="swarm") && library.Any(p=>p.Schema=="dynamic-choice"),"Library discovers full configs across supported paradigms");
        Check(library.All(p=>p.Schema!="system" && p.Schema!="sequence" && !p.Reference.StartsWith("Archive/")),"Block library excludes system/sequence and archived collections");
        var pure=LosslessJson.Parse("{\"sequences\":[{\"tag\":\"a\",\"extension\":1.2345678901234567890123456789},{\"tag\":\"b\"},{\"tag\":\"c\"}]}");
        ExperimentBlocks.Move(pure,0,2); Check((string)pure["sequences"][2]["tag"]=="a" && pure["sequences"][2]["extension"].ToString()=="1.2345678901234567890123456789","Reorder retains extensions and numeric lexemes");
        var phased=JObject.Parse("{\"preStimulus\":{\"enabled\":true,\"durationSeconds\":10,\"headingReference\":{\"enabled\":true,\"windowSeconds\":20}},\"stimulus\":{\"enabled\":true,\"durationSeconds\":30},\"postStimulus\":{\"enabled\":false,\"durationSeconds\":100}}");
        Check(ExperimentBlocks.EffectiveDuration(phased,900)==50,"Overview accounts for phase-owned timing and heading assessment");
        Check(ExperimentBlocks.Time(3860)=="1 h 4 min 20 s","Run overview reports exact minutes and seconds");
        Check(ExperimentBlocks.Describe(JObject.Parse("{\"uniformSkyColor\":{},\"skyboxPath\":\"x.png\",\"nightSky\":{\"enabled\":true}}")).Contains("Uniform sky") && !ExperimentBlocks.Describe(JObject.Parse("{\"uniformSkyColor\":{},\"skyboxPath\":\"x.png\",\"nightSky\":{\"enabled\":true}}")).Contains("Live sky"),"Overview respects actual sky override precedence");
        var schemas=ConfigSchema.Load();
        Check(ConfigSchema.Enabled(JObject.Parse("{\"initialRotation\":{\"x\":0,\"y\":90,\"z\":0}}"),(JObject)schemas.Get("swarm")["properties"]["vrConfigs"]["items"],"initialRotation"),"Per-rig pose remains editable without nonexistent sibling flags");
        Check(!ConfigSchema.Enabled(JObject.Parse("{\"resetRotationOnStart\":false,\"randomInitialRotation\":false}"),schemas.Get("choice"),"initialRotation"),"Disabling pose reset greys its stored start rotation");
        SessionState.SetInt(Key+".unitExtra",checks.Count);
        var system=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Templates/system_config.template.json"))); system["telemetry"]["enabled"]=false;
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),system.ToString());
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"ui-smoke.json"),"{\"objects\":[],\"closedLoopOrientation\":false,\"closedLoopPosition\":false}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),"{\"autoStart\":false,\"loop\":false,\"randomise\":false,\"sequences\":[{\"sceneName\":\"Choice\",\"duration\":0.4,\"gain\":1,\"parameters\":{\"configFile\":\"ui-smoke.json\"}}]}");
        SessionState.SetBool(Key,true); EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity"); EditorApplication.isPlaying=true;
    }
    private static void Later(int next,int delay=8) { stage=next; frame=Time.frameCount+delay; }
    private static void Render(ConfigWorkbench ui)
    { if(target==null) { target=new RenderTexture(1440,900,24); target.Create(); } ui.Document.panelSettings.targetTexture=target; }
    private static void Capture(string file)
    { RenderTexture.active=target; var image=new Texture2D(1440,900,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1440,900),0,0); image.Apply(); RenderTexture.active=null; File.WriteAllBytes(Path.Combine(Application.dataPath,"../"+file),image.EncodeToPNG()); Object.Destroy(image); }
    private static void Tick()
    {
        if(Time.frameCount<frame) return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Studio validation timeout, stage "+stage);
            var ui=ConfigWorkbench.Instance;
            if(stage==0)
            {
                Check(ui!=null && ui.Document.rootVisualElement.Q<ListView>(className:"block-list")!=null,"Sequence builder is the default workspace");
                library=ExperimentBlocks.Catalog(Application.streamingAssetsPath);
                string before=ui.Workspace.Sequence.Root.ToString(Formatting.None); Call(ui,"BuildContent"); Check(ui.Workspace.Sequence.Root.ToString(Formatting.None)==before,"Rendering builder controls never inserts or rounds values");
                // Add blocks through the same GUI library button and selection event the experimenter uses.
                Call(ui,"ShowBlockLibrary",new object[]{null});
                var list=ui.Document.rootVisualElement.Q<ListView>("preset-list"); var source=(IList)list.itemsSource;
                int found=Enumerable.Range(0,source.Count).First(i=>((ExperimentPreset)source[i]).Reference=="Templates/experiment.template.json"); list.SetSelection(found);
                Check(ui.Document.rootVisualElement.Q<Label>("preset-title")!=null,"Library selection shows config effects and timing before insertion");
                Submit(ui.Document.rootVisualElement.Q<Button>("insert-preset"));
                Check(ui.Workspace.Sequence.Root["sequences"].Count()==2 && (string)ui.Workspace.Sequence.Root["sequences"][1]["parameters"]["configFile"]=="Templates/experiment.template.json","Actual library Add button inserts a referenced complete config");
                Check(ui.Document.rootVisualElement.Query<Button>().ToList().Any(b=>b.Query<Label>().ToList().Any(l=>l.text=="Experiment")),"Newly referenced experiment appears in navigation immediately");
                ui.AddPresetBlock(library.First(p=>p.Reference=="Examples/Swarm/heading-aligned.example.json"));
                string custom=Path.Combine(Application.dataPath,"../studio-custom-yaw.json"); File.Copy(Path.Combine(Application.streamingAssetsPath,"Examples/Optomotor/yaw.example.json"),custom,true);
                Call(ui,"ShowBlockLibrary",new object[]{null}); ui.Document.rootVisualElement.Q<TextField>("custom-preset-path").value=custom;
                Submit(ui.Document.rootVisualElement.Query<Button>().ToList().First(b=>b.Query<Label>().ToList().Any(l=>l.text=="Load custom path")));
                Submit(ui.Document.rootVisualElement.Q<Button>("insert-preset"));
                Check((string)ui.Workspace.Sequence.Root["sequences"][3]["parameters"]["configFile"]==custom,"Custom absolute paths load and insert through the real library UI");
                ui.Workspace.Sequence.Edit(r=>((JArray)r["sequences"]).RemoveAt(0)); Put(ui,"activeBlock",0); Call(ui,"BuildNavigation"); Call(ui,"BuildContent"); Render(ui); Later(1);
            }
            else if(stage==1)
            {
                Capture("config-ui-studio-sequence.png");
                Check(ui.Document.rootVisualElement.Query<WorkbenchIcon>().ToList().Count>8,"Native vector icons render throughout builder");
                var steps=(JArray)ui.Workspace.Sequence.Root["sequences"]; steps[0]["extension"]=new JRaw("1.2345678901234567890123456789");
                string original=steps[0].ToString(Formatting.None);
                var list=ui.Document.rootVisualElement.Q<ListView>(className:"block-list");
                var move=list.viewController.GetType().GetMethod("Move",BindingFlags.Instance|BindingFlags.Public);
                Check(move!=null,"Native ListView reorder controller exposes Move"); move.Invoke(list.viewController,new object[]{0,2});
                Check(ui.Workspace.Sequence.Root["sequences"][2].ToString(Formatting.None)==original,"Native drag reorder event updates JSON while preserving full blocks");
                ui.Workspace.Sequence.Undo(); Call(ui,"BuildContent");
                ui.DuplicateBlock(0); Check(ui.Workspace.Sequence.Root["sequences"][1].ToString(Formatting.None)==original,"Duplicate copies every block field losslessly"); ui.RemoveBlock(1); Check(ui.Workspace.Sequence.Root["sequences"].Count()==3,"Remove affects only selected block");
                Put(ui,"activeBlock",1); Call(ui,"BuildContent");
                var gain=ui.Document.rootVisualElement.Q<TextField>("number-gain"); gain.value="1.12345678901234567890123456789";
                Check(ui.Workspace.Sequence.Root["sequences"][1]["gain"].ToString()=="1.12345678901234567890123456789","Exact number entry retains precision beyond slider float precision");
                var slider=ui.Document.rootVisualElement.Q<Slider>("slider-gain"); slider.value=2.3f; Call(ui,"FlushNumbers");
                Check(ui.Workspace.Sequence.Root["sequences"][1]["gain"].ToString()=="2.3","Slider and exact entry synchronize on explicit edits");
                ui.Workspace.Sequence.Undo(); Check(ui.Workspace.Sequence.Root["sequences"][1]["gain"].ToString()=="1.12345678901234567890123456789","Undo slider edit restores the exact previous number");
                Call(ui,"EditBlock",0); Put(ui,"settingsTab","Motion"); Call(ui,"BuildContent"); Render(ui); Later(2);
            }
            else if(stage==2)
            {
                Capture("config-ui-studio-settings.png");
                var toggle=ui.Document.rootVisualElement.Q<Toggle>("flag-stimulus-autopilotEnabled");
                Check(toggle!=null && toggle.ClassListContains("is-on"),"Movement switch displays an illuminated ON state");
                toggle.value=false;
                Check(ui.Workspace.Documents.First(d=>d.Path.EndsWith("experiment.template.json")).Root["stimulus"]["autopilotEnabled"].Value<bool>()==false,"Actual styled switch updates experiment config");
                Check(!ui.Document.rootVisualElement.Q<VisualElement>("setting-stimulus-autopilotSpeed").enabledInHierarchy,"Autopilot OFF greys its dependent speed immediately");
                ui.Workspace.Documents.First(d=>d.Path.EndsWith("experiment.template.json")).Undo(); Call(ui,"BuildContent");
                string before=ui.Workspace.Documents.First(d=>d.Path.EndsWith("experiment.template.json")).Root.ToString(Formatting.None); Call(ui,"BuildContent"); Check(before==ui.Workspace.Documents.First(d=>d.Path.EndsWith("experiment.template.json")).Root.ToString(Formatting.None),"Grouped editors preserve all untouched config values");
                var experiment=ui.Workspace.Documents.First(d=>d.Path.EndsWith("experiment.template.json"));
                experiment.Edit(r=>((JObject)r).Remove("postStimulus")); string withoutPost=experiment.Root.ToString(Formatting.None);
                Put(ui,"activePhase","postStimulus"); Call(ui,"BuildContent");
                Check(experiment.Root.ToString(Formatting.None)==withoutPost,"Viewing an absent optional phase does not create JSON settings");
                Check(ui.Document.rootVisualElement.Q<Label>("config-effects").text.Contains("inactive"),"Absent/disabled phase overview does not claim its settings are active");
                experiment.Undo(); Put(ui,"activePhase","stimulus"); Call(ui,"BuildContent");
                Call(ui,"Select",ui.Workspace.Sequence); Call(ui,"ShowBlockLibrary",new object[]{null});
                var list=ui.Document.rootVisualElement.Q<ListView>("preset-list"); var source=(IList)list.itemsSource;
                list.SetSelection(Enumerable.Range(0,source.Count).First(i=>((ExperimentPreset)source[i]).Reference=="Examples/Swarm/heading-aligned.example.json")); Render(ui); Later(3);
            }
            else if(stage==3)
            {
                Capture("config-ui-studio-library.png"); Call(ui,"CloseModal");
                Call(ui,"Select",ui.Workspace.System);
                ui.Document.rootVisualElement.Q<TextField>("number-vSyncCount").value="1";
                Check(!ui.Document.rootVisualElement.Q<VisualElement>("setting-targetFrameRate").enabledInHierarchy && ui.Document.rootVisualElement.Q<Label>("system-sync").text.Contains("overridden"),"Numeric vSync changes immediately grey FPS controls and update the system overview");
                Render(ui); Later(30);
            }
            else if(stage==30)
            {
                Capture("config-ui-studio-system.png"); ui.Workspace.System.Undo(); Call(ui,"Select",ui.Workspace.Sequence);
                var steps=(JArray)ui.Workspace.Sequence.Root["sequences"]; string extension=steps[0]["extension"].ToString(); string gain=steps[0]["gain"].ToString();
                ui.AddPresetBlock(library.First(p=>p.Reference=="Examples/Optomotor/yaw.example.json"),null,0);
                Check((string)ui.Workspace.Sequence.Root["sequences"][0]["parameters"]["configFile"]=="Examples/Optomotor/yaw.example.json" && ui.Workspace.Sequence.Root["sequences"][0]["extension"].ToString()==extension && ui.Workspace.Sequence.Root["sequences"][0]["gain"].ToString()==gain,"Replacing a config changes its reference and preserves existing block tuning/extensions");
                ui.Workspace.Sequence.Undo();
                string oldReference=(string)ui.Workspace.Sequence.Root["sequences"][0]["parameters"]["configFile"];
                Call(ui,"ShowVariation",0); var destination=ui.Document.rootVisualElement.Query<TextField>().ToList().First(t=>t.label=="New path");
                string variation="UserConfigs/studio-variation-"+Guid.NewGuid().ToString("N")+".json"; destination.value=variation;
                string originalConfig=ui.Workspace.Documents.First(d=>d.Path.EndsWith("experiment.template.json")).Root.ToString(Formatting.None);
                Submit(ui.Document.rootVisualElement.Query<Button>().ToList().First(b=>b.Query<Label>().ToList().Any(l=>l.text=="Create variation")));
                Check((string)ui.Workspace.Sequence.Root["sequences"][0]["parameters"]["configFile"]==variation && LosslessJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,variation))).ToString(Formatting.None)==originalConfig,"Creating a variation copies every config value and relinks only that block");
                ui.Workspace.Sequence.Undo(); Call(ui,"Select",ui.Workspace.Sequence); Check((string)ui.Workspace.Sequence.Root["sequences"][0]["parameters"]["configFile"]==oldReference,"Undo variation restores the original reference");
                // Benchmark native cards, rather than only the advanced JSON list.
                var many=(JArray)ui.Workspace.Sequence.Root["sequences"]; var first=many[0].DeepClone(); while(many.Count<10000) many.Add(first.DeepClone());
                var watch=Stopwatch.StartNew(); Call(ui,"BuildContent"); watch.Stop(); timings["mount10000BlockCardsMs"]=watch.Elapsed.TotalMilliseconds; Render(ui); Later(4);
            }
            else if(stage==4)
            {
                int count=ui.Document.rootVisualElement.Query<VisualElement>(className:"block-card").ToList().Count;
                Check(count>0 && count<20,"10,000 native block cards mount fewer than 20 visual cards (actual "+count+")");
                foreach(var doc in ui.Workspace.Documents) if(doc!=ui.Workspace.Sequence && doc.Dirty) doc.Reload();
                ui.Workspace.Sequence.Reload(); Call(ui,"BuildContent");
                // Raw JSON is still the full fidelity escape hatch.
                Put(ui,"jsonMode",true); Call(ui,"BuildContent"); var raw=ui.Document.rootVisualElement.Q<TextField>(className:"raw-json"); raw.value=raw.value.Replace("0.4","0.5");
                Submit(ui.Document.rootVisualElement.Query<Button>().ToList().First(b=>b.text=="Apply JSON")); Check(ui.Workspace.Sequence.Root["sequences"][0]["duration"].ToString()=="0.5","Raw JSON Apply remains interoperable with builder");
                Put(ui,"jsonMode",false); Call(ui,"BuildContent"); ui.Document.panelSettings.targetTexture=null; ui.StartExperiment(); Later(5,2);
            }
            else if(stage==5)
            {
                Check(MainController.Instance.SequenceRunning,"Refined builder starts the production runner");
                Check(ui==null && Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Length==0,"Refined UI and its modal/schedulers are absent during experiment"); Later(6,5);
            }
            else if(stage==6 && ui!=null)
            {
                Check(SceneManager.GetActiveScene().name=="ControlScene" && !MainController.Instance.SequenceRunning,"Experiment completion recreates the builder");
                if(runs++==0) { ui.StartExperiment(); Later(5,2); } else { Check(errors.Count==0,"No UI or experiment runtime errors"); Finish(null); }
            }
        }
        catch(Exception error) { Finish(error.GetBaseException().ToString()); }
    }
    private static void Finish(string failure)
    {
        EditorApplication.update-=Tick; Application.logMessageReceived-=Log; SceneManager.sceneLoaded-=NoTracking; SessionState.SetBool(Key,false);
        var unit=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"../ui-unit-validation.json")));
        File.WriteAllText(Path.Combine(Application.dataPath,"../config-ui-studio-validation.json"),JsonConvert.SerializeObject(new {checks=(int)unit["checks"]+checks.Count+SessionState.GetInt(Key+".unitExtra",0),failure,errors,timings,unity=Application.unityVersion,studioChecks=checks},Formatting.Indented));
        EditorApplication.isPlaying=false; EditorApplication.delayCall+=()=>EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
