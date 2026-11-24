using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Reads a saved run without consulting or changing the currently installed experiment JSON.</summary>
public sealed class ReplaySessionArchive
{
    public sealed class Sample
    {
        public long timestampTicks;
        public double time,monotonicTime=double.NaN;
        public Vector3 position, rotation;
        public string rig, recordedRigName, scene, configFile, stepName, phase, skyboxId, presentationId;
        public int sequenceStep, stepIndex, trial,loopIndex,cumulativeStep;
        public int unityFrame = -1;
        public bool colorsSwapped;
        public float? grayAtTrialStart;
        public string blackSideAtTrialStart;
        internal bool swapEvent;
        public string StimulusKey => ContextKey + "|swap:" + colorsSwapped + "|gray:" + grayAtTrialStart?.ToString(CultureInfo.InvariantCulture) + "|black:" + blackSideAtTrialStart;
        internal string savedContextKey;
        public string ContextKey => savedContextKey ?? (presentationId + "|" + scene + "|" + sequenceStep + "|" + trial + "|" + loopIndex + "|" + cumulativeStep + "|" + configFile + "|" + stepName + "|" + phase);
        internal bool MatchesContext(Sample other) => presentationId == other.presentationId && scene == other.scene &&
            sequenceStep == other.sequenceStep && trial == other.trial && loopIndex == other.loopIndex && cumulativeStep == other.cumulativeStep &&
            configFile == other.configFile && stepName == other.stepName && phase == other.phase;
    }

    public string DirectoryPath { get; }
    public Dictionary<string, List<Sample>> Rigs { get; } = new Dictionary<string, List<Sample>>(StringComparer.Ordinal);
    public List<string> Warnings { get; } = new List<string>();
    public long OriginTicks { get; private set; }
    public double Duration { get; private set; }
    public double FirstExperimentTime { get; private set; }
    public int PoseCacheHits { get; private set; }
    public JObject Sequence { get; private set; }
    public JObject SystemConfiguration { get; private set; }
    public RecordedPosePolicy PosePolicy { get; }
    public ReplayPresentationJournal Presentations { get; }
    public bool AllowsPartialPlayback { get; private set; }
    public bool HasInterruptedSeal { get; private set; }
    internal bool PlaybackPrepared { get; private set; }
    private HashSet<string> playbackFiles;
    private readonly Dictionary<string,(long ticks,string sha)> sealedPoseSources = new Dictionary<string,(long,string)>(StringComparer.Ordinal);
    private string poseCacheContext;
    private readonly JObject manifest;
    private readonly Dictionary<string,(long bytes,long ticks)> verifiedRecordingFiles = new Dictionary<string,(long,long)>(StringComparer.Ordinal);
    private readonly HashSet<string> verified = new HashSet<string>(StringComparer.Ordinal);

    public ReplaySessionArchive(string directory)
    {
        DirectoryPath = Path.GetFullPath(directory.Trim().Trim('"'));
        if (!Directory.Exists(DirectoryPath)) throw new DirectoryNotFoundException("Run folder does not exist: " + DirectoryPath);
        Presentations = new ReplayPresentationJournal(DirectoryPath);
        string manifestPath = Path.Combine(DirectoryPath, "config_manifest.json");
        if (File.Exists(manifestPath))
        {
            manifest = JObject.Parse(File.ReadAllText(manifestPath));
            if (manifest.Value<bool?>("complete") == false) Warnings.Add("The recording's config archive is marked incomplete.");
        }
        else Warnings.Add("Legacy recording: no hashed config manifest. Only files actually saved in this run can be restored.");
        Sequence = ReadRole("sequence", "sequenceConfig.json");
        SystemConfiguration = ReadRole("system", "system_config.json");
        PosePolicy=RecordedPosePolicy.FromSavedSequence(Sequence,manifest!=null&&Sequence!=null);
    }

    public void RequireCompatibleBuild()
    {
#if MATREX_SMOOTHED_WALKING_REPLAY && UNITY_STANDALONE_LINUX && !UNITY_EDITOR
        var acquisition = Presentations.Contract?["player"] as JObject;
        if (acquisition != null && acquisition["editorRuntimeIdentity"] == null &&
            (string)acquisition["buildGuid"] == "a5adef84c4674343aa1ed62a93798d91" &&
            (string)acquisition["applicationVersion"] == "0.4.4-smoothed-walking" &&
            (string)acquisition["unityVersion"] == "6000.3.16f1" &&
            (string)acquisition["runtimeSourceFingerprintSha256"] == "4f67c971d0c64b2fd1d1c2d2e228ffaf2a9132fdcd4be91a74b1ff7064ee430e")
        {
            Warn("This recording was acquired with the earlier Smoothed Walking build. Its archived evidence is used for replay.");
        }
        else
#endif
        {
            Presentations.ValidateBuild(PlayerBuildIdentity.Capture());
        }
        if(!Presentations.IsCurrentContract)return;
        if(File.Exists(Path.Combine(DirectoryPath,"replay_evidence_errors.jsonl")))
            throw new InvalidDataException("Acquisition stopped because required replay evidence could not be recorded. Inspect replay_evidence_errors.jsonl.");
        string path=Path.Combine(DirectoryPath,"recording_manifest.json");
        if(!File.Exists(path))throw new InvalidDataException("Same-build replay requires a finalized recording manifest.");
        var evidence=ReplayPresentationJournal.Parse(File.ReadAllText(path));
        if((string)evidence["lifecycle"]?["status"]!="finalized" || (bool?)evidence["lifecycle"]?["writersDrained"]!=true)
            throw new InvalidDataException("The recording is not finalized with all writers drained.");
        foreach(var writer in evidence["writerEvidence"] as JArray??new JArray())
            if((long?)writer["droppedFrames"]>0 || !string.IsNullOrEmpty((string)writer["failure"]))
                throw new InvalidDataException("The recording has incomplete population evidence: "+writer["relativePath"]);
        var expectedFiles=evidence["files"] as JArray??throw new InvalidDataException("Finalized acquisition file inventory is missing.");
        var expectedPaths=new HashSet<string>(StringComparer.Ordinal);
        foreach(var item in expectedFiles.OfType<JObject>())
        {
            string relative=(string)item["relativePath"];
            if(string.IsNullOrWhiteSpace(relative)||Path.GetFileName(relative)!=relative||!expectedPaths.Add(relative))
                throw new InvalidDataException("Invalid or duplicate acquisition inventory path.");
            if((long?)item["rejectedRows"]>0||item["readError"]!=null||(bool?)item["changedDuringScan"]==true)
                throw new InvalidDataException("Acquisition evidence is incomplete: "+relative);
            string file=Path.Combine(DirectoryPath,relative);var info=new FileInfo(file);
            if(!info.Exists||info.Length!=(long?)item["bytes"]||string.IsNullOrEmpty((string)item["sha256"]))
                throw new InvalidDataException("Required acquisition file is missing or has changed: "+relative);
            if(verifiedRecordingFiles.TryGetValue(file,out var prior)&&prior.bytes==info.Length&&prior.ticks==info.LastWriteTimeUtc.Ticks)continue;
            string hash;using(var algorithm=System.Security.Cryptography.SHA256.Create())using(var input=File.OpenRead(file))
                hash=BitConverter.ToString(algorithm.ComputeHash(input)).Replace("-","").ToLowerInvariant();
            if(hash!=(string)item["sha256"])throw new InvalidDataException("Acquisition file hash differs from finalized evidence: "+relative);
            verifiedRecordingFiles[file]=(info.Length,info.LastWriteTimeUtc.Ticks);
        }
        if(!expectedPaths.SetEquals(RecordedFiles(DirectoryPath).Select(Path.GetFileName)))
            throw new InvalidDataException("Acquisition file inventory differs from the finalized recording.");
    }

