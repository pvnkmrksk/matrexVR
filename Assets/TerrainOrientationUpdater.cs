using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Runs after input and periodic wrapping. One Unity unit is one centimeter.
[DefaultExecutionOrder(1000)]
public class TerrainOrientationUpdater : MonoBehaviour
{
    public enum GroundConformanceMode { HeightOnly, PerpendicularToNormal }
    public enum HeightMode { AboveGround, Absolute }
    public bool useGravity = true;
    public GroundConformanceMode conformanceMode;
    public HeightMode heightMode;
    public float verticalOffset = 1f;
    public float absoluteHeight = 1f;
    public float antSize = 0.5f;
    public float raycastOriginHeight = 1000f;
    public float raycastDistance = 2000f;
    public int sampleCount = 5;
    public LayerMask groundLayers = Physics.DefaultRaycastLayers;
    public float alignSpeed = 12f;
    [Tooltip("Explicit surface avoids hitting trees, props or the visible mesh.")]
    public TerrainNavigationSurface navigationSurface;
    private Vector3 lastValidPosition;
    private bool hasValidPosition;
    private JObject sceneDefaults;
    private bool configured;

    private void Start()
    {
        CaptureDefaults();
        if (!configured) ApplyConfiguration(MainController.Instance?.GetCurrentSequenceStep()?.parameters);
        Conform(0f, true);
    }

    private void CaptureDefaults()
    {
        if (sceneDefaults != null) return;
        sceneDefaults = JObject.FromObject(new TerrainNavigationConfig {
            heightMode = heightMode, orientationMode = conformanceMode,
            heightAboveGroundCm = verticalOffset, absoluteHeightCm = absoluteHeight,
            normalSampleRadiusCm = antSize, alignmentSpeed = alignSpeed
        });
    }

    public void ApplyConfiguration(Dictionary<string, object> parameters)
    {
        CaptureDefaults();
        var settings = (JObject)sceneDefaults.DeepClone();
        var system = MainController.Instance?.GetSystemConfigForGameObject(gameObject).terrainNavigation;
        if (system != null) settings.Merge(system);
        if (parameters != null && parameters.TryGetValue("terrainNavigation", out var value) && value != null)
            settings.Merge(value as JObject ?? JObject.FromObject(value));
        var config = settings.ToObject<TerrainNavigationConfig>();
        heightMode = config.heightMode;
        conformanceMode = config.orientationMode;
        verticalOffset = Mathf.Max(0, config.heightAboveGroundCm);
        absoluteHeight = config.absoluteHeightCm;
        antSize = Mathf.Max(0, config.normalSampleRadiusCm);
        alignSpeed = Mathf.Max(0, config.alignmentSpeed);
        hasValidPosition = false;
        configured = true;
    }

    private void LateUpdate() { Conform(Time.deltaTime); }

    public bool Conform(float deltaTime, bool snap = false)
    {
        Vector3 position = transform.position;
        if (!Sample(position, out var ground, out var normal))
        {
            // Reject movement outside the navigation footprint (including holes).
            if (hasValidPosition) transform.position = lastValidPosition;
            return false;
        }
        if (useGravity)
            position.y = heightMode == HeightMode.AboveGround ? ground + verticalOffset : absoluteHeight;
        transform.position = position;
        lastValidPosition = position;
        hasValidPosition = true;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        Vector3 up = Vector3.up;
        if (conformanceMode == GroundConformanceMode.PerpendicularToNormal)
        {
            Vector3 sum = normal;
            int samples = Mathf.Clamp(sampleCount, 1, 32);
            for (int i = 0; i < samples; i++)
            {
                float angle = 2f * Mathf.PI * i / samples;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * antSize;
                if (Sample(position + offset, out _, out var n)) sum += n;
            }
            up = sum.normalized;
            // Preserve yaw exactly; projecting onto the slope would introduce heading drift.
            forward.y = -(up.x * forward.x + up.z * forward.z) / Mathf.Max(up.y, 0.0001f);
        }
        Quaternion target = Quaternion.LookRotation(forward, up);
        transform.rotation = snap || alignSpeed == 0 || conformanceMode == GroundConformanceMode.HeightOnly
            ? target : Quaternion.Slerp(transform.rotation, target, 1 - Mathf.Exp(-alignSpeed * deltaTime));
        return true;
    }

    private bool Sample(Vector3 position, out float height, out Vector3 normal)
    {
        if (navigationSurface != null) return navigationSurface.Sample(position, out height, out normal);
        if (Physics.Raycast(position + Vector3.up * raycastOriginHeight, Vector3.down, out var hit,
            raycastOriginHeight + raycastDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            height = hit.point.y;
            normal = hit.normal;
            return true;
        }
        height = 0; normal = Vector3.up; return false;
    }
}

[System.Serializable]
public class TerrainNavigationConfig
{
    public TerrainOrientationUpdater.HeightMode heightMode;
    public TerrainOrientationUpdater.GroundConformanceMode orientationMode;
    public float heightAboveGroundCm = 1f;
    public float absoluteHeightCm = 1f;
    public float normalSampleRadiusCm = 0.5f;
    public float alignmentSpeed = 12f;
}
