using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Replay controls on ControlScene: pick/paste a RunData folder, press Replay to load and play.
/// </summary>
public class ControlSceneReplayPanel : MonoBehaviour
{
    [Header("Host (created on MainController if empty)")]
    public ReplayController replayController;

    [Header("Optional wired UI (built at runtime if null)")]
    public InputField pathInput;
    public Dropdown sessionDropdown;
    public Button replayButton;
    public Button stopReplayButton;
    public Button refreshButton;
    public Button playPauseButton;
    public Slider scrubSlider;
    public Text statusText;

    [Header("Layout")]
    public bool buildPanelAtRuntime = true;
    public Rect panelAnchor = new Rect(0.02f, 0.02f, 0.55f, 0.22f);

    private readonly List<string> _sessionPaths = new();
    private ReplayScrubSlider _scrubHandler;
    private bool _replayActive;

    private void Start()
    {
        EnsureReplayHost();
        if (buildPanelAtRuntime && pathInput == null)
            BuildPanelUi();

        WireControls();
        RefreshSessionList();
        SetStatus("Paste or select a RunData session, then press Replay.");
    }

    private void Update()
    {
        if (replayController == null || !_replayActive)
            return;

        if (playPauseButton != null)
        {
            var label = playPauseButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = replayController.IsPlaying ? "Pause" : "Play";
        }
    }

    private void EnsureReplayHost()
    {
        MainController main = FindObjectOfType<MainController>();
        if (main == null)
        {
            Debug.LogError("[ControlSceneReplay] MainController not found.");
            return;
        }

        replayController = main.GetComponent<ReplayController>();
        if (replayController == null)
            replayController = main.gameObject.AddComponent<ReplayController>();

        replayController.fullSceneReplay = true;
        replayController.startPaused = true;
        replayController.sessionFolder = "";
        replayController.absoluteSessionPath = "";

        if (main.GetComponent<ReplaySceneOrchestrator>() == null)
            main.gameObject.AddComponent<ReplaySceneOrchestrator>();

        replayController.scrubSlider = scrubSlider;
        replayController.statusText = statusText;
    }

    private void WireControls()
    {
        if (refreshButton != null)
            refreshButton.onClick.AddListener(RefreshSessionList);
        if (sessionDropdown != null)
            sessionDropdown.onValueChanged.AddListener(OnDropdownChanged);
        if (replayButton != null)
            replayButton.onClick.AddListener(OnReplayClicked);
        if (stopReplayButton != null)
            stopReplayButton.onClick.AddListener(OnStopReplayClicked);
        if (playPauseButton != null)
            playPauseButton.onClick.AddListener(OnPlayPauseClicked);

        if (scrubSlider != null)
        {
            _scrubHandler = scrubSlider.GetComponent<ReplayScrubSlider>();
            if (_scrubHandler == null)
                _scrubHandler = scrubSlider.gameObject.AddComponent<ReplayScrubSlider>();
            _scrubHandler.replay = replayController;
            replayController.scrubSlider = scrubSlider;
            replayController.scrubHandler = _scrubHandler;
        }
    }

    public void RefreshSessionList()
    {
        _sessionPaths.Clear();
        string root = Path.Combine(Application.dataPath, "RunData");
        if (Directory.Exists(root))
        {
            string[] dirs = Directory.GetDirectories(root);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            Array.Reverse(dirs);
            _sessionPaths.AddRange(dirs);
        }

        if (sessionDropdown == null)
            return;

        sessionDropdown.ClearOptions();
        var labels = new List<string>();
        foreach (string p in _sessionPaths)
            labels.Add(Path.GetFileName(p));
        if (labels.Count == 0)
            labels.Add("(no sessions under Assets/RunData)");
        sessionDropdown.AddOptions(labels);

        if (_sessionPaths.Count > 0)
            OnDropdownChanged(0);
    }

    private void OnDropdownChanged(int index)
    {
        if (pathInput == null || index < 0 || index >= _sessionPaths.Count)
            return;
        pathInput.text = _sessionPaths[index];
    }

    private void OnReplayClicked()
    {
        string path = ResolvePathFromUi();
        if (string.IsNullOrEmpty(path))
        {
            SetStatus("Enter or select a session folder first.");
            return;
        }

        if (replayController == null)
        {
            SetStatus("Replay host missing.");
            return;
        }

        if (!replayController.LoadSessionFromPath(path))
        {
            SetStatus("Failed to load session (need CSV logs in folder).");
            _replayActive = false;
            return;
        }

        _replayActive = true;
        if (!replayController.IsPlaying)
            replayController.TogglePlay();

        SetStatus($"Replaying: {Path.GetFileName(path)}");
    }

    private void OnStopReplayClicked()
    {
        if (replayController != null)
            replayController.UnloadSession();

        _replayActive = false;
        SceneManager.LoadScene("ControlScene");
        SetStatus("Replay stopped. Back on ControlScene.");
    }

    private void OnPlayPauseClicked()
    {
        if (!_replayActive || replayController == null)
        {
            SetStatus("Press Replay first.");
            return;
        }
        replayController.TogglePlay();
    }

