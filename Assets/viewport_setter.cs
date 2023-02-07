using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class viewport_setter : MonoBehaviour
{
    // Start is called before the first frame update

    [SerializeField]
    private int led_panel_width = 128;

    [SerializeField]
    private int led_panel_height = 128;

    [SerializeField]
    private int start_row = 0;

    [SerializeField]
    private int start_col = 0;

    void Start()
    {
        //create a four tiny viewport for each camera based on the led panel size in pixels and the
        // screen resolution. each camera will render to one viewport. the four viewports will be arranged
        // in a row, starting from the top left corner of the screen.
        // the four viewports will be the same resolution as the led panel.
        // the four viewports will be arranged in a row, starting from the top left corner of the screen.



        // 5. set the background color of the scene to black
    }

    // Update is called once per frame
    void Update()
    {
        // 1. get the screen resolution
        int screen_width = Screen.width;
        int screen_height = Screen.height;

        // 2. calculate the viewport size. the viewport size is the same as the led panel size.
        float viewport_width = (float)led_panel_width / (float)screen_width;
        float viewport_height = (float)led_panel_height / (float)screen_height;

        // 3. calculate the viewport position. the viewport position starts from the top left corner of the screen.
        // adjust the start location based on the start row and start column. flip the start row because the screen
        // coordinate system starts from the top left corner.

        float viewport_x = (float)start_col * viewport_width;
        float viewport_y = 1.0f - viewport_height - (float)start_row * viewport_height;

        // 4. set the viewport for each camera in the scene, starting from the top left corner of the screen.
        // the camera name is Main Camera L, Main Camera F, Main Camera R, Main Camera B
        Camera camera = GameObject.Find("Main Camera L").GetComponent<Camera>();
        camera.rect = new Rect(viewport_x, viewport_y, viewport_width, viewport_height);

        camera = GameObject.Find("Main Camera F").GetComponent<Camera>();
        camera.rect = new Rect(
            viewport_x + viewport_width,
            viewport_y,
            viewport_width,
            viewport_height
        );

        camera = GameObject.Find("Main Camera R").GetComponent<Camera>();
        camera.rect = new Rect(
            viewport_x + 2 * viewport_width,
            viewport_y,
            viewport_width,
            viewport_height
        );

        camera = GameObject.Find("Main Camera B").GetComponent<Camera>();
        camera.rect = new Rect(
            viewport_x + 3 * viewport_width,
            viewport_y,
            viewport_width,
            viewport_height
        );
    }
}
