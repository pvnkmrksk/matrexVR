using UnityEngine;

public class SinusoidalGrating : MonoBehaviour
{
    // The width and height of the texture in pixels
    [SerializeField]
    private int textureWidth = 256;

    [SerializeField]
    private int textureHeight = 256;

    // The frequency and amplitude of the sinusoidal wave
    [SerializeField]
    [Range(0f, 10f)]
    private float frequency = 4f;

    // The combined power and amplitude level
    [SerializeField]
    [Range(0f, 1f)]
    private float level = 0.5f;

    // The colors of the grating
    [SerializeField]
    private Color color1 = Color.black;

    [SerializeField]
    private Color color2 = Color.white;

    // The radius and height of the cylinder
    [SerializeField]
    private float cylinderRadius = 1f;

    [SerializeField]
    private float cylinderHeight = 2f;

    // The number of segments along the circumference and height of the cylinder
    [SerializeField]
    private int cylinderSegments = 32;

    [SerializeField]
    private int cylinderStacks = 16;

    // The material to apply the texture to
    [SerializeField]
    private Material material;

    // The flag for interactive mode
    [SerializeField]
    private bool interactive = false;

    // The texture and mesh references
    private Texture2D texture;
    private Mesh mesh;

    void Start()
    {
        // Create the texture and fill it with the sinusoidal grating pattern
        texture = new Texture2D(textureWidth, textureHeight);
        FillTexture();
        //         // Create a new material in code using the Standard shader
        // material = new Material(Shader.Find("Standard"));
        // Create a new material in code using an Unlit shader
        material = new Material(Shader.Find("Unlit/Texture"));

        //         material.mainTexture = texture;

        // Assign the texture to the material
        material.mainTexture = texture;

        // Create the cylinder mesh and assign it to a new game object
        mesh = CreateCylinderMesh(cylinderRadius, cylinderHeight, cylinderSegments, cylinderStacks);
        GameObject cylinder = new GameObject("GratingDrum");
        cylinder.AddComponent<MeshFilter>().mesh = mesh;
        cylinder.AddComponent<MeshRenderer>().material = material;

        // attach drumRotator script to the cylinder
        cylinder.AddComponent<DrumRotator>();
    }

    void Update()
    {
        // If interactive mode is on, update the texture and mesh every frame
        if (interactive)
        {
            FillTexture();
        }
    }

    // A helper method to fill the texture with the sinusoidal grating pattern
    // A helper method to fill the texture with the sinusoidal grating pattern
    void FillTexture()
    {
        for (int x = 0; x < textureWidth; x++)
        {
            for (int y = 0; y < textureHeight; y++)
            {
                // Calculate the normalized coordinate along the x-axis
                float u = (float)x / (textureWidth - 1);

                // Calculate the sinusoidal value at this coordinate
                float s = Mathf.Sin(u * frequency * 2 * Mathf.PI);

                // // Map the sinusoidal value to a color between color1 and color2 with variable amplitude
                // Calculate the normal sine and the raised power sine
                float normalSine = s * level + 0.5f;
                float powerSine = Mathf.Pow(normalSine, 4f);

                // Interpolate between them based on the level
                float t = Mathf.Lerp(normalSine, powerSine, level * 2 - 1);
                Color c = Color.Lerp(color1, color2, t);
                // Set the pixel color at this coordinate
                texture.SetPixel(x, y, c);
            }
        }

        // Apply the changes to the texture
        texture.Apply();
    }

    // Or define your own saturate function
    float saturate(float x)
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
