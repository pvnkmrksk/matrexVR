using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewportSetter : MonoBehaviour
{
    // Start is called before the first frame update

    [SerializeField]
    private int ledPanelWidth = 128;

    [SerializeField]
    private int ledPanelHeight = 128;

    [SerializeField]
    private int startRow = 0;

    [SerializeField]
    private int startCol = 0;

    [SerializeField]
    private bool horizontal = true;

    [SerializeField]
    private bool interactive = false;

    void Start()
    {
        setViewport();
    }

    void setViewport()
    {
        //create a four tiny viewport for each camera based on the led panel size in pixels and the
        // screen resolution. each camera will render to one viewport. the four viewports will be arranged
        // in a row, starting from the top left corner of the screen.
        // the four viewports will be the same resolution as the led panel.
        // the four viewports will be arranged in a row, starting from the top left corner of the screen.

        // // clear the old viewport image in the screen
        // GL.Clear(true, true, Color.black);
        // 1. get the screen resolution

        int screen_width = Screen.width;
        int screen_height = Screen.height;

        // 2. calculate the viewport size. the viewport size is the same as the led panel size.
        float viewport_width = (float)ledPanelWidth / (float)screen_width;
        float viewport_height = (float)ledPanelHeight / (float)screen_height;

        // 3. calculate the viewport position. the viewport position starts from the top left corner of the screen.
        // adjust the start location based on the start row and start column. flip the start row because the screen
        // coordinate system starts from the top left corner.

        float viewport_x = (float)startCol * viewport_width;
        float viewport_y = 1.0f - viewport_height - (float)startRow * viewport_height;

        // 4. set the viewport for each camera in the scene, starting from the top left corner of the screen.
        // the camera name is Main Camera L, Main Camera F, Main Camera R, Main Camera B
        // Camera camera = GameObject.Find("Main Camera L").GetComponent<Camera>();

        // camera.rect = new Rect(viewport_x, viewport_y, viewport_width, viewport_height);



        // find all the cameras in the children of this script
        Camera[] cameras = GetComponentsInChildren<Camera>();

        if (horizontal)
        {
            // find and set each camera by name
            foreach (Camera cam in cameras)
            {
                if (cam.name == "Main Camera R")
                {
                    cam.rect = new Rect(viewport_x, viewport_y, viewport_width, viewport_height);
                }

                if (cam.name == "Main Camera B")
                {
                    cam.rect = new Rect(
                        viewport_x + viewport_width,
                        viewport_y,
                        viewport_width,
                        viewport_height
                    );
                }

                if (cam.name == "Main Camera L")
                {
                    cam.rect = new Rect(
                        viewport_x + 2 * viewport_width,
                        viewport_y,
                        viewport_width,
                        viewport_height
                    );
                }

                if (cam.name == "Main Camera F")
                {
                    cam.rect = new Rect(
                        viewport_x + 3 * viewport_width,
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
