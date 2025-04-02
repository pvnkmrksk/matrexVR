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
    private float frequency = 4f;

    [SerializeField]
    [Range(0f, 1f)]
    private float contrast = 0.5f;

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

    private Texture2D texture;
    private Mesh mesh;
    private Material material;
    private bool textureNeedsUpdate = false;
    private int updateCount = 0;

    private void Awake()
    {
        Debug.Log($"SinusoidalGrating.Awake() - {gameObject.name}");

        // Create texture
        texture = new Texture2D(textureWidth, textureHeight);

        // Create material
        material = new Material(Shader.Find("Unlit/Texture"));
        material.mainTexture = texture;

        // Create mesh
        mesh = CreateCylinderMesh(cylinderRadius, cylinderHeight, cylinderSegments, cylinderStacks);

        // Add or get components
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
            Debug.Log("Added MeshFilter component");
        }
        meshFilter.mesh = mesh;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            Debug.Log("Added MeshRenderer component");
        }
        meshRenderer.material = material;

        Debug.Log("SinusoidalGrating initialization complete");
    }

    private void Start()
    {
        Debug.Log($"SinusoidalGrating.Start() - {gameObject.name}, Initial frequency: {frequency}, contrast: {contrast}");
        // Generate the initial texture
        UpdateTexture();
    }

    // Public method for OptomotorSceneController to update grating parameters
    public void SetGratingParameters(float newFrequency, float newContrast, Color newColor1, Color newColor2)
    {
        Debug.Log($"SetGratingParameters() - Frequency: {newFrequency}, Contrast: {newContrast}, Color1: {newColor1}, Color2: {newColor2}");

        // Check if parameters are actually changing
        bool paramsChanged = false;

        if (frequency != newFrequency)
        {
            Debug.Log($"Frequency changed from {frequency} to {newFrequency}");
            frequency = newFrequency;
            paramsChanged = true;
        }

        if (contrast != newContrast)
        {
            Debug.Log($"Contrast changed from {contrast} to {newContrast}");
            contrast = newContrast;
            paramsChanged = true;
        }

        if (color1 != newColor1)
        {
            Debug.Log($"Color1 changed from {color1} to {newColor1}");
            color1 = newColor1;
            paramsChanged = true;
        }

        if (color2 != newColor2)
        {
            Debug.Log($"Color2 changed from {color2} to {newColor2}");
            color2 = newColor2;
            paramsChanged = true;
        }

        // Mark texture for update if any parameter changed
        if (paramsChanged)
        {
            textureNeedsUpdate = true;
            Debug.Log("Parameters changed, texture will be updated");
        }
        else
        {
            Debug.Log("No parameters changed, no update needed");
        }
    }

    private void Update()
    {
        // Update texture if needed
        if (textureNeedsUpdate)
        {
            UpdateTexture();
            textureNeedsUpdate = false;
            updateCount++;
            Debug.Log($"Updated texture (count: {updateCount}) - Current values: Frequency={frequency}, Contrast={contrast}");
        }
    }

    private void UpdateTexture()
    {
        Debug.Log($"Updating texture with frequency={frequency}, contrast={contrast}");

        for (int x = 0; x < textureWidth; x++)
        {
            for (int y = 0; y < textureHeight; y++)
            {
                float u = (float)x / (textureWidth - 1);
                float s = Mathf.Sin(u * frequency * 2 * Mathf.PI);

                // Apply contrast (previously called 'level')
                float normalSine = s * 0.5f + 0.5f;

                // Apply contrast function
                float contrastValue = ApplyContrast(normalSine, contrast);

                // Mix colors
                Color c = Color.Lerp(color1, color2, contrastValue);
                texture.SetPixel(x, y, c);
            }
        }

        texture.Apply(); // Apply the texture after all pixels have been set
        Debug.Log("Texture update completed");
    }

    private float ApplyContrast(float value, float contrastAmount)
    {
        // Apply a sigmoid contrast function
        if (contrastAmount >= 0.99f)
        {
            // Binary contrast (black and white only)
            return value >= 0.5f ? 1f : 0f;
        }
        else if (contrastAmount <= 0.01f)
        {
            // No contrast (mid-gray only)
            return 0.5f;
        }
        else
        {
            // Sigmoid contrast function that preserves the midpoint at 0.5
            float x = (value - 0.5f) * 2f; // Scale to [-1, 1]
            float factor = (1f / (1f - contrastAmount)) - 1f;
            float sigmoid = x / (Mathf.Sqrt(1f + factor * x * x));
            return sigmoid * 0.5f + 0.5f; // Scale back to [0, 1]
        }
    }

    private float Saturate(float x)
    {
        return Mathf.Max(0f, Mathf.Min(1f, x));
    }

    // A helper method to create a cylinder mesh with a given radius, height, number of segments and stacks
    Mesh CreateCylinderMesh(float radius, float height, int segments, int stacks)
    {
        Debug.Log($"Creating cylinder mesh: r={radius}, h={height}, segments={segments}, stacks={stacks}");
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

        Debug.Log("Cylinder mesh created successfully");
        return mesh;
    }

    // For debugging - add a way to manually force update
    public void ForceTextureUpdate()
    {
        Debug.Log("Forcing texture update");
        textureNeedsUpdate = true;
    }
}