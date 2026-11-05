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
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PathPilotUiValidation
{
    const string Key="PathPilotUiValidation.Active";
    static readonly List<string> checks=new(),errors=new();
    static int stage,frame;static double deadline;static string systemJson,systemPath,output;static RenderTexture target;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);}
    static void Submit(Button b){if(b==null)throw new Exception("Missing UI button");using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
    static void Next(int value){stage=value;frame=Time.frameCount+10;}
    static PathPilotUiValidation(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){output=SessionState.GetString(Key+"Output","");deadline=EditorApplication.timeSinceStartup+80;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}};}
    static void Log(string m,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception)&&!stack.Contains("UnityEditor.Search.SearchDatabase")&&m!="No backup data file found.")errors.Add(m);}
    public static void Run(){
        if(!File.Exists(Path.Combine(Application.dataPath,"../ANT_PILOT_VALIDATION_COPY")))throw new Exception("Isolated pilot only");
        ConfigurationSchemaBuilder.Generate();
        output=Path.GetFullPath(Path.Combine(Application.dataPath,"../validation-output/ui"));Directory.CreateDirectory(output);
        SessionState.SetString(Key+"Output",output);SessionState.SetBool(Key,true);
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    static void Tick(){
        if(Time.frameCount<frame)return;
        try{
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("UI stage "+stage);
            if(errors.Count>0)throw new Exception(errors[0]);
            var ui=ConfigWorkbench.Instance;if(ui?.Document==null)return;var root=ui.Document.rootVisualElement;
            switch(stage){
                case 0:
                    target=new RenderTexture(1600,1000,24);target.Create();ui.Document.panelSettings.targetTexture=target;ui.Document.panelSettings.scale=1;
                    systemJson=ui.Workspace.System.Root.ToString(Formatting.None);systemPath=ui.Workspace.System.Path;
                    Submit(root.Q<Button>("session-load-sequence"));Next(1);break;
                case 1:
                    var picker=root.Q<DropdownField>("sequence-starter-choice");Check(picker!=null,"Sequence picker opens from Start");
                    Check(picker.choices.Contains("Ant · historical reproduction"),"Historical reproduction is selectable");
                    picker.value="Ant · path-angle pilot";
                    Check(root.Q<Label>("sequence-starter-timing").text.StartsWith("3 h ·"),"Pilot chooser shows a three-hour session");
                    Check(root.Q<Button>("sequence-starter-load").enabledSelf,"Pilot is available to load");Capture("pilot-picker");
                    Submit(root.Q<Button>("sequence-starter-load"));Next(2);break;
                case 2:
                    Check(ui.Workspace.Sequence.Path.Replace('\\','/').EndsWith("Experiments/PathAnglePilot/sequence.json"),"Load selects the pilot sequence");
                    Check(ui.Workspace.System.Path==systemPath&&ui.Workspace.System.Root.ToString(Formatting.None)==systemJson,"Load preserves the apparatus path and exact settings");
                    Check(ui.Workspace.Validate().Count==0,"Pilot passes normal workspace validation");
                    Check(root.Q<Label>("sequence-total-duration").text=="3 h total","Sequence overview displays true session duration");
                    Check(root.Query<Label>().ToList().Any(l=>l.text.Contains("10 conditions · randomized complete blocks")),"Ten-condition randomized protocol is visible");
                    Submit(root.Q<Button>("back-to-session"));Next(3);break;
                case 3:
                    Check(ui.Workspace.Sequence.Path.Replace('\\','/').EndsWith("PathAnglePilot/sequence.json"),"Returning to Start retains the pilot selection");
                    Check(root.Query<Label>().ToList().Any(l=>l.text.Contains("Randomized complete blocks · Hard stop")),"Start explains the hard cutoff");Capture("pilot-selected");
                    Submit(root.Q<Button>("session-load-sequence"));Next(4);break;
                case 4:
                    root.Q<DropdownField>("sequence-starter-choice").value="Ant · historical reproduction";Submit(root.Q<Button>("sequence-starter-load"));Next(5);break;
                case 5:
                    Check(ui.Workspace.Sequence.Path.Replace('\\','/').EndsWith("HistoricalReplicationPreview/sequence.json"),"Can switch back to the reproduction");
                    Check(ui.Workspace.System.Root.ToString(Formatting.None)==systemJson,"Switching back also preserves apparatus settings");
                    var catalog=ExperimentBlocks.Catalog(RuntimePaths.ConfigRoot);Check(!catalog.Any(p=>p.Reference=="Experiments/PathAnglePilot/appearance.json"),"Ground appearance is not offered as an experiment block");
                    var pilot=catalog.Single(p=>p.Reference=="Experiments/PathAnglePilot/design.json");Check(pilot.Title=="Ant · path-angle pilot"&&pilot.Duration==10800,"Block library names pilot and respects session duration");
                    Check((double)ExperimentBlocks.NewBlock(pilot)["duration"]>10800,"Added pilot block leaves deadline ownership with pilot controller");
                    Finish(null);break;
            }
        }catch(Exception e){Finish(e.ToString());}
    }
    static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var t=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,target.width,target.height),0,0);t.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),t.EncodeToPNG());RenderTexture.active=old;Object.Destroy(t);}
    static void Finish(string error){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(new{passed=error==null,error,checks,errors},Formatting.Indented));EditorApplication.Exit(error==null?0:1);}
}
#endif
