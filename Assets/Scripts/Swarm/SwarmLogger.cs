using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(1300)]
public class SwarmLogger : MonoBehaviour
{
    public LayerMask locustLayerMask;
    private LocustSpawner spawner;
    private AsyncSwarmWriter writer;
    private int cachedRevision = -1, cachedMask;
    private bool closed;
    private readonly List<AgentSource> agents = new List<AgentSource>();
    private BogongSwarmRenderer batchRenderer;
    private static readonly Unity.Profiling.ProfilerMarker captureMarker = new Unity.Profiling.ProfilerMarker("SwarmLogger.Capture");
    public int PendingFrames => writer?.PendingFrames ?? 0;
    public long DroppedFrames => writer?.DroppedFrames ?? 0;
    public string Failure => writer?.Failure;
    public double CaptureMilliseconds { get; private set; }
    public long WrittenRows => writer?.WrittenRows ?? 0;
    public string LogPath { get; private set; }

    private readonly struct AgentSource
    {
        public readonly GameObject Object;
        public readonly Transform Transform;
        public readonly string Name, Layer;
        public readonly RecordedAnimationCapture Animation;
        public readonly int Index;

        public AgentSource(GameObject agent, int index)
        {
            Object = agent;
            Transform = agent.transform;
            Name = agent.name;
            Layer = RuntimeRigRegistry.LayerLabel(agent.layer);
            Animation = new RecordedAnimationCapture(agent);
            Index = index;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallShutdown() { Application.quitting -= AsyncSwarmWriter.DrainAll; Application.quitting += AsyncSwarmWriter.DrainAll; }

    private void Start()
    {
        if (closed) return;
        var master = MasterDataLogger.Instance;
        spawner = GetComponent<LocustSpawner>();
        if (master == null || spawner == null || string.IsNullOrEmpty(master.directoryPath)) return;
        if (!master.AcceptingWrites || !master.SaveSwarmData) { closed = true; return; }
        string layerNames = "";
        for (int i = 0; i < 32; i++) if ((locustLayerMask.value & (1 << i)) != 0) layerNames += RuntimeRigRegistry.LayerLabel(i) + "_";
        string path = Path.Combine(master.directoryPath,
            $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fffffff}_SimulatedLocustData_{layerNames.TrimEnd('_')}_{spawner.numberOfLocusts}_{spawner.spawnAreaSize}_{spawner.mu}_{spawner.kappa}_{spawner.locustSpeed}.matpop");
        LogPath = path;
        string header = FormattableString.Invariant($"Timestamp,Name,Layer,X,Y,Z,skyboxId,skyboxSampleUtc,experimentPhase,RotationX,RotationY,RotationZ,AnimationState,UnityFrame,ScaleX,ScaleY,ScaleZ,NumberOfLocusts:{spawner.numberOfLocusts},SpawnAreaSize:{spawner.spawnAreaSize},Mu:{spawner.mu},Kappa:{spawner.kappa},LocustSpeed:{spawner.locustSpeed},LocustSpeedStdDev:{spawner.locustSpeedStdDev},DirectionDistribution:{spawner.directionDistribution},KappaElevation:{spawner.kappaElevation}");
        writer = new AsyncSwarmWriter(path, header);
    }

    private void LateUpdate() => CaptureFrame();
    public void CaptureFrame()
    {
        if (closed || writer == null || spawner == null) return;
        if (writer.Failure != null) { StopForEvidenceFailure(writer.Failure); return; }
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        using (captureMarker.Auto())
        {
            if (cachedRevision != spawner.SpawnRevision || cachedMask != locustLayerMask.value)
            {
                agents.Clear();
                batchRenderer = spawner.GetComponentInChildren<BogongSwarmRenderer>(true);
                int sourceIndex = -1;
                foreach (var agent in spawner.Spawned)
                {
                    sourceIndex++;
                    if (agent != null && (locustLayerMask.value & (1 << agent.layer)) != 0)
                        agents.Add(new AgentSource(agent, sourceIndex));
                }
                cachedRevision = spawner.SpawnRevision; cachedMask = locustLayerMask.value;
            }
            var frame = writer.Rent(agents.Count);
            frame.timestamp = RecordedFrameClock.Now; frame.unityFrame = Time.frameCount;
            frame.skyboxId = NightSkyController.CurrentId;
            frame.skyboxSampleUtc = NightSkyController.CurrentSampleUtc;
            frame.phase = MainController.RecordedExperimentPhase;
            for (int i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (agent.Transform == null || !agent.Object.activeInHierarchy) continue;
                agent.Transform.GetPositionAndRotation(out Vector3 p, out Quaternion orientation);
                Vector3 rotation = orientation.eulerAngles;
                Vector3 scale = agent.Transform.lossyScale;
                // A slot cannot be reused while the worker still owns this rented frame.
                var animation=frame.agents[frame.count].animationSnapshot as RecordedAnimationSnapshot??new RecordedAnimationSnapshot();
                agent.Animation.CaptureSnapshot(animation,batchRenderer,agent.Index);
                frame.agents[frame.count++] = new AsyncSwarmWriter.Agent { index = agent.Index, name = agent.Name, layer = agent.Layer, x = p.x, y = p.y, z = p.z,
                    rx = rotation.x, ry = rotation.y, rz = rotation.z, sx=scale.x,sy=scale.y,sz=scale.z,
                    animationSnapshot = animation };
            }
            if (!writer.Submit(frame)) StopForEvidenceFailure("The bounded population recording queue could not accept the rendered frame.");
        }
        CaptureMilliseconds = 1000.0 * (System.Diagnostics.Stopwatch.GetTimestamp() - start) / System.Diagnostics.Stopwatch.Frequency;
    }

    private void StopForEvidenceFailure(string reason)
    {
        CloseForArchive();
        var error = new IOException("Swarm recording evidence could not be preserved: " + reason);
        if (MainController.Instance != null) MainController.Instance.FailRecordingEvidence(error);
        else Debug.LogException(error);
    }
    public void CloseForArchive() { closed = true; writer?.Complete(); }
    private void OnDestroy() => CloseForArchive();
}
