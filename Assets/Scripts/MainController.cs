using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using InSceneSequence;

public interface ISceneController
{
    void InitializeScene(Dictionary<string, object> parameters);
}

public class MainController : MonoBehaviour
{
    public static MainController Instance { get; private set; }
    public static event System.Action SystemConfigurationChanged;
    public bool SequenceRunning => sequenceStarted;
    public float RemainingStepSeconds => phaseRunner != null && phaseRunner.Active ? (float)phaseRunner.RemainingSeconds : timer;
    private ExperimentPhases phaseRunner;
    public string ExperimentPhase => phaseRunner != null && phaseRunner.Active ? phaseRunner.CurrentPhase : AssessmentPending ? "assessment" : "stimulus";
    // Shared by recorded rows and live telemetry; do not label idle/error rows as stimulus.
    public static string RecordedExperimentPhase => Instance == null ? "idle" :
        Instance.SequenceError != null || Instance.AssessmentFailed ? "error" :
        !Instance.SequenceRunning ? "idle" : Instance.ExperimentPhase;
    public double RemainingPhaseSeconds => phaseRunner != null && phaseRunner.Active ? phaseRunner.RemainingPhaseSeconds : timer;
    public string SequenceError { get; private set; }
    public IReadOnlyDictionary<string, ClosedLoop> VRClosedLoops => vrClosedLoops;
    public IReadOnlyDictionary<string, SystemConfig> SystemConfigs => systemConfigs;
    public TelemetryConfig TelemetrySettings { get; private set; } = new TelemetryConfig();
    public StatusOverlayConfig StatusOverlaySettings { get; private set; } = new StatusOverlayConfig();
    public int TargetFrameRate { get; private set; } = 120;
    public int VSyncCount { get; private set; } = 0;
    public OverheadCameraConfig OverheadCameraSettings { get; private set; } = new OverheadCameraConfig();

    public List<SequenceStep> sequenceSteps = new List<SequenceStep>();
    public List<int> executionOrder = new List<int>();
    public int currentStep = 0;
    public int currentTrial = 0;
    private float timer;
    private bool sequenceStarted = false;
    private MasterDataLogger masterDataLogger;
    public bool loopSequence = false;
    private bool randomise = false; // Added field
    private bool autoStart;
    private readonly Dictionary<string, ClosedLoop> vrClosedLoops = new Dictionary<string, ClosedLoop>();
    private readonly Dictionary<string, float> persistentDCOffsets = new Dictionary<string, float>();
    private int selectedVRIndex = 1;
    public int SelectedVRIndex => selectedVRIndex;
    public bool CurrentAutoTrimEnabled => sequenceStarted && GetCurrentSequenceStep()?.autoTrim == true;

    public void RememberDCOffset(string vrId, float offset) => persistentDCOffsets[vrId] = offset;

