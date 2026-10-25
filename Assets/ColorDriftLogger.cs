using System;
using System.Globalization;
using System.Text;
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
    }

    // This logger writes one compact row per cylinder, not the base rig-pose schema.
    protected override string BuildHeader() => "Current Time,CylinderName,CurrentBlue,TargetMean,experimentPhase";

    // We won’t rely on DataLogger’s default single-line approach here.
    // Instead, we’ll manually iterate over each cylinder and write one line per cylinder per frame.
    protected override void LateUpdate()
    {
        // If you want a time stamp each frame:
        string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture);
        
        // For each dynamic cylinder, append a line with the currentBlue
        foreach (ColorDrift drift in colorDrifts)
        {
            // Build the line:
            // We already know DataLogger has a 'line' member, so reuse or just declare a new local string.

            if (drift == null) continue;
            float myBlue = drift.CurrentBlue;       // was drift.currentBlue
            bool isUsingA = drift.IsUsingMeanA;     // was drift.IsUsingMeanA

            float targetMean = isUsingA ? drift.meanBlueA : drift.meanBlueB;

            var row = new StringBuilder("\n");
            RecordedCsv.Append(row,currentTime);row.Append(',');RecordedCsv.Append(row,drift.gameObject.name);
            row.Append(',');RecordedCsv.Append(row,myBlue);row.Append(',');RecordedCsv.Append(row,targetMean);
            row.Append(',');RecordedCsv.Append(row,MainController.RecordedExperimentPhase);line=row.ToString();

            
            // Call LogData() to either buffer or write the line directly (depending on the DataLogger settings)
            LogData(line);
        }
    }
}
