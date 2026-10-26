using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Read-only, bounded-memory inspection shared by the run library and recording finalizer.</summary>
public static class RecordingSummary
{
    public const string ManifestName = "recording_manifest.json";
    private const long MaximumManifestBytes = 4 * 1024 * 1024;
    private static readonly string[] PoseColumns = { "GameObjectPosX", "GameObjectPosY", "GameObjectPosZ", "GameObjectRotX", "GameObjectRotY", "GameObjectRotZ" };

    public static JObject Scan(string directory, bool includeHashes = false, CancellationToken cancellation = default,
        Action<string, string[], string[]> onPlayablePose = null, bool scanPopulationRows = false)
    {
        directory = Path.GetFullPath(directory);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        var warnings = new List<string>();
        ReplaySessionArchive archive = null;
        try { archive = new ReplaySessionArchive(directory); warnings.AddRange(archive.Warnings); }
        catch (Exception error) { warnings.Add("Saved configuration metadata is unavailable: " + error.Message); }
        var policy = archive?.PosePolicy ?? RecordedPosePolicy.FromSavedSequence(null, false);
        var files = new JArray();
        var rigs = new SortedSet<string>(StringComparer.Ordinal);
        var scenes = new SortedSet<string>(StringComparer.Ordinal);
        var cycles = new HashSet<string>(StringComparer.Ordinal);
        var steps = new SortedSet<int>();
        var contexts = new HashSet<string>(StringComparer.Ordinal);
        var presentations = new SortedDictionary<string, HashSet<string>>(StringComparer.Ordinal);
        long stored = 0, playable = 0, rejected = 0, setup = 0, handoff = 0;
        long? first = null, last = null;
        bool cyclesComplete = true, changed = false;
        foreach (string file in ReplaySessionArchive.CsvFiles(directory))
        {
            cancellation.ThrowIfCancellationRequested();
            var before = new FileInfo(file);
            long bytes = before.Length, writeTicks = before.LastWriteTimeUtc.Ticks;
            var info = new JObject { ["relativePath"] = Path.GetFileName(file), ["bytes"] = bytes, ["lastWriteUtcTicks"] = writeTicks };
            long fileStored = 0, filePlayable = 0, fileRejected = 0, fileExcluded = 0;
            long? fileFirst = null, fileLast = null;
            var fileRigs = new SortedSet<string>(StringComparer.Ordinal);
            bool scannedRows = true;
            try
            {
                using (var reader = ReplaySessionArchive.OpenCsv(file))
                {
                    string record = ReplaySessionArchive.ReadCsvRecord(reader);
                    if (record == null) { info["role"] = "empty"; }
                    else
                    {
                        string[] header = ReplaySessionArchive.Csv(record.TrimStart('\ufeff'));
                        int time = Array.IndexOf(header, "Current Time"), rig = Array.IndexOf(header, "VR");
                        bool pose = time >= 0 && rig >= 0;
                        scannedRows = pose || scanPopulationRows;
                        info["role"] = pose ? "animal-pose" : Array.IndexOf(header, "Timestamp") >= 0 ? "agent-population" : "other-csv";
                        if (!pose) time = Array.IndexOf(header, "Timestamp");
                        int[] positions = PoseColumns.Select(name => Array.IndexOf(header, name)).ToArray();
                        int metadataStart = Array.FindIndex(header, name => name.Contains(":"));
                        int expectedColumns = !pose && metadataStart >= 0 ? metadataStart : header.Length;
                        while (scannedRows && (record = ReplaySessionArchive.ReadCsvRecord(reader)) != null)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            if (string.IsNullOrWhiteSpace(record)) continue;
                            fileStored++; if (pose) stored++;
                            string[] cells;
                            try { cells = ReplaySessionArchive.Csv(record); }
                            catch (InvalidDataException) { fileRejected++; if (pose) rejected++; continue; }
                            if (cells.Length != expectedColumns)
                            { fileRejected++; if (pose) rejected++; continue; }
                            if (pose)
                            {
                                var excluded = policy.Classify(file, header, cells);
                                if (excluded != RecordedPoseExclusion.None)
                                {
                                    fileExcluded++;
                                    if (excluded == RecordedPoseExclusion.SetupOrReplay) setup++; else handoff++;
                                    continue;
                                }
                            }
                            if (time < 0 || time >= cells.Length || !ReplaySessionArchive.TryTimestamp(cells[time], out long ticks) ||
                                (pose && (positions.Any(index => index < 0) || string.IsNullOrWhiteSpace(cells[rig]) ||
                                positions.Any(index => !ReplaySessionArchive.TryNumber(cells[index], out _)))))
                            { fileRejected++; if (pose) rejected++; continue; }
                            fileFirst = Min(fileFirst, ticks); fileLast = Max(fileLast, ticks);
                            if (!pose) continue;
                            filePlayable++; playable++; first = Min(first, ticks); last = Max(last, ticks);
                            string id = ReplaySessionArchive.CanonicalRig(cells[rig]); rigs.Add(id); fileRigs.Add(id);
                            string scene = ReplaySessionArchive.Cell(header, cells, "Scene"); if (!string.IsNullOrEmpty(scene)) scenes.Add(scene);
                            string cycle = ReplaySessionArchive.Cell(header, cells, "CurrentTrial");
                            if (Array.IndexOf(header, "CurrentTrial") < 0 || !int.TryParse(cycle, NumberStyles.Integer, CultureInfo.InvariantCulture, out int cycleNumber)) cyclesComplete = false;
                            else cycles.Add(cycleNumber.ToString(CultureInfo.InvariantCulture));
                            if (int.TryParse(ReplaySessionArchive.Cell(header, cells, "CurrentStep"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int step)) steps.Add(step);
                            string[] keys = { scene, cycle, ReplaySessionArchive.Cell(header, cells, "CurrentStep"),
                                ReplaySessionArchive.Cell(header, cells, "loopIndex"), ReplaySessionArchive.Cell(header, cells, "cumulativeStep"),
                                ReplaySessionArchive.Cell(header, cells, "ConfigFile"), ReplaySessionArchive.Cell(header, cells, "stepIndex"),
                                ReplaySessionArchive.Cell(header, cells, "stepName") };
                            string presentation = new JArray(keys).ToString(Formatting.None);
                            contexts.Add(new JArray(presentation, ReplaySessionArchive.Cell(header, cells, "experimentPhase")).ToString(Formatting.None));
                            if (!presentations.TryGetValue(id, out var seen)) presentations[id] = seen = new HashSet<string>(StringComparer.Ordinal);
                            seen.Add(presentation);
                            onPlayablePose?.Invoke(file, header, cells);
                        }
                    }
                }
                if (includeHashes) info["sha256"] = HashFile(file, cancellation);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { info["readError"] = error.Message; warnings.Add(Path.GetFileName(file) + ": " + error.Message); }
            var after = new FileInfo(file);
            bool fileChanged = !after.Exists || after.Length != bytes || after.LastWriteTimeUtc.Ticks != writeTicks;
            changed |= fileChanged;
            info["changedDuringScan"] = fileChanged;
            info["rowsInspected"] = scannedRows;
            info["storedRows"] = scannedRows ? (JToken)new JValue(fileStored) : JValue.CreateNull(); info["playableRows"] = filePlayable; info["rejectedRows"] = fileRejected;
            info["excludedRows"] = fileExcluded; info["firstLocal"] = Local(fileFirst); info["lastLocal"] = Local(fileLast);
            info["wallClockSpanSeconds"] = Span(fileFirst, fileLast); info["rigIds"] = new JArray(fileRigs);
            files.Add(info);
        }
        if (changed) warnings.Add("One or more files changed while scanning; this is an observation of an active or changing recording.");
        if (rejected > 0) warnings.Add(rejected + " malformed or incomplete pose rows were rejected.");
        if (setup + handoff > 0) warnings.Add((setup + handoff) + " stored pose rows are excluded by the same saved-evidence policy as replay.");
        warnings.Add("Durations are spans of local wall-clock timestamps, not measured active exposure; gaps and clock changes can affect them.");
        if (!cyclesComplete && playable > 0) warnings.Add("Some playable rows have no valid CurrentTrial; the sequence-cycle count is unknown.");
        var counts = new JObject(); foreach (var pair in presentations) counts[pair.Key] = pair.Value.Count;
        return new JObject {
            ["schemaVersion"] = 1, ["analysisVersion"] = 1, ["scannedUtc"] = DateTimeOffset.UtcNow,
            ["timestampBasis"] = "local-wall-clock", ["wallClockStartLocal"] = Local(first), ["wallClockEndLocal"] = Local(last),
            ["wallClockSpanSeconds"] = Span(first, last), ["changedDuringScan"] = changed,
            ["poseRows"] = new JObject { ["stored"] = stored, ["playable"] = playable, ["rejected"] = rejected,
                ["excluded"] = new JObject { ["setupReplay"] = setup, ["handoffIdle"] = handoff } },
            ["observed"] = new JObject { ["rigIds"] = new JArray(rigs), ["sceneNames"] = new JArray(scenes),
                ["sequenceCycleCount"] = cyclesComplete && playable > 0 ? (JToken)new JValue(cycles.Count) : JValue.CreateNull(),
                ["sequenceStepIndices"] = new JArray(steps), ["contextCount"] = contexts.Count, ["presentationsByRig"] = counts,
                ["presentationCountBasis"] = "unique phase-independent recorded scene/cycle/step/config/internal-step contexts per rig; missing legacy counters can merge occurrences" },
            ["metadata"] = Metadata(directory, archive, warnings), ["files"] = files, ["warnings"] = new JArray(warnings.Distinct())
        };
    }

    /// <summary>A cheap freshness check, not a replacement for hash verification when opening a run.</summary>
    public static bool TryReadFinalized(string directory, out JObject summary, out string reason)
    {
        summary = null; reason = null;
        try
        {
            string path = Path.Combine(directory, ManifestName);
            var file = new FileInfo(path);
            if (!file.Exists) { reason = "No recording manifest; legacy or interrupted recording."; return false; }
            if (file.Length > MaximumManifestBytes) { reason = "Recording manifest exceeds the size limit."; return false; }
            var candidate = JObject.Parse(File.ReadAllText(path));
            if ((int?)candidate["schemaVersion"] != 1 || (int?)candidate["analysisVersion"] != 1 ||
                (string)candidate["lifecycle"]?["status"] != "finalized" || (bool?)candidate["lifecycle"]?["writersDrained"] != true)
            { reason = "Recording has not been finalized."; return false; }
            if (!(candidate["files"] is JArray files)) { reason = "Recording file inventory is absent."; return false; }
            var expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject item in files.OfType<JObject>())
            {
                string relative = (string)item["relativePath"];
                if (string.IsNullOrWhiteSpace(relative) || relative != Path.GetFileName(relative) || !expected.Add(relative))
                { reason = "Recording inventory contains an invalid or duplicate path."; return false; }
                var current = new FileInfo(Path.Combine(directory, relative));
                if (!current.Exists || current.Length != (long?)item["bytes"] || current.LastWriteTimeUtc.Ticks != (long?)item["lastWriteUtcTicks"])
                { reason = "Recording files changed after finalization."; return false; }
            }
            if (!expected.SetEquals(ReplaySessionArchive.CsvFiles(directory).Select(Path.GetFileName)))
            { reason = "Recording file inventory changed after finalization."; return false; }
            var metadataFiles = candidate["metadataFiles"] as JArray ?? new JArray();
            foreach (JObject item in metadataFiles.OfType<JObject>())
            {
                string relative = (string)item["relativePath"];
                if (relative != Path.GetFileName(relative)) { reason = "Metadata inventory path is invalid."; return false; }
                var current = new FileInfo(Path.Combine(directory, relative));
                if (!current.Exists || current.Length != (long?)item["bytes"] || current.LastWriteTimeUtc.Ticks != (long?)item["lastWriteUtcTicks"])
                { reason = "Recording metadata changed after finalization."; return false; }
            }
            if (!new HashSet<string>(metadataFiles.OfType<JObject>().Select(item => (string)item["relativePath"]), StringComparer.Ordinal)
                .SetEquals(MetadataInventory(directory).OfType<JObject>().Select(item => (string)item["relativePath"])))
            { reason = "Recording metadata inventory changed after finalization."; return false; }
            summary = candidate; return true;
        }
        catch (Exception error) { reason = "Recording manifest is unreadable: " + error.Message; return false; }
    }

