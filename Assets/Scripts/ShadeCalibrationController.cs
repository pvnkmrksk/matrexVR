using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Displays one measured, spatially uniform colour on one VR cube at a time.
/// All panels belonging to the other cubes remain at RGB (0, 0, 0).
/// </summary>
public sealed class ShadeCalibrationController : MonoBehaviour
{
    private const string ConfigFileName = "shade_calibration.json";

    [Serializable]
    private sealed class CalibrationConfig
    {
        public int targetDisplay = 0;
        public int initialCube = 1;
        public int initialShade = 0;
        public int labelX = 10;
        public int labelY = 10;
        public PanelLayout[] cubes;
        public Shade[] shades;
    }

    [Serializable]
    private sealed class PanelLayout
    {
        public string vrId;
        public int ledPanelWidth = 128;
        public int ledPanelHeight = 128;
        public int startRow;
        public int startCol;
        public bool horizontal = true;
        public string displayOrder = "RBLF";
        public string alwaysBlackPanels = "";
    }

    [Serializable]
    private sealed class Shade
    {
        public string name;
        public string hex;
        public int r;
        public int g;
        public int b;
    }

    private sealed class CubeCameras
    {
        public string name;
        public readonly List<Camera> cameras = new List<Camera>();
        public readonly List<bool> alwaysBlack = new List<bool>();
    }

    private CalibrationConfig config;
    private readonly List<CubeCameras> cubeCameras = new List<CubeCameras>();
    private int activeCube;
    private int activeShade;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private GUIStyle labelStyle;
    private GUIStyle boxStyle;

    private void Awake()
    {
        QualitySettings.vSyncCount = 1;
        Application.runInBackground = true;

        if (!LoadConfig())
        {
            enabled = false;
            return;
        }

        activeCube = Mathf.Clamp(config.initialCube - 1, 0, config.cubes.Length - 1);
        activeShade = Mathf.Clamp(config.initialShade, 0, config.shades.Length - 1);

        ActivateTargetDisplay();
        CreateCameras();
        UpdateCameraRects(true);
        ApplyColours();
        LogCurrentSelection();
    }

    private bool LoadConfig()
    {
        string path = Path.Combine(Application.streamingAssetsPath, ConfigFileName);
        try
        {
            if (!File.Exists(path))
            {
                Debug.LogError("Shade calibration config was not found: " + path);
                return false;
            }

            config = JsonUtility.FromJson<CalibrationConfig>(File.ReadAllText(path));
            if (config == null || config.cubes == null || config.cubes.Length == 0 ||
                config.shades == null || config.shades.Length == 0)
            {
                Debug.LogError("Shade calibration config contains no cubes or shades: " + path);
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Could not load shade calibration config: " + exception.Message);
            return false;
        }
    }

    private void ActivateTargetDisplay()
    {
        if (config.targetDisplay > 0 && config.targetDisplay < Display.displays.Length)
        {
            Display.displays[config.targetDisplay].Activate();
        }
    }

    private void CreateCameras()
    {
        Camera baseCamera = NewCamera("Black surround", -100f);
        baseCamera.rect = new Rect(0f, 0f, 1f, 1f);
        baseCamera.backgroundColor = Color.black;

        for (int cubeIndex = 0; cubeIndex < config.cubes.Length; cubeIndex++)
        {
            PanelLayout cube = config.cubes[cubeIndex];
            CubeCameras group = new CubeCameras { name = cube.vrId };
            int panelCount = string.IsNullOrEmpty(cube.displayOrder) ? 1 : cube.displayOrder.Length;

            for (int panelIndex = 0; panelIndex < panelCount; panelIndex++)
            {
                Camera camera = NewCamera(cube.vrId + " panel " + (panelIndex + 1), panelIndex);
                group.cameras.Add(camera);
                char panelId = string.IsNullOrEmpty(cube.displayOrder)
                    ? '\0'
                    : cube.displayOrder[panelIndex];
                group.alwaysBlack.Add(
                    !string.IsNullOrEmpty(cube.alwaysBlackPanels) &&
                    cube.alwaysBlackPanels.IndexOf(panelId) >= 0);
            }

            cubeCameras.Add(group);
        }
    }

    private Camera NewCamera(string cameraName, float depth)
    {
        GameObject cameraObject = new GameObject(cameraName);
        cameraObject.transform.SetParent(transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 0;
        camera.depth = depth;
        camera.targetDisplay = config.targetDisplay;
        camera.useOcclusionCulling = false;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        return camera;
    }

    private void Update()
    {
        bool changed = false;

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.UpArrow) ||
            Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.Equals) ||
            Input.GetKeyDown(KeyCode.KeypadPlus))
        {
            activeShade = Wrap(activeShade + 1, config.shades.Length);
            changed = true;
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                 Input.GetKeyDown(KeyCode.PageDown) || Input.GetKeyDown(KeyCode.Minus) ||
                 Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            activeShade = Wrap(activeShade - 1, config.shades.Length);
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            activeCube = Wrap(activeCube + 1, config.cubes.Length);
            changed = true;
        }

