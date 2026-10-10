using System;
using System.Collections.Generic;
using System.Linq;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class TelemetryConfig
{
    public bool enabled = true;
    public string bindAddress = "127.0.0.1";
    public int port = 9880;
    public float rateHz = 20;
    public string topic = "matrex.telemetry.v1";
    public void Validate()
    {
        if (!enabled) return;
        if (port < 1 || port > 65535 || string.IsNullOrWhiteSpace(bindAddress) || string.IsNullOrWhiteSpace(topic) ||
            float.IsNaN(rateHz) || float.IsInfinity(rateHz) || rateHz <= 0 || rateHz > 240)
            throw new ArgumentException("telemetry requires an address, port 1..65535, topic, and rateHz in (0,240].");
    }
}

/// <summary>Versioned snapshots on a dedicated PUB port. All socket operations stay on the Unity thread.</summary>
[DefaultExecutionOrder(2000)]
public sealed class ExperimentTelemetry : MonoBehaviour
{
    private PublisherSocket publisher;
    private TelemetryConfig settings;
    private double nextPublish;
    private long sequence;
    public string State { get; private set; } = "waiting for system config";
    public string Endpoint { get; private set; }
    public long DroppedSnapshots { get; private set; }

    private void OnEnable() => MainController.SystemConfigurationChanged += Configure;
    private void Start() { if (settings == null) Configure(); }
    private void OnDisable()
    {
        MainController.SystemConfigurationChanged -= Configure;
        Close();
    }
    private void Close() { publisher?.Dispose(); publisher = null; }

    public void Configure()
    {
        Close();
        var main = MainController.Instance;
        settings = main != null ? main.TelemetrySettings : null;
        if (settings == null) return;
        State = "disabled";
        if (!settings.enabled) return;
        try
        {
            settings.Validate();
            if (main.SystemConfigs.Values.Any(rig => rig.zmqPort == settings.port))
                throw new ArgumentException("Telemetry port must differ from every tracking input port.");
            Endpoint = $"tcp://{settings.bindAddress}:{settings.port}";
            publisher = new PublisherSocket();
            publisher.Options.Linger = TimeSpan.Zero;
            publisher.Options.SendHighWatermark = 2;
            publisher.Bind(Endpoint);
            State = "publishing";
            nextPublish = 0;
        }
        catch (Exception error)
        {
            Close(); State = "error";
            Debug.LogError("Telemetry publisher failed: " + error);
        }
    }

    private void LateUpdate()
    {
        if (publisher == null || Time.realtimeSinceStartupAsDouble < nextPublish) return;
        nextPublish = Time.realtimeSinceStartupAsDouble + 1.0 / settings.rateHz;
        try
        {
            var message = new NetMQMessage();
            message.Append(settings.topic);
            message.Append(JsonConvert.SerializeObject(Capture(++sequence)));
            if (!publisher.TrySendMultipartMessage(TimeSpan.Zero, message)) DroppedSnapshots++;
        }
        catch (Exception error)
        {
            Close(); State = "error";
            Debug.LogError("Telemetry send failed: " + error);
        }
    }

