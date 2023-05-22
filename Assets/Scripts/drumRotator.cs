using UnityEngine;
using System.Collections;
using System.IO;

// A script that rotates a drum based on an input sequence from a JSON file
public class drumRotator : MonoBehaviour
{
    // Variables for the JSON file path and name
    [SerializeField]
    private string filePath = Application.dataPath + "/rotationConfigs.json";

    [SerializeField]
    private string fileName = "rotationConfigs.json";

    // Variables for the input keys for jumping back or forward in the rotation sequence
    [SerializeField]
    private KeyCode prevKey = KeyCode.LeftArrow; // key to jump to the previous rotation

    [SerializeField]
    private KeyCode nextKey = KeyCode.RightArrow; // key to jump to the next rotation

    // Variable for the current index of the rotation sequence
    private int currentIndex = 0;

    // Variable for the array of rotation configurations
    private RotationConfig[] inputSequence;

    // Define an enum for the axes of external rotation
    public enum ExternalRotationAxis
    {
        None, // no external rotation
        Pitch, // x axis
        Yaw, // y axis
        Roll // z axis
    }

    // Define a struct for rotation configuration
    [System.Serializable]
    public struct RotationConfig
    {
        // Fields for speed, direction and duration
        public float speed;
        public bool clockwise;
        public float duration;

        // Field for the external rotation axis
        public ExternalRotationAxis externalRotationAxis;
    }

    // Define a class for rotation configuration collection
    [System.Serializable]
    public class RotationConfigCollection
    {
        // Field for the array of structs
        public RotationConfig[] rotationConfigs;
    }

    // Variables for the default values of the rotation configuration fields
    private const float DEFAULT_SPEED = 100f; // degrees per second
    private const bool DEFAULT_CLOCKWISE = true; // direction of rotation
    private const float DEFAULT_DURATION = 5f; // seconds to rotate for
    private const ExternalRotationAxis DEFAULT_EXTERNAL_ROTATION_AXIS = ExternalRotationAxis.None; // external rotation axis

    void Start()
    {
        // Load the JSON file and deserialize it into an array of structs
        LoadJSONFile(filePath);

        // Start a coroutine to perform the rotations
        StartCoroutine(RotateDrum());
    }

    void Update()
    {
        // Check if the user presses the prev or next key
        if (Input.GetKeyDown(prevKey))
        {
            // Jump to the previous rotation in the sequence
            JumpToPrevRotation();
        }
        else if (Input.GetKeyDown(nextKey))
        {
            // Jump to the next rotation in the sequence
            JumpToNextRotation();
        }
    }

    // A function to load the JSON file and deserialize it into an array of structs
    void LoadJSONFile(string path)
    {
        // Check if the file exists at the path
        if (File.Exists(path))
        {
            // Read the JSON file as a string
            string json = File.ReadAllText(path);

            // Deserialize the JSON string into an object of the wrapper class
            RotationConfigCollection configCollection =
                JsonUtility.FromJson<RotationConfigCollection>(json);

            // Get the array of structs from the object
            inputSequence = configCollection.rotationConfigs;

            // Loop through the input sequence and fill in the missing fields with default values or previous values
            for (int i = 0; i < inputSequence.Length; i++)
            {
                // Get the current config from the input sequence
                RotationConfig config = inputSequence[i];

                // Check if the speed field is zero (meaning it is missing from the JSON file)
                if (config.speed == 0f)
                {
                    // Use the default speed value if this is the first config
                    if (i == 0)
                    {
                        config.speed = DEFAULT_SPEED;
                    }
                    else
                    {
                        // Use the previous speed value otherwise
                        config.speed = inputSequence[i - 1].speed;
                    }
                }

                // Check if the duration field is zero (meaning it is missing from the JSON file)
                if (config.duration == 0f)
                {
                    // Use the default duration value if this is the first config
                    if (i == 0)
                    {
                        config.duration = DEFAULT_DURATION;
                    }
                    else
                    {
                        // Use the previous duration value otherwise
                        config.duration = inputSequence[i - 1].duration;
                    }
                }

                // Check if the external rotation axis field is None (meaning it is missing from the JSON file)
                if (config.externalRotationAxis == ExternalRotationAxis.None)
                {
                    // Use the default external rotation axis value if this is the first config
                    if (i == 0)
                    {
                        config.externalRotationAxis = DEFAULT_EXTERNAL_ROTATION_AXIS;
                    }
                    else
                    {
                        // Use the previous external rotation axis value otherwise
                        config.externalRotationAxis = inputSequence[i - 1].externalRotationAxis;
                    }
                }

                // Set the current config back to the input sequence with the filled in fields
                inputSequence[i] = config;

                // Print the current config to the console
                Debug.Log(
                    "Config "
                        + i
                        + ": speed = "
                        + config.speed
                        + ", clockwise = "
                        + config.clockwise
                        + ", duration = "
                        + config.duration
                        + ", external rotation axis = "
                        + config.externalRotationAxis
                );
            }
        }
        else
        {
            Debug.LogError("File not found at " + path);
        }
    }

