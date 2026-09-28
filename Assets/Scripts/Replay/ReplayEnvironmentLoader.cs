using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Loads a sequence design JSON and spawns the objects for a given step name.
/// Attach this to an empty GameObject in the replay scene and wire prefabs/materials.
/// </summary>
public class ReplayEnvironmentLoader : MonoBehaviour
{
    [Header("Design source")]
    [Tooltip("Design file name (e.g., sequenceDesign_choice_desync_boundary.json). Resolved relative to StreamingAssets.")]
    public string designFileName;

    [Header("Assets")]
    [Tooltip("Prefabs referenced by the design (names must match the 'type' field).")]
    public GameObject[] prefabs;

    [Tooltip("Materials referenced by the design (optional).")]
    public Material[] materials;

    [Header("Runtime")]
    [Tooltip("Parent for spawned environment objects.")]
    public Transform environmentRoot;

    private readonly List<string> rigIds = new();

    private readonly Dictionary<string, GameObject> prefabDict = new();
    private readonly Dictionary<string, Material> materialDict = new();
    private readonly Dictionary<string, Step> stepsByName = new();
    private Step intertrialStep;
    private string currentStepName;

    [Serializable]
    private class DesignFile
    {
        public Step intertrial;
        public Step[] steps;
    }

    [Serializable]
    private class Step
    {
        public string name;
        public SceneObjectSpec[] objects;
        public CameraSpec[] camera;
        public bool closedLoopOrientation;
        public bool closedLoopPosition;
    }

    [Serializable]
    private class SceneObjectSpec
    {
        public string type;
        public Polar polar;
        public Scale scale;
        public string material;
        public float[] color;

        // legacy fields
        public string prefab;
        public float[] pos;
        public float mu;
        public bool flip;
    }

    [Serializable]
    private class Polar
    {
        public float radius, angle, height;
    }

    [Serializable]
    private class Scale
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

    private void Awake()
    {
        foreach (var p in prefabs)
            prefabDict[p.name] = p;
        foreach (var m in materials)
            materialDict[m.name] = m;

        if (!environmentRoot)
        {
            var go = new GameObject("ReplayEnvironment");
            environmentRoot = go.transform;
        }
    }

    public void SetRigIds(IEnumerable<string> ids)
    {
        rigIds.Clear();
        if (ids != null)
            rigIds.AddRange(ids);
    }

    public void LoadDesign(string fileName = null)
    {
        if (!string.IsNullOrEmpty(fileName))
            designFileName = fileName;

        if (string.IsNullOrEmpty(designFileName))
        {
            Debug.LogWarning("[ReplayEnv] No design file name provided.");
            return;
        }

        string path = Path.Combine(Application.streamingAssetsPath, designFileName);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[ReplayEnv] Design file not found at {path}");
            return;
        }

        string json = File.ReadAllText(path);
        var design = JsonConvert.DeserializeObject<DesignFile>(json);
        stepsByName.Clear();
        if (design?.steps != null)
        {
            foreach (var s in design.steps)
            {
                if (!string.IsNullOrEmpty(s.name))
                    stepsByName[s.name] = s;
            }
        }
        intertrialStep = design?.intertrial;
        Debug.Log($"[ReplayEnv] Loaded design {designFileName} with {stepsByName.Count} steps");
    }

    public void ApplyStep(string stepName)
    {
        if (string.IsNullOrEmpty(stepName) || stepName == currentStepName)
            return;

        Step step = null;
        if (!stepsByName.TryGetValue(stepName, out step))
        {
            // allow intertrial match if names differ
            if (intertrialStep != null && intertrialStep.name == stepName)
                step = intertrialStep;
        }

        if (step == null)
        {
            Debug.LogWarning($"[ReplayEnv] Step '{stepName}' not found in design {designFileName}");
            return;
        }

        currentStepName = stepName;
        ClearEnvironment();
        SpawnObjects(step);
    }

    public CameraSpec[] GetCameraSpecs(string stepName)
    {
        if (stepsByName.TryGetValue(stepName, out var s))
            return s.camera;
        if (intertrialStep != null && intertrialStep.name == stepName)
            return intertrialStep.camera;
        return null;
    }

    private void ClearEnvironment()
    {
        if (!environmentRoot) return;
        for (int i = environmentRoot.childCount - 1; i >= 0; --i)
            Destroy(environmentRoot.GetChild(i).gameObject);
    }

    private void SpawnObjects(Step step)
    {
        if (step.objects == null)
            return;

        var targets = rigIds.Count > 0 ? rigIds : new List<string> { "VR1" };

        foreach (var rigId in targets)
        {
            Transform rigRoot = new GameObject($"Env_{rigId}").transform;
            rigRoot.parent = environmentRoot;

            foreach (var obj in step.objects)
            {
                string prefabName = string.IsNullOrEmpty(obj.type) ? obj.prefab : obj.type;
                if (!prefabDict.TryGetValue(prefabName, out var prefab))
                {
                    Debug.LogWarning($"[ReplayEnv] Prefab '{prefabName}' missing for step '{step.name}'");
                    continue;
                }

                Vector3 pos = obj.polar != null ? PolarToXZ(obj.polar) : ToVector3(obj.pos);
                Quaternion rot = Quaternion.Euler(0, obj.mu, 0);
                GameObject go = Instantiate(prefab, pos, rot, rigRoot);
                Vector3 scale = obj.scale != null ? new Vector3(obj.scale.x, obj.scale.y, obj.scale.z) : go.transform.localScale;
                if (obj.flip) scale.x *= -1;
                go.transform.localScale = scale;

                int layer = LayerMask.NameToLayer(GetLayerNameForRig(rigId));
                if (layer >= 0)
                    SetLayerRecursively(go, layer);
                go.tag = GetLayerNameForRig(rigId);

                if (go.TryGetComponent<Renderer>(out var rend))
                {
                    string matName = obj.material;
                    if (!string.IsNullOrEmpty(matName) && materialDict.TryGetValue(matName, out var mat))
                        rend.material = new Material(mat);

                    if (obj.color != null && obj.color.Length >= 3 && rend.material != null)
                        rend.material.color = ToColor(obj.color);
                }
            }
        }
    }

    private void ApplyCamera(CameraSpec[] specs)
    {
        if (specs == null)
            return;

        foreach (var spec in specs)
        {
            Camera[] cams = string.IsNullOrEmpty(spec.vrId)
                ? Camera.allCameras
                : Array.FindAll(Camera.allCameras, c => c.transform.root.name == spec.vrId || c.name == spec.vrId);

            foreach (var cam in cams)
            {
                cam.clearFlags = spec.clearFlags;
                if (spec.bgColor != null && spec.bgColor.Length >= 3)
                    cam.backgroundColor = ToColor(spec.bgColor);
            }
        }
    }

    private static Vector3 PolarToXZ(Polar p)
    {
        float rad = p.angle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad) * p.radius, p.height, Mathf.Cos(rad) * p.radius);
    }

    private static Vector3 ToVector3(float[] arr) =>
        arr != null && arr.Length >= 3 ? new Vector3(arr[0], arr[1], arr[2]) : Vector3.zero;

    private static Color ToColor(float[] arr) =>
        arr != null && arr.Length >= 3
            ? new Color(arr[0], arr[1], arr[2], arr.Length > 3 ? arr[3] : 1)
            : Color.white;

    private static string GetLayerNameForRig(string rigId)
    {
        // "VR1" -> "ChoiceVR1" if possible, else rigId
        if (rigId.StartsWith("VR", StringComparison.OrdinalIgnoreCase) && rigId.Length >= 3)
            return $"Choice{rigId}";
        return rigId;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
