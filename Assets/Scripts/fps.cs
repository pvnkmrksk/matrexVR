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

        // place the fps at the top left corner of the screen, 128 px below the top
        Rect rect = new Rect(200, 128, w, h * 12 / 100);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = h * 2 / 100;
        style.normal.textColor = new Color(0.0f, 0.0f, 0.5f, 1.0f);
        float msec = deltaTime * 1000.0f;
        float fps = 1.0f / deltaTime;
        string text = string.Format("  {0:0.} fps", fps);
        // string text = string.Format("{0:0.0} ms ({1:0.} fps)", msec, fps);
        GUI.Label(rect, text, style);
    }
}
