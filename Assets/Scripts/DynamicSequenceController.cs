using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InSceneSequence;
using Newtonsoft.Json;
using UnityEngine;

public class DynamicSequenceController : MonoBehaviour, IInSceneSequencer
{
    [Serializable]
    private class DesignFile
    {
        public int seed = -1;
        public int repetitions = 1;
        public bool sync = true;
        public Step intertrial;
        public Step[] steps;
        public float size = 1.5f;
        public float[] boxSize;
        public AdaptiveDecisionConfig adaptiveDecision;
    }

    [Serializable]
    private class AdaptiveDecisionConfig
    {
        public bool enabled = false;
        public float startGray = 0.5f;
        public float grayStep = 0.05f;
        public int controlEvery = 5;
        public float noBarControlSeconds = 20f;
    }

    [Serializable]
    private class Step
    {
        public string name = "";
        public Trigger trigger;
        public SceneObjectSpec[] objects;
        public CameraSpec[] camera;
        public string skybox;
        public string[] resetVR;
        public bool closedLoopOrientation;
        public bool closedLoopPosition;
        public Vector3 initialPosition = Vector3.zero;
        public Vector3 initialRotation = Vector3.zero;
        public bool randomInitialRotation = false;
        public float swapAfterSeconds = 0f;
    }

    [Serializable]
    public class Trigger
    {
        public string type;
        public float seconds;
        public string areaTag;
        public string vrId;
        public string shape = "box";
        public float size = 1.5f;
        public float[] boxSize;
        public float radius;
        public float height;
        public float timeoutSeconds = 0f;
        public bool triggerOnExit = false;
        public bool advanceOnTrigger = true;
    }

    [Serializable]
    public class SceneObjectSpec
    {
        public string type;
        public Polar polar;
        public Scale scale;
        public string material;
        public float[] color;
        public float[] swapColor;
        public bool flip;
        public float visualAngleDegrees;

        public bool randomInitialRotation = false;
        public float mu = 0f;

        public string prefab;
        public float[] pos;
        public float[] rot;
        public string mat;
        public string role;
    }

    [Serializable]
    public class Polar
    {
        public float radius, angle, height;
    }

    [Serializable]
    public class Scale
    {
        public float x, y, z;
    }

    [Serializable]
    public class CameraSpec
    {
        public string vrId;
        public CameraClearFlags clearFlags = CameraClearFlags.SolidColor;
        public float[] bgColor;
    }

    private class RigState
    {
        public string id;
        public Transform rig;
        public List<Step> steps;
        public int index = -1;
        public int loopCount = 0;
        public int cumulativeStep = 0;
        public Transform container;
        public Coroutine routine;
        public float adaptiveGray;
        public int adaptiveTrialCount = 0;
    }

    private class AreaWaitResult
    {
        public bool Triggered;
        public int TriggeredObjectIndex = -1;
        public bool TimedOut;
    }

    private class SpawnedObject
    {
        public SceneObjectSpec Spec;
        public Renderer Renderer;
    }

    private DesignFile designFile;
    private List<Step> orderedSteps;
    private readonly Dictionary<string, Transform> players = new();
    private readonly Dictionary<string, RigState> rigStates = new();

    [Header("Drag every prefab that can appear in a step")]
    public GameObject[] prefabs;

    [Header("Drag every material that can appear in a step")]
    public Material[] materials;

    [Header("Sequencer")]
    [Tooltip("If true, when a rig reaches the end of its sequence it will loop from the start.")]
    public bool loopSequence = true;

    private readonly Dictionary<string, GameObject> prefabDict = new();
    private readonly Dictionary<string, Material> materialDict = new();

    private void Awake()
    {
        foreach (var p in prefabs)
            prefabDict[p.name] = p;
        foreach (var m in materials)
            materialDict[m.name] = m;
    }

    public void InitializeScene(Dictionary<string, object> parameters)
    {
        string design = parameters != null && parameters.TryGetValue("design", out var p)
            ? p.ToString()
            : "dynamicSequenceDesign.json";

        LoadDesign(design);
        CachePlayers();
        InitRigStates();

        foreach (var state in rigStates.Values)
            state.routine = StartCoroutine(RunRigSequence(state));
    }

