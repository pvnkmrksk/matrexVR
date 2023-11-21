//scenemanager import
using UnityEngine.SceneManagement;
public class Trial : BaseExperimentComponent
{
    public string SceneName { get; set; }
    public int Repetitions { get; set; }

    public Trial(string sceneName, int repetitions) // Modified constructor
    {
        SceneName = sceneName;
        Repetitions = repetitions;
    }

    public void Initialize()
    {
        // Initialization code goes here
    }

    public void Cleanup()
    {
        // Cleanup code goes here
    }

    public void HandleEvent()
    {
        // Event handling code goes here
    }

    public void RecordData()
    {
        // Data recording code goes here
    }

    public override void StartComponent()
    {
        // Run the trial the specified number of times
        for (int i = 0; i < Repetitions; i++)
        {
            Initialize();
            SceneManager.LoadScene(SceneName);
            HandleEvent();
            RecordData();
            Cleanup();
        }
    }

    // Other methods as needed...
}