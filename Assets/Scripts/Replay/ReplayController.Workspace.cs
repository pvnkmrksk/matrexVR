using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

public partial class ReplayController
{
    private const float DefaultReplaySplit = .5f;
    private const int ReplayDragDivider = 143293;
    private static readonly Color ReplayWorkspaceBackground = new Color(.035f, .05f, .075f);
    private static readonly Color ReplayTileBackground = new Color(.012f, .02f, .032f);
    private static readonly Dictionary<string, Texture2D> replayIcons = new Dictionary<string, Texture2D>();
    private readonly List<string> cameraViewOrder = new List<string>();
    private readonly Dictionary<string, JObject> savedReplayRigs = new Dictionary<string, JObject>();
    private readonly Dictionary<string, RenderTexture> gridTextures = new Dictionary<string, RenderTexture>();
    private bool splitReplayView = true;
    private float replaySplitFraction = DefaultReplaySplit;
    private Vector2 dividerPointer;

    private bool CanShowReplayGrid => createRigCameras && camerasByRig.Count > 0;
    private bool UseReplayGrid => splitReplayView && selectedView < 0 && CanShowReplayGrid && !OperatorOverlayVisibility.Hidden;

    private struct ReplayWorkspaceGeometry
    {
        public Rect Bar, Grid, Divider, Overview;
    }

    private ReplayWorkspaceGeometry ReplayWorkspaceLayout(float scale)
    {
        float margin = 12 * scale;
        var header = ReplayHeaderRect(Screen.width, Screen.height, scale);
        var transport = ReplayToolbarRect(Screen.width, Screen.height, scale);
        var bar = new Rect(margin, header.yMax + 8 * scale, Screen.width - margin * 2, 40 * scale);
        float top = bar.yMax + 8 * scale;
        var content = new Rect(bar.x, top, bar.width, Mathf.Max(1, transport.y - 8 * scale - top));
        if (!UseReplayGrid) return new ReplayWorkspaceGeometry { Bar = bar, Overview = content };

        float dividerWidth = 14 * scale;
        float gridWidth = (content.width - dividerWidth) * replaySplitFraction;
        var grid = new Rect(content.x, content.y, gridWidth, content.height);
        var divider = new Rect(grid.xMax, content.y, dividerWidth, content.height);
        var map = new Rect(divider.xMax, content.y, content.xMax - divider.xMax, content.height);
        return new ReplayWorkspaceGeometry { Bar = bar, Grid = grid, Divider = divider, Overview = map };
    }

    private static Rect GridTile(Rect grid, int index, int count, float scale)
    {
        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = (count + columns - 1) / columns;
        float gap = 4 * scale;
        float width = (grid.width - (columns - 1) * gap) / columns;
        float height = (grid.height - (rows - 1) * gap) / rows;
        return new Rect(grid.x + (index % columns) * (width + gap), grid.y + (index / columns) * (height + gap), width, height);
    }

    private static Rect CameraViewport(Rect gui)
    {
        return new Rect(gui.x / Screen.width, 1 - gui.yMax / Screen.height, gui.width / Screen.width, gui.height / Screen.height);
    }

    private Rect WorkspaceReplayViewport()
    {
        if (archive == null || loading || OperatorOverlayVisibility.Hidden) return CurrentReplayViewport();
        return CameraViewport(ReplayWorkspaceLayout(ReplayUiScale(DesktopDisplay.UiScale, Screen.width)).Overview);
    }

    private void BuildReplayViewOrder()
    {
        // Keep the archive's reference-track order for timeline and scene restoration.
        cameraViewOrder.AddRange(rigOrder.OrderBy(id =>
        {
            int index = RuntimeRigRegistry.ParseIndex(ReplayEnvironmentLoader.CanonicalRig(id));
            return index > 0 ? index : int.MaxValue;
        }).ThenBy(id => id, System.StringComparer.OrdinalIgnoreCase));

        if (archive.SystemConfiguration?["configs"] is JArray configs)
            foreach (var config in configs.OfType<JObject>())
                if (config.Value<string>("vrId") is string id)
                    savedReplayRigs[ReplayEnvironmentLoader.CanonicalRig(id)] = config;
    }

    private JObject SavedReplayRig(string id)
    {
        savedReplayRigs.TryGetValue(ReplayEnvironmentLoader.CanonicalRig(id), out var config);
        return config;
    }

