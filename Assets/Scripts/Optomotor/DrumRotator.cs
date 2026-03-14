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
    private string lastAxis = null; // same-axis continuity: only reset orientation when axis changes

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
        drum = this.gameObject;
        initialRotation = drum.transform.rotation;
    }

    void Start()
    {

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
        rotationSpeed = speed;
        rotateClockwise = clockwise;
        rotationAxis = StringToAxis(axis);

        bool axisChanged = (axis != lastAxis);
        lastAxis = axis;

        if (axisChanged)
        {
            // New axis: reset base and spin so drum jumps to clean orientation for this axis
            baseOrientation = initialRotation * TiltQuaternionFromAxis(axis);
            spinAngle = 0f;
            drum.transform.rotation = baseOrientation;
        }
        // Same axis: keep current baseOrientation and spinAngle so drum continues from where it left off

        totalRotation = 0f;
        lastRotationAmount = 0f;

        // Stop existing coroutine if running
        if (rotationCoroutine != null)
        {
            StopCoroutine(rotationCoroutine);
            rotationCoroutine = null;
            isRotating = false;
        }

        if (rotationSpeed != 0)
        {
            isRotating = true;
            rotationCoroutine = StartCoroutine(RotateDrum());
        }
        else
        {
            isRotating = false;
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

        return axis;
    }

    // 90° tilt for Pitch/Roll so drum axis is horizontal; spin is then always around local yaw (Pitch=Z tilt, Roll=X tilt)
    private Quaternion TiltQuaternionFromAxis(string axisName)
    {
        switch (axisName)
        {
            case "Yaw":
                return Quaternion.identity;
            case "Pitch":
                return Quaternion.Euler(0f, 0f, 90f);
            case "Roll":
                return Quaternion.Euler(90f, 0f, 0f);
            default:
                return Quaternion.identity;
        }
    }

    public void ResetRotation()
    {
        drum.transform.rotation = initialRotation;
    }

    private IEnumerator RotateDrum()
    {
        yield return new WaitForEndOfFrame();
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
                isPaused = !isPaused;

            if (Input.GetKeyDown(KeyCode.D))
                Debug.Log($"Drum: axis={lastAxis ?? "Yaw"} speed={rotationSpeed}°/s clockwise={rotateClockwise} paused={isPaused} spin={spinAngle:F0}°");
        }
    }

    // Add a public method to directly test rotation
    public void TestRotation(float testSpeed)
    {
        SetRotationParameters(testSpeed, true, "Yaw");
    }

    public float GetRotationSpeed()
    {
        return rotationSpeed;
    }

    public string GetRotationAxis()
    {
        return lastAxis ?? "Yaw";
    }

    // Public getter for rotation direction
    public bool IsClockwise()
    {
        return rotateClockwise;
    }

    void OnDestroy()
    {
        if (rotationCoroutine != null)
        {
            StopCoroutine(rotationCoroutine);
        }
    }
}
