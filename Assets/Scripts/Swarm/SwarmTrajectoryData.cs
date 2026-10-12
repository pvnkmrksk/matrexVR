using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>Immutable, shared across rigs. Canonical CSV: ID,X,Y,Z,time; metres and relative seconds unless dataUnitsPerMetre overrides the distance scale.</summary>
public sealed class SwarmTrajectoryData
{
    public struct Sample { public double time; public Vector3 position; }
    public sealed class Track
    {
        public string id;
        public Sample[] samples;
        public bool TrySample(double time, double maxGap, out Vector3 position, out Vector3 velocity)
        {
            position = velocity = Vector3.zero;
            if (time < samples[0].time || time > samples[samples.Length - 1].time) return false;
            int lo = 0, hi = samples.Length - 1;
            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (samples[mid].time <= time) lo = mid; else hi = mid - 1; }
            int next = Math.Min(lo + 1, samples.Length - 1);
            Sample a = samples[lo], b = samples[next];
            if (next == lo && lo > 0) { a = samples[lo - 1]; b = samples[lo]; }
            double gap = b.time - a.time;
            if (gap > maxGap && time != samples[lo].time) return false;
            position = gap > 0 ? Vector3.Lerp(a.position, b.position, (float)((time - a.time) / gap)) : a.position;
            velocity = gap > 0 ? (b.position - a.position) / (float)gap : Vector3.zero;
            return true;
        }
    }
    public Track[] Tracks { get; private set; }
    public Bounds Bounds { get; private set; }
    public Vector3 Centroid { get; private set; }
    public double Duration { get; private set; }
    private static readonly Dictionary<string, SwarmTrajectoryData> cache = new Dictionary<string, SwarmTrajectoryData>();

    public static SwarmTrajectoryData Load(string path)
    {
        string text=ExperimentConfigFiles.ReadAllText(path,"trajectory");
        string key=Path.GetFullPath(path)+"|"+RunConfigArchive.Hash(System.Text.Encoding.UTF8.GetBytes(text));
        if (cache.TryGetValue(key, out var data)) return data;
        data = Parse(text.Split('\n')); cache[key] = data; return data;
    }
    public static SwarmTrajectoryData Parse(IEnumerable<string> lines)
    {
        var groups = new Dictionary<string, List<Sample>>();
        bool first = true; int row = 0, count = 0; double sumX = 0, sumY = 0, sumZ = 0; Bounds bounds = default;
        double duration = 0;
        foreach (string line in lines)
        {
            row++; if (string.IsNullOrWhiteSpace(line)) continue;
            string[] f = line.Split(',');
            if (first)
            {
                first = false;
                if (string.Join(",", f).Trim().TrimStart('\ufeff') != "ID,X,Y,Z,time")
                    throw new FormatException("Trajectory header must be ID,X,Y,Z,time. Run tools/convert_swarm_trajectories.py first.");
                continue;
            }
            if (f.Length != 5 || string.IsNullOrWhiteSpace(f[0])) throw new FormatException("Malformed trajectory row " + row);
            Vector3 p = new Vector3(ParseFloat(f[1]), ParseFloat(f[2]), ParseFloat(f[3]));
            double time = double.Parse(f[4], CultureInfo.InvariantCulture);
            if (double.IsNaN(time) || double.IsInfinity(time) || time < 0) throw new FormatException("Invalid trajectory time at row " + row);
            if (!groups.TryGetValue(f[0], out var list)) groups[f[0]] = list = new List<Sample>();
            list.Add(new Sample { time = time, position = p });
            if (count++ == 0) bounds = new Bounds(p, Vector3.zero); else bounds.Encapsulate(p);
            sumX += p.x; sumY += p.y; sumZ += p.z; duration = Math.Max(duration, time);
        }
        if (count == 0 || duration <= 0) throw new FormatException("Trajectory needs samples and a positive duration.");
        var tracks = new List<Track>();
        foreach (var pair in groups)
        {
            pair.Value.Sort((a, b) => a.time.CompareTo(b.time));
            for (int i = 1; i < pair.Value.Count; i++)
                if (pair.Value[i].time == pair.Value[i - 1].time) throw new FormatException("Duplicate ID/time: " + pair.Key);
            tracks.Add(new Track { id = pair.Key, samples = pair.Value.ToArray() });
        }
        return new SwarmTrajectoryData { Tracks = tracks.ToArray(), Bounds = bounds,
            Centroid = new Vector3((float)(sumX / count), (float)(sumY / count), (float)(sumZ / count)), Duration = duration };
    }
    private static float ParseFloat(string text)
    {
        float v = float.Parse(text, CultureInfo.InvariantCulture);
        if (float.IsNaN(v) || float.IsInfinity(v)) throw new FormatException("Nonfinite trajectory position.");
        return v;
    }
}
