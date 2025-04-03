using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewportSetter : MonoBehaviour
{
    // Start is called before the first frame update

    [SerializeField]
    public int ledPanelWidth = 128;

    [SerializeField]
    public int ledPanelHeight = 128;

    [SerializeField]
    public int startRow = 0;

    [SerializeField]
    public int startCol = 0;

    [SerializeField]
    public bool horizontal = true;

    [SerializeField]
    private bool interactive = false;

    [SerializeField]
    private string displayOrder = "DRBLFU"; // Default display order

    void Start()
    {
        // set vsyn to true to avoid tearing and 60 fps
        QualitySettings.vSyncCount = 1;

        // Apply system config at start
        ApplySystemConfig();

        setViewport();
    }

    private void ApplySystemConfig()
    {
        // Find the MainController
        MainController mainController = FindObjectOfType<MainController>();
        if (mainController != null)
        {
            // Get config values based on GameObject name
            SystemConfig config = mainController.GetSystemConfigForGameObject(gameObject);

            // Apply config values directly
            ledPanelWidth = config.ledPanelWidth;
            ledPanelHeight = config.ledPanelHeight;
            startRow = config.startRow;
            startCol = config.startCol;
            horizontal = config.horizontal;
            displayOrder = config.displayOrder;

            Debug.Log($"Applied system config to {gameObject.name}: Panel={ledPanelWidth}x{ledPanelHeight}, Position={startCol},{startRow}, Horizontal={horizontal}, DisplayOrder={displayOrder}");
        }
    }

    void setViewport()
    {
        // This method creates viewports for cameras that render a portion of the led panel.
        // The viewports have the same pixel size as the led panel and are aligned to the top left corner of the screen.
        // The viewports can be arranged horizontally or vertically depending on the horizontal flag.
        // The viewports can be updated every frame or only once depending on the interactive flag.

        // Clear the previous frame from the screen with a black color
        GL.Clear(true, true, Color.black);

        // Get the current screen resolution in pixels
        int screen_width = Screen.width;
        int screen_height = Screen.height;

        // Calculate the viewport size as a fraction of the screen size
        float viewport_width = (float)ledPanelWidth / (float)screen_width;
        float viewport_height = (float)ledPanelHeight / (float)screen_height;

        // Calculate the viewport position as a fraction of the screen size
        // The position is based on the start row and start column parameters
        // The position is inverted on the y-axis because the screen origin is at the top left corner
        float viewport_x = (float)startCol * viewport_width;
        float viewport_y = 1.0f - viewport_height - (float)startRow * viewport_height;

        // find all the cameras in the children of this script
        Camera[] cameras = GetComponentsInChildren<Camera>();

        if (horizontal)
        {
            // Set viewports based on display order
            for (int i = 0; i < displayOrder.Length; i++)
            {
                char cameraId = displayOrder[i];
                foreach (Camera cam in cameras)
                {
                    if (cam.name == $"Main Camera {cameraId}")
                    {
                        cam.rect = new Rect(
                            viewport_x + i * viewport_width,
                            viewport_y,
                            viewport_width,
                            viewport_height
                        );
                        cam.fieldOfView = 90; // set fov to 90 degrees
                        break;
                    }
                }
            }
        }
        else
        {
            // Set viewports based on display order (vertical arrangement)
            for (int i = 0; i < displayOrder.Length; i++)
            {
                char cameraId = displayOrder[i];
                foreach (Camera cam in cameras)
                {
                    if (cam.name == $"Main Camera {cameraId}")
                    {
                        cam.rect = new Rect(
                            viewport_x,
                            viewport_y - i * viewport_height,
                            viewport_width,
                            viewport_height
                        );
                        cam.fieldOfView = 90; // set fov to 90 degrees
                        break;
                    }
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (interactive)
        {
            setViewport();
        }
    }
}
