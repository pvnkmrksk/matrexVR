using UnityEngine;

public class CorrectFicTracUnityController : MonoBehaviour
{
    [SerializeField] private float sphereRadius = 1f;
    [SerializeField] private KeyCode resetKey = KeyCode.R;
    [SerializeField] private float initializationDelay = 0.1f; // Delay before starting to use FicTrac data

    private UdpAnimalDataReceiver _dataReceiver;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Vector3 _lastFicTracData;
    private bool _isInitialized = false;
    private const int COL_X = 15;
    private const int COL_Y = 16;
    private const int COL_ROT = 17;
    private Quaternion _ficTracRotationOffset;
    private float _initializationTimer;

    private void Start()
    {
        _dataReceiver = GetComponent<UdpAnimalDataReceiver>();
        if (_dataReceiver == null)
            Debug.LogError("UdpAnimalDataReceiver component not found!");
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        ResetPositionAndRotation();
    }

    private void Update()
    {
        if (_dataReceiver?.AnimalData == null) return;

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
        float[] data = _dataReceiver.AnimalData;
        return new Vector3(data[COL_Y], data[COL_X], data[COL_ROT]);
    }
}