using System;
using System.Collections.Generic;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

/// <summary>A generated stock roster whose pose and animation samples can be recorded in bulk.</summary>
internal sealed class StockPopulationCapture : IDisposable
{
    private readonly struct Seed
    {
        internal readonly Quaternion Rotation;
        internal readonly Vector3 LocalScale;

        internal Seed(Quaternion rotation, Vector3 localScale)
        {
            Rotation = rotation;
            LocalScale = localScale;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Strict)]
    private struct ReadStock : IJobParallelForTransform
    {
        [ReadOnly] internal NativeArray<Seed> seeds;
        [ReadOnly] internal NativeArray<StockLocustPlayback.Playhead> playheads;
        [WriteOnly] internal NativeArray<AsyncSwarmWriter.StockSample> output;
        internal NativeQueue<byte>.ParallelWriter changed;
        internal int sampleCount;

        public void Execute(int index, TransformAccess transform)
        {
            if (transform.isValid)
            {
                var seed = seeds[index];
                transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
                var playhead = playheads[index];
                if (Same(rotation, seed.Rotation) && Same(transform.localScale, seed.LocalScale) &&
                    (uint)playhead.sample < (uint)sampleCount && math.isfinite(playhead.speed))
                {
                    output[index] = new AsyncSwarmWriter.StockSample {
                        Position = position, Phase = (float)playhead.sample / sampleCount, Speed = playhead.speed
                    };
                    return;
                }
            }
            changed.Enqueue(1);
        }
    }

    private sealed class Group : IDisposable
    {
        internal readonly StockLocustPlayback Playback;
        internal readonly int Start, Count;
        private TransformAccessArray transforms;
        private NativeArray<Seed> seeds;
        private NativeArray<AsyncSwarmWriter.StockSample> samples;
        private NativeQueue<byte> changed;

