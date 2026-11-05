using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

/// <summary>Translation-only animation gate, measured in world units per second (cm/s in this project).</summary>
[DefaultExecutionOrder(150)]
public class AnimateOnMove : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("noiseThreshold")] private float speedThreshold = 0.5f;
    private Animator animator;
    private Animation legacyAnimation;
    private readonly Dictionary<string, float> legacySpeeds = new Dictionary<string, float>();
    private Transform movementSource;
    private Vector3 lastPosition;
    private Vector2 periodicSize;
    private float playbackSpeed = 1f;
    private bool configured;
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
            if (animator != null && animator.speed > 0) playbackSpeed = animator.speed;
        }
        if (legacyAnimation == null)
        {
            legacyAnimation = GetComponent<Animation>();
            if (legacyAnimation != null)
                foreach (AnimationState state in legacyAnimation) legacySpeeds[state.name] = state.speed;
        }
        movementSource = source != null ? source : transform;
        speedThreshold = Mathf.Max(0, threshold);
        periodicSize = wrapSize;
        lastPosition = movementSource.position;
        CurrentSpeed = 0;
        configured = true;
        enabled = gate;
        SetPlayback(!gate);
    }
    private void Start()
    {
        if (!configured) Configure(transform, true, speedThreshold, Vector2.zero);
    }
    private void LateUpdate() => MeasureTranslation(Time.deltaTime);

    private void MeasureTranslation(float deltaTime)
    {
        if (!enabled || (animator == null && legacyAnimation == null) || movementSource == null) return;
        Vector3 position = movementSource.position;
        Vector3 delta = position - lastPosition;
        lastPosition = position;
        // Use the tracked root, never an animated bone. Yaw alone cannot trigger walking.
        if (periodicSize.x > 0) delta.x = Kannadi.Wrap(delta.x, periodicSize.x);
        if (periodicSize.y > 0) delta.z = Kannadi.Wrap(delta.z, periodicSize.y);
        CurrentSpeed = deltaTime > 0 ? new Vector2(delta.x, delta.z).magnitude / deltaTime : 0;
        SetPlayback(CurrentSpeed > speedThreshold);
    }
    private void SetPlayback(bool moving)
    {
        if (animator != null) animator.speed = moving ? playbackSpeed : 0;
        if (legacyAnimation != null)
            foreach (AnimationState state in legacyAnimation)
                state.speed = moving && legacySpeeds.TryGetValue(state.name, out float speed) ? speed : 0;
    }
    private void OnDisable() => SetPlayback(true);
    // Legacy public setters retained for existing callers.
    public void SetNoiseThreshold(float threshold) => speedThreshold = Mathf.Max(0, threshold);
    public void SetEnabled(bool value) => Configure(movementSource, value, speedThreshold, periodicSize);
}
