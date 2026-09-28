using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Heading arrows and recent trajectories drawn only in the operator's overview.</summary>
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(CanvasRenderer))]
public class OverheadTrackOverlay : MaskableGraphic
{
    private struct Sample
    {
        public Vector3 position;
        public float time;
        public bool breakBefore;
    }
    private sealed class Track
    {
        public ClosedLoop rig;
        public int number;
        public Vector2 actualPosition, markerPosition;
        public bool onScreen;
        public Color tint;
        public Text label;
        public Vector2 heading = Vector2.up;
        public readonly List<Sample> samples = new List<Sample>();
    }

    private readonly List<Track> tracks = new List<Track>();
    private Camera overview;
    private OverheadCameraConfig settings;
    private float nextDiscovery, nextSample;
    private bool visible = true;
    private static readonly Color[] RigColors = {
        new Color(1f, 0.28f, 0.25f), new Color(0.3f, 1f, 0.4f),
        new Color(0.2f, 0.85f, 1f), new Color(1f, 0.88f, 0.2f)
    };
    public int TrackedRigCount => tracks.Count;

    public void Initialize(Camera camera, OverheadCameraConfig config)
    {
        overview = camera;
        settings = config;
        raycastTarget = false;
        RefreshRigs();
    }
    public void SetVisible(bool value)
    {
        visible = value;
        if (visible)
        {
            // Do not connect a path across the interval when monitoring was paused.
            foreach (Track track in tracks) track.samples.Clear();
            nextDiscovery = nextSample = 0;
            RefreshRigs();
        }
        foreach (Track track in tracks) track.label.enabled = false;
        enabled = visible; // Stops sampling, projection, layout and UI mesh rebuilding while hidden.
        if (visible) UpdateLabels();
        SetVerticesDirty();
    }

