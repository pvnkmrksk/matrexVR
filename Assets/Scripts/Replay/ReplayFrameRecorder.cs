using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Records replay rig camera views to PNG sequences under RunData session/frames_replay/.
/// </summary>
public class ReplayFrameRecorder : MonoBehaviour
{
    public bool recordOnPlay = true;
    public int captureWidth = 256;
    public int captureHeight = 256;
    public int captureEveryNFrames = 1;

    private readonly Dictionary<string, VrPanelFrameCapture> _captureByRig = new();
    private ReplayController _replay;
    private int _frameCounter;
    private string _sessionPath;

    public void Initialize(ReplayController replay, string sessionPath, IReadOnlyDictionary<string, Camera> cameras)
    {
        _replay = replay;
        _sessionPath = sessionPath;

        foreach (var kvp in cameras)
        {
            var go = new GameObject($"ReplayCapture_{kvp.Key}");
            go.transform.SetParent(transform);
            var cap = go.AddComponent<VrPanelFrameCapture>();
            cap.faceCameras = new[] { kvp.Value };
            cap.faceWidth = captureWidth;
            cap.faceHeight = captureHeight;
            cap.maxInFlight = 2;
            _captureByRig[kvp.Key] = cap;
        }
    }

    public void BeginAll()
    {
        if (!recordOnPlay || string.IsNullOrEmpty(_sessionPath))
            return;

        string root = Path.Combine(_sessionPath, "frames_replay");
        foreach (var kvp in _captureByRig)
        {
            string dir = Path.Combine(root, kvp.Key);
            kvp.Value.BeginRecording(dir);
        }
        _frameCounter = 0;
        Debug.Log($"[ReplayFrameRecorder] Recording to {root}");
    }

    public void EndAll()
    {
        foreach (var cap in _captureByRig.Values)
            cap.EndRecording();
    }

    private void LateUpdate()
    {
        if (_replay == null || !_replay.IsPlayingBack || _captureByRig.Count == 0)
            return;

        bool anyRecording = false;
        foreach (var cap in _captureByRig.Values)
        {
            if (cap.IsRecording)
            {
                anyRecording = true;
                break;
            }
        }
        if (!anyRecording)
            return;

        _frameCounter++;
        if (_frameCounter % captureEveryNFrames != 0)
            return;

        foreach (var cap in _captureByRig.Values)
            cap.CaptureFrame();
    }
}
