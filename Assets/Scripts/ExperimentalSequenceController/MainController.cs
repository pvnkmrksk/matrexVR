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

    void Start()
    {
        DontDestroyOnLoad(this.gameObject);
        LoadSequenceConfiguration();
        LoadScene(sequenceSteps[currentStep]);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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
            Debug.LogError("Either the scene controller or the parameters are null.");
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Update()
    {
        ManageTimerAndTransitions();
    }

    void LoadScene(SequenceStep step)
    {
        SyncTimestamp();
        SceneManager.LoadScene(step.sceneName);
    }

    void SyncTimestamp()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");
    }

     void LoadSequenceConfiguration()
    {
        string jsonPath = Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json");
        string jsonString = File.ReadAllText(jsonPath);
        
        // Deserialize the JSON content into a custom object
        SequenceConfig config = JsonConvert.DeserializeObject<SequenceConfig>(jsonString);

        foreach (SequenceItem item in config.sequences)
        {
            SequenceStep newStep = new SequenceStep(item.sceneName, item.duration, item.parameters);
            sequenceSteps.Add(newStep);
            Debug.Log("Added sequence step: " + JsonUtility.ToJson(newStep));
        }

        // Log the loaded sequences for debugging
        Debug.Log("Loaded sequences: " + sequenceSteps.Count);
        
        foreach (SequenceStep step in sequenceSteps)
        {
            Debug.Log("Scene Name: " + step.sceneName);
            Debug.Log("Duration: " + step.duration);

            // Log each key in the parameters dictionary for the current SequenceStep
            foreach (string key in step.parameters.Keys)
            {
                Debug.Log("Parameter Key: " + key);
            }
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
                SceneManager.LoadScene("EndScene");  // Transition back to ControlScene
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