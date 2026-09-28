using System;
using System.Collections.Generic;
using UnityEngine;

public class ColorDriftLogger : DataLogger
{
    private List<ColorDrift> colorDrifts;

    protected override void Start()
    {
        // IMPORTANT: call base.Start() so DataLogger sets up file, timestamp, etc.
        base.Start(); 

        // Gather all ColorDrift components in the scene:
        colorDrifts = new List<ColorDrift>(FindObjectsOfType<ColorDrift>());

        // Optionally, write a dedicated header if you like
        // Because DataLogger already writes a default header, you can just append columns below.
        // Or you can do it in InitLog() or after the file is first created.
        logFile.WriteLine(",CylinderName,CurrentBlue");
    }

    // We won’t rely on DataLogger’s default single-line approach here.
    // Instead, we’ll manually iterate over each cylinder and write one line per cylinder per frame.
    protected override void Update()
    {
        // If you want a time stamp each frame:
        string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        
        // For each dynamic cylinder, append a line with the currentBlue
        foreach (ColorDrift drift in colorDrifts)
        {
            // Build the line:
            // We already know DataLogger has a 'line' member, so reuse or just declare a new local string.

            float myBlue = drift.CurrentBlue;       // was drift.currentBlue
            bool isUsingA = drift.IsUsingMeanA;     // was drift.IsUsingMeanA

            float targetMean = isUsingA ? drift.meanBlueA : drift.meanBlueB;

            line = $"\n{currentTime},{drift.gameObject.name},{myBlue:F2},{targetMean:F2}";

            
            // Call LogData() to either buffer or write the line directly (depending on the DataLogger settings)
            LogData(line);
        }
    }
}
