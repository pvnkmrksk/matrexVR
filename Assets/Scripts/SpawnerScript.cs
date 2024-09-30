using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SpawnGridType
{
    Hexagonal,
    Manhattan,
    Random
}

public class SpawnerScript : MonoBehaviour
{
    public GameObject instancePrefab;
    public int numberOfInstances = 32;
    public float spawnWidth = 30f;
    public float spawnHeight = 50f;
    public SpawnGridType gridType = SpawnGridType.Random;

    [Header("Orientation Parameters")]
    public float mu = 0f; // Mean direction in degrees
    public float kappa = 10f; // Concentration parameter

    private List<Vector3> spawnPositions = new List<Vector3>();

    void Start()
    {
        GenerateSpawnPositions();
        SpawnInstances();
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
        float hexRadius = Mathf.Sqrt((spawnWidth * spawnHeight) / (2f * Mathf.Sqrt(3f) * numberOfInstances));
        float horizontalDistance = hexRadius * 2f;
        float verticalDistance = hexRadius * Mathf.Sqrt(3f);

        int columns = Mathf.FloorToInt(spawnWidth / horizontalDistance);
        int rows = Mathf.FloorToInt(spawnHeight / verticalDistance);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                float xPos = col * horizontalDistance + ((row % 2 == 0) ? 0 : hexRadius);
                float zPos = row * verticalDistance;

                xPos -= spawnWidth / 2f;
                zPos -= spawnHeight / 2f;

                spawnPositions.Add(new Vector3(xPos, 0f, zPos));
            }
        }
    }

    void GenerateManhattanGrid()
    {
        float cellSize = Mathf.Sqrt((spawnWidth * spawnHeight) / numberOfInstances);
        int cols = Mathf.FloorToInt(spawnWidth / cellSize);
        int rows = Mathf.FloorToInt(spawnHeight / cellSize);

        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                float posX = -spawnWidth / 2f + (x + 0.5f) * cellSize;
                float posZ = -spawnHeight / 2f + (y + 0.5f) * cellSize;
                spawnPositions.Add(new Vector3(posX, 0f, posZ));
            }
        }
    }

    void GenerateRandomPositions()
    {
        for (int i = 0; i < numberOfInstances; i++)
        {
            float posX = Random.Range(-spawnWidth / 2f, spawnWidth / 2f);
            float posZ = Random.Range(-spawnHeight / 2f, spawnHeight / 2f);
            spawnPositions.Add(new Vector3(posX, 0f, posZ));
        }
    }

    void SpawnInstances()
    {
        int instancesToSpawn = Mathf.Min(numberOfInstances, spawnPositions.Count);

        for (int i = 0; i < instancesToSpawn; i++)
        {
            Vector3 position = spawnPositions[i];
            GameObject instance = Instantiate(instancePrefab, position, Quaternion.identity, transform);
            
            // Set orientation using von Mises distribution
            float orientation = GenerateVanMisesRotation(mu, kappa);
            instance.transform.rotation = Quaternion.Euler(0f, orientation, 0f);
        }
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
