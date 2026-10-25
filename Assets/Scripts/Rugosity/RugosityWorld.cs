using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-150)]
public sealed class RugosityWorld : MonoBehaviour
{
    public enum ConfigurationPolicy { ApplyAtTrialStart, InspectorOnly }
    [Tooltip("InspectorOnly ignores incoming JSON in the Editor. Builds always apply config patches.")]
    public ConfigurationPolicy configurationPolicy;
    public RugositySettings settings = new RugositySettings();
    public RugositySurface[] surfaces = Array.Empty<RugositySurface>();
    [Tooltip("Empty finds ClosedLoop rigs in this scene. Assign camera transforms for a manual lab.")]
    public Transform[] rigTargets = Array.Empty<Transform>();
    public string LastError { get; private set; }
    public bool IsReady { get; private set; }
    public RugositySurface HeightSurface { get; private set; }
    public RugositySurface NormalSurface { get; private set; }
    private JObject defaults;
    private bool dirty;
    private string lastRecorded;
    private readonly Dictionary<Behaviour, bool> originalBehaviours = new Dictionary<Behaviour, bool>();
    private readonly Dictionary<Renderer, bool> originalRenderers = new Dictionary<Renderer, bool>();
    private readonly Dictionary<Collider, bool> originalColliders = new Dictionary<Collider, bool>();

    private void Awake() { CaptureDefaults(); ApplyLiveSettings(); }
    private void OnEnable() { dirty = true; }
    private void OnValidate() { dirty = true; }
    private void Update() { if (dirty) ApplyLiveSettings(); }
    private void OnDisable() { IsReady = false; RestoreRoles(); }
    private void CaptureDefaults() { if (defaults == null) defaults = JObject.FromObject(settings); }

    public void ApplyConfiguration(RugosityConfig config, JObject overrides = null)
    {
        CaptureDefaults();
        if (Application.isEditor && configurationPolicy == ConfigurationPolicy.InspectorOnly)
        { ApplyLiveSettings(); return; }
        var resolved = (JObject)defaults.DeepClone();
        var merge = new JsonMergeSettings { MergeNullValueHandling = MergeNullValueHandling.Ignore };
        if (config != null) resolved.Merge(JObject.FromObject(config), merge);
        if (overrides != null) resolved.Merge(overrides, merge);
        var next = resolved.ToObject<RugositySettings>();
        Validate(next); // Invalid config must fail the trial, rather than partly applying it.
        settings = next;
        ApplyLiveSettings();
    }

