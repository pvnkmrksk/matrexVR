using UnityEngine;

public class ClosedLoop : MonoBehaviour
{
    [Header("Initial Settings")]
    public Vector3 initialPosition = Vector3.zero;
    public Vector3 initialRotation = Vector3.zero;

    [Header("FicTrac Settings")]
    public float sphereRadius = 5f; // Default value for sphere radius in centimeters

    [Header("Gain Settings")]
    [SerializeField, Range(0, 1000)]
    private float xGain = 100.0f;

    [SerializeField, Range(0, 1000)]
    private float yGain = 100.0f;

    [SerializeField, Range(0, 1000)]
    private float zGain = 100.0f;

    [SerializeField, Range(0, 1000)]
    private float rollGain = 1f;

    [SerializeField, Range(0, 1000)]
    private float yawGain = 1f;

    [SerializeField, Range(0, 1000)]
    private float pitchGain = 1f;

    [Header("Closed Loop Settings")]
    [SerializeField]
    private bool closedLoopPosition = true;

    [SerializeField]
    private bool closedLoopOrientation = true;

    [SerializeField]
    private bool accumulatePosition = true;

    [SerializeField]
    private bool accumulateRotation = true;

    private ZmqListener _zmqListener;
    private Vector3 accumulatedPosition;
    private Quaternion accumulatedRotation;

    private void Start()
    {
        _zmqListener = GetComponent<ZmqListener>();
        ResetPosition();
        ResetRotation();
    }

    private void Update()
    {
        HandleInput();

        if (_zmqListener.pose != null)
        {
            UpdateTransform();
        }
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.O))
            ToggleClosedLoopOrientation();
        if (Input.GetKeyDown(KeyCode.P))
            ToggleClosedLoopPosition();
        if (Input.GetKeyDown(KeyCode.LeftBracket))
            ToggleAccumulateRotation();
        if (Input.GetKeyDown(KeyCode.RightBracket))
            ToggleAccumulatePosition();
        if (Input.GetKeyDown(KeyCode.R))
            ResetPosition();
        if (Input.GetKeyDown(KeyCode.T))
            ResetRotation();

        if (Input.GetKeyUp(KeyCode.Escape))
        {
            Application.Quit();
        }
    }

    private void UpdateTransform()
    {
        Vector3 newPosition = _zmqListener.pose.position;
        Quaternion newRotation = _zmqListener.pose.rotation;

        // Handle position (Force mode)
        if (closedLoopPosition && IsValidVector3(newPosition))
        {
            // Calculate position change based on input, applying gains and scaling
            Vector3 positionChange =
                new Vector3(
                    newPosition.x * xGain,
                    newPosition.z * -zGain, // Note: Z and Y are swapped and Y is negated
                    -newPosition.y * yGain
                ) * sphereRadius;

            if (accumulatePosition)
            {
                // Treat position change as velocity for continuous movement
                Vector3 velocity = positionChange;
                transform.Translate(velocity * Time.deltaTime, Space.World);
            }
            else
            {
                // Set position directly for immediate response
                transform.position = initialPosition + positionChange;
            }
        }

        // Handle rotation
        if (closedLoopOrientation && IsValidQuaternion(newRotation))
        {
            if (accumulateRotation)
            {
                // Torque mode: Continuous rotation based on input
                Quaternion rotationDelta = Quaternion.identity;

                // Pitch: Rotation around local X-axis (right axis in Unity)
                rotationDelta *= Quaternion.AngleAxis(-newRotation.x * pitchGain, transform.right);

                // Yaw: Rotation around local Y-axis (up axis in Unity)
                rotationDelta *= Quaternion.AngleAxis(newRotation.y * yawGain, transform.up);

                // Roll: Rotation around local Z-axis (forward axis in Unity)
                rotationDelta *= Quaternion.AngleAxis(newRotation.z * rollGain, transform.forward);

                // Apply the calculated rotation
                transform.rotation *= rotationDelta;
            }
            else
            {
                // Direct rotation mode
                Vector3 eulerRotation = newRotation.eulerAngles;
                eulerRotation.x = -eulerRotation.x; // Flip the X-axis rotation
                Quaternion flippedRotation = Quaternion.Euler(eulerRotation);

                // Combine with initial rotation and apply
                Quaternion targetRotation = Quaternion.Euler(initialRotation) * flippedRotation;
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.deltaTime * 20f
                );
            }
        }
    }

    public void ResetPosition()
    {
        transform.position = initialPosition;
        accumulatedPosition = Vector3.zero;
        Debug.Log("Position reset");
    }

    public void ResetRotation()
    {
        transform.rotation = Quaternion.Euler(initialRotation);
        accumulatedRotation = Quaternion.identity;
        Debug.Log("Rotation reset");
    }

    public void ToggleClosedLoopPosition()
    {
        closedLoopPosition = !closedLoopPosition;
        Debug.Log($"Closed Loop Position: {(closedLoopPosition ? "ON" : "OFF")}");
    }

    public void ToggleClosedLoopOrientation()
    {
        closedLoopOrientation = !closedLoopOrientation;
        Debug.Log($"Closed Loop Orientation: {(closedLoopOrientation ? "ON" : "OFF")}");
    }

    public void ToggleAccumulatePosition()
    {
        accumulatePosition = !accumulatePosition;
        if (!accumulatePosition)
            ResetPosition();
        Debug.Log($"Accumulate Position: {(accumulatePosition ? "ON" : "OFF")}");
    }

    public void ToggleAccumulateRotation()
    {
        accumulateRotation = !accumulateRotation;
        if (!accumulateRotation)
            ResetRotation();
        Debug.Log($"Accumulate Rotation: {(accumulateRotation ? "ON" : "OFF")}");
    }

    private bool IsValidVector3(Vector3 v)
    {
        return !float.IsNaN(v.x)
            && !float.IsNaN(v.y)
            && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x)
            && !float.IsInfinity(v.y)
            && !float.IsInfinity(v.z);
    }

    private bool IsValidQuaternion(Quaternion q)
    {
        return !float.IsNaN(q.x)
            && !float.IsNaN(q.y)
            && !float.IsNaN(q.z)
            && !float.IsNaN(q.w)
            && !float.IsInfinity(q.x)
            && !float.IsInfinity(q.y)
            && !float.IsInfinity(q.z)
            && !float.IsInfinity(q.w);
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
}
