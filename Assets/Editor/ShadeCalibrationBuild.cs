using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShadeCalibrationBuild
{
    private const string ScenePath = "Assets/Scenes/ShadeCalibration.unity";
    private const string BuildDirectory = "Builds/ShadeCalibration";
    private const string ExecutablePath = BuildDirectory + "/ShadeCalibration.x86_64";

    [MenuItem("Tools/Shade Calibration/Create Scene")]
    public static void CreateScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject controller = new GameObject("Shade Calibration Controller");
        controller.AddComponent<ShadeCalibrationController>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("Created shade calibration scene: " + ScenePath);
    }

    [MenuItem("Tools/Shade Calibration/Build Linux")]
    public static void BuildLinux()
    {
        CreateScene();
        Directory.CreateDirectory(BuildDirectory);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = ExecutablePath,
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException("Shade calibration build failed: " + report.summary.result);
        }

        Debug.Log("Shade calibration build complete: " + ExecutablePath);
    }
}
