using UnityEngine;
using System.Collections;

public class SinusoidalGrating : MonoBehaviour
{
    [Header("Texture Settings")]
    [SerializeField]
    private int textureWidth = 256;

    [SerializeField]
    private int textureHeight = 256;

    [Header("Sinusoidal Settings")]
    [SerializeField]
    [Range(0f, 10f)]
    public float frequency = 4f;

    [SerializeField]
    [Range(0f, 1f)]
    public float level = 0.5f;

    [Header("Color Settings")]
    [SerializeField]
    private Color color1 = Color.black;

    [SerializeField]
    private Color color2 = Color.white;

    [Header("Cylinder Settings")]
    [SerializeField]
    private float cylinderRadius = 1f;

    [SerializeField]
    private float cylinderHeight = 2f;

    [SerializeField]
    private int cylinderSegments = 32;

    [SerializeField]
    private int cylinderStacks = 16;

    [Header("Miscellaneous Settings")]
    [SerializeField]
    private Material material;

    private Texture2D texture;
    private Mesh mesh;

    public DrumRotator drumRotator; // Reference to the DrumRotator script

    public DataLogger dataLogger; // Reference to the DataLogger script 
    private void Start()
    {
        texture = new Texture2D(textureWidth, textureHeight);
        material = new Material(Shader.Find("Unlit/Texture"));
        material.mainTexture = texture;

        mesh = CreateCylinderMesh(cylinderRadius, cylinderHeight, cylinderSegments, cylinderStacks);
        GameObject cylinder = new GameObject("GratingDrum");
        cylinder.AddComponent<MeshFilter>().mesh = mesh;
        cylinder.AddComponent<MeshRenderer>().material = material;
        drumRotator = cylinder.AddComponent<DrumRotator>(); // Assign the DrumRotator component

        dataLogger = cylinder.AddComponent<DataLogger>(); // Assign the DataLogger component
        // Set the bool to include ZMQ data as false
        dataLogger.includeZmqData = false;
        // Subscribe to the ConfigurationChanged event
        drumRotator.ConfigurationChanged += HandleConfigurationChanged;

        // Apply the initial rotation config
        ApplyRotationConfig();

        // Delay the filling of the texture
        StartCoroutine(DelayedFillTexture());
    }

    private IEnumerator DelayedFillTexture()
    {
        // Wait for one frame
        yield return null;

        // Fill the texture
        FillTexture();

        // Apply the texture to the material
        material.mainTexture = texture;
    }

    // Event handler for the ConfigurationChanged event
    private void HandleConfigurationChanged()
    {
        ApplyRotationConfig();
    }

    // Method to apply the rotation config to the sinusoidal grating
    private void ApplyRotationConfig()
    {
        if (
            drumRotator != null
            && drumRotator.configs != null
            && drumRotator.currentIndex < drumRotator.configs.Count
        )
        {
            frequency = drumRotator.configs[drumRotator.currentIndex].frequency;
            level = drumRotator.configs[drumRotator.currentIndex].level;
            FillTexture();
        }
    }
    private void Update()
    {
        // Handle input or any other logic here

        // Call the UpdateLogger method of the DataLogger script
        dataLogger?.UpdateLogger();
    }
    private void FillTexture()
    {
        if (
            drumRotator != null
            && drumRotator.configs != null
            && drumRotator.currentIndex < drumRotator.configs.Count
        )
        {
            RotationConfig currentConfig = drumRotator.configs[drumRotator.currentIndex];

            frequency = currentConfig.frequency;
            level = currentConfig.level;

            for (int x = 0; x < textureWidth; x++)
            {
                for (int y = 0; y < textureHeight; y++)
                {
                    float u = (float)x / (textureWidth - 1);
                    float s = Mathf.Sin(u * frequency * 2 * Mathf.PI);
                    float normalSine = s * level + 0.5f;
                    float powerSine = Mathf.Pow(normalSine, 4f);
                    float t = Mathf.Lerp(normalSine, powerSine, level * 2 - 1);
                    Color c = Color.Lerp(color1, color2, t);
                    texture.SetPixel(x, y, c);
                }
            }

            texture.Apply(); // Apply the texture after all pixels have been set
        }
    }

    private float Saturate(float x)
    {
        return Mathf.Max(0f, Mathf.Min(1f, x));
    }

    // A helper method to create a cylinder mesh with a given radius, height, number of segments and stacks
    Mesh CreateCylinderMesh(float radius, float height, int segments, int stacks)
    {
        Mesh mesh = new Mesh();

        // Calculate the number of vertices and triangles in the mesh
        int vertexCount = (segments + 1) * (stacks + 1);
        int triangleCount = segments * stacks * 6;

        // Create arrays to store the vertices, normals, uvs and triangles
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[triangleCount];

        // Loop through each segment and stack of the cylinder
        for (int i = 0; i <= segments; i++)
        {
            for (int j = 0; j <= stacks; j++)
            {
                // Calculate the normalized coordinates along the circumference and height of the cylinder
                float u = (float)i / segments;
                float v = (float)j / stacks;

                // Calculate the angle and position of the vertex on the cylinder surface
                float angle = u * 2 * Mathf.PI;
                float x = radius * Mathf.Cos(angle);
                float y = v * height - height / 2;
                float z = radius * Mathf.Sin(angle);

                // Calculate the normal vector of the vertex on the cylinder surface
                float nx = Mathf.Cos(angle);
                float ny = 0f;
                float nz = Mathf.Sin(angle);

                // Set the vertex position, normal and uv in the arrays
                int index = i * (stacks + 1) + j;
                vertices[index] = new Vector3(x, y, z);
                normals[index] = new Vector3(nx, ny, nz);
                uvs[index] = new Vector2(u, v);

                // Set the triangle indices in the array if not on the last segment or stack
                if (i < segments && j < stacks)
                {
                    int tIndex = i * stacks * 6 + j * 6;
                    triangles[tIndex] = index;
                    triangles[tIndex + 1] = index + stacks + 1;
                    triangles[tIndex + 2] = index + stacks + 2;
                    triangles[tIndex + 3] = index;
                    triangles[tIndex + 4] = index + stacks + 2;
                    triangles[tIndex + 5] = index + 1;
                }
            }
        }

        // Assign the arrays to the mesh and recalculate bounds
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }
}
