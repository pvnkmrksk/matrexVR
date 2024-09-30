using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisibilityScript : MonoBehaviour
{
    public float visibleOffDuration = 4f;      // Total duration of one visibility cycle
    public float visibleOnDuration = 1f;    // Duration when the instance is visible

    private float cycleDuration;
    [Tooltip("Phase offset duration between 0 and total duration in seconds.")]    public float phaseOffset = 0f;        // Phase offset at the start in seconds

    private Renderer[] renderers;
    private float timer;

    void Start()
    {
        //todo add duty cycle instead of durations
        // Get all Renderer components in the instance (in case it has multiple parts)
        renderers = GetComponentsInChildren<Renderer>();
        cycleDuration=visibleOnDuration+visibleOffDuration;

        // Initialize the timer with the phase offset
        timer = phaseOffset % cycleDuration;
    }

    void Update()
    {
        // Increment timer
        timer += Time.deltaTime;

        // Loop the timer back to 0 after the cycle duration
        if (timer > cycleDuration)
        {
            timer -= cycleDuration;
        }

        // Determine visibility
        if (timer <= visibleOnDuration)
        {
            SetVisibility(true);
        }
        else
        {
            SetVisibility(false);
        }
    }

    void SetVisibility(bool isVisible)
    {
        foreach (Renderer rend in renderers)
        {
            rend.enabled = isVisible;
        }
    }
}
