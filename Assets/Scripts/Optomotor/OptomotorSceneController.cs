using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using System;

public class OptomotorSceneController : MonoBehaviour, ISceneController
{
    // Coordinates optomotor trial execution:
    // loads config, applies each stimulus to drum/grating/closed-loop,
    // and keeps a per-stimulus logging snapshot for data loggers.
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
        // FPS and VSync settings removed - now handled centrally by MainController
        
        CreateDrumObject();
        FindClosedLoopComponents();
        DisableAutopilot();
    }

    private void DisableAutopilot()
    {
        // Disable autopilot mode for all Keyboard components in the scene
        // This is important for optomotor scenes where autopilot should be off
        Keyboard[] keyboardComponents = FindObjectsOfType<Keyboard>();
        foreach (Keyboard kb in keyboardComponents)
        {
            kb.SetAutopilotMode(false);
        }
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
        // Sequence entrypoint from MainController.
        if (parameters == null)
        {
            Debug.LogError("Parameters are null in InitializeScene");
            return;
        }

        if (parameters.ContainsKey("configFile"))
        {
            string configFileName = parameters["configFile"].ToString();
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
        if (File.Exists(configPath))
        {
            try
            {
                string jsonText = File.ReadAllText(configPath);
                optomotorConfig = JsonConvert.DeserializeObject<OptomotorConfig>(jsonText);
                loggingData["OptomotorConfigFile"] = configFileName;

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

            yield return new WaitForSeconds(duration);

            currentStimulusIndex = (currentStimulusIndex + 1) % optomotorConfig.stimuli.Count;

            if (currentStimulusIndex == 0 && !optomotorConfig.loop)
                isRunning = false;
        }
    }

    private void ApplyStimulusConfig(int stimulusIndex)
    {
        // Applies one indexed stimulus consistently across all subsystems.
        if (optomotorConfig == null || stimulusIndex >= optomotorConfig.stimuli.Count)
            return;

        OptomotorStimulus stimulus = optomotorConfig.stimuli[stimulusIndex];

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

        // Add yaw mode data from the first ClosedLoop component (if any)
        if (closedLoopComponents.Count > 0)
        {
            ClosedLoop firstClosedLoop = closedLoopComponents[0];
            loggingData["UseYawMode"] = firstClosedLoop.GetUseYawMode();
            loggingData["YawGain"] = firstClosedLoop.GetYawGain();
            loggingData["YawDCOffset"] = firstClosedLoop.GetYawDCOffset();
            loggingData["YawInput"] = firstClosedLoop.GetLastYawInput();
            loggingData["YawOutput"] = firstClosedLoop.GetLastYawOutput();
            loggingData["SphereDiameter"] = firstClosedLoop.GetSphereDiameter();
        }
        else
        {
            // Provide default values if no ClosedLoop components
            loggingData["UseYawMode"] = true;
            loggingData["YawGain"] = 0.0f;
            loggingData["YawDCOffset"] = 0.0f;
            loggingData["YawInput"] = 0.0f;
            loggingData["YawOutput"] = 0.0f;
            loggingData["SphereDiameter"] = 1.0f;
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
        // Update yaw mode data every time this is called (every frame for logging)
        UpdateYawModeData();
        return loggingData;
    }

    private void UpdateYawModeData()
    {
        // Update yaw mode data from the first ClosedLoop component (if any)
        if (closedLoopComponents.Count > 0)
        {
            ClosedLoop firstClosedLoop = closedLoopComponents[0];
            loggingData["UseYawMode"] = firstClosedLoop.GetUseYawMode();
            loggingData["YawGain"] = firstClosedLoop.GetYawGain();
            loggingData["YawDCOffset"] = firstClosedLoop.GetYawDCOffset();
            loggingData["YawInput"] = firstClosedLoop.GetLastYawInput();
            loggingData["YawOutput"] = firstClosedLoop.GetLastYawOutput();
            loggingData["SphereDiameter"] = firstClosedLoop.GetSphereDiameter();
        }
        else
        {
            // Provide default values if no ClosedLoop components
            loggingData["UseYawMode"] = true;
            loggingData["YawGain"] = 0.0f;
            loggingData["YawDCOffset"] = 0.0f;
            loggingData["YawInput"] = 0.0f;
            loggingData["YawOutput"] = 0.0f;
            loggingData["SphereDiameter"] = 1.0f;
        }
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