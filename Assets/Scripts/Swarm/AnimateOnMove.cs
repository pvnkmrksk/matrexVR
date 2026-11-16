using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Translation-only animation gate, measured in world units per second (cm/s in this project).</summary>
[DefaultExecutionOrder(510)]
public class AnimateOnMove : MonoBehaviour
{
    private const double SpeedWindowSeconds = 0.30;
    private const float ReferenceWalkingSpeed = 2f; // World units per second at the authored clip rate.

    [SerializeField, FormerlySerializedAs("noiseThreshold")] private float speedThreshold = 0.5f;
    private Animator animator;
    private Animation legacyAnimation;
    private readonly Dictionary<string, float> legacySpeeds = new Dictionary<string, float>();
    private Transform movementSource;
    private Vector3 lastPosition;
    private Vector2 periodicSize;
    private double elapsed;
    private double2 unwrappedPosition;
    private Queue<(double time, double2 position)> positionHistory;
    private (double time, double2 position) beforeWindow;
    private float basePlaybackSpeed = 1f;
    private bool configured;
    private bool externalSampling, walking;
    private StockLocustPlayback clipPlayback;
    private int clipIndex;
    public float CurrentSpeed { get; private set; }

    public static void ConfigureHierarchy(GameObject root, Transform source, bool gate, float threshold, Vector2 wrapSize)
    {
        foreach (Animator target in root.GetComponentsInChildren<Animator>(true))
            ConfigureTarget(target, source, gate, threshold, wrapSize);
        foreach (Animation target in root.GetComponentsInChildren<Animation>(true))
            ConfigureTarget(target, source, gate, threshold, wrapSize);
    }
    private static void ConfigureTarget(Component target, Transform source, bool gate, float threshold, Vector2 wrapSize)
    {
            AnimateOnMove control = target.GetComponent<AnimateOnMove>();
            if (control == null && !gate) return;
            if (control == null) control = target.gameObject.AddComponent<AnimateOnMove>();
            control.Configure(source, gate, threshold, wrapSize);
    }
    public void Configure(Transform source, bool gate, float threshold, Vector2 wrapSize)
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator != null && animator.speed > 0) basePlaybackSpeed = animator.speed;
        }
        if (legacyAnimation == null)
        {
            legacyAnimation = GetComponent<Animation>();
            if (legacyAnimation != null)
                foreach (AnimationState state in legacyAnimation) legacySpeeds[state.name] = state.speed;
        }
        externalSampling = false;
        movementSource = source != null ? source : transform;
        speedThreshold = Mathf.Max(0, threshold);
        periodicSize = wrapSize;
        ResetMeasurement();
        configured = true;
        enabled = gate;
        SetPlaybackMultiplier(gate ? 0f : 1f);
    }
    private void Start()
    {
        if (!configured) Configure(transform, true, speedThreshold, Vector2.zero);
    }
    private void LateUpdate() => MeasureTranslation(Time.deltaTime);

    private void ResetMeasurement()
    {
        lastPosition = movementSource != null ? movementSource.position : transform.position;
        elapsed = 0;
        unwrappedPosition = default;
        beforeWindow = default;
        positionHistory?.Clear();
        CurrentSpeed = 0;
        walking = false;
    }

    private void MeasureTranslation(float deltaTime)
    {
        if (!enabled || (animator == null && legacyAnimation == null) || movementSource == null) return;
        if (deltaTime <= 0)
        {
            ResetMeasurement();
            SetPlaybackMultiplier(0f);
            return;
        }

        Vector3 position = movementSource.position;
        Vector3 delta = position - lastPosition;
        lastPosition = position;
        // Unwrap each step before accumulating signed horizontal movement so jitter cancels.
        if (periodicSize.x > 0 && Mathf.Abs(delta.x) >= periodicSize.x / 2f)
            delta.x = Kannadi.Wrap(delta.x, periodicSize.x);
        if (periodicSize.y > 0 && Mathf.Abs(delta.z) >= periodicSize.y / 2f)
            delta.z = Kannadi.Wrap(delta.z, periodicSize.y);
        unwrappedPosition += new double2(delta.x, delta.z);
        elapsed += deltaTime;
        positionHistory ??= new Queue<(double time, double2 position)>();
        positionHistory.Enqueue((elapsed, unwrappedPosition));

        double windowStart = elapsed - SpeedWindowSeconds;
        while (positionHistory.Peek().time <= windowStart)
            beforeWindow = positionHistory.Dequeue();

        // Interpolate at the time cutoff. Before the first sample, assume the source was at rest.
        double2 startPosition = beforeWindow.position;
        if (windowStart > beforeWindow.time)
        {
            var afterWindowStart = positionHistory.Peek();
            double fraction = (windowStart - beforeWindow.time) / (afterWindowStart.time - beforeWindow.time);
            startPosition = math.lerp(startPosition, afterWindowStart.position, fraction);
        }
        CurrentSpeed = (float)(math.length(unwrappedPosition - startPosition) / SpeedWindowSeconds);
        SetPlaybackMultiplier(CurrentSpeed > speedThreshold ? CurrentSpeed / ReferenceWalkingSpeed : 0f);
    }

    private void SetPlaybackMultiplier(float multiplier)
    {
        float speed = basePlaybackSpeed * multiplier;
        if (clipPlayback != null) clipPlayback.SetSpeed(clipIndex, speed);
        else if (animator != null) animator.speed = speed;
        if (legacyAnimation != null)
            foreach (AnimationState state in legacyAnimation)
                state.speed = legacySpeeds.TryGetValue(state.name, out float legacySpeed) ? legacySpeed * multiplier : 0;
    }
    internal void AttachClipPlayback(StockLocustPlayback playback, int index)
    {
        clipPlayback = playback;
        clipIndex = index;
    }

    internal void DetachClipPlayback(StockLocustPlayback playback)
    {
        if (clipPlayback == playback) clipPlayback = null;
    }

    internal bool CanSampleExternally => configured && enabled && animator != null &&
        legacyAnimation == null && movementSource == transform;

    internal void BeginExternalSampling()
    {
        externalSampling = true;
        ResetMeasurement();
        enabled = false;
        SetPlaybackMultiplier(0f);
    }

    internal void ApplySampledSpeed(float speed)
    {
        if (!externalSampling) return;
        CurrentSpeed = speed;
        // Start above the configured threshold. Stop at 80% to avoid chattering.
        bool moving = speed > speedThreshold * (walking ? .8f : 1f);
        if (!moving && !walking) return;
        walking = moving;
        SetPlaybackMultiplier(walking ? speed / ReferenceWalkingSpeed : 0f);
    }

    internal void EndExternalSampling()
    {
        if (!externalSampling) return;
        externalSampling = false;
        ResetMeasurement();
        SetPlaybackMultiplier(0f);
        enabled = true;
    }

    private void OnEnable()
    {
        if (!configured || externalSampling) return;
        ResetMeasurement();
        SetPlaybackMultiplier(0f);
    }

    private void OnDisable()
    {
        if (!externalSampling) SetPlaybackMultiplier(1f);
    }
    // Legacy public setters retained for existing callers.
    public void SetNoiseThreshold(float threshold) => speedThreshold = Mathf.Max(0, threshold);
    public void SetEnabled(bool value) => Configure(movementSource, value, speedThreshold, periodicSize);
}
