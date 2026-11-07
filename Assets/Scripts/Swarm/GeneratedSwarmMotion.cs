using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

/// <summary>Advance generated swarms after observer movement and before animation, rendering and logging.</summary>
[DefaultExecutionOrder(500)]
public sealed class GeneratedSwarmMotion : MonoBehaviour
{
    private const int MinimumJobSize = 128;
    private const int AnimationCohorts = 10;
    private static readonly bool UseStaggeredAnimation =
        System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--per-frame-swarm-animation-gate") < 0;
    private static readonly bool UseParallelMotion =
        System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--serial-swarm-motion") < 0;
    private static readonly List<GeneratedSwarmMotion> parallelGroups = new List<GeneratedSwarmMotion>();
    private static int scheduledFrame = -1;

    private LocustMover[] movers;
    private BoundaryManager boundary;
    private TransformAccessArray jobTransforms;
    private NativeArray<MotionInput> jobInputs;
    private GameObject[] jobAgents;
    private AnimateOnMove[] animationGates;
    private NativeArray<AnimationSample> animationSamples;
    private bool preferStaggeredAnimation;
    private int nextAnimationCohort;
    private bool animationSamplesPaused;
    public bool UsesParallelMotion => jobInputs.IsCreated;
    public bool UsesStaggeredAnimation => animationSamples.IsCreated;

    private struct MotionInput
    {
        public float distance;
        public byte active;
    }

    [BurstCompile(FloatMode = FloatMode.Strict)]
    private struct MotionJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<MotionInput> inputs;
        public BoundaryManager.WrappingFrame bounds;

