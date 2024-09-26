using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisibilityScript : MonoBehaviour
{
    public float cycleDuration = 4f;      // Total duration of one visibility cycle
    public float visibleDuration = 1f;    // Duration when the instance is visible
    public float phaseOffset = 0f;        // Phase offset at the start

    private Renderer[] renderers;
    private float timer;

    void Start()
    {
        // Get all Renderer components in the instance (in case it has multiple parts)
        renderers = GetComponentsInChildren<Renderer>();

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
        if (timer <= visibleDuration)
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