    public void CleanupScene() { }

    public void AdvanceStep(Dictionary<string, object> parameters)
    {
        // not used; sequence advances internally
    }

    private void LoadDesign(string file)
    {
        string path = Path.Combine(Application.streamingAssetsPath, file);
        designFile = JsonConvert.DeserializeObject<DesignFile>(File.ReadAllText(path));
        orderedSteps = IsAdaptiveDecisionEnabled() ? new List<Step>() : BuildOrdered(designFile, null);
    }

    private bool IsAdaptiveDecisionEnabled()
    {
        return designFile?.adaptiveDecision != null && designFile.adaptiveDecision.enabled;
    }

    private List<Step> BuildOrdered(DesignFile design, int? seedOverride)
    {
        int seed = seedOverride ?? design.seed;
        var rng = new System.Random(seed < 0 ? Environment.TickCount : seed);
        var list = new List<Step>();

        for (int rep = 0; rep < design.repetitions; ++rep)
        {
            var trials = new List<Step>(design.steps ?? Array.Empty<Step>());
            for (int n = trials.Count; n > 1; --n)
            {
                int k = rng.Next(n);
                (trials[k], trials[n - 1]) = (trials[n - 1], trials[k]);
            }

            foreach (var t in trials)
            {
                if (design.intertrial != null)
                    list.Add(design.intertrial);
                list.Add(t);
            }
        }
        return list;
    }

    private void CachePlayers()
    {
        foreach (var dl in FindObjectsOfType<DataLogger>())
            players[dl.name] = dl.transform;
    }

    private void InitRigStates()
    {
        float startGray = designFile?.adaptiveDecision != null ? designFile.adaptiveDecision.startGray : 0.5f;

        foreach (var kv in players)
        {
            var container = new GameObject($"Rig_{kv.Key}_Objects").transform;
            container.SetParent(transform);

            rigStates[kv.Key] = new RigState
            {
                id = kv.Key,
                rig = kv.Value,
                steps = new List<Step>(orderedSteps),
                container = container,
                adaptiveGray = Mathf.Clamp01(startGray)
            };
        }
    }

    private IEnumerator RunRigSequence(RigState state)
    {
        if (IsAdaptiveDecisionEnabled())
        {
            yield return RunAdaptiveSequence(state);
            yield break;
        }

        while (true)
        {
            state.index++;
            if (state.index >= state.steps.Count)
            {
                if (loopSequence)
                {
                    if (designFile != null)
                        state.steps = BuildOrdered(designFile, Environment.TickCount);
                    state.loopCount++;
                    state.index = -1;
                    continue;
                }
                yield break;
            }

            var step = state.steps[state.index];
            yield return RunSingleStep(state, step, null, null);
        }
    }

    private IEnumerator RunAdaptiveSequence(RigState state)
    {
        var cfg = designFile.adaptiveDecision;

        while (true)
        {
            if (designFile.intertrial != null)
            {
                state.index++;
                var intertrialStep = CloneStep(designFile.intertrial);
                intertrialStep.name = $"{designFile.intertrial.name}_adaptive";
                yield return RunSingleStep(state, intertrialStep, null, null);
            }

            state.adaptiveTrialCount++;
            bool isControlTrial = cfg.controlEvery > 0 && state.adaptiveTrialCount % cfg.controlEvery == 0;

            string blackSideAtTrialStart;
            Step trialStep = isControlTrial
                ? BuildAdaptiveControlStep(state, cfg, out blackSideAtTrialStart)
                : BuildAdaptiveNormalStep(state, out blackSideAtTrialStart);

            state.index++;
            var areaResult = new AreaWaitResult();
            yield return RunSingleStep(
                state,
                trialStep,
                areaResult,
                () => LogAdaptiveTrialStart(state, state.adaptiveGray, blackSideAtTrialStart)
            );

            if (isControlTrial || !areaResult.Triggered || areaResult.TriggeredObjectIndex < 0)
                continue;

            if (trialStep.objects == null || areaResult.TriggeredObjectIndex >= trialStep.objects.Length)
                continue;

            string role = trialStep.objects[areaResult.TriggeredObjectIndex].role ?? "";
            if (string.Equals(role, "black", StringComparison.OrdinalIgnoreCase))
                state.adaptiveGray = Mathf.Clamp01(state.adaptiveGray - cfg.grayStep);
            else if (string.Equals(role, "gray", StringComparison.OrdinalIgnoreCase))
                state.adaptiveGray = Mathf.Clamp01(state.adaptiveGray + cfg.grayStep);
        }
    }

