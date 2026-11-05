#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PathPilotDeadlineWatchdog
{
    static PathPilotDeadlineWatchdog()
    {
        EditorApplication.update += () => {
            if (!EditorApplication.isPlaying || !EditorApplication.isPaused) return;
            foreach (var controller in Object.FindObjectsByType<DynamicSequenceController>(FindObjectsSortMode.None))
                controller.TickPilotSession();
        };
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.ExitingPlayMode)
                foreach (var controller in Object.FindObjectsByType<DynamicSequenceController>(FindObjectsSortMode.None))
                    controller.EndPilotSession("editor_stop");
        };
    }
}
#endif