    // Interactive playback uses the archived schema, source sizes and readable
    // evidence. RequireCompatibleBuild remains the full, eager seal audit.
    public void PrepareForPlayback()
    {
        if (PlaybackPrepared) return;
        if (!Presentations.IsCurrentContract) { PlaybackPrepared = true; return; }
        var current = PlayerBuildIdentity.Capture();
        try { Presentations.ValidateBuild(current); }
        catch (InvalidDataException) when (IsSupportedAcquisition(Presentations.Contract["player"] as JObject, current))
        {
            Warn("Replaying archived evidence from MATREX " + (string)Presentations.Contract["player"]["applicationVersion"] + ".");
        }

        string path = Path.Combine(DirectoryPath, "recording_manifest.json");
        var evidence = File.Exists(path) ? ReplayPresentationJournal.Parse(File.ReadAllText(path)) : null;
        HasInterruptedSeal = (string)evidence?["lifecycle"]?["status"] != "finalized" ||
            (bool?)evidence?["lifecycle"]?["writersDrained"] != true;
        if (HasInterruptedSeal) MarkPartial("Acquisition did not finish sealing. Replay uses frames that were saved before recording stopped.");

        var errors = new FileInfo(Path.Combine(DirectoryPath, "replay_evidence_errors.jsonl"));
        if (errors.Exists && errors.Length > 0) MarkPartial("Acquisition reported an evidence error. Review replay_evidence_errors.jsonl for details.");
        if (evidence?["writerEvidence"] is JArray writers && writers.Any(writer =>
            (long?)writer["droppedFrames"] > 0 || !string.IsNullOrEmpty((string)writer["failure"])))
            MarkPartial("The recorder reported dropped population frames or a writer failure.");

        if (HasInterruptedSeal || !(evidence?["files"] is JArray files)) { PlaybackPrepared = true; return; }
        playbackFiles = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        int missingPopulation = 0, missingPose = 0, changed = 0;
        foreach (var item in files.OfType<JObject>())
        {
            string relative = (string)item["relativePath"];
            if (string.IsNullOrWhiteSpace(relative) || Path.GetFileName(relative) != relative || !names.Add(relative))
                throw new InvalidDataException("Invalid or duplicate acquisition inventory path.");
            string role = (string)item["role"];
            if (role != "agent-population" && role != "animal-pose" && role != null) continue;
            var file = new FileInfo(Path.Combine(DirectoryPath, relative));
            if (!file.Exists)
            {
                if (role == "agent-population" || PopulationRecording.IsFile(relative)) missingPopulation++;
                else missingPose++;
                continue;
            }
            if (file.Length != (long?)item["bytes"] || (bool?)item["changedDuringScan"] == true ||
                (long?)item["rejectedRows"] > 0 || !string.IsNullOrEmpty((string)item["readError"]))
            {
                changed++;
                continue;
            }
            playbackFiles.Add(file.FullName);
            if (role == "animal-pose" && (long?)item["lastWriteUtcTicks"] is long ticks && (string)item["sha256"] is string hash)
                sealedPoseSources[file.FullName] = (ticks, hash);
        }
        if (missingPopulation > 0) MarkPartial(missingPopulation + " population tracks are missing from this copy. Their agents are shown only where recorded tracks are available.");
        if (missingPose > 0) MarkPartial(missingPose + " recorded animal pose files are missing.");
        if (changed > 0) MarkPartial(changed + " changed or unreadable files were skipped.");
        PlaybackPrepared = true;
    }

    private static bool IsSupportedAcquisition(JObject recorded, JObject current)
    {
        if (recorded == null || recorded["editorRuntimeIdentity"] != null ||
            (string)recorded["unityVersion"] != (string)current?["unityVersion"]) return false;
        string guid = (string)recorded["buildGuid"];
        string fingerprint = (string)recorded["runtimeSourceFingerprintSha256"];
        return (guid == "f88e1383fd30465a970a27483fee7095" && fingerprint == "a985081f75f30088b3ee576f69df365e2744019172999f3977ae8e79025f165f") ||
            (guid == "d26a9e88f2f64eccafbea41ff24a384f" && fingerprint == "11f67e0797d618e37f3c86bb0008969415d979b64a5482af882a3749ed143d9e") ||
            (guid == "9168de619d794e8f9f6d82526b78a4b5" && fingerprint == "8971ffcaf1ef5e121b6af8f9c08821971df09ec1c586b28d060691df4e62d807") ||
            (guid == "a5adef84c4674343aa1ed62a93798d91" && fingerprint == "4f67c971d0c64b2fd1d1c2d2e228ffaf2a9132fdcd4be91a74b1ff7064ee430e") ||
            (guid == "6f33a9594f9a4056ab3222758103a4b9" && fingerprint == "52e34a9a556db0ae6a03f797ac8018d8d1dd0c0855c9c35c7b4c5ad79965e283") ||
            (guid == "c9a5757d7a2144b097123c38875022fd" && fingerprint == "9f455f36c09b0beb6e703ec5ac80013617d0d021278349401b6e99892eb714e0") ||
            (guid == "eef52b99631e44889aa849d5234604a2" && fingerprint == "22a16519d498bfb7acc8fd884b9be454fca7f7665a94f96d2d6254d869963d8c");
    }

