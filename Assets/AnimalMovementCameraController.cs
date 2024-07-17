using UnityEngine;

public class CorrectFicTracUnityController : MonoBehaviour
{
    [SerializeField] private float sphereRadius = 1f;
    [SerializeField] private KeyCode resetKey = KeyCode.R; // Add this line

    private UdpAnimalDataReceiver _dataReceiver;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Vector3 _lastFicTracData;
    private bool _isInitialized = false;
    private const int COL_X = 15;
    private const int COL_Y = 16;
    private const int COL_ROT = 17;

    private void Start()
    {
        _dataReceiver = GetComponent<UdpAnimalDataReceiver>();
        if (_dataReceiver == null)
            Debug.LogError("UdpAnimalDataReceiver component not found!");
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
    }

    private void Update()
    {
        if (_dataReceiver?.AnimalData == null) return;

        if (Input.GetKeyDown(resetKey)) // Add this check
        {
            ResetPositionAndRotation();
            return;
        }

        if (!_isInitialized)
            InitializeFicTracData();
        else
            UpdateTransform();
    }

    private void InitializeFicTracData()
    {
        _lastFicTracData = GetCurrentFicTracData();
        _isInitialized = true;
        Debug.Log($"Initialized with FicTrac data: ({_lastFicTracData.x}, {_lastFicTracData.y}, {_lastFicTracData.z})");
    }

    private void UpdateTransform()
    {
        Vector3 currentFicTracData = GetCurrentFicTracData();
        Vector3 ficTracDelta = currentFicTracData - _lastFicTracData;

        // Apply position change
        Vector3 positionDelta = new Vector3(ficTracDelta.x, 0, ficTracDelta.y) * sphereRadius;
        transform.Translate(positionDelta, Space.World);

        // Apply rotation change
        float rotationDelta = ficTracDelta.z * Mathf.Rad2Deg;
        transform.Rotate(0, rotationDelta, 0, Space.World);

        _lastFicTracData = currentFicTracData;
    }

    public void ResetPositionAndRotation()
    {
        transform.SetPositionAndRotation(_initialPosition, _initialRotation);
        _isInitialized = false; // Add this line
        InitializeFicTracData(); // Re-initialize FicTrac data reference
        Debug.Log("Reset to initial position and rotation. Re-initialized FicTrac data reference.");
    }

    private Vector3 GetCurrentFicTracData()
    {
        float[] data = _dataReceiver.AnimalData;
        return new Vector3(data[COL_Y], data[COL_X], data[COL_ROT]);
    }
}