using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Explicit local-player verification captures the real backing framebuffer, never a rescaled Editor proxy.</summary>
public sealed class DesktopPlayerDiagnostics : MonoBehaviour
{
    private string output;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Application.isEditor) return;
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--desktop-diagnostics");
        if (index < 0 || index + 1 >= args.Length) return;
        var host = new GameObject("Explicit player display verification");
        DontDestroyOnLoad(host);
        host.AddComponent<DesktopPlayerDiagnostics>().output = Path.GetFullPath(args[index + 1]);
    }
    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(5);
        yield return new WaitForEndOfFrame();
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var resolution = Screen.currentResolution;
        var display = Screen.mainWindowDisplayInfo;
        var ui = ConfigurationUI.ConfigWorkbench.Instance;
        var panel = ui?.Document?.panelSettings;
        File.WriteAllText(output + ".json", JsonConvert.SerializeObject(new {
            unity = Application.unityVersion, os = SystemInfo.operatingSystem,
            paths = new { application = Application.dataPath, persistent = Application.persistentDataPath, configs = RuntimePaths.ConfigRoot, recordings = RuntimePaths.RunDataRoot },
            screen = new { width = Screen.width, height = Screen.height, mode = Screen.fullScreenMode.ToString(), dpi = Screen.dpi },
            native = new { width = resolution.width, height = resolution.height },
            display = new { name = display.name, width = display.width, height = display.height },
            ui = new { scale = DesktopDisplay.UiScale, panelScale = panel != null ? panel.scale : 0, directFramebuffer = panel != null && panel.targetTexture == null,
                logicalWidth = ui != null ? ui.Document.rootVisualElement.resolvedStyle.width : 0,
                logicalHeight = ui != null ? ui.Document.rootVisualElement.resolvedStyle.height : 0 },
            profile = ui != null && DesktopQuickStart.IsQuickStart(ui.Workspace) ? new { mode = DesktopQuickStart.Mode(ui.Workspace), scene = DesktopQuickStart.Scene(ui.Workspace), demo = DesktopQuickStart.IsDemo(ui.Workspace) } : null,
            performance = fps.Latest
        }, Formatting.Indented));
        ScreenCapture.CaptureScreenshot(output + ".png");
        yield return null; yield return new WaitForSecondsRealtime(1);
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--quit-after-diagnostics") >= 0) Application.Quit();
        Destroy(gameObject);
    }
}