    private bool UsePlaybackFile(string path) => playbackFiles == null || playbackFiles.Contains(path);

    public IEnumerable<string> PopulationFiles() => RecordedFiles(DirectoryPath)
        .Where(path => UsePlaybackFile(path) && !poseFiles.Contains(path));

    private readonly HashSet<string> poseFiles = new HashSet<string>(StringComparer.Ordinal);

    internal void MarkPartial(string warning)
    {
        AllowsPartialPlayback = true;
        Warn(warning);
    }

    internal void TrimAfterWorldFrame(int lastFrame)
    {
        bool trimmed = false;
        foreach (string rig in Rigs.Keys.ToArray())
        {
            var samples = Rigs[rig];
            int count = samples.Count;
            while (count > 0 && samples[count - 1].unityFrame > lastFrame) count--;
            if (count == samples.Count) continue;
            trimmed = true;
            if (count == 0) Rigs.Remove(rig);
            else samples.RemoveRange(count, samples.Count - count);
        }
        if (Rigs.Count == 0) throw new InvalidDataException("No recorded animal pose overlaps the saved world frames.");
        if (!trimmed) return;
        Duration = Rigs.Values.Max(rig => rig[rig.Count - 1].time);
        FirstExperimentTime = Math.Min(FirstExperimentTime, Duration);
        MarkPartial("The replay ends at the last complete world frame. Later animal samples remain in the original logs.");
    }

    private string PoseCacheContext()
    {
        if (poseCacheContext != null) return poseCacheContext;
        string presentations = Path.Combine(DirectoryPath, ReplayPresentationJournal.FileName);
        string journalHash = "";
        if (Presentations.IsCurrentContract)
        {
            using var input = File.OpenRead(presentations);
            using var sha = System.Security.Cryptography.SHA256.Create();
            journalHash = Convert.ToBase64String(sha.ComputeHash(input));
        }
        string sequence = Sequence?.ToString(Newtonsoft.Json.Formatting.None) ?? "";
        return poseCacheContext = RunConfigArchive.Hash(Encoding.UTF8.GetBytes((manifest != null ? "sealed" : "legacy") +
            "\n" + journalHash + "\n" + sequence));
    }

    internal ReplayPoseCache.PendingWrite BeginPoseCacheWrite(string path, string cacheRoot)
    {
        cacheRoot = ReplayPoseCache.OutsideRecording(cacheRoot, DirectoryPath);
        return cacheRoot == null ? null : new ReplayPoseCache.PendingWrite(cacheRoot, new FileInfo(path), PoseCacheContext());
    }

    public void LoadRigSamples() => LoadRigSamples(CancellationToken.None);

