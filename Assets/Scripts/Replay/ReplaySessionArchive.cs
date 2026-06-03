using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Indexes all configs and metadata copied into a RunData session folder.
/// </summary>
public class ReplaySessionArchive
{
    public ReplaySessionData Data { get; private set; }
    public SequenceConfig Sequence { get; private set; }
    public Dictionary<string, SystemConfig> SystemConfigs { get; } = new();
    public int GlobalTargetDisplay { get; private set; } = 1;
    public string SessionPath { get; private set; }
    public string SequenceConfigPath { get; private set; }
    public string SystemConfigPath { get; private set; }

    private readonly Dictionary<string, string> _configByExactName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _configBySuffix = new(StringComparer.OrdinalIgnoreCase);

    public bool Load(string sessionPath)
    {
        SessionPath = sessionPath;
        _configByExactName.Clear();
        _configBySuffix.Clear();
        SystemConfigs.Clear();

        if (!Directory.Exists(sessionPath))
        {
            Debug.LogError($"[ReplayArchive] Missing folder: {sessionPath}");
            return false;
        }

        IndexJsonFiles(sessionPath);

        Data = new ReplaySessionData();
        if (!Data.Load(sessionPath))
        {
            Debug.LogError("[ReplayArchive] No CSV data in session.");
            return false;
        }

        if (!LoadSequenceConfig())
        {
            Debug.LogWarning("[ReplayArchive] No sequence config; using CSV timeline only.");
        }

        if (!LoadSystemConfig())
            Debug.LogWarning("[ReplayArchive] No system config in session; using defaults.");

        EnrichStepMarkersWithScenes();
        return true;
    }

    public bool TryResolveConfigPath(string fileName, out string fullPath)
    {
        fullPath = null;
        if (string.IsNullOrEmpty(fileName))
            return false;

        if (_configByExactName.TryGetValue(fileName, out fullPath))
            return File.Exists(fullPath);

        if (_configBySuffix.TryGetValue(fileName, out fullPath))
            return File.Exists(fullPath);

        string inSession = Path.Combine(SessionPath, fileName);
        if (File.Exists(inSession))
        {
            fullPath = inSession;
            return true;
        }

        foreach (string f in Directory.GetFiles(SessionPath, "*.json"))
        {
            if (f.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(f).EndsWith("_" + fileName, StringComparison.OrdinalIgnoreCase))
            {
                fullPath = f;
                return true;
            }
        }

        return false;
    }

    public bool TryResolveAssetPath(string relativePath, out string fullPath)
    {
        fullPath = null;
        if (string.IsNullOrEmpty(relativePath))
            return false;

        string inSession = Path.Combine(SessionPath, relativePath);
        if (File.Exists(inSession))
        {
            fullPath = inSession;
            return true;
        }

        string streaming = Path.Combine(Application.streamingAssetsPath, relativePath);
        if (File.Exists(streaming))
        {
            fullPath = streaming;
            return true;
        }

        return false;
    }

    public SequenceItem GetPlannedStep(int index)
    {
        if (Sequence?.sequences == null || index < 0 || index >= Sequence.sequences.Length)
            return null;
        return Sequence.sequences[index];
    }

    public int GetPlannedStepIndexAtTime(float timeSeconds)
    {
        if (Sequence?.sequences == null || Sequence.sequences.Length == 0)
            return Data.FindStepIndexAtTime(timeSeconds);

        float t = 0f;
        for (int i = 0; i < Sequence.sequences.Length; i++)
        {
            float dur = Sequence.sequences[i].duration;
            if (timeSeconds < t + dur)
                return i;
            t += dur;
        }
        return Sequence.sequences.Length - 1;
    }

    private void IndexJsonFiles(string sessionPath)
    {
        foreach (string f in Directory.GetFiles(sessionPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(f);
            _configByExactName[name] = f;

            int us = name.IndexOf('_');
            if (us >= 0 && us < name.Length - 1)
            {
                string suffix = name.Substring(us + 1);
                if (!_configBySuffix.ContainsKey(suffix))
                    _configBySuffix[suffix] = f;
            }
        }
    }

    private bool LoadSequenceConfig()
    {
        string[] cfgs = Directory.GetFiles(SessionPath, "*_sequenceConfig.json");
        if (cfgs.Length == 0)
            cfgs = Directory.GetFiles(SessionPath, "sequenceConfig.json");

        if (cfgs.Length == 0)
            return false;

        SequenceConfigPath = cfgs[0];
        Sequence = JsonConvert.DeserializeObject<SequenceConfig>(File.ReadAllText(SequenceConfigPath));
        return Sequence?.sequences != null && Sequence.sequences.Length > 0;
    }

    private bool LoadSystemConfig()
    {
        string[] cfgs = Directory.GetFiles(SessionPath, "*_system_config*.json");
        if (cfgs.Length == 0)
            cfgs = Directory.GetFiles(SessionPath, "system_config.json");

        if (cfgs.Length == 0)
            return false;

        SystemConfigPath = cfgs[0];
        JObject full = JObject.Parse(File.ReadAllText(SystemConfigPath));
        if (full["targetDisplay"] != null)
            GlobalTargetDisplay = full["targetDisplay"].Value<int>();

        JArray arr = (JArray)full["configs"];
        if (arr == null)
            return false;

        SystemConfig[] loaded = arr.ToObject<SystemConfig[]>();
        foreach (SystemConfig c in loaded)
        {
            c.targetDisplay = GlobalTargetDisplay;
            SystemConfigs[c.vrId] = c;
        }
        return SystemConfigs.Count > 0;
    }

    private void EnrichStepMarkersWithScenes()
    {
        foreach (var m in Data.StepMarkers)
        {
            if (m.index >= 0)
            {
                var step = GetPlannedStep(m.index);
                if (step != null)
                {
                    m.sceneName = step.sceneName;
                    if (string.IsNullOrEmpty(m.name) && step.parameters != null
                        && step.parameters.TryGetValue("configFile", out object cf))
                        m.name = cf?.ToString();
                }
            }
        }
    }
}
