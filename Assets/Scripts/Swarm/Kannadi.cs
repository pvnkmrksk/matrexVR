using System;
using System.Collections.Generic;
using System.Linq;
using InSceneSequence;
using UnityEngine;
using UnityEngine.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Kannadi (ಕನ್ನಡಿ, mirror): tracked animals replicated across a hexagonal grid.</summary>
[DefaultExecutionOrder(100)]
public class Kannadi : MonoBehaviour, IInSceneSequencer
{
    public GameObject tilePrefab;
    public int numberOfRings = 3;
    [FormerlySerializedAs("spacing")] public float hexRadius = 10f;
    public GameObject[] Clones { get; private set; } = new GameObject[0];
    public int VRIndex => ParseVRIndex(gameObject.name);
    private Vector3[] offsets = new Vector3[0];
    private Quaternion prefabRotation;
    private KannadiConfig config = new KannadiConfig();
    private int? watchIndex;
    private bool initialized;
    private LocustSpawner swarmSpawner;
    private string swarmPopulationKey, mirrorPopulationKey, mirrorAppearanceKey;
    private MirrorBatch mirrorBatch;
    public bool IsSwarm => string.Equals(config.populationMode, "Swarm", StringComparison.OrdinalIgnoreCase);
    private float MirrorBoundaryX => config.boundaryLengthX ?? SwarmConfig.DefaultBoundaryLength;
    private float MirrorBoundaryZ => config.boundaryLengthZ ?? SwarmConfig.DefaultBoundaryLength;
    protected virtual float RowSpacing => hexRadius * 2f;

    // One batch can represent several tracked rigs. Each body retains its source
    // rig's stimulus layer, so every observer's visibility rules still apply.
    private sealed class MirrorBatch
    {
        internal readonly Kannadi[] Rigs;
        private readonly List<GameObject> hosts = new List<GameObject>();
        private bool pending = true;

        internal MirrorBatch(Kannadi[] rigs) => Rigs = rigs;

        internal void Invalidate()
        {
            foreach (var host in hosts)
                if (host != null) { host.SetActive(false); UnityEngine.Object.Destroy(host); }
            hosts.Clear();
            pending = true;
        }

        internal void RebuildIfPending()
        {
            if (!pending) return;
            pending = false;
            if (!NativeLocustBatchRenderer.Available) return;
            var stock = Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog")?.Find("SimulatedLocust");
            if (stock == null) return;
            var byMaterial = new Dictionary<Material, List<GameObject>>();
            Kannadi owner = null;
            foreach (var rig in Rigs)
            {
                if (rig == null || !rig.isActiveAndEnabled || rig.IsSwarm || rig.tilePrefab != stock) continue;
                if (owner == null) owner = rig;
                foreach (var clone in rig.Clones)
                {
                    if (clone == null || !clone.activeInHierarchy) continue;
                    var material = clone.GetComponentInChildren<SkinnedMeshRenderer>()?.sharedMaterial;
                    if (material == null) continue;
                    if (!byMaterial.TryGetValue(material, out var agents))
                        byMaterial.Add(material, agents = new List<GameObject>());
                    agents.Add(clone);
                }
            }
            foreach (var agents in byMaterial.Values)
            {
                if (agents.Count < 2) continue;
                var phases = new float[agents.Count];
                for (int i = 0; i < agents.Count; i++)
                {
                    var animator = agents[i].GetComponent<Animator>();
                    if (animator != null && animator.isInitialized)
                        phases[i] = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                }
                var host = NativeLocustBatchRenderer.TryCreate(owner.transform, agents[0].layer, stock, agents, phases, layered: true);
                if (host != null) hosts.Add(host);
            }
        }
    }

    private void Awake() => RuntimeStatusOverlay.RegisterStatusSource(this);

    protected virtual void Start()
    {
        // sceneLoaded initializes before Start; direct scene play still gets a usable grid.
        if (!initialized) InitializeScene(new Dictionary<string, object>());
    }

