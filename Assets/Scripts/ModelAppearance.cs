using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public sealed class ModelTextureVariantConfig
{
    public string name;
    public string texturePath;
}

[Serializable]
public sealed class ModelAppearanceConfig
{
    [Tooltip("Override this object's surface without changing the original model file.")]
    public bool enabled;
    public ColorConfig color = new ColorConfig { r = 1, g = 1, b = 1, a = 1 };
    [Tooltip("Keep the model's base-color texture. Disable for a uniform chosen color.")]
    public bool keepTexture = true;
    public float metallic;
    public float smoothness = .5f;
    [Tooltip("Named replacement base-color images for this model's existing UVs. Use PNG or JPEG.")]
    public ModelTextureVariantConfig[] textureVariants;
    [Tooltip("Select a named base-color texture. An empty selection retains the original texture settings.")]
    public string selectedTextureVariant = "";

    [JsonIgnore] internal bool HasSelectedTexture => !string.IsNullOrEmpty(selectedTextureVariant);

    internal string SelectedTexturePath()
    {
        if (!HasSelectedTexture) return null;
        foreach (var variant in textureVariants ?? Array.Empty<ModelTextureVariantConfig>())
            if (string.Equals(variant?.name, selectedTextureVariant, StringComparison.OrdinalIgnoreCase))
                return variant.texturePath;
        throw new ArgumentException("Unknown selected texture variant: " + selectedTextureVariant);
    }

    internal void ValidateTextureVariants()
    {
        if (textureVariants == null || textureVariants.Length == 0) return;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variant in textureVariants)
        {
            if (variant == null || string.IsNullOrWhiteSpace(variant.name) || variant.name != variant.name.Trim() || !names.Add(variant.name))
                throw new ArgumentException("Texture variants need unique names without leading or trailing whitespace.");
            ModelTextureLibrary.ValidateReference(variant.texturePath);
        }
    }

    public void Validate()
    {
        ValidateTextureVariants();
        SelectedTexturePath();
        if (!enabled) return;
        if (color == null) throw new ArgumentException("Choose a model color before enabling its appearance override.");
        foreach (float value in new[] { color.r, color.g, color.b, color.a, metallic, smoothness })
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1)
                throw new ArgumentException("Model color, metallic and smoothness values must be between zero and one.");
    }
}

/// <summary>Instance-owned surface overrides shared by worlds, previews and canonical replay.</summary>
[ExecuteAlways]
public sealed class ModelAppearance : MonoBehaviour
{
    private Renderer[] renderers;
    private Material[][] originals, copies;
    private Texture2D previewTexture;
    private static readonly string[] BaseColorTextures = { "_MainTex", "_BaseMap", "_BaseColorTexture", "baseColorTexture" };
    public static int ActiveMaterialCount { get; private set; }

    public static void Apply(GameObject instance, ModelAppearanceConfig settings, bool preview = false)
    {
        if (instance == null) return;
        var owner = instance.GetComponent<ModelAppearance>();
        if (settings == null || (!settings.enabled && !settings.HasSelectedTexture))
        {
            if (owner != null) owner.Release();
            return;
        }
        settings.Validate();
        string reference = settings.SelectedTexturePath();
        bool ownsTexture = reference != null && (preview || !Application.isPlaying) && !ReplayExecutionContext.Active;
        Texture2D replacement = reference == null ? null : ownsTexture
            ? ModelTextureLibrary.Decode(reference, File.ReadAllBytes(ExperimentConfigFiles.Resolve(reference)))
            : ModelTextureLibrary.Prepared(reference);
        try
        {
            if (owner == null) owner = instance.AddComponent<ModelAppearance>();
            owner.Configure(settings, replacement);
            if (ownsTexture) owner.previewTexture = replacement;
        }
        catch
        {
            if (ownsTexture) ModelTextureLibrary.Release(replacement);
            throw;
        }
    }

    private void Configure(ModelAppearanceConfig settings, Texture2D replacement)
    {
        Release();
        renderers = GetComponentsInChildren<Renderer>(true);
        originals = new Material[renderers.Length][]; copies = new Material[renderers.Length][];
        bool appliedTexture = false;
        try
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                originals[i] = renderers[i].sharedMaterials;
                copies[i] = new Material[originals[i].Length];
                for (int slot = 0; slot < copies[i].Length; slot++)
                {
                    var source = originals[i][slot]; if (source == null) continue;
                    var material = CopyMaterial(source);
                    copies[i][slot] = material;
                    appliedTexture |= ApplyMaterialSettings(material, settings, replacement);
                }
                renderers[i].sharedMaterials = copies[i];
            }
            if (replacement != null && !appliedTexture)
                throw new InvalidOperationException("This model has no supported base-color texture slot.");
        }
        catch { Release(); throw; }
    }

    internal static Material CreateSharedTextureMaterial(Material source, ModelAppearanceConfig settings)
    {
        settings.Validate();
        var replacement = ModelTextureLibrary.Prepared(settings.SelectedTexturePath());
        var material = CopyMaterial(source);
        try
        {
            if (!ApplyMaterialSettings(material, settings, replacement))
                throw new InvalidOperationException("This model has no supported base-color texture slot.");
            return material;
        }
        catch { ReleaseMaterial(material); throw; }
    }

    private static Material CopyMaterial(Material source)
    {
        var material = new Material(source) { name = source.name + " (object appearance)" };
        ActiveMaterialCount++;
        return material;
    }

    private static bool ApplyMaterialSettings(Material material, ModelAppearanceConfig settings, Texture2D replacement)
    {
        if (settings.enabled)
        {
            Color color = new Color(settings.color.r, settings.color.g, settings.color.b, settings.color.a);
            foreach (string property in new[] { "_Color", "_BaseColor", "_BaseColorFactor", "baseColorFactor" })
                if (material.HasProperty(property)) material.SetColor(property, color);
            foreach (string property in new[] { "_Metallic", "_MetallicFactor", "metallicFactor" })
                if (material.HasProperty(property)) material.SetFloat(property, settings.metallic);
            foreach (string property in new[] { "_Glossiness", "_Smoothness", "_GlossMapScale" })
                if (material.HasProperty(property)) material.SetFloat(property, settings.smoothness);
            foreach (string property in new[] { "_Roughness", "_RoughnessFactor", "roughnessFactor" })
                if (material.HasProperty(property)) material.SetFloat(property, 1 - settings.smoothness);
        }
        bool appliedTexture = false;
        if (replacement != null || (settings.enabled && !settings.keepTexture))
            foreach (string property in BaseColorTextures)
                if (material.HasProperty(property))
                {
                    material.SetTexture(property, replacement);
                    appliedTexture = true;
                }
        return appliedTexture;
    }

    internal static void ReleaseMaterial(Material material)
    {
        if (material == null) return;
        if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        ActiveMaterialCount--;
    }

    private void Release()
    {
        if (previewTexture != null) ModelTextureLibrary.Release(previewTexture);
        previewTexture = null;
        if (copies == null) return;
        for (int i = 0; i < copies.Length; i++)
        {
            if (renderers[i] != null && originals[i] != null) renderers[i].sharedMaterials = originals[i];
            foreach (var material in copies[i] ?? Array.Empty<Material>()) ReleaseMaterial(material);
        }
        copies = null; originals = null; renderers = null;
    }
    private void OnDestroy() => Release();
}
