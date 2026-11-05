#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Opens the ten-condition path pilot with desktop keyboard controls.</summary>
[InitializeOnLoad]
public static class PathPilotLauncher
{
    const string Pending = "AntExperiments.PathPilot.Pending";
    const string Folder = "Experiments/PathAnglePilot/";
    static double deadline;
    static PathPilotLauncher()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false)) {
                deadline = EditorApplication.timeSinceStartup + 60;
                EditorApplication.update -= StartWhenReady;
                EditorApplication.update += StartWhenReady;
            }
            if (state == PlayModeStateChange.ExitingPlayMode) {
                SessionState.SetBool(Pending, false);
                EditorApplication.update -= StartWhenReady;
            }
        };
    }
    [MenuItem("Ant experiments/Start path-angle pilot (keyboard)")]
    public static void StartPreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
            Debug.LogWarning("Stop Play mode first, then choose Ant experiments > Start path-angle pilot (keyboard).");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    static void StartWhenReady()
    {
        try {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Path pilot setup did not become ready.");
            var main = MainController.Instance;
            var logger = MasterDataLogger.Instance;
            if (main == null || logger == null) return;
            if (logger.AcceptingWrites) { _ = logger.FinalizeSessionAsync("pilot-preview-setup"); return; }
            if (logger.IsFinalizing) return;
            main.PrepareConfiguredRun(Folder + "system.preview.json", Folder + "sequence.json");
            main.StartSequence();
            EditorApplication.update -= StartWhenReady;
            SessionState.SetBool(Pending, false);
            var gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
            Debug.Log("Path pilot keyboard preview: click Game; arrows move; A/D turn; R resets; stop with the Unity Play button. Pilot trials begin after the five-second intertrial.");
        } catch (Exception error) {
            EditorApplication.update -= StartWhenReady;
            SessionState.SetBool(Pending, false);
            Debug.LogException(error);
        }
    }
}
#endif
