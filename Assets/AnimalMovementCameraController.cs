using UnityEngine;

public class SimpleFicTracUnityController : MonoBehaviour
{
    [SerializeField] private float sphereRadius = 1f; // Radius of the sphere in Unity units

    private UdpAnimalDataReceiver _dataReceiver;

    private void Start()
    {
        _dataReceiver = GetComponent<UdpAnimalDataReceiver>();
        if (_dataReceiver == null)
        {
            Debug.LogError("UdpAnimalDataReceiver component not found!");
        }
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
        // Using columns 15 and 14 for X and Z in Unity (swapped for correct orientation)
        float x = data[16] * sphereRadius; // FicTrac Y (east) to Unity X
        float z = data[15] * sphereRadius; // FicTrac X (north) to Unity Z
        transform.position = new Vector3(x, 0, z);

        // Update rotation
        // Using column 16 for rotation around Y axis in Unity
        float yRotation = data[17] * Mathf.Rad2Deg; // Convert to degrees and negate for Unity's clockwise rotation
        transform.rotation = Quaternion.Euler(0, yRotation, 0);
    }
}