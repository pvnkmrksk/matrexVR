using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Replay transport + session loading. Full-scene mode loads real scenes/configs from RunData
/// and applies logged poses only (no closed loop).
/// </summary>
public class ReplayController : MonoBehaviour
{
    [Header("Session (optional auto-load)")]
    public string sessionFolder;
    public string absoluteSessionPath;

    [Header("Mode")]
    [Tooltip("Load real experiment scenes + prefabs from archived configs (pixel-accurate with same build).")]
    public bool fullSceneReplay = true;

    [Tooltip("Legacy marker spheres when fullSceneReplay is off.")]
    public bool createRigCameras = true;
    public GameObject rigMarkerPrefab;
    public float targetHorizontalFov = 110f;
    public ReplayEnvironmentLoader environmentLoader;

    [Header("Playback")]
    public float playbackSpeed = 1f;
    public float minSpeed = 0.05f;
    public float maxSpeed = 16f;
    public bool loop = true;
    public bool startPaused = true;

    [Header("UI")]
    public Slider scrubSlider;
    public ReplayScrubSlider scrubHandler;
    public Text statusText;
    public ReplaySessionUI sessionUi;

    [Header("Frame export")]
    public ReplayFrameRecorder frameRecorder;
    public bool exportFramesOnPlay = true;
    public int captureFaceWidth = 64;
    public int captureFaceHeight = 64;

    public bool IsPlayingBack => _ready;
    public bool IsPlaying => _isPlaying;
    public float CurrentTime => _replayTime;
    public float Duration => _session?.MaxTime ?? 0f;

    private ReplaySessionArchive _archive;
    private ReplaySessionData _session;
    private ReplaySceneOrchestrator _orchestrator;

    private readonly Dictionary<string, GameObject> _markerByRig = new();
    private readonly Dictionary<string, Camera> _camerasByRig = new();
    private readonly List<string> _rigOrder = new();

    private float _replayTime;
    private bool _isPlaying;
    private bool _ready;
    private int _referenceFrameIndex;

    private void Start()
    {
        _orchestrator = GetComponent<ReplaySceneOrchestrator>();
        if (fullSceneReplay && _orchestrator == null)
            _orchestrator = gameObject.AddComponent<ReplaySceneOrchestrator>();

        if (sessionUi == null)
            sessionUi = FindObjectOfType<ReplaySessionUI>();

        WireScrubSlider();

        if (frameRecorder == null)
            frameRecorder = GetComponent<ReplayFrameRecorder>();
        if (frameRecorder == null)
            frameRecorder = gameObject.AddComponent<ReplayFrameRecorder>();

        frameRecorder.ClearCaptures();

        string autoPath = ResolveSessionPathOptional();
        if (!string.IsNullOrEmpty(autoPath))
            LoadSessionFromPath(autoPath);
        else
            SetStatus("Pick a RunData folder (GUI) or set sessionFolder.");
    }

    /// <summary>Load all CSV + archived configs from a session directory.</summary>
    public bool LoadSessionFromPath(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            Debug.LogError($"[Replay] Invalid session path: {path}");
            return false;
        }

        UnloadSession();

        _archive = new ReplaySessionArchive();
        if (!_archive.Load(path))
            return false;

        _session = _archive.Data;
        absoluteSessionPath = path;
        sessionFolder = Path.GetFileName(path);

        if (fullSceneReplay)
        {
            _orchestrator.LoadSession(_archive);
            RefreshCaptureFromScene();
        }
        else
            LoadLegacyMarkers();

        _replayTime = 0f;
        _isPlaying = !startPaused;
        _ready = true;

        if (_isPlaying && exportFramesOnPlay)
            frameRecorder?.BeginAll();

