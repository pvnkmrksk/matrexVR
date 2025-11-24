#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class BuildSymmetricScaledDecisionDesign
{
    [MenuItem("Tools/Generate Symmetric Scaled Decision SequenceDesign.json")]
    private static void Build()
    {
        // Fixed 15 s inter-trial skybox
        var skyStep = new
        {
            name = "skybox",
            trigger = new { type = "time", seconds = 15 },
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

        // Radius sweep; collision-based trigger when player reaches either object
        int[] radii = { 20, 40, 60, 80, 100 };
        var black = new[] { 0f, 0f, 0f, 1f };

        const float triggerSize = 5f; // meters
        const float triggerHeight = 5f;

        var steps = radii
            .Select(radius => new
            {
                name = $"SymmetricScaledDecision_{radius}_20deg_collision",
                trigger = new
                {
                    type = "area",
                    areaTag = "Goal",
                    vrId = "any",
                    shape = "cylinder",
                    size = triggerSize,
                    radius = triggerSize * 0.5f,
                    height = triggerHeight
                },
                closedLoopOrientation = true,
                closedLoopPosition = true,
                randomInitialRotation = false,
                objects = new[]
                {
                    new
                    {
                        type = "ScalingCylinder",
                        polar = new { radius, angle = -20, height = 0 },
                        material = "SetColor",
                        color = black,
                        scale = new { x = 7, y = 100, z = 7 },
                        visualAngleDegrees = 10
                    },
                    new
                    {
                        type = "ScalingCylinder",
                        polar = new { radius, angle = 20, height = 0 },
                        material = "SetColor",
                        color = black,
                        scale = new { x = 7, y = 100, z = 7 },
                        visualAngleDegrees = 10
                    }
                },
                camera = new[]
                {
                    new
                    {
                        vrId = "VR1",
                        clearFlags = "SolidColor",
                        bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f }
                    },
                    new
                    {
                        vrId = "VR2",
                        clearFlags = "SolidColor",
                        bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f }
                    },
                    new
                    {
                        vrId = "VR3",
                        clearFlags = "SolidColor",
                        bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f }
                    },
                    new
                    {
                        vrId = "VR4",
                        clearFlags = "SolidColor",
                        bgColor = new[] { 0.8f, 0.8f, 0.8f, 1f }
                    }
                }
            })
            .ToArray();

        var design = new
        {
            seed = -1, // negative → re-shuffle each session
            repetitions = 1,
            sync = true,
            intertrial = skyStep,
            steps = steps
        };

        string path = Path.Combine(
            Application.streamingAssetsPath,
            "sequenceDesign_symmetricScaledDecision.json"
        );
        Directory.CreateDirectory(Application.streamingAssetsPath);
        File.WriteAllText(path, JsonConvert.SerializeObject(design, Formatting.Indented));

        Debug.Log($"Wrote design → {path}");
        AssetDatabase.Refresh();
    }
}
#endif
