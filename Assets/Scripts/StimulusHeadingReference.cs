using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public sealed class HeadingReferenceConfig
{
    public bool enabled = false;
    public double windowSeconds = 180;
    public double startOffsetSeconds = 0;

    public void Validate()
    {
        if (enabled && (double.IsNaN(startOffsetSeconds) || double.IsInfinity(startOffsetSeconds) || startOffsetSeconds < 0))
            throw new ArgumentException("headingReference.startOffsetSeconds must be finite and nonnegative when enabled.");
        if (enabled && (double.IsNaN(windowSeconds) || double.IsInfinity(windowSeconds) || windowSeconds <= 0))
            throw new ArgumentException("headingReference.windowSeconds must be positive and finite when enabled.");
    }
}

[Serializable]
public sealed class HeadingReferenceResult
{
    public double meanHeadingDegrees;
    public double meanSin, meanCos;
    public double resultantLength; // r = |mean unit vector|, in [0,1]. Not a significance test.
    public int sampleCount;
    public double windowSeconds, observationStartedAt, presentedAt;
    public string presentedUtc;
    public float Resolve(float relativeDegrees) => Mathf.Repeat((float)meanHeadingDegrees + relativeDegrees, 360f);
}

/// <summary>Standard circular mean over timestamped heading samples in a trailing window.</summary>
public sealed class CircularHeadingWindow
{
    private struct Sample { public double time, sin, cos; }
    private readonly Queue<Sample> samples = new Queue<Sample>();
    private double sumSin, sumCos;

    public void Add(double time, double degrees, double windowSeconds)
    {
        if (double.IsNaN(degrees) || double.IsInfinity(degrees)) return;
        double radians = degrees * Math.PI / 180;
        var sample = new Sample { time = time, sin = Math.Sin(radians), cos = Math.Cos(radians) };
        samples.Enqueue(sample); sumSin += sample.sin; sumCos += sample.cos;
        Prune(time, windowSeconds);
    }

    public void Prune(double now, double windowSeconds)
    {
        while (samples.Count > 0 && samples.Peek().time < now - windowSeconds)
        {
            Sample old = samples.Dequeue(); sumSin -= old.sin; sumCos -= old.cos;
        }
        if (samples.Count == 0) sumSin = sumCos = 0;
    }

    public HeadingReferenceResult Read()
    {
        if (samples.Count == 0) throw new InvalidOperationException("No finite heading samples were observed.");
        double sin = sumSin / samples.Count, cos = sumCos / samples.Count;
        // atan2 is deliberately retained even for a near-zero vector. Low r is
        // reported, never used to reject or extend an animal's observation.
        double degrees = (Math.Atan2(sin, cos) * 180 / Math.PI + 360) % 360;
        return new HeadingReferenceResult { meanHeadingDegrees = degrees, meanSin = sin, meanCos = cos,
            resultantLength = Math.Min(1, Math.Sqrt(sin * sin + cos * cos)), sampleCount = samples.Count };
    }
}

/// <summary>
/// Reusable per-rig stimulus reference. Any controller can defer presentation with Begin,
/// then use result.Resolve(angle) or RotateStimulus. Never rotates or resets the animal.
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class StimulusHeadingReference : MonoBehaviour
{
    private CircularHeadingWindow history = new CircularHeadingWindow();
    private HeadingReferenceConfig config = new HeadingReferenceConfig();
    private Action<HeadingReferenceResult> present;
    private double startedAt, deadline;
    public string Phase { get; private set; } = "disabled";
    public HeadingReferenceResult Result { get; private set; }
    public bool IsObserving => Phase == "observing";
    public double RemainingSeconds => IsObserving ? Math.Max(0, deadline - Time.timeAsDouble) : 0;

    public static StimulusHeadingReference For(ClosedLoop rig)
    {
        var reference = rig.GetComponent<StimulusHeadingReference>();
        return reference != null ? reference : rig.gameObject.AddComponent<StimulusHeadingReference>();
    }

    public void Begin(HeadingReferenceConfig settings, Action<HeadingReferenceResult> onPresent)
    {
        Cancel();
        config = settings ?? new HeadingReferenceConfig();
        config.Validate();
        if (!config.enabled) { onPresent?.Invoke(null); return; }
        present = onPresent;
        startedAt = Time.timeAsDouble + config.startOffsetSeconds;
        deadline = startedAt + config.windowSeconds;
        Phase = "observing";
    }

    public void Cancel()
    {
        present = null; Result = null; Phase = "disabled";
        history = new CircularHeadingWindow();
    }

    private void LateUpdate() => Tick(Time.timeAsDouble);

    // One real sample per rendered frame, after tracking and manual movement.
    // No fabricated catch-up samples if rendering stalls.
    public void Tick(double now)
    {
        if (!IsObserving || now < startedAt) return;
        if (now <= deadline) history.Add(now, transform.eulerAngles.y, config.windowSeconds);
        if (now < deadline) return;
        history.Prune(deadline, config.windowSeconds);
        try
        {
            Result = history.Read();
            Result.windowSeconds = config.windowSeconds;
            Result.observationStartedAt = startedAt;
            Result.presentedAt = now;
            Result.presentedUtc = DateTime.UtcNow.ToString("O");
            Phase = "presented";
            Action<HeadingReferenceResult> callback = present;
            present = null; // Invoke exactly once, including after a failed presentation.
            callback?.Invoke(Result);
            var main = MainController.Instance;
            string vrId = main != null && main.TryGetSystemConfigForGameObject(gameObject, out var system) ? system.vrId : name;
            string record = JsonConvert.SerializeObject(new {
                vrId, scene = gameObject.scene.name, trial = main != null ? main.currentTrial : 0,
                step = main != null ? main.currentStep : 0, reference = Result });
            Debug.Log("Heading reference presented: " + record);
            if (MasterDataLogger.Instance != null)
                File.AppendAllText(Path.Combine(MasterDataLogger.Instance.directoryPath, "heading_reference.jsonl"), record + "\n");
        }
        catch (Exception error)
        {
            Phase = "error";
            Debug.LogException(error);
        }
    }

    public static void RotateStimulus(Transform stimulus, Vector3 pivot, HeadingReferenceResult reference)
    {
        if (reference == null) return;
        stimulus.RotateAround(pivot, Vector3.up, (float)reference.meanHeadingDegrees);
    }
}
