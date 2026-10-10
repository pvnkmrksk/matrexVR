using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One draw per swarm/camera; centers move on the CPU, camera sizing stays on the GPU.</summary>
[DefaultExecutionOrder(1000)]
public sealed class BogongSwarmRenderer : MonoBehaviour
{
    private IReadOnlyList<GameObject> agents;
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private Material material;
    private Vector3[] centers;
    private Bounds centerBounds;
    private float tangentHalfAngle, worldRadius;
    private bool angular;
    public int AgentCount => agents?.Count ?? 0;

    public void Configure(IReadOnlyList<GameObject> source, BogongVisualConfig config)
    {
        config = config ?? new BogongVisualConfig();
        config.Validate(); agents = source;
        Shader shader = Resources.Load<Shader>("Bogong/BatchedDot");
        if (shader == null || !shader.isSupported) throw new System.InvalidOperationException("Bogong/BatchedDot shader is unavailable.");
        material = new Material(shader) { name = "Batched solid Bogong swarm" };
        angular = string.Equals(config.sizeMode, "Angular", System.StringComparison.OrdinalIgnoreCase);
        tangentHalfAngle = Mathf.Tan(.5f * config.angularSizeDegrees * Mathf.Deg2Rad);
        worldRadius = .5f * config.size;
        material.SetColor("_Color", new Color(config.color.r, config.color.g, config.color.b, 1));
        material.SetFloat("_Angular", angular ? 1 : 0);
        material.SetFloat("_TangentHalfAngle", tangentHalfAngle);
        material.SetFloat("_Radius", worldRadius);
        material.SetFloat("_FlickerFrequency", config.flickerFrequencyHz);
        material.SetFloat("_FlickerDuty", config.flickerDutyCycle);
        mesh = new Mesh { name = "Bogong swarm centers", indexFormat = IndexFormat.UInt32 };
        mesh.MarkDynamic();
        int count = agents.Count;
        centers = new Vector3[count * 4];
        var uv = new Vector2[count * 4];
        var phases = new Vector2[count * 4];
        var triangles = new int[count * 6];
        for (int i = 0; i < count; i++)
        {
            int v = i * 4, t = i * 6;
            uv[v] = new Vector2(0, 0); uv[v + 1] = new Vector2(1, 0);
            uv[v + 2] = new Vector2(1, 1); uv[v + 3] = new Vector2(0, 1);
            float phase = Random.value;
            for (int j = 0; j < 4; j++) phases[v + j] = new Vector2(phase, 0);
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        mesh.vertices = centers; mesh.uv = uv; mesh.uv2 = phases; mesh.triangles = triangles;
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        RefreshCenters();
    }

    private void OnEnable() => Camera.onPreCull += PrepareForCamera;
    private void OnDisable() => Camera.onPreCull -= PrepareForCamera;
    private void LateUpdate() => RefreshCenters();

    public void RefreshCenters()
    {
        if (mesh == null || agents.Count == 0) return;
        centerBounds = new Bounds(agents[0].transform.position, Vector3.zero);
        for (int i = 0; i < agents.Count; i++)
        {
            Vector3 position = agents[i].transform.position;
            centerBounds.Encapsulate(position);
            int v = i * 4;
            centers[v] = centers[v + 1] = centers[v + 2] = centers[v + 3] = position;
        }
        // Positions are world coordinates: the shader ignores the swarm host's transform.
        mesh.SetVertices(centers);
        meshRenderer.bounds = centerBounds;
    }

    private void PrepareForCamera(Camera camera)
    {
        if (meshRenderer == null || (camera.cullingMask & (1 << gameObject.layer)) == 0) return;
        // A conservative bound for the padded circles, including dots at cube-face seams.
        float radius = angular ? (Vector3.Distance(camera.transform.position, centerBounds.center) +
                                  centerBounds.extents.magnitude) * tangentHalfAngle : worldRadius;
        var bounds = centerBounds;
        bounds.Expand(Mathf.Max(.0001f, 4.25f * radius));
        meshRenderer.bounds = bounds;
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
