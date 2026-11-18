using System.Collections.Generic;
using UnityEngine;

public partial class ReplayController
{
    private void DrawPixelAnimalView()
    {
        if (selectedView < 0 || !pixelScaleAnimalView || previewCamera == null || animalPreviewTexture == null) return;
        var viewport = WorkspaceReplayViewport();
        var area = new Rect(viewport.x * Screen.width, (1 - viewport.yMax) * Screen.height, viewport.width * Screen.width, viewport.height * Screen.height);
        var color = GUI.color; GUI.color = Color.black; GUI.DrawTexture(area, Texture2D.whiteTexture); GUI.color = color;
        GUI.DrawTexture(area, animalPreviewTexture, ScaleMode.ScaleToFit, false);
    }
    private readonly Dictionary<string, ReplayAgentVisuals> agentAnnotations = new Dictionary<string, ReplayAgentVisuals>();
    private readonly List<Rect> annotationLabels = new List<Rect>();
    private GUIStyle agentLabelStyle;
    private int clippingContextVersion = -1;
    private Bounds? clippingEnvironment;
    private bool overviewHighContrast;
    private int overviewSceneMask;

    private bool HighContrastOverview => overviewHighContrast && !overviewPerspective;

    private void ApplyOverviewContrast()
    {
        overview.cullingMask = HighContrastOverview ? LayerMask.GetMask("OverheadCameraMarkers") : overviewSceneMask;
        overview.backgroundColor = HighContrastOverview ? Color.black : new Color(.04f, .055f, .075f);
    }

    private Rect OverviewGuiRect()
    {
        var pixels = overview.pixelRect;
        return new Rect(pixels.x, Screen.height - pixels.yMax, pixels.width, pixels.height);
    }

    private IEnumerable<GameObject> VisiblePopulationVisuals()
    {
        foreach (var population in populations)
        {
            foreach (int id in population.active)
            {
                var visual = population.visuals[id];
                if (visual != null && visual.activeInHierarchy) yield return visual;
            }
        }
    }

    private void DrawHighContrastLocusts(float scale)
    {
        if (Event.current.type != EventType.Repaint || !HighContrastOverview || selectedView >= 0 ||
            overview == null || archive == null || loading || environmentLoader?.Loading == true) return;

        var viewport = OverviewGuiRect();
        float size = Mathf.Max(6, 8 * scale);
        GUI.BeginGroup(viewport);
        foreach (var visual in VisiblePopulationVisuals()) DrawDot(visual.transform.position, size, Color.white);
        foreach (var pair in markerByRig)
            if (pair.Value.activeInHierarchy)
                DrawDot(pair.Value.transform.position, size * 1.5f, Color.Lerp(agentAnnotations[pair.Key].Tint, Color.white, .35f));
        GUI.EndGroup();

        void DrawDot(Vector3 position, float diameter, Color color)
        {
            var point = overview.WorldToViewportPoint(position);
            if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) return;
            float radius = diameter * .5f;
            var rect = new Rect(point.x * viewport.width - radius, (1 - point.y) * viewport.height - radius, diameter, diameter);
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, radius);
        }
    }

    private static Rect CurrentReplayViewport() => ReplayViewportRect(Screen.width, Screen.height, ReplayUiScale(DesktopDisplay.UiScale, Screen.width));

    private void FitReplayAnimalViewport(Camera camera)
    {
        if(camera==null || camera.targetTexture!=null)return;
        var viewport=WorkspaceReplayViewport();
        if(!matchRecordedCamera){camera.rect=viewport;return;}
        float available=Mathf.Max(.01f,Screen.width*viewport.width/(Screen.height*viewport.height)),aspect=Mathf.Max(.01f,camera.aspect);
        camera.rect=aspect<=available?new Rect(viewport.x+(viewport.width-viewport.width*aspect/available)*.5f,viewport.y,viewport.width*aspect/available,viewport.height):
            new Rect(viewport.x,viewport.y+(viewport.height-viewport.height*available/aspect)*.5f,viewport.width,viewport.height*available/aspect);
    }

    private void UpdateReplayCameraClipping()
    {
        if (overview == null) return;
        if (environmentLoader != null && !environmentLoader.Loading && clippingContextVersion != environmentLoader.ContextVersion)
        {
            clippingContextVersion = environmentLoader.ContextVersion; clippingEnvironment = null;
            int annotationLayer = LayerMask.NameToLayer("OverheadCameraMarkers");
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.gameObject.layer == annotationLayer || renderer is ParticleSystemRenderer) continue;
                var bounds = renderer.bounds;
                if (float.IsNaN(bounds.center.x) || float.IsInfinity(bounds.extents.magnitude)) continue;
                if (!clippingEnvironment.HasValue) clippingEnvironment = bounds;
                else { var accumulated = clippingEnvironment.Value; accumulated.Encapsulate(bounds); clippingEnvironment = accumulated; }
            }
            foreach (var terrain in FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            {
                if (!terrain.enabled || terrain.terrainData == null) continue;
                var bounds = new Bounds(terrain.transform.position + terrain.terrainData.size * .5f, terrain.terrainData.size);
                if (!clippingEnvironment.HasValue) clippingEnvironment = bounds;
                else { var accumulated = clippingEnvironment.Value; accumulated.Encapsulate(bounds); clippingEnvironment = accumulated; }
            }
        }
        ReplayCameraGeometry.UpdateClipping(overview, trajectoryBounds, clippingEnvironment);
    }

    private void RefreshReplayAnnotations()
    {
        if (archive == null || overview == null) return;
        var viewport = WorkspaceReplayViewport();
        if (overview.rect != viewport)
        {
            overview.rect = viewport;
            if (selectedView < 0 && !overviewUserAdjusted) FitOverview();
        }
        UpdateReplayCameraClipping();
        bool show = selectedView < 0 && !OperatorOverlayVisibility.Hidden && environmentLoader?.Loading != true;
        foreach (string id in cameraViewOrder)
        {
            if (!agentAnnotations.TryGetValue(id, out var annotation)) continue;
            var marker = markerByRig[id]; var samples = archive.Rigs[id]; int cursor = AtOrBefore(samples, replayTime);
            camerasByRig.TryGetValue(id, out var front);
            int start = pathsByRig.TryGetValue(id, out var path) ? path.start : cursor;
            annotation.Refresh(overview, front, samples, cursor, start, replayTime, marker.transform.position, marker.transform.rotation,
                show && marker.activeInHierarchy, rigMarkerPrefab == null, trailSeconds, trailProminence);
            if (path != null)
            {
                path.line.widthMultiplier = ReplayAgentVisuals.UnitsPerPixel(overview, marker.transform.position) * 1.3f * Mathf.Max(.1f, trailProminence);
                var color = annotation.Tint; color.a = Mathf.Clamp01(.2f * trailProminence);
                path.line.startColor = path.line.endColor = color;
            }
        }
    }

    private bool LabelBlocked(Rect rect)
    {
        if (ContainsReplayChrome(new Vector2(rect.center.x, Screen.height - rect.center.y)) ||
            ContainsReplayChrome(new Vector2(rect.xMin, Screen.height - rect.yMin)) ||
            ContainsReplayChrome(new Vector2(rect.xMax, Screen.height - rect.yMin)) ||
            ContainsReplayChrome(new Vector2(rect.xMin, Screen.height - rect.yMax)) ||
            ContainsReplayChrome(new Vector2(rect.xMax, Screen.height - rect.yMax))) return true;
        foreach (var previous in annotationLabels) if (previous.Overlaps(rect)) return true;
        return false;
    }

    private Rect FreeAgentLabel(Vector2 position, Rect viewport, float width, float height, float scale)
    {
        Rect Fit(float x, float y) => new Rect(Mathf.Clamp(x, viewport.xMin + 5 * scale, viewport.xMax - width - 5 * scale),
            Mathf.Clamp(y, viewport.yMin + 5 * scale, viewport.yMax - height - 5 * scale), width, height);
        var candidate = Fit(position.x + 16 * scale, position.y - 28 * scale);
        if (!LabelBlocked(candidate)) return candidate;
        // Keep simultaneous and off-screen animals identifiable even at an identical location.
        for (int i = 0; i < 32; i++)
        {
            float angle = i * Mathf.PI * .5f, distance = (i / 4 + 1) * (height + 6 * scale);
            candidate = Fit(position.x + Mathf.Cos(angle) * distance, position.y + Mathf.Sin(angle) * distance);
            if (!LabelBlocked(candidate)) return candidate;
        }
        for (float y = viewport.yMin + 8 * scale; y < viewport.yMax - height; y += height + 6 * scale)
            for (float x = viewport.xMin + 8 * scale; x < viewport.xMax - width; x += width + 8 * scale)
            { candidate = Fit(x, y); if (!LabelBlocked(candidate)) return candidate; }
        return candidate;
    }

    private static string EdgeDirection(Vector2 direction)
    {
        int octant = (Mathf.RoundToInt(Mathf.Atan2(direction.y, direction.x) / (Mathf.PI * .25f)) + 8) % 8;
        return new[] { "→", "↘", "↓", "↙", "←", "↖", "↑", "↗" }[octant];
    }

    private void DrawReplayAgentLabels(float scale)
    {
        if (archive == null || loading || selectedView >= 0 || overview == null || environmentLoader?.Loading == true ||
            folderPicker != null || mediaPanel?.HasModal == true || showDetails || Event.current.type != EventType.Repaint) return;
        if (agentLabelStyle == null) agentLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip };
        agentLabelStyle.font = GUI.skin.font; agentLabelStyle.fontSize = Mathf.RoundToInt(13 * scale);
        var viewport = OverviewGuiRect();
        annotationLabels.Clear();
        foreach (string id in cameraViewOrder)
        {
            if (!agentAnnotations.TryGetValue(id, out var annotation) || !annotation.AnnotationVisible) continue;
            var marker = markerByRig[id];
            Vector2 position = ReplayCameraGeometry.MarkerPosition(overview, marker.transform.position, viewport, 24 * scale, out bool onScreen, out Vector2 direction);
            string label = onScreen ? id : EdgeDirection(direction) + " " + id;
            float width = Mathf.Max(66 * scale, agentLabelStyle.CalcSize(new GUIContent(label)).x + 20 * scale), height = 28 * scale;
            var rect = FreeAgentLabel(position, viewport, width, height, scale); annotationLabels.Add(rect);
            Color previous = GUI.color;
            GUI.color = new Color(.025f, .04f, .055f, .95f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = annotation.Tint; GUI.DrawTexture(new Rect(rect.x, rect.y, 3 * scale, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white; agentLabelStyle.normal.textColor = annotation.Tint; GUI.Label(rect, label, agentLabelStyle);
            GUI.color = previous;
        }
    }
}
