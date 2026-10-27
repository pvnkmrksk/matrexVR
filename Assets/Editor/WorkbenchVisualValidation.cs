#if UNITY_EDITOR
using System;
using System.Collections;
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
using Object = UnityEngine.Object;

/// <summary>Capture the real UI Toolkit renderer and check display aspect, control density and typography.</summary>
[InitializeOnLoad]
public static class WorkbenchVisualValidation
{
    private const string Key = "WorkbenchVisualValidation.Active";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> checks = new List<string>(), errors = new List<string>();
    private static readonly List<object> geometry = new List<object>();
    private static int stage, nextFrame;
    private static double deadline,hoverDeadline;
    private static RenderTexture target;
    private static ConfigDocument experiment;
    private static string terrainBaseBeforeOverride,terrainSequenceBeforeOverride;
    private static string Output => Path.Combine(Application.dataPath, "../workbench-visual-validation");
    static WorkbenchVisualValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 240;
            Application.logMessageReceived += Log;
            SceneManager.sceneLoaded += Loaded;
            EditorApplication.update += Tick;
        };
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY")))
            throw new Exception("Use a marked disposable project.");
        Directory.CreateDirectory(Output);File.Delete(Path.Combine(Output,"results.json"));
        ConfigurationSchemaBuilder.Generate();
        string profile = DesktopQuickStart.EnsureProfile(Application.streamingAssetsPath, "flying", "night-sky", true);
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "system_config.json"), File.ReadAllText(Path.Combine(profile, "system.json")));
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json"), File.ReadAllText(Path.Combine(profile, "sequence.json")));
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "visual-motion.example.json"),
            "{\"closedLoopOrientation\":true,\"closedLoopPosition\":false,\"autopilotEnabled\":true,\"autopilotSpeed\":7,\"walkingPositionGain\":0.01,\"windSpeed\":0,\"windDirection\":0,\"resetPositionOnStart\":false,\"resetRotationOnStart\":true,\"randomInitialRotation\":true}");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");
        EditorApplication.isPlaying = true;
    }
    private static void Loaded(Scene scene, LoadSceneMode mode)
    { foreach (var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) listener.enabled = false; }
    private static void Log(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (message == "No backup data file found." || stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        errors.Add(message);
    }
    private static void Check(bool ok, string message)
    { if (!ok) throw new Exception(message); checks.Add(message); }
    private static void Call(ConfigWorkbench ui, string name, params object[] args)
    { typeof(ConfigWorkbench).GetMethod(name, Hidden).Invoke(ui, args); }
    private static void Next(int value, int wait = 12) { stage = value; nextFrame = Time.frameCount + wait; }
    private static void Submit(Button button)
    {
        Check(button != null, "Requested sequence action exists");
        using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
    }
    private static void SequenceChoices(ConfigWorkbench ui)
    {
        var original = ui.Workspace.Sequence;
        string systemPath = ui.Workspace.System.Path, systemJson = ui.Workspace.System.Root.ToString();
        original.Edit(json => json["_visualDraftProbe"] = true);
        string[] references = {
            "Examples/Rugosity/sequence-treatment-matched-terrain.json",
            "Examples/Rugosity/sequence-treatment-visual-terrain_navigation-flat.json",
            "Examples/Rugosity/sequence-treatment-visual-flat_navigation-terrain.json",
            "Examples/Rugosity/sequence-treatment-matched-flat.json",
            "Examples/Sequences/swarm-entrainment-social.sequence.json",
            "Examples/Sequences/social-coupling-control.sequence.json"
        };
        int[] counts = { 4, 4, 4, 4, 2, 1 };
        for (int i = 0; i < references.Length; i++)
        {
            var chooser = ui.Document.rootVisualElement.Q<DropdownField>("sequence-starter-choice");
            Check(chooser.choices.Count == 6, "Six prepared sequence options are available");
            chooser.value = chooser.choices[i];
            Check(ui.Document.rootVisualElement.Q<Label>("sequence-starter-timing").text.Contains("block"), "Sequence shows its current duration and block count");
            Submit(ui.Document.rootVisualElement.Q<Button>("sequence-starter-load"));
            Check(ui.Workspace.Sequence.Path == Path.GetFullPath(ExperimentConfigFiles.Resolve(references[i])), "Prepared sequence loads: " + references[i]);
            Check(((JArray)ui.Workspace.Sequence.Root["sequences"]).Count == counts[i], "Loaded sequence has expected block count");
            Check(ui.Workspace.System.Path == systemPath && ui.Workspace.System.Root.ToString() == systemJson, "Loading retains the exact apparatus");
            Check(ui.Workspace.Documents.Contains(original) && original.Dirty && (bool)original.Root["_visualDraftProbe"], "Other sequence draft remains available");
            Check(!MainController.Instance.SequenceRunning, "Loading never starts an experiment");
            if (i + 1 < references.Length) Call(ui, "ShowSequenceLibrary");
        }
        original.Undo(); ui.Workspace.SelectPrimary("sequence", original.Path); Call(ui, "ShowSessionHome");
    }
    private static void Render(ConfigWorkbench ui, int width, int height, float scale)
    {
        if (target != null) { ui.Document.panelSettings.targetTexture = null; target.Release(); Object.Destroy(target); }
        target = new RenderTexture(width, height, 24); target.Create();
        ui.Document.panelSettings.scale = scale; ui.Document.panelSettings.targetTexture = target;
    }
    private static void Capture(string name)
    {
        var previous = RenderTexture.active; RenderTexture.active = target;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(Path.Combine(Output, name + ".png"), pixels.EncodeToPNG()); Object.Destroy(pixels);
    }
    private static void Shell(ConfigWorkbench ui)
    {
        var root = ui.Document.rootVisualElement;
        float width = root.resolvedStyle.width, height = root.resolvedStyle.height;
        Check(width >= 1900 && height >= 1060, "Full HD logical workspace retained");
        foreach (var bar in new[] { "header", "footer" })
        {
            var element = root.Q<VisualElement>(className: bar);
            Check(element.worldBound.height <= 65, bar + " uses at most 64 logical pixels plus rounding");
            foreach (var button in element.Query<Button>().ToList())
                Check(button.worldBound.xMin >= -1 && button.worldBound.xMax <= width + 1,
                    bar + " action remains inside the screen: " + button.name);
        }
        Check(root.Q<VisualElement>("setup-navigation").worldBound.width <= 210, "Navigation leaves the workspace at least 1710 Full HD pixels");
        Check(root.Q<Button>("start-experiment").worldBound.height <= 40, "Primary action is a compact desktop control");
        foreach (var label in root.Query<Label>().ToList().Where(x => x.ClassListContains("title") || x.ClassListContains("start-title")))
            Check(Mathf.Abs(label.resolvedStyle.letterSpacing) < .01f, "Heading uses natural font spacing");
    }
    private static void SquarePreviews(ConfigWorkbench ui)
    {
        var root = ui.Document.rootVisualElement;
        var cells = root.Query<Label>(className: "apparatus-pixel-cell").ToList();
        Check(cells.Count == 6, "Display map contains all six configured 128 × 128 faces");
        foreach (var cell in cells)
        {
            var bounds = cell.worldBound;
            Check(bounds.width > 0 && Mathf.Abs(bounds.width - bounds.height) < .2f,
                "128 × 128 output pixels remain square: " + cell.name);
            geometry.Add(new { control = cell.name, width = bounds.width, height = bounds.height, targetWidth = target.width, targetHeight = target.height });
        }
        foreach (var face in root.Query<Button>(className: "apparatus-face").ToList())
            Check(Mathf.Abs(face.worldBound.width - face.worldBound.height) < .2f, "Cube face is square: " + face.name);
        foreach (var tile in root.Query<VisualElement>(className: "apparatus-strip-tile").ToList())
            Check(Mathf.Abs(tile.worldBound.width - tile.worldBound.height) < .2f, "Output-order face is square");
    }
    private static void Typography()
    {
        var font = (UnityEngine.TextCore.Text.FontAsset)typeof(ConfigWorkbench).Assembly.GetType("ConfigurationUI.WorkbenchTypography").GetField("regular", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        Check(font != null && font.getFontFeatures, "Inter retains dynamic OpenType font features");
        Check(font.regularStyleSpacing == 0 && font.boldStyleSpacing == 0, "Font weight does not inject synthetic letter spacing");
        // Public fontFeatureTable contains an internal list in Unity 6. Read it
        // only in this diagnostic; the app uses the supported FontAsset API.
        var pairs = font.fontFeatureTable.GetType().GetField("m_GlyphPairAdjustmentRecords", Hidden)?.GetValue(font.fontFeatureTable) as ICollection;
        Check(pairs != null && pairs.Count > 0, "Inter has imported pair-positioning records for kerning");
    }
    private static void ShowDisplay(ConfigWorkbench ui)
    {
        var system = (JObject)ui.Workspace.System.Root.DeepClone();
        DesktopApparatusProfile.Begin(system, "led", true); DesktopApparatusProfile.ResizeRigs(system, 1);
        var rig = (JObject)system["configs"][0]; rig["displayOrder"] = "DRBLFU";
        rig["ledPanelWidth"] = 128; rig["ledPanelHeight"] = 128;
        rig["startRow"] = 0; rig["startCol"] = 0; rig["horizontal"] = true;
        rig["targetDisplay"] = 1; DesktopApparatusProfile.Marker(system)["monitorCount"] = 2;
        ui.Workspace.System.ApplyJson(system.ToString()); Call(ui, "Select", ui.Workspace.System);
    }
    private static void EditPage(ConfigWorkbench ui,string reference,string schema,string tab)
    {
        experiment=ui.Workspace.Open(reference,schema);Call(ui,"Select",experiment);
        typeof(ConfigWorkbench).GetField("settingsTab",Hidden).SetValue(ui,tab);Call(ui,"BuildContent");
    }
    private static void FieldBounds(ConfigWorkbench ui)
    {
        foreach(var card in ui.Document.rootVisualElement.Query<VisualElement>(className:"studio-setting").ToList())
        {
            if(card.resolvedStyle.display==DisplayStyle.None||card.worldBound.width<=0)continue;
            var title=card.Q<Label>(className:"setting-title");
            if(title!=null&&title.text.Length>=8)Check(title.worldBound.width>=48,"Field label retains readable width: "+card.name);
            foreach(var field in card.Children().OfType<TextField>())
                Check(field.worldBound.xMax<=card.worldBound.xMax+1,"Text field stays inside its card: "+card.name);
            if(card.ClassListContains("vector-setting"))
            {
                var controls=card.Q<VisualElement>(className:"vector-controls");
                var axes=controls.Children().OfType<Label>().ToList();
                Check(axes.Count==3&&Mathf.Abs(axes[0].worldBound.y-axes[2].worldBound.y)<1,"Three vector axes share one line: "+card.name);
            }
        }
    }
    private static void Hover(VisualElement target)
    {
        Check(target!=null,"Hover target exists");hoverDeadline=EditorApplication.timeSinceStartup+.9;
        using(var evt=PointerOverEvent.GetPooled(new Event {type=EventType.MouseMove,mousePosition=target.worldBound.center})){evt.target=target;target.SendEvent(evt);}
        using(var evt=PointerEnterEvent.GetPooled(new Event {type=EventType.MouseMove,mousePosition=target.worldBound.center})){evt.target=target;target.SendEvent(evt);}
        using(var evt=PointerMoveEvent.GetPooled(new Event {type=EventType.MouseMove,mousePosition=target.worldBound.center})){evt.target=target;target.SendEvent(evt);}
    }
    private static double Light(Color color)
    {
        double Channel(float value)=>value<=.04045?value/12.92:Math.Pow((value+.055)/1.055,2.4);
        return .2126*Channel(color.r)+.7152*Channel(color.g)+.0722*Channel(color.b);
    }
    private static void Contrast(Color text,Color background,string label)
    {double a=Light(text),b=Light(background);Check((Math.Max(a,b)+.05)/(Math.Min(a,b)+.05)>=4.5,label+" has at least 4.5:1 text contrast");}
    private static void HoverCard(ConfigWorkbench ui)
    {
        var root=ui.Document.rootVisualElement;var card=root.Q<VisualElement>("runtime-hover-help");
        Check(card.resolvedStyle.display==DisplayStyle.Flex,"Pointer hover displays help");
        Check(card.worldBound.xMin>=0&&card.worldBound.yMin>=0&&card.worldBound.xMax<=root.worldBound.xMax&&card.worldBound.yMax<=root.worldBound.yMax,"Help stays inside the workspace");
        foreach(string name in new[]{"runtime-hover-help-title","runtime-hover-help-body","runtime-hover-help-facts"})
        {
            var label=root.Q<Label>(name);if(label.resolvedStyle.display==DisplayStyle.None)continue;
            Contrast(label.resolvedStyle.color,card.resolvedStyle.backgroundColor,name);
            var measure=label.MeasureTextSize(label.text,label.contentRect.width,VisualElement.MeasureMode.Exactly,0,VisualElement.MeasureMode.Undefined);
            Check(label.contentRect.height+.5f>=measure.y,"Help text is not clipped: "+name);
        }
    }
    private static void MakeLibraryFixtures()
    {
        string folder=Path.Combine(Output,"recordings");Directory.CreateDirectory(folder);
        string[] titles={"Terrain · matched relief","Swarm entrainment · animal 04","Night sky · calibration"};
        for(int i=0;i<titles.Length;i++)
        {
            string run=Path.Combine(folder,"20261027_10000"+i);Directory.CreateDirectory(run);
            File.WriteAllText(Path.Combine(run,"system_config.json"),"{\"configs\":[{\"vrId\":\"VR1\",\"closedLoopMode\":\"Rekkefly\"}]}");
            File.WriteAllText(Path.Combine(run,"sequenceConfig.json"),"{\"sequences\":[{\"sceneName\":\"Choice\",\"duration\":120,\"parameters\":{\"configFile\":\"choice.json\"}}]}");
            File.WriteAllText(Path.Combine(run,"choice.json"),"{\"objects\":[]}");
            File.WriteAllText(Path.Combine(run,"AnimalMetadata.json"),new JObject{["ExperimentName"]=titles[i],["Animals"]=new JArray(new JObject{["VR"]="VR1",["AnimalID"]="Animal 04"})}.ToString());
            File.WriteAllText(Path.Combine(run,"poses.csv"),"Current Time,VR,Scene,CurrentStep,ConfigFile,CurrentTrial,GameObjectPosX,GameObjectPosY,GameObjectPosZ,GameObjectRotX,GameObjectRotY,GameObjectRotZ\n2026-10-27 10:00:00.000,VR1,Choice,0,choice.json,0,1,2,3,0,40,0\n2026-10-27 10:00:01.000,VR1,Choice,0,choice.json,0,2,2,3,0,42,0\n");
        }
    }
    private static void TerrainSceneLabels(ConfigWorkbench ui)
    {
        var root=ui.Document.rootVisualElement;
        var names=root.Query<Label>(className:"block-name").ToList();
        Check(names.Count==4&&names.Select(label=>label.text).Distinct().Count()==4,"Terrain rows have four distinct spatial-pattern titles");
        var descriptions=root.Query<Label>(className:"block-description").ToList();
        Check(descriptions.Select(label=>label.text).Distinct().Count()==4,"Terrain row summaries reflect each row's surface override");
        foreach(var label in root.Query<Label>().ToList().Where(label=>label.tooltip!=null&&label.tooltip.StartsWith("Unity scene: TerrainChoice_",StringComparison.Ordinal)))
        {
            var measured=label.MeasureTextSize(label.text,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined);
            geometry.Add(new {control="terrain-scene-label",text=label.text,scene=label.tooltip.Substring("Unity scene: ".Length),width=label.contentRect.width,textWidth=measured.x,
                clipped=measured.x>label.contentRect.width+1,targetWidth=target.width,targetHeight=target.height});
            Check(measured.x<=label.contentRect.width+1,"Terrain scene caption is fully readable: "+label.text);
        }
        var dropdown=root.Q<DropdownField>();
        Check(dropdown!=null&&dropdown.value.StartsWith("TerrainChoice_",StringComparison.Ordinal),"Friendly scene dropdown retains the actual Unity scene value");
    }
    private static void Tick()
    {
        if (Time.frameCount < nextFrame) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Visual validation timeout at " + stage);
            var ui = ConfigWorkbench.Instance; if (ui?.Document == null) return;
            switch (stage)
            {
                case 0: Render(ui, 1920, 1080, 1); Next(1, 60); break;
                case 1:
                    Shell(ui); Typography(); Capture("home-1920x1080");
                    File.WriteAllLines(Path.Combine(Output,"foldout-visual-tree.txt"),ui.Document.rootVisualElement.Q<Foldout>("navigation-files").Query<VisualElement>().ToList().Select(e=>e.GetType().Name+" / "+e.name+" / "+string.Join(" ",e.GetClasses())+" / bg="+e.resolvedStyle.backgroundColor+" border="+e.resolvedStyle.borderTopWidth));
                    Submit(ui.Document.rootVisualElement.Q<Button>("session-load-sequence")); Next(7); break;
                case 7:
                    Capture("sequence-chooser-1920x1080"); SequenceChoices(ui); ShowDisplay(ui); Next(2); break;
                case 2:
                    Shell(ui); SquarePreviews(ui); Capture("apparatus-1920x1080");
                    ui.Document.rootVisualElement.Q<ScrollView>("apparatus-scroll").ScrollTo(ui.Document.rootVisualElement.Q<VisualElement>("apparatus-net-VR1"));Next(28);break;
                case 28:
                    Capture("apparatus-face-layout-1920x1080");
                    experiment = ui.Workspace.Open("visual-motion.example.json", "choice");
                    Call(ui, "Select", experiment); typeof(ConfigWorkbench).GetField("settingsTab", Hidden).SetValue(ui, "Motion");
                    Call(ui, "BuildContent"); Next(3); break;
                case 3:
                    Shell(ui);
                    foreach (var toggle in ui.Document.rootVisualElement.Query<VisualElement>(className: "boolean-setting").ToList())
                        Check(toggle.worldBound.width <= 330, "Boolean control occupies less than one fifth of Full HD: " + toggle.name);
                    FieldBounds(ui);Capture("motion-1920x1080"); Render(ui, 3840, 2160, 2); Next(4); break;
                case 4:
                    Shell(ui); Capture("motion-3840x2160"); ShowDisplay(ui); Next(5); break;
                case 5:
                    SquarePreviews(ui); Capture("apparatus-3840x2160"); Render(ui, 2560, 1440, 1.25f); Next(6); break;
                case 6:
                    Shell(ui); SquarePreviews(ui); Capture("apparatus-2560x1440");
                    Render(ui,1920,1080,1);var system=(JObject)ui.Workspace.System.Root.DeepClone();DesktopApparatusProfile.Complete(system);ui.Workspace.System.ApplyJson(system.ToString());Call(ui,"ShowSessionHome");Next(8);break;
                case 8:
                    Capture("configured-home-1920x1080");ui.Document.rootVisualElement.Q<Foldout>("recording-details").value=true;Next(9);break;
                case 9:
                    Capture("recording-options-1920x1080");ui.Document.rootVisualElement.Q<ScrollView>("start-workspace").ScrollTo(ui.Document.rootVisualElement.Q<VisualElement>("subject-VR1"));Next(29);break;
                case 29:
                    Capture("recording-details-scrolled-1920x1080");ui.Workspace.SelectPrimary("sequence","Examples/Sequences/swarm-entrainment-social.sequence.json");Call(ui,"Select",ui.Workspace.Sequence);Next(10);break;
                case 10:
                    Capture("sequence-1920x1080");Call(ui,"ShowBlockLibrary",(object)null);Next(11);break;
                case 11:
                    Capture("block-library-dialog-1920x1080");Call(ui,"CloseModal");EditPage(ui,"Examples/Swarm/ordered-entrainment.example.json","swarm","Stimulus");Next(12);break;
                case 12:
                    FieldBounds(ui);Capture("stimulus-1920x1080");EditPage(ui,"Examples/Flight/selwyn-live-sky.example.json","choice","Sky");Next(13);break;
                case 13:
                    FieldBounds(ui);Capture("sky-1920x1080");ui.Document.rootVisualElement.Q<VisualElement>("setting-nightSky").Q<Foldout>().value=true;Next(27);break;
                case 27:
                    FieldBounds(ui);Capture("sky-expanded-1920x1080");Call(ui,"ShowRuntimeColorDialog",new Color(.25f,.45f,.65f),false,new Action<Color>(_=>{}));Next(14);break;
                case 14:
                    Capture("color-dialog-1920x1080");Call(ui,"CloseModal");Call(ui,"ShowEffectiveConfiguration");Next(15);break;
                case 15:
                    Capture("effective-json-dialog-1920x1080");Call(ui,"CloseModal");EditPage(ui,"visual-motion.example.json","choice","All parameters");Next(16);break;
                case 16:
                    Capture("all-parameters-1920x1080");typeof(ConfigWorkbench).GetField("jsonMode",Hidden).SetValue(ui,true);Call(ui,"BuildContent");Next(17);break;
                case 17:
                    Capture("raw-json-1920x1080");EditPage(ui,"visual-motion.example.json","choice","Motion");Next(18);break;
                case 18:
                    ui.Document.rootVisualElement.Q<TextField>("number-autopilotSpeed").Focus();Next(19,1);break;
                case 19:
                    Capture("field-keyboard-focus-1920x1080");Hover(ui.Document.rootVisualElement.Q<VisualElement>("setting-closedLoopOrientation"));Next(20,60);break;
                case 20:
                    if(EditorApplication.timeSinceStartup<hoverDeadline)return;
                    HoverCard(ui);Capture("parameter-hover-1920x1080");Call(ui,"HideHoverHelp");Hover(ui.Document.rootVisualElement.Q<Button>("start-experiment"));Next(21,60);break;
                case 21:
                    if(EditorApplication.timeSinceStartup<hoverDeadline)return;
                    var primary=ui.Document.rootVisualElement.Q<Button>("start-experiment");Contrast(primary.Q<Label>().resolvedStyle.color,primary.resolvedStyle.backgroundColor,"Primary hover label");Capture("primary-button-hover-1920x1080");
                    Call(ui,"HideHoverHelp");primary.SetEnabled(false);Next(22);break;
                case 22:
                    var disabled=ui.Document.rootVisualElement.Q<Button>("start-experiment");Contrast(disabled.Q<Label>().resolvedStyle.color,disabled.resolvedStyle.backgroundColor,"Disabled primary hover caption");Capture("disabled-controls-1920x1080");Call(ui,"ShowOpenFile");Next(23);break;
                case 23:
                    Capture("open-file-dialog-1920x1080");Call(ui,"CloseModal");Call(ui,"ShowKeyboardHelp");Next(24);break;
                case 24:
                    Capture("keyboard-help-dialog-1920x1080");Call(ui,"CloseModal");MakeLibraryFixtures();Call(ui,"ShowRecordingLibrary");
                    typeof(ConfigWorkbench).GetField("libraryRoot",Hidden).SetValue(ui,Path.Combine(Output,"recordings"));Call(ui,"BuildContent");Call(ui,"StartLibraryScan");Next(25,80);break;
                case 25:
                    var list=ui.Document.rootVisualElement.Q<ListView>("recording-library-list");Check(list.itemsSource.Count==3,"Recording library contains three synthetic run examples");list.SetSelection(0);Next(26);break;
                case 26:
                    Capture("recording-library-1920x1080");
                    ui.Workspace.SelectPrimary("sequence","Examples/Rugosity/sequence-treatment-matched-terrain.json");Call(ui,"Select",ui.Workspace.Sequence);Next(30);break;
                case 30:
                    Check(((JArray)ui.Workspace.Sequence.Root["sequences"]).Count==4,"Loaded terrain sequence exposes four actual choice scenes");
                    Check(((JArray)ui.Workspace.Sequence.Root["sequences"]).OfType<JObject>().All(row=>((string)row["sceneName"]).StartsWith("TerrainChoice_",StringComparison.Ordinal)),"Terrain capture uses the authored TerrainChoice scene family");
                    Shell(ui);Capture("terrain-sequence-1920x1080");TerrainSceneLabels(ui);Render(ui,3840,2160,2);Next(31);break;
                case 31:
                    Shell(ui);Capture("terrain-sequence-3840x2160");TerrainSceneLabels(ui);
                    Render(ui,1920,1080,1);EditPage(ui,"Examples/Rugosity/terrain-treatment.json","choice","Stimulus");
                    var terrain=ui.Document.rootVisualElement.Q<VisualElement>("setting-rugosity");Check(terrain!=null,"Shared terrain configuration exposes the rugosity block");
                    terrain.Q<Foldout>().value=true;Next(32);break;
                case 32:
                    FieldBounds(ui);
                    foreach(string role in new[]{"visualSurface","collisionSurface","heightSurface","normalSurface"})
                        Check(ui.Document.rootVisualElement.Q<VisualElement>("setting-rugosity-"+role)!=null,"Terrain treatment role is visible: "+role);
                    Capture("terrain-settings-1920x1080");Render(ui,3840,2160,2);Next(33);break;
                case 33:
                    FieldBounds(ui);Capture("terrain-settings-3840x2160");
                    Render(ui,1920,1080,1);ui.Workspace.SelectPrimary("sequence","Examples/Rugosity/sequence-treatment-visual-flat_navigation-terrain.json");Call(ui,"Select",ui.Workspace.Sequence);Next(34);break;
                case 34:
                    TerrainSceneLabels(ui);Capture("terrain-navigation-only-sequence-1920x1080");
                    terrainSequenceBeforeOverride=ui.Workspace.Sequence.Root.ToString();
                    experiment=ui.Workspace.Open("Examples/Rugosity/terrain-treatment.json","choice");terrainBaseBeforeOverride=experiment.Root.ToString();
                    Call(ui,"EditBlock",1);typeof(ConfigWorkbench).GetField("settingsTab",Hidden).SetValue(ui,"Stimulus");Call(ui,"BuildContent");
                    ui.Document.rootVisualElement.Q<VisualElement>("setting-rugosity").Q<Foldout>().value=true;Next(35);break;
                case 35:
                    var notice=ui.Document.rootVisualElement.Q<VisualElement>("sequence-terrain-overrides");
                    Check(notice!=null&&notice.worldBound.height>0,"Base terrain editor visibly distinguishes selected sequence overrides");
                    Check(notice.Q<Label>(className:"sequence-override-title").text.Contains("02"),"Override notice identifies selected second block");
                    Check(notice.Q<Label>("sequence-override-visual").text=="Visual: Flat","Navigation-only treatment visibly reports its effective flat visual surface");
                    Check(notice.Q<Label>("sequence-override-navigation").text=="Navigation: smooth left / rough right","Navigation-only treatment visibly reports selected block's effective collision surface");
                    Check(experiment.Root.ToString()==terrainBaseBeforeOverride,"Viewing sequence overrides never rewrites shared base configuration");
                    Check((string)ExperimentBlocks.Payload(experiment.Root as JObject)["rugosity"]["visualSurface"]=="RoughSmooth","Shared base controls retain their actual source value");
                    FieldBounds(ui);Capture("terrain-navigation-only-overrides-1920x1080");Submit(notice.Q<Button>("edit-sequence-terrain-overrides"));Next(36);break;
                case 36:
                    var overridePath=(List<string>)typeof(ConfigWorkbench).GetField("path",Hidden).GetValue(ui);
                    Check(string.Join("/",overridePath)=="sequences/1/parameters/rugosity","Override action opens the selected row's exact JSON object");
                    Check(ui.Workspace.Sequence.Root.ToString()==terrainSequenceBeforeOverride,"Opening override editor preserves exact sequence JSON");
                    Check(experiment.Root.ToString()==terrainBaseBeforeOverride,"Opening override editor preserves exact shared base JSON");
                    Capture("terrain-overrides-editor-1920x1080");
                    Check(!MainController.Instance.SequenceRunning,"Visual checks never start acquisition or experiment recording");Finish(null);break;
            }
        }
        catch (Exception error) { Finish(error.ToString()); }
    }
    private static void Finish(string failure)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log; SceneManager.sceneLoaded -= Loaded;
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "results.json"), JsonConvert.SerializeObject(new {
            checks = checks.Count, failure, errors, geometry, passed = checks, unity = Application.unityVersion,
            scope = "Native UI Toolkit offscreen rendering at Full HD, 4K at 2× scale and 2560×1440 at 1.25× scale. Synthetic configs; no trackers or experiment started."
        }, Formatting.Indented));
        EditorApplication.Exit(failure == null && errors.Count == 0 ? 0 : 1);
    }
}
#endif
