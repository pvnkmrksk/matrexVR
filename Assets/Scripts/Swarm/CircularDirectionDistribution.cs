using System;
using UnityEngine;

/// <summary>Normalized von Mises yaw/elevation in degrees. Opt-in; legacy swarms keep their original sampler.</summary>
public static class CircularDirectionDistribution
{
    public static float Sample(float meanDegrees, float kappa)
    {
        if (float.IsNaN(meanDegrees) || float.IsInfinity(meanDegrees) ||
            float.IsNaN(kappa) || float.IsInfinity(kappa) || kappa < 0)
            throw new ArgumentException("Direction mean must be finite and kappa finite/nonnegative.");
        if (kappa < 1e-6f) return meanDegrees + UnityEngine.Random.Range(-180f, 180f);
        // At very large concentration the normal limit avoids cancellation in r-f.
        // It still samples a spread; unlike the legacy sampler there is no point-mass cutoff.
        if (kappa >= 1e6f)
        {
            double u = Math.Max(1e-12, UnityEngine.Random.value);
            double normal = Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * UnityEngine.Random.value);
            return meanDegrees + (float)(normal / Math.Sqrt(kappa) * 180 / Math.PI);
        }
        double a = 1 + Math.Sqrt(1 + 4 * (double)kappa * kappa);
        double b = (a - Math.Sqrt(2 * a)) / (2 * kappa);
        double r = (1 + b * b) / (2 * b);
        while (true)
        {
            double z = Math.Cos(Math.PI * UnityEngine.Random.value);
            double f = Math.Max(-1, Math.Min(1, (1 + r * z) / (r + z)));
            double c = kappa * (r - f);
            double u = Math.Max(1e-12, UnityEngine.Random.value);
            if (u < c * (2 - c) || (c > 0 && Math.Log(c / u) + 1 - c >= 0))
                return meanDegrees + (float)((UnityEngine.Random.value > .5f ? 1 : -1) * Math.Acos(f) * 180 / Math.PI);
        }
    }
}
