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
    internal const string SharedSwarmLayerName = "KannadiSharedSwarm";
    private const double WalkingVisibilityHoldSeconds = 1.0;
    internal static int SharedSwarmMask => LayerMask.GetMask(SharedSwarmLayerName);
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
    private SharedSwarm sharedSwarm;
    private ZmqListener tracker;
    private ClosedLoop trackedRig;
    private AnimateOnMove.TranslationSpeed walkingSpeed;
    private long walkingPoseContinuityId;
    private bool walkingAnimalVisible;
    private double? walkingVisibilitySince;
    private int refreshBandRenderersThroughFrame = -1;
    public bool IsSwarm => string.Equals(config.populationMode, "Swarm", StringComparison.OrdinalIgnoreCase);
    public bool IsMixed => config.IsMixed;
    internal bool AnimalsVisible { get; private set; } = true;
    internal bool SeamlessPeriodicView => IsMixed && config.seamlessPeriodicView;
    internal Vector2 PeriodicViewArenaSize => new Vector2(MirrorBoundaryX, MirrorBoundaryZ);
    private float MirrorBoundaryX => config.boundaryLengthX ?? SwarmConfig.DefaultBoundaryLength;
    private float MirrorBoundaryZ => config.boundaryLengthZ ?? SwarmConfig.DefaultBoundaryLength;
    private Vector2 AnimationWrapSize => config.periodicBoundary ? PeriodicViewArenaSize : Vector2.zero;
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
            BatchClones(0);
        }

        internal void AppendCopies(int firstOrdinal)
        {
            if (!pending) BatchClones(firstOrdinal);
        }

        private void BatchClones(int firstOrdinal)
        {
            if (!NativeLocustBatchRenderer.Available) return;
            var stock = Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog")?.Find("SimulatedLocust");
            if (stock == null) return;
            var byMaterial = new Dictionary<Material, List<GameObject>>();
            Kannadi owner = null;
            foreach (var rig in Rigs)
            {
                if (rig == null || !rig.isActiveAndEnabled || rig.IsSwarm || rig.tilePrefab != stock) continue;
                if (owner == null) owner = rig;
                for (int i = firstOrdinal; i < rig.Clones.Length; i++)
                {
                    var clone = rig.Clones[i];
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

    private sealed class SharedSwarm
    {
        private readonly LocustSpawner spawner;
        private readonly int bodiesPerObserver;
        private readonly JObject fixedSettings;
        private Vector3Config[] visibleOffsets;
        internal int CopiesPerSource => visibleOffsets.Length;

        internal SharedSwarm(KannadiConfig settings, GameObject prefab, Kannadi[] rigs)
        {
            fixedSettings = FixedSettings(settings);
            visibleOffsets = settings.avatarCopyOffsets;
            bodiesPerObserver = BodyCount(settings);
            int layer = LayerMask.NameToLayer(SharedSwarmLayerName);
            var host = new GameObject("Kannadi Shared Swarm") { layer = layer };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, rigs[0].gameObject.scene);
            spawner = host.AddComponent<LocustSpawner>();
            try
            {
                spawner.boundaryManager = host.AddComponent<BoundaryManager>();
                spawner.locustPrefab = prefab;
                spawner.RuntimeLayer = layer;
                spawner.layerName = SharedSwarmLayerName;
                spawner.loadConfigFromJsonFile = false;
                SwarmConfiguration.Apply(spawner, settings, null);
                if (settings.seamlessPeriodicView)
                    foreach (var body in spawner.Spawned) PeriodicArenaView.RegisterBody(body);
                spawner.InitializeRemovalOrder();
                var cameras = rigs.SelectMany(rig => rig.GetComponentsInChildren<Camera>(true)).ToArray();
                spawner.OverrideClipPlanes(cameras, settings.nearClipMetres * spawner.worldUnitsPerMetre,
                    settings.farClipMetres * spawner.worldUnitsPerMetre);
                var logger = host.AddComponent<SwarmLogger>();
                logger.locustLayerMask = SharedSwarmMask;
            }
            catch
            {
                Remove();
                throw;
            }
        }

        private static JObject FixedSettings(KannadiConfig settings)
        {
            var fields = JObject.FromObject(settings);
            fields.Remove(nameof(SwarmConfig.agentCount));
            fields.Remove(nameof(KannadiConfig.avatarCopyOffsets));
            fields.Remove(nameof(KannadiConfig.resetPositionOnStart));
            fields.Remove(nameof(KannadiConfig.resetRotationOnStart));
            return fields;
        }

        private static int BodyCount(KannadiConfig settings) => settings.agentCount.Value +
            (1 + settings.avatarCopyOffsets.Length) * (RuntimeRigRegistry.MaximumRigCount - 1);

        internal void ValidateTransition(KannadiConfig next)
        {
            if (next.resetPositionOnStart || next.resetRotationOnStart || !JToken.DeepEquals(fixedSettings, FixedSettings(next)))
                throw new ArgumentException("A shared-swarm stage must preserve the arena, poses and fixed stimulus settings.");
            if (BodyCount(next) != bodiesPerObserver || next.agentCount.Value > spawner.PopulationCount || next.avatarCopyOffsets.Length < CopiesPerSource)
                throw new ArgumentException("A shared-swarm stage must exchange simulated locusts for copies at a fixed per-rig body count.");
            for (int i = 0; i < CopiesPerSource; i++)
                if (!visibleOffsets[i].ToVector3().Equals(next.avatarCopyOffsets[i].ToVector3()))
                    throw new ArgumentException("A shared-swarm stage must retain all existing avatar copy offsets in the same order.");
        }

        internal void ApplyTransition(KannadiConfig next)
        {
            spawner.ReduceGeneratedPopulationTo(next.agentCount.Value);
            visibleOffsets = next.avatarCopyOffsets;
        }

        internal void Remove()
        {
            spawner.PrepareObservation();
            spawner.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(spawner.gameObject);
        }
    }

    private void Awake()
    {
        tracker = GetComponent<ZmqListener>();
        RuntimeStatusOverlay.RegisterStatusSource(this);
    }

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
            KannadiConfig next = KannadiConfig.Load(parameters);
            Kannadi[] rigs = FindObjectsOfType<Kannadi>().Where(r => r.isActiveAndEnabled && r.gameObject.scene == gameObject.scene).OrderBy(r => r.VRIndex).ToArray();
            var shared = sharedSwarm ?? rigs.FirstOrDefault()?.sharedSwarm;
            if (next.IsMixed)
            {
                if (rigs.Length != RuntimeRigRegistry.MaximumRigCount || rigs.Where((rig, i) => rig.VRIndex != i + 1 || rig.GetComponent<ClosedLoop>() == null).Any())
                    throw new ArgumentException("The Kannadi shared swarm requires configured tracked rigs VR1 through VR16.");
                if (SharedSwarmMask == 0) throw new InvalidOperationException("The KannadiSharedSwarm layer is missing.");
                shared?.ValidateTransition(next);
            }
            // Validate all prefab selections before replacing any existing grid.
            var selected = rigs.Select(r => r.ResolvePrefab(next)).ToArray();
            LocustGround.Apply(gameObject.scene, next.groundAppearance);
            if (!next.IsMixed && shared != null)
            {
                shared.Remove();
                shared = null;
                foreach (var rig in rigs) rig.sharedSwarm = null;
            }
            NightSkyController.Apply(gameObject, null, null);
            Keyboard.ApplyAutopilotConfig(next.autopilotEnabled, next.autopilotSpeed);
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
                if (!next.IsMixed && !ExperimentPhases.IsPhase(parameters) && tracking != null)
                    StimulusHeadingReference.For(tracking).Begin(next.headingReference, result => {
                        if (result != null && next.useHeadingReference) rig.GenerateHexGrid();
                    });
                using (ExperimentRandom.ScopeFor(rig, "kannadi-initialization")) rig.Apply(next, selected[i], i, rigs.Length);
            }
            if (next.IsMixed)
            {
                if (shared == null) shared = new SharedSwarm(next, selected[0], rigs);
                else
                {
                    int firstNewBody = 1 + shared.CopiesPerSource;
                    shared.ApplyTransition(next);
                    if (shared.CopiesPerSource + 1 > firstNewBody) batch.AppendCopies(firstNewBody);
                }
                foreach (var rig in rigs) rig.sharedSwarm = shared;
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
        bool wasMixed = IsMixed;
        Vector2 previousWrapSize = AnimationWrapSize;
        float previousWalkingThreshold = config.animationSpeedThreshold;
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
                float yaw = IsMixed && next.randomInitialRotation
                    ? next.SampleInitialYaw(ExperimentRandom.InitialHeadingSample(next.initialHeadingSeed, VRIndex))
                    : next.mirrorSpawn.Yaw(next.initialRotation?.y ?? 0, ordinal, rigCount, random ? UnityEngine.Random.value : 0);
                rotation = Quaternion.Euler(rotation.eulerAngles.x, yaw, rotation.eulerAngles.z);
            }
        }
        else if (next.randomInitialRotation && next.resetRotationOnStart)
            rotation = Quaternion.Euler(rotation.eulerAngles.x, next.SampleInitialYaw(ExperimentRandom.InitialHeadingSample(next.initialHeadingSeed, VRIndex)), rotation.eulerAngles.z);
        ClosedLoop closedLoop = trackedRig = GetComponent<ClosedLoop>();
        if (closedLoop != null)
        {
            closedLoop.ApplyStartPose(position, rotation, next.resetPositionOnStart, next.resetRotationOnStart);
            closedLoop.SetLocustGains(vr?.closedLoopPosition ?? next.closedLoopPosition ?? 1f,
                                      vr?.closedLoopOrientation ?? next.closedLoopOrientation ?? 1f);
        }
        else transform.SetPositionAndRotation(next.resetPositionOnStart ? position : transform.position, next.resetRotationOnStart ? rotation : transform.rotation);
        if (IsSwarm || !next.hideStationaryAnimals)
            walkingSpeed = null;
        else if (walkingSpeed == null || next.resetPositionOnStart || !previousWrapSize.Equals(AnimationWrapSize))
        {
            if (walkingSpeed == null)
            {
                walkingSpeed = new AnimateOnMove.TranslationSpeed(transform.position, AnimationWrapSize);
                walkingAnimalVisible = false;
            }
            else
                walkingSpeed.Reset(transform.position, AnimationWrapSize);
            walkingVisibilitySince = null;
            walkingPoseContinuityId = trackedRig != null ? trackedRig.PoseContinuityId : 0;
        }
        else if (previousWalkingThreshold != next.animationSpeedThreshold)
            walkingVisibilitySince = null;
        ConfigureCameras();
        UpdateAnimalVisibility();
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
        else if (IsMixed)
        {
            if (!wasMixed)
            {
                CleanupClones();
                DisablePrivateSwarm();
                mirrorPopulationKey = swarmPopulationKey = null;
            }
            AppendSharedArenaCopies(next.avatarCopyOffsets);
        }
        else
        {
            DisablePrivateSwarm();
            string key = JsonConvert.SerializeObject(new { numberOfRings, hexRadius, prefab = prefab.GetInstanceID(), next.animateOnMove,
                next.animationSpeedThreshold, next.hideStationaryAnimals, next.periodicBoundary, next.boundaryLengthX, next.boundaryLengthZ,
                next.useHeadingReference, watchIndex });
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

    private void DisablePrivateSwarm()
    {
        if (swarmSpawner != null) swarmSpawner.PrepareObservation();
        var logger = GetComponent<SwarmLogger>();
        if (logger != null) logger.enabled = false;
    }

    private void ConfigureCameras()
    {
        int sharedMask = SharedSwarmMask;
        int swarmMask = RuntimeRigRegistry.StimulusMask | sharedMask;
        int ownLayer = RuntimeRigRegistry.StimulusLayer(VRIndex);
        if (watchIndex.HasValue && RuntimeRigRegistry.StimulusLayer(watchIndex.Value) < 0)
            throw new InvalidOperationException("Kannadi watchIndex has no configured stimulus layer: " + watchIndex.Value);
        int visible = IsMixed ? RuntimeRigRegistry.PeerStimulusMask(VRIndex) | sharedMask
            : IsSwarm ? 1 << ownLayer
            : watchIndex.HasValue ? (1 << RuntimeRigRegistry.StimulusLayer(watchIndex.Value))
            : RuntimeRigRegistry.PeerStimulusMask(VRIndex);
        int markerMask = LayerMask.GetMask("OverheadCameraMarkers");
        foreach (Camera camera in GetComponentsInChildren<Camera>(true))
        {
            // Preserve the floor and shared world, replace only the animal layers.
            camera.cullingMask = (camera.cullingMask & ~swarmMask & ~markerMask) | visible;
            PeriodicArenaView.Configure(camera, SeamlessPeriodicView, MirrorBoundaryX, MirrorBoundaryZ);
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
        if (IsMixed) return;
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
                copies.Add(CreateAvatar(layer, copies.Count));
                positions.Add(offset);
            }
        }
        Clones = copies.ToArray(); offsets = positions.ToArray();
        mirrorBatch?.Invalidate();
        UpdateClones();
    }

    private void AppendSharedArenaCopies(Vector3Config[] copyOffsets)
    {
        int firstNewBody = Clones.Length;
        int bodyCount = 1 + copyOffsets.Length;
        if (firstNewBody == bodyCount) return;
        int layer = RuntimeRigRegistry.StimulusLayer(VRIndex);
        var bodies = new GameObject[bodyCount];
        var positions = new Vector3[bodyCount];
        Clones.CopyTo(bodies, 0);
        offsets.CopyTo(positions, 0);
        prefabRotation = tilePrefab.transform.rotation;
        for (int i = firstNewBody; i < bodyCount; i++)
        {
            var offset = i == 0 ? Vector3.zero : copyOffsets[i - 1].ToVector3();
            offset.y = tilePrefab.transform.position.y;
            positions[i] = offset;
            bodies[i] = CreateAvatar(layer, i);
            if (SeamlessPeriodicView) PeriodicArenaView.RegisterBody(bodies[i]);
        }
        Clones = bodies;
        offsets = positions;
        UpdateClones(firstNewBody);
    }

    private GameObject CreateAvatar(int layer, int ordinal)
    {
        GameObject clone = ExternalModelLibrary.InstantiatePrefab(tilePrefab);
        clone.transform.SetPositionAndRotation(Vector3.zero, prefabRotation);
        if (!string.IsNullOrWhiteSpace(config.kannadiModelPath) || !string.IsNullOrWhiteSpace(config.modelPath)) ExternalModelLibrary.ConfigureAnimation(clone);
        clone.name = $"VR{VRIndex}_Kannadi_{ordinal}";
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
            // BandSpawner.Start adds child renderers after the avatar is created.
            if (!AnimalsVisible) refreshBandRenderersThroughFrame = Time.frameCount + 1;
        }
        AnimateOnMove.ConfigureHierarchy(clone, transform, config.animateOnMove, config.animationSpeedThreshold, AnimationWrapSize, walkingSpeed);
        clone.AddComponent<KannadiMirrorTile>().ConfigureAnimation(transform, config.animateOnMove, config.animationSpeedThreshold, AnimationWrapSize, walkingSpeed);
        ModelAppearance.Apply(clone, config.modelAppearance);
        if (!AnimalsVisible) SetAvatarVisibility(clone);
        return clone;
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
        // A rig reset should clear the speed window. The regular arena wrap below remains part of continuous motion.
        bool resetWalkingSpeed = walkingSpeed != null && trackedRig != null &&
            trackedRig.PoseContinuityId != walkingPoseContinuityId;
        if (!IsSwarm && config.periodicBoundary)
        {
            Vector3 position = transform.position;
            Vector3 wrapped = WrapPosition(position);
            if (Mathf.Abs(wrapped.x - position.x) > MirrorBoundaryX / 2f ||
                Mathf.Abs(wrapped.z - position.z) > MirrorBoundaryZ / 2f)
                trackedRig?.MarkPositionDiscontinuity();
            transform.position = wrapped;
        }
        if (walkingSpeed != null)
        {
            if (resetWalkingSpeed)
            {
                walkingSpeed.Reset(transform.position, AnimationWrapSize);
                walkingVisibilitySince = null;
            }
            else
            {
                walkingSpeed.Sample(transform.position, Time.deltaTime, config.animationSpeedThreshold, useHysteresis: true);
                UpdateWalkingVisibility(walkingSpeed.Speed, Time.timeAsDouble);
            }
            walkingPoseContinuityId = trackedRig != null ? trackedRig.PoseContinuityId : 0;
        }
        UpdateAnimalVisibility();
        UpdateClones();
        mirrorBatch?.RebuildIfPending();
    }

    private void UpdateWalkingVisibility(float speed, double time)
    {
        float startThreshold = config.animationSpeedThreshold;
        float stopThreshold = startThreshold * AnimateOnMove.WalkingStopThresholdRatio;
        // A zero threshold still allows a fully stationary animal to hide.
        bool shouldChange = walkingAnimalVisible ? speed < stopThreshold || speed == 0f : speed > startThreshold;
        if (!shouldChange)
        {
            walkingVisibilitySince = null;
            return;
        }
        walkingVisibilitySince ??= time;
        if (time - walkingVisibilitySince.Value < WalkingVisibilityHoldSeconds) return;

        walkingAnimalVisible = !walkingAnimalVisible;
        walkingVisibilitySince = null;
    }

    private void UpdateAnimalVisibility()
    {
        bool visible = IsSwarm || ((walkingSpeed == null || walkingAnimalVisible) &&
            (!config.hideInactiveAnimals || (tracker != null && tracker.isActiveAndEnabled && tracker.HasFreshPose())));
        if (AnimalsVisible == visible && Time.frameCount > refreshBandRenderersThroughFrame) return;
        AnimalsVisible = visible;
        foreach (var clone in Clones)
            if (clone != null) SetAvatarVisibility(clone);
    }

    private void SetAvatarVisibility(GameObject avatar)
    {
        foreach (var renderer in avatar.GetComponentsInChildren<Renderer>(true))
            renderer.forceRenderingOff = !AnimalsVisible;
    }

    private void UpdateClones(int firstOrdinal = 0)
    {
        for (int i = firstOrdinal; i < Clones.Length; i++)
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
