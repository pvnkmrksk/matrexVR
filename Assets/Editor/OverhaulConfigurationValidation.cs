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
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

/// <summary>Production parser, document, form and rendered dark-theme checks in a disposable project.</summary>
[InitializeOnLoad]
public static class OverhaulConfigurationValidation
{
    private const string Key="OverhaulConfigurationValidation.Active";
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly List<string> Checks=new List<string>();
    private static RenderTexture target;
    private static int stage,frame;
    private static double deadline;
    private static string Output=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"OverhaulValidation");
    static OverhaulConfigurationValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            Checks.Clear();Checks.AddRange(JsonConvert.DeserializeObject<List<string>>(SessionState.GetString(Key+".checks","[]")));
            stage=0;frame=Time.frameCount+12;deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update+=Tick;
        };
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Checks.Add(message);}
    private static void Call(ConfigWorkbench ui,string name,params object[] args)=>typeof(ConfigWorkbench).GetMethod(name,Private).Invoke(ui,args);
    private static void Put(ConfigWorkbench ui,string name,object value)=>typeof(ConfigWorkbench).GetField(name,Private).SetValue(ui,value);
    private static void Submit(Button button){if(button==null)throw new Exception("Expected button missing");using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}}
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),"KANNADI_VALIDATION_COPY")))throw new Exception("Disposable validation copy required.");
        Directory.CreateDirectory(Output);ConfigurationSchemaBuilder.Generate();Checks.Clear();
        try
        {
            var config=(JObject)LosslessJson.Parse("{\"title\":\"Human title\",\"comments\":\"Preserve notes\",\"objects\":[],\"extension\":1.12345678901234567890123456789}");
            string exact=config["extension"].ToString();ExperimentBlocks.AddPhaseTimeline(config,30);
            Check(ExperimentBlocks.Title(config,"nested/file.json")=="Human title","Human title takes precedence over filename");
            Check(ExperimentBlocks.Title(new JObject(),"nested/my_file.json")=="My file","Untitled legacy config falls back to filename");
            Check(config["comments"].ToString()=="Preserve notes"&&config["stimulus"]["extension"].ToString()==exact,"Phase conversion retains root notes and exact unknown stimulus data");
            Check(ExperimentPhase.Parse(config).Count==1&&ExperimentPhase.Parse(config)[0].Duration==30,"Conversion preserves original active stimulus and timing");
            config["preStimulus"]["enabled"]=true;config["postStimulus"]["enabled"]=true;
            Check(ExperimentPhase.Parse(config).Select(p=>p.Name).SequenceEqual(new[]{"preStimulus","stimulus","postStimulus"}),"Phases stay in pre/stimulus/post order");
            Check(!(bool)config["postStimulus"]["resetPositionOnStart"]&&!(bool)config["postStimulus"]["resetRotationOnStart"],"Optional phase copies default to continuous pose");
            var heading=JObject.Parse("{\"headingReference\":{\"enabled\":true,\"windowSeconds\":3,\"startOffsetSeconds\":2},\"objects\":[]}");
            ExperimentBlocks.AddPhaseTimeline(heading,30);
            var measured=ExperimentPhase.Parse(heading);
            Check(measured.Count==2&&measured[0].Duration==5&&measured[0].Heading.enabled&&!measured[1].Heading.enabled,"Flat heading observation moves into an enabled pre-phase with its configured clock");
            Check(ExperimentBlocks.NextAnimalId("16",16,1)=="32"&&ExperimentBlocks.NextAnimalId("ant009",16,1)=="ant025"&&ExperimentBlocks.NextAnimalId("named",16,1)=="named"&&ExperimentBlocks.NextAnimalId("",16,7)=="7","Animal batch numbering supports numeric, prefixed, padded, empty and named IDs");
            foreach(int count in new[]{1,2,3,4,8,16})
            {
                var apparatus=new JObject();DesktopApparatusProfile.ResizeRigs(apparatus,count);
                Check(DesktopApparatusProfile.Rigs(apparatus).Count()==count,"Resize creates exact VR count "+count);
                Check(DesktopApparatusProfile.Rigs(apparatus).Select(r=>(string)r["vrId"]).Distinct().Count()==count&&DesktopApparatusProfile.Rigs(apparatus).Select(r=>(int)r["zmqPort"]).Distinct().Count()==count,"VR IDs and input ports remain unique at "+count);
            }
            bool rejected=false;try{DesktopApparatusProfile.ResizeRigs(new JObject(),17);}catch(ArgumentException){rejected=true;}Check(rejected,"Above 16 is rejected explicitly");
            var system=new JObject();DesktopApparatusProfile.ResizeRigs(system,16);DesktopApparatusProfile.Begin(system,"led",false);DesktopApparatusProfile.Complete(system);system["telemetry"]=new JObject{["enabled"]=false};
            File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),system.ToString());
            string nested=Path.Combine(Application.streamingAssetsPath,"Overhaul/nested");Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested,"experiment.json"),"{\"title\":\"One reusable experiment\",\"objects\":[],\"closedLoopOrientation\":false,\"closedLoopPosition\":false}");
            File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),"{\"title\":\"Whole paradigm\",\"autoStart\":false,\"loop\":false,\"randomSeedMode\":\"Fixed\",\"randomSeed\":1,\"sequences\":[{\"title\":\"Named block\",\"group\":\"pair\",\"sceneName\":\"Choice\",\"duration\":30,\"parameters\":{\"configFile\":\"Overhaul/nested/experiment.json\"}}]}");
            SessionState.SetString(Key+".checks",JsonConvert.SerializeObject(Checks));SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
        }
        catch(Exception error){Finish(error.GetBaseException().ToString());}
    }
    private static void Render(ConfigWorkbench ui)
    {if(target==null){target=new RenderTexture(1440,900,24);target.Create();}ui.Document.panelSettings.targetTexture=target;}
    private static void Capture(string name)
    {var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());Object.Destroy(image);RenderTexture.active=old;}
    private static void Tick()
    {
        if(Time.frameCount<frame)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Configuration validation timed out at stage "+stage);
            var ui=ConfigWorkbench.Instance;if(ui==null)return;var root=ui.Document.rootVisualElement;Render(ui);
            if(stage==0)
            {
                Check(root.Q<Label>("session-sequence-reference")!=null,"Start shows read-only sequence path");
                var validation=ui.Workspace.Validate(false);Check(validation.Count==0,"16-VR workspace passes production validation: "+string.Join("; ",validation));
                Call(ui,"Select",ui.Workspace.Sequence);
                Check(root.Q<TextField>("config-title").value=="Whole paradigm","Paradigm title is editable in sequence GUI");
                Check(root.Q<VisualElement>("setting-sequences-0-group")!=null,"Block group is available in the inspector");
                root.Q<DropdownField>("sequence-seed-mode").value="Random";
                Check(!root.Q<IntegerField>("sequence-random-seed").enabledInHierarchy,"Random seed mode grays explicit seed");
                root.Q<DropdownField>("sequence-seed-mode").value="Fixed";root.Q<IntegerField>("sequence-random-seed").value=41;
                Check((int)ui.Workspace.Sequence.Root["randomSeed"]==41,"Seed control writes exact sequence JSON");
                var experiment=ui.Workspace.Open("Overhaul/nested/experiment.json","choice");Call(ui,"Select",experiment);Submit(root.Q<Button>("add-phase-timeline"));
                Check(ExperimentBlocks.Phased((JObject)experiment.Root),"GUI conversion creates the phase timeline");
                root.Q<Toggle>("phase-keep-pose").value=true;
                Check(!(bool)experiment.Root["stimulus"]["resetPositionOnStart"]&&!(bool)experiment.Root["stimulus"]["resetRotationOnStart"],"GUI continuity switch preserves stimulus pose");
                Put(ui,"settingsTab","Sky");Call(ui,"BuildContent");
                Check(root.Q<DropdownField>("sky-background-mode").choices.Count==4,"Sky offers default, solid, panorama and astronomical modes");
                root.Q<DropdownField>("sky-background-mode").value="Starry night sky";
                var expanded=(HashSet<string>)typeof(ConfigWorkbench).GetField("expandedSections",Private).GetValue(ui);expanded.Add(experiment.Path+"|stimulus/nightSky");Call(ui,"BuildContent");
                Check(root.Q<Toggle>("night-sky-use-current-time")!=null,"Night sky has direct current/fixed time switch");
                Check(!root.Q<VisualElement>("setting-stimulus-nightSky-localTime").enabledInHierarchy,"Current-time mode disables fixed time field");
                root.Q<Toggle>("night-sky-use-current-time").value=false;
                Check(root.Q<VisualElement>("setting-stimulus-nightSky-localTime").enabledInHierarchy,"Fixed-time mode restores time field");
                root.Q<DropdownField>("night-sky-utc-offset").value="UTC+05:30";
                Check((double)experiment.Root["stimulus"]["nightSky"]["utcOffsetHours"]==5.5,"UTC dropdown supports fractional-hour offsets");
                stage=1;frame=Time.frameCount+8;
            }
            else if(stage==1)
            {
                Color color=root.resolvedStyle.backgroundColor;Check(color.r<.2&&color.g<.2&&color.b<.2,"Setup defaults to a dark background");Capture("configuration-sky-dark.png");
                Call(ui,"Select",ui.Workspace.System);
                Check(root.Query<Foldout>(className:"apparatus-rig").ToList().Count==16,"All 16 rig editors mount");
                Check(!ui.Workspace.Validate(false).Any(error=>error.Contains("stimulus layer")),"Named deterministic VR1–VR16 layer slots pass setup validation");
                foreach(int count in new[]{1,2,4,8,16})Check(root.Q<Button>("apparatus-count-"+count)!=null,"Quick count control "+count+" exists");
                stage=2;frame=Time.frameCount+8;
            }
            else if(stage==2){Capture("configuration-16-rigs-dark.png");Finish(null);}
        }
        catch(Exception error){Finish(error.GetBaseException().ToString());}
    }
    private static void Finish(string failure)
    {
        EditorApplication.update-=Tick;SessionState.SetBool(Key,false);Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output,"configuration.json"),JsonConvert.SerializeObject(new{checks=Checks.Count,failure,completed=Checks,unity=Application.unityVersion},Formatting.Indented));
        EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
