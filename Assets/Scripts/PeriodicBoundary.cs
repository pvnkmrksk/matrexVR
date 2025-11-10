using UnityEngine;

public class PeriodicBoundary : MonoBehaviour
{
    [Tooltip("Boundary Center coordinates")] public Vector3 boundaryCenter = Vector3.zero;
    [Tooltip("Boundary Width in centimeters.")] public float boundaryLengthX = 20f;
    [Tooltip("Boundary Length in centimeters.")] public float boundaryLengthZ = 20f;

    [Tooltip("Moving the boundary with the transform attached to this script.")] public bool moveWithTransform = false;
    [Tooltip("Target Transform to move the boundary with.")] public Transform targetTransform;
    [Tooltip("Rotation angle in degrees around the Y-axis for the boundary area.")] public float boundaryRotation = 0f;

    private Quaternion rotationQuaternion;

    private void Start()
    {
        UpdateRotationQuaternion();
    }
    
    /// <summary>
    /// Updates the rotation quaternion based on boundaryRotation.
    /// Call this if boundaryRotation is changed after Start().
    /// </summary>
    public void UpdateRotationQuaternion()
    {
        rotationQuaternion = Quaternion.Euler(0, boundaryRotation, 0);
    }

    private void OnDrawGizmos()
    {
        // Set the color for the boundary visualization
        Gizmos.color = Color.red;
        // Calculate the boundary box dimensions and position
        Vector3 size = new Vector3(boundaryLengthX, 1f, boundaryLengthZ);
        Vector3 center = moveWithTransform && targetTransform != null ? targetTransform.position : boundaryCenter;

        // Apply rotation to the gizmo
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(center, Quaternion.Euler(0, boundaryRotation, 0), Vector3.one);
        Gizmos.matrix = rotationMatrix;

        // Draw a wire cube to represent the boundary
        Gizmos.DrawWireCube(Vector3.zero, size);
    }

    public bool HandlePeriodicBoundaries(Transform objectTransform)
    {
        Vector3 center = moveWithTransform && targetTransform != null ? targetTransform.position : boundaryCenter;
        Vector3 position = objectTransform.position;
        Vector3 originalPosition = position;

        // Convert position to local space relative to the rotated boundary
        Vector3 localPosition = Quaternion.Inverse(rotationQuaternion) * (position - center);

        float halfWidth = boundaryLengthX / 2f;
        float halfLength = boundaryLengthZ / 2f;
        bool wasWrapped = false;

        // Check X-axis boundaries (use >= and <= to catch positions exactly at boundary)
        if (localPosition.x >= halfWidth)
        {
            localPosition.x -= boundaryLengthX;
            wasWrapped = true;
        }
        else if (localPosition.x <= -halfWidth)
        {
            localPosition.x += boundaryLengthX;
            wasWrapped = true;
        }

        // Check Z-axis boundaries (use >= and <= to catch positions exactly at boundary)
        if (localPosition.z >= halfLength)
        {
            localPosition.z -= boundaryLengthZ;
            wasWrapped = true;
        }
        else if (localPosition.z <= -halfLength)
        {
            localPosition.z += boundaryLengthZ;
            wasWrapped = true;
        }

        // Convert back to world space
        position = center + (rotationQuaternion * localPosition);
        objectTransform.position = position;
        
        return wasWrapped;
    }

    void LateUpdate()
    {
        // Handle periodic boundaries and update the position of the GameObject this script is attached to.
        // Use LateUpdate to ensure this runs AFTER other Update() methods that might set positions
        // Note: For Kannadi clones, wrapping is done immediately in UpdateClonesPositionAndRotation()
        // This LateUpdate is for objects that move independently (like VR parents or other moving objects)
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