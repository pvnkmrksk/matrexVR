using System.Collections.Generic;
using UnityEngine;

/// <summary>Own every display pixel before partial stimulus viewports and operator UI render.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class DisplayBackgroundClear : MonoBehaviour
{
    private static DisplayBackgroundClear instance;
    private readonly List<Camera> cameras = new List<Camera>();

    public static bool CoversDisplay(int display) =>
        instance != null && display >= 0 && display < instance.cameras.Count &&
        instance.cameras[display] != null && instance.cameras[display].isActiveAndEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var root = new GameObject("Display background clear");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<DisplayBackgroundClear>();
        instance.SynchronizeDisplays();
    }

    private void LateUpdate() => SynchronizeDisplays();

    private void SynchronizeDisplays()
    {
        for (int display = 0; display < Display.displays.Length; display++)
        {
            bool active = display == 0 || Display.displays[display].active;
            if (display >= cameras.Count) cameras.Add(null);
            var camera = cameras[display];
            if (active && camera == null)
            {
                var child = new GameObject("Black display " + display);
                child.transform.SetParent(transform, false);
                camera = child.AddComponent<Camera>();
                camera.targetDisplay = display;
                camera.rect = new Rect(0, 0, 1, 1);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 0;
                camera.depth = -10001;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.useOcclusionCulling = false;
                cameras[display] = camera;
            }
            // Swap-chain images each need their own clear. This camera submits no geometry.
            if (camera != null && camera.enabled != active) camera.enabled = active;
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