    // Dashboard button changes only the running row. Later rows keep their own configured flag.
    public void SetCurrentAutoTrim(bool enabled)
    {
        var step = GetCurrentSequenceStep();
        if (!sequenceStarted || step == null) return;
        step.autoTrim = enabled;
        ApplyAutoTrimToClosedLoopComponents(step);
    }

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
    private ISceneController activeSceneController;   // <— NEW

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
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate MainController detected, destroying the newer instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

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

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
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
        }
    }

    void Start()
    {
        // Log that we're starting
        Debugger.Log("MainController.Start()", 3);

        // Load the sequence configuration
        LoadSequenceConfiguration();
        // Opt-in preview sequences can start with Play in ControlScene. Start only once:
        // returning to Control with Escape must not immediately restart the experiment.
        if (autoStart && SceneManager.GetActiveScene().name == "ControlScene") StartSequence();
    }

    // Load system configurations from the specified file
    private void LoadSystemConfigurations()
    {
        // A removed/invalid configuration must not leave the previous layout rendering.
        systemConfigs.Clear();
        try
        {
            if (string.IsNullOrEmpty(Application.streamingAssetsPath) || string.IsNullOrEmpty(systemConfigFileName))
                throw new System.ArgumentException("System configuration path is empty.");
            string configPath = Path.Combine(Application.streamingAssetsPath, systemConfigFileName);
            if (!File.Exists(configPath))
                throw new FileNotFoundException("System config file not found", configPath);

            string jsonText = File.ReadAllText(configPath);

            // Parse the JSON using JObject instead of dynamic
            JObject fullConfig = JObject.Parse(jsonText);
            ApplyFrameTiming(fullConfig);
            LoadDiagnosticsConfigurations(fullConfig);
            OverheadCameraSettings = fullConfig["overheadCamera"]?.ToObject<OverheadCameraConfig>() ?? new OverheadCameraConfig();

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
                    // Set target display from global setting
                    config.targetDisplay = globalTargetDisplay;
                    systemConfigs[config.vrId] = config;
                    Debugger.Log($"Loaded system config for: {config.vrId} with targetDisplay: {config.targetDisplay}", 3);
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
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                File.Copy(configPath, destPath, true);
                Debugger.Log($"Copied system config file to: {destPath}", 3);
            }
        }
        catch (System.Exception e)
        {
            systemConfigs.Clear();
            Debugger.Log($"Error loading system config file: {e.Message}", 1);
        }
        finally
        {
            SystemConfigurationChanged?.Invoke();
        }
    }

    private void ApplyFrameTiming(JObject config)
    {
        int target = config.Value<int?>("targetFrameRate") ?? 120;
        int sync = config.Value<int?>("vSyncCount") ?? 0;
        if (target != -1 && target <= 0)
            throw new System.ArgumentException("targetFrameRate must be positive, or -1 for uncapped desktop rendering.");
        if (sync < 0 || sync > 4)
            throw new System.ArgumentException("vSyncCount must be from 0 to 4.");
        TargetFrameRate = target;
        VSyncCount = sync;
        QualitySettings.vSyncCount = sync;
        Application.targetFrameRate = target;
    }

    private void LoadDiagnosticsConfigurations(JObject config)
    {
        // Optional monitoring must not erase the independently configured tracker ports.
        // Value validation and bind failures belong to ExperimentTelemetry.Configure.
        try { TelemetrySettings = config["telemetry"]?.ToObject<TelemetryConfig>() ?? new TelemetryConfig(); }
        catch (System.Exception error)
        {
            TelemetrySettings = new TelemetryConfig { enabled = false };
            Debug.LogError("Invalid telemetry configuration; output disabled: " + error.Message);
        }
        try { StatusOverlaySettings = config["statusOverlay"]?.ToObject<StatusOverlayConfig>() ?? new StatusOverlayConfig(); }
        catch (System.Exception error)
        {
            StatusOverlaySettings = new StatusOverlayConfig();
            Debug.LogError("Invalid status overlay configuration; using defaults: " + error.Message);
        }
    }

    // Match a complete rig ID, including parent rigs, without confusing VR1 and VR10.
    // Rendering callers use TryGet so a missing rig cannot borrow VR1's physical viewports.
    public bool TryGetSystemConfigForGameObject(GameObject target, out SystemConfig config)
    {
        for (Transform item = target.transform; item != null; item = item.parent)
        {
            var match = System.Text.RegularExpressions.Regex.Match(item.name, @"(?<![A-Za-z0-9])VR[0-9]+(?![A-Za-z0-9])");
            if (match.Success) return systemConfigs.TryGetValue(match.Value, out config);
        }
        config = null;
        return false;
    }

    public SystemConfig GetSystemConfigForGameObject(GameObject gameObject)
    {
        if (TryGetSystemConfigForGameObject(gameObject, out SystemConfig config)) return config;
        Debugger.Log($"No system config found for {gameObject.name}", 1);
        return new SystemConfig { displayOrder = "" };
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
        phaseRunner?.Cancel();
        currentStep = 0;
        foreach (ClosedLoop rig in vrClosedLoops.Values)
            if (rig != null)
            {
                rig.GetComponent<StimulusHeadingReference>()?.Cancel();
                rig.ConfigureAutoTrim(false, null);
            }
    }

    public void StartSequence()
    {
        Debugger.Log("MainController.StartSequence()", 3);

        if (sequenceSteps.Count == 0)
        {
            Debug.LogError("Cannot start sequence because no sequence steps were loaded.");
            return;
        }

        sequenceStarted = true;
        SequenceError = null;

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

        if (!sequenceStarted || executionOrder.Count == 0 || currentStep >= executionOrder.Count)
        {
            Debugger.Log("Ignoring sceneLoaded callback because sequence execution is not active yet.", 4);
            return;
        }

        // Clear screen to black immediately after loading
        ClearScreenToBlack();

        SequenceStep currentStepData = sequenceSteps[executionOrder[currentStep]];

        // Note: Components will load their own configs based on vrId
        // No need to scan for them here

        activeSceneController = null;
        foreach (var obj in FindObjectsOfType<MonoBehaviour>()) // MonoBehaviour is the base class for all Unity Behaviours
        {
            if (!obj.isActiveAndEnabled || obj.gameObject.scene != scene) continue;
            if (obj is Kannadi)
            {
                activeSceneController = (ISceneController)obj;
                break;
            }
            if (activeSceneController == null && obj is ISceneController)
                activeSceneController = (ISceneController)obj;
        }

        if (activeSceneController != null && currentStepData.parameters != null)
        {
            if (!currentStepData.parameters.ContainsKey("gain")) currentStepData.parameters["gain"] = currentStepData.gain;
            InitializeStep(() => ApplyExperiment(currentStepData.parameters, activeSceneController.InitializeScene));
            timer = currentStepData.duration;
        }
        else
        {
            Debugger.Log("Either the scene controller or the parameters are null.", 2);
        }
        ApplyGainToClosedLoopComponents(currentStepData.gain);
        ApplyAutoTrimToClosedLoopComponents(currentStepData);
    }

    private void ApplyGainToClosedLoopComponents(float gain)
    {
        foreach (ClosedLoop tracking in FindObjectsByType<ClosedLoop>(FindObjectsSortMode.None))
            tracking.SetYawGain(gain);
    }

    private void ApplyAutoTrimToClosedLoopComponents(SequenceStep step)
    {
        try
        {
            (step.autoTrimSettings ?? new AutoTrimConfig()).Validate();
            foreach (ClosedLoop tracking in FindObjectsByType<ClosedLoop>(FindObjectsSortMode.None))
                tracking.ConfigureAutoTrim(sequenceStarted && SequenceError == null && step.autoTrim, step.autoTrimSettings);
        }
        catch (System.Exception error)
        {
            foreach (ClosedLoop tracking in FindObjectsByType<ClosedLoop>(FindObjectsSortMode.None)) tracking.ConfigureAutoTrim(false, null);
            FailExperiment(error);
        }
    }

    public void RegisterVRClosedLoop(string vrId, ClosedLoop tracking)
    {
        if (string.IsNullOrEmpty(vrId) || tracking == null) return;
        vrClosedLoops[vrId] = tracking;
        if (persistentDCOffsets.TryGetValue(vrId, out float offset)) tracking.SetYawDCOffset(offset);
        else persistentDCOffsets[vrId] = tracking.GetYawDCOffset();
        var step = GetCurrentSequenceStep();
        if (sequenceStarted && SequenceError == null && step != null) tracking.ConfigureAutoTrim(step.autoTrim, step.autoTrimSettings);
    }

    public void UnregisterVRClosedLoop(string vrId, ClosedLoop tracking)
    {
        if (vrClosedLoops.TryGetValue(vrId, out ClosedLoop current) && current == tracking) vrClosedLoops.Remove(vrId);
    }

    private void HandleBogongGainInput()
    {
        if (ExperimentInput.IsEditingText) return;
        var keys = UnityEngine.InputSystem.Keyboard.current;
        if (keys == null) return;
        if (keys.digit1Key.wasPressedThisFrame || keys.numpad1Key.wasPressedThisFrame) selectedVRIndex = 1;
        else if (keys.digit2Key.wasPressedThisFrame || keys.numpad2Key.wasPressedThisFrame) selectedVRIndex = 2;
        else if (keys.digit3Key.wasPressedThisFrame || keys.numpad3Key.wasPressedThisFrame) selectedVRIndex = 3;
        else if (keys.digit4Key.wasPressedThisFrame || keys.numpad4Key.wasPressedThisFrame) selectedVRIndex = 4;
        string vrId = "VR" + selectedVRIndex;
        if (!vrClosedLoops.TryGetValue(vrId, out ClosedLoop tracking) || tracking == null || !tracking.UsesBogongInput) return;
        // BogongAustralia: dcOffsetStep (0.005) * 100 * dt; held brackets select the direction.
        float offset = tracking.GetYawDCOffset();
        if (keys.rightBracketKey.isPressed) offset += .005f * 100f * Time.deltaTime;
        if (keys.leftBracketKey.isPressed) offset -= .005f * 100f * Time.deltaTime;
        tracking.SetYawDCOffset(offset);
        persistentDCOffsets[vrId] = offset;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Update()
    {
        HandleBogongGainInput();
        if (sequenceStarted)
        {
            ManageTimerAndTransitions();
        }

        // One owner for Escape: stop the experiment; Escape in Control exits the player.
        if (ExperimentInput.Released("Cancel")) HandleEscape();
    }

    public void HandleEscape()
    {
        if (SceneManager.GetActiveScene().name == "ControlScene")
        {
            Application.Quit();
            return;
        }
        StopSequence();
        SceneManager.LoadScene("ControlScene");
    }

    void LoadScene(SequenceStep step)
    {
        phaseRunner?.Cancel();
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
        sequenceSteps.Clear();
        executionOrder.Clear();
        autoStart = false;

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
                    autoStart = config.autoStart;

                    foreach (SequenceItem item in config.sequences)
                    {
                        SequenceStep newStep = new SequenceStep(
                            item.sceneName,
                            item.duration,
                            item.parameters,
                            item.reloadScene,
                            item.gain,
                            item.autoTrim,
                            item.autoTrimSettings
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
                    if (masterDataLogger != null)
                    {
                        string timestamp = masterDataLogger.timestamp;
                        Debugger.Log("Timestamp: " + timestamp, 4);
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

private void ApplyExperiment(Dictionary<string, object> parameters, System.Action<Dictionary<string, object>> apply)
{
    if (phaseRunner == null) phaseRunner = gameObject.AddComponent<ExperimentPhases>();
    var resolved = ExperimentConfigFiles.ResolveReferences(parameters);
    if (!phaseRunner.TryBegin(resolved, apply, FailExperiment)) apply(resolved);
}

private void FailExperiment(System.Exception error)
{
    SequenceError = error.Message;
    sequenceStarted = false;
    phaseRunner?.Cancel();
    foreach (ClosedLoop rig in vrClosedLoops.Values)
        if (rig != null) rig.ConfigureAutoTrim(false, null);
    Debug.LogException(error);
}

private void InitializeStep(System.Action initialize)
{
    try { initialize(); }
    catch (System.Exception error)
    {
        FailExperiment(error);
    }
}

public bool AssessmentFailed
{
    get
    {
        foreach (ClosedLoop rig in vrClosedLoops.Values)
            if (rig != null && rig.GetComponent<StimulusHeadingReference>()?.Phase == "error") return true;
        return false;
    }
}

public bool AssessmentPending
{
    get
    {
        foreach (ClosedLoop rig in vrClosedLoops.Values)
        {
            if (rig == null) continue;
            var reference = rig.GetComponent<StimulusHeadingReference>();
            if (reference != null && (reference.IsObserving || reference.Phase == "error")) return true;
        }
        return false;
    }
}

void ManageTimerAndTransitions()
{
    // Phased files own their complete schedule. Flat files retain a presentation
    // timer that waits for any legacy heading observation.
    if (phaseRunner != null && phaseRunner.Active)
    {
        timer = (float)phaseRunner.RemainingSeconds;
        if (!phaseRunner.Finished) return;
    }
    else
    {
        if (AssessmentPending) return;
        timer -= Time.deltaTime;
    }

    if (timer > 0) return;   // still running this step

    // ----------------------------------------------------------
    // TIME’S UP → decide how to move to the next SequenceStep
    // ----------------------------------------------------------
    currentStep++;

    // end-of-list logic (loop / quit) stays exactly as before
    if (currentStep >= sequenceSteps.Count)
    {
        if (loopSequence)
        {
            currentStep = 0;
            currentTrial++;

            if (randomise) InitializeExecutionOrder();
        }
        else
        {
            Debugger.Log("Sequence completed and looping disabled. Exiting application.", 3);
            Application.Quit();
            return;
        }
    }

    // ----------------------------------------------------------
    // examine the *next* step
    // ----------------------------------------------------------
    SequenceStep next = sequenceSteps[executionOrder[currentStep]];
    // ❶ cast once, store the reference (null if the active controller
    //    does NOT implement IInSceneSequencer)
    var sequencer = activeSceneController as IInSceneSequencer;

    // ❷ build the condition
    bool canMutateInPlace =
            !next.reloadScene &&
            SceneManager.GetActiveScene().name == next.sceneName &&
            sequencer != null;

    if (canMutateInPlace)
    {
        // ★ NEW PATH: keep scene, just tell it to advance
        if (next.parameters != null && !next.parameters.ContainsKey("gain")) next.parameters["gain"] = next.gain;
        InitializeStep(() => ApplyExperiment(next.parameters, sequencer.AdvanceStep));
        ApplyGainToClosedLoopComponents(next.gain);
        ApplyAutoTrimToClosedLoopComponents(next);
        timer = next.duration;      // restart timer for the new sub-step
    }
    else
    {
        // LEGACY PATH: load another scene (old behaviour)
        LoadScene(next);
    }
}


    void SaveReferencedChoiceConfigs(SequenceConfig config, string timestamp, string sceneName)
    {
        HashSet<string> copiedConfigFiles = new HashSet<string>();

        foreach (SequenceItem item in config.sequences)
        foreach (string referenceKey in new[] { "configFile", "design" })
        {
            if (item.parameters != null && item.parameters.TryGetValue(referenceKey, out object reference) && reference != null)
            {
                string configFileName = reference.ToString();

                if (!copiedConfigFiles.Add(configFileName))
                {
                    Debugger.Log($"Skipping duplicate choice config copy: {configFileName}", 4);
                    continue;
                }

                string sourcePath = ExperimentConfigFiles.Resolve(configFileName);

                if (File.Exists(sourcePath))
                {
                    string destinationPath = Path.Combine(
                        masterDataLogger.directoryPath,
                        $"{timestamp}_{sceneName}_{configFileName}"
                    );
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                    File.Copy(sourcePath, destinationPath, true);
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
        // This creates a temporary camera to clear the screen to black
        // It's cheaper than keeping an extra camera around all the time
        Camera clearCamera = new GameObject("TempClearCamera").AddComponent<Camera>();
        clearCamera.clearFlags = CameraClearFlags.SolidColor;
        clearCamera.backgroundColor = Color.black;
        clearCamera.cullingMask = 0; // Render nothing
        clearCamera.Render(); // Force a render
        Destroy(clearCamera.gameObject); // Clean up

        // Also force a GL clear to ensure everything is black
        GL.Clear(true, true, Color.black);
    }
}

[System.Serializable]
public class SequenceStep
{
    public string sceneName;
    public float duration;
    public Dictionary<string, object> parameters;
    public bool   reloadScene = true;
    public float gain = 1f;
    public bool autoTrim = false;
    public AutoTrimConfig autoTrimSettings = new AutoTrimConfig();
    public SequenceStep(string sceneName, float duration, Dictionary<string, object> parameters, bool reloadScene = true, float gain = 1f,
                        bool autoTrim = false, AutoTrimConfig autoTrimSettings = null)
    {
        this.sceneName = sceneName;
        this.duration = duration;
        this.parameters = parameters;
        this.reloadScene = reloadScene;
        this.gain = gain;
        this.autoTrim = autoTrim;
        this.autoTrimSettings = autoTrimSettings ?? new AutoTrimConfig();
    }
}

[System.Serializable]
public class SequenceConfig
{
    public bool autoStart = false;
    public bool randomise = false; // Added field
    public bool loop = true; // Added field for controlling whether the sequence should loop
    public SequenceItem[] sequences;
}

[System.Serializable]
public class SequenceItem
{
    public string sceneName;
    public float duration;
    public float gain = 1f;
    public bool autoTrim = false;
    public AutoTrimConfig autoTrimSettings = new AutoTrimConfig();
    public Dictionary<string, object> parameters;

    // NEW —— defaults to true, so legacy JSON stays valid
    public bool reloadScene = true;
}

[System.Serializable]
public enum ClosedLoopMode { FicTrac, Kinefly, Tirbala }

[System.Serializable]
public class SystemConfig
{
    public ClosedLoopMode closedLoopMode = ClosedLoopMode.FicTrac;
    public ManualControlConfig manualControls;
    public float sphereDiameter = 1.0f;
    public int ledPanelWidth = 128;
    public int ledPanelHeight = 128;
    public int startRow = 0;
    public int startCol = 0;
    public bool horizontal = true;
    public string zmqAddress = "localhost";
    public int zmqPort = 9872;
    public string vrId = "VR1";
    public string displayOrder = ""; // Explicit rendering allow-list: Down, Right, Back, Left, Front, Up.
    public int targetDisplay = 1; // 0 for primary, 1 for secondary display
}

[System.Serializable]
public class SystemConfigArray
{
    public SystemConfig[] configs;
}