    private IEnumerator RunSingleStep(RigState state, Step step, AreaWaitResult areaResult, Action onStepStarted)
    {
        foreach (Transform child in state.container)
            Destroy(child.gameObject);

        yield return null;

        ResetVR(state.id, step);
        float stepStartTime = Time.time;
        var spawned = SpawnObjects(state.id, step.objects, state.container);

        ApplyCameraSettings(state.id, step.camera);
        ApplySkybox(step.skybox);
        ApplyClosedLoopFlags(state.id, step);

        state.rig.GetComponent<DataLogger>()?.SetStep(state.index, step.name, state.loopCount, state.cumulativeStep);
        onStepStarted?.Invoke();

        if (step.swapAfterSeconds > 0f && spawned.Count > 0)
            StartCoroutine(SwapAfterDelay(state, step, spawned, stepStartTime));

        if (step.trigger == null)
        {
            Debug.LogWarning($"[DynamicSequence] {state.id} step {step.name} missing trigger -> advancing immediately");
        }
        else if (step.trigger.type == "time")
        {
            yield return new WaitForSeconds(step.trigger.seconds);
        }
        else
        {
            if (areaResult == null)
                areaResult = new AreaWaitResult();
            yield return WaitForArea(state, step, areaResult);
        }

        state.cumulativeStep++;
    }

    private List<SpawnedObject> SpawnObjects(string vrId, SceneObjectSpec[] specs, Transform parent)
    {
        var spawned = new List<SpawnedObject>();
        if (specs == null)
            return spawned;

        foreach (var obj in specs)
        {
            string prefabName = string.IsNullOrEmpty(obj.type) ? obj.prefab : obj.type;
            if (!prefabDict.TryGetValue(prefabName, out var prefab))
            {
                Debug.LogWarning($"Prefab '{prefabName}' missing");
                continue;
            }

            int vrIndex = int.Parse(vrId.Substring(2));
            string layerName = $"ChoiceVR{vrIndex}";
            int layerId = LayerMask.NameToLayer(layerName);

            Vector3 position = obj.polar != null ? PolarToXZ(obj.polar) : ToVector3(obj.pos);
            Quaternion rotation = obj.randomInitialRotation
                ? Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0)
                : Quaternion.Euler(0, obj.mu, 0);

            GameObject instance = Instantiate(prefab, position, rotation, parent);
            instance.tag = layerName;
            if (layerId != -1)
                SetLayerRecursively(instance, layerId);

            Vector3 scale = obj.scale != null
                ? new Vector3(obj.scale.x, obj.scale.y, obj.scale.z)
                : instance.transform.localScale;
            if (obj.flip)
                scale.x *= -1;
            instance.transform.localScale = scale;

            Renderer rend = null;
            if (instance.TryGetComponent<Renderer>(out rend))
            {
                string matName = !string.IsNullOrEmpty(obj.material) ? obj.material : obj.mat;
                if (!string.IsNullOrEmpty(matName) && materialDict.TryGetValue(matName, out var mat))
                    rend.material = new Material(mat);

                if (obj.color != null && obj.color.Length >= 3 && rend.material != null)
                    rend.material.color = ToColor(obj.color);
            }

            if (instance.TryGetComponent<ScaleWithDistance>(out var swd))
                swd.visualAngleDegrees = obj.visualAngleDegrees;

            spawned.Add(new SpawnedObject
            {
                Spec = obj,
                Renderer = rend
            });
        }

        return spawned;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private void ApplyCameraSettings(string vrId, CameraSpec[] specs)
    {
        if (specs == null)
            return;

        foreach (var spec in specs)
        {
            if (spec.vrId != vrId)
                continue;
            if (!players.TryGetValue(spec.vrId, out var rig))
                continue;

            Camera[] cams = rig.GetComponentsInChildren<Camera>(true);
            foreach (var cam in cams)
            {
                cam.clearFlags = spec.clearFlags;
                if (spec.bgColor != null && spec.bgColor.Length >= 3)
                    cam.backgroundColor = ToColor(spec.bgColor);
            }
        }
    }

