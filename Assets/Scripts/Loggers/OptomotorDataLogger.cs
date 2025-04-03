using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

public class OptomotorDataLogger : DataLogger
{
    private OptomotorSceneController controller;

    protected override void Start()
    {
        // Disable ZMQ data for this logger
        includeZmqData = false;

        // Add our headers before calling base.Start() so they're included in the header row
        AddHeader("StimulusIndex");
        AddHeader("Frequency");
        AddHeader("Contrast");
        AddHeader("DutyCycle");
        AddHeader("Speed");
        AddHeader("RotationAxis");
        AddHeader("ClockwiseRotation");
        AddHeader("ClosedLoopOrientation");
        AddHeader("ClosedLoopPosition");

        base.Start();
        controller = FindObjectOfType<OptomotorSceneController>();

        // Make sure we found the controller
        if (controller == null)
        {
            Debug.LogError("OptomotorSceneController not found in scene");
        }
    }

    // Override the new method instead of PrepareLogData
    protected override void CollectAdditionalData()
    {
        // Get the stimulus data and add it to additionalData
        if (controller != null)
        {
            Dictionary<string, object> stimulusData = controller.GetLoggingData();
            if (stimulusData != null)
            {
                foreach (string header in additionalHeaders)
                {
                    if (stimulusData.TryGetValue(header, out object value))
                    {
                        SetAdditionalData(header, value);
                    }
                }
            }
        }
    }
}