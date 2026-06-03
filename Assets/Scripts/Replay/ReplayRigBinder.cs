using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Finds VR rig roots in loaded experiment scenes and attaches pose replay drivers.
/// </summary>
public static class ReplayRigBinder
{
    public static void BindScene(ReplaySessionData session)
    {
        ReplayPoseApplier.ClearActive();

        if (session == null)
            return;

        foreach (string rigId in session.SamplesByRig.Keys)
        {
            GameObject rig = FindRigRoot(rigId);
            if (rig == null)
            {
                Debug.LogWarning($"[ReplayRigBinder] Rig '{rigId}' not found in scene {SceneManager.GetActiveScene().name}");
                continue;
            }

            ReplayPoseApplier applier = rig.GetComponent<ReplayPoseApplier>();
            if (applier == null)
                applier = rig.AddComponent<ReplayPoseApplier>();
            applier.rigId = rigId;
        }

        FreezeLiveExperimentLogic();
        Debug.Log($"[ReplayRigBinder] Bound {ReplayPoseApplier.ActiveAppliers.Count} rigs in {SceneManager.GetActiveScene().name}");
    }

    private static GameObject FindRigRoot(string rigId)
    {
        GameObject exact = GameObject.Find(rigId);
        if (exact != null)
            return exact;

        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name == rigId || go.name.Contains(rigId))
                return go;
        }
        return null;
    }

    private static void FreezeLiveExperimentLogic()
    {
        foreach (var cl in Object.FindObjectsOfType<ClosedLoop>())
            cl.enabled = false;
        foreach (var zmq in Object.FindObjectsOfType<ZmqListener>())
            zmq.enabled = false;
        foreach (var opt in Object.FindObjectsOfType<OptomotorSceneController>())
            opt.enabled = false;
        foreach (var dyn in Object.FindObjectsOfType<DynamicSequenceController>())
            dyn.enabled = false;
        foreach (var kb in Object.FindObjectsOfType<Keyboard>())
            kb.enabled = false;
    }
}
