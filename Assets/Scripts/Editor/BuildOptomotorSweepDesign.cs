#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class BuildOptomotorSweepDesign
{
    [MenuItem("Tools/Generate Optomotor Sweep Stimuli")]
    private static void Build()
    {
        float baseSpeed = 36f;
        float baseFrequency = 16f;
        int[] log2Steps = Enumerable.Range(-3, 7).ToArray(); // [-3 to 3]
        float duration = 15f;
        string rotationAxis = "Yaw";
        float contrast = 1f;

        // Create helper to compute log2 sweeps
        float[] Log2Sweep(float baseVal) =>
            log2Steps.Select(step => baseVal * Mathf.Pow(2f, step)).ToArray();

        // Directions: true = CW, false = CCW
        bool[] directions = { true, false };

        // Sweep speeds, fixed frequency
        var sweepSpeedStimuli = (
            from speed in Log2Sweep(baseSpeed)
            from cw in directions
            select new
            {
                speed = speed,
                frequency = baseFrequency,
                clockwise = cw,
                duration = duration,
                rotationAxis = rotationAxis,
                contrast = contrast
            }).ToArray();

        // Sweep frequencies, fixed speed
        var sweepFreqStimuli = (
            from freq in Log2Sweep(baseFrequency)
            from cw in directions
            select new
            {
                speed = baseSpeed,
                frequency = freq,
                clockwise = cw,
                duration = duration,
                rotationAxis = rotationAxis,
                contrast = contrast
            }).ToArray();

        // Design wrapper
        var sweepSpeedDesign = new { loop = true, stimuli = sweepSpeedStimuli };
        var sweepFreqDesign = new { loop = true, stimuli = sweepFreqStimuli };

        // Write to StreamingAssets
        string basePath = Application.streamingAssetsPath;
        Directory.CreateDirectory(basePath);

        string speedPath = Path.Combine(basePath, "optomotor_sweep_speed_fixed_freq.json");
        File.WriteAllText(speedPath, JsonConvert.SerializeObject(sweepSpeedDesign, Formatting.Indented));

        string freqPath = Path.Combine(basePath, "optomotor_sweep_freq_fixed_speed.json");
        File.WriteAllText(freqPath, JsonConvert.SerializeObject(sweepFreqDesign, Formatting.Indented));

        Debug.Log($"✅ Wrote optomotor designs:\n→ {speedPath}\n→ {freqPath}");
        AssetDatabase.Refresh();
    }
}
#endif
