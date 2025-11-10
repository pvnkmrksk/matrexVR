using UnityEngine;
public class ClosedLoop : MonoBehaviour
{


    [SerializeField][Tooltip("The diameter of the sphere in cm")] private float sphereDiameter = 1f;

    private float sphereRadius;
    [SerializeField][Tooltip("The key to reset the position and rotation")] private KeyCode resetKey = KeyCode.R;
    [SerializeField][Tooltip("The delay in seconds before starting to use FicTrac data after reset.")] private float initializationDelay = 0.1f;

    private ZmqListener _zmqListener;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Vector3 _lastFicTracData;
    private bool _isInitialized = false;
    private Quaternion _ficTracRotationOffset;
    private float _initializationTimer;
    private bool _baseRotationSet = false; // Track if base rotation has been properly set
    private Quaternion _baseRotation; // rotation defined by scene/config at startup

    // Add these new variables
    [SerializeField][Tooltip("Whether to apply the FicTrac position in closed loop")] private float closedLoopPosition = 1.0f;
    [SerializeField][Tooltip("Whether to apply the FicTrac rotation in closed loop")] private float closedLoopOrientation = 1.0f;

    private void Start()
    {
        sphereRadius = sphereDiameter / 2f;
        _zmqListener = GetComponent<ZmqListener>();
        if (_zmqListener == null)
            Debug.LogError("ZmqListener component not found!");
        
        // Store current transform as initial, and set base rotation as fallback
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        _baseRotation = _initialRotation; // Set fallback base rotation
        
        // Wait for proper initialization from scene controller
        _isInitialized = false;
        _ficTracRotationOffset = Quaternion.identity;
        _initializationTimer = 0f;
        _lastFicTracData = Vector3.zero;
    }

    private void Update()
    {
        HandleInput();

        if (_zmqListener.pose == null) return;

        if (Input.GetKeyDown(resetKey))
        {
            ResetPositionAndRotation();
            return;
        }

        if (!_isInitialized)
        {
            _initializationTimer += Time.deltaTime;
            if (_initializationTimer >= initializationDelay && _baseRotationSet)
            {
                InitializeFicTracData();
            }
        }
        else
        {
            UpdateTransform();
        }
    }

    private void InitializeFicTracData()
    {
        _lastFicTracData = GetCurrentFicTracData();
        float initialYaw = _lastFicTracData.z;
        // Compute offset so that desiredRotation starts from the current base rotation
        Quaternion fictracYaw = Quaternion.Euler(0, initialYaw * Mathf.Rad2Deg, 0);
        _ficTracRotationOffset = _baseRotation * Quaternion.Inverse(fictracYaw);
        _isInitialized = true;
        Debug.Log($"Initialized with FicTrac data: ({_lastFicTracData.x}, {_lastFicTracData.y}, {_lastFicTracData.z})");
        Debug.Log($"Base rotation: {_baseRotation.eulerAngles}, FicTrac offset: {_ficTracRotationOffset.eulerAngles}");
    }

    private void UpdateTransform()
    {
        Vector3 currentFicTracData = GetCurrentFicTracData();
        Vector3 ficTracDelta = currentFicTracData - _lastFicTracData;

        // Compute the absolute desired rotation (yaw angle)
        float targetYaw = currentFicTracData.z * Mathf.Rad2Deg;
        Quaternion desiredRotation = Quaternion.Euler(0, targetYaw, 0);

        // Apply the rotation offset from initialization
        desiredRotation = _ficTracRotationOffset * desiredRotation;


        // Apply position change only if closedLoopPosition is true
        if (closedLoopPosition != 0.0f)
        {
            Vector3 positionDelta = _ficTracRotationOffset * new Vector3(ficTracDelta.x, 0, ficTracDelta.y) * sphereRadius * closedLoopPosition;
            transform.Translate(positionDelta, Space.World);
        }

        // Control the convergence behavior based on closedLoopOrientation
        if (closedLoopOrientation > 0.0f)
        {
            float step = closedLoopOrientation * 360f * Time.deltaTime; // scale up/down
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, step);
        }

        _lastFicTracData = currentFicTracData;
    }

    public void ResetPositionAndRotation()
    {
        transform.SetPositionAndRotation(_initialPosition, _initialRotation);
        _isInitialized = false;
        _ficTracRotationOffset = Quaternion.identity;
        _initializationTimer = 0f;
        _lastFicTracData = Vector3.zero;
        Debug.Log("Reset to initial position and rotation. Waiting for re-initialization...");
    }

    // Set a new base pose that FicTrac should align to, without resetting to zero
    public void SetBasePose(Vector3 position, Quaternion rotation)
    {
        _initialPosition = position;
        _initialRotation = rotation;
        _baseRotation = rotation; // This is the key - set the base rotation for FicTrac alignment
        _baseRotationSet = true; // Mark that base rotation has been properly set
        transform.SetPositionAndRotation(position, rotation);
        // Recompute alignment on next InitializeFicTracData
        _isInitialized = false;
        _ficTracRotationOffset = Quaternion.identity;
        _initializationTimer = 0f;
        _lastFicTracData = Vector3.zero;
        Debug.Log($"SetBasePose: position={position}, rotation={rotation.eulerAngles}");
    }

    // Convenience to set only rotation as base
    public void SetBaseRotation(Quaternion rotation)
    {
        SetBasePose(transform.position, rotation);
    }

    private Vector3 GetCurrentFicTracData()
    {
        Pose pose = _zmqListener.pose;
        return new Vector3(pose.position.y, pose.position.x, pose.rotation.eulerAngles.y * Mathf.Deg2Rad);
    }

    // New methods
    public void ToggleClosedLoopPosition()
    {
        if (closedLoopPosition != 0.0f)
        {
            closedLoopPosition = 0.0f;
            Debug.Log("Closed Loop Position: OFF");
            return;
        }
        else
        {
            closedLoopPosition = 1.0f;
            Debug.Log("Closed Loop Position: ON");
            return;
        }
    }

    public void ToggleClosedLoopOrientation()
    {
        if (closedLoopOrientation != 0.0f)
        {
            closedLoopOrientation = 0.0f;
            Debug.Log("Closed Loop Position: OFF");
            return;
        }
        else
        {
            closedLoopOrientation = 1.0f;
            Debug.Log("Closed Loop Position: ON");
            return;
        }
    }

    // Public methods for external scripts to control the behaviors
    public void SetClosedLoopOrientation(float value)
    {
        closedLoopOrientation = value;
    }

    public void SetClosedLoopPosition(float value)
    {
        closedLoopPosition = value;
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.O))
            ToggleClosedLoopOrientation();
        if (Input.GetKeyDown(KeyCode.P))
            ToggleClosedLoopPosition();

        if (Input.GetKeyUp(KeyCode.Escape))
        {
            Application.Quit();
        }
    }

}