        for (int cubeIndex = 0; cubeIndex < Mathf.Min(config.cubes.Length, 9); cubeIndex++)
        {
            KeyCode key = (KeyCode)((int)KeyCode.Alpha1 + cubeIndex);
            KeyCode keypadKey = (KeyCode)((int)KeyCode.Keypad1 + cubeIndex);
            if (Input.GetKeyDown(key) || Input.GetKeyDown(keypadKey))
            {
                activeCube = cubeIndex;
                changed = true;
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
        {
            int blackIndex = Array.FindIndex(config.shades,
                shade => shade.r == 0 && shade.g == 0 && shade.b == 0);
            if (blackIndex >= 0)
            {
                activeShade = blackIndex;
                changed = true;
            }
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            int backgroundIndex = Array.FindIndex(config.shades,
                shade => string.Equals(shade.name, "background", StringComparison.OrdinalIgnoreCase));
            if (backgroundIndex >= 0)
            {
                activeShade = backgroundIndex;
                changed = true;
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }

        UpdateCameraRects(false);

        if (changed)
        {
            ApplyColours();
            LogCurrentSelection();
        }
    }

    private void UpdateCameraRects(bool force)
    {
        if (!force && Screen.width == lastScreenWidth && Screen.height == lastScreenHeight)
        {
            return;
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        for (int cubeIndex = 0; cubeIndex < config.cubes.Length; cubeIndex++)
        {
            PanelLayout cube = config.cubes[cubeIndex];
            for (int panelIndex = 0; panelIndex < cubeCameras[cubeIndex].cameras.Count; panelIndex++)
            {
                float xPixels = cube.startCol * cube.ledPanelWidth;
                float yFromTopPixels = cube.startRow * cube.ledPanelHeight;

                if (cube.horizontal)
                {
                    xPixels += panelIndex * cube.ledPanelWidth;
                }
                else
                {
                    yFromTopPixels += panelIndex * cube.ledPanelHeight;
                }

                float x = xPixels / Screen.width;
                float y = 1f - ((yFromTopPixels + cube.ledPanelHeight) / Screen.height);
                float width = (float)cube.ledPanelWidth / Screen.width;
                float height = (float)cube.ledPanelHeight / Screen.height;
                cubeCameras[cubeIndex].cameras[panelIndex].rect = new Rect(x, y, width, height);
            }
        }
    }

    private void ApplyColours()
    {
        Shade shade = config.shades[activeShade];
        Color selected = new Color32(
            (byte)Mathf.Clamp(shade.r, 0, 255),
            (byte)Mathf.Clamp(shade.g, 0, 255),
            (byte)Mathf.Clamp(shade.b, 0, 255),
            255);

        for (int cubeIndex = 0; cubeIndex < cubeCameras.Count; cubeIndex++)
        {
            for (int panelIndex = 0;
                 panelIndex < cubeCameras[cubeIndex].cameras.Count;
                 panelIndex++)
            {
                bool showShade = cubeIndex == activeCube &&
                    !cubeCameras[cubeIndex].alwaysBlack[panelIndex];
                cubeCameras[cubeIndex].cameras[panelIndex].backgroundColor =
                    showShade ? selected : Color.black;
            }
        }
    }

    private void OnGUI()
    {
        if (config == null || config.shades == null || config.shades.Length == 0)
        {
            return;
        }

        EnsureGuiStyles();
        Shade shade = config.shades[activeShade];
        string label = string.Format(
            "{0}   |   shade {1}/{2}: {3}   {4}   RGB {5}, {6}, {7}\n" +
            "Arrows: shade    1-{8}: VR cube    Tab: next cube    0: black    B: background    Esc: quit",
            cubeCameras[activeCube].name,
            activeShade + 1,
            config.shades.Length,
            shade.name,
            shade.hex,
            shade.r,
            shade.g,
            shade.b,
            config.cubes.Length);

        GUI.Box(new Rect(config.labelX, config.labelY, 900, 92), GUIContent.none, boxStyle);
        GUI.Label(new Rect(config.labelX + 12, config.labelY + 8, 876, 76), label, labelStyle);
    }

    private void EnsureGuiStyles()
    {
        if (labelStyle != null)
        {
            return;
        }

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleLeft
        };
        boxStyle = new GUIStyle(GUI.skin.box);
    }

    private void LogCurrentSelection()
    {
        Shade shade = config.shades[activeShade];
        Debug.Log(string.Format("Shade calibration: {0}, {1}, {2}, RGB({3},{4},{5})",
            cubeCameras[activeCube].name, shade.name, shade.hex, shade.r, shade.g, shade.b));
    }

    private static int Wrap(int value, int count)
    {
        return (value % count + count) % count;
    }
}
