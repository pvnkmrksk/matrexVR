using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkyboxCameraScript : MonoBehaviour {

    //To correctly align the skybox, this script needs to know three main things:
    //(1) which direction the main camera is looking in
    //(2) what is the longitude/latitude and which direction (in the x-z plane) is North in the game
    //(3) what is the date and time (based on month/day/hour/minute/second and timezone. The year is irrelevant)
    //With this information, the skybox camera is given the correct orientation to view the skybox correctly and to show the real world star positions. 
    //Note: the camera viewing the skybox is rotated, not the skybox itself. So rotation directions are the opposite of what you might first think!

    //This information can be hard coded or left as default values, or might use variables that change and are fetched from or set by other scripts.
    //e.g. the player might travel long distances, affecting the longitude/latitude. Or time might pass quickly, requiring the stars to spin across the sky

    //In this example scene, the information for (1) and (2) is fetched from the main camera
    //The information for (3) is sent by the GUIscript, which controls the date/time interface at the bottom right of the screen.

    //(1) which direction is camera looking?      
    private Camera mainCamera; //reference mainCamera to get rotation from its transform. MAKE SURE TO TAG YOUR MAIN CAMERA!
    private Quaternion viewingAngle; //the angle of the skybox camera based on which direction the player is looking    

    //(2) what is the longitude latitude and which direction is North
    private MainCameraScript mainCameraScript; //reference mainCamera script to get longitude and latitude
    private float longitude = 0f; //east/west position. Defaults to 0.
    private float latitude = 0f; //north/south position. Defaults to 0.    
    public Vector3 gameNorthDirection = new Vector3(0f, 0f, 1f); //vector pointing in North direction in gameworld. Defaults to +z direction
    private Vector3 skyboxNorthDirection = new Vector3(0f, 0f, 1f); //North on the skybox is fixed in the +z direction. Make sure it's a normalised vector if you ever change it for some reason!
    private Quaternion locationAngle; //the angle of the skybox camera based on the longitude/latitude and North direction

    //(3) what is the time and date
    //Time can be provided as GMT time, or it can be provided as a local time along with the timezone different from GMT
    //In this script, time is handled as a number of seconds that has passed from noon on 22 March. Note that the year is not needed or used.
    //22 March was the date of the spring equinox in the year 2000 and is used as the origin point of right ascension, which determines the positions of the stars round the celestial sphere
    //If you want to learn more, you can look up on amateur telescope websites how the origin point (or line) of right ascension is determined in astronomy.
    //The simple version is that, at noon on 22 March, the y-axis rotation of the skybox is exactly -180 degrees round from due North. Rotations for all other times of the year are calculated from there.

    //This script takes a date and converts it to a number of seconds since noon on 22 March.
    //To simplify this calculation, it first calculates the number of seconds since midnight at the start of 1 January.
    //Midnight at the end of 31 December is then 365days*24hours*60minutes*60seconds = 31536000 seconds = secondsInAYear
    //To get the number of seconds since 22 March is then a simple subtraction
    //NB THIS SCRIPT DOES NOT HANDLE LEAP YEARS! February must have 28 days.
    //Instead, it uses a 365 day year and a perfectly looping cycle to make a decent approximation of the position of the stars based on their positions in 2000.
    //Differences from the real night sky are probably so small that you couldn't see them for 100 years or more!    
    private int[] sumOfDaysArray = new int[] { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 }; //cumulative total of the number of days that have passed at the start of each month. It is used to help calculate timeSinceJan
    //private int[] daysPerMonthArray = new int[] { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };    
    const int secondsInAYear = 31536000; //number of seconds in a year
    const int secondsJanToEquinox = 6955200; //number of seconds from midnight on 1 January to noon on 22 March. =(31+28+21)days+12 hours = (80days*24hours+12hours)*60minutes*60seconds = 6955200seconds    
    private double timeSinceJan = 6955200d; //number of seconds since midnight at the start of 1 January. Defaults to 6955200
    private double timeSinceEquinox = 0d; //number of seconds since noon on 22 March, the moment of the Spring Equinox. Defaults to 0.

    //The earth takes 24 hours to spin on its axis, so the sun is always at its highest point at noon each day. This is called a solar day.
    //But because the earth also moves round the sun, the stars are not in the same position every 24 hours.
    //The time it takes for the stars to end up back at the same position each day is only 23 hours and 56 minutes. This is called a sidereal day.    
    //Over the course of a year, the sun (appears to) circle the earth 365 times, but the stars (appear to) circle the earth 366 times.
    //As an aside, this observable fact is ignored by flat earth believers in several of their failed attempts to prove the earth is flat!
    //The timeSinceEquinox value is therefore scaled up before it's used to get the correct position of the stars at that time
    private const float siderealTime = 366 / 365f; //ratio between solar day and sidereal day, used to scale up timeSinceEquinox.
    private Quaternion timeAngle; //the angle of the skybox camera based on the date/time 

    //enable swapping between skyboxes
    public Material blank, named, constell; //the three skyboxes
    private Material[] skyboxArray;
    private int selectedSkybox = 2;
    private Skybox skyboxComponent;

    // Use this for initialization
    private void Start () {
        mainCamera = Camera.main;
        mainCameraScript = mainCamera.GetComponent<MainCameraScript>();

        skyboxArray = new Material[] { blank, named, constell };
        skyboxComponent = gameObject.GetComponent<Skybox>();
        skyboxComponent.material = skyboxArray[selectedSkybox];

        timeAngle = Quaternion.Euler(0f,0,0f);
    }

    //----(1)----
    //The skybox needs to know which way the player is looking to adjust its rotation.    
    //There are many ways of getting or setting the player look direction.
    //For this example scene, the rotation of the mainCamera is fetched during each LateUpdate phase and copied direct to the skyboxCamera viewing angle   
    private void GetViewingAngle()
    {
        viewingAngle = mainCamera.transform.rotation;
    }

    //----(2)----
    //The skybox needs to know the longitude/latitude of the player to adjust its rotation. The in-game direction for North is also needed
    //There are many ways of getting or setting these
    //For this example scene, the player moves the mainCamera around a world map using WASD or arrow keys to move N/S/E/W, and this controls their longitude/latitude
    //The player's longitude and latitude and the north direction on the world map are fetched from the mainCamera scipt each LateUpdate phase    
    //For the north direction, the angle between skybox north (+z) and game North is calcuated. 
    //Based on a longitude/latitude and north direction, we work out the angle of the skybox at noon on 22 March
    //Note that, the north direction for the rectangular map is also +z, so the angle between skybox north and game north is zero.
    //For the polar map, though, the direction of north changes with position on the map, so the north direction is updated every LateUpdatephase.
    //A more likely situation is that you fix North (probably in one of the +z or +x directions) and only need to set gameNorthDirection during the Start phase.    
    private void GetLocationAngle()
    {
        //get longitude and latitude from mainCamera script        
        longitude = mainCameraScript.longitude;
        latitude = mainCameraScript.latitude;

        //get direction of North from mainCamera script and normalise it
        gameNorthDirection = Vector3.Normalize(mainCameraScript.northAngle);

        //work out angle between skybox north (+z) and game north                
        //Real games probably have north as one of the +z or +x directions, but this script can handle North being any direction in the x-z plane.
        //cos(angle) = dot product of gameNorthDirection and skyboxNorthDirection
        float angleDifference = Mathf.Acos(Vector3.Dot(gameNorthDirection, skyboxNorthDirection)); //angle in radians
        if (gameNorthDirection.x < 0)
        {
            angleDifference = 2 * Mathf.PI - angleDifference;//adapt result so give desired value in all quadrants
        }

        //The skybox needs to be rotated about an axis in the x-zplane depending on the latitude and about the y-axis depending on the longitude
        //the y-axis angle is y = -longitude - angleDifference. (ie y=-longitude when the game North is +z)
        //The latitude is more complicated. 
        //For the case where game north is in the +z direction, then the rotation is simply about the x-axis        
        //Then, at the north pole (latitude = +90), the x-axis angle = 0 and Polaris is directly overhead (the +y direction).
        //Consider a traveller moving directly south, Polaris will appear to get closer to the northern horizon. Thus, the camera rotates about the -x axis
        //So, the formula for the x rotation is x = latitude -90        
        //If game north is not in the +z direction, then this rotations will be about an axis in th x-z plane perpendicular to the direction of North
        Vector3 rotAxis = -Vector3.Cross(gameNorthDirection, Vector3.up); //cross product gives us the axis for rotation
        //create a rotation by first rotating about the +y axis, the rotating about the horizontal axis
        locationAngle = Quaternion.AngleAxis(-longitude - Mathf.Rad2Deg * angleDifference, Vector3.up) * Quaternion.AngleAxis((latitude - 90f), rotAxis);
    }

    //----(3)----
    //The skybox needs to know the date and time (and timezone if time is not GMT) to adjust its rotation.
    //There are many ways that a game could handle time.
    //For this sample scene, the skybox receives the date and time at GMT via UpdateDateTime(), and converts them to a rotation of the skybox camera. If no data is received, the default time is 22 March at noon.
    //There's an explanation of some of the physics further up in the comments of this script    
    //The date/time are sent by the GUIScript attached to the time/date GUI
    //The GUIScript uses total seconds ("timeTotal") counted from 1 January to keep track of time and converts that to a date/time.
    //This script works based on a total count of the number of seconds elapsed since noon on 22 March.     
    //It could just grab timeTotal from GUIScript and convert it, but since most games wouldn't track time in that way, it wouldn't be a very helpful example
    //Instead, this script uses a more likely scenario where it receives the time/date from GUIScript and converts that to a total number of seconds
    //Note that, if you have time passing only in realtime in your game, you could use a coroutine or similar so that this conversion is only done every few seconds
    //The movement of the stars will be so slight you won't notice the jump, and it might save some overhead in each update cycle.    
    private void GetDateTimeAngle()
    {
        //If you're not bothered about orienting the stars to a specific date or aren't sending this script a date/time, but want them to move across the sky, you could call a simple function like this every update phase
        //float timeAngleDelta = Time.deltaTime * 360 / (24 * 60 * 60); //realtime movement of stars across sky, one rotation per 24 hours
        //timeAngle = Quaternion.Euler(0f, timeAngle.eulerAngles.y - timeAngleDelta, 0f);

        //get the timeSinceEquinox, scale it up by the siderealTime factor and divide by number of seconds in a day
        double angle = (timeSinceEquinox * siderealTime / (24 * 60 * 60));
        //this is the number of 360 degree rotations the stars have made since the equinox
        //this could be a large number, but we only need the fractional part, which we multiply by 360 to get the angle
        angle = (angle - Mathf.Floor((float)angle)) * 360;

        //on 22 March, the rotation was -180, so substract the additional rotation angle to get the current rotation
        timeAngle = Quaternion.Euler(0f, -180f - (float)angle, 0f);
    }

    //receives the date/time (from GUIScript) and converts it to a number of seconds since noon on 22 March
    //NB The month is stored as an array index value, so January=0 and December=11. 
    //If you have January=1 and December=12, just subtract one from the received month
    //also optionally receives the timezone (as a number of hours +- GMT). Otherwise is defaults to 0    
    public void UpdateDateTime(int month, int day, int hour, int minute, float second, int timezone = 0)
    {
        //calculate the number of seconds of the receive date/time since the start of the year
        //converted to GMT time by subtracting the timezone from the hour.
        timeSinceJan = (((sumOfDaysArray[month] + day - 1) * 24 + (hour - timezone)) * 60 + minute) * 60 + second;

        //simple subtraction to get time since 22 March
        timeSinceEquinox = timeSinceJan - secondsJanToEquinox;
        //add secondsInAYear as a corrective offset to dates prior to 22 March so that they are positive numbers
        if (timeSinceEquinox < 0)
            timeSinceEquinox += secondsInAYear;
    }


    //Set final rotation  of skybox camera LateUpdate to make sure all other movements completed
    private void LateUpdate () {
        
        GetDateTimeAngle();
        GetLocationAngle();
        GetViewingAngle();

        //add all the rotations in sequence to get a final angle for the skybox
        transform.rotation = timeAngle * locationAngle * viewingAngle;
    }

    public void NextSkyBox()
    {
        selectedSkybox++;
        if (selectedSkybox >= skyboxArray.Length)
            selectedSkybox = 0;

        skyboxComponent.material = skyboxArray[selectedSkybox];
    }

}
