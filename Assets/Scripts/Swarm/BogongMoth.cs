using UnityEngine;

/// <summary>Reusable procedural moth asset. Save this root as a prefab; geometry is rebuilt on enable.</summary>
[ExecuteAlways]
public sealed class BogongMoth : MonoBehaviour
{
    public BogongVisualConfig visual = new BogongVisualConfig { shape = "Moth" };
    public float worldUnitsPerMetre = 1f;
    private GameObject generated;
    private void OnEnable() => Rebuild();
    [ContextMenu("Rebuild moth visual")]
    public void Rebuild()
    {
        OnDisable();
        visual.shape = "Moth";
        generated = new GameObject("Generated moth mesh") { hideFlags = HideFlags.DontSave };
        generated.layer = gameObject.layer; generated.transform.SetParent(transform, false);
        generated.AddComponent<BogongSwarmRenderer>().Configure(new[] { gameObject }, visual, worldUnitsPerMetre);
    }
    private void OnDisable()
    {
        if (generated == null) return;
        generated.SetActive(false);
        if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated);
        generated = null;
    }
}
