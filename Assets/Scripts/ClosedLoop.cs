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

    // New yaw-based orientation mode variables
    [SerializeField][Tooltip("Whether to use yaw-based orientation mode instead of standard orientation")] private bool useYawMode = false;
    [SerializeField][Tooltip("Gain factor for yaw-based orientation scaling")] private float yawGain = 1.0f;
    [SerializeField][Tooltip("DC offset for yaw-based orientation (in degrees)")] private float yawDCOffset = 0.0f;
    [SerializeField][Tooltip("Step size for gain adjustments")] private float gainStep = 0.1f;
    [SerializeField][Tooltip("Step size for DC offset adjustments (in degrees)")] private float dcOffsetStep = 0.1f;

    // Stores the initial world rotation, including any random rotation applied at start
    private Quaternion _initialWorldRotation;

    // Add these for logging support
    private float _lastYawInput = 0f;
    private float _lastYawOutput = 0f;

    private void Start()
    {
        sphereRadius = sphereDiameter / 2f;
        _zmqListener = GetComponent<ZmqListener>();
        if (_zmqListener == null)
            Debug.LogError("ZmqListener component not found!");
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        ResetPositionAndRotation();
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
            Vector3 positionDelta = _ficTracRotationOffset * new Vector3(ficTracDelta.x, 0, ficTracDelta.y) * sphereRadius;
            transform.Translate(positionDelta, Space.World);
        }

        // Apply rotation change based on yaw mode setting
        if (useYawMode)
        {
            // Yaw mode: closedLoopOrientation acts as on/off flag for yaw mode
            if (closedLoopOrientation)
            {
                // Yaw-based orientation mode: (gain * yaw) - DC offset
                // Use the absolute yaw value directly from ZMQ
                float absoluteYaw = currentFicTracData.z * Mathf.Rad2Deg;
                _lastYawInput = absoluteYaw;
                
                // Apply gain and DC offset with correct formula: (gain * yaw) - dcoffset
                float rotationDelta = (yawGain * absoluteYaw) - yawDCOffset;
                
                // Store the processed output for logging
                _lastYawOutput = rotationDelta;
                
                // Apply the rotation
                transform.Rotate(0, rotationDelta, 0, Space.Self);
                
                Debug.Log($"Yaw Mode: Input={_lastYawInput:F2}°, Gain={yawGain:F2}, DCOffset={yawDCOffset:F2}°, Output={_lastYawOutput:F2}°");
            }
            else
            {
                // Yaw mode is enabled but orientation is off - no rotation applied
                _lastYawInput = currentFicTracData.z * Mathf.Rad2Deg;
                _lastYawOutput = 0f;
            }
        }
        else
        {
            // Standard mode: use original delta-based closed loop orientation
            if (closedLoopOrientation)
            {
                // Standard orientation mode uses delta
                float rotationDelta = ficTracDelta.z * Mathf.Rad2Deg;
                _lastYawInput = rotationDelta;
                _lastYawOutput = rotationDelta;
                
                // Apply the rotation
                transform.Rotate(0, rotationDelta, 0, Space.Self);
            }
            else
            {
                // Standard mode with orientation off - no rotation applied
                _lastYawInput = 0f;
                _lastYawOutput = 0f;
            }
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
        _lastYawInput = 0f;
        _lastYawOutput = 0f;
        Debug.Log("Reset to initial position and rotation. Waiting for re-initialization...");
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
        Debug.Log($"Closed Loop Position: {(closedLoopPosition ? "ON" : "OFF")}");
        Debugger.Log($"Closed Loop Position toggled to: {(closedLoopPosition ? "ON" : "OFF")}", 3);
    }

    public void ToggleClosedLoopOrientation()
    {
        closedLoopOrientation = !closedLoopOrientation;
        Debug.Log($"Closed Loop Orientation: {(closedLoopOrientation ? "ON" : "OFF")}");
        Debugger.Log($"Closed Loop Orientation toggled to: {(closedLoopOrientation ? "ON" : "OFF")}", 3);
    }

    // New method to toggle yaw mode
    public void ToggleYawMode()
    {
        useYawMode = !useYawMode;
        Debug.Log($"Yaw Mode: {(useYawMode ? "ON" : "OFF")} (Gain={yawGain:F2}, DCOffset={yawDCOffset:F2}°)");
        Debugger.Log($"Yaw Mode toggled to: {(useYawMode ? "ON" : "OFF")} (Gain={yawGain:F2}, DCOffset={yawDCOffset:F2}°)", 3);
    }

    // Methods to adjust gain
    public void IncreaseGain()
    {
        yawGain += gainStep;
        Debug.Log($"Yaw Gain increased to: {yawGain:F2}");
        Debugger.Log($"Yaw Gain increased to: {yawGain:F2} (step: +{gainStep:F2})", 3);
    }

    public void DecreaseGain()
    {
        yawGain -= gainStep;
        Debug.Log($"Yaw Gain decreased to: {yawGain:F2}");
        Debugger.Log($"Yaw Gain decreased to: {yawGain:F2} (step: -{gainStep:F2})", 3);
    }

    // Methods to adjust DC offset
    public void IncreaseDCOffset()
    {
        yawDCOffset += dcOffsetStep;
        Debug.Log($"Yaw DC Offset increased to: {yawDCOffset:F2}°");
        Debugger.Log($"Yaw DC Offset increased to: {yawDCOffset:F2}° (step: +{dcOffsetStep:F2}°)", 3);
    }

    public void DecreaseDCOffset()
    {
        yawDCOffset -= dcOffsetStep;
        Debug.Log($"Yaw DC Offset decreased to: {yawDCOffset:F2}°");
        Debugger.Log($"Yaw DC Offset decreased to: {yawDCOffset:F2}° (step: -{dcOffsetStep:F2}°)", 3);
    }

    // Public methods for external scripts to control the behaviors
    public void SetClosedLoopOrientation(bool value)
    {
        closedLoopOrientation = value;
    }

    public void SetClosedLoopPosition(bool value)
    {
        closedLoopPosition = value;
    }

    public void SetYawMode(bool value)
    {
        useYawMode = value;
    }

    public void SetYawGain(float value)
    {
        yawGain = value;
    }

    public void SetYawDCOffset(float value)
    {
        yawDCOffset = value;
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

    // Public getters for logging
    public bool GetUseYawMode() { return useYawMode; }
    public float GetYawGain() { return yawGain; }
    public float GetYawDCOffset() { return yawDCOffset; }
    public float GetLastYawInput() { return _lastYawInput; }
    public float GetLastYawOutput() { return _lastYawOutput; }
    public bool GetClosedLoopOrientation() { return closedLoopOrientation; }
    public bool GetClosedLoopPosition() { return closedLoopPosition; }
    public float GetSphereDiameter() { return sphereDiameter; }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.O))
            ToggleClosedLoopOrientation();
        if (Input.GetKeyDown(KeyCode.P))
            ToggleClosedLoopPosition();

        // Yaw mode toggle using Ctrl+Y
        if (Input.GetKeyDown(KeyCode.Y) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            ToggleYawMode();
        
        // Gain adjustments using + and - keys
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
            IncreaseGain();
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            DecreaseGain();
        
        // DC offset adjustments using [ and ] keys
        if (Input.GetKeyDown(KeyCode.RightBracket))
            IncreaseDCOffset();
        if (Input.GetKeyDown(KeyCode.LeftBracket))
            DecreaseDCOffset();

        if (Input.GetKeyUp(KeyCode.Escape))
        {
            Application.Quit();
        }
    }
}