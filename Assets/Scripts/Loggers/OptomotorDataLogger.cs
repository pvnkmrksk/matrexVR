using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Data logger for the Optomotor experiment.
/// 
/// This logger records additional data specific to optomotor experiments, including:
/// - Stimulus parameters (frequency, contrast, speed, etc.)
/// - Rotation settings (axis, direction)
/// - Closed loop configuration
/// 
/// This logger automatically fetches data from the OptomotorSceneController.
/// </summary>
public class OptomotorDataLogger : DataLogger
{
    /// <summary>
    /// Reference to the OptomotorSceneController that provides stimulus data
    /// </summary>
    private OptomotorSceneController controller;

    /// <summary>
    /// Initializes the Optomotor logger.
    /// 
    /// This method:
    /// 1. Disables ZMQ data (not needed for optomotor experiments)
    /// 2. Adds all required columns for stimulus data
    /// 3. Finds the OptomotorSceneController to get data from
    /// </summary>
    protected override void Start()
    {
        // Disable ZMQ data for this logger
        includeZmqData = false;

        // Add columns for stimulus parameters
        AddColumns(
            "StimulusIndex",
            "Frequency",
            "Contrast",
            "DutyCycle",
            "Speed",
            "RotationAxis",
            "ClockwiseRotation",
            "ClosedLoopOrientation",
            "ClosedLoopPosition"
        );

        base.Start();

        // Find the controller that provides stimulus data
        controller = FindObjectOfType<OptomotorSceneController>();
        if (controller == null)
        {
            Debug.LogError("OptomotorSceneController not found in scene");
        }
    }

    /// <summary>
    /// Collects additional data for the Optomotor experiment.
    /// 
    /// This method retrieves stimulus data from the OptomotorSceneController
    /// and adds it to the log using the SetData method.
    /// 
    /// The controller's GetLoggingData() method returns a dictionary with all 
    /// current stimulus parameters that match the column headers we added in Start().
    /// </summary>
    protected override void CollectAdditionalData()
    {
        if (controller != null)
        {
            Dictionary<string, object> stimulusData = controller.GetLoggingData();
            if (stimulusData != null && stimulusData.Count > 0)
            {
                // Use the convenient helper method to add all data
                SetData(stimulusData);
            }
        }
    }
}


// public class MyCustomLogger : DataLogger
// {
//     private MySensor temperatureSensor;
    
//     protected override void Start()
//     {
//         // Add your custom columns
//         AddColumns("Temperature", "Humidity", "Pressure");
        
//         base.Start();
        
//         // Find your components
//         temperatureSensor = GetComponent<MySensor>();
//     }
    
//     protected override void CollectAdditionalData()
//     {
//         // Manual way to set individual values:
//         SetData("Temperature", temperatureSensor.temperature);
//         SetData("Humidity", GetComponent<HumiditySensor>().GetHumidity());
        
//         // For calculated values:
//         float pressure = CalculatePressure();
//         SetData("Pressure", pressure);
//     }
    
//     private float CalculatePressure()
//     {
//         // Your custom calculation code
//         return 1013.25f;
//     }
// }