    public object Capture(long snapshotSequence)
    {
        var main = MainController.Instance;
        var scene = SceneManager.GetActiveScene();
        var rigs = new List<object>();
        if (main != null)
        {
            foreach (string id in main.SystemConfigs.Keys.Concat(main.VRClosedLoops.Keys).Distinct().OrderBy(id => id))
            {
                main.VRClosedLoops.TryGetValue(id, out ClosedLoop rig);
                if (rig == null) { rigs.Add(new { vrId = id, active = false }); continue; }
                var listener = rig.GetComponent<ZmqListener>();
                var reference = rig.GetComponent<StimulusHeadingReference>();
                Vector3 position = rig.transform.position, euler = rig.transform.eulerAngles;
                ZmqListener.RawSample raw = listener != null ? listener.ReadRawSample() : default;
                rigs.Add(new {
                    vrId = id, active = rig.isActiveAndEnabled,
                    pose = new { x = position.x, y = position.y, z = position.z, pitch = euler.x, yaw = euler.y, roll = euler.z },
                    headingDegrees = euler.y,
                    input = new { source = rig.GetCurrentMode().ToString(),
                        motionMode = rig.MotionMode,
                        endpoint = listener != null ? listener.Endpoint : null,
                        state = listener != null ? listener.State : "missing listener",
                        wireFormat = listener != null ? listener.WireFormat : null,
                        latest = Sample(raw), consumed = Sample(rig.LastConsumedSample),
                        interpretation = rig.InputInterpretation },
                    closedLoop = new { positionEnabled = rig.PositionTrackingEnabled, orientationEnabled = rig.OrientationTrackingEnabled,
                        positionGain = rig.PositionGain, orientationGain = rig.OrientationGain,
                        yawGain = rig.GetYawGain(), yawDCOffsetRadians = rig.GetYawDCOffset(), forceGain = rig.ForceGain, torqueGain = rig.TorqueGain,
                        appliedThisFrame = rig.AppliedTrackingThisFrame,
                        appliedPositionDelta = new { x = rig.LastAppliedPositionDelta.x, y = rig.LastAppliedPositionDelta.y, z = rig.LastAppliedPositionDelta.z },
                        appliedYawDeltaDegrees = rig.LastAppliedYawDeltaDegrees },
                    autoTrim = new { supported = rig.SupportsAutoTrim, enabled = rig.AutoTrim.Enabled,
                        state = rig.AutoTrim.State, flightReady = rig.AutoTrim.FlightReady, flying = rig.AutoTrim.Flying,
                        flightVarianceRadiansSquared = rig.AutoTrim.FlightVariance,
                        medianDegPerSecond = rig.AutoTrim.EffectiveMedianDegPerSecond,
                        windowProgress = rig.AutoTrim.WindowProgress, passes = rig.AutoTrim.Passes },
                    headingReference = new { phase = reference != null ? reference.Phase : "disabled",
                        remainingSeconds = reference != null ? reference.RemainingSeconds : 0, result = reference != null ? reference.Result : null }
                });
            }
        }
        return new {
            schemaVersion = 1, sequence = snapshotSequence, timestampUtc = DateTime.UtcNow.ToString("O"),
            realtimeSeconds = Time.realtimeSinceStartupAsDouble, experimentSeconds = Time.timeAsDouble, frame = Time.frameCount,
            system = new { running = main != null && main.SequenceRunning, trialNumber = main != null ? main.currentTrial + 1 : 0,
                sceneNumber = main != null && main.SequenceRunning ? main.currentStep + 1 : 0,
                sceneName = scene.name, sceneBuildIndex = scene.buildIndex,
                remainingStepSeconds = main != null ? main.RemainingStepSeconds : 0,
                remainingPhaseSeconds = main != null ? main.RemainingPhaseSeconds : 0,
                phase = MainController.RecordedExperimentPhase,
                error = main != null ? main.SequenceError : null,
                telemetry = State, droppedSnapshots = DroppedSnapshots },
            rigs, errors = RuntimeStatusOverlay.RecentErrors()
        };
    }

    private static object Sample(ZmqListener.RawSample raw)
    {
        double? age = raw.hasPose ? Math.Max(0, TimeSpan.FromTicks(DateTime.UtcNow.Ticks - raw.receivedUtcTicks).TotalSeconds) : (double?)null;
        return new { available = raw.hasPose, sequence = raw.sequence,
            receivedUtc = raw.hasPose ? new DateTime(raw.receivedUtcTicks, DateTimeKind.Utc).ToString("O") : null,
            ageSeconds = age, fresh = age.HasValue && age.Value <= 1,
            x = raw.position.x, y = raw.position.y, z = raw.position.z,
            pitch = raw.rotation.x, yaw = raw.rotation.y, roll = raw.rotation.z };
    }
}
