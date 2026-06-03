using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stand-in for MainController during replay: system configs and target display only.
/// </summary>
public class ReplayExperimentHost : MonoBehaviour
{
    public static ReplayExperimentHost Instance { get; private set; }

    private Dictionary<string, SystemConfig> _configs = new();
    private int _targetDisplay = 1;

    public static ReplayExperimentHost Ensure()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject("ReplayExperimentHost");
        DontDestroyOnLoad(go);
        return go.AddComponent<ReplayExperimentHost>();
    }

    public void ApplyArchive(ReplaySessionArchive archive)
    {
        _configs.Clear();
        if (archive?.SystemConfigs != null)
        {
            foreach (var kvp in archive.SystemConfigs)
                _configs[kvp.Key] = kvp.Value;
        }
        _targetDisplay = archive?.GlobalTargetDisplay ?? 1;
    }

    public SystemConfig GetSystemConfigForGameObject(GameObject gameObject)
    {
        foreach (var kvp in _configs)
        {
            if (gameObject.name.Contains(kvp.Key))
                return kvp.Value;
        }
        if (_configs.TryGetValue("VR1", out SystemConfig vr1))
            return vr1;
        return new SystemConfig { vrId = "VR1", targetDisplay = _targetDisplay };
    }

    public SystemConfig GetSystemConfig(string vrId)
    {
        if (_configs.TryGetValue(vrId, out SystemConfig c))
            return c;
        return new SystemConfig { vrId = vrId, targetDisplay = _targetDisplay };
    }

    public int GetTargetDisplay() => _targetDisplay;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
