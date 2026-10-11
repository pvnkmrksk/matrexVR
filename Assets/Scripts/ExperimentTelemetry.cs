using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    public string topic = "matrex.telemetry.v2";
    public void Validate()
    {
        if (!enabled) return;
        if (port < 1 || port > 65535 || string.IsNullOrWhiteSpace(bindAddress) || string.IsNullOrWhiteSpace(topic) ||
            float.IsNaN(rateHz) || float.IsInfinity(rateHz) || rateHz <= 0 || rateHz > 240)
            throw new ArgumentException("telemetry requires an address, port 1..65535, topic, and rateHz in (0,240].");
    }
}

/// <summary>Capture Unity state on the frame thread; serialize and publish a bounded latest snapshot on a worker.</summary>
[DefaultExecutionOrder(2000)]
public sealed class ExperimentTelemetry : MonoBehaviour
{
    private PublishWorker worker;
    private TelemetryConfig settings;
    private double nextPublish;
    private long sequence;
    private string state = "waiting for system config";
    private long droppedSnapshots;
    private bool reportedWorkerFailure;
    public string State => worker == null ? state : worker.Failure != null ? "error" : worker.Ready ? "publishing" : "starting";
    public string Endpoint { get; private set; }
    public long DroppedSnapshots => Interlocked.Read(ref droppedSnapshots);

    private void OnEnable()
    {
        MainController.SystemConfigurationChanged += Configure;
        if (MainController.Instance != null && MainController.Instance.SystemConfigs.Count > 0) Configure();
    }
    private void Start() { if (settings == null) Configure(); }
    private void OnDisable()
    {
        MainController.SystemConfigurationChanged -= Configure;
        Close();
    }
    private void Close() { worker?.Dispose(); worker = null; }

    public void Configure()
    {
        Close();
        var main = MainController.Instance;
        settings = main != null ? main.TelemetrySettings : null;
        if (settings == null) return;
        state = "disabled";
        if (!settings.enabled) return;
        try
        {
            settings.Validate();
            // Upgrade the old built-in default in existing local system configs with the payload.
            if (settings.topic == "matrex.telemetry.v1") settings.topic = "matrex.telemetry.v2";
            if (main.SystemConfigs.Values.Any(rig => rig.zmqPort == settings.port))
                throw new ArgumentException("Telemetry port must differ from every tracking input port.");
            Endpoint = $"tcp://{settings.bindAddress}:{settings.port}";
            reportedWorkerFailure = false;
            worker = new PublishWorker(Endpoint, settings.topic, () => Interlocked.Increment(ref droppedSnapshots));
            nextPublish = 0;
        }
        catch (Exception error)
        {
            Close(); state = "error";
            Debug.LogError("Telemetry publisher failed: " + error);
        }
    }

    private void LateUpdate()
    {
        if (worker == null) return;
        if (worker.Failure != null)
        {
            if (!reportedWorkerFailure) { reportedWorkerFailure = true; Debug.LogError("Telemetry publisher failed: " + worker.Failure); }
            return;
        }
        if (Time.realtimeSinceStartupAsDouble < nextPublish) return;
        nextPublish = Time.realtimeSinceStartupAsDouble + 1.0 / settings.rateHz;
        try
        {
            worker.Submit(Capture(++sequence));
        }
        catch (Exception error)
        {
            Close(); state = "error";
            Debug.LogError("Telemetry send failed: " + error);
        }
    }

