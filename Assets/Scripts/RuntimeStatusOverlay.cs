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
    private static RuntimeStatusOverlay instance;
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
    private Button flightCheckButton;
    private Text flightCheckButtonText;
    private double nextRefresh;
    private int layoutLineCount = -1;
    private long layoutErrorRevision = -1;
    private bool visible = true;
    private bool operatorVisibility;
    private ExperimentTelemetry telemetry;
    private UnityEngine.InputSystem.InputAction dashboardToggle;
    private int lastToggleFrame=-1;
    private bool showDetails;
    private bool layoutAvailable;
    private int layoutVersion=-1,lastWidth,lastHeight;
    private Vector2 lastPanelSize;
    public bool Visible => visible&&!OperatorOverlayVisibility.Hidden;
    public bool ShouldDisplay => isActiveAndEnabled && Visible && layoutAvailable && !ConfigurationUI.ConfigWorkbench.IsOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetErrors()
    {
        lock (errorLock) { errors.Clear(); errorRevision = 0; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var root = new GameObject("Experiment diagnostics");
        DontDestroyOnLoad(root);
        root.AddComponent<RuntimeStatusOverlay>();
        root.AddComponent<ExperimentTelemetry>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { enabled = false; Destroy(this); return; }
        instance = this;
        Application.logMessageReceivedThreaded += CaptureError;
        MainController.SystemConfigurationChanged += Configure;
    }

    private void OnEnable()
    {
        if (instance != this) return;
        layoutVersion=-1;
        if (dashboardToggle == null)
        {
            dashboardToggle = new UnityEngine.InputSystem.InputAction("Toggle flight dashboard", UnityEngine.InputSystem.InputActionType.Button, "<Keyboard>/tab");
            dashboardToggle.performed += ToggleDashboard;
        }
        dashboardToggle.Enable();
    }
    private void OnDisable()
    {
        dashboardToggle?.Disable();
        if(instance!=this)return;
        if(panel!=null)panel.SetActive(false);
        OperatorOverlayLayout.Release("status");layoutVersion=-1;
    }
    private void ToggleDashboard(UnityEngine.InputSystem.InputAction.CallbackContext context)
    { TryToggleDashboard(); }
    private void TryToggleDashboard()
    {
        if (ConfigurationUI.ConfigWorkbench.IsOpen) return;
        // Display-only control stays available during a run, including stale legacy
        // metadata focus. Idle text forms retain Tab for field navigation.
        if (lastToggleFrame!=Time.frameCount && ((MainController.Instance != null && MainController.Instance.SequenceRunning) || !ExperimentInput.IsEditingText))
        {
            lastToggleFrame=Time.frameCount;
            OperatorOverlayVisibility.Toggle();
        }
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
        panel = new GameObject("Status panel", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        panel.transform.SetParent(ui.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-12, 12);
        rect.sizeDelta = new Vector2(650, 340);
        var background = panel.GetComponent<Image>();
        background.color = new Color(.047f,.075f,.102f,.96f); background.raycastTarget = false;
        var textObject = new GameObject("Status text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panel.transform, false);
        rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12, 48); rect.offsetMax = new Vector2(-12, -10);
        label = textObject.GetComponent<Text>();
        label.font = Resources.Load<Font>("ConfigurationUI/Fonts/Inter-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 13; label.color = new Color(.84f,.88f,.92f); label.supportRichText = true;
        label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        var buttonObject = new GameObject("Auto trim toggle", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panel.transform, false);
        rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-12, 10); rect.sizeDelta = new Vector2(170, 28);
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
        autoTrimButtonText.color = new Color(.84f,.9f,.93f); autoTrimButtonText.raycastTarget = false;
        var flightButtonObject = new GameObject("Flight check toggle", typeof(RectTransform), typeof(Image), typeof(Button));
        flightButtonObject.transform.SetParent(panel.transform, false);
        rect = flightButtonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-190, 10); rect.sizeDelta = new Vector2(260, 28);
        flightCheckButton = flightButtonObject.GetComponent<Button>();
        flightCheckButton.targetGraphic = flightButtonObject.GetComponent<Image>();
        flightCheckButton.onClick.AddListener(() => {
            var main = MainController.Instance;
            if (main != null) main.SetCurrentFlightCheck(!main.CurrentFlightCheckEnabled);
            nextRefresh = 0;
        });
        var flightLabelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        flightLabelObject.transform.SetParent(flightButtonObject.transform, false);
        rect = flightLabelObject.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        flightCheckButtonText = flightLabelObject.GetComponent<Text>(); flightCheckButtonText.font = label.font;
        flightCheckButtonText.fontSize = 15; flightCheckButtonText.alignment = TextAnchor.MiddleCenter;
        flightCheckButtonText.color = new Color(.84f,.9f,.93f); flightCheckButtonText.raycastTarget = false;
        var detailsObject=new GameObject("Diagnostics details",typeof(RectTransform),typeof(Image),typeof(Button));detailsObject.transform.SetParent(panel.transform,false);
        var detailsRect=detailsObject.GetComponent<RectTransform>();detailsRect.anchorMin=detailsRect.anchorMax=detailsRect.pivot=Vector2.zero;detailsRect.anchoredPosition=new Vector2(12,10);detailsRect.sizeDelta=new Vector2(104,28);
        var detailsButton=detailsObject.GetComponent<Button>();detailsButton.targetGraphic=detailsObject.GetComponent<Image>();detailsButton.targetGraphic.color=new Color(.13f,.18f,.24f);
        var detailsLabel=new GameObject("Label",typeof(RectTransform),typeof(Text));detailsLabel.transform.SetParent(detailsObject.transform,false);
        var detailsLabelRect=detailsLabel.GetComponent<RectTransform>();detailsLabelRect.anchorMin=Vector2.zero;detailsLabelRect.anchorMax=Vector2.one;detailsLabelRect.offsetMin=detailsLabelRect.offsetMax=Vector2.zero;
        var detailsText=detailsLabel.GetComponent<Text>();detailsText.font=label.font;detailsText.fontSize=13;detailsText.alignment=TextAnchor.MiddleCenter;detailsText.color=new Color(.72f,.83f,.9f);detailsText.raycastTarget=false;detailsText.text="More details";
        detailsButton.onClick.AddListener(()=>{showDetails=!showDetails;detailsText.text=showDetails?"Less details":"More details";layoutLineCount=-1;nextRefresh=0;});
        var nextAnimal = new GameObject("Next animal", typeof(RectTransform), typeof(Image), typeof(Button));
        nextAnimal.transform.SetParent(panel.transform, false);
        var nextRect = nextAnimal.GetComponent<RectTransform>();
        nextRect.anchorMin = nextRect.anchorMax = nextRect.pivot = Vector2.zero;
        nextRect.anchoredPosition = new Vector2(124, 10); nextRect.sizeDelta = new Vector2(72, 28);
        var nextButton = nextAnimal.GetComponent<Button>(); nextButton.targetGraphic = nextAnimal.GetComponent<Image>();
        nextButton.targetGraphic.color = new Color(.13f, .18f, .24f);
        var nextLabel = new GameObject("Label", typeof(RectTransform), typeof(Text)); nextLabel.transform.SetParent(nextAnimal.transform, false);
        var nextLabelRect = nextLabel.GetComponent<RectTransform>(); nextLabelRect.anchorMin = Vector2.zero; nextLabelRect.anchorMax = Vector2.one;
        nextLabelRect.offsetMin = nextLabelRect.offsetMax = Vector2.zero;
        var nextText = nextLabel.GetComponent<Text>(); nextText.font = label.font; nextText.fontSize = 11;
        nextText.alignment = TextAnchor.MiddleCenter; nextText.color = new Color(.72f, .83f, .9f); nextText.raycastTarget = false; nextText.text = "Next animal";
        nextButton.onClick.AddListener(() => { MainController.Instance?.SelectNextAnimal(); nextRefresh = 0; });
        Configure();
    }

    private void Configure()
    {
        if (canvas == null) return;
        OperatorOverlayLayout.Invalidate();layoutVersion=-1;
        var settings = MainController.Instance != null ? MainController.Instance.StatusOverlaySettings : new StatusOverlayConfig();
        canvas.targetDisplay = settings.targetDisplay >= 0 && settings.targetDisplay < Display.displays.Length ? settings.targetDisplay : 0;
        if (canvas.targetDisplay > 0) Display.displays[canvas.targetDisplay].Activate();
        if (!operatorVisibility) ApplyVisibility(settings.visible);
    }

    public void SetVisible(bool value)
    {
        bool wasDisplayed=ShouldDisplay;operatorVisibility=true;ApplyVisibility(value);nextRefresh=0;
        if(canvas!=null && wasDisplayed && !value) DisplayFrameClear.ClearOnce(canvas.targetDisplay);
        else if(canvas!=null && value) DisplayFrameClear.Cancel(canvas.targetDisplay);
    }
    private void ApplyVisibility(bool value) { visible = value; if (panel != null) panel.SetActive(ShouldDisplay); }

    private void Update()
    {
        if(UnityEngine.InputSystem.Keyboard.current?.tabKey.wasPressedThisFrame==true) TryToggleDashboard();
        // The setup form owns the idle screen and Tab navigation. Keep the operator's
        // dashboard preference, but suspend its canvas and refresh work between runs.
        if (panel != null && panel.activeSelf != ShouldDisplay) panel.SetActive(ShouldDisplay);
        if (ConfigurationUI.ConfigWorkbench.IsOpen) return;
        long revision;
        lock (errorLock) revision = errorRevision;
        if (revision != shownRevision) { shownRevision = revision; ApplyVisibility(true); nextRefresh = 0; }
        if (!Visible || label == null || Time.realtimeSinceStartupAsDouble < nextRefresh) return;
        nextRefresh = Time.realtimeSinceStartupAsDouble + .5;
        if (UnityEngine.EventSystems.EventSystem.current == null)
            new GameObject("Dashboard EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        var main = MainController.Instance;
        bool available = false;
        if (main != null && main.SequenceRunning)
            foreach (var rig in main.VRClosedLoops.Values) if (rig != null && rig.SupportsAutoTrim) { available = true; break; }
        autoTrimButton.interactable = available;
        autoTrimButton.gameObject.SetActive(showDetails&&available);
        bool enabled = main != null && main.CurrentAutoTrimEnabled;
        autoTrimButtonText.text = enabled ? "Auto-trim on · stop" : "Auto-trim off · start";
        autoTrimButton.targetGraphic.color = !available ? new Color(.1f,.13f,.16f) : enabled ? new Color(.08f,.28f,.23f) : new Color(.13f,.18f,.24f);
        flightCheckButton.interactable = available;
        flightCheckButton.gameObject.SetActive(showDetails&&available);
        bool flightCheck = main == null || main.CurrentFlightCheckEnabled;
        flightCheckButtonText.text = flightCheck ? "Flight check on · bypass" : "Flight bypassed · restore check";
        flightCheckButton.targetGraphic.color = !available ? new Color(.1f,.13f,.16f) : flightCheck ? new Color(.13f,.18f,.24f) : new Color(.3f,.22f,.12f);
        label.text = BuildText();
        RefreshLayout();
    }

    private void LateUpdate()
    {
        if(canvas==null||panel==null)return;
        if(!Visible||ConfigurationUI.ConfigWorkbench.IsOpen)
        {OperatorOverlayLayout.Release("status");layoutAvailable=false;panel.SetActive(false);return;}
        var rect=panel.GetComponent<RectTransform>();float scale=Mathf.Max(.01f,canvas.scaleFactor);
        var screen=OperatorOverlayLayout.DisplaySize(canvas.targetDisplay);var size=rect.sizeDelta*scale;
        if(layoutVersion!=OperatorOverlayLayout.Revision||lastWidth!=(int)screen.x||lastHeight!=(int)screen.y||lastPanelSize!=size)
        {
            layoutAvailable=OperatorOverlayLayout.Reserve("status",canvas.targetDisplay,new Rect(screen.x-size.x-12,12,size.x,size.y),out var placed);
            if(layoutAvailable){rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=placed.position/scale;}
            lastWidth=(int)screen.x;lastHeight=(int)screen.y;lastPanelSize=size;layoutVersion=OperatorOverlayLayout.Revision;
        }
        panel.SetActive(ShouldDisplay);
    }
    private void RefreshLayout()
    {
        // Only remeasure the rich-text layout when rows or errors change. Numeric refreshes
        // still update at 2 Hz, but no longer lay out the entire dashboard twice each time.
        int lines = 0;
        foreach (char c in label.text) if (c == '\n') lines++;
        if (lines != layoutLineCount || layoutErrorRevision != shownRevision)
        {
            layoutLineCount = lines; layoutErrorRevision = shownRevision;
            var panelRect = panel.GetComponent<RectTransform>();
            float width=showDetails?650:480;
            panelRect.sizeDelta = new Vector2(width, panelRect.sizeDelta.y);
            Canvas.ForceUpdateCanvases();
            panelRect.sizeDelta = new Vector2(width, Mathf.Clamp(label.preferredHeight + 62, 180, 780));
        }
    }

    public string BuildText() => showDetails ? BuildDetailedText() : BuildCompactText();

    private string BuildCompactText()
    {
        var main=MainController.Instance;
        var text=new StringBuilder("<size=17><b><color=#E8EEF3>RUN STATUS</color></b></size>   <size=11><color=#99ADBD>Tab hides overlays</color></size>\n");
        AppendRunContext(text,main);
        if(main!=null)
        {
            RugosityWorld terrain=null;
            foreach(var rig in main.VRClosedLoops.Values)
                if(rig!=null&&rig.TryGetComponent<RugosityRig>(out var surfaceRig)&&surfaceRig.ControlsPose){terrain=surfaceRig.world;break;}
            if(terrain!=null)
                text.AppendLine($"<color=#A8C6D9>Terrain L/R · visual {SurfaceLabel(terrain.settings.visualSurface)} · navigation {SurfaceLabel(terrain.settings.collisionSurface)}</color>");
            foreach(var entry in main.SystemConfigs)
            {
                main.VRClosedLoops.TryGetValue(entry.Key,out var rig);
                var listener=rig!=null?rig.GetComponent<ZmqListener>():null;
                string endpoint=listener!=null&&!string.IsNullOrEmpty(listener.Endpoint)?listener.Endpoint:$"tcp://{entry.Value.zmqAddress}:{entry.Value.zmqPort}";
                text.Append($"\n<b>{EscapeRich(entry.Key)}</b> · {EscapeRich(main.GetBackendDisplayName(entry.Value))} · <color=#A8C6D9>{EscapeRich(endpoint)}</color>");
                if(rig!=null&&rig.name!=entry.Key)text.Append(" · "+EscapeRich(rig.name));
                text.AppendLine();
                if(rig==null){text.AppendLine("<color=#BBC8D5>INACTIVE · configured input</color>");continue;}
                string state=listener==null?"missing listener":listener.State;
                double age=listener!=null?listener.SecondsSinceLastPose:double.PositiveInfinity;
                string ageLabel=double.IsInfinity(age)?"no pose received":$"age {Math.Max(0,age):F2} s";
                string color=state=="receiving"?"88D8BD":state=="error"?"FFADAD":"E9CA8D";
                text.Append($"<color=#{color}>{EscapeRich(state)} · {ageLabel}</color>  ");
                if(rig.SupportsAutoTrim)
                {
                    var flight=rig.AutoTrim;
                    text.Append(Badge(flight.Flying?"FLYING":flight.FlightReady?"NOT FLYING":"ACQUIRING",flight.Flying?"88D8BD":"BBC8D5"));
                }
                text.AppendLine();
                if(rig.SupportsAutoTrim)text.Append($"DC {Signed(rig.GetYawDCOffset()*KineflyAutoTrim.RadToDeg)}° · ");
                text.Append(Badge(rig.OrientationTrackingEnabled?"YAW CLOSED":"YAW OPEN",rig.OrientationTrackingEnabled?"88D8BD":"BBC8D5"));
                text.Append(Badge(rig.PositionTrackingEnabled?"POS CLOSED":"POS OPEN",rig.PositionTrackingEnabled?"88D8BD":"BBC8D5"));
                if(entry.Key=="VR"+main.SelectedVRIndex)text.Append("<color=#FFD166>SELECTED</color>");
                text.AppendLine();
            }
        }
        AppendErrors(text);
        return text.ToString();
    }

    private static string SurfaceLabel(string surface)
    {
        switch(surface)
        {
            case "RoughSmooth":return "rough / smooth";
            case "SmoothRough":return "smooth / rough";
            case "SmoothSmooth":return "smooth / smooth";
            case "RoughRough":return "rough / rough";
            case "Flat":return "flat / flat";
            default:return EscapeRich(surface);
        }
    }
    private static void AppendRunContext(StringBuilder text,MainController main)
    {
        text.Append("<b>"+EscapeRich(SceneManager.GetActiveScene().name)+"</b>\n");
        if(main==null){text.AppendLine("Idle · no experiment controller");return;}
        int count=main.executionOrder.Count>0?main.executionOrder.Count:main.sequenceSteps.Count;
        text.AppendLine($"Trial {main.currentTrial+1} · Step {Math.Min(main.currentStep+1,Math.Max(1,count))} / {count} · "+
            (main.SceneTransitionPending?"loading":main.SequenceError!=null?"ERROR":main.AssessmentFailed?"assessment failed":!main.SequenceRunning?"Idle":main.ExperimentPhase));
        if(main.SequenceRunning)
        {
            text.AppendLine($"Elapsed {main.CurrentStepElapsedSeconds:F1} / {main.CurrentStepDurationSeconds:F1} s · Remaining {main.RemainingStepSeconds:F1} s");
            if(main.AssessmentPending)text.AppendLine("Heading assessment active");
        }
        if(main.SequenceError!=null)text.AppendLine("<color=#FFADAD>"+EscapeRich(main.SequenceError)+"</color>");
    }

    private string BuildDetailedText()
    {
        var main = MainController.Instance;
        var text = new StringBuilder("<size=19><b><color=#E8EEF3>EXPERIMENT</color></b></size>   <size=12><color=#8898AA>[Tab] hide · [1–9] select · [0] next · [ / ] DC</color></size>\n");
        AppendRunContext(text,main);
        if (main != null)
        {
            foreach (var entry in main.SystemConfigs)
            {
                text.Append($"\n<b>{entry.Key}</b> ");
                if (!main.VRClosedLoops.TryGetValue(entry.Key, out var rig) || rig == null) { text.AppendLine("<color=#88929C>INACTIVE</color>"); continue; }
                var listener = rig.GetComponent<ZmqListener>();
                if(rig.SupportsAutoTrim)
                {
                    var flight=rig.AutoTrim;
                    text.Append("<size=18><b>"+Badge(flight.Flying?"FLYING":flight.FlightReady?"NOT FLYING":"ACQUIRING",flight.Flying?"88D8BD":flight.FlightReady?"BBC8D5":"A3B6C8")+"</b></size>  ");
                }
                else text.Append("<size=16>"+Badge(rig.MotionMode.ToString().ToUpperInvariant(),"A3B6C8")+"</size>  ");
                // Keep the actual offset at the front of every active rig, including unsupported/off modes.
                text.Append($"<size=14><color=#{(rig.SupportsAutoTrim ? "9DC5DC" : "88929C")}>DC {Signed(rig.GetYawDCOffset() * KineflyAutoTrim.RadToDeg)}°</color></size> ");
                text.Append(Badge(rig.OrientationTrackingEnabled ? "YAW CLOSED" : "YAW OPEN", rig.OrientationTrackingEnabled ? "70E1B0" : "88929C"));
                text.Append(Badge(rig.PositionTrackingEnabled ? "POS CLOSED" : "POS OPEN", rig.PositionTrackingEnabled ? "70E1B0" : "88929C"));
                text.AppendLine(entry.Key == "VR" + main.SelectedVRIndex ? " <color=#FFD166>SELECTED</color>" : "");
                if (rig.SupportsAutoTrim)
                {
                    var trim = rig.AutoTrim;
                    text.AppendLine($"  {Badge($"GAIN {rig.GetYawGain():G4}", rig.OrientationTrackingEnabled && rig.GetYawGain() != 0 ? "FFFFFF" : "88929C")} " +
                        (trim.FlightCheckEnabled ? "" : Badge("FLIGHT BYPASSED", "FFD166")));
                    text.AppendLine($"  <size=12><color=#99ADBD>Wing SD {Math.Sqrt(trim.FlightVariance) * KineflyAutoTrim.RadToDeg:F2}° · threshold {Math.Sqrt(trim.FlightThreshold) * KineflyAutoTrim.RadToDeg:F2}° · yaw {Signed(rig.GetLastYawOutput())}°/s</color></size>");
                    if(showDetails) {
                    text.AppendLine($"  <size=12><color=#99ADBD>DC compensation {Signed(rig.GetYawGain() * rig.GetYawDCOffset() * KineflyAutoTrim.RadToDeg)}°/s · {trim.FlightSampleCount} samples</color></size>");
                    bool working = trim.Enabled && trim.FlightAllowsTrim && rig.OrientationTrackingEnabled && rig.GetYawGain() != 0;
                    string state = !trim.Enabled ? "AUTO TRIM OFF / DC HELD" : "AUTO TRIM: " + (trim.State == "TRIMMING" ? "ADJUSTING" : trim.State);
                    text.Append("  " + Badge(state, !trim.Enabled ? "88929C" : trim.State == "CENTERED" ? "70E1B0" : working ? "FFD166" : "88929C"));
                    if (trim.State == "COLLECTING" || trim.State == "VERIFYING / NOISE") text.Append($" {trim.WindowProgress:P0} / {trim.MeasurementSeconds:F0}s");
                    if (trim.Enabled) text.Append($"  pass {trim.Passes}");
                    text.AppendLine();
                    if (trim.EffectiveMeanDegPerSecond.HasValue)
                        text.AppendLine($"  window mean {Signed(trim.EffectiveMeanDegPerSecond.Value)}°/s | median {Signed(trim.EffectiveMedianDegPerSecond.Value)}°/s | ±{trim.UncertaintyDegPerSecond:F2}°/s");
                    var change = trim.LastAdjustment;
                    if (change != null)
                    {
                        double age = Time.realtimeSinceStartupAsDouble - change.time;
                        bool recentChange = working && age >= 0 && age <= 5 && rig.GetYawDCOffset() == change.toRadians;
                        string direction = change.deltaRadians > 0 ? "DC UP" : "DC DOWN";
                        text.AppendLine("  " + Badge($"LAST AUTO: {direction} {Signed(change.deltaRadians * KineflyAutoTrim.RadToDeg)}°", recentChange ? "FFD166" : "9DA8B5") +
                            $"{Signed(change.fromRadians * KineflyAutoTrim.RadToDeg)} -> {Signed(change.toRadians * KineflyAutoTrim.RadToDeg)}°");
                    }
                    else text.AppendLine("  <color=#9DA8B5>DC HELD / no automatic adjustment yet</color>");
                    } else text.AppendLine("  <size=12>"+Badge(trim.Enabled?"AUTO TRIM "+trim.State:"AUTO TRIM OFF · DC HELD",trim.Enabled?"9ECFBD":"95AABC")+"</size>");
                }
                else
                {
                    text.AppendLine($"  {rig.GetCurrentMode()} / {rig.MotionMode} " + Badge("AUTO TRIM UNAVAILABLE", "88929C"));
                    text.AppendLine($"  {Badge($"P GAIN {rig.PositionGain:G4}", rig.PositionTrackingEnabled ? "FFFFFF" : "88929C")} " +
                        Badge($"YAW GAIN {rig.OrientationGain:G4}", rig.OrientationTrackingEnabled ? "FFFFFF" : "88929C") + "<color=#9DA8B5>Requires flight yaw-rate mode</color>");
                }
                if(showDetails) text.AppendLine($"  <size=12><color=#99ADBD>Input SUB: {(listener == null ? "missing listener" : EscapeRich(listener.Endpoint) + " / " + listener.State)}</color></size>");
                var reference = rig.GetComponent<StimulusHeadingReference>();
                if (reference != null && reference.IsObserving) text.AppendLine($"  heading assessment: {reference.RemainingSeconds:F1}s");
                else if (showDetails && reference != null && reference.Result != null) text.AppendLine($"  zero {reference.Result.meanHeadingDegrees:F1} deg, r={reference.Result.resultantLength:F3} ({reference.Phase})");
            }
        }
        if(showDetails) text.AppendLine($"Telemetry PUB (output): {(telemetry != null ? telemetry.State + " " + telemetry.Endpoint : "starting")}");
        int pending = 0; long dropped = 0; double capture = 0; int loggers = 0;
        foreach (var logger in FindObjectsByType<SwarmLogger>(FindObjectsSortMode.None))
        { pending += logger.PendingFrames; dropped += logger.DroppedFrames; capture += logger.CaptureMilliseconds; loggers++; }
        if (loggers > 0) text.AppendLine($"Swarm log: worker / capture {capture:F2} ms / queued {pending} / dropped {dropped}");
        AppendErrors(text);
        return text.ToString();
    }

    private static void AppendErrors(StringBuilder text)
    {
        ErrorEntry[] recent = RecentErrors();
        if (recent.Length > 0)
        {
            ErrorEntry latest = recent[recent.Length - 1];
            text.AppendLine($"{latest.type} {latest.timestampUtc} ({recent.Length} recent)");
            text.Append(EscapeRich(Shorten(latest.message, 420)));
            text.Append("\nFull details: runtime_trace.log / telemetry errors");
        }
    }

    private static string Badge(string value, string color) => $"<color=#{color}><b>[{value}]</b></color> ";
    private static string Signed(double value) => value.ToString("+0.00;-0.00;0.00", System.Globalization.CultureInfo.InvariantCulture);
    private static string EscapeRich(string value) => (value ?? "").Replace("<", "‹").Replace(">", "›");

    private static string Shorten(string value, int length) => value == null ? "" : value.Length <= length ? value : value.Substring(0, length) + "…";
    private static void CaptureError(string condition, string trace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
#if UNITY_EDITOR
        if((trace??"").Contains("UnityEditor.Search.SearchDatabase")) return;
#endif
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
        dashboardToggle?.Dispose();
        if (instance != this) return;
        OperatorOverlayLayout.Release("status");
        Application.logMessageReceivedThreaded -= CaptureError;
        MainController.SystemConfigurationChanged -= Configure;
        instance = null;
    }
}