    private void ResetReplayLayout()
    {
        splitReplayView = true;
        replaySplitFraction = DefaultReplaySplit;
        overviewUserAdjusted = false;
        ShowOverview();
    }

    private void DrawReplayWorkspace(float scale)
    {
        if (archive == null || loading) return;
        var layout = ReplayWorkspaceLayout(scale);
        var top = layout.Overview.y;
        var bottom = layout.Overview.yMax;
        ReplayFill(new Rect(0, 0, Screen.width, top), ReplayWorkspaceBackground);
        ReplayFill(new Rect(0, bottom, Screen.width, Screen.height - bottom), ReplayWorkspaceBackground);
        ReplayFill(new Rect(0, top, layout.Bar.x, bottom - top), ReplayWorkspaceBackground);
        ReplayFill(new Rect(layout.Bar.xMax, top, Screen.width - layout.Bar.xMax, bottom - top), ReplayWorkspaceBackground);

        bool canInteract = folderPicker == null && mediaPanel?.HasModal != true;
        Vector2 pointer = Event.current.mousePosition;
        bool blocked = Event.current.isMouse && IsReplayWorkspacePointerCovered(pointer, scale);
        GUI.enabled = canInteract && !blocked;
        GUI.Box(layout.Bar, GUIContent.none);
        float x = layout.Bar.x + 7 * scale, y = layout.Bar.y + 4 * scale, height = 32 * scale;
        if (CanShowReplayGrid)
        {
            if (ReplayWorkspaceButton(ReplayControlRect(ref x, y, 100 * scale, height, 5 * scale), "layout-dashboard", "Split", "See first-person feeds and the God’s-eye view together.", UseReplayGrid))
            { splitReplayView = true; ShowOverview(); }
        }
        if (ReplayWorkspaceButton(ReplayControlRect(ref x, y, 129 * scale, height, 8 * scale), "eye", "God’s-eye", "Focus the overview camera.", selectedView < 0 && !UseReplayGrid))
        { splitReplayView = false; ShowOverview(); }

        if (UseReplayGrid || selectedView >= 0)
        {
            x += 7 * scale;
            if (ReplayWorkspaceButton(ReplayControlRect(ref x, y, 81 * scale, height, 3 * scale), "camera", "Real", "Display recorded LED panel pixels at their native resolution.", pixelScaleAnimalView))
                SetReplayImageMode(true);
            if (ReplayWorkspaceButton(ReplayControlRect(ref x, y, 94 * scale, height, 9 * scale), "user", "Human", "Use a smooth, higher-resolution rendering of the same recorded camera projection.", !pixelScaleAnimalView))
                SetReplayImageMode(false);
        }

        GUI.Label(new Rect(x + 8 * scale, y, 280 * scale, height),
            selectedView >= 0 ? rigOrder[selectedView] + " · First person" :
            UseReplayGrid ? cameraViewOrder.Count + " first-person feeds  ·  God’s-eye" : "God’s-eye overview", replayContext);

        float right = layout.Bar.xMax - 7 * scale;
        if (ReplayWorkspaceButton(new Rect(right - 92 * scale, y, 92 * scale, height), "refresh-cw", "Reset", "Restore the default equal split and fit the overview."))
            ResetReplayLayout();
        right -= 98 * scale;
        if (selectedView < 0)
        {
            if (ReplayWorkspaceButton(new Rect(right - 64 * scale, y, 64 * scale, height), "eye", "Fit", "Fit all active tracks into the overview."))
                FitOverview();
            right -= 70 * scale;
            if (ReplayWorkspaceButton(new Rect(right - 122 * scale, y, 122 * scale, height), "panel-left", overviewPerspective ? "Perspective" : "Top-down", "Switch the overview camera between perspective and top-down projection.", overviewPerspective))
                ToggleOverviewProjection();
        }

        if (layout.Grid.width > 0)
        {
            DrawReplayGrid(layout.Grid, scale, canInteract);
            ReplayFill(layout.Divider, ReplayWorkspaceBackground);
            var grip = new Rect(layout.Divider.center.x - 6 * scale, layout.Divider.center.y - 12 * scale, 12 * scale, 24 * scale);
            DrawReplayIcon(grip, "grip-vertical", new Color(.47f, .63f, .71f));
            if (DragReplayHandle(layout.Divider, ReplayDragDivider, canInteract, !blocked, ref dividerPointer, out var delta))
            {
                replaySplitFraction = Mathf.Clamp(replaySplitFraction + delta.x / (layout.Bar.width - layout.Divider.width), .22f, .78f);
                ClearPathFocus();
            }
        }
        GUI.enabled = true;
    }

