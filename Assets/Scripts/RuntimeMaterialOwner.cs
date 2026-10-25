using UnityEngine;

/// <summary>Releases an instance material with the object that uses it, including in-place trial resets.</summary>
public sealed class RuntimeMaterialOwner : MonoBehaviour
{
    private Material owned;
    public static int ActiveMaterialCount { get; private set; }

    public static Material Assign(Renderer renderer, Material source = null)
    {
        if (renderer == null) return null;
        var owner = renderer.GetComponent<RuntimeMaterialOwner>();
        if (owner == null) owner = renderer.gameObject.AddComponent<RuntimeMaterialOwner>();
        owner.Release();
        source = source != null ? source : renderer.sharedMaterial;
        if (source == null) return null;
        owner.owned = new Material(source) { name = source.name + " (owned trial instance)" };
        ActiveMaterialCount++;
        renderer.sharedMaterial = owner.owned;
        return owner.owned;
    }

    private void Release()
    {
        if (owned == null) return;
        Destroy(owned);
        owned = null;
        ActiveMaterialCount--;
    }
    private void OnDestroy() => Release();
}
