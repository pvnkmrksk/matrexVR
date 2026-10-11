using System;
using System.Collections.Generic;

[Serializable]
public sealed class AutoTrimConfig
{
    public double windowSeconds = 15;
    public double maxWindowSeconds = 120;
    public string centerStatistic = "mean";
    public bool noiseAware = true;
    public double flightWindowSeconds = 2;
    // Population variance of the raw L-R signal, in radians squared (before gain/offset).
    public bool flightCheckEnabled = true;
    public double flightVarianceThreshold = .01;
    public double flightConfirmationSeconds = 1;
    public double flightVarianceHysteresis = .5;
    public double updateIntervalSeconds = .5;
    public double aggressiveness = 1;
    public double maxStepRadians = 1;
    public double maxOffsetRadians = 2;
    public double settleSeconds = 3;
    public double toleranceDegPerSecond = 1;

    public void Validate()
    {
        if (!InRange(windowSeconds, 1, 300) || !InRange(maxWindowSeconds, 1, 300) ||
            !(string.Equals(centerStatistic, "mean", StringComparison.OrdinalIgnoreCase) || string.Equals(centerStatistic, "median", StringComparison.OrdinalIgnoreCase)) ||
            !InRange(flightWindowSeconds, .2, 30) ||
            !InRange(flightVarianceThreshold, 0, 1000000) || !InRange(flightConfirmationSeconds, 0, 30) ||
            !InRange(flightVarianceHysteresis, 0, 1) || !InRange(updateIntervalSeconds, .1, 60) ||
            !InRange(aggressiveness, .001, 1) || !InRange(maxStepRadians, .000001, 10) || !InRange(maxOffsetRadians, .000001, 10) ||
            !InRange(settleSeconds, 0, 300) || !InRange(toleranceDegPerSecond, 0, 1000000))
            throw new ArgumentException("Invalid autoTrimSettings: check the documented finite parameter ranges.");
    }
    private static bool InRange(double value, double min, double max) => !double.IsNaN(value) && value >= min && value <= max;
}

/// <summary>Independent per-rig estimator. Only fresh, distinct input samples enter its windows.</summary>
public sealed class KineflyAutoTrim
{
    public const double RadToDeg = 180 / Math.PI;
    public sealed class Adjustment
    {
        public readonly double time;
        public readonly float fromRadians, toRadians, deltaRadians;
        public readonly float gain;
        public Adjustment(double time, float from, float to, float gain)
        { this.time = time; fromRadians = from; toRadians = to; deltaRadians = to - from; this.gain = gain; }
    }
    private struct Sample { public double time, yaw; public Sample(double t, double y) { time = t; yaw = y; } }
    private readonly Queue<Sample> flight = new Queue<Sample>();
    private readonly Queue<Sample> trim = new Queue<Sample>();
    // Retain workspace across evaluations; a long window must not allocate/sort every half second.
    private Sample[] sampleWorkspace = Array.Empty<Sample>();
    private double[] valueWorkspace = Array.Empty<double>();
    private readonly List<double> blockWorkspace = new List<double>(300);
    private AutoTrimConfig settings = new AutoTrimConfig();
    private long lastSequence = -1;
    private double lastSampleTime = double.NegativeInfinity, nextEvaluation, settleUntil;
    private double sum, sumSquares, learnedStandardError;
    private double flightStarted, trimStarted;
    private double flightCandidateSince = double.NaN;
    private float previousGain, previousOffset;
    private bool haveControlValues;
    public bool Enabled { get; private set; }
    public bool FlightReady { get; private set; }
    public bool Flying { get; private set; }
    public bool FlightCheckEnabled { get; private set; } = true;
    public bool FlightAllowsTrim => !FlightCheckEnabled || Flying;
    public double FlightThreshold => settings.flightVarianceThreshold;
    public int FlightSampleCount => flight.Count;
    public double FlightVariance { get; private set; }
    public double? EffectiveMedianDegPerSecond { get; private set; }
    public double? EffectiveMeanDegPerSecond { get; private set; }
    public double? UncertaintyDegPerSecond { get; private set; }
    public double MeasurementSeconds { get; private set; } = 15;
    public string CenterStatistic => settings.centerStatistic;
    public double WindowProgress { get; private set; }
    public int Passes { get; private set; }
    // Keep the last applied automatic change visible when trimming is paused or switched off.
    public Adjustment LastAdjustment { get; private set; }
    public string State { get; private set; } = "OFF";

