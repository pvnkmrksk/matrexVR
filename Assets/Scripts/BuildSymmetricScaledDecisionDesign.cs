#if UNITY_EDITOR
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class BuildSymmetricScaledDecisionDesign
{
    [MenuItem("Tools/Generate Adaptive Symmetric Scaled Decision SequenceDesign.json")]
    private static void Build()
    {
        var intertrial = new
        {
            name = "skybox",
            trigger = new { type = "time", seconds = 5 },
            closedLoopOrientation = true,
            closedLoopPosition = true,
            objects = new[]
            {
                new
                {
                    type = "glassplane",
                    polar = new { radius = 0, angle = 0, height = -1 }
                }
            },
            camera = new[]
            {
                new { vrId = "VR1", clearFlags = "Skybox" },
                new { vrId = "VR2", clearFlags = "Skybox" },
                new { vrId = "VR3", clearFlags = "Skybox" },
                new { vrId = "VR4", clearFlags = "Skybox" }
            }
        };

        var templateStep = new
        {
            name = "AdaptiveDecisionTemplate_60cm_40deg",
            trigger = new
            {
                type = "area",
                areaTag = "Goal",
                vrId = "any",
                shape = "cylinder",
                size = 5f,
                radius = 2.5f,
                height = 5f,
                timeoutSeconds = 180f
            },
            closedLoopOrientation = true,
            closedLoopPosition = true,
            randomInitialRotation = false,
            objects = new[]
            {
                new
                {
                    type = "ScalingCylinder",
                    polar = new { radius = 60, angle = -20, height = 0 },
                    material = "SetColor",
                    color = new[] { 0f, 0f, 0f, 1f },
                    scale = new { x = 7, y = 100, z = 7 },
                    visualAngleDegrees = 10
                },
                new
                {
                    type = "ScalingCylinder",
                    polar = new { radius = 60, angle = 20, height = 0 },
                    material = "SetColor",
                    color = new[] { 0.5f, 0.5f, 0.5f, 1f },
                    scale = new { x = 7, y = 100, z = 7 },
                    visualAngleDegrees = 10
                }
            },
            camera = new[]
            {
                new { vrId = "VR1", clearFlags = "SolidColor", bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f } },
                new { vrId = "VR2", clearFlags = "SolidColor", bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f } },
                new { vrId = "VR3", clearFlags = "SolidColor", bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f } },
                new { vrId = "VR4", clearFlags = "SolidColor", bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f } }
            }
        };

        var design = new
        {
            seed = -1,
            repetitions = 1,
            sync = true,
            adaptiveDecision = new
            {
                enabled = true,
                startGray = 0.5f,
                grayStep = 0.05f,
                controlEvery = 5,
                noBarControlSeconds = 20f
            },
            intertrial,
            steps = new[] { templateStep }
        };

        string path = Path.Combine(
            Application.streamingAssetsPath,
            "sequenceDesign_symmetricScaledDecision_adaptive.json"
        );

        Directory.CreateDirectory(Application.streamingAssetsPath);
        File.WriteAllText(path, JsonConvert.SerializeObject(design, Formatting.Indented));

        Debug.Log($"Wrote design -> {path}");
        AssetDatabase.Refresh();
    }
}
#endif
