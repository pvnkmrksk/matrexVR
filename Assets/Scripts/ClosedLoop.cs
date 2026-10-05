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
    private bool useLocustGains;
    private ClosedLoopMode currentMode = ClosedLoopMode.Kinefly;
    [SerializeField] private bool useYawMode = true;
    [SerializeField] private float yawGain = 1f;
    [SerializeField] private float yawDCOffset;
    [SerializeField] private float gainStep = 1f;
    [SerializeField] private bool useForceMode;
    [SerializeField] private float forceGain = 1f;
    [SerializeField] private float torqueGain = 1f;
    [SerializeField] private float forceGainStep = .1f;
    [SerializeField] private float torqueGainStep = .1f;
    private float lastYawInput, lastYawOutput;
    private string registeredVrId;
    public bool UsesBogongInput => !useLocustGains;
    public float GetYawGain() => yawGain;
    public float GetYawDCOffset() => yawDCOffset;
    public float GetLastYawInput() => lastYawInput;
    public float GetLastYawOutput() => lastYawOutput;
    public bool GetUseYawMode() => useYawMode;
    public bool GetUseForceMode() => useForceMode;
    public ClosedLoopMode GetCurrentMode() => currentMode;
    public void SetYawGain(float value) => yawGain = value;
    public void SetYawDCOffset(float value) => yawDCOffset = value;
    public void SetYawMode(bool value) => useYawMode = value;
    public void SetForceMode(bool value) => useForceMode = value;
    public void SetForceGain(float value) => forceGain = value;
    public void SetTorqueGain(float value) => torqueGain = value;

    private void Start()
    {
        MainController main = FindObjectOfType<MainController>();
        currentMode = main != null ? main.GetSystemConfigForGameObject(gameObject).closedLoopMode : ClosedLoopMode.Kinefly;
        if (!System.Enum.IsDefined(typeof(ClosedLoopMode), currentMode)) currentMode = ClosedLoopMode.FicTrac;
        if (currentMode == ClosedLoopMode.Kinefly || currentMode == ClosedLoopMode.Tirbala) useLocustGains = false;
        // BogongAustralia sets yaw mode true even for its FicTrac enum value.
        // Preserve that historical behavior; SetLocustGains is the separate walking path.
        useYawMode = true;
        useForceMode = currentMode == ClosedLoopMode.Tirbala;
        if (useLocustGains) ApplySphereDiameterFromSystemConfig();
        sphereRadius = sphereDiameter / 2f;
        _zmqListener = GetComponent<ZmqListener>();
        if (_zmqListener == null)
            Debug.LogError("ZmqListener component not found!");
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        if (useLocustGains) _initialWorldRotation = _initialRotation;
        if (main != null)
        {
            registeredVrId = main.GetSystemConfigForGameObject(gameObject).vrId;
            main.RegisterVRClosedLoop(registeredVrId, this);
        }
        ResetPositionAndRotation();
    }

    private void Update()
    {
        if (HandleInput()) return;

        if (_zmqListener == null || (useLocustGains && !_zmqListener.HasPose))
            return;

        if (useLocustGains && !_zmqListener.HasFreshPose())
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
        AdvanceTracking(Time.deltaTime);
    }

    private void AdvanceTracking(float seconds)
    {
        Vector3 currentFicTracData = GetCurrentFicTracData();
        Vector3 ficTracDelta = currentFicTracData - _lastFicTracData;

        // Apply position change only if closedLoopPosition is true
        if (closedLoopPosition)
        {
            // Use _ficTracRotationOffset to correctly transform the position delta
            // This accounts for both the initial FicTrac orientation and any random initial rotation
            Vector3 positionDelta = _ficTracRotationOffset * new Vector3(ficTracDelta.x, 0, ficTracDelta.y) * sphereRadius;
            if (useLocustGains) positionDelta *= locustPositionGain;
            transform.Translate(positionDelta, Space.World);
        }

        // Apply rotation change only if closedLoopOrientation is true
        if (!useLocustGains)
        {
            ApplyBogongOrientation(currentFicTracData, ficTracDelta, seconds);
        }
        else if (closedLoopOrientation)
        {
            // True angular gain: one sensor degree produces one world degree at gain 1.
            // No dt-based convergence, smoothing, or turn-speed clamp in walking mode.
            float rotationDelta = Mathf.DeltaAngle(_lastFicTracData.z * Mathf.Rad2Deg, currentFicTracData.z * Mathf.Rad2Deg);
            transform.Rotate(0, rotationDelta * locustOrientationGain, 0, Space.Self);
        }

        _lastFicTracData = currentFicTracData;
    }

    private void ApplyBogongOrientation(Vector3 current, Vector3 delta, float seconds)
    {
        // Exact arithmetic and branch order from BogongAustralia ef8a68b, UpdateTransform.
        if (useForceMode)
        {
            if (closedLoopOrientation)
            {
                transform.Translate(new Vector3(current.x * forceGain, 0, 0) * sphereRadius, Space.World);
                transform.Rotate(0, current.z * torqueGain, 0, Space.Self);
            }
        }
        else if (useYawMode)
        {
            lastYawInput = current.z * Mathf.Rad2Deg;
            if (closedLoopOrientation)
            {
                float rotationDeltaRadians = yawGain * (current.z - yawDCOffset);
                float rotationDeltaDegrees = rotationDeltaRadians * Mathf.Rad2Deg;
                lastYawOutput = rotationDeltaDegrees;
                transform.Rotate(0, rotationDeltaDegrees * seconds, 0, Space.Self);
            }
            else lastYawOutput = 0;
        }
        else
        {
            lastYawInput = lastYawOutput = closedLoopOrientation ? delta.z * Mathf.Rad2Deg : 0;
            if (closedLoopOrientation) transform.Rotate(0, lastYawOutput, 0, Space.Self);
        }
    }

    private void OnDestroy()
    {
        if (MainController.Instance != null && registeredVrId != null)
            MainController.Instance.UnregisterVRClosedLoop(registeredVrId, this);
    }
    public void ResetPositionAndRotation()
    {
        transform.SetPositionAndRotation(_initialPosition, _initialRotation);
        if (useLocustGains) _initialWorldRotation = _initialRotation;
        _isInitialized = false;
        _ficTracRotationOffset = Quaternion.identity;
        _initializationTimer = 0f;
        _lastFicTracData = Vector3.zero;
        _nextStalePoseWarningTime = 0f;
        lastYawInput = lastYawOutput = 0;
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
        MainController main = FindObjectOfType<MainController>();
        currentMode = main != null ? main.GetSystemConfigForGameObject(gameObject).closedLoopMode : ClosedLoopMode.FicTrac;
        useLocustGains = currentMode != ClosedLoopMode.Kinefly && currentMode != ClosedLoopMode.Tirbala;
        if (useLocustGains) ApplySphereDiameterFromSystemConfig();
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
        if (!useLocustGains)
        {
            ZmqListener.RawSample sample = _zmqListener.ReadRawSample();
            return new Vector3(sample.position.y, sample.position.x, sample.rotation.y);
        }
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
        if (useLocustGains && value && !closedLoopOrientation)
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

        if (!useLocustGains && !ExperimentInput.IsEditingText)
        {
            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (keys != null)
            {
                if (keys.equalsKey.wasPressedThisFrame || keys.numpadPlusKey.wasPressedThisFrame) yawGain += gainStep;
                if (keys.minusKey.wasPressedThisFrame || keys.numpadMinusKey.wasPressedThisFrame) yawGain -= gainStep;
                if (ExperimentInput.ControlHeld && keys.yKey.wasPressedThisFrame) useYawMode = !useYawMode;
                if (ExperimentInput.ControlHeld && keys.fKey.wasPressedThisFrame) useForceMode = !useForceMode;
                if (ExperimentInput.ControlHeld && (keys.equalsKey.wasPressedThisFrame || keys.numpadPlusKey.wasPressedThisFrame)) forceGain += forceGainStep;
                if (ExperimentInput.ControlHeld && (keys.minusKey.wasPressedThisFrame || keys.numpadMinusKey.wasPressedThisFrame)) forceGain -= forceGainStep;
                if (ExperimentInput.ControlHeld && keys.rightBracketKey.wasPressedThisFrame) torqueGain += torqueGainStep;
                if (ExperimentInput.ControlHeld && keys.leftBracketKey.wasPressedThisFrame) torqueGain -= torqueGainStep;
            }
        }

        return false;
    }
}
