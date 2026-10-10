using System;
using System.Collections.Generic;

[Serializable]
public sealed class AutoTrimConfig
{
    public double windowSeconds = 20;
    public double flightWindowSeconds = 2;
    // Population variance of the raw L-R signal, in radians squared (before gain/offset).
    public double flightVarianceThreshold = .3;
    public double flightConfirmationSeconds = 1;
    public double flightVarianceHysteresis = .5;
    public double updateIntervalSeconds = .5;
    public double aggressiveness = .25;
    public double maxStepRadians = .02;
    public double settleSeconds = 5;
    public double toleranceDegPerSecond = 1;

    public void Validate()
    {
        if (!InRange(windowSeconds, 1, 300) || !InRange(flightWindowSeconds, .2, 30) ||
            !InRange(flightVarianceThreshold, 0, 1000000) || !InRange(flightConfirmationSeconds, 0, 30) ||
            !InRange(flightVarianceHysteresis, 0, 1) || !InRange(updateIntervalSeconds, .1, 60) ||
            !InRange(aggressiveness, .001, 1) || !InRange(maxStepRadians, .000001, 10) ||
            !InRange(settleSeconds, 0, 300) || !InRange(toleranceDegPerSecond, 0, 1000000))
            throw new ArgumentException("Invalid autoTrimSettings: check the documented finite parameter ranges.");
    }
    private static bool InRange(double value, double min, double max) => !double.IsNaN(value) && value >= min && value <= max;
}

/// <summary>Independent per-rig estimator. Only fresh, distinct input samples enter its windows.</summary>
public sealed class KineflyAutoTrim
{
    public sealed class Adjustment
    {
        public readonly double time;
        public readonly float fromRadians, toRadians, deltaRadians;
        public Adjustment(double time, float from, float to)
        { this.time = time; fromRadians = from; toRadians = to; deltaRadians = to - from; }
    }
    private struct Sample { public double time, yaw; public Sample(double t, double y) { time = t; yaw = y; } }
    private readonly Queue<Sample> flight = new Queue<Sample>();
    private readonly Queue<Sample> trim = new Queue<Sample>();
    private AutoTrimConfig settings = new AutoTrimConfig();
    private long lastSequence = -1;
    private double lastSampleTime = double.NegativeInfinity, nextEvaluation, settleUntil;
    private double sum, sumSquares;
    private double flightStarted, trimStarted;
    private double flightCandidateSince = double.NaN;
    private float previousGain, previousOffset;
    private bool haveControlValues;
    public bool Enabled { get; private set; }
    public bool FlightReady { get; private set; }
    public bool Flying { get; private set; }
    public double FlightVariance { get; private set; }
    public double? EffectiveMedianDegPerSecond { get; private set; }
    public double WindowProgress { get; private set; }
    public int Passes { get; private set; }
    // Keep the last applied automatic change visible when trimming is paused or switched off.
    public Adjustment LastAdjustment { get; private set; }
    public string State { get; private set; } = "OFF";

    public void Configure(bool enabled, AutoTrimConfig config)
    {
        settings = config ?? new AutoTrimConfig();
        settings.Validate();
        Enabled = enabled;
        flight.Clear(); sum = sumSquares = 0;
        FlightReady = Flying = false;
        flightCandidateSince = double.NaN;
        FlightVariance = 0;
        lastSequence = -1; lastSampleTime = double.NegativeInfinity;
        nextEvaluation = settleUntil = 0; haveControlValues = false; Passes = 0;
        ClearTrim(); State = enabled ? "WAITING FOR INPUT" : "OFF";
    }

    private void ClearTrim() { trim.Clear(); WindowProgress = 0; EffectiveMedianDegPerSecond = null; }
    private static bool FullWindow(Queue<Sample> samples, double now, double seconds, double started) =>
        samples.Count >= Math.Max(5, (int)Math.Ceiling(seconds * 5)) && now - started >= seconds;

