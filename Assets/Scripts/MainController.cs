using System.Collections.Generic;
using System.Linq;
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
    public bool SceneTransitionPending { get; private set; }
    public string InputKind { get; private set; } = "tracker";
    public float RemainingStepSeconds => phaseRunner != null && phaseRunner.Active ? (float)phaseRunner.RemainingSeconds : Mathf.Max(0,timer);
    public double CurrentStepDurationSeconds => phaseRunner != null && phaseRunner.Active ? phaseRunner.TotalSeconds : System.Math.Max(0, GetCurrentSequenceStep()?.duration ?? 0);
    public double CurrentStepElapsedSeconds => System.Math.Max(0, CurrentStepDurationSeconds - RemainingStepSeconds);
    private ExperimentPhases phaseRunner;
    public string ExperimentPhase => phaseRunner != null && phaseRunner.Active ? phaseRunner.CurrentPhase : AssessmentPending ? "assessment" : "stimulus";
    // Shared by recorded rows and live telemetry; do not label idle/error rows as stimulus.
    public static string RecordedExperimentPhase => Instance == null ? "idle" :
        Instance.SequenceError != null || Instance.AssessmentFailed ? "error" :
        !Instance.SequenceRunning ? "idle" : Instance.ExperimentPhase;
    public double RemainingPhaseSeconds => phaseRunner != null && phaseRunner.Active ? phaseRunner.RemainingPhaseSeconds : Mathf.Max(0,timer);
    public string SequenceError { get; private set; }
    public IReadOnlyDictionary<string, ClosedLoop> VRClosedLoops => vrClosedLoops;
    public IReadOnlyDictionary<string, SystemConfig> SystemConfigs => systemConfigs;
    public AnimalData CurrentAnimalMetadata { get; private set; } = new AnimalData();
    public string GetBackendDisplayName(SystemConfig config) => TrackerBackend.DisplayName(config.closedLoopMode);
    public void SetAnimalMetadata(AnimalData metadata) => CurrentAnimalMetadata = metadata ?? new AnimalData();
    public TelemetryConfig TelemetrySettings { get; private set; } = new TelemetryConfig();
    public StatusOverlayConfig StatusOverlaySettings { get; private set; } = new StatusOverlayConfig();
    public OverlayPlacementConfig FrameOverlaySettings { get; private set; } = new OverlayPlacementConfig();
    public OverlayPlacementConfig DesktopControlsSettings { get; private set; } = new OverlayPlacementConfig { targetDisplay = -1 };
    public int TargetFrameRate { get; private set; } = 60;
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
    public int LoopStartIndex { get; private set; } = 0;
    private bool randomise = false; // Added field
    private bool autoStart;
    private readonly Dictionary<string, ClosedLoop> vrClosedLoops = new Dictionary<string, ClosedLoop>();
    private readonly Dictionary<string, float> persistentDCOffsets = new Dictionary<string, float>();
    private readonly Dictionary<string, HeadingReferenceResult> savedHeadings = new Dictionary<string, HeadingReferenceResult>();

    private string HeadingKey(GameObject rig, string id) =>
        (TryGetSystemConfigForGameObject(rig, out var config) ? config.vrId : rig.name) + ":" + id;

    public void SaveHeadingReference(GameObject rig, string id, HeadingReferenceResult result)
    {
        if (!sequenceStarted || result == null) throw new System.InvalidOperationException("Cannot save a heading outside a running sequence.");
        savedHeadings[HeadingKey(rig, id)] = result;
    }

    public HeadingReferenceResult ResolveHeadingReference(GameObject rig, string id)
    {
        if (!sequenceStarted || !savedHeadings.TryGetValue(HeadingKey(rig, id), out var result))
            throw new System.InvalidOperationException($"Heading reference '{id}' has not been measured for {rig.name} in this sequence cycle. Run the Milky Way measurement block first.");
        return result;
    }
    private int selectedVRIndex = 1;
    public int SelectedVRIndex => selectedVRIndex;
    public bool CurrentAutoTrimEnabled => sequenceStarted && GetCurrentSequenceStep()?.autoTrim == true;
    private AutoTrimConfig currentAutoTrimSettings = new AutoTrimConfig();
    private bool? flightCheckOverride;
    public bool CurrentFlightCheckEnabled => flightCheckOverride ?? currentAutoTrimSettings.flightCheckEnabled;

    public void RememberDCOffset(string vrId, float offset) => persistentDCOffsets[vrId] = offset;

    // Dashboard button changes only the running row. Later rows keep their own configured flag.
    public void SetCurrentAutoTrim(bool enabled)
    {
        var step = GetCurrentSequenceStep();
        if (!sequenceStarted || step == null) return;
        step.autoTrim = enabled;
        ApplyAutoTrimToClosedLoopComponents(step);
    }

    public void SetCurrentFlightCheck(bool enabled)
    {
        if (!sequenceStarted) return;
        flightCheckOverride = enabled;
        foreach (ClosedLoop rig in vrClosedLoops.Values)
            if (rig != null) rig.AutoTrim.SetFlightCheckEnabled(enabled);
    }

    // System Config properties
    [SerializeField]
    private string systemConfigFileName = "system_config.json";
    public string SystemConfigurationFileName => systemConfigFileName;
    private string sequenceConfigFileName = "sequenceConfig.json";
    public string SequenceConfigurationFileName => sequenceConfigFileName;
    public string SequenceTitle { get; private set; }
    public string CurrentExperimentTitle { get; private set; }
    public bool SequencePreparing { get; private set; }
    public bool SequenceStopping { get; private set; }
    public string RecordingTransitionError { get; private set; }
    public string PreparationStatus { get; private set; }
    public System.Threading.Tasks.Task RecordingCompletion => recordingCompletion;
    public bool NativeRecordingStopSettled { get; private set; } = true;
    private System.Threading.CancellationTokenSource preparationCancellation;
    private System.Threading.Tasks.Task nativeRecordingStartTask = System.Threading.Tasks.Task.CompletedTask;
    private System.Threading.Tasks.Task recordingCompletion = System.Threading.Tasks.Task.CompletedTask;
    private string recordingRunId, recordingDirectory;
    private string randomSeedMode = "Fixed";
    private int randomSeed = 1;
    private int preparationGeneration;
    public bool ReturnToSetupAfterSequence { get; private set; }

    // Called only from the idle setup screen, after validation and saving.
    public void PrepareConfiguredRun(string systemFile, string sequenceFile)
    {
        if (sequenceStarted || SequencePreparing || SequenceStopping) throw new System.InvalidOperationException("Wait for the experiment and its recorders to stop before changing configuration.");
        systemConfigFileName = systemFile;
        sequenceConfigFileName = sequenceFile;
        DesktopQuickStart.RememberWorkspace(systemFile, sequenceFile);
        masterDataLogger = MasterDataLogger.Instance;
        masterDataLogger.BeginConfiguredSession(systemFile, sequenceFile);
        recordingRunId = null;
        recordingDirectory = null;
        nativeRecordingStartTask = System.Threading.Tasks.Task.CompletedTask;
        RecordingTransitionError = null;
        NativeRecordingStopSettled = true;
        SequenceError = null;
        currentTrial = 0;
        savedHeadings.Clear();
        LoadSystemConfigurations();
        LoadSequenceConfiguration();
        ReturnToSetupAfterSequence = true;
    }

    // Dictionary to store loaded system configs
    private Dictionary<string, SystemConfig> systemConfigs = new Dictionary<string, SystemConfig>();
    private string systemConfigurationError;

    [Tooltip("0: Off, ,1: Error, 2: Warning, 3: Info, 4: Debug")]
    [SerializeField]
    [Range(0, 4)]
    private int logLevel = 0; // 0: All, 1: Error, 2: Warning, 3: Info, 4: Debug

    // Serialized compatibility switch for eagerly activating configured outputs.
    // Per-rig routing is always owned by ViewportSetter.
    [SerializeField]
    [Tooltip("Activate configured outputs at startup. Each rig retains its own output display.")]
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
        if(masterDataLogger==null)
            masterDataLogger=new GameObject("MasterDataLogger").AddComponent<MasterDataLogger>();

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
        if (DesktopQuickStart.InitialSelection(out string desktopSystem, out string desktopSequence))
        { systemConfigFileName = desktopSystem; sequenceConfigFileName = desktopSequence; }
        LoadSystemConfigurations();
        LoadAnimalMetadata();

        // Setup display handling if enabled
        if (preventMultipleWindows)
        {
            HandleDisplaySetup();
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Activate requested outputs without overwriting per-rig or operator cameras.
    private void HandleDisplaySetup()
    {
        foreach (int display in systemConfigs.Values.Select(config => config.targetDisplay).Distinct())
        {
            if (display <= 0 || display >= Display.displays.Length) continue;
            Display.displays[display].Activate();
            Debugger.Log($"Activated configured display {display}", 3);
        }
    }

    public static int DefaultTargetDisplay(JObject root, string[] arguments, int fallback = 1)
    {
        int display = root["targetDisplay"]?.Value<int>() ?? fallback;
        for (int i = 0; i < arguments.Length - 1; i++)
            if (string.Equals(arguments[i], "-display", System.StringComparison.OrdinalIgnoreCase) && int.TryParse(arguments[i + 1], out int requested))
                return requested;
        return display;
    }

    public static SystemConfig[] ParseSystemConfigurations(JArray source, int fallbackDisplay)
    {
        var configs = source.ToObject<SystemConfig[]>();
        for (int i = 0; i < configs.Length; i++)
            if (configs[i] != null && source[i] is JObject rig && rig.Property("targetDisplay") == null)
                configs[i].targetDisplay = fallbackDisplay;
        return configs;
    }

    void Start()
    {
        // Log that we're starting
        Debugger.Log("MainController.Start()", 3);

        // Load the sequence configuration
        LoadSequenceConfiguration();
        // Opt-in preview sequences can start with Play in ControlScene. Start only once:
        // returning to Control with Escape must not immediately restart the experiment.
        if (autoStart && SceneManager.GetActiveScene().name == "ControlScene" && !ConfigurationUI.ConfigWorkbench.ForceSetup) StartSequence();
    }

    private void LoadAnimalMetadata()
    {
        string backup = Path.Combine(RuntimePaths.RunDataRoot, "Backup", "AnimalMetadata.json");
        if (!File.Exists(backup)) backup = Path.Combine(RuntimePaths.RunDataRoot, "Backup", "FlyMetaData.json");
        try { if (File.Exists(backup)) SetAnimalMetadata(JsonConvert.DeserializeObject<AnimalData>(File.ReadAllText(backup))); }
        catch (System.Exception error) { Debug.LogWarning("Animal metadata could not be read: " + error.Message); }
    }

    // Load system configurations from the specified file
    private void LoadSystemConfigurations()
    {
        // A removed/invalid configuration must not leave the previous layout rendering.
        systemConfigs.Clear();
        systemConfigurationError = null;
        try
        {
            if (string.IsNullOrEmpty(RuntimePaths.ConfigRoot) || string.IsNullOrEmpty(systemConfigFileName))
                throw new System.ArgumentException("System configuration path is empty.");
            string configPath = Path.Combine(RuntimePaths.ConfigRoot, systemConfigFileName);
            if (!File.Exists(configPath))
                throw new FileNotFoundException("System config file not found", configPath);

            string jsonText = ExperimentConfigFiles.ReadAllText(configPath,"system");

            // Parse the JSON using JObject instead of dynamic
            JObject fullConfig = JObject.Parse(jsonText);
            InputKind = fullConfig["desktopQuickStart"] is JObject preview && (bool?)preview["demo"] == true ? "demonstration" : "tracker";
            ApplyFrameTiming(fullConfig);
            LoadDiagnosticsConfigurations(fullConfig);
            OverheadCameraSettings = fullConfig["overheadCamera"]?.ToObject<OverheadCameraConfig>() ?? new OverheadCameraConfig();

            // Legacy global/CLI output is a fallback; explicit rig routing wins.
            globalTargetDisplay = DefaultTargetDisplay(fullConfig, System.Environment.GetCommandLineArgs());

            // Parse the configs array
            JArray configsArray = (JArray)fullConfig["configs"];
            if (configsArray != null)
            {
                SystemConfig[] loadedConfigs = ParseSystemConfigurations(configsArray, globalTargetDisplay);

                // Clear existing configs
                systemConfigs.Clear();

                // Add each config to dictionary with VR ID as key
                foreach (SystemConfig config in loadedConfigs)
                {
                    if (config == null) throw new System.ArgumentException("Null rig configuration.");
                    if (config == null || RuntimeRigRegistry.ParseIndex(config.vrId) < 1 || config.vrId != "VR" + RuntimeRigRegistry.ParseIndex(config.vrId))
                        throw new System.ArgumentException("Every rig needs an ID such as VR1 or VR10.");
                    if (systemConfigs.ContainsKey(config.vrId)) throw new System.ArgumentException("Duplicate rig ID: " + config.vrId);
                    systemConfigs.Add(config.vrId, config);
                    Debugger.Log($"Loaded system config for: {config.vrId} with targetDisplay: {config.targetDisplay}", 3);
                }

                if (!systemConfigs.ContainsKey("VR" + selectedVRIndex) && systemConfigs.Count > 0)
                        selectedVRIndex = systemConfigs.Keys.Select(RuntimeRigRegistry.ParseIndex).Min();
                Debugger.Log($"Successfully loaded system config file: {systemConfigFileName}", 3);
            }
            else
            {
                Debugger.Log("No configs array found in system config file", 1);
            }

        }
        catch (System.Exception e)
        {
            systemConfigs.Clear();
            systemConfigurationError = "Invalid system configuration: " + e.Message;
            Debug.LogError(systemConfigurationError);
        }
        finally
        {
            SystemConfigurationChanged?.Invoke();
        }
    }

    private void ApplyFrameTiming(JObject config)
    {
        int target = config.Value<int?>("targetFrameRate") ?? 60;
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

    public void SetRuntimeFrameRate(int target)
    {
        if(target!=-1&&target<=0)throw new System.ArgumentException("Use a positive frame-rate cap, or -1 for uncapped.");
        if(VSyncCount>0)throw new System.InvalidOperationException("VSync controls presentation timing. Change VSync in system settings first.");
        if(target==TargetFrameRate)return;
        var logger=MasterDataLogger.Instance;
        if(SequenceRunning&&logger!=null&&logger.AcceptingWrites)
        {
            var evidence=new JObject { ["schemaVersion"]=1,["event"]="frameRateCap",["utc"]=System.DateTimeOffset.UtcNow,
                ["unityFrame"]=Time.frameCount,["unityTimeSeconds"]=Time.timeAsDouble,["previousTarget"]=TargetFrameRate,["target"]=target,["vSyncCount"]=VSyncCount };
            File.AppendAllText(Path.Combine(logger.directoryPath,"runtime_display_settings.jsonl"),evidence.ToString(Formatting.None)+"\n");
        }
        TargetFrameRate=target;Application.targetFrameRate=target;
    }

    public void ApplyIdleFrameTiming(JObject config)
    {
        if(SequenceRunning||SequencePreparing)throw new System.InvalidOperationException("Use the live frame-rate editor while an experiment is running or preparing.");
        ApplyFrameTiming(config);
    }

    private void LoadDiagnosticsConfigurations(JObject config)
    {
        try { FrameOverlaySettings=config["frameOverlay"]?.ToObject<OverlayPlacementConfig>()??new OverlayPlacementConfig(); }
        catch(System.Exception error){FrameOverlaySettings=new OverlayPlacementConfig();Debug.LogWarning("Invalid frame overlay placement; using defaults: "+error.Message);}
        try {
            DesktopControlsSettings=config["desktopControls"]?.ToObject<OverlayPlacementConfig>()??new OverlayPlacementConfig { targetDisplay=-1 };
            if(!(config["desktopControls"] is JObject controls)||controls["targetDisplay"]==null)DesktopControlsSettings.targetDisplay=-1;
        }
        catch(System.Exception error){DesktopControlsSettings=new OverlayPlacementConfig {targetDisplay=-1};Debug.LogWarning("Invalid desktop controls placement; using defaults: "+error.Message);}
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
        if(ReplayExecutionContext.Active)return ReplayExecutionContext.TrySystemConfig(target,out config);
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

    public void StopSequence()=>StopSequence("stopped");

    public void StopSequence(string endReason)
    {
        ExperimentRandom.End();
        preparationGeneration++;
        preparationCancellation?.Cancel();
        SequencePreparing = false;
        sequenceStarted = false;
        savedHeadings.Clear();
        phaseRunner?.Cancel();
        currentStep = 0;
        foreach (ClosedLoop rig in vrClosedLoops.Values)
            if (rig != null)
            {
                rig.GetComponent<StimulusHeadingReference>()?.Cancel();
                rig.AutoTrim.Disable();
            }
        FinalizeRecording(endReason);
    }

    public System.Threading.Tasks.Task StopSequenceAsync(string endReason = "stopped")
    {
        StopSequence(endReason);
        return recordingCompletion;
    }

    private void FinalizeRecording(string endReason)
    {
        var logger = MasterDataLogger.Instance;
        if (logger == null || SequenceStopping || logger.FinalizationTask != null) return;
        SequenceStopping = true;
        // Pose/world writers stop immediately. The media session stays open for
        // the matching native take's eventual completion receipt.
        try { logger.SealSessionForFinalization(); }
        catch (System.Exception error) { RecordTransitionFailure(logger, "Closing VR writers: " + error.Message); }
        recordingCompletion = FinishRecordingAsync(logger, recordingRunId, endReason, nativeRecordingStartTask);
    }

    private async System.Threading.Tasks.Task FinishRecordingAsync(MasterDataLogger logger, string runId,
        string endReason, System.Threading.Tasks.Task startTask)
    {
        try
        {
            // End owns cancellation and settles any queued start before it
            // acknowledges that this run's native writers have finished.
            if (!string.IsNullOrEmpty(runId))
            {
                PreparationStatus = "Finishing tracker recordings…";
                try { await TrackerProcessManager.EndExperimentRecordingAsync(runId, endReason); }
                catch (System.Exception error) { RecordTransitionFailure(logger, "Tracker recording stop: " + error.GetBaseException().Message); }
            }
            else
            {
                try { await startTask; }
                catch (System.OperationCanceledException) { }
                catch (System.Exception error) { RecordTransitionFailure(logger, "Tracker recording start: " + error.GetBaseException().Message); }
            }
            NativeRecordingStopSettled = true;
            PreparationStatus = "Saving recording…";
            await logger.FinalizeSessionAsync(endReason);
            if (!string.IsNullOrEmpty(logger.FinalizationError))
                RecordingTransitionError = logger.FinalizationError;
        }
        catch (System.Exception error) { RecordTransitionFailure(logger, "Recording finalization: " + error.GetBaseException().Message); }
        finally
        {
            NativeRecordingStopSettled = true;
            SequenceStopping = false;
            PreparationStatus = null;
            preparationCancellation?.Dispose();
            preparationCancellation = null;
        }
    }

    private void RecordTransitionFailure(MasterDataLogger logger, string message)
    {
        RecordingTransitionError = message;
        if (string.IsNullOrEmpty(SequenceError)) SequenceError = message;
        try { logger.RegisterRecordingFailure(message); }
        catch (System.Exception error) { Debug.LogError("Could not retain recording failure: " + error.Message); }
        Debug.LogError(message);
    }

    public void StartSequence()
    {
        if (sequenceStarted || SequencePreparing || SequenceStopping) return;
        if (systemConfigurationError != null) { FailExperiment(new System.ArgumentException(systemConfigurationError)); return; }
        Debugger.Log("MainController.StartSequence()", 3);
        try {MasterDataLogger.Instance.ConfigArchive.RequireComplete();}
        catch(System.Exception error) {FailExperiment(error);return;}
        if (sequenceStarted) return;

        if (sequenceSteps.Count == 0)
        {
            Debug.LogError("Cannot start sequence because no sequence steps were loaded.");
            return;
        }

        var currentLogger = MasterDataLogger.Instance;
        if (recordingDirectory != currentLogger.directoryPath || string.IsNullOrEmpty(recordingRunId))
        {
            recordingDirectory = currentLogger.directoryPath;
            recordingRunId = System.Guid.NewGuid().ToString("N");
            nativeRecordingStartTask = System.Threading.Tasks.Task.CompletedTask;
            NativeRecordingStopSettled = true;
        }
        SequencePreparing = true;
        PreparationStatus = "Loading model assets…";
        RecordingTransitionError = null;
        preparationCancellation = new System.Threading.CancellationTokenSource();
        _ = PrepareAndStartSequence(++preparationGeneration);
    }

    private async System.Threading.Tasks.Task PrepareAndStartSequence(int generation)
    {
        try
        {
            // Import before starting the stimulus clock: loading a model must not shorten a trial.
            foreach (string reference in sequenceSteps.SelectMany(step => ExternalModelLibrary.CollectReferences(
                step.parameters != null && (step.parameters.ContainsKey("configFile") || step.parameters.ContainsKey("design"))
                    ? ExperimentPhases.Read(step.parameters, step.parameters.ContainsKey("configFile") ? "configFile" : "design") : null)
                .Concat(ExternalModelLibrary.CollectReferences(step.parameters != null ? JObject.FromObject(step.parameters) : null))).Distinct())
            {
                await ExternalModelLibrary.PreloadAsync(reference, ExperimentConfigFiles.ReadAllBytes(reference, "model"),
                    preparationCancellation.Token);
                if (generation != preparationGeneration) return;
            }
            if (generation != preparationGeneration) return;
            var logger = MasterDataLogger.Instance;
            if (recordingDirectory != logger.directoryPath || string.IsNullOrEmpty(recordingRunId))
            {
                recordingDirectory = logger.directoryPath;
                recordingRunId = System.Guid.NewGuid().ToString("N");
            }
            NativeRecordingStopSettled = false;
            PreparationStatus = "Starting tracker recording…";
            nativeRecordingStartTask = TrackerProcessManager.BeginExperimentRecordingAsync(
                systemConfigs.Values, recordingDirectory, recordingRunId, preparationCancellation.Token);
            await nativeRecordingStartTask;
            if (generation != preparationGeneration) return;
            ExperimentRandom.Begin(randomSeedMode, randomSeed, MasterDataLogger.Instance.directoryPath, SequenceTitle, sequenceConfigFileName);
            SequencePreparing = false;
            PreparationStatus = null;
            BeginPreparedSequence();
        }
        catch (System.Exception error) { if (generation == preparationGeneration) { SequencePreparing = false; FailExperiment(error); } }
    }

    private void BeginPreparedSequence()
    {
        sequenceStarted = true;
        SequenceError = null;
        savedHeadings.Clear();

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
        executionOrder.AddRange(BuildExecutionOrder(sequenceSteps, ExperimentRandom.Sequence));
    }

    public static List<int> BuildExecutionOrder(IList<SequenceStep> steps, System.Random rng)
    {
        var units = new List<List<int>>();
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < steps.Count; i++)
        {
            string group = steps[i].group;
            if (!string.IsNullOrWhiteSpace(group) && i > 0 && steps[i - 1].group == group) units[units.Count - 1].Add(i);
            else
            {
                if (!string.IsNullOrWhiteSpace(group) && !seen.Add(group)) throw new System.ArgumentException("Sequence group '" + group + "' must occupy consecutive rows.");
                units.Add(new List<int> { i });
            }
        }
        int n = units.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            var value = units[k]; units[k] = units[n]; units[n] = value;
        }
        return units.SelectMany(unit => unit).ToList();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debugger.Log("MainController.OnSceneLoaded()", 3);
        // Saved replay system/layout owns its additive environment, including its rig count.
        if (mode == LoadSceneMode.Additive && FindFirstObjectByType<ReplayController>() != null) return;
        if (mode == LoadSceneMode.Single) SceneTransitionPending = false;
        try { RuntimeRigRegistry.Configure(scene, systemConfigs); }
        catch (System.Exception error) { FailExperiment(error); return; }

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
            currentAutoTrimSettings.Validate();
            foreach (ClosedLoop tracking in FindObjectsByType<ClosedLoop>(FindObjectsSortMode.None))
            {
                tracking.ConfigureAutoTrim(sequenceStarted && SequenceError == null && step.autoTrim, currentAutoTrimSettings);
                tracking.AutoTrim.SetFlightCheckEnabled(CurrentFlightCheckEnabled);
            }
        }
        catch (System.Exception error)
        {
            foreach (ClosedLoop tracking in FindObjectsByType<ClosedLoop>(FindObjectsSortMode.None)) tracking.AutoTrim.Disable();
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
        if (sequenceStarted && SequenceError == null && step != null)
        {
            tracking.ConfigureAutoTrim(step.autoTrim, currentAutoTrimSettings);
            tracking.AutoTrim.SetFlightCheckEnabled(CurrentFlightCheckEnabled);
        }
    }

    public void UnregisterVRClosedLoop(string vrId, ClosedLoop tracking)
    {
        if (vrClosedLoops.TryGetValue(vrId, out ClosedLoop current) && current == tracking) vrClosedLoops.Remove(vrId);
    }

    private static readonly UnityEngine.InputSystem.Key[] animalDigitKeys = { UnityEngine.InputSystem.Key.Digit1, UnityEngine.InputSystem.Key.Digit2, UnityEngine.InputSystem.Key.Digit3, UnityEngine.InputSystem.Key.Digit4, UnityEngine.InputSystem.Key.Digit5, UnityEngine.InputSystem.Key.Digit6, UnityEngine.InputSystem.Key.Digit7, UnityEngine.InputSystem.Key.Digit8, UnityEngine.InputSystem.Key.Digit9 };
    private static readonly UnityEngine.InputSystem.Key[] animalNumberKeys = { UnityEngine.InputSystem.Key.Numpad1, UnityEngine.InputSystem.Key.Numpad2, UnityEngine.InputSystem.Key.Numpad3, UnityEngine.InputSystem.Key.Numpad4, UnityEngine.InputSystem.Key.Numpad5, UnityEngine.InputSystem.Key.Numpad6, UnityEngine.InputSystem.Key.Numpad7, UnityEngine.InputSystem.Key.Numpad8, UnityEngine.InputSystem.Key.Numpad9 };

    public void SelectNextAnimal()
    {
        int[] ids = systemConfigs.Keys.Select(RuntimeRigRegistry.ParseIndex).OrderBy(i => i).ToArray();
        if (ids.Length == 0) return;
        int at = System.Array.IndexOf(ids, selectedVRIndex);
        selectedVRIndex = ids[(at + 1) % ids.Length];
    }

    private void HandleBogongGainInput()
    {
        if (ExperimentInput.IsEditingText) return;
        var keys = UnityEngine.InputSystem.Keyboard.current;
        if (keys == null) return;
        for (int index = 0; index < animalDigitKeys.Length; index++)
            if (keys[animalDigitKeys[index]].wasPressedThisFrame || keys[animalNumberKeys[index]].wasPressedThisFrame)
                if (systemConfigs.ContainsKey("VR" + (index + 1))) selectedVRIndex = index + 1;
        if (keys.digit0Key.wasPressedThisFrame || keys.numpad0Key.wasPressedThisFrame) SelectNextAnimal();
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
        if (ExperimentInput.CancelReleased) HandleEscape();
    }

    public void HandleEscape()
    {
        if (SequencePreparing) { StopSequence("cancelled-during-model-load"); return; }
        if(SceneManager.GetActiveScene().name=="ReplayScene" && FindFirstObjectByType<ReplayController>()?.TryCloseModal()==true)return;
        if (SceneManager.GetActiveScene().name == "ControlScene")
        {
            if(ConfigurationUI.ConfigWorkbench.Instance?.TryHandleSetupEscape()==true)return;
            Application.Quit();
            return;
        }
        StopSequence();
        SceneManager.LoadScene("ControlScene");
    }

    void LoadScene(SequenceStep step)
    {
        SceneTransitionPending = true;
        phaseRunner?.Cancel();
        Debugger.Log("MainController.LoadScene()", 3);
        SyncTimestamp();

        // Clear the screen to black before loading the new scene
        ClearScreenToBlack();

        SceneManager.LoadScene(SceneCompatibility.Resolve(step.sceneName));
    }

    void SyncTimestamp()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");
    }

    void LoadSequenceConfiguration()
    {
        try {LoadSequenceConfigurationCore();}
        catch(System.Exception error)
        {
            sequenceSteps.Clear();executionOrder.Clear();autoStart=false;
            FailExperiment(error);
        }
    }

    private void LoadSequenceConfigurationCore()
    {
        sequenceSteps.Clear();
        executionOrder.Clear();
        autoStart = false;

        // Get the path to the sequence configuration JSON file
        string jsonPath = Path.Combine(RuntimePaths.ConfigRoot, sequenceConfigFileName);

        // Check if the streamingAssetsPath directory exists
        if (Directory.Exists(RuntimePaths.ConfigRoot))
        {
            // Check if the sequenceConfig.json file exists
            if (File.Exists(jsonPath))
            {
                // Read the JSON file contents
                string jsonString = ExperimentConfigFiles.ReadAllText(jsonPath,"sequence");

                // Deserialize the JSON content into a custom object
                SequenceConfig config = JsonConvert.DeserializeObject<SequenceConfig>(jsonString);

                if (config != null)
                {
                    SwarmHeadingSequenceValidation.Validate(config);
                    randomise = config.randomise; // Get the randomise parameter
                    loopSequence = config.loop; // Set looping based on config
                    LoopStartIndex = config.loopStartIndex;
                    autoStart = config.autoStart;
                    SequenceTitle = config.title;
                    randomSeedMode = config.randomSeedMode ?? "Fixed";
                    randomSeed = config.randomSeed;
                    if (!string.Equals(randomSeedMode, "Fixed", System.StringComparison.OrdinalIgnoreCase) && !string.Equals(randomSeedMode, "Random", System.StringComparison.OrdinalIgnoreCase))
                        throw new System.ArgumentException("randomSeedMode must be Fixed or Random.");

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
                        newStep.title = item.title; newStep.comments = item.comments; newStep.group = item.group;
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
    var resolved = SceneCompatibility.Parameters(GetCurrentSequenceStep()?.sceneName, ExperimentConfigFiles.ResolveReferences(parameters));
    System.Action<Dictionary<string, object>> applyConfigured = payload => {
        var step = GetCurrentSequenceStep();
        string key = payload != null && payload.ContainsKey("configFile") ? "configFile" : "design";
        JObject document = payload != null && (ExperimentPhases.IsPhase(payload) || payload.ContainsKey(key)) ? ExperimentPhases.Read(payload, key) : null;
        CurrentExperimentTitle = (string)document?["title"];
        if (string.IsNullOrWhiteSpace(CurrentExperimentTitle) && payload != null && payload.TryGetValue(key, out var reference) && reference != null)
            CurrentExperimentTitle = (string)JObject.Parse(ExperimentConfigFiles.ReadAllText(reference.ToString()))["title"];
        // Experiment/phase values take precedence; retain old sequence settings for existing recordings/configs.
        var settings = document?["autoTrimSettings"]?.ToObject<AutoTrimConfig>() ?? step?.autoTrimSettings ?? new AutoTrimConfig();
        settings.Validate();
        currentAutoTrimSettings = settings;
        flightCheckOverride = null;
        MasterDataLogger.Instance.ConfigArchive.RequireComplete();
        using (ExperimentRandom.Scope("trial:" + currentTrial + ":row:" + executionOrder[currentStep] + ":phase:" + ExperimentPhase))
        {
            var presentation = ReplayPresentationRecorder.Begin(this, document, payload);
            ExperimentVisibility.Restore(SceneManager.GetActiveScene());
            apply(payload);
            ExperimentWorldObjects.Apply(SceneManager.GetActiveScene(), document);
            ExperimentSun.Apply(SceneManager.GetActiveScene(), document);
            ExperimentSky.ApplyScene(SceneManager.GetActiveScene(), document);
            ExperimentSky.Apply(document);
            ExperimentVisibility.Apply(SceneManager.GetActiveScene(), document);
            ExperimentRandom.SeedParticles(SceneManager.GetActiveScene(), "trial:" + currentTrial + ":row:" + executionOrder[currentStep] + ":phase:" + ExperimentPhase);
            ReplayPresentationRecorder.Commit(presentation, this);
        }
        StartCoroutine(RecordingThumbnail.Capture(SceneManager.GetActiveScene(), MasterDataLogger.Instance.directoryPath));
        MasterDataLogger.Instance.ConfigArchive.RequireComplete();
        if (step != null) ApplyAutoTrimToClosedLoopComponents(step);
    };
    if (!phaseRunner.TryBegin(resolved, applyConfigured, FailExperiment)) applyConfigured(resolved);
}

public void FailRecordingEvidence(System.Exception error)
{
    var logger=MasterDataLogger.Instance;
    if(logger!=null&&logger.AcceptingWrites)
    {
        try { File.AppendAllText(Path.Combine(logger.directoryPath,"replay_evidence_errors.jsonl"),new JObject {
            ["frame"]=Time.frameCount,["presentationId"]=ReplayPresentationRecorder.CurrentPresentationId,["error"]=error.Message
        }.ToString(Newtonsoft.Json.Formatting.None)+"\n"); }
        catch(System.Exception writeError){Debug.LogError("Could not archive replay evidence failure: "+writeError.Message);}
    }
    FailExperiment(error);
}

private void FailExperiment(System.Exception error)
{
    ExperimentRandom.End();
    preparationGeneration++;
    preparationCancellation?.Cancel();
    SequencePreparing = false;
    SequenceError = error.Message;
    sequenceStarted = false;
    phaseRunner?.Cancel();
    foreach (ClosedLoop rig in vrClosedLoops.Values)
        if (rig != null) rig.AutoTrim.Disable();
    Debug.LogException(error);
    FinalizeRecording("failed");
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
        foreach (ClosedLoop rig in vrClosedLoops.Values)
        {
            var reference = rig != null ? rig.GetComponent<StimulusHeadingReference>() : null;
            if (reference != null && (reference.BlocksPresentationTimer || reference.Phase == "error")) return;
        }
        timer -= Time.deltaTime;
    }

    if (timer > 0) return;   // still running this step
    // Concurrent measurements finish in LateUpdate; keep their final frame before advancing.
    if (AssessmentPending) return;

    // ----------------------------------------------------------
    // TIME’S UP → decide how to move to the next SequenceStep
    // ----------------------------------------------------------
    currentStep++;

    // end-of-list logic (loop / quit) stays exactly as before
    if (currentStep >= sequenceSteps.Count)
    {
        if (loopSequence)
        {
            currentStep = LoopStartIndex;
            currentTrial++;
            savedHeadings.Clear(); // Never reuse a previous cycle's baseline.

            if (randomise) InitializeExecutionOrder();
        }
        else
        {
            Debugger.Log("Sequence completed and looping disabled.", 3);
            StopSequence("completed");
            if (ReturnToSetupAfterSequence)
            {
                SceneManager.LoadScene("ControlScene");
                return;
            }
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
            SceneManager.GetActiveScene().name == SceneCompatibility.Resolve(next.sceneName) &&
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
    public string title, comments, group;
    public string sceneName;
    public float duration;
    public Dictionary<string, object> parameters;
    public bool   reloadScene = true;
    public float gain = 1f;
    public bool autoTrim = false;
    public AutoTrimConfig autoTrimSettings;
    public SequenceStep(string sceneName, float duration, Dictionary<string, object> parameters, bool reloadScene = true, float gain = 1f,
                        bool autoTrim = false, AutoTrimConfig autoTrimSettings = null)
    {
        this.sceneName = sceneName;
        this.duration = duration;
        this.parameters = parameters;
        this.reloadScene = reloadScene;
        this.gain = gain;
        this.autoTrim = autoTrim;
        this.autoTrimSettings = autoTrimSettings;
    }
}

[System.Serializable]
public class SequenceConfig
{
    public string title, comments;
    public string randomSeedMode = "Fixed";
    public int randomSeed = 1;
    public int loopStartIndex = 0; // Zero-based first row to revisit after the final row; preceding rows run once.
    public void ValidateLoop()
    {
        if (loopStartIndex < 0 || (loopStartIndex > 0 && (sequences == null || loopStartIndex >= sequences.Length)))
            throw new System.ArgumentException("loopStartIndex must identify an existing zero-based sequence row.");
        if (loopStartIndex > 0 && randomise)
            throw new System.ArgumentException("A one-time sequence prefix requires randomise=false.");
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < (sequences?.Length ?? 0); i++)
        {
            var row = sequences[i];
            if (row == null) throw new System.ArgumentException("Sequence rows cannot be null.");
            if (string.IsNullOrWhiteSpace(row.group)) continue;
            bool continues = i > 0 && sequences[i - 1].group == row.group;
            if (i == loopStartIndex && continues) throw new System.ArgumentException("loopStartIndex cannot split sequence group '" + row.group + "'.");
            if (!continues && !seen.Add(row.group)) throw new System.ArgumentException("Sequence group '" + row.group + "' must occupy consecutive rows.");
        }
    }
    public bool autoStart = false;
    public bool randomise = false; // Added field
    public bool loop = true; // Added field for controlling whether the sequence should loop
    public SequenceItem[] sequences;
}

[System.Serializable]
public class SequenceItem
{
    public string title, comments, group;
    public string sceneName;
    public float duration;
    public float gain = 1f;
    public bool autoTrim = false;
    public AutoTrimConfig autoTrimSettings;
    public Dictionary<string, object> parameters;

    // NEW —— defaults to true, so legacy JSON stays valid
    public bool reloadScene = true;
}

[System.Serializable]
[JsonConverter(typeof(TrackerBackendConverter))]
public enum ClosedLoopMode { FicTrac = 0, Kinefly = 1, Tirbala = 2, Hejjetrac = 3, Rekkefly = 4 }

[System.Serializable]
public class SystemConfig
{
    public bool desktopPreview = false; // One front view fills its native-resolution desktop window.
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
    public string speciesName = "";
    public TrackerLaunchConfig tracker;
    public string displayOrder = ""; // Explicit rendering allow-list: Down, Right, Back, Left, Front, Up.
    public int targetDisplay = 1; // 0 for primary, 1 for secondary display
}

[System.Serializable]
public class SystemConfigArray
{
    public SystemConfig[] configs;
}
