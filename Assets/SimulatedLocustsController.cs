using System.Collections.Generic;
using UnityEngine;

public class SimulatedLocustsController : MonoBehaviour, ISceneController
{
    public void InitializeScene(Dictionary<string, object> parameters)
    {
        // Check if parameters is null
        if (parameters == null) 
        {
            Debug.LogError("Parameters is null.");
            return;
        }
        
        Debug.Log("Initializing Scene with: " + parameters.ToString());

        // Debug log the values of parameters
        foreach (var entry in parameters)
        {
            Debug.Log($"Parameter Key: {entry.Key}, Value: {entry.Value}");
        }
        
        // Find all GameObjects with the LocustSpawner script
        LocustSpawner[] locustSpawners = FindObjectsOfType<LocustSpawner>();
        
        // Check if locustSpawners is null or empty
        if (locustSpawners == null || locustSpawners.Length == 0) 
        {
            Debug.LogWarning("No LocustSpawners found.");
            return;
        }

        foreach (LocustSpawner spawner in locustSpawners)
        {
            // Update parameters from the provided dictionary

            if (parameters.TryGetValue("numberOfLocusts", out object numberOfLocustsValue) && numberOfLocustsValue is int)
            {
                spawner.numberOfLocusts = (int)numberOfLocustsValue;
            }
            else
            {
                Debug.LogWarning("Invalid or missing 'numberOfLocusts' parameter.");
                
            }

            if (parameters.TryGetValue("spawnAreaSize", out object spawnAreaSizeValue) && spawnAreaSizeValue is float)
            {
                spawner.spawnAreaSize = (float)spawnAreaSizeValue;
            }
            else
            {
                Debug.LogWarning("Invalid or missing 'spawnAreaSize' parameter.");
            }

            if (parameters.TryGetValue("mu", out object muValue) && muValue is float)
            {
                spawner.mu = (float)muValue;
            }
            else
            {
                Debug.LogWarning("Invalid or missing 'mu' parameter.");
            }

            if (parameters.TryGetValue("kappa", out object kappaValue) && kappaValue is float)
            {
                spawner.kappa = (float)kappaValue;
            }
            else
            {
                Debug.LogWarning("Invalid or missing 'kappa' parameter.");
            }

            if (parameters.TryGetValue("locustSpeed", out object locustSpeedValue) && locustSpeedValue is float)
            {
                spawner.locustSpeed = (float)locustSpeedValue;
            }
            else
            {
                Debug.LogWarning("Invalid or missing 'locustSpeed' parameter.");
            }

            // You can continue for other parameters you wish to control
            Debug.Log("Initializing SimulatedLocusts scene with parameters: " + parameters.ToString());
        }
    }


    public void StartDataLogging(string timestamp)
    {
        // Implement data logging here, if necessary
    }

    void Update()
    {
        // Implement scene-specific logic here, if any
    }
}
