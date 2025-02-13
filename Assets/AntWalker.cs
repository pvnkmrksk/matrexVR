using UnityEngine;

public class AntWalker : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 3f;           // Speed of horizontal movement
    public float rotationSpeed = 90f;      // Degrees per second for Q/E rotation
    public float alignSpeed = 10f;         // How fast to align rotation to the terrain

    [Header("Raycast Settings")]
    public float raycastOriginHeight = 5f; // Height above the character to cast the ray
    public float heightOffset = 0.1f;      // Small offset to keep the character above the surface

    // Internal yaw based on input
    private float currentYaw = 0f;

    void Update()
    {
        // --- Input Handling ---
        float inputX = Input.GetAxis("Horizontal"); // arrow keys left/right
        float inputZ = Input.GetAxis("Vertical");     // arrow keys up/down

        if (Input.GetKey(KeyCode.Q))
            currentYaw -= rotationSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E))
            currentYaw += rotationSpeed * Time.deltaTime;

        // Calculate movement direction in world space using current yaw.
        Vector3 moveDirection = Quaternion.Euler(0, currentYaw, 0) * new Vector3(inputX, 0, inputZ);
        transform.position += moveDirection * moveSpeed * Time.deltaTime;

        // --- Surface Alignment ---
        // Raycast downward from above the character.
        Vector3 rayOrigin = transform.position + Vector3.up * raycastOriginHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastOriginHeight * 2f))
        {
            // Snap vertical position to terrain surface (plus an offset).
            Vector3 targetPosition = hit.point + hit.normal * heightOffset;
            transform.position = new Vector3(transform.position.x, targetPosition.y, transform.position.z);

            // Compute desired forward direction based on current yaw.
            Vector3 desiredForward = Quaternion.Euler(0, currentYaw, 0) * Vector3.forward;
            // Project this direction onto the terrain tangent (so it lies along the surface).
            desiredForward = Vector3.ProjectOnPlane(desiredForward, hit.normal).normalized;
            if (desiredForward.sqrMagnitude < 0.001f)
                desiredForward = transform.forward;

            // Compute the target rotation: up aligns with hit.normal and forward is desiredForward.
            Quaternion targetRotation = Quaternion.LookRotation(desiredForward, hit.normal);
            // Smoothly rotate the character toward the target.
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, alignSpeed * Time.deltaTime);
        }
    }
}