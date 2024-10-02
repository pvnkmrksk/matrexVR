using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SpawnGridType
{
    Hexagonal,
    Manhattan,
    Random
}

public class BandSpawner : MonoBehaviour
{
    public GameObject instancePrefab;
    [Tooltip("Number of instances to spawn.")] public int numberOfInstances = 32;
    [Tooltip("Width of the spawn area in centimeters.")] public float spawnWidth = 30f;
    [Tooltip("Length of the spawn area in centimeters.")] public float spawnLength = 50f;
    [Tooltip("Pattern of the spawn grid. Note: Manhattan and hexagonal grid need optimization for the balance between number of instances and pair-wise distance.")] public SpawnGridType gridType = SpawnGridType.Random;

    [Header("Orientation Parameters")]
    [Tooltip("Mean heading direction in degrees. Note: Unity's polar coordinate system uses left-handed coordinates.")] public float mu = 0f;
    [Tooltip("Coherence or order parameter, close to 0 means random orientation, 100000 means no randomization.")] public float kappa = 10f;

    [Header("Movement Parameters")]
    [Tooltip("Speed of the instances in centimeters per second.")] public float speed = 3f;

    [Header("Visibility Parameters")]
    [Tooltip("Agent Invisible Duration in seconds.")] public float visibleOffDuration = 4f;
    [Tooltip("Agent Visible Duration in seconds.")] public float visibleOnDuration = 1f;

    [Header("Periodic Boundary Parameters")]
    [Tooltip("Boundary Width in centimeters. Please make sure that the boundary width is greater than the spawn width to avoid agents spawning on the boundary.")] public float boundaryWidth = 20f;
    [Tooltip("Boundary Length in centimeters. Please make sure that the boundary length is greater than the spawn length to avoid agents spawning on the boundary.")] public float boundaryLength = 20f;
    [Tooltip("If true, the spawner will move relative to the custom transform.")] public bool moveWithCustomTransform = false;
    [Tooltip("Custom transform to use as the reference when moveWithCustomTransform is true.")] public Transform customParentTransform;

    private Vector3 initialOffset;
    private List<Vector3> spawnPositions = new List<Vector3>();

    private PeriodicBoundary boundaryComponent;

    private static int globalInstanceCounter = 0;
    private int localInstanceCounter = 0;

    /// <summary>
    /// Initializes the spawner, sets up the initial transform, and spawns instances.
    /// </summary>
    void Start()
    {
        SetupInitialTransform();
        GenerateSpawnPositions();
        SpawnInstances();
    }

    /// <summary>
    /// Sets up the initial offset if custom transform movement is enabled.
    /// </summary>
    void SetupInitialTransform()
    {
        if (moveWithCustomTransform && customParentTransform != null)
        {
            initialOffset = transform.position - customParentTransform.position;
        }
    }

    /// <summary>
    /// Updates the spawner's position and boundary center each frame if custom transform movement is enabled.
    /// </summary>
    void Update()
    {
        if (moveWithCustomTransform && customParentTransform != null)
        {
            UpdateTransform();
            UpdateBoundaryCenter();
        }
    }

    /// <summary>
    /// Updates the spawner's position based on the custom parent transform.
    /// </summary>
    void UpdateTransform()
    {
        // Only apply translation, ignoring rotation completely
        transform.position = customParentTransform.position + initialOffset;
    }

    /// <summary>
    /// Updates the boundary center for all PeriodicBoundary components to match the spawner's current position.
    /// </summary>
    void UpdateBoundaryCenter()
    {
        Vector3 center = transform.position;
        PeriodicBoundary[] boundaries = GetComponentsInChildren<PeriodicBoundary>();
        foreach (PeriodicBoundary boundary in boundaries)
        {
            boundary.boundaryCenter = center;
        }
    }

    void GenerateSpawnPositions()
    {
        switch (gridType)
        {
            case SpawnGridType.Hexagonal:
                GenerateHexagonalGrid();
                break;
            case SpawnGridType.Manhattan:
                GenerateManhattanGrid();
                break;
            case SpawnGridType.Random:
                GenerateRandomPositions();
                break;
        }
    }

