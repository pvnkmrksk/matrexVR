using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Loads RunData CSV traces and optional sequence metadata for replay.
/// </summary>
public class ReplaySessionData
{
    public class Sample
    {
        public float t;
        public Vector3 pos;
        public Vector3 rot;
        public Quaternion rotQuat;
        public string stepName;
        public int stepIndex;
    }

    public class StepMarker
    {
        public float t;
        public string name;
        public int index;
        public string sceneName;
    }

    public class SequenceStepInfo
    {
        public string sceneName;
        public float duration;
        public string configFile;
    }

    public readonly Dictionary<string, List<Sample>> SamplesByRig = new();
    public readonly List<StepMarker> StepMarkers = new();
    public readonly List<SequenceStepInfo> PlannedSequence = new();
    public float MaxTime { get; private set; }
    public string SessionPath { get; private set; }
    public string DesignFileName { get; private set; }

    public bool Load(string sessionPath)
    {
        SessionPath = sessionPath;
        SamplesByRig.Clear();
        StepMarkers.Clear();
        PlannedSequence.Clear();
        MaxTime = 0f;

        if (!Directory.Exists(sessionPath))
            return false;

        foreach (string file in Directory.GetFiles(sessionPath, "*.csv"))
        {
            try
            {
                LoadCsv(file);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ReplaySession] CSV {file}: {ex.Message}");
            }
        }

        foreach (var list in SamplesByRig.Values)
        {
            if (list.Count > 0)
                MaxTime = Mathf.Max(MaxTime, list[^1].t);
        }

