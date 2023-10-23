using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public enum RotationAxis
{
    Yaw,
    Pitch,
    Roll
}

[System.Serializable]
public class RotationConfig
{
    public float speed;
    public bool clockwise;
    public float duration;
    public string externalRotationAxis;
    public float frequency; // Remove the get and set methods
    public float level; // Remove the get and set methods


}

[System.Serializable]
public class RotationConfigList
{
    public List<RotationConfig> rotationConfigs;
}

//todo: rotation change is glitchy and reverses at times
public class DrumRotator : MonoBehaviour
{
    public GameObject drum;
    public Quaternion initialRotation;


    string configFilePath = Path.Combine(Application.streamingAssetsPath, "rotationConfig.json");
    // string jsonString = File.ReadAllText(jsonPath);
    public int currentIndex = 0; // Change access modifier to public
    public List<RotationConfig> configs; // Change access modifier to public

    public bool isPaused = true;
    public bool isStepping = false;

    private Vector3 StringToAxis(string axisName)
    {
        switch (axisName)
        {
            case "Pitch":
                return Vector3.right;
            case "Yaw":
                return Vector3.up;
            case "Roll":
                return Vector3.forward;
            default:
                return Vector3.zero;
        }
    }

    public int initialDelayFrames = 10; // Number of frames to delay before starting the rotation

    //get the Cameras gameobject which holds all the cameras
    GameObject camerasObject;

    void Start()
    {
        GameObject camerasObject = GameObject.Find("Cameras");

        drum = this.gameObject;
        initialRotation = drum.transform.rotation;

        configs = LoadRotationConfigsFromJson(configFilePath);

        if (configs != null)
        {
            StartCoroutine(StartRotationWithDelay());
        }
        else
        {
            Logger.Log("Failed to load rotation configs from " + configFilePath, 1);
        }
    }

    //todo: move to sscene controller json handling system
    private IEnumerator StartRotationWithDelay()
    {
        // Delay for the specified number of frames
        for (int i = 0; i < initialDelayFrames; i++)
        {
            yield return null;
        }

        // Start the rotation
        StartCoroutine(RotateDrum());
    }

    // Define a custom event for configuration change
    public event Action ConfigurationChanged;

    // Method to raise the configuration change event
    private void RaiseConfigurationChanged()
    {
        ConfigurationChanged?.Invoke();
        Logger.Log(
            "Configuration changed: Frequency = "
                + configs[currentIndex].frequency
                + ", Level = "
                + configs[currentIndex].level
                + ", Speed = "
                + configs[currentIndex].speed
        );
    }

    private List<RotationConfig> LoadRotationConfigsFromJson(string path)
    {
        if (File.Exists(path))
        {
            string jsonText = File.ReadAllText(path);
            var configList = JsonUtility.FromJson<RotationConfigList>(jsonText);

            if (configList != null)
            {
                return configList.rotationConfigs;
            }
        }

        return null;
    }

    private IEnumerator RotateDrum()
    {
        if (configs == null)
        {
            throw new Exception("Rotation configs list is null.");
        }

        while (true)
        {
            RotationConfig config = configs[currentIndex];

            if (config == null)
            {
                throw new Exception("Rotation config is null.");
            }

            // Reset the rotation to the initial rotation
            drum.transform.rotation = initialRotation;

            Vector3 axis = StringToAxis(config.externalRotationAxis);
            // Logger.Log("Axis: " + axis);
            // Assuming deltaTime represents the time elapsed since the last frame
            float speedPerSecond = config.speed; // Speed in degrees/second
            // float speedPerFrame = speedPerSecond * Time.deltaTime; // Speed in degrees/frame
            // Assuming deltaTime represents the time elapsed since the last frame
            float speedPerFrame = speedPerSecond * (1f / 60f); // Speed in degrees/frame at 60 fps

            // Adjust speed for clockwise/counter-clockwise
            if (!config.clockwise)
            {
                speedPerFrame *= -1;
            }

            // Calculate total rotation
            float totalRotation = 0;
            bool isRotationFinished = false;
            while (!isRotationFinished)
            {
                if (!isPaused && !isStepping)
                {
                    //rotate the drum along the axis specified by the config
                    drum.transform.Rotate(axis, speedPerFrame);
                    totalRotation += Mathf.Abs(speedPerFrame);
                }
                yield return null;

                if (isStepping || totalRotation >= (config.speed * config.duration))
                {
                    isRotationFinished = true;
                }
            }
            Logger.Log("Finished rotation " + currentIndex + " of " + configs.Count);
            RaiseConfigurationChanged(); // Raise the event when the configuration changes
            // Increment index or reset to 0 if end of list
            if (!isStepping)
            {
                currentIndex = (currentIndex + 1) % configs.Count;
                isStepping = false;
            }
            else
            {
                isStepping = false;
            }
        }
    }

    void Update()
    {
        // Step forward or backward through the index using square brackets
        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            currentIndex = (currentIndex + 1) % configs.Count;
            isStepping = true;
            RaiseConfigurationChanged(); // Raise the event when the configuration changes
        }
        else if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            currentIndex = (currentIndex - 1 + configs.Count) % configs.Count;
            isStepping = true;
            RaiseConfigurationChanged(); // Raise the event when the configuration changes
        }

        // Pause and play using backslash
        if (Input.GetKeyDown(KeyCode.Backslash))
        {
            isPaused = !isPaused;
        }

        // Use R to reset the drum to the initial rotation
        if (Input.GetKeyDown(KeyCode.R))
        {
            Logger.Log("Resetting drum rotation");
            drum.transform.rotation = initialRotation;
        }
    }
}
