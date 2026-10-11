#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Hidden-mean Gaussian input, independent truth checks, and real motion integration.</summary>
public static class KineflyGaussianValidation
{
    private static int checks;
    private static readonly List<object> results = new List<object>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static double Normal(System.Random random) => Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Use a disposable validation project.");
        string failure = null;
        try
        {
            foreach (double mean in new[] { -1.8, -1.0, 1.0, 1.8 }) Simulate(mean, 0, 2.5f, 42, false);
            // Learned/manual offsets can begin at the opposite end of the allowed range.
            Simulate(2, 0, 2.5f, 42, false, -2);
            Simulate(-2, 0, -2.5f, 43, false, 2);
            foreach (double mean in new[] { -.75, .75 })
            foreach (double sigma in new[] { .15, .35 })
            foreach (float gain in new[] { -2.5f, 2.5f })
            for (int seed = 0; seed < 8; seed++) Simulate(mean, sigma, gain, seed, false);
            Simulate(.75, .15, 2.5f, 44, false, 0, -.75);
            Simulate(-.75, .15, -2.5f, 45, false, 0, .75);
            Simulate(.75, .15, 2.5f, 42, true);
            Simulate(-.75, .15, -2.5f, 43, true);
        }
        catch (Exception error) { failure = error.ToString(); Debug.LogError(failure); }
        File.WriteAllText(Path.Combine(Application.dataPath, "../gaussian-auto-trim-validation.json"), JsonConvert.SerializeObject(new {
            checks, failure, unity = Application.unityVersion, cases = results, settings = new AutoTrimConfig(),
            note = "Independent seeded Gaussian samples at 25 Hz for 240 seconds. The hidden mean is supplied only to the generator and truth checks, never to the controller. Two traces feed the real ZmqListener raw-packet path and ClosedLoop motion method. Noise permits diffusion of heading; tests check residual drift and statistical uncertainty, not perfectly constant heading." }, Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
    private static void Simulate(double hiddenMean, double sigma, float gain, int seed, bool motion, float initialOffset = 0, double? changedMean = null)
    {
        var random = new System.Random(seed);
        var settings = new AutoTrimConfig { flightCheckEnabled = sigma > 0 };
        var trim = new KineflyAutoTrim(); trim.Configure(true, settings);
        float offset = initialOffset; double heading = 0, untrimmed = 0, tailHeading = 0, centeredAt = -1;
        var corrections = new List<object>();
        GameObject root = null; ClosedLoop loop = null; ZmqListener listener = null; object packet = null;
        MethodInfo publish = null, read = null, rotate = null; FieldInfo yawField = null;
        StreamWriter trace = null;
        if (motion)
        {
            root = new GameObject("Gaussian hidden-mean motion test"); root.SetActive(false);
            listener = root.AddComponent<ZmqListener>(); loop = root.AddComponent<ClosedLoop>(); loop.SetYawGain(gain);
            typeof(ClosedLoop).GetField("_zmqListener", Private).SetValue(loop, listener);
            var packetType = typeof(ZmqListener).GetNestedType("ZmqMessage", BindingFlags.NonPublic);
            packet = Activator.CreateInstance(packetType); yawField = packetType.GetField("yaw");
            publish = typeof(ZmqListener).GetMethod("UpdatePose", Private);
            read = typeof(ClosedLoop).GetMethod("GetCurrentFicTracData", Private);
            rotate = typeof(ClosedLoop).GetMethod("ApplyBogongOrientation", Private);
            trace = new StreamWriter(Path.Combine(Application.dataPath, "../gaussian-heading-" + seed + ".csv"));
            trace.WriteLine("seconds,headingDegrees,untrimmedHeadingDegrees,dcDegrees,hiddenMeanDegrees,yawRateDegPerSecond,meanDegPerSecond,uncertaintyDegPerSecond,passes,state");
        }
        try
        {
            for (int i = 0; i < 6000; i++)
            {
                double now = i * .04;
                double activeMean = changedMean.HasValue && now >= 120 ? changedMean.Value : hiddenMean;
                float yaw = (float)(activeMean + sigma * Normal(random));
                if (motion)
                {
                    yawField.SetValue(packet, yaw); publish.Invoke(listener, new[] { packet });
                    var raw = listener.ReadRawSample();
                    Check(raw.rotation.y == yaw && raw.sequence == i + 1, "Synthetic signal travels through actual raw wire decoding");
                }
                if (trim.Tick(now, i, yaw, true, true, true, gain, offset, out float next))
                {
                    Check(Math.Abs(next - offset) <= settings.maxStepRadians + 1e-6 && Math.Abs(next) <= settings.maxOffsetRadians, "Gaussian correction respects both bounds");
                    corrections.Add(new { seconds = now, dcDegrees = next * KineflyAutoTrim.RadToDeg, residualMeanDegPerSecond = trim.EffectiveMeanDegPerSecond });
                    offset = next;
                    if (motion) loop.SetYawDCOffset(offset);
                }
                if (trim.State == "CENTERED" && centeredAt < 0) centeredAt = now;
                double delta;
                if (motion)
                {
                    var current = (Vector3)read.Invoke(loop, null); float previous = root.transform.eulerAngles.y;
                    rotate.Invoke(loop, new object[] { current, Vector3.zero, .04f });
                    delta = Mathf.DeltaAngle(previous, root.transform.eulerAngles.y);
                    Check(Math.Abs(delta - loop.GetLastYawOutput() * .04) < .0002, "Real heading increment matches applied output rate");
                }
                else delta = gain * (yaw - offset) * KineflyAutoTrim.RadToDeg * .04;
                heading += delta; untrimmed += gain * yaw * KineflyAutoTrim.RadToDeg * .04;
                if (now >= 180) tailHeading += delta;
                if (trace != null)
                    trace.WriteLine(string.Join(",", new[] { now.ToString("F2", CultureInfo.InvariantCulture), heading.ToString("R", CultureInfo.InvariantCulture),
                        untrimmed.ToString("R", CultureInfo.InvariantCulture), (offset * KineflyAutoTrim.RadToDeg).ToString("R", CultureInfo.InvariantCulture),
                        (hiddenMean * KineflyAutoTrim.RadToDeg).ToString("R", CultureInfo.InvariantCulture),
                        loop.GetLastYawOutput().ToString("R", CultureInfo.InvariantCulture), trim.EffectiveMeanDegPerSecond?.ToString("R", CultureInfo.InvariantCulture) ?? "",
                        trim.UncertaintyDegPerSecond?.ToString("R", CultureInfo.InvariantCulture) ?? "", trim.Passes.ToString(), trim.State }));
            }
            double finalMean = changedMean ?? hiddenMean;
            if (changedMean.HasValue)
                Check(corrections.Any(change => { var json = Newtonsoft.Json.Linq.JObject.FromObject(change); double t = json.Value<double>("seconds"); return t >= 120 && t <= 145; }),
                    "A changed animal mean interrupts long verification within one initial window plus settling");
            double trueDrift = gain * (finalMean - offset) * KineflyAutoTrim.RadToDeg;
            double uncertainty = trim.UncertaintyDegPerSecond ?? 0;
            Check(trim.Passes >= 1 && trim.Passes <= 5, "Hidden mean converges in no more than five correction passes: " + JsonConvert.SerializeObject(new { hiddenMean, sigma, gain, seed, passes = trim.Passes, corrections }));
            Check(Math.Abs(trueDrift) <= Math.Max(settings.toleranceDegPerSecond, 2 * uncertainty), "Residual hidden-mean drift lies within measured precision");
            Check(Math.Abs(tailHeading / 60) < Math.Abs(gain * hiddenMean * KineflyAutoTrim.RadToDeg) * .05, "Final heading slope loses at least 95 percent of the original bias");
            if (sigma == 0) Check(trim.State == "CENTERED" && Math.Abs(trueDrift) <= settings.toleranceDegPerSecond, "Noise-free unknown mean reaches tolerance within five passes");
            results.Add(new { hiddenMeanDegrees = hiddenMean * KineflyAutoTrim.RadToDeg, initialDCOffsetDegrees = initialOffset * KineflyAutoTrim.RadToDeg, changedMeanDegrees = changedMean * KineflyAutoTrim.RadToDeg, meanChangesAtSeconds = changedMean.HasValue ? (double?)120 : null, noiseStdDevDegrees = sigma * KineflyAutoTrim.RadToDeg,
                gain, seed, actualMotionIntegration = motion, passes = trim.Passes, corrections, centeredAtSeconds = centeredAt < 0 ? (double?)null : centeredAt,
                finalDCOffsetDegrees = offset * KineflyAutoTrim.RadToDeg, trueResidualDriftDegPerSecond = trueDrift,
                final60SecondHeadingSlopeDegPerSecond = tailHeading / 60, uncertaintyDegPerSecond = uncertainty, measurementSeconds = trim.MeasurementSeconds, state = trim.State });
        }
        finally { trace?.Dispose(); if (root != null) Object.DestroyImmediate(root); }
    }
}
#endif
