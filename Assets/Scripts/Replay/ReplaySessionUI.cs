using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GUI to pick a RunData session folder and start full-scene replay.
/// </summary>
public class ReplaySessionUI : MonoBehaviour
{
    [Header("References")]
    public ReplayController replayController;
    public InputField pathInput;
    public Dropdown sessionDropdown;
    public Button loadButton;
    public Button refreshButton;
    public Text statusLabel;

    [Header("Options")]
    public bool buildUiIfMissing = true;

    private readonly List<string> _sessionPaths = new();

    private void Start()
    {
        if (replayController == null)
            replayController = FindObjectOfType<ReplayController>();

        if (buildUiIfMissing && pathInput == null)
            BuildRuntimeUi();

        if (loadButton != null)
            loadButton.onClick.AddListener(OnLoadClicked);
        if (refreshButton != null)
            refreshButton.onClick.AddListener(RefreshSessionList);
        if (sessionDropdown != null)
            sessionDropdown.onValueChanged.AddListener(OnDropdownChanged);

        RefreshSessionList();
        SetStatus("Select a RunData session and press Load.");
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
            labels.Add("(no sessions)");
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

    private void OnLoadClicked()
    {
        string path = pathInput != null ? pathInput.text.Trim() : "";
        if (string.IsNullOrEmpty(path))
        {
            SetStatus("Enter or select a session folder.");
            return;
        }

        if (!Directory.Exists(path))
        {
            string underRunData = Path.Combine(Application.dataPath, "RunData", path);
            if (Directory.Exists(underRunData))
                path = underRunData;
            else
            {
                SetStatus($"Folder not found: {path}");
                return;
            }
        }

        if (replayController == null)
        {
            SetStatus("ReplayController missing in scene.");
            return;
        }

        if (replayController.LoadSessionFromPath(path))
            SetStatus($"Loaded: {Path.GetFileName(path)}");
        else
            SetStatus("Failed to load session (need CSV + configs).");
    }

    private void SetStatus(string msg)
    {
        if (statusLabel != null)
            statusLabel.text = msg;
        Debug.Log($"[ReplaySessionUI] {msg}");
    }

    private void BuildRuntimeUi()
    {
        var canvasGo = new GameObject("ReplaySessionCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        var panel = CreatePanel(canvasGo.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(520, 160));

        pathInput = CreateInput(panel, "Session path", new Vector2(10, -10), new Vector2(500, 30));
        sessionDropdown = CreateDropdown(panel, new Vector2(10, -50), new Vector2(500, 30));
        loadButton = CreateButton(panel, "Load session", new Vector2(10, -90), new Vector2(150, 28), OnLoadClicked);
        refreshButton = CreateButton(panel, "Refresh list", new Vector2(170, -90), new Vector2(150, 28), RefreshSessionList);
        statusLabel = CreateLabel(panel, "Status", new Vector2(10, -125), new Vector2(500, 24));
    }

    private static RectTransform CreatePanel(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.75f);
        return rt;
    }

    private static InputField CreateInput(Transform parent, string placeholder, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("PathInput");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);
        var text = CreateText(go.transform, "", 14, TextAnchor.MiddleLeft);
        text.GetComponent<RectTransform>().offsetMin = new Vector2(8, 2);
        text.GetComponent<RectTransform>().offsetMax = new Vector2(-8, -2);
        var input = go.AddComponent<InputField>();
        input.textComponent = text;
        input.placeholder = CreateText(go.transform, placeholder, 14, TextAnchor.MiddleLeft).GetComponent<Text>();
        input.placeholder.color = new Color(1, 1, 1, 0.35f);
        return input;
    }

    private static Dropdown CreateDropdown(Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("SessionDropdown");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);
        var label = CreateText(go.transform, "", 14, TextAnchor.MiddleLeft);
        var dropdown = go.AddComponent<Dropdown>();
        dropdown.targetGraphic = go.GetComponent<Image>();
        dropdown.captionText = label;
        return dropdown;
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
        go.AddComponent<Image>().color = new Color(0.25f, 0.45f, 0.25f, 1f);
        CreateText(go.transform, label, 14, TextAnchor.MiddleCenter);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        return btn;
    }

    private static Text CreateLabel(Transform parent, string text, Vector2 pos, Vector2 size)
    {
        return CreateText(parent, text, 13, TextAnchor.UpperLeft, pos, size);
    }

    private static Text CreateText(Transform parent, string text, int fontSize, TextAnchor anchor, Vector2? pos = null, Vector2? size = null)
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
            rt.offsetMin = rt.offsetMax = Vector2.zero;
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
