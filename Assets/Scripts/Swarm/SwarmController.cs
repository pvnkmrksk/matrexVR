using System;
using System.Collections.Generic;
using UnityEngine;

public class SwarmController : MonoBehaviour, ISceneController
{
    public void InitializeScene(Dictionary<string, object> parameters)
    {
        if (parameters == null)
        {
            Debugger.Log("Parameters is null.", 1);
            return;
        }

        Debugger.Log("Initializing Scene with: " + parameters.ToString());

        foreach (var entry in parameters)
        {
            Debugger.Log(
                $"Parameter Key: {entry.Key}, Value: {entry.Value}, Type: {entry.Value?.GetType()}"
            );
        }

        LocustSpawner[] locustSpawners = FindObjectsOfType<LocustSpawner>();

        if (locustSpawners == null || locustSpawners.Length == 0)
        {
            Debugger.Log("No LocustSpawners found.", 2);
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
                Debugger.Log("Invalid or missing 'numberOfLocusts' parameter.", 2);
            }

            if (parameters.TryGetValue("spawnAreaSize", out object spawnAreaSizeValue))
            {
                spawner.spawnAreaSize = Convert.ToSingle(spawnAreaSizeValue);
            }
            else
            {
                Debugger.Log("Invalid or missing 'spawnAreaSize' parameter.", 2);
            }

            if (parameters.TryGetValue("mu", out object muValue))
            {
                spawner.mu = Convert.ToSingle(muValue);
            }
            else
            {
                Debugger.Log("Invalid or missing 'mu' parameter.", 2);
            }

            if (parameters.TryGetValue("kappa", out object kappaValue))
            {
                spawner.kappa = Convert.ToSingle(kappaValue);
            }
            else
            {
                Debugger.Log("Invalid or missing 'kappa' parameter.", 2);
            }

            if (parameters.TryGetValue("locustSpeed", out object locustSpeedValue))
            {
                spawner.locustSpeed = Convert.ToSingle(locustSpeedValue);
            }
            else
            {
                Debugger.Log("Invalid or missing 'locustSpeed' parameter.", 2);
            }

            Debugger.Log("Initializing Swarm scene with parameters: " + parameters.ToString());
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
