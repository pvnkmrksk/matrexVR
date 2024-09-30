using UnityEngine;

public class PeriodicBoundary: MonoBehaviour
{
    public Vector3 boundaryCenter = Vector3.zero;
    public float boundaryWidth = 20f;
    public float boundaryDepth = 20f;

    public bool moveWithTransform = false;
    public Transform targetTransform;

    public void HandlePeriodicBoundaries(Transform objectTransform)
    {
        Vector3 center = moveWithTransform && targetTransform != null ? targetTransform.position : boundaryCenter;
        Vector3 position = objectTransform.position;

        float halfWidth = boundaryWidth / 2f;
        float halfDepth = boundaryDepth / 2f;

        // Check X-axis boundaries
        if (position.x > center.x + halfWidth)
            position.x -= boundaryWidth;
        else if (position.x < center.x - halfWidth)
            position.x += boundaryWidth;

        // Check Z-axis boundaries
        if (position.z > center.z + halfDepth)
            position.z -= boundaryDepth;
        else if (position.z < center.z - halfDepth)
            position.z += boundaryDepth;

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