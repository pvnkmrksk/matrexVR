using System;
using UnityEngine;

/// <summary>One shared, opaque black material for the stock rig, including replay and native fallback.</summary>
public static class StockLocustAppearance
{
    private static Material black;
    internal static Material BlackMaterial => black != null ? black : black = Resources.Load<Material>("NativeLocust/FlatBlack");

    internal static Material TryCreateSharedVariant(GameObject prefab, ModelAppearanceConfig settings, out Material source)
    {
        source = null;
        if (!Application.isPlaying || prefab == null || settings?.HasSelectedTexture != true) return null;
        var catalog = Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog");
        if (catalog == null || prefab != catalog.Find("SimulatedLocust")) return null;
        source = SingleSkinMaterial(prefab, out _);
        return source != null ? ModelAppearance.CreateSharedTextureMaterial(source, settings) : null;
    }

    internal static bool ApplySharedVariant(GameObject agent, Material source, Material variant)
    {
        if (variant == null || source == null || SingleSkinMaterial(agent, out var skin) != source) return false;
        skin.sharedMaterial = variant;
        return true;
    }

    // The stock shader only consumes one skin and one material. Keep the
    // ordinary appearance path if the prefab or an instance has extra slots.
    private static Material SingleSkinMaterial(GameObject root, out SkinnedMeshRenderer skin)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        skin = renderers.Length == 1 ? renderers[0] as SkinnedMeshRenderer : null;
        return skin != null && skin.sharedMaterials.Length == 1 ? skin.sharedMaterial : null;
    }

    internal static void ApplyBlack(GameObject agent)
    {
        var skin = agent.GetComponentInChildren<SkinnedMeshRenderer>(true);
        Material material = BlackMaterial;
        if (skin == null || material == null)
            throw new InvalidOperationException("Black stock locusts require the stock skin and shared black material.");
        skin.sharedMaterial = material;
    }
}
