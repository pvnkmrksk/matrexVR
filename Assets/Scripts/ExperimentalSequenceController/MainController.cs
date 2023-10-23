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
    private int currentStep = 0;
    private float timer;
    private bool sequenceStarted = false;
    private MasterDataLogger masterDataLogger;

    [Tooltip("0: Off, ,1: Error, 2: Warning, 3: Info, 4: Debug")]
    [SerializeField][Range(0, 4)] private int logLevel = 0; // 0: All, 1: Error, 2: Warning, 3: Info, 4: Debug

    void Awake()
    {
        // Set the log level
        Logger.CurrentLogLevel = logLevel;
        Logger.Log("MainController.Awake()", 3);

        // Make sure the MainController persists across scene changes
        DontDestroyOnLoad(this.gameObject);

        // Load the sequence configuration
        LoadSequenceConfiguration();

        // Find the MasterDataLogger component
        masterDataLogger = GetComponentInChildren<MasterDataLogger>();
        if (masterDataLogger == null)
        {
            Logger.Log("MasterDataLogger not found on the GameObject or its children.", 1);
        }
        else
        {
            Logger.Log("MasterDataLogger found on the GameObject or its children.", 1);
            Logger.Log("MasterDataLogger.directoryPath: " + masterDataLogger.directoryPath, 1);
        }
    }

    void Start()
    {
        Debug.Log("MainController.Start()");

        // Make sure the MainController persists across scene changes
        DontDestroyOnLoad(this.gameObject);

        // Load the sequence configuration
        LoadSequenceConfiguration();

        // Find the MasterDataLogger component
        masterDataLogger = GetComponentInChildren<MasterDataLogger>();
        if (masterDataLogger == null)
        {
            Debug.LogError("MasterDataLogger not found on the GameObject.");
        }
        else
        {
            Debug.Log("MasterDataLogger found on the GameObject.");
            Debug.Log("MasterDataLogger.directoryPath: " + masterDataLogger.directoryPath);
        }
    }

    public void StartSequence()
    {
        Logger.Log("MainController.StartSequence()");
        sequenceStarted = true;
        timer = sequenceSteps[currentStep].duration;  // Initialize timer for the first scene
        LoadScene(sequenceSteps[currentStep]);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Logger.Log("MainController.OnSceneLoaded()");
        SequenceStep currentStepData = sequenceSteps[currentStep];

        ISceneController currentSceneController = null;
        foreach (var obj in FindObjectsOfType<MonoBehaviour>())  // MonoBehaviour is the base class for all Unity Behaviours
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
            Logger.Log("Either the scene controller or the parameters are null.", 1);
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
        else if (Input.GetKeyUp(KeyCode.Escape))
        {
            Application.Quit();
        }
    }


    void LoadScene(SequenceStep step)
    {
        Logger.Log("MainController.LoadScene()");
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
                    foreach (SequenceItem item in config.sequences)
                    {
                        SequenceStep newStep = new SequenceStep(item.sceneName, item.duration, item.parameters);
                        sequenceSteps.Add(newStep);
                        Logger.Log("Added sequence step: " + JsonUtility.ToJson(newStep), 3);
                    }

                    // Log the loaded sequences for debugging
                    Logger.Log("Loaded sequences: " + sequenceSteps.Count, 4);

                    foreach (SequenceStep step in sequenceSteps)
                    {
                        Logger.Log("Scene Name: " + step.sceneName, 4);
                        Logger.Log("Duration: " + step.duration, 4);

                        // Log each key in the parameters dictionary for the current SequenceStep
                        if (step.parameters != null)
                        {
                            foreach (string key in step.parameters.Keys)
                            {
                                Logger.Log("Parameter Key: " + key, 4);
                            }
                        }
                    }

                    // Copy the JSON file to the data logging directory
                    string timestamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");
                    if (masterDataLogger != null)
                    {
                        string sceneName = SceneManager.GetActiveScene().name;
                        string destinationPath = Path.Combine(masterDataLogger.directoryPath, timestamp + "_" + sceneName + "_sequenceConfig.json");
                        File.Copy(jsonPath, destinationPath);
                    }
                    else
                    {
                        Logger.Log("Failed to deserialize sequence configuration JSON.", 1);
                    }
                }
                else
                {
                    Logger.Log("Failed to deserialize sequence configuration JSON.", 1);
                }
            }
            else
            {
                Logger.Log("sequenceConfig.json file not found.", 1);
            }
        }
        else
        {
            Logger.Log("StreamingAssets folder not found.", 1);
        }
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
            if (currentStep >= sequenceSteps.Count)
            {
                // End the sequence and return to the ControlScene
                SceneManager.LoadScene("ControllScene");  // Transition back to ControlScene
                Destroy(this.gameObject);  // Destroy the MainController GameObject
            }
            else
            {
                // Load the next scene
                LoadScene(sequenceSteps[currentStep]);
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
    public SequenceItem[] sequences;
}

[System.Serializable]
public class SequenceItem
{
    public string sceneName;
    public float duration;
    public Dictionary<string, object> parameters;
}