using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A script that creates a grating texture and applies it to a cylinder
public class GratingTexture : MonoBehaviour
{
    // Variables for the texture parameters
    [SerializeField]
    private int textureWidth = 256; // the width of the texture in pixels

    [SerializeField]
    private int textureHeight = 256; // the height of the texture in pixels

    [SerializeField]
    private float waveFrequency = 10f; // the frequency of the sine wave in radians per pixel

    [SerializeField]
    private float wavePhase = 0f; // the phase of the sine wave in radians

    [SerializeField]
    private float textureContrast = 1f; // the contrast of the texture, from 0 to 1

    [SerializeField]
    private bool useContinuousMapping = false; // whether to use a continuous or binary mapping for the texture

    void Start()
    {
        // Create a grating texture using the variables
        Texture2D texture = GenerateGratingTexture(
            textureWidth,
            textureHeight,
            waveFrequency,
            wavePhase,
            textureContrast,
            useContinuousMapping
        );

        // Get the material from the cylinder's renderer
        Material material = GetComponent<Renderer>().material;

        // Change the texture of the material
        material.mainTexture = texture;
    }

    // A function to generate a grating texture
    Texture2D GenerateGratingTexture(
        int width,
        int height,
        float frequency,
        float phase,
        float contrast,
        bool continuous
    )
    {
        // Create a new texture with no mipmaps
        Texture2D texture = new Texture2D(width, height);

        // Set the pixel colors
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Convert x and y to polar coordinates
                float r = Mathf.Sqrt(x * x + y * y);
                float theta = Mathf.Atan2(y, x);

                // Calculate the sine wave value
                float value = Mathf.Sin(frequency * theta + phase);

                // Map the value to black and white colors
                Color color = MapValueToColor(value, contrast, continuous);

                // Set pixel to color
                texture.SetPixel(x, y, color);
            }
        }

        // Apply the changes to the texture
        texture.Apply();

        // Return the texture
        return texture;
    }

    // A function to map a value to a color based on contrast and mapping mode
    Color MapValueToColor(float value, float contrast, bool continuous)
    {
        Color color;

        if (continuous)
        {
            // Use a linear mapping from -1 to 1 to 0 to 1
            value = (value + 1) / 2;

            // Apply contrast by raising the value to a power
            value = Mathf.Pow(value, contrast);

            // Use grayscale color
            color = new Color(value, value, value);
        }
        else
        {
            // Use a binary mapping from -1 to 1 to 0 or 1
            if (value > 0)
            {
                color = Color.white;
            }
            else
            {
                color = Color.black;
            }

            // Apply contrast by multiplying the color by a factor
            color *= contrast;
        }

        return color;
    }
}
