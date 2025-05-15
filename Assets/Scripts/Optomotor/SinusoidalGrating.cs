using UnityEngine;
using System.Collections;

public class SinusoidalGrating : MonoBehaviour
{
    [Header("Texture Settings")]
    [SerializeField]
    [Tooltip("Width of the texture in pixels")]
    private int textureWidth = 256;

    [SerializeField]
    [Tooltip("Height of the texture in pixels")]
    private int textureHeight = 256;

    [Header("Sinusoidal Settings")]
    [SerializeField]
    [Range(0f, 10f)]
    [Tooltip("Spatial frequency of the grating in cycles per revolution")]
    private float frequency = 4f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Contrast of the grating (0 = no contrast, 1 = maximum contrast)")]
    private float contrast = 0.5f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Duty cycle of the grating (0 = all dark, 1 = all light, 0.5 = equal dark/light)")]
    private float dutyCycle = 0.5f;

    [Header("Color Settings")]
    [SerializeField]
    [Tooltip("First color of the grating (typically dark color)")]
    private Color color1 = Color.black;

    [SerializeField]
    [Tooltip("Second color of the grating (typically light color)")]
    private Color color2 = Color.white;

    [Header("Cylinder Settings")]
    [SerializeField]
    [Tooltip("Radius of the cylinder in meters")]
    private float cylinderRadius = 1f;

    [SerializeField]
    [Tooltip("Height of the cylinder in meters")]
    private float cylinderHeight = 2f;

    [SerializeField]
    [Tooltip("Number of segments around the cylinder")]
    private int cylinderSegments = 32;

    [SerializeField]
    [Tooltip("Number of stacks along the height of the cylinder")]
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
    public void SetGratingParameters(float newFrequency, float newContrast, float newDutyCycle, Color newColor1, Color newColor2)
    {
        Debug.Log($"SetGratingParameters() - Frequency: {newFrequency}, Contrast: {newContrast}, DutyCycle: {newDutyCycle}, Color1: {newColor1}, Color2: {newColor2}");

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

        if (dutyCycle != newDutyCycle)
        {
            Debug.Log($"DutyCycle changed from {dutyCycle} to {newDutyCycle}");
            dutyCycle = newDutyCycle;
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
        Debug.Log($"Updating texture with frequency={frequency}, contrast={contrast}, dutyCycle={dutyCycle}");

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

                // Apply duty cycle for discrete case (high contrast)
                if (contrast >= 0.99f)
                {
                    // For each cycle, we want:
                    // - dutyCycle portion to be white (1)
                    // - (1-dutyCycle) portion to be black (0)

                    // Calculate position within current cycle (0 to 1)
                    float cyclePosition = (u * frequency) % 1f;

                    // If we're in the first 'dutyCycle' portion of the cycle, make it white
                    // Otherwise make it black
                    contrastValue = cyclePosition < dutyCycle ? 1f : 0f;
                }

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