using System;
using UnityEngine;

/// <summary>Optional Bogong-style wind drift and height above ground for Choice experiments.</summary>
[DefaultExecutionOrder(110)]
public class MigrationMotion : MonoBehaviour
{
    public float WindSpeed { get; private set; }
    public float WindDirection { get; private set; }
    public float AglHeight { get; private set; }
    public int GroundLayerMask { get; private set; } = 1;

    public void Configure(float speed, float direction, float height, int groundLayerMask = 1)
    {
        if (!Finite(speed) || speed < 0 || !Finite(direction) || !Finite(height) || height < 0)
            throw new ArgumentException("Migration wind speed and AGL must be finite and nonnegative; direction must be finite.");
        WindSpeed = speed; WindDirection = direction; AglHeight = height; GroundLayerMask = groundLayerMask;
        enabled = speed > 0 || height > 0;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private void LateUpdate() => Advance(Time.deltaTime);

    public void Advance(float seconds)
    {
        if (!enabled) return;
        // Meteorological convention: direction indicates where the wind comes FROM.
        float angle = (WindDirection + 180f) * Mathf.Deg2Rad;
        Vector3 position = transform.position + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * (WindSpeed * seconds);
        if (AglHeight > 0 && TryGroundHeight(position, out float ground)) position.y = ground + AglHeight;
        transform.position = position;
    }

    private bool TryGroundHeight(Vector3 position, out float height)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if ((GroundLayerMask & (1 << terrain.gameObject.layer)) == 0) continue;
            Vector3 local = position - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x >= 0 && local.z >= 0 && local.x <= size.x && local.z <= size.z)
            {
                height = terrain.SampleHeight(position) + terrain.transform.position.y;
                return true;
            }
        }
        // Flat/collider-based scenes also work. Missing ground preserves Y instead of teleporting to zero.
        if (Physics.Raycast(position + Vector3.up * 10000f, Vector3.down, out RaycastHit hit,
                20000f, GroundLayerMask, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
        {
            height = hit.point.y;
            return true;
        }
        height = 0;
        return false;
    }
}
