using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using Newtonsoft.Json;

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

    [Tooltip("0: Off, ,1: Error, 2: Warning, 3: Info, 4: Debug")]
    [SerializeField]
    [Range(0, 4)]
    private int logLevel = 0; // 0: All, 1: Error, 2: Warning, 3: Info, 4: Debug
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

    void Start()
    {
        // Set the log level
        Debugger.CurrentLogLevel = logLevel;
        Debugger.Log("MainController.Start()", 3);

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

        // Load the sequence configuration
        LoadSequenceConfiguration();
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
        SequenceStep currentStepData = sequenceSteps[executionOrder[currentStep]];

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
            currentSceneController.InitializeScene(currentStepData.parameters);
            timer = currentStepData.duration;
        }
        else
        {
            Debugger.Log("Either the scene controller or the parameters are null.", 2);
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Update()
    {
        if (sequenceStarted)
        {
            ManageTimerAndTransitions();
        }

        // if esc is pressed, quit
        if (Input.GetKeyUp(KeyCode.Escape))
        {
            Application.Quit();
        }
    }

    void LoadScene(SequenceStep step)
    {
        Debugger.Log("MainController.LoadScene()", 3);
        SyncTimestamp();
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

                    foreach (SequenceItem item in config.sequences)
                    {
                        SequenceStep newStep = new SequenceStep(
                            item.sceneName,
                            item.duration,
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
                    // End the sequence and return to the ControlScene
                    SceneManager.LoadScene("ControlScene"); // Transition back to ControlScene
                    Destroy(this.gameObject); // Destroy the MainController GameObject
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
}

[System.Serializable]
public class SequenceStep
{
    public string sceneName;
    public float duration;
    public Dictionary<string, object> parameters;

    public SequenceStep(string sceneName, float duration, Dictionary<string, object> parameters)
    {
        this.sceneName = sceneName;
        this.duration = duration;
        this.parameters = parameters;
    }
}

[System.Serializable]
public class SequenceConfig
{
    public bool randomise = false; // Added field
    public SequenceItem[] sequences;
}

[System.Serializable]
public class SequenceItem
{
    public string sceneName;
    public float duration;
    public Dictionary<string, object> parameters;
}