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
    [Header("Instance Parameters")]

    [Header("Orientation")]
    [Tooltip("Mean heading direction in degrees. Note: Unity's polar coordinate system uses left-handed coordinates.")] public float mu = 0f;
    [Tooltip("Coherence or order parameter, close to 0 means random orientation, 100000 means no randomization.")] public float kappa = 100000f;

    [Header("Movement")]
    [Tooltip("Speed of the instances in centimeters per second.")] public float speed = 2f;

    [Tooltip("If true, the spawner will move relative to the custom transform.")] public bool moveWithCustomTransform = false;
    [Tooltip("Custom transform to use as the reference when moveWithCustomTransform is true.")] public Transform customParentTransform;

    private Vector3 initialOffset;
    private List<Vector3> spawnPositions = new List<Vector3>();

    [Header("Visibility")]
    [Tooltip("Agent Invisible Duration in seconds.")] public float visibleOffDuration = 0f;
    [Tooltip("Agent Visible Duration in seconds.")] public float visibleOnDuration = 1f;



    [Header("Others")]

    public int vrIndex = 0; // This will be set by ChoiceController

    [Header("Boundary & Spawn Area Parameters")]

    [Tooltip("Pattern of the spawn grid. Note: Manhattan and hexagonal grid need optimization for the balance between number of instances and pair-wise distance.")] public SpawnGridType gridType = SpawnGridType.Random;
    [Tooltip("Boundary Width in centimeters. Please make sure that the boundary width is greater than the spawn width to avoid agents spawning on the boundary.")] public float boundaryWidth = 24f;
    [Tooltip("Boundary Length in centimeters. Please make sure that the boundary length is greater than the spawn length to avoid agents spawning on the boundary.")] public float boundaryLength = 52f;

    [Tooltip("Width of the spawn area in centimeters.")] public float spawnWidth = 24f;
    [Tooltip("Length of the spawn area in centimeters.")] public float spawnLength = 52f;

    [HideInInspector] 
    [Tooltip("Radius of hexagons when using Hexagonal grid type. Note for Boundary and Spawn Area: Ensure the Width to be m*2*hexRadius and Length to be n*1.732*hexRadius to avoid gaps in the grid")] 
    public float hexRadius = 1f;
    [HideInInspector]
    [Tooltip("Number of instances to spawn when using random grid type.")] 
    public int numberOfInstances = 32;

    [HideInInspector]
    [Tooltip("Length of sections when using Manhattan grid type.")] 
    public float sectionLength = 1f;

    [HideInInspector]
    [Tooltip("Width of sections when using Manhattan grid type.")] 
    public float sectionWidth = 1f;


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
        float horizontalDistance = hexRadius * 2f;
        float verticalDistance = hexRadius * Mathf.Sqrt(3f);

        int columns = Mathf.FloorToInt(spawnWidth / horizontalDistance);
        int rows = Mathf.FloorToInt(spawnLength / verticalDistance);
        //The use of FloorToInt prevent agents simulated at overlap location but at the cost of missing one row or column (whose direction is parallel to the moving direction) at the edge.
        //For example, if mu is 0, then one column on the right edge will not be simulated, which can be fixed by hardcode +1 to the columns when the mu is 0, however, if mu is not zero, this will cause a column to be duplicated
        //Therefore, we add the additional one row or column depends on the mu to avoid the edge effect.
        if (mu == 0 || mu == 180)
        {
            columns += 1;
        }
        else if (mu == 90 || mu == 270)
        {
            rows += 1;
        }

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

        // Update numberOfInstances based on the actual number of spawned positions
        numberOfInstances = spawnPositions.Count;
    }

    void GenerateManhattanGrid()
    {
        //float cellSize = Mathf.Sqrt((spawnWidth * spawnLength) / numberOfInstances);
        int cols = Mathf.FloorToInt(spawnWidth / sectionWidth);
        int rows = Mathf.FloorToInt(spawnLength / sectionLength);

        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                float posX = -spawnWidth / 2f + (x + 0.5f) * sectionWidth;
                float posZ = -spawnLength / 2f + (y + 0.5f) * sectionLength;
                spawnPositions.Add(new Vector3(posX, 0f, posZ));
            }
        }

        // Update numberOfInstances based on the actual number of spawned positions
        numberOfInstances = spawnPositions.Count;
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
        int instancesToSpawn = spawnPositions.Count;
        int parentLayer = gameObject.layer;

        for (int i = 0; i < instancesToSpawn; i++)
        {
            Vector3 position = spawnPositions[i];
            GameObject instance = Instantiate(instancePrefab, position, Quaternion.identity, transform);
            
            // Recursively set the layer for the instance and all its children
            SetLayerRecursively(instance, parentLayer);

            // Keep the naming convention you like
            instance.name = $"{instancePrefab.name}_{gameObject.name}_{globalInstanceCounter:D6}";
            globalInstanceCounter++;
            localInstanceCounter++;

            // Set orientation using von Mises distribution
            float orientation = GenerateVanMisesRotation(mu, kappa);
            // Add 90 degrees to rotate the reference direction from x-axis to z-axis
            instance.transform.rotation = Quaternion.Euler(0f, orientation + 90f, 0f);

            // Add and configure components (DirectionalMovement, VisibilityScript, PeriodicBoundary)
            SetupInstanceComponents(instance, orientation+ 90f);
        }

        UpdateBoundaryCenter();
    }

    private void SetupInstanceComponents(GameObject instance, float orientation)
    {
        // Add and configure DirectionalMovement
        DirectionalMovement movement = instance.AddComponent<DirectionalMovement>();
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
        // No need to add 90 degrees here, as we're doing it when setting the rotation
        return angle * Mathf.Rad2Deg;
    }

    // Add this new method to recursively set the layer
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}