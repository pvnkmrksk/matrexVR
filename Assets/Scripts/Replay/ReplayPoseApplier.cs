using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies logged CSV pose to a VR rig. Disables closed-loop and ZMQ on this object.
/// </summary>
public class ReplayPoseApplier : MonoBehaviour
{
    public string rigId;

    private static readonly List<ReplayPoseApplier> Active = new();

    public static IReadOnlyList<ReplayPoseApplier> ActiveAppliers => Active;

    private void Awake()
    {
        if (string.IsNullOrEmpty(rigId))
            rigId = gameObject.name;

        foreach (var cl in GetComponents<ClosedLoop>())
            cl.enabled = false;
        foreach (var zmq in GetComponents<ZmqListener>())
            zmq.enabled = false;
        foreach (var kb in GetComponents<Keyboard>())
            kb.enabled = false;
        foreach (var cap in GetComponents<ExperimentFrameRecorder>())
            cap.enabled = false;

        if (!Active.Contains(this))
            Active.Add(this);
    }

    private void OnDestroy()
    {
        Active.Remove(this);
    }

    private void LateUpdate()
    {
        if (!ReplaySessionContext.IsActive || ReplaySessionContext.Orchestrator == null)
            return;

        ReplaySessionData session = ReplaySessionContext.Archive?.Data;
        if (session == null)
            return;

        float t = ReplaySessionContext.Orchestrator.ReplayTime;
        session.GetPoseAtTime(rigId, t, out Vector3 pos, out Quaternion rot);
        transform.SetPositionAndRotation(pos, rot);
    }

    public static void ClearActive() => Active.Clear();
}
