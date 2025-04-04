using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using System;

public class OptomotorSceneController : MonoBehaviour, ISceneController
{
    [SerializeField] private GameObject drumPrefab;

    private GameObject drumObject;
    private DrumRotator drumRotator;
    private SinusoidalGrating sinusoidalGrating;
    private List<ClosedLoop> closedLoopComponents = new List<ClosedLoop>();
    private OptomotorConfig optomotorConfig;
    private int currentStimulusIndex = 0;
    private bool isRunning = false;
    private Dictionary<string, object> loggingData = new Dictionary<string, object>();

    void Awake()
    {
        CreateDrumObject();
        FindClosedLoopComponents();
    }

    private void CreateDrumObject()
    {
        if (drumPrefab == null)
        {
            Debug.LogError("Drum prefab not assigned");
            return;
        }

        drumObject = Instantiate(drumPrefab);
        drumRotator = drumObject.GetComponent<DrumRotator>();
        sinusoidalGrating = drumObject.GetComponent<SinusoidalGrating>();
        drumObject.AddComponent<OptomotorDataLogger>();
    }

    private void FindClosedLoopComponents()
    {
        closedLoopComponents.Clear();
        ClosedLoop[] foundComponents = FindObjectsOfType<ClosedLoop>();
        if (foundComponents != null && foundComponents.Length > 0)
        {
            closedLoopComponents.AddRange(foundComponents);
        }
    }

    public void InitializeScene(Dictionary<string, object> parameters)
    {
        Debug.Log($"OptomotorSceneController.InitializeScene() called with {parameters?.Count ?? 0} parameters");

        if (parameters == null)
        {
            Debug.LogError("Parameters are null in InitializeScene");
            return;
        }

        if (parameters.ContainsKey("configFile"))
        {
            string configFileName = parameters["configFile"].ToString();
            Debug.Log($"Loading optomotor config file: {configFileName}");
            LoadOptomotorConfig(configFileName);
            StartCoroutine(RunStimulusSequence());
        }
        else
        {
            Debug.LogError("No configFile parameter provided in sequence.");
        }
    }

    private void LoadOptomotorConfig(string configFileName)
    {
        string configPath = Path.Combine(Application.streamingAssetsPath, configFileName);
        Debug.Log($"Looking for config file at: {configPath}");

        if (File.Exists(configPath))
        {
            try
            {
                string jsonText = File.ReadAllText(configPath);
                Debug.Log($"Read config file: {jsonText.Substring(0, Math.Min(100, jsonText.Length))}...");

                optomotorConfig = JsonConvert.DeserializeObject<OptomotorConfig>(jsonText);

                // Store config filename for logging
                loggingData["OptomotorConfigFile"] = configFileName;

                Debug.Log($"Loaded optomotor config with {optomotorConfig.stimuli.Count} stimuli");

                // Config copying is already handled by MainController
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading optomotor config: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"Optomotor config file not found: {configPath}");
        }
    }

    private IEnumerator RunStimulusSequence()
    {
        Debug.Log("Starting stimulus sequence");

        if (optomotorConfig == null || optomotorConfig.stimuli.Count == 0)
        {
            Debug.LogError("No stimuli configured");
            yield break;
        }

        isRunning = true;
        currentStimulusIndex = 0;

        while (isRunning)
        {
            // Apply current stimulus configuration
            ApplyStimulusConfig(currentStimulusIndex);

            // Wait for the stimulus duration
            OptomotorStimulus currentStimulus = optomotorConfig.stimuli[currentStimulusIndex];
            float duration = currentStimulus.duration;

            Debug.Log($"Running stimulus {currentStimulusIndex} for {duration} seconds - Speed: {currentStimulus.speed}, Freq: {currentStimulus.frequency}");

            yield return new WaitForSeconds(duration);

            // Move to next stimulus
            currentStimulusIndex = (currentStimulusIndex + 1) % optomotorConfig.stimuli.Count;
            Debug.Log($"Moving to next stimulus: {currentStimulusIndex}");

            // If we've gone through all stimuli and not set to loop, stop
            if (currentStimulusIndex == 0 && !optomotorConfig.loop)
            {
                isRunning = false;
                Debug.Log("Finished all stimuli, not looping");
            }
        }
    }

