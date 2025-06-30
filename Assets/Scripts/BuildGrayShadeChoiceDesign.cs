#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class BuildGrayShadeChoiceDesign
{
    [MenuItem("Tools/Generate Gray Shade Choice SequenceDesign.json")]
    private static void Build()
    {
        // 1) fixed inter-trial skybox step
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

        // 2) generate all shade × reference order × angle combinations
        float[][] shades =
        {
            new[] { 0.2f, 0.2f, 0.2f, 1f },
            new[] { 0.4f, 0.4f, 0.4f, 1f },
            new[] { 0.6f, 0.6f, 0.6f, 1f },
            new[] { 0.8f, 0.8f, 0.8f, 1f },
            new[] { 1.0f, 1.0f, 1.0f, 1f }
        };

        float[] reference = new[] { 0.6f, 0.6f, 0.6f, 1f };
        int[] angles = { 90, 180 };

        var steps =
            (from shade in shades
             from flip in new[] { false, true }
             from angle in angles
             let c1 = flip ? reference : shade
             let c2 = flip ? shade : reference
             let name = $"StaticChoice_{ColorName(c1)}_{ColorName(c2)}_{angle}deg"
             select new
             {
                 name = name,
                 trigger = new { type = "time", seconds = 10 },
                 closedLoopOrientation = true,
                 closedLoopPosition = true,
                 randomInitialRotation = true,
                 objects = new[]
                 {
                     new
                     {
                         type = "ScalingCylinder",
                         polar = new { radius = 50000, angle = -angle / 2f, height = 0 },
                         material = "Blue",
                         color = c1,
                         scale = new { x = 7, y = 100, z = 7 },
                         visualAngleDegrees = 10
                     },
                     new
                     {
                         type = "ScalingCylinder",
                         polar = new { radius = 50000, angle = angle / 2f, height = 0 },
                         material = "Blue",
                         color = c2,
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
             }).ToArray();

        // 3) assemble the design root
        var design = new
        {
            seed = -1,
            repetitions = 40,
            sync = true,
            intertrial = skyStep,
            steps = steps
        };

        // 4) write to StreamingAssets
        string path = Path.Combine(Application.streamingAssetsPath, "sequenceDesign_grayShades.json");
        Directory.CreateDirectory(Application.streamingAssetsPath);
        File.WriteAllText(path, JsonConvert.SerializeObject(design, Formatting.Indented));

        Debug.Log($"Wrote design → {path}");
        AssetDatabase.Refresh();
    }

    private static string ColorName(float[] color)
    {
        return Mathf.RoundToInt(color[0] * 100).ToString("D2"); // e.g., "20", "50", "80"
    }

}
#endif
