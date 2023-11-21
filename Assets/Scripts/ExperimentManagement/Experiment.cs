using UnityEngine;
public class Experiment : BaseExperimentComponent
{
    public Experiment(ExperimentConfig data)
    {
        // Use the config data directly
        if (data == null)
        {
            Debug.LogError("Failed to load experiment config");
            return;
        }

        // Instantiate a single Trial with the scene name and repetitions
        Trial trial = new Trial(data.SceneName, data.TrialRepetitions);
        components.Add(trial);
    }

    public override void StartComponent()
    {
        // Start the first trial in the experiment
        if (components.Count > 0)
        {
            currentIndex = 0;
            components[currentIndex].StartComponent();
        }
    }

    // Rest of the class...
}