    private void ApplyStimulusConfig(int stimulusIndex)
    {
        if (optomotorConfig == null || stimulusIndex >= optomotorConfig.stimuli.Count)
            return;

        OptomotorStimulus stimulus = optomotorConfig.stimuli[stimulusIndex];
        Debug.Log($"Applying stimulus {stimulusIndex}: Speed={stimulus.speed}, Clockwise={stimulus.clockwise}, Axis={stimulus.rotationAxis}");

        // Update the DrumRotator
        if (drumRotator != null)
        {
            drumRotator.SetRotationParameters(
                stimulus.speed,
                stimulus.clockwise,
                stimulus.rotationAxis
            );
        }

        // Update the SinusoidalGrating
        if (sinusoidalGrating != null)
        {
            Color color1 = HexToColor(stimulus.color1);
            Color color2 = HexToColor(stimulus.color2);

            sinusoidalGrating.SetGratingParameters(
                stimulus.frequency,
                stimulus.contrast,
                stimulus.dutyCycle,
                color1,
                color2
            );
        }

        // Apply closed loop settings to all ClosedLoop components
        foreach (ClosedLoop cl in closedLoopComponents)
        {
            Debug.Log($"Setting closed loop values for {cl.gameObject.name}: Orientation={stimulus.closedLoopOrientation}, Position={stimulus.closedLoopPosition}");
            cl.SetClosedLoopOrientation(stimulus.closedLoopOrientation);
            cl.SetClosedLoopPosition(stimulus.closedLoopPosition);
        }

        // Update logging data
        loggingData.Clear();
        loggingData["StimulusIndex"] = stimulusIndex;
        loggingData["Frequency"] = stimulus.frequency;
        loggingData["Contrast"] = stimulus.contrast;
        loggingData["DutyCycle"] = stimulus.dutyCycle;
        loggingData["Speed"] = stimulus.speed;
        loggingData["RotationAxis"] = stimulus.rotationAxis;
        loggingData["ClockwiseRotation"] = stimulus.clockwise;
        loggingData["ClosedLoopOrientation"] = stimulus.closedLoopOrientation;
        loggingData["ClosedLoopPosition"] = stimulus.closedLoopPosition;

        Debug.Log($"Updated logging data with {loggingData.Count} entries");
        foreach (var kvp in loggingData)
        {
            Debug.Log($"Logging data: {kvp.Key} = {kvp.Value}");
        }
    }

    private Color HexToColor(string hex)
    {
        // Remove # if present
        if (hex.StartsWith("#"))
            hex = hex.Substring(1);

        // Parse the hex string
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color color))
            return color;

        // Return default color if parsing fails
        Debug.LogWarning($"Failed to parse color hex: {hex}, using default instead");
        return hex.ToLower() == "ffffff" ? Color.white : Color.black;
    }

    // Provide data to loggers
    public Dictionary<string, object> GetLoggingData()
    {
        return loggingData;
    }

    public Transform GetDrumTransform()
    {
        return drumObject?.transform;
    }

    void OnDestroy()
    {
        isRunning = false;
    }
}

[System.Serializable]
public class OptomotorConfig
{
    public bool loop = false;
    public List<OptomotorStimulus> stimuli = new List<OptomotorStimulus>();
}

[System.Serializable]
public class OptomotorStimulus
{
    [Tooltip("Duration of the stimulus in seconds")]
    public float duration = 10.0f;

    [Tooltip("Rotation speed in degrees per second")]
    public float speed = 20.0f;

    [Tooltip("Whether the rotation is clockwise")]
    public bool clockwise = true;

    [Tooltip("Axis of rotation (Yaw, Pitch, or Roll)")]
    public string rotationAxis = "Yaw";

    [Tooltip("Spatial frequency of the grating in cycles per revolution")]
    public float frequency = 4.0f;

    [Tooltip("Contrast of the grating (0 = no contrast, 1 = maximum contrast)")]
    public float contrast = 0.5f;

    [Tooltip("Duty cycle of the grating (0 = all dark, 1 = all light, 0.5 = equal dark/light)")]
    public float dutyCycle = 0.5f;

    [Tooltip("First color of the grating in hex format (e.g. #000000)")]
    public string color1 = "#000000";

    [Tooltip("Second color of the grating in hex format (e.g. #FFFFFF)")]
    public string color2 = "#FFFFFF";

    [Tooltip("Whether to use closed-loop orientation control")]
    public bool closedLoopOrientation = false;

    [Tooltip("Whether to use closed-loop position control")]
    public bool closedLoopPosition = false;
}