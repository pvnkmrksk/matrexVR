using UnityEngine;

public class AnimalMovementCameraController : MonoBehaviour
{
    [SerializeField] private float sphereRadius = 1f; // Radius of the sphere in Unity units
    [SerializeField] private Vector3 initialPosition = Vector3.zero;
    [SerializeField] private Vector3 initialRotation = Vector3.zero;

    private Vector2 _integratedPosition;
    private float _integratedHeading;
    private Vector2 _positionOffset;
    private float _headingOffset;

    private UdpAnimalDataReceiver _dataReceiver;

    private void Start()
    {
        _dataReceiver = GetComponent<UdpAnimalDataReceiver>();
        if (_dataReceiver == null)
        {
            Debug.LogError("UdpAnimalDataReceiver component not found!");
        }
        ResetPosition();
        ResetRotation();
    }

    private void Update()
    {
        if (_dataReceiver != null && _dataReceiver.AnimalData != null)
        {
            UpdateTransform(_dataReceiver.AnimalData);
        }
    }

    private void UpdateTransform(float[] data)
    {
        // Update position
        _integratedPosition.x = data[14]; // Integrated x position (lab)
        _integratedPosition.y = data[15]; // Integrated y position (lab)
        Vector3 newPosition = new Vector3(
            (_integratedPosition.x - _positionOffset.x) * sphereRadius,
            0,
            (_integratedPosition.y - _positionOffset.y) * sphereRadius
        );
        transform.position = initialPosition + newPosition;

        // Update rotation
        _integratedHeading = data[16]; // Integrated animal heading (lab)
        float newHeading = (_integratedHeading - _headingOffset) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(initialRotation.x, newHeading, initialRotation.z);
    }

    public void ResetPosition()
    {
        _positionOffset = _integratedPosition;
        transform.position = initialPosition;
    }

    public void ResetRotation()
    {
        _headingOffset = _integratedHeading;
        transform.rotation = Quaternion.Euler(initialRotation);
    }
}