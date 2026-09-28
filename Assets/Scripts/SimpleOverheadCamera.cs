using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[Serializable]
public class OverheadCameraConfig
{
    public bool enabled = true;
    public int targetDisplay = -1; // Follow system_config's stimulus display by default.
    public float x = 0.58f, y = 0.02f, width = 0.4f, height = 0.4f;
    public int resolution = 512;
    public float markerSizePixels = 14f;
    public float trailDurationSeconds = 60f;
    public float trailSampleInterval = 0.1f;
    public float trailWidthPixels = 1.5f;
    public float trailBreakDistance = 50f;
}

/// <summary>Operator overview with screen-sized heading arrows and fading trajectory history.</summary>
public class SimpleOverheadCamera : MonoBehaviour
{
    public float cameraHeight = 150f;
    public float viewportSize = 0.4f;
    public Vector2 viewportOffset = new Vector2(0.02f, 0.02f);
    public int renderTextureResolution = 512;
    public bool enableOnStart = true;
    private Camera overheadCam;
    private RenderTexture renderTexture;
    private GameObject uiCanvas, cameraObject, backgroundObject;
    private RawImage displayImage;
    private OverheadTrackOverlay trackOverlay;
    private OverheadCameraController controller;
    private bool visible;

    public static void EnsureInScene()
    {
        if (FindObjectOfType<SimpleOverheadCamera>() == null)
            new GameObject("Overhead Camera Setup").AddComponent<SimpleOverheadCamera>();
    }

    private IEnumerator Start()
    {
        MainController main = MainController.Instance;
        OverheadCameraConfig settings = main != null ? main.OverheadCameraSettings : new OverheadCameraConfig {
            enabled = enableOnStart, x = 1f - viewportOffset.x - viewportSize,
            y = viewportOffset.y, width = viewportSize, height = viewportSize, resolution = renderTextureResolution
        };
        if (!settings.enabled) yield break;
        int display = settings.targetDisplay >= 0 ? settings.targetDisplay : (main != null ? main.GetSystemConfig("VR1").targetDisplay : 0);
        if (display < 0 || display >= Display.displays.Length) display = 0;
        else if (display > 0) Display.displays[display].Activate();

        // Own all generated objects so scene transitions and component removal clean up fully.
        backgroundObject = new GameObject("Overview Background");
        Camera background = backgroundObject.AddComponent<Camera>();
        background.depth = -100;
        background.clearFlags = CameraClearFlags.SolidColor;
        background.backgroundColor = Color.black;
        background.cullingMask = 0;
        background.targetDisplay = display;

        renderTexture = new RenderTexture(Mathf.Clamp(settings.resolution, 128, 2048), Mathf.Clamp(settings.resolution, 128, 2048), 16);
        renderTexture.name = "Kannadi Overview";
        renderTexture.Create();
        cameraObject = new GameObject("Simple Overhead Camera");
        overheadCam = cameraObject.AddComponent<Camera>();
        overheadCam.targetTexture = renderTexture;
        overheadCam.cullingMask = ~LayerMask.GetMask("UI");
        overheadCam.clearFlags = CameraClearFlags.SolidColor;
        overheadCam.backgroundColor = Color.black;
        controller = cameraObject.AddComponent<OverheadCameraController>();
        controller.distance = cameraHeight;

        uiCanvas = new GameObject("Overhead Camera Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        Canvas canvas = uiCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.targetDisplay = display;
        canvas.sortingOrder = 1000;
        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("Overview EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)).transform.SetParent(uiCanvas.transform);

        RectTransform panel = new GameObject("Overview Panel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(uiCanvas.transform, false);
        float x = Mathf.Clamp(settings.x, 0f, 0.9f), y = Mathf.Clamp(settings.y, 0f, 0.9f);
        panel.anchorMin = new Vector2(x, y);
        panel.anchorMax = new Vector2(x + Mathf.Clamp(settings.width, 0.1f, 1f - x), y + Mathf.Clamp(settings.height, 0.1f, 1f - y));
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 1f);
        RectTransform imageRect = new GameObject("Overhead Camera Display", typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
        imageRect.SetParent(panel, false);
        imageRect.anchorMin = Vector2.zero; imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = new Vector2(2, 2); imageRect.offsetMax = new Vector2(-2, -30);
        displayImage = imageRect.GetComponent<RawImage>();
        displayImage.texture = renderTexture;
        imageRect.gameObject.AddComponent<RectMask2D>();
        RectTransform overlayRect = new GameObject("Animal Headings and Trails", typeof(RectTransform), typeof(CanvasRenderer), typeof(OverheadTrackOverlay)).GetComponent<RectTransform>();
        overlayRect.SetParent(imageRect, false);
        overlayRect.anchorMin = Vector2.zero; overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
        trackOverlay = overlayRect.GetComponent<OverheadTrackOverlay>();
        trackOverlay.Initialize(overheadCam, settings);
        controller.inputRect = imageRect;
        controller.inputDisplay = display;
        AddButton(panel, "Reset view", 0, () => controller.ResetToSkyView());
        AddButton(panel, "Show / hide", 108, ToggleCamera);
        visible = true;
        yield return null; // Rigs and band members have now completed Start.
        controller.CalculateTargetFromLocusts();
    }

    private void AddButton(RectTransform panel, string label, float left, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        rect.SetParent(panel, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(left, 0); rect.sizeDelta = new Vector2(106, 28);
        rect.GetComponent<Image>().color = new Color(0.16f, 0.16f, 0.16f);
        rect.GetComponent<Button>().onClick.AddListener(action);
        RectTransform textRect = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
        textRect.SetParent(rect, false); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        Text text = textRect.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = label; text.fontSize = 14; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
    }

    public void ToggleCamera()
    {
        visible = !visible;
        if (visible && renderTexture != null && !renderTexture.IsCreated()) renderTexture.Create();
        if (overheadCam != null) overheadCam.enabled = visible;
        if (cameraObject != null) cameraObject.SetActive(visible);
        if (backgroundObject != null) backgroundObject.SetActive(visible);
        if (displayImage != null) displayImage.enabled = visible;
        if (trackOverlay != null) trackOverlay.SetVisible(visible);
        if (!visible && renderTexture != null) renderTexture.Release();
    }
    private void LateUpdate()
    {
        if (visible && overheadCam != null && displayImage != null && displayImage.rectTransform.rect.height > 0)
            overheadCam.aspect = displayImage.rectTransform.rect.width / displayImage.rectTransform.rect.height;
    }
    private void OnDestroy()
    {
        if (cameraObject != null) Destroy(cameraObject);
        if (backgroundObject != null) Destroy(backgroundObject);
        if (uiCanvas != null) Destroy(uiCanvas);
        if (renderTexture != null) { renderTexture.Release(); Destroy(renderTexture); }
    }
}
