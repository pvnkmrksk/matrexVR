using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>Editable experiments and recordings live outside installed application bundles.</summary>
public static class RuntimePaths
{
    private const string RunDataGuide = "RunData_README.md";
    private static string configRoot, runDataRoot;
    public static string ConfigRoot { get { Initialize(); return configRoot; } }
    public static string RunDataRoot { get { Initialize(); return runDataRoot; } }
    public static string[] GetReplayLibraryRoots(string activeRoot = null) => FindReplayLibraryRoots(activeRoot ?? RunDataRoot,
        Argument("--replay-library-dir"), Application.dataPath, Application.persistentDataPath, Application.isEditor);

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
        if (!Application.isEditor && !File.Exists(Path.Combine(data, "recording_manifest.json")) &&
            !File.Exists(Path.Combine(data, "config_manifest.json")))
            CopyRunDataReadme(data);
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

    internal static void CopyRunDataReadme(string directory)
    {
        string source = Path.Combine(Application.streamingAssetsPath, RunDataGuide);
        string destination = Path.Combine(directory, "README.md");
        try
        {
            if (File.Exists(source) && !File.Exists(destination))
                File.Copy(source, destination, overwrite: false);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is System.Security.SecurityException)
        { /* Optional documentation must never block recording. */ }
    }

    // Keep each installed player's recordings together, outside its signed bundle.
    // Protected installation directories use a stable per-installation fallback.
    public static string DefaultRecordingRoot(string dataPath, string persistentPath, bool editor)
    {
        if (editor) return Path.Combine(dataPath, "RunData");
        var (installation, _) = PlayerInstallation(dataPath);
        if (installation.Parent != null)
        {
            string local = Path.Combine(installation.Parent.FullName, "RunData");
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
        return ProtectedRecordingRoot(dataPath, persistentPath);
    }

    internal static string[] FindReplayLibraryRoots(string active, string previous, string dataPath, string persistentPath, bool editor)
    {
        var comparer = Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var roots = new List<string> { active };
        if (!string.IsNullOrEmpty(previous)) roots.Add(previous);
        if (editor) return roots.Distinct(comparer).ToArray();

        var (installation, name) = PlayerInstallation(dataPath);
        var playerParent = installation.Parent;
        if (playerParent != null)
        {
            AddLayouts(playerParent.FullName);
            // Versioned desktop players often sit beside each other in the same workspace.
            // Only inspect matching player installations and their two known recording locations.
            if (playerParent.Parent != null)
                try
                {
                    foreach (var sibling in playerParent.Parent.EnumerateDirectories())
                    {
                        if (comparer.Equals(sibling.FullName, playerParent.FullName) ||
                            !Directory.Exists(Path.Combine(sibling.FullName, installation.Name))) continue;
                        AddLayouts(sibling.FullName);
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
        }
        AddIfPresent(ProtectedRecordingRoot(dataPath, persistentPath));
        AddIfPresent(Path.Combine(persistentPath, "RunData"));
        return roots.Select(path => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Distinct(comparer).ToArray();

        void AddLayouts(string parent)
        {
            AddIfPresent(Path.Combine(parent, "RunData"));
            AddIfPresent(Path.Combine(parent, name + " RunData"));
        }

        void AddIfPresent(string path)
        {
            if (Directory.Exists(path)) roots.Add(path);
        }
    }

    private static (DirectoryInfo directory, string name) PlayerInstallation(string dataPath)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        for (var bundle = directory; bundle != null; bundle = bundle.Parent)
            if (bundle.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                return (bundle, Path.GetFileNameWithoutExtension(bundle.Name));
        string name = directory.Name;
        if (name.EndsWith("_Data", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 5);
        return (directory, name);
    }

    private static string ProtectedRecordingRoot(string dataPath, string persistentPath)
    {
        string canonical = Path.GetFullPath(dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        using (var hash = SHA256.Create())
        {
            string identity = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", "").Substring(0, 16).ToLowerInvariant();
            return Path.Combine(persistentPath, "Installations", identity, "RunData");
        }
    }
}
