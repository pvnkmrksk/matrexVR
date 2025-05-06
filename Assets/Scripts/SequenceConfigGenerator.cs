using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class SequenceDesign          // <- matches the JSON structure above
{
    public int seed = -1;
    public int repetitions = 2;
    public SequenceItem intertrial;
    public SequenceItem[] steps;
}

/// <summary>
/// Builds the ordered SequenceConfig used at runtime,
/// writes it to persistentDataPath, and returns it.
/// </summary>
public static class SequenceConfigGenerator
{
    private const string DesignFileName = "sequenceDesign.json";

    public static (SequenceConfig, string /*path*/) CreateSessionConfig()
    {
        // 1) Load design-spec
        string designPath = Path.Combine(Application.streamingAssetsPath,
                                         DesignFileName);
        if (!File.Exists(designPath))
            throw new FileNotFoundException($"Design file missing: {designPath}");

        SequenceDesign design =
            JsonConvert.DeserializeObject<SequenceDesign>(
                File.ReadAllText(designPath));

        if (design.steps == null || design.steps.Length == 0)
            throw new Exception("Design file has no 'steps'.");

        // 2) Decide seed
        int seed = design.seed < 0 ? Environment.TickCount : design.seed;
        System.Random rng = new System.Random(seed);

        // 3) Build the ordered list
        List<SequenceItem> ordered = new List<SequenceItem>();
        for (int rep = 0; rep < design.repetitions; ++rep)
        {
            // copy → shuffle → interleave
            List<SequenceItem> shuffled = new List<SequenceItem>(design.steps);
            Shuffle(shuffled, rng);

            foreach (SequenceItem step in shuffled)
            {
                ordered.Add(design.intertrial);  // intertrial first
                ordered.Add(step);               // real trial second
            }
        }

        // 4) Wrap into SequenceConfig (the type you already have)
        SequenceConfig sessionConfig = new SequenceConfig
        {
            seed       = seed,
            randomise  = false,      // we already fixed the order
            sequences  = ordered.ToArray()
        };

        // 5) Persist
        string ts   = DateTime.Now.ToString("yyyyMMddHHmmss");
        string file = $"sequenceConfig_{ts}.json";
        string path = Path.Combine(Application.persistentDataPath, file);

        File.WriteAllText(path,
            JsonConvert.SerializeObject(sessionConfig, Formatting.Indented));

        Debug.Log($"[SequenceConfigGenerator] saved session config → {path}");
        return (sessionConfig, path);
    }

    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int n = list.Count; n > 1; n--)
        {
            int k    = rng.Next(n);
            T temp   = list[k];
            list[k]  = list[n - 1];
            list[n-1]= temp;
        }
    }
}
