using UnityEngine;

public class ClosedLoop : MonoBehaviour
{
    [Header("Gain Settings")]
    [SerializeField, Range(0, 1000)]
    private float xGain = 100.0f;
    [SerializeField, Range(0, 1000)]
    private float yGain = 100.0f;
    [SerializeField, Range(0, 1000)]
    private float zGain = 100.0f;
    [SerializeField, Range(0, 1000)]
    private float rollGain = 100.0f;
    [SerializeField, Range(0, 1000)]
    private float pitchGain = 100.0f;
    [SerializeField, Range(0, 1000)]
    private float yawGain = 100.0f;

    [Header("Loop Settings")]
    [Tooltip("Toggle closed loop orientation.")]
    private bool closedLoopOrientation = false;
    [Tooltip("Toggle closed loop position.")]
    private bool closedLoopPosition = false;
    [Tooltip("To close the loop on the raw position or on velocity as applying a force or torque")]
    [SerializeField]
    private bool momentumClosedLoop = false;

    [Header("Position Offset")]
    [Tooltip("Initial position offset.")]
    private Vector3 posOffset = Vector3.zero;

    [Header("Rotation Offset")]
    [Tooltip("Initial rotation offset.")]
    private Quaternion rotOffset = Quaternion.identity;

    private Camera mainCamera;
    private ZmqListener _zmqListener;

    private void Awake()
    {
        _zmqListener = GetComponent<ZmqListener>();
        mainCamera = Camera.main;
        Application.targetFrameRate = 60;
        Time.fixedDeltaTime = 1f / 60f;
    }

    private void Update()
    {
        HandleInput();
        ApplyTransformations();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.O))
        {
            ToggleClosedLoopOrientation();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            ToggleClosedLoopPosition();
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleMomentumClosedLoop();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetTransform();
        }
    }

    private void ApplyTransformations()
    {
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera is missing in the scene");
            return;
        }

        if (_zmqListener.pose == null)
        {
            return;
        }

        if (closedLoopPosition)
        {
            ApplyPositionTransform();
        }

        if (closedLoopOrientation)
        {
            ApplyOrientationTransform();
        }
    }

    private void ApplyPositionTransform()
    {
        Vector3 newPosition;
        if (momentumClosedLoop)
        {
            // Apply force based on the camera's orientation and ZMQ listener's data
            newPosition = transform.position + mainCamera.transform.forward * _zmqListener.pose.position.y * zGain;
            newPosition += mainCamera.transform.right * _zmqListener.pose.position.x * xGain;
            newPosition += mainCamera.transform.up * _zmqListener.pose.position.z * yGain;
        }
        else
        {
            // Directly set position and subtract the posOffset
            newPosition = new Vector3(
                _zmqListener.pose.position.x * xGain,
                _zmqListener.pose.position.z * zGain,
                _zmqListener.pose.position.y * yGain
            ) - posOffset;
        }
        transform.position = newPosition;
    }


    private void ApplyOrientationTransform()
    {
        if (momentumClosedLoop)
        {
            // Apply torque
            transform.Rotate(
                new Vector3(
                    _zmqListener.pose.rotation.x * rollGain,
                    _zmqListener.pose.rotation.y * yawGain,
                    _zmqListener.pose.rotation.z * pitchGain
                )
            );
        }
        else
        {
            // Directly set rotation
            transform.rotation = Quaternion.Euler(
                _zmqListener.pose.rotation.eulerAngles.x * pitchGain,
                _zmqListener.pose.rotation.eulerAngles.y * yawGain,
                _zmqListener.pose.rotation.eulerAngles.z * rollGain
            );
        }
    }

    private void ToggleClosedLoopOrientation()
    {
        closedLoopOrientation = !closedLoopOrientation;
    }

    private void ToggleClosedLoopPosition()
    {
        closedLoopPosition = !closedLoopPosition;
    }

    private void ToggleMomentumClosedLoop()
    {
        momentumClosedLoop = !momentumClosedLoop;
    }

   private void ResetTransform()
{
    // Set position and rotation to (0,0,0)
    transform.position = Vector3.zero;
    transform.rotation = Quaternion.Euler(Vector3.zero);

    // Update posOffset and rotOffset to current _zmqListener values
    if (_zmqListener.pose != null)
    {
        posOffset = new Vector3(
            _zmqListener.pose.position.x * xGain,
            _zmqListener.pose.position.z * zGain,
            _zmqListener.pose.position.y * yGain
        );

        rotOffset = Quaternion.Euler(
            _zmqListener.pose.rotation.eulerAngles.x * pitchGain,
            _zmqListener.pose.rotation.eulerAngles.y * yawGain,
            _zmqListener.pose.rotation.eulerAngles.z * rollGain
        );
    }
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

public void SetMomentumClosedLoop(bool value)
{
    momentumClosedLoop = value;
}

public void SetPositionOffset(Vector3 offset)
{
    posOffset = offset;
}
// Public method to set rotation offset from external scripts
public void SetRotationOffset(Quaternion offset)
{
    rotOffset = offset;
}
}
