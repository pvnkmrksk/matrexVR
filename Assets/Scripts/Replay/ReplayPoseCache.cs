using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

/// <summary>Disposable, local replay projections. CSV remains the acquisition record.</summary>
internal static class ReplayPoseCache
{
    private const long MaximumCacheBytes = 1024L * 1024 * 1024;
    private const string Extension = ".matpose";
    private static readonly object CacheGate = new object();
    public static string DefaultRoot
    {
        get
        {
            string root = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (string.IsNullOrEmpty(root) || !Path.IsPathRooted(root))
                root = Path.Combine(Application.persistentDataPath, "Cache");
            return Path.Combine(root, "MatrexReplay", "AnimalPose-v1");
        }
    }

    internal static string OutsideRecording(string root, string recording)
    {
        if (string.IsNullOrEmpty(root)) return null;
        string existing = Path.GetFullPath(root);
        while (!Directory.Exists(existing)) existing = Path.GetDirectoryName(existing);
        string physical = RecordingLibrary.CanonicalDirectory(existing);
        string candidate = Path.Combine(physical, Path.GetRelativePath(existing, Path.GetFullPath(root)));
        string relative = Path.GetRelativePath(RecordingLibrary.CanonicalDirectory(recording), candidate);
        return relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(relative) ? candidate : null;
    }

    internal static string Key(FileInfo source, string context, string sealedHash)
    {
        // A sealed digest can identify an unchanged copy across paths. Unsealed
        // sources use their local path and are invalidated when their stat changes.
        string identity = string.IsNullOrEmpty(sealedHash) ? source.FullName : sealedHash;
        return RunConfigArchive.Hash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", context, source.Name,
            source.Length.ToString(CultureInfo.InvariantCulture), source.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture), identity)));
    }

    internal static bool OptionalFailure(Exception error) => error is InvalidDataException || error is IOException ||
        error is UnauthorizedAccessException || error is System.Security.SecurityException;

    internal static bool TryRead(string root, string key, CancellationToken cancellation, Action<long,long> reportProgress,
        out Dictionary<string,List<ReplaySessionArchive.Sample>> rigs, out List<string> warnings)
    {
        rigs = null;
        warnings = null;
        string path = Path.Combine(root, key + Extension);
        if (!File.Exists(path)) return false;
        try
        {
            using (var input = File.OpenRead(path))
                (rigs, warnings) = ReplayPoseCacheCodec.Read(input, key, cancellation, reportProgress);
            try { File.SetLastWriteTimeUtc(path, DateTime.UtcNow); }
            catch (Exception error) when (OptionalFailure(error)) { }
            return true;
        }
        catch (Exception error) when (OptionalFailure(error))
        {
            TryDelete(path);
            return false;
        }
    }

    internal sealed class PendingWrite : IDisposable
    {
        private readonly string root, context, temporary;
        private readonly FileInfo source;
        private readonly long sourceBytes, sourceWriteTicks;
        private readonly FileStream output;
        private readonly ReplayPoseCacheCodec.Writer writer;
        private bool sourceHasPose, closed;

        internal PendingWrite(string root, FileInfo source, string context)
        {
            this.root = root;
            this.source = source;
            this.context = context;
            sourceBytes = source.Length;
            sourceWriteTicks = source.LastWriteTimeUtc.Ticks;
            Directory.CreateDirectory(root);
            temporary = Path.Combine(root, Guid.NewGuid().ToString("N") + ".partial");
            output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536);
            try { writer = new ReplayPoseCacheCodec.Writer(output); }
            catch { output.Dispose(); TryDelete(temporary); throw; }
        }

        public void Add(ReplaySessionArchive.Sample sample, CancellationToken cancellation)
        {
            sourceHasPose = true;
            writer.Add(sample, cancellation);
        }

        public void Complete(string sealedHash, IReadOnlyList<string> warnings)
        {
            source.Refresh();
            if (!sourceHasPose || !source.Exists || source.Length != sourceBytes || source.LastWriteTimeUtc.Ticks != sourceWriteTicks) return;
            string key = Key(source, context, sealedHash);
            writer.Complete(key, warnings);
            long bytes = output.Length;
            Close();
            if (bytes > MaximumCacheBytes) return;
            lock (CacheGate)
            {
                string target = Path.Combine(root, key + Extension);
                if (File.Exists(target)) return;
                File.Move(temporary, target);
                long total = 0;
                foreach (var item in new DirectoryInfo(root).EnumerateFiles("*" + Extension).OrderByDescending(item => item.LastWriteTimeUtc))
                {
                    total += item.Length;
                    if (total > MaximumCacheBytes && item.FullName != target) TryDelete(item.FullName);
                }
            }
        }

        public void Dispose()
        {
            try { Close(); }
            finally { TryDelete(temporary); }
        }

        private void Close()
        {
            if (closed) return;
            closed = true;
            try { writer.Dispose(); }
            finally { output.Dispose(); }
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception error) when (OptionalFailure(error)) { }
    }
}