    // Returns true only when a new offset should be applied. All other paths preserve the caller's offset.
    public bool Tick(double now, long sequence, double yaw, bool fresh, bool supported, bool closedLoop,
                     float gain, float offset, out float newOffset)
    {
        newOffset = offset;
        if (!supported)
        { ClearTrim(); flight.Clear(); sum = sumSquares = 0; FlightVariance = 0; FlightReady = Flying = false; State = "N/A"; return false; }
        if (!fresh || double.IsNaN(yaw) || double.IsInfinity(yaw))
        {
            flight.Clear(); sum = sumSquares = 0; FlightVariance = 0; FlightReady = Flying = false;
            ClearTrim(); State = Enabled ? "WAITING FOR INPUT" : "OFF"; return false;
        }
        // Discard evidence after a gap, a gain change or a manual offset adjustment.
        if (now - lastSampleTime > 1)
        {
            flight.Clear(); sum = sumSquares = 0; FlightReady = Flying = false;
            ClearTrim();
        }
        if (haveControlValues && (gain != previousGain || offset != previousOffset))
        { ClearTrim(); settleUntil = now + settings.settleSeconds; }
        previousGain = gain; previousOffset = offset; haveControlValues = true;

        bool newSample = sequence != lastSequence && now - lastSampleTime >= .02; // At most 50 Hz per rig.
        if (newSample)
        {
            lastSequence = sequence; lastSampleTime = now;
            if (flight.Count == 0) flightStarted = now;
            flight.Enqueue(new Sample(now, yaw)); sum += yaw; sumSquares += yaw * yaw;
        }
        while (flight.Count > 0 && now - flight.Peek().time > settings.flightWindowSeconds)
        { var old = flight.Dequeue(); sum -= old.yaw; sumSquares -= old.yaw * old.yaw; }
        FlightReady = FullWindow(flight, now, settings.flightWindowSeconds, flightStarted);
        FlightVariance = flight.Count > 1 ? Math.Max(0, sumSquares / flight.Count - Math.Pow(sum / flight.Count, 2)) : 0;
        if (!FlightReady || FlightVariance <= settings.flightVarianceThreshold * settings.flightVarianceHysteresis)
        { Flying = false; flightCandidateSince = double.NaN; }
        else if (!Flying)
        {
            if (FlightVariance <= settings.flightVarianceThreshold) flightCandidateSince = double.NaN;
            else
            {
                if (double.IsNaN(flightCandidateSince)) flightCandidateSince = now;
                Flying = now - flightCandidateSince >= settings.flightConfirmationSeconds;
            }
        }

        if (!Enabled) { ClearTrim(); State = "OFF"; return false; }
        if (!closedLoop) { ClearTrim(); State = "PAUSED / OPEN LOOP"; return false; }
        if (float.IsNaN(gain) || float.IsInfinity(gain) || gain == 0 || float.IsNaN(offset) || float.IsInfinity(offset))
        { ClearTrim(); State = "PAUSED / GAIN"; return false; }
        if (!Flying) { ClearTrim(); State = FlightReady ? "PAUSED / NOT FLYING" : "MEASURING FLIGHT"; return false; }
        if (now < settleUntil) { State = "SETTLING"; return false; }

        if (newSample)
        {
            if (trim.Count == 0) trimStarted = now;
            trim.Enqueue(new Sample(now, yaw));
        }
        while (trim.Count > 0 && now - trim.Peek().time > settings.windowSeconds) trim.Dequeue();
        WindowProgress = trim.Count == 0 ? 0 : Math.Min(1, (now - trimStarted) / settings.windowSeconds);
        if (!FullWindow(trim, now, settings.windowSeconds, trimStarted)) { State = "COLLECTING"; return false; }
        if (now < nextEvaluation) return false;
        nextEvaluation = now + settings.updateIntervalSeconds;
        double[] values = new double[trim.Count]; int i = 0;
        foreach (var sample in trim) values[i++] = sample.yaw;
        Array.Sort(values);
        double median = values.Length % 2 == 0 ? .5 * (values[values.Length / 2 - 1] + values[values.Length / 2]) : values[values.Length / 2];
        double residual = median - offset;
        EffectiveMedianDegPerSecond = gain * residual * (180 / Math.PI);
        if (Math.Abs(EffectiveMedianDegPerSecond.Value) <= settings.toleranceDegPerSecond)
        { State = "CENTERED"; return false; }
        double correction = Math.Max(-settings.maxStepRadians, Math.Min(settings.maxStepRadians, settings.aggressiveness * residual));
        newOffset = (float)(offset + correction);
        if (newOffset == offset) { State = "CENTERED"; return false; }
        LastAdjustment = new Adjustment(now, offset, newOffset);
        previousOffset = newOffset;
        Passes++; settleUntil = now + settings.settleSeconds;
        // Each pass uses a complete new window after the animal has had time to adapt.
        trim.Clear(); WindowProgress = 0; State = "TRIMMING";
        return true;
    }
}
