using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Read-only recording discovery and summaries. Only the separately supplied cache directory is writable.</summary>
public static class RecordingLibrary
{
    private const long MaximumJsonBytes=4*1024*1024;
    private const long SmallSummaryBytes=2*1024*1024;
    private const int PosePreviewRows = 256;
    private static readonly string[] PoseColumns = { "GameObjectPosX", "GameObjectPosY", "GameObjectPosZ", "GameObjectRotX", "GameObjectRotY", "GameObjectRotZ" };
    private static readonly StringComparer PathComparer = Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    public sealed class Entry
    {
        public string DirectoryPath { get; internal set; }
        public string Id { get; internal set; }
        public string Title { get; internal set; }
        public string DateLabel { get; internal set; }
        public JObject Summary { get; internal set; }
        public string Error { get; internal set; }
        public bool Indexed { get; internal set; }
        public bool Cached { get; internal set; }
        public bool Finalized { get; internal set; }
        public bool PreviewOnly { get; internal set; }
        public bool HasPossiblePoseRows { get; internal set; }
        public string RecordingState { get; internal set; }="unknown";
        public string FreshnessToken { get; internal set; }
        private string searchText;
        public string SearchText {get {if(searchText==null)searchText=Title+" "+Id+" "+Summary?["metadata"]+" "+Summary?["observed"];return searchText;}}
        public bool ChangedDuringScan => (bool?)Summary?["changedDuringScan"]==true;
        public long PlayableRows => (long?)Summary?["poseRows"]?["playable"]??0;
        public bool CanReplay => Indexed&&(PlayableRows>0||HasPossiblePoseRows)&&string.IsNullOrEmpty(Error)&&!ChangedDuringScan;
        public bool Omit => Indexed&&Summary!=null&&PlayableRows==0&&!HasPossiblePoseRows&&(long?)Summary["poseRows"]?["rejected"]==0;
    }
    /// <summary>Only the active source waits for its writer/media barrier; older runs remain available.</summary>
    public static bool IsFinalizingRecording(string selectedDirectory,string activeDirectory,bool isFinalizing)
    {
        if(!isFinalizing||string.IsNullOrWhiteSpace(selectedDirectory)||string.IsNullOrWhiteSpace(activeDirectory))return false;
        try
        {
            return PathComparer.Equals(CanonicalDirectory(selectedDirectory), CanonicalDirectory(activeDirectory));
        }
        catch(ArgumentException){return false;}
        catch(NotSupportedException){return false;}
    }
    public static IReadOnlyList<Entry> Discover(string root,CancellationToken cancellation=default,bool finalizedOnly=false)
        => Discover(new[]{root},cancellation,finalizedOnly);