    public void LoadRigSamples(CancellationToken cancellation, string cacheRoot = null, Action<long,long> reportProgress = null)
    {
        Rigs.Clear(); poseFiles.Clear(); OriginTicks = long.MaxValue; Duration = FirstExperimentTime = 0;
        PoseCacheHits = 0;
        cacheRoot = ReplayPoseCache.OutsideRecording(cacheRoot, DirectoryPath);
        var files = CsvFiles(DirectoryPath).Where(UsePlaybackFile).Select(path => new FileInfo(path)).ToArray();
        long totalBytes = files.Sum(file => file.Length), completedBytes = 0;
        reportProgress?.Invoke(0, totalBytes);
        foreach (var source in files)
        {
            cancellation.ThrowIfCancellationRequested();
            string file = source.FullName;
            long fileBytes = source.Length, fileTicks = source.LastWriteTimeUtc.Ticks;
            long reportedFileBytes = 0;
            void ReportFile(long position)
            {
                position = Math.Min(fileBytes, position);
                if (position <= reportedFileBytes) return;
                reportedFileBytes = position;
                reportProgress?.Invoke(completedBytes + position, totalBytes);
            }
            string hash = sealedPoseSources.TryGetValue(file, out var sealedFile) && sealedFile.ticks == fileTicks ? sealedFile.sha : null;
            PoseFile result = null;
            if (cacheRoot != null)
            {
                try
                {
                    string key = ReplayPoseCache.Key(source, PoseCacheContext(), hash);
                    if (ReplayPoseCache.TryRead(cacheRoot, key, cancellation, (loaded, size) =>
                        ReportFile(size > 0 ? (long)(fileBytes * ((double)loaded / size)) : 0),
                        out var rigs, out var warnings))
                    {
                        source.Refresh();
                        if (source.Exists && source.Length == fileBytes && source.LastWriteTimeUtc.Ticks == fileTicks)
                        {
                            result = new PoseFile(rigs, warnings);
                            PoseCacheHits++;
                        }
                    }
                }
                catch (Exception error) when (ReplayPoseCache.OptionalFailure(error)) { }
            }
            if (result == null)
            {
                result = ReadPoseFile(file, cancellation, ReportFile);
                if (cacheRoot != null && result != null && result.Rigs.Count != 0)
                {
                    try
                    {
                        source.Refresh();
                        if (source.Exists && source.Length == fileBytes && source.LastWriteTimeUtc.Ticks == fileTicks)
                        {
                            using var saved = new ReplayPoseCache.PendingWrite(cacheRoot, source, PoseCacheContext());
                            foreach (var rig in result.Rigs.Values)
                                foreach (var sample in rig) saved.Add(sample, cancellation);
                            saved.Complete(hash, result.Warnings);
                        }
                    }
                    catch (Exception error) when (ReplayPoseCache.OptionalFailure(error)) { }
                }
            }
            completedBytes += fileBytes;
            reportProgress?.Invoke(completedBytes, totalBytes);
            if (result == null) continue;
            poseFiles.Add(file);
            Warnings.AddRange(result.Warnings);
            foreach (var pair in result.Rigs)
            {
                if (Rigs.TryGetValue(pair.Key, out var existing)) existing.AddRange(pair.Value);
                else Rigs.Add(pair.Key, pair.Value);
            }
        }
        if (Rigs.Count == 0) throw new InvalidDataException("This folder contains no readable animal pose CSV rows (Current Time, VR and six GameObject pose columns). " + string.Join(" ", Warnings.Take(4)));

        double monotonicOrigin = double.PositiveInfinity;
        int firstUnityFrame = int.MaxValue;
        bool renderedClock = Presentations.IsCurrentContract;
        foreach (string id in Rigs.Keys.ToArray())
        {
            cancellation.ThrowIfCancellationRequested();
            var samples = Rigs[id];
            // Stable sorting is only needed when merging files or recovering older out-of-order CSVs.
            for (int i = 1; i < samples.Count; i++)
                if (PoseOrder(samples[i], renderedClock) < PoseOrder(samples[i - 1], renderedClock))
                {
                    samples = samples.OrderBy(sample => PoseOrder(sample, renderedClock)).ToList();
                    Rigs[id] = samples;
                    break;
                }
            var first = samples[0];
            if (!renderedClock)
            {
                OriginTicks = Math.Min(OriginTicks, first.timestampTicks);
                continue;
            }
            if (OriginTicks == long.MaxValue || first.unityFrame < firstUnityFrame)
            {
                firstUnityFrame = first.unityFrame;
                OriginTicks = first.timestampTicks;
            }
            monotonicOrigin = Math.Min(monotonicOrigin, first.monotonicTime);
        }

        double firstExperimentTime = double.PositiveInfinity;
        foreach (var pair in Rigs)
        {
            var samples = pair.Value;
            Sample previous = null;
            string context = null, black = "";
            bool swapped = false;
            float? gray = null;
            for (int i = 0; i < samples.Count; i++)
            {
                if ((i & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                var sample = samples[i];
                if (renderedClock && previous != null &&
                    (sample.unityFrame <= previous.unityFrame || sample.monotonicTime <= previous.monotonicTime))
                    throw new InvalidDataException("Acquisition frame clock is duplicated or not increasing for " + pair.Key);
                sample.time = renderedClock ? sample.monotonicTime - monotonicOrigin :
                    (sample.timestampTicks - OriginTicks) / (double)TimeSpan.TicksPerSecond;
                if (previous == null || !sample.MatchesContext(previous))
                {
                    string nextContext = sample.ContextKey;
                    if (nextContext != context) { context = nextContext; swapped = false; gray = null; black = ""; }
                }
                sample.savedContextKey = context;
                if (sample.swapEvent) swapped = true;
                if (sample.grayAtTrialStart.HasValue) gray = sample.grayAtTrialStart;
                if (!string.IsNullOrEmpty(sample.blackSideAtTrialStart)) black = sample.blackSideAtTrialStart;
                sample.colorsSwapped = swapped;
                sample.grayAtTrialStart = gray;
                sample.blackSideAtTrialStart = black;
                if (sample.time < firstExperimentTime && sample.scene != "ControlScene" && sample.scene != "ReplayScene")
                    firstExperimentTime = sample.time;
                previous = sample;
            }
            Duration = Math.Max(Duration, samples[samples.Count - 1].time);
        }
        FirstExperimentTime = double.IsPositiveInfinity(firstExperimentTime) ? 0 : firstExperimentTime;
        if (!renderedClock) Warnings.Add("Historical rig clocks are local wall time. Clock changes, unlogged animation, and tracker validity cannot be recovered from these CSVs.");
    }

    private static long PoseOrder(Sample sample, bool renderedClock) => renderedClock ? sample.unityFrame : sample.timestampTicks;

    private sealed class PoseFile
    {
        public readonly Dictionary<string, List<Sample>> Rigs;
        public readonly List<string> Warnings;
        public PoseFile() : this(new Dictionary<string, List<Sample>>(StringComparer.Ordinal), new List<string>()) { }
        public PoseFile(Dictionary<string, List<Sample>> rigs, List<string> warnings) { Rigs = rigs; Warnings = warnings; }
    }

    private sealed class PoseColumns
    {
        public readonly int Time, Rig, Scene, LogicalScene, Config, StepName, Phase, Skybox, Presentation, Frame, Monotonic, Swap, Gray, Black, Step, StepIndex, Trial, Loop, Cumulative;
        public readonly int[] Pose;
        public readonly int[] Selected;
        public bool CanCheckHandoff => Scene >= 0 && LogicalScene >= 0 && Phase >= 0 && Step >= 0 && Frame >= 0;

        public PoseColumns(string[] header)
        {
            int Find(string name) => Array.IndexOf(header, name);
            Time = Find("Current Time"); Rig = Find("VR"); Scene = Find("Scene"); LogicalScene = Find("CurrentSequenceScene");
            Config = Find("ConfigFile"); StepName = Find("stepName"); Phase = Find("experimentPhase"); Skybox = Find("skyboxId");
            Presentation = Find("presentationId"); Frame = Find("unityFrame"); Monotonic = Find("monotonicTime");
            Swap = Find("swapElapsedSec"); Gray = Find("grayAtTrialStart"); Black = Find("blackSideAtTrialStart");
            Step = Find("CurrentStep"); StepIndex = Find("stepIndex"); Trial = Find("CurrentTrial");
            Loop = Find("loopIndex"); Cumulative = Find("cumulativeStep");
            Pose = new[] { Find("GameObjectPosX"), Find("GameObjectPosY"), Find("GameObjectPosZ"), Find("GameObjectRotX"), Find("GameObjectRotY"), Find("GameObjectRotZ") };
            Selected = new[] { Time, Rig, Scene, LogicalScene, Config, StepName, Phase, Skybox, Presentation, Frame, Monotonic,
                Swap, Gray, Black, Step, StepIndex, Trial, Loop, Cumulative }.Concat(Pose).Where(index => index >= 0).Distinct().OrderBy(index => index).ToArray();
        }
    }

    private readonly ref struct PoseRow
    {
        private readonly ReadOnlySpan<char> line;
        private readonly (int start, int count)[] cells;
        private readonly string[] quoted;

        public PoseRow(string line, (int start, int count)[] cells, string[] quoted)
        { this.line = line.AsSpan(); this.cells = cells; this.quoted = quoted; }

        public ReadOnlySpan<char> this[int column] => column < 0 ? ReadOnlySpan<char>.Empty : quoted != null ? quoted[column].AsSpan() :
            line.Slice(cells[column].start, cells[column].count);
    }

    private sealed class PoseText
    {
        private readonly string[] previous;
        private string second;
        private long secondTicks;

        public PoseText(int columns) { previous = new string[columns]; }

        public string Read(PoseRow row, int column)
        {
            if (column < 0) return "";
            var value = row[column];
            if (value.Length == 0) return "";
            string last = previous[column];
            if (last != null && value.SequenceEqual(last.AsSpan())) return last;
            return previous[column] = value.ToString();
        }

        public bool ReadTimestamp(ReadOnlySpan<char> value, out long ticks)
        {
            // Acquisition writes seven fractional digits. Parse the shared whole second once.
            if (value.Length == 27 && value[19] == '.')
            {
                int fraction = 0;
                for (int i = 20; i < 27; i++)
                {
                    int digit = value[i] - '0';
                    if ((uint)digit > 9) { fraction = -1; break; }
                    fraction = fraction * 10 + digit;
                }
                if (fraction >= 0)
                {
                    var prefix = value.Slice(0, 19);
                    if (prefix.SequenceEqual(second.AsSpan()))
                    { ticks = secondTicks + fraction; return true; }
                    if (DateTime.TryParseExact(prefix, "yyyy-MM-dd HH:mm:ss".AsSpan(), CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTime whole))
                    {
                        second = prefix.ToString();
                        secondTicks = whole.Ticks;
                        ticks = secondTicks + fraction;
                        return true;
                    }
                }
            }
            bool valid = DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime timestamp);
            ticks = timestamp.Ticks;
            return valid;
        }
    }

    private static bool SlicePoseRow(string line, int columnCount, int[] selected, (int start, int count)[] cells)
    {
        int start = 0, next = 0;
        for (int column = 0; column < columnCount; column++)
        {
            int end = line.IndexOf(',', start);
            if (end < 0)
            {
                if (column != columnCount - 1) return false;
                if (next < selected.Length) cells[column] = (start, line.Length - start);
                return true;
            }
            if (next < selected.Length && column == selected[next])
            {
                cells[column] = (start, end - start);
                next++;
            }
            start = end + 1;
        }
        return false;
    }

    private static bool PoseNumber(ReadOnlySpan<char> value, out float number)
    {
        number = 0;
        return !value.IsEmpty && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
            !float.IsNaN(number) && !float.IsInfinity(number);
    }

    private static int PoseInteger(ReadOnlySpan<char> value, int fallback = 0)
    {
        if (value.IsEmpty) return fallback;
        if (value.Length == 1 && (uint)(value[0] - '0') <= 9) return value[0] - '0';
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) return parsed;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double numeric) &&
            numeric >= int.MinValue && numeric <= int.MaxValue && numeric == Math.Truncate(numeric) ? (int)numeric : fallback;
    }

    private static bool KnownRig(string canonical) => canonical.Length == 3 && canonical[0] == 'V' && canonical[1] == 'R' && canonical[2] >= '1' && canonical[2] <= '9' ||
        canonical.Length == 4 && canonical[0] == 'V' && canonical[1] == 'R' && canonical[2] == '1' && canonical[3] >= '0' && canonical[3] <= '6';

    internal PoseProjection CreatePoseProjection(string file, string[] header) => new PoseProjection(this, file, header);

    internal sealed class PoseProjection
    {
        private readonly ReplaySessionArchive archive;
        private readonly string file;
        private readonly PoseColumns map;
        private readonly PoseText text;
        private readonly bool renderedClock;
        private int rejected, misaligned, nonExperiment, handoff;
        private string lastRigName, canonicalRig, lastPresentation;
        private bool allowedRig;

        internal PoseProjection(ReplaySessionArchive archive, string file, string[] header)
        {
            this.archive = archive;
            this.file = file;
            map = new PoseColumns(header);
            text = new PoseText(header.Length);
            renderedClock = archive.Presentations.IsCurrentContract;
        }

        public bool IsAnimalFile => map.Time >= 0 && map.Rig >= 0;
        public bool HasPoseColumns => IsAnimalFile && map.Pose.All(index => index >= 0);
        internal int[] SelectedColumns => map.Selected;

        internal void Reject(bool wrongColumnCount)
        {
            rejected++;
            if (wrongColumnCount) misaligned++;
        }

        internal Sample ProjectText(string line, (int start, int count)[] spans) => Project(new PoseRow(line, spans, null));
        public Sample ProjectCells(string[] cells) => Project(new PoseRow(null, null, cells));

        private Sample Project(PoseRow row)
        {
            string scene = text.Read(row, map.Scene);
            int frame = PoseInteger(row[map.Frame], -1), step = PoseInteger(row[map.Step], -1);
            string phase = text.Read(row, map.Phase);
            var exclusion = archive.PosePolicy.Classify(file, scene, text.Read(row, map.LogicalScene), phase, step, frame, map.CanCheckHandoff);
            if (exclusion == RecordedPoseExclusion.SetupOrReplay) { nonExperiment++; return null; }
            if (exclusion == RecordedPoseExclusion.VerifiedIdleOrHandoff) { handoff++; return null; }

            string rigName = text.Read(row, map.Rig);
            if (string.IsNullOrWhiteSpace(rigName) || !text.ReadTimestamp(row[map.Time], out long ticks) ||
                !PoseNumber(row[map.Pose[0]], out float x) || !PoseNumber(row[map.Pose[1]], out float y) || !PoseNumber(row[map.Pose[2]], out float z) ||
                !PoseNumber(row[map.Pose[3]], out float rx) || !PoseNumber(row[map.Pose[4]], out float ry) || !PoseNumber(row[map.Pose[5]], out float rz))
            { rejected++; return null; }
            if (rigName != lastRigName)
            {
                lastRigName = rigName;
                canonicalRig = CanonicalRig(rigName);
                allowedRig = !renderedClock || KnownRig(canonicalRig);
            }
            if (!allowedRig) return null;

            string presentation = text.Read(row, map.Presentation);
            double monotonic = double.TryParse(row[map.Monotonic], NumberStyles.Float, CultureInfo.InvariantCulture, out double clock) ? clock : double.NaN;
            if (!renderedClock && !string.IsNullOrEmpty(presentation))
                throw new InvalidDataException("Frame references a presentation but its same-build contract is missing.");
            if (renderedClock)
            {
                if (frame < 0 || double.IsNaN(monotonic) || double.IsInfinity(monotonic))
                    throw new InvalidDataException("Same-build pose frame has no monotonic rendered-frame clock.");
                if (presentation != lastPresentation) { archive.Presentations.RequireEntry(presentation); lastPresentation = presentation; }
            }
            return new Sample
            {
                timestampTicks = ticks, rig = canonicalRig, recordedRigName = rigName,
                position = new Vector3(x, y, z), rotation = new Vector3(rx, ry, rz), scene = scene,
                configFile = text.Read(row, map.Config), stepName = text.Read(row, map.StepName), phase = phase,
                skyboxId = text.Read(row, map.Skybox), presentationId = presentation, unityFrame = frame, monotonicTime = monotonic,
                swapEvent = PoseNumber(row[map.Swap], out _), grayAtTrialStart = PoseNumber(row[map.Gray], out float gray) ? gray : (float?)null,
                blackSideAtTrialStart = text.Read(row, map.Black), sequenceStep = step, stepIndex = PoseInteger(row[map.StepIndex], -1),
                trial = PoseInteger(row[map.Trial]), loopIndex = PoseInteger(row[map.Loop]), cumulativeStep = PoseInteger(row[map.Cumulative])
            };
        }

        public IReadOnlyList<string> FinishWarnings()
        {
            var warnings = new List<string>();
            string name = Path.GetFileName(file);
            if (!HasPoseColumns) warnings.Add("Missing pose columns: " + name);
            if (rejected > 0) warnings.Add(name + ": skipped " + rejected + " malformed pose rows.");
            if (misaligned > 0) warnings.Add(name + ": " + misaligned + " rows have a different column count; legacy decimal-comma numbers or unquoted delimiters cannot be reconstructed safely.");
            if (nonExperiment > 0) warnings.Add(name + ": excluded " + nonExperiment + " setup/replay pose rows from experiment playback.");
            if (handoff > 0) warnings.Add(name + ": excluded " + handoff + " verified idle/scene-handoff rows; acquisition gaps remain unchanged.");
            return warnings;
        }
    }

    private PoseFile ReadPoseFile(string file, CancellationToken cancellation, Action<long> reportProgress)
    {
        using var reader = OpenCsv(file, out var source);
        string first = ReadCsvRecord(reader);
        if (first == null) return null;
        string[] header = Csv(first.TrimStart('\ufeff'));
        var projector = CreatePoseProjection(file, header);
        if (!projector.IsAnimalFile) return null;
        var result = new PoseFile();
        if (projector.HasPoseColumns)
        {
            var cells = new (int, int)[header.Length];
            string line, lastRig = null;
            List<Sample> samples = null;
            int count = 0;
            while ((line = reader.ReadLine()) != null)
            {
                if ((count++ & 4095) == 0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    reportProgress?.Invoke(source.Position);
                }
                if (string.IsNullOrWhiteSpace(line)) continue;
                Sample sample;
                if (line.IndexOf('"') >= 0)
                {
                    string[] quoted;
                    try { quoted = Csv(ReadCsvRecord(reader, line)); }
                    catch (InvalidDataException) { projector.Reject(false); continue; }
                    if (quoted.Length != header.Length) { projector.Reject(true); continue; }
                    sample = projector.ProjectCells(quoted);
                }
                else
                {
                    if (!SlicePoseRow(line, header.Length, projector.SelectedColumns, cells)) { projector.Reject(true); continue; }
                    sample = projector.ProjectText(line, cells);
                }
                if (sample == null) continue;
                if (sample.rig != lastRig)
                {
                    lastRig = sample.rig;
                    if (!result.Rigs.TryGetValue(lastRig, out samples)) result.Rigs[lastRig] = samples = new List<Sample>();
                }
                samples.Add(sample);
            }
        }
        result.Warnings.AddRange(projector.FinishWarnings());
        return result;
    }

    public JObject ExperimentFor(Sample sample)
    {
        if (Presentations.IsCurrentContract) return (JObject)Presentations.For(sample.presentationId)["config"];
        string reference = sample.configFile;
        var rows = Sequence?["sequences"] as JArray;
        JObject parameters = rows != null && sample.sequenceStep >= 0 && sample.sequenceStep < rows.Count ? rows[sample.sequenceStep]?["parameters"] as JObject : null;
        // The logged config is authoritative, including randomized execution order.
        if(string.IsNullOrWhiteSpace(reference) && Sequence?.Value<bool?>("randomise")==true)
        {Warn("Randomized sequence row has no recorded config filename; static experiment reconstruction is ambiguous.");return null;}
        if (string.IsNullOrWhiteSpace(reference)) reference = (string)parameters?["configFile"] ?? (string)parameters?["design"];
        if (string.IsNullOrWhiteSpace(reference)) return null;
        string path = Resolve(reference);
        if (path == null) { Warn("Saved configuration missing: " + reference); return null; }
        JObject config = JObject.Parse(File.ReadAllText(path));
        string title = (string)config["title"];
        if (config["preStimulus"] != null || config["stimulus"] != null || config["postStimulus"] != null)
        {
            string phase = string.IsNullOrWhiteSpace(sample.phase) || sample.phase == "assessment" ? "preStimulus" : sample.phase;
            if (!(config[phase] is JObject payload)) { Warn("Saved phase missing: " + phase + " in " + reference); return null; }
            config = (JObject)payload.DeepClone();
            if (config["title"] == null && !string.IsNullOrWhiteSpace(title)) config["title"] = title;
        }
        // Choice applies the sequence's rugosity patch after extracting a phase.
        // Reusing one base terrain config must preserve its recorded per-row treatment.
        bool SameReference(JObject row)
        {
            string saved=(string)row?["parameters"]?["configFile"]??(string)row?["parameters"]?["design"];
            string scene=(string)row?["sceneName"];
            return string.Equals(saved?.Replace('\\','/'),reference?.Replace('\\','/'),StringComparison.Ordinal) &&
                (string.IsNullOrEmpty(scene)||string.Equals(scene == "Swarm" ? "Kannadi" : scene,sample.scene == "Swarm" ? "Kannadi" : sample.scene,StringComparison.Ordinal));
        }
        JObject terrainPatch=null;
        if(Sequence?.Value<bool?>("randomise")==true)
        {
            var candidates=(rows??new JArray()).OfType<JObject>().Where(SameReference).ToArray();
            if(candidates.Any(row=>row["parameters"]?["rugosity"] is JObject))
            {
                var patches=candidates.Select(row=>row["parameters"]?["rugosity"] as JObject).ToArray();
                if(patches.Length>0 && patches.All(value=>JToken.DeepEquals(value,patches[0])))terrainPatch=patches[0];
                else {Warn("Saved randomized terrain treatment is ambiguous: multiple inline rugosity patches share "+reference+". The source row was not recorded; no terrain treatment is assumed.");return null;}
            }
        }
        else if(rows!=null && sample.sequenceStep>=0 && sample.sequenceStep<rows.Count && SameReference(rows[sample.sequenceStep] as JObject))
            terrainPatch=parameters?["rugosity"] as JObject;
        if(terrainPatch!=null)
        {
            var rugosity=config["rugosity"] as JObject??new JObject();
            rugosity.Merge(terrainPatch,new JsonMergeSettings{MergeNullValueHandling=MergeNullValueHandling.Ignore});
            config["rugosity"]=rugosity;
        }
        // New players execute legacy Swarm rows in Kannadi. The archived source
        // filename still identifies the historical population and unit defaults.
        var matchingRows = (rows ?? new JArray()).OfType<JObject>().Where(SameReference).ToArray();
        foreach (string key in new[] { "populationMode", "worldUnitsPerMetre", "kannadiModelPath" })
        {
            JToken Effective(JObject row) => row["parameters"]?[key] ?? (key == "populationMode" && (string)row["sceneName"] == "Swarm" ? new JValue("Swarm") :
                key == "worldUnitsPerMetre" && (string)row["sceneName"] == "Swarm" ? new JValue(100) : config[key]);
            var values = matchingRows.Select(Effective).ToArray();
            if (values.Length > 0 && values.All(value => JToken.DeepEquals(value, values[0])))
            { if (values[0] != null) config[key] = values[0].DeepClone(); }
            else if (values.Length > 1) Warn("Saved randomized population setting is ambiguous for " + reference + ": " + key);
        }
        return config;
    }

    public string Resolve(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        var matches = (manifest?["files"] as JArray)?.OfType<JObject>().Where(f =>
            (string)f["sourceRelativePath"] == reference || (string)f["sourcePath"] == reference ||
            (f["references"] as JArray)?.Values<string>().Contains(reference) == true).ToArray();
        if (matches != null && matches.Length > 0)
        {
            if (matches.Select(m => (string)m["sha256"]).Distinct().Count() > 1)
                throw new InvalidDataException("Multiple saved versions of '" + reference + "' exist; exact version selection requires an acquisition clock mapping.");
            JObject entry = matches[0];
            string snapshot = SafePath((string)entry["snapshotPath"]);
            if (!File.Exists(snapshot)) throw new FileNotFoundException("Saved snapshot is missing.",snapshot);
            if (verified.Add(snapshot))
            {
                string expected = (string)entry["sha256"];
                if (string.IsNullOrEmpty(expected) || !string.Equals(expected,RunConfigArchive.Hash(File.ReadAllBytes(snapshot)),StringComparison.OrdinalIgnoreCase))
                { verified.Remove(snapshot); throw new InvalidDataException("Saved config checksum mismatch: " + reference); }
            }
            return snapshot;
        }
        if (manifest != null) return null; // Never silently use live configs or an unverified mutable copy.
        if (Path.IsPathRooted(reference) || reference.Contains(":")) reference = Path.GetFileName(reference.Replace('\\','/'));
        foreach (string candidate in new[] { Path.Combine("Configs","Files",reference), reference })
        {
            string path = SafePath(candidate);
            if (File.Exists(path)) return path;
        }
        // Pre-manifest loggers saved configs as <session>_<scene>_<original filename>.
        // Accept only a unique saved content version; never consult the live Config directory.
        string basename=Path.GetFileName(reference);
        var legacy=Directory.GetFiles(DirectoryPath,"*.json").Where(path=>Path.GetFileName(path).EndsWith("_"+basename,StringComparison.Ordinal)).ToArray();
        if(legacy.Length>0)
        {
            if(legacy.Select(path=>RunConfigArchive.Hash(File.ReadAllBytes(path))).Distinct().Count()>1)
                throw new InvalidDataException("Multiple saved legacy versions of '"+reference+"' exist; exact version is ambiguous.");
            return legacy.OrderBy(path=>path,StringComparer.Ordinal).First();
        }
        return null;
    }

    public string SkyImage(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (id.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_')) throw new InvalidDataException("Invalid recorded sky ID.");
        string path = SafePath(Path.Combine("Skyboxes",id+".png"));
        if (!File.Exists(path)) { Warn("Saved sky image missing: " + id); return null; }
        string metadata = Path.ChangeExtension(path,".json");
        if (File.Exists(metadata))
        {
            var json = JObject.Parse(File.ReadAllText(metadata));
            string hash = (string)json["pngSha256"] ?? (string)json["imageSha256"];
            if (!string.IsNullOrEmpty(hash) && !string.Equals(hash,RunConfigArchive.Hash(File.ReadAllBytes(path)),StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Saved sky image checksum mismatch: " + id);
        }
        return path;
    }

    private JObject ReadRole(string role,string conventional)
    {
        var entries = (manifest?["files"] as JArray)?.OfType<JObject>().Where(f => (f["roles"] as JArray)?.Values<string>().Contains(role) == true).ToArray();
        if (entries != null && entries.Length > 0)
        {
            var entry = entries[0];
            string reference = (string)entry["sourceRelativePath"] ?? (string)entry["sourcePath"];
            string path = Resolve(reference);
            return path == null ? null : JObject.Parse(File.ReadAllText(path));
        }
        string direct = manifest == null ? Resolve(conventional) : null;
        if (direct == null && manifest == null)
        {
            var alternatives = Directory.GetFiles(DirectoryPath,"*_"+conventional).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            if (alternatives.Length == 1) direct = alternatives[0];
            else if (alternatives.Length > 1) Warn("Several legacy " + conventional + " copies exist; select a complete hashed run for unambiguous reconstruction.");
        }
        if (direct == null) { Warn("Saved " + role + " JSON is unavailable."); return null; }
        return JObject.Parse(File.ReadAllText(direct));
    }

    public string SafePath(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(":")) throw new InvalidDataException("Invalid saved relative path: " + relative);
        string path = Path.GetFullPath(Path.Combine(DirectoryPath,relative.Replace('\\',Path.DirectorySeparatorChar)));
        string prefix = DirectoryPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix,StringComparison.Ordinal)) throw new InvalidDataException("Saved path escapes the run folder: " + relative);
        return path;
    }

    public void Warn(string warning) { if (!Warnings.Contains(warning)) Warnings.Add(warning); }
    public static string NewestRunFolder(string root)
    {
        if(!Directory.Exists(root))return null;
        foreach(string folder in Directory.GetDirectories(root).OrderByDescending(p=>p,StringComparer.Ordinal))
        {
            string name=Path.GetFileName(folder);
            if(name.Equals("Backup",StringComparison.OrdinalIgnoreCase) || name.Equals("Cache",StringComparison.OrdinalIgnoreCase) || name.StartsWith(".",StringComparison.Ordinal))continue;
            foreach(string file in CsvFiles(folder))
            {
                try
                {
                    using(var input=OpenCsv(file))
                    {
                        string first=ReadCsvRecord(input);if(first==null)continue;
                        string[] header=Csv(first.TrimStart('\ufeff'));
                        if(!header.Contains("Current Time") || !header.Contains("VR") || !header.Contains("GameObjectPosX") || !header.Contains("GameObjectRotY"))continue;
                        string row;
                        while((row=ReadCsvRecord(input))!=null)
                        {
                            if(string.IsNullOrWhiteSpace(row))continue;
                            string[] values=Csv(row);
                            if(string.IsNullOrWhiteSpace(Cell(header,values,"VR")) || !TryTimestamp(Cell(header,values,"Current Time"),out _))continue;
                            string scene=Cell(header,values,"Scene");
                            if(RecordedPosePolicy.IsNonExperimentScene(scene,file))continue;
                            return folder;
                        }
                    }
                }
                catch(InvalidDataException){}catch(IOException){}
            }
        }
        return null;
    }
    public static string CanonicalRig(string name)
    {
        if(string.IsNullOrEmpty(name))return name;
        if(name.StartsWith("VR",StringComparison.OrdinalIgnoreCase))
        {int n=2;while(n<name.Length && char.IsDigit(name[n]))n++;if(n>2)return name.Substring(0,n).ToUpperInvariant();}
        return name;
    }
    public static string[] CsvFiles(string directory) => Directory.GetFiles(directory,"*.csv*").Where(p => p.EndsWith(".csv",StringComparison.OrdinalIgnoreCase) || p.EndsWith(".csv.gz",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
    public static string[] RecordedFiles(string directory) => CsvFiles(directory)
        .Concat(Directory.GetFiles(directory, "*").Where(path => path.EndsWith(".matpop", StringComparison.OrdinalIgnoreCase)))
        .OrderBy(path => path, StringComparer.Ordinal).ToArray();
    public static StreamReader OpenCsv(string path) => OpenCsv(path, out _);
    private static StreamReader OpenCsv(string path, out FileStream source)
    {
        source = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
        Stream stream = source;
        if (path.EndsWith(".gz",StringComparison.OrdinalIgnoreCase)) stream = new GZipStream(stream,CompressionMode.Decompress);
        return new StreamReader(stream,Encoding.UTF8,true,65536);
    }
    public static bool TryTimestamp(string value,out long ticks)
    {
        // These columns have no zone. Preserve the acquisition computer's wall-clock ordering on another OS/time zone.
        bool ok = DateTime.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out DateTime timestamp);
        ticks = timestamp.Ticks; return ok;
    }
    public static bool TryNumber(string value,out float number) => float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out number) && !float.IsNaN(number) && !float.IsInfinity(number);
    public static int Integer(string value,int fallback=0)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) return parsed;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double numeric) &&
            numeric >= int.MinValue && numeric <= int.MaxValue && numeric == Math.Truncate(numeric) ? (int)numeric : fallback;
    }
    public static string ReadCsvRecord(StreamReader reader) => ReadCsvRecord(reader, reader.ReadLine());

    private static string ReadCsvRecord(StreamReader reader, string first)
    {
        if (first == null || first.IndexOf('"') < 0) return first;
        string line = first;
        StringBuilder record = null;
        bool quoted = false;
        while (true)
        {
            for (int i = 0; i < line.Length; i++)
                if (line[i] == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') i++;
                    else quoted = !quoted;
                }
            if (!quoted || (line = reader.ReadLine()) == null) return record?.ToString() ?? first;
            if (record == null) record = new StringBuilder(first);
            record.Append('\n').Append(line);
        }
    }
    public static string Cell(string[] header,string[] values,string key)
    { int index = Array.IndexOf(header,key); return index >= 0 && index < values.Length ? values[index] : ""; }
    public static string[] Csv(string row)
    {
        if (row.IndexOf('"') < 0) return row.Split(',');
        var cells = new List<string>(); var cell = new StringBuilder(); bool quoted=false;
        for(int i=0;i<row.Length;i++)
        {
            char c=row[i];
            if(c=='"') { if(quoted && i+1<row.Length && row[i+1]=='"') {cell.Append('"');i++;} else quoted=!quoted; }
            else if(c==',' && !quoted) {cells.Add(cell.ToString());cell.Clear();}
            else cell.Append(c);
        }
        if(quoted) throw new InvalidDataException("Unclosed quoted CSV field.");
        cells.Add(cell.ToString()); return cells.ToArray();
    }
}
