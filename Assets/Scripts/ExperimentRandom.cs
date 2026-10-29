using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Owned experiment random streams; recorded poses/events remain replay's authority.</summary>
public static class ExperimentRandom
{
    public static int ResolvedSeed { get; private set; } = 1;
    public static bool Active { get; private set; }
    public static System.Random Sequence { get; private set; } = new System.Random(1);
    private static readonly Dictionary<string, int> occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
    private static string evidencePath;
    private static string activeContext, latestContext;
    public static string Context => activeContext ?? latestContext ?? "world";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Active = false; ResolvedSeed = 1; Sequence = new System.Random(1); occurrences.Clear(); evidencePath = null; activeContext = latestContext = null;
    }

    public static void End()
    {
        Active = false; evidencePath = null; occurrences.Clear(); activeContext = latestContext = null;
    }

    public static void Begin(string mode, int configuredSeed, string directory, string title, string sequenceReference)
    {
        if (!string.Equals(mode, "Fixed", StringComparison.OrdinalIgnoreCase) && !string.Equals(mode, "Random", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("randomSeedMode must be Fixed or Random.");
        int seed = configuredSeed;
        if (string.Equals(mode, "Random", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = new byte[4]; using (var generator = RandomNumberGenerator.Create()) generator.GetBytes(bytes);
            seed = BitConverter.ToInt32(bytes, 0);
        }
        var record = new JObject {
            ["schemaVersion"] = 1, ["mode"] = mode, ["configuredSeed"] = configuredSeed, ["resolvedSeed"] = seed,
            ["sequenceTitle"] = title, ["sequenceConfig"] = sequenceReference, ["algorithm"] = "fnv1a-keyed-v1",
            ["scope"] = "Seeded initialization, sequence shuffle, dynamic choices, component random streams and particle seeds. Recorded poses, animation and events are authoritative; frame timing, physics and third-party scripts may vary."
        };
        // Never replace a seed for a session that has already begun.
        using (var stream = new FileStream(Path.Combine(directory, "reproducibility.json"), FileMode.CreateNew, FileAccess.Write))
        using (var writer = new StreamWriter(stream)) writer.Write(record.ToString());
        ResolvedSeed = seed; occurrences.Clear(); activeContext = latestContext = null;
        Sequence = new System.Random(Derive(seed, "sequence"));
        UnityEngine.Random.InitState(Derive(seed, "world"));
        evidencePath = Path.Combine(directory, "random_streams.jsonl");
        Active = true;
    }

    public static int Derive(int seed, string key)
    {
        unchecked
        {
            uint hash = 2166136261u ^ (uint)seed;
            foreach (char character in key ?? "") { hash ^= character; hash *= 16777619u; }
            return (int)hash;
        }
    }

    public static int NextSeed(string key)
    {
        occurrences.TryGetValue(key, out int occurrence); occurrences[key] = occurrence + 1;
        int seed = Derive(ResolvedSeed, key + ":" + occurrence);
        if (Active && evidencePath != null)
            File.AppendAllText(evidencePath, new JObject { ["key"] = key, ["occurrence"] = occurrence, ["seed"] = seed }.ToString(Newtonsoft.Json.Formatting.None) + "\n");
        return seed;
    }

    public static IDisposable Scope(string key) => new UnityScope(NextSeed(key), key, true);
    public static IDisposable ScopeFor(Component component, string purpose)
    {
        string key = Context + ":" + ComponentKey(component) + ":" + purpose;
        return new UnityScope(NextSeed(key), key, false);
    }
    public static System.Random ForComponent(Component component, string purpose) => new System.Random(NextSeed(Context + ":" + ComponentKey(component) + ":" + purpose));
    public static string ComponentKey(Component component)
    {
        if (component == null) throw new ArgumentNullException(nameof(component));
        var parts = new List<string>();
        for (Transform item = component.transform; item != null; item = item.parent)
        {
            int duplicate = 0;
            if (item.parent != null)
                for (int i = 0; i < item.GetSiblingIndex(); i++) if (item.parent.GetChild(i).name == item.name) duplicate++;
            parts.Insert(0, item.name + "[" + duplicate + "]");
        }
        int slot = Array.IndexOf(component.GetComponents(component.GetType()), component);
        return component.gameObject.scene.name + ":" + string.Join("/", parts) + ":" + component.GetType().FullName + "[" + slot + "]";
    }

    private sealed class UnityScope : IDisposable
    {
        private readonly UnityEngine.Random.State prior;
        private readonly string priorContext;
        public UnityScope(int seed, string context, bool ownsContext)
        {
            prior = UnityEngine.Random.state; priorContext = activeContext;
            if (ownsContext && activeContext == null) latestContext = context;
            activeContext = context; UnityEngine.Random.InitState(seed);
        }
        public void Dispose() { UnityEngine.Random.state = prior; activeContext = priorContext; }
    }

    public static void SeedParticles(Scene scene, string context)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.useAutoRandomSeed = false;
                particle.randomSeed = unchecked((uint)Derive(ResolvedSeed, context + ":" + ComponentKey(particle))) | 1u;
            }
    }
}
