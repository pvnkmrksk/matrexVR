using UnityEngine;
using UnityEngine.UI;

/// <summary>Persistent operator FPS readout. Observes timing without changing frame limits or VSync.</summary>
public class fps : MonoBehaviour
{
    private static fps instance;
    private Text label;
    private Canvas displayCanvas;
    private float elapsed;
    private int frames;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureVisible()
    {
        if (instance == null) new GameObject("Frame Rate Display").AddComponent<fps>();
    }
    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        GameObject canvasObject = new GameObject("FPS Canvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        displayCanvas = canvasObject.GetComponent<Canvas>();
        displayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        displayCanvas.sortingOrder = 2000;
        RectTransform box = new GameObject("FPS", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        box.SetParent(canvasObject.transform, false);
        box.anchorMin = box.anchorMax = box.pivot = Vector2.one;
        box.anchoredPosition = new Vector2(-12, -12);
        box.sizeDelta = new Vector2(132, 30);
        box.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
        box.GetComponent<Image>().raycastTarget = false;
        label = new GameObject("FPS Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(box, false);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 18;
        label.color = Color.yellow;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.text = "— FPS";
        MainController.SystemConfigurationChanged += ApplyDisplay;
        ApplyDisplay();
    }
    private void ApplyDisplay()
    {
        if (displayCanvas == null) return;
        int display = MainController.Instance != null ? MainController.Instance.GetSystemConfig("VR1").targetDisplay : 0;
        displayCanvas.targetDisplay = display >= 0 && display < Display.displays.Length ? display : 0;
    }
    private void Update()
    {
        if (instance != this) return;
        elapsed += Time.unscaledDeltaTime;
        frames++;
        if (elapsed < 0.25f) return;
        label.text = $"{frames / elapsed:0} FPS";
        elapsed = 0; frames = 0;
    }
    private void OnDestroy()
    {
        MainController.SystemConfigurationChanged -= ApplyDisplay;
        if (instance == this) instance = null;
    }
}
