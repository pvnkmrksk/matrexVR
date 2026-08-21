#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class BuildDirectednessVSDecisionAccuracyAngleSweepDesign
{
    [MenuItem("Tools/Generate Directedness VS Decision Accuracy Angle Sweep SequenceDesign.json")]
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
            camera = BuildSkyboxCameras()
        };

        var black = new[] { 0f, 0f, 0f, 1f };
        var gray04 = new[] { 0.4f, 0.4f, 0.4f, 1f };
        int[] conflictAngles = { 40, 80, 120, 160, 180 };
        var steps = new List<object>();

        foreach (int conflictAngle in conflictAngles)
        {
            float halfAngle = conflictAngle / 2f;
            steps.Add(BuildStep(
                $"Decision_blackGray04_60cm_{conflictAngle}deg_noflip",
                -halfAngle,
                halfAngle,
                black,
                gray04
            ));
            steps.Add(BuildStep(
                $"Decision_Gray04black_60cm_{conflictAngle}deg_flip",
                -halfAngle,
                halfAngle,
                gray04,
                black
            ));
        }

        var design = new
        {
            seed = -1,
            repetitions = 1,
            sync = true,
            intertrial,
            steps = steps.ToArray()
        };

        string path = Path.Combine(
            Application.streamingAssetsPath,
            "sequenceDesign_directednessVSDecisionAccuracy_angleSweep.json"
        );

        Directory.CreateDirectory(Application.streamingAssetsPath);
        File.WriteAllText(path, JsonConvert.SerializeObject(design, Formatting.Indented));

        Debug.Log($"Wrote design -> {path}");
        AssetDatabase.Refresh();
    }

    private static object BuildStep(
        string name,
        float leftAngle,
        float rightAngle,
        float[] leftColor,
        float[] rightColor
    )
    {
        return new
        {
            name,
            trigger = new
            {
                type = "area",
                areaTag = "Goal",
                vrId = "any",
                shape = "cylinder",
                size = 5f,
                radius = 2.5f,
                height = 5f,
                timeoutSeconds = 120f
            },
            closedLoopOrientation = true,
            closedLoopPosition = true,
            randomInitialRotation = false,
            objects = new[]
            {
                BuildGoal(leftAngle, leftColor),
                BuildGoal(rightAngle, rightColor)
            },
            camera = BuildSolidCameras()
        };
    }

    private static object BuildGoal(float angle, float[] color)
    {
        return new
        {
            type = "ScalingCylinder",
            polar = new { radius = 60, angle, height = 0 },
            material = "SetColor",
            color,
            scale = new { x = 7, y = 100, z = 7 },
            visualAngleDegrees = 10
        };
    }

    private static object[] BuildSkyboxCameras()
    {
        return new object[]
        {
            new { vrId = "VR1", clearFlags = "Skybox" },
            new { vrId = "VR2", clearFlags = "Skybox" },
            new { vrId = "VR3", clearFlags = "Skybox" },
            new { vrId = "VR4", clearFlags = "Skybox" }
        };
    }

    private static object[] BuildSolidCameras()
    {
        return new object[]
        {
            BuildSolidCamera("VR1"),
            BuildSolidCamera("VR2"),
            BuildSolidCamera("VR3"),
            BuildSolidCamera("VR4")
        };
    }

    private static object BuildSolidCamera(string vrId)
    {
        return new
        {
            vrId,
            clearFlags = "SolidColor",
            bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f }
        };
    }
}
#endif
