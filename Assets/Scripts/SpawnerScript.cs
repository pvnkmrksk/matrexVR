using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnerScript : MonoBehaviour
{
    // Reference to the prefab to spawn
    public GameObject instancePrefab;

    // Parameters
    public float speed = 5f;
    public float mu = 0f; // Mean direction in degrees (on the XZ plane)
    public Vector3 bandCenter = Vector3.zero; // XYZ of the band center
    public float bandWidth = 10f;
    public float bandLength = 5f;

    public float boundaryWidth = 20f; // Width along the X-axis chiyu note: this can be the same as bandlength, the orientation of the instance moving direction is not correct
    public float boundaryDepth = 20f; // Depth along the Z-axi chiyu note: this can be the same as bandlength
    
    public int numberOfInstances = 10;
    public float kappa = 0f; // Orientation parameter
    public bool moveWithTransform = false;
    public Transform targetTransform;

    private GameObject[] instances;
    private Vector3[] initialRelativePositions;

    void Start()
    {
        SpawnInstances();
    }

    void SpawnInstances()
    {
        for (int i = 0; i < numberOfInstances; i++)
        {
            // Random position within the band

            Vector3 position = bandCenter + new Vector3(
                Random.Range(-bandWidth / 2f, bandWidth / 2f),
                0f, // Y is zero for ground plane
                Random.Range(-bandLength / 2f, bandLength / 2f)
            );
            // Instantiate the prefab
            GameObject instance = Instantiate(instancePrefab, position, Quaternion.identity);

            // Get orientation angle using Von Mises distribution
            float angle = GetOrientationAngle();

            // Convert angle to direction vector
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            // Apply rotation
            instance.transform.rotation = Quaternion.LookRotation(direction);

            // Add movement script to instance
            MovementScript movement = instance.AddComponent<MovementScript>();
            movement.direction = direction.normalized;
            movement.speed = speed;
        }
    }

    float GetOrientationAngle()
    {
        // Mean direction in radians
        float muRadians = mu * Mathf.Deg2Rad;

        if (kappa <= 0f)
        {
            // Uniform random orientation between 0 and 360 degrees
            return Random.Range(0f, 2f * Mathf.PI);
        }
        else
        {
            // Generate angle using Von Mises distribution
            return SampleVonMises(muRadians, kappa);
        }
    }

    float SampleVonMises(float mu, float kappa)
    {
        // Implementation of the Von Mises sampling
        // For simplicity, we'll use a basic approximation

        float s = 0.5f / kappa;
        float r = s + Mathf.Sqrt(1f + s * s);

        while (true)
        {
            float u1 = Random.Range(0f, 1f);
            float z = Mathf.Cos(Mathf.PI * u1);
            float f = (1f + r * z) / (r + z);
            float c = kappa * (r - f);

            float u2 = Random.Range(0f, 1f);

            if (u2 < c * (2f - c) || u2 <= c * Mathf.Exp(1f - c))
            {
                float u3 = Random.Range(0f, 1f);
                float sign = (u3 - 0.5f) >= 0f ? 1f : -1f;
                float theta = sign * Mathf.Acos(f);
                return mu + theta;
            }
        }
    }
}