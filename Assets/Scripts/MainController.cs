using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

public interface ISceneController
{
    void InitializeScene(Dictionary<string, object> parameters);
}

public class MainController : MonoBehaviour
{
    public List<SequenceStep> sequenceSteps = new List<SequenceStep>();
    public List<int> executionOrder = new List<int>();
    public int currentStep = 0;
    public int currentTrial = 0;
    private float timer;
    private bool sequenceStarted = false;
    private MasterDataLogger masterDataLogger;
    public bool loopSequence = false;
    private bool randomise = false; // Added field

    // System Config properties
    [SerializeField]
    private string systemConfigFileName = "system_config.json";

    // Dictionary to store loaded system configs
    private Dictionary<string, SystemConfig> systemConfigs = new Dictionary<string, SystemConfig>();

    [Tooltip("0: Off, ,1: Error, 2: Warning, 3: Info, 4: Debug")]
    [SerializeField]
    [Range(0, 4)]
    private int logLevel = 0; // 0: All, 1: Error, 2: Warning, 3: Info, 4: Debug

    // Add this flag to control single window mode
    [SerializeField]
    private bool preventMultipleWindows = true;

    // Add global display target property
    private int globalTargetDisplay = 1; // Default value

    // Centralized FPS/VSync settings
    [Header("Frame Rate Settings")]
    [SerializeField][Tooltip("Target frame rate (60 recommended for VSync, -1 for unlimited)")] 
    private int targetFrameRate = 60;
    [SerializeField][Tooltip("VSync count (0=off, 1=60fps, 2=30fps)")] 
    private int vSyncCount = 1;

    // Persistent black background camera
    private Camera backgroundCamera;

    // Status UI component
    private StatusUI statusUI;

    // VR DC Offset Management
    private Dictionary<string, ClosedLoop> vrClosedLoops = new Dictionary<string, ClosedLoop>();
    private Dictionary<string, float> persistentDCOffsets = new Dictionary<string, float>(); // Persist DC offsets across trials/scenes
    private int selectedVRIndex = 1; // 1-4, default to VR1
    private float dcOffsetStep = 0.005f; // Step size for DC offset adjustments (in radians, ~0.1 degrees)

    // In MainController class
    public SequenceStep GetCurrentSequenceStep()
    {
        if (currentStep < executionOrder.Count)
        {
            int stepIndex = executionOrder[currentStep];
            return sequenceSteps[stepIndex];
        }
        return null;
    }

    void Awake()
    {
        // Set the log level first
        Debugger.CurrentLogLevel = logLevel;
        Debugger.Log("MainController.Awake()", 3);

        // Make sure the MainController persists across scene changes
        DontDestroyOnLoad(this.gameObject);

        // Access the MasterDataLogger instance
        masterDataLogger = MasterDataLogger.Instance;

        if (masterDataLogger == null)
        {
            Debugger.Log("MasterDataLogger instance not found", 1);
        }
        else
        {
            Debugger.Log("MasterDataLogger instance found.", 3);
            Debugger.Log("MasterDataLogger.directoryPath: " + masterDataLogger.directoryPath, 4);
        }

        // Load system configurations first
        LoadSystemConfigurations();

        // Setup display handling if enabled
        if (preventMultipleWindows)
        {
            HandleDisplaySetup();
        }

        // Set FPS and VSync - locked to 60fps
        // IMPORTANT: VSync=1 locks to monitor refresh rate (60Hz=60fps, 120Hz=120fps)
        // To force 60fps regardless of monitor, use VSync=0 and targetFrameRate=60
        // For builds, we enforce both to ensure 60fps even on 120Hz monitors
        QualitySettings.vSyncCount = 0; // Disable VSync to force targetFrameRate
        Application.targetFrameRate = targetFrameRate;
        Debugger.Log($"FPS locked to: {Application.targetFrameRate}, VSync: {QualitySettings.vSyncCount} (forced via targetFrameRate)", 3);

        // Create persistent black background camera
        CreatePersistentBackgroundCamera();

        // Create status UI
        CreateStatusUI();
    }

