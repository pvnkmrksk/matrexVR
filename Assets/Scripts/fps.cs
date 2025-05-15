using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class fps : MonoBehaviour
{
    // add fps to the top left corner of the screen to monitor the frame rate
    float deltaTime = 0.0f;

    // make the game run at max speed witth no frame rate limit
    void Awake()
    {
        Application.targetFrameRate = 1000;
        QualitySettings.vSyncCount = 0;
    }

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void OnGUI()
    {
        int w = Screen.width,
            h = Screen.height;

        GUIStyle style = new GUIStyle();

        // Position the FPS counter in the top right corner
        // Use a fixed size box that's big enough for the text
        int boxWidth = 150;
        int boxHeight = h * 2 / 100;
        Rect backgroundRect = new Rect(w / 2, boxHeight + 10, boxWidth, boxHeight);

        // Draw a background box to clear previous frames
        GUI.color = new Color(0, 0, 0, 0.5f);
        GUI.Box(backgroundRect, "");

        // Reset color for the text
        GUI.color = Color.white;

        // Use the same rect for the label
        style.alignment = TextAnchor.MiddleLeft;
        style.fontSize = h * 2 / 100;
        style.normal.textColor = new Color(1.0f, 1.0f, 0.0f, 1.0f); // Yellow for visibility
        float msec = deltaTime * 1000.0f;
        float fps = 1.0f / deltaTime;
        string text = string.Format("{0:0.} fps", fps);
        GUI.Label(backgroundRect, text, style);
    }
}
