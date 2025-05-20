#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class BuildChoiceDesign
{
    [MenuItem("Tools/Generate Choice SequenceDesign.json")]
    private static void Build()
    {
        // 1) the fixed inter-trial skybox step
        var skyStep = new {
            name     = "skybox",
            trigger  = new { type = "time", seconds = 15 },
            camera   = new[] {
                new { vrId="VR1", clearFlags="Skybox" },
                new { vrId="VR2", clearFlags="Skybox" },
                new { vrId="VR3", clearFlags="Skybox" },
                new { vrId="VR4", clearFlags="Skybox" }
            }
        };

        // 2) generate all angle × colour-order combinations
        int[]  angles = { 20,30,50,70,90,110,130,150,180 };
        var    colours= new[] { ("Blue","BlueGreen"), ("BlueGreen","Blue") };

        var steps =
            (from a in angles
             from c in colours
             select new {
                 name = $"ScalingChoice_{c.Item1}_{c.Item2}_{a}deg",
                 trigger  = new { type="time", seconds = 45 },
                 closedLoopOrientation = true,
                 closedLoopPosition    = true,
                 objects = new[] {
                     new {
                         type="ScalingCylinder",
                         polar=new { radius=60, angle= -a/2f, height = 0 },
                         material = c.Item1,
                         scale=new { x=7, y=100, z=7 },
                         visualAngleDegrees=10
                     },
                     new {
                         type="ScalingCylinder",
                         polar=new { radius=60, angle=  a/2f, height = 0 },
                         material = c.Item2,
                         scale=new { x=7, y=100, z=7 },
                         visualAngleDegrees=10
                     }
                 },
                 camera = new[] {
                     new { vrId="VR1", clearFlags="SolidColor", bgColor=new[]{0.8f,0.8f,0.8f,1f}},
                     new { vrId="VR2", clearFlags="SolidColor", bgColor=new[]{0.8f,0.8f,0.8f,1f}},
                     new { vrId="VR3", clearFlags="SolidColor", bgColor=new[]{0.8f,0.8f,0.8f,1f}},
                     new { vrId="VR4", clearFlags="SolidColor", bgColor=new[]{0.8f,0.8f,0.8f,1f}}
                 }
             }).ToArray();

        // 3) assemble the design root
        var design = new {
            seed = -1,                  // negative → re-shuffle each session
            repetitions = 10,
            sync = true,
            intertrial = skyStep,
            steps      = steps
        };

        // 4) write to StreamingAssets
        string path = Path.Combine(Application.streamingAssetsPath, "sequenceDesign_choice.json");
        Directory.CreateDirectory(Application.streamingAssetsPath);
        File.WriteAllText(path, JsonConvert.SerializeObject(design, Formatting.Indented));

        Debug.Log($"Wrote design → {path}");
        AssetDatabase.Refresh();
    }
}
#endif