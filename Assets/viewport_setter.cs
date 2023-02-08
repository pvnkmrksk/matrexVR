// Author: Pavan Kaushik
// Email: pkaushik@ab.mpg.de
// Date: 2022-02-08
// Description:
// This script sets the viewport for each camera in the scene. The viewport is a tiny rectangle in the screen
// that the camera renders to. The viewport size is the same as the led panel size. The viewport position
// starts from the top left corner of the screen. The four viewports will be arranged in a row, starting
// from the top left corner of the screen. The four viewports will be the same resolution as the led panel.
// The four viewports will be arranged in a row, starting from the top left corner of the screen.
// The four viewports will be the same resolution as the led panel.
//
//
//
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.



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

    [SerializeField]
    private bool horizontal = true;

    [SerializeField]
    private bool interactive = false;

    void Start()
    {
        set_viewport();
    }

    void set_viewport()
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

        if (horizontal)
        {
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
        else
        {
            camera = GameObject.Find("Main Camera F").GetComponent<Camera>();
            camera.rect = new Rect(
                viewport_x,
                viewport_y - viewport_height,
                viewport_width,
                viewport_height
            );

            camera = GameObject.Find("Main Camera R").GetComponent<Camera>();
            camera.rect = new Rect(
                viewport_x,
                viewport_y - 2 * viewport_height,
                viewport_width,
                viewport_height
            );

            camera = GameObject.Find("Main Camera B").GetComponent<Camera>();
            camera.rect = new Rect(
                viewport_x,
                viewport_y - 3 * viewport_height,
                viewport_width,
                viewport_height
            );
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (interactive)
        {
            set_viewport();
        }
    }
}
