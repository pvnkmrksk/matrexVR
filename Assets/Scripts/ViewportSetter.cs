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

    // VR identifier for config lookup
    [SerializeField]
    public string vrId = "VR1";

    void Start()
    {
        // set vsyn to true to avoid tearing and 60 fps
        QualitySettings.vSyncCount = 1;

        // Apply VR config at start
        ApplyVRConfig();

        setViewport();
    }

    private void ApplyVRConfig()
    {
        // Find the MainController
        MainController mainController = FindObjectOfType<MainController>();
        if (mainController != null)
        {
            // Get config values based on vrId
            VRConfig config = mainController.GetVRConfig(vrId);

            // Apply config values directly
            ledPanelWidth = config.ledPanelWidth;
            ledPanelHeight = config.ledPanelHeight;
            startRow = config.startRow;
            startCol = config.startCol;
            horizontal = config.horizontal;
        }
    }

    void setViewport()
    {
        // This method creates four viewports for four cameras that render a portion of the led panel.
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

        // Set the viewport and fov for each camera in the scene by name
        // The cameras are named Main Camera L, Main Camera F, Main Camera R, Main Camera B
        // Camera camera = GameObject.Find("Main Camera L").GetComponent<Camera>();

        // camera.rect = new Rect(viewport_x, viewport_y, viewport_width, viewport_height);


        // find all the cameras in the children of this script
        Camera[] cameras = GetComponentsInChildren<Camera>();

        if (horizontal)
        {
            // find and set each camera by name
            foreach (Camera cam in cameras)
            {
                if (cam.name == "Main Camera D")
                {
                    cam.rect = new Rect(viewport_x, viewport_y, viewport_width, viewport_height);
                }

                if (cam.name == "Main Camera R")
                {
                    cam.rect = new Rect(
                        viewport_x + viewport_width,
                        viewport_y,
                        viewport_width,
                        viewport_height
                    );
                }

                if (cam.name == "Main Camera B")
                {
                    cam.rect = new Rect(
                        viewport_x + 2 * viewport_width,
                        viewport_y,
                        viewport_width,
                        viewport_height
                    );
                }

                if (cam.name == "Main Camera L")
                {
                    cam.rect = new Rect(
                        viewport_x + 3 * viewport_width,
                        viewport_y,
                        viewport_width,
                        viewport_height
                    );
                }

                if (cam.name == "Main Camera F")
                {
                    cam.rect = new Rect(
                        viewport_x + 4 * viewport_width,
                        viewport_y,
                        viewport_width,
                        viewport_height
                    );
                }
                if (cam.name == "Main Camera U")
                {
                    cam.rect = new Rect(
                        viewport_x + 5 * viewport_width,
                        viewport_y,
                        viewport_width,
                        viewport_height
                    );
                }
                cam.fieldOfView = 90; // set fov to 90 degrees
            }
        }
        else
        {
            // find and set each camera by name
            foreach (Camera cam in cameras)
            {
                if (cam.name == "Main Camera L")
                {
                    cam.rect = new Rect(viewport_x, viewport_y, viewport_width, viewport_height);
                }

                if (cam.name == "Main Camera F")
                {
                    cam.rect = new Rect(
                        viewport_x,
                        viewport_y - viewport_height,
                        viewport_width,
                        viewport_height
                    );
                }

                if (cam.name == "Main Camera R")
                {
                    cam.rect = new Rect(
                        viewport_x,
                        viewport_y - 2 * viewport_height,
                        viewport_width,
                        viewport_height
                    );
                }

                if (cam.name == "Main Camera B")
                {
                    cam.rect = new Rect(
                        viewport_x,
                        viewport_y - 3 * viewport_height,
                        viewport_width,
                        viewport_height
                    );
                }
                cam.fieldOfView = 90; // set fov to 90 degrees
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
