#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Run on a disposable project copy: Unity -batchmode -projectPath ... -executeMethod KannadiValidation.Run
[InitializeOnLoad]
public static class KannadiValidation
{
    private const string Key = "KannadiValidation.Active";
    private static int phase, nextFrame, checks;
    private static double timeout;
    private static MainController main;
    private static readonly List<string> failures = new List<string>();
    private static readonly List<string> editorMessages = new List<string>();
    private static Kannadi[] rigs;
    private static Vector3 expectedPosition;
    private static int rigInstance;
    private static string logDirectory;
    private static Camera hiddenCamera;
    private static int hiddenRenders;
    private static void CountHiddenRender(Camera camera) { if (camera == hiddenCamera) hiddenRenders++; }
    static KannadiValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                phase = 0; nextFrame = Time.frameCount + 2; timeout = EditorApplication.timeSinceStartup + 120;
                Application.logMessageReceived += OnLog;
                SceneManager.sceneLoaded += DisableLiveInputs;
                EditorApplication.update += Tick;
            }
        };
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY")))
            throw new InvalidOperationException("Run KannadiValidation only on a disposable copy with KANNADI_VALIDATION_COPY at its root.");
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (stack.Contains("UnityEditor.Search.SearchDatabase")) { editorMessages.Add(message); return; }
        if (type == LogType.Exception || type == LogType.Error) failures.Add(message + "\n" + stack);
    }
    private static void DisableLiveInputs(Scene scene, LoadSceneMode mode)
    {
        foreach (ZmqListener listener in Object.FindObjectsOfType<ZmqListener>())
        {
            listener.enabled = false;
            Call(listener, "ApplySystemConfig");
        }
    }
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("Validation failed: " + message);
    }
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);
    private static Dictionary<string, object> Params(string json) => JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
    private static void Later() { phase++; nextFrame = Time.frameCount + 8; }
    private static void NextSequenceStep()
    {
        typeof(MainController).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, 0f);
        Call(main, "ManageTimerAndTransitions");
    }
    private static void CheckViewports(string scene)
    {
        Check(QualitySettings.vSyncCount == main.VSyncCount && Application.targetFrameRate == main.TargetFrameRate, scene + " preserves configured FPS instead of forcing VSync");
        ViewportSetter[] setters = Object.FindObjectsOfType<ViewportSetter>();
        Check(setters.Length == 4, scene + " has four configured rigs");
        foreach (ViewportSetter setter in setters)
        {
            bool found = main.TryGetSystemConfigForGameObject(setter.gameObject, out SystemConfig config);
            string order = found ? ViewportSetter.NormalizeDisplayOrder(config.displayOrder) : "";
            Camera[] cameras = setter.GetComponentsInChildren<Camera>(true);
            foreach (Camera camera in cameras)
            {
                int slot = camera.name.StartsWith("Main Camera ") && camera.name.Length == 13 ? order.IndexOf(camera.name[12]) : -1;
                Check(camera.enabled == (slot >= 0), scene + "/" + setter.name + "/" + camera.name + " enabled only by configured letter");
                if (slot < 0) continue;
                Check(camera.gameObject.activeInHierarchy && camera.targetDisplay == config.targetDisplay, scene + " listed camera active on configured display");
                float x = (config.startCol + (config.horizontal ? slot : 0)) * config.ledPanelWidth;
                float y = Screen.height - (config.startRow + 1 + (config.horizontal ? 0 : slot)) * config.ledPanelHeight;
                // Batch Game View clips pixelRect at its 640x480 edge; verify the configured rectangle before clipping.
                Rect rect = new Rect(camera.rect.x * Screen.width, camera.rect.y * Screen.height, camera.rect.width * Screen.width, camera.rect.height * Screen.height);
                Check(Mathf.Abs(rect.x - x) < 0.1f && Mathf.Abs(rect.y - y) < 0.1f &&
                    Mathf.Abs(rect.width - config.ledPanelWidth) < 0.1f && Mathf.Abs(rect.height - config.ledPanelHeight) < 0.1f,
                    scene + "/" + setter.name + "/" + camera.name + " expected pixels " + new Rect(x, y, config.ledPanelWidth, config.ledPanelHeight) + " got " + rect + " normalized " + camera.rect + " screen " + Screen.width + "x" + Screen.height);
            }
            Check(cameras.Count(c => c.enabled) == order.Count(letter => cameras.Any(c => c.name == "Main Camera " + letter)), scene + " each available configured camera enabled once");
        }
    }
    private static void CheckCameraReconfiguration()
    {
        var all = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "system_config.json")));
        var configs = (Newtonsoft.Json.Linq.JArray)all["configs"];
        string[] orders = { "UL", "FDRBLU", "", "B" };
        for (int i = 0; i < configs.Count; i++)
        {
            configs[i]["displayOrder"] = orders[i];
            configs[i]["horizontal"] = i % 2 == 1;
            configs[i]["startRow"] = i;
            configs[i]["startCol"] = i + 1;
            configs[i]["ledPanelWidth"] = 64;
            configs[i]["ledPanelHeight"] = 96;
        }
        string file = "Kannadi/validation-system.json";
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, file), all.ToString());
        main.SetSystemConfigFile(file);
        CheckViewports("Kannadi reordered system config");
        configs.RemoveAt(3);
        ((Newtonsoft.Json.Linq.JObject)configs[1]).Remove("displayOrder");
        configs[0]["displayOrder"] = null;
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, file), all.ToString());
        main.SetSystemConfigFile(file);
        CheckViewports("Kannadi missing rig / missing or null displayOrder");
        main.SetSystemConfigFile("system_config.json");
        CheckViewports("Kannadi restored VR1 config");

        var unknown = new GameObject("VR10 validation");
        Camera camera = new GameObject("Main Camera R").AddComponent<Camera>();
        camera.transform.SetParent(unknown.transform);
        ViewportSetter setter = unknown.AddComponent<ViewportSetter>();
        Check(!main.TryGetSystemConfigForGameObject(unknown, out _) && !camera.enabled, "VR10 cannot borrow VR1 config");
        unknown.name = "VR1 validation";
        setter.RefreshSystemConfig();
        Check(camera.enabled, "known rig re-enables its listed camera");
        Check(main.TryGetSystemConfigForGameObject(camera.gameObject, out var inherited) && inherited.vrId == "VR1", "child resolves parent rig configuration");
        Camera up = new GameObject("Main Camera U").AddComponent<Camera>();
        up.transform.SetParent(unknown.transform); up.gameObject.SetActive(false);
        Camera unlisted = new GameObject("Diagnostic camera").AddComponent<Camera>();
        unlisted.transform.SetParent(unknown.transform);
        setter.ApplyConfiguration(new SystemConfig { targetDisplay = 0, displayOrder = "uUr?R", ledPanelWidth = 64, ledPanelHeight = 96 });
        Check(camera.enabled && up.enabled && up.gameObject.activeSelf && !unlisted.enabled, "inactive listed cameras enabled; unknown and duplicate letters ignored");
        Check(Mathf.Abs(up.pixelRect.x) < 0.1f && Mathf.Abs(camera.pixelRect.x - 64) < 0.1f, "duplicate/invalid letters do not shift camera slots");
        setter.ApplyConfiguration(new SystemConfig { targetDisplay = Display.displays.Length, displayOrder = "RU" });
        Check(!camera.enabled && !up.enabled && !unlisted.enabled, "unavailable display cannot render cameras elsewhere");
        setter.ApplyConfiguration(new SystemConfig { targetDisplay = 0, displayOrder = "R" });
        setter.enabled = false;
        Check(!camera.enabled, "disabled layout cannot leave stimulus camera rendering");
        unknown.SetActive(false);
        Object.Destroy(unknown);
    }
    private static Vector2 Heading(OverheadTrackOverlay overlay, object track)
    {
        object[] args = { track, Vector2.zero, Vector2.zero };
        overlay.GetType().GetMethod("MarkerPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(overlay, args);
        return (Vector2)args[2];
    }
    private static Vector3[] ArrowVertices(OverheadTrackOverlay overlay)
    {
        using (var vh = new VertexHelper())
        {
            overlay.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null).Invoke(overlay, new object[] { vh });
            var result = new Vector3[vh.currentVertCount];
            for (int i = 0; i < result.Length; i++) { var vertex = new UIVertex(); vh.PopulateUIVertex(ref vertex, i); result[i] = vertex.position; }
            return result;
        }
    }
    private static void CheckOverviewTracking()
    {
        var overlay = Object.FindObjectOfType<OverheadTrackOverlay>();
        Check(overlay != null && overlay.TrackedRigCount == 4, "one heading per scene rig");
        Check(overlay.GetComponentsInChildren<Text>().Select(t => t.text).OrderBy(t => t).SequenceEqual(new[] { "1", "2", "3", "4" }), "numeric labels without VR prefix");
        Check(!GameObject.FindObjectsOfType<Renderer>().Any(r => r.name.StartsWith("Tracked VR")), "old sphere markers removed");
        Check(overlay.GetComponentInParent<RectMask2D>() != null && !overlay.raycastTarget, "markers clipped to overview and do not block mouse input");
        OverheadCameraConfig settings = Field<OverheadCameraConfig>(overlay, "settings");
        Check(settings.trailDurationSeconds == 60, "one-minute trajectory history by default");
        var tracks = Field<System.Collections.IList>(overlay, "tracks");
        foreach (object t in tracks) Field<System.Collections.IList>(t, "samples").Clear();
        object track = tracks[0];
        ClosedLoop rig = Field<ClosedLoop>(track, "rig");
        Vector3 originalPosition = rig.transform.position;
        Quaternion originalRotation = rig.transform.rotation;
        rig.transform.rotation = Quaternion.identity;
        Check(Vector2.Dot(Heading(overlay, track), Vector2.up) > 0.999f, "north-facing animal arrow points up in nadir view");
        rig.transform.rotation = Quaternion.Euler(0, 90, 0);
        Check(Vector2.Dot(Heading(overlay, track), Vector2.right) > 0.999f, "heading arrow follows animal yaw");
        var orbit = Object.FindObjectOfType<OverheadCameraController>();
        float originalDistance = orbit.distance;
        orbit.distance = 50; Call(orbit, "UpdateCameraPosition");
        Vector3[] near = ArrowVertices(overlay);
        orbit.distance = 500; Call(orbit, "UpdateCameraPosition");
        Vector3[] far = ArrowVertices(overlay);
        Check(near.Length >= 96 && far.Length >= 96, "four arrows contain no sphere geometry");
        Check(Mathf.Abs(Vector3.Distance(near[near.Length - 24], near[near.Length - 22]) - Vector3.Distance(far[far.Length - 24], far[far.Length - 22])) < 0.01f, "arrow remains same size over tenfold zoom-out");
        var labelPositions = tracks.Cast<object>().Select(t => Field<Vector2>(t, "markerPosition")).ToArray();
        Check(tracks.Cast<object>().All(t => Field<Vector2>(t, "actualPosition") == Field<Vector2>(t, "markerPosition")), "markers stay at exact rig positions even when crowded");
        var second = tracks[1];
        var secondRig = Field<ClosedLoop>(second, "rig");
        Vector3 secondPosition = secondRig.transform.position;
        secondRig.transform.position = rig.transform.position;
        Call(overlay, "UpdateLabels");
        Check(Field<Vector2>(track, "markerPosition") == Field<Vector2>(second, "markerPosition"), "overlapping animals keep overlapping markers without rearrangement");
        secondRig.transform.position = secondPosition;
        orbit.distance = originalDistance; Call(orbit, "UpdateCameraPosition");
        float now = Time.unscaledTime;
        rig.transform.position = Vector3.zero; Call(overlay, "SampleTrajectories", now - 61);
        rig.transform.position = Vector3.right; Call(overlay, "SampleTrajectories", now - 59);
        rig.transform.position = Vector3.right * 2; Call(overlay, "SampleTrajectories", now - 30);
        rig.transform.position = Vector3.right * 3; Call(overlay, "SampleTrajectories", now);
        var samples = Field<System.Collections.IList>(track, "samples");
        Check(samples.Count == 3 && Mathf.Approximately(Field<float>(samples[0], "time"), now - 59), "expired trajectory samples removed and recent minute retained");
        var opacity = typeof(OverheadTrackOverlay).GetMethod("TrailOpacity", BindingFlags.Static | BindingFlags.NonPublic);
        float recent = (float)opacity.Invoke(null, new object[] { 0f, 60f });
        float old = (float)opacity.Invoke(null, new object[] { 30f, 60f });
        float expired = (float)opacity.Invoke(null, new object[] { 61f, 60f });
        Check(recent > old && old > 0 && recent < 0.5f && expired == 0, "faint trajectory fades with age to zero");
        rig.transform.position = Vector3.right * 100; Call(overlay, "SampleTrajectories", now + 0.1f);
        Check(Field<bool>(samples[samples.Count - 1], "breakBefore"), "periodic wrap / teleport does not draw a long false path");
        var extra = new GameObject("VR12 test rig"); extra.AddComponent<ClosedLoop>();
        Call(overlay, "RefreshRigs");
        Check(overlay.TrackedRigCount == 5 && overlay.GetComponentsInChildren<Text>().Any(t => t.text == "12"), "additional scene rigs get markers beyond four");
        extra.SetActive(false); Call(overlay, "RefreshRigs"); Object.Destroy(extra);
        Check(overlay.TrackedRigCount == 4, "inactive or removed rigs lose markers");
        foreach (object t in tracks) Field<System.Collections.IList>(t, "samples").Clear();
        var setup = Object.FindObjectOfType<SimpleOverheadCamera>();
        setup.ToggleCamera();
        Check(ArrowVertices(overlay).Length == 0 && overlay.GetComponentsInChildren<Text>().All(t => !t.enabled), "hide overview hides arrows and labels");
        Call(overlay, "LateUpdate");
        Check(!overlay.enabled && Field<System.Collections.IList>(track, "samples").Count == 0, "hidden overview stops tracking updates");
        Check(!Field<Camera>(setup, "overheadCam").enabled && !Field<GameObject>(setup, "cameraObject").activeSelf && !Field<GameObject>(setup, "backgroundObject").activeSelf, "hide disables both overview cameras");
        Check(!Field<RenderTexture>(setup, "renderTexture").IsCreated(), "hidden overview releases its render target");
        Check(GameObject.Find("FPS Label").GetComponent<Text>().enabled, "FPS remains visible when overview is hidden");
        setup.ToggleCamera();
        Check(overlay.enabled && Field<Camera>(setup, "overheadCam").enabled && Field<RenderTexture>(setup, "renderTexture").IsCreated(), "show restores rendering and tracking");
        rig.transform.SetPositionAndRotation(originalPosition, originalRotation);
        foreach (object t in tracks) Field<System.Collections.IList>(t, "samples").Clear();
        // Illustrative synthetic paths for the saved operator-panel screenshot.
        var positions = new Dictionary<ClosedLoop, Vector3>();
        foreach (object t in tracks) { var r = Field<ClosedLoop>(t, "rig"); positions[r] = r.transform.position; }
        for (int i = 0; i <= 120; i++)
        {
            foreach (object t in tracks)
            {
                var r = Field<ClosedLoop>(t, "rig");
                float fraction = i / 120f, side = r.name.Contains("VR1") || r.name.Contains("VR3") ? 1 : -1;
                r.transform.position = positions[r] + new Vector3(side * 30 * (1 - fraction), 0, 12 * Mathf.Sin(fraction * Mathf.PI * 2));
            }
            Call(overlay, "SampleTrajectories", now - 60 + i * 0.5f);
        }
        foreach (var pair in positions) pair.Key.transform.position = pair.Value;
        Call(overlay, "UpdateLabels");
    }
    private static void CaptureOverviewPanel()
    {
        Canvas canvas = GameObject.Find("Overhead Camera Canvas").GetComponent<Canvas>();
        RectTransform panel = GameObject.Find("Overview Panel").GetComponent<RectTransform>();
        Vector2 anchorMin = panel.anchorMin, anchorMax = panel.anchorMax;
        var previewObject = new GameObject("Overview validation capture");
        Camera preview = previewObject.AddComponent<Camera>();
        preview.transform.position = new Vector3(0, 0, -100000);
        preview.clearFlags = CameraClearFlags.SolidColor;
        preview.backgroundColor = Color.black;
        var target = new RenderTexture(640, 640, 24); target.Create(); preview.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = preview; canvas.planeDistance = 1;
        panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
        Canvas.ForceUpdateCanvases();
        Call(Object.FindObjectOfType<SimpleOverheadCamera>(), "LateUpdate");
        Call(Object.FindObjectOfType<OverheadTrackOverlay>(), "LateUpdate");
        Canvas.ForceUpdateCanvases();
        GameObject.Find("Simple Overhead Camera").GetComponent<Camera>().Render();
        preview.Render();
        RenderTexture.active = target;
        var pixels = new Texture2D(640, 640, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../kannadi-overview.png"), pixels.EncodeToPNG());
        Object.Destroy(pixels); RenderTexture.active = null;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
        panel.anchorMin = anchorMin; panel.anchorMax = anchorMax;
        preview.targetTexture = null; target.Release(); Object.Destroy(target); Object.Destroy(previewObject);
        Canvas.ForceUpdateCanvases();
    }
    private static void CheckTextResources()
    {
        var settings = TMPro.TMP_Settings.instance;
        Check(settings != null, "TMP settings resource resolves");
        var required = typeof(TMPro.TMP_Settings).GetField("s_CurrentAssetVersion", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        Check((string)Field<string>(settings, "assetVersion") == (string)required, "TMP essential resources match Unity 6 importer version");
        Check(TMPro.TMP_Settings.defaultFontAsset != null && TMPro.TMP_Settings.defaultFontAsset.material.shader.isSupported, "TMP default font and shader resolve");
        GameObject root = new GameObject("TMP validation canvas", typeof(Canvas));
        var text = new GameObject("TMP test", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TextMeshProUGUI>();
        text.transform.SetParent(root.transform, false);
        text.font = TMPro.TMP_Settings.defaultFontAsset;
        text.text = "1234"; text.ForceMeshUpdate();
        Check(text.textInfo.characterCount == 4 && text.textInfo.meshInfo[0].vertexCount > 0, "TMP renders with imported resources");
        Object.Destroy(root);
    }
    private static void CheckSceneOverview()
    {
        Check(Object.FindObjectsOfType<fps>().Length == 1, "one FPS display across scenes");
        RectTransform fpsBox = GameObject.Find("FPS").GetComponent<RectTransform>();
        Check(fpsBox.anchorMin == Vector2.one && fpsBox.anchorMax == Vector2.one && fpsBox.anchoredPosition.x < 0 && fpsBox.anchoredPosition.y < 0, "FPS anchored inside top-right corner");
        var floor = GameObject.Find("Plane").GetComponent<Renderer>();
        Check(floor.sharedMaterial.name == "LocustDryGrass" && floor.sharedMaterial.mainTexture != null && floor.sharedMaterial.shader.isSupported, "dry grass ground material available in scene");
        Check(Mathf.Approximately(floor.bounds.size.x / floor.sharedMaterial.mainTextureScale.x, 20), "grass tile spans 20 world centimeters for optic flow");
        Vector2 extent = GameObject.Find("Overview Panel").GetComponent<RectTransform>().anchorMax - GameObject.Find("Overview Panel").GetComponent<RectTransform>().anchorMin;
        Check(Vector2.Distance(extent, new Vector2(0.4f, 0.4f)) < 0.001f, "larger forty-percent overview panel");
    }
    private static void CheckAnimationGate()
    {
        var gate = rigs[0].Clones[0].GetComponentInChildren<AnimateOnMove>();
        Check(gate != null && gate.enabled, "Kannadi animation gate enabled from experiment config");
        Animator animator = gate.GetComponent<Animator>();
        Transform root = rigs[0].transform;
        Vector3 originalPosition = root.position;
        Quaternion originalRotation = root.rotation;
        gate.Configure(root, true, 0.5f, new Vector2(200, 200));
        root.position += Vector3.right * 0.049f; Call(gate, "MeasureTranslation", 0.1f);
        Check(Mathf.Abs(gate.CurrentSpeed - 0.49f) < 0.001f && animator.speed == 0, "translation below 0.5 cm/s stops legs");
        root.position += Vector3.right * 0.051f; Call(gate, "MeasureTranslation", 0.1f);
        Check(Mathf.Abs(gate.CurrentSpeed - 0.51f) < 0.001f && animator.speed > 0, "translation above 0.5 cm/s animates legs");
        root.rotation *= Quaternion.Euler(0, 90, 0); Call(gate, "MeasureTranslation", 0.1f);
        Check(gate.CurrentSpeed == 0 && animator.speed == 0, "rotation in place cannot animate legs");
        root.position = new Vector3(99.98f, 1, 0); gate.Configure(root, true, 0.5f, new Vector2(200, 200));
        root.position = new Vector3(-99.98f, 1, 0); Call(gate, "MeasureTranslation", 0.1f);
        Check(Mathf.Abs(gate.CurrentSpeed - 0.4f) < 0.001f && animator.speed == 0, "periodic wrap does not create false high walking speed");
        gate.Configure(root, false, 0.5f, Vector2.zero);
        Check(!gate.enabled && animator.speed > 0, "boolean disables speed gating and restores animation");
        gate.Configure(root, true, 2f, Vector2.zero);
        root.position += Vector3.right * 0.1f; Call(gate, "MeasureTranslation", 0.1f);
        Check(animator.speed == 0, "custom speed threshold is respected in world units");
        root.SetPositionAndRotation(originalPosition, originalRotation);
        gate.Configure(root, true, 0.5f, new Vector2(200, 200));
        Check(KannadiConfig.Load(Params("{\"animationNoiseThreshold\":0.25}")).animationSpeedThreshold == 0.25f, "legacy animation threshold remains compatible");
        bool rejected = false;
        try { KannadiConfig.Load(Params("{\"animationSpeedThreshold\":-1}")); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "negative animation thresholds rejected");
    }
    // EditorApplication.update can run outside the player input phase; explicitly route synthetic events.
    private static void UpdateTestInput() => typeof(UnityEngine.InputSystem.InputSystem)
        .GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(UnityEngine.InputSystem.LowLevel.InputUpdateType) }, null)
        .Invoke(null, new object[] { UnityEngine.InputSystem.LowLevel.InputUpdateType.Dynamic });

    private static void CheckModernInput(ClosedLoop tracking)
    {
        var inputSettings = UnityEngine.InputSystem.InputSystem.settings;
        var oldEditorBehavior = inputSettings.editorInputBehaviorInPlayMode;
        var oldBackgroundBehavior = inputSettings.backgroundBehavior;
        inputSettings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        inputSettings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        var pad = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Gamepad>();
        var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
        try
        {
            tracking.transform.position = Vector3.one * 20;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.R));
            UpdateTestInput();
            Check(ExperimentInput.Pressed("Reset"), "Input System receives R action (key=" + keyboard.rKey.isPressed + ", editing=" + ExperimentInput.IsEditingText + ", focus=" + Application.isFocused + ")");
            Call(tracking, "Update");
            Check(tracking.transform.position == Vector3.zero, "R key resets a rig with missing tracking through Update");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            UpdateTestInput();
            Check(!ExperimentInput.Pressed("Reset"), "released R does not repeatedly reset");
            bool before = Field<bool>(tracking, "closedLoopPosition");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.P));
            UpdateTestInput();
            Call(tracking, "Update");
            Check(Field<bool>(tracking, "closedLoopPosition") != before, "P key toggles tracking through Update");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.LowLevel.GamepadState { leftStick = Vector2.up, rightStick = Vector2.right });
            UpdateTestInput();
            Check(ExperimentInput.Move.y > 0.99f && ExperimentInput.Axis("Yaw") > 0.99f, "gamepad sticks drive translation and heading actions");
            Vector3 manualStart = tracking.transform.position;
            Quaternion manualHeading = tracking.transform.rotation;
            tracking.GetComponent<Keyboard>().ApplyInput(0.1f);
            Check(Vector3.Distance(manualStart, tracking.transform.position) > 0 && Quaternion.Angle(manualHeading, tracking.transform.rotation) > 0, "gamepad actions actually translate and rotate the rig (distance=" + Vector3.Distance(manualStart, tracking.transform.position) + ", angle=" + Quaternion.Angle(manualHeading, tracking.transform.rotation) + ")");
            tracking.transform.position = Vector3.one * 10;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.LowLevel.GamepadState().WithButton(UnityEngine.InputSystem.LowLevel.GamepadButton.Select));
            UpdateTestInput();
            Call(tracking, "Update");
            Check(tracking.transform.position == Vector3.zero, "gamepad Select resets the tracked rig");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(123, 234), scroll = new Vector2(0, 1) });
            UpdateTestInput();
            Check(ExperimentInput.MousePosition == new Vector3(123,234,0) && Mathf.Approximately(ExperimentInput.Scroll, 0.1f), "pointer and wheel retain overview zoom units");
            var zoomObject = new GameObject("Zoom input regression", typeof(Camera));
            zoomObject.GetComponent<Camera>().enabled = false;
            try
            {
                var zoom = zoomObject.AddComponent<OverheadCameraController>();
                zoom.distance = 150f;
                Call(zoom, "Start");
                Call(zoom, "HandleMouseInput");
                Check(Mathf.Approximately(zoom.distance, 130f), "normalized wheel tick zooms 20 world units, not 1/6 unit");
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(123,234), scroll = new Vector2(0, -1) });
                UpdateTestInput();
                Call(zoom, "HandleMouseInput");
                Check(Mathf.Approximately(zoom.distance, 150f), "reverse wheel tick restores zoom distance");
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(123,234), scroll = new Vector2(0, 0.25f) });
                UpdateTestInput();
                Call(zoom, "HandleMouseInput");
                Check(Mathf.Approximately(zoom.distance, 145f), "fractional trackpad scroll retains proportional zoom");
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(123,234) });
                UpdateTestInput();
                Call(zoom, "HandleMouseInput");
                Check(Mathf.Approximately(zoom.distance, 145f), "no scroll produces no residual zoom");
            }
            finally { Object.Destroy(zoomObject); }
            Check(Vector3.Distance(Quaternion.Euler(0,90,0) * Vector3.forward, Vector3.right) < 0.001f, "positive yaw turns forward toward right");
        }
        finally
        {
            UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
            UnityEngine.InputSystem.InputSystem.RemoveDevice(pad);
            UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
            inputSettings.editorInputBehaviorInPlayMode = oldEditorBehavior;
            inputSettings.backgroundBehavior = oldBackgroundBehavior;
        }
    }

    private static void CheckTemplateSchemas()
    {
        var settings = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error };
        foreach (string file in Directory.GetFiles(Path.Combine(Application.streamingAssetsPath, "Templates"), "*.json"))
        {
            string json = File.ReadAllText(file);
            var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
            Type type = obj["sequences"] != null ? typeof(SequenceConfig) :
                obj["stimuli"] != null ? typeof(OptomotorConfig) :
                obj["objects"] != null ? typeof(SceneConfig) :
                obj["steps"] != null ? typeof(DynamicSequenceController).GetNestedType("DesignFile", BindingFlags.NonPublic) :
                obj["numberOfRings"] != null ? typeof(KannadiConfig) : null;
            if (type != null) Check(JsonConvert.DeserializeObject(json, type, settings) != null, "strict template schema " + Path.GetFileName(file));
            if (obj["configs"] != null)
                foreach (var rig in obj["configs"]) Check(JsonConvert.DeserializeObject<SystemConfig>(rig.ToString(), settings) != null, "strict system rig schema");
        }
    }

    private static void CheckWalkingGains()
    {
        var root = new GameObject("Walking gain regression");
        var listener = root.AddComponent<ZmqListener>(); listener.enabled = false;
        var tracking = root.AddComponent<ClosedLoop>(); tracking.enabled = false;
        Call(tracking, "Start");
        tracking.SetSphereDiameter(2.6f);
        var publish = (Action<Pose>)typeof(ZmqListener).GetMethod("PublishPose", BindingFlags.Instance | BindingFlags.NonPublic).CreateDelegate(typeof(Action<Pose>), listener);
        try
        {
            Check(Field<float>(tracking, "locustPositionGain") == 1f && Field<float>(tracking, "locustOrientationGain") == 1f, "walking component defaults to unit translation and angular gains");
            foreach (float gain in new[] { 1f, 0.5f, 2f, 0f })
            {
                tracking.SetLocustGains(gain, gain);
                tracking.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                publish(new Pose(Vector3.zero, Quaternion.identity));
                Call(tracking, "InitializeFicTracData");
                publish(new Pose(new Vector3(0, 2, 0), Quaternion.Euler(0, 90, 0)));
                Call(tracking, "UpdateTransform");
                Check(Vector3.Distance(root.transform.position, new Vector3(2.6f * gain,0,0)) < 0.0001f, "calibrated displacement multiplied exactly by gain " + gain);
                Check(Quaternion.Angle(root.transform.rotation, Quaternion.Euler(0,90 * gain,0)) < 0.03f, "90-degree packet gets exact angular gain without a frame-rate cap: " + gain);
                var position = root.transform.position; var rotation = root.transform.rotation;
                for (int i = 0; i < 8; i++) Call(tracking, "UpdateTransform");
                Check(root.transform.position == position && Quaternion.Angle(root.transform.rotation, rotation) < 0.03f, "repeated render frames cannot keep turning toward an old packet");
            }
            tracking.SetLocustGains(1, 1);
            tracking.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0,45,0));
            publish(new Pose(Vector3.zero, Quaternion.Euler(0,359,0)));
            Call(tracking, "InitializeFicTracData");
            float previous = 359, expected = 45;
            foreach (float yaw in new[] { 1f, 3f, 355f, 10f, 100f, 170f, 181f, 270f, 359f, 1f })
            {
                publish(new Pose(Vector3.zero, Quaternion.Euler(0,yaw,0)));
                Call(tracking, "UpdateTransform");
                expected += Mathf.DeltaAngle(previous, yaw); previous = yaw;
                Check(Quaternion.Angle(root.transform.rotation, Quaternion.Euler(0,expected,0)) < 0.03f, "unit yaw follows wraps/reversals immediately at " + yaw);
            }
            // Emulate simultaneous publisher/render threads with disjoint, recognizable snapshots.
            Pose a = new Pose(new Vector3(1,2,3), Quaternion.Euler(0,10,0));
            Pose b = new Pose(new Vector3(4,5,6), Quaternion.Euler(0,80,0));
            publish(a);
            var worker = new System.Threading.Thread(() => { for (int i = 0; i < 50000; i++) publish((i & 1) == 0 ? a : b); });
            worker.Start();
            bool coherent = true;
            for (int i = 0; i < 10000; i++)
            {
                Pose snapshot = listener.pose;
                coherent &= (snapshot.position == a.position && snapshot.rotation.Equals(a.rotation)) ||
                    (snapshot.position == b.position && snapshot.rotation.Equals(b.rotation));
            }
            worker.Join();
            Check(coherent, "network pose reads never mix position/quaternion from different packets");
        }
        finally { Object.Destroy(root); }
    }

    private static void CheckFrameTiming()
    {
        Call(main, "ApplyFrameTiming", Newtonsoft.Json.Linq.JObject.Parse("{\"targetFrameRate\":75,\"vSyncCount\":0}"));
        Check(Application.targetFrameRate == 75 && QualitySettings.vSyncCount == 0, "explicit FPS target controls software cap");
        Call(main, "ApplyFrameTiming", Newtonsoft.Json.Linq.JObject.Parse("{\"targetFrameRate\":120,\"vSyncCount\":2}"));
        Check(Application.targetFrameRate == 120 && QualitySettings.vSyncCount == 2, "explicit VSync mode is honored");
        Call(main, "ApplyFrameTiming", Newtonsoft.Json.Linq.JObject.Parse("{\"targetFrameRate\":-1}"));
        Check(Application.targetFrameRate == -1 && QualitySettings.vSyncCount == 0, "uncapped mode is explicit");
        Call(main, "ApplyFrameTiming", new Newtonsoft.Json.Linq.JObject());
        Check(Application.targetFrameRate == 120 && QualitySettings.vSyncCount == 0, "missing timing fields use 120 FPS with no VSync override");
    }

    private static void CheckMigration()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.layer = 31;
        ground.transform.position = new Vector3(0,4,0);
        ground.transform.localScale = new Vector3(100,2,100);
        var animal = new GameObject("AGL test");
        try
        {
            Physics.SyncTransforms();
            var migration = animal.AddComponent<MigrationMotion>();
            migration.Configure(2,0,10,1 << 31);
            migration.Advance(1);
            Check(Vector3.Distance(animal.transform.position, new Vector3(0,15,-2)) < 0.001f, "wind FROM north moves south; AGL includes raised ground height");
            animal.transform.position = new Vector3(1000,15,0);
            migration.Advance(1);
            Check(animal.transform.position.y == 15, "missing ground preserves height");
            migration.Configure(0,0,0);
            Check(!migration.enabled, "zero wind and AGL disable migration work");
        }
        finally { Object.Destroy(ground); Object.Destroy(animal); }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < nextFrame) return;
        try
        {
            if (EditorApplication.timeSinceStartup > timeout) throw new Exception("Validation timed out.");
            switch (phase)
            {
                case 0:
                    CheckTextResources();
                    CheckMigration();
                    CheckWalkingGains();
                    CheckTemplateSchemas();
                    var parsed = KannadiConfig.Load(Params("{\"closedLoopPosition\":true,\"closedLoopOrientation\":0.5,\"numberOfRings\":0,\"spacing\":7}"));
                    Check(parsed.closedLoopPosition == 1 && parsed.closedLoopOrientation == 0.5f && parsed.numberOfRings == 0, "legacy boolean/numeric gains and zero rings");
                    Check(Mathf.Approximately(Kannadi.Wrap(351, 100), -49) && Mathf.Approximately(Kannadi.Wrap(-251, 100), 49), "overshoot wrapping");
                    bool rejected = false;
                    try { KannadiConfig.Load(Params("{\"vrConfigs\":[{\"vrIndex\":1,\"watchIndex\":5}]}")); } catch (ArgumentException) { rejected = true; }
                    Check(rejected, "reject invalid watchIndex");
                    Screen.SetResolution(1920, 1080, false);
                    Directory.CreateDirectory(Path.Combine(Application.dataPath, "RunData/Backup"));
                    File.WriteAllText(Path.Combine(Application.dataPath, "RunData/Backup/FlyMetaData.json"), "{\"ExperimenterName\":\"Validation\",\"Comments\":\"Synthetic test session\",\"Flies\":[],\"UsedFlyIDs\":[]}");
                    new GameObject("Validation Logger").AddComponent<MasterDataLogger>();
                    main = new GameObject("Validation Main").AddComponent<MainController>();
                    logDirectory = MasterDataLogger.Instance.directoryPath;
                    Later(); break;
                case 1:
                    CheckFrameTiming();
                    Check(main.GetSystemConfig("VR4").zmqPort == 9874, "four VR1 hardware configurations loaded");
                    Check(main.sequenceSteps[0].sceneName == "Swarm" && main.sequenceSteps[0].duration == 20 && main.sequenceSteps[1].sceneName == "Kannadi", "20-second Swarm entrainment precedes Kannadi");
                    main.sequenceSteps[0].duration = 1000;
                    main.sequenceSteps.Insert(1, new SequenceStep("Swarm", 1000, Params("{\"numberOfLocusts\":3}"), false));
                    main.StartSequence(); Later(); break;
                case 2:
                    Check(SceneManager.GetActiveScene().name == "Swarm", "Swarm sequence dispatch");
                    var spawners = Object.FindObjectsOfType<LocustSpawner>();
                    Check(spawners.Length == 4, "four swarm rigs");
                    Check(GameObject.FindGameObjectsWithTag("SimulatedLocust").Length == 1024, "256 swarm agents per rig, no duplicate Start spawn");
                    foreach (ClosedLoop cl in Object.FindObjectsOfType<ClosedLoop>()) Check(Field<float>(cl, "sphereDiameter") == 2.6f, "system sphere diameter applies to Swarm");
                    Check(spawners.All(p => p.kappa >= 10000 && p.mu == 0 && p.spawnAreaSize == 200), "dense aligned swarm config applied to every rig");
                    Check(Object.FindObjectsOfType<LocustMover>().All(m => Vector3.Dot(m.transform.forward, Vector3.forward) > 0.999f), "entrainment locusts share configured direction");
                    CheckSceneOverview();
                    Check(Object.FindObjectsOfType<AnimateOnMove>().Length >= 1024 && Object.FindObjectsOfType<AnimateOnMove>().All(a => a.GetComponent<Animator>().speed > 0), "moving Swarm locusts animate above configured threshold");
                    CheckOverviewTracking();
                    CheckViewports("Swarm");
                    Check(Object.FindObjectsOfType<Kannadi>().Length == 0, "Swarm contains no mirror controller");
                    int swarmInstance = Object.FindObjectOfType<SwarmController>().GetInstanceID();
                    NextSequenceStep();
                    Check(Object.FindObjectOfType<SwarmController>().GetInstanceID() == swarmInstance, "Swarm sequence advances without reload");
                    Check(GameObject.FindGameObjectsWithTag("SimulatedLocust").Length == 12, "Swarm replaces population on in-scene step");
                    NextSequenceStep(); Later(); break;
                case 3:
                    CheckViewports("Kannadi");
                    rigs = Object.FindObjectsOfType<Kannadi>().OrderBy(r => r.VRIndex).ToArray();
                    Check(rigs.Length == 4 && rigs.All(r => r.Clones.Length == 37), "four Kannadi grids with 37 clones each");
                    Check(GameObject.FindGameObjectsWithTag("SimulatedLocust").Length == 148, "Swarm agents cleaned up at Kannadi transition");
                    var catalog = Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog");
                    Check(catalog != null && catalog.Find("SimulatedLocust") != null && catalog.Find("LocustBand_black") != null, "build-safe single and band prefab references");
                    foreach (GameObject prefab in catalog.prefabs)
                    {
                        Check(prefab != null && prefab.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), "catalog prefab resolves without missing scripts");
                        Check(prefab.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterials.All(m => m != null)), "catalog materials resolve");
                    }
                    var orbit = Object.FindObjectOfType<OverheadCameraController>();
                    typeof(OverheadCameraController).GetField("verticalAngle", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(orbit, 89f);
                    Call(orbit, "UpdateCameraPosition");
                    Check(orbit.transform.position.y - orbit.targetPosition.y > orbit.distance * 0.99f, "orbit remains overhead immediately below 90 degrees");
                    orbit.ResetToSkyView();
                    foreach (Kannadi rig in rigs)
                    {
                        var cl = rig.GetComponent<ClosedLoop>();
                        Check(Field<float>(cl, "sphereDiameter") == 2.6f, "system sphere diameter applies to Kannadi");
                        Check(rig.GetComponent<ZmqListener>().port == 9870 + rig.VRIndex, "per-rig ZMQ config");
                        foreach (Camera camera in rig.GetComponentsInChildren<Camera>())
                        {
                            int own = LayerMask.GetMask("SimulatedLocustsVR" + rig.VRIndex);
                            Check((camera.cullingMask & own) == 0, "default camera excludes own replicas");
                            Check((camera.cullingMask & LayerMask.GetMask("OverheadCameraMarkers")) == 0, "markers excluded from stimulus cameras");
                            Check(camera.targetDisplay == 0, "system target display");
                        }
                    }
                    Check(Object.FindObjectsOfType<SimpleOverheadCamera>().Length == 1, "single overview");
                    Check(GameObject.Find("Overhead Camera Display") != null && Object.FindObjectOfType<OverheadCameraController>().inputRect != null, "overview panel input binding");
                    CheckSceneOverview();
                    CheckAnimationGate();
                    CheckOverviewTracking();
                    CaptureOverviewPanel();
                    hiddenCamera = Field<Camera>(Object.FindObjectOfType<SimpleOverheadCamera>(), "overheadCam");
                    hiddenRenders = 0;
                    Object.FindObjectOfType<SimpleOverheadCamera>().ToggleCamera();
                    Camera.onPostRender += CountHiddenRender;
                    rigs[0].AdvanceStep(Params("{\"numberOfRings\":1,\"hexRadius\":5,\"boundaryLengthX\":200,\"boundaryLengthZ\":120,\"vrConfigs\":[{\"vrIndex\":1,\"watchIndex\":1}]}"));
                    Check(rigs[0].Clones.Length == 6 && rigs[1].Clones.Length == 7, "self-view omits only its own center");
                    rigs[0].transform.SetPositionAndRotation(new Vector3(301, 1, 183), Quaternion.Euler(0, 95, 0));
                    Later(); break;
                case 4:
                    Check(hiddenRenders == 0, "hidden overview submits no camera renders over multiple frames");
                    Camera.onPostRender -= CountHiddenRender;
                    Object.FindObjectOfType<SimpleOverheadCamera>().ToggleCamera();
                    Check(Mathf.Approximately(rigs[0].transform.position.x, -99) && Mathf.Approximately(rigs[0].transform.position.z, -57), "rectangular arena preserves overshoot");
                    foreach (var clone in rigs[0].Clones)
                    {
                        Check(clone.transform.position.x >= -100 && clone.transform.position.x < 100 && clone.transform.position.z >= -60 && clone.transform.position.z < 60, "replicas remain inside rectangular boundary");
                        Check(Quaternion.Angle(clone.transform.rotation, rigs[0].transform.rotation * rigs[0].tilePrefab.transform.rotation) < 0.01f, "replica follows tracked yaw");
                        Check(Mathf.Approximately(clone.transform.position.y, rigs[0].tilePrefab.transform.position.y), "prefab height preserved");
                    }
                    rigs[0].AdvanceStep(Params("{\"numberOfRings\":0,\"vrConfigs\":[{\"vrIndex\":1,\"watchIndex\":2},{\"vrIndex\":2,\"watchIndex\":2}]}"));
                    Check(rigs[0].Clones.Length == 1 && rigs[1].Clones.Length == 0, "zero-ring peer and self views");
                    Camera peer = rigs[0].GetComponentInChildren<Camera>();
                    Check((peer.cullingMask & LayerMask.GetMask("SimulatedLocustsVR2")) != 0 && (peer.cullingMask & LayerMask.GetMask("SimulatedLocustsVR3")) == 0, "watchIndex selects the peer layer");
                    Check((peer.cullingMask & 1) != 0, "watchIndex preserves the shared environment");
                    rigs[0].AdvanceStep(Params("{\"numberOfRings\":-1}"));
                    Check(rigs.All(r => r.Clones.Length == 0), "negative rings remove all replicas");
                    rigs[0].AdvanceStep(Params("{\"numberOfRings\":0,\"kannadiTilePrefab\":\"LocustBand_black\",\"animateOnMove\":true,\"animationSpeedThreshold\":0.5}"));
                    Later(); break;
                case 5:
                    Check(rigs.All(r => r.Clones.Length == 1 && r.Clones[0].GetComponentsInChildren<Renderer>().Length > 0), "band prefabs spawn visible members");
                    Check(rigs.SelectMany(r => r.Clones[0].GetComponentsInChildren<DirectionalMovement>()).All(m => !m.enabled), "mirrored bands have no independent movement");
                    Check(rigs.SelectMany(r => r.Clones[0].GetComponentsInChildren<Animator>()).All(a => a.GetComponent<AnimateOnMove>() != null && a.GetComponent<AnimateOnMove>().enabled), "delayed band members inherit animation speed gating");
                    expectedPosition = rigs[0].Clones[0].transform.GetChild(0).position + new Vector3(3, 0, 4);
                    rigs[0].transform.position += new Vector3(3, 0, 4);
                    Later(); break;
                case 6:
                    Check(Vector3.Distance(expectedPosition, rigs[0].Clones[0].transform.GetChild(0).position) < 0.01f, "band members follow tracked translation");
                    main.sequenceSteps.Clear(); main.executionOrder.Clear(); main.sequenceSteps.Add(new SequenceStep("Kannadi", 1000, Params("{\"numberOfRings\":1,\"spacing\":9}"), true));
                    main.sequenceSteps.Add(new SequenceStep("Kannadi", 1000, Params("{\"numberOfRings\":0}"), false));
                    main.sequenceSteps.Add(new SequenceStep("Choice", 1000, Params("{\"configFile\":\"Choice_empty.json\"}"), true));
                    main.sequenceSteps.Add(new SequenceStep("Choice_TwoTreeIndia_nopath_path", 1000, Params("{\"configFile\":\"BinaryChoiceIndia_flip_noflip.json\"}"), true));
                    main.sequenceSteps.Add(new SequenceStep("Choice_desync", 1000, Params("{\"design\":\"dynamicSequenceDesign.json\"}"), true));
                    main.StartSequence(); Later(); break;
                case 7:
                    rigs = Object.FindObjectsOfType<Kannadi>();
                    Check(rigs.Length == 4 && rigs.All(r => r.Clones.Length == 7), "Kannadi reload through sequence config");
                    CheckCameraReconfiguration();
                    Check(Object.FindObjectsOfType<SimpleOverheadCamera>().Length == 1, "Kannadi reload gets one overview");
                    var tracking = rigs[0].GetComponent<ClosedLoop>();
                    var listener = rigs[0].GetComponent<ZmqListener>();
                    tracking.SetLocustGains(0.5f, 1f); tracking.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, 90, 0));
                    typeof(ZmqListener).GetProperty("pose").SetValue(listener, new Pose(Vector3.zero, Quaternion.identity));
                    Call(tracking, "InitializeFicTracData");
                    typeof(ZmqListener).GetProperty("pose").SetValue(listener, new Pose(new Vector3(0, 2, 0), Quaternion.identity));
                    Call(tracking, "UpdateTransform");
                    Check(Vector3.Distance(tracking.transform.position, new Vector3(0, 0, -1.3f)) < 0.01f, "numeric translation gain and sphere diameter respect base rotation");
                    expectedPosition = tracking.transform.position;
                    Call(tracking, "Update");
                    Check(tracking.transform.position == expectedPosition, "missing tracking pose cannot move animal");
                    typeof(ZmqListener).GetProperty("HasPose").SetValue(listener, true);
                    typeof(ZmqListener).GetField("lastPoseTicksUtc", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(listener, DateTime.UtcNow.AddSeconds(-30).Ticks);
                    Call(tracking, "Update");
                    Check(tracking.transform.position == expectedPosition, "stale tracking pose cannot move animal");
                    tracking.ResetPositionAndRotation();
                    Check(tracking.transform.position == Vector3.zero && Quaternion.Angle(tracking.transform.rotation, Quaternion.Euler(0, 90, 0)) < 0.01f, "reset works with stale tracking");
                    typeof(ZmqListener).GetProperty("HasPose").SetValue(listener, false);
                    tracking.transform.position = Vector3.one;
                    tracking.ResetPositionAndRotation();
                    Check(tracking.transform.position == Vector3.zero, "reset works with absent tracking");
                    tracking.ToggleClosedLoopPosition();
                    Check(!Field<bool>(tracking, "closedLoopPosition"), "position toggle is enabled");
                    tracking.ToggleClosedLoopPosition();
                    tracking.ToggleClosedLoopOrientation();
                    Check(!Field<bool>(tracking, "closedLoopOrientation"), "orientation toggle is enabled");
                    tracking.transform.rotation = Quaternion.Euler(0, 45, 0);
                    tracking.ToggleClosedLoopOrientation();
                    Check(!Field<bool>(tracking, "_isInitialized"), "resuming orientation without tracking waits for a new baseline");
                    CheckModernInput(tracking);
                    rigInstance = rigs[0].GetInstanceID();
                    NextSequenceStep();
                    Check(rigs[0].GetInstanceID() == rigInstance && rigs.All(r => r.Clones.Length == 1), "Kannadi sequence advances without reload");
                    CheckViewports("Kannadi in-scene sequence step");
                    NextSequenceStep(); Later(); break;
                case 8:
                    Check(SceneManager.GetActiveScene().name == "Choice" && Object.FindObjectOfType<ChoiceController>() != null, "Choice sequence dispatch");
                    CheckViewports("Choice");
                    Check(Object.FindObjectsOfType<Kannadi>().Length == 0, "Choice has no mirror components");
                    NextSequenceStep(); Later(); break;
                case 9:
                    Check(SceneManager.GetActiveScene().name == "Choice_TwoTreeIndia_nopath_path", "JuliusTree experiment sequence dispatch");
                    CheckViewports("JuliusTree two-tree scene");
                    NextSequenceStep(); Later(); break;
                case 10:
                    Check(SceneManager.GetActiveScene().name == "Choice_desync" && Object.FindObjectOfType<DynamicSequenceController>() != null, "dynamic sequence dispatch");
                    CheckViewports("JuliusTree dynamic sequence");
                    main.HandleEscape(); Later(); break;
                case 11:
                    Check(Object.FindObjectsOfType<SimpleOverheadCamera>().Length == 0 && GameObject.Find("Overhead Camera Canvas") == null, "overview resources cleaned up on exit");
                    Check(Directory.GetFiles(logDirectory, "*Kannadi*Clones.csv.gz").Length >= 8, "Kannadi clone logging across reloads");
                    Check(!Resources.FindObjectsOfTypeAll<EditorWindow>().Any(w => w.GetType().Name == "TMP_PackageResourceImporterWindow"), "TMP import prompt does not reopen");
                    Check(Object.FindObjectsOfType<fps>().Length == 1, "FPS display survives sequence exit");
                    Check(Object.FindObjectsOfType<UnityEngine.EventSystems.StandaloneInputModule>().Length == 0, "Control uses the Input System UI module");
                    Check(Object.FindObjectsOfType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().Length > 0, "Control UI has an active new input module");
                    main.sequenceSteps = new List<SequenceStep> {
                        new SequenceStep("Optomotor", 1000, Params("{\"configFile\":\"Templates/optomotor.template.json\"}")),
                        new SequenceStep("Choice", 1000, Params("{\"configFile\":\"Templates/migration.template.json\"}")),
                        new SequenceStep("Choice_desync", 1000, Params("{\"design\":\"Templates/dynamic-choice.template.json\"}"))
                    };
                    main.StartSequence(); Later(); break;
                case 12:
                    Check(SceneManager.GetActiveScene().name == "Optomotor", "Optomotor template dispatch");
                    Check(Object.FindObjectOfType<DrumRotator>() != null && Field<bool>(Object.FindObjectOfType<DrumRotator>(), "isRotating"), "Optomotor template starts the drum");
                    CheckViewports("Optomotor");
                    NextSequenceStep(); Later(); break;
                case 13:
                    Check(Object.FindObjectsOfType<MigrationMotion>().Length == 4, "migration Choice template configures four rigs");
                    Check(Object.FindObjectsOfType<MigrationMotion>().All(m => m.AglHeight == 100 && m.enabled), "AGL template values reach runtime");
                    Object.FindObjectOfType<ChoiceController>().AdvanceStep(Params("{\"configFile\":\"Templates/choice.template.json\"}"));
                    Check(Object.FindObjectsOfType<MigrationMotion>().All(m => !m.enabled), "normal Choice step turns off prior migration settings");
                    var choices = Field<Dictionary<string, GameObject>>(Object.FindObjectOfType<ChoiceController>(), "prefabDict");
                    Check(choices.ContainsKey("tree01") && choices.ContainsKey("LocustBand_black"), "Choice templates reference registered prefabs");
                    Object.FindObjectOfType<ChoiceController>().AdvanceStep(Params("{\"configFile\":\"Templates/choice-band.template.json\"}"));
                    Later(); break;
                case 14:
                    Check(Object.FindObjectsOfType<BandSpawner>().Length == 4, "band template spawns four registered bands");
                    Check(Object.FindObjectsOfType<DirectionalMovement>().Length >= 128, "band template creates its moving individuals");
                    int bandLayers = LayerMask.GetMask("SimulatedLocustsVR1", "SimulatedLocustsVR2", "SimulatedLocustsVR3", "SimulatedLocustsVR4");
                    foreach (ClosedLoop rig in Object.FindObjectsOfType<ClosedLoop>())
                    {
                        int ownLayer = LayerMask.GetMask("SimulatedLocusts" + rig.name);
                        Check(ownLayer != 0 && rig.GetComponentsInChildren<Camera>().All(c => (c.cullingMask & bandLayers) == ownLayer), "Choice band cameras see only their rig population");
                    }
                    NextSequenceStep(); Later(); break;
                case 15:
                    Check(SceneManager.GetActiveScene().name == "Choice_desync", "full dynamic template dispatch");
                    var dynamicController = Object.FindObjectOfType<DynamicSequenceController>();
                    Type stepType = typeof(DynamicSequenceController).GetNestedType("Step", BindingFlags.NonPublic);
                    object disabledStep = JsonConvert.DeserializeObject("{\"closedLoopPosition\":false,\"closedLoopOrientation\":false}", stepType);
                    Call(dynamicController, "ApplyClosedLoopFlags", "VR1", disabledStep);
                    var disabledRig = Object.FindObjectsOfType<ClosedLoop>().First(c => c.name == "VR1");
                    Check(!Field<bool>(disabledRig, "closedLoopPosition") && !Field<bool>(disabledRig, "closedLoopOrientation"), "dynamic step respects both tracking flags false");
                    main.HandleEscape(); Later(); break;
                case 16:
                    Finish(); break;
            }
        }
        catch (Exception error) { failures.Add(error.ToString()); Finish(); }
    }
    private static void Finish()
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        SceneManager.sceneLoaded -= DisableLiveInputs;
        Camera.onPostRender -= CountHiddenRender;
        File.WriteAllText(Path.Combine(Application.dataPath, "../kannadi-validation.json"), JsonConvert.SerializeObject(new { checks, failures, editorMessages }, Formatting.Indented));
        Debug.Log($"KANNADI VALIDATION: {checks} checks, {failures.Count} failures");
        EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }
}
#endif