    public static JArray MetadataInventory(string directory)
    {
        var result = new JArray();
        foreach (string name in new[] { "AnimalMetadata.json", "FlyMetaData.json", "config_manifest.json", "companion_provenance.json", "system_config.json", "sequenceConfig.json", "media_manifest.json" })
        {
            var file = new FileInfo(Path.Combine(directory, name));
            if (file.Exists) result.Add(new JObject { ["relativePath"] = name, ["bytes"] = file.Length, ["lastWriteUtcTicks"] = file.LastWriteTimeUtc.Ticks });
        }
        return result;
    }

    private static JObject Metadata(string directory, ReplaySessionArchive archive, List<string> warnings)
    {
        JObject data = null;
        foreach (string name in new[] { "AnimalMetadata.json", "FlyMetaData.json" })
        {
            string path = Path.Combine(directory, name);
            if (!File.Exists(path)) continue;
            try
            {
                if (new FileInfo(path).Length > MaximumManifestBytes) throw new IOException("Animal metadata exceeds the size limit.");
                data = JObject.Parse(File.ReadAllText(path)); break;
            }
            catch (Exception error) { warnings.Add(name + ": " + error.Message); }
        }
        // An explicit canonical Animals property takes precedence over its legacy alias, even when null/empty.
        var animals = (data?.Property("Animals") != null ? data["Animals"] : data?["Flies"]) as JArray ?? new JArray();
        var normalized = new JArray(animals.OfType<JObject>().Select(animal => new JObject {
            ["VR"] = animal["VR"]?.DeepClone(), ["AnimalID"] = (animal.Property("AnimalID") != null ? animal["AnimalID"] : animal["FlyID"])?.DeepClone(),
            ["SpeciesName"] = animal["SpeciesName"]?.DeepClone() }));
        var rigs = archive?.SystemConfiguration?["configs"] as JArray ?? new JArray();
        foreach (var rig in rigs.OfType<JObject>())
        {
            string id = (string)rig["vrId"];
            if (string.IsNullOrWhiteSpace(id)) continue;
            var animal = normalized.OfType<JObject>().FirstOrDefault(item => (string)item["VR"] == id);
            if (animal == null) { animal = new JObject { ["VR"] = id, ["AnimalID"] = null }; normalized.Add(animal); }
            if (string.IsNullOrWhiteSpace((string)animal["SpeciesName"])) animal["SpeciesName"] = rig["speciesName"]?.DeepClone();
        }
        var ids = normalized.OfType<JObject>().Select(animal => (string)animal["AnimalID"]).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().OrderBy(id => id, StringComparer.Ordinal);
        var planned = archive?.Sequence?["sequences"] as JArray ?? new JArray();
        return new JObject { ["experimentName"] = data?["ExperimentName"]?.DeepClone(), ["experimenterName"] = data?["ExperimenterName"]?.DeepClone(),
            ["configuredAnimals"] = normalized, ["identifiedAnimalIds"] = new JArray(ids),
            ["backends"] = new JArray(rigs.OfType<JObject>().Select(rig => (string)rig["closedLoopMode"]).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct()),
            ["plannedSceneNames"] = new JArray(planned.OfType<JObject>().Select(step => (string)step["sceneName"]).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct()) };
    }

    public static string HashFile(string path, CancellationToken cancellation = default)
    {
        using (var stream = File.OpenRead(path))
        using (var hash = SHA256.Create())
        {
            byte[] buffer = new byte[65536]; int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
            { cancellation.ThrowIfCancellationRequested(); hash.TransformBlock(buffer, 0, count, buffer, 0); }
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
        }
    }
    private static long Min(long? old, long value) => old.HasValue ? Math.Min(old.Value, value) : value;
    private static long Max(long? old, long value) => old.HasValue ? Math.Max(old.Value, value) : value;
    private static string Local(long? ticks) => ticks.HasValue ? new DateTime(ticks.Value).ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture) : null;
    private static JToken Span(long? first, long? last) => first.HasValue && last.HasValue ? new JValue((last.Value - first.Value) / (double)TimeSpan.TicksPerSecond) : JValue.CreateNull();
}