    private static int RigNumber(Transform item)
    {
        for (; item != null; item = item.parent)
        {
            Match match = Regex.Match(item.name, @"(?<![A-Za-z0-9])VR([0-9]+)(?![A-Za-z0-9])");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int number)) return number;
        }
        return 0;
    }
    private void RefreshRigs()
    {
        var present = new HashSet<ClosedLoop>();
        foreach (ClosedLoop rig in FindObjectsByType<ClosedLoop>(FindObjectsSortMode.None))
        {
            int number = RigNumber(rig.transform);
            if (!rig.isActiveAndEnabled || rig.gameObject.scene != gameObject.scene || number <= 0) continue;
            present.Add(rig);
            if (tracks.Exists(t => t.rig == rig)) continue;
            Color tint = number <= RigColors.Length ? RigColors[number - 1] : Color.HSVToRGB(Mathf.Repeat(number * 0.618034f, 1f), 0.65f, 1f);
            Text label = new GameObject("Animal " + number, typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
            label.transform.SetParent(transform, false);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(36, 22);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = number.ToString();
            label.fontSize = 14;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = tint;
            label.raycastTarget = false;
            label.GetComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
            label.GetComponent<Outline>().effectDistance = Vector2.one;
            tracks.Add(new Track { rig = rig, number = number, tint = tint, label = label });
        }
        for (int i = tracks.Count - 1; i >= 0; i--)
        {
            if (present.Contains(tracks[i].rig)) continue;
            tracks[i].label.enabled = false;
            Destroy(tracks[i].label.gameObject);
            tracks.RemoveAt(i);
        }
        tracks.Sort((a, b) => a.number.CompareTo(b.number));
    }

    private float HistorySeconds => Mathf.Clamp(settings.trailDurationSeconds, 0, 60);
    private float SampleInterval => Mathf.Clamp(settings.trailSampleInterval, 0.1f, 1f);
    private void SampleTrajectories(float now)
    {
        foreach (Track track in tracks)
        {
            if (track.rig == null) continue;
            // Stationary samples also expire: an old path disappears while the animal rests.
            track.samples.RemoveAll(s => s.time < now - HistorySeconds);
            if (HistorySeconds <= 0) { track.samples.Clear(); continue; }
            Vector3 position = track.rig.transform.position;
            bool jump = track.samples.Count > 0 && Vector3.Distance(track.samples[track.samples.Count - 1].position, position) > Mathf.Max(1, settings.trailBreakDistance);
            track.samples.Add(new Sample { position = position, time = now, breakBefore = jump });
        }
    }
    private void LateUpdate()
    {
        if (!visible || overview == null || settings == null) return;
        float now = Time.unscaledTime;
        if (now >= nextDiscovery) { RefreshRigs(); nextDiscovery = now + 0.5f; }
        if (now >= nextSample) { SampleTrajectories(now); nextSample = now + SampleInterval; }
        UpdateLabels();
        SetVerticesDirty();
    }
    private bool Project(Vector3 world, out Vector2 point)
    {
        Vector3 view = overview.WorldToViewportPoint(world);
        Rect rect = rectTransform.rect;
        point = rect.min + Vector2.Scale(new Vector2(view.x, view.y), rect.size);
        return view.z > overview.nearClipPlane && view.z < overview.farClipPlane;
    }
    private bool MarkerPose(Track track, out Vector2 position, out Vector2 heading)
    {
        position = Vector2.zero;
        heading = track.heading;
        if (track.rig == null || !track.rig.isActiveAndEnabled || !Project(track.rig.transform.position, out position)) return false;
        Vector3 forward = Vector3.ProjectOnPlane(track.rig.transform.forward, Vector3.up).normalized;
        if (Project(track.rig.transform.position + forward, out Vector2 tip) && (tip - position).sqrMagnitude > 0.000001f)
            heading = track.heading = (tip - position).normalized;
        return true;
    }
    private void LayoutMarkers()
    {
        Rect bounds = rectTransform.rect;
        foreach (Track track in tracks)
        {
            track.onScreen = MarkerPose(track, out Vector2 actual, out _) && bounds.Contains(actual);
            // Preserve the exact rig position, including overlapping animals.
            track.actualPosition = track.markerPosition = actual;
        }
    }
    private void UpdateLabels()
    {
        if (overview == null) return;
        LayoutMarkers();
        foreach (Track track in tracks)
        {
            track.label.enabled = visible && track.onScreen;
            track.label.rectTransform.anchoredPosition = track.markerPosition + new Vector2(12, 12);
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!visible || overview == null || settings == null) return;
        LayoutMarkers();
        float now = Time.unscaledTime;
        float pixelScale = canvas != null ? 1f / Mathf.Max(canvas.scaleFactor, 0.01f) : 1f;
        float trailWidth = Mathf.Clamp(settings.trailWidthPixels, 0.5f, 4f) * pixelScale;
        // Bound UI geometry even when many more than four rigs are present.
        int segmentsPerRig = Mathf.Max(1, 12000 / Mathf.Max(tracks.Count, 1));
        foreach (Track track in tracks)
        {
            int stride = Mathf.Max(1, Mathf.CeilToInt((float)track.samples.Count / segmentsPerRig));
            for (int i = stride; i < track.samples.Count; i += stride)
            {
                bool jump = false;
                for (int j = i - stride + 1; j <= i; j++) jump |= track.samples[j].breakBefore;
                Sample a = track.samples[i - stride], b = track.samples[i];
                if (jump || !Project(a.position, out Vector2 start) || !Project(b.position, out Vector2 end)) continue;
                Color ca = track.tint, cb = track.tint;
                ca.a = TrailOpacity(now - a.time, HistorySeconds);
                cb.a = TrailOpacity(now - b.time, HistorySeconds);
                Line(vh, start, end, trailWidth, ca, cb);
            }
        }
        foreach (Track track in tracks)
        {
            if (!track.onScreen || !MarkerPose(track, out _, out Vector2 forward)) continue;
            Vector2 position = track.markerPosition;
            Vector2 side = new Vector2(-forward.y, forward.x);
            float size = Mathf.Clamp(settings.markerSizePixels, 8, 28) * pixelScale;
            Vector2 tip = position + forward * size * 0.6f;
            Vector2 tail = position - forward * size * 0.4f;
            Vector2 left = position + side * size * 0.3f;
            Vector2 right = position - side * size * 0.3f;
            // Dark outlines keep tiny headings legible on both the floor and virtual animals.
            Arrow(vh, tail, tip, left, right, 3.5f * pixelScale, new Color(0, 0, 0, 0.85f));
            Arrow(vh, tail, tip, left, right, 1.7f * pixelScale, track.tint);
        }
    }
    private static float TrailOpacity(float age, float duration) => duration <= 0 ? 0 : 0.35f * Mathf.Clamp01(1 - age / duration);
    private static void Arrow(VertexHelper vh, Vector2 tail, Vector2 tip, Vector2 left, Vector2 right, float width, Color tint)
    {
        Line(vh, tail, tip, width, tint, tint);
        Line(vh, left, tip, width, tint, tint);
        Line(vh, right, tip, width, tint, tint);
    }
    private static void Line(VertexHelper vh, Vector2 start, Vector2 end, float width, Color a, Color b)
    {
        Vector2 delta = end - start;
        if (delta.sqrMagnitude < 0.001f || (a.a <= 0 && b.a <= 0)) return;
        Vector2 normal = new Vector2(-delta.y, delta.x).normalized * width * 0.5f;
        int first = vh.currentVertCount;
        vh.AddVert(start - normal, a, Vector2.zero); vh.AddVert(start + normal, a, Vector2.zero);
        vh.AddVert(end + normal, b, Vector2.zero); vh.AddVert(end - normal, b, Vector2.zero);
        vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
    }
}
