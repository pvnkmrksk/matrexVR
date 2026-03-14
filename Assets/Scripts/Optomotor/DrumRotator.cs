using UnityEngine;
using System;
using System.Collections;

public enum RotationAxis
{
    Yaw,
    Pitch,
    Roll
}

public class DrumRotator : MonoBehaviour
{
    private GameObject drum;
    private Quaternion initialRotation;

    // Rotation parameters
    private float rotationSpeed = 0f;
    private bool rotateClockwise = true;
    private Vector3 rotationAxis = Vector3.up; // Kept for API; orientation now from baseOrientation

    // Tilt then spin: base orientation (tilt for Pitch/Roll), spin always around local yaw
    private Quaternion baseOrientation;
    private float spinAngle = 0f;

    // Rotation state
    private bool isRotating = false;
    private bool isPaused = false;
    private Coroutine rotationCoroutine;

    // For manual control in debug/development
    private bool allowManualControl = true;

    // For debugging
    private float totalRotation = 0f;
    private float lastRotationAmount = 0f;

    void Awake()
    {
        Debug.Log($"DrumRotator.Awake() - {gameObject.name}");
        
        // FPS and VSync settings removed - now handled centrally by MainController
        
        drum = this.gameObject;
        initialRotation = drum.transform.rotation;
    }

    void Start()
    {
        Debug.Log($"DrumRotator.Start() - {gameObject.name}");

        // Activate all monitors for multi-monitor setup
        // Display.displays[0].Activate(); // Main display always activated by default
        // for (int i = 1; i < Display.displays.Length; i++)
        // {
        //     Display.displays[i].Activate();
        // }
    }

    // Public method to set rotation parameters from OptomotorSceneController
    public void SetRotationParameters(float speed, bool clockwise, string axis)
    {
        Debug.Log($"DrumRotator.SetRotationParameters() - Speed: {speed}, Clockwise: {clockwise}, Axis: {axis}");

        rotationSpeed = speed;
        rotateClockwise = clockwise;
        rotationAxis = StringToAxis(axis);

        // Base orientation: Yaw = no tilt, Pitch = 90° around X, Roll = 90° around Z; spin always around local yaw
        baseOrientation = initialRotation * TiltQuaternionFromAxis(axis);
        spinAngle = 0f;
        drum.transform.rotation = baseOrientation;

        totalRotation = 0f;
        lastRotationAmount = 0f;

        // Stop existing coroutine if running
        if (rotationCoroutine != null)
        {
            StopCoroutine(rotationCoroutine);
            rotationCoroutine = null;
            isRotating = false;
            Debug.Log("Stopped previous rotation coroutine");
        }

        // Start new rotation if speed is not zero
        if (rotationSpeed != 0)
        {
            // IMPORTANT: Set isRotating flag BEFORE starting the coroutine
            isRotating = true;
            Debug.Log($"Set isRotating to {isRotating} BEFORE starting coroutine");

            rotationCoroutine = StartCoroutine(RotateDrum());
            Debug.Log($"Started rotation coroutine with speed: {rotationSpeed}");
        }
        else
        {
            isRotating = false;
            Debug.Log("Speed is zero, not starting rotation");
        }
    }

    private Vector3 StringToAxis(string axisName)
    {
        Vector3 axis;
        switch (axisName)
        {
            case "Pitch":
                axis = Vector3.right;
                break;
            case "Yaw":
                axis = Vector3.up;
                break;
            case "Roll":
                axis = Vector3.forward;
                break;
            default:
                Debug.LogWarning($"Unknown rotation axis: {axisName}, defaulting to Yaw");
                axis = Vector3.up;
                break;
        }

        Debug.Log($"Converted axis '{axisName}' to {axis}");
        return axis;
    }

    // 90° tilt for Pitch/Roll so drum axis is horizontal; spin is then always around local yaw
    private Quaternion TiltQuaternionFromAxis(string axisName)
    {
        switch (axisName)
        {
            case "Yaw":
                return Quaternion.identity;
            case "Pitch":
                return Quaternion.Euler(90f, 0f, 0f);
            case "Roll":
                return Quaternion.Euler(0f, 0f, 90f);
            default:
                return Quaternion.identity;
        }
    }

    public void ResetRotation()
    {
        Debug.Log("Resetting drum rotation to initial state");
        drum.transform.rotation = initialRotation;
    }

    private IEnumerator RotateDrum()
    {
        Debug.Log($"RotateDrum coroutine started. isRotating={isRotating}");

        // Base orientation and spinAngle already set in SetRotationParameters; no ResetRotation here

        // Wait one frame to ensure everything is initialized
        yield return new WaitForEndOfFrame();

        // Rotation is now handled in Update() for frame-locked behavior
        // This coroutine just sets up the state
        Debug.Log($"Rotation setup complete. Speed={rotationSpeed}, isRotating={isRotating}");
    }

    void Update()
    {
        // Frame-locked rotation in Update() instead of coroutine for consistent timing
        if (isRotating && !isPaused && rotationSpeed != 0)
        {
            // Calculate rotation amount for this frame (frame-locked via Time.deltaTime)
            float rotationAmount = rotationSpeed * Time.deltaTime;

            // Apply direction
            if (!rotateClockwise)
            {
                rotationAmount *= -1;
            }

            // Spin always around local yaw (drum's long axis)
            spinAngle += rotationAmount;
            drum.transform.rotation = baseOrientation * Quaternion.AngleAxis(spinAngle, Vector3.up);

            // Track rotation for debugging
            totalRotation += Mathf.Abs(rotationAmount);
            lastRotationAmount = rotationAmount;
        }

        // Manual control for debugging/development
        if (allowManualControl)
        {
            // Reset rotation
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetRotation();
            }

            // Pause/resume rotation
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Backslash))
            {
                isPaused = !isPaused;
                Debug.Log($"Rotation paused: {isPaused}");
            }

            // Debug current rotation state
            if (Input.GetKeyDown(KeyCode.D))
            {
                Debug.Log($"Rotation debug: isRotating={isRotating}, isPaused={isPaused}, Speed={rotationSpeed}, TotalRotation={totalRotation}");
            }
        }
    }

    // Add a public method to directly test rotation
    public void TestRotation(float testSpeed)
    {
        Debug.Log($"Manual test rotation with speed {testSpeed}");
        SetRotationParameters(testSpeed, true, "Yaw");
    }

    // Public getter for current rotation speed
    public float GetRotationSpeed()
    {
        return rotationSpeed;
    }

    // Public getter for rotation direction
    public bool IsClockwise()
    {
        return rotateClockwise;
    }

    void OnDestroy()
    {
        Debug.Log("DrumRotator being destroyed");
        if (rotationCoroutine != null)
        {
            StopCoroutine(rotationCoroutine);
        }
    }
}
