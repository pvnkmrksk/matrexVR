using System.Collections.Generic;
using UnityEngine;

public class SimulatedLocustsController : MonoBehaviour
{
    public void InitializeScene(Dictionary<string, object> parameters)
    {
        // Find all GameObjects with the LocustSpawner script
        LocustSpawner[] locustSpawners = FindObjectsOfType<LocustSpawner>();

        foreach (LocustSpawner spawner in locustSpawners)
        {
            // Update parameters from the provided dictionary
            if (parameters.ContainsKey("numberOfLocusts"))
            {
                spawner.numberOfLocusts = (int)parameters["numberOfLocusts"];
            }
            
            if (parameters.ContainsKey("spawnAreaSize"))
            {
                spawner.spawnAreaSize = (float)parameters["spawnAreaSize"];
            }
            
            if (parameters.ContainsKey("mu"))
            {
                spawner.mu = (float)parameters["mu"];
            }

            if (parameters.ContainsKey("kappa"))
            {
                spawner.kappa = (float)parameters["kappa"];
            }

            if (parameters.ContainsKey("locustSpeed"))
            {
                spawner.locustSpeed = (float)parameters["locustSpeed"];
            }
            
            // You can continue for other parameters you wish to control
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
