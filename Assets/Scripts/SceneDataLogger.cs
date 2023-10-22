using System;
using System.Collections.Generic;
using UnityEngine;

// Data logger for scene-specific data
public class SceneDataLogger : DataLogger
{
    // Initializes the log file
    public override void InitLog()
    // In C#, when a method in a derived class has the same name as a method in its base class, the compiler needs to know whether the derived class's method is intended to:
    // 1. Override the base class's method: This means the derived class's method will be used instead of the base class's method when called on an instance of the derived class. To indicate this, you use the override keyword.
    // 2. Hide the base class's method: This means the derived class's method is unrelated to the base class's method, even though they have the same name. To indicate this, you use the new keyword.
    // In your case, you want to override the InitLog method in the DataLogger class, so you should add the override keyword to the InitLog method in the SceneDataLogger class:

    {
        // Call the base class's InitLog method
        base.InitLog();

        // Add a new column to the header row
        logFile.WriteLine(",Active GameObjects");
    }

    // Prepares a line of data to be logged
    protected override void PrepareLogData()
    {
        // Call the base class's PrepareLogData method to prepare the basic data
        base.PrepareLogData();

        // Count the number of active GameObjects in the scene
        int activeGameObjectCount = 0;
        foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>())
        {
            if (go.activeInHierarchy)
            {
                activeGameObjectCount++;
            }
        }

        // Append the number of active GameObjects to line
        line += "," + activeGameObjectCount.ToString();
    }

}