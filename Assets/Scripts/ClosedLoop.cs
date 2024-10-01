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

    private void Start()
    {
        sphereRadius = sphereDiameter / 2f;
        _zmqListener = GetComponent<ZmqListener>();
        if (_zmqListener == null)
            Debug.LogError("ZmqListener component not found!");
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        ResetPositionAndRotation();
    }

    private void Update()
    {
        HandleInput();

        if (_zmqListener.pose == null) return;

        if (Input.GetKeyDown(resetKey))
        {
            ResetPositionAndRotation();
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
        _ficTracRotationOffset = Quaternion.Euler(0, -initialYaw * Mathf.Rad2Deg, 0);
        _isInitialized = true;
        Debug.Log($"Initialized with FicTrac data: ({_lastFicTracData.x}, {_lastFicTracData.y}, {_lastFicTracData.z})");
    }

    private void UpdateTransform()
    {
        Vector3 currentFicTracData = GetCurrentFicTracData();
        Vector3 ficTracDelta = currentFicTracData - _lastFicTracData;

        // Apply position change only if closedLoopPosition is true
        if (closedLoopPosition)
        {
            Vector3 positionDelta = _ficTracRotationOffset * new Vector3(ficTracDelta.x, 0, ficTracDelta.y) * sphereRadius;
            transform.Translate(positionDelta, Space.World);
            
        }

        // Apply rotation change only if closedLoopOrientation is true
        if (closedLoopOrientation)
        {
            float rotationDelta = ficTracDelta.z * Mathf.Rad2Deg;
            transform.Rotate(0, rotationDelta, 0, Space.World);
        }

        _lastFicTracData = currentFicTracData;
    }

    public void ResetPositionAndRotation()
    {
        transform.SetPositionAndRotation(_initialPosition, _initialRotation);
        _isInitialized = false;
        _ficTracRotationOffset = Quaternion.identity;
        _initializationTimer = 0f;
        _lastFicTracData = Vector3.zero;
        Debug.Log("Reset to initial position and rotation. Waiting for re-initialization...");
    }

    private Vector3 GetCurrentFicTracData()
    {
        Pose pose = _zmqListener.pose;
        return new Vector3(pose.position.y, pose.position.x, pose.rotation.eulerAngles.y * Mathf.Deg2Rad);
    }

    // New methods
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

    // Public methods for external scripts to control the behaviors
    public void SetClosedLoopOrientation(bool value)
    {
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

        transform.SetPositionAndRotation(_initialPosition, _initialRotation);

    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.O))
            ToggleClosedLoopOrientation();
        if (Input.GetKeyDown(KeyCode.P))
            ToggleClosedLoopPosition();

        if (Input.GetKeyUp(KeyCode.Escape))
        {
            Application.Quit();
        }
    }
}