    // A coroutine to rotate the drum based on an input sequence
    IEnumerator RotateDrum()
    {
        while (true) // loop indefinitely until stopped or paused
        {
            // Get the current config from the input sequence based on the index
            RotationConfig config = inputSequence[currentIndex];

            // Get the speed, direction and duration from the config
            float speed = config.speed;
            bool clockwise = config.clockwise;
            float duration = config.duration;

            // Get the external rotation axis from the config
            ExternalRotationAxis externalRotationAxis = config.externalRotationAxis;

            // Perform the external rotation based on the axis
            switch (externalRotationAxis)
            {
                case ExternalRotationAxis.Pitch:
                    // Rotate around the x axis by 90 degrees
                    transform.Rotate(90f, 0f, 0f);
                    break;
                case ExternalRotationAxis.Yaw:
                    // Rotate around the y axis by 90 degrees
                    transform.Rotate(0f, 90f, 0f);
                    break;
                case ExternalRotationAxis.Roll:
                    // Rotate around the z axis by 90 degrees
                    transform.Rotate(0f, 0f, 90f);
                    break;
                case ExternalRotationAxis.None:
                    // Do nothing
                    break;
            }

            // Calculate the angle of internal rotation
            float angle = speed * duration;

            // Adjust the sign of the angle based on the direction
            if (!clockwise)
            {
                angle = -angle;
            }

            // Rotate the drum by the angle around the y axis over time
            Quaternion startRotation = transform.rotation; // initial rotation
            Quaternion endRotation = Quaternion.Euler(0f, angle, 0f) * startRotation; // final rotation

            //print the end rotation
            Debug.Log("End Rotation: " + endRotation);

            float time = 0f; // elapsed time

            while (time < duration) // while not done rotating
            {
                // Interpolate between the start and end rotations based on time
                transform.rotation = Quaternion.Lerp(startRotation, endRotation, time / duration);

                // Increment the time by delta time
                time += Time.deltaTime;

                // Yield until next frame
                yield return null;
            }

            // Set the final rotation exactly
            transform.rotation = endRotation;

            // Wait for the duration before moving to the next rotation
            // yield return new WaitForSeconds(duration);

            // Increment the index by one and wrap around if needed
            currentIndex = (currentIndex + 1) % inputSequence.Length;
        }
    }

    // A function to jump to the previous rotation in the sequence
    void JumpToPrevRotation()
    {
        // Stop the current coroutine
        StopCoroutine(RotateDrum());

        // Decrement the index by one and wrap around if needed
        currentIndex = (currentIndex - 1 + inputSequence.Length) % inputSequence.Length;

        // Reset the drum rotation to identity
        transform.rotation = Quaternion.identity;

        // Start a new coroutine to perform the rotations from the new index
        StartCoroutine(RotateDrum());
    }

    // A function to jump to the next rotation in the sequence
    void JumpToNextRotation()
    {
        // Stop the current coroutine
        StopCoroutine(RotateDrum());

        // Increment the index by one and wrap around if needed
        currentIndex = (currentIndex + 1) % inputSequence.Length;

        // Reset the drum rotation to identity
        transform.rotation = Quaternion.identity;

        // Start a new coroutine to perform the rotations from the new index
        StartCoroutine(RotateDrum());
    }
}


// using UnityEngine;
// using System.Collections;
// using System.IO;

// // A script that rotates a drum based on an input sequence from a JSON file
// public class drumRotator : MonoBehaviour
// {
//     // Variables for the JSON file path and name
//     [SerializeField]
//     private string filePath = Application.dataPath + "/rotationConfigs.json";

//     [SerializeField]
//     private string fileName = "rotationConfigs.json";

//     // Variables for the input keys for jumping back or forward in the rotation sequence
//     [SerializeField]
//     private KeyCode prevKey = KeyCode.LeftArrow; // key to jump to the previous rotation

//     [SerializeField]
//     private KeyCode nextKey = KeyCode.RightArrow; // key to jump to the next rotation

//     // Variable for the current index of the rotation sequence
//     private int currentIndex = 0;

//     // Variable for the array of rotation configurations
//     private RotationConfig[] inputSequence;

//     // Define an enum for the axes of external rotation
//     public enum ExternalRotationAxis
//     {
//         None, // no external rotation
//         Pitch, // x axis
//         Yaw, // y axis
//         Roll // z axis
//     }

//     // Define a struct for rotation configuration
//     [System.Serializable]
//     public struct RotationConfig
//     {
//         // Fields for speed, direction and duration
//         public float speed;
//         public bool clockwise;
//         public float duration;

//         // Field for the external rotation axis
//         public ExternalRotationAxis externalRotationAxis;
//     }

