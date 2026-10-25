using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>Desktop presentation is native fullscreen; LED viewport calibration remains independent.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class DesktopDisplay : MonoBehaviour
{
    private const string Preference = "Matrex.ApplicationDisplay";
    private static DesktopDisplay instance;
    private readonly List<DisplayInfo> displays = new List<DisplayInfo>();
    private bool changing;
    private float nextCheck;
    public static float UiScale
    {
        get
        {
            if (Application.isEditor) return 1f;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            try
            {
                IntPtr app = Send(GetClass("NSApplication"), Selector("sharedApplication"));
                IntPtr window = Send(app, Selector("mainWindow"));
                IntPtr screen = window != IntPtr.Zero ? Send(window, Selector("screen")) : Send(GetClass("NSScreen"), Selector("mainScreen"));
                if (screen != IntPtr.Zero) return Mathf.Clamp((float)SendDouble(screen, Selector("backingScaleFactor")), 1f, 3f);
            }
            catch (Exception) { /* Headless validation and early native startup can have no Cocoa window. */ }
#endif
            return Mathf.Clamp(Screen.dpi > 0 ? Screen.dpi / 96f : 1f, 1f, 2.5f);
        }
    }
    public static string[] DisplayNames
    {
        get
        {
            var layout = new List<DisplayInfo>(); Screen.GetDisplayLayout(layout);
            var names = new string[layout.Count];
            for (int i = 0; i < layout.Count; i++) names[i] = (i + 1) + " · " + layout[i].name;
            return names;
        }
    }
    public static int SelectedDisplay => PlayerPrefs.GetInt(Preference, 0);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { instance = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (Application.isEditor || Application.isBatchMode || instance != null) return;
        var host = new GameObject("Native desktop presentation");
        DontDestroyOnLoad(host); instance = host.AddComponent<DesktopDisplay>();
    }
    private IEnumerator Start() { yield return Apply(); }
    public static void SelectDisplay(int index)
    {
        var layout = new List<DisplayInfo>(); Screen.GetDisplayLayout(layout);
        if (index < 0 || index >= layout.Count) throw new ArgumentOutOfRangeException(nameof(index));
        PlayerPrefs.SetInt(Preference, index); PlayerPrefs.Save();
        if (instance != null && !instance.changing) instance.StartCoroutine(instance.Apply());
    }
    private IEnumerator Apply()
    {
        changing = true;
        Screen.GetDisplayLayout(displays);
        int index = Mathf.Clamp(SelectedDisplay, 0, Math.Max(0, displays.Count - 1));
        if (displays.Count > 0)
        {
            DisplayInfo destination = displays[index];
            var move = Screen.MoveMainWindowTo(destination, Vector2Int.zero);
            if (move != null) yield return move;
        }
#if MATREX_ALLOW_WINDOWED
        // A source build may opt out; production players do not expose a low-resolution window mode.
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--windowed") >= 0)
        { Screen.fullScreenMode = FullScreenMode.Windowed; changing = false; yield break; }
#endif
        var native = Screen.currentResolution;
        int width = native.width, height = native.height;
        if (width <= 0 || height <= 0)
        {
            var display = Screen.mainWindowDisplayInfo;
            width = display.width; height = display.height;
        }
        Screen.SetResolution(Math.Max(1, width), Math.Max(1, height), FullScreenMode.FullScreenWindow);
        yield return null; yield return null;
        Debug.Log("[DesktopDisplay] Native fullscreen " + Screen.width + "×" + Screen.height + "; UI backing scale " + UiScale + "; " + Screen.mainWindowDisplayInfo.name);
        changing = false;
    }
    private void Update()
    {
        if (changing || !Application.isFocused || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1;
#if !MATREX_ALLOW_WINDOWED
        if (Screen.fullScreenMode != FullScreenMode.FullScreenWindow) StartCoroutine(Apply());
#endif
    }
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    [DllImport(ObjC, EntryPoint = "objc_getClass")] private static extern IntPtr GetClass(string name);
    [DllImport(ObjC, EntryPoint = "sel_registerName")] private static extern IntPtr Selector(string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern double SendDouble(IntPtr receiver, IntPtr selector);
#endif
}