        ApplyReplayTime(_replayTime);
        UpdateUi();
        Debug.Log($"[Replay] Session ready: {path} ({_session.SamplesByRig.Count} rigs, {_session.MaxTime:F1}s, fullScene={fullSceneReplay})");
        return true;
    }

    public void UnloadSession()
    {
        _ready = false;
        _isPlaying = false;
        frameRecorder?.EndAll();
        _orchestrator?.UnloadSession();
        ReplaySessionContext.Deactivate();

        foreach (var m in _markerByRig.Values)
        {
            if (m != null)
                Destroy(m);
        }
        _markerByRig.Clear();
        _camerasByRig.Clear();
        _rigOrder.Clear();
        _archive = null;
        _session = null;
    }

    public void OnSceneLoadedForCapture() => RefreshCaptureFromScene();

    private void RefreshCaptureFromScene()
    {
        if (frameRecorder == null || _archive == null)
            return;

        frameRecorder.Initialize(this, _archive.SessionPath, null);
        frameRecorder.ClearCaptures();

        foreach (ReplayPoseApplier applier in ReplayPoseApplier.ActiveAppliers)
        {
            if (applier == null)
                continue;

            ViewportSetter vp = applier.GetComponent<ViewportSetter>();
            if (vp == null)
                vp = applier.GetComponentInChildren<ViewportSetter>();
            if (vp == null)
                continue;

            Camera[] faces = VrPanelFrameCapture.CollectFaceCameras(vp);
            if (faces.Length == 0)
                continue;

            var stripGo = new GameObject($"ReplayStripCam_{applier.rigId}");
            stripGo.transform.SetParent(applier.transform, false);
            var cap = stripGo.AddComponent<VrPanelFrameCapture>();
            cap.faceCameras = faces;
            var cfg = ReplayExperimentHost.Instance?.GetSystemConfig(applier.rigId);
            cap.faceWidth = cfg?.ledPanelWidth ?? captureFaceWidth;
            cap.faceHeight = cfg?.ledPanelHeight ?? captureFaceHeight;
            cap.maxInFlight = 2;
            frameRecorder.RegisterCapture(applier.rigId, cap);
        }
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
        if (Input.GetKeyDown(KeyCode.R))
            frameRecorder?.BeginAll();
    }

    public void TogglePlay()
    {
        _isPlaying = !_isPlaying;
        if (_isPlaying && exportFramesOnPlay)
            frameRecorder?.BeginAll();
        else if (!_isPlaying)
            frameRecorder?.EndAll();
    }

    public void SetPlaybackSpeed(float speed) => playbackSpeed = Mathf.Clamp(speed, minSpeed, maxSpeed);

    public void OnScrubDrag(float normalized)
    {
        bool wasPlaying = _isPlaying;
        _isPlaying = false;
        _replayTime = Mathf.Clamp01(normalized) * _session.MaxTime;
        ApplyReplayTime(_replayTime, forceScene: true);
        _isPlaying = wasPlaying;
    }

    public void OnScrubReleased(float normalized)
    {
        _replayTime = Mathf.Clamp01(normalized) * _session.MaxTime;
        ApplyReplayTime(_replayTime, forceScene: true);
    }

    private void SeekBy(float delta) => SeekTo(_replayTime + delta);

    private void SeekTo(float time)
    {
        _replayTime = Mathf.Clamp(time, 0f, _session.MaxTime);
        ApplyReplayTime(_replayTime, forceScene: true);
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
        if (string.IsNullOrEmpty(rig) || !_session.SamplesByRig.TryGetValue(rig, out var list))
            return;
        _referenceFrameIndex = Mathf.Clamp(_referenceFrameIndex + direction, 0, list.Count - 1);
        SeekTo(list[_referenceFrameIndex].t);
    }

    private void ApplyReplayTime(float time, bool forceScene = false)
    {
        if (fullSceneReplay && _orchestrator != null)
            _orchestrator.SetReplayTime(time, forceScene);
        else
            ApplyLegacyMarkers(time);

        SyncReferenceFrameIndex(time);
    }

    private void ApplyLegacyMarkers(float time)
    {
        foreach (string rig in _rigOrder)
        {
            if (!_markerByRig.TryGetValue(rig, out var marker))
                continue;
            _session.GetPoseAtTime(rig, time, out Vector3 pos, out Quaternion rot);
            marker.transform.SetPositionAndRotation(pos, rot);
        }
    }

    private void LoadLegacyMarkers()
    {
        foreach (var kvp in _session.SamplesByRig)
        {
            string rig = kvp.Key;
            GameObject marker = rigMarkerPrefab != null
                ? Instantiate(rigMarkerPrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"Replay_{rig}";
            _markerByRig[rig] = marker;
            _rigOrder.Add(rig);
        }
        if (createRigCameras)
            CreateLegacyCameras();
        frameRecorder?.Initialize(this, _archive.SessionPath, _camerasByRig);
    }

    private void CreateLegacyCameras()
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
            cam.rect = count switch
            {
                2 => new Rect(0, i * 0.5f, 1, 0.5f),
                3 or 4 => new Rect((i % 2) * 0.5f, (i / 2) * 0.5f, 0.5f, 0.5f),
                _ => new Rect(0, 0, 1, 1)
            };
            float aspect = cam.aspect > 0 ? cam.aspect : 1f;
            cam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(targetHorizontalFov * Mathf.Deg2Rad * 0.5f) / aspect) * Mathf.Rad2Deg;
            int layer = LayerMask.NameToLayer(GetLayerNameForRig(rig));
            if (layer >= 0)
                cam.cullingMask = 1 << layer;
            _camerasByRig[rig] = cam;
        }
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

    private void WireScrubSlider()
    {
        if (scrubHandler != null)
            scrubHandler.replay = this;
        else if (scrubSlider != null)
        {
            var handler = scrubSlider.GetComponent<ReplayScrubSlider>();
            if (handler == null)
                handler = scrubSlider.gameObject.AddComponent<ReplayScrubSlider>();
            handler.replay = this;
            scrubHandler = handler;
        }
    }

    private void UpdateUi()
    {
        if (_session == null || _session.MaxTime <= 0f)
            return;

        float norm = _replayTime / _session.MaxTime;
        scrubHandler?.SetNormalized(norm, fromPlayback: true);
        scrubSlider?.SetValueWithoutNotify(norm);

        SetStatus(
            $"{FormatTime(_replayTime)} / {FormatTime(_session.MaxTime)}  " +
            $"{(_isPlaying ? "PLAY" : "PAUSE")}  {playbackSpeed:0.##}x  " +
            $"step:{GetStepLabel()}  [{(_fullSceneReplay ? "SCENE" : "MARKER")}]");
    }

    private string GetStepLabel()
    {
        int idx = _session.FindStepIndexAtTime(_replayTime);
        if (_session.StepMarkers.Count > idx)
            return _session.StepMarkers[idx].name;
        return "-";
    }

    private bool _fullSceneReplay => fullSceneReplay;

    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
    }

    private static string FormatTime(float t)
    {
        int m = (int)(t / 60f);
        return $"{m:D2}:{t % 60f:05.2f}";
    }

    private string ResolveSessionPathOptional()
    {
        if (!string.IsNullOrEmpty(absoluteSessionPath) && Directory.Exists(absoluteSessionPath))
            return absoluteSessionPath;
        if (string.IsNullOrEmpty(sessionFolder))
            return null;
        string p = Path.Combine(Application.dataPath, "RunData", sessionFolder);
        return Directory.Exists(p) ? p : null;
    }

    private static string GetLayerNameForRig(string rigId) =>
        rigId.StartsWith("VR", StringComparison.OrdinalIgnoreCase) && rigId.Length >= 3
            ? $"Choice{rigId}"
            : rigId;

    private void OnDestroy() => UnloadSession();
}
