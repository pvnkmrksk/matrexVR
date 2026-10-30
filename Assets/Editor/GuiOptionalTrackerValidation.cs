#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ConfigurationUI;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>GUI companion failures exercise actual preparation coroutines; process boundaries are injected.</summary>
[InitializeOnLoad]
public static class GuiOptionalTrackerValidation
{
    private const string Active="GuiOptionalTrackerValidation.Active";
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly List<string> checks=new List<string>();
    private static int stage,afterFrame,started,attempted;
    private static double deadline,began;
    private static string Output=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"gui-optional-tracker-validation");
    static GuiOptionalTrackerValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Active,false))return;
            stage=0;started=0;attempted=0;checks.Clear();afterFrame=Time.frameCount+12;deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Tick;
        };
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Use a marked disposable project.");
        ConfigurationSchemaBuilder.Generate();
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),"{\"apparatusConfiguration\":{\"version\":1,\"configured\":true,\"presentation\":\"led\",\"monitorCount\":1,\"launchBundledTrackers\":true},\"telemetry\":{\"enabled\":false},\"configs\":[{\"vrId\":\"VR1\",\"closedLoopMode\":\"Rekkefly\",\"displayOrder\":\"DRBLFU\",\"horizontal\":true,\"targetDisplay\":0,\"zmqAddress\":\"127.0.0.1\",\"zmqPort\":30191,\"ledPanelWidth\":16,\"ledPanelHeight\":16,\"startRow\":0,\"startCol\":0,\"tracker\":{\"saveVideo\":false,\"recordingControl\":\"Manual\"}}]}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"__gui-optional-tracker.json"),"{\"title\":\"Optional native tracker\",\"objects\":[]}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),"{\"title\":\"Optional native tracker\",\"autoStart\":false,\"loop\":false,\"randomise\":false,\"sequences\":[{\"sceneName\":\"Choice\",\"duration\":30,\"parameters\":{\"configFile\":\"__gui-optional-tracker.json\"}}]}");
        SessionState.SetBool(Active,true);EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);}
    private static void Set(ConfigWorkbench ui,string field,object value)=>typeof(ConfigWorkbench).GetField(field,Hidden).SetValue(ui,value);
    private static object Get(ConfigWorkbench ui,string field)=>typeof(ConfigWorkbench).GetField(field,Hidden).GetValue(ui);
    private static void Begin(ConfigWorkbench ui,bool startAfter)=>typeof(ConfigWorkbench).GetMethod("BeginCompanionPreparation",Hidden).Invoke(ui,new object[]{startAfter});
    private static List<string> Warnings(ConfigWorkbench ui)=>(List<string>)Get(ui,"companionWarnings");
    private static void Tick()
    {
        if(Time.frameCount<afterFrame)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Optional tracker GUI stage "+stage);
            var ui=ConfigWorkbench.Instance;if(ui==null&&stage<6)return;
            if(stage==0)
            {
                Set(ui,"startExperimentForValidation",(Action)(()=>started++));
                Set(ui,"prepareCompanionForValidation",(Func<SystemConfig,bool,Task<CompanionCatalog.Prepared>>)((rig,demo)=>{attempted++;return Task.FromException<CompanionCatalog.Prepared>(new FileNotFoundException("Synthetic missing tracker bundle"));}));
                Begin(ui,true);stage=1;afterFrame=Time.frameCount+4;return;
            }
            if(stage<6&&(bool)Get(ui,"preparing"))return;
            if(stage==1)
            {
                Check(started==1&&attempted==1,"A faulted native preparation task still reaches experiment Start once");Check(Warnings(ui).Count==1,"The missing-bundle reason is retained as a warning");
                Set(ui,"prepareCompanionForValidation",(Func<SystemConfig,bool,Task<CompanionCatalog.Prepared>>)((rig,demo)=>{attempted++;return Task.FromResult(new CompanionCatalog.Prepared{rig=rig});}));
                Set(ui,"launchCompanionForValidation",(Action<CompanionCatalog.Prepared>)(_=>throw new IOException("Synthetic launch failure")));
                Begin(ui,true);stage=2;afterFrame=Time.frameCount+4;return;
            }
            if(stage==2)
            {
                Check(started==2&&Warnings(ui).Exists(message=>message.Contains("launch failure")),"A native launch exception warns and still reaches experiment Start");
                Set(ui,"prepareCompanionForValidation",(Func<SystemConfig,bool,Task<CompanionCatalog.Prepared>>)((rig,demo)=>throw new InvalidOperationException("Synthetic incompatible tracker")));
                Begin(ui,true);stage=3;afterFrame=Time.frameCount+4;return;
            }
            if(stage==3)
            {
                Check(started==3&&Warnings(ui).Exists(message=>message.Contains("incompatible")),"A synchronous preparation exception cannot block VR Start");
                ui.Workspace.System.Edit(root=>DesktopApparatusProfile.ResizeRigs((Newtonsoft.Json.Linq.JObject)root,16));
                Set(ui,"prepareCompanionForValidation",(Func<SystemConfig,bool,Task<CompanionCatalog.Prepared>>)((rig,demo)=>{attempted++;return new TaskCompletionSource<CompanionCatalog.Prepared>().Task;}));
                Set(ui,"launchCompanionForValidation",(Action<CompanionCatalog.Prepared>)(_=>throw new Exception("A pending task was launched")));
                began=EditorApplication.timeSinceStartup;Begin(ui,true);stage=4;afterFrame=Time.frameCount+4;return;
            }
            if(stage==4)
            {
                Check(started==4&&Warnings(ui).Count==16,"Sixteen pending native preparations produce per-rig warnings and still start the VR");
                Check(EditorApplication.timeSinceStartup-began<4,"Sixteen missing trackers share one bounded preparation budget");
                Set(ui,"prepareCompanionForValidation",(Func<SystemConfig,bool,Task<CompanionCatalog.Prepared>>)((rig,demo)=>Task.FromException<CompanionCatalog.Prepared>(new IOException("Explicit Open tracker failure"))));
                Begin(ui,false);stage=5;afterFrame=Time.frameCount+4;return;
            }
            if(stage==5)
            {
                Check(started==4,"An explicit Open tracker failure does not start the experiment");
                Check(ui.Document.rootVisualElement.Q<UnityEngine.UIElements.Button>("start-experiment").enabledSelf,"The primary Start action is usable after a tracker-opening failure");
                Set(ui,"startExperimentForValidation",null);
                Set(ui,"prepareCompanionForValidation",(Func<SystemConfig,bool,Task<CompanionCatalog.Prepared>>)((rig,demo)=>Task.FromException<CompanionCatalog.Prepared>(new FileNotFoundException("GUI requested missing native bundle"))));
                var start=ui.Document.rootVisualElement.Q<Button>("start-experiment");using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=start;start.SendEvent(evt);}
                began=EditorApplication.timeSinceStartup;stage=6;afterFrame=Time.frameCount+4;return;
            }
            if(stage==6)
            {
                var main=MainController.Instance;if(!main.SequenceRunning){if(EditorApplication.timeSinceStartup-began<12)return;throw new Exception("Real GUI Start did not reach the experiment clock: "+main.SequenceError);}
                Check(main.SequenceRunning,"The real GUI Start callback reaches Main's running experiment despite missing native bundles");
                Check(main.RecordingWarnings.Count>=16&&System.Linq.Enumerable.Any(main.RecordingWarnings,message=>message.Contains("GUI requested missing native bundle")),"GUI preparation warnings survive Main's configuration reset and remain available during the experiment");
                main.StopSequence("gui-optional-tracker-validation");stage=7;afterFrame=Time.frameCount+4;return;
            }
            if(stage==7)
            {
                if(MainController.Instance.SequenceStopping||MasterDataLogger.Instance.IsFinalizing)return;
                Check(!MainController.Instance.SequenceRunning,"The experiment started through the GUI stops cleanly without native trackers");
                Check(string.IsNullOrEmpty(MasterDataLogger.Instance.FinalizationError),"Optional-native GUI run finalizes without poisoning the recording archive");Finish(null);
            }
        }
        catch(Exception error){Finish(error.GetBaseException().ToString());}
    }
    private static void Finish(string failure)
    {
        SessionState.SetBool(Active,false);EditorApplication.update-=Tick;Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,passed=checks,failure,unity=Application.unityVersion,scope="Actual GUI coroutines, real Main Start/Stop and recording finalization; native prepare/launch process boundaries are injected. No physical tracker or video-acquisition claim."},Formatting.Indented));
        if(failure!=null)Debug.LogError(failure);EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
