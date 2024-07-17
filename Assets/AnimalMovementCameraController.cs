using UnityEngine;

public class CorrectFicTracUnityController : MonoBehaviour
{
    [SerializeField] private float sphereRadius = 1f;
    [SerializeField] private KeyCode resetKey = KeyCode.R;
    [SerializeField] private float initializationDelay = 0.1f;

    private ZmqListener _zmqListener;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Vector3 _lastFicTracData;
    private bool _isInitialized = false;
    private Quaternion _ficTracRotationOffset;
    private float _initializationTimer;

    private void Start()
    {
        _zmqListener = GetComponent<ZmqListener>();
        if (_zmqListener == null)
            Debug.LogError("ZmqListener component not found!");
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        ResetPositionAndRotation();
    }

    private void Update()
    {
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

        // Apply position change
        Vector3 positionDelta = _ficTracRotationOffset * new Vector3(ficTracDelta.x, 0, ficTracDelta.y) * sphereRadius;
        transform.Translate(positionDelta, Space.World);

        // Apply rotation change
        float rotationDelta = ficTracDelta.z * Mathf.Rad2Deg;
        transform.Rotate(0, rotationDelta, 0, Space.World);

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
        // Assuming the ZMQ data is in the same format as your previous UDP data
        // You may need to adjust this based on the actual data format from ZMQ
        return new Vector3(pose.position.y, pose.position.x, pose.rotation.eulerAngles.y * Mathf.Deg2Rad);
    }
}