#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class BuildGrayShadeAngleSweepSwapDesign
{
    [MenuItem("Tools/Generate Gray Shade Choice AngleSweep Swap SequenceDesign.json")]
    private static void Build()
    {
        var skyStep = new
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
                    polar = new
                    {
                        radius = 0,
                        angle = 0,
                        height = -1
                    }
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

        float[] black = { 0.0f, 0.0f, 0.0f, 1f };
        float[] gray = { 0.5f, 0.5f, 0.5f, 1f };
        float[][] shades = { black, gray };
        int[] angles = { 50, 60, 70, 75, 80, 90, 100 };
        int[] symmetricAngles = { 90 }; // only run symmetric at 90°
        float swapAtSeconds = 10f;
        float stepSeconds = 20f;

        var swapSteps =
            from flip in new[] { false, true }
            from angle in angles
            let leftFirst = flip ? gray : black
            let rightFirst = flip ? black : gray
            let side = flip ? "flip" : "noflip"
            let baseName = $"StaticChoiceSwap_{side}_{ColorName(leftFirst)}_{ColorName(rightFirst)}_{angle}deg"
            select BuildStep(
                baseName,
                angle,
                leftFirst,
                rightFirst,
                stepSeconds,
                swapAtSeconds
            );

        var symmetricSteps =
            from shade in shades
            from angle in symmetricAngles
            let name = $"StaticChoiceSym_{ColorName(shade)}_{ColorName(shade)}_{angle}deg"
            select BuildStep(
                name,
                angle,
                shade,
                shade,
                stepSeconds,
                swapAfterSeconds: 0f
            );

        var soloSteps =
            from shade in shades
            let name = $"StaticSolo_{ColorName(shade)}_0deg"
            select BuildSoloStep(
                name,
                shade,
                stepSeconds
            );

        var design = new
        {
            seed = -1,
            repetitions = 40,
            sync = true,
            intertrial = skyStep,
            steps = swapSteps
                .Concat(symmetricSteps)
                .Concat(soloSteps)
                .ToArray()
        };

        string path = Path.Combine(
            Application.streamingAssetsPath,
            "sequenceDesign_grayShades_angleSweep_swap.json"
        );
        Directory.CreateDirectory(Application.streamingAssetsPath);
        File.WriteAllText(path, JsonConvert.SerializeObject(design, Formatting.Indented));

        Debug.Log($"Wrote design → {path}");
        AssetDatabase.Refresh();
    }

    private static dynamic BuildStep(
        string name,
        int angle,
        float[] leftColor,
        float[] rightColor,
        float seconds,
        float swapAfterSeconds
    )
    {
        return new
        {
            name = name,
            trigger = new { type = "time", seconds },
            closedLoopOrientation = true,
            closedLoopPosition = true,
            randomInitialRotation = false,
            swapAfterSeconds = swapAfterSeconds,
            objects = new[]
            {
                new
                {
                    type = "ScalingCylinder",
                    polar = new
                    {
                        radius = 50000,
                        angle = -angle / 2f,
                        height = 0
                    },
                    material = "Blue",
                    color = leftColor,
                    swapColor = rightColor,
                    scale = new
                    {
                        x = 7,
                        y = 100,
                        z = 7
                    },
                    visualAngleDegrees = 10
                },
                new
                {
                    type = "ScalingCylinder",
                    polar = new
                    {
                        radius = 50000,
                        angle = angle / 2f,
                        height = 0
                    },
                    material = "Blue",
                    color = rightColor,
                    swapColor = leftColor,
                    scale = new
                    {
                        x = 7,
                        y = 100,
                        z = 7
                    },
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
        };
    }

    private static dynamic BuildSoloStep(string name, float[] color, float seconds)
    {
        return new
        {
            name = name,
            trigger = new { type = "time", seconds },
            closedLoopOrientation = true,
            closedLoopPosition = true,
            randomInitialRotation = false,
            swapAfterSeconds = 0f,
            objects = new[]
            {
                new
                {
                    type = "ScalingCylinder",
                    polar = new
                    {
                        radius = 50000,
                        angle = 0,
                        height = 0
                    },
                    material = "Blue",
                    color = color,
                    scale = new
                    {
                        x = 7,
                        y = 100,
                        z = 7
                    },
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
        };
    }

    private static string ColorName(float[] color)
    {
        return Mathf.RoundToInt(color[0] * 100).ToString("D2");
    }
}
#endif
