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
        GameObject ui = new GameObject("Status canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
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
        rect.sizeDelta = new Vector2(640, 340);
        var background = panel.GetComponent<Image>();
        background.color = new Color(0, 0, 0, .88f); background.raycastTarget = false;
        var textObject = new GameObject("Status text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panel.transform, false);
        rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12, 10); rect.offsetMax = new Vector2(-12, -10);
        label = textObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 16; label.color = Color.white; label.supportRichText = false;
        label.raycastTarget = false;
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
        nextRefresh = Time.realtimeSinceStartupAsDouble + .2;
        label.text = BuildText();
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(640, Mathf.Clamp(label.preferredHeight + 24, 160, 700));
    }

    public string BuildText()
    {
        var main = MainController.Instance;
        var text = new StringBuilder("EXPERIMENT STATUS   [Tab: show/hide]\n");
        text.Append(SceneManager.GetActiveScene().name);
        if (main != null)
        {
            text.Append($" | Trial {main.currentTrial + 1} | Scene {main.currentStep + 1}\n");
            text.Append(main.SequenceError != null ? "ERROR: " + main.SequenceError :
                main.AssessmentFailed ? "ERROR: heading measurement failed" : !main.SequenceRunning ? "Idle" : $"{main.ExperimentPhase}: {main.RemainingPhaseSeconds:F1}s (cycle {main.RemainingStepSeconds:F1}s)");
            text.AppendLine();
            foreach (var entry in main.SystemConfigs)
            {
                text.Append(entry.Key + ": ");
                if (!main.VRClosedLoops.TryGetValue(entry.Key, out var rig) || rig == null) { text.AppendLine("inactive"); continue; }
                var listener = rig.GetComponent<ZmqListener>();
                text.AppendLine($"{rig.GetCurrentMode()} / {rig.MotionMode} ({rig.InputInterpretation})");
                text.AppendLine($"  Input SUB: {(listener == null ? "missing listener" : listener.Endpoint + " — " + listener.State)}");
                text.AppendLine($"  gain P/O {rig.PositionGain:G4}/{rig.OrientationGain:G4} [{(rig.PositionTrackingEnabled ? "on" : "off")}/{(rig.OrientationTrackingEnabled ? "on" : "off")}]  DC {rig.GetYawDCOffset():F4} rad");
                var reference = rig.GetComponent<StimulusHeadingReference>();
                if (reference != null && reference.IsObserving) text.AppendLine($"  heading assessment: {reference.RemainingSeconds:F1}s");
                else if (reference != null && reference.Result != null) text.AppendLine($"  zero {reference.Result.meanHeadingDegrees:F1} deg, r={reference.Result.resultantLength:F3} ({reference.Phase})");
            }
        }
        text.AppendLine($"Telemetry PUB (output): {(telemetry != null ? telemetry.State + " " + telemetry.Endpoint : "starting")}");
        ErrorEntry[] recent = RecentErrors();
        if (recent.Length > 0)
        {
            ErrorEntry latest = recent[recent.Length - 1];
            text.AppendLine($"{latest.type} {latest.timestampUtc} ({recent.Length} recent)");
            text.Append(Shorten(latest.message, 420));
            text.Append("\nFull details: runtime_trace.log / telemetry errors");
        }
        return text.ToString();
    }

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
