using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One draw per swarm/camera; centers move on the CPU, camera sizing stays on the GPU.</summary>
[DefaultExecutionOrder(1000)]
[ExecuteAlways]
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
    private bool moth;
    private int stride;
    private Vector4[] rotations;
    private Vector2[] phases;
    public int AgentCount => agents?.Count ?? 0;

    public void Configure(IReadOnlyList<GameObject> source, BogongVisualConfig config, float worldUnitsPerMetre = 100f)
    {
        config = config ?? new BogongVisualConfig();
        config.Validate(); agents = source;
        moth = string.Equals(config.shape, "Moth", System.StringComparison.OrdinalIgnoreCase);
        Shader shader = Resources.Load<Shader>(moth ? "Bogong/BatchedMoth" : "Bogong/BatchedDot");
        if (shader == null || !shader.isSupported) throw new System.InvalidOperationException("Bogong batch shader is unavailable: " + config.shape);
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
        if (moth)
        {
            material.SetFloat("_BodyLength", config.bodyLengthMetres * worldUnitsPerMetre);
            material.SetFloat("_BodyDiameter", config.bodyDiameterMetres * worldUnitsPerMetre);
            material.SetFloat("_WingSpan", config.wingSpanMetres * worldUnitsPerMetre);
            material.SetFloat("_FlapFrequency", config.flapFrequencyHz);
            material.SetFloat("_WingOpacity", config.wingOpacity);
            material.SetFloat("_Subpixel", config.subpixelVisibility ? 1 : 0);
            worldRadius = Mathf.Max(config.bodyLengthMetres, config.wingSpanMetres) * worldUnitsPerMetre / 2f;
        }
        Resize(agents.Count);
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        RefreshCenters();
    }

    private void Resize(int count)
    {
        Mesh previous = mesh;
        Vector2[] previousPhases = phases;
        mesh = moth ? BogongMothMesh.Create(count) : new Mesh { name = "Bogong swarm centers", indexFormat = IndexFormat.UInt32 };
        mesh.MarkDynamic();
        stride = moth ? BogongMothMesh.VerticesPerAgent : 4;
        centers = new Vector3[count * stride];
        phases = new Vector2[count * stride];
        if (moth) rotations = new Vector4[centers.Length];
        var uv = new Vector2[count * 4];
        var triangles = new int[count * 6];
        for (int i = 0; i < count; i++)
        {
            int v = i * 4, t = i * 6;
            uv[v] = new Vector2(0, 0); uv[v + 1] = new Vector2(1, 0);
            uv[v + 2] = new Vector2(1, 1); uv[v + 3] = new Vector2(0, 1);
            float phase = previousPhases != null && i * stride < previousPhases.Length ? previousPhases[i * stride].x : Random.value;
            for (int j = 0; j < stride; j++) phases[i * stride + j] = new Vector2(phase, 0);
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        if (!moth) { mesh.vertices = centers; mesh.uv = uv; mesh.triangles = triangles; }
        mesh.uv2 = phases;
        if (previous != null)
        {
            GetComponent<MeshFilter>().sharedMesh = mesh;
            if (Application.isPlaying) Destroy(previous); else DestroyImmediate(previous);
        }
    }

    private void OnEnable() => Camera.onPreCull += PrepareForCamera;
    private void OnDisable() => Camera.onPreCull -= PrepareForCamera;
    private void LateUpdate() => RefreshCenters();

    public void RefreshCenters()
    {
        if (mesh == null || agents.Count == 0) return;
        if (agents.Count * stride > centers.Length) Resize(Mathf.Max(agents.Count, centers.Length / stride * 2));
        bool any = false;
        for (int i = 0; i < agents.Count; i++)
        {
            Vector3 position = agents[i].transform.position;
            bool active = agents[i].activeInHierarchy;
            if (active) { if (!any) centerBounds = new Bounds(position, Vector3.zero); else centerBounds.Encapsulate(position); any = true; }
            Quaternion q = agents[i].transform.rotation;
            for (int j = 0; j < stride; j++)
            {
                int v = i * stride + j;
                centers[v] = position; phases[v].y = active ? 1 : 0;
                if (moth) rotations[v] = new Vector4(q.x, q.y, q.z, q.w);
            }
        }
        // Positions are world coordinates: the shader ignores the swarm host's transform.
        mesh.SetVertices(centers);
        mesh.uv2 = phases;
        if (moth) mesh.SetTangents(rotations);
        meshRenderer.enabled = any;
        meshRenderer.bounds = centerBounds;
    }

    private void PrepareForCamera(Camera camera)
    {
        if (meshRenderer == null || (camera.cullingMask & (1 << gameObject.layer)) == 0) return;
        if (moth) material.SetVector("_PanelPixels", new Vector4(camera.pixelWidth, camera.pixelHeight, 0, 0));
        // A conservative bound for the padded circles, including dots at cube-face seams.
        float radius = angular ? (Vector3.Distance(camera.transform.position, centerBounds.center) +
                                  centerBounds.extents.magnitude) * tangentHalfAngle : worldRadius;
        var bounds = centerBounds;
        // Include the proxy's pixel-sized support as well as physical wings.
        if (moth)
        {
            float depth = Vector3.Distance(camera.transform.position, centerBounds.center) + centerBounds.extents.magnitude;
            float pixel = camera.orthographic ? 2f * camera.orthographicSize / Mathf.Max(1, camera.pixelHeight)
                : 2f * depth * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) / Mathf.Max(1, camera.pixelHeight);
            radius = Mathf.Max(radius, 3f * pixel);
        }
        bounds.Expand(Mathf.Max(.0001f, 4.25f * radius));
        meshRenderer.bounds = bounds;
    }

    private void OnDestroy()
    {
        if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
    }
}
