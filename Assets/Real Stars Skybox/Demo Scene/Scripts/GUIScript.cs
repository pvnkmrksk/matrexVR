using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class GUIScript : MonoBehaviour {

    public Camera skyboxCamera;
    private SkyboxCameraScript skybox;

    private Camera mainCamera;
    private MainCameraScript mainCameraScript;

    public Text latLongText;
    public Text dateDayText;
    public Text dateMonthText;
    public Text GMTText;
    public Text timeHourText;
    public Text timeMinuteText;
    public Text timeSecondText;
    private int timeHour = 12; //this is the time in the timezone set in the GUI, not GMT time. The skybox script converts time to GMT for calculations.
    private int timeMinute = 0;
    private float timeSecond = 0; //store seconds as a float to take account of partial seconds. Other time variables stored as ints
    private int timeZone = 0; //some countries have a 1/2 hour (ie a float) timezone, but this is ignored here and timezone is set as an int for simplicity
    private int dateDay = 22;
    private int dateMonth = 2; //starting time/date is 12:00 (GMT) on 22 March (spring equinox), the date when the skybox is at y-axis rotation of -180
    private string[] monthsArray = new string[] {"Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"};
    private int[] daysPerMonthArray = new int[] { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

    //Time is displayed as a time/date, but handled internally as a number of seconds, stored as timeTotal
    //The start of the year is 0 seconds, and midnight at the end of 31 December is 365days*24hours*60minutes*60seconds = 31536000 seconds
    //Either the date or the number of seconds can be provided to the skyboxCamera script
    //NB The skybox script doesn't handle leap years and is only a "rough" approximation of the position of the stars, based on their alignment in the year 2000. 
    private double timeTotal; //number of seconds since midnight at the start of 1 January.
    const int secondsInAYear = 31536000;
    //private float 
    //sumOfDays is the cumulative total of the number of days that have passed at the start of each month
    //used to help calculate timeTotal since start of year
    private int[] sumOfDaysArray = new int[] { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 };


    //interface seta a speed value indicatyng the speed at which time passes in game: from 0 (paused) to 3 (very fast)
    //the speed value is converted to a daysPerSecond value for internal calculations
    private float daysPerSecond = 0f; //number of seconds of real time for a day of game time to pass.    
    //speed 3 => daysPerSecond= 1/1 (1 day per second); speed 2 => daysPerSecond=1/60 (1 day per minute); speed 1 => daysPerSecond=1/3600 (1 day per hour)

    // Use this for initialization
    void Start () {
        mainCamera = Camera.main;
        mainCameraScript = mainCamera.GetComponent<MainCameraScript>();
        skybox = skyboxCamera.GetComponent<SkyboxCameraScript>();
        UpdateTime();
    }    
	
	// Update is called once per frame
	void Update () {
        if (daysPerSecond > 0)
            PassageOfTime();

        latLongText.text = "Lat/Long: " + mainCameraScript.latitude.ToString("0.0") + ", " + mainCameraScript.longitude.ToString("0.0");
        dateDayText.text = dateDay.ToString("0");
        dateMonthText.text = monthsArray[dateMonth];
        //Timezone control not implemented
        GMTText.text = "GMT" + timeZone.ToString("+00;-00");
        timeHourText.text = timeHour.ToString("00"); //display time at with timezone changes
        timeMinuteText.text = timeMinute.ToString("00");
        timeSecondText.text = timeSecond.ToString("00");

        skybox.UpdateDateTime(dateMonth, dateDay, timeHour, timeMinute, timeSecond, timeZone);

    }    

    private void PassageOfTime()
    {
        double secondsPassed = Time.deltaTime * daysPerSecond * 24 * 60 * 60; //number of seconds passed since last frame        
        timeTotal += secondsPassed;
        if (timeTotal >= secondsInAYear)
            timeTotal -= secondsInAYear;

        float currentDays = (float)timeTotal / (24 * 60 * 60f);
        float currentHours = (currentDays - Mathf.Floor(currentDays)) * 24;
        float currentMinutes = (currentHours - Mathf.Floor(currentHours)) * 60;
        float currentSeconds = (currentMinutes - Mathf.Floor(currentMinutes)) * 60;

        //now calculate the month, day, hour and minute for display
        for (int i = 0; i < 12; i++)
        {
            if (currentDays > sumOfDaysArray[i])            
                dateMonth = i;
        }

        dateDay = Mathf.FloorToInt(currentDays) - sumOfDaysArray[dateMonth] + 1;
        timeHour = Mathf.FloorToInt(currentHours);
        timeMinute = Mathf.FloorToInt(currentMinutes);
        timeSecond = Mathf.Floor(currentSeconds);
    }

    private void UpdateTime()
    {
        timeTotal = (((sumOfDaysArray[dateMonth] + dateDay - 1) * 24 + timeHour) * 60 + timeMinute) * 60 + timeSecond;
    }


    //Whenever the GUI is used to change the time/date, the methods below check hour/minute/day/month/year boundary conditions
    private void CheckDay()
    {
        if (dateDay <= 0)
        {
            dateMonth -= 1;
            CheckMonth();
            dateDay = daysPerMonthArray[dateMonth];
        }        
        else if(dateDay> daysPerMonthArray[dateMonth])
        {
            dateMonth += 1;
            CheckMonth();
            dateDay = 1;
        }

        UpdateTime();
    }

    private void CheckMonth()
    {
        if (dateMonth < 0)
        {
            dateMonth = 11;            
        }
        else if (dateMonth > 11)
        {
            dateMonth = 0;
        }

        if (dateDay > daysPerMonthArray[dateMonth])
            dateDay = daysPerMonthArray[dateMonth];

        UpdateTime();
    }

    private void CheckMinute()
    {
        if (timeMinute <= 0)
        {
            timeMinute = 59;
            timeHour -= 1;
            CheckHour();            
        }
        else if (timeMinute > 59)
        {
            timeMinute = 0;
            timeHour += 1;
            CheckHour();            
        }

        UpdateTime();
    }

    private void CheckHour()
    {
        if (timeHour < 0)
        {
            timeHour = 23;
            dateDay -= 1;
            CheckDay();
        }
        else if (timeHour > 23)
        {
            timeHour = 0;
            dateDay += 1;
            CheckDay();
        }

        UpdateTime();
    }
   
    private void CheckTimeZone()
    {
        if (timeZone < -11)
        {
            timeZone = 12;
        }
        else if (timeZone > 12)
        {
            timeZone = -11;
        }
    }


    //---------------
    //GUI METHODS called by on screen buttons
    //

    public void DateDayDown()
    {
        dateDay -= 1;
        CheckDay();
    }

    public void DateDayUp()
    {
        dateDay += 1;
        CheckDay();
    }

    public void DateMonthDown()
    {
        dateMonth -= 1;
        CheckMonth();
    }

    public void DateMonthUp()
    {
        dateMonth += 1;
        CheckMonth();
    }

    public void TimeHourDown()
    {
        timeHour -= 1;
        CheckHour();
    }

    public void TimeHourUp()
    {
        timeHour += 1;
        CheckHour();
    }

    public void TimeMinuteDown()
    {
        timeMinute -= 1;
        CheckMinute();
    }

    public void TimeMinuteUp()
    {
        timeMinute += 1;
        CheckMinute();
    }

    //Note: changing the timezone DOES NOT change the time. Conversions to GMT are handled by the skybox script
    public void TimeZoneDown()
    {
        timeZone -= 1;
        CheckTimeZone();
    }

    public void TimeZoneUp()
    {
        timeZone += 1;
        CheckTimeZone();
    }

    //sets timezone automatically based on longitude. 
    //This will not always (or even often!) give the "correct" value, since timezones don't really follow longitude lines. 
    //e.g. continental Europe is almost entirely at GMT+1, and the UK uses different timezones (GMT or BST) depending on the time of year
    //But this button will get you in the ballpark and you can adjust from there.
    //Except for countries like Australia which have a 1/2 hour time zone just to be difficult!
    public void TimeZoneAuto()
    {
        timeZone = (int)Mathf.Floor(Mathf.Abs(mainCameraScript.longitude * 2 / 30)) * (int)Mathf.Sign(mainCameraScript.longitude);        
        CheckTimeZone();
    }

    //sets the speed at which time passes from the 4 GUI buttons ()
    public void SetSpeed(int speed)
    {
        if (speed == 0)
            daysPerSecond = 0f;
        else
            daysPerSecond = Mathf.Pow(60, (speed - 1)) * 1 / 3600f;       
    }
}
