using UnityEngine;

public class ClosedLoop : MonoBehaviour
{
    [Header("Initial Settings")]
    public Vector3 initialPosition = Vector3.zero;
    public Vector3 initialRotation = Vector3.zero;

    [Header("FicTrac Settings")]
    [Tooltip("Radius of the sphere in centimeters")]
    public float sphereRadius = 5f; // Default value for sphere radius in centimeters

    [Header("Gain Settings")]
    [SerializeField, Range(0, 1000)]
    private float xGain = 100.0f;

    [SerializeField, Range(0, 1000)]
    private float yGain = 100.0f;

    [SerializeField, Range(0, 1000)]
    private float zGain = 100.0f;

    [SerializeField, Range(0, 10)]
    private float rotationGain = 1f;

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

        // Handle position
        if (closedLoopPosition && IsValidVector3(newPosition))
        {
            Vector3 positionChange =
                new Vector3(newPosition.x * xGain, newPosition.z * -zGain, -newPosition.y * yGain)
                * sphereRadius;

            if (accumulatePosition)
            {
                accumulatedPosition += positionChange * Time.deltaTime;
                transform.position = initialPosition + accumulatedPosition;
            }
            else
            {
                transform.position = initialPosition + positionChange;
            }
        }

        // Handle rotation
        if (closedLoopOrientation && IsValidQuaternion(newRotation))
        {
            Quaternion rotationChange = Quaternion.Slerp(
                Quaternion.identity,
                newRotation,
                rotationGain
            );

            if (accumulateRotation)
            {
                accumulatedRotation *= rotationChange;
                transform.rotation = Quaternion.Euler(initialRotation) * accumulatedRotation;
            }
            else
            {
                transform.rotation = Quaternion.Euler(initialRotation) * rotationChange;
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
