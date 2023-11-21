/* The system is designed to run experiments defined by a hierarchical structure: Paradigms, Experiments, and Trials, driven by JSON configurations.

Key Components:

BaseExperimentComponent: A non-MonoBehaviour C# class that serves as the base for Paradigms, Experiments, and Trials. It handles the logical flow and navigation between components.
ExperimentManager (MonoBehaviour): Attached to a GameObject, this script manages the starting and transitioning of experiments and trials. It handles Unity-specific functionalities like scene transitions and timing.
Paradigm, Experiment, Trial Classes: Inherit from BaseExperimentComponent. They represent the hierarchical structure of the experiments.
JSON Configuration: Defines the structure of paradigms, experiments, and trials, including scene names and durations.
Plan of Action:

Loading JSON Configurations:
The ExperimentManager script loads JSON data to initialize paradigms and experiments.
The JSON file should define the entire structure and sequence of the experiments.
Initializing Components:
Paradigms and Experiments are initialized with data from the JSON file.
Each Paradigm contains Experiments, and each Experiment contains Trials.
Managing Experiment Flow:
ExperimentManager starts the first Paradigm.
Control then passes through Experiments and Trials, following the defined sequence.
Timing and Scene Transitions:
ExperimentManager manages trial durations and transitions between trials.
Unity's coroutines (StartCoroutine) handle timing.
User Inputs for Navigation:
Implement keyboard inputs in ExperimentManager to navigate between trials and experiments for testing and manual control.
Scene Setup:
Attach ExperimentManager to a GameObject in the main scene.
Create necessary scenes for trials as defined in the JSON configuration.
 */
// ExperimentManager.cs

using UnityEngine;
using System.Collections;

public class ExperimentManager : MonoBehaviour
{
    public static ExperimentManager Instance { get; private set; }

    private BaseExperimentComponent currentComponent;

    void Awake()
    {
        // Singleton pattern to ensure only one instance exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetAndStartComponent(BaseExperimentComponent component)
    {
        currentComponent = component;
        currentComponent.StartComponent();
    }

    void Start()
    {
        // Load configuration and start the first component
        ParadigmList paradigmList = ConfigLoader.LoadConfig<ParadigmList>("ExampleConfig");
        if (paradigmList != null && paradigmList.Paradigms.Count > 0)
        {
            Debug.Log("Loaded " + paradigmList.Paradigms.Count + " paradigms.");
            ParadigmConfig firstParadigmConfig = paradigmList.Paradigms[0];
            Debug.Log("Starting first paradigm: " + firstParadigmConfig.Name);

            // Create a new Paradigm with the first ParadigmConfig
            Paradigm firstParadigm = new Paradigm(firstParadigmConfig);
            SetAndStartComponent(firstParadigm);
        }
        else
        {
            Debug.LogError("Failed to load paradigms from configuration.");
        }
    }
    // Method to start a Coroutine from non-MonoBehaviour classes
    public Coroutine StartExperimentCoroutine(IEnumerator coroutine)
    {
        return StartCoroutine(coroutine);
    }
}
