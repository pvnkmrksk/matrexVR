using UnityEngine;

[DefaultExecutionOrder(1000)]
public sealed class RugosityRig : MonoBehaviour
{
    public RugosityWorld world;
    public bool HasSurface { get; private set; }
    public float GroundHeight { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.up;
    public bool ControlsPose => enabled && world != null && world.enabled && world.IsReady && world.settings.enabled;
    private bool hasValid;
    private Vector3 lastValid;
    private void LateUpdate() => Conform(Time.deltaTime);

    public bool Conform(float seconds, bool snap = false)
    {
        if (!ControlsPose) { hasValid = false; return false; }
        var s = world.settings;
        var p = transform.position;
        HasSurface = world.HeightSurface.Sample(p, out float ground, out _);
        Vector3 normal = Vector3.up;
        if (s.orientationMode == RugosityOrientationMode.SurfaceNormal)
            HasSurface &= world.NormalSurface.Sample(p, out _, out normal);
        if (!HasSurface)
        {
            if (hasValid) transform.position = lastValid;
            return false;
        }
        GroundHeight = ground;
        p.y = s.heightMode == RugosityHeightMode.AboveGround ? ground + s.clearanceCm / s.centimetersPerUnit : s.absoluteHeight;
        transform.position = p;
        lastValid = p; hasValid = true;
        if (s.orientationMode == RugosityOrientationMode.SurfaceNormal && s.normalRadiusCm > 0)
        {
            Vector3 sum = normal;
            for (int i = 0; i < s.normalSamples; i++)
            {
                float angle = 2 * Mathf.PI * i / s.normalSamples;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (s.normalRadiusCm / s.centimetersPerUnit);
                if (world.NormalSurface.Sample(p + offset, out _, out var n)) sum += n;
            }
            normal = sum.normalized;
        }
        GroundNormal = normal;
        // Smooth the up vector, then reconstruct the heading. Slerping complete
        // rotations introduces yaw drift when pitch and roll change together.
        Vector3 appliedNormal = snap || s.orientationMode == RugosityOrientationMode.WorldUp || s.tiltResponse == 0
            ? normal : Vector3.Slerp(transform.up, normal, 1 - Mathf.Exp(-s.tiltResponse * seconds)).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .00001f) forward = Vector3.forward;
        // Preserve horizontal heading exactly while making forward tangent to the ground.
        forward.y = -(appliedNormal.x * forward.x + appliedNormal.z * forward.z) / Mathf.Max(appliedNormal.y, .0001f);
        transform.rotation = Quaternion.LookRotation(forward, appliedNormal);
        return true;
    }
}
