#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class BuildstaticChoiceDesign
{
    [MenuItem("Tools/Generate static Choice SequenceDesign.json")]
    private static void Build()
    {
        // 1) the fixed inter-trial skybox step
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

        // 2) generate all angle × colour-order combinations
        int[] angles =
        {
            0,
            25,
            45,
            46,
            47,
            48,
            49,
            50,
            51,
            52,
            53,
            54,
            55,
            56,
            57,
            58,
            59,
            60,
            61,
            62,
            63,
            64,
            65,
            66,
            67,
            68,
            69,
            70,
            71,
            72,
            73,
            74,
            75,
            76,
            77,
            78,
            79,
            80,
            81,
            82,
            83,
            84,
            85,
            86,
            87,
            88,
            89,
            90,
            95,
            105,
            115,
            135,
            155,
            180
        };
        var colours = new[] { ("Black", "Black") };

        var steps = (
            from a in angles
            from c in colours
            select new
            {
                name = $"StaticChoice_{c.Item1}_{c.Item2}_{a}deg",
                trigger = new { type = "time", seconds = 10 },
                closedLoopOrientation = true,
                closedLoopPosition = true,
                randomInitialRotation = true,
                objects = new[]
                {
                    new
                    {
                        type = "ScalingCylinder",
                        polar = new
                        {
                            radius = 50000,
                            angle = -a / 2f,
                            height = 0
                        },
                        material = c.Item1,
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
                            angle = a / 2f,
                            height = 0
                        },
                        material = c.Item2,
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
            }
        ).ToArray();

        // 3) assemble the design root
        var design = new
        {
            seed = -1, // negative → re-shuffle each session
            repetitions = 40,
            sync = true,
            intertrial = skyStep,
            steps = steps
        };

        // 4) write to StreamingAssets
        string path = Path.Combine(
            Application.streamingAssetsPath,
            "sequenceDesign_staticChoice.json"
        );
        Directory.CreateDirectory(Application.streamingAssetsPath);
        File.WriteAllText(path, JsonConvert.SerializeObject(design, Formatting.Indented));

        Debug.Log($"Wrote design → {path}");
        AssetDatabase.Refresh();
    }
}
#endif
