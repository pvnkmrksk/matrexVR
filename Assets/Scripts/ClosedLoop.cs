using UnityEngine;
using System;

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
    [SerializeField][Tooltip("Step size for gain adjustments")] private float gainStep = 1.0f;
    [SerializeField][Tooltip("Step size for DC offset adjustments (in degrees)")] private float dcOffsetStep = 0.1f;

    // Force/torque accumulation mode variables (placeholder for Tirbala)
    [SerializeField][Tooltip("Whether to use force/torque accumulation mode")] private bool useForceMode = false;
    [SerializeField][Tooltip("Force accumulation gain factor")] private float forceGain = 1.0f;
    [SerializeField][Tooltip("Torque accumulation gain factor")] private float torqueGain = 1.0f;
    [SerializeField][Tooltip("Step size for force gain adjustments")] private float forceGainStep = 0.1f;
    [SerializeField][Tooltip("Step size for torque gain adjustments")] private float torqueGainStep = 0.1f;

    // Stores the initial world rotation, including any random rotation applied at start
    private Quaternion _initialWorldRotation;

    // Add these for logging support
    private float _lastYawInput = 0f;
    private float _lastYawOutput = 0f;
    private float _lastForceInput = 0f;
    private float _lastForceOutput = 0f;
    private float _lastTorqueInput = 0f;
    private float _lastTorqueOutput = 0f;

    // Closed loop mode configuration
    private ClosedLoopMode _currentMode = ClosedLoopMode.FicTrac;

    private void Start()
    {
        sphereRadius = sphereDiameter / 2f;
        _zmqListener = GetComponent<ZmqListener>();
        if (_zmqListener == null)
            Debug.LogError("ZmqListener component not found!");
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        
        // Load and apply closed loop mode configuration
        LoadClosedLoopConfiguration();
        
        ResetPositionAndRotation();
    }

    private void LoadClosedLoopConfiguration()
    {
        // Find the MainController to get system config
        MainController mainController = FindObjectOfType<MainController>();
        if (mainController != null)
        {
            SystemConfig config = mainController.GetSystemConfigForGameObject(gameObject);
            
            // Validate the closed loop mode
            if (!Enum.IsDefined(typeof(ClosedLoopMode), config.closedLoopMode))
            {
                Debug.LogError($"Invalid closedLoopMode '{config.closedLoopMode}' for {gameObject.name}. Valid values are: {string.Join(", ", Enum.GetNames(typeof(ClosedLoopMode)))}. Defaulting to FicTrac.");
                _currentMode = ClosedLoopMode.FicTrac;
            }
            else
            {
                _currentMode = config.closedLoopMode;
            }
            
            // Apply mode-specific default settings
            ApplyModeConfiguration(_currentMode);
            
            Debug.Log($"Applied closed loop mode configuration: {_currentMode} for {gameObject.name}");
        }
        else
        {
            Debug.LogWarning("MainController not found, using default FicTrac mode");
            _currentMode = ClosedLoopMode.FicTrac;
            ApplyModeConfiguration(_currentMode);
        }
    }

    private void ApplyModeConfiguration(ClosedLoopMode mode)
    {
        switch (mode)
        {
            case ClosedLoopMode.FicTrac:
                // FicTrac: Walking mode - yaw mode off, force mode off
                useYawMode = false;
                useForceMode = false;
                Debug.Log("Applied FicTrac mode: yaw mode OFF, force mode OFF");
                break;
                
            case ClosedLoopMode.Kinefly:
                // Kinefly: Yaw mode on, force mode off
                useYawMode = true;
                useForceMode = false;
                Debug.Log("Applied Kinefly mode: yaw mode ON, force mode OFF");
                break;
                
            case ClosedLoopMode.Tirbala:
                // Tirbala: Force/torque accumulation mode - yaw mode off, force mode on
                useYawMode = false;
                useForceMode = true;
                Debug.Log("Applied Tirbala mode: yaw mode OFF, force mode ON");
                break;
        }
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

        // Handle different rotation modes based on configuration
        if (useForceMode)
        {
            // Force/torque accumulation mode (Tirbala)
            if (closedLoopOrientation)
            {
                // Placeholder for force/torque accumulation
                // This will be implemented based on the specific requirements for Tirbala
                // For now, we'll use a simple implementation that can be expanded
                
                // Extract force and torque data from the incoming data
                // Assuming the data structure includes force/torque information
                float forceInput = currentFicTracData.x; // Placeholder - adjust based on actual data structure
                float torqueInput = currentFicTracData.z; // Placeholder - adjust based on actual data structure
                
                _lastForceInput = forceInput;
                _lastTorqueInput = torqueInput;
                
                // Apply force and torque gains
                float forceOutput = forceInput * forceGain;
                float torqueOutput = torqueInput * torqueGain;
                
                _lastForceOutput = forceOutput;
                _lastTorqueOutput = torqueOutput;
                
                // Apply the accumulated force/torque effects
                // This is a placeholder implementation - adjust based on actual requirements
                Vector3 forceEffect = new Vector3(forceOutput, 0, 0) * sphereRadius;
                transform.Translate(forceEffect, Space.World);
                
                // Apply torque as rotation
                transform.Rotate(0, torqueOutput, 0, Space.Self);
                
                Debug.Log($"Force Mode: Force Input={_lastForceInput:F2}, Force Output={_lastForceOutput:F2}, Torque Input={_lastTorqueInput:F2}, Torque Output={_lastTorqueOutput:F2}");
            }
            else
            {
                // Force mode is enabled but orientation is off - no effects applied
                _lastForceInput = 0f;
                _lastForceOutput = 0f;
                _lastTorqueInput = 0f;
                _lastTorqueOutput = 0f;
            }
        }
        else if (useYawMode)
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
        _lastForceInput = 0f;
        _lastForceOutput = 0f;
        _lastTorqueInput = 0f;
        _lastTorqueOutput = 0f;
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

    // Methods for force/torque mode
    public void ToggleForceMode()
    {
        useForceMode = !useForceMode;
        Debug.Log($"Force Mode: {(useForceMode ? "ON" : "OFF")} (ForceGain={forceGain:F2}, TorqueGain={torqueGain:F2})");
        Debugger.Log($"Force Mode toggled to: {(useForceMode ? "ON" : "OFF")} (ForceGain={forceGain:F2}, TorqueGain={torqueGain:F2})", 3);
    }

    public void IncreaseForceGain()
    {
        forceGain += forceGainStep;
        Debug.Log($"Force Gain increased to: {forceGain:F2}");
        Debugger.Log($"Force Gain increased to: {forceGain:F2} (step: +{forceGainStep:F2})", 3);
    }

    public void DecreaseForceGain()
    {
        forceGain -= forceGainStep;
        Debug.Log($"Force Gain decreased to: {forceGain:F2}");
        Debugger.Log($"Force Gain decreased to: {forceGain:F2} (step: -{forceGainStep:F2})", 3);
    }

    public void IncreaseTorqueGain()
    {
        torqueGain += torqueGainStep;
        Debug.Log($"Torque Gain increased to: {torqueGain:F2}");
        Debugger.Log($"Torque Gain increased to: {torqueGain:F2} (step: +{torqueGainStep:F2})", 3);
    }

    public void DecreaseTorqueGain()
    {
        torqueGain -= torqueGainStep;
        Debug.Log($"Torque Gain decreased to: {torqueGain:F2}");
        Debugger.Log($"Torque Gain decreased to: {torqueGain:F2} (step: -{torqueGainStep:F2})", 3);
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

    public void SetForceMode(bool value)
    {
        useForceMode = value;
    }

    public void SetForceGain(float value)
    {
        forceGain = value;
    }

    public void SetTorqueGain(float value)
    {
        torqueGain = value;
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
    
    // New getters for force/torque mode
    public bool GetUseForceMode() { return useForceMode; }
    public float GetForceGain() { return forceGain; }
    public float GetTorqueGain() { return torqueGain; }
    public float GetLastForceInput() { return _lastForceInput; }
    public float GetLastForceOutput() { return _lastForceOutput; }
    public float GetLastTorqueInput() { return _lastTorqueInput; }
    public float GetLastTorqueOutput() { return _lastTorqueOutput; }
    public ClosedLoopMode GetCurrentMode() { return _currentMode; }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.O))
            ToggleClosedLoopOrientation();
        if (Input.GetKeyDown(KeyCode.P))
            ToggleClosedLoopPosition();

        // Yaw mode toggle using Ctrl+Y
        if (Input.GetKeyDown(KeyCode.Y) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            ToggleYawMode();
        
        // Force mode toggle using Ctrl+F
        if (Input.GetKeyDown(KeyCode.F) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            ToggleForceMode();
        
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

        // Force gain adjustments using Ctrl+Plus and Ctrl+Minus
        if ((Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) && 
            (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            IncreaseForceGain();
        if ((Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) && 
            (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            DecreaseForceGain();
        
        // Torque gain adjustments using Ctrl+[ and Ctrl+]
        if (Input.GetKeyDown(KeyCode.RightBracket) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            IncreaseTorqueGain();
        if (Input.GetKeyDown(KeyCode.LeftBracket) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            DecreaseTorqueGain();

        if (Input.GetKeyUp(KeyCode.Escape))
        {
            Application.Quit();
        }
    }
}