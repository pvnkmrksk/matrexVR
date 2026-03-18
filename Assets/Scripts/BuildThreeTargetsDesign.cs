#if UNITY_EDITOR
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class BuildThreeTargetsDesign
{
    [MenuItem("Tools/Generate 3 Targets SequenceDesign.json")]
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

        var barTrigger = new
        {
            type = "area",
            areaTag = "Goal",
            vrId = "any",
            shape = "cylinder",
            size = 5f,
            radius = 2.5f,
            height = 5f,
            timeoutSeconds = 120f
        };

        var solidCamera = new[]
        {
            new { vrId = "VR1", clearFlags = "SolidColor", bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f } },
            new { vrId = "VR2", clearFlags = "SolidColor", bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f } },
            new { vrId = "VR3", clearFlags = "SolidColor", bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f } },
            new { vrId = "VR4", clearFlags = "SolidColor", bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f } }
        };

        var black = new[] { 0f, 0f, 0f, 1f };
        var scale = new { x = 7, y = 100, z = 7 };

        var singleBlack = new
        {
            name = "ThreeTargets_singleBlack_60cm_0deg",
            trigger = barTrigger,
            closedLoopOrientation = true,
            closedLoopPosition = true,
            randomInitialRotation = false,
            objects = new[]
            {
                new
                {
                    type = "ScalingCylinder",
                    polar = new { radius = 60, angle = 0, height = 0 },
                    material = "SetColor",
                    color = black,
                    scale,
                    visualAngleDegrees = 10
                }
            },
            camera = solidCamera
        };

        var twoBlack = new
        {
            name = "ThreeTargets_twoBlack_60cm_40deg",
            trigger = barTrigger,
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
                    color = black,
                    scale,
                    visualAngleDegrees = 10
                },
                new
                {
                    type = "ScalingCylinder",
                    polar = new { radius = 60, angle = 20, height = 0 },
                    material = "SetColor",
                    color = black,
                    scale,
                    visualAngleDegrees = 10
                }
            },
            camera = solidCamera
        };

        var threeBlack = new
        {
            name = "ThreeTargets_threeBlack_100cm_40deg",
            trigger = barTrigger,
            closedLoopOrientation = true,
            closedLoopPosition = true,
            randomInitialRotation = false,
            objects = new[]
            {
                new
                {
                    type = "ScalingCylinder",
                    polar = new { radius = 100, angle = -20, height = 0 },
                    material = "SetColor",
                    color = black,
                    scale,
                    visualAngleDegrees = 10
                },
                new
                {
                    type = "ScalingCylinder",
                    polar = new { radius = 100, angle = 0, height = 0 },
                    material = "SetColor",
                    color = black,
                    scale,
                    visualAngleDegrees = 10
                },
                new
                {
                    type = "ScalingCylinder",
                    polar = new { radius = 100, angle = 20, height = 0 },
                    material = "SetColor",
                    color = black,
                    scale,
                    visualAngleDegrees = 10
                }
            },
            camera = solidCamera
        };

        var emptyWorld = new
        {
            name = "ThreeTargets_emptyWorld_20s",
            trigger = new { type = "time", seconds = 20 },
            closedLoopOrientation = true,
            closedLoopPosition = true,
            randomInitialRotation = false,
            objects = new object[] { },
            camera = solidCamera
        };

        var design = new
        {
            seed = -1,
            repetitions = 1,
            sync = true,
            intertrial,
            steps = new object[]
            {
                singleBlack,
                twoBlack,
                emptyWorld,
                threeBlack,
                threeBlack,
                threeBlack
            }
        };

        string path = Path.Combine(
            Application.streamingAssetsPath,
            "sequenceDesign_3targets.json"
        );

        Directory.CreateDirectory(Application.streamingAssetsPath);
        File.WriteAllText(path, JsonConvert.SerializeObject(design, Formatting.Indented));

        Debug.Log($"Wrote design -> {path}");
        AssetDatabase.Refresh();
    }
}
#endif
