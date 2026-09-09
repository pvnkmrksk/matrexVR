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
    private const string Vr3BuildDirectory = "Builds/ShadeCalibrationVR3";
    private const string Vr3ExecutablePath = Vr3BuildDirectory + "/ShadeCalibrationVR3.x86_64";
    private const string Vr3ConfigPath = "Assets/StreamingAssets/shade_calibration_VR3.json";

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
        BuildLinuxPlayer(ExecutablePath, null);
    }

    [MenuItem("Tools/Shade Calibration/Build Linux VR3")]
    public static void BuildLinuxVR3()
    {
        BuildLinuxPlayer(Vr3ExecutablePath, Vr3ConfigPath);
    }

    private static void BuildLinuxPlayer(string executablePath, string replacementConfigPath)
    {
        CreateScene();
        Directory.CreateDirectory(Path.GetDirectoryName(executablePath));

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = executablePath,
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException("Shade calibration build failed: " + report.summary.result);
        }

        if (!string.IsNullOrEmpty(replacementConfigPath))
        {
            string executableName = Path.GetFileNameWithoutExtension(executablePath);
            string builtConfigPath = Path.Combine(
                Path.GetDirectoryName(executablePath),
                executableName + "_Data/StreamingAssets/shade_calibration.json");
            File.Copy(replacementConfigPath, builtConfigPath, true);
        }

        Debug.Log("Shade calibration build complete: " + executablePath);
    }
}