    public static int ParseVRIndex(string name)
    {
        return RuntimeRigRegistry.ParseIndex(name);
    }

    public void InitializeScene(Dictionary<string, object> parameters)
    {
        try
        {
            NightSkyController.Apply(gameObject, null, null);
            KannadiConfig next = KannadiConfig.Load(parameters);
            Keyboard.ApplyAutopilotConfig(next.autopilotEnabled, next.autopilotSpeed);
            Kannadi[] rigs = FindObjectsOfType<Kannadi>().Where(r => r.isActiveAndEnabled && r.gameObject.scene == gameObject.scene).OrderBy(r => r.VRIndex).ToArray();
            // Validate all prefab selections before replacing any existing grid.
            var selected = rigs.Select(r => r.ResolvePrefab(next)).ToArray();
            var batch = mirrorBatch ?? rigs.FirstOrDefault()?.mirrorBatch;
            if (batch == null || !batch.Rigs.SequenceEqual(rigs))
            {
                batch?.Invalidate();
                batch = new MirrorBatch(rigs);
            }
            foreach (var rig in rigs) rig.mirrorBatch = batch;
            for (int i = 0; i < rigs.Length; i++)
            {
                var rig = rigs[i];
                var tracking = rig.GetComponent<ClosedLoop>();
                if (!ExperimentPhases.IsPhase(parameters) && tracking != null)
                    StimulusHeadingReference.For(tracking).Begin(next.headingReference, result => {
                        if (result != null && next.useHeadingReference) rig.GenerateHexGrid();
                    });
                using (ExperimentRandom.ScopeFor(rig, "kannadi-initialization")) rig.Apply(next, selected[i], i, rigs.Length);
            }
            batch.RebuildIfPending();
            NightSkyController.Apply(gameObject, next.nightSky, next.skyboxPath, next.uniformSkyColor);
            var world = GetComponent<KannadiWorldObjects>() ?? gameObject.AddComponent<KannadiWorldObjects>();
            world.Apply(next.objects, rigs.Select(r => r.GetComponent<ClosedLoop>()).Where(r => r != null).ToArray());
            SimpleOverheadCamera.EnsureInScene();
        }
        catch (Exception error)
        {
            throw new InvalidOperationException("Cannot initialize Kannadi: " + error.Message, error);
        }
    }
    public void AdvanceStep(Dictionary<string, object> parameters) => InitializeScene(parameters);

