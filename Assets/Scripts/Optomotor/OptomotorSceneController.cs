using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using System;

public class OptomotorSceneController : MonoBehaviour, ISceneController
{
    [SerializeField] private GameObject drumPrefab;
    [SerializeField] private string drumObjectName = "OptomotorDrum";
    [SerializeField] private float drumRadius = 1.0f; // Default drum radius if creating from scratch

    private GameObject drumObject;
    private DrumRotator drumRotator;
    private SinusoidalGrating sinusoidalGrating;
    private DataLogger dataLogger;

    // Reference to camera objects that handle response/feedback
    private List<ClosedLoop> closedLoopComponents = new List<ClosedLoop>();

    private OptomotorConfig optomotorConfig;
    private int currentStimulusIndex = 0;
    private bool isRunning = false;

    // Dictionary to store the timestamp for logging
    private Dictionary<string, object> loggingData = new Dictionary<string, object>();

    void Awake()
    {
        Debug.Log($"OptomotorSceneController.Awake() - {gameObject.name}");

        // Create the drum object - per user comment, we don't search but create it
        CreateDrumObject();

        // Find all ClosedLoop components in the scene (these are on camera prefabs)
        FindClosedLoopComponents();
    }

    private void CreateDrumObject()
    {
        // Create drum from prefab if available
        if (drumPrefab != null)
        {
            drumObject = Instantiate(drumPrefab);
            drumObject.name = drumObjectName;
            Debug.Log($"Created drum object from prefab: {drumObjectName}");
        }
        else
        {
            // Create a basic drum object from scratch
            drumObject = new GameObject(drumObjectName);
            Debug.Log($"Created basic drum object: {drumObjectName}");
        }

        if (drumObject != null)
        {
            // Set drum position to center of scene
            drumObject.transform.position = Vector3.zero;

            // Get or add required components for stimulus generation
            drumRotator = drumObject.GetComponent<DrumRotator>();
            if (drumRotator == null)
            {
                drumRotator = drumObject.AddComponent<DrumRotator>();
                Debug.Log("Added DrumRotator component");
            }

            sinusoidalGrating = drumObject.GetComponent<SinusoidalGrating>();
            if (sinusoidalGrating == null)
            {
                sinusoidalGrating = drumObject.AddComponent<SinusoidalGrating>();
                Debug.Log("Added SinusoidalGrating component");
            }

            // Add data logger if needed
            dataLogger = drumObject.GetComponent<DataLogger>();
            if (dataLogger == null)
            {
                dataLogger = drumObject.AddComponent<OptomotorDataLogger>();
                Debug.Log("Added OptomotorDataLogger component");
            }
        }
        else
        {
            Debug.LogError("Failed to create drum object.");
        }
    }

    private void FindClosedLoopComponents()
    {
        // Clear previous references
        closedLoopComponents.Clear();

        // Find all ClosedLoop components in the scene
        ClosedLoop[] foundComponents = FindObjectsOfType<ClosedLoop>();
        if (foundComponents != null && foundComponents.Length > 0)
        {
            closedLoopComponents.AddRange(foundComponents);
            Debug.Log($"Found {closedLoopComponents.Count} ClosedLoop components in the scene");
        }
        else
        {
            Debug.LogWarning("No ClosedLoop components found in the scene. Closed-loop functionality will be disabled.");
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

                // Copy config file to log directory for reference
                CopyConfigToLogDirectory(configPath, configFileName);
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

    private void CopyConfigToLogDirectory(string sourcePath, string configFileName)
    {
        MasterDataLogger masterLogger = MasterDataLogger.Instance;
        if (masterLogger != null)
        {
            string timestamp = masterLogger.timestamp;
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string destPath = Path.Combine(
                masterLogger.directoryPath,
                $"{timestamp}_{sceneName}_{configFileName}"
            );

            try
            {
                File.Copy(sourcePath, destPath);
                Debug.Log($"Copied optomotor config to: {destPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to copy config file: {e.Message}");
            }
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

        // Update the DrumRotator for visual stimulus
        if (drumRotator != null)
        {
            drumRotator.SetRotationParameters(
                stimulus.speed,
                stimulus.clockwise,
                stimulus.rotationAxis
            );
            Debug.Log($"Applied rotation parameters to drum");
        }
        else
        {
            Debug.LogError("drumRotator is null when trying to apply configuration");
        }

        // Update the SinusoidalGrating for visual appearance
        if (sinusoidalGrating != null)
        {
            // Convert hex string to Color
            Color color1 = HexToColor(stimulus.color1);
            Color color2 = HexToColor(stimulus.color2);

            sinusoidalGrating.SetGratingParameters(
                stimulus.frequency,
                stimulus.contrast,
                color1,
                color2
            );
            Debug.Log($"Applied grating parameters: Freq={stimulus.frequency}, Contrast={stimulus.contrast}");
        }
        else
        {
            Debug.LogError("sinusoidalGrating is null when trying to apply configuration");
        }

        // Update all ClosedLoop components in the scene (camera prefabs)
        foreach (ClosedLoop closedLoop in closedLoopComponents)
        {
            if (closedLoop != null)
            {
                closedLoop.SetClosedLoopOrientation(stimulus.closedLoopOrientation);
                closedLoop.SetClosedLoopPosition(stimulus.closedLoopPosition);
                Debug.Log($"Updated ClosedLoop component on {closedLoop.gameObject.name}");
            }
        }

        // Update logging data
        loggingData["CurrentStimulusIndex"] = stimulusIndex;
        loggingData["Frequency"] = stimulus.frequency;
        loggingData["Contrast"] = stimulus.contrast;
        loggingData["Speed"] = stimulus.speed;
        loggingData["RotationAxis"] = stimulus.rotationAxis;
        loggingData["ClockwiseRotation"] = stimulus.clockwise;
        loggingData["ClosedLoopOrientation"] = stimulus.closedLoopOrientation;
        loggingData["ClosedLoopPosition"] = stimulus.closedLoopPosition;
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
    // Rotation parameters
    public float duration = 10.0f;
    public float speed = 20.0f;
    public bool clockwise = true;
    public string rotationAxis = "Yaw";

    // Grating parameters
    public float frequency = 4.0f;
    public float contrast = 0.5f;
    public string color1 = "#000000";
    public string color2 = "#FFFFFF";

    // Closed loop parameters
    public bool closedLoopOrientation = false;
    public bool closedLoopPosition = false;
}