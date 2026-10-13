using System.Collections.Generic;
using UnityEngine;

/// <summary>Advance the opt-in periodic swarm after observer/AGL movement, before rendering and logging.</summary>
[DefaultExecutionOrder(500)]
public sealed class GeneratedSwarmMotion : MonoBehaviour
{
    private LocustMover[] movers;
    public void Configure(IReadOnlyList<GameObject> agents)
    {
        movers = new LocustMover[agents.Count];
        for (int i = 0; i < agents.Count; i++)
        {
            movers[i] = agents[i].GetComponent<LocustMover>();
            if (movers[i] != null) movers[i].enabled = false;
        }
    }
    private void LateUpdate() => Advance(Time.deltaTime);
    public void Advance(float seconds)
    {
        if (movers == null) return;
        for (int i = 0; i < movers.Length; i++)
            if (movers[i] != null && movers[i].gameObject.activeInHierarchy) movers[i].Advance(seconds);
    }
    public static float SampleSpeed(float mean, float standardDeviation)
    {
        if (standardDeviation == 0) return mean; // Do not alter the historical random sequence.
        if (float.IsNaN(mean) || float.IsInfinity(mean) || mean < 0 ||
            float.IsNaN(standardDeviation) || float.IsInfinity(standardDeviation) || standardDeviation < 0)
            throw new System.ArgumentException("Generated speed distribution requires a finite nonnegative mean and positive standard deviation.");
        float speed;
        do
        {
            float normal = Mathf.Sqrt(-2f * Mathf.Log(Mathf.Max(1e-7f, Random.value))) * Mathf.Cos(2f * Mathf.PI * Random.value);
            speed = mean + standardDeviation * normal;
        } while (speed < 0);
        return speed;
    }
}
