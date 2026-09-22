using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Scene defaults: first letter is appearance (r/s), second is navigation (r/s).
[DefaultExecutionOrder(-100)]
public class NavrugSceneSetup : MonoBehaviour
{
    public Terrain terrain;
    public TerrainData ruggedData;
    public bool flatAppearance;
    public bool flatNavigation;
    public float flatHeightCm = 25f;
    private TerrainData flatData;
    private TerrainNavigationSurface surface;
    private bool defaultAppearance, defaultNavigation;
    private float defaultFlatHeight;

    private void Awake()
    {
        defaultAppearance = flatAppearance; defaultNavigation = flatNavigation;
        defaultFlatHeight = flatHeightCm;
        Initialize();
    }

    public void Initialize()
    {
        if (surface != null || terrain == null || ruggedData == null) return;
        surface = terrain.gameObject.AddComponent<TerrainNavigationSurface>();
        surface.ruggedData = ruggedData;
        surface.flatHeightCm = flatHeightCm;
        foreach (var root in gameObject.scene.GetRootGameObjects())
        foreach (var rig in root.GetComponentsInChildren<ClosedLoop>(true))
        {
            var follower = rig.GetComponent<TerrainOrientationUpdater>();
            if (follower == null) follower = rig.gameObject.AddComponent<TerrainOrientationUpdater>();
        }
        foreach (var root in gameObject.scene.GetRootGameObjects())
        foreach (var follower in root.GetComponentsInChildren<TerrainOrientationUpdater>(true))
        {
            follower.navigationSurface = surface;
            follower.verticalOffset = 1f;
            follower.antSize = 0.5f;
            follower.useGravity = true;
            follower.enabled = true;
        }
        SetSurfaces(flatAppearance, flatNavigation);
    }

    public void ApplyConfiguration(Dictionary<string, object> parameters)
    {
        Initialize();
        bool appearance = defaultAppearance, navigation = defaultNavigation;
        float flatHeight = defaultFlatHeight;
        if (parameters != null && parameters.TryGetValue("terrainWorld", out var value) && value != null)
        {
            var config = value as JObject ?? JObject.FromObject(value);
            appearance = config.Value<bool?>("flatAppearance") ?? appearance;
            navigation = config.Value<bool?>("flatNavigation") ?? navigation;
            flatHeight = config.Value<float?>("flatHeightCm") ?? flatHeight;
        }
        if (flatHeight != flatHeightCm)
        {
            flatHeightCm = flatHeight;
            if (flatData != null)
            {
                if (Application.isPlaying) Destroy(flatData); else DestroyImmediate(flatData);
                flatData = null;
            }
        }
        surface.flatHeightCm = flatHeightCm;
        SetSurfaces(appearance, navigation);
    }

    public void SetSurfaces(bool appearance, bool navigation)
    {
        if (surface == null) return;
        flatAppearance = appearance; flatNavigation = navigation;
        if ((appearance || navigation) && flatData == null)
        {
            flatData = Instantiate(ruggedData);
            flatData.name = "Flat appearance (runtime copy)";
            int resolution = flatData.heightmapResolution;
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++) heights[z, x] = flatHeightCm / flatData.size.y;
            flatData.SetHeights(0, 0, heights);
        }
        terrain.terrainData = appearance ? flatData : ruggedData;
        var collider = terrain.GetComponent<TerrainCollider>();
        if (collider != null) collider.terrainData = navigation ? flatData : ruggedData;
        surface.flat = navigation;
    }

    private void OnDestroy()
    {
        if (flatData == null) return;
        if (Application.isPlaying) Destroy(flatData); else DestroyImmediate(flatData);
    }
}
