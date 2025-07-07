using UnityEngine;

public class TerrainOrientationUpdater : MonoBehaviour
{
    [Header("General Settings")]
    [Tooltip("If true, the object will fall (or be moved vertically) until it reaches the terrain.")]
    public bool useGravity = true;
    [Tooltip("Offset above the terrain surface (in units) that the object should maintain.")]
    public float verticalOffset = 0.1f;
    [Tooltip("Simulated size of the ant. A larger value means a larger sampling area to average the terrain normal.")]
    public float antSize = 0.5f;

    [Header("Raycast Settings")]
    [Tooltip("Height above the object from which to start raycasts.")]
    public float raycastOriginHeight = 5f;
    [Tooltip("How many sample points (evenly spaced in a circle) to use for averaging the terrain normal.")]
    public int sampleCount = 5;

    [Header("Orientation Settings")]
    [Tooltip("Speed at which the object rotates to align with the terrain.")]
    public float alignSpeed = 10f;

    void Update()
    {
        Vector3 summedNormals = Vector3.zero;
        int validSamples = 0;
        // Sample the terrain normals in a circle around the object's position.
        for (int i = 0; i < sampleCount; i++)
        {
            float angle = (360f / sampleCount) * i;
            Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0, Mathf.Sin(angle * Mathf.Deg2Rad)) * antSize;
            Vector3 sampleOrigin = transform.position + offset + Vector3.up * raycastOriginHeight;

            if (Physics.Raycast(sampleOrigin, Vector3.down, out RaycastHit hit, raycastOriginHeight * 2f))
            {
                summedNormals += hit.normal;
                validSamples++;
            }
        }

        if (validSamples > 0)
        {
            // Compute the averaged terrain normal.
            Vector3 averageNormal = summedNormals / validSamples;

            // Preserve the current yaw (horizontal orientation).
            Vector3 currentForward = transform.forward;
            Vector3 desiredForward = Vector3.ProjectOnPlane(currentForward, averageNormal).normalized;
            if (desiredForward.sqrMagnitude < 0.001f)
                desiredForward = Vector3.forward;

            Quaternion targetRotation = Quaternion.LookRotation(desiredForward, averageNormal);
            // Update rotation smoothly (or instantly, if alignSpeed is high).
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, alignSpeed * Time.deltaTime);

            // If gravity is enabled, adjust the vertical position.
            if (useGravity)
            {
                // Use a central raycast from above the object to get the terrain height.
                Vector3 centerRayOrigin = transform.position + Vector3.up * raycastOriginHeight;
                if (Physics.Raycast(centerRayOrigin, Vector3.down, out RaycastHit centerHit, raycastOriginHeight * 2f))
                {
                    Vector3 targetPosition = new Vector3(transform.position.x, centerHit.point.y + verticalOffset, transform.position.z);
                    // Adjust vertical position; here Lerp is used for a smooth drop.
                    transform.position = Vector3.Lerp(transform.position, targetPosition, 10f * Time.deltaTime);
                }
            }
        }
    }
}