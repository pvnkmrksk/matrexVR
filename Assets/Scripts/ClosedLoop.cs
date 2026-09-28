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

    // Add these new variables
    [SerializeField][Tooltip("Whether to apply the FicTrac position in closed loop")] private bool closedLoopPosition = true;
    [SerializeField][Tooltip("Whether to apply the FicTrac rotation in closed loop")] private bool closedLoopOrientation = true;

    // Stores the initial world rotation, including any random rotation applied at start
    private Quaternion _initialWorldRotation;
    private float _nextStalePoseWarningTime;
    private float locustPositionGain = 1f;
    private float locustOrientationGain = 1f;

    private void Start()
    {
        ApplySphereDiameterFromSystemConfig();
        sphereRadius = sphereDiameter / 2f;
        _zmqListener = GetComponent<ZmqListener>();
        if (_zmqListener == null)
            Debug.LogError("ZmqListener component not found!");
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        _initialWorldRotation = _initialRotation;
        ResetPositionAndRotation();
    }

    private void Update()
    {
        if (HandleInput()) return;

        if (_zmqListener == null || !_zmqListener.HasPose)
            return;

        if (!_zmqListener.HasFreshPose())
        {
            if (Time.unscaledTime >= _nextStalePoseWarningTime)
            {
                Debug.LogWarning(
                    $"[{gameObject.name}] FicTrac/ZMQ pose is stale ({_zmqListener.SecondsSinceLastPose:F2}s since last update)."
                );
                _nextStalePoseWarningTime = Time.unscaledTime + 5f;
            }
            return;
        }

        if (!_isInitialized)
        {
            _initializationTimer += Time.deltaTime;
            if (_initializationTimer >= initializationDelay)
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

        // Combine the initial world rotation with the FicTrac offset
        // This ensures that any random initial rotation is accounted for
        // when calculating position changes in UpdateTransform
        _ficTracRotationOffset = _initialWorldRotation * Quaternion.Euler(0, -initialYaw * Mathf.Rad2Deg, 0);
        _isInitialized = true;
        Debug.Log($"Initialized with FicTrac data: ({_lastFicTracData.x}, {_lastFicTracData.y}, {_lastFicTracData.z})");
    }

    private void UpdateTransform()
    {
        Vector3 currentFicTracData = GetCurrentFicTracData();
        Vector3 ficTracDelta = currentFicTracData - _lastFicTracData;

        // Apply position change only if closedLoopPosition is true
        if (closedLoopPosition)
        {
            // Use _ficTracRotationOffset to correctly transform the position delta
            // This accounts for both the initial FicTrac orientation and any random initial rotation
            Vector3 positionDelta = _ficTracRotationOffset * new Vector3(ficTracDelta.x, 0, ficTracDelta.y) * sphereRadius * locustPositionGain;
            transform.Translate(positionDelta, Space.World);
        }

        // Apply rotation change only if closedLoopOrientation is true
        if (closedLoopOrientation)
        {
            // True angular gain: one sensor degree produces one world degree at gain 1.
            // No dt-based convergence, smoothing, or turn-speed clamp in walking mode.
            float rotationDelta = Mathf.DeltaAngle(_lastFicTracData.z * Mathf.Rad2Deg, currentFicTracData.z * Mathf.Rad2Deg);
            transform.Rotate(0, rotationDelta * locustOrientationGain, 0, Space.Self);
        }

        _lastFicTracData = currentFicTracData;
    }
    public void ResetPositionAndRotation()
    {
        transform.SetPositionAndRotation(_initialPosition, _initialRotation);
        _initialWorldRotation = _initialRotation;
        _isInitialized = false;
        _ficTracRotationOffset = Quaternion.identity;
        _initializationTimer = 0f;
        _lastFicTracData = Vector3.zero;
        _nextStalePoseWarningTime = 0f;
        Debug.Log("Reset to initial position and rotation. Waiting for re-initialization...");
    }

    // Manual yaw rotates the displacement reference; measured yaw deltas remain additive.
    public void ApplyManualRotation(Quaternion worldDelta)
    {
        _ficTracRotationOffset = worldDelta * _ficTracRotationOffset;
        _initialWorldRotation = worldDelta * _initialWorldRotation;
    }

    public void SetLocustGains(float positionGain, float orientationGain)
    {
        if (float.IsNaN(positionGain) || float.IsInfinity(positionGain) || positionGain < 0 ||
            float.IsNaN(orientationGain) || float.IsInfinity(orientationGain) || orientationGain < 0)
            throw new System.ArgumentOutOfRangeException("Walking gains must be finite and nonnegative.");
        locustPositionGain = positionGain;
        locustOrientationGain = orientationGain;
        closedLoopPosition = positionGain != 0;
        closedLoopOrientation = orientationGain > 0;
    }

    public void SetSphereDiameter(float diameterCm)
    {
        sphereDiameter = diameterCm;
        sphereRadius = sphereDiameter / 2f;
    }

    private void ApplySphereDiameterFromSystemConfig()
    {
        MainController main = FindObjectOfType<MainController>();
        if (main == null)
            return;

        SystemConfig config = main.GetSystemConfigForGameObject(gameObject);
        SetSphereDiameter(config.sphereDiameter);
        Debug.Log($"[ClosedLoop] {gameObject.name} sphere diameter set to {config.sphereDiameter} cm from system_config");
    }

    private Vector3 GetCurrentFicTracData()
    {
        Pose pose = _zmqListener.pose;
        return new Vector3(pose.position.y, pose.position.x, pose.rotation.eulerAngles.y * Mathf.Deg2Rad);
    }

    // New methods
    public void ToggleClosedLoopPosition()
    {
        closedLoopPosition = !closedLoopPosition;
        if (closedLoopPosition && locustPositionGain == 0) locustPositionGain = 1f;
        Debug.Log($"Closed Loop Position: {(closedLoopPosition ? "ON" : "OFF")}");
    }

    public void ToggleClosedLoopOrientation()
    {
        SetClosedLoopOrientation(!closedLoopOrientation);
        if (closedLoopOrientation && locustOrientationGain == 0) locustOrientationGain = 1f;
        Debug.Log($"Closed Loop Orientation: {(closedLoopOrientation ? "ON" : "OFF")}");
    }

    // Public methods for external scripts to control the behaviors
    public void SetClosedLoopOrientation(bool value)
    {
        if (value && !closedLoopOrientation)
        {
            // Resume from the visible heading, without catching up on yaw while disabled.
            _initialWorldRotation = transform.rotation;
            if (_zmqListener != null && _zmqListener.HasFreshPose()) InitializeFicTracData();
            else { _isInitialized = false; _initializationTimer = 0f; }
        }
        closedLoopOrientation = value;
    }

    public void SetClosedLoopPosition(bool value)
    {
        closedLoopPosition = value;
    }

    public void SetPositionAndRotation(Vector3 initialPosition, Quaternion initialRotation)
    {
        _initialPosition = initialPosition;
        _initialRotation = initialRotation;
        // Store the initial world rotation to account for random rotations
        _initialWorldRotation = initialRotation;

        transform.SetPositionAndRotation(_initialPosition, _initialRotation);
        ResetPositionAndRotation();
    }

    private bool HandleInput()
    {
        if (ExperimentInput.Pressed(resetKey, KeyCode.R, "Reset"))
        {
            ResetPositionAndRotation();
            return true;
        }
        if (ExperimentInput.Pressed("ToggleOrientation"))
            ToggleClosedLoopOrientation();
        if (ExperimentInput.Pressed("TogglePosition"))
            ToggleClosedLoopPosition();

        return false;
    }
}
