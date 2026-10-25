using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Adapt authored rig templates to the configured animals before Start opens trackers.</summary>
public static class RuntimeRigRegistry
{
    private static readonly Dictionary<int, int> layers = new Dictionary<int, int>();
    public static int ParseIndex(string name)
    {
        var match = Regex.Match(name ?? "", @"(?<![A-Za-z0-9])VR([1-9][0-9]*)(?![0-9])");
        return match.Success && int.TryParse(match.Groups[1].Value, out int index) ? index : 0;
    }
    public static int StimulusLayer(int index)
    {
        if (layers.TryGetValue(index, out int layer)) return layer;
        return LayerMask.NameToLayer("SimulatedLocustsVR" + index);
    }
    public static int StimulusMask
    {
        get
        {
            int mask = LayerMask.GetMask("SimulatedLocustsVR1", "SimulatedLocustsVR2", "SimulatedLocustsVR3", "SimulatedLocustsVR4");
            foreach (int layer in layers.Values) mask |= 1 << layer;
            return mask;
        }
    }
    public static string LayerLabel(int layer)
    {
        string label = LayerMask.LayerToName(layer);
        if (!string.IsNullOrEmpty(label)) return label;
        foreach (var pair in layers) if (pair.Value == layer) return "AgentsVR" + pair.Key;
        return "Layer" + layer;
    }
    public static ClosedLoop[] Rigs(Scene scene) => UnityEngine.Object.FindObjectsByType<ClosedLoop>(FindObjectsSortMode.None)
        .Where(r => r.isActiveAndEnabled && r.gameObject.scene == scene).OrderBy(r => ParseIndex(r.name)).ToArray();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => layers.Clear();

    public static void Configure(Scene scene, IReadOnlyDictionary<string, SystemConfig> configs)
    {
        var authored = UnityEngine.Object.FindObjectsByType<ClosedLoop>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(r => r.gameObject.scene == scene && ParseIndex(r.name) > 0).ToArray();
        if (authored.Length == 0) return; // Control-only and replay scenes need no animal template.
        var wanted = new SortedSet<int>();
        foreach (string id in configs.Keys)
        {
            int index = ParseIndex(id);
            if (index < 1 || id != "VR" + index) throw new ArgumentException("Rig IDs must be unique VR1, VR2, … values: " + id);
            wanted.Add(index);
        }
        var nextLayers = new Dictionary<int, int>();
        for (int i = 1; i <= 4; i++)
        {
            int layer = LayerMask.NameToLayer("SimulatedLocustsVR" + i);
            if (layer >= 0) nextLayers[i] = layer;
        }
        var free = new Queue<int>(Enumerable.Range(8, 24).Where(i => string.IsNullOrEmpty(LayerMask.LayerToName(i))));
        foreach (int index in wanted)
        {
            if (nextLayers.ContainsKey(index)) continue;
            if (free.Count == 0) throw new ArgumentException("The configured animals exceed the available isolated stimulus layers. Remove unused rig entries or build a scene with another rendering strategy.");
            nextLayers[index] = free.Dequeue();
        }
        layers.Clear();
        foreach (var pair in nextLayers) layers.Add(pair.Key, pair.Value);
        ClosedLoop template = authored.OrderBy(r => ParseIndex(r.name)).Last();
        int templateIndex = ParseIndex(template.name);
        int templateLayer = StimulusLayer(templateIndex);
        var existing = authored.GroupBy(r => ParseIndex(r.name)).ToDictionary(g => g.Key, g => g.First());
        foreach (int index in wanted)
        {
            if (existing.ContainsKey(index)) continue;
            // Instantiating an inactive template lets us rename IDs and masks before any Start.
            bool active = template.gameObject.activeSelf;
            template.gameObject.SetActive(false);
            GameObject clone;
            try { clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent); }
            finally { template.gameObject.SetActive(active); }
            clone.name = Regex.Replace(template.name, @"VR" + templateIndex + @"(?![0-9])", "VR" + index);
            if (clone.scene != scene) SceneManager.MoveGameObjectToScene(clone, scene);
            foreach (Transform item in clone.GetComponentsInChildren<Transform>(true))
            {
                item.name = Regex.Replace(item.name, @"VR" + templateIndex + @"(?![0-9])", "VR" + index);
                if (item.gameObject.layer == templateLayer || item.gameObject.layer == LayerMask.NameToLayer("ChoiceVR" + templateIndex))
                    item.gameObject.layer = StimulusLayer(index);
                if (item.CompareTag("ChoiceVR" + Mathf.Clamp(templateIndex, 1, 4))) item.tag = "Untagged";
            }
            foreach (Camera camera in clone.GetComponentsInChildren<Camera>(true))
                camera.cullingMask = (camera.cullingMask & ~StimulusMask & ~LayerMask.GetMask("ChoiceVR1", "ChoiceVR2", "ChoiceVR3", "ChoiceVR4")) | (1 << StimulusLayer(index));
            foreach (LocustSpawner spawner in clone.GetComponentsInChildren<LocustSpawner>(true))
            {
                spawner.layerName = "VR" + index + "_Agents";
                spawner.RuntimeLayer = StimulusLayer(index);
            }
            existing[index] = clone.GetComponent<ClosedLoop>();
            clone.SetActive(true);
        }
        foreach (var pair in existing)
        {
            bool enabled = wanted.Contains(pair.Key);
            pair.Value.gameObject.SetActive(enabled);
            if (!enabled) continue;
            foreach (LocustSpawner spawner in pair.Value.GetComponentsInChildren<LocustSpawner>(true))
                spawner.RuntimeLayer = StimulusLayer(pair.Key);
            foreach (SwarmLogger logger in pair.Value.GetComponentsInChildren<SwarmLogger>(true))
                logger.locustLayerMask = 1 << StimulusLayer(pair.Key);
            pair.Value.GetComponent<ViewportSetter>()?.RefreshSystemConfig();
        }
    }
}