    void GenerateHexagonalGrid()
    {
        float hexRadius = Mathf.Sqrt((spawnWidth * spawnLength) / (2f * Mathf.Sqrt(3f) * numberOfInstances));
        float horizontalDistance = hexRadius * 2f;
        float verticalDistance = hexRadius * Mathf.Sqrt(3f);

        int columns = Mathf.FloorToInt(spawnWidth / horizontalDistance);
        int rows = Mathf.FloorToInt(spawnLength / verticalDistance);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                float xPos = col * horizontalDistance + ((row % 2 == 0) ? 0 : hexRadius);
                float zPos = row * verticalDistance;

                xPos -= spawnWidth / 2f;
                zPos -= spawnLength / 2f;

                spawnPositions.Add(new Vector3(xPos, 0f, zPos));
            }
        }
    }

    void GenerateManhattanGrid()
    {
        float cellSize = Mathf.Sqrt((spawnWidth * spawnLength) / numberOfInstances);
        int cols = Mathf.FloorToInt(spawnWidth / cellSize);
        int rows = Mathf.FloorToInt(spawnLength / cellSize);

        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                float posX = -spawnWidth / 2f + (x + 0.5f) * cellSize;
                float posZ = -spawnLength / 2f + (y + 0.5f) * cellSize;
                spawnPositions.Add(new Vector3(posX, 0f, posZ));
            }
        }
    }

    void GenerateRandomPositions()
    {
        for (int i = 0; i < numberOfInstances; i++)
        {
            float posX = Random.Range(-spawnWidth / 2f, spawnWidth / 2f);
            float posZ = Random.Range(-spawnLength / 2f, spawnLength / 2f);
            spawnPositions.Add(new Vector3(posX, 0f, posZ));
        }
    }

    public void SpawnInstances()
    {
        int instancesToSpawn = Mathf.Min(numberOfInstances, spawnPositions.Count);

        for (int i = 0; i < instancesToSpawn; i++)
        {
            Vector3 position = spawnPositions[i];
            GameObject instance = Instantiate(instancePrefab, position, Quaternion.identity, transform);
            
            // Assign a unique name with serial number
            instance.name = $"{instancePrefab.name}_{gameObject.name}_{globalInstanceCounter:D6}";
            globalInstanceCounter++;
            localInstanceCounter++;
            // Set orientation using von Mises distribution

            float orientation = GenerateVanMisesRotation(mu, kappa);
            instance.transform.rotation = Quaternion.Euler(0f, orientation, 0f);

            // Add and configure DirectionalMovement
            DirectionalMovement movement = instance.AddComponent<DirectionalMovement>();
            // movement.SetSpeed(Random.Range(minSpeed, maxSpeed)); might be useful for rendering agents with different speeds in the future
            movement.SetSpeed(speed);
            movement.SetDirection(orientation);

            // Add and configure VisibilityScript
            VisibilityScript visibility = instance.AddComponent<VisibilityScript>();
            visibility.visibleOffDuration = visibleOffDuration;
            visibility.visibleOnDuration = visibleOnDuration;
            visibility.phaseOffset = UnityEngine.Random.Range(0f, visibleOffDuration + visibleOnDuration);

            // Add and configure PeriodicBoundary
            PeriodicBoundary boundary = instance.AddComponent<PeriodicBoundary>();
            boundary.boundaryWidth = boundaryWidth;
            boundary.boundaryLength = boundaryLength;
        }

        UpdateBoundaryCenter();
    }

    float GenerateVanMisesRotation(float mu, float kappa)
    {
        if (kappa <= 0)
        {
            kappa = 0.0001f;
        }
        else if (kappa >= 10000)
        {
            return mu;
        }

        float angle = VanMisesDistribution.Generate(Mathf.Deg2Rad * mu, kappa);
        return angle * Mathf.Rad2Deg;
    }
}