        LoadSequenceMetadata(sessionPath);
        BuildStepMarkers();
        DesignFileName = ResolveDesignFile(sessionPath);
        return SamplesByRig.Count > 0 && MaxTime > 0f;
    }

    public string GetReferenceRig()
    {
        foreach (var k in SamplesByRig.Keys)
            return k;
        return null;
    }

    public void GetPoseAtTime(string rig, float time, out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero;
        rot = Quaternion.identity;
        if (!SamplesByRig.TryGetValue(rig, out var list) || list.Count == 0)
            return;

        int lo = 0;
        int hi = list.Count - 1;
        if (time <= list[0].t)
        {
            pos = list[0].pos;
            rot = list[0].rotQuat;
            return;
        }
        if (time >= list[hi].t)
        {
            pos = list[hi].pos;
            rot = list[hi].rotQuat;
            return;
        }

        while (lo < hi - 1)
        {
            int mid = (lo + hi) >> 1;
            if (list[mid].t <= time)
                lo = mid;
            else
                hi = mid;
        }

        Sample a = list[lo];
        Sample b = list[hi];
        float span = Mathf.Max(0.0001f, b.t - a.t);
        float u = Mathf.Clamp01((time - a.t) / span);
        pos = Vector3.Lerp(a.pos, b.pos, u);
        rot = Quaternion.Slerp(a.rotQuat, b.rotQuat, u);
    }

    public int FindStepIndexAtTime(float time)
    {
        int idx = 0;
        for (int i = 0; i < StepMarkers.Count; i++)
        {
            if (StepMarkers[i].t <= time)
                idx = i;
            else
                break;
        }
        return idx;
    }

    private void BuildStepMarkers()
    {
        StepMarkers.Clear();
        string rig = GetReferenceRig();
        if (string.IsNullOrEmpty(rig) || !SamplesByRig.TryGetValue(rig, out var list))
            return;

        string lastName = list[0].stepName;
        int lastIdx = list[0].stepIndex;
        StepMarkers.Add(new StepMarker { t = list[0].t, name = lastName, index = lastIdx });

        for (int i = 1; i < list.Count; i++)
        {
            if (list[i].stepName != lastName || list[i].stepIndex != lastIdx)
            {
                lastName = list[i].stepName;
                lastIdx = list[i].stepIndex;
                StepMarkers.Add(new StepMarker { t = list[i].t, name = lastName, index = lastIdx });
            }
        }

        // Fallback: planned sequence boundaries if CSV lacks stepName
        if (StepMarkers.Count <= 1 && PlannedSequence.Count > 0)
        {
            StepMarkers.Clear();
            float t = 0f;
            for (int i = 0; i < PlannedSequence.Count; i++)
            {
                var s = PlannedSequence[i];
                StepMarkers.Add(new StepMarker
                {
                    t = t,
                    name = s.configFile ?? s.sceneName,
                    index = i,
                    sceneName = s.sceneName
                });
                t += s.duration;
            }
        }
    }

    private void LoadSequenceMetadata(string sessionPath)
    {
        try
        {
            string[] cfgs = Directory.GetFiles(sessionPath, "*_sequenceConfig.json");
            if (cfgs.Length == 0)
                return;

            var config = JsonConvert.DeserializeObject<SequenceConfigCopy>(File.ReadAllText(cfgs[0]));
            if (config?.sequences == null)
                return;

            foreach (var seq in config.sequences)
            {
                string cfg = null;
                if (seq.parameters != null)
                {
                    if (seq.parameters.TryGetValue("configFile", out var cf))
                        cfg = cf?.ToString();
                    else if (seq.parameters.TryGetValue("design", out var d))
                        cfg = d?.ToString();
                }
                PlannedSequence.Add(new SequenceStepInfo
                {
                    sceneName = seq.sceneName,
                    duration = seq.duration,
                    configFile = cfg
                });
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ReplaySession] sequenceConfig: {ex.Message}");
        }
    }

    private static string ResolveDesignFile(string sessionPath)
    {
        try
        {
            string[] cfgs = Directory.GetFiles(sessionPath, "*_sequenceConfig.json");
            if (cfgs.Length == 0)
                return null;
            var config = JsonConvert.DeserializeObject<SequenceConfigCopy>(File.ReadAllText(cfgs[0]));
            if (config?.sequences == null)
                return null;
            foreach (var seq in config.sequences)
            {
                if (seq.parameters == null)
                    continue;
                if (seq.parameters.TryGetValue("design", out var design))
                    return design?.ToString();
                if (seq.parameters.TryGetValue("configFile", out var cf))
                    return cf?.ToString();
            }
        }
        catch { }
        return null;
    }

    private void LoadCsv(string path)
    {
        string[] lines = File.ReadAllLines(path);
        if (lines.Length < 2)
            return;

        string[] headers = lines[0].Split(',');
        int idxTime = Array.IndexOf(headers, "Current Time");
        int idxVr = Array.IndexOf(headers, "VR");
        int idxPosX = Array.IndexOf(headers, "GameObjectPosX");
        int idxPosY = Array.IndexOf(headers, "GameObjectPosY");
        int idxPosZ = Array.IndexOf(headers, "GameObjectPosZ");
        int idxRotX = Array.IndexOf(headers, "GameObjectRotX");
        int idxRotY = Array.IndexOf(headers, "GameObjectRotY");
        int idxRotZ = Array.IndexOf(headers, "GameObjectRotZ");
        int idxStepName = Array.IndexOf(headers, "stepName");
        int idxStepIndex = Array.IndexOf(headers, "stepIndex");

        if (idxTime < 0 || idxVr < 0 || idxPosX < 0)
            return;

        DateTime? first = null;
        foreach (string line in lines[1..])
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            string[] parts = line.Split(',');
            if (!DateTime.TryParse(parts[idxTime], CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var ts))
                continue;
            if (first == null)
                first = ts;

            float t = (float)(ts - first.Value).TotalSeconds;
            var sample = new Sample
            {
                t = t,
                pos = new Vector3(ParseF(parts, idxPosX), ParseF(parts, idxPosY), ParseF(parts, idxPosZ)),
                rot = new Vector3(
                    ParseF(parts, idxRotX),
                    ParseF(parts, idxRotY),
                    ParseF(parts, idxRotZ)),
                stepName = idxStepName >= 0 && idxStepName < parts.Length ? parts[idxStepName] : "",
                stepIndex = idxStepIndex >= 0 && idxStepIndex < parts.Length ? ParseI(parts[idxStepIndex]) : -1
            };
            sample.rotQuat = Quaternion.Euler(sample.rot);

            string rig = parts[idxVr];
            if (!SamplesByRig.TryGetValue(rig, out var list))
            {
                list = new List<Sample>();
                SamplesByRig[rig] = list;
            }
            list.Add(sample);
        }
    }

    private static float ParseF(string[] p, int i) =>
        i >= 0 && i < p.Length && float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;

    private static int ParseI(string s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : -1;

    [Serializable]
    private class SequenceConfigCopy
    {
        public SequenceItem[] sequences;
    }

    [Serializable]
    private class SequenceItem
    {
        public string sceneName;
        public float duration;
        public Dictionary<string, object> parameters;
    }
}
