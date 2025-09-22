using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapCameraScript : MonoBehaviour {

    //This script is just for the example scene to display the PIP maps of the world.
    //It's not necessary to get the skybox to work.

    private Transform mainCamera;

    // Use this for initialization
    void Start () {
        Camera mapCam = GetComponentInParent<Camera>();
        
        //want mapcamera to be 128 pixels high and 256 wide
        mapCam.rect = new Rect(5.0f / Screen.width, 5.0f / Screen.height, 256.0f / Screen.width, 128.0f / Screen.height);
        mapCam.aspect = 2.0f;
    }	
}
