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

/// <summary>Real UI Toolkit count editing and history at1/2/4/8/16. Synthetic callbacks, not OS pointer delivery.</summary>
[InitializeOnLoad]
public static class GuiRigCountValidation
{
    private const string Active="GuiRigCountValidation.Active";
    private static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly int[] Counts={1,2,4,8,16};
    private static readonly List<string> checks=new List<string>();
    private static int stage,index,afterFrame;
    private static double deadline;
    private static Rect countBounds,firstRigBounds,saveBounds;
    private static string savedBytes;
    private static RenderTexture target;
    private static string Output=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"gui-rig-count-validation");
    static GuiRigCountValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Active,false))return;
            checks.Clear();stage=0;index=0;afterFrame=Time.frameCount+10;deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Tick;
        };
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Use a marked disposable project.");
        Directory.CreateDirectory(Output);ConfigurationSchemaBuilder.Generate();
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),"{\"apparatusConfiguration\":{\"version\":1,\"configured\":true,\"presentation\":\"led\",\"monitorCount\":1,\"launchBundledTrackers\":false},\"telemetry\":{\"enabled\":false},\"configs\":[{\"vrId\":\"VR1\",\"closedLoopMode\":\"Kinefly\",\"displayOrder\":\"DRBLFU\",\"horizontal\":true,\"targetDisplay\":0,\"zmqAddress\":\"localhost\",\"zmqPort\":30191,\"ledPanelWidth\":16,\"ledPanelHeight\":16,\"startRow\":0,\"startCol\":0,\"unknownRoutingParameter\":\"retain me\",\"tracker\":{\"saveVideo\":false,\"recordingControl\":\"Manual\"}}]}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"__gui-rig-count.json"),"{\"title\":\"Rig-count GUI acceptance\",\"objects\":[]}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),"{\"title\":\"Rig-count GUI acceptance\",\"autoStart\":false,\"loop\":false,\"randomise\":false,\"sequences\":[{\"sceneName\":\"Choice\",\"duration\":1,\"parameters\":{\"configFile\":\"__gui-rig-count.json\"}}]}");
        SessionState.SetBool(Active,true);EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    private static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
    private static void Call(ConfigWorkbench ui,string method,params object[] arguments)=>typeof(ConfigWorkbench).GetMethod(method,Hidden).Invoke(ui,arguments);
    private static Button Button(ConfigWorkbench ui,string name)=>(Button)typeof(ConfigWorkbench).GetField(name,Hidden).GetValue(ui);
    private static void Click(VisualElement element){using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=element;element.SendEvent(evt);}}
    private static bool Same(Rect a,Rect b)=>Vector2.Distance(a.position,b.position)<.25f&&Vector2.Distance(a.size,b.size)<.25f;
    private static JArray Rigs(ConfigWorkbench ui)=>(JArray)ui.Workspace.System.Root["configs"];
    private static void Tick()
    {
        if(Time.frameCount<afterFrame)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("GUI rig-count stage "+stage);
            var ui=ConfigWorkbench.Instance;if(ui==null)return;var root=ui.Document.rootVisualElement;
            if(stage==0)
            {
                target=new RenderTexture(1600,1000,24);target.Create();ui.Document.panelSettings.targetTexture=target;
                Call(ui,"Select",ui.Workspace.System);stage=1;afterFrame=Time.frameCount+5;return;
            }
            if(stage==1)
            {
                countBounds=root.Q<IntegerField>("apparatus-vr-count").worldBound;firstRigBounds=root.Q<Foldout>("apparatus-rig-VR1").worldBound;saveBounds=Button(ui,"save").worldBound;
                stage=2;
            }
            if(stage==2)
            {
                int count=Counts[index];var field=root.Q<IntegerField>("apparatus-vr-count");field.Focus();field.value=count;
                stage=3;afterFrame=Time.frameCount+5;return;
            }
            if(stage==3)
            {
                int count=Counts[index];var rigs=Rigs(ui);
                Check(rigs.Count==count,"Numeric count control creates exactly"+count+" VR configurations");
                Check(rigs.Values<JObject>().Select(rig=>(string)rig["vrId"]).SequenceEqual(Enumerable.Range(1,count).Select(id=>"VR"+id)),count+" rigs have consecutive unique routing IDs");
                Check(rigs.Values<JObject>().Select(rig=>rig["zmqAddress"]+":"+rig["zmqPort"]).Distinct().Count()==count,count+" rigs have distinct tracker endpoints");
                Check((string)rigs[0]["displayOrder"]=="DRBLFU"&&(int)rigs[0]["ledPanelWidth"]==16&&(string)rigs[0]["unknownRoutingParameter"]=="retain me",count+" rig edit retains the original calibration and unknown fields");
                foreach(int id in Enumerable.Range(1,count))Check(root.Q<Foldout>("apparatus-rig-VR"+id)!=null&&root.Q<TextField>("apparatus-order-VR"+id)!=null&&root.Q<IntegerField>("apparatus-port-VR"+id)!=null,"VR"+id+" has editable calibration and tracker controls at count"+count);
                Check(root.Query<Foldout>(className:"apparatus-rig").ToList().Count==count,count+" rig cards match the configuration count");
                Check(Same(countBounds,root.Q<IntegerField>("apparatus-vr-count").worldBound)&&Same(saveBounds,Button(ui,"save").worldBound),count+" rigs retain count and Save control geometry");
                Check(Same(firstRigBounds,root.Q<Foldout>("apparatus-rig-VR1").worldBound),count+" rigs retain first-card geometry through reserved map space");
                Capture("rig-count-"+count+".png");
                if(++index<Counts.Length){stage=2;afterFrame=Time.frameCount+2;return;}
                Check(Button(ui,"save").enabledSelf,"A changed rig count enables Save");Click(Button(ui,"save"));stage=4;afterFrame=Time.frameCount+5;return;
            }
            if(stage==4)
            {
                Check(!ui.Workspace.Documents.Any(document=>document.Dirty)&&!Button(ui,"save").enabledSelf,"Saving all sixteen rigs produces a clean disabled Save state");
                savedBytes=File.ReadAllText(ui.Workspace.System.Path);Check(JObject.Parse(savedBytes)["configs"].Count()==16,"Saved system JSON contains all sixteen rigs");
                Click(Button(ui,"undo"));stage=5;afterFrame=Time.frameCount+5;return;
            }
            if(stage==5)
            {
                Check(Rigs(ui).Count==8&&root.Q<IntegerField>("apparatus-vr-count").value==8,"Undo restores the previous eight-rig UI and document");
                Check(File.ReadAllText(ui.Workspace.System.Path)==savedBytes,"Undo does not silently overwrite saved configuration bytes");
                Check(Button(ui,"save").enabledSelf,"Undo after Save marks the document dirty");Click(Button(ui,"redo"));stage=6;afterFrame=Time.frameCount+5;return;
            }
            if(stage==6)
            {
                Check(Rigs(ui).Count==16&&root.Q<IntegerField>("apparatus-vr-count").value==16,"Redo restores the sixteen-rig UI and document");
                Check(!ui.Workspace.Documents.Any(document=>document.Dirty)&&!Button(ui,"save").enabledSelf,"Redo to the saved state leaves Save disabled");
                Check(ui.Workspace.Validate().Count==0,"All sixteen saved configurations pass workspace validation");
                Finish(null);
            }
        }
        catch(Exception error){Finish(error.GetBaseException().ToString());}
    }
    private static void Capture(string name)
    {
        var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=previous;
    }
    private static void Finish(string failure)
    {
        SessionState.SetBool(Active,false);EditorApplication.update-=Tick;Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,passed=checks,failure,unity=Application.unityVersion,scope="Actual UI Toolkit numeric/Save/Undo/Redo callbacks at1/2/4/8/16, real document serialization and synthetic input. No OS pointer or physical tracking claim."},Formatting.Indented));
        if(failure!=null)Debug.LogError(failure);EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
