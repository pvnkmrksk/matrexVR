// BaseExperimentComponent.cs

// BaseExperimentComponent.cs

using UnityEngine;
using System.Collections; // Add this line
using System.Collections.Generic;

public abstract class BaseExperimentComponent
{
    protected int currentIndex = 0;
    protected List<BaseExperimentComponent> components = new List<BaseExperimentComponent>();

    // Properties for stimulus durations, etc.
    public float PreStimulusDuration { get; set; }
    public float PostStimulusDuration { get; set; }
    public bool IsPreStimulusActive { get; protected set; }
    public bool IsPostStimulusActive { get; protected set; }

    public abstract void StartComponent();

    public virtual void NextComponent()
    {
        if (currentIndex < components.Count - 1)
        {
            currentIndex++;
            components[currentIndex].StartComponent();
        }
    }

    public virtual void PreviousComponent()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            components[currentIndex].StartComponent();
        }
    }

    protected virtual void InitializeTimers()
    {
        // Start the pre-stimulus timer
        IsPreStimulusActive = true;
        ExperimentManager.Instance.StartCoroutine(StartPreStimulusTimer());
    }

    private IEnumerator StartPreStimulusTimer()
    {
        yield return new WaitForSeconds(PreStimulusDuration);
        EndPreStimulus();
    }

    protected virtual void EndPreStimulus()
    {
        IsPreStimulusActive = false;
        IsPostStimulusActive = true;
        // Start the post-stimulus timer
        ExperimentManager.Instance.StartCoroutine(StartPostStimulusTimer());
    }

    private IEnumerator StartPostStimulusTimer()
    {
        yield return new WaitForSeconds(PostStimulusDuration);
        EndPostStimulus();
    }

    protected virtual void EndPostStimulus()
    {
        IsPostStimulusActive = false;
        // Implement any actions that need to happen at the end of the post-stimulus phase
        // For example, moving to the next component
        NextComponent();
    }

    // Additional methods as required for your specific experiment setup...
}
