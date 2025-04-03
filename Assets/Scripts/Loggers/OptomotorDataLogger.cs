using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

public class OptomotorDataLogger : DataLogger
{
    // Reference to the OptomotorSceneController
    private OptomotorSceneController optomotorController;

    protected override void Start()
    {
        base.Start();

        // Find the OptomotorSceneController
        optomotorController = FindObjectOfType<OptomotorSceneController>();
        if (optomotorController == null)
        {
            Debugger.Log("OptomotorSceneController not found in the scene", 1);
        }
    }

    // Public method to trigger logging from the scene controller
    public void TriggerLogging()
    {
        // Prepare the log data first
        PrepareLogData();
        // Then call the base LogData with the prepared line
        LogData(line);
    }

    // Override the PrepareLogData method to include optomotor-specific data
    protected override void PrepareLogData()
    {
        // First get the base log data
        base.PrepareLogData();

        // If we have an optomotor controller, append its data
        if (optomotorController != null)
        {
            Dictionary<string, object> optomotorData = optomotorController.GetLoggingData();

            // Add optomotor data to the log line
            if (optomotorData.Count > 0)
            {
                // Append optomotor parameters
                if (optomotorData.TryGetValue("CurrentStimulusIndex", out object stimIndex))
                    line += $",{stimIndex}";
                else
                    line += ",";

                if (optomotorData.TryGetValue("Frequency", out object freq))
                    line += $",{freq}";
                else
                    line += ",";

                if (optomotorData.TryGetValue("Contrast", out object contrast))
                    line += $",{contrast}";
                else
                    line += ",";

                if (optomotorData.TryGetValue("DutyCycle", out object dutyCycle))
                    line += $",{dutyCycle}";
                else
                    line += ",";

                if (optomotorData.TryGetValue("Speed", out object speed))
                    line += $",{speed}";
                else
                    line += ",";

                if (optomotorData.TryGetValue("RotationAxis", out object axis))
                    line += $",{axis}";
                else
                    line += ",";

                if (optomotorData.TryGetValue("ClockwiseRotation", out object clockwise))
                    line += $",{clockwise}";
                else
                    line += ",";

                if (optomotorData.TryGetValue("ClosedLoopOrientation", out object closedOrient))
                    line += $",{closedOrient}";
                else
                    line += ",";

                if (optomotorData.TryGetValue("ClosedLoopPosition", out object closedPos))
                    line += $",{closedPos}";
                else
                    line += ",";
            }
        }
    }

    // Override InitLog to add optomotor-specific headers
    public override void InitLog()
    {
        // First call the base implementation to set up the basic log
        base.InitLog();

        // Now append our additional headers to the existing CSV file
        // Since the base.InitLog() already created the file with headers, we need to:
        // 1. Check if we need to add more headers (the file length would be exactly the header length if new)
        // 2. Append our additional headers if needed

        // Get file info
        FileInfo fileInfo = new FileInfo(base.logPath);
        if (fileInfo.Exists && fileInfo.Length > 0)
        {
            // Read the existing header to check if it already has our additional columns
            string existingHeader = File.ReadAllLines(base.logPath)[0];

            // If the header doesn't already include our columns, add them
            if (!existingHeader.Contains("StimulusIndex"))
            {
                // Append optomotor-specific headers
                // Close the log file first
                logFile?.Dispose();

                // Open the file for appending and add the headers
                using (StreamWriter writer = File.AppendText(base.logPath))
                {
                    writer.Write(",StimulusIndex,Frequency,Contrast,DutyCycle,Speed,RotationAxis,ClockwiseRotation,ClosedLoopOrientation,ClosedLoopPosition");
                }

                // Reopen the log file
                FileStream fileStream = new FileStream(
                    base.logPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read
                );
                logFile = new StreamWriter(fileStream);
            }
        }

        Debugger.Log("Optomotor logger initialized at: " + base.logPath, 3);
    }
}