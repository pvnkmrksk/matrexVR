#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Opens the historical six-condition protocol with desktop keyboard controls.</summary>
[InitializeOnLoad]
public static class ReproducedExperimentPreview
{
    const string Pending = "AntExperiments.HistoricalPreview.Pending";
    const string Folder = "Experiments/HistoricalReplicationPreview/";
    static double deadline;
    static ReproducedExperimentPreview()
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
    [MenuItem("Ant experiments/Start reproduced experiment preview")]
    public static void StartPreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
            Debug.LogWarning("Stop Play mode first, then choose Ant experiments > Start reproduced experiment preview.");
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
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Historical preview setup did not become ready.");
            var main = MainController.Instance;
            var logger = MasterDataLogger.Instance;
            if (main == null || logger == null) return;
            if (logger.AcceptingWrites) { _ = logger.FinalizeSessionAsync("historical-preview-setup"); return; }
            if (logger.IsFinalizing) return;
            main.PrepareConfiguredRun(Folder + "system.json", Folder + "sequence.json");
            main.StartSequence();
            EditorApplication.update -= StartWhenReady;
            SessionState.SetBool(Pending, false);
            var gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
            Debug.Log("Historical keyboard preview: click Game; arrows move; A/D turn; R resets; stop with the Unity Play button. Original trials begin after the five-second intertrial.");
        } catch (Exception error) {
            EditorApplication.update -= StartWhenReady;
            SessionState.SetBool(Pending, false);
            Debug.LogException(error);
        }
    }
}
#endif
