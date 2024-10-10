using UnityEngine;

public class PeriodicBoundary : MonoBehaviour
{
    [Tooltip("Boundary Center coordinates")] public Vector3 boundaryCenter = Vector3.zero;
    [Tooltip("Boundary Width in centimeters.")] public float boundaryWidth = 20f;
    [Tooltip("Boundary Length in centimeters.")] public float boundaryLength = 20f;

    [Tooltip("Moving the boundary with the transform attached to this script.")] public bool moveWithTransform = false;
    [Tooltip("Target Transform to move the boundary with.")] public Transform targetTransform;
    [Tooltip("Rotation angle in degrees around the Y-axis for the boundary area.")] public float boundaryRotation = 0f;

    private Quaternion rotationQuaternion;

    private void Start()
    {
        rotationQuaternion = Quaternion.Euler(0, boundaryRotation, 0);
    }

    private void OnDrawGizmos()
    {
        // Set the color for the boundary visualization
        Gizmos.color = Color.red;
        // Calculate the boundary box dimensions and position
        Vector3 size = new Vector3(boundaryWidth, 1f, boundaryLength);
        Vector3 center = moveWithTransform && targetTransform != null ? targetTransform.position : boundaryCenter;

        // Apply rotation to the gizmo
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(center, Quaternion.Euler(0, boundaryRotation, 0), Vector3.one);
        Gizmos.matrix = rotationMatrix;

        // Draw a wire cube to represent the boundary
        Gizmos.DrawWireCube(Vector3.zero, size);
    }

    public void HandlePeriodicBoundaries(Transform objectTransform)
    {
        Vector3 center = moveWithTransform && targetTransform != null ? targetTransform.position : boundaryCenter;
        Vector3 position = objectTransform.position;

        // Convert position to local space relative to the rotated boundary
        Vector3 localPosition = Quaternion.Inverse(rotationQuaternion) * (position - center);

        float halfWidth = boundaryWidth / 2f;
        float halfLength = boundaryLength / 2f;

        // Check X-axis boundaries
        if (localPosition.x > halfWidth)
            localPosition.x -= boundaryWidth;
        else if (localPosition.x < -halfWidth)
            localPosition.x += boundaryWidth;

        // Check Z-axis boundaries
        if (localPosition.z > halfLength)
            localPosition.z -= boundaryLength;
        else if (localPosition.z < -halfLength)
            localPosition.z += boundaryLength;

        // Convert back to world space
        position = center + (rotationQuaternion * localPosition);
        objectTransform.position = position;
    }

    void Update()
    {
        // Handle periodic boundaries and update the position of the GameObject this script is attached to.
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