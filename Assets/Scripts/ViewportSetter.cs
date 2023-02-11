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

        int screenWidth = Screen.width;
        int screenHeight = Screen.height;

        // 2. calculate the viewport size. the viewport size is the same as the led panel size.
        float viewportWidth = (float)ledPanelWidth / (float)screenWidth;
        float viewportHeight = (float)ledPanelHeight / (float)screenHeight;

        // 3. calculate the viewport position. the viewport position starts from the top left corner of the screen.
        // adjust the start location based on the start row and start column. flip the start row because the screen
        // coordinate system starts from the top left corner.

        float viewportX = (float)startCol * viewportWidth;
        float viewportY = 1.0f - viewportHeight - (float)startRow * viewportHeight;

        // 4. set the viewport for each camera in the scene, starting from the top left corner of the screen.
        // the camera name is Main Camera L, Main Camera F, Main Camera R, Main Camera B
        Camera camera = GameObject.Find("Main Camera L").GetComponent<Camera>();

        camera.rect = new Rect(viewportX, viewportY, viewportWidth, viewportHeight);

        if (horizontal)
        {
            camera = GameObject.Find("Main Camera F").GetComponent<Camera>();
            camera.rect = new Rect(
                viewportX + viewportWidth,
                viewportY,
                viewportWidth,
                viewportHeight
            );

            camera = GameObject.Find("Main Camera R").GetComponent<Camera>();
            camera.rect = new Rect(
                viewportX + 2 * viewportWidth,
                viewportY,
                viewportWidth,
                viewportHeight
            );

            camera = GameObject.Find("Main Camera B").GetComponent<Camera>();
            camera.rect = new Rect(
                viewportX + 3 * viewportWidth,
                viewportY,
                viewportWidth,
                viewportHeight
            );
        }
        else
        {
            camera = GameObject.Find("Main Camera F").GetComponent<Camera>();
            camera.rect = new Rect(
                viewportX,
                viewportY - viewportHeight,
                viewportWidth,
                viewportHeight
            );

            camera = GameObject.Find("Main Camera R").GetComponent<Camera>();
            camera.rect = new Rect(
                viewportX,
                viewportY - 2 * viewportHeight,
                viewportWidth,
                viewportHeight
            );

            camera = GameObject.Find("Main Camera B").GetComponent<Camera>();
            camera.rect = new Rect(
                viewportX,
                viewportY - 3 * viewportHeight,
                viewportWidth,
                viewportHeight
            );
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
