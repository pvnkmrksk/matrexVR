using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public class MainController : MonoBehaviour
{
    // List to hold sequence steps
    public List<SequenceStep> sequenceSteps = new List<SequenceStep>();
    
    // Variable to keep track of current step
    private int currentStep = 0;
    
    // Timer
    private float timer;

    // Start is called before the first frame update
    void Start()
    {
        DontDestroyOnLoad(this.gameObject);  // This line ensures the GameObject persists
        // Load initial sequence configuration
        LoadSequenceConfiguration();
        
        // Initialize the first scene
        LoadScene(sequenceSteps[currentStep]);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SimulatedLocustsController currentSceneController = FindObjectOfType<SimulatedLocustsController>();
        if (currentSceneController != null) {
            Debug.Log("Found the controller after scene loaded.");
            currentSceneController.InitializeScene(sequenceSteps[currentStep].parameters);
        } else {
            Debug.Log("Controller still not found after scene loaded.");
        }
    }
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;  // Unsubscribe from sceneLoaded event
    }

    // Update is called once per frame
    void Update()
    {
        // Handle timer and scene transitions
        ManageTimerAndTransitions();
        
        // Optionally: Handle real-time adjustments
    }

    void LoadScene(SequenceStep step)
    {
        // Generate and synchronize timestamp
        SyncTimestamp();
        
        // Load the scene
        SceneManager.LoadScene(step.sceneName);
    }

    void SyncTimestamp()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");
        //MasterDataLogger.Instance.SetTimestamp(timestamp);
    }

    void LoadSequenceConfiguration()
    {
        string jsonPath = Application.dataPath + "/Config/sequenceConfig.json";
        string jsonString = File.ReadAllText(jsonPath);
        Debug.Log("Loaded Sequence: " + jsonString);  // In LoadSequenceConfiguration

        SequenceConfig config = JsonUtility.FromJson<SequenceConfig>(jsonString);

        foreach (SequenceItem item in config.sequences)
        {
            sequenceSteps.Add(new SequenceStep(item.sceneName, item.duration, item.parameters));
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
                // Loop or end sequence here
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
