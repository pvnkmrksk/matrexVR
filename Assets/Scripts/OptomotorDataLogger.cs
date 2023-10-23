// using System;
// using System.Collections.Generic;
// using UnityEngine;

// // Data logger for scene-specific data Example for override
// public class OptomotorDataLogger : DataLogger
// {
//     // Find the Grating Drum go transform the script is attached to
//     public GameObject gratingDrum;
//     private GameObject drum;
//     protected override void Start()
//     {

//         // Call the base class's Start method
//         base.Start();

//         // Find the GratingDrum GameObject
//         drum = gratingDrum;

//         // Check if the drum GameObject is found
//         if (drum != null)
//         {
//             // Log the drum's transform
//             Logger.Log("Found GratingDrum GameObject: " + drum.name);
//         }
//         else
//         {
//             Logger.Log("GratingDrum GameObject not found in the scene.", 1);
//         }
//     }



//     // Initializes the log file
//     public override void InitLog()
//     // In C#, when a method in a derived class has the same name as a method in its base class, the compiler needs to know whether the derived class's method is intended to:
//     // 1. Override the base class's method: This means the derived class's method will be used instead of the base class's method when called on an instance of the derived class. To indicate this, you use the override keyword.
//     // 2. Hide the base class's method: This means the derived class's method is unrelated to the base class's method, even though they have the same name. To indicate this, you use the new keyword.
//     // In your case, you want to override the InitLog method in the DataLogger class, so you should add the override keyword to the InitLog method in the SceneDataLogger class:

//     {
//         // Call the base class's InitLog method
//         base.InitLog();

//         // Add a new column to the header row
//         logFile.WriteLine(",DrumPosX,DrumPosY,DrumPosZ,DrumRotX,DrumRotY,DrumRotZ");
//         Logger.Log("OptomotorDataLogger.InitLog()");
//     }

//     // Prepares a line of data to be logged
//     protected override void PrepareLogData()
//     {
//         // Call the base class's PrepareLogData method to prepare the basic data
//         base.PrepareLogData();

//         // Check if the drum GameObject is assigned
//         if (drum != null)
//         {
//             // Get the position and rotation of the drum GameObject
//             Vector3 drumPosition = drum.transform.position;
//             Quaternion drumRotation = drum.transform.rotation;

//             Logger.Log("OptomotorDataLogger.PrepareLogData()");
//             // Append the drum's position and rotation to line
//             line += $",{drumPosition.x},{drumPosition.y},{drumPosition.z},{drumRotation.eulerAngles.x},{drumRotation.eulerAngles.y},{drumRotation.eulerAngles.z}";
//         }
//         else
//         {
//             Logger.Log("Drum GameObject is not assigned in the OptomotorDataLogger.", 1);
//         }
//     }
// }