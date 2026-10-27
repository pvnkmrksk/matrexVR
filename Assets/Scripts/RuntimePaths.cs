using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
        string data = Argument("--data-dir") ?? DefaultRecordingRoot(Application.dataPath, Application.persistentDataPath, Application.isEditor);
        string bundled = Path.GetFullPath(Application.streamingAssetsPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Application.isEditor && Path.GetFullPath(configs).StartsWith(bundled, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The writable configuration directory must be outside the bundled StreamingAssets folder.");
        // An incomplete existing profile can still contain an operator's sequence.
        // Only a wholly new profile gets the ready-to-run desktop defaults.
        bool freshProfile = !File.Exists(Path.Combine(configs, "system_config.json")) &&
            !File.Exists(Path.Combine(configs, "sequenceConfig.json"));
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
        if (!Application.isEditor && freshProfile) DesktopQuickStart.SeedFreshProfile(configs);
    }

    // Keep each installed player's recordings together, outside its signed bundle.
    // Protected installation directories use a stable per-installation fallback.
    public static string DefaultRecordingRoot(string dataPath, string persistentPath, bool editor)
    {
        if (editor) return Path.Combine(dataPath, "RunData");
        string canonical = Path.GetFullPath(dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var directory = new DirectoryInfo(canonical);
        var bundle = directory;
        while (bundle != null && !bundle.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) bundle = bundle.Parent;
        string parent = bundle?.Parent?.FullName ?? directory.Parent?.FullName;
        string name = bundle == null ? directory.Name : Path.GetFileNameWithoutExtension(bundle.Name);
        if (name.EndsWith("_Data", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 5);
        if (!string.IsNullOrEmpty(parent))
        {
            string local = Path.Combine(parent, name + " RunData");
            try
            {
                Directory.CreateDirectory(local);
                string probe = Path.Combine(local, ".write-check-" + Guid.NewGuid().ToString("N"));
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
                return local;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is System.Security.SecurityException)
            { /* Read-only installations still need a writable, isolated library. */ }
        }
        using (var hash = SHA256.Create())
        {
            string identity = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", "").Substring(0, 16).ToLowerInvariant();
            return Path.Combine(persistentPath, "Installations", identity, "RunData");
        }
    }
}
