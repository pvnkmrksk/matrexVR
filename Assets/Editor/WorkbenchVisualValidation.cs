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
    private static double deadline;
    private static RenderTexture target;
    private static ConfigDocument experiment;
    private static string Output => Path.Combine(Application.dataPath, "../workbench-visual-validation");
    static WorkbenchVisualValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            Application.logMessageReceived += Log;
            SceneManager.sceneLoaded += Loaded;
            EditorApplication.update += Tick;
        };
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY")))
            throw new Exception("Use a marked disposable project.");
        Directory.CreateDirectory(Output);
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
        ui.Workspace.System.ApplyJson(system.ToString()); Call(ui, "Select", ui.Workspace.System);
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
                    Submit(ui.Document.rootVisualElement.Q<Button>("session-load-sequence")); Next(7); break;
                case 7:
                    Capture("sequence-chooser-1920x1080"); SequenceChoices(ui); ShowDisplay(ui); Next(2); break;
                case 2:
                    Shell(ui); SquarePreviews(ui); Capture("apparatus-1920x1080");
                    experiment = ui.Workspace.Open("visual-motion.example.json", "choice");
                    Call(ui, "Select", experiment); typeof(ConfigWorkbench).GetField("settingsTab", Hidden).SetValue(ui, "Motion");
                    Call(ui, "BuildContent"); Next(3); break;
                case 3:
                    Shell(ui);
                    foreach (var toggle in ui.Document.rootVisualElement.Query<VisualElement>(className: "boolean-setting").ToList())
                        Check(toggle.worldBound.width <= 272, "Boolean control stays compact: " + toggle.name);
                    Capture("motion-1920x1080"); Render(ui, 3840, 2160, 2); Next(4); break;
                case 4:
                    Shell(ui); Capture("motion-3840x2160"); ShowDisplay(ui); Next(5); break;
                case 5:
                    SquarePreviews(ui); Capture("apparatus-3840x2160"); Render(ui, 2560, 1440, 1.25f); Next(6); break;
                case 6:
                    Shell(ui); SquarePreviews(ui); Capture("apparatus-2560x1440");
                    Check(!MainController.Instance.SequenceRunning, "Visual checks never start acquisition or experiment recording");
                    Finish(null); break;
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