    private void SetReplayImageMode(bool panelPixels)
    {
        if (pixelScaleAnimalView == panelPixels) return;
        pixelScaleAnimalView = panelPixels;
        if (selectedView >= 0) ApplyView();
    }

    private void DrawReplayGrid(Rect area, float scale, bool canInteract)
    {
        ReplayFill(area, ReplayWorkspaceBackground);
        GUI.enabled = canInteract && (!Event.current.isMouse || !IsReplayWorkspacePointerCovered(Event.current.mousePosition, scale));
        for (int i = 0; i < cameraViewOrder.Count; i++)
        {
            string id = cameraViewOrder[i];
            var tile = GridTile(area, i, cameraViewOrder.Count, scale);
            ReplayFill(tile, ReplayTileBackground);
            bool hasFrame = environmentLoader?.Loading != true && LoadError == null &&
                markerByRig[id].activeInHierarchy && gridTextures.ContainsKey(id);
            if (hasFrame) GUI.DrawTexture(tile, gridTextures[id], ScaleMode.ScaleToFit, false);

            Color color = agentAnnotations[id].Tint;
            ReplayFill(new Rect(tile.x, tile.y, tile.width, Mathf.Max(1, scale)), new Color(color.r, color.g, color.b, .72f));
            var chip = new Rect(tile.x + 6 * scale, tile.y + 5 * scale, 49 * scale, 22 * scale);
            ReplayFill(chip, new Color(.024f, .038f, .055f, .94f));
            ReplayFill(new Rect(chip.x, chip.y, 2 * scale, chip.height), color);
            var label = chip; label.xMin += 8 * scale;
            GUI.Label(label, id, replayContext);
            if (!hasFrame)
            {
                var status = new Rect(tile.x + 6 * scale, chip.yMax + 1 * scale, tile.width - 12 * scale, 20 * scale);
                GUI.Label(status, environmentLoader?.Loading == true ? "Restoring scene…" : "No frame", replayContext);
            }

            // The media panel and run dialogs are drawn on top of the workspace.
            // Keep their pointer targets exclusive when their cards overlap a feed.
            if (GUI.Button(tile, new GUIContent("", "Focus " + id + " first-person view."), GUIStyle.none))
            {
                ClearPathFocus();
                ShowAnimalView(id);
            }
        }
        GUI.enabled = canInteract;
    }

    private bool IsReplayWorkspacePointerCovered(Vector2 pointer, float scale)
    {
        return mediaPanel?.ContainsPointer(new Vector2(pointer.x, Screen.height - pointer.y)) == true ||
            (showRunPicker && ReplayRunPickerRect(scale).Contains(pointer)) ||
            ((showDetails || showReplayHelp || showReplayTrails) && ReplayInfoRect(scale).Contains(pointer));
    }

    private bool UpdateReplayGrid()
    {
        if (!UseReplayGrid)
        {
            ReleaseReplayGrid();
            return false;
        }
        if (previewCamera != null)
        {
            previewCamera.targetTexture = null;
            previewCamera = null;
        }
        float scale = ReplayUiScale(DesktopDisplay.UiScale, Screen.width);
        Rect grid = ReplayWorkspaceLayout(scale).Grid;
        bool sceneReady = environmentLoader?.Loading != true;
        for (int i = 0; i < cameraViewOrder.Count; i++)
        {
            string id = cameraViewOrder[i];
            var camera = camerasByRig[id];
            var tile = GridTile(grid, i, cameraViewOrder.Count, scale);
            var size = ReplayGridTextureSize(id, camera.aspect, tile, out bool native);
            FilterMode filter = native ? FilterMode.Point : FilterMode.Bilinear;
            if (!gridTextures.TryGetValue(id, out var texture) || texture.width != size.x || texture.height != size.y || texture.filterMode != filter)
            {
                // Keep the camera aspect stable when Unity derives it from its target.
                float aspect = camera.aspect;
                camera.targetTexture = null;
                camera.aspect = aspect;
                if (texture != null) DestroyReplayTexture(texture);
                texture = new RenderTexture(size.x, size.y, 24)
                {
                    name = "Replay feed " + id, filterMode = filter, useMipMap = false
                };
                texture.Create();
                gridTextures[id] = texture;
            }
            if (camera.targetTexture != texture) camera.targetTexture = texture;
            var fullTarget = new Rect(0, 0, 1, 1);
            if (camera.rect != fullTarget) camera.rect = fullTarget;
            bool enabled = sceneReady && markerByRig[id].activeInHierarchy;
            if (camera.enabled != enabled) camera.enabled = enabled;
        }
        string previewRig = mediaPanel?.PreviewRig;
        if (previewRig != null && sceneReady && gridTextures.TryGetValue(previewRig, out var preview) && markerByRig[previewRig].activeInHierarchy)
            mediaPanel.SetAnimalPreview(preview, previewRig + " · first person");
        else
            mediaPanel?.SetAnimalPreview(null, null);
        return true;
    }