    public static IReadOnlyList<Entry> Discover(IEnumerable<string> roots, CancellationToken cancellation=default, bool finalizedOnly=false)
    {
        var entries=new List<Entry>();
        foreach (string directory in DiscoveryDirectories(roots))
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                if (finalizedOnly)
                {
                    var lifecycle = ReadSmall(Path.Combine(directory,RecordingSummary.ManifestName))?["lifecycle"];
                    if ((string)lifecycle?["status"] != "finalized" || (bool?)lifecycle?["writersDrained"] != true) continue;
                }
                if(HasPoseHeader(directory,cancellation))entries.Add(Preview(directory));
            }
            catch(OperationCanceledException){throw;}
            catch(Exception error){var entry=Preview(directory);entry.Error="Cannot inspect recording: "+error.Message;entry.Indexed=true;entries.Add(entry);}
        }
        return entries.OrderByDescending(entry=>entry.Id,StringComparer.Ordinal).ToArray();
    }

    internal static IEnumerable<string> DiscoveryDirectories(IEnumerable<string> roots)
    {
        var seen = new HashSet<string>(PathComparer);
        foreach (string root in roots.Where(Directory.Exists).Select(CanonicalDirectory).Distinct(PathComparer))
        {
            if (seen.Add(root)) yield return root;
            foreach (string child in ChildDirectories(root))
            {
                string name = Path.GetFileName(child);
                if (name.StartsWith(".", StringComparison.Ordinal) || name.Equals("Backup", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Cache", StringComparison.OrdinalIgnoreCase)) continue;
                string path = CanonicalDirectory(child);
                if (seen.Add(path)) yield return path;
                if (name.Equals("RunData", StringComparison.OrdinalIgnoreCase) || name.EndsWith(" RunData", StringComparison.OrdinalIgnoreCase))
                    foreach (string run in ChildDirectories(path))
                    {
                        string runName = Path.GetFileName(run);
                        if (runName.StartsWith(".", StringComparison.Ordinal) || runName.Equals("Backup", StringComparison.OrdinalIgnoreCase) ||
                            runName.Equals("Cache", StringComparison.OrdinalIgnoreCase)) continue;
                        string recording = CanonicalDirectory(run);
                        if (seen.Add(recording)) yield return recording;
                    }
            }
        }
    }

    internal static string CanonicalDirectory(string directory)
    {
        string full = new DirectoryInfo(directory).FullName;
        if (Path.DirectorySeparatorChar != '/') return full;
        // Packaged Linux/Mac installations can share one RunData through directory links.
        // Resolving those aliases keeps recordings and active recording guards unique.
        IntPtr resolved = IntPtr.Zero;
        try
        {
            resolved = realpath(full, IntPtr.Zero);
            return resolved == IntPtr.Zero ? full : Marshal.PtrToStringUTF8(resolved) ?? full;
        }
        catch (DllNotFoundException) { return full; }
        catch (EntryPointNotFoundException) { return full; }
        finally { if (resolved != IntPtr.Zero) free(resolved); }
    }

    [DllImport("libc")] private static extern IntPtr realpath(string path, IntPtr resolved);
    [DllImport("libc")] private static extern void free(IntPtr pointer);

    private static string[] ChildDirectories(string directory)
    {
        try { return Directory.GetDirectories(directory); }
        catch (IOException) { return Array.Empty<string>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    public static Entry Inspect(string directory,string cacheDirectory,CancellationToken cancellation=default,bool previewOnly=false)
    {
        cancellation.ThrowIfCancellationRequested();var entry=Preview(directory);entry.Indexed=true;
        try
        {
            CheckMetadataSize(entry.DirectoryPath, previewOnly);
            string fingerprint=Fingerprint(entry.DirectoryPath,cancellation);entry.FreshnessToken=fingerprint;
            string manifestPath = Path.Combine(entry.DirectoryPath, RecordingSummary.ManifestName);
            var manifest = previewOnly ? TryReadSmall(manifestPath) : ReadSmall(manifestPath);
            entry.RecordingState=(string)manifest?["lifecycle"]?["status"]??"unknown";
            JObject summary;
            if(RecordingSummary.TryReadFinalized(entry.DirectoryPath,out summary,out _))entry.Finalized=true;
            else
            {
                summary=ReadCache(entry.DirectoryPath,cacheDirectory,fingerprint);
                if(summary!=null)entry.Cached=true;
                else if (previewOnly && !CanSummarizeQuickly(entry.DirectoryPath))
                {
                    entry.PreviewOnly = true;
                    summary = PreviewSummary(entry, manifest, cancellation);
                }
                else
                {
                    summary=RecordingSummary.Scan(entry.DirectoryPath,false,cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    if(fingerprint!=Fingerprint(entry.DirectoryPath,cancellation))summary["changedDuringScan"]=true;
                    if((bool?)summary["changedDuringScan"]!=true)WriteCache(entry.DirectoryPath,cacheDirectory,fingerprint,summary);
                }
            }
            cancellation.ThrowIfCancellationRequested();
            entry.Summary=(JObject)summary.DeepClone();
            if(fingerprint!=Fingerprint(entry.DirectoryPath,cancellation)){entry.Summary["changedDuringScan"]=true;entry.Finalized=false;}
            string title=(string)summary["metadata"]?["experimentName"];
            if(!string.IsNullOrWhiteSpace(title))entry.Title=title;
            string first=(string)summary["wallClockStartLocal"];
            if(DateTime.TryParse(first,CultureInfo.InvariantCulture,DateTimeStyles.None,out var started))entry.DateLabel=started.ToString("dd MMM yyyy · HH:mm",CultureInfo.InvariantCulture);
            // Replay uses these exact saved config checks too. Missing legacy JSON
            // remains a warning; corrupt saved snapshots cannot be presented as ready.
            if (!previewOnly) new ReplaySessionArchive(entry.DirectoryPath);
            else ReadSmall(Path.Combine(entry.DirectoryPath, "config_manifest.json"));
        }
        catch(OperationCanceledException){throw;}
        catch(Exception error){entry.Error=error.Message;}
        return entry;
    }
    private static Entry Preview(string directory)
    {
        string path=Path.GetFullPath(directory),id=Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        string date=DateTime.TryParseExact(id.Length>=15?id.Substring(0,15):id,"yyyyMMdd_HHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed)?parsed.ToString("dd MMM yyyy · HH:mm",CultureInfo.InvariantCulture):"Date unavailable";
        var entry=new Entry{DirectoryPath=path,Id=id,Title=id,DateLabel=date};
        try
        {
            var metadata=ReadSmall(Path.Combine(path,"AnimalMetadata.json"))??ReadSmall(Path.Combine(path,"FlyMetaData.json"));
            string title=(string)metadata?["ExperimentName"];if(!string.IsNullOrWhiteSpace(title))entry.Title=title;
        }
        catch { /* Full inspection will report malformed/oversized metadata. */ }
        return entry;
    }
    private static bool HasPoseHeader(string directory,CancellationToken cancellation)
    {
        foreach(string file in ReplaySessionArchive.CsvFiles(directory))
        {
            cancellation.ThrowIfCancellationRequested();
            try { using (var input = ReplaySessionArchive.OpenCsv(file)) if (ReadPoseHeader(input, cancellation) != null) return true; }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException) { }
        }
        return false;
    }
    private static string[] ReadPoseHeader(StreamReader input, CancellationToken cancellation)
    {
        var text = new StringBuilder();
        int character;
        while (text.Length < 65536 && (character = input.Read()) >= 0 && character != '\n')
        {
            if ((text.Length & 4095) == 0) cancellation.ThrowIfCancellationRequested();
            text.Append((char)character);
        }
        if (text.Length == 0 || text.Length >= 65536) return null;
        var header = ReplaySessionArchive.Csv(text.ToString().TrimStart('\ufeff').TrimEnd('\r'));
        return header.Contains("Current Time") && header.Contains("VR") && PoseColumns.All(header.Contains) ? header : null;
    }

    private static bool CanSummarizeQuickly(string directory)
    {
        string[] files = ReplaySessionArchive.RecordedFiles(directory);
        if (files.Length > 32 || files.Any(path => path.EndsWith(".csv.gz", StringComparison.OrdinalIgnoreCase))) return false;
        var journal = new FileInfo(Path.Combine(directory, "presentations.jsonl"));
        long bytes = journal.Exists ? journal.Length : 0;
        foreach (string file in files)
        {
            if (!PopulationRecording.IsFile(file)) bytes += new FileInfo(file).Length;
            if (bytes > SmallSummaryBytes) return false;
        }
        return bytes <= SmallSummaryBytes;
    }

    private static JObject PreviewSummary(Entry entry, JObject manifest, CancellationToken cancellation)
    {
        string directory = entry.DirectoryPath;
        var warnings = new List<string>();
        var files = new JArray();
        var rigs = new SortedSet<string>(StringComparer.Ordinal);
        var scenes = new SortedSet<string>(StringComparer.Ordinal);
        var expected = new Dictionary<string, JObject>(PathComparer);
        bool finalized = entry.RecordingState == "finalized" && (bool?)manifest?["lifecycle"]?["writersDrained"] == true;
        if (finalized && manifest["files"] is JArray inventory)
            foreach (var item in inventory.OfType<JObject>())
            {
                string path = (string)item["relativePath"];
                if (!string.IsNullOrEmpty(path) && path == Path.GetFileName(path)) expected[path] = item;
            }

        int matched = 0, matchedPoses = 0;
        bool sampledPoses = false;
        long sampledPlayable = 0, sampledRejected = 0;
        var policy = RecordedPosePolicy.FromSavedSequence(null, false);
        foreach (string file in ReplaySessionArchive.RecordedFiles(directory))
        {
            cancellation.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            var evidence = new JObject { ["relativePath"] = info.Name, ["bytes"] = info.Length, ["rowsInspected"] = false };
            if (expected.TryGetValue(info.Name, out var saved) && info.Length == (long?)saved["bytes"] &&
                (long?)saved["lastWriteUtcTicks"] is long ticks && Math.Abs(info.LastWriteTimeUtc.Ticks - ticks) < TimeSpan.TicksPerMillisecond)
            {
                // A filesystem copy can round the timestamp. This is only a library preview.
                matched++;
                if ((string)saved["role"] == "animal-pose")
                {
                    matchedPoses++;
                    sampledPlayable += (long?)saved["playableRows"] ?? 0;
                    sampledRejected += (long?)saved["rejectedRows"] ?? 0;
                    foreach (var rig in saved["rigIds"] as JArray ?? new JArray()) rigs.Add((string)rig);
                }
                files.Add(saved.DeepClone());
                continue;
            }
            files.Add(evidence);
            if (PopulationRecording.IsFile(file))
            {
                evidence["role"] = "agent-population";
                continue;
            }
            try
            {
                using (var input = ReplaySessionArchive.OpenCsv(file))
                {
                    var header = ReadPoseHeader(input, cancellation);
                    if (header == null) { evidence["role"] = "other-csv"; continue; }
                    evidence["role"] = "animal-pose";
                    sampledPoses = true;
                    var columns = PoseColumns.Select(name => Array.IndexOf(header, name)).ToArray();
                    int timestamp = Array.IndexOf(header, "Current Time"), vr = Array.IndexOf(header, "VR");
                    for (int i = 0; i < PosePreviewRows; i++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        string row = ReplaySessionArchive.ReadCsvRecord(input);
                        if (row == null) break;
                        if (string.IsNullOrWhiteSpace(row)) continue;
                        string[] cells;
                        try { cells = ReplaySessionArchive.Csv(row); }
                        catch (InvalidDataException) { sampledRejected++; continue; }
                        if (cells.Length != header.Length) { sampledRejected++; continue; }
                        if (policy.Classify(file, header, cells) != RecordedPoseExclusion.None) continue;
                        if (string.IsNullOrWhiteSpace(cells[vr]) || !ReplaySessionArchive.TryTimestamp(cells[timestamp], out _) ||
                            columns.Any(index => !ReplaySessionArchive.TryNumber(cells[index], out _))) { sampledRejected++; continue; }
                        sampledPlayable++;
                        rigs.Add(ReplaySessionArchive.CanonicalRig(cells[vr]));
                        string scene = ReplaySessionArchive.Cell(header, cells, "Scene");
                        if (!string.IsNullOrWhiteSpace(scene)) scenes.Add(scene);
                        break;
                    }
                    // The initial rows may be setup or ambiguous. Let playback examine later rows.
                    if (!input.EndOfStream) entry.HasPossiblePoseRows = true;
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException)
            { warnings.Add(info.Name + ": " + error.Message); }
        }

        JObject summary;
        bool completePoseSet = !sampledPoses && matchedPoses > 0 && matchedPoses == expected.Values.Count(item => (string)item["role"] == "animal-pose");
        if (completePoseSet)
        {
            summary = new JObject();
            foreach (string key in new[] { "poseRows", "observed", "wallClockStartLocal", "wallClockEndLocal", "wallClockSpanSeconds" })
                summary[key] = manifest[key]?.DeepClone();
        }
        else
        {
            summary = new JObject {
                ["poseRows"] = new JObject { ["playable"] = sampledPlayable, ["rejected"] = sampledRejected },
                ["observed"] = new JObject { ["rigIds"] = new JArray(rigs), ["sceneNames"] = new JArray(scenes) }
            };
        }
        if (matched < expected.Count) warnings.Add("Some indexed recording files are missing or have changed. This preview uses the files available in this folder.");
        if (sampledPoses) warnings.Add("Pose availability was checked using initial rows. Playback examines the full set of available pose logs.");
        summary["metadata"] = RecordingSummary.ReadMetadata(directory,
            TryReadSmall(Path.Combine(directory, "sequenceConfig.json")), TryReadSmall(Path.Combine(directory, "system_config.json")), warnings);
        summary["files"] = files;
        summary["warnings"] = new JArray(warnings);
        return summary;
    }

    private static void CheckMetadataSize(string directory, bool previewOnly)
    {
        foreach(string name in new[]{"config_manifest.json","AnimalMetadata.json","FlyMetaData.json","system_config.json","sequenceConfig.json",RecordingSummary.ManifestName})
        {
            if (previewOnly && name == RecordingSummary.ManifestName) continue;
            var file=new FileInfo(Path.Combine(directory,name));
            if(file.Exists&&file.Length>MaximumJsonBytes)throw new IOException(name+" exceeds the library metadata size limit. Open this folder directly in replay for advanced inspection.");
        }
    }
    private static JObject ReadSmall(string file)
    {
        var info=new FileInfo(file);if(!info.Exists)return null;
        if(info.Length>MaximumJsonBytes)throw new IOException("Metadata exceeds the size limit.");
        return JObject.Parse(File.ReadAllText(file));
    }
    private static JObject TryReadSmall(string path)
    {
        try { return ReadSmall(path); }
        catch (Exception error) when (error is IOException || error is JsonException || error is UnauthorizedAccessException) { return null; }
    }
    public static string GetFreshnessToken(string directory,CancellationToken cancellation=default)=>Fingerprint(Path.GetFullPath(directory),cancellation);
    private static string Fingerprint(string directory,CancellationToken cancellation)
    {
        var files=new List<string>(ReplaySessionArchive.RecordedFiles(directory));
        files.AddRange(new[]{"config_manifest.json","AnimalMetadata.json","FlyMetaData.json","system_config.json","sequenceConfig.json",RecordingSummary.ManifestName}.Select(name=>Path.Combine(directory,name)).Where(File.Exists));
        string snapshots=Path.Combine(directory,"Configs","Snapshots");
        if(Directory.Exists(snapshots))files.AddRange(Directory.EnumerateFiles(snapshots));
        var text=new StringBuilder();
        foreach(string file in files.OrderBy(path=>path,StringComparer.Ordinal))
        {
            cancellation.ThrowIfCancellationRequested();var info=new FileInfo(file);
            text.Append(Path.GetRelativePath(directory,file)).Append('\t').Append(info.Length).Append('\t').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
        }
        return Digest(text.ToString());
    }
    private static string CachePath(string directory,string cacheDirectory)
    {
        if(string.IsNullOrWhiteSpace(cacheDirectory))return null;
        string root=Path.GetFullPath(cacheDirectory),source=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        if(root==source||root.StartsWith(source+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))return null;
        return Path.Combine(root,Digest(source)+".json");
    }
    private static JObject ReadCache(string directory,string cacheDirectory,string fingerprint)
    {
        try
        {
            string path=CachePath(directory,cacheDirectory);if(path==null)return null;
            var cached=ReadSmall(path);
            if((int?)cached?["version"]!=1||(string)cached["directory"]!=directory||(string)cached["fingerprint"]!=fingerprint)return null;
            return cached["summary"] as JObject;
        }
        catch {return null;}
    }
    private static void WriteCache(string directory,string cacheDirectory,string fingerprint,JObject summary)
    {
        string path=CachePath(directory,cacheDirectory);if(path==null)return;
        string temporary=null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            File.WriteAllText(temporary,new JObject{["version"]=1,["directory"]=directory,["fingerprint"]=fingerprint,["summary"]=summary}.ToString(Formatting.None),new UTF8Encoding(false));
            if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
        }
        catch { /* A read-only/unavailable cache must not prevent replay discovery. */ }
        finally {if(temporary!=null&&File.Exists(temporary))try{File.Delete(temporary);}catch{}}
    }
    private static string Digest(string text)
    {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
}