//     // Define a class for rotation configuration collection
//     [System.Serializable]
//     public class RotationConfigCollection
//     {
//         // Field for the array of structs
//         public RotationConfig[] rotationConfigs;
//     }

//     void Start()
//     {
//         // Load the JSON file and deserialize it into an array of structs
//         LoadJSONFile(filePath);

//         // Start a coroutine to perform the rotations
//         StartCoroutine(RotateDrum());
//     }

//     void Update()
//     {
//         // Check if the user presses the prev or next key
//         if (Input.GetKeyDown(prevKey))
//         {
//             // Jump to the previous rotation in the sequence
//             JumpToPrevRotation();
//         }
//         else if (Input.GetKeyDown(nextKey))
//         {
//             // Jump to the next rotation in the sequence
//             JumpToNextRotation();
//         }
//     }

//     // A function to load the JSON file and deserialize it into an array of structs
//     void LoadJSONFile(string path)
//     {
//         // Check if the file exists at the path
//         if (File.Exists(path))
//         {
//             // Read the JSON file as a string
//             string json = File.ReadAllText(path);

//             // Deserialize the JSON string into an object of the wrapper class
//             RotationConfigCollection configCollection =
//                 JsonUtility.FromJson<RotationConfigCollection>(json);

//             // Get the array of structs from the object
//             inputSequence = configCollection.rotationConfigs;
//         }
//         else
//         {
//             Debug.LogError("File not found at " + path);
//         }
//     }

//     // A coroutine to rotate the drum based on an input sequence
//     IEnumerator RotateDrum()
//     {
//         while (true) // loop indefinitely until stopped or paused
//         {
//             // Get the current config from the input sequence based on the index
//             RotationConfig config = inputSequence[currentIndex];

//             // Get the speed, direction and duration from the config
//             float speed = config.speed;
//             bool clockwise = config.clockwise;
//             float duration = config.duration;

//             // Get the external rotation axis from the config
//             ExternalRotationAxis externalRotationAxis = config.externalRotationAxis;

//             // Perform the external rotation based on the axis
//             switch (externalRotationAxis)
//             {
//                 case ExternalRotationAxis.Pitch:
//                     // Rotate around the x axis by 90 degrees
//                     transform.Rotate(90f, 0f, 0f);
//                     break;
//                 case ExternalRotationAxis.Yaw:
//                     // Rotate around the y axis by 90 degrees
//                     transform.Rotate(0f, 90f, 0f);
//                     break;
//                 case ExternalRotationAxis.Roll:
//                     // Rotate around the z axis by 90 degrees
//                     transform.Rotate(0f, 0f, 90f);
//                     break;
//                 case ExternalRotationAxis.None:
//                     // Do nothing
//                     break;
//             }

//             // Calculate the angle of internal rotation
//             float angle = speed * duration;

//             // Adjust the sign of the angle based on the direction
//             if (!clockwise)
//             {
//                 angle = -angle;
//             }
//             // Rotate the drum by the angle around the y axis over time
//             Quaternion startRotation = transform.rotation; // initial rotation
//             Quaternion endRotation = Quaternion.Euler(0f, angle, 0f) * startRotation; // final rotation
//             float time = 0f; // elapsed time

//             while (time < duration) // while not done rotating
//             {
//                 // Interpolate between the start and end rotations based on time
//                 transform.rotation = Quaternion.Lerp(startRotation, endRotation, time / duration);

//                 // Increment the time by delta time
//                 time += Time.deltaTime;

//                 // Yield until next frame
//                 yield return null;
//             }

//             // Set the final rotation exactly
//             transform.rotation = endRotation;

//             // Wait for the duration before moving to the next rotation
//             yield return new WaitForSeconds(duration);

//             // Increment the index by one and wrap around if needed
//             currentIndex = (currentIndex + 1) % inputSequence.Length;
//         }
//     }

//     // A function to jump to the previous rotation in the sequence
//     void JumpToPrevRotation()
//     {
//         // Stop the current coroutine
//         StopCoroutine(RotateDrum());

//         // Decrement the index by one and wrap around if needed
//         currentIndex = (currentIndex - 1 + inputSequence.Length) % inputSequence.Length;

//         // Reset the drum rotation to identity
//         transform.rotation = Quaternion.identity;

//         // Start a new coroutine to perform the rotations from the new index
//         StartCoroutine(RotateDrum());
//     }

//     // A function to jump to the next rotation in the sequence
//     void JumpToNextRotation()
//     {
//         // Stop the current coroutine
//         StopCoroutine(RotateDrum());

//         // Increment the index by one and wrap around if needed
//         currentIndex = (currentIndex + 1) % inputSequence.Length;

//         // Reset the drum rotation to identity
//         transform.rotation = Quaternion.identity;

//         // Start a new coroutine to perform the rotations from the new index
//         StartCoroutine(RotateDrum());
//     }
// }
