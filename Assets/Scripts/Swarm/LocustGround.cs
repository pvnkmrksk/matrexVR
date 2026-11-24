using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(MeshRenderer))]
public sealed class LocustGround : MonoBehaviour
{
    private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
    private static readonly int TextureTransform = Shader.PropertyToID("_MainTex_ST");

    private MeshRenderer surface;
    private Material authoredMaterial;
    private MaterialPropertyBlock authoredProperties;
    private MaterialPropertyBlock overrides;

    internal static void Apply(Scene scene, LocustGroundConfig settings)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var ground = root.GetComponent<LocustGround>();
            if (ground != null) ground.Apply(settings);
        }
    }

    private void Apply(LocustGroundConfig settings)
    {
        if (surface == null)
        {
            if (settings == null) return;
            surface = GetComponent<MeshRenderer>();
            authoredMaterial = surface.sharedMaterial;
            authoredProperties = new MaterialPropertyBlock();
            surface.GetPropertyBlock(authoredProperties);
            overrides = new MaterialPropertyBlock();
        }
        if (settings == null || (settings.texture == LocustGroundTexture.SceneDefault &&
            !settings.tilingX.HasValue && !settings.tilingY.HasValue))
        {
            surface.sharedMaterial = authoredMaterial;
            surface.SetPropertyBlock(authoredProperties);
            return;
        }

        settings.Validate();
        bool custom = settings.texture == LocustGroundTexture.Custom;
        Material material = settings.texture == LocustGroundTexture.SceneDefault || custom
            ? authoredMaterial : Resources.Load<Material>("LocustGround/" + settings.texture);
        if (material == null || !material.HasProperty(MainTexture))
            throw new InvalidOperationException("Ground material has no base texture for " + settings.texture + ".");

        Texture2D texture = custom ? ModelTextureLibrary.Prepared(settings.groundTexturePath) : null;
        Vector2 scale = material.mainTextureScale;
        Vector2 offset = material.mainTextureOffset;
        surface.sharedMaterial = material;
        surface.SetPropertyBlock(authoredProperties);
        if (custom || settings.tilingX.HasValue || settings.tilingY.HasValue)
        {
            surface.GetPropertyBlock(overrides);
            if (custom) overrides.SetTexture(MainTexture, texture);
            overrides.SetVector(TextureTransform, new Vector4(settings.tilingX ?? scale.x, settings.tilingY ?? scale.y, offset.x, offset.y));
            surface.SetPropertyBlock(overrides);
        }
    }
}
