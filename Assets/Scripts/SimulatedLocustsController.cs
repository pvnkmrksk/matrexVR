using System;
using System.Collections.Generic;
using UnityEngine;

public class SimulatedLocustsController : MonoBehaviour, ISceneController
{
    public void InitializeScene(Dictionary<string, object> parameters)
        {
            if (parameters == null) 
            {
                Debug.LogError("Parameters is null.");
                return;
            }
            
            Debug.Log("Initializing Scene with: " + parameters.ToString());

            foreach (var entry in parameters)
            {
                Debug.Log($"Parameter Key: {entry.Key}, Value: {entry.Value}, Type: {entry.Value?.GetType()}");
            }

            LocustSpawner[] locustSpawners = FindObjectsOfType<LocustSpawner>();

            if (locustSpawners == null || locustSpawners.Length == 0) 
            {
                Debug.LogWarning("No LocustSpawners found.");
                return;
            }

            foreach (LocustSpawner spawner in locustSpawners)
            {
                if (parameters.TryGetValue("numberOfLocusts", out object numberOfLocustsValue))
                {
                    spawner.numberOfLocusts = Convert.ToInt32(numberOfLocustsValue);
                }
                else
                {
                    Debug.LogWarning("Invalid or missing 'numberOfLocusts' parameter.");
                }

                if (parameters.TryGetValue("spawnAreaSize", out object spawnAreaSizeValue))
                {
                    spawner.spawnAreaSize = Convert.ToSingle(spawnAreaSizeValue);
                }
                else
                {
                    Debug.LogWarning("Invalid or missing 'spawnAreaSize' parameter.");
                }

                if (parameters.TryGetValue("mu", out object muValue))
                {
                    spawner.mu = Convert.ToSingle(muValue);
                }
                else
                {
                    Debug.LogWarning("Invalid or missing 'mu' parameter.");
                }

                if (parameters.TryGetValue("kappa", out object kappaValue))
                {
                    spawner.kappa = Convert.ToSingle(kappaValue);
                }
                else
                {
                    Debug.LogWarning("Invalid or missing 'kappa' parameter.");
                }

                if (parameters.TryGetValue("locustSpeed", out object locustSpeedValue))
                {
                    spawner.locustSpeed = Convert.ToSingle(locustSpeedValue);
                }
                else
                {
                    Debug.LogWarning("Invalid or missing 'locustSpeed' parameter.");
                }

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
