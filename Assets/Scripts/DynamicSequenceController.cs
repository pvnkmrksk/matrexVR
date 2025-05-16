using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using InSceneSequence;

public class DynamicSequenceController : MonoBehaviour, IInSceneSequencer
{
    [System.Serializable]
    private class DesignFile
    {
        public int    seed       = -1;
        public int    repetitions= 1;
        public bool   sync       = true;
        public Step[] steps;
    }
    [System.Serializable]
    private class Step
    {
        public string                name       = "";
        public Trigger               trigger;
        public SceneObjectSpec[]     objects;
        public CameraSpec[]          camera;
        public string[]              resetVR;
    }
    [System.Serializable] public class Trigger
    {
        public string type;          // "time" | "area"
        public float  seconds;       // if time
        public string areaTag;       // if area
        public string vrId;          // "any" or "VR1", …
    }
    [System.Serializable] public class SceneObjectSpec
    {
        public string prefab;
        public float[] pos;
        public float[] rot;
        public string  mat;
    }
    [System.Serializable] public class CameraSpec
    {
        public string vrId;
        public CameraClearFlags clearFlags = CameraClearFlags.SolidColor;
        public float[] bgColor; // r,g,b,a 0-1
    }

    private List<Step> orderedSteps;
    private int currentStep = -1;
    private Dictionary<string, Transform> players = new();

    // ───────────────────────────── ISceneController ───────────────────────────
    public void InitializeScene(Dictionary<string, object> parameters)
    {
        string designFile = parameters != null && parameters.TryGetValue("design", out var p)
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
        var design  = JsonConvert.DeserializeObject<DesignFile>(File.ReadAllText(path));

        // shuffle etc. (reuse SequenceConfigGenerator logic)
        orderedSteps = BuildOrdered(design);
    }

    private List<Step> BuildOrdered(DesignFile design)
    {
        var rng   = new System.Random(design.seed < 0 ? System.Environment.TickCount
                                                      : design.seed);
        var list  = new List<Step>();

        for (int r=0; r < design.repetitions; r++)
        {
            var shuffled = new List<Step>(design.steps);
            // Fischer-Yates shuffle
            for (int n=shuffled.Count; n>1; --n)
            {
                int k   = rng.Next(n);
                var tmp = shuffled[k];
                shuffled[k]      = shuffled[n-1];
                shuffled[n-1]    = tmp;
            }
            list.AddRange(shuffled);
        }
        return list;
    }

    private void CachePlayers()
    {
        foreach (var dl in FindObjectsOfType<DataLogger>())
            players[dl.name] = dl.transform;           // assumes each VR root is named “VR1” …
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

        // 2) instantiate new
        SpawnObjects(step.objects);

        // 3) camera tweaks
        ApplyCameraSettings(step.camera);

        // 4) reset VRs
        ResetVRs(step.resetVR);

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

        foreach (var s in specs)
        {
            var prefab = Resources.Load<GameObject>(s.prefab);
            if (!prefab) { Debug.LogWarning($"Prefab {s.prefab} missing"); continue; }

            var go = Instantiate(prefab, transform);
            go.transform.localPosition = ToVector3(s.pos);
            go.transform.localEulerAngles = ToVector3(s.rot);

            if (!string.IsNullOrEmpty(s.mat))
            {
                var mat = Resources.Load<Material>(s.mat);
                if (mat && go.TryGetComponent<Renderer>(out var r))
                    r.material = mat;
            }
        }
    }

    private void ApplyCameraSettings(CameraSpec[] specs)
    {
        if (specs == null) return;
        foreach (var spec in specs)
        {
            if (!players.TryGetValue(spec.vrId, out var rig)) continue;
            var cam = rig.GetComponentInChildren<Camera>(true);
            if (!cam) continue;

            cam.clearFlags = spec.clearFlags;
            if (spec.bgColor != null && spec.bgColor.Length >= 3)
                cam.backgroundColor = ToColor(spec.bgColor);
        }
    }

    private void ResetVRs(string[] ids)
    {
        if (ids == null) return;
        foreach (var id in ids)
            if (players.TryGetValue(id, out var rig))
                rig.localPosition = Vector3.zero; // or whatever “origin” rule you prefer
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
        var box= go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size      = new Vector3(2,2,2); // temp – in practice read from design
        go.tag        = t.areaTag;
        go.transform.parent = transform;
        go.AddComponent<TriggerRelay>().Init(Handler);

        while (!done) yield return null;
    }

    // helpers
    private static Vector3 ToVector3(float[] arr) =>
        arr != null && arr.Length >= 3 ? new Vector3(arr[0],arr[1],arr[2]) : Vector3.zero;
    private static Color   ToColor  (float[] arr) =>
        arr != null && arr.Length >= 3 ?
            new Color(arr[0], arr[1], arr[2], arr.Length>3?arr[3]:1) : Color.black;
}

public class TriggerRelay : MonoBehaviour
{
    private System.Action<Collider> onEnter;
    public void Init(System.Action<Collider> enter) => onEnter = enter;
    private void OnTriggerEnter(Collider other) => onEnter?.Invoke(other);
}
