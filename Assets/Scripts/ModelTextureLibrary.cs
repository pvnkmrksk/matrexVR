using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Decode named base-color images once at the experiment or replay loading boundary.</summary>
internal static class ModelTextureLibrary
{
    private static readonly Dictionary<string, Texture2D> runTextures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

    internal static bool IsAppearance(string key) => key == "appearance" || key == "modelAppearance";

    internal static void ValidateReference(string reference)
    {
        string extension = Path.GetExtension(reference);
        if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Texture variants require a PNG or JPEG file: " + reference);
    }

    internal static IEnumerable<string> CollectReferences(JToken document)
    {
        if (document is JObject obj)
        {
            if ((bool?)obj["enabled"] == false) yield break;
            foreach (var property in obj.Properties())
            {
                if (IsAppearance(property.Name) && property.Value is JObject appearance)
                {
                    var settings = appearance.ToObject<ModelAppearanceConfig>();
                    settings.ValidateTextureVariants();
                    foreach (var variant in settings.textureVariants ?? Array.Empty<ModelTextureVariantConfig>())
                        yield return variant.texturePath;
                }
                else foreach (string reference in CollectReferences(property.Value)) yield return reference;
            }
        }
        else if (document is JArray array)
            foreach (var child in array) foreach (string reference in CollectReferences(child)) yield return reference;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void ClearRun() => Clear(runTextures);

    internal static void PreloadRun(string reference, byte[] bytes) => runTextures.Add(reference, Decode(reference, bytes));

    internal static Texture2D Prepared(string reference)
    {
        if (ReplayExecutionContext.Active) return ReplayExecutionContext.Texture(reference);
        if (runTextures.TryGetValue(reference, out var texture) && texture != null) return texture;
        throw new InvalidOperationException("Texture variant was not preloaded before the experiment: " + reference);
    }

    internal static Texture2D Decode(string reference, byte[] bytes)
    {
        ValidateReference(reference);
        // Preserve sRGB base-color sampling. Upload mipmaps and release CPU pixel storage.
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, false)
            { name = Path.GetFileName(reference), filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave };
        try
        {
            if (!texture.LoadImage(bytes, markNonReadable: true))
                throw new InvalidDataException("Cannot decode texture variant as PNG or JPEG: " + reference);
            return texture;
        }
        catch { Release(texture); throw; }
    }

    internal static void Clear(Dictionary<string, Texture2D> textures)
    {
        foreach (var texture in textures.Values) Release(texture);
        textures.Clear();
    }

    internal static void Release(Texture2D texture)
    {
        if (texture == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
        else UnityEngine.Object.DestroyImmediate(texture);
    }
}
