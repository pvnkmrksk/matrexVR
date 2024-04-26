using UnityEngine;

public class ClosedLoop : MonoBehaviour
{
    [Header("Initial Settings")]
    public Vector3 initialPosition = Vector3.zero;
    public Vector3 initialRotation = Vector3.zero;

    [Header("FicTrac Settings")]
    public float sphereRadius = 0.45f; // Default value for sphere radius in centimeters

    [Header("Gain Settings")]
    [SerializeField, Range(0, 1000)]
    private float xGain = 100.0f;
    [SerializeField, Range(0, 1000)]
    private float yGain = 100.0f;
    [SerializeField, Range(0, 1000)]
    private float zGain = 100.0f;
    [SerializeField, Range(0, 100)]
    private float rollGain = 1.0f;
    [SerializeField, Range(0, 100)]
    private float pitchGain = 1.0f;
    [SerializeField, Range(0, 100)]
    private float yawGain = 1.0f;

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
                currentSensorData.x - lastSensorData.x, // Unity's X is right, sensor's X is right

                (currentSensorData.z - lastSensorData.z), // Unity's Y is up, sensor's Z is down (negative Unity Y)
                -(currentSensorData.y - lastSensorData.y)  // Unity's Z is forward, sensor's Y is backward

            );

            // Apply the gains and sphereRadius scaling to position delta
            positionDelta = new Vector3(positionDelta.x * yGain, positionDelta.y * -zGain, positionDelta.z * xGain) * sphereRadius;

            // Apply position deltas
            transform.position += positionDelta;
        }

        if (closedLoopOrientation)
            {
            // Calculate rotation deltas
            // Vector3 rotationDelta = new Vector3(
            //     currentSensorData.pitch - lastSensorData.pitch, // Pitch - rotation around X-axis
            //     currentSensorData.yaw - lastSensorData.yaw,     // Yaw - rotation around Y-axis
            //     currentSensorData.roll - lastSensorData.roll    // Roll - rotation around Z-axis
            // );

            // // Apply the gains to rotation delta differential/delta rotation
            // rotationDelta = new Vector3(rotationDelta.x * pitchGain, rotationDelta.y * yawGain, rotationDelta.z * rollGain);

            // Apply rotation deltas as torque by using the direct sensor data scaled with the gains, integrated over time/torque
            transform.Rotate(- currentSensorData.pitch * pitchGain, 
                            currentSensorData.yaw * yawGain, 
                            - currentSensorData.roll * rollGain, Space.Self);

            // transform.Rotate(rotationDelta.x, rotationDelta.y, rotationDelta.z, Space.Self);
            

            // Update lastSensorData for the next frame
            lastSensorData = currentSensorData;
            }
    }

    public void ResetPosition()
    {
        transform.position = initialPosition;
        //lastSensorData = currentSensorData;
    }

    public void ResetRotation()
    {
        transform.rotation = Quaternion.Euler(initialRotation);
        //lastSensorData = currentSensorData;
 
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

