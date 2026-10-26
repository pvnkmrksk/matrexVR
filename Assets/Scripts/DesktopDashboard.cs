using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>An optional external viewer. No plotting server is started by opening an experiment.</summary>
public sealed class DesktopDashboard : MonoBehaviour
{
    private static DesktopDashboard instance;
    private const string BaseUrl = "http://127.0.0.1:8060";
    private bool opening;
    private FileStream launchLock;
    public static string Status { get; private set; } = "Optional external live plots; experiment rendering stays independent.";
    public static void Open()
    {
        if (MainController.Instance?.TelemetrySettings?.enabled == false)
        { Status = "Telemetry is disabled for this setup. Enable it under Advanced / Monitoring before opening live plots."; return; }
        if (instance == null)
        {
            var host = new GameObject("Optional external dashboard launcher");
            DontDestroyOnLoad(host); instance = host.AddComponent<DesktopDashboard>();
        }
        if (!instance.opening) instance.StartCoroutine(instance.OpenViewer());
    }
    private IEnumerator OpenViewer()
    {
        opening = true; Status = "Checking the local viewer…";
        using (var probe = UnityWebRequest.Get(BaseUrl + "/api/live/snapshot"))
        {
            probe.timeout = 2; yield return probe.SendWebRequest();
            if (IsViewer(probe)) { yield return ConnectViewer(!IsCurrent(probe)); opening = false; yield break; }
            if (probe.result == UnityWebRequest.Result.Success)
            { Fail("Port 8060 belongs to another service. Choose a remote bridge or stop that service explicitly."); yield break; }
        }
        Status = "Preparing the optional viewer…";
        string bundled = BundleFolder(), persistent = Application.persistentDataPath;
        Task<string> prepare = Task.Run(() => InstallViewer(bundled, persistent));
        while (!prepare.IsCompleted) yield return null;
        if (prepare.IsFaulted) { Fail("The optional viewer could not be prepared: " + prepare.Exception.GetBaseException().Message); yield break; }
        if (!StartViewer(prepare.Result)) { opening = false; yield break; }
        float deadline = Time.realtimeSinceStartup + 25;
        while (Time.realtimeSinceStartup < deadline)
        {
            using (var probe = UnityWebRequest.Get(BaseUrl + "/api/live/snapshot"))
            {
                probe.timeout = 1; yield return probe.SendWebRequest();
                if (IsViewer(probe))
                {
                    launchLock?.Dispose(); launchLock = null;
                    yield return ConnectViewer(!IsCurrent(probe)); opening = false; yield break;
                }
            }
            yield return new WaitForSecondsRealtime(.25f);
        }
        Fail("The optional viewer did not become ready. Your experiment continues independently; inspect its log before retrying.");
    }
    private static string Endpoint()
    {
        var settings = MainController.Instance?.TelemetrySettings;
        string host = settings?.bindAddress ?? "127.0.0.1";
        if (host == "*" || host == "0.0.0.0" || host == "::") host = "127.0.0.1";
        if (host.Contains(":") && !host.StartsWith("[")) host = "[" + host + "]";
        return "tcp://" + host + ":" + (settings?.port ?? 9880);
    }
    private static bool IsCurrent(UnityWebRequest request)
    {
        try
        {
            var receiver = JObject.Parse(request.downloadHandler.text)["receiver"];
            return (string)receiver?["endpoint"] == Endpoint() && (string)receiver?["topic"] == (MainController.Instance?.TelemetrySettings?.topic ?? "matrex.telemetry.v2");
        }
        catch (Exception) { return false; }
    }
    private IEnumerator ConnectViewer(bool reconnect)
    {
        string payload = new JObject { ["endpoint"] = Endpoint(), ["topic"] = MainController.Instance?.TelemetrySettings?.topic ?? "matrex.telemetry.v2" }.ToString();
        if (reconnect)
        using (var connect = UnityWebRequest.Post(BaseUrl + "/api/live/connect", payload, "application/json"))
        {
            connect.timeout = 3; yield return connect.SendWebRequest();
            if (connect.result != UnityWebRequest.Result.Success)
            { Status = "The viewer is running, but its publisher connection could not be changed: " + connect.error; yield break; }
        }
        Status = "Live viewer ready · " + Endpoint(); Application.OpenURL(BaseUrl + "/live/");
    }
    private static bool IsViewer(UnityWebRequest request)
    {
        if (request.result != UnityWebRequest.Result.Success) return false;
        try { var json = JObject.Parse(request.downloadHandler.text); return json["receiver"] is JObject && json["version"] != null; }
        catch (Exception) { return false; }
    }
    private static string BundleFolder()
    {
        string relative = Path.Combine("Companions", "dashboard", CompanionCatalog.Platform);
        string folder = Path.Combine(Application.dataPath, relative);
        return Directory.Exists(folder) ? folder : Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
    }
    internal static Task<string> PrepareBundledExecutableAsync()
    {
        string bundled=BundleFolder(),persistent=Application.persistentDataPath;
        return Task.Run(()=>InstallViewer(bundled,persistent));
    }
    private static string Hash(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    private static string InstallViewer(string bundled, string persistent)
    {
        string manifest = Path.Combine(bundled, "dashboard-bundle.json");
        if (!File.Exists(manifest)) throw new FileNotFoundException("This source build has no bundled dashboard. Use the Daari Deepa download or a bridge on another computer.");
        var info = JObject.Parse(File.ReadAllText(manifest));
        string digest = (string)info["executableSha256"], archive = (string)info["archiveSha256"];
        if (!System.Text.RegularExpressions.Regex.IsMatch(archive ?? "", "^[0-9a-f]{64}$") || !System.Text.RegularExpressions.Regex.IsMatch(digest ?? "", "^[0-9a-f]{64}$")) throw new IOException("Dashboard integrity metadata is invalid.");
        string root = Path.Combine(persistent, "Companions", "dashboard", archive.Substring(0, 16));
        string suffix = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? ".exe" : "";
        string executable = Path.Combine(root, "DaariDeepa", "DaariDeepa" + suffix);
        Directory.CreateDirectory(Path.GetDirectoryName(root));
        using (var gate = new FileStream(root + ".install.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            if (File.Exists(executable))
            {
                if (Hash(executable) != digest) throw new IOException("The installed viewer changed. Close it before repairing its versioned installation.");
                return executable;
            }
            string temporary = root + ".installing";
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Directory.CreateDirectory(temporary);
                foreach (string source in Directory.EnumerateFiles(bundled, "*", SearchOption.AllDirectories))
                {
                    string target = Path.Combine(temporary, Path.GetRelativePath(bundled, source));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(source, target, false);
                }
            }
            else
            {
                bool mac = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
                var start = new ProcessStartInfo {
                    FileName = mac ? "/usr/bin/ditto" : "/bin/cp",
                    Arguments = TrackerProcessManager.JoinArguments(mac ? new[] { bundled, temporary } : new[] { "-a", bundled + "/.", temporary }),
                    UseShellExecute = false, CreateNoWindow = true
                };
                using (var copy = Process.Start(start)) { copy.WaitForExit(); if (copy.ExitCode != 0) throw new IOException("Viewer installation copy failed."); }
            }
            if (Hash(Path.Combine(temporary, "DaariDeepa", "DaariDeepa" + suffix)) != digest) throw new IOException("Bundled viewer checksum mismatch.");
            if (Directory.Exists(root)) throw new IOException("Incomplete viewer installation: " + root);
            Directory.Move(temporary, root);
        }
        return executable;
    }
    private bool StartViewer(string executable)
    {
        try
        {
            string state = Path.Combine(Application.persistentDataPath, "CompanionState"); Directory.CreateDirectory(state);
            launchLock = new FileStream(Path.Combine(state, "dashboard-start.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            string receipt = Path.Combine(state, "dashboard-process.json");
            if (File.Exists(receipt))
            {
                try
                {
                    var previous = JObject.Parse(File.ReadAllText(receipt));
                    using (var existing = Process.GetProcessById((int)previous["pid"]))
                        if (!existing.HasExited && existing.StartTime.ToUniversalTime().Ticks == (long)previous["startedUtcTicks"])
                        { Status = "Waiting for the existing viewer process…"; return true; }
                }
                catch (ArgumentException) { }
                catch (Newtonsoft.Json.JsonException) { } // An interrupted older launch can leave a partial receipt.
                catch (InvalidCastException) { }
            }
            var info = TrackerProcessManager.DetachedStart(executable, new[] { "--no-browser", "--live-only", "--host", "127.0.0.1", "--port", "8060", "--telemetry", Endpoint(), "--telemetry-topic", MainController.Instance?.TelemetrySettings?.topic ?? "matrex.telemetry.v2" }, Path.GetDirectoryName(executable), Path.Combine(state, "dashboard.log"));
            using (var process = Process.Start(info))
            {
                if (process == null) throw new IOException("The viewer process could not start.");
                string temporary = receipt + ".tmp";
                File.WriteAllText(temporary, new JObject { ["pid"] = process.Id, ["startedUtcTicks"] = process.StartTime.ToUniversalTime().Ticks }.ToString());
                if (File.Exists(receipt)) File.Replace(temporary, receipt, null);
                else File.Move(temporary, receipt);
            }
            Status = "Starting the independent viewer…"; return true;
        }
        catch (IOException error) { Status = "Viewer launch unavailable: " + error.Message; launchLock?.Dispose(); launchLock = null; return false; }
        catch (Exception error) { Status = "Viewer launch failed: " + error.Message; launchLock?.Dispose(); launchLock = null; return false; }
    }
    private void Fail(string message) { Status = message; launchLock?.Dispose(); launchLock = null; opening = false; UnityEngine.Debug.LogWarning(message); }
    private void OnDestroy() { launchLock?.Dispose(); if (instance == this) instance = null; }
}
