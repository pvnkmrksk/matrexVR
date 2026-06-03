using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads real experiment scenes from archived sequence + system configs; applies logged poses only.
/// </summary>
public class ReplaySceneOrchestrator : MonoBehaviour
{
    public float ReplayTime { get; private set; }

    private ReplaySessionArchive _archive;
    private int _currentStepIndex = -1;
    private bool _sceneLoading;
    private float _pendingTime;
    private bool _subscribed;

    public bool IsReady => _archive != null && _archive.Data != null;

    public void LoadSession(ReplaySessionArchive archive)
    {
        _archive = archive;
        _currentStepIndex = -1;
        ReplayTime = 0f;

        ReplayExperimentHost.Ensure().ApplyArchive(archive);
        ReplaySessionContext.Activate(archive, this);

        if (!_subscribed)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            _subscribed = true;
        }

        int stepIndex = _archive.GetPlannedStepIndexAtTime(0f);
        LoadStep(stepIndex, 0f);
        Debug.Log($"[ReplayOrchestrator] Session loaded: {_archive.SessionPath}");
    }

    public void UnloadSession()
    {
        if (_subscribed)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _subscribed = false;
        }
        ReplaySessionContext.Deactivate();
        _archive = null;
        _currentStepIndex = -1;
    }

    public void SetReplayTime(float time, bool forceReloadScene = false)
    {
        if (!IsReady)
            return;

        ReplayTime = Mathf.Clamp(time, 0f, _archive.Data.MaxTime);
        int stepIndex = _archive.GetPlannedStepIndexAtTime(ReplayTime);

        if (forceReloadScene || stepIndex != _currentStepIndex)
            LoadStep(stepIndex, ReplayTime);
        else
            ApplyPosesOnly();
    }

    private void LoadStep(int stepIndex, float applyTime)
    {
        SequenceItem step = _archive.GetPlannedStep(stepIndex);
        if (step == null)
        {
            Debug.LogWarning($"[ReplayOrchestrator] No planned step at index {stepIndex}");
            ApplyPosesOnly();
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(step.sceneName))
        {
            Debug.LogError(
                $"[ReplayOrchestrator] Scene '{step.sceneName}' is not in Build Settings. " +
                "Replay requires the same build that recorded the session.");
            return;
        }

        _currentStepIndex = stepIndex;
        _pendingTime = applyTime;
        _sceneLoading = true;
        SceneManager.LoadScene(step.sceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_sceneLoading || _archive == null)
            return;

        _sceneLoading = false;
        SequenceItem step = _archive.GetPlannedStep(_currentStepIndex);
        if (step == null)
            return;

        ISceneController controller = FindSceneController();
        if (controller != null)
        {
            Dictionary<string, object> parameters = BuildParameters(step);
            controller.InitializeScene(parameters);
        }
        else
        {
            Debug.LogWarning($"[ReplayOrchestrator] No ISceneController in {scene.name}");
        }

        ReplayRigBinder.BindScene(_archive.Data);
        ApplyTargetDisplay();
        ReplayTime = _pendingTime;
        ApplyPosesOnly();

        ReplayController replay = FindObjectOfType<ReplayController>();
        replay?.OnSceneLoadedForCapture();
    }

    private void ApplyPosesOnly()
    {
        foreach (ReplayPoseApplier applier in ReplayPoseApplier.ActiveAppliers)
        {
            if (applier == null)
                continue;
            _archive.Data.GetPoseAtTime(applier.rigId, ReplayTime, out Vector3 pos, out Quaternion rot);
            applier.transform.SetPositionAndRotation(pos, rot);
        }
    }

    private void ApplyTargetDisplay()
    {
        int display = _archive.GlobalTargetDisplay;
        foreach (Camera cam in Object.FindObjectsOfType<Camera>())
            cam.targetDisplay = display;
    }

    private static ISceneController FindSceneController()
    {
#if UNITY_2023_1_OR_NEWER
        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
#else
        foreach (MonoBehaviour mb in Object.FindObjectsOfType<MonoBehaviour>())
#endif
        {
            if (mb is ISceneController sc)
                return sc;
        }
        return null;
    }

    private static Dictionary<string, object> BuildParameters(SequenceItem step)
    {
        var parameters = new Dictionary<string, object>();
        if (step.parameters != null)
        {
            foreach (var kvp in step.parameters)
                parameters[kvp.Key] = kvp.Value;
        }
        if (!parameters.ContainsKey("gain"))
            parameters["gain"] = step.gain;
        return parameters;
    }
}
