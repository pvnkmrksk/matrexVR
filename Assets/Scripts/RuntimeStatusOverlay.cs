using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public sealed class StatusOverlayConfig
{
    public bool visible = true;
    public int targetDisplay = 0;
}

/// <summary>Persistent operator HUD and bounded, thread-safe player error capture.</summary>
public sealed class RuntimeStatusOverlay : MonoBehaviour
{
    [Serializable]
    public sealed class ErrorEntry { public string timestampUtc, type, message, stackTrace; }
    private static readonly object errorLock = new object();
    private static readonly Queue<ErrorEntry> errors = new Queue<ErrorEntry>();
    private static long errorRevision;
    private long shownRevision;
    private GameObject panel;
    private Canvas canvas;
    private Text label;
    private Button autoTrimButton;
    private Text autoTrimButtonText;
    private double nextRefresh;
    private bool visible = true;
    private ExperimentTelemetry telemetry;
    public bool Visible => visible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetErrors()
    {
        lock (errorLock) { errors.Clear(); errorRevision = 0; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        var root = new GameObject("Experiment diagnostics");
        DontDestroyOnLoad(root);
        root.AddComponent<RuntimeStatusOverlay>();
        root.AddComponent<ExperimentTelemetry>();
    }

    private void Awake()
    {
        Application.logMessageReceivedThreaded += CaptureError;
        MainController.SystemConfigurationChanged += Configure;
    }

    private void Start()
    {
        telemetry = GetComponent<ExperimentTelemetry>();
        GameObject ui = new GameObject("Status canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        ui.transform.SetParent(transform, false);
        canvas = ui.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3000;
        var scaler = ui.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;
        panel = new GameObject("Status panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(ui.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-12, 12);
        rect.sizeDelta = new Vector2(720, 340);
        var background = panel.GetComponent<Image>();
        background.color = new Color(0, 0, 0, .88f); background.raycastTarget = false;
        var textObject = new GameObject("Status text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panel.transform, false);
        rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12, 48); rect.offsetMax = new Vector2(-12, -10);
        label = textObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 16; label.color = Color.white; label.supportRichText = true;
        label.raycastTarget = false;
        var buttonObject = new GameObject("Auto trim toggle", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panel.transform, false);
        rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-12, 10); rect.sizeDelta = new Vector2(210, 28);
        autoTrimButton = buttonObject.GetComponent<Button>();
        autoTrimButton.targetGraphic = buttonObject.GetComponent<Image>();
        autoTrimButton.onClick.AddListener(() => {
            var main = MainController.Instance;
            if (main != null) main.SetCurrentAutoTrim(!main.CurrentAutoTrimEnabled);
            nextRefresh = 0;
        });
        var buttonLabel = new GameObject("Label", typeof(RectTransform), typeof(Text));
        buttonLabel.transform.SetParent(buttonObject.transform, false);
        rect = buttonLabel.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        autoTrimButtonText = buttonLabel.GetComponent<Text>(); autoTrimButtonText.font = label.font;
        autoTrimButtonText.fontSize = 15; autoTrimButtonText.alignment = TextAnchor.MiddleCenter;
        autoTrimButtonText.color = Color.black; autoTrimButtonText.raycastTarget = false;
        Configure();
    }

    private void Configure()
    {
        if (canvas == null) return;
        var settings = MainController.Instance != null ? MainController.Instance.StatusOverlaySettings : new StatusOverlayConfig();
        canvas.targetDisplay = settings.targetDisplay >= 0 && settings.targetDisplay < Display.displays.Length ? settings.targetDisplay : 0;
        if (canvas.targetDisplay > 0) Display.displays[canvas.targetDisplay].Activate();
        SetVisible(settings.visible);
    }

    public void SetVisible(bool value) { visible = value; if (panel != null) panel.SetActive(visible); }

    private void Update()
    {
        long revision;
        lock (errorLock) revision = errorRevision;
        if (revision != shownRevision) { shownRevision = revision; SetVisible(true); nextRefresh = 0; }
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (!ExperimentInput.IsEditingText && keyboard != null && keyboard.tabKey.wasPressedThisFrame) SetVisible(!visible);
        if (!visible || label == null || Time.realtimeSinceStartupAsDouble < nextRefresh) return;
        nextRefresh = Time.realtimeSinceStartupAsDouble + .5;
        if (UnityEngine.EventSystems.EventSystem.current == null)
            new GameObject("Dashboard EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        var main = MainController.Instance;
        bool available = false;
        if (main != null && main.SequenceRunning)
            foreach (var rig in main.VRClosedLoops.Values) if (rig != null && rig.SupportsAutoTrim) { available = true; break; }
        autoTrimButton.interactable = available;
        bool enabled = main != null && main.CurrentAutoTrimEnabled;
        autoTrimButtonText.text = enabled ? "AUTO TRIM ON  /  stop" : "AUTO TRIM OFF  /  start";
        autoTrimButton.targetGraphic.color = !available ? Color.gray : enabled ? new Color(.45f, .9f, .75f) : new Color(.7f, .75f, .8f);
        label.text = BuildText();
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(720, Mathf.Clamp(label.preferredHeight + 62, 180, 820));
    }

    public string BuildText()
    {
        var main = MainController.Instance;
        var text = new StringBuilder("<size=19><b>FLIGHT DASHBOARD</b></size>   <color=#9DA8B5>[Tab]  [1-4] select  [ / ] DC</color>\n");
        text.Append(SceneManager.GetActiveScene().name);
        if (main != null)
        {
            text.Append($" | Trial {main.currentTrial + 1} | Scene {main.currentStep + 1}\n");
            text.Append(main.SequenceError != null ? "ERROR: " + main.SequenceError :
                main.AssessmentFailed ? "ERROR: heading measurement failed" : !main.SequenceRunning ? "Idle" : $"{main.ExperimentPhase}: {main.RemainingPhaseSeconds:F1}s (cycle {main.RemainingStepSeconds:F1}s)");
            text.AppendLine();
            foreach (var entry in main.SystemConfigs)
            {
                text.Append($"\n<b>{entry.Key}</b> ");
                if (!main.VRClosedLoops.TryGetValue(entry.Key, out var rig) || rig == null) { text.AppendLine("<color=#88929C>INACTIVE</color>"); continue; }
                var listener = rig.GetComponent<ZmqListener>();
                text.Append(Badge(rig.OrientationTrackingEnabled ? "YAW CLOSED" : "YAW OPEN", rig.OrientationTrackingEnabled ? "70E1B0" : "88929C"));
                text.Append(Badge(rig.PositionTrackingEnabled ? "POS CLOSED" : "POS OPEN", rig.PositionTrackingEnabled ? "70E1B0" : "88929C"));
                text.AppendLine($" {rig.GetCurrentMode()} / {rig.MotionMode}" + (entry.Key == "VR" + main.SelectedVRIndex ? " <color=#FFD166>SELECTED</color>" : ""));
                if (rig.SupportsAutoTrim)
                {
                    var trim = rig.AutoTrim;
                    text.AppendLine($"  {Badge($"GAIN {rig.GetYawGain():G4}", rig.OrientationTrackingEnabled && rig.GetYawGain() != 0 ? "FFFFFF" : "88929C")} DC {rig.GetYawDCOffset():F4} rad  " +
                        Badge(trim.Flying ? "FLYING" : trim.FlightReady ? "NOT FLYING" : "FLIGHT ?", trim.Flying ? "70E1B0" : "FFD166") + $" var {trim.FlightVariance:F3}");
                    bool working = trim.Enabled && trim.Flying && rig.OrientationTrackingEnabled && rig.GetYawGain() != 0;
                    string state = !trim.Enabled ? "AUTO TRIM OFF" : working && trim.State != "CENTERED" ? "AUTO TRIMMING / " + trim.State : "AUTO TRIM / " + trim.State;
                    text.Append("  " + Badge(state, !trim.Enabled ? "88929C" : trim.State == "CENTERED" ? "70E1B0" : working ? "FFD166" : "88929C"));
                    if (trim.State == "COLLECTING") text.Append($" {trim.WindowProgress:P0}");
                    if (trim.EffectiveMedianDegPerSecond.HasValue) text.Append($" last median {trim.EffectiveMedianDegPerSecond.Value:F2} deg/s");
                    if (trim.Enabled) text.Append($"  pass {trim.Passes}");
                    text.AppendLine();
                }
                else text.AppendLine($"  {Badge($"P GAIN {rig.PositionGain:G4}", rig.PositionTrackingEnabled ? "FFFFFF" : "88929C")} " +
                    Badge($"YAW GAIN {rig.OrientationGain:G4}", rig.OrientationTrackingEnabled ? "FFFFFF" : "88929C"));
                text.AppendLine($"  <color=#9DA8B5>Input SUB: {(listener == null ? "missing listener" : EscapeRich(listener.Endpoint) + " / " + listener.State)}</color>");
                var reference = rig.GetComponent<StimulusHeadingReference>();
                if (reference != null && reference.IsObserving) text.AppendLine($"  heading assessment: {reference.RemainingSeconds:F1}s");
                else if (reference != null && reference.Result != null) text.AppendLine($"  zero {reference.Result.meanHeadingDegrees:F1} deg, r={reference.Result.resultantLength:F3} ({reference.Phase})");
            }
        }
        text.AppendLine($"Telemetry PUB (output): {(telemetry != null ? telemetry.State + " " + telemetry.Endpoint : "starting")}");
        int pending = 0; long dropped = 0; double capture = 0; int loggers = 0;
        foreach (var logger in FindObjectsByType<SwarmLogger>(FindObjectsSortMode.None))
        { pending += logger.PendingFrames; dropped += logger.DroppedFrames; capture += logger.CaptureMilliseconds; loggers++; }
        if (loggers > 0) text.AppendLine($"Swarm log: worker / capture {capture:F2} ms / queued {pending} / dropped {dropped}");
        ErrorEntry[] recent = RecentErrors();
        if (recent.Length > 0)
        {
            ErrorEntry latest = recent[recent.Length - 1];
            text.AppendLine($"{latest.type} {latest.timestampUtc} ({recent.Length} recent)");
            text.Append(EscapeRich(Shorten(latest.message, 420)));
            text.Append("\nFull details: runtime_trace.log / telemetry errors");
        }
        return text.ToString();
    }

    private static string Badge(string value, string color) => $"<color=#{color}><b>[{value}]</b></color> ";
    private static string EscapeRich(string value) => (value ?? "").Replace("<", "‹").Replace(">", "›");

    private static string Shorten(string value, int length) => value == null ? "" : value.Length <= length ? value : value.Substring(0, length) + "…";
    private static void CaptureError(string condition, string trace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        lock (errorLock)
        {
            while (errors.Count >= 8) errors.Dequeue();
            errors.Enqueue(new ErrorEntry { timestampUtc = DateTime.UtcNow.ToString("O"), type = type.ToString(),
                message = Shorten(condition, 8192), stackTrace = Shorten(trace, 8192) });
            errorRevision++;
        }
    }

    public static ErrorEntry[] RecentErrors() { lock (errorLock) return errors.ToArray(); }
    private void OnDestroy()
    {
        Application.logMessageReceivedThreaded -= CaptureError;
        MainController.SystemConfigurationChanged -= Configure;
    }
}
