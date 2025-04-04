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
    private Vector3 rotationAxis = Vector3.up; // Default to Yaw (Y-axis)

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
        drum = this.gameObject;
        initialRotation = drum.transform.rotation;
    }

    void Start()
    {
        Debug.Log($"DrumRotator.Start() - {gameObject.name}");

        // Activate all monitors for multi-monitor setup
        Display.displays[0].Activate(); // Main display always activated by default
        for (int i = 1; i < Display.displays.Length; i++)
        {
            Display.displays[i].Activate();
        }
    }

    // Public method to set rotation parameters from OptomotorSceneController
    public void SetRotationParameters(float speed, bool clockwise, string axis)
    {
        Debug.Log($"DrumRotator.SetRotationParameters() - Speed: {speed}, Clockwise: {clockwise}, Axis: {axis}");

        rotationSpeed = speed;
        rotateClockwise = clockwise;
        rotationAxis = StringToAxis(axis);

        // Reset rotation and tracking variables
        ResetRotation();
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

    public void ResetRotation()
    {
        Debug.Log("Resetting drum rotation to initial state");
        drum.transform.rotation = initialRotation;
    }

    private IEnumerator RotateDrum()
    {
        Debug.Log($"RotateDrum coroutine started. isRotating={isRotating}");

        // Reset the rotation to initial state
        ResetRotation();

        // Added a short delay to ensure stability
        yield return new WaitForEndOfFrame();

        // Debug counter for logging
        int frameCount = 0;
        float elapsedTime = 0f;

        // Force log the first rotation to confirm it's working
        Debug.Log($"Beginning rotation loop. Speed={rotationSpeed}, isRotating={isRotating}");

        while (isRotating)
        {
            if (!isPaused)
            {
                // Calculate rotation amount for this frame
                float rotationAmount = rotationSpeed * Time.deltaTime;

                // Apply direction
                if (!rotateClockwise)
                {
                    rotationAmount *= -1;
                }

                // Apply rotation
                drum.transform.Rotate(rotationAxis, rotationAmount);

                // Track rotation for debugging
                totalRotation += Mathf.Abs(rotationAmount);
                lastRotationAmount = rotationAmount;

                // Log progress occasionally
                frameCount++;
                elapsedTime += Time.deltaTime;
                // if (frameCount % 60 == 0) // Log every ~60 frames
                // {
                //     Debug.Log($"Drum rotating: Speed={rotationSpeed}, LastAmount={lastRotationAmount}, TotalRotation={totalRotation}, ElapsedTime={elapsedTime}");
                // }
            }

            yield return null;
        }

        Debug.Log($"RotateDrum coroutine ended. isRotating={isRotating}");
    }

    void Update()
    {
        if (!allowManualControl) return;

        // Manual control for debugging/development

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

    // Add a public method to directly test rotation
    public void TestRotation(float testSpeed)
    {
        Debug.Log($"Manual test rotation with speed {testSpeed}");
        SetRotationParameters(testSpeed, true, "Yaw");
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
