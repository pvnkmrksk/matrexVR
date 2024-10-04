using UnityEngine;

public class PeriodicBoundary: MonoBehaviour
{
    [Tooltip("Boundary Center coordinates")] public Vector3 boundaryCenter = Vector3.zero;
    [Tooltip("Boundary Width in centimeters.")] public float boundaryWidth = 20f;
    [Tooltip("Boundary Length in centimeters.")] public float boundaryLength = 20f;

    [Tooltip("Moving the boundary with the transform attached to this script.")] public bool moveWithTransform = false;
    [Tooltip("Target Transform to move the boundary with.")] public Transform targetTransform;

    private void OnDrawGizmos()
    {
        // Set the color for the boundary visualization
        Gizmos.color = Color.red;
        // Calculate the boundary box dimensions and position
        Vector3 size = new Vector3(boundaryWidth, 1f, boundaryLength);
        Vector3 center = moveWithTransform && targetTransform != null ? targetTransform.position : boundaryCenter;

        // Draw a wire cube to represent the boundary
        Gizmos.DrawWireCube(center, size);
    }

    public void HandlePeriodicBoundaries(Transform objectTransform)
    {
        Vector3 center = moveWithTransform && targetTransform != null ? targetTransform.position : boundaryCenter;
        Vector3 position = objectTransform.position;

        float halfWidth = boundaryWidth / 2f;
        float halfLength = boundaryLength / 2f;

        // Check X-axis boundaries
        if (position.x > center.x + halfWidth)
            position.x -= boundaryWidth;
        else if (position.x < center.x - halfWidth)
            position.x += boundaryWidth;

        // Check Z-axis boundaries
        if (position.z > center.z + halfLength)
            position.z -= boundaryLength;
        else if (position.z < center.z - halfLength)
            position.z += boundaryLength;

        objectTransform.position = position;
    }

    void Update()
    {
        // Handle periodic boundaries and update the position of the go the script is attached to.
        
        if (targetTransform != null)
        {
            HandlePeriodicBoundaries(targetTransform);            
        }
        else
        {
            HandlePeriodicBoundaries(transform);
        }

    }
}