        internal Group(StockLocustPlayback playback, IReadOnlyList<SwarmLogger.AgentSource> agents,
            AsyncSwarmWriter.StockIdentity[] identities, int start)
        {
            Playback = playback;
            Start = start;
            Count = playback.AgentCount;
            try
            {
                var roots = new Transform[Count];
                seeds = new NativeArray<Seed>(Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < Count; i++)
                {
                    var agent = agents[Start + i];
                    roots[i] = agent.Transform;
                    agent.Transform.GetPositionAndRotation(out _, out Quaternion rotation);
                    seeds[i] = new Seed(rotation, agent.Transform.localScale);
                    identities[Start + i] = new AsyncSwarmWriter.StockIdentity(
                        agent.Name, agent.Layer, agent.Index, playback.StateHash, rotation.eulerAngles, agent.Transform.lossyScale);
                }
                transforms = new TransformAccessArray(roots);
                samples = new NativeArray<AsyncSwarmWriter.StockSample>(Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                changed = new NativeQueue<byte>(Allocator.Persistent);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal JobHandle Schedule() => new ReadStock {
            seeds = seeds, playheads = Playback.RecordingPlayheads,
            sampleCount = Playback.RecordingSampleCount, output = samples, changed = changed.AsParallelWriter()
        }.ScheduleReadOnly(transforms, 64);

        internal bool Changed => !changed.IsEmpty();
        internal void CopySamples(AsyncSwarmWriter.StockSample[] destination) =>
            NativeArray<AsyncSwarmWriter.StockSample>.Copy(samples, 0, destination, Start, Count);

        internal void Verify(IReadOnlyList<SwarmLogger.AgentSource> agents, AsyncSwarmWriter.StockIdentity[] identities)
        {
            for (int i = 0; i < Count; i++)
            {
                var agent = agents[Start + i];
                var identity = identities[Start + i];
                var sample = samples[i];
                agent.Transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
                Playback.ReadState(i, out float phase, out float speed);
                if (!agent.Object.activeInHierarchy || agent.Transform.parent != null ||
                    !Same(position, sample.Position) || !Same(rotation.eulerAngles, identity.Euler) ||
                    !Same(agent.Transform.lossyScale, identity.Scale) || !Same(phase, sample.Phase) ||
                    !Same(speed, sample.Speed) || identity.State != Playback.StateHash)
                    throw new InvalidDataException("Stock population packet differs from Unity for " + agent.Name + " on frame " + Time.frameCount);
            }
        }

        public void Dispose()
        {
            if (transforms.isCreated) transforms.Dispose();
            if (seeds.IsCreated) seeds.Dispose();
            if (samples.IsCreated) samples.Dispose();
            if (changed.IsCreated) changed.Dispose();
        }
    }

    private readonly List<Group> groups = new List<Group>();
    private static readonly Action<AsyncSwarmWriter.Frame> materialize = Materialize;
    private readonly AsyncSwarmWriter.StockIdentity[] identities;
    private readonly StockPopulationMembership[] membership;
    private bool invalidated;

    private StockPopulationCapture(int count)
    {
        identities = new AsyncSwarmWriter.StockIdentity[count];
        membership = new StockPopulationMembership[count];
    }

    internal static StockPopulationCapture TryCreate(IReadOnlyList<SwarmLogger.AgentSource> agents)
    {
        if (agents.Count == 0 || !agents[0].Animation.TryGetStockPacketBinding(out _, out _)) return null;
        var capture = new StockPopulationCapture(agents.Count);
        bool ready = false;
        try
        {
            int start = 0;
            while (start < agents.Count)
            {
                if (!agents[start].Animation.TryGetStockPacketBinding(out var playback, out int firstSlot) || firstSlot != 0 ||
                    playback.AgentCount > agents.Count - start) return null;
                for (int i = 0; i < playback.AgentCount; i++)
                {
                    var agent = agents[start + i];
                    if (agent.Transform == null || agent.Transform.parent != null || !agent.Object.activeInHierarchy ||
                        !agent.Animation.TryGetStockPacketBinding(out var binding, out int slot) || binding != playback || slot != i)
                        return null;
                    var member = agent.Object.GetComponent<StockPopulationMembership>();
                    if (member == null) member = agent.Object.AddComponent<StockPopulationMembership>();
                    if (!member.isActiveAndEnabled) return null;
                    capture.membership[start + i] = member;
                    member.Changed += capture.Invalidate;
                }
                capture.groups.Add(new Group(playback, agents, capture.identities, start));
                start += playback.AgentCount;
            }
            ready = true;
            return capture;
        }
        finally
        {
            if (!ready) capture.Dispose();
        }
    }

    internal bool CanSchedule
    {
        get
        {
            if (invalidated) return false;
            foreach (var group in groups)
                if (group.Playback == null || !group.Playback.UsesCache) return false;
            return true;
        }
    }

    internal void Schedule(ref JobHandle handle, ref bool pending)
    {
        foreach (var group in groups)
        {
            handle = JobHandle.CombineDependencies(handle, group.Schedule());
            pending = true;
        }
    }

    internal AsyncSwarmWriter.Frame RentFrame(AsyncSwarmWriter writer, IReadOnlyList<SwarmLogger.AgentSource> agents, bool verify)
    {
        if (!CanSchedule) return null;
        foreach (var group in groups)
        {
            if (!group.Changed) continue;
            Invalidate();
            return null;
        }

        if (verify)
            foreach (var group in groups) group.Verify(agents, identities);
        var frame = writer.RentStock(identities, materialize);
        foreach (var group in groups) group.CopySamples(frame.stockSamples);
        return frame;
    }

    // Called on the writer thread. The leased frame contains only managed plain data.
    internal static void Materialize(AsyncSwarmWriter.Frame frame)
    {
        if (frame.agents == null || frame.agents.Length < frame.count)
            frame.agents = new AsyncSwarmWriter.Agent[frame.count];
        for (int i = 0; i < frame.count; i++)
        {
            var identity = frame.stockIdentities[i];
            var sample = frame.stockSamples[i];
            var snapshot = frame.agents[i].animationSnapshot as RecordedAnimationSnapshot ?? new RecordedAnimationSnapshot();
            if (!snapshot.TrySetStockAnimator(identity.State, sample.Phase, sample.Speed))
                throw new InvalidDataException("Invalid stock animation state in population packet.");
            frame.agents[i] = new AsyncSwarmWriter.Agent {
                name = identity.Name, layer = identity.Layer, index = identity.Index, animationSnapshot = snapshot,
                x = sample.Position.x, y = sample.Position.y, z = sample.Position.z,
                rx = identity.Euler.x, ry = identity.Euler.y, rz = identity.Euler.z,
                sx = identity.Scale.x, sy = identity.Scale.y, sz = identity.Scale.z
            };
        }
    }

    private void Invalidate() => invalidated = true;

    private static bool Same(float a, float b) => math.asuint(a) == math.asuint(b);
    private static bool Same(Vector3 a, Vector3 b) => Same(a.x, b.x) && Same(a.y, b.y) && Same(a.z, b.z);
    private static bool Same(Quaternion a, Quaternion b) => Same(a.x, b.x) && Same(a.y, b.y) && Same(a.z, b.z) && Same(a.w, b.w);

    public void Dispose()
    {
        foreach (var member in membership)
            if (member != null) member.Changed -= Invalidate;
        foreach (var group in groups) group.Dispose();
    }
}
