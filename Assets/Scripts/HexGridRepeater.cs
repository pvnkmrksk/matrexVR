using UnityEngine;

/// <summary>
/// Generates a hexagonal grid of tiles around the center.
/// </summary>
public class HexGridGenerator : MonoBehaviour
{
    public GameObject tilePrefab; // Prefab for the tile. Locust prefab
    public int numberOfRings = 3; // Number of rings outward from the center
    public float spacing = 10f; // Spacing between tiles in the hexagonal grid in cm

    private GameObject[] clones; // Array to store clones
    private Vector3[] initialWorldPositions; // Array to store initial world positions
    private Quaternion[] initialLocalRotations; // Array to store initial local rotations

    void Start()
    {
        GenerateHexGrid();
    }

    void Update()
    {
        UpdateClonesPositionAndRotation();
    }

    /// <summary>
    /// Generates the hexagonal grid of tiles.
    /// </summary>
    void GenerateHexGrid()
    {
        int numberOfTiles = CalculateNumberOfTiles(numberOfRings);
        clones = new GameObject[numberOfTiles];
        initialWorldPositions = new Vector3[numberOfTiles];
        initialLocalRotations = new Quaternion[numberOfTiles];

        float hexWidth = spacing * Mathf.Sqrt(3);
        float hexHeight = spacing * 2;

        int index = 0;
        for (int q = -numberOfRings; q <= numberOfRings; q++)
        {
            int r1 = Mathf.Max(-numberOfRings, -q - numberOfRings);
            int r2 = Mathf.Min(numberOfRings, -q + numberOfRings);
            for (int r = r1; r <= r2; r++)
            {
                Vector3 hexPosition = new Vector3(hexWidth * (q + r / 2f), 0f, hexHeight * r / 2f);

                Vector3 worldPosition = transform.position + hexPosition;
                GameObject clone = Instantiate(tilePrefab, worldPosition, Quaternion.identity); // No parent transform
                SetLayerAndTagRecursively(clone, gameObject.layer, gameObject.tag); // Set layer and tag recursively
                initialWorldPositions[index] = hexPosition; // Store local position relative to the parent
                initialLocalRotations[index] = clone.transform.rotation; // Store initial rotation
                clones[index] = clone;
                index++;
            }
        }
    }

    /// <summary>
    /// Calculates the total number of tiles in the hexagonal grid.
    /// </summary>
    /// <param name="rings">The number of rings outward from the center.</param>
    /// <returns>The total number of tiles.</returns>
    int CalculateNumberOfTiles(int rings)
    {
        int tiles = 1;
        for (int i = 1; i <= rings; i++)
        {
            tiles += 6 * i;
        }
        return tiles;
    }

    /// <summary>
    /// Sets the layer and tag of the specified GameObject and its children recursively.
    /// </summary>
    /// <param name="obj">The GameObject to set the layer and tag for.</param>
    /// <param name="layer">The layer to set.</param>
    /// <param name="tag">The tag to set.</param>
    void SetLayerAndTagRecursively(GameObject obj, int layer, string tag)
    {
        obj.layer = layer;
        obj.tag = tag;
        foreach (Transform child in obj.transform)
        {
            SetLayerAndTagRecursively(child.gameObject, layer, tag);
        }
    }

    /// <summary>
    /// Updates the position and rotation of the clones based on the parent's position and rotation.
    /// </summary>
    void UpdateClonesPositionAndRotation()
    {
        for (int i = 0; i < clones.Length; i++)
        {
            if (clones[i] != null)
            {
                // Maintain the initial local position relative to the parent
                clones[i].transform.position = transform.position + initialWorldPositions[i];
                // Apply the parent's rotation to each clone individually
                clones[i].transform.rotation = transform.rotation * initialLocalRotations[i];
            }
        }
    }
}
