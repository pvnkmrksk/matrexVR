using UnityEngine;

// Preserve prefab references and inspector settings while using Unity's Input System actions.
[DefaultExecutionOrder(100)]
public class Keyboard : MonoBehaviour
{
    [SerializeField] private float translateSpeed = 10f;
    [SerializeField] private float rotateSpeed = 10f;
    [SerializeField] private float maxTranslateSpeed = 100f;
    [SerializeField] private float maxRotateSpeed = 300f;
    [SerializeField] private bool allowVerticalTranslation = false;
    [SerializeField] private bool allowPitchAndRoll = false;
    private bool autopilotEnabled;
    private float autopilotSpeed = 10f;

    private void OnEnable() => MainController.SystemConfigurationChanged += ApplySystemConfig;
    private void OnDisable() => MainController.SystemConfigurationChanged -= ApplySystemConfig;
    private void Start() => ApplySystemConfig();
    private bool manualEnabled = true;
    private void ApplySystemConfig()
    {
        var main = MainController.Instance;
        if (main == null || !main.TryGetSystemConfigForGameObject(gameObject, out SystemConfig system)) return;
        var config = system.manualControls;
        if (config == null) return; // Preserve existing prefab tuning when the optional block is absent.
        manualEnabled = config.enabled;
        maxTranslateSpeed = Mathf.Max(0, config.maxTranslateSpeed);
        maxRotateSpeed = Mathf.Max(0, config.maxRotateSpeed);
        translateSpeed = Mathf.Clamp(config.translateSpeed, 0, maxTranslateSpeed);
        rotateSpeed = Mathf.Clamp(config.rotateSpeed, 0, maxRotateSpeed);
        allowVerticalTranslation = config.allowVerticalTranslation;
        allowPitchAndRoll = config.allowPitchAndRoll;
    }

    void Update() => ApplyInput(Time.deltaTime);

    public void ApplyInput(float deltaTime)
    {
        if (!manualEnabled || ExperimentInput.IsEditingText) return;
        if (ExperimentInput.ControlHeld && ExperimentInput.Pressed("Pause"))
            autopilotEnabled = !autopilotEnabled;
        // One adjustment per press, independent of frame rate. No simultaneous movement.
        if (ExperimentInput.Held("SpeedModifier"))
        {
            if (ExperimentInput.Pressed("SpeedUp")) translateSpeed += 1f;
            if (ExperimentInput.Pressed("SpeedDown")) translateSpeed -= 1f;
            if (ExperimentInput.Pressed("TurnSpeedUp")) rotateSpeed += 1f;
            if (ExperimentInput.Pressed("TurnSpeedDown")) rotateSpeed -= 1f;
            translateSpeed = Mathf.Clamp(translateSpeed, 0, maxTranslateSpeed);
            rotateSpeed = Mathf.Clamp(rotateSpeed, 0, maxRotateSpeed);
            return;
        }
        Vector2 move = ExperimentInput.Move;
        // Autopilot supplies a constant forward velocity while retaining lateral,
        // reverse and yaw input for Mario Kart-style steering.
        float manualForward = move.y * translateSpeed;
        if (autopilotEnabled) manualForward = Mathf.Min(move.y, 0f) * translateSpeed;
        float forward = (autopilotEnabled ? autopilotSpeed : 0f) + manualForward;
        transform.Translate(new Vector3(move.x * translateSpeed,
            allowVerticalTranslation ? ExperimentInput.Axis("Vertical") * translateSpeed : 0,
            forward) * deltaTime, Space.Self);
        Quaternion before = transform.rotation;
        transform.Rotate(new Vector3(allowPitchAndRoll ? ExperimentInput.Axis("Pitch") : 0,
            ExperimentInput.Axis("Yaw"), allowPitchAndRoll ? ExperimentInput.Axis("Roll") : 0) * (rotateSpeed * deltaTime), Space.Self);
        if (Quaternion.Angle(before, transform.rotation) > 0.00001f)
            GetComponent<ClosedLoop>()?.ApplyManualRotation(transform.rotation * Quaternion.Inverse(before));
    }

    public bool AutopilotEnabled => autopilotEnabled;
    public float AutopilotSpeed => autopilotSpeed;
    public void SetAutopilotMode(bool enabled) => autopilotEnabled = enabled;
    public void SetAutopilotSpeed(float speed) => autopilotSpeed = Mathf.Max(0, speed);

    public static void ApplyAutopilotConfig(bool enabled, float speed)
    {
        foreach (Keyboard keyboard in FindObjectsOfType<Keyboard>(true))
        {
            keyboard.SetAutopilotSpeed(speed);
            keyboard.SetAutopilotMode(enabled);
        }
    }
}

[System.Serializable]
public class ManualControlConfig
{
    public bool enabled = true;
    public float translateSpeed = 10f;
    public float rotateSpeed = 50f;
    public float maxTranslateSpeed = 100f;
    public float maxRotateSpeed = 300f;
    public bool allowVerticalTranslation = false;
    public bool allowPitchAndRoll = false;
}