    public void Configure(bool enabled, AutoTrimConfig config)
    {
        settings = config ?? new AutoTrimConfig();
        settings.Validate();
        learnedStandardError = 0;
        Enabled = enabled;
        FlightCheckEnabled = settings.flightCheckEnabled;
        flight.Clear(); sum = sumSquares = 0;
        FlightReady = Flying = false;
        flightCandidateSince = double.NaN;
        FlightVariance = 0;
        lastSequence = -1; lastSampleTime = double.NegativeInfinity;
        nextEvaluation = settleUntil = 0; haveControlValues = false; Passes = 0;
        ClearTrim(); State = enabled ? "WAITING FOR INPUT" : "OFF";
    }

    private void ClearTrim()
    {
        trim.Clear(); WindowProgress = 0; EffectiveMedianDegPerSecond = EffectiveMeanDegPerSecond = UncertaintyDegPerSecond = null;
        MeasurementSeconds = settings.windowSeconds;
    }
    public void Disable() { Enabled = false; ClearTrim(); State = "OFF"; }
    public void SetFlightCheckEnabled(bool enabled)
    {
        if (FlightCheckEnabled == enabled) return;
        FlightCheckEnabled = enabled;
        ClearTrim(); // Never mix flight-qualified and bypassed evidence in a correction window.
    }
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
            flightCandidateSince = double.NaN;
            ClearTrim(); State = Enabled ? "WAITING FOR INPUT" : "OFF"; return false;
        }
        // Discard evidence after a gap, a gain change or a manual offset adjustment.
        if (now - lastSampleTime > 1)
        {
            flight.Clear(); sum = sumSquares = 0; FlightReady = Flying = false;
            flightCandidateSince = double.NaN;
            ClearTrim();
        }
        if (haveControlValues && (gain != previousGain || offset != previousOffset))
        { if (offset != previousOffset) learnedStandardError = 0; ClearTrim(); settleUntil = now + settings.settleSeconds; }
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
        if (!FlightAllowsTrim) { ClearTrim(); State = FlightReady ? "PAUSED / NOT FLYING" : "MEASURING FLIGHT"; return false; }
        if (now < settleUntil) { State = "SETTLING"; return false; }

        if (newSample)
        {
            if (trim.Count == 0) trimStarted = now;
            trim.Enqueue(new Sample(now, yaw));
        }
        while (trim.Count > 0 && now - trim.Peek().time > MeasurementSeconds) trim.Dequeue();
        WindowProgress = trim.Count == 0 ? 0 : Math.Min(1, (now - trimStarted) / MeasurementSeconds);
        if (!FullWindow(trim, now, settings.windowSeconds, trimStarted)) { State = "COLLECTING"; return false; }
        if (now < nextEvaluation) return false;
        nextEvaluation = now + settings.updateIntervalSeconds;
        bool verifiedWindow = FullWindow(trim, now, MeasurementSeconds, trimStarted);
        int sampleCount = trim.Count;
        if (sampleWorkspace.Length < sampleCount)
        {
            int capacity = Math.Max(sampleCount, Math.Max(1024, sampleWorkspace.Length * 2));
            sampleWorkspace = new Sample[capacity]; valueWorkspace = new double[capacity];
        }
        trim.CopyTo(sampleWorkspace, 0);
        Sample[] samples = sampleWorkspace;
        Estimate estimate = Calculate(samples, sampleCount, 0);
        bool useMedian = string.Equals(settings.centerStatistic, "median", StringComparison.OrdinalIgnoreCase);
        // A long precision window must not conceal a new animal response. Reacquire only when
        // the latest initial-length window exceeds five combined standard errors, not ordinary sample noise.
        int recentStart = 0;
        while (recentStart < sampleCount - 1 && now - samples[recentStart].time > settings.windowSeconds) recentStart++;
        bool recentReady = sampleCount - recentStart >= Math.Max(5, (int)Math.Ceiling(settings.windowSeconds * 5));
        Estimate recent = !recentReady || recentStart == 0 ? estimate : Calculate(samples, sampleCount, recentStart);
        double recentCenter = useMedian ? recent.median : recent.mean;
        double recentUncertainty = ErrorBand(Math.Sqrt(recent.standardError * recent.standardError + learnedStandardError * learnedStandardError), gain, useMedian);
        bool reacquiring = recentReady && settings.noiseAware && MeasurementSeconds > settings.windowSeconds &&
            Math.Abs(gain * (recentCenter - offset) * RadToDeg) > Math.Max(settings.toleranceDegPerSecond, 2.5 * recentUncertainty);
        if (reacquiring) estimate = recent;
        double mean = estimate.mean, median = estimate.median;
        double residual = (useMedian ? median : mean) - offset;
        EffectiveMedianDegPerSecond = gain * (median - offset) * RadToDeg;
        EffectiveMeanDegPerSecond = gain * (mean - offset) * RadToDeg;
        double rate = Math.Abs(gain * residual * RadToDeg);
        UncertaintyDegPerSecond = ErrorBand(estimate.standardError, gain, useMedian);
        double uncertainty = settings.noiseAware ? UncertaintyDegPerSecond.Value : 0;
        double tolerance = settings.toleranceDegPerSecond;
        double changeUncertainty = settings.noiseAware ? ErrorBand(Math.Sqrt(estimate.standardError * estimate.standardError + learnedStandardError * learnedStandardError), gain, useMedian) : 0;
        if (!verifiedWindow && !reacquiring) { State = "VERIFYING / NOISE"; return false; }
        // Once a complete precision window is available, the applied offset is a known number:
        // use that window's error alone so uncertainty in an older fit cannot hold a proven bias.
        double correctionBand = reacquiring ? 2.5 * changeUncertainty : uncertainty;
        if (rate <= Math.Max(tolerance, correctionBand))
        {
            double expanded = RequiredWindow(uncertainty, tolerance);
            if (expanded > MeasurementSeconds + .01)
            { MeasurementSeconds = expanded; WindowProgress = Math.Min(1, (now - trimStarted) / expanded); State = "VERIFYING / NOISE"; return false; }
            State = rate <= tolerance && uncertainty <= tolerance ? "CENTERED" : "NOISE LIMITED";
            return false;
        }
        double correction = Math.Max(-settings.maxStepRadians, Math.Min(settings.maxStepRadians, settings.aggressiveness * residual));
        // Respect both limits, even when a manually supplied starting offset exceeds the automatic range.
        double lower = Math.Max(-settings.maxStepRadians, -settings.maxOffsetRadians - offset);
        double upper = Math.Min(settings.maxStepRadians, settings.maxOffsetRadians - offset);
        if (lower > upper) { State = "PAUSED / OFFSET LIMIT"; return false; }
        correction = Math.Max(lower, Math.Min(upper, correction));
        newOffset = (float)(offset + correction);
        if (newOffset == offset) { State = "OFFSET LIMIT"; return false; }
        LastAdjustment = new Adjustment(now, offset, newOffset, gain);
        // Display the window under the offset actually applied, rather than its obsolete pre-correction error.
        EffectiveMedianDegPerSecond = gain * (median - newOffset) * RadToDeg;
        EffectiveMeanDegPerSecond = gain * (mean - newOffset) * RadToDeg;
        double afterRate = Math.Abs(gain * ((useMedian ? median : mean) - newOffset) * RadToDeg);
        // A detected change makes the mixed old/new window unsuitable for precision estimates.
        // Take a fresh short pass first, then extend only from the animal's new response.
        MeasurementSeconds = !reacquiring && afterRate <= Math.Max(tolerance, uncertainty) ? RequiredWindow(uncertainty, tolerance) : settings.windowSeconds;
        // Offset fitted to a noisy window has estimation error too. Include both windows' error
        // when deciding whether a later short-window discrepancy warrants another correction.
        if (Math.Abs(settings.aggressiveness * residual) <= settings.maxStepRadians && Math.Abs(newOffset) < settings.maxOffsetRadians)
            learnedStandardError = Math.Sqrt(Math.Pow((1 - settings.aggressiveness) * learnedStandardError, 2) +
                Math.Pow(settings.aggressiveness * estimate.standardError, 2));
        previousOffset = newOffset;
        Passes++; settleUntil = now + settings.settleSeconds;
        // Each pass uses a complete new window after the animal has had time to adapt.
        trim.Clear(); WindowProgress = 0; State = "TRIMMING";
        return true;
    }

    private struct Estimate { public double mean, median, standardError; }
    private static double ErrorBand(double standardError, float gain, bool median) =>
        2 * standardError * Math.Abs(gain) * RadToDeg * (median ? 1.253314 : 1);

    private Estimate Calculate(Sample[] samples, int end, int start)
    {
        int count = end - start;
        double[] values = valueWorkspace; double mean = 0;
        for (int i = start; i < end; i++) { values[i - start] = samples[i].yaw; mean += samples[i].yaw; }
        mean /= count;
        double variance = 0;
        for (int i = 0; i < count; i++) variance += (values[i] - mean) * (values[i] - mean);
        double standardError = Math.Sqrt(variance / (count - 1) / count);
        // One-second block means expose correlated input; never assume every frame is independent.
        var blocks = blockWorkspace; blocks.Clear(); double blockStart = samples[start].time, blockSum = 0; int blockCount = 0;
        for (int i = start; i < end; i++)
        {
            var sample = samples[i];
            if (sample.time - blockStart >= 1)
            { if (blockCount > 0) blocks.Add(blockSum / blockCount); blockStart = sample.time; blockSum = 0; blockCount = 0; }
            blockSum += sample.yaw; blockCount++;
        }
        if (blocks.Count >= 3)
        {
            double blockMean = 0; foreach (double block in blocks) blockMean += block; blockMean /= blocks.Count;
            double blockVariance = 0; foreach (double block in blocks) blockVariance += (block - blockMean) * (block - blockMean);
            standardError = Math.Max(standardError, Math.Sqrt(blockVariance / (blocks.Count - 1) / blocks.Count));
        }
        double median = SelectMedian(values, count);
        return new Estimate { mean = mean, median = median, standardError = standardError };
    }

    // Partition only around the median. Limit partition work before falling back to a partial sort
    // so ordered, identical and adversarial input cannot produce an unbounded quadratic spike.
    private static double SelectMedian(double[] values, int count)
    {
        int middle = count / 2, low = 0, high = count - 1;
        int budget = 2 * (int)Math.Log(count, 2) + 1;
        while (low < high)
        {
            if (--budget == 0) { Array.Sort(values, low, high - low + 1); break; }
            double a = values[low], b = values[low + (high - low) / 2], c = values[high];
            double pivot = Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
            int left = low, right = high;
            while (left <= right)
            {
                while (values[left] < pivot) left++;
                while (values[right] > pivot) right--;
                if (left <= right) { double swap = values[left]; values[left++] = values[right]; values[right--] = swap; }
            }
            if (middle <= right) high = right;
            else if (middle >= left) low = left;
            else break;
        }
        if (count % 2 != 0) return values[middle];
        double lower = values[0];
        for (int i = 1; i < middle; i++) lower = Math.Max(lower, values[i]);
        return .5 * (lower + values[middle]);
    }

    private double RequiredWindow(double uncertainty, double tolerance)
    {
        if (uncertainty <= tolerance) return MeasurementSeconds;
        double maximum = Math.Max(settings.windowSeconds, settings.maxWindowSeconds);
        return Math.Min(maximum, Math.Max(MeasurementSeconds, MeasurementSeconds * Math.Pow(uncertainty / Math.Max(tolerance, 1e-9), 2)));
    }
}
