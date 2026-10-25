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
    private bool desktopPreview;
    private bool applying;

    private void Awake() => DisableCameras();

    private void OnEnable()
    {
        MainController.SystemConfigurationChanged += RefreshSystemConfig;
        RefreshSystemConfig();
    }

    private void Start()
    {
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
        desktopPreview = config.desktopPreview;
        ApplyViewports();
    }

    private Camera[] RigCameras() => GetComponentsInChildren<Camera>(true);
    private bool Owns(Camera camera) => camera.GetComponentInParent<ViewportSetter>(true) == this;

    private Camera[] EnsureRequestedCameras(Camera[] cameras, string order)
    {
        Camera front = null;
        foreach (Camera camera in cameras)
            if (Owns(camera) && camera.name == "Main Camera F") { front = camera; break; }
        // Custom rigs without an authored Front have no trustworthy projection or
        // centre to copy. Leave those rigs unchanged instead of inventing a view.
        if (front == null) return cameras;
        var complete = new List<Camera>(cameras);
        foreach (char face in order)
        {
            bool exists = false;
            foreach (Camera camera in complete)
                if (Owns(camera) && camera.name == "Main Camera " + face) { exists = true; break; }
            if (exists) continue;

            var child = new GameObject("Main Camera " + face);
            child.SetActive(false);
            child.layer = front.gameObject.layer;
            child.transform.SetParent(front.transform.parent, false);
            Vector3 turn = face == 'R' ? new Vector3(0, 90, 0) :
                face == 'B' ? new Vector3(0, 180, 0) :
                face == 'L' ? new Vector3(0, -90, 0) :
                face == 'U' ? new Vector3(-90, 0, 0) : new Vector3(90, 0, 0);
            var generated = child.AddComponent<Camera>();
            generated.CopyFrom(front);
            child.transform.localPosition = front.transform.localPosition;
            child.transform.localScale = front.transform.localScale;
            child.transform.localRotation = front.transform.localRotation * Quaternion.Euler(turn);
            generated.ResetWorldToCameraMatrix();
            generated.ResetCullingMatrix();
            generated.enabled = false;
            var sourceSky = front.GetComponent<Skybox>();
            if (sourceSky != null)
            {
                var sky = child.AddComponent<Skybox>();
                sky.material = sourceSky.material;
                sky.enabled = sourceSky.enabled;
            }
            // The child belongs to this scene's rig. Ordinary scene destruction
            // releases it; there is no cross-scene camera cache or persistent host.
            complete.Add(generated);
        }
        return complete.ToArray();
    }

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
        if (config.desktopPreview) return new Rect(0, 0, 1, 1);
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
                startRow = startRow, startCol = startCol, horizontal = horizontal, desktopPreview = desktopPreview
            };
            if (desktopPreview && order.Length != 1) return;
            cameras = EnsureRequestedCameras(cameras, order);
            for (int slot = 0; slot < order.Length; slot++)
            {
                foreach (Camera camera in cameras)
                {
                    if (!Owns(camera) || camera.name != "Main Camera " + order[slot]) continue;
                    camera.targetDisplay = targetDisplay;
                    camera.rect = PanelRect(slot, layout, width, height);
                    camera.fieldOfView = 90f;
                    if (order[slot] == 'U' || order[slot] == 'D')
                    {
                        // Vertical faces inherit the same rig population as Front. Older
                        // prefabs left Up with VR1's mask, leaking the wrong swarm into VR2-4.
                        foreach (Camera front in cameras)
                            if (Owns(front) && front.name == "Main Camera F")
                            {
                                camera.cullingMask = front.cullingMask;
                                camera.transform.position = front.transform.position;
                                camera.nearClipPlane = front.nearClipPlane;
                                camera.farClipPlane = front.farClipPlane;
                                break;
                            }
                    }
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
