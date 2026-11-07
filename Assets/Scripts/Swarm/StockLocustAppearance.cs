using System;
using UnityEngine;

/// <summary>One shared, opaque black material for the stock rig, including replay and native fallback.</summary>
public static class StockLocustAppearance
{
    private static Material black;
    internal static Material BlackMaterial => black != null ? black : black = Resources.Load<Material>("NativeLocust/FlatBlack");

    internal static void ApplyBlack(GameObject agent)
    {
        var skin = agent.GetComponentInChildren<SkinnedMeshRenderer>(true);
        Material material = BlackMaterial;
        if (skin == null || material == null)
            throw new InvalidOperationException("Black stock locusts require the stock skin and shared black material.");
        skin.sharedMaterial = material;
    }
}
