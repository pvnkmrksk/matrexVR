using UnityEngine;
public class Paradigm : BaseExperimentComponent
{
    public Paradigm(ParadigmConfig data)
    {
        Debug.Log("Loading paradigm: " + data.Name);

        // Load the experiment list
        ExperimentList experimentList = ConfigLoader.LoadConfig<ExperimentList>(data.ExperimentsConfig);

        if (experimentList == null)
        {
            Debug.LogError("Failed to load experiment list for paradigm: " + data.Name);
            return;
        }

        Debug.Log("Loaded " + experimentList.Experiments.Count + " experiments for paradigm: " + data.Name);

        // Create an Experiment for each ExperimentConfig
        foreach (var experimentData in experimentList.Experiments)
        {
            Debug.Log("Creating experiment: " + experimentData.Name);
            Experiment experiment = new Experiment(experimentData);
            components.Add(experiment);
        }
    }



    public override void StartComponent()
    {
        // Start the first experiment in the paradigm
        if (components.Count > 0)
        {
            currentIndex = 0;
            components[currentIndex].StartComponent();
        }
    }

    // Implement other methods as required for Paradigm-specific logic
}