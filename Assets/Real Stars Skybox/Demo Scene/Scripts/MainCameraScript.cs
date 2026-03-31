using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainCameraScript : MonoBehaviour {

    private float turnSpeed = 3f; // How fast the camera rotates
    private float moveSpeed = 10f; // How fast the camera moves
    private float turnSmoothing = 4f; // How much smoothing to apply to the turn input, to reduce mouse-turn jerkiness
    
    private float panAngle; // The camera's pan angle (y axis rotation)
    private float tiltAngle; // The camera's tilt angle (x axis rotation)      
    public Vector3 northAngle; //which direction is north? // = V3(0,0,1) for rect map, but changes to point towards centre of map for polar map
    private float tiltMax = 85f; // The maximum value of the tilt rotation
    private float tiltMin = 85f; // The minimum value of the tilt rotation
    private Quaternion targetRot; //The target rotation 
    
    public float longitude; // The longitude of the camera's position (equals the camera's x position for rectangular map)
    public float latitude; // The latitude of the camera's position (equals the camera's z position for rectangular map)  
    private float longitudeMax = 180; //the boundaries of longitude
    private float latitudeMax = 89; //the boundaries of laitude
    private float radialPos; //radial polar co-ordinates of camera's position
    private float anglePos; //angular polar co-ordinates of camera's position    

    private Vector3 targetPos; //The target position

    //Which map is being displayed
    public GameObject rectMap; //the rectangular map
                               //rectangular map credit: NASA Visible Earth, Blue Marble collection
                               //Downloaded from https://visibleearth.nasa.gov/view_cat.php?categoryID=1484 and modified to add country borders

    public GameObject polarMap; //the polar map 
                                //polar map credit: Wikimedia user Strebe
                                //downloaded from https://commons.wikimedia.org/wiki/File:Azimuthal_equidistant_projection_SW.jpg
                                //Licensed as Attribution-ShareAlike 3.0 Unported (CC BY-SA 3.0)  https://creativecommons.org/licenses/by-sa/3.0/deed.en
    public bool useRectMap;    

    private void Awake()
    {        
        targetRot = transform.rotation;
        targetPos = transform.position;

        rectMap.SetActive(true);        
        polarMap.SetActive(false);
        useRectMap = true;
        northAngle = new Vector3(0f, 0f, 1f);
        ConvertLatLongRect();
    }

    private void Update()
    {
        // Get mouse input for looking while holding down "Fire2" (usually left alt and right mouse button)
        if (Input.GetButton("Fire2"))
            GetRotationMovement();        
            GetLinearMovement();        
    }

    //Get keyboard input and convert to a change in latitude/longitude
    //NB you do not move in the direction you are looking!
    //The up/down/left/right directions move you North/South/East/West on the map
    private void GetLinearMovement()
    {        
        //control with WASD or arrow keys
        float vert = Input.GetAxis("Vertical"); 
        float horiz = Input.GetAxis("Horizontal");

        latitude += moveSpeed * vert * Time.deltaTime;
        longitude += moveSpeed * horiz * Time.deltaTime;

        //clamp latitude position at north/south poles - hard boundary
        latitude = Mathf.Clamp(latitude, -latitudeMax, latitudeMax);

        //longitude wraps around, so jump from 180 to -180 at boundary
        if (Mathf.Abs(longitude) > longitudeMax)
            longitude -= 2 * longitudeMax * Mathf.Sign(longitude);

        //apply that movement depending on which map is being used
        if (useRectMap)
            ConvertLatLongRect();
        else
            ConvertLatLongPolar();

    }
    
    //Convert latitude/longitude to position on the rectangular map - direct mapping from one to the other
    private void ConvertLatLongRect()
    {        
        targetPos = new Vector3(longitude, targetPos.y, latitude);
        transform.position = targetPos;
    }
    //update camera position and direction when switching to the rectangular map
    private void SwitchToRect()
    {        
        ConvertLatLongRect(); //move to new position on map at same long/lat        
        northAngle = new Vector3(0f, 0f, 1f); //new north     
    }    

    //Convert latitude/longitude to position on the polar map
    //For the polar map N/S is a change in radial position, and E/W is a change in angualr position around the centre of the map
    private void ConvertLatLongPolar()
    {
        //convert latitude and longitude to polar co-ordinates
        //radialPos ranges from r=0 (+90 latitude) to r=90 (-90 latitude)        
        radialPos = 45 - latitude / 2f;

        //anglePos ranges from 0 (0 longitude) to 90 (+90 long) to 180 (+180 long) --(boundary jump)-- to 180 (-180 long) to 270 (-90 long) to 359 (-1 long)        
        if (longitude >= 0 && longitude <= 180)
            anglePos = longitude;
        else if (longitude >= -180 && longitude < 0)
            anglePos = longitude + 360;

        //-z direction is line of 0 longitude, and longitude increases anti-clockwise
        //so x=radialPos * sin (anglePos)
        //z=-radialPos * cos (anglePos)
        targetPos = new Vector3(radialPos * Mathf.Sin(Mathf.Deg2Rad * anglePos), targetPos.y, -radialPos * Mathf.Cos(Mathf.Deg2Rad * anglePos));
        transform.position = targetPos;

        //also need to update which direction is north - on the polar map, north always points towards (0,0,0), so it's the inverse of the x/z co-ordinates of the camera's transform        
        northAngle = new Vector3(-transform.position.x, 0f, -transform.position.z);

    }

    //update camera position and direction when switching to the polar map
    private void SwitchToPolar()
    {
        ConvertLatLongPolar(); //move to new position on map at same long/lat and get new northAngle      
    }

    private void GetRotationMovement()
    {

        // Get mouse input for looking while holding down "Fire2" (usually left alt and right mouse button)
        float pan = Input.GetAxis("Mouse X");
        float tilt = Input.GetAxis("Mouse Y");


        // Adjust pan and tilt angles
        panAngle += pan * turnSpeed;        
        tiltAngle -= tilt * turnSpeed;
        // clamp tiltAngle within range
        tiltAngle = Mathf.Clamp(tiltAngle, -tiltMin, tiltMax);

        // Create target rotation
        targetRot = Quaternion.Euler(tiltAngle, panAngle, 0f);

        if (turnSmoothing > 0)
        {            
            transform.rotation = Quaternion.Slerp(transform.localRotation, targetRot, turnSmoothing * Time.deltaTime);            
        }
        else
        {            
            transform.rotation = targetRot;            
        }        
    }

    public void SwitchMap()
    {
        rectMap.SetActive(!rectMap.activeSelf);
        polarMap.SetActive(!polarMap.activeSelf);
        useRectMap = !useRectMap;

        //reposition marker on new map
        if (useRectMap) //just switched from polar to rectangular
        {
            SwitchToRect();
        }
        else //just switched from rect to polar
            SwitchToPolar();
    }
}
