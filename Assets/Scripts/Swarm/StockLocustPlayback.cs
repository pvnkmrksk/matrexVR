using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>One shared clip, with independent playheads for each stock locust.</summary>
[DefaultExecutionOrder(490)]
public sealed class StockLocustPlayback : MonoBehaviour
{
    private struct Playhead
    {
        public double phase;
        public float speed;
        public int sample;
    }

    [BurstCompile(FloatMode = FloatMode.Strict)]
    private struct AdvancePlayheads : IJobParallelFor
    {
        public NativeArray<Playhead> playheads;
        public double step;
        public int samples;

        public void Execute(int index)
        {
            var state = playheads[index];
            if (state.speed == 0) return;
            state.phase += step * state.speed;
            state.phase -= math.floor(state.phase);
            state.sample = NearestSample(state.phase, samples);
            playheads[index] = state;
        }
    }

    private readonly struct Binding
    {
        public readonly StockLocustPlayback batch;
        public readonly int index;
        public Binding(StockLocustPlayback batch, int index) { this.batch = batch; this.index = index; }
    }

    private static readonly Dictionary<int, Binding> bindings = new Dictionary<int, Binding>();
    private static readonly List<StockLocustPlayback> batches = new List<StockLocustPlayback>();
    private static int lastFrame = -1;
    private StockLocustClipCache clip;
    private Animator[] animators;
    private AnimateOnMove[] gates;
    private int[] agentIds;
    private NativeArray<Playhead> playheads;
    internal bool UsesCache => playheads.IsCreated;
    internal int StateHash => clip.StateHash;
    internal int AgentCount => UsesCache ? playheads.Length : 0;
    internal string ClipIdentity => clip.Identity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBatches()
    {
        batches.Clear();
        bindings.Clear();
        lastFrame = -1;
    }

    internal static StockLocustPlayback TryCreate(GameObject host, StockLocustClipCache clip,
        IReadOnlyList<GameObject> agents, IReadOnlyList<float> initialPhases)
    {
        if (initialPhases == null || initialPhases.Count != agents.Count) return null;
        var animators = new Animator[agents.Count];
        var gates = new AnimateOnMove[agents.Count];
        var ids = new int[agents.Count];
        for (int i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            var animator = agent.GetComponent<Animator>();
            int id = agent.GetInstanceID();
            if (animator == null || !animator.isActiveAndEnabled || bindings.ContainsKey(id)) return null;
            animators[i] = animator;
            gates[i] = agent.GetComponent<AnimateOnMove>();
            ids[i] = id;
        }

        var batch = host.AddComponent<StockLocustPlayback>();
        batch.clip = clip;
        batch.animators = animators;
        batch.gates = gates;
        batch.agentIds = ids;
        batch.playheads = new NativeArray<Playhead>(agents.Count, Allocator.Persistent);
        for (int i = 0; i < agents.Count; i++)
        {
            double phase = initialPhases[i] - math.floor(initialPhases[i]);
            batch.playheads[i] = new Playhead {
                phase = phase, sample = NearestSample(phase, clip.SampleCount), speed = animators[i].speed
            };
            bindings.Add(ids[i], new Binding(batch, i));
            if (gates[i] != null) gates[i].AttachClipPlayback(batch, i);
            animators[i].enabled = false;
        }
        batches.Add(batch);
        return batch;
    }

    private static int NearestSample(double phase, int count)
    {
        int sample = (int)(phase * count + .5);
        return sample == count ? 0 : sample;
    }

    internal int SampleIndex(int index) => playheads[index].sample;

    internal static bool TryGet(GameObject agent, out StockLocustPlayback batch, out int index)
    {
        if (bindings.TryGetValue(agent.GetInstanceID(), out var binding))
        {
            batch = binding.batch;
            index = binding.index;
            return true;
        }
        batch = null;
        index = 0;
        return false;
    }

    internal void ReadState(int index, out float normalizedTime, out float speed)
    {
        var state = playheads[index];
        normalizedTime = clip.SamplePhase(state.sample);
        speed = state.speed;
    }

    internal void SetSpeed(int index, float speed)
    {
        var state = playheads[index];
        state.speed = speed;
        playheads[index] = state;
    }

    private void LateUpdate()
    {
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        float seconds = Time.deltaTime;
        if (seconds <= 0) return;
        using var timing = RuntimePerformanceProbe.Measure(RuntimePerformanceProbe.CpuStage.Playheads);
        JobHandle all = default;
        try
        {
            foreach (var batch in batches)
            {
                var job = new AdvancePlayheads {
                    playheads = batch.playheads, step = (double)seconds / batch.clip.Length, samples = batch.clip.SampleCount
                };
                all = JobHandle.CombineDependencies(all, job.Schedule(batch.playheads.Length, 64));
            }
        }
        finally { all.Complete(); }
    }

    internal void RestoreNative() => Release(true);

    internal void Discard() => Release(false);

    private void Release(bool restoreNative)
    {
        if (!UsesCache) return;
        batches.Remove(this);
        for (int i = 0; i < animators.Length; i++)
        {
            bindings.Remove(agentIds[i]);
            var animator = animators[i];
            if (gates[i] != null) gates[i].DetachClipPlayback(this);
            if (!restoreNative || animator == null) continue;
            ReadState(i, out float phase, out float speed);
            animator.enabled = true;
            animator.speed = speed;
            if (!animator.isActiveAndEnabled) continue;
            animator.Play(clip.StateHash, 0, phase);
            animator.Update(0);
        }
        playheads.Dispose();
    }

    private void OnDisable() => RestoreNative();
}
