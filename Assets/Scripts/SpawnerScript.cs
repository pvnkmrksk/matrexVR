using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnerScript : MonoBehaviour
{
    // Reference to the prefab to spawn
    public GameObject instancePrefab;

    // Parameters
    public float speed = 2f;
    public float mu = 0f; // Mean direction in degrees (on the XZ plane)
    public Vector3 bandCenter = Vector3.zero; // XYZ of the band center
    public float bandWidth = 30f;
    public float bandDepth = 50f;

    public float boundaryWidth = 30f; // Width along the X-axis chiyu note: this can be the same as bandlength, the orientation of the instance moving direction is not correct
    public float boundaryDepth = 50f; // Depth along the Z-axi chiyu note: this can be the same as bandlength
    
    public int numberOfInstances = 32;
    public float kappa = 10000f; // Orientation parameter
    public bool moveWithTransform = false;
    public Transform targetTransform;

    // Visibility Parameters
    public bool enableVisibilityCycling = false;
    public float visibleOffDuration = 4f;      // Total cycle duration
    public float visibleOnDuration = 1f;    // Duration when the instance is visible

    private float cycleDuration;

    [Tooltip("Phase offset duration between 0 and total duration in seconds.")]    public float phaseOffset = 0f;        // Phase offset at the start in seconds

    public bool randomizePhase = false;   // Randomize phase for each instance

    public float minimumDistance = 1f; // Minimum distance between instances

    // New parameter for uniform distribution
    public bool useHexagonalGrid = false;
    private GameObject[] instances;
    private Vector3[] initialRelativePositions;


    void Start()
    {
        if (useHexagonalGrid)
        {
            float hexagonRadius = CalculateHexagonRadius();
            List<Vector3> positions = GenerateHexagonalGridPositions(hexagonRadius);
            int maxInstances = Mathf.Min(numberOfInstances, positions.Count);

            instances = new GameObject[maxInstances];
            initialRelativePositions = new Vector3[maxInstances];
        }
        else
        {
            instances = new GameObject[numberOfInstances];
            initialRelativePositions = new Vector3[numberOfInstances];
        }

        SpawnInstances();
    }
    void Update()
    {
        if (moveWithTransform && targetTransform != null)
        {
            // Update the band and periodic boundaries to follow the targetTransform
            bandCenter = targetTransform.position;

            // Update instances' positions relative to the targetTransform
            for (int i = 0; i < instances.Length; i++)
            {
                if (instances[i] != null)
                {
                    instances[i].transform.position = targetTransform.position + initialRelativePositions[i];
                }
            }
        }
    }

    void SpawnInstances()
    {
        if (useHexagonalGrid)
        {
            float hexagonRadius = CalculateHexagonRadius();
            List<Vector3> positions = GenerateHexagonalGridPositions(hexagonRadius);

            int instancesToSpawn = Mathf.Min(numberOfInstances, positions.Count);

            for (int i = 0; i < instancesToSpawn; i++)
            {
                Vector3 position = positions[i];

                // Proceed with instantiation and setup
                SpawnInstanceAtPosition(position, i);
            }
        }
        else if (minimumDistance > 0f)
        {
            // Grid-based placement with randomness
            // Calculate number of instances along each axis based on desired minimum distance

            int countX = Mathf.FloorToInt(bandWidth / minimumDistance);
            int countZ = Mathf.FloorToInt(bandDepth / minimumDistance);
            int totalPositions = countX * countZ;

            int instancesToSpawn = Mathf.Min(numberOfInstances, totalPositions);

            List<Vector3> positions = new List<Vector3>();
            float startX = bandCenter.x - bandWidth / 2f;
            float startZ = bandCenter.z - bandDepth / 2f;
            float stepX = bandWidth / countX;
            float stepZ = bandDepth / countZ;

            for (int x = 0; x < countX; x++)
            {
                for (int z = 0; z < countZ; z++)
                {
                    float posX = startX + x * stepX + Random.Range(-stepX / 4f, stepX / 4f);
                    float posZ = startZ + z * stepZ + Random.Range(-stepZ / 4f, stepZ / 4f);
                    positions.Add(new Vector3(posX, 0f, posZ));
                }
            }

            Shuffle(positions);

            for (int i = 0; i < instancesToSpawn; i++)
            {
                Vector3 position = positions[i];
                SpawnInstanceAtPosition(position, i);
            }
        }
        else
        {
                        // Random distribution using the provided method
            for (int i = 0; i < numberOfInstances; i++)
            {
                // Random position within the band
                Vector3 position = bandCenter + new Vector3(
                    Random.Range(-bandWidth / 2f, bandWidth / 2f),
                    0f, // Y is zero for ground plane
                    Random.Range(-bandDepth / 2f, bandDepth / 2f)
                );

                // Proceed with instantiation and setup
                SpawnInstanceAtPosition(position, i);
            }  
        }

    }

    void SpawnInstanceAtPosition(Vector3 position, int index)
    {
        if (moveWithTransform && targetTransform != null)
        {
            initialRelativePositions[index] = position - targetTransform.position;
            position = targetTransform.position + initialRelativePositions[index];
        }

        GameObject instance = Instantiate(instancePrefab, position, Quaternion.identity);

        // Add and configure PeriodicBoundary component
        PeriodicBoundary periodicBoundary = instance.AddComponent<PeriodicBoundary>();
        periodicBoundary.boundaryCenter = bandCenter;
        periodicBoundary.boundaryWidth = boundaryWidth;
        periodicBoundary.boundaryDepth = boundaryDepth;
        periodicBoundary.moveWithTransform = moveWithTransform;
        periodicBoundary.targetTransform = targetTransform;

        // Add and configure DirectionalMovement component
        DirectionalMovement movement = instance.AddComponent<DirectionalMovement>();
        movement.SetSpeed(speed);
        movement.SetDirection(GetOrientationAngle() * Mathf.Rad2Deg);

        if (enableVisibilityCycling)
        {
            VisibilityScript visibility = instance.AddComponent<VisibilityScript>();
            visibility.visibleOffDuration = visibleOffDuration;
            visibility.visibleOnDuration = visibleOnDuration;
            cycleDuration = visibleOffDuration + visibleOnDuration;

            if (randomizePhase)
            {
                visibility.phaseOffset = Random.Range(0f, cycleDuration);
            }
            else
            {
                visibility.phaseOffset = phaseOffset;
            }
        }

        instances[index] = instance;
    }

    float CalculateHexagonRadius()
    {
        // Total band area
        float bandArea = bandWidth * bandDepth;

        // Area per instance
        float areaPerInstance = bandArea / numberOfInstances;

        // Calculate hexagon radius
        float hexagonRadius = Mathf.Sqrt((2f * areaPerInstance) / (3f * Mathf.Sqrt(3f)));

        return hexagonRadius;
    }

    List<Vector3> GenerateHexagonalGridPositions(float hexagonRadius)
    {
        List<Vector3> positions = new List<Vector3>();

        // Hexagon dimensions
        float hexWidth = 2f * hexagonRadius;
        float hexHeight = Mathf.Sqrt(3f) * hexagonRadius;

        // Spacing between hexagon centers
        float horizSpacing = hexWidth * 0.75f;
        float vertSpacing = hexHeight;

        // Number of hexagons that fit within the band dimensions
        int numHexX = Mathf.CeilToInt(bandWidth / horizSpacing);
        int numHexZ = Mathf.CeilToInt(bandDepth / vertSpacing);

        // Starting offsets to center the grid
        float offsetX = bandCenter.x - ((numHexX - 1) * horizSpacing) / 2f;
        float offsetZ = bandCenter.z - ((numHexZ - 1) * vertSpacing) / 2f;

        for (int x = 0; x < numHexX; x++)
        {
            for (int z = 0; z < numHexZ; z++)
            {
                // Calculate position
                float posX = offsetX + x * horizSpacing;
                float posZ = offsetZ + z * vertSpacing;

                // Offset every other column
                if (x % 2 == 1)
                {
                    posZ += vertSpacing / 2f;
                }

                // Check if position is within band boundaries
                if (posX >= bandCenter.x - bandWidth / 2f && posX <= bandCenter.x + bandWidth / 2f &&
                    posZ >= bandCenter.z - bandDepth / 2f && posZ <= bandCenter.z + bandDepth / 2f)
                {
                    positions.Add(new Vector3(posX, 0f, posZ));
                }
            }
        }

        // Optionally shuffle positions
        Shuffle(positions);

        return positions;
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
    void Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        for (int i = 0; i < n; i++)
        {
            int r = Random.Range(i, n);
            T tmp = list[r];
            list[r] = list[i];
            list[i] = tmp;
        }
    }
}