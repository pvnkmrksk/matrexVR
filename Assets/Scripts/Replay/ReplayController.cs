using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Offline experiment replay from RunData CSVs with transport controls and optional frame export.
/// </summary>
public class ReplayController : MonoBehaviour
{
    [Header("Session")]
    public string sessionFolder;
    public string absoluteSessionPath;

    [Header("Visualization")]
    public GameObject rigMarkerPrefab;
    public bool createRigCameras = true;
    public float targetHorizontalFov = 110f;

    [Header("Playback")]
    public float playbackSpeed = 1f;
    public float minSpeed = 0.05f;
    public float maxSpeed = 16f;
    public bool loop = true;
    public bool startPaused = false;

    [Header("UI")]
    public Slider scrubSlider;
    public ReplayScrubSlider scrubHandler;
    public Text statusText;

    [Header("Environment")]
    public ReplayEnvironmentLoader environmentLoader;

    [Header("Frame export")]
    public ReplayFrameRecorder frameRecorder;
    public bool exportFramesOnPlay = true;

    public bool IsPlayingBack => _ready;
    public bool IsPlaying => _isPlaying;
    public float CurrentTime => _replayTime;
    public float Duration => _session?.MaxTime ?? 0f;

    private ReplaySessionData _session;
    private readonly Dictionary<string, GameObject> _markerByRig = new();
    private readonly Dictionary<string, Camera> _camerasByRig = new();
    private readonly List<string> _rigOrder = new();
    private float _replayTime;
    private bool _isPlaying;
    private bool _ready;
    private string _currentEnvStep;
    private int _referenceFrameIndex;

    private void Start()
    {
        string path = ResolveSessionPath();
        _session = new ReplaySessionData();
        if (!_session.Load(path))
        {
            Debug.LogError($"[Replay] Failed to load session: {path}");
            return;
        }

        if (environmentLoader && !string.IsNullOrEmpty(_session.DesignFileName))
            environmentLoader.LoadDesign(_session.DesignFileName);

        SpawnMarkers();
        if (environmentLoader)
            environmentLoader.SetRigIds(_rigOrder);

        if (createRigCameras)
            CreateRigCameras();

        if (scrubHandler != null)
            scrubHandler.replay = this;
        else if (scrubSlider != null)
        {
            var handler = scrubSlider.gameObject.GetComponent<ReplayScrubSlider>();
            if (handler == null)
                handler = scrubSlider.gameObject.AddComponent<ReplayScrubSlider>();
            handler.replay = this;
            scrubHandler = handler;
        }

        if (frameRecorder == null)
            frameRecorder = GetComponent<ReplayFrameRecorder>();
        if (frameRecorder == null)
            frameRecorder = gameObject.AddComponent<ReplayFrameRecorder>();

        frameRecorder.Initialize(this, path, _camerasByRig);
        frameRecorder.recordOnPlay = exportFramesOnPlay;

        _replayTime = 0f;
        _isPlaying = !startPaused;
        _ready = true;
        ApplyReplayTime(_replayTime);
        UpdateUi();
        Debug.Log($"[Replay] Loaded {_rigOrder.Count} rigs, duration {_session.MaxTime:F1}s");
    }

    private void Update()
    {
        if (!_ready)
            return;

        HandleInput();

        if (_isPlaying && _session.MaxTime > 0f)
        {
            _replayTime += Time.deltaTime * playbackSpeed;
            if (_replayTime > _session.MaxTime)
            {
                if (loop)
                    _replayTime = 0f;
                else
                {
                    _replayTime = _session.MaxTime;
                    _isPlaying = false;
                    frameRecorder?.EndAll();
                }
            }
            ApplyReplayTime(_replayTime);
        }

        UpdateUi();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            TogglePlay();

        if (Input.GetKeyDown(KeyCode.Home))
            SeekTo(0f);
        if (Input.GetKeyDown(KeyCode.End))
            SeekTo(_session.MaxTime);

        if (Input.GetKeyDown(KeyCode.LeftArrow))
            SeekBy(-1f);
        if (Input.GetKeyDown(KeyCode.RightArrow))
            SeekBy(1f);

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightControl))
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
                SeekBy(-10f);
            if (Input.GetKeyDown(KeyCode.RightArrow))
                SeekBy(10f);
        }

        // Hold , / . for smooth rewind / fast-forward
        if (Input.GetKey(KeyCode.Comma))
            SeekBy(-Time.deltaTime * 3f * Mathf.Max(playbackSpeed, 0.25f));
        if (Input.GetKey(KeyCode.Period))
            SeekBy(Time.deltaTime * 3f * Mathf.Max(playbackSpeed, 0.25f));

        if (Input.GetKeyDown(KeyCode.LeftBracket))
            SetPlaybackSpeed(playbackSpeed * 0.5f);
        if (Input.GetKeyDown(KeyCode.RightBracket))
            SetPlaybackSpeed(playbackSpeed * 2f);

        if (Input.GetKeyDown(KeyCode.N))
            JumpStep(-1);
        if (Input.GetKeyDown(KeyCode.M))
            JumpStep(1);

        if (Input.GetKeyDown(KeyCode.J))
            StepFrame(-1);
        if (Input.GetKeyDown(KeyCode.K))
            StepFrame(1);

        if (Input.GetKeyDown(KeyCode.Alpha1))
            SetPlaybackSpeed(0.25f);
        if (Input.GetKeyDown(KeyCode.Alpha2))
            SetPlaybackSpeed(0.5f);
        if (Input.GetKeyDown(KeyCode.Alpha3))
            SetPlaybackSpeed(1f);
        if (Input.GetKeyDown(KeyCode.Alpha4))
            SetPlaybackSpeed(2f);
        if (Input.GetKeyDown(KeyCode.Alpha5))
            SetPlaybackSpeed(4f);

        if (Input.GetKeyDown(KeyCode.R) && frameRecorder != null)
            frameRecorder.BeginAll();
    }

    public void TogglePlay()
    {
        _isPlaying = !_isPlaying;
        if (_isPlaying && exportFramesOnPlay)
            frameRecorder?.BeginAll();
        else if (!_isPlaying)
            frameRecorder?.EndAll();
    }

    public void SetPlaybackSpeed(float speed)
    {
        playbackSpeed = Mathf.Clamp(speed, minSpeed, maxSpeed);
    }

    public void OnScrubDrag(float normalized)
    {
        bool wasPlaying = _isPlaying;
        _isPlaying = false;
        _replayTime = Mathf.Clamp01(normalized) * _session.MaxTime;
        ApplyReplayTime(_replayTime);
        _isPlaying = wasPlaying;
    }

    public void OnScrubReleased(float normalized)
    {
        _replayTime = Mathf.Clamp01(normalized) * _session.MaxTime;
        ApplyReplayTime(_replayTime);
    }

    private void SeekBy(float delta)
    {
        SeekTo(_replayTime + delta);
    }

    private void SeekTo(float time)
    {
        _replayTime = Mathf.Clamp(time, 0f, _session.MaxTime);
        ApplyReplayTime(_replayTime);
    }

    private void JumpStep(int direction)
    {
        if (_session.StepMarkers.Count == 0)
            return;

        int idx = _session.FindStepIndexAtTime(_replayTime);
        idx = Mathf.Clamp(idx + direction, 0, _session.StepMarkers.Count - 1);
        SeekTo(_session.StepMarkers[idx].t);
    }

    private void StepFrame(int direction)
    {
        string rig = _session.GetReferenceRig();
        if (string.IsNullOrEmpty(rig) || !_session.SamplesByRig.TryGetValue(rig, out var list) || list.Count == 0)
            return;

        if (direction < 0)
            _referenceFrameIndex = Mathf.Max(0, _referenceFrameIndex + direction);
        else
            _referenceFrameIndex = Mathf.Min(list.Count - 1, _referenceFrameIndex + direction);

        _replayTime = list[_referenceFrameIndex].t;
        ApplyReplayTime(_replayTime);
    }

    private void ApplyReplayTime(float time)
    {
        foreach (string rig in _rigOrder)
        {
            if (!_markerByRig.TryGetValue(rig, out var marker))
                continue;

            _session.GetPoseAtTime(rig, time, out Vector3 pos, out Quaternion rot);
            marker.transform.SetPositionAndRotation(pos, rot);
        }

        if (environmentLoader != null && _session.StepMarkers.Count > 0)
        {
            int idx = _session.FindStepIndexAtTime(time);
            var marker = _session.StepMarkers[idx];
            if (!string.IsNullOrEmpty(marker.name) && marker.name != _currentEnvStep)
            {
                _currentEnvStep = marker.name;
                environmentLoader.ApplyStep(_currentEnvStep);
                ApplyCameraSpecs(environmentLoader.GetCameraSpecs(_currentEnvStep));
            }
        }

        SyncReferenceFrameIndex(time);
    }

    private void SyncReferenceFrameIndex(float time)
    {
        string rig = _session.GetReferenceRig();
        if (string.IsNullOrEmpty(rig) || !_session.SamplesByRig.TryGetValue(rig, out var list))
            return;

        int lo = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].t <= time)
                lo = i;
            else
                break;
        }
        _referenceFrameIndex = lo;
    }

    private void UpdateUi()
    {
        if (_session.MaxTime > 0f)
        {
            float norm = _replayTime / _session.MaxTime;
            if (scrubHandler != null)
                scrubHandler.SetNormalized(norm, fromPlayback: true);
            else if (scrubSlider != null)
                scrubSlider.SetValueWithoutNotify(norm);
        }

        if (statusText != null)
        {
            int stepIdx = _session.FindStepIndexAtTime(_replayTime);
            string stepLabel = _session.StepMarkers.Count > stepIdx
                ? _session.StepMarkers[stepIdx].name
                : "-";
            statusText.text =
                $"{FormatTime(_replayTime)} / {FormatTime(_session.MaxTime)}  " +
                $"{(_isPlaying ? "PLAY" : "PAUSE")}  {playbackSpeed:0.##}x  step:{stepLabel}";
        }
    }

    private static string FormatTime(float t)
    {
        int m = (int)(t / 60f);
        float s = t % 60f;
        return $"{m:D2}:{s:05.2f}";
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

    private void SpawnMarkers()
    {
        foreach (var kvp in _session.SamplesByRig)
        {
            string rig = kvp.Key;
            GameObject marker;
            if (rigMarkerPrefab == null)
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.transform.localScale = Vector3.one * 0.25f;
            }
            else
            {
                marker = Instantiate(rigMarkerPrefab);
            }
            marker.name = $"Replay_{rig}";
            _markerByRig[rig] = marker;
            _rigOrder.Add(rig);
        }
    }

    private void CreateRigCameras()
    {
        int count = _rigOrder.Count;
        for (int i = 0; i < count; i++)
        {
            string rig = _rigOrder[i];
            if (!_markerByRig.TryGetValue(rig, out var marker))
                continue;

            var camGo = new GameObject($"{rig}_Camera");
            camGo.transform.SetParent(marker.transform, false);
            var cam = camGo.AddComponent<Camera>();

            Rect rect = count switch
            {
                2 => new Rect(0, i * 0.5f, 1, 0.5f),
                3 or 4 => new Rect((i % 2) * 0.5f, (i / 2) * 0.5f, 0.5f, 0.5f),
                _ => new Rect(0, 0, 1, 1)
            };
            cam.rect = rect;

            float aspect = cam.aspect > 0 ? cam.aspect : 1f;
            float vFov = 2f * Mathf.Atan(Mathf.Tan(targetHorizontalFov * Mathf.Deg2Rad * 0.5f) / aspect) * Mathf.Rad2Deg;
            cam.fieldOfView = vFov;

            int layer = LayerMask.NameToLayer(GetLayerNameForRig(rig));
            if (layer >= 0)
                cam.cullingMask = 1 << layer;

            _camerasByRig[rig] = cam;
        }
    }

    private void ApplyCameraSpecs(ReplayEnvironmentLoader.CameraSpec[] specs)
    {
        if (specs == null)
            return;
        foreach (var spec in specs)
        {
            if (string.IsNullOrEmpty(spec.vrId) || !_camerasByRig.TryGetValue(spec.vrId, out var cam))
                continue;
            cam.clearFlags = spec.clearFlags;
            if (spec.bgColor != null && spec.bgColor.Length >= 3)
            {
                cam.backgroundColor = new Color(spec.bgColor[0], spec.bgColor[1], spec.bgColor[2],
                    spec.bgColor.Length > 3 ? spec.bgColor[3] : 1f);
            }
        }
    }

    private static string GetLayerNameForRig(string rigId)
    {
        if (rigId.StartsWith("VR", StringComparison.OrdinalIgnoreCase) && rigId.Length >= 3)
            return $"Choice{rigId}";
        return rigId;
    }
}
