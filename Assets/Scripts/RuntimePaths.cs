using System;
using System.IO;
using UnityEngine;

/// <summary>Editable experiments and recordings live outside installed application bundles.</summary>
public static class RuntimePaths
{
    private static string configRoot, runDataRoot;
    public static string ConfigRoot { get { Initialize(); return configRoot; } }
    public static string RunDataRoot { get { Initialize(); return runDataRoot; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { configRoot = null; runDataRoot = null; }

    private static string Argument(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == name) return Path.GetFullPath(args[i + 1]);
        return null;
    }

    private static void Initialize()
    {
        if (configRoot != null) return;
        string configs = Argument("--config-dir") ?? (Application.isEditor ? Application.streamingAssetsPath : Path.Combine(Application.persistentDataPath, "Config"));
        string data = Argument("--data-dir") ?? (Application.isEditor ? Path.Combine(Application.dataPath, "RunData") : Path.Combine(Application.persistentDataPath, "RunData"));
        string bundled = Path.GetFullPath(Application.streamingAssetsPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Application.isEditor && Path.GetFullPath(configs).StartsWith(bundled, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The writable configuration directory must be outside the bundled StreamingAssets folder.");
        Directory.CreateDirectory(configs);
        Directory.CreateDirectory(data);
        if (!Application.isEditor && Path.GetFullPath(configs) != Path.GetFullPath(Application.streamingAssetsPath))
        {
            // Seed missing files only. An application update must never overwrite the operator's JSON.
            foreach (string source in Directory.EnumerateFiles(Application.streamingAssetsPath, "*", SearchOption.AllDirectories))
            {
                if (source.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string destination = Path.Combine(configs, Path.GetRelativePath(Application.streamingAssetsPath, source));
                if (File.Exists(destination)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination, false);
            }
        }
        string system = Path.Combine(configs, "system_config.json");
        string template = Path.Combine(configs, "Templates", "system_config.template.json");
        if (!File.Exists(system) && File.Exists(template)) File.Copy(template, system, false);
        runDataRoot = data;
        configRoot = configs;
    }
}
