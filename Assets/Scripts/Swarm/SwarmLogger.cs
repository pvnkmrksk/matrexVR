using System;
using System.Collections.Generic;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

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
    private int poseStart = -1;
    private int[] capturedIndices;
    private EulerSample[] eulerCache;
    private StockPopulationCapture stockCapture;
    private int stockScheduledFrame = -1;
    private bool stockEligible;
    private static readonly Unity.Profiling.ProfilerMarker captureMarker = new Unity.Profiling.ProfilerMarker("SwarmLogger.Capture");
    private static readonly Unity.Profiling.ProfilerMarker poseScheduleMarker = new Unity.Profiling.ProfilerMarker("SwarmLogger.SchedulePoseJob");
    private static readonly Unity.Profiling.ProfilerMarker poseWaitMarker = new Unity.Profiling.ProfilerMarker("SwarmLogger.WaitForPoseJob");
    private static readonly bool usePoseJob = Array.IndexOf(Environment.GetCommandLineArgs(), "--serial-swarm-population-capture") < 0;
    private static readonly bool verifyPoseJob = Array.IndexOf(Environment.GetCommandLineArgs(), "--verify-swarm-population-pose-jobs") >= 0;
    private static readonly bool useStockPackets = usePoseJob &&
        Array.IndexOf(Environment.GetCommandLineArgs(), "--serial-stock-population-packets") < 0;
    private static readonly bool verifyStockPackets = verifyPoseJob ||
        Array.IndexOf(Environment.GetCommandLineArgs(), "--verify-stock-population-packets") >= 0;
    private static readonly List<SwarmLogger> activeLoggers = new List<SwarmLogger>();
    private static TransformAccessArray poseTransforms;
    private static NativeArray<PoseSample> poses;
    private static JobHandle poseHandle;
    private static bool sourcesChanged = true, posePending, poseJobFailed;
    private static int scheduledFrame = -1, completedFrame = -1;
    private const int MinimumJobSize = 512;
    public int PendingFrames => writer?.PendingFrames ?? 0;
    public long DroppedFrames => writer?.DroppedFrames ?? 0;
    public string Failure => writer?.Failure;
    public double CaptureMilliseconds { get; private set; }
    public double PoseScheduleMilliseconds { get; private set; }
    public double PoseWaitMilliseconds { get; private set; }
    public int JobPoseRows { get; private set; }
    public int StockPacketRows { get; private set; }
    public long WrittenRows => writer?.WrittenRows ?? 0;
    public string LogPath { get; private set; }

    internal readonly struct AgentSource
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

    private struct PoseSample
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public int Frame;
        public bool Valid;
    }

    private struct EulerSample
    {
        public Quaternion Rotation;
        public Vector3 Euler;
        public bool Valid;
    }

    [BurstCompile(FloatMode = FloatMode.Strict)]
    private struct ReadPoses : IJobParallelForTransform
    {
        [WriteOnly] public NativeArray<PoseSample> Output;
        public int Frame;

        public void Execute(int index, TransformAccess transform)
        {
            if (!transform.isValid) return;
            transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
            Output[index] = new PoseSample { Position = position, Rotation = rotation, Frame = Frame, Valid = true };
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPoseCapture()
    {
        ReleasePoseBuffers();
        activeLoggers.Clear();
        sourcesChanged = true;
        poseJobFailed = false;
        scheduledFrame = -1;
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
        sourcesChanged = true;
    }

    private void OnEnable()
    {
        activeLoggers.Add(this);
        sourcesChanged = true;
    }

    private void OnDisable()
    {
        activeLoggers.Remove(this);
        poseStart = -1;
        stockScheduledFrame = -1;
        sourcesChanged = true;
        if (activeLoggers.Count == 0) ReleasePoseBuffers();
    }

    private bool CanCapture => !closed && writer != null && spawner != null;

    private void RefreshSources()
    {
        if (cachedRevision == spawner.SpawnRevision && cachedMask == locustLayerMask.value) return;
        ReleaseStockCapture();
        agents.Clear();
        batchRenderer = spawner.GetComponentInChildren<BogongSwarmRenderer>(true);
        int sourceIndex = -1;
        foreach (var agent in spawner.Spawned)
        {
            sourceIndex++;
            if (agent != null && (locustLayerMask.value & (1 << agent.layer)) != 0)
                agents.Add(new AgentSource(agent, sourceIndex));
        }
        cachedRevision = spawner.SpawnRevision;
        cachedMask = locustLayerMask.value;
        poseStart = -1;
        capturedIndices = null;
        eulerCache = null;
        sourcesChanged = true;
        if (useStockPackets && !poseJobFailed && agents.Count >= MinimumJobSize)
            stockCapture = StockPopulationCapture.TryCreate(agents);
    }

    private static void RebuildPoseBuffers(int count)
    {
        ReleasePoseBuffers();
        foreach (var logger in activeLoggers) logger.poseStart = -1;
        if (count < MinimumJobSize) return;

        var transforms = new Transform[count];
        int offset = 0;
        foreach (var logger in activeLoggers)
        {
            if (!logger.CanCapture || logger.stockEligible || logger.agents.Count == 0) continue;
            logger.poseStart = offset;
            int length = logger.agents.Count;
            if (logger.capturedIndices == null)
            {
                logger.capturedIndices = new int[length];
                logger.eulerCache = new EulerSample[length];
            }
            for (int i = 0; i < length; i++) transforms[offset++] = logger.agents[i].Transform;
        }
        poseTransforms = new TransformAccessArray(transforms);
        poses = new NativeArray<PoseSample>(transforms.Length, Allocator.Persistent);
    }

    private void SchedulePoseCapture(int frame)
    {
        if (!usePoseJob || poseJobFailed || scheduledFrame == frame) return;
        scheduledFrame = frame;
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        using (poseScheduleMarker.Auto())
        {
            try
            {
                int count = 0;
                foreach (var logger in activeLoggers)
                {
                    if (!logger.CanCapture) continue;
                    logger.RefreshSources();
                    bool eligible = logger.stockCapture?.CanSchedule == true;
                    if (logger.stockEligible != eligible)
                    {
                        logger.stockEligible = eligible;
                        sourcesChanged = true;
                    }
                    if (!eligible) count += logger.agents.Count;
                }
                if (sourcesChanged)
                {
                    RebuildPoseBuffers(count);
                    sourcesChanged = false;
                }
                if (poseTransforms.isCreated)
                {
                    poseHandle = new ReadPoses { Output = poses, Frame = frame }.ScheduleReadOnly(poseTransforms, 64);
                    posePending = true;
                }
                foreach (var logger in activeLoggers)
                {
                    if (!logger.CanCapture || !logger.stockEligible) continue;
                    logger.stockCapture.Schedule(ref poseHandle, ref posePending);
                    logger.stockScheduledFrame = frame;
                }
                if (posePending) JobHandle.ScheduleBatchedJobs();
            }
            catch (Exception error)
            {
                FinishPoseCapture();
                DisablePoseJob(error);
            }
        }
        PoseScheduleMilliseconds = ElapsedMilliseconds(start);
    }

    private static void CompletePoseJob()
    {
        if (!posePending) return;
        posePending = false;
        try
        {
            poseHandle.Complete();
            completedFrame = scheduledFrame;
        }
        catch (Exception error) { DisablePoseJob(error); }
        finally { poseHandle = default; }
    }

    private static void DisablePoseJob(Exception error)
    {
        if (poseJobFailed) return;
        poseJobFailed = true;
        completedFrame = -1;
        Debug.LogWarning("Parallel swarm pose capture reverted to Unity transform reads: " + error.Message);
    }

    private static void ReleasePoseBuffers()
    {
        CompletePoseJob();
        if (poseTransforms.isCreated) poseTransforms.Dispose();
        if (poses.IsCreated) poses.Dispose();
        completedFrame = -1;
    }

    private void FinishPoseCapture()
    {
        if (!posePending) return;
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        using (poseWaitMarker.Auto()) CompletePoseJob();
        PoseWaitMilliseconds = ElapsedMilliseconds(start);
    }

    private static double ElapsedMilliseconds(long start) =>
        1000.0 * (System.Diagnostics.Stopwatch.GetTimestamp() - start) / System.Diagnostics.Stopwatch.Frequency;

    private static bool SameBits(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
    private static bool SameRotation(Quaternion a, Quaternion b) =>
        SameBits(a.x, b.x) && SameBits(a.y, b.y) && SameBits(a.z, b.z) && SameBits(a.w, b.w);
    private static bool SameVector(Vector3 a, Vector3 b) => SameBits(a.x, b.x) && SameBits(a.y, b.y) && SameBits(a.z, b.z);

    private Vector3 ReadEuler(int index, Quaternion rotation)
    {
        ref var cached = ref eulerCache[index];
        if (!cached.Valid || !SameRotation(cached.Rotation, rotation))
            cached = new EulerSample { Rotation = rotation, Euler = rotation.eulerAngles, Valid = true };
        return cached.Euler;
    }

    private void LateUpdate() => CaptureFrame(true);
    public void CaptureFrame() => CaptureFrame(false);

    private void CaptureFrame(bool allowPoseJob)
    {
        PoseScheduleMilliseconds = PoseWaitMilliseconds = 0;
        JobPoseRows = StockPacketRows = 0;
        if (!CanCapture) return;
        if (writer.Failure != null) { StopForEvidenceFailure(writer.Failure); return; }
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        using (captureMarker.Auto())
        {
            int frameNumber = Time.frameCount;
            if (allowPoseJob) SchedulePoseCapture(frameNumber);
            try
            {
                RefreshSources();
                DateTime timestamp = RecordedFrameClock.Now;
                bool readJob = allowPoseJob && poseStart >= 0 && !poseJobFailed &&
                    (posePending || completedFrame == frameNumber);
                AsyncSwarmWriter.Frame packet = null;
                if (allowPoseJob && stockScheduledFrame == frameNumber && !poseJobFailed)
                {
                    FinishPoseCapture();
                    if (completedFrame == frameNumber && stockCapture != null)
                        packet = stockCapture.RentFrame(writer, agents, verifyStockPackets);
                }
                var frame = packet ?? writer.Rent(agents.Count);
                frame.timestamp = timestamp; frame.unityFrame = frameNumber;
                frame.skyboxId = NightSkyController.CurrentId;
                frame.skyboxSampleUtc = NightSkyController.CurrentSampleUtc;
                frame.phase = MainController.RecordedExperimentPhase;
                if (packet != null) JobPoseRows = StockPacketRows = frame.count;
                else CaptureAgents(frame, readJob);
                if (!writer.Submit(frame)) StopForEvidenceFailure("The bounded population recording queue could not accept the rendered frame.");
            }
            finally { FinishPoseCapture(); }
        }
        CaptureMilliseconds = ElapsedMilliseconds(start);
    }

    private void CaptureAgents(AsyncSwarmWriter.Frame frame, bool readJob)
    {
        bool pending = readJob && posePending;
        for (int i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            if (agent.Transform == null || !agent.Object.activeInHierarchy) continue;
            ref var row = ref frame.agents[frame.count];
            // A slot cannot be reused while the worker still owns this rented frame.
            var animation = row.animationSnapshot as RecordedAnimationSnapshot ?? new RecordedAnimationSnapshot();
            Vector3 scale = agent.Transform.lossyScale;
            agent.Animation.CaptureSnapshot(animation, batchRenderer, agent.Index);
            row = new AsyncSwarmWriter.Agent { index = agent.Index, name = agent.Name, layer = agent.Layer,
                sx = scale.x, sy = scale.y, sz = scale.z, animationSnapshot = animation };
            if (pending) capturedIndices[frame.count] = i;
            else if (readJob) FillJobPose(i, frame.unityFrame, ref row);
            else ReadPoseOnMain(agent.Transform, ref row);
            frame.count++;
        }
        if (!pending) return;

        // Join before returning to Unity callbacks. The first logger overlaps its
        // managed evidence gathering with transform reads for every active rig.
        FinishPoseCapture();
        for (int i = 0; i < frame.count; i++)
            FillJobPose(capturedIndices[i], frame.unityFrame, ref frame.agents[i]);
    }

    private void FillJobPose(int index, int frame, ref AsyncSwarmWriter.Agent row)
    {
        var sample = completedFrame == frame ? poses[poseStart + index] : default;
        if (!sample.Valid || sample.Frame != frame)
        {
            ReadPoseOnMain(agents[index].Transform, ref row);
            return;
        }
        Vector3 euler = ReadEuler(index, sample.Rotation);
        if (verifyPoseJob) VerifyPose(agents[index], sample.Position, euler);
        WritePose(ref row, sample.Position, euler);
        JobPoseRows++;
    }

    private static void ReadPoseOnMain(Transform transform, ref AsyncSwarmWriter.Agent row)
    {
        transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
        WritePose(ref row, position, rotation.eulerAngles);
    }

    private static void WritePose(ref AsyncSwarmWriter.Agent row, Vector3 position, Vector3 euler)
    {
        row.x = position.x; row.y = position.y; row.z = position.z;
        row.rx = euler.x; row.ry = euler.y; row.rz = euler.z;
    }

    private static void VerifyPose(AgentSource agent, Vector3 position, Vector3 euler)
    {
        agent.Transform.GetPositionAndRotation(out Vector3 serialPosition, out Quaternion serialRotation);
        if (!SameVector(position, serialPosition) || !SameVector(euler, serialRotation.eulerAngles))
            throw new InvalidDataException("Parallel population pose differs from Unity for " + agent.Name + " on frame " + Time.frameCount);
    }

    private void StopForEvidenceFailure(string reason)
    {
        CloseForArchive();
        var error = new IOException("Swarm recording evidence could not be preserved: " + reason);
        if (MainController.Instance != null) MainController.Instance.FailRecordingEvidence(error);
        else Debug.LogException(error);
    }
    public void CloseForArchive() { closed = true; sourcesChanged = true; writer?.Complete(); }

    private void ReleaseStockCapture()
    {
        if (stockCapture == null) return;
        CompletePoseJob();
        stockCapture.Dispose();
        stockCapture = null;
        stockEligible = false;
        stockScheduledFrame = -1;
    }

    private void OnDestroy()
    {
        CloseForArchive();
        ReleaseStockCapture();
    }
}
