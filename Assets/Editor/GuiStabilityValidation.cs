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
using Object = UnityEngine.Object;

/// <summary>Real control identity, layout, focus and mode behavior; disposable project only.</summary>
[InitializeOnLoad]
public static class GuiStabilityValidation
{
    private const string Active="GuiStabilityValidation.Active";
    private static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly List<string> checks=new List<string>();
    private static int stage,afterFrame;
    private static double deadline,previewDeadline;
    private static VisualElement held,secondary;
    private static Rect heldBounds,secondaryBounds;
    private static Vector2 scrollOffset;
    private static RenderTexture target;
    private static string Output=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"gui-stability-validation");
    static GuiStabilityValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Active,false))return;
            checks.Clear();stage=0;afterFrame=Time.frameCount+10;deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update+=Tick;
        };
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Use a marked disposable project.");
        Directory.CreateDirectory(Output);ConfigurationSchemaBuilder.Generate();
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),"{\"apparatusConfiguration\":{\"version\":1,\"configured\":true,\"presentation\":\"led\",\"monitorCount\":1,\"launchBundledTrackers\":false},\"telemetry\":{\"enabled\":false},\"configs\":[{\"vrId\":\"VR1\",\"closedLoopMode\":\"Kinefly\",\"displayOrder\":\"DRBLFU\",\"horizontal\":true,\"targetDisplay\":0,\"zmqAddress\":\"localhost\",\"zmqPort\":30191,\"ledPanelWidth\":16,\"ledPanelHeight\":16,\"startRow\":0,\"startCol\":0,\"tracker\":{\"saveVideo\":false,\"recordingControl\":\"Manual\"}}]}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"__gui-stability.json"),"{\"title\":\"Stable editor\",\"closedLoopOrientation\":0,\"closedLoopPosition\":true,\"autopilotEnabled\":false,\"autopilotSpeed\":0.4,\"initialPosition\":{\"x\":0,\"y\":1,\"z\":0},\"initialRotation\":{\"x\":0,\"y\":0,\"z\":0},\"uniformSkyColor\":null,\"skyboxPath\":\"\",\"objects\":[],\"nightSky\":{\"enabled\":false,\"localTime\":\"22:00:00\",\"localDate\":\"2026-10-29\",\"latitudeDegrees\":12,\"longitudeDegrees\":77,\"utcOffsetHours\":5.5},\"sun\":{\"enabled\":true,\"intensity\":1},\"extension\":{\"largeInteger\":9007199254740993}}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),"{\"title\":\"GUI stability\",\"autoStart\":false,\"loop\":false,\"randomise\":false,\"sequences\":[{\"sceneName\":\"Choice\",\"duration\":1,\"parameters\":{\"configFile\":\"__gui-stability.json\"}}]}");
        SessionState.SetBool(Active,true);EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    private static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
    private static void Call(ConfigWorkbench ui,string method,params object[] arguments)=>typeof(ConfigWorkbench).GetMethod(method,Hidden).Invoke(ui,arguments);
    private static void Tab(ConfigWorkbench ui,string tab){typeof(ConfigWorkbench).GetField("settingsTab",Hidden).SetValue(ui,tab);Call(ui,"BuildContent");}
    private static void Click(VisualElement element){Check(element!=null,"Requested control exists");using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=element;element.SendEvent(evt);}}
    private static bool Same(Rect a,Rect b)=>Vector2.Distance(a.position,b.position)<.25f&&Vector2.Distance(a.size,b.size)<.25f;
    private static void Hold(VisualElement a,VisualElement b){held=a;secondary=b;heldBounds=a.worldBound;secondaryBounds=b.worldBound;}
    private static void Tick()
    {
        if(Time.frameCount<afterFrame)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("GUI stage "+stage);
            var ui=ConfigWorkbench.Instance;if(ui==null)return;var root=ui.Document.rootVisualElement;
            if(stage==0)
            {
                target=new RenderTexture(1600,1000,24);target.Create();ui.Document.panelSettings.targetTexture=target;
                foreach(string icon in new[]{"chevron-down","rotate-ccw","refresh-cw","layout-dashboard","ellipsis","eye","save","palette","image","box","code-xml"})Check(Resources.Load<Texture2D>("ConfigurationUI/Icons/"+icon)!=null,"Pinned Lucide icon loads: "+icon);
                JToken enumValue=null;Action<JToken> changed=value=>enumValue=value;
                var enumField=(DropdownField)typeof(ConfigWorkbench).GetMethod("EnumControl",Hidden).Invoke(ui,new object[]{new JArray("First","Second",0,1),new JValue(1),changed});root.Add(enumField);
                Check(enumField.choices.SequenceEqual(new[]{"First","Second"})&&enumField.value=="Second","Numeric enum JSON is displayed as semantic choices, without duplicate0/1 entries");
                enumField.value="First";Check(enumValue?.Type==JTokenType.Integer&&(int)enumValue==0,"Named enum selection preserves a saved numeric representation");enumField.RemoveFromHierarchy();
                Call(ui,"Select",ui.Workspace.Sequence);stage=1;afterFrame=Time.frameCount+5;return;
            }
            if(stage==1)
            {
                Check(root.Q<Toggle>("sequence-loop")!=null&&root.Q<Button>("sequence-loop")==null,"Loop is a standard checkbox, not a state button");
                Check(root.Q<IntegerField>("sequence-loop-start")!=null&&!root.Q<IntegerField>("sequence-loop-start").enabledSelf,"Inactive repeat block remains present and gray");
                Hold(root.Q<IntegerField>("sequence-loop-start"),root.Q<IntegerField>("sequence-random-seed"));
                root.Q<Toggle>("sequence-loop").value=true;root.Q<DropdownField>("sequence-seed-mode").value="Random";
                stage=2;afterFrame=Time.frameCount+4;return;
            }
            if(stage==2)
            {
                Check(ReferenceEquals(held,root.Q<IntegerField>("sequence-loop-start"))&&ReferenceEquals(secondary,root.Q<IntegerField>("sequence-random-seed")),"Loop and seed mode retain the exact controls");
                Check(Same(heldBounds,held.worldBound)&&Same(secondaryBounds,secondary.worldBound),"Loop and seed changes retain control geometry; loop "+heldBounds+" => "+held.worldBound+"; seed "+secondaryBounds+" => "+secondary.worldBound);
                Check(held.enabledSelf&&!secondary.enabledSelf,"Loop enables its field and Random grays the retained explicit seed");Capture("stable-sequence.png");
                var doc=ui.Workspace.Documents.First(value=>value.Path.EndsWith("__gui-stability.json",StringComparison.Ordinal));Call(ui,"Select",doc);Tab(ui,"Motion");stage=3;afterFrame=Time.frameCount+4;return;
            }
            if(stage==3)
            {
                var legacy=root.Q<Toggle>("flag-closedLoopOrientation");Check(legacy!=null,"A schema boolean stored as0 is displayed as a checkbox");legacy.value=true;
                Check(ui.Workspace.Documents.First(value=>value.Path.EndsWith("__gui-stability.json",StringComparison.Ordinal)).Root["closedLoopOrientation"].Type==JTokenType.Boolean,"Editing a legacy boolean writes the declared boolean type");
                var enabled=root.Q<Toggle>("flag-autopilotEnabled");Hold(root.Q<VisualElement>("setting-autopilotSpeed"),enabled);
                Check(!held.enabledInHierarchy,"Inactive autopilot speed stays visible but disabled");
                enabled.Focus();enabled.value=true;stage=4;afterFrame=Time.frameCount+4;return;
            }
            if(stage==4)
            {
                Check(ReferenceEquals(held,root.Q<VisualElement>("setting-autopilotSpeed"))&&ReferenceEquals(secondary,root.Q<Toggle>("flag-autopilotEnabled")),"Feature flags preserve the exact form controls");
                Check(Same(heldBounds,held.worldBound)&&Same(secondaryBounds,secondary.worldBound),"Feature flags do not move the field grid");
                Check(held.enabledInHierarchy&&(root.panel.focusController.focusedElement==secondary||secondary.Contains(root.panel.focusController.focusedElement as VisualElement)),"Enabling a feature updates dependencies and retains keyboard focus");
                Capture("stable-motion.png");Tab(ui,"Sky");var group=root.Q<VisualElement>("setting-nightSky");group.Q<Foldout>().value=true;
                stage=5;afterFrame=Time.frameCount+4;return;
            }
            if(stage==5)
            {
                Hold(root.Q<VisualElement>("setting-uniformSkyColor"),root.Q<VisualElement>("setting-sun"));
                var time=root.Q<VisualElement>("setting-nightSky-localTime");Check(time!=null,"Night-sky time is present before enabling its mode");
                root.Q<DropdownField>("sky-background-mode").value="Starry night sky";stage=6;afterFrame=Time.frameCount+4;return;
            }
            if(stage==6)
            {
                Check(Same(heldBounds,held.worldBound)&&Same(secondaryBounds,secondary.worldBound),"Sky selection does not move color or light controls");
                Check(!held.enabledInHierarchy&&root.Q<VisualElement>("setting-nightSky").enabledInHierarchy,"Sky selection grays the other categories in place");
                Hold(root.Q<VisualElement>("setting-nightSky-localTime"),root.Q<VisualElement>("setting-nightSky-utcOffsetHours"));
                root.Q<Toggle>("night-sky-use-current-time").value=true;stage=7;afterFrame=Time.frameCount+4;return;
            }
            if(stage==7)
            {
                Check(Same(heldBounds,held.worldBound)&&Same(secondaryBounds,secondary.worldBound),"Current-time mode retains time/offset geometry");
                Check(!held.enabledInHierarchy,"Current-time mode disables the retained fixed-time field");
                var timeField=held.Q<TextField>();using(var stale=ChangeEvent<string>.GetPooled("22:00:00","23:00:00")){stale.target=timeField;timeField.SendEvent(stale);}
                Check(ui.Workspace.Documents.First(value=>value.Path.EndsWith("__gui-stability.json",StringComparison.Ordinal)).Root["nightSky"]["localTime"].ToString()=="now","A queued old blur event cannot overwrite the newer current-time selection");
                Check(root.Q<TextField>("number-autopilotSpeed")==null,"Sky tab has its own stable parameter category");
                stage=70;previewDeadline=EditorApplication.timeSinceStartup+5;afterFrame=Time.frameCount+10;return;
            }
            if(stage==70)
            {
                var preview=root.Q<VisualElement>("sky-rendered-preview");if(preview?.Q<Image>()?.image==null&&EditorApplication.timeSinceStartup<previewDeadline)return;Check(preview?.Q<Image>()?.image!=null,"Visible edit-time sky pane receives a rendered thumbnail: "+preview?.Q<Label>()?.text+"; pane="+preview?.worldBound+"; root="+root.panel.visualTree.worldBound);
                Capture("stable-sky.png");Call(ui,"Select",ui.Workspace.System);stage=8;afterFrame=Time.frameCount+4;return;
            }
            if(stage==8)
            {
                Hold(root.Q<VisualElement>("apparatus-led-controls-VR1"),root.Q<TextField>("apparatus-order-VR1"));
                var scroll=root.Q<ScrollView>("apparatus-scroll");scroll.scrollOffset=new Vector2(0,100);scrollOffset=scroll.scrollOffset;
                Click(root.Q<Button>("apparatus-output-desktop"));stage=9;afterFrame=Time.frameCount+4;return;
            }
            if(stage==9)
            {
                Check(ReferenceEquals(held,root.Q<VisualElement>("apparatus-led-controls-VR1"))&&ReferenceEquals(secondary,root.Q<TextField>("apparatus-order-VR1")),"Output selection retains the LED calibration controls");
                // Scroll position is compared separately from world bounds.
                Check(Mathf.Abs(root.Q<ScrollView>("apparatus-scroll").scrollOffset.y-scrollOffset.y)<.25f,"Output selection preserves the operator's scroll position");
                Check(!held.enabledInHierarchy,"Monitor output grays retained LED calibration");
                Click(root.Q<Button>("apparatus-output-led"));stage=10;afterFrame=Time.frameCount+4;return;
            }
            if(stage==10)
            {
                Check(held.enabledInHierarchy&&root.Q<TextField>("apparatus-order-VR1").value=="DRBLFU","Returning to LED output restores the calibrated face order");
                Check(ui.Workspace.Documents.First(value=>value.Path.EndsWith("__gui-stability.json",StringComparison.Ordinal)).Root["extension"]["largeInteger"].ToString()=="9007199254740993","In-place edits retain unknown exact JSON extension values");
                root.Q<ScrollView>("apparatus-scroll").scrollOffset=Vector2.zero;stage=11;afterFrame=Time.frameCount+4;return;
            }
            if(stage==11)
            {
                Capture("stable-apparatus.png");
                var doc=ui.Workspace.Documents.First(value=>value.Path.EndsWith("__gui-stability.json",StringComparison.Ordinal));Call(ui,"Select",doc);Tab(ui,"Visibility");stage=12;afterFrame=Time.frameCount+5;return;
            }
            if(stage==12)
            {
                var grid=root.Q<VisualElement>("visibility-matrix-grid");Check(grid!=null&&grid.Query<Toggle>().ToList().Count==256,"Visibility reserves its full16 by16 checkbox matrix");
                Hold(grid,root.Q<Toggle>("visibility-vr1-vr1"));Check(!held.enabledInHierarchy,"Disabled visibility matrix stays visible and gray");
                root.Q<Toggle>("visibility-matrix-enabled").value=true;stage=13;afterFrame=Time.frameCount+4;return;
            }
            if(stage==13)
            {
                Check(Same(heldBounds,held.worldBound)&&Same(secondaryBounds,secondary.worldBound),"Enabling the matrix retains all cell geometry");
                Check(root.Q<Toggle>("visibility-vr1-vr1").enabledInHierarchy&&!root.Q<Toggle>("visibility-vr2-vr2").enabledInHierarchy,"Only configured VR matrix cells become editable");
                secondary.Focus();((Toggle)secondary).value=true;stage=14;afterFrame=Time.frameCount+4;return;
            }
            if(stage==14)
            {
                Check(ReferenceEquals(secondary,root.Q<Toggle>("visibility-vr1-vr1"))&&Same(secondaryBounds,secondary.worldBound),"Matrix cell edits retain the cell and its position");
                Check(root.panel.focusController.focusedElement==secondary||secondary.Contains(root.panel.focusController.focusedElement as VisualElement),"Matrix checkbox edits retain keyboard focus");
                var inspector=root.Q<VisualElement>(className:"studio-inspector");
                if(inspector!=null)foreach(var field in inspector.Query<TextField>().ToList())Check(field.worldBound.xMax<=inspector.worldBound.xMax+1,"Inspector text fields fit the inspector width");
                Capture("stable-visibility.png");Call(ui,"Select",ui.Workspace.Sequence);stage=15;afterFrame=Time.frameCount+4;return;
            }
            if(stage==15){Capture("stable-sequence.png");Finish(null);}
        }
        catch(Exception error){Finish(error.GetBaseException().ToString());}
    }
    private static void Capture(string name)
    {
        var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());Object.Destroy(image);RenderTexture.active=previous;
    }
    private static void Finish(string failure)
    {
        SessionState.SetBool(Active,false);EditorApplication.update-=Tick;Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,passed=checks,failure,unity=Application.unityVersion,scope="Real UI Toolkit value edits, control identity, geometry, focus, scroll and disabled-mode behavior. No physical tracker claim."},Formatting.Indented));
        if(failure!=null)Debug.LogError(failure);EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
