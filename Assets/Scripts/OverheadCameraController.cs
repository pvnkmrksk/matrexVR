using UnityEngine;

/// <summary>
/// Overhead camera controller with mouse interactions for orbit, pan, and zoom.
/// Designed to view all individuals in the scene from above.
/// </summary>
public class OverheadCameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("Target position to orbit around (usually center of the scene)")]
    public Vector3 targetPosition = Vector3.zero;
    
    [Tooltip("Initial distance from target")]
    public float distance = 100f;
    
    [Tooltip("Minimum distance from target")]
    public float minDistance = 10f;
    
    [Tooltip("Maximum distance from target")]
    public float maxDistance = 500f;
    
    [Header("Mouse Controls")]
    [Tooltip("Mouse sensitivity for rotation")]
    public float rotationSpeed = 2f;
    
    [Tooltip("Mouse sensitivity for panning")]
    public float panSpeed = 0.5f;
    
    [Tooltip("Scroll wheel sensitivity for zooming")]
    public float zoomSpeed = 200f;
    
    [Header("Rotation Limits")]
    [Tooltip("Minimum vertical rotation angle (degrees)")]
    public float minVerticalAngle = 10f;
    
    [Tooltip("Maximum vertical rotation angle (degrees)")]
    public float maxVerticalAngle = 90f;
    
    [Header("Initial View")]
    [Tooltip("Initial horizontal rotation (degrees)")]
    public float initialHorizontalAngle = 0f;
    
    [Tooltip("Initial vertical rotation (degrees)")]
    public float initialVerticalAngle = 90f;
    
    // Private variables
    private float horizontalAngle;
    private float verticalAngle;
    private Vector3 lastMousePosition;
    private bool isDragging = false;
    private Camera cam;
    
    // Saved initial state for reset
    private Vector3 savedInitialTargetPosition;
    private float savedInitialDistance;
    private float savedInitialHorizontalAngle;
    private float savedInitialVerticalAngle;
    
    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = gameObject.AddComponent<Camera>();
        }
        
        // Force initial angles to nadir view (90 degrees straight down)
        horizontalAngle = initialHorizontalAngle;
        verticalAngle = 90f; // Always start at nadir (straight down)
        
        // Save initial state
        savedInitialTargetPosition = targetPosition;
        savedInitialDistance = distance;
        savedInitialHorizontalAngle = initialHorizontalAngle;
        savedInitialVerticalAngle = 90f; // Always save nadir as initial
        
        // Set initial camera position - ensure it's looking straight down
        UpdateCameraPosition();
        
        // Verify we're looking straight down (nadir view)
        // At 90 degrees vertical, camera should be directly above target looking down
        if (Mathf.Approximately(verticalAngle, 90f))
        {
            transform.position = targetPosition + Vector3.up * distance;
            transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        }
        
        // Configure camera for overhead view
        cam.orthographic = false;
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 2000f;
    }
    
    void LateUpdate()
    {
        HandleMouseInput();
    }
    
    void HandleMouseInput()
    {
        // Simple Unity editor-style controls
        // Right Mouse = Orbit
        // Middle Mouse = Pan  
        // Scroll = Zoom
        
        // Handle mouse wheel for zooming
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            distance -= scroll * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            UpdateCameraPosition();
        }
        
        // Handle mouse button down
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            isDragging = true;
            lastMousePosition = Input.mousePosition;
        }
        
        // Handle mouse button up
        if (Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))
        {
            isDragging = false;
        }
        
        // Handle mouse drag
        if (isDragging)
        {
            Vector3 mouseDelta = Input.mousePosition - lastMousePosition;
            
            // Right mouse button: Orbit around target
            if (Input.GetMouseButton(1))
            {
                horizontalAngle += mouseDelta.x * rotationSpeed * 0.1f;
                verticalAngle -= mouseDelta.y * rotationSpeed * 0.1f; // Unity style: drag down = look down (increase angle)
                
                verticalAngle = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);
                UpdateCameraPosition();
            }
            // Middle mouse button: Pan
            else if (Input.GetMouseButton(2))
            {
                Vector3 right = transform.right;
                Vector3 up = transform.up;
                
                float panAmount = panSpeed * 0.01f * (distance / 100f);
                targetPosition -= right * mouseDelta.x * panAmount;
                targetPosition += up * mouseDelta.y * panAmount;
                
                UpdateCameraPosition();
            }
            
            lastMousePosition = Input.mousePosition;
        }
    }
    
    void UpdateCameraPosition()
    {
        // Ensure vertical angle is clamped
        verticalAngle = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);
        
        // Special case for nadir view (90 degrees = straight down)
        if (Mathf.Approximately(verticalAngle, 90f))
        {
            // Camera directly above target, looking straight down
            transform.position = targetPosition + Vector3.up * distance;
            transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            return;
        }
        
        // Convert angles to radians
        float hRad = horizontalAngle * Mathf.Deg2Rad;
        float vRad = verticalAngle * Mathf.Deg2Rad;
        
        // Calculate position based on spherical coordinates
        float x = distance * Mathf.Sin(vRad) * Mathf.Sin(hRad);
        float y = distance * Mathf.Cos(vRad);
        float z = distance * Mathf.Sin(vRad) * Mathf.Cos(hRad);
        
        // Set camera position relative to target
        transform.position = targetPosition + new Vector3(x, y, z);
        
        // Look at target
        transform.LookAt(targetPosition);
    }
    
    /// <summary>
    /// Reset camera to initial sky view
    /// </summary>
    public void ResetToSkyView()
    {
        targetPosition = savedInitialTargetPosition;
        distance = savedInitialDistance;
        horizontalAngle = savedInitialHorizontalAngle;
        verticalAngle = 90f; // Always reset to nadir (straight down) view
        UpdateCameraPosition();
    }
    
    /// <summary>
    /// Auto-calculate target position from all locusts in the scene
    /// </summary>
    public void CalculateTargetFromLocusts()
    {
        GameObject[] locusts = GameObject.FindGameObjectsWithTag("SimulatedLocust");
        
        if (locusts.Length == 0)
        {
            Debug.LogWarning("No locusts found with tag 'SimulatedLocust'. Using default target position.");
            return;
        }
        
        Vector3 center = Vector3.zero;
        foreach (GameObject locust in locusts)
        {
            center += locust.transform.position;
        }
        center /= locusts.Length;
        
        targetPosition = center;
        savedInitialTargetPosition = center;
        
        // Calculate appropriate distance to see all locusts
        float maxDistance = 0f;
        foreach (GameObject locust in locusts)
        {
            float dist = Vector3.Distance(locust.transform.position, center);
            if (dist > maxDistance)
            {
                maxDistance = dist;
            }
        }
        
        // Set distance to see all locusts with some margin
        distance = Mathf.Max(maxDistance * 2f, 50f);
        distance = Mathf.Clamp(distance, minDistance, this.maxDistance);
        savedInitialDistance = distance;
        
        // Update saved angles to current initial values
        savedInitialHorizontalAngle = initialHorizontalAngle;
        savedInitialVerticalAngle = initialVerticalAngle;
        
        UpdateCameraPosition();
    }
}

