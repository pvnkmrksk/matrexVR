using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Adapt authored rig templates to the configured animals before Start opens trackers.</summary>
public static class RuntimeRigRegistry
{
    public const int MaximumRigCount = 16;
    private static readonly Dictionary<int, int> layers = new Dictionary<int, int>();
    public static int ParseIndex(string name)
    {
        var match = Regex.Match(name ?? "", @"(?<![A-Za-z0-9])VR([1-9][0-9]*)(?![0-9])");
        return match.Success && int.TryParse(match.Groups[1].Value, out int index) ? index : 0;
    }
    public static int StimulusLayer(int index)
    {
        if (index < 1 || index > MaximumRigCount) return -1;
        if (layers.TryGetValue(index, out int layer)) return layer;
        // Keep the original four layer IDs for archived scenes. New rigs have a
        // stable slot independent of which subset of animals is active.
        return index <= 4 ? LayerMask.NameToLayer("SimulatedLocustsVR" + index) : index + 14;
    }
    public static int StimulusMask
    {
        get
        {
            int mask = 0;
            for (int i = 1; i <= MaximumRigCount; i++)
                if (StimulusLayer(i) >= 0) mask |= 1 << StimulusLayer(i);
            return mask;
        }
    }
    public static int PeerStimulusMask(int index)
    {
        int layer = StimulusLayer(index);
        if (layer < 0) throw new ArgumentOutOfRangeException(nameof(index), "Rig index must be 1 through 16.");
        return StimulusMask & ~(1 << layer);
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
        foreach (var rig in authored) EnsureSixFaceRig(rig);
        var wanted = new SortedSet<int>();
        foreach (string id in configs.Keys)
        {
            int index = ParseIndex(id);
            if (index < 1 || index > MaximumRigCount || id != "VR" + index) throw new ArgumentException("Rig IDs must be unique VR1 through VR16 values: " + id);
            wanted.Add(index);
        }
        var nextLayers = new Dictionary<int, int>();
        for (int i = 1; i <= MaximumRigCount; i++)
        {
            int layer = StimulusLayer(i);
            if (layer >= 0) nextLayers[i] = layer;
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
            pair.Value.GetComponentInChildren<ViewportSetter>(true)?.ApplyConfiguration(configs["VR" + pair.Key]);
        }
    }

    private static void EnsureSixFaceRig(ClosedLoop rig)
    {
        var setter = rig.GetComponentInChildren<ViewportSetter>(true);
        if (setter == null)
        {
            // Older manual terrain labs put one camera on the tracking root.
            // Preserve its projection/material settings in an editable child;
            // tracking and the physical camera allow-list then have separate owners.
            Camera source = rig.GetComponent<Camera>() ?? rig.GetComponentInChildren<Camera>(true);
            if (source == null) throw new InvalidOperationException("Configured rig has no camera template: " + rig.name);
            var front = new GameObject("Main Camera F");
            front.transform.SetParent(rig.transform, false);
            var camera = front.AddComponent<Camera>(); camera.CopyFrom(source);
            camera.fieldOfView = 90; camera.enabled = false;
            source.enabled = false;
            if (Application.isPlaying) UnityEngine.Object.Destroy(source);
            else UnityEngine.Object.DestroyImmediate(source);
            setter = rig.gameObject.AddComponent<ViewportSetter>();
        }
        setter.EnsureSixFaces();
    }
}
