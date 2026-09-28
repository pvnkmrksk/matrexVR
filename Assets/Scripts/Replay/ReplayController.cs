using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using UnityEngine.UI;
using UnityEngine;

/// <summary>
/// Minimal offline replay controller that reads existing DataLogger CSVs
/// from RunData/<timestamp>/ and replays rig positions in the scene.
/// </summary>
public class ReplayController : MonoBehaviour
{
    [Tooltip("Name of the session folder under Assets/RunData (e.g., 20250101_123000). Leave empty to auto-pick newest.")]
    public string sessionFolder;

    [Tooltip("Optional absolute path override; leave empty to use Assets/RunData/<sessionFolder>.")]
    public string absoluteSessionPath;

    [Tooltip("Prefab used to visualize a rig (e.g., a sphere/arrow).")]
    public GameObject rigMarkerPrefab;

    [Tooltip("Create a camera per rig and split the screen into viewports.")]
    public bool createRigCameras = true;

    [Tooltip("Desired horizontal FOV in degrees for replay cameras.")]
    public float targetHorizontalFov = 110f;

    [Tooltip("Seconds of replay per real-time second.")]
    public float playbackSpeed = 1f;

    [Tooltip("Loop playback when reaching the end of the log.")]
    public bool loop = true;

    [Header("Scrubbing / Controls")]
    [Tooltip("UI slider that maps 0..1 to 0..maxTime; hook its OnValueChanged to SetScrubNormalized.")]
    public Slider scrubSlider;
    public KeyCode togglePlayKey = KeyCode.Space;
    public KeyCode backKey = KeyCode.LeftArrow;
    public KeyCode forwardKey = KeyCode.RightArrow;
    public KeyCode prevStepKey = KeyCode.N;
    public KeyCode nextStepKey = KeyCode.M;
    public float smallStepSeconds = 1f;
    public float largeStepSeconds = 10f;
    public bool pauseOnSeek = true;
    [Tooltip("If true, after a seek/scrub, playback resumes automatically if it was playing before.")]
    public bool autoResumeAfterSeek = true;

    [Header("Step markers (optional)")]
    [Tooltip("Parent RectTransform to place step markers under; if empty, uses the slider transform.")]
    public RectTransform stepMarkerContainer;
    [Tooltip("Prefab (UI Image/RectTransform) for a step marker on the scrub bar.")]
    public GameObject stepMarkerPrefab;

    [Header("Environment (optional)")]
    [Tooltip("Optional environment loader; will swap environment when stepName changes.")]
    public ReplayEnvironmentLoader environmentLoader;

    private readonly Dictionary<string, List<Sample>> samplesByRig = new();
    private readonly Dictionary<string, int> cursorByRig = new();
    private readonly Dictionary<string, GameObject> markerByRig = new();
    private float replayTime;
    private float maxTime;
    private bool ready;
    private string designFileName;
    private string currentEnvStep;
    private readonly Dictionary<string, Camera> camerasByRig = new();
    private readonly List<string> rigOrder = new();
    private bool isPlaying = true;
    private bool sliderDragging;
    private readonly List<StepChange> stepChanges = new();
    private readonly List<GameObject> stepMarkerInstances = new();
    private bool resumePending;

    private class Sample
    {
        public float t;
        public Vector3 pos;
        public Vector3 rot;
        public string stepName;
        public int stepIndex;
    }

    private void Start()
    {
        string path = ResolveSessionPath();
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            Debug.LogError($"[Replay] Session path not found: {path}");
            return;
        }

        designFileName = ResolveDesignFile(path);
        if (environmentLoader && !string.IsNullOrEmpty(designFileName))
            environmentLoader.LoadDesign(designFileName);

        LoadCsvs(path);
        SpawnMarkers();
        if (environmentLoader)
            environmentLoader.SetRigIds(rigOrder);

        if (createRigCameras)
            CreateRigCameras();

        BuildStepTimeline();
        RenderStepMarkers();