    // Handle display setup - simplified to use a single display for all VR setups
    private void HandleDisplaySetup()
    {
        // Check if a display argument was provided via command line
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].ToLower() == "-display" && int.TryParse(args[i + 1], out int display))
            {
                globalTargetDisplay = display;
                Debugger.Log($"Using command line specified display: {globalTargetDisplay}", 3);
                break;
            }
        }

        // Activate the target display if it exists
        if (globalTargetDisplay > 0 && Display.displays.Length > globalTargetDisplay)
        {
            Display.displays[globalTargetDisplay].Activate();
            Debugger.Log($"Activated display {globalTargetDisplay}", 3);

            // Apply this display to all cameras in the scene
            Camera[] allCameras = FindObjectsOfType<Camera>();
            foreach (Camera cam in allCameras)
            {
                cam.targetDisplay = globalTargetDisplay;
            }
            
            // Update background camera display as well
            if (backgroundCamera != null)
            {
                backgroundCamera.targetDisplay = globalTargetDisplay;
            }
        }
    }

    void Start()
    {
        // Log that we're starting
        Debugger.Log("MainController.Start()", 3);

        // Load the sequence configuration
        LoadSequenceConfiguration();
    }

    // Load system configurations from the specified file
    private void LoadSystemConfigurations()
    {
        // Check if systemConfigFileName is set
        if (string.IsNullOrEmpty(systemConfigFileName))
        {
            Debugger.Log("System config file name is not set, skipping load", 2);
            return;
        }
        
        // Check if StreamingAssets path is available
        if (string.IsNullOrEmpty(Application.streamingAssetsPath))
        {
            Debugger.Log("StreamingAssets path is not available, skipping system config load", 2);
            return;
        }
        
        string configPath = Path.Combine(Application.streamingAssetsPath, systemConfigFileName);

        if (!File.Exists(configPath))
        {
            Debugger.Log($"System config file not found: {configPath}", 1);
            return;
        }

        try
        {
            string jsonText = File.ReadAllText(configPath);

            // Parse the JSON using JObject instead of dynamic
            JObject fullConfig = JObject.Parse(jsonText);

            // Extract global target display if it exists
            if (fullConfig["targetDisplay"] != null)
            {
                globalTargetDisplay = fullConfig["targetDisplay"].Value<int>();
                Debugger.Log($"Found global targetDisplay: {globalTargetDisplay}", 3);
            }

            // Parse the configs array
            JArray configsArray = (JArray)fullConfig["configs"];
            if (configsArray != null)
            {
                SystemConfig[] loadedConfigs = configsArray.ToObject<SystemConfig[]>();

                // Clear existing configs
                systemConfigs.Clear();

                // Add each config to dictionary with VR ID as key
                foreach (SystemConfig config in loadedConfigs)
                {
                    // Validate and handle closedLoopMode
                    ValidateClosedLoopMode(config);
                    
                    // Set target display from global setting
                    config.targetDisplay = globalTargetDisplay;
                    systemConfigs[config.vrId] = config;
                    Debugger.Log($"Loaded system config for: {config.vrId} with targetDisplay: {config.targetDisplay}, closedLoopMode: {config.closedLoopMode}", 3);
                }

                Debugger.Log($"Successfully loaded system config file: {systemConfigFileName}", 3);
            }
            else
            {
                Debugger.Log("No configs array found in system config file", 1);
            }

            // Copy the system config file to the log directory
            if (masterDataLogger != null)
            {
                string timestamp = masterDataLogger.timestamp;
                string sceneName = SceneManager.GetActiveScene().name;
                string destPath = Path.Combine(
                    masterDataLogger.directoryPath,
                    $"{timestamp}_{sceneName}_{systemConfigFileName}"
                );
                File.Copy(configPath, destPath);
                Debugger.Log($"Copied system config file to: {destPath}", 3);
            }
        }
        catch (System.Exception e)
        {
            Debugger.Log($"Error loading system config file: {e.Message}", 1);
        }
    }

    // Get system config based on GameObject name
    public SystemConfig GetSystemConfigForGameObject(GameObject gameObject)
    {
        // Check if the GameObject name contains any of our known VR IDs
        foreach (var kvp in systemConfigs)
        {
            if (gameObject.name.Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }

        // If no match, try to get config for "VR1" as default
        if (systemConfigs.ContainsKey("VR1"))
        {
            Debugger.Log($"No matching config for {gameObject.name}, using VR1 config", 2);
            return systemConfigs["VR1"];
        }

        Debugger.Log($"No config found for {gameObject.name}", 1);
        return new SystemConfig { vrId = "VR1" };
    }

    // Get system config for a specific VR ID
    public SystemConfig GetSystemConfig(string vrId)
    {
        if (systemConfigs.ContainsKey(vrId))
        {
            return systemConfigs[vrId];
        }

        Debugger.Log($"System config for {vrId} not found, returning default", 2);
        return new SystemConfig { vrId = vrId };
    }

    // Method to set a different system config file
    public void SetSystemConfigFile(string fileName)
    {
        systemConfigFileName = fileName;
        LoadSystemConfigurations();
        Debugger.Log($"Loaded new system config file: {fileName}", 3);
    }

    public void StopSequence()
    {
        sequenceStarted = false;
        currentStep = 0;
    }

    public void StartSequence()
    {
        Debugger.Log("MainController.StartSequence()", 3);
        sequenceStarted = true;

        // Initialize execution order
        if (randomise)
        {
            InitializeExecutionOrder();
        }
        else
        {
            // Sequential order
            executionOrder.Clear();
            for (int i = 0; i < sequenceSteps.Count; i++)
            {
                executionOrder.Add(i);
            }
        }

        currentStep = 0;
        timer = sequenceSteps[executionOrder[currentStep]].duration; // Initialize timer for the first scene
        LoadScene(sequenceSteps[executionOrder[currentStep]]);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void InitializeExecutionOrder()
    {
        executionOrder.Clear();
        for (int i = 0; i < sequenceSteps.Count; i++)
        {
            executionOrder.Add(i);
        }
        // Shuffle executionOrder
        ShuffleList(executionOrder);
    }

    void ShuffleList<T>(IList<T> list)
    {
        // Implement a simple Fisher-Yates shuffle
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            // Swap list[k] with list[n]
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debugger.Log("MainController.OnSceneLoaded()", 3);

        // Re-apply FPS/VSync settings to ensure they remain locked (like a clock)
        // Critical for builds - ensure frame rate is locked every scene load
        // Use VSync=0 to force targetFrameRate (VSync=1 locks to monitor refresh rate)
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;
        
        // Force apply again after a frame to ensure it sticks in builds
        StartCoroutine(ReapplyFrameRateSettings());

        // Clear screen to black immediately after loading
        ClearScreenToBlack();

        SequenceStep currentStepData = sequenceSteps[executionOrder[currentStep]];

        // Refresh VR ClosedLoop registrations when scene loads
        // This ensures all VR components in the new scene are registered
        RefreshVRRegistrations();

        // Note: Components will load their own configs based on vrId
        // No need to scan for them here

        ISceneController currentSceneController = null;
        foreach (var obj in FindObjectsOfType<MonoBehaviour>()) // MonoBehaviour is the base class for all Unity Behaviours
        {
            if (obj is ISceneController)
            {
                currentSceneController = (ISceneController)obj;
                break;
            }
        }

        if (currentSceneController != null && currentStepData.parameters != null)
        {
            // Add gain to parameters if not already present
            if (!currentStepData.parameters.ContainsKey("gain"))
            {
                currentStepData.parameters["gain"] = currentStepData.gain;
            }
            
            currentSceneController.InitializeScene(currentStepData.parameters);
            timer = currentStepData.duration;
        }
        else
        {
            Debugger.Log("Either the scene controller or the parameters are null.", 2);
        }
        
        // Apply gain to all ClosedLoop components in the scene
        ApplyGainToClosedLoopComponents(currentStepData.gain);
    }

    private IEnumerator ReapplyFrameRateSettings()
    {
        // Wait one frame then re-apply to ensure settings stick in builds
        yield return null;
        QualitySettings.vSyncCount = 0; // Disable VSync to force targetFrameRate
        Application.targetFrameRate = targetFrameRate;
        Debugger.Log($"Re-applied FPS settings: {Application.targetFrameRate} fps, VSync: {QualitySettings.vSyncCount} (forced via targetFrameRate)", 3);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        // Clean up background camera
        if (backgroundCamera != null)
        {
            DestroyImmediate(backgroundCamera.gameObject);
        }

        // Clean up status UI
        if (statusUI != null)
        {
            DestroyImmediate(statusUI.gameObject);
        }
    }

    void Update()
    {
        // Aggressively enforce FPS settings every frame in builds
        // This is critical because builds can have different behavior than editor
        // Use VSync=0 to force targetFrameRate (VSync=1 locks to monitor refresh rate)
        if (QualitySettings.vSyncCount != 0)
        {
            QualitySettings.vSyncCount = 0;
        }
        if (Application.targetFrameRate != targetFrameRate)
        {
            Application.targetFrameRate = targetFrameRate;
        }

        if (sequenceStarted)
        {
            ManageTimerAndTransitions();
        }

        // if esc is pressed, quit
        if (Input.GetKeyUp(KeyCode.Escape))
        {
            Application.Quit();
        }

        // Toggle status UI with Tab key
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (statusUI != null)
            {
                statusUI.ToggleStatusUI();
            }
        }

        // Handle VR selection (1-4 keys)
        HandleVRSelectionInput();

        // Handle DC offset adjustments for selected VR
        HandleDCOffsetInput();
    }

    private void HandleVRSelectionInput()
    {
        // Select VR1-4 with number keys 1-4
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            selectedVRIndex = 1;
            Debug.Log($"Selected VR1");
            Debugger.Log($"Selected VR1 for DC offset adjustment", 3);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            selectedVRIndex = 2;
            Debug.Log($"Selected VR2");
            Debugger.Log($"Selected VR2 for DC offset adjustment", 3);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            selectedVRIndex = 3;
            Debug.Log($"Selected VR3");
            Debugger.Log($"Selected VR3 for DC offset adjustment", 3);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
        {
            selectedVRIndex = 4;
            Debug.Log($"Selected VR4");
            Debugger.Log($"Selected VR4 for DC offset adjustment", 3);
        }
    }

    private void HandleDCOffsetInput()
    {
        // DC offset adjustments using [ and ] keys for selected VR
        // Continuous adjustment while key is held (not just on key down)
        if (Input.GetKey(KeyCode.RightBracket))
        {
            IncreaseSelectedVRDCOffset();
        }
        if (Input.GetKey(KeyCode.LeftBracket))
        {
            DecreaseSelectedVRDCOffset();
        }
    }

    private void IncreaseSelectedVRDCOffset()
    {
        string vrId = $"VR{selectedVRIndex}";
        if (vrClosedLoops.ContainsKey(vrId))
        {
            ClosedLoop closedLoop = vrClosedLoops[vrId];
            float currentOffset = closedLoop.GetYawDCOffset();
            // Use frame-rate independent step (adjust per second, not per frame)
            // Much faster adjustment - 100x the base step per second
            float stepPerSecond = dcOffsetStep * 100f; // 100x faster for continuous adjustment
            float newOffset = currentOffset + (stepPerSecond * Time.deltaTime);
            closedLoop.SetYawDCOffset(newOffset);
            // Store persistently so it survives scene changes
            persistentDCOffsets[vrId] = newOffset;
            // Only log occasionally to avoid spam
            if (Time.frameCount % 30 == 0) // Log every ~0.5 seconds at 60fps
            {
                Debug.Log($"VR{selectedVRIndex} DC Offset: {newOffset:F4} rad ({newOffset * Mathf.Rad2Deg:F2}°)");
            }
        }
        else
        {
            Debug.LogWarning($"VR{selectedVRIndex} not found for DC offset adjustment");
        }
    }

    private void DecreaseSelectedVRDCOffset()
    {
        string vrId = $"VR{selectedVRIndex}";
        if (vrClosedLoops.ContainsKey(vrId))
        {
            ClosedLoop closedLoop = vrClosedLoops[vrId];
            float currentOffset = closedLoop.GetYawDCOffset();
            // Use frame-rate independent step (adjust per second, not per frame)
            // Much faster adjustment - 100x the base step per second
            float stepPerSecond = dcOffsetStep * 100f; // 100x faster for continuous adjustment
            float newOffset = currentOffset - (stepPerSecond * Time.deltaTime);
            closedLoop.SetYawDCOffset(newOffset);
            // Store persistently so it survives scene changes
            persistentDCOffsets[vrId] = newOffset;
            // Only log occasionally to avoid spam
            if (Time.frameCount % 30 == 0) // Log every ~0.5 seconds at 60fps
            {
                Debug.Log($"VR{selectedVRIndex} DC Offset: {newOffset:F4} rad ({newOffset * Mathf.Rad2Deg:F2}°)");
            }
        }
        else
        {
            Debug.LogWarning($"VR{selectedVRIndex} not found for DC offset adjustment");
        }
    }

    // Public methods for VR DC offset management
    public void RegisterVRClosedLoop(string vrId, ClosedLoop closedLoop)
    {
        if (!string.IsNullOrEmpty(vrId) && closedLoop != null)
        {
            vrClosedLoops[vrId] = closedLoop;
            
            // Restore persistent DC offset if it exists
            if (persistentDCOffsets.ContainsKey(vrId))
            {
                float savedOffset = persistentDCOffsets[vrId];
                closedLoop.SetYawDCOffset(savedOffset);
                Debugger.Log($"Restored persistent DC offset for {vrId}: {savedOffset:F4} rad ({savedOffset * Mathf.Rad2Deg:F2}°)", 3);
            }
            else
            {
                // Initialize with current value for persistence
                persistentDCOffsets[vrId] = closedLoop.GetYawDCOffset();
            }
            
            Debugger.Log($"Registered {vrId} ClosedLoop component", 3);
        }
    }

    public void UnregisterVRClosedLoop(string vrId)
    {
        if (vrClosedLoops.ContainsKey(vrId))
        {
            vrClosedLoops.Remove(vrId);
            Debugger.Log($"Unregistered {vrId} ClosedLoop component", 3);
        }
    }

    public Dictionary<string, float> GetAllVRDCOffsets()
    {
        Dictionary<string, float> offsets = new Dictionary<string, float>();
        for (int i = 1; i <= 4; i++)
        {
            string vrId = $"VR{i}";
            if (vrClosedLoops.ContainsKey(vrId))
            {
                offsets[vrId] = vrClosedLoops[vrId].GetYawDCOffset();
            }
            else
            {
                offsets[vrId] = 0.0f; // Default if not found
            }
        }
        return offsets;
    }

    public int GetSelectedVRIndex()
    {
        return selectedVRIndex;
    }

    private void RefreshVRRegistrations()
    {
        // Find all ClosedLoop components in the scene and register/update them
        // This ensures all VR components in the new scene are registered
        // Note: ClosedLoop.Start() will also register, but using Dictionary ensures no duplicates
        ClosedLoop[] allClosedLoops = FindObjectsOfType<ClosedLoop>();
        foreach (ClosedLoop closedLoop in allClosedLoops)
        {
            SystemConfig config = GetSystemConfigForGameObject(closedLoop.gameObject);
            RegisterVRClosedLoop(config.vrId, closedLoop);
        }
        
        Debugger.Log($"Refreshed VR registrations: {vrClosedLoops.Count} VRs registered", 3);
    }

    void LoadScene(SequenceStep step)
    {
        Debugger.Log("MainController.LoadScene()", 3);
        SyncTimestamp();

        // Clear the screen to black before loading the new scene
        ClearScreenToBlack();

        SceneManager.LoadScene(step.sceneName);
    }

    void SyncTimestamp()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");
    }

    void LoadSequenceConfiguration()
    {
        // Get the path to the sequence configuration JSON file
        string jsonPath = Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json");

        // Check if the streamingAssetsPath directory exists
        if (Directory.Exists(Application.streamingAssetsPath))
        {
            // Check if the sequenceConfig.json file exists
            if (File.Exists(jsonPath))
            {
                // Read the JSON file contents
                string jsonString = File.ReadAllText(jsonPath);

                // Deserialize the JSON content into a custom object
                SequenceConfig config = JsonConvert.DeserializeObject<SequenceConfig>(jsonString);

                if (config != null)
                {
                    randomise = config.randomise; // Get the randomise parameter
                    loopSequence = config.loop; // Set looping based on config

                    foreach (SequenceItem item in config.sequences)
                    {
                        SequenceStep newStep = new SequenceStep(
                            item.sceneName,
                            item.duration,
                            item.gain, // Pass gain
                            item.parameters
                        );
                        sequenceSteps.Add(newStep);
                        Debugger.Log("Added sequence step: " + JsonUtility.ToJson(newStep), 3);
                    }

                    // Log the loaded sequences for debugging
                    Debugger.Log("Loaded sequences: " + sequenceSteps.Count, 4);

                    foreach (SequenceStep step in sequenceSteps)
                    {
                        Debugger.Log("Scene Name: " + step.sceneName, 4);
                        Debugger.Log("Duration: " + step.duration, 4);
                        Debugger.Log("Gain: " + step.gain, 4); // Log gain

                        // Log each key in the parameters dictionary for the current SequenceStep
                        if (step.parameters != null)
                        {
                            foreach (string key in step.parameters.Keys)
                            {
                                Debugger.Log("Parameter Key: " + key, 4);
                            }
                        }
                    }

                    // Get the timestamp from the MasterDataLogger component
                    string timestamp = masterDataLogger.timestamp;
                    Debugger.Log("Timestamp: " + timestamp, 4);
                    if (masterDataLogger != null)
                    {
                        Debug.Log("MasterDataLogger is not null");
                        Debug.Log("Timestamp: " + timestamp);

                        // Copy the sequence config JSON file
                        string sceneName = SceneManager.GetActiveScene().name;
                        string destinationPath = Path.Combine(
                            masterDataLogger.directoryPath,
                            $"{timestamp}_{sceneName}_sequenceConfig.json"
                        );
                        File.Copy(jsonPath, destinationPath);

                        // Save referenced choice config JSON files
                        SaveReferencedChoiceConfigs(config, timestamp, sceneName);
                    }
                    else
                    {
                        Debug.Log("MasterDataLogger is null");
                    }
                }
                else
                {
                    Debugger.Log("Failed to deserialize sequence configuration JSON.", 1);
                }
            }
            else
            {
                Debugger.Log("sequenceConfig.json file not found.", 1);
            }
        }
        else
        {
            Debugger.Log("StreamingAssets folder not found.", 1);
        }
    }

    void OnDisable()
    {
        Debug.Log("MainController was disabled.");
    }

    void ManageTimerAndTransitions()
    {
        // Decrease the timer
        timer -= Time.deltaTime;

        // Check if time is up
        if (timer <= 0)
        {
            // Move to the next step
            currentStep++;

            // If at the end of the sequence
            if (currentStep >= sequenceSteps.Count)
            {
                // Check if looping is enabled
                if (loopSequence)
                {
                    // Restart the sequence from the first step
                    currentStep = 0;

                    // Increment the trial counter
                    currentTrial++;

                    // Re-initialize execution order if randomise is true
                    if (randomise)
                    {
                        InitializeExecutionOrder();
                    }

                    LoadScene(sequenceSteps[executionOrder[currentStep]]);
                }
                else
                {
                    // End the sequence and exit the application
                    Debugger.Log("Sequence completed and looping disabled. Exiting application.", 3);
                    Application.Quit();
                }
            }
            else
            {
                // Load the next scene
                LoadScene(sequenceSteps[executionOrder[currentStep]]);
            }
        }
    }

    void SaveReferencedChoiceConfigs(SequenceConfig config, string timestamp, string sceneName)
    {
        foreach (SequenceItem item in config.sequences)
        {
            if (item.parameters != null && item.parameters.ContainsKey("configFile"))
            {
                string configFileName = item.parameters["configFile"].ToString();
                string sourcePath = Path.Combine(Application.streamingAssetsPath, configFileName);

                if (File.Exists(sourcePath))
                {
                    string destinationPath = Path.Combine(
                        masterDataLogger.directoryPath,
                        $"{timestamp}_{sceneName}_{configFileName}"
                    );
                    File.Copy(sourcePath, destinationPath);
                    Debugger.Log($"Copied choice config: {configFileName}", 3);
                }
                else
                {
                    Debugger.Log($"Choice config file not found: {configFileName}", 2);
                }
            }
        }
    }

    // Add method to clear screen to black
    void ClearScreenToBlack()
    {
        // Since we have a persistent background camera, we just need to ensure it's active
        // and has the correct target display
        if (backgroundCamera != null)
        {
            backgroundCamera.targetDisplay = globalTargetDisplay;
            backgroundCamera.enabled = true;
        }
        
        // Force a GL clear to ensure everything is black immediately
        GL.Clear(true, true, Color.black);
    }

    private void ValidateClosedLoopMode(SystemConfig config)
    {
        // Check if the closedLoopMode is valid
        if (!Enum.IsDefined(typeof(ClosedLoopMode), config.closedLoopMode))
        {
            Debugger.Log($"Invalid closedLoopMode value '{config.closedLoopMode}' for system config '{config.vrId}'. Valid values are: {string.Join(", ", Enum.GetNames(typeof(ClosedLoopMode)))}. Defaulting to FicTrac.", 1);
            config.closedLoopMode = ClosedLoopMode.FicTrac;
        }
        else
        {
            Debugger.Log($"Valid closedLoopMode '{config.closedLoopMode}' loaded for system config '{config.vrId}'", 4);
        }
    }

    private void CreatePersistentBackgroundCamera()
    {
        // Create a new GameObject for the background camera
        GameObject backgroundCameraObject = new GameObject("BackgroundCamera");
        backgroundCameraObject.hideFlags = HideFlags.HideAndDontSave; // Hide and don't save to scene

        // Add a Camera component
        backgroundCamera = backgroundCameraObject.AddComponent<Camera>();
        backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        backgroundCamera.backgroundColor = Color.black;
        backgroundCamera.cullingMask = 0; // Render nothing
        backgroundCamera.depth = -100; // Ensure it's behind all other cameras
        backgroundCamera.orthographic = true; // Use orthographic projection for 2D
        backgroundCamera.orthographicSize = 100f; // Large orthographic size to cover the screen
        backgroundCamera.nearClipPlane = -100f;
        backgroundCamera.farClipPlane = 100f;

        // Set the camera to render to the main display
        backgroundCamera.targetDisplay = globalTargetDisplay;

        // Ensure the camera is not affected by scene changes
        DontDestroyOnLoad(backgroundCameraObject);
    }

    private void CreateStatusUI()
    {
        // Create a new GameObject for the status UI
        GameObject statusUIObject = new GameObject("StatusUI");
        statusUIObject.hideFlags = HideFlags.HideAndDontSave; // Hide and don't save to scene

        // Add the StatusUI component
        statusUI = statusUIObject.AddComponent<StatusUI>();

        // Ensure the status UI is not affected by scene changes
        DontDestroyOnLoad(statusUIObject);

        Debugger.Log("Status UI created and will persist across scenes", 3);
    }

    private void ApplyGainToClosedLoopComponents(float gain)
    {
        // Find all GameObjects with the ClosedLoop component
        ClosedLoop[] closedLoops = FindObjectsOfType<ClosedLoop>();

        foreach (ClosedLoop loop in closedLoops)
        {
            // Apply the gain to the ClosedLoop component using the existing SetYawGain method
            loop.SetYawGain(gain);
            Debugger.Log($"Applied yaw gain {gain} to ClosedLoop component on GameObject: {loop.gameObject.name}", 3);
        }
        
        if (closedLoops.Length > 0)
        {
            Debugger.Log($"Applied gain {gain} to {closedLoops.Length} ClosedLoop components", 3);
        }
        else
        {
            Debugger.Log("No ClosedLoop components found to apply gain to", 3);
        }
    }
}

