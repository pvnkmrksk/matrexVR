using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Recorded births anchored to the animal once, with independent world-space tails.
/// No periodic wrapping. A new loop can spawn an ID while its previous instance is still flying.</summary>
public sealed class SwarmTrajectoryStream
{
    private sealed class Segment
    {
        public SwarmTrajectoryData.Track track;
        public int first, last, ordinal;
        public Vector3 initialVelocity, terminalVelocity;
        public double SpawnTime;
        public double Start => track.samples[first].time;
        public double End => track.samples[last].time;
        public void Sample(double time, out Vector3 position, out Vector3 velocity)
        {
            var samples = track.samples;
            if (time < Start)
            {
                velocity = initialVelocity;
                position = samples[first].position + velocity * (float)(time - Start);
                return;
            }
            if (time >= End)
            {
                velocity = terminalVelocity;
                position = samples[last].position + velocity * (float)(time - End);
                return;
            }
            int lo = first, hi = last;
            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (samples[mid].time <= time) lo = mid; else hi = mid - 1; }
            int next = Math.Min(lo + 1, last);
            double dt = samples[next].time - samples[lo].time;
            velocity = dt > 0 ? (samples[next].position - samples[lo].position) / (float)dt : Vector3.zero;
            position = samples[lo].position + velocity * (float)Math.Max(0, time - samples[lo].time);
        }
    }
    private sealed class Flight
    {
        public GameObject root;
        public Segment segment;
        public Vector3 anchor;
        public double cycleStart;
        public bool active;
    }
    private readonly SwarmTrajectoryData data;
    private readonly SwarmPlaybackConfig config;
    private readonly Transform animal;
    private readonly Vector3 offset, fixedOrigin;
    private readonly Quaternion rotation;
    private readonly float scale, distanceSquared;
    private readonly Func<GameObject> growPool;
    private readonly Action changed;
    private readonly List<Segment> segments = new List<Segment>();
    private readonly List<Flight> pool = new List<Flight>();
    private readonly List<Flight> flying = new List<Flight>();
    private readonly Queue<Flight> free = new Queue<Flight>();
    private double previous = double.NaN;
    private long nextCycle;
    private int nextSegment;
    public int ActiveCount { get { int count=0; foreach(var f in flying) if(f.root.activeSelf) count++; return count; } }
    public long TotalBirths { get; private set; }

    public SwarmTrajectoryStream(SwarmTrajectoryData source, SwarmPlaybackConfig settings,
        IReadOnlyList<GameObject> roots, Transform target, Vector3 centerOffset, Quaternion yaw,
        float worldUnitsPerMetre, Vector3 fallbackOrigin, Func<GameObject> createAgent, Action populationChanged)
    {
        data = source; config = settings; animal = target; offset = centerOffset; rotation = yaw;
        fixedOrigin = target != null ? target.position : fallbackOrigin;
        scale = worldUnitsPerMetre / config.dataUnitsPerMetre;
        distanceSquared = Mathf.Pow(config.despawnDistanceMetres * worldUnitsPerMetre, 2);
        growPool = createAgent; changed = populationChanged;
        foreach (var root in roots) pool.Add(new Flight { root = root });
        foreach (var track in data.Tracks)
        {
            int first = 0, ordinal = 0;
            for (int i = 1; i <= track.samples.Length; i++)
            {
                if (i < track.samples.Length && track.samples[i].time - track.samples[i-1].time <= config.maxGapSeconds + 1e-8) continue;
                var segment = new Segment { track = track, first = first, last = i - 1, ordinal = ordinal++ };
                segment.SpawnTime = segment.Start - config.preRollSeconds;
                int terminalStart = segment.last;
                // Average only within this uninterrupted track segment; never bridge a missing-data gap.
                if (terminalStart > first)
                {
                    int initialEnd=first+1;
                    while(initialEnd<segment.last && track.samples[initialEnd].time < segment.Start+config.terminalVelocityWindowSeconds) initialEnd++;
                    double initialDt=track.samples[initialEnd].time-segment.Start;
                    segment.initialVelocity=(track.samples[initialEnd].position-track.samples[first].position)/(float)initialDt;
                    terminalStart--;
                    double cutoff = segment.End - config.terminalVelocityWindowSeconds;
                    while (terminalStart > first && track.samples[terminalStart].time > cutoff) terminalStart--;
                    double dt = segment.End - track.samples[terminalStart].time;
                    segment.terminalVelocity = (track.samples[segment.last].position - track.samples[terminalStart].position) / (float)dt;
                    if (config.continuationSpeedMetresPerSecond.HasValue && segment.terminalVelocity.sqrMagnitude > 1e-12f)
                        segment.terminalVelocity = segment.terminalVelocity.normalized *
                            config.continuationSpeedMetresPerSecond.Value * config.dataUnitsPerMetre;
                    if (config.continuationSpeedMetresPerSecond.HasValue && segment.initialVelocity.sqrMagnitude > 1e-12f)
                        segment.initialVelocity=segment.initialVelocity.normalized *
                            config.continuationSpeedMetresPerSecond.Value * config.dataUnitsPerMetre;
                }
                segments.Add(segment); first = i;
            }
        }
        segments.Sort((a,b) => { int cmp = a.Start.CompareTo(b.Start); return cmp != 0 ? cmp : string.CompareOrdinal(a.track.id,b.track.id); });
    }

    public void Advance(double rawTime)
    {
        if (double.IsNaN(previous) || rawTime < previous)
        {
            Reset();
            long cycle = config.loop ? (long)Math.Floor(rawTime / data.Duration) : 0;
            long lastCycle=config.loop ? (long)Math.Floor((rawTime+config.preRollSeconds)/data.Duration) : 0;
            // Seek can initialize incoming tracks too, but cannot reconstruct past animal anchors/tails.
            for(long c=cycle;c<=lastCycle;c++)
            {
                double cycleStart=c*data.Duration, time=rawTime-cycleStart;
                foreach (var segment in segments)
                    if (segment.SpawnTime <= time && segment.End >= time) Spawn(segment,c,cycleStart,rawTime);
            }
            nextCycle=lastCycle;
            nextSegment=FirstAfter(rawTime-nextCycle*data.Duration);
            if(nextSegment==segments.Count) {nextCycle++;nextSegment=0;}
        }
        else
        {
            UpdateFlights(rawTime);
            // A cursor replaces an O(all recorded segments) scan every frame.
            while((config.loop || nextCycle==0) && nextCycle*data.Duration+segments[nextSegment].SpawnTime<=rawTime)
            {
                Spawn(segments[nextSegment],nextCycle,nextCycle*data.Duration,rawTime);
                if(++nextSegment==segments.Count) {nextCycle++;nextSegment=0;}
            }
        }
        previous = rawTime;
    }
    private int FirstAfter(double time)
    {
        int lo=0,hi=segments.Count;
        while(lo<hi) {int mid=(lo+hi)/2; if(segments[mid].SpawnTime<=time) lo=mid+1; else hi=mid;}
        return lo;
    }
    private void Reset()
    {
        flying.Clear(); free.Clear(); TotalBirths = 0;
        foreach (var flight in pool) { flight.active = false; flight.root.SetActive(false); free.Enqueue(flight); }
    }
    private void Spawn(Segment segment, long cycle, double cycleStart, double rawTime)
    {
        Flight flight;
        if (free.Count > 0) flight = free.Dequeue();
        else
        {
            if (growPool == null) throw new InvalidOperationException("Playback pool needs a factory to retain overlapping loop instances.");
            flight = new Flight { root = growPool() }; pool.Add(flight);
        }
        flight.segment = segment; flight.cycleStart = cycleStart;
        Vector3 origin = animal != null ? animal.position : fixedOrigin;
        flight.anchor = config.centerOnAnimal ? origin + offset - rotation * (data.Centroid * scale) : offset;
        string prefix = flight.root.name;
        int marker = prefix.IndexOf("_ID_", StringComparison.Ordinal);
        prefix = marker >= 0 ? prefix.Substring(0,marker) : "Playback";
        flight.root.name = prefix + "_ID_" + segment.track.id + (cycle == 0 && segment.ordinal == 0 ? "" : "_C" + cycle + "_S" + segment.ordinal);
        changed?.Invoke();
        flight.active = true; flying.Add(flight); flight.root.SetActive(true);
        TotalBirths++;
        if (!Place(flight,rawTime)) Retire(flying.Count - 1);
    }
    private void UpdateFlights(double rawTime)
    {
        for (int i = flying.Count - 1; i >= 0; i--)
            if (!Place(flying[i],rawTime)) Retire(i);
    }
    private bool Place(Flight flight, double rawTime)
    {
        double time=rawTime-flight.cycleStart;
        flight.segment.Sample(time, out Vector3 position, out Vector3 velocity);
        position = flight.anchor + rotation * (position * scale);
        Vector3 observer = animal != null ? animal.position : fixedOrigin;
        if ((position - observer).sqrMagnitude > distanceSquared)
        {
            // A long incoming lead can begin outside the cutoff. Keep its scheduled flight
            // pending so it can enter; do not draw it or discard its later measured samples.
            flight.root.SetActive(false);
            return time<flight.segment.Start;
        }
        if(!flight.root.activeSelf) flight.root.SetActive(true);
        flight.root.transform.position = position;
        Vector3 forward = rotation * velocity;
        if (forward.sqrMagnitude > 1e-8f) flight.root.transform.rotation = Quaternion.LookRotation(forward,Vector3.up);
        return true;
    }
    private void Retire(int index)
    {
        var flight = flying[index]; flight.active = false; flight.root.SetActive(false);
        flying.RemoveAt(index); free.Enqueue(flight);
    }
}
