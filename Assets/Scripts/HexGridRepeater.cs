using UnityEngine;

public class HexGridGenerator : MonoBehaviour
{
    public GameObject tilePrefab;
    public int numberOfRings = 3; // Number of rings outward from the center
    public float spacing = 1.5f;

    private GameObject[] clones;
    private Vector3[] initialWorldPositions;

    void Start()
    {
        GenerateHexGrid();
    }

    void Update()
    {
        RotateClonesIndividually();
    }

    void GenerateHexGrid()
    {
        int numberOfTiles = CalculateNumberOfTiles(numberOfRings);
        clones = new GameObject[numberOfTiles];
        initialWorldPositions = new Vector3[numberOfTiles];

        float hexWidth = spacing * Mathf.Sqrt(3);
        float hexHeight = spacing * 2;

        int index = 0;
        for (int q = -numberOfRings; q <= numberOfRings; q++)
        {
            int r1 = Mathf.Max(-numberOfRings, -q - numberOfRings);
            int r2 = Mathf.Min(numberOfRings, -q + numberOfRings);
            for (int r = r1; r <= r2; r++)
            {
                Vector3 hexPosition = new Vector3(
                    hexWidth * (q + r / 2f),
                    0f,
                    hexHeight * r / 2f
                );

                Vector3 worldPosition = transform.position + hexPosition;
                GameObject clone = Instantiate(tilePrefab, worldPosition, Quaternion.identity, transform);
                clone.layer = gameObject.layer; // Set the clone's layer to the parent's layer
                initialWorldPositions[index] = clone.transform.position;
                clones[index] = clone;
                index++;
            }
        }
    }

    int CalculateNumberOfTiles(int rings)
    {
        int tiles = 1;
        for (int i = 1; i <= rings; i++)
        {
            tiles += 6 * i;
        }
        return tiles;
    }

    void RotateClonesIndividually()
    {
        for (int i = 0; i < clones.Length; i++)
        {
            if (clones[i] != null)
            {
                // Calculate local position based on initial world position
                Vector3 localPosition = transform.InverseTransformPoint(initialWorldPositions[i]);
                clones[i].transform.localPosition = localPosition;

                // Rotate each clone to match the parent's rotation
                clones[i].transform.rotation = transform.rotation;
            }
        }
    }
}
