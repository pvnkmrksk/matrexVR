using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Opaque, unlit circular patch, sized for each camera that actually renders it.</summary>
public sealed class Bogong : MonoBehaviour
{
    private BogongVisualConfig config;
    private Renderer cachedRenderer;
    private float phaseOffset;
    private Material material;
    private bool angular;
    private float tangentHalfAngle;

    public void Configure(BogongVisualConfig visual)
    {
        config = visual ?? new BogongVisualConfig();
        config.Validate();
        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer != null)
        {
            Shader shader = Resources.Load<Shader>("Bogong/Dot");
            if (shader == null || !shader.isSupported) throw new System.InvalidOperationException("Bogong/Dot shader is unavailable.");
            if (material == null) material = new Material(shader) { name = "Solid Bogong dot" };
            material.SetColor("_Color", new Color(config.color.r, config.color.g, config.color.b, 1));
            cachedRenderer.sharedMaterial = material;
            cachedRenderer.shadowCastingMode = ShadowCastingMode.Off;
            cachedRenderer.receiveShadows = false;
            cachedRenderer.lightProbeUsage = LightProbeUsage.Off;
            cachedRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        angular = string.Equals(config.sizeMode, "Angular", System.StringComparison.OrdinalIgnoreCase);
        tangentHalfAngle = Mathf.Tan(.5f * config.angularSizeDegrees * Mathf.Deg2Rad);
        phaseOffset = Random.value;
        Update();
    }

    private void Awake()
    {
        if (config == null) Configure(new BogongVisualConfig());
    }

    private void OnEnable()
    {
        Camera.onPreCull += PrepareForCamera;
        Update();
    }
    private void OnDisable()
    {
        Camera.onPreCull -= PrepareForCamera;
        if (cachedRenderer != null) cachedRenderer.enabled = false;
    }
    private void Update()
    {
        if (cachedRenderer == null || config == null) return;
        cachedRenderer.enabled = config.flickerFrequencyHz <= 0 ||
            Mathf.Repeat((Time.time + phaseOffset) * config.flickerFrequencyHz, 1f) < config.flickerDutyCycle;
    }
    private void PrepareForCamera(Camera camera)
    {
        if (cachedRenderer == null || !cachedRenderer.enabled || material == null ||
            (camera.cullingMask & (1 << gameObject.layer)) == 0) return;
        float radius = angular ? Vector3.Distance(camera.transform.position, transform.position) * tangentHalfAngle : .5f * config.size;
        material.SetFloat("_Radius", radius);
        // The shader expands a padded quad. Set its real world bounds BEFORE culling,
        // including dots straddling cube-face edges, without rotating the movement root.
        cachedRenderer.bounds = new Bounds(transform.position, Vector3.one * Mathf.Max(.0001f, 4.25f * radius));
    }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
