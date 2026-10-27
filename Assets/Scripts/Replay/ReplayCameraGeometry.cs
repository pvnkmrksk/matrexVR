using UnityEngine;

/// <summary>Scale-aware clipping and standard viewport projection for the operator's replay camera.</summary>
public static class ReplayCameraGeometry
{
    private static float ProjectedRadius(Vector3 forward, Vector3 extents) =>
        Mathf.Abs(forward.x) * extents.x + Mathf.Abs(forward.y) * extents.y + Mathf.Abs(forward.z) * extents.z;

    public static void UpdateClipping(Camera camera, Bounds subject, Bounds? environment)
    {
        if (camera == null) return;
        Vector3 forward = camera.transform.forward;
        float subjectDepth = Vector3.Dot(subject.center - camera.transform.position, forward);
        float radius = ProjectedRadius(forward, subject.extents);
        float viewScale = camera.orthographic ? camera.orthographicSize * 2 : Mathf.Max(.001f, Mathf.Abs(subjectDepth));
        float near = Mathf.Clamp(viewScale * .0001f, .0001f, .05f);
        float nearest = subjectDepth - radius;
        if (nearest > 0) near = Mathf.Min(near, Mathf.Max(.0001f, nearest * .25f));
        float far = Mathf.Max(10, subjectDepth + radius + Mathf.Max(2, viewScale * .2f));
        if (environment.HasValue)
        {
            var bounds = environment.Value;
            float depth = Vector3.Dot(bounds.center - camera.transform.position, forward);
            far = Mathf.Max(far, depth + ProjectedRadius(forward, bounds.extents) + Mathf.Max(2, viewScale * .1f));
        }
        camera.nearClipPlane = near;
        // Replace the preceding scene's range instead of retaining a monotonically increasing far clip.
        camera.farClipPlane = Mathf.Clamp(far, near + 1, 10000000);
    }

    public static Vector2 MarkerPosition(Camera camera, Vector3 world, Rect guiViewport, float margin, out bool onScreen, out Vector2 direction, bool forceEdge = false)
    {
        Vector3 screen = camera.WorldToScreenPoint(world);
        Vector2 point = new Vector2(screen.x, Screen.height - screen.y);
        var safe = new Rect(guiViewport.x + margin, guiViewport.y + margin,
            Mathf.Max(1, guiViewport.width - margin * 2), Mathf.Max(1, guiViewport.height - margin * 2));
        direction = point - safe.center;
        if (screen.z < 0) direction = -direction;
        if (direction.sqrMagnitude < .000001f) direction = Vector2.up;
        direction.Normalize();
        onScreen = !forceEdge && screen.z > camera.nearClipPlane && safe.Contains(point);
        if (onScreen) return point;
        float x = Mathf.Abs(direction.x) > .00001f ? safe.width * .5f / Mathf.Abs(direction.x) : float.PositiveInfinity;
        float y = Mathf.Abs(direction.y) > .00001f ? safe.height * .5f / Mathf.Abs(direction.y) : float.PositiveInfinity;
        return safe.center + direction * Mathf.Min(x, y);
    }
}
