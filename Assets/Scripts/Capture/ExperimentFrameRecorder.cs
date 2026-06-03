using System.IO;
using UnityEngine;

/// <summary>
/// Records LED-strip PNG sequences per VR rig during live experiments.
/// Attach to the same GameObject as ViewportSetter (VR1…VR4).
/// </summary>
[RequireComponent(typeof(ViewportSetter))]
public class ExperimentFrameRecorder : MonoBehaviour
{
    [Tooltip("Start recording automatically when a run directory exists.")]
    public bool recordAutomatically = true;

    [Tooltip("Capture every N frames (1 = every frame).")]
    public int captureEveryNFrames = 1;

    public bool IsRecording => _capture != null && _capture.IsRecording;

    private VrPanelFrameCapture _capture;
    private ViewportSetter _viewport;
    private int _frameCounter;

    private void Awake()
    {
        _viewport = GetComponent<ViewportSetter>();
        _capture = gameObject.AddComponent<VrPanelFrameCapture>();
    }

    private void Start()
    {
        if (!recordAutomatically)
            return;

        TryStartFromMasterLogger();
    }

    public void TryStartFromMasterLogger()
    {
        if (MasterDataLogger.Instance == null)
            return;

        _capture.faceCameras = VrPanelFrameCapture.CollectFaceCameras(_viewport);
        var main = FindMainController();
        if (main != null)
        {
            var cfg = main.GetSystemConfigForGameObject(gameObject);
            _capture.faceWidth = cfg.ledPanelWidth;
            _capture.faceHeight = cfg.ledPanelHeight;
        }

        string vrName = gameObject.name;
        string dir = Path.Combine(MasterDataLogger.Instance.directoryPath, "frames", vrName);
        _capture.BeginRecording(dir);
        _frameCounter = 0;
        Debug.Log($"[ExperimentFrameRecorder] Recording {vrName} → {dir}");
    }

    public void StopRecording()
    {
        _capture?.EndRecording();
    }

    private void LateUpdate()
    {
        if (_capture == null || !_capture.IsRecording)
            return;

        _frameCounter++;
        if (_frameCounter % captureEveryNFrames != 0)
            return;

        _capture.CaptureFrame();
    }

    private void OnDestroy()
    {
        StopRecording();
    }

    private static MainController FindMainController()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<MainController>();
#else
        return FindObjectOfType<MainController>();
#endif
    }
}