    public GameObject ResolvePrefab(KannadiConfig settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.kannadiModelPath)) return ExternalModelLibrary.GetPrefab(settings.kannadiModelPath);
        if (!string.IsNullOrWhiteSpace(settings.modelPath)) return ExternalModelLibrary.GetPrefab(settings.modelPath);
        string name = settings.kannadiTilePrefab;
        if (string.IsNullOrEmpty(name))
        {
            if (tilePrefab != null) return tilePrefab;
            name = "SimulatedLocust";
        }
        KannadiPrefabCatalog catalog = Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog");
        GameObject prefab = catalog != null ? catalog.Find(name) : null;
        if (prefab == null && tilePrefab != null && tilePrefab.name == name) prefab = tilePrefab;
        if (prefab == null) throw new ArgumentException("Unknown kannadiTilePrefab '" + name + "'. Add it to the KannadiPrefabCatalog.");
        return prefab;
    }

    private void Apply(KannadiConfig next, GameObject prefab, int ordinal, int rigCount)
    {
        bool wasSwarm = IsSwarm;
        config = next;
        tilePrefab = prefab;
        numberOfRings = next.numberOfRings ?? numberOfRings;
        hexRadius = next.hexRadius ?? next.spacing ?? hexRadius;
        VRConfig vr = next.vrConfigs?.FirstOrDefault(v => v.vrIndex == VRIndex);
        watchIndex = vr?.watchIndex;
        Vector3 position = (vr?.initialPosition ?? next.initialPosition)?.ToVector3() ?? transform.position;
        Quaternion rotation = Quaternion.Euler((vr?.initialRotation ?? next.initialRotation)?.ToVector3() ?? transform.eulerAngles);
        if (!IsSwarm && next.mirrorSpawn != null)
        {
            bool random = next.mirrorSpawn.layout == SwarmSpawnType.Random;
            if (next.resetPositionOnStart && vr?.initialPosition == null)
            {
                var center = next.initialPosition?.ToVector3() ?? new Vector3(0, position.y, 0);
                position = random
                    ? next.mirrorSpawn.Position(center, ordinal, rigCount, UnityEngine.Random.value, UnityEngine.Random.value)
                    : next.mirrorSpawn.Position(center, ordinal, rigCount);
            }
            if (next.resetRotationOnStart && vr?.initialRotation == null)
            {
                float yaw = next.mirrorSpawn.Yaw(next.initialRotation?.y ?? 0, ordinal, rigCount, random ? UnityEngine.Random.value : 0);
                rotation = Quaternion.Euler(rotation.eulerAngles.x, yaw, rotation.eulerAngles.z);
            }
        }
        else if (next.randomInitialRotation && next.resetRotationOnStart)
            rotation = Quaternion.Euler(rotation.eulerAngles.x, next.SampleInitialYaw(UnityEngine.Random.value), rotation.eulerAngles.z);
        ClosedLoop closedLoop = GetComponent<ClosedLoop>();
        if (closedLoop != null)
        {
            closedLoop.ApplyStartPose(position, rotation, next.resetPositionOnStart, next.resetRotationOnStart);
            closedLoop.SetLocustGains(vr?.closedLoopPosition ?? next.closedLoopPosition ?? 1f,
                                      vr?.closedLoopOrientation ?? next.closedLoopOrientation ?? 1f);
        }
        else transform.SetPositionAndRotation(next.resetPositionOnStart ? position : transform.position, next.resetRotationOnStart ? rotation : transform.rotation);
        ConfigureCameras();
        if (IsSwarm)
        {
            CleanupClones();
            var population = new JObject();
            foreach (var field in typeof(SwarmConfig).GetFields()) population[field.Name] = field.GetValue(next) == null ? JValue.CreateNull() : JToken.FromObject(field.GetValue(next));
            string key = population.ToString(Formatting.None) + ":" + prefab.GetInstanceID();
            bool preserve = initialized && wasSwarm && !next.resetPositionOnStart && swarmPopulationKey == key && swarmSpawner != null;
            swarmSpawner = GetComponent<LocustSpawner>() ?? gameObject.AddComponent<LocustSpawner>();
            swarmSpawner.boundaryManager = GetComponent<BoundaryManager>() ?? gameObject.AddComponent<BoundaryManager>();
            swarmSpawner.locustPrefab = prefab;
            swarmSpawner.RuntimeLayer = RuntimeRigRegistry.StimulusLayer(VRIndex);
            swarmSpawner.layerName = "SimulatedLocustsVR" + VRIndex;
            swarmSpawner.loadConfigFromJsonFile = false;
            if (!next.SwarmEnabled) swarmSpawner.PrepareObservation();
            else if (!preserve) SwarmConfiguration.Apply(swarmSpawner, next, closedLoop);
            swarmPopulationKey = next.SwarmEnabled ? key : null;
            var logger = GetComponent<SwarmLogger>() ?? gameObject.AddComponent<SwarmLogger>();
            logger.locustLayerMask = 1 << swarmSpawner.RuntimeLayer; logger.enabled = true;
        }
        else
        {
            if (swarmSpawner != null) swarmSpawner.PrepareObservation();
            var logger = GetComponent<SwarmLogger>(); if (logger != null) logger.enabled = false;
            string key = JsonConvert.SerializeObject(new { numberOfRings, hexRadius, prefab = prefab.GetInstanceID(), next.animateOnMove,
                next.animationSpeedThreshold, next.periodicBoundary, next.boundaryLengthX, next.boundaryLengthZ, next.useHeadingReference, watchIndex });
            string appearanceKey = JsonConvert.SerializeObject(next.modelAppearance);
            if (!(initialized && !wasSwarm && !next.resetPositionOnStart && mirrorPopulationKey == key)) GenerateHexGrid();
            else if (mirrorAppearanceKey != appearanceKey)
            {
                mirrorBatch?.Invalidate();
                foreach (var clone in Clones) ModelAppearance.Apply(clone, next.modelAppearance);
            }
            mirrorAppearanceKey = appearanceKey;
            mirrorPopulationKey = key; swarmPopulationKey = null;
        }
        initialized = true;
        var mirrorLogger = GetComponent<KannadiLogger>();
        if (!IsSwarm && mirrorLogger == null) mirrorLogger = gameObject.AddComponent<KannadiLogger>();
        if (mirrorLogger != null) mirrorLogger.enabled = !IsSwarm;
    }

    private void ConfigureCameras()
    {
        int swarmMask = RuntimeRigRegistry.StimulusMask;
        int ownLayer = RuntimeRigRegistry.StimulusLayer(VRIndex);
        if (watchIndex.HasValue && RuntimeRigRegistry.StimulusLayer(watchIndex.Value) < 0)
            throw new InvalidOperationException("Kannadi watchIndex has no configured stimulus layer: " + watchIndex.Value);
        int visible = IsSwarm ? 1 << ownLayer : watchIndex.HasValue ? (1 << RuntimeRigRegistry.StimulusLayer(watchIndex.Value))
                                        : RuntimeRigRegistry.PeerStimulusMask(VRIndex);
        int markerMask = LayerMask.GetMask("OverheadCameraMarkers");
        foreach (Camera camera in GetComponentsInChildren<Camera>(true))
        {
            // Preserve the floor and shared world, replace only the animal layers.
            camera.cullingMask = (camera.cullingMask & ~swarmMask & ~markerMask) | visible;
            if (config.backgroundColor != null)
            {
                ColorConfig c = config.backgroundColor;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(c.r, c.g, c.b, c.a);
            }
        }
    }

    public void SetVRCameraCullingMask(int vrIndex, int index)
    {
        if (index < 1 || RuntimeRigRegistry.StimulusLayer(index) < 0) throw new ArgumentOutOfRangeException(nameof(index));
        foreach (Kannadi rig in FindObjectsOfType<Kannadi>())
            if (rig.VRIndex == vrIndex) { rig.watchIndex = index; rig.ConfigureCameras(); }
    }

    public void GenerateHexGrid()
    {
        CleanupClones();
        if (IsSwarm || numberOfRings < 0 || tilePrefab == null) return;
        int layer = RuntimeRigRegistry.StimulusLayer(VRIndex);
        if (layer < 0) throw new InvalidOperationException("Missing animal layer for " + gameObject.name);
        bool skipCenter = watchIndex == VRIndex;
        var copies = new List<GameObject>();
        var positions = new List<Vector3>();
        prefabRotation = tilePrefab.transform.rotation;
        for (int q = -numberOfRings; q <= numberOfRings; q++)
        {
            int first = Mathf.Max(-numberOfRings, -q - numberOfRings);
            int last = Mathf.Min(numberOfRings, -q + numberOfRings);
            for (int r = first; r <= last; r++)
            {
                if (skipCenter && q == 0 && r == 0) continue;
                Vector3 offset = new Vector3(hexRadius * Mathf.Sqrt(3f) * (q + r / 2f), tilePrefab.transform.position.y, RowSpacing * r);
                var reference = config.useHeadingReference ? GetComponent<StimulusHeadingReference>()?.Result : null;
                if (reference != null) offset = Quaternion.Euler(0, (float)reference.meanHeadingDegrees, 0) * offset;
                GameObject clone = ExternalModelLibrary.InstantiatePrefab(tilePrefab);
                clone.transform.SetPositionAndRotation(Vector3.zero, prefabRotation);
                if (!string.IsNullOrWhiteSpace(config.kannadiModelPath) || !string.IsNullOrWhiteSpace(config.modelPath)) ExternalModelLibrary.ConfigureAnimation(clone);
                clone.name = $"VR{VRIndex}_Kannadi_{copies.Count}";
                SetLayerRecursively(clone.transform, layer);
                clone.tag = "SimulatedLocust";
                // Mirrored individuals are driven only by the tracked rig.
                foreach (LocustMover mover in clone.GetComponentsInChildren<LocustMover>(true)) mover.enabled = false;
                foreach (DirectionalMovement mover in clone.GetComponentsInChildren<DirectionalMovement>(true)) mover.enabled = false;
                foreach (PeriodicBoundary boundary in clone.GetComponentsInChildren<PeriodicBoundary>(true)) boundary.enabled = false;
                foreach (BandSpawner band in clone.GetComponentsInChildren<BandSpawner>(true))
                {
                    band.speed = 0;
                    band.lockAgentWithAnimalPosition = true;
                    band.lockBoundaryWithAnimalPosition = true;
                    band.customParentTransform = null;
                    band.gameObject.layer = layer;
                }
                Vector2 wrapSize = config.periodicBoundary ? new Vector2(MirrorBoundaryX, MirrorBoundaryZ) : Vector2.zero;
                AnimateOnMove.ConfigureHierarchy(clone, transform, config.animateOnMove, config.animationSpeedThreshold, wrapSize);
                clone.AddComponent<KannadiMirrorTile>().ConfigureAnimation(transform, config.animateOnMove, config.animationSpeedThreshold, wrapSize);
                ModelAppearance.Apply(clone, config.modelAppearance);
                copies.Add(clone); positions.Add(offset);
            }
        }
        Clones = copies.ToArray(); offsets = positions.ToArray();
        UpdateClones();
    }

    private static void SetLayerRecursively(Transform item, int layer)
    {
        item.gameObject.layer = layer;
        foreach (Transform child in item) SetLayerRecursively(child, layer);
    }

    // Modulo preserves overshoot even after a displacement larger than the whole arena.
    public static float Wrap(float value, float length) => Mathf.Repeat(value + length / 2f, length) - length / 2f;
    private Vector3 WrapPosition(Vector3 value)
    {
        if (config.periodicBoundary)
        {
            value.x = Wrap(value.x, MirrorBoundaryX);
            value.z = Wrap(value.z, MirrorBoundaryZ);
        }
        return value;
    }
    protected virtual void LateUpdate()
    {
        if (!initialized) return;
        if (!IsSwarm && config.periodicBoundary)
        {
            Vector3 position = transform.position;
            Vector3 wrapped = WrapPosition(position);
            if (Mathf.Abs(wrapped.x - position.x) > MirrorBoundaryX / 2f ||
                Mathf.Abs(wrapped.z - position.z) > MirrorBoundaryZ / 2f)
                GetComponent<ClosedLoop>()?.MarkPositionDiscontinuity();
            transform.position = wrapped;
        }
        UpdateClones();
        mirrorBatch?.RebuildIfPending();
    }
    private void UpdateClones()
    {
        for (int i = 0; i < Clones.Length; i++)
        {
            if (Clones[i] == null) continue;
            Vector3 position = new Vector3(transform.position.x + offsets[i].x, offsets[i].y, transform.position.z + offsets[i].z);
            Clones[i].transform.SetPositionAndRotation(WrapPosition(position), transform.rotation * prefabRotation);
        }
    }
    private void CleanupClones()
    {
        if (Clones.Length != 0) mirrorBatch?.Invalidate();
        foreach (GameObject clone in Clones)
            if (clone != null) { clone.SetActive(false); Destroy(clone); }
        Clones = new GameObject[0]; offsets = new Vector3[0];
    }
    protected virtual void OnDestroy()
    {
        RuntimeStatusOverlay.UnregisterStatusSource(this);
        CleanupClones();
    }
}
