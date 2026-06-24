using UnityEngine;

public class TerrainOrientationUpdater : MonoBehaviour
{
    public enum GroundConformanceMode
    {
        HeightOnly,
        PerpendicularToNormal
    }

    [Header("General Settings")]
    [Tooltip("If true, keep the object on the ground/terrain below it.")]
    public bool useGravity = true;
    [Tooltip("Choose whether to only correct height or also align to the surface normal.")]
    public GroundConformanceMode conformanceMode = GroundConformanceMode.HeightOnly;
    [Tooltip("Offset above the terrain surface (in units) that the object should maintain.")]
    public float verticalOffset = 0.01f;
    [Tooltip("Sampling radius used when averaging terrain normals.")]
    public float antSize = 0.015f;

    [Header("Raycast Settings")]
    [Tooltip("Height above the object from which to start raycasts.")]
    public float raycastOriginHeight = 1f;
    [Tooltip("Maximum downward distance below the object's current position to search for ground.")]
    public float raycastDistance = 5f;
    [Tooltip("How many sample points to use when averaging the terrain normal.")]
    public int sampleCount = 5;
    [Tooltip("Which layers count as ground.")]
    public LayerMask groundLayers = Physics.DefaultRaycastLayers;

    [Header("Orientation Settings")]
    [Tooltip("Speed at which the object rotates to align with the terrain.")]
    public float alignSpeed = 10f;

    void LateUpdate()
    {
        Vector3 centerOrigin = transform.position + Vector3.up * raycastOriginHeight;
        float totalRayDistance = raycastOriginHeight + raycastDistance;
        if (!Physics.Raycast(centerOrigin, Vector3.down, out RaycastHit centerHit, totalRayDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        if (useGravity)
        {
            Vector3 position = transform.position;
            position.y = centerHit.point.y + verticalOffset;
            transform.position = position;
        }

        if (conformanceMode != GroundConformanceMode.PerpendicularToNormal)
        {
            return;
        }

        Vector3 averageNormal = GetAverageNormal();
        if (averageNormal.sqrMagnitude < 0.0001f)
        {
            averageNormal = centerHit.normal;
        }

        Vector3 desiredForward = Vector3.ProjectOnPlane(transform.forward, averageNormal).normalized;
        if (desiredForward.sqrMagnitude < 0.001f)
        {
            desiredForward = Vector3.ProjectOnPlane(Vector3.forward, averageNormal).normalized;
        }

        if (desiredForward.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(desiredForward, averageNormal);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, alignSpeed * Time.deltaTime);
    }

    private Vector3 GetAverageNormal()
    {
        Vector3 summedNormals = Vector3.zero;
        int validSamples = 0;
        int samples = Mathf.Max(1, sampleCount);

        for (int i = 0; i < samples; i++)
        {
            float angle = (360f / samples) * i * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * antSize;
            Vector3 sampleOrigin = transform.position + offset + Vector3.up * raycastOriginHeight;

            if (Physics.Raycast(sampleOrigin, Vector3.down, out RaycastHit hit, raycastOriginHeight + raycastDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                summedNormals += hit.normal;
                validSamples++;
            }
        }

        if (validSamples == 0)
        {
            return Vector3.zero;
        }

        return (summedNormals / validSamples).normalized;
    }
}