    public static void Apply(Scene scene, RugosityConfig config, JObject overrides = null)
    {
        var worlds = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RugosityWorld>(true)).ToArray();
        if (worlds.Length == 0 && (config?.enabled == true || overrides?.Value<bool?>("enabled") == true))
            throw new InvalidOperationException("rugosity is enabled but the scene has no RugosityWorld with assigned surfaces.");
        foreach (var world in worlds.Where(w => w.isActiveAndEnabled)) world.ApplyConfiguration(config, overrides);
    }

    private RugositySurface Resolve(string id)
    {
        var matches = surfaces.Where(s => s != null && s.surfaceId == id).ToArray();
        if (matches.Length != 1) throw new ArgumentException("Rugosity surface '" + id + "' must resolve exactly once in " + name);
        return matches[0];
    }
    private static bool Finite(float x) => !float.IsInfinity(x) && !float.IsNaN(x);
    public void Validate(RugositySettings s)
    {
        if (s == null) throw new ArgumentException("Missing rugosity settings.");
        if (!s.enabled) return;
        if (!Finite(s.centimetersPerUnit) || s.centimetersPerUnit <= 0 || !Finite(s.clearanceCm) || s.clearanceCm < 0 ||
            !Finite(s.absoluteHeight) || !Finite(s.normalRadiusCm) || s.normalRadiusCm < 0 ||
            !Finite(s.tiltResponse) || s.tiltResponse < 0 || s.normalSamples < 1 || s.normalSamples > 32 ||
            !Enum.IsDefined(typeof(RugosityHeightMode), s.heightMode) || !Enum.IsDefined(typeof(RugosityOrientationMode), s.orientationMode))
            throw new ArgumentException("Rugosity requires finite heights/radii, positive centimetersPerUnit, nonnegative clearance/response and 1–32 normal samples.");
        Resolve(s.visualSurface); Resolve(s.collisionSurface);
        if (s.heightMode == RugosityHeightMode.AboveGround) Resolve(string.IsNullOrEmpty(s.heightSurface) ? s.collisionSurface : s.heightSurface);
        if (s.orientationMode == RugosityOrientationMode.SurfaceNormal) Resolve(string.IsNullOrEmpty(s.normalSurface) ? s.collisionSurface : s.normalSurface);
    }

    public void ApplyLiveSettings()
    {
        if (!isActiveAndEnabled) { IsReady = false; return; }
        CaptureDefaults(); dirty = false;
        try
        {
            Validate(settings);
            if (!settings.enabled) { IsReady = false; RestoreRoles(); RecordState(); return; }
            var visual = Resolve(settings.visualSurface);
            var collision = Resolve(settings.collisionSurface);
            HeightSurface = settings.heightMode == RugosityHeightMode.AboveGround ? Resolve(string.IsNullOrEmpty(settings.heightSurface) ? settings.collisionSurface : settings.heightSurface) : collision;
            NormalSurface = settings.orientationMode == RugosityOrientationMode.SurfaceNormal ? Resolve(string.IsNullOrEmpty(settings.normalSurface) ? settings.collisionSurface : settings.normalSurface) : collision;
            foreach (var surface in surfaces.Where(s => s != null))
            {
                foreach (var t in surface.terrains) if (t != null && !originalBehaviours.ContainsKey(t)) originalBehaviours[t] = t.enabled;
                foreach (var r in surface.renderers) if (r != null && !originalRenderers.ContainsKey(r)) originalRenderers[r] = r.enabled;
                foreach (var c in surface.colliders) if (c != null && !originalColliders.ContainsKey(c)) originalColliders[c] = c.enabled;
                surface.SetRoles(surface == visual, surface == collision);
            }
            var targets = rigTargets.Length > 0 ? rigTargets : gameObject.scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<ClosedLoop>(true)).Select(r => r.transform).ToArray();
            foreach (var target in targets.Where(t => t != null))
            {
                var rig = target.GetComponent<RugosityRig>();
                if (rig == null) rig = target.gameObject.AddComponent<RugosityRig>();
                rig.world = this;
                var legacy = target.GetComponent<TerrainOrientationUpdater>();
                if (legacy != null)
                {
                    if (!originalBehaviours.ContainsKey(legacy)) originalBehaviours[legacy] = legacy.enabled;
                    legacy.enabled = false;
                }
            }
            LastError = null; IsReady = true;
            RecordState();
        }
        catch (Exception error)
        {
            IsReady = false; RestoreRoles();
            if (LastError != error.Message) Debug.LogError("Rugosity: " + error.Message, this);
            LastError = error.Message;
        }
    }

    private void RestoreRoles()
    {
        foreach (var p in originalBehaviours) if (p.Key != null) p.Key.enabled = p.Value;
        foreach (var p in originalRenderers) if (p.Key != null) p.Key.enabled = p.Value;
        foreach (var p in originalColliders) if (p.Key != null) p.Key.enabled = p.Value;
    }
    private void RecordState()
    {
        string json = JsonConvert.SerializeObject(settings);
        if (json == lastRecorded) return;
        lastRecorded = json;
        if (!Application.isPlaying) return;
        string record = JsonConvert.SerializeObject(new { utc = DateTime.UtcNow, frame = Time.frameCount,
            scene = gameObject.scene.name, world = name, policy = configurationPolicy.ToString(), settings });
        Debug.Log("RUGOSITY_STATE " + record);
        var logger = MasterDataLogger.Instance;
        if (logger != null && !string.IsNullOrEmpty(logger.directoryPath))
            File.AppendAllText(Path.Combine(logger.directoryPath, "rugosity_states.jsonl"), record + "\n");
    }
}
