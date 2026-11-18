using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Translation-only animation gate, measured in world units per second (cm/s in this project).</summary>
[DefaultExecutionOrder(510)]
public class AnimateOnMove : MonoBehaviour
{
    internal const float WalkingStopThresholdRatio = .8f;
    private const float ReferenceWalkingSpeed = 2f; // World units per second at the authored clip rate.

    internal sealed class TranslationSpeed
    {
        private const double SpeedWindowSeconds = 0.30;
        private Vector3 lastPosition;
        private Vector2 periodicSize;
        private double elapsed;
        private double2 unwrappedPosition;
        private Queue<(double time, double2 position)> positionHistory;
        private (double time, double2 position) beforeWindow;

        internal float Speed { get; private set; }
        internal bool IsWalking { get; private set; }

        internal TranslationSpeed(Vector3 position, Vector2 wrapSize) => Reset(position, wrapSize);

        internal void Reset(Vector3 position, Vector2 wrapSize)
        {
            lastPosition = position;
            periodicSize = wrapSize;
            elapsed = 0;
            unwrappedPosition = default;
            beforeWindow = default;
            positionHistory?.Clear();
            Speed = 0;
            IsWalking = false;
        }

        internal void Sample(Vector3 position, float deltaTime, float threshold, bool useHysteresis = false)
        {
            if (deltaTime <= 0)
            {
                Reset(position, periodicSize);
                return;
            }

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
            Speed = (float)(math.length(unwrappedPosition - startPosition) / SpeedWindowSeconds);
            IsWalking = IsAboveThreshold(Speed, threshold, useHysteresis && IsWalking);
        }
    }

    [SerializeField, FormerlySerializedAs("noiseThreshold")] private float speedThreshold = 0.5f;
    private Animator animator;
    private Animation legacyAnimation;
    private readonly Dictionary<string, float> legacySpeeds = new Dictionary<string, float>();
    private Transform movementSource;
    private Vector2 periodicSize;
    private TranslationSpeed localSpeed, sharedSpeed;
    private float basePlaybackSpeed = 1f;
    private bool configured;
    private bool externalSampling, walking;
    private StockLocustPlayback clipPlayback;
    private int clipIndex;
    public float CurrentSpeed { get; private set; }

    public static void ConfigureHierarchy(GameObject root, Transform source, bool gate, float threshold, Vector2 wrapSize) =>
        ConfigureHierarchy(root, source, gate, threshold, wrapSize, null);

    internal static void ConfigureHierarchy(GameObject root, Transform source, bool gate, float threshold, Vector2 wrapSize,
        TranslationSpeed sharedSpeed)
    {
        foreach (Animator target in root.GetComponentsInChildren<Animator>(true))
            ConfigureTarget(target, source, gate, threshold, wrapSize, sharedSpeed);
        foreach (Animation target in root.GetComponentsInChildren<Animation>(true))
            ConfigureTarget(target, source, gate, threshold, wrapSize, sharedSpeed);
    }
    private static void ConfigureTarget(Component target, Transform source, bool gate, float threshold, Vector2 wrapSize,
        TranslationSpeed sharedSpeed)
    {
        AnimateOnMove control = target.GetComponent<AnimateOnMove>();
        if (control == null && !gate) return;
        if (control == null) control = target.gameObject.AddComponent<AnimateOnMove>();
        control.Configure(source, gate, threshold, wrapSize, sharedSpeed);
    }
    public void Configure(Transform source, bool gate, float threshold, Vector2 wrapSize) =>
        Configure(source, gate, threshold, wrapSize, null);

    private void Configure(Transform source, bool gate, float threshold, Vector2 wrapSize, TranslationSpeed sharedSpeed)
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
        this.sharedSpeed = sharedSpeed;
        ResetMeasurement();
        configured = true;
        enabled = gate;
        if (gate) ApplyTranslationSpeed(sharedSpeed ?? localSpeed);
        else SetPlaybackMultiplier(1f);
    }
    private void Start()
    {
        if (!configured) Configure(transform, true, speedThreshold, Vector2.zero);
    }
    private void LateUpdate() => MeasureTranslation(Time.deltaTime);

    private void ResetMeasurement()
    {
        if (sharedSpeed == null)
        {
            Vector3 position = movementSource != null ? movementSource.position : transform.position;
            if (localSpeed == null) localSpeed = new TranslationSpeed(position, periodicSize);
            else localSpeed.Reset(position, periodicSize);
        }
        CurrentSpeed = 0;
        walking = false;
    }

    private void MeasureTranslation(float deltaTime)
    {
        if (!enabled || (animator == null && legacyAnimation == null) || movementSource == null) return;
        if (sharedSpeed == null) localSpeed.Sample(movementSource.position, deltaTime, speedThreshold);
        ApplyTranslationSpeed(sharedSpeed ?? localSpeed);
    }

    private void ApplyTranslationSpeed(TranslationSpeed measurement)
    {
        CurrentSpeed = measurement.Speed;
        SetPlaybackMultiplier(measurement.IsWalking ? CurrentSpeed / ReferenceWalkingSpeed : 0f);
    }

    // Start strictly above the threshold. Hysteresis keeps an already-walking animal moving above 80%.
    private static bool IsAboveThreshold(float speed, float threshold, bool retainWalking) =>
        speed > threshold * (retainWalking ? WalkingStopThresholdRatio : 1f);

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
        if (sharedSpeed != null && enabled) ApplyTranslationSpeed(sharedSpeed);
    }

    internal void DetachClipPlayback(StockLocustPlayback playback)
    {
        if (clipPlayback == playback) clipPlayback = null;
    }

    internal bool CanSampleExternally => configured && enabled && sharedSpeed == null && animator != null &&
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
        bool moving = IsAboveThreshold(speed, speedThreshold, walking);
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
        ApplyTranslationSpeed(sharedSpeed ?? localSpeed);
    }

    private void OnDisable()
    {
        if (!externalSampling) SetPlaybackMultiplier(1f);
    }
    // Legacy public setters retained for existing callers.
    public void SetNoiseThreshold(float threshold) => speedThreshold = Mathf.Max(0, threshold);
    public void SetEnabled(bool value) => Configure(movementSource, value, speedThreshold, periodicSize, sharedSpeed);
}
