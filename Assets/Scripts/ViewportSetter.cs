using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns the stimulus cameras for one rig; system_config is the rendering allow-list.</summary>
public class ViewportSetter : MonoBehaviour
{
    public int ledPanelWidth = 128;
    public int ledPanelHeight = 128;
    public int startRow;
    public int startCol;
    public bool horizontal = true;
    [SerializeField] private bool interactive;
    [SerializeField] private string displayOrder = "DRBLFU";
    [SerializeField] private int targetDisplay = 1;
    private int lastWidth, lastHeight;
    private bool configured;
    private bool applying;

    private void Awake() => DisableCameras();

    private void OnEnable()
    {
        MainController.SystemConfigurationChanged += RefreshSystemConfig;
        RefreshSystemConfig();
    }

    private void Start()
    {
        QualitySettings.vSyncCount = 1;
        RefreshSystemConfig();
    }

    private void OnDisable()
    {
        MainController.SystemConfigurationChanged -= RefreshSystemConfig;
        DisableCameras();
    }

    public void RefreshSystemConfig()
    {
        MainController main = MainController.Instance;
        if (main == null || !main.TryGetSystemConfigForGameObject(gameObject, out SystemConfig config))
        {
            configured = false;
            DisableCameras();
            return;
        }
        ApplyConfiguration(config);
    }

    public void ApplyConfiguration(SystemConfig config)
    {
        configured = config != null;
        if (!configured) { DisableCameras(); return; }
        ledPanelWidth = config.ledPanelWidth;
        ledPanelHeight = config.ledPanelHeight;
        startRow = config.startRow;
        startCol = config.startCol;
        horizontal = config.horizontal;
        displayOrder = config.displayOrder;
        targetDisplay = config.targetDisplay;
        ApplyViewports();
    }

    private Camera[] RigCameras() => GetComponentsInChildren<Camera>(true);
    private bool Owns(Camera camera) => camera.GetComponentInParent<ViewportSetter>(true) == this;

    private void DisableCameras()
    {
        foreach (Camera camera in RigCameras())
            if (Owns(camera)) camera.enabled = false;
    }

    // Unknown/duplicate letters never create extra views or shift the remaining valid slots.
    public static string NormalizeDisplayOrder(string order)
    {
        var letters = new List<char>();
        foreach (char letter in (order ?? "").ToUpperInvariant())
            if ("DRBLFU".IndexOf(letter) >= 0 && !letters.Contains(letter)) letters.Add(letter);
        return new string(letters.ToArray());
    }

    public static Rect PanelRect(int slot, SystemConfig config, int width, int height)
    {
        float w = (float)config.ledPanelWidth / width;
        float h = (float)config.ledPanelHeight / height;
        return new Rect((config.startCol + (config.horizontal ? slot : 0)) * w,
            1f - (config.startRow + 1 + (config.horizontal ? 0 : slot)) * h, w, h);
    }

    private void ApplyViewports()
    {
        if (applying) return;
        applying = true;
        try
        {
            // Include inactive children so serialized camera state can never override the config.
            Camera[] cameras = RigCameras();
            foreach (Camera camera in cameras)
                if (Owns(camera)) camera.enabled = false;

            if (!configured || !isActiveAndEnabled || targetDisplay < 0 || targetDisplay >= Display.displays.Length ||
                ledPanelWidth <= 0 || ledPanelHeight <= 0) return;

            if (targetDisplay > 0 && !Display.displays[targetDisplay].active) Display.displays[targetDisplay].Activate();
            int width = targetDisplay == 0 ? Screen.width : Display.displays[targetDisplay].renderingWidth;
            int height = targetDisplay == 0 ? Screen.height : Display.displays[targetDisplay].renderingHeight;
            if (width <= 0 || height <= 0) return;
            lastWidth = width; lastHeight = height;
            string order = NormalizeDisplayOrder(displayOrder);
            var layout = new SystemConfig {
                ledPanelWidth = ledPanelWidth, ledPanelHeight = ledPanelHeight,
                startRow = startRow, startCol = startCol, horizontal = horizontal
            };
            for (int slot = 0; slot < order.Length; slot++)
            {
                foreach (Camera camera in cameras)
                {
                    if (!Owns(camera) || camera.name != "Main Camera " + order[slot]) continue;
                    camera.targetDisplay = targetDisplay;
                    camera.rect = PanelRect(slot, layout, width, height);
                    camera.fieldOfView = 90f;
                    // Listed cameras may be inactive in an older prefab (especially Up/Down).
                    camera.gameObject.SetActive(true);
                    camera.enabled = true;
                    break;
                }
            }
        }
        finally { applying = false; }
    }

    private void OnTransformChildrenChanged()
    {
        if (configured) ApplyViewports();
    }

    private void Update()
    {
        if (!configured) return;
        int width = targetDisplay == 0 ? Screen.width :
            (targetDisplay >= 0 && targetDisplay < Display.displays.Length ? Display.displays[targetDisplay].renderingWidth : 0);
        int height = targetDisplay == 0 ? Screen.height :
            (targetDisplay >= 0 && targetDisplay < Display.displays.Length ? Display.displays[targetDisplay].renderingHeight : 0);
        if (interactive || width != lastWidth || height != lastHeight) ApplyViewports();
    }
}
