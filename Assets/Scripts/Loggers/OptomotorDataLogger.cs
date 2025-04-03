using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

public class OptomotorDataLogger : DataLogger
{
    private OptomotorSceneController controller;

    protected override void Start()
    {
        base.Start();
        controller = FindObjectOfType<OptomotorSceneController>();
    }

    public override void InitLog()
    {
        base.InitLog();

        // Add only the optomotor-specific headers
        logFile.Write(",StimulusIndex,Frequency,Contrast,DutyCycle,Speed,RotationAxis,ClockwiseRotation,ClosedLoopOrientation,ClosedLoopPosition");
    }

    protected override void PrepareLogData()
    {
        base.PrepareLogData();

        if (controller == null)
        {
            Debugger.Log("OptomotorSceneController not found in the scene", 1);
            return;
        }

        // Get the current stimulus data
        Dictionary<string, object> stimulusData = controller.GetLoggingData();

        // Add stimulus parameters
        AppendData(stimulusData, "CurrentStimulusIndex");
        AppendData(stimulusData, "Frequency");
        AppendData(stimulusData, "Contrast");
        AppendData(stimulusData, "DutyCycle");
        AppendData(stimulusData, "Speed");
        AppendData(stimulusData, "RotationAxis");
        AppendData(stimulusData, "ClockwiseRotation");
        AppendData(stimulusData, "ClosedLoopOrientation");
        AppendData(stimulusData, "ClosedLoopPosition");
    }

    private void AppendData(Dictionary<string, object> data, string key)
    {
        if (data.TryGetValue(key, out object value))
            line += $",{value}";
        else
            line += ",";
    }
}