    private Vector2Int ReplayGridTextureSize(string id, float aspect, Rect tile, out bool native)
    {
        var saved = SavedReplayRig(id);
        int panelWidth = (int?)saved?["ledPanelWidth"] ?? 0;
        int panelHeight = (int?)saved?["ledPanelHeight"] ?? 0;
        native = pixelScaleAnimalView && (bool?)saved?["desktopPreview"] != true && panelWidth > 0 && panelHeight > 0;
        if (native) return new Vector2Int(Mathf.Clamp(panelWidth, 1, 4096), Mathf.Clamp(panelHeight, 1, 1024));

        // Round the major dimension so dragging does not reallocate all targets
        // for each native mouse pixel. Keep the recorded projection's aspect.
        aspect = Mathf.Clamp(aspect, .01f, 100f);
        float imageWidth = Mathf.Min(tile.width, tile.height * aspect);
        float imageHeight = imageWidth / aspect;
        int longest = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(imageWidth, imageHeight) / 32) * 32, 64, 640);
        return aspect >= 1
            ? new Vector2Int(longest, Mathf.Max(1, Mathf.RoundToInt(longest / aspect)))
            : new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(longest * aspect)), longest);
    }

    private void ReleaseReplayGrid()
    {
        foreach (var pair in gridTextures)
        {
            if (camerasByRig.TryGetValue(pair.Key, out var camera) && camera != null)
            {
                camera.targetTexture = null;
                camera.enabled = false;
            }
            DestroyReplayTexture(pair.Value);
        }
        gridTextures.Clear();
    }

    private void DestroyReplayTexture(RenderTexture texture)
    {
        texture.Release();
        Destroy(texture);
    }

    private bool ReplayWorkspaceButton(Rect rect, string icon, string text, string tooltip, bool active = false)
    {
        bool clicked = GUI.Button(rect, new GUIContent("     " + text, tooltip),
            active ? replayInterface.PrimaryButton : GUI.skin.button);
        DrawReplayIcon(new Rect(rect.x + 9 * replaySkinScale, rect.center.y - 8 * replaySkinScale, 16 * replaySkinScale, 16 * replaySkinScale), icon, Color.white);
        if (clicked) ClearPathFocus();
        return clicked;
    }

    internal static void DrawReplayIcon(Rect rect, string icon, Color tint)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (!replayIcons.TryGetValue(icon, out var texture))
        {
            texture = Resources.Load<Texture2D>("ConfigurationUI/Icons/" + icon);
            replayIcons[icon] = texture;
        }
        if (texture == null) return;
        Color previous = GUI.color;
        GUI.color = tint;
        GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
        GUI.color = previous;
    }

    private static void ReplayFill(Rect rect, Color color)
    {
        if (Event.current.type != EventType.Repaint) return;
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    internal static bool DragReplayHandle(Rect rect, int hint, bool enabled, bool canStart, ref Vector2 lastPointer, out Vector2 delta)
    {
        delta = Vector2.zero;
        int control = GUIUtility.GetControlID(hint, FocusType.Passive);
        if (!enabled || !Application.isFocused)
        {
            if (GUIUtility.hotControl == control) GUIUtility.hotControl = 0;
            return false;
        }

        var evt = Event.current;
        bool released = evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp;
        if (canStart && evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition) && GUIUtility.hotControl == 0)
        {
            GUIUtility.hotControl = control;
            lastPointer = evt.mousePosition;
            evt.Use();
        }
        else if (GUIUtility.hotControl == control && (evt.type == EventType.MouseDrag || released))
        {
            delta = evt.mousePosition - lastPointer;
            lastPointer = evt.mousePosition;
            if (released) GUIUtility.hotControl = 0;
            if (evt.type != EventType.Used) evt.Use();
        }
        return delta.sqrMagnitude > 0;
    }
}
