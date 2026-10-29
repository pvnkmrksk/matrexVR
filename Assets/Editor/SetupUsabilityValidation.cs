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

[InitializeOnLoad]
public static class SetupUsabilityValidation
{
    private const string Key="SetupUsabilityValidation.Active";
    private static readonly List<string> checks=new List<string>();
    private static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int stage,afterFrame;
    private static double deadline;
    private static RenderTexture texture;
    private static string Output=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"SetupUsabilityValidation");
    static SetupUsabilityValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            checks.Clear();stage=0;afterFrame=Time.frameCount+8;deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Tick;
        };
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),"KANNADI_VALIDATION_COPY")))throw new Exception("Use a disposable marked project.");
        Directory.CreateDirectory(Output);ConfigurationSchemaBuilder.Generate();
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),"{\"apparatusConfiguration\":{\"version\":1,\"configured\":true,\"presentation\":\"led\",\"monitorCount\":1,\"launchBundledTrackers\":false},\"telemetry\":{\"enabled\":false},\"configs\":[{\"vrId\":\"VR1\",\"closedLoopMode\":\"Kinefly\",\"displayOrder\":\"F\",\"targetDisplay\":0,\"zmqAddress\":\"localhost\",\"zmqPort\":30191,\"ledPanelWidth\":16,\"ledPanelHeight\":16}]}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"__setup-usability.json"),"{\"title\":\"Small experiment\",\"objects\":[]}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),"{\"title\":\"Usability validation\",\"autoStart\":false,\"loop\":false,\"sequences\":[{\"sceneName\":\"Choice\",\"duration\":1,\"parameters\":{\"configFile\":\"__setup-usability.json\"}}]}");
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    private static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
    private static void Call(ConfigWorkbench ui,string name,params object[] args)=>typeof(ConfigWorkbench).GetMethod(name,Hidden).Invoke(ui,args);
    private static void Submit(VisualElement target)
    {
        if(target==null)throw new Exception("Expected control missing");
        target.Focus();
        using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=target;target.SendEvent(evt);}
    }
    private static void Tick()
    {
        if(Time.frameCount<afterFrame)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timed out waiting for setup at stage "+stage);
            var main=MainController.Instance;var ui=ConfigWorkbench.Instance;if(main==null||ui==null)return;
            var root=ui.Document.rootVisualElement;
            if(stage==0)
            {
                Check(main.TargetFrameRate==60&&Application.targetFrameRate==60,"Omitted cap defaults to60 in the production loader");
                Check(!root.Q<Button>("save-changes").enabledSelf,"Clean setup disables Save changes");
                string original=ui.Workspace.Sequence.Root.ToString();Submit(root.Q<Label>("about-matrex"));
                Check(root.Q<Label>("about-credits")?.text.Contains("Pavan Kumar Kaushik")==true,"Quiet brand action opens design credits");
                Call(ui,"CloseModal");Check(root.Q<Label>("about-credits")==null&&ui.Workspace.Sequence.Root.ToString()==original&&!ui.Workspace.Sequence.Dirty,"Closing About preserves the saved draft and keeps credits out of the main workspace");
                Submit(root.Q<Label>("setup-frame-rate"));
                Check(root.Q<Foldout>("apparatus-operator-options")?.value==true&&root.Q<VisualElement>("setting-targetFrameRate")!=null,"Cap readout opens its exact system setting");
                stage=1;afterFrame=Time.frameCount+3;return;
            }
            if(stage==1)
            {
                var cap=root.Q<TextField>("number-targetFrameRate");cap.Focus();
                Check(!root.Q<Button>("save-changes").enabledSelf,"Focusing a clean delayed field does not enable Save");
                // Exercise the actual text editing manipulator after its layout/
                // focus pass; assigning TextElement.text is a programmatic commit.
                cap.SelectAll();var textInput=cap.Q<TextElement>();
                using(var key=KeyDownEvent.GetPooled('7',KeyCode.Alpha7,EventModifiers.None)){key.target=textInput;textInput.SendEvent(key);}
                using(var key=KeyDownEvent.GetPooled('3',KeyCode.Alpha3,EventModifiers.None)){key.target=textInput;textInput.SendEvent(key);}
                bool saveEnabled=root.Q<Button>("save-changes").enabledSelf;
                Check(saveEnabled,"Pending delayed text enables Save; enabled="+saveEnabled+" dirty="+ui.Workspace.System.Dirty+" value="+cap.value+" text="+cap.text+" json="+ui.Workspace.System.Root["targetFrameRate"]);
                Check(!ui.Workspace.System.Dirty,"Delayed input remains uncommitted until Save/blur; value="+cap.value+" text="+cap.text+" json="+ui.Workspace.System.Root["targetFrameRate"]);
                Submit(root.Q<Button>("save-changes"));
                var savedCap=JObject.Parse(File.ReadAllText(ui.Workspace.System.Path))["targetFrameRate"];
                var status=(Label)typeof(ConfigWorkbench).GetField("status",Hidden).GetValue(ui);
                Check((int?)savedCap==73,"Save commits the pending delayed numeric buffer through its production callback; value="+savedCap+" status="+status.text+" draft="+ui.Workspace.System.Root["targetFrameRate"]+" enabled="+root.Q<Button>("save-changes").enabledSelf);
                Check(main.TargetFrameRate==73&&Application.targetFrameRate==73&&root.Q<Label>("setup-frame-rate").text.Contains("cap 73"),"Saving the idle cap applies it immediately to the engine and header");
                Check(!root.Q<Button>("save-changes").enabledSelf,"Save returns to disabled after successful persistence");
                cap=root.Q<TextField>("number-targetFrameRate");cap.Focus();cap.SelectAll();textInput=cap.Q<TextElement>();
                using(var key=KeyDownEvent.GetPooled('7',KeyCode.Alpha7,EventModifiers.None)){key.target=textInput;textInput.SendEvent(key);}
                using(var key=KeyDownEvent.GetPooled('4',KeyCode.Alpha4,EventModifiers.None)){key.target=textInput;textInput.SendEvent(key);}
                Check(!ui.Workspace.System.Dirty&&root.Q<Button>("save-changes").enabledSelf,"Second delayed edit remains pending before the save shortcut");
                using(var key=KeyDownEvent.GetPooled('s',KeyCode.S,EventModifiers.Control)){key.target=textInput;textInput.SendEvent(key);}
                Check((int?)JObject.Parse(File.ReadAllText(ui.Workspace.System.Path))["targetFrameRate"]==74&&!ui.Workspace.System.Dirty&&!root.Q<Button>("save-changes").enabledSelf&&main.TargetFrameRate==74,"Ctrl+S commits and persists a focused delayed editor within the same key event");
                Call(ui,"ShowSessionHome");var comments=root.Q<TextField>("session-comments");comments.value=(comments.value??"")+" metadata edit";
                Check(root.Q<Button>("save-changes").enabledSelf,"Subject/session metadata edits also enable Save");Submit(root.Q<Button>("save-changes"));
                Check(!root.Q<Button>("save-changes").enabledSelf,"Saving metadata returns Save to its clean disabled state");
                Call(ui,"Select",ui.Workspace.System);
                Check(root.Q<IntegerField>("apparatus-vr-count")!=null&&root.Q<Button>("apparatus-count-1")==null,"One numeric VR count replaces quick-count shortcuts");
                Check(root.Q<DropdownField>("apparatus-presentation")==null&&root.Q<Button>("apparatus-output-led")!=null&&root.Q<Button>("apparatus-output-desktop")!=null,"Two output icon buttons replace the output dropdown");
                Submit(root.Q<Button>("apparatus-output-desktop"));
                Check(DesktopApparatusProfile.Presentation(ui.Workspace.System.Root)=="desktop"&&root.Q<Button>("apparatus-output-desktop").ClassListContains("is-on"),"Monitor icon selects the desktop presentation and shows selection");
                ui.Workspace.System.Undo();Call(ui,"BuildContent");Call(ui,"UpdateStatus",new object[]{null});
                Check(DesktopApparatusProfile.Presentation(ui.Workspace.System.Root)=="led"&&!ui.Workspace.System.Dirty,"Output selection remains undoable without losing calibration");
                ui.OpenFrameRateSetting();
                Check(root.Q<Foldout>("overlay-advanced-settings")!=null&&root.Q<Label>("overlay-configuration-path")?.text==ui.Workspace.System.Path,"Advanced overlay settings expose the exact physical configuration path");
                Check(!root.Q<VisualElement>("setting-frameOverlay-x").enabledInHierarchy,"Automatic overlay placement grays manual coordinates");
                root.Q<Toggle>("flag-frameOverlay-automaticPlacement").value=false;
                Check(root.Q<VisualElement>("setting-frameOverlay-x").enabledInHierarchy,"Disabling automatic placement enables preferred coordinates");
                root.Q<Toggle>("flag-frameOverlay-visible").value=false;
                Check(!root.Q<VisualElement>("setting-frameOverlay-x").enabledInHierarchy,"Disabling an overlay grays its placement controls");
                texture=new RenderTexture(1440,900,24);texture.Create();ui.Document.panelSettings.targetTexture=texture;
                stage=2;afterFrame=Time.frameCount+8;return;
            }
            if(stage==2)
            {
                var active=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,"setup-refinements.png"),image.EncodeToPNG());Object.Destroy(image);RenderTexture.active=active;
                string system=ui.Workspace.System.Path,before=File.ReadAllText(system);
                Object.DestroyImmediate(ui.gameObject);
                var running=typeof(MainController).GetField("sequenceStarted",Hidden);running.SetValue(main,true);
                try
                {
                    ConfigWorkbench.RequestFrameRateSetting();
                    Check(RuntimeFrameRateEditor.IsOpen&&main.SequenceRunning,"Opening live cap settings does not stop the experiment");
                    main.SetRuntimeFrameRate(30);
                    Check(Application.targetFrameRate==30&&main.TargetFrameRate==30&&main.SequenceRunning,"Live cap applies immediately while acquisition remains running");
                    Check(File.ReadAllText(system)==before,"Live cap changes do not overwrite the saved initial system config");
                    string path=Path.Combine(MasterDataLogger.Instance.directoryPath,"runtime_display_settings.jsonl");
                    var evidence=JObject.Parse(File.ReadLines(path).Last());Check((int)evidence["target"]==30&&evidence["utc"]!=null&&evidence["unityFrame"]!=null,"Live performance changes append explicit timestamp/frame provenance");
                    typeof(MainController).GetProperty("VSyncCount").SetValue(main,1);
                    bool rejected=false;try{main.SetRuntimeFrameRate(25);}catch(InvalidOperationException){rejected=true;}
                    Check(rejected&&main.TargetFrameRate==30,"VSync prevents an ineffective cap edit");
                    typeof(MainController).GetProperty("VSyncCount").SetValue(main,0);main.SetRuntimeFrameRate(60);
                }
                finally{running.SetValue(main,false);var editor=Object.FindFirstObjectByType<RuntimeFrameRateEditor>();if(editor!=null)Object.DestroyImmediate(editor.gameObject);}
                Finish(null);
            }
        }
        catch(Exception error){Finish(error.GetBaseException().ToString());}
    }
    private static void Finish(string failure)
    {
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,passed=checks,failure,unity=Application.unityVersion},Formatting.Indented));
        if(failure!=null)Debug.LogError(failure);EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
