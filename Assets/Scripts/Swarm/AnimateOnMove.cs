using UnityEngine;

/// <summary>
/// Controls animation based on movement speed with noise threshold to prevent glitchy start/stop.
/// </summary>
public class AnimateOnMove : MonoBehaviour
{
    private Animator animator;
    private Vector3 lastPosition;
    private float currentSpeed = 0f;
    private bool isAnimating = false;
    
    [SerializeField]
    private float noiseThreshold = 0.1f; // Minimum speed (units per second) to start animation
    [SerializeField]
    private float stopThreshold = 0.05f; // Lower threshold to stop (hysteresis to prevent glitching)
    [SerializeField]
    private float smoothingFactor = 0.1f; // Smoothing factor for speed calculation
    
    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning($"AnimateOnMove: No Animator found on {gameObject.name}");
            enabled = false;
            return;
        }
        
        lastPosition = transform.position;
        
        // Start with animation disabled if animateOnMove is enabled
        if (animator != null)
        {
            animator.speed = 0f; // Stop animation initially
        }
    }
    
    void Update()
    {
        if (animator == null) return;
        
        // Calculate smoothed speed
        Vector3 positionDelta = transform.position - lastPosition;
        float frameSpeed = positionDelta.magnitude / Time.deltaTime;
        currentSpeed = Mathf.Lerp(currentSpeed, frameSpeed, smoothingFactor);
        
        // Hysteresis: different thresholds for start and stop to prevent glitching
        if (!isAnimating && currentSpeed > noiseThreshold)
        {
            // Start animation
            isAnimating = true;
            animator.speed = 1f;
        }
        else if (isAnimating && currentSpeed < stopThreshold)
        {
            // Stop animation (using lower threshold for smooth stop)
            isAnimating = false;
            animator.speed = 0f;
        }
        
        lastPosition = transform.position;
    }
    
    public void SetNoiseThreshold(float threshold)
    {
        // Threshold is in units per second (speed)
        noiseThreshold = threshold;
        stopThreshold = threshold * 0.5f; // Stop threshold is half of start threshold (hysteresis)
    }
    
    public void SetEnabled(bool enabled)
    {
        this.enabled = enabled;
        if (animator != null)
        {
            animator.speed = enabled ? (isAnimating ? 1f : 0f) : 1f; // If disabled, always animate
        }
    }
}