    private void ApplyClosedLoopFlags(string vrId, Step s)
    {
        if (!s.closedLoopOrientation && !s.closedLoopPosition)
            return;

        if (players.TryGetValue(vrId, out var rig))
        {
            foreach (var cl in rig.GetComponentsInChildren<ClosedLoop>())
            {
                cl.SetClosedLoopOrientation(s.closedLoopOrientation);
                cl.SetClosedLoopPosition(s.closedLoopPosition);
            }
        }
    }

    private void ResetVR(string vrId, Step step)
    {
        if (!players.TryGetValue(vrId, out var rig))
            return;

        Quaternion initialRotation = step.randomInitialRotation
            ? Quaternion.Euler(step.initialRotation.x, UnityEngine.Random.Range(0f, 360f), step.initialRotation.z)
            : Quaternion.Euler(step.initialRotation);

        ClosedLoop cl = rig.GetComponent<ClosedLoop>();
        if (cl != null)
        {
            cl.SetPositionAndRotation(step.initialPosition, initialRotation);
            cl.ResetPositionAndRotation();
        }
    }

    private void ApplySkybox(string skyboxName)
    {
        if (string.IsNullOrEmpty(skyboxName))
            return;

        Material sky = Resources.Load<Material>(skyboxName);
        if (sky)
            RenderSettings.skybox = sky;
        else
            Debug.LogWarning($"Skybox '{skyboxName}' not found in Resources");
    }

    private IEnumerator WaitForArea(RigState state, Step step, AreaWaitResult result)
    {
        bool done = false;
        var triggers = new List<GameObject>();
        var specs = step.objects ?? Array.Empty<SceneObjectSpec>();

        if (specs.Length == 0)
        {
            specs = new[] { new SceneObjectSpec { polar = new Polar { radius = 0, angle = 0, height = 0 } } };
        }

        int i = 0;
        foreach (var obj in specs)
        {
            int localIndex = i;
            var go = new GameObject($"Trigger_{state.id}_{state.index}_{i++}");
            go.transform.parent = state.container;
            string targetTag = string.IsNullOrEmpty(step.trigger.areaTag) ? "Untagged" : step.trigger.areaTag;
            try
            {
                go.tag = targetTag;
            }
            catch (UnityException)
            {
                go.tag = "Untagged";
            }

            go.transform.position = obj.polar != null ? PolarToXZ(obj.polar) : ToVector3(obj.pos);

            Collider triggerCollider;
            bool useCylinder = string.Equals(step.trigger.shape, "cylinder", StringComparison.OrdinalIgnoreCase);
            if (useCylinder)
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.isTrigger = true;
                cap.direction = 1;

                float baseSize = Mathf.Max(0.5f, step.trigger.size);
                float radius = step.trigger.radius > 0 ? step.trigger.radius : baseSize * 0.5f;
                float height = step.trigger.height > 0 ? step.trigger.height : baseSize;

                if (obj.scale != null)
                {
                    radius = Mathf.Max(radius, Mathf.Max(obj.scale.x, obj.scale.z) * 0.5f);
                    height = Mathf.Max(height, obj.scale.y);
                }

                cap.radius = radius;
                cap.height = Mathf.Max(cap.radius * 2f, height);
                triggerCollider = cap;
            }
            else
            {
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;

                Vector3 size = Vector3.one * Mathf.Max(0.5f, step.trigger.size);
                if (step.trigger.boxSize != null && step.trigger.boxSize.Length >= 3)
                {
                    size = new Vector3(step.trigger.boxSize[0], step.trigger.boxSize[1], step.trigger.boxSize[2]);
                }
                else if (obj.scale != null)
                {
                    size = new Vector3(
                        Mathf.Max(size.x, obj.scale.x),
                        Mathf.Max(size.y, obj.scale.y),
                        Mathf.Max(size.z, obj.scale.z)
                    );
                }
                box.size = size;
                triggerCollider = box;
            }

            void Handler(Collider c)
            {
                string targetVr = string.IsNullOrEmpty(step.trigger.vrId) || step.trigger.vrId == "any"
                    ? state.id
                    : step.trigger.vrId;

                if (c && c.transform.root && c.transform.root.name == targetVr)
                {
                    result.Triggered = true;
                    result.TriggeredObjectIndex = localIndex;
                    if (!step.trigger.advanceOnTrigger)
                    {
                        ResetVR(targetVr, step);
                    }
                    else
                    {
                        done = true;
                    }
                }
            }

            bool useExit = step.trigger != null && step.trigger.triggerOnExit;
            go.AddComponent<TriggerRelay>().Init(useExit ? null : Handler, useExit ? Handler : null);
            triggers.Add(go);

            _ = triggerCollider;
        }

