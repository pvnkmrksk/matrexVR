using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityGLTF;

/// <summary>Preload self-contained GLB models before stimulus clocks begin.</summary>
public static class ExternalModelLibrary
{
    private sealed class Entry { public string Hash; public GameObject Prefab; }
    private static readonly Dictionary<string, Entry> models = new Dictionary<string, Entry>(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> generations = new Dictionary<string, int>(StringComparer.Ordinal);
    private static Transform root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { models.Clear(); generations.Clear(); root = null; }

    public static IEnumerable<string> CollectReferences(JToken document)
    {
        if (document is JObject obj)
        {
            if ((bool?)obj["enabled"] == false) yield break;
            foreach (var property in obj.Properties())
            {
                if ((property.Name == "modelPath" || property.Name == "kannadiModelPath") && property.Value.Type == JTokenType.String)
                {
                    string path = (string)property.Value;
                    if (!string.IsNullOrWhiteSpace(path)) yield return path;
                }
                else foreach (string path in CollectReferences(property.Value)) yield return path;
            }
        }
        else if (document is JArray array)
            foreach (var child in array) foreach (string path in CollectReferences(child)) yield return path;
    }

    public static async Task PreloadAsync(string reference, byte[] bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateEmbeddedGlb(reference, bytes);
        string hash = RunConfigArchive.Hash(bytes);
        generations.TryGetValue(reference, out int previousGeneration);
        int generation = previousGeneration + 1; generations[reference] = generation;
        if (models.TryGetValue(reference, out Entry old) && old.Hash == hash && old.Prefab != null) return;
        if (root == null)
        {
            root = new GameObject("External model templates").transform;
            root.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(root.gameObject);
        }
        GameObject loaded = null;
        using (var stream = new MemoryStream(bytes, false))
        using (var importer = new GLTFSceneImporter(stream, new ImportOptions {
            AnimationMethod = AnimationMethod.Legacy, AnimationLoopTime = true, CameraImport = CameraImportOption.None
        }))
        {
            importer.SceneParent = root;
            try
            {
                await importer.LoadSceneAsync(showSceneObj: false, cancellationToken: cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                loaded = importer.CreatedObject;
                if (loaded == null) throw new InvalidDataException("The GLB contains no scene.");
                loaded.name = Path.GetFileNameWithoutExtension(reference);
                loaded.SetActive(false);
                // Imported lights and cameras are asset authoring details; the
                // experiment's sun and physical cameras retain ownership.
                foreach (var camera in loaded.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var light in loaded.GetComponentsInChildren<Light>(true)) light.enabled = false;
                foreach (var animation in loaded.GetComponentsInChildren<Animation>(true)) animation.playAutomatically = true;
                if (!generations.TryGetValue(reference, out int latestGeneration) || latestGeneration != generation)
                {
                    Release(loaded); loaded = null; return;
                }
                // Another import may have completed while this one yielded.
                models.TryGetValue(reference, out old);
                models[reference] = new Entry { Hash = hash, Prefab = loaded };
                if (old?.Prefab != null) Release(old.Prefab);
            }
            catch
            {
                // Import cancellation can happen before LoadSceneAsync returns its root.
                loaded = loaded != null ? loaded : importer.CreatedObject;
                if (loaded != null) Release(loaded);
                throw;
            }
        }
    }
    private static void Release(GameObject target)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(target); else UnityEngine.Object.DestroyImmediate(target);
    }

    public static GameObject GetPrefab(string reference)
    {
        reference = ReplayExecutionContext.ModelKey(reference);
        if (!models.TryGetValue(reference, out Entry entry) || entry.Prefab == null)
            throw new InvalidOperationException("External model was not preloaded before the experiment: " + reference);
        return entry.Prefab;
    }

    public static GameObject InstantiatePrefab(GameObject prefab, Transform parent = null)
    {
        var gltf = prefab.GetComponent<InstantiatedGLTFObject>();
        GameObject instance = gltf != null ? gltf.Duplicate().gameObject : UnityEngine.Object.Instantiate(prefab);
        if (parent != null) instance.transform.SetParent(parent, false);
        instance.SetActive(true);
        return instance;
    }

    public static void ConfigureAnimation(GameObject instance, string clip = null, float speed = 1)
    {
        if (float.IsNaN(speed) || float.IsInfinity(speed) || speed < 0)
            throw new ArgumentException("animationSpeed must be finite and nonnegative.");
        foreach (var animation in instance.GetComponentsInChildren<Animation>(true))
        {
            string selected = string.IsNullOrWhiteSpace(clip) ? animation.clip?.name : clip;
            if (selected == null) continue;
            if (animation[selected] == null) throw new ArgumentException("Animation clip is not present in model: " + selected);
            animation[selected].speed = speed;
            animation[selected].time = 0;
            animation[selected].wrapMode = WrapMode.Loop;
            animation.Play(selected);
            animation.Sample();
        }
        foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.speed = speed;
    }

    public static void ValidateEmbeddedGlb(string reference, byte[] bytes)
    {
        if (!string.Equals(Path.GetExtension(reference), ".glb", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Custom models must be self-contained .glb files (export geometry, textures and animations together).");
        if (bytes == null || bytes.Length < 20 || BitConverter.ToUInt32(bytes, 0) != 0x46546c67 ||
            BitConverter.ToUInt32(bytes, 4) != 2 || BitConverter.ToUInt32(bytes, 8) != bytes.Length ||
            BitConverter.ToUInt32(bytes, 16) != 0x4e4f534a)
            throw new InvalidDataException("Invalid GLB 2.0 header: " + reference);
        uint jsonLength = BitConverter.ToUInt32(bytes, 12);
        if (jsonLength > bytes.Length - 20) throw new InvalidDataException("Truncated GLB JSON chunk: " + reference);
        JObject json = JObject.Parse(Encoding.UTF8.GetString(bytes, 20, (int)jsonLength).TrimEnd('\0', ' '));
        // These decoders are optional UnityGLTF packages and are not shipped in
        // this player. Reject required compression before a partial import can
        // be mistaken for the purchased/exported model's complete appearance.
        var unavailable = new[] { "KHR_draco_mesh_compression", "KHR_texture_basisu", "EXT_meshopt_compression" };
        foreach (string extension in (json["extensionsRequired"] as JArray ?? new JArray()).Values<string>())
            if (unavailable.Contains(extension, StringComparer.Ordinal))
                throw new InvalidDataException("This build cannot decode required " + extension + ". Export an uncompressed GLB with embedded PNG/JPEG textures, or build with its matching decoder package.");
        foreach (var uri in json.Descendants().OfType<JProperty>().Where(p => p.Name == "uri"))
            if (uri.Value.Type == JTokenType.String && !((string)uri.Value).StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Embed all model buffers and textures in the .glb; external references cannot be archived reliably: " + uri.Value);
    }
}
