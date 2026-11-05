#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ConfigurationUI;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Rendered operator screens at practical desktop sizes. Synthetic callbacks only.</summary>
[InitializeOnLoad]
public static class ResponsiveWorkbenchValidation
{
    private const string Active="ResponsiveWorkbenchValidation.Active";
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly List<string> checks=new List<string>();
    private static readonly Vector2Int[] Sizes={new Vector2Int(1280,800),new Vector2Int(1440,900),new Vector2Int(1600,1000)};
    private static readonly string[] Pages={"Home","Basics","Appearance","Population","Motion","Advanced","Apparatus","Sequence"};
    private static int sizeIndex,pageIndex,stage,afterFrame;
    private static double deadline,previewDeadline;
    private static Rect modelBounds,colorBounds;
    private static Texture previousPreview;
    private static RenderTexture target;
    private static bool baseline;
    private static string Output=>Path.Combine(Path.GetDirectoryName(Application.dataPath),baseline?"responsive-workbench-before":"responsive-workbench-validation");
    static ResponsiveWorkbenchValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Active,false))return;
            baseline=SessionState.GetBool(Active+".Baseline",false);typeof(ConfigWorkbench).GetField("forceRuntimeColorControls",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,!baseline);sizeIndex=0;pageIndex=0;stage=0;checks.Clear();afterFrame=Time.frameCount+12;deadline=EditorApplication.timeSinceStartup+240;EditorApplication.update+=Tick;
        };
    }
    public static void RunBaseline(){SessionState.SetBool(Active+".Baseline",true);Run();}
    public static void RunValidation(){SessionState.SetBool(Active+".Baseline",false);Run();}
    private static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Use a marked disposable project.");
        ConfigurationSchemaBuilder.Generate();ChoiceAssetCatalogBuilder.Generate();
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),"{\"apparatusConfiguration\":{\"version\":1,\"configured\":true,\"presentation\":\"led\",\"monitorCount\":1,\"launchBundledTrackers\":false},\"telemetry\":{\"enabled\":false},\"configs\":[{\"vrId\":\"VR1\",\"closedLoopMode\":\"Kinefly\",\"displayOrder\":\"DRBLFU\",\"horizontal\":true,\"targetDisplay\":0,\"zmqAddress\":\"localhost\",\"zmqPort\":30191,\"ledPanelWidth\":16,\"ledPanelHeight\":16,\"startRow\":0,\"startCol\":0,\"tracker\":{\"saveVideo\":false,\"recordingControl\":\"Manual\"}}]}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"__responsive-gui.json"),"{\"title\":\"Object and landscape experiment\",\"durationSeconds\":30,\"closedLoopOrientation\":true,\"closedLoopPosition\":true,\"autopilotEnabled\":false,\"autopilotSpeed\":0.4,\"uniformSkyColor\":null,\"skyboxPath\":\"\",\"sun\":{\"enabled\":true,\"intensity\":1},\"objects\":[{\"type\":\"Cube\",\"modelPath\":\"\",\"material\":\"White\",\"position\":{\"x\":0,\"y\":0,\"z\":3},\"rotation\":{\"x\":0,\"y\":0,\"z\":0},\"scale\":{\"x\":1,\"y\":1,\"z\":1}}]}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"__responsive-population.json"),"{\"title\":\"Kannadi body appearance\",\"populationMode\":\"Mirror\",\"kannadiModelPath\":\"Models/Examples/SimulatedLocust.glb\",\"kannadiTilePrefab\":\"SimulatedLocust\",\"modelAppearance\":{\"enabled\":true,\"color\":{\"r\":0.2,\"g\":0.4,\"b\":0.8,\"a\":1}}}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),"{\"title\":\"Responsive GUI\",\"autoStart\":false,\"loop\":false,\"randomise\":false,\"sequences\":[{\"sceneName\":\"Choice\",\"duration\":30,\"parameters\":{\"configFile\":\"__responsive-gui.json\"}}]}");
        SessionState.SetBool(Active,true);EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    private static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
    private static void Call(ConfigWorkbench ui,string method,params object[] args)=>typeof(ConfigWorkbench).GetMethod(method,Hidden).Invoke(ui,args);
    private static void Render(ConfigWorkbench ui,Vector2Int size)
    {
        if(target!=null){ui.Document.panelSettings.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);}
        target=new RenderTexture(size.x,size.y,24);target.Create();ui.Document.panelSettings.scale=1;ui.Document.panelSettings.targetTexture=target;
    }
    private static void Page(ConfigWorkbench ui)
    {
        string name=Pages[pageIndex];
        if(name=="Home")Call(ui,"ShowSessionHome");
        else if(name=="Apparatus")Call(ui,"Select",ui.Workspace.System);
        else if(name=="Sequence")Call(ui,"Select",ui.Workspace.Sequence);
        else if(name=="Population")
        {var document=ui.Workspace.Open("__responsive-population.json","swarm");Call(ui,"Select",document);typeof(ConfigWorkbench).GetField("settingsTab",Hidden).SetValue(ui,"Appearance");Call(ui,"BuildContent");}
        else
        {
            var document=ui.Workspace.Documents.First(value=>value.Path.EndsWith("__responsive-gui.json",StringComparison.Ordinal));if(name=="Appearance")document.Edit(root=>{root["objects"][0]["modelPath"]="";if(root["objects"][0]["appearance"] is Newtonsoft.Json.Linq.JObject appearance)appearance["enabled"]=false;});Call(ui,"Select",document);
            string tab=baseline?(name=="Basics"?"Essentials":name=="Appearance"?"Sky":name=="Advanced"?"All parameters":name):name;
            typeof(ConfigWorkbench).GetField("settingsTab",Hidden).SetValue(ui,tab);Call(ui,"BuildContent");
        }
    }
    private static void Tick()
    {
        if(Time.frameCount<afterFrame)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Responsive GUI stage "+stage);
            var ui=ConfigWorkbench.Instance;if(ui==null)return;
            if(stage==0){Render(ui,Sizes[sizeIndex]);Page(ui);stage=1;previewDeadline=EditorApplication.timeSinceStartup+8;afterFrame=Time.frameCount+12;return;}
            var root=ui.Document.rootVisualElement;string label=Sizes[sizeIndex].x+"x"+Sizes[sizeIndex].y+" "+Pages[pageIndex];
            if(!baseline&&Pages[pageIndex]=="Population"&&root.Q<VisualElement>("object-preview-kannadiModelPath")?.Q<Image>()?.image==null&&EditorApplication.timeSinceStartup<previewDeadline)return;
            if(!baseline&&Pages[pageIndex]=="Appearance")
            {
                var preview=root.Q<VisualElement>("object-preview-objects-0-modelPath");var image=preview?.Q<Image>();
                if((image?.image==null||stage==2&&ReferenceEquals(previousPreview,image.image))&&EditorApplication.timeSinceStartup<previewDeadline)return;
                Check(image?.image!=null,label+" renders an actual object thumbnail: "+preview?.Q<Label>()?.text);
                if(stage==1)
                {
                    modelBounds=root.Q<VisualElement>("setting-objects-0-modelPath").worldBound;colorBounds=root.Q<Toggle>("appearance-enabled-objects-0").worldBound;previousPreview=image.image;
                    var chooser=root.Q<DropdownField>("external-model-objects-0-modelPath");string example="Models/Examples/RiggedSimple.glb";Check(chooser.choices.Contains(example),label+" lists the embedded animated GLB example");chooser.value=example;
                    root.Q<Toggle>("appearance-enabled-objects-0").value=true;
                    var swatch=root.Q<Button>("color-objects-0-appearance-color");using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=swatch;swatch.SendEvent(evt);}
                    stage=3;afterFrame=Time.frameCount+6;return;
                }
                if(stage==3)
                {
                    var hex=root.Q<TextField>("runtime-color-hex");Check(hex!=null,label+" opens the player color dialog");hex.value="#26A633";
                    var apply=root.Q<Button>("apply-runtime-color");Check(apply!=null&&apply.worldBound.width>=100&&apply.worldBound.xMin>=0&&apply.worldBound.xMax<=Sizes[sizeIndex].x&&apply.worldBound.yMax<=Sizes[sizeIndex].y,label+" keeps the color Apply action visible");
                    Capture(label.Replace(' ','-')+"-color");previousPreview=image.image;using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=apply;apply.SendEvent(evt);}
                    stage=2;previewDeadline=EditorApplication.timeSinceStartup+8;afterFrame=Time.frameCount+12;return;
                }
                Check(Vector2.Distance(modelBounds.position,root.Q<VisualElement>("setting-objects-0-modelPath").worldBound.position)<.25f&&Vector2.Distance(colorBounds.position,root.Q<Toggle>("appearance-enabled-objects-0").worldBound.position)<.25f,label+" GLB and color changes retain field geometry");
                Check(!root.Q<VisualElement>("setting-objects-0-type").enabledInHierarchy,label+" external GLB grays the retained built-in selector");
                var owner=ui.Workspace.Documents.First(document=>document.Path.EndsWith("__responsive-gui.json",StringComparison.Ordinal)).Root["objects"][0];
                Check((string)owner["modelPath"]=="Models/Examples/RiggedSimple.glb"&&(bool)owner["appearance"]["enabled"]&&ExperimentBlocks.Number(owner["appearance"]["color"]["g"])>.6,label+" model and color controls edit the actual experiment document");
            }
            if(!baseline)
            {
                foreach(string name in new[]{"start-experiment","save-changes"})
                {
                    var action=root.Q<Button>(name);Check(action!=null&&action.resolvedStyle.display!=DisplayStyle.None&&action.worldBound.width>=100&&action.worldBound.height>=30,label+" keeps "+name+" visible");
                    Check(action.worldBound.xMin>=0&&action.worldBound.xMax<=Sizes[sizeIndex].x+.5f&&action.worldBound.yMax<=Sizes[sizeIndex].y+.5f,label+" keeps "+name+" inside the window");
                }
                if(!new[]{"Home","Apparatus","Sequence"}.Contains(Pages[pageIndex]))Check(root.Q<VisualElement>(className:"settings-tabs").Query<Button>().ToList().Count==4,label+" has four conceptual tabs");
            }
            if(!baseline&&Pages[pageIndex]=="Appearance")
            {
                var color=root.Q<Toggle>("appearance-enabled-objects-0");Check(color!=null&&color.worldBound.yMax<root.Q<VisualElement>(className:"footer").worldBound.yMin,label+" keeps the object color override above the footer");
                var model=root.Q<TextField>("external-model-path-objects-0-modelPath");Check(model!=null&&model.worldBound.width>200&&model.worldBound.yMax<root.Q<VisualElement>(className:"footer").worldBound.yMin,label+" exposes a readable external GLB path field");
            }
            if(!baseline&&Pages[pageIndex]=="Population")
            {
                Check(root.Q<VisualElement>("object-preview-kannadiModelPath")?.Q<Image>()?.image!=null,label+" renders the selected Kannadi GLB preview");
                var model=root.Q<TextField>("external-model-path-kannadiModelPath");Check(model!=null&&model.worldBound.width>200,label+" exposes the Kannadi GLB path");Check(!root.Q<VisualElement>("setting-kannadiTilePrefab").enabledInHierarchy,label+" external population GLB grays the retained built-in model selector");
                var surface=root.Q<VisualElement>("setting-modelAppearance");var detail=surface?.Q<Foldout>(className:"surface-details");var toggle=detail?.Q<Toggle>(className:"unity-foldout__toggle");
                Check(toggle!=null&&toggle.resolvedStyle.display!=DisplayStyle.None&&toggle.worldBound.height>=24,label+" exposes population Surface details instead of hiding its button");
                var color=root.Q<Button>("color-modelAppearance-color");Check(color!=null&&color.worldBound.yMax<root.Q<VisualElement>(className:"footer").worldBound.yMin,label+" keeps population color controls visible; color="+color?.worldBound+"; surface="+surface.worldBound+"; footer="+root.Q<VisualElement>(className:"footer").worldBound);
            }
            if(!baseline&&Pages[pageIndex]=="Basics")
                foreach(var arrows in root.Query<VisualElement>(className:"spin-arrows").ToList())foreach(var button in arrows.Query<Button>().ToList())Check(button.worldBound.height<=14.5f&&button.worldBound.yMin>=arrows.worldBound.yMin-.5f&&button.worldBound.yMax<=arrows.worldBound.yMax+.5f,label+" numeric stepper buttons stay inside their control");
            Capture(label.Replace(' ','-'));
            if(++pageIndex<Pages.Length){Page(ui);stage=1;previewDeadline=EditorApplication.timeSinceStartup+8;afterFrame=Time.frameCount+12;return;}
            pageIndex=0;if(++sizeIndex<Sizes.Length){stage=0;afterFrame=Time.frameCount+1;return;}
            Finish(null);
        }
        catch(Exception error){Finish(error.GetBaseException().ToString());}
    }
    private static void Capture(string name)
    {
        Directory.CreateDirectory(Output);var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=previous;
    }
    private static void Finish(string failure)
    {
        SessionState.SetBool(Active,false);EditorApplication.update-=Tick;Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,passed=checks,failure,unity=Application.unityVersion,scope="Real UI Toolkit graphics at1280x800/1440x900/1600x1000. Synthetic callbacks; no native pointer or physical tracker claim."},Formatting.Indented));
        if(failure!=null)Debug.LogError(failure);EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
