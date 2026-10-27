using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Built-in Unity line renderers for operator-only, seekable replay annotations.</summary>
public sealed class ReplayAgentVisuals : MonoBehaviour
{
    public const double RecentHistorySeconds = 5;
    private LineRenderer recent, heading, frustum, halo;
    private Renderer body;
    private Color tint;
    private readonly Vector3[] corners = new Vector3[4], conePoints = new Vector3[11], arrowPoints = new Vector3[5], ringPoints = new Vector3[33];
    private readonly List<Vector3> history = new List<Vector3>(258);
    private Vector3[] historyPoints = Array.Empty<Vector3>();
    private double previousTime = double.NaN;
    private int previousStart = -1, previousCursor = -1;
    private Vector3 previousPosition;
    public int HistoryPointCount => recent != null ? recent.positionCount : 0;
    public int HistoryStartIndex { get; private set; } = -1;
    public int HistoryEndIndex { get; private set; } = -1;
    public bool AnnotationVisible { get; private set; }
    public Color Tint => tint;
    public LineRenderer CameraFrustum => frustum;
    public LineRenderer RecentTrail => recent;

    public static Color RigColor(int number)
    {
        // Keep the first four colors consistent with the live operator overview.
        switch (Mathf.Max(1, number))
        {
            case 1: return new Color(1f, .28f, .25f);
            case 2: return new Color(.3f, 1f, .4f);
            case 3: return new Color(.2f, .85f, 1f);
            case 4: return new Color(1f, .88f, .2f);
            default: return Color.HSVToRGB(Mathf.Repeat(number * .618034f, 1), .65f, 1);
        }
    }

    public void Initialize(int number, Renderer markerBody)
    {
        tint = RigColor(number); body = markerBody;
        recent = MakeLine("Last five seconds — recorded positions", .9f);
        heading = MakeLine("Current recorded heading — not a prediction", .95f);
        frustum = MakeLine("Front camera frustum — recorded projection", .5f);
        halo = MakeLine("Operator selection halo", .8f);
        recent.numCornerVertices = 3; recent.numCapVertices = 3;
        if (body != null)
        {
            var material = MakeOverlayMaterial("Replay animal marker");
            if (material != null) { RuntimeMaterialOwner.Assign(body, material); Destroy(material); }
        }
    }

    private Material MakeOverlayMaterial(string label)
    {
        var shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
        if (shader == null) return null;
        var material = new Material(shader) { name = label, color = Color.white, renderQueue = 4000 };
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.SetInt("_ZWrite", 0);
        material.SetInt("_ZTest", (int)CompareFunction.Always);
        return material;
    }

    private LineRenderer MakeLine(string label, float alpha)
    {
        var child = new GameObject(label, typeof(LineRenderer)); child.transform.SetParent(transform, false); child.layer = gameObject.layer;
        var line = child.GetComponent<LineRenderer>(); line.useWorldSpace = true;
        line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false; line.alignment = LineAlignment.View;
        var material = MakeOverlayMaterial(label);
        if (material != null) { RuntimeMaterialOwner.Assign(line, material); Destroy(material); }
        line.startColor = line.endColor = new Color(tint.r, tint.g, tint.b, alpha);
        return line;
    }

    public static float UnitsPerPixel(Camera camera, Vector3 position)
    {
        float height = Mathf.Max(1, camera.pixelHeight);
        if (camera.orthographic) return Mathf.Max(.000001f, 2 * camera.orthographicSize / height);
        float depth = Mathf.Max(camera.nearClipPlane, camera.transform.InverseTransformPoint(position).z);
        return Mathf.Max(.000001f, 2 * depth * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) / height);
    }

    public void Refresh(Camera overview, Camera front, List<ReplaySessionArchive.Sample> samples, int cursor, int continuousStart,
        double seconds, Vector3 position, Quaternion rotation, bool show, bool resizePrimitive)
    {
        AnnotationVisible = show;
        if (recent != null) recent.enabled = show;
        if (heading != null) heading.enabled = show;
        if (frustum != null) frustum.enabled = show;
        if (halo != null) halo.enabled = show;
        if (body != null) body.enabled = show;
        if (!show || overview == null) return;
        float unit = UnitsPerPixel(overview, position);
        if (resizePrimitive && body != null) body.transform.localScale = Vector3.one * unit * 15;
        float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 3.4f);
        if (body != null && body.sharedMaterial != null) body.sharedMaterial.color = Color.Lerp(tint, Color.white, .12f + .16f * pulse);
        recent.widthMultiplier = unit * 3;
        recent.startColor = new Color(tint.r, tint.g, tint.b, .12f);
        recent.endColor = new Color(tint.r, tint.g, tint.b, .8f + pulse * .2f);
        UpdateHistory(samples, cursor, continuousStart, seconds, position);

        Vector3 forward = rotation * Vector3.forward, right = rotation * Vector3.right;
        float length = unit * 37, wing = unit * 7;
        Vector3 tip = position + forward * length;
        arrowPoints[0] = position; arrowPoints[1] = tip;
        arrowPoints[2] = tip - forward * wing + right * wing * .6f;
        arrowPoints[3] = tip; arrowPoints[4] = tip - forward * wing - right * wing * .6f;
        heading.widthMultiplier = unit * 2.2f; heading.positionCount = arrowPoints.Length; heading.SetPositions(arrowPoints);

        for (int i = 0; i < ringPoints.Length; i++)
        {
            float angle = i * Mathf.PI * 2 / (ringPoints.Length - 1), radius = unit * (12 + pulse * 1.5f);
            ringPoints[i] = position + (overview.transform.right * Mathf.Cos(angle) + overview.transform.up * Mathf.Sin(angle)) * radius;
        }
        halo.widthMultiplier = unit * 1.4f; halo.positionCount = ringPoints.Length; halo.SetPositions(ringPoints);
        if (front == null) { frustum.enabled = false; return; }
        // Unity computes the actual camera's saved projection; no invented cone mesh or future pose.
        front.CalculateFrustumCorners(new Rect(0, 0, 1, 1), unit * 58, Camera.MonoOrStereoscopicEye.Mono, corners);
        Vector3 origin = front.transform.position;
        // The camera's projection ignores the marker body's display-only scale.
        for (int i = 0; i < corners.Length; i++) corners[i] = origin + front.transform.rotation * corners[i];
        conePoints[0] = origin; conePoints[1] = corners[0]; conePoints[2] = corners[1]; conePoints[3] = origin;
        conePoints[4] = corners[2]; conePoints[5] = corners[3]; conePoints[6] = origin;
        conePoints[7] = corners[0]; conePoints[8] = corners[3]; conePoints[9] = corners[2]; conePoints[10] = corners[1];
        frustum.widthMultiplier = unit * 1.1f; frustum.positionCount = conePoints.Length; frustum.SetPositions(conePoints);
    }

    private void UpdateHistory(List<ReplaySessionArchive.Sample> samples, int cursor, int continuousStart, double seconds, Vector3 position)
    {
        if (samples == null || samples.Count == 0) { recent.positionCount = 0; return; }
        cursor = Mathf.Clamp(cursor, 0, samples.Count - 1);
        int start = Mathf.Clamp(continuousStart, 0, cursor);
        int low = start, high = cursor;
        while (low < high) { int mid = (low + high) / 2; if (samples[mid].time < seconds - RecentHistorySeconds) low = mid + 1; else high = mid; }
        start = low;
        if (previousTime == seconds && previousStart == start && previousCursor == cursor && previousPosition == position) return;
        previousTime = seconds; previousStart = start; previousCursor = cursor; previousPosition = position;
        HistoryStartIndex = start; HistoryEndIndex = cursor; history.Clear();
        int stride = Math.Max(1, (cursor - start + 254) / 255);
        for (int i = start; i <= cursor; i += stride) history.Add(samples[i].position);
        if (history.Count == 0 || history[history.Count - 1] != position) history.Add(position);
        if (historyPoints.Length != history.Count) historyPoints = new Vector3[history.Count];
        history.CopyTo(historyPoints); recent.positionCount = history.Count; recent.SetPositions(historyPoints);
    }
}
