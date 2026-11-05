using System;
using UnityEngine;

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

    public void Validate()
    {
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
    public static int ActiveMaterialCount { get; private set; }

    public static void Apply(GameObject instance, ModelAppearanceConfig settings)
    {
        if (instance == null) return;
        var owner = instance.GetComponent<ModelAppearance>();
        if (settings?.enabled != true) { if (owner != null) owner.Release(); return; }
        settings.Validate();
        if (owner == null) owner = instance.AddComponent<ModelAppearance>();
        owner.Configure(settings);
    }

    private void Configure(ModelAppearanceConfig settings)
    {
        Release();
        renderers = GetComponentsInChildren<Renderer>(true);
        originals = new Material[renderers.Length][]; copies = new Material[renderers.Length][];
        try
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                originals[i] = renderers[i].sharedMaterials;
                copies[i] = new Material[originals[i].Length];
                for (int slot = 0; slot < copies[i].Length; slot++)
                {
                    var source = originals[i][slot]; if (source == null) continue;
                    var material = new Material(source) { name = source.name + " (object appearance)" };
                    copies[i][slot] = material; ActiveMaterialCount++;
                    Color color = new Color(settings.color.r, settings.color.g, settings.color.b, settings.color.a);
                    foreach (string property in new[] { "_Color", "_BaseColor", "_BaseColorFactor", "baseColorFactor" })
                        if (material.HasProperty(property)) material.SetColor(property, color);
                    foreach (string property in new[] { "_Metallic", "_MetallicFactor", "metallicFactor" })
                        if (material.HasProperty(property)) material.SetFloat(property, settings.metallic);
                    foreach (string property in new[] { "_Glossiness", "_Smoothness", "_GlossMapScale" })
                        if (material.HasProperty(property)) material.SetFloat(property, settings.smoothness);
                    foreach (string property in new[] { "_Roughness", "_RoughnessFactor", "roughnessFactor" })
                        if (material.HasProperty(property)) material.SetFloat(property, 1 - settings.smoothness);
                    if (!settings.keepTexture)
                        foreach (string property in new[] { "_MainTex", "_BaseMap", "_BaseColorTexture", "baseColorTexture" })
                            if (material.HasProperty(property)) material.SetTexture(property, null);
                }
                renderers[i].sharedMaterials = copies[i];
            }
        }
        catch { Release(); throw; }
    }

    private void Release()
    {
        if (copies == null) return;
        for (int i = 0; i < copies.Length; i++)
        {
            if (renderers[i] != null && originals[i] != null) renderers[i].sharedMaterials = originals[i];
            foreach (var material in copies[i] ?? Array.Empty<Material>())
                if (material != null)
                {
                    if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
                    ActiveMaterialCount--;
                }
        }
        copies = null; originals = null; renderers = null;
    }
    private void OnDestroy() => Release();
}
