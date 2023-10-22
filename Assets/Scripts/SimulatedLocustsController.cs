using System;
using System.Collections.Generic;
using UnityEngine;

public class SimulatedLocustsController : MonoBehaviour, ISceneController
{
    public void InitializeScene(Dictionary<string, object> parameters)
    {
        if (parameters == null)
        {
            Logger.Log("Parameters is null.", 1);
            return;
        }

        Logger.Log("Initializing Scene with: " + parameters.ToString());

        foreach (var entry in parameters)
        {
            Logger.Log($"Parameter Key: {entry.Key}, Value: {entry.Value}, Type: {entry.Value?.GetType()}");
        }

        LocustSpawner[] locustSpawners = FindObjectsOfType<LocustSpawner>();

        if (locustSpawners == null || locustSpawners.Length == 0)
        {
            Logger.Log("No LocustSpawners found.", 2);
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
                Logger.Log("Invalid or missing 'numberOfLocusts' parameter.", 2);
            }

            if (parameters.TryGetValue("spawnAreaSize", out object spawnAreaSizeValue))
            {
                spawner.spawnAreaSize = Convert.ToSingle(spawnAreaSizeValue);
            }
            else
            {
                Logger.Log("Invalid or missing 'spawnAreaSize' parameter.", 2);
            }

            if (parameters.TryGetValue("mu", out object muValue))
            {
                spawner.mu = Convert.ToSingle(muValue);
            }
            else
            {
                Logger.Log("Invalid or missing 'mu' parameter.", 2);
            }

            if (parameters.TryGetValue("kappa", out object kappaValue))
            {
                spawner.kappa = Convert.ToSingle(kappaValue);
            }
            else
            {
                Logger.Log("Invalid or missing 'kappa' parameter.", 2);
            }

            if (parameters.TryGetValue("locustSpeed", out object locustSpeedValue))
            {
                spawner.locustSpeed = Convert.ToSingle(locustSpeedValue);
            }
            else
            {
                Logger.Log("Invalid or missing 'locustSpeed' parameter.", 2);
            }

            Logger.Log("Initializing SimulatedLocusts scene with parameters: " + parameters.ToString());
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