    // A slow subscriber or encoder never builds a queue on the frame thread. NetMQ sockets
    // are created, used and disposed on the same worker, including after config changes.
    private sealed class PublishWorker : IDisposable
    {
        private readonly AutoResetEvent signal = new AutoResetEvent(false);
        private readonly Thread thread;
        private readonly Action dropped;
        private object latest;
        private volatile bool stopping;
        private volatile bool ready;
        private volatile string failure;
        public bool Ready => ready;
        public string Failure => failure;
        public PublishWorker(string endpoint, string topic, Action dropped)
        {
            this.dropped = dropped;
            thread = new Thread(() => Run(endpoint, topic)) { IsBackground = true, Name = "Experiment telemetry" };
            thread.Start();
        }
        public void Submit(object snapshot)
        {
            if (Interlocked.Exchange(ref latest, snapshot) != null) dropped();
            signal.Set();
        }
        private void Run(string endpoint, string topic)
        {
            try
            {
                using (var publisher = new PublisherSocket())
                {
                    publisher.Options.Linger = TimeSpan.Zero;
                    publisher.Options.SendHighWatermark = 2;
                    publisher.Bind(endpoint); ready = true;
                    while (!stopping)
                    {
                        object snapshot = Interlocked.Exchange(ref latest, null);
                        if (snapshot == null) { signal.WaitOne(100); continue; }
                        var message = new NetMQMessage();
                        message.Append(topic); message.Append(JsonConvert.SerializeObject(snapshot));
                        if (!publisher.TrySendMultipartMessage(TimeSpan.Zero, message)) dropped();
                    }
                }
            }
            catch (Exception error) { failure = error.ToString(); }
        }
        public void Dispose()
        {
            stopping = true; signal.Set(); thread.Join();
            Interlocked.Exchange(ref latest, null); signal.Dispose();
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
                        latest = Sample(raw, rig), consumed = Sample(rig.LastConsumedSample, rig),
                        interpretation = rig.InputInterpretation.Replace("radians", "degrees"),
                        angularUnits = rig.GetUseForceMode() ? "tracker-native-torque" : "degrees" },
                    closedLoop = new { positionEnabled = rig.PositionTrackingEnabled, orientationEnabled = rig.OrientationTrackingEnabled,
                        positionGain = rig.PositionGain, orientationGain = rig.OrientationGain,
                        yawGain = rig.GetYawGain(), yawDCOffsetDegrees = rig.GetYawDCOffset() * KineflyAutoTrim.RadToDeg,
                        yawDCCompensationDegPerSecond = rig.GetYawGain() * rig.GetYawDCOffset() * KineflyAutoTrim.RadToDeg,
                        yawOutputDegPerSecond = rig.GetLastYawOutput(), forceGain = rig.ForceGain, torqueGain = rig.TorqueGain,
                        appliedThisFrame = rig.AppliedTrackingThisFrame,
                        appliedPositionDelta = new { x = rig.LastAppliedPositionDelta.x, y = rig.LastAppliedPositionDelta.y, z = rig.LastAppliedPositionDelta.z },
                        appliedYawDeltaDegrees = rig.LastAppliedYawDeltaDegrees },
                    autoTrim = new { supported = rig.SupportsAutoTrim, enabled = rig.AutoTrim.Enabled,
                        state = rig.AutoTrim.State, flightReady = rig.AutoTrim.FlightReady, flying = rig.AutoTrim.Flying,
                        flightCheckEnabled = rig.AutoTrim.FlightCheckEnabled,
                        flightThresholdDegreesSquared = rig.AutoTrim.FlightThreshold * KineflyAutoTrim.RadToDeg * KineflyAutoTrim.RadToDeg,
                        flightSamples = rig.AutoTrim.FlightSampleCount,
                        flightVarianceDegreesSquared = rig.AutoTrim.FlightVariance * KineflyAutoTrim.RadToDeg * KineflyAutoTrim.RadToDeg,
                        flightStdDevDegrees = Math.Sqrt(rig.AutoTrim.FlightVariance) * KineflyAutoTrim.RadToDeg,
                        flightThresholdStdDevDegrees = Math.Sqrt(rig.AutoTrim.FlightThreshold) * KineflyAutoTrim.RadToDeg,
                        medianDegPerSecond = rig.AutoTrim.EffectiveMedianDegPerSecond,
                        meanDegPerSecond = rig.AutoTrim.EffectiveMeanDegPerSecond, uncertaintyDegPerSecond = rig.AutoTrim.UncertaintyDegPerSecond,
                        centerStatistic = rig.AutoTrim.CenterStatistic, measurementSeconds = rig.AutoTrim.MeasurementSeconds,
                        lastAdjustment = Adjustment(rig.AutoTrim.LastAdjustment),
                        windowProgress = rig.AutoTrim.WindowProgress, passes = rig.AutoTrim.Passes },
                    headingReference = new { phase = reference != null ? reference.Phase : "disabled",
                        remainingSeconds = reference != null ? reference.RemainingSeconds : 0, result = reference != null ? reference.Result : null }
                });
            }
        }
        return new {
            schemaVersion = 2, sequence = snapshotSequence, timestampUtc = DateTime.UtcNow.ToString("O"),
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

    private static object Sample(ZmqListener.RawSample raw, ClosedLoop rig)
    {
        double? age = raw.hasPose ? Math.Max(0, TimeSpan.FromTicks(DateTime.UtcNow.Ticks - raw.receivedUtcTicks).TotalSeconds) : (double?)null;
        bool wingAngles = rig.GetCurrentMode() == ClosedLoopMode.Kinefly;
        double angularScale = rig.UsesBogongInput && !rig.GetUseForceMode() ? KineflyAutoTrim.RadToDeg : 1;
        return new { available = raw.hasPose, sequence = raw.sequence,
            receivedUtc = raw.hasPose ? new DateTime(raw.receivedUtcTicks, DateTimeKind.Utc).ToString("O") : null,
            ageSeconds = age, fresh = age.HasValue && age.Value <= 1,
            positionUnits = wingAngles ? "x/y: wing-angle-degrees; z: tracker-native" : "tracker-native",
            x = raw.position.x * (wingAngles ? KineflyAutoTrim.RadToDeg : 1),
            y = raw.position.y * (wingAngles ? KineflyAutoTrim.RadToDeg : 1), z = raw.position.z,
            pitch = raw.rotation.x * angularScale,
            yaw = raw.rotation.y * angularScale,
            roll = raw.rotation.z * angularScale };
    }

    private static object Adjustment(KineflyAutoTrim.Adjustment change) => change == null ? null : new {
        realtimeSeconds = change.time, fromDegrees = change.fromRadians * KineflyAutoTrim.RadToDeg,
        toDegrees = change.toRadians * KineflyAutoTrim.RadToDeg, deltaDegrees = change.deltaRadians * KineflyAutoTrim.RadToDeg,
        yawRateChangeDegPerSecond = -change.gain * change.deltaRadians * KineflyAutoTrim.RadToDeg };
}