        public void Execute(int index, TransformAccess agent)
        {
            var input = inputs[index];
            if (input.active == 0 || !agent.isValid) return;
            Move(agent, input.distance, bounds);
        }
    }

    private struct AnimationSample
    {
        public Vector3 position;
        public float travel, elapsed;
        public bool seen;
    }

    [BurstCompile(FloatMode = FloatMode.Strict)]
    private struct MotionWithAnimationSamplesJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<MotionInput> inputs;
        public NativeArray<AnimationSample> samples;
        public BoundaryManager.WrappingFrame bounds;
        public Vector2 wrap;
        public float seconds;

        public void Execute(int index, TransformAccess agent)
        {
            var input = inputs[index];
            if (input.active == 0 || !agent.isValid)
            {
                samples[index] = default;
                return;
            }
            var position = Move(agent, input.distance, bounds);
            var sample = samples[index];
            if (seconds <= 0) sample.travel = sample.elapsed = 0;
            else if (sample.seen)
            {
                var delta = position - sample.position;
                // Unwrap each step before accumulating so a longer sample interval
                // cannot turn a full traversal into an apparent stop.
                if (wrap.x > 0) delta.x = Kannadi.Wrap(delta.x, wrap.x);
                if (wrap.y > 0) delta.z = Kannadi.Wrap(delta.z, wrap.y);
                sample.travel += new Vector2(delta.x, delta.z).magnitude;
                sample.elapsed += seconds;
            }
            sample.position = position;
            sample.seen = true;
            samples[index] = sample;
        }
    }

    private static Vector3 Move(TransformAccess agent, float distance, in BoundaryManager.WrappingFrame bounds)
    {
        Vector3 current = agent.position;
        Vector3 next = LocustMover.NextPosition(current, distance != 0 ? agent.rotation : default, distance, bounds);
        if (!next.Equals(current)) agent.position = next;
        return next;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetParallelGroups()
    {
        parallelGroups.Clear();
        scheduledFrame = -1;
    }

    public void Configure(IReadOnlyList<GameObject> agents, BoundaryManager bounds, bool stockAnimation = false)
    {
        ReleaseJobBuffers();
        boundary = bounds;
        preferStaggeredAnimation = UseStaggeredAnimation && stockAnimation;
        movers = new LocustMover[agents.Count];
        for (int i = 0; i < agents.Count; i++)
        {
            movers[i] = agents[i].GetComponent<LocustMover>();
            if (movers[i] != null) movers[i].enabled = false;
        }
        if (!UseParallelMotion || agents.Count < MinimumJobSize) return;

        // Generated agents are independent root transforms. Retain serial
        // ordering for any authored hierarchy or incomplete set of movers.
        var transforms = new Transform[agents.Count];
        for (int i = 0; i < transforms.Length; i++)
        {
            if (movers[i] == null || movers[i].transform.parent != null) return;
            transforms[i] = movers[i].transform;
        }

        jobAgents = new GameObject[agents.Count];
        for (int i = 0; i < jobAgents.Length; i++) jobAgents[i] = agents[i];
        jobTransforms = new TransformAccessArray(transforms, Mathf.Max(1, agents.Count / 64));
        jobInputs = new NativeArray<MotionInput>(agents.Count, Allocator.Persistent);
        if (isActiveAndEnabled) RegisterParallelMotion();
    }

    private void OnEnable()
    {
        if (UsesParallelMotion) RegisterParallelMotion();
    }

    private void RegisterParallelMotion()
    {
        parallelGroups.Add(this);
        if (!preferStaggeredAnimation) return;

        var gates = new AnimateOnMove[jobAgents.Length];
        for (int i = 0; i < gates.Length; i++)
        {
            if (jobAgents[i] == null) return;
            var gate = jobAgents[i].GetComponent<AnimateOnMove>();
            if (gate == null || !gate.CanSampleExternally) return;
            gates[i] = gate;
        }
        animationSamples = new NativeArray<AnimationSample>(gates.Length, Allocator.Persistent);
        animationGates = gates;
        animationSamplesPaused = false;
        nextAnimationCohort = jobAgents[0].layer % AnimationCohorts;
        for (int i = 0; i < gates.Length; i++)
        {
            animationSamples[i] = new AnimationSample { position = jobAgents[i].transform.position, seen = true };
            gates[i].BeginExternalSampling();
        }
    }

    private void OnDisable()
    {
        parallelGroups.Remove(this);
        ReleaseAnimationSamples();
    }

    private void ReleaseAnimationSamples()
    {
        if (!animationSamples.IsCreated) return;
        foreach (var gate in animationGates)
            if (gate != null) gate.EndExternalSampling();
        animationGates = null;
        animationSamples.Dispose();
    }
    private void OnDestroy() => ReleaseJobBuffers();

    private void ReleaseJobBuffers()
    {
        parallelGroups.Remove(this);
        ReleaseAnimationSamples();
        if (jobTransforms.isCreated) jobTransforms.Dispose();
        if (jobInputs.IsCreated) jobInputs.Dispose();
        jobAgents = null;
    }

    private void LateUpdate()
    {
        if (!UsesParallelMotion)
        {
            Advance(Time.deltaTime);
            return;
        }
        if (scheduledFrame == Time.frameCount) return;
        scheduledFrame = Time.frameCount;

        // Dispatch independent groups together, then publish their transforms
        // before any later animation, camera, telemetry or recorder callback.
        JobHandle all = default;
        float seconds = Time.deltaTime;
        try
        {
            foreach (var group in parallelGroups)
                all = JobHandle.CombineDependencies(all, group.ScheduleMotion(seconds));
        }
        finally { all.Complete(); }
        foreach (var group in parallelGroups) group.PublishAnimationSamples(seconds);
    }

    private void PublishAnimationSamples(float seconds)
    {
        if (!animationSamples.IsCreated) return;
        if (seconds <= 0)
        {
            if (!animationSamplesPaused)
                foreach (var gate in animationGates)
                    if (gate != null) gate.ApplySampledSpeed(0);
            animationSamplesPaused = true;
            return;
        }
        animationSamplesPaused = false;
        for (int i = nextAnimationCohort; i < animationGates.Length; i += AnimationCohorts)
        {
            var sample = animationSamples[i];
            if (animationGates[i] == null) continue;
            animationGates[i].ApplySampledSpeed(sample.elapsed > 0 ? sample.travel / sample.elapsed : 0);
            sample.travel = sample.elapsed = 0;
            animationSamples[i] = sample;
        }
        nextAnimationCohort = (nextAnimationCohort + 1) % AnimationCohorts;
    }

    private JobHandle ScheduleMotion(float seconds)
    {
        for (int i = 0; i < movers.Length; i++)
        {
            var mover = movers[i];
            jobInputs[i] = mover != null && jobAgents[i].activeInHierarchy
                ? new MotionInput { distance = mover.speed * seconds, active = 1 }
                : default;
        }
        var bounds = CurrentBounds();
        if (!animationSamples.IsCreated)
            return new MotionJob { inputs = jobInputs, bounds = bounds }.Schedule(jobTransforms);
        return new MotionWithAnimationSamplesJob {
            inputs = jobInputs, samples = animationSamples, bounds = bounds, seconds = seconds,
            wrap = boundary != null ? new Vector2(boundary.LengthX, boundary.LengthZ) : Vector2.zero
        }.Schedule(jobTransforms);
    }

    private BoundaryManager.WrappingFrame CurrentBounds() =>
        boundary != null ? new BoundaryManager.WrappingFrame(boundary) : default;

    public void Advance(float seconds)
    {
        if (movers == null) return;
        if (UsesParallelMotion)
        {
            ScheduleMotion(seconds).Complete();
            PublishAnimationSamples(seconds);
            return;
        }
        var frame = CurrentBounds();
        for (int i = 0; i < movers.Length; i++)
            if (movers[i] != null && movers[i].gameObject.activeInHierarchy) movers[i].Advance(seconds, frame);
    }
    public static float SampleSpeed(float mean, float standardDeviation, System.Random deterministic = null)
    {
        if (standardDeviation == 0) return mean; // Do not alter the historical random sequence.
        if (float.IsNaN(mean) || float.IsInfinity(mean) || mean < 0 ||
            float.IsNaN(standardDeviation) || float.IsInfinity(standardDeviation) || standardDeviation < 0)
            throw new System.ArgumentException("Generated speed distribution requires a finite nonnegative mean and positive standard deviation.");
        float speed;
        do
        {
            float u = deterministic == null ? Random.value : (float)deterministic.NextDouble();
            float angle = deterministic == null ? Random.value : (float)deterministic.NextDouble();
            float normal = Mathf.Sqrt(-2f * Mathf.Log(Mathf.Max(1e-7f, u))) * Mathf.Cos(2f * Mathf.PI * angle);
            speed = mean + standardDeviation * normal;
        } while (speed < 0);
        return speed;
    }
}