        float deadline = step.trigger.timeoutSeconds > 0
            ? Time.time + step.trigger.timeoutSeconds
            : float.PositiveInfinity;

        while (!done)
        {
            if (Time.time >= deadline)
            {
                result.TimedOut = true;
                break;
            }
            yield return null;
        }

        foreach (var go in triggers)
            Destroy(go);
    }

    private IEnumerator SwapAfterDelay(RigState state, Step step, List<SpawnedObject> spawned, float stepStartTime)
    {
        yield return new WaitForSeconds(step.swapAfterSeconds);

        ApplySwapColors(spawned, step);
        float elapsed = Time.time - stepStartTime;
        LogSwapEvent(state, elapsed);
    }

    private void ApplySwapColors(List<SpawnedObject> spawned, Step step)
    {
        if (spawned == null || spawned.Count == 0)
            return;

        bool hasExplicit = spawned.Any(s => s.Spec.swapColor != null && s.Spec.swapColor.Length >= 3);

        if (hasExplicit)
        {
            foreach (var so in spawned)
            {
                if (so.Renderer == null || so.Spec.swapColor == null || so.Spec.swapColor.Length < 3)
                    continue;
                so.Renderer.material.color = ToColor(so.Spec.swapColor);
            }
        }
        else if (spawned.Count >= 2)
        {
            Color c0 = spawned[0].Renderer ? spawned[0].Renderer.material.color : Color.black;
            Color c1 = spawned[1].Renderer ? spawned[1].Renderer.material.color : Color.black;

            if (spawned[0].Renderer)
                spawned[0].Renderer.material.color = c1;
            if (spawned[1].Renderer)
                spawned[1].Renderer.material.color = c0;
        }
    }

    private void LogSwapEvent(RigState state, float elapsedSeconds)
    {
        var logger = state.rig.GetComponent<DataLogger>();
        if (logger == null)
            return;

        logger.SetData("swapElapsedSec", elapsedSeconds);
        logger.SetData("swapWallClock", DateTime.Now.ToString("HH:mm:ss.fff"));
        logger.UpdateLogger();
    }

    private void LogAdaptiveTrialStart(RigState state, float gray, string blackSide)
    {
        var logger = state.rig.GetComponent<DataLogger>();
        if (logger == null)
            return;

        logger.SetData("grayAtTrialStart", gray);
        logger.SetData("blackSideAtTrialStart", blackSide);
        logger.UpdateLogger();
    }

    private Step BuildAdaptiveNormalStep(RigState state, out string blackSide)
    {
        Step step = CloneStep(GetAdaptiveTemplateStep());
        bool blackOnLeft = UnityEngine.Random.value < 0.5f;
        blackSide = blackOnLeft ? "left" : "right";

        step.name = $"AdaptiveDecision_Normal_{state.adaptiveTrialCount:D4}_{(blackOnLeft ? "blackL" : "blackR")}";

        if (step.objects == null || step.objects.Length < 2)
            step.objects = BuildDefaultPairObjects();

        int leftIndex = GetLeftObjectIndex(step.objects);
        int rightIndex = leftIndex == 0 ? 1 : 0;

        float[] black = { 0f, 0f, 0f, 1f };
        float g = Mathf.Clamp01(state.adaptiveGray);
        float[] gray = { g, g, g, 1f };

        if (blackOnLeft)
        {
            step.objects[leftIndex].color = black;
            step.objects[leftIndex].role = "black";
            step.objects[rightIndex].color = gray;
            step.objects[rightIndex].role = "gray";
        }
        else
        {
            step.objects[leftIndex].color = gray;
            step.objects[leftIndex].role = "gray";
            step.objects[rightIndex].color = black;
            step.objects[rightIndex].role = "black";
        }

        return step;
    }

    private Step BuildAdaptiveControlStep(RigState state, AdaptiveDecisionConfig cfg, out string blackSide)
    {
        blackSide = "control";
        Step step = CloneStep(GetAdaptiveTemplateStep());
        int controlType = UnityEngine.Random.Range(0, 3);
        float[] black = { 0f, 0f, 0f, 1f };

        switch (controlType)
        {
            case 0:
                step.name = $"AdaptiveDecision_Control_twoBlack_{state.adaptiveTrialCount:D4}";
                if (step.objects == null || step.objects.Length < 2)
                    step.objects = BuildDefaultPairObjects();
                foreach (var obj in step.objects.Take(2))
                {
                    obj.color = black;
                    obj.role = "black";
                }
                break;

            case 1:
                step.name = $"AdaptiveDecision_Control_singleBlack0deg_{state.adaptiveTrialCount:D4}";
                var source = step.objects != null && step.objects.Length > 0
                    ? CloneObject(step.objects[0])
                    : BuildDefaultObject(-20f);
                source.polar.radius = ResolveTemplateRadius(step.objects);
                source.polar.angle = 0f;
                source.color = black;
                source.role = "black";
                step.objects = new[] { source };
                break;

            default:
                step.name = $"AdaptiveDecision_Control_noBar_{state.adaptiveTrialCount:D4}";
                step.objects = Array.Empty<SceneObjectSpec>();
                step.trigger = new Trigger
                {
                    type = "time",
                    seconds = Mathf.Max(0.1f, cfg.noBarControlSeconds)
                };
                break;
        }

        return step;
    }

    private Step GetAdaptiveTemplateStep()
    {
        if (designFile?.steps != null && designFile.steps.Length > 0 && designFile.steps[0] != null)
            return designFile.steps[0];

        return new Step
        {
            name = "AdaptiveDecisionTemplate",
            trigger = new Trigger
            {
                type = "area",
                areaTag = "Goal",
                vrId = "any",
                shape = "cylinder",
                size = 5f,
                radius = 2.5f,
                height = 5f,
                timeoutSeconds = 180f
            },
            closedLoopOrientation = true,
            closedLoopPosition = true,
            randomInitialRotation = false,
            objects = BuildDefaultPairObjects(),
            camera = BuildDefaultCameraSpecs()
        };
    }

    private SceneObjectSpec[] BuildDefaultPairObjects()
    {
        return new[] { BuildDefaultObject(-20f), BuildDefaultObject(20f) };
    }

    private SceneObjectSpec BuildDefaultObject(float angleDeg)
    {
        return new SceneObjectSpec
        {
            type = "ScalingCylinder",
            material = "SetColor",
            polar = new Polar { radius = 60f, angle = angleDeg, height = 0f },
            scale = new Scale { x = 7f, y = 100f, z = 7f },
            visualAngleDegrees = 10f
        };
    }

    private CameraSpec[] BuildDefaultCameraSpecs()
    {
        return new[]
        {
            new CameraSpec { vrId = "VR1", clearFlags = CameraClearFlags.SolidColor, bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f } },
            new CameraSpec { vrId = "VR2", clearFlags = CameraClearFlags.SolidColor, bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f } },
            new CameraSpec { vrId = "VR3", clearFlags = CameraClearFlags.SolidColor, bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f } },
            new CameraSpec { vrId = "VR4", clearFlags = CameraClearFlags.SolidColor, bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f } }
        };
    }

    private float ResolveTemplateRadius(SceneObjectSpec[] objects)
    {
        if (objects == null || objects.Length == 0)
            return 60f;

        foreach (var obj in objects)
        {
            if (obj?.polar != null && obj.polar.radius > 0f)
                return obj.polar.radius;
        }
        return 60f;
    }

    private int GetLeftObjectIndex(SceneObjectSpec[] objects)
    {
        if (objects == null || objects.Length < 2)
            return 0;

        float a0 = objects[0]?.polar != null ? objects[0].polar.angle : 0f;
        float a1 = objects[1]?.polar != null ? objects[1].polar.angle : 0f;
        return a0 <= a1 ? 0 : 1;
    }

    private Step CloneStep(Step s)
    {
        if (s == null)
            return null;

        return new Step
        {
            name = s.name,
            trigger = s.trigger == null
                ? null
                : new Trigger
                {
                    type = s.trigger.type,
                    seconds = s.trigger.seconds,
                    areaTag = s.trigger.areaTag,
                    vrId = s.trigger.vrId,
                    shape = s.trigger.shape,
                    size = s.trigger.size,
                    boxSize = s.trigger.boxSize != null ? (float[])s.trigger.boxSize.Clone() : null,
                    radius = s.trigger.radius,
                    height = s.trigger.height,
                    timeoutSeconds = s.trigger.timeoutSeconds,
                    triggerOnExit = s.trigger.triggerOnExit,
                    advanceOnTrigger = s.trigger.advanceOnTrigger
                },
            objects = s.objects?.Select(CloneObject).ToArray(),
            camera = s.camera?.Select(c => new CameraSpec
            {
                vrId = c.vrId,
                clearFlags = c.clearFlags,
                bgColor = c.bgColor != null ? (float[])c.bgColor.Clone() : null
            }).ToArray(),
            skybox = s.skybox,
            resetVR = s.resetVR != null ? (string[])s.resetVR.Clone() : null,
            closedLoopOrientation = s.closedLoopOrientation,
            closedLoopPosition = s.closedLoopPosition,
            initialPosition = s.initialPosition,
            initialRotation = s.initialRotation,
            randomInitialRotation = s.randomInitialRotation,
            swapAfterSeconds = s.swapAfterSeconds
        };
    }

    private SceneObjectSpec CloneObject(SceneObjectSpec obj)
    {
        if (obj == null)
            return null;

        return new SceneObjectSpec
        {
            type = obj.type,
            polar = obj.polar == null ? null : new Polar { radius = obj.polar.radius, angle = obj.polar.angle, height = obj.polar.height },
            scale = obj.scale == null ? null : new Scale { x = obj.scale.x, y = obj.scale.y, z = obj.scale.z },
            material = obj.material,
            color = obj.color != null ? (float[])obj.color.Clone() : null,
            swapColor = obj.swapColor != null ? (float[])obj.swapColor.Clone() : null,
            flip = obj.flip,
            visualAngleDegrees = obj.visualAngleDegrees,
            randomInitialRotation = obj.randomInitialRotation,
            mu = obj.mu,
            prefab = obj.prefab,
            pos = obj.pos != null ? (float[])obj.pos.Clone() : null,
            rot = obj.rot != null ? (float[])obj.rot.Clone() : null,
            mat = obj.mat,
            role = obj.role
        };
    }

    private static Vector3 ToVector3(float[] arr)
    {
        return arr != null && arr.Length >= 3 ? new Vector3(arr[0], arr[1], arr[2]) : Vector3.zero;
    }

    private static Color ToColor(float[] arr)
    {
        return arr != null && arr.Length >= 3
            ? new Color(arr[0], arr[1], arr[2], arr.Length > 3 ? arr[3] : 1)
            : Color.black;
    }

    private static Vector3 PolarToXZ(Polar p)
    {
        if (p == null)
            return Vector3.zero;
        float x = p.radius * Mathf.Sin(p.angle * Mathf.Deg2Rad);
        float z = p.radius * Mathf.Cos(p.angle * Mathf.Deg2Rad);
        return new Vector3(x, p.height, z);
    }
}

public class TriggerRelay : MonoBehaviour
{
    private Action<Collider> onEnter;
    private Action<Collider> onExit;

    public void Init(Action<Collider> enter, Action<Collider> exit = null)
    {
        onEnter = enter;
        onExit = exit;
    }

    private void OnTriggerEnter(Collider other) => onEnter?.Invoke(other);

    private void OnTriggerExit(Collider other) => onExit?.Invoke(other);
}
