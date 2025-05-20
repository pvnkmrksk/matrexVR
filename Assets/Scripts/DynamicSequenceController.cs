using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using InSceneSequence;
using System;

public class DynamicSequenceController : MonoBehaviour, IInSceneSequencer
{
    [System.Serializable]
    private class DesignFile
    {
        public int seed = -1;
        public int repetitions = 1;
        public bool sync = true;
        public Step intertrial;
        public Step[] steps;
    }

    [System.Serializable]
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
    }

    [System.Serializable]
    public class Trigger
    {
        public string type; // "time" | "area"
        public float seconds; // if time
        public string areaTag; // if area
        public string vrId; // "any" or "VR1", …
    }

    [System.Serializable]
    public class SceneObjectSpec
    {
        public string type; // prefab name (new)
        public Polar polar; // radius/angle/height (new)
        public Scale scale; // object scale   (new)
        public string material; // material name  (new)
        public bool flip; // mirror on X    (new)
        public float visualAngleDegrees; // for ScaleWithDistance (new)

        public bool randomInitialRotation = false;
        public float mu = 0f; 

        // legacy fields still accepted
        public string prefab;
        public float[] pos;
        public float[] rot;
        public string mat; // ← legacy material alias
    }

    [System.Serializable]
    public class Polar
    {
        public float radius,
            angle,
            height;
    }

    [System.Serializable]
    public class Scale
    {
        public float x,
            y,
            z;
    }

    [System.Serializable]
    public class CameraSpec
    {
        public string vrId;
        public CameraClearFlags clearFlags = CameraClearFlags.SolidColor;
        public float[] bgColor; // r,g,b,a 0-1
    }

    private List<Step> orderedSteps;
    private int currentStep = -1;
    private Dictionary<string, Transform> players = new();

    // ───────── inspector convenience ─────────
    [Header("Drag every prefab that can appear in a step")]
    public GameObject[] prefabs;

    [Header("Drag every material that can appear in a step")]
    public Material[] materials;

    // internal lookup maps
    private readonly Dictionary<string, GameObject> prefabDict = new();
    private readonly Dictionary<string, Material> materialDict = new();

    // ───────────────────────────── ISceneController ───────────────────────────
    private void Awake()
    {
        foreach (var p in prefabs)
            prefabDict[p.name] = p;
        foreach (var m in materials)
            materialDict[m.name] = m;
    }

    public void InitializeScene(Dictionary<string, object> parameters)
    {
        string designFile =
            parameters != null && parameters.TryGetValue("design", out var p)
                ? p.ToString()
                : "dynamicSequenceDesign.json";

        LoadDesign(designFile);
        CachePlayers();

        StartCoroutine(RunNextStep());
    }

    // not used here
    public void CleanupScene() { }

    // ─────────────────────── IInSceneSequencer (from MainController) ──────────
    public void AdvanceStep(Dictionary<string, object> parameters)
    {
        // we don’t rely on MainController to advance; use it only for legacy fall-backs
    }

    // ───────────────────────────────── internal ───────────────────────────────
    private void LoadDesign(string file)
    {
        string path = Path.Combine(Application.streamingAssetsPath, file);
        var design = JsonConvert.DeserializeObject<DesignFile>(File.ReadAllText(path));

        // shuffle etc. (reuse SequenceConfigGenerator logic)
        orderedSteps = BuildOrdered(design);
    }

    private List<Step> BuildOrdered(DesignFile design)
    {
        var rng = new System.Random(design.seed < 0 ? Environment.TickCount : design.seed);
        var list = new List<Step>();

        for (int rep = 0; rep < design.repetitions; ++rep)
        {
            // 1) copy & shuffle the trials
            var trials = new List<Step>(design.steps);
            for (int n = trials.Count; n > 1; --n)
            {
                int k = rng.Next(n);
                (trials[k], trials[n - 1]) = (trials[n - 1], trials[k]);
            }

            // 2) interleave inter-trial
            foreach (var t in trials)
            {
                if (design.intertrial != null) // ← add skybox
                    list.Add(design.intertrial);

                list.Add(t); // then real trial
            }
        }
        return list;
    }

    private void CachePlayers()
    {
        foreach (var dl in FindObjectsOfType<DataLogger>())
            players[dl.name] = dl.transform; // assumes each VR root is named “VR1” …
    }

    private IEnumerator RunNextStep()
    {
        currentStep++;
        if (currentStep >= orderedSteps.Count)
        {
            Debug.Log("[DynamicSequence] finished sequence");
            yield break;
        }

        var step = orderedSteps[currentStep];
        Debug.Log($"[DynamicSequence] Step {currentStep} – {step.name}");

        // 1) cleanup previous
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        // Defer reset and spawn by one frame
        yield return null; // ✅ wait for end of current frame

        ResetVRs(step.resetVR, step);
        SpawnObjects(step.objects);

        // 3) camera tweaks
        ApplyCameraSettings(step.camera);

        if (!string.IsNullOrEmpty(step.skybox))
        {
            // skybox materials live in Resources/SunnySkyMat.mat  (for example)
            Material sky = Resources.Load<Material>(step.skybox);
            if (sky)
                RenderSettings.skybox = sky;
            else
                Debug.LogWarning($"Skybox '{step.skybox}' not found in Resources");
        }

        ApplyClosedLoopFlags(step);

        // 5) tell DataLoggers
        foreach (var kv in players)
            kv.Value.GetComponent<DataLogger>()?.SetStep(currentStep, step.name);

        // 6) arm trigger
        if (step.trigger.type == "time")
            yield return new WaitForSeconds(step.trigger.seconds);
        else
            yield return WaitForArea(step.trigger);

        // 7) recurse
        StartCoroutine(RunNextStep());
    }

    private void SpawnObjects(SceneObjectSpec[] specs)
    {
        if (specs == null) return;

        foreach (var obj in specs)
        {
            string prefabName = string.IsNullOrEmpty(obj.type) ? obj.prefab : obj.type;
            if (!prefabDict.TryGetValue(prefabName, out var prefab))
            {
                Debug.LogWarning($"Prefab '{prefabName}' missing");
                continue;
            }

            foreach (var (vrId, rig) in players)
            {
                int vrIndex = int.Parse(vrId.Substring(2)); // "VR1" → 1
                string layerName = $"ChoiceVR{vrIndex}";
                int layerId = LayerMask.NameToLayer(layerName);

                Vector3 position = obj.polar != null
                    ? PolarToXZ(obj.polar)
                    : ToVector3(obj.pos);

                // Rotation
                Quaternion rotation;
                if (obj.randomInitialRotation)
                    rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
                else
                    rotation = Quaternion.Euler(0, obj.mu, 0);

                // Instantiate
                GameObject instance = Instantiate(prefab, position, rotation, transform);
                instance.tag = layerName;
                if (layerId != -1)
                    SetLayerRecursively(instance, layerId);

                // Scale + Flip
                Vector3 scale = obj.scale != null
                    ? new Vector3(obj.scale.x, obj.scale.y, obj.scale.z)
                    : instance.transform.localScale;

                if (obj.flip)
                    scale.x *= -1;

                instance.transform.localScale = scale;

                // Material
                string matName = !string.IsNullOrEmpty(obj.material) ? obj.material : obj.mat;
                if (!string.IsNullOrEmpty(matName) &&
                    materialDict.TryGetValue(matName, out var mat) &&
                    instance.TryGetComponent<Renderer>(out var rend))
                {
                    rend.material = mat;
                }

                // Visual Angle
                if (instance.TryGetComponent<ScaleWithDistance>(out var swd))
                    swd.visualAngleDegrees = obj.visualAngleDegrees;
            }
        }
    }


    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private void ApplyCameraSettings(CameraSpec[] specs)
    {
        if (specs == null)
            return;

        foreach (var spec in specs)
        {
            if (!players.TryGetValue(spec.vrId, out var rig))
                continue;

            // ✅ get *all* cameras under that VR rig
            Camera[] cams = rig.GetComponentsInChildren<Camera>(true);
            foreach (var cam in cams)
            {
                cam.clearFlags = spec.clearFlags;

                if (spec.bgColor != null && spec.bgColor.Length >= 3)
                    cam.backgroundColor = ToColor(spec.bgColor);
            }
        }
    }

    private void ApplyClosedLoopFlags(Step s)
    {
        if (!s.closedLoopOrientation && !s.closedLoopPosition)
            return;

        foreach (var cl in FindObjectsOfType<ClosedLoop>())
        {
            cl.SetClosedLoopOrientation(s.closedLoopOrientation);
            cl.SetClosedLoopPosition(s.closedLoopPosition);
        }
    }
    private void ResetVRs(string[] ids, Step step)
    {
        // Fallback to all known rigs if none specified
        if (ids == null || ids.Length == 0)
        {
            ids = new string[players.Keys.Count];
            players.Keys.CopyTo(ids, 0);
        }

        Quaternion initialRotation = step.randomInitialRotation
            ? Quaternion.Euler(step.initialRotation.x, UnityEngine.Random.Range(0f, 360f), step.initialRotation.z)
            : Quaternion.Euler(step.initialRotation);

        foreach (var id in ids)
        {
            if (players.TryGetValue(id, out var rig))
            {
                ClosedLoop cl = rig.GetComponent<ClosedLoop>();
                if (cl != null)
                {
                    Debug.Log($"[ResetVRs] Calling SetPositionAndRotation on '{id}' with pos={step.initialPosition}, rot={initialRotation.eulerAngles}");
                    cl.SetPositionAndRotation(step.initialPosition, initialRotation);
                    cl.ResetPositionAndRotation();
                }
                else
                {
                    Debug.LogWarning($"[ResetVRs] No ClosedLoop component on {id}");
                }
            }
            else
            {
                Debug.LogWarning($"[ResetVRs] No rig found for id {id}");
            }
        }
    }



    private IEnumerator WaitForArea(Trigger t)
    {
        bool done = false;
        void Handler(Collider c)
        {
            if (t.vrId == "any" || c.transform.root.name == t.vrId)
                done = true;
        }

        var go = new GameObject($"Trigger_{currentStep}");
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(2, 2, 2); // temp – in practice read from design
        go.tag = t.areaTag;
        go.transform.parent = transform;
        go.AddComponent<TriggerRelay>().Init(Handler);

        while (!done)
            yield return null;
    }

    // helpers
    private static Vector3 ToVector3(float[] arr) =>
        arr != null && arr.Length >= 3 ? new Vector3(arr[0], arr[1], arr[2]) : Vector3.zero;

    private static Color ToColor(float[] arr) =>
        arr != null && arr.Length >= 3
            ? new Color(arr[0], arr[1], arr[2], arr.Length > 3 ? arr[3] : 1)
            : Color.black;

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
    private System.Action<Collider> onEnter;

    public void Init(System.Action<Collider> enter) => onEnter = enter;

    private void OnTriggerEnter(Collider other) => onEnter?.Invoke(other);
}
