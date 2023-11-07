using UnityEngine;

public class ClosedLoop : MonoBehaviour
{
    [Header("Initial Settings")]
    public Vector3 initialPosition = Vector3.zero;
    public Vector3 initialRotation = Vector3.zero;

    [Header("FicTrac Settings")]
    public float sphereRadius = 5.0f; // Default value for sphere radius in centimeters

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
    public bool closedLoopOrientation = true;
    [Tooltip("Toggle closed loop position.")]
    public bool closedLoopPosition = true;

    // Placeholder structure for the sensor data
    private struct SensorData
    {
        public float x;
        public float y;
        public float z;
        public float roll;
        public float pitch;
        public float yaw;
    }

    private SensorData lastSensorData;
    private SensorData currentSensorData;

    // Reference to the ZmqListener component
    private ZmqListener _zmqListener;

    // Reference to the main camera
    private Camera mainCamera;

    // Position and Rotation Offsets
    private Vector3 posOffset = Vector3.zero;
    private Quaternion rotOffset = Quaternion.identity;

    private void Start()
    {
        _zmqListener = GetComponent<ZmqListener>();
        mainCamera = Camera.main;
        Application.targetFrameRate = 60;
        Time.fixedDeltaTime = 1f / 60f;

        // Set initial sensor data
        UpdateSensorData();

        // Apply the initial position to the transform
        transform.position = initialPosition;
        transform.rotation = Quaternion.Euler(initialRotation);
    }

    private void Update()
    {
        HandleInput();
        UpdateSensorData();
        ApplyDeltaTransformations();
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

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetPosition();
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            ResetRotation();
        }
    }

    private void UpdateSensorData()
    {
        if (_zmqListener.pose != null)
        {
            // Update current sensor data from the ZmqListener
            currentSensorData.x = _zmqListener.pose.position.x;
            currentSensorData.y = _zmqListener.pose.position.y;
            currentSensorData.z = _zmqListener.pose.position.z;
            currentSensorData.pitch = _zmqListener.pose.rotation.eulerAngles.x;
            currentSensorData.yaw = _zmqListener.pose.rotation.eulerAngles.y;
            currentSensorData.roll = _zmqListener.pose.rotation.eulerAngles.z;
        }
    }

    private void ApplyDeltaTransformations()
    {
           if (closedLoopPosition)
        {
            // Calculate position deltas
        Vector3 positionDelta = new Vector3(
            currentSensorData.y - lastSensorData.y,
            -(currentSensorData.z - lastSensorData.z), // Unity's Y is up, sensor's Z is up (negative Unity Y)
            currentSensorData.x - lastSensorData.x
        );

        // Apply the gains and sphereRadius scaling to position delta
        positionDelta = new Vector3(positionDelta.x * yGain, positionDelta.y * -zGain, positionDelta.z * xGain) * sphereRadius;

        // Apply position deltas
        transform.position += positionDelta;
        }
           if (closedLoopOrientation)
        {
        // Calculate rotation deltas
        Vector3 rotationDelta = new Vector3(
            currentSensorData.pitch - lastSensorData.pitch, // Pitch - rotation around X-axis
            currentSensorData.yaw - lastSensorData.yaw,     // Yaw - rotation around Y-axis
            currentSensorData.roll - lastSensorData.roll    // Roll - rotation around Z-axis
        );

        // Apply the gains to rotation delta
        rotationDelta = new Vector3(rotationDelta.x * pitchGain, rotationDelta.y * yawGain, rotationDelta.z * rollGain);

        // Apply rotation deltas as torque
        transform.Rotate(rotationDelta.x, rotationDelta.y, rotationDelta.z, Space.Self);

        // Update lastSensorData for the next frame
        lastSensorData = currentSensorData;
        }
    }

    public void ResetPosition()
    {
        transform.position = initialPosition;
        lastSensorData = currentSensorData;
    }

    public void ResetRotation()
    {
        transform.rotation = Quaternion.Euler(initialRotation);
        lastSensorData = currentSensorData;
 
    }
    private void ToggleClosedLoopOrientation()
    {
        closedLoopOrientation = !closedLoopOrientation;
    }

    private void ToggleClosedLoopPosition()
    {
        closedLoopPosition = !closedLoopPosition;
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

