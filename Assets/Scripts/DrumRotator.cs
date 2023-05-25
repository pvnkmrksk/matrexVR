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

public class DrumRotator : MonoBehaviour
{
    public GameObject drum;
    public Quaternion initialRotation;

    public string configFilePath = "rotationConfigs.json";
    public int currentIndex = 0; // Change access modifier to public
    public List<RotationConfig> configs; // Change access modifier to public

    private bool isPaused = true;
    private bool isStepping = false;

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

    void Start()
    {
        drum = this.gameObject;
        initialRotation = drum.transform.rotation;

        configs = LoadRotationConfigsFromJson(configFilePath);

        if (configs != null)
        {
            StartCoroutine(StartRotationWithDelay());
        }
        else
        {
            Debug.LogError("Failed to load rotation configs from " + configFilePath);
        }
    }

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
        Debug.Log(
            "Configuration changed: Frequency = "
                + configs[currentIndex].frequency
                + ", Level = "
                + configs[currentIndex].level
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

            // Assuming deltaTime represents the time elapsed since the last frame
            float speedPerSecond = config.speed; // Speed in degrees/second
            float speedPerFrame = speedPerSecond * Time.deltaTime; // Speed in degrees/frame

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
                    drum.transform.Rotate(axis, speedPerFrame);
                    totalRotation += Mathf.Abs(speedPerFrame);
                }
                yield return null;

                if (isStepping || totalRotation >= (config.speed * config.duration))
                {
                    isRotationFinished = true;
                }
            }
            Debug.Log("Finished rotation " + currentIndex + " of " + configs.Count);
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
    }
}
