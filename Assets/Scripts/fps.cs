using UnityEngine;
using UnityEngine.UI;

/// <summary>Persistent frame pacing and workload readout, refreshed at 2 Hz.</summary>
public class fps : MonoBehaviour
{
    // Immutable, so the telemetry worker only receives captured values, never Unity objects.
    public sealed class FrameStatistics
    {
        public double realtimeSeconds, windowSeconds, framesPerSecond, frameMeanMs, framePeakMs;
        public int targetFrameRate, vSyncCount, timingSamples;
        public double? budgetMs, cpuMainMs, cpuRenderMs, gpuMs, presentWaitMs;
        public string graphicsApi;
    }
    public static FrameStatistics Latest { get; private set; }
    private static fps instance;
    private Text label;
    private Canvas displayCanvas;
    private readonly FrameTiming[] timings = new FrameTiming[1];
    private double previousTime, elapsed, peakInterval;
    private double cpuMain, cpuRender, gpu, presentWait;
    private int timingSamples, gpuSamples;
    private ulong previousTimestamp;
    private bool timingsEnabled;
    private string graphicsApi;
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
        Latest = null;
        previousTime = Time.realtimeSinceStartupAsDouble;
        timingsEnabled = FrameTimingManager.IsFeatureEnabled();
        graphicsApi = SystemInfo.graphicsDeviceType.ToString();
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
        box.sizeDelta = new Vector2(520, 72);
        box.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
        box.GetComponent<Image>().raycastTarget = false;
        label = new GameObject("FPS Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(box, false);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 16;
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
        double now = Time.realtimeSinceStartupAsDouble;
        double interval = now - previousTime;
        previousTime = now;
        if (interval <= 0) return;
        elapsed += interval;
        peakInterval = System.Math.Max(peakInterval, interval);
        frames++;
        if (timingsEnabled)
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
            {
                var timing = timings[0];
                // Results arrive several frames later; do not count repeated results twice.
                if (timing.frameStartTimestamp != previousTimestamp && Valid(timing.cpuFrameTime) && timing.cpuFrameTime > 0)
                {
                    previousTimestamp = timing.frameStartTimestamp;
                    if (Valid(timing.cpuMainThreadFrameTime) && Valid(timing.cpuRenderThreadFrameTime) && Valid(timing.cpuMainThreadPresentWaitTime))
                    {
                        cpuMain += timing.cpuMainThreadFrameTime;
                        cpuRender += timing.cpuRenderThreadFrameTime;
                        presentWait += timing.cpuMainThreadPresentWaitTime;
                        timingSamples++;
                    }
                    if (Valid(timing.gpuFrameTime) && timing.gpuFrameTime > 0) { gpu += timing.gpuFrameTime; gpuSamples++; }
                }
            }
        }
        if (elapsed < 0.5) return;
        Latest = new FrameStatistics {
            realtimeSeconds = now, windowSeconds = elapsed, framesPerSecond = frames / elapsed,
            frameMeanMs = elapsed * 1000 / frames, framePeakMs = peakInterval * 1000,
            targetFrameRate = Application.targetFrameRate, vSyncCount = QualitySettings.vSyncCount,
            budgetMs = QualitySettings.vSyncCount == 0 && Application.targetFrameRate > 0 ? 1000.0 / Application.targetFrameRate : (double?)null,
            timingSamples = timingSamples,
            cpuMainMs = timingSamples > 0 ? cpuMain / timingSamples : (double?)null,
            cpuRenderMs = timingSamples > 0 && cpuRender > 0 ? cpuRender / timingSamples : (double?)null,
            presentWaitMs = timingSamples > 0 ? presentWait / timingSamples : (double?)null,
            gpuMs = gpuSamples > 0 ? gpu / gpuSamples : (double?)null, graphicsApi = graphicsApi
        };
        string target = QualitySettings.vSyncCount > 0 ? $"VSync /{QualitySettings.vSyncCount}"
            : Application.targetFrameRate > 0 ? $"target {Application.targetFrameRate} | VSync OFF" : "uncapped | VSync OFF";
        label.text = $"{Latest.framesPerSecond:0} FPS | {target}\n" +
            $"Frame {Latest.frameMeanMs:F1} / peak {Latest.framePeakMs:F1} ms" + (Latest.budgetMs.HasValue ? $" | budget {Latest.budgetMs:F1} ms\n" : "\n") +
            $"CPU {Ms(Latest.cpuMainMs)} | render {Ms(Latest.cpuRenderMs)} | GPU {Ms(Latest.gpuMs)} | wait {Ms(Latest.presentWaitMs)} ms";
        elapsed = peakInterval = cpuMain = cpuRender = gpu = presentWait = 0;
        frames = timingSamples = gpuSamples = 0;
    }
    private static bool Valid(double value) => value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    private static string Ms(double? value) => value.HasValue ? value.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) : "—";
    private void OnDestroy()
    {
        MainController.SystemConfigurationChanged -= ApplyDisplay;
        if (instance == this) { instance = null; Latest = null; }
    }
}