[System.Serializable]
public class SequenceStep
{
    public string sceneName;
    public float duration;
    public float gain; // Gain value for yaw control
    public Dictionary<string, object> parameters;

    public SequenceStep(string sceneName, float duration, float gain, Dictionary<string, object> parameters)
    {
        this.sceneName = sceneName;
        this.duration = duration;
        this.gain = gain;
        this.parameters = parameters;
    }
}

[System.Serializable]
public class SequenceConfig
{
    public bool randomise = false; // Added field
    public bool loop = true; // Added field for controlling whether the sequence should loop
    public SequenceItem[] sequences;
}

[System.Serializable]
public class SequenceItem
{
    public string sceneName;
    public float duration;
    [Tooltip("Gain value for yaw control in ClosedLoop components. Default is 1.0 if not specified in JSON.")]
    public float gain = 1.0f; // Default gain value for yaw control
    public Dictionary<string, object> parameters;
}

[System.Serializable]
public enum ClosedLoopMode
{
    FicTrac,    // Walking mode - yaw mode off, force mode off
    Kinefly,    // Yaw mode on, force mode off
    Tirbala     // Force/torque accumulation mode - yaw mode off, force mode on
}

[System.Serializable]
public class SystemConfig
{
    public float sphereDiameter = 1.0f;
    public int ledPanelWidth = 128;
    public int ledPanelHeight = 128;
    public int startRow = 0;
    public int startCol = 0;
    public bool horizontal = true;
    public string zmqAddress = "localhost";
    public int zmqPort = 9872;
    public string vrId = "VR1";
    public string displayOrder = "DRBLFU"; // Default display order: Down, Right, Back, Left, Front, Up
    public int targetDisplay = 1; // 0 for primary, 1 for secondary display
    public ClosedLoopMode closedLoopMode = ClosedLoopMode.FicTrac; // Default to FicTrac mode
}

[System.Serializable]
public class SystemConfigArray
{
    public SystemConfig[] configs;
}