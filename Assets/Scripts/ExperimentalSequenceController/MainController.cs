using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        
        // Initialize the scene via its Scene Controller Script
        //SceneController currentSceneController = FindObjectOfType<SceneController>();
        //currentSceneController.InitializeScene(step.parameters);
    }

    void SyncTimestamp()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");
        //MasterDataLogger.Instance.SetTimestamp(timestamp);
    }

    void LoadSequenceConfiguration()
    {
        // Read the sequence configuration (from a file or Editor)
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