    private string ResolvePathFromUi()
    {
        string path = pathInput != null ? pathInput.text.Trim() : "";
        if (string.IsNullOrEmpty(path))
            return null;

        if (Directory.Exists(path))
            return path;

        string underRunData = Path.Combine(Application.dataPath, "RunData", path);
        if (Directory.Exists(underRunData))
            return underRunData;

        return null;
    }

    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
        Debug.Log($"[ControlSceneReplay] {msg}");
    }

    private void BuildPanelUi()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[ControlSceneReplay] No Canvas in ControlScene.");
            return;
        }

        var panelGo = new GameObject("ReplayPanel");
        panelGo.transform.SetParent(canvas.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(panelAnchor.x, panelAnchor.y);
        panelRt.anchorMax = new Vector2(panelAnchor.x + panelAnchor.width, panelAnchor.y + panelAnchor.height);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        panelGo.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.92f);

        float y = -8f;
        const float rowH = 28f;
        const float pad = 8f;
        float w = 800f;

        pathInput = CreateInput(panelGo.transform, "RunData session path", new Vector2(pad, y), new Vector2(w - pad * 2, rowH));
        y -= rowH + 4f;

        sessionDropdown = CreateDropdown(panelGo.transform, new Vector2(pad, y), new Vector2(w * 0.55f, rowH));
        refreshButton = CreateButton(panelGo.transform, "Refresh", new Vector2(pad + w * 0.56f, y), new Vector2(80, rowH), RefreshSessionList);
        y -= rowH + 6f;

        replayButton = CreateButton(panelGo.transform, "Replay", new Vector2(pad, y), new Vector2(100, rowH + 4), OnReplayClicked);
        playPauseButton = CreateButton(panelGo.transform, "Play", new Vector2(pad + 108, y), new Vector2(80, rowH + 4), OnPlayPauseClicked);
        stopReplayButton = CreateButton(panelGo.transform, "Stop", new Vector2(pad + 196, y), new Vector2(80, rowH + 4), OnStopReplayClicked);
        y -= rowH + 8f;

        var scrubGo = new GameObject("ScrubSlider");
        scrubGo.transform.SetParent(panelGo.transform, false);
        var scrubRt = scrubGo.AddComponent<RectTransform>();
        scrubRt.anchorMin = scrubRt.anchorMax = new Vector2(0, 1);
        scrubRt.pivot = new Vector2(0, 1);
        scrubRt.anchoredPosition = new Vector2(pad, y);
        scrubRt.sizeDelta = new Vector2(w - pad * 2, 20);
        scrubGo.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);
        scrubSlider = scrubGo.AddComponent<Slider>();
        scrubSlider.fillRect = CreateSliderFill(scrubGo.transform).GetComponent<RectTransform>();
        scrubSlider.handleRect = CreateSliderHandle(scrubGo.transform).GetComponent<RectTransform>();
        scrubSlider.minValue = 0f;
        scrubSlider.maxValue = 1f;
        y -= 26f;

        statusText = CreateLabel(panelGo.transform, "Status", new Vector2(pad, y), new Vector2(w - pad * 2, 22));
    }

    private static GameObject CreateSliderFill(Transform parent)
    {
        var go = new GameObject("Fill");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(5, 5);
        rt.offsetMax = new Vector2(-5, -5);
        go.AddComponent<Image>().color = new Color(0.3f, 0.55f, 0.85f, 1f);
        return go;
    }

    private static GameObject CreateSliderHandle(Transform parent)
    {
        var go = new GameObject("Handle");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(16, 24);
        go.AddComponent<Image>().color = Color.white;
        return go;
    }

    private static InputField CreateInput(Transform parent, string placeholder, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Input");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 1f);
        var text = CreateUiText(go.transform, "", 13, TextAnchor.MiddleLeft);
        var ph = CreateUiText(go.transform, placeholder, 13, TextAnchor.MiddleLeft);
        ph.color = new Color(1, 1, 1, 0.35f);
        var input = go.AddComponent<InputField>();
        input.textComponent = text;
        input.placeholder = ph;
        return input;
    }

    private static Dropdown CreateDropdown(Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Dropdown");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<Image>().color = new Color(0.2f, 0.22f, 0.26f, 1f);
        var label = CreateUiText(go.transform, "", 13, TextAnchor.MiddleLeft);
        var dd = go.AddComponent<Dropdown>();
        dd.captionText = label;
        return dd;
    }

    private static Button CreateButton(Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<Image>().color = new Color(0.22f, 0.42f, 0.28f, 1f);
        CreateUiText(go.transform, label, 14, TextAnchor.MiddleCenter);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        return btn;
    }

    private static Text CreateLabel(Transform parent, string text, Vector2 pos, Vector2 size)
    {
        return CreateUiText(parent, text, 12, TextAnchor.UpperLeft, pos, size);
    }

    private static Text CreateUiText(Transform parent, string text, int fontSize, TextAnchor anchor, Vector2? pos = null, Vector2? size = null)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        if (pos.HasValue)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos.Value;
            rt.sizeDelta = size ?? new Vector2(200, 24);
        }
        else
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = Color.white;
        return t;
    }
}
