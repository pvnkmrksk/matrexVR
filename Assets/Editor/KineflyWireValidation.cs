#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Exercise the network boundary, not just deserialized objects injected into ClosedLoop.
[InitializeOnLoad]
public static class KineflyWireValidation
{
    private const string Key = "KineflyWireValidation.Active";
    private static readonly List<PublisherSocket> publishers = new List<PublisherSocket>();
    private static readonly ConcurrentQueue<string> errors = new ConcurrentQueue<string>();
    private static ClosedLoop[] rigs;
    private static MainController main;
    private static SubscriberSocket telemetry;
    private static int phase, variant, checks, packets, rejectedPackets;
    private static double deadline, nextSend;
    private static volatile bool expectPacketError;
    private static volatile string expectedErrorPrefix;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }

    static KineflyWireValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 30;
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY")))
            throw new Exception("Use a disposable Unity validation copy.");
        var system = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Templates/system-config-kinefly.template.json")));
        system["telemetry"] = JObject.FromObject(new { enabled = true, bindAddress = "127.0.0.1", port = 29880, rateHz = 30 });
        system["overheadCamera"]["enabled"] = false;
        foreach (JObject rig in system["configs"])
        {
            rig["zmqAddress"] = "127.0.0.1";
            rig["zmqPort"] = 29870 + int.Parse(rig["vrId"].ToString().Substring(2));
        }
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "system_config.json"), system.ToString());
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "kinefly-wire.json"), JsonConvert.SerializeObject(new {
            numberOfLocusts = 1, locustSpeed = 0, closedLoopPosition = 0, closedLoopOrientation = 1, autopilotEnabled = false
        }));
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json"), JsonConvert.SerializeObject(new {
            autoStart = true, loop = false, sequences = new[] {
                new { sceneName = "Swarm", duration = 1000, gain = 2.5, parameters = new { configFile = "kinefly-wire.json" } }
            }
        }));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (stack.Contains("UnityEditor.Search.SearchDatabase") || message == "No backup data file found.") return;
        if (expectPacketError && message.StartsWith("Invalid tracking packet")) { System.Threading.Interlocked.Increment(ref rejectedPackets); return; }
        if (expectedErrorPrefix != null && message.StartsWith(expectedErrorPrefix)) return;
        errors.Enqueue(message);
    }

    private static float Yaw(int rig) => -.35f - variant - rig * .1f;
    private static void SendPackets()
    {
        for (int i = 0; i < publishers.Count; i++)
        {
            // Same flat fields, metadata and single send as Kinefly_Docker's ros_zmq_bridge.py.
            string json = variant == 3 ? JsonConvert.SerializeObject(new { yaw = Yaw(i) }) :
                JsonConvert.SerializeObject(new { x = 1.25, y = 1.25 - Yaw(i), z = 0, pitch = 0, roll = 0,
                    yaw = Yaw(i), frame_number = ++packets, timestamp_unix = 1791170000.125 });
            if (variant == 1) publishers[i].SendMoreFrame("pose").SendFrame(json);
            else
            {
                if (variant == 2) publishers[i].SendFrame("pose"); // Legacy independent topic and payload messages.
                publishers[i].SendFrame(json);
            }
        }
    }

    private static void Tick()
    {
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (now > deadline) throw new TimeoutException("Kinefly wire validation timed out: phase=" + phase + ", variant=" + variant + "; " + string.Join("; ", errors));
            switch (phase)
            {
                case 0:
                    SceneManager.LoadScene("ControlScene"); phase = 1; break;
                case 1:
                    main = MainController.Instance;
                    if (main == null || main.VRClosedLoops.Count != 4 || SceneManager.GetActiveScene().name != "Swarm") return;
                    rigs = main.VRClosedLoops.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToArray();
                    for (int i = 0; i < 4; i++)
                    {
                        var input = rigs[i].GetComponent<ZmqListener>();
                        Check(input.address == "127.0.0.1" && input.port == 29871 + i, "Each rig loads its own configured input endpoint");
                        Check(rigs[i].GetCurrentMode() == ClosedLoopMode.Kinefly && rigs[i].UsesBogongInput && rigs[i].GetUseYawMode(), "Explicit Kinefly remains flight yaw-rate in Swarm");
                        var pub = new PublisherSocket(); pub.Options.Linger = TimeSpan.Zero;
                        pub.Bind("tcp://127.0.0.1:" + (29871 + i)); publishers.Add(pub);
                    }
                    telemetry = new SubscriberSocket(); telemetry.Options.Linger = TimeSpan.Zero;
                    telemetry.Connect("tcp://127.0.0.1:29880"); telemetry.Subscribe("matrex.telemetry.v1");
                    phase = 2; deadline = now + 8; break;
                case 2:
                    if (now >= nextSend) { SendPackets(); nextSend = now + .05; }
                    if (!rigs.Select((rig, i) => {
                        var raw = rig.GetComponent<ZmqListener>().ReadRawSample();
                        return raw.hasPose && Math.Abs(raw.rotation.y - Yaw(i)) < .00001f && rig.AppliedTrackingThisFrame &&
                            Math.Abs(rig.LastConsumedSample.rotation.y - Yaw(i)) < .00001f;
                    }).All(ok => ok)) return;
                    for (int i = 0; i < 4; i++)
                    {
                        var raw = rigs[i].GetComponent<ZmqListener>().ReadRawSample();
                        Check(Math.Abs(raw.rotation.y - Yaw(i)) < .00001f, "Signed raw yaw retained, rig " + i + ", framing " + variant);
                        Check(raw.position.x == (variant == 3 ? 0 : 1.25f), "Missing fields default to zero; metadata allowed");
                        Check(Math.Abs(rigs[i].GetLastYawOutput() - 2.5f * Yaw(i) * Mathf.Rad2Deg) < .001f,
                            "Received packet drives historical flight gain: output=" + rigs[i].GetLastYawOutput() + ", gain=" + rigs[i].GetYawGain() + ", yaw=" + Yaw(i));
                    }
                    Check(errors.Count == 0, "Valid legacy packets cause no errors: " + string.Join("; ", errors));
                    variant++;
                    if (variant == 4)
                    {
                        expectPacketError = true;
                        foreach (var pub in publishers) pub.SendFrame("{\"yaw\":\"invalid\"}");
                    }
                    if (variant == 5) phase = 3;
                    deadline = now + 8; break;
                case 3:
                    var packet = new NetMQMessage();
                    if (!telemetry.TryReceiveMultipartMessage(TimeSpan.Zero, ref packet)) return;
                    var snapshot = JObject.Parse(packet[1].ConvertToString());
                    if (!snapshot["rigs"].All(r => r["input"]["latest"].Value<bool>("available"))) return;
                    Check(snapshot["rigs"].Count() == 4, "Separate telemetry channel includes all four receiving rigs");
                    Check(snapshot["rigs"].All(r => r["input"].Value<string>("source") == "Kinefly" && r["input"].Value<string>("interpretation") == "yaw-rate-radians"), "Telemetry explicitly identifies Kinefly flight interpretation");
                    Check(rejectedPackets >= 4, "Malformed packets were reported on every input and valid packets subsequently recovered");
                    string hud = Object.FindFirstObjectByType<RuntimeStatusOverlay>().BuildText();
                    Check(hud.Contains("Kinefly / flight") && hud.Contains("Telemetry PUB (output)"), "HUD explicitly separates Kinefly flight input and telemetry output");
                    for (int i = 0; i < 4; i++) Check(hud.Contains("tcp://127.0.0.1:" + (29871 + i)), "HUD shows actual input port for rig " + i);
                    DiagnosticsChecks();
                    phase = 5; deadline = now + 8; break;
                case 5:
                    if (now >= nextSend) { SendPackets(); nextSend = now + .05; }
                    if (!rigs.Select((rig, i) => Math.Abs(rig.GetComponent<ZmqListener>().ReadRawSample().rotation.y - Yaw(i)) < .00001f).All(ok => ok)) return;
                    Check(true, "All four inputs still receive after invalid optional monitoring settings");
                    Check(errors.Count == 0, "No unexpected runtime errors");
                    main.StopSequence(); SceneManager.LoadScene("ControlScene"); phase = 4; nextSend = now + .5; break;
                case 4:
                    if (now < nextSend) return;
                    Check(errors.Count == 0, "Scene shutdown has no unexpected errors");
                    Finish(null); break;
            }
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void DiagnosticsChecks()
    {
        string original = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "system_config.json"));
        foreach (JToken invalidPort in new JToken[] { -1, "not a port" })
        {
            var config = JObject.Parse(original);
            config["telemetry"]["port"] = invalidPort;
            File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "wire-invalid-monitor.json"), config.ToString());
            expectedErrorPrefix = invalidPort.Type == JTokenType.Integer ? "Telemetry publisher failed:" : "Invalid telemetry configuration;";
            main.SetSystemConfigFile("wire-invalid-monitor.json");
            Check(main.SystemConfigs.Count == 4, "Invalid optional telemetry cannot clear input configurations");
            for (int i = 0; i < 4; i++)
                Check(main.GetSystemConfig("VR" + (i + 1)).zmqPort == 29871 + i, "Invalid telemetry preserves each tracking port");
        }
        expectedErrorPrefix = null;
        main.SetSystemConfigFile("system_config.json");
        Check(Object.FindFirstObjectByType<ExperimentTelemetry>().State == "publishing", "Valid telemetry recovers independently");
        var root = new GameObject("Unconfigured tracking component");
        var listener = root.AddComponent<ZmqListener>(); listener.enabled = false;
        listener.address = "127.0.0.2"; listener.port = 29999;
        expectedErrorPrefix = "No tracking config found";
        typeof(ZmqListener).GetMethod("ApplySystemConfig", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(listener, null);
        expectedErrorPrefix = null;
        Check(listener.address == "127.0.0.2" && listener.port == 29999, "Missing rig config must not silently redirect to localhost:9872");
        Object.Destroy(root);
    }

    private static void Finish(string failure)
    {
        EditorApplication.update -= Tick; Application.logMessageReceivedThreaded -= OnLog;
        SessionState.SetBool(Key, false);
        foreach (var publisher in publishers) publisher.Dispose();
        publishers.Clear(); telemetry?.Dispose(); telemetry = null;
        File.WriteAllText(Path.Combine(Application.dataPath, "../kinefly-wire-validation.json"), JsonConvert.SerializeObject(new {
            checks, failure, errors, unity = Application.unityVersion,
            coverage = "Four configured ports, single JSON Kinefly bridge frames, multipart topic+JSON, independent topic/JSON messages, yaw-only fields, metadata, signed raw flight gains, malformed-packet recovery, separate telemetry, explicit HUD mode/endpoints, invalid monitoring config isolation, missing-rig endpoint retention, clean shutdown"
        }, Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
#endif
