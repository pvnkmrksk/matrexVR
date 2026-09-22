using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TerrainSequenceStartupValidation
{
    private const string Key = "TerrainSequenceStartupValidation";
    private static double start;
    static TerrainSequenceStartupValidation()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Check;
    }

    public static void OpenControlScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");
    }

    public static void Run()
    {
        OpenControlScene();
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static void Check()
    {
        if (!EditorApplication.isPlaying) return;
        if (start == 0) start = EditorApplication.timeSinceStartup;
        double elapsed = EditorApplication.timeSinceStartup - start;
        if (elapsed < 3) return;
        var main = MainController.Instance;
        if (main != null && main.sequenceSteps.Count == 16 && main.executionOrder.Count == 16 &&
            SceneManager.GetActiveScene().name == "navrug_rr" && main.GetCurrentSequenceStep() != null)
        {
            File.WriteAllText("/tmp/ledpanel-terrain-autostart.txt", "PASS: ControlScene Play automatically loaded the active 16-condition sequence and entered navrug_rr.\n");
            End(0);
        }
        else if (elapsed > 30)
        {
            Debug.LogError("Terrain sequence did not start automatically from ControlScene.");
            End(1);
        }
    }

    private static void End(int code)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Check;
        EditorApplication.Exit(code);
    }
}
