using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/// <summary>Resolves independently installed trackers without starting or modifying them.</summary>
public static class TrackerInstallation
{
    private static readonly bool Windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static readonly bool Mac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    private static readonly bool Unix = Mac || RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    private static readonly string[] FlightNames = { "rekkefly", "Rekkefly" };
    private static readonly string[] WalkingNames = { "hejjetrac", "hejjetrak", "HejjéTrac", "Hejjetrak", "Hejjetrac" };
    private static readonly ConcurrentDictionary<(string path, long size, long ticks), Lazy<bool>> ControlSupport =
        new ConcurrentDictionary<(string path, long size, long ticks), Lazy<bool>>();

    private static string[] Names(ClosedLoopMode mode) => TrackerBackend.IsFlight(mode) ? FlightNames :
        TrackerBackend.IsWalking(mode) ? WalkingNames : Array.Empty<string>();

    public static bool ModernBackend(ClosedLoopMode mode) => mode == ClosedLoopMode.Rekkefly || mode == ClosedLoopMode.Hejjetrac;

    public static string Find(ClosedLoopMode mode)
    {
        if ((!Windows && !Unix) || !ModernBackend(mode)) return "";
        string[] names = Names(mode);
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (Mac)
        {
            foreach (string root in new[] { "/Applications", HomeChild(home, "Applications") })
            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(root)) continue;
                string executable = BundleExecutable(Path.Combine(root, name + ".app"), names);
                if (!string.IsNullOrEmpty(executable)) return executable;
            }
        }
        foreach (string directory in Directories(names, home))
        foreach (string name in names)
        {
            string executable = Path.Combine(directory, Windows ? name + ".exe" : name);
            if (IsExecutable(executable)) return executable;
        }
        return "";
    }

    private static string HomeChild(string root, params string[] path) =>
        string.IsNullOrWhiteSpace(root) ? "" : path.Aggregate(root, Path.Combine);

    private static IEnumerable<string> Directories(string[] names, string home)
    {
        var candidates = new List<string>();
        if (Windows)
        {
            foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                HomeChild(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs") })
            foreach (string name in names)
            {
                candidates.Add(HomeChild(root, name));
                candidates.Add(HomeChild(root, name, "bin"));
            }
            candidates.Add(HomeChild(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links"));
        }
        else
        {
            candidates.AddRange(new[] { HomeChild(home, ".local", "bin"), HomeChild(home, "bin"),
                "/usr/local/bin", "/usr/bin", "/opt/homebrew/bin" });
            foreach (string name in names)
            {
                candidates.Add(Path.Combine("/opt", name));
                candidates.Add(Path.Combine("/opt", name, "bin"));
            }
        }
        candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
        return candidates.Select(path => path.Trim().Trim('"'))
            .Where(path => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path))
            .Distinct(Windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }

    public static string ResolveSelection(string path, ClosedLoopMode mode)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new ArgumentException("Choose an absolute path to the native tracker launcher.");
        path = Path.GetFullPath(path);
        if (Mac && string.Equals(Path.GetExtension(path.TrimEnd('/')), ".app", StringComparison.OrdinalIgnoreCase))
            path = BundleExecutable(path, Names(mode));
        if (string.IsNullOrEmpty(path) || !IsExecutable(path))
            throw new ArgumentException("Choose a native tracker executable or a complete macOS tracker app.");
        return path;
    }

    private static string BundleExecutable(string path, string[] names)
    {
        foreach (string name in names)
        {
            string candidate = Path.Combine(path, "Contents", "MacOS", name);
            if (IsExecutable(candidate)) return candidate;
        }
        return "";
    }

    private static bool IsExecutable(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".bat" || extension == ".cmd" || extension == ".ps1" || extension == ".sh") return false;
        return File.Exists(path) && (Windows ? extension == ".exe" : Unix && access(path, 1) == 0);
    }

    [DllImport("libc", EntryPoint = "access")]
    private static extern int access(string pathname, int mode);

    internal static bool SupportsRecordingControl(string executable)
    {
        var file = new FileInfo(executable);
        var identity = (executable, file.Length, file.LastWriteTimeUtc.Ticks);
        return ControlSupport.GetOrAdd(identity, key => new Lazy<bool>(() => ProbeControl(key.path))).Value;
    }

    private static bool ProbeControl(string executable)
    {
        const int budgetMs = 650;
        var clock = Stopwatch.StartNew();
        try
        {
            var start = new ProcessStartInfo(executable, "--help")
            {
                WorkingDirectory = Path.GetDirectoryName(executable),
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (var process = Process.Start(start))
            {
                if (process == null) return false;
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(Math.Max(0, budgetMs - (int)clock.ElapsedMilliseconds)))
                {
                    try { process.Kill(); }
                    catch (InvalidOperationException) { }
                    return false;
                }
                if (process.ExitCode != 0 || !Task.WaitAll(new Task[] { output, error },
                    Math.Max(0, budgetMs - (int)clock.ElapsedMilliseconds))) return false;
                string help = output.Result + "\n" + error.Result;
                return Regex.IsMatch(help, @"(^|\s)--control-dir(\s|=|$)") &&
                    Regex.IsMatch(help, @"(^|\s)--control-instance(\s|=|$)");
            }
        }
        catch (Exception error) when (error is IOException || error is InvalidOperationException ||
            error is System.ComponentModel.Win32Exception || error is AggregateException)
        { return false; }
    }
}