        ready = samplesByRig.Count > 0;
        if (!ready)
            Debug.LogWarning("[Replay] No samples loaded; nothing to replay.");
    }

    private void Update()
    {
        if (!ready || maxTime <= 0f)
            return;

        HandleInput();

        if (isPlaying)
        {
            replayTime += Time.deltaTime * playbackSpeed;
            if (replayTime > maxTime)
            {
                if (loop)
                    replayTime %= maxTime;
                else
                {
                    replayTime = maxTime;
                    isPlaying = false;
                }
            }
        }

        foreach (var kvp in samplesByRig)
        {
            string rig = kvp.Key;
            List<Sample> list = kvp.Value;
            if (list.Count == 0 || !markerByRig.TryGetValue(rig, out var marker))
                continue;

            // advance cursor if needed
            int cursor = cursorByRig[rig];
            // move forward
            while (cursor < list.Count - 1 && list[cursor + 1].t <= replayTime)
                cursor++;
            // move backward
            while (cursor > 0 && list[cursor].t > replayTime)
                cursor--;
            cursorByRig[rig] = cursor;

            Sample a = list[cursor];
            Sample b = cursor < list.Count - 1 ? list[cursor + 1] : a;
            float span = Mathf.Max(0.0001f, b.t - a.t);
            float lerp = Mathf.Clamp01((replayTime - a.t) / span);
            Vector3 pos = Vector3.Lerp(a.pos, b.pos, lerp);
            Vector3 rot = Vector3.Lerp(a.rot, b.rot, lerp);

            marker.transform.position = pos;
            marker.transform.rotation = Quaternion.Euler(rot);

            // Track step changes using the first rig as reference
            if (environmentLoader != null && rig == GetReferenceRig() && !string.IsNullOrEmpty(a.stepName))
            {
                if (a.stepName != currentEnvStep)
                {
                    currentEnvStep = a.stepName;
                    environmentLoader.ApplyStep(currentEnvStep);

                    // update camera bg for this step
                    var specs = environmentLoader.GetCameraSpecs(currentEnvStep);
                    ApplyCameraSpecs(specs);
                }
            }
        }

        if (scrubSlider && !sliderDragging && maxTime > 0f)
            scrubSlider.value = Mathf.Clamp01(replayTime / maxTime);

        if (resumePending && !sliderDragging)
        {
            isPlaying = true;
            resumePending = false;
        }
    }

    private string ResolveSessionPath()
    {
        if (!string.IsNullOrEmpty(absoluteSessionPath))
            return absoluteSessionPath;

        string runDataRoot = Path.Combine(Application.dataPath, "RunData");
        if (string.IsNullOrEmpty(sessionFolder))
        {
            if (!Directory.Exists(runDataRoot))
                return null;

            string[] dirs = Directory.GetDirectories(runDataRoot);
            if (dirs.Length == 0)
                return null;

            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            return dirs[^1];
        }

        return Path.Combine(runDataRoot, sessionFolder);
    }

    private void LoadCsvs(string path)
    {
        string[] files = Directory.GetFiles(path, "*.csv", SearchOption.TopDirectoryOnly);
        foreach (string file in files)
        {
            try
            {
                LoadCsv(file);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Replay] Failed to parse {file}: {ex.Message}");
            }
        }

        // compute max time
        foreach (var list in samplesByRig.Values)
        {
            if (list.Count > 0)
                maxTime = Mathf.Max(maxTime, list[^1].t);
        }
    }

    private void LoadCsv(string path)
    {
        string[] lines = File.ReadAllLines(path);
        if (lines.Length < 2)
            return;

        string[] headers = lines[0].Split(',');
        int idxTime = Array.IndexOf(headers, "Current Time");
        int idxVr = Array.IndexOf(headers, "VR");
        int idxPosX = Array.IndexOf(headers, "GameObjectPosX");
        int idxPosY = Array.IndexOf(headers, "GameObjectPosY");
        int idxPosZ = Array.IndexOf(headers, "GameObjectPosZ");
        int idxRotX = Array.IndexOf(headers, "GameObjectRotX");
        int idxRotY = Array.IndexOf(headers, "GameObjectRotY");
        int idxRotZ = Array.IndexOf(headers, "GameObjectRotZ");
        int idxStepName = Array.IndexOf(headers, "stepName");
        int idxStepIndex = Array.IndexOf(headers, "stepIndex");

        if (idxTime < 0 || idxVr < 0 || idxPosX < 0 || idxRotY < 0)
            return;

        DateTime? first = null;
        foreach (string line in lines[1..])
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] parts = line.Split(',');
            if (parts.Length <= Mathf.Max(idxRotZ, idxStepIndex, idxStepName))
                continue;

            if (!DateTime.TryParse(parts[idxTime], CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var ts))
                continue;

            if (first == null)
                first = ts;

            string rig = parts[idxVr];
            float t = (float)(ts - first.Value).TotalSeconds;
            float x = ParseFloat(parts[idxPosX]);
            float y = ParseFloat(parts[idxPosY]);
            float z = ParseFloat(parts[idxPosZ]);
            float rx = ParseFloat(parts[idxRotX]);
            float ry = ParseFloat(parts[idxRotY]);
            float rz = ParseFloat(parts[idxRotZ]);

            var sample = new Sample
            {
                t = t,
                pos = new Vector3(x, y, z),
                rot = new Vector3(rx, ry, rz),
                stepName = idxStepName >= 0 ? parts[idxStepName] : "",
                stepIndex = idxStepIndex >= 0 ? ParseInt(parts[idxStepIndex]) : -1
            };

            if (!samplesByRig.TryGetValue(rig, out var list))
            {
                list = new List<Sample>();
                samplesByRig[rig] = list;
                cursorByRig[rig] = 0;
            }
            list.Add(sample);
        }
    }

    private void SpawnMarkers()
    {
        foreach (var kvp in samplesByRig)
        {
            string rig = kvp.Key;
            if (rigMarkerPrefab == null)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.transform.localScale = Vector3.one * 0.25f;
                marker.name = $"Replay_{rig}";
                markerByRig[rig] = marker;
                continue;
            }

            GameObject go = Instantiate(rigMarkerPrefab);
            go.name = $"Replay_{rig}";
            markerByRig[rig] = go;

            rigOrder.Add(rig);
        }
    }

    private static float ParseFloat(string s)
    {
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v);
        return v;
    }

    private static int ParseInt(string s)
    {
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v);
        return v;
    }

    private string GetReferenceRig()
    {
        foreach (var kvp in samplesByRig)
            return kvp.Key; // first
        return null;
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(togglePlayKey))
            isPlaying = !isPlaying;

        if (Input.GetKeyDown(backKey))
            Seek(-smallStepSeconds);
        if (Input.GetKeyDown(forwardKey))
            Seek(smallStepSeconds);

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            if (Input.GetKeyDown(backKey))
                Seek(-largeStepSeconds);
            if (Input.GetKeyDown(forwardKey))
                Seek(largeStepSeconds);
        }

        if (Input.GetKeyDown(prevStepKey))
            JumpToStep(-1);
        if (Input.GetKeyDown(nextStepKey))
            JumpToStep(1);
    }

    private void Seek(float deltaSeconds)
    {
        bool wasPlaying = isPlaying;
        replayTime = Mathf.Clamp(replayTime + deltaSeconds, 0f, maxTime > 0 ? maxTime : replayTime);
        if (pauseOnSeek)
        {
            isPlaying = false;
            if (autoResumeAfterSeek && wasPlaying)
                resumePending = true;
        }
        else
        {
            isPlaying = wasPlaying;
        }
    }

    public void SetScrubNormalized(float normalized)
    {
        if (maxTime <= 0f)
            return;
        bool wasPlaying = isPlaying;
        sliderDragging = true;
        replayTime = Mathf.Clamp01(normalized) * maxTime;
        sliderDragging = false;
        if (pauseOnSeek)
        {
            isPlaying = false;
            if (autoResumeAfterSeek && wasPlaying)
                resumePending = true;
        }
        else
        {
            isPlaying = wasPlaying;
        }
    }

    private void JumpToStep(int direction)
    {
        if (stepChanges.Count == 0)
            return;

        // find current step index
        int currentIdx = 0;
        for (int i = 0; i < stepChanges.Count; i++)
        {
            if (stepChanges[i].t <= replayTime)
                currentIdx = i;
            else
                break;
        }

        int targetIdx = Mathf.Clamp(currentIdx + direction, 0, stepChanges.Count - 1);
        replayTime = stepChanges[targetIdx].t;
        if (pauseOnSeek)
        {
            bool wasPlaying = isPlaying;
            isPlaying = false;
            if (autoResumeAfterSeek && wasPlaying)
                resumePending = true;
        }
    }

    private void BuildStepTimeline()
    {
        stepChanges.Clear();
        string refRig = GetReferenceRig();
        if (string.IsNullOrEmpty(refRig) || !samplesByRig.TryGetValue(refRig, out var list) || list.Count == 0)
            return;

        string lastName = list[0].stepName;
        int lastIdx = list[0].stepIndex;
        stepChanges.Add(new StepChange { t = list[0].t, name = lastName, index = lastIdx });
        for (int i = 1; i < list.Count; i++)
        {
            if (list[i].stepName != lastName || list[i].stepIndex != lastIdx)
            {
                lastName = list[i].stepName;
                lastIdx = list[i].stepIndex;
                stepChanges.Add(new StepChange { t = list[i].t, name = lastName, index = lastIdx });
            }
        }
    }

    private void RenderStepMarkers()
    {
        foreach (var go in stepMarkerInstances)
            Destroy(go);
        stepMarkerInstances.Clear();

        if (!scrubSlider || stepMarkerPrefab == null || maxTime <= 0f || stepChanges.Count == 0)
            return;

        RectTransform container = stepMarkerContainer ? stepMarkerContainer : scrubSlider.transform as RectTransform;
        if (container == null)
            return;

        foreach (var sc in stepChanges)
        {
            float norm = Mathf.Clamp01(sc.t / maxTime);
            GameObject marker = Instantiate(stepMarkerPrefab, container);
            if (marker.transform is RectTransform rt)
            {
                rt.anchorMin = new Vector2(norm, 0);
                rt.anchorMax = new Vector2(norm, 1);
                rt.anchoredPosition = Vector2.zero;
            }
            stepMarkerInstances.Add(marker);
        }
    }

    private void ApplyCameraSpecs(ReplayEnvironmentLoader.CameraSpec[] specs)
    {
        if (specs == null)
            return;

        foreach (var spec in specs)
        {
            if (string.IsNullOrEmpty(spec.vrId))
                continue;
            if (!camerasByRig.TryGetValue(spec.vrId, out var cam))
                continue;

            cam.clearFlags = spec.clearFlags;
            if (spec.bgColor != null && spec.bgColor.Length >= 3)
            {
                cam.backgroundColor = new Color(spec.bgColor[0], spec.bgColor[1], spec.bgColor[2],
                    spec.bgColor.Length > 3 ? spec.bgColor[3] : 1f);
            }
        }
    }

    private void CreateRigCameras()
    {
        int count = rigOrder.Count;
        for (int i = 0; i < count; i++)
        {
            string rig = rigOrder[i];
            if (!markerByRig.TryGetValue(rig, out var marker))
                continue;

            GameObject camGo = new GameObject($"{rig}_Camera");
            camGo.transform.SetParent(marker.transform, false);
            var cam = camGo.AddComponent<Camera>();

            // split viewport into grid
            Rect rect = new Rect(0, 0, 1, 1);
            if (count == 2)
                rect = new Rect(0, i * 0.5f, 1, 0.5f);
            else if (count == 3 || count == 4)
                rect = new Rect((i % 2) * 0.5f, (i / 2) * 0.5f, 0.5f, 0.5f);
            cam.rect = rect;

            // set vertical FOV based on desired horizontal FOV and aspect
            float aspect = cam.aspect > 0 ? cam.aspect : 1f;
            float vFov = 2f * Mathf.Atan(Mathf.Tan(targetHorizontalFov * Mathf.Deg2Rad * 0.5f) / aspect) * Mathf.Rad2Deg;
            cam.fieldOfView = vFov;

            // culling mask for layer
            int layer = LayerMask.NameToLayer(GetLayerNameForRig(rig));
            if (layer >= 0)
                cam.cullingMask = 1 << layer;

            camerasByRig[rig] = cam;
        }
    }

    private static string GetLayerNameForRig(string rigId)
    {
        if (rigId.StartsWith("VR", StringComparison.OrdinalIgnoreCase) && rigId.Length >= 3)
            return $"Choice{rigId}";
        return rigId;
    }

    private string ResolveDesignFile(string sessionPath)
    {
        try
        {
            string[] cfgs = Directory.GetFiles(sessionPath, "*_sequenceConfig.json");
            if (cfgs.Length == 0)
                return null;

            string json = File.ReadAllText(cfgs[0]);
            var config = JsonConvert.DeserializeObject<SequenceConfigCopy>(json);
            if (config?.sequences != null && config.sequences.Length > 0)
            {
                var seq = config.sequences[0];
                if (seq.parameters != null && seq.parameters.TryGetValue("design", out var d))
                    return d.ToString();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Replay] Failed to read sequenceConfig: {ex.Message}");
        }
        return null;
    }

    [Serializable]
    private class SequenceConfigCopy
    {
        public SequenceItem[] sequences;
    }

    [Serializable]
    private class SequenceItem
    {
        public string sceneName;
        public Dictionary<string, object> parameters;
    }

    private class StepChange
    {
        public float t;
        public string name;
        public int index;
    }
}
