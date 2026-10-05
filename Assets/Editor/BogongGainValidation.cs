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
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BogongGainValidation
{
    private const string Key = "BogongGainValidation.Active";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static int checks, phase;
    private static double deadline;
    private static string failure;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Flags).GetValue(target);
    private static Type Reference(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("BogongHistorical." + name)).First(t => t != null);

    static BogongGainValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            { deadline = EditorApplication.timeSinceStartup + 180; EditorApplication.update += Validate; }
        };
        SceneManager.sceneLoaded += (scene, mode) => {
            if (!SessionState.GetBool(Key, false)) return;
            foreach (var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) listener.enabled = false;
        };
    }

    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Use a disposable validation copy.");
        Reference("ClosedLoop"); // tools/prepare_bogong_gain_reference.py installs the verified historical source.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static void Feed(ZmqListener listener, float x, float y, float yaw)
    {
        Type packetType = typeof(ZmqListener).GetNestedType("ZmqMessage", BindingFlags.NonPublic);
        object packet = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(new { x, y, z = 0, pitch = .21f, yaw, roll = -.17f }), packetType);
        Call(listener, "UpdatePose", packet); // Test the real JSON-message-to-tracking path, including signed raw radians.
    }

    private static void CompareHistorical()
    {
        var modernRoot = new GameObject("Restored Bogong");
        var listener = modernRoot.AddComponent<ZmqListener>(); listener.enabled = false;
        var tracking = modernRoot.AddComponent<ClosedLoop>(); tracking.enabled = false;
        var oldRoot = new GameObject("BogongAustralia ef8a68b");
        var oldListener = (MonoBehaviour)oldRoot.AddComponent(Reference("ZmqListener")); oldListener.enabled = false;
        var old = (MonoBehaviour)oldRoot.AddComponent(Reference("ClosedLoop")); old.enabled = false;
        FieldInfo clock = Reference("ReferenceClock").GetField("deltaTime");
        bool logging = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = false;
        try
        {
            Set(tracking, "sphereDiameter", 2.5f); Set(old, "sphereDiameter", 2.5f);
            Call(tracking, "Start"); Call(old, "Start");
            foreach (float gain in new[] { -10f, -1f, 0f, .5f, 1f, 2.5f, 10f, 40f })
            foreach (float offset in new[] { -.3f, 0f, .45f })
            foreach (float dt in new[] { 1f/30, 1f/60, 1f/120, .0137f })
            foreach (int mode in new[] { 0, 1, 2 }) // historical yaw, standard delta, force/torque placeholder
            {
                Vector3 start = new Vector3(2, 7, -3);
                Quaternion rotation = Quaternion.Euler(12, 73, -8);
                tracking.SetPositionAndRotation(start, rotation); Call(old, "SetPositionAndRotation", start, rotation);
                tracking.SetYawMode(mode == 0); Call(old, "SetYawMode", mode == 0);
                tracking.SetForceMode(mode == 2); Call(old, "SetForceMode", mode == 2);
                tracking.SetYawGain(gain); Call(old, "SetYawGain", gain);
                tracking.SetYawDCOffset(offset); Call(old, "SetYawDCOffset", offset);
                tracking.SetForceGain(.75f); Call(old, "SetForceGain", .75f);
                tracking.SetTorqueGain(-.6f); Call(old, "SetTorqueGain", -.6f);
                Feed(listener, -.4f, .8f, -.25f);
                Set(oldListener, "position", new Vector3(-.4f, .8f, 0)); Set(oldListener, "rawRotation", new Vector3(.21f, -.25f, -.17f));
                Call(tracking, "InitializeFicTracData"); Call(old, "InitializeFicTracData");
                for (int frame = 0; frame < 96; frame++)
                {
                    // Constant input, changing input, sign changes, raw +/-2pi, and packets held across frames.
                    float yaw = frame < 24 ? .3f : frame < 48 ? -.6f : frame < 72 ? 6.1f : -.2f + .7f * Mathf.Sin(frame * .3f);
                    float x = frame * .01f, y = frame * -.02f;
                    if (frame % 4 == 0)
                    {
                        Feed(listener, x, y, yaw);
                        Set(oldListener, "position", new Vector3(x, y, 0)); Set(oldListener, "rawRotation", new Vector3(.21f, yaw, -.17f));
                    }
                    bool enabled = frame < 36 || frame >= 44;
                    tracking.SetClosedLoopOrientation(enabled); Call(old, "SetClosedLoopOrientation", enabled);
                    tracking.SetClosedLoopPosition(frame % 9 != 0); Call(old, "SetClosedLoopPosition", frame % 9 != 0);
                    if (frame == 58)
                    {
                        tracking.ResetPositionAndRotation(); Call(old, "ResetPositionAndRotation");
                        Call(tracking, "InitializeFicTracData"); Call(old, "InitializeFicTracData");
                    }
                    float frameSeconds = dt == .0137f ? dt * (frame % 3 == 0 ? .6f : frame % 3 == 1 ? 1.1f : 1.7f) : dt;
                    clock.SetValue(null, frameSeconds);
                    Call(tracking, "AdvanceTracking", frameSeconds); Call(old, "UpdateTransform");
                    Check(modernRoot.transform.position.Equals(oldRoot.transform.position), "Position differs from historical code at frame " + frame);
                    Check(modernRoot.transform.rotation.Equals(oldRoot.transform.rotation), "Rotation differs from historical code: mode=" + mode + " gain=" + gain + " offset=" + offset + " dt=" + dt + " frame=" + frame);
                    if (mode != 2)
                        Check(tracking.GetLastYawInput().Equals(Call(old, "GetLastYawInput")) && tracking.GetLastYawOutput().Equals(Call(old, "GetLastYawOutput")), "Historical yaw input/output differs.");
                }
            }
            // Real Update must retain the held-input behavior, including a stale packet.
            tracking.SetPositionAndRotation(Vector3.zero, Quaternion.identity); tracking.SetClosedLoopOrientation(true);
            tracking.SetYawMode(true); tracking.SetForceMode(false); tracking.SetYawGain(10); tracking.SetYawDCOffset(0);
            Feed(listener, 0, 0, .3f); Call(tracking, "InitializeFicTracData");
            Set(listener, "lastPoseTicksUtc", DateTime.UtcNow.AddSeconds(-10).Ticks);
            Quaternion before = tracking.transform.rotation;
            Call(tracking, "Update");
            Check(!before.Equals(tracking.transform.rotation), "Historical Kinefly must continue integrating a held stale packet.");
        }
        finally
        {
            Debug.unityLogger.logEnabled = logging;
            Object.DestroyImmediate(modernRoot); Object.DestroyImmediate(oldRoot);
        }
    }

    private static void PrepareSequence()
    {
        var system = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Templates/system-config-six-cameras.template.json")));
        foreach (var rig in system["configs"]) rig["closedLoopMode"] = "Kinefly";
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "system_config.json"), system.ToString());
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "bogong-gain-validation.json"), "{\"closedLoopOrientation\":true,\"closedLoopPosition\":false}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json"), "{\"autoStart\":true,\"loop\":false,\"sequences\":[{\"sceneName\":\"Choice\",\"duration\":1000,\"gain\":10,\"parameters\":{\"configFile\":\"bogong-gain-validation.json\"}},{\"sceneName\":\"Choice\",\"duration\":1000,\"gain\":-2.5,\"reloadScene\":false,\"parameters\":{\"configFile\":\"bogong-gain-validation.json\"}},{\"sceneName\":\"Choice\",\"duration\":1000,\"parameters\":{\"configFile\":\"bogong-gain-validation.json\"}}]}");
        SceneManager.LoadScene("ControlScene");
    }

    private static void Validate()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Gain regression timed out.");
            if (phase == 0)
            {
                CompareHistorical();
                Check(JsonConvert.DeserializeObject<SequenceItem>("{}").gain == 1, "Omitted sequence gain defaults to historical 1.");
                PrepareSequence(); phase = 1; return;
            }
            if (SceneManager.GetActiveScene().name != "Choice" || MainController.Instance == null) return;
            var main = MainController.Instance;
            var trackers = Object.FindObjectsByType<ClosedLoop>(FindObjectsSortMode.None);
            if (trackers.Length != 4) return;
            if (phase == 1)
            {
                Check(trackers.All(t => t.UsesBogongInput && t.GetYawGain() == 10), "Real sequence loads gain 10 on all four Kinefly rigs.");
                foreach (var tracking in trackers)
                {
                    // Calling the walking configuration helper must not replace an explicitly selected Kinefly feed.
                    tracking.SetLocustGains(0, .5f);
                    Check(tracking.UsesBogongInput && tracking.GetYawGain() == 10, "Kinefly remains historical under Swarm/Kannadi gain configuration.");
                    tracking.SetYawDCOffset(.125f);
                    Get<Dictionary<string, float>>(main, "persistentDCOffsets")[tracking.name] = .125f;
                    var logger = tracking.GetComponent<DataLogger>();
                    Feed(tracking.GetComponent<ZmqListener>(), 0, 0, -.2f);
                    Call(logger, "PrepareLogData");
                    var values = Get<Dictionary<string, object>>(logger, "additionalData");
                    Check((string)values["trackingImplementation"] == "BogongAustralia/ef8a68b" &&
                          (string)values["trackingInputUnits"] == "radians" && (float)values["wireYaw"] == -.2f &&
                          (float)values["yawGain"] == 10, "Rig data records the actual Kinefly input, units and gain.");
                    Check((float)values["SensRotYRad"] == -.2f && (float)values["SensRotXRad"] == .21f &&
                          (float)values["SensRotZRad"] == -.17f, "Historical raw-radian log columns are preserved.");
                }
                Set(main, "timer", -1f); Call(main, "ManageTimerAndTransitions");
                Check(trackers.All(t => t.GetYawGain() == -2.5f), "In-place trial transitions apply signed sequence gain.");
                Set(main, "timer", -1f); Call(main, "ManageTimerAndTransitions");
                phase = 2; return;
            }
            Check(main.currentStep == 2 && trackers.All(t => t.UsesBogongInput && t.GetYawGain() == 1), "Reloaded trial restores omitted default gain.");
            Check(trackers.All(t => t.GetYawDCOffset() == .125f), "Per-rig DC offset survives scene reload.");
            Finish(null);
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void Finish(string error)
    {
        failure = error; SessionState.SetBool(Key, false); EditorApplication.update -= Validate;
        File.WriteAllText(Path.Combine(Application.dataPath, "../bogong-gain-validation.json"), JsonConvert.SerializeObject(new {
            checks, failure, referenceCommit = "ef8a68b2ad2ff690e803007511666296a31eda5a",
            comparison = "Exact Vector3 and Quaternion component equality on every simulated frame", unity = Application.unityVersion
        }, Formatting.Indented));
        if (error != null) Debug.LogError(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
#endif
