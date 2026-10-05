#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HeadingTelemetryValidation
{
    private const string Key = "HeadingTelemetryValidation.Active";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static int checks, phase, nextFrame, originalRigId;
    private static double deadline, phaseStart;
    private static SubscriberSocket subscriber;
    private static PublisherSocket sensor;
    private static JObject received;
    private static MainController main;
    private static ClosedLoop[] rigs;
    private static readonly List<string> failures = new List<string>();
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Flags).GetValue(target);
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    private static bool Angle(double actual, double expected) => Math.Abs(Mathf.DeltaAngle((float)actual, (float)expected)) < .01;

    static HeadingTelemetryValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            phase = 0; nextFrame = Time.frameCount + 3; deadline = EditorApplication.timeSinceStartup + 100;
            EditorApplication.update += Tick;
            SceneManager.sceneLoaded += DisableInputs;
            Application.logMessageReceived += OnLog;
        };
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (message.StartsWith("[Validation expected]") || stack.Contains("UnityEditor.Search.SearchDatabase") || message == "No backup data file found.") return;
        failures.Add(message + "\n" + stack);
    }
    private static void DisableInputs(Scene scene, LoadSceneMode mode)
    {
        foreach (ZmqListener input in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) input.enabled = false;
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY")))
            throw new Exception("Use a disposable Unity validation copy.");
        Fixture();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    private static void Fixture()
    {
        var system = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Templates/system_config.template.json")));
        system["telemetry"] = JObject.FromObject(new { enabled = true, bindAddress = "127.0.0.1", port = 19880, rateHz = 30 });
        system["overheadCamera"]["enabled"] = false;
        foreach (JObject rig in system["configs"])
        {
            int index = int.Parse(rig["vrId"].ToString().Substring(2));
            rig["zmqPort"] = 19870 + index;
            rig["closedLoopMode"] = index == 1 ? "Kinefly" : "FicTrac";
        }
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "system_config.json"), system.ToString());
        var config = new { numberOfLocusts = 2, mu = 90, kappa = 100000, locustSpeed = 0,
            closedLoopPosition = 0, closedLoopOrientation = 0, autopilotEnabled = false,
            headingReference = new { enabled = true, windowSeconds = 1.0 },
            vrConfigs = Enumerable.Range(1, 4).Select(i => new { vrIndex = i,
                initialPosition = new { x = i * 3, y = 1, z = 0 }, initialRotation = new { x = 0, y = (i - 1) * 90, z = 0 } }) };
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "heading-validation.json"), JsonConvert.SerializeObject(config));
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json"), JsonConvert.SerializeObject(new {
            autoStart = true, loop = false, sequences = new[] {
                new { sceneName = "Swarm", duration = 1000, gain = 2.5, reloadScene = true, parameters = new { configFile = "heading-validation.json" } },
                new { sceneName = "Swarm", duration = 1000, gain = -2.0, reloadScene = false, parameters = new { configFile = "heading-validation.json" } }
            } }));
    }
    private static void UnitChecks()
    {
        var wrap = new CircularHeadingWindow(); wrap.Add(0, 359, 3); wrap.Add(1, 1, 3);
        Check(Angle(wrap.Read().meanHeadingDegrees, 0), "359/1 circular mean crosses zero, not 180");
        Check(Math.Abs(wrap.Read().resultantLength - Math.Cos(Math.PI / 180)) < 1e-10, "resultant length follows unit-vector mean");
        wrap.Add(5, 90, 3);
        Check(Angle(wrap.Read().meanHeadingDegrees, 90) && wrap.Read().sampleCount == 1, "trailing window evicts earlier headings");
        var spin = new CircularHeadingWindow();
        for (int i = 0; i < 360; i++) spin.Add(i / 360.0, i, 3);
        Check(spin.Read().resultantLength < 1e-12 && !double.IsNaN(spin.Read().meanHeadingDegrees), "full circle still supplies a finite angle and near-zero r");
        new HeadingReferenceConfig { enabled = false, windowSeconds = -1 }.Validate();
        bool threw = false; try { new HeadingReferenceConfig { enabled = true, windowSeconds = double.NaN }.Validate(); } catch (ArgumentException) { threw = true; }
        Check(threw, "enabled invalid observation rejected");
        Check(new HeadingReferenceConfig().enabled == false && new HeadingReferenceConfig().windowSeconds == 180, "opt-in and default three-minute window");
        var config = KannadiConfig.Load(new Dictionary<string, object> { ["headingReference"] = JObject.FromObject(new { enabled = true, windowSeconds = 1 }) });
        Check(config.headingReference == null, "inline sequence cannot enable heading reference");
        config = KannadiConfig.Load(new Dictionary<string, object> { ["configFile"] = "heading-validation.json", ["headingReference"] = JObject.FromObject(new { enabled = false }) });
        Check(config.headingReference.enabled, "inline sequence cannot override experiment heading reference");
        var root = new GameObject("Generic stimulus reference test");
        var reference = root.AddComponent<StimulusHeadingReference>(); reference.enabled = false;
        int calls = 0;
        reference.Begin(null, result => { Check(result == null, "omission presents with absolute angles"); calls++; });
        Check(calls == 1 && !reference.IsObserving, "disabled has no pre-stimulus delay");
        reference.Begin(new HeadingReferenceConfig { enabled = true, windowSeconds = 1 }, result => calls++);
        double now = Time.timeAsDouble;
        root.transform.SetPositionAndRotation(new Vector3(3, 4, 5), Quaternion.Euler(0, 33, 0));
        reference.Tick(now + .2);
        Check(reference.IsObserving && calls == 1, "observes without early presentation");
        reference.Tick(now + 1.01);
        Check(calls == 2 && Angle(reference.Result.Resolve(90), 123), "generic per-rig relative angle resolves");
        Vector3 before = root.transform.position;
        root.transform.rotation = Quaternion.Euler(0, 200, 0); reference.Tick(now + 2);
        Check(calls == 2 && Angle(reference.Result.meanHeadingDegrees, 33) && before == root.transform.position, "reference freezes; no reset or continued alignment");
        var target = new GameObject("Arbitrary stimulus"); target.transform.position = before + Vector3.forward * 10;
        StimulusHeadingReference.RotateStimulus(target.transform, before, reference.Result);
        Check(Angle(Mathf.Atan2((target.transform.position - before).x, (target.transform.position - before).z) * Mathf.Rad2Deg, 33), "non-swarm transform adapter rotates around presentation pivot");
        reference.Begin(new HeadingReferenceConfig { enabled = true, windowSeconds = 1 }, _ => calls++); reference.Cancel(); reference.Tick(now + 4);
        Check(calls == 2, "cancellation prevents obsolete stimulus callback");
        Object.DestroyImmediate(target); Object.DestroyImmediate(root);
    }
    private static void Later(int next) { phase = next; nextFrame = Time.frameCount + 2; phaseStart = EditorApplication.timeSinceStartup; }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Validation timed out at phase " + phase);
            if (Time.frameCount < nextFrame) return;
            switch (phase)
            {
                case 0:
                    UnitChecks(); SceneManager.LoadScene("ControlScene"); Later(1); break;
                case 1:
                    main = MainController.Instance;
                    if (SceneManager.GetActiveScene().name != "Swarm" || main.VRClosedLoops.Count < 4) return;
                    rigs = main.VRClosedLoops.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToArray();
                    originalRigId = rigs[0].GetInstanceID();
                    Check(main.AssessmentPending, "MainController holds duration during assessment");
                    Check(Math.Abs(main.RemainingStepSeconds - 1000) < .01, "presentation duration not spent on assessment");
                    Check(Object.FindObjectsByType<LocustMover>(FindObjectsSortMode.None).Length == 0, "no swarm flashes during observation");
                    subscriber = new SubscriberSocket(); subscriber.Options.Linger = TimeSpan.Zero;
                    subscriber.Connect("tcp://127.0.0.1:19880"); subscriber.Subscribe("matrex.telemetry.v1");
                    sensor = new PublisherSocket(); sensor.Options.Linger = TimeSpan.Zero; sensor.Bind("tcp://127.0.0.1:19871");
                    rigs[0].GetComponent<ZmqListener>().enabled = true;
                    Later(2); break;
                case 2:
                    sensor.SendMoreFrame("pose").SendFrame("{\"x\":1.25,\"y\":-2.5,\"z\":3.75,\"pitch\":-0.1,\"yaw\":-0.35,\"roll\":0.2}");
                    NetMQMessage packet = new NetMQMessage();
                    if (subscriber.TryReceiveMultipartMessage(TimeSpan.Zero, ref packet))
                    {
                        Check(packet.FrameCount == 2 && packet[0].ConvertToString() == "matrex.telemetry.v1", "wire topic and JSON two-frame contract");
                        received = JObject.Parse(packet[1].ConvertToString());
                    }
                    if (main.AssessmentPending || received == null || !rigs[0].GetComponent<ZmqListener>().HasPose) return;
                    Check(received.Value<int>("schemaVersion") == 1 && received["rigs"].Count() == 4, "all four rigs in a versioned snapshot");
                    for (int i = 0; i < 4; i++)
                    {
                        var reference = rigs[i].GetComponent<StimulusHeadingReference>();
                        Check(Angle(reference.Result.meanHeadingDegrees, i * 90), "independent VR reference " + i);
                        Check(Angle(rigs[i].GetComponent<LocustSpawner>().mu, i * 90 + 90), "relative swarm mu " + i);
                        Check(rigs[i].transform.position == new Vector3((i + 1) * 3, 1, 0) && Angle(rigs[i].transform.eulerAngles.y, i * 90), "onset preserves rig pose " + i);
                    }
                    Check(Object.FindObjectsByType<LocustMover>(FindObjectsSortMode.None).Length == 8, "stimuli spawn only after complete assessment");
                    var telemetry = Object.FindFirstObjectByType<ExperimentTelemetry>();
                    var snapshot = JObject.FromObject(telemetry.Capture(999));
                    var first = snapshot["rigs"][0];
                    Check(first["pose"].Count() == 6, "full six-component pose");
                    Check(first["input"]["latest"].Value<double>("yaw") < -.349 && first["input"]["latest"].Value<double>("x") == 1.25, "raw signed wire values preserved");
                    Check(first["input"].Value<string>("source") == "Kinefly" && first["closedLoop"].Value<double>("yawGain") == 2.5, "source and actual gains");
                    Check(snapshot["system"].Value<int>("trialNumber") == 1 && snapshot["system"].Value<string>("sceneName") == "Swarm", "trial and scene state");
                    File.WriteAllText(Path.Combine(Application.dataPath, "../telemetry-example.json"), snapshot.ToString());
                    Check(File.ReadAllLines(Path.Combine(MasterDataLogger.Instance.directoryPath, "heading_reference.jsonl")).Length == 4, "onset context saved separately for every rig");
                    rigs[0].transform.position = new Vector3(17, 2, 23); rigs[0].transform.rotation = Quaternion.Euler(0, 45, 0);
                    Set(main, "timer", 0f); Call(main, "ManageTimerAndTransitions");
                    Check(main.currentStep == 1 && rigs[0].GetInstanceID() == originalRigId, "in-place trial retains rig instance");
                    Check(rigs[0].transform.position == new Vector3(17, 2, 23) && Angle(rigs[0].transform.eulerAngles.y, 45), "next assessment does not reset pose");
                    Check(Object.FindObjectsByType<LocustMover>(FindObjectsSortMode.None).Length == 0, "previous stimulus hidden during next assessment");
                    Later(3); break;
                case 3:
                    if (main.AssessmentPending) return;
                    Check(Angle(rigs[0].GetComponent<StimulusHeadingReference>().Result.meanHeadingDegrees, 45), "next assessment uses recent heading only");
                    Check(Angle(rigs[0].GetComponent<LocustSpawner>().mu, 135), "relative angle does not accumulate previous reference");
                    ChoiceChecks();
                    var hud = Object.FindFirstObjectByType<RuntimeStatusOverlay>();
                    hud.SetVisible(false);
                    Debugger.CurrentLogLevel = 0;
                    Debugger.Log("[Validation expected] player diagnostic survives log level zero", 1);
                    Later(4); break;
                case 4:
                    var overlay = Object.FindFirstObjectByType<RuntimeStatusOverlay>();
                    Check(overlay.Visible && overlay.BuildText().Contains("player diagnostic"), "new error reopens HUD and displays message");
                    Check(RuntimeStatusOverlay.RecentErrors().Any(e => e.message.Contains("player diagnostic")), "error enters outbound bounded history");
                    CheckTab(overlay);
                    RenderOverlay(overlay);
                    Later(5); break;
                case 5:
                    if (Time.frameCount < nextFrame + 3) return;
                    Check(failures.Count == 0, "Unexpected runtime errors: " + string.Join("\n", failures));
                    main.StopSequence(); SceneManager.LoadScene("ControlScene"); Later(6); break;
                case 6:
                    Check(failures.Count == 0, "Clean scene shutdown: " + string.Join("\n", failures));
                    Finish(null); break;
            }
        }
        catch (Exception error) { Finish(error.ToString()); }
    }
    // Synthetic editor events must enter the player input phase, regardless of window focus.
    private static void UpdateTestInput() => typeof(InputSystem)
        .GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(InputUpdateType) }, null)
        .Invoke(null, new object[] { InputUpdateType.Dynamic });

    private static void CheckTab(RuntimeStatusOverlay overlay)
    {
        var settings = InputSystem.settings;
        var oldEditor = settings.editorInputBehaviorInPlayMode;
        var oldBackground = settings.backgroundBehavior;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard = InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        try
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Tab));
            UpdateTestInput();
            Call(overlay, "Update");
            Check(!overlay.Visible, "Tab hides visible overlay via Input System");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); UpdateTestInput();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Tab)); UpdateTestInput();
            Call(overlay, "Update");
            Check(overlay.Visible, "Tab restores hidden overlay via Input System");
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            settings.editorInputBehaviorInPlayMode = oldEditor;
            settings.backgroundBehavior = oldBackground;
        }
    }
    private static void RenderOverlay(RuntimeStatusOverlay overlay)
    {
        Canvas canvas = overlay.GetComponentInChildren<Canvas>();
        var root = new GameObject("HUD validation camera");
        var camera = root.AddComponent<Camera>(); camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .12f, .18f);
        camera.cullingMask = 1 << 5;
        foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        var target = new RenderTexture(1600, 900, 24); target.Create(); camera.targetTexture = target;
        var previous = RenderTexture.active;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var capture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        capture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); capture.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../heading-status-overlay.png"), capture.EncodeToPNG());
        Check(overlay.GetComponentInChildren<Text>().preferredHeight < 676, "full status and latest error fit within panel");
        RenderTexture.active = previous; camera.targetTexture = null;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
        Object.DestroyImmediate(capture); Object.DestroyImmediate(target); Object.DestroyImmediate(root);
    }
    private static void ChoiceChecks()
    {
        // Exercise the actual non-swarm adapter with an ordinary Choice prefab.
        var host = new GameObject("Choice reference test"); host.SetActive(false);
        var controller = host.AddComponent<ChoiceController>();
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.name = "ReferenceCube";
        controller.prefabs = new[] { prefab }; controller.materials = Array.Empty<Material>();
        host.SetActive(true);
        var config = new SceneConfig { objects = new[] { new SceneObject { type = "ReferenceCube", position = new Position { radius = 10, angle = 90 } } } };
        var result = new HeadingReferenceResult { meanHeadingDegrees = 45 };
        Call(controller, "SpawnRelativeObjects", config, rigs[0], result);
        Transform spawned = Get<Transform>(controller, "spawnedObjectsRoot").GetChild(0);
        Vector3 delta = spawned.position - rigs[0].transform.position;
        Check(Angle(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 135), "Choice polar angle uses frozen per-rig zero");
        Check(spawned.gameObject.layer == LayerMask.NameToLayer("SimulatedLocustsVR1"), "Choice stimulus isolated to its rig");
        Object.DestroyImmediate(Get<Transform>(controller, "spawnedObjectsRoot").gameObject);
        Object.DestroyImmediate(host); Object.DestroyImmediate(prefab);
    }
    private static void Finish(string error)
    {
        subscriber?.Dispose(); sensor?.Dispose(); subscriber = null; sensor = null;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        SceneManager.sceneLoaded -= DisableInputs;
        SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Application.dataPath, "../heading-telemetry-validation.json"), JsonConvert.SerializeObject(new {
            checks, failure = error, unexpectedErrors = failures, unity = Application.unityVersion,
            coverage = "Circular statistics, disabled/default/invalid/sequence config, generic and Choice adapters, real Swarm lifecycle, per-rig onset/no reset, repeat trial, PUB/SUB tracking and telemetry, raw units, player errors and HUD"
        }, Formatting.Indented));
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
#endif
