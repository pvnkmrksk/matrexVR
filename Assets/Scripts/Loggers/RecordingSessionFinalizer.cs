using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Pure file/worker orchestration. Finalized means drained and inspected, not a completed experimental protocol.</summary>
public sealed class RecordingSessionFinalizer
{
    private readonly string directory, archivePath;
    private readonly JObject player;
    private readonly object gate = new object();
    private readonly object archiveGate = new object();
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    private readonly DateTimeOffset created = DateTimeOffset.UtcNow;
    private JObject latest;
    private bool shutdown;
    public Task<JObject> FinalizationTask { get; private set; }
    public string Error { get { lock (gate) return (string)latest?["lifecycle"]?["error"]; } }

    public RecordingSessionFinalizer(string directory, string archivePath, JObject player)
    {
        this.directory = Path.GetFullPath(directory); this.archivePath = Path.GetFullPath(archivePath);
        this.player = (JObject)player.DeepClone();
        Directory.CreateDirectory(this.directory);
        latest = new JObject { ["schemaVersion"] = 1, ["analysisVersion"] = 1,
            ["lifecycle"] = Lifecycle("open", null, false, null) };
        WriteAtomic(this.directory, latest);
    }

    public Task<JObject> FinalizeAsync(string endReason, Func<CancellationToken, Task> drain,
        Func<CancellationToken, Task<JObject>> media, Func<JArray> writerEvidence,
        Action<string, string[], string[]> observePose = null, string[] closeErrors = null)
    {
        lock (gate)
        {
            if (FinalizationTask != null) return FinalizationTask;
            if (shutdown) throw new InvalidOperationException("Recording shutdown has already begun.");
            latest["lifecycle"] = Lifecycle("open", endReason, false, null);
            latest["lifecycle"]["finalizationPending"] = true;
            WriteAtomic(directory, latest);
            return FinalizationTask = Task.Run(async () =>
            {
                bool drained = false;
                JObject summary = null;
                JArray writers = null;
                try
                {
                    await drain(cancellation.Token).ConfigureAwait(false); drained = true;
                    writers = writerEvidence?.Invoke() ?? new JArray();
                    cancellation.Token.ThrowIfCancellationRequested();
                    summary = RecordingSummary.Scan(directory, true, cancellation.Token, observePose, true, writers);
                    summary["writerEvidence"] = writers;
                    JObject receipt = await media(cancellation.Token).ConfigureAwait(false);
                    cancellation.Token.ThrowIfCancellationRequested();
                    string[] failures = writers.OfType<JObject>().Select(item => (long?)item["droppedFrames"]>0 ? "Population writer dropped "+item["droppedFrames"]+" rendered frames: "+item["relativePath"] : (string)item["failure"])
                        .Concat(closeErrors ?? Array.Empty<string>()).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
                    bool readFailure = (bool?)summary["changedDuringScan"] == true ||
                        ((JArray)summary["files"]).OfType<JObject>().Any(file => file["readError"] != null);
                    string error = failures.Length > 0 ? string.Join("; ", failures) : readFailure ? "Some recording files could not be inspected consistently." : null;
                    summary["metadataFiles"] = RecordingSummary.MetadataInventory(directory);
                    summary["writerEvidence"] = writers; summary["media"] = receipt;
                    summary["lifecycle"] = Lifecycle(error == null ? "finalized" : "error", endReason, failures.Length == 0, error);
                    summary["lifecycle"]["finalizedUtc"] = DateTimeOffset.UtcNow;
                    lock (gate)
                    {
                        if (shutdown) return (JObject)latest.DeepClone();
                        latest = summary; WriteAtomic(directory, latest);
                    }
                    return (JObject)summary.DeepClone();
                }
                catch (Exception error)
                {
                    lock (gate)
                    {
                        if (shutdown) return (JObject)latest.DeepClone();
                        latest = summary ?? latest;
                        if (writers != null) latest["writerEvidence"] = writers;
                        latest["lifecycle"] = Lifecycle("error", endReason, drained, error.Message);
                        WriteAtomic(directory, latest);
                        return (JObject)latest.DeepClone();
                    }
                }
            });
        }
    }

    /// <summary>Editor stop/crash-like teardown cannot promise an unfinished async media copy or summary completed.</summary>
    public void MarkShutdown(bool writersDrained, JObject mediaReceipt, string error = null, JArray writerEvidence = null)
    {
        lock (gate)
        {
            if (FinalizationTask != null && FinalizationTask.IsCompleted) return;
            shutdown = true; cancellation.Cancel();
            latest["lifecycle"] = Lifecycle(error == null ? "open" : "error", "application-shutdown", writersDrained, error);
            latest["lifecycle"]["finalizationPending"] = true;
            latest["lifecycle"]["warning"] = "Asynchronous recording finalization did not complete before shutdown.";
            latest["media"] = mediaReceipt;
            if (writerEvidence != null) latest["writerEvidence"] = writerEvidence;
            WriteAtomic(directory, latest);
        }
    }

    // Compatibility for callers that explicitly request a ZIP. Ordinary Stop seals the run directory.
    public void WriteArchive()
    {
        lock (archiveGate)
        {
            lock (gate)
                if (FinalizationTask == null || !FinalizationTask.IsCompleted || (string)latest?["lifecycle"]?["status"] != "finalized")
                    throw new InvalidOperationException("Only a finalized recording can be archived.");
            CreateArchive(directory, archivePath, CancellationToken.None);
        }
    }

    private JObject Lifecycle(string status, string reason, bool drained, string error) => new JObject {
        ["status"] = status, ["createdUtc"] = created, ["writersDrained"] = drained, ["endReason"] = reason,
        ["sequenceOutcome"] = reason == "completed" ? "completed" : reason == "error" || reason == "failed" ? "failed" :
            reason?.StartsWith("cancelled",StringComparison.Ordinal)==true ? "cancelled" :
            reason == "stopped" || reason == "local-control-stop" || reason == "application-quit" ? "stopped" : "unknown",
        ["player"] = player.DeepClone(), ["error"] = error
    };

    public static void WriteAtomic(string directory, JObject summary)
    {
        string target = Path.Combine(directory, RecordingSummary.ManifestName);
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".writing";
        try
        {
            File.WriteAllText(temporary, summary.ToString(Formatting.Indented) + "\n", new UTF8Encoding(false));
            if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void CreateArchive(string directory, string archive, CancellationToken token)
    {
        if (File.Exists(archive)) return;
        string temporary = archive + "." + Guid.NewGuid().ToString("N") + ".writing";
        try
        {
            token.ThrowIfCancellationRequested();
            using (var output = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                var buffer = new byte[256 * 1024];
                foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    token.ThrowIfCancellationRequested();
                    string relative = Path.GetRelativePath(directory, file).Replace(Path.DirectorySeparatorChar, '/');
                    string extension = Path.GetExtension(file).ToLowerInvariant();
                    var compression = PopulationRecording.IsFile(file) || extension == ".gz" || extension == ".zip" ||
                        extension == ".mp4" || extension == ".png" || extension == ".jpg"
                        ? CompressionLevel.NoCompression : CompressionLevel.Fastest;
                    var entry = output.CreateEntry(relative, compression);
                    DateTime modified = File.GetLastWriteTime(file);
                    if (modified.Year >= 1980 && modified.Year <= 2107) entry.LastWriteTime = modified;
                    using (var source = File.OpenRead(file))
                    using (var destination = entry.Open())
                    {
                        int count;
                        while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            token.ThrowIfCancellationRequested();
                            destination.Write(buffer, 0, count);
                        }
                    }
                }
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, archive);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
