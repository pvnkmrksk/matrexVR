using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Reads a saved run without consulting or changing the currently installed experiment JSON.</summary>
public sealed class ReplaySessionArchive
{
    public sealed class Sample
    {
        public long timestampTicks;
        public double time;
        public Vector3 position, rotation;
        public string rig, recordedRigName, scene, configFile, stepName, phase, skyboxId;
        public int sequenceStep, stepIndex, trial,loopIndex,cumulativeStep;
        public int unityFrame = -1;
        public bool colorsSwapped;
        public float? grayAtTrialStart;
        public string blackSideAtTrialStart;
        internal bool swapEvent;
        public string StimulusKey => ContextKey + "|swap:" + colorsSwapped + "|gray:" + grayAtTrialStart?.ToString(CultureInfo.InvariantCulture) + "|black:" + blackSideAtTrialStart;
        public string ContextKey => scene + "|" + sequenceStep + "|" + trial + "|" + loopIndex + "|" + cumulativeStep + "|" + configFile + "|" + stepName + "|" + phase;
    }

    public string DirectoryPath { get; }
    public Dictionary<string, List<Sample>> Rigs { get; } = new Dictionary<string, List<Sample>>(StringComparer.Ordinal);
    public List<string> Warnings { get; } = new List<string>();
    public long OriginTicks { get; private set; }
    public double Duration { get; private set; }
    public double FirstExperimentTime { get; private set; }
    public JObject Sequence { get; private set; }
    public JObject SystemConfiguration { get; private set; }
    private readonly JObject manifest;
    private readonly HashSet<string> verified = new HashSet<string>(StringComparer.Ordinal);

    public ReplaySessionArchive(string directory)
    {
        DirectoryPath = Path.GetFullPath(directory.Trim().Trim('"'));
        if (!Directory.Exists(DirectoryPath)) throw new DirectoryNotFoundException("Run folder does not exist: " + DirectoryPath);
        string manifestPath = Path.Combine(DirectoryPath, "config_manifest.json");
        if (File.Exists(manifestPath))
        {
            manifest = JObject.Parse(File.ReadAllText(manifestPath));
            if (manifest.Value<bool?>("complete") == false) Warnings.Add("The recording's config archive is marked incomplete.");
        }
        else Warnings.Add("Legacy recording: no hashed config manifest. Only files actually saved in this run can be restored.");
        Sequence = ReadRole("sequence", "sequenceConfig.json");
        SystemConfiguration = ReadRole("system", "system_config.json");
    }

    public void LoadRigSamples()
    {
        Rigs.Clear(); OriginTicks = long.MaxValue; Duration = 0;
        foreach (string file in CsvFiles(DirectoryPath))
        {
            using (var reader = OpenCsv(file))
            {
                string first = ReadCsvRecord(reader);
                if (first == null) continue;
                var header = Csv(first.TrimStart('\ufeff'));
                int time = Array.IndexOf(header, "Current Time"), rig = Array.IndexOf(header, "VR");
                if (time < 0 || rig < 0) continue; // Agent and audit logs have a different schema.
                int[] pose = { Array.IndexOf(header,"GameObjectPosX"), Array.IndexOf(header,"GameObjectPosY"), Array.IndexOf(header,"GameObjectPosZ"),
                    Array.IndexOf(header,"GameObjectRotX"), Array.IndexOf(header,"GameObjectRotY"), Array.IndexOf(header,"GameObjectRotZ") };
                if (pose.Any(i => i < 0)) { Warnings.Add("Missing pose columns: " + Path.GetFileName(file)); continue; }
                int max = Math.Max(Math.Max(time, rig), pose.Max()), rejected = 0, misaligned = 0, nonExperiment = 0;
                string line;
                while ((line = ReadCsvRecord(reader)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] cells;
                    try { cells = Csv(line); } catch (InvalidDataException) { rejected++; continue; }
                    if (cells.Length != header.Length) { rejected++; misaligned++; continue; }
                    if (IsNonExperimentPose(Cell(header,cells,"Scene"),file)) { nonExperiment++; continue; }
                    if (cells.Length <= max || string.IsNullOrWhiteSpace(cells[rig]) || !TryTimestamp(cells[time], out long ticks)) { rejected++; continue; }
                    float[] values = new float[6]; bool valid = true;
                    for (int i = 0; i < pose.Length; i++) valid &= TryNumber(cells[pose[i]], out values[i]);
                    if (!valid) { rejected++; continue; }
                    var sample = new Sample { timestampTicks = ticks, rig = CanonicalRig(cells[rig]),recordedRigName=cells[rig],
                        position = new Vector3(values[0], values[1], values[2]), rotation = new Vector3(values[3], values[4], values[5]),
                        scene = Cell(header,cells,"Scene"), configFile = Cell(header,cells,"ConfigFile"), stepName = Cell(header,cells,"stepName"),
                        phase = Cell(header,cells,"experimentPhase"), skyboxId = Cell(header,cells,"skyboxId"),
                        unityFrame = Integer(Cell(header,cells,"unityFrame"),-1),
                        swapEvent = TryNumber(Cell(header,cells,"swapElapsedSec"), out _),
                        grayAtTrialStart = TryNumber(Cell(header,cells,"grayAtTrialStart"), out float gray) ? gray : (float?)null,
                        blackSideAtTrialStart = Cell(header,cells,"blackSideAtTrialStart"),
                        sequenceStep = Integer(Cell(header,cells,"CurrentStep"),-1), stepIndex = Integer(Cell(header,cells,"stepIndex"),-1),
                        trial = Integer(Cell(header,cells,"CurrentTrial"),0),loopIndex=Integer(Cell(header,cells,"loopIndex")),cumulativeStep=Integer(Cell(header,cells,"cumulativeStep")) };
                    if (!Rigs.TryGetValue(sample.rig, out var samples)) Rigs[sample.rig] = samples = new List<Sample>();
                    samples.Add(sample); OriginTicks = Math.Min(OriginTicks,ticks);
                }
                if (rejected > 0) Warnings.Add(Path.GetFileName(file) + ": skipped " + rejected + " malformed pose rows.");
                if (misaligned > 0) Warnings.Add(Path.GetFileName(file) + ": " + misaligned + " rows have a different column count; legacy decimal-comma numbers or unquoted delimiters cannot be reconstructed safely.");
                if (nonExperiment > 0) Warnings.Add(Path.GetFileName(file) + ": excluded " + nonExperiment + " setup/replay pose rows from experiment playback.");
            }
        }
        if (Rigs.Count == 0) throw new InvalidDataException("This folder contains no readable animal pose CSV rows (Current Time, VR and six GameObject pose columns). " + string.Join(" ",Warnings.Take(4)));
        foreach (string id in Rigs.Keys.ToArray())
        {
            // Stable sorting preserves explicit onset/swap rows written in the same timestamp tick.
            var samples = Rigs[id].OrderBy(sample => sample.timestampTicks).ToList(); Rigs[id] = samples;
            string context = null; bool swapped = false; float? gray = null; string black = "";
            foreach (var sample in samples)
            {
                sample.time = (sample.timestampTicks - OriginTicks) / (double)TimeSpan.TicksPerSecond;
                if (sample.ContextKey != context) { context = sample.ContextKey; swapped = false; gray = null; black = ""; }
                if (sample.swapEvent) swapped = true;
                if (sample.grayAtTrialStart.HasValue) gray = sample.grayAtTrialStart;
                if (!string.IsNullOrEmpty(sample.blackSideAtTrialStart)) black = sample.blackSideAtTrialStart;
                sample.colorsSwapped = swapped; sample.grayAtTrialStart = gray; sample.blackSideAtTrialStart = black;
            }
            Duration = Math.Max(Duration,samples[samples.Count-1].time);
        }
        FirstExperimentTime = Rigs.Values.SelectMany(samples => samples).Where(sample => sample.scene != "ControlScene" && sample.scene != "ReplayScene").Select(sample => sample.time).DefaultIfEmpty(0).Min();
        Warnings.Add("Historical rig clocks are local wall time. Clock changes, unlogged animation, and tracker validity cannot be recovered from these CSVs.");
    }

    public JObject ExperimentFor(Sample sample)
    {
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
        if (config["preStimulus"] != null || config["stimulus"] != null || config["postStimulus"] != null)
        {
            string phase = string.IsNullOrWhiteSpace(sample.phase) || sample.phase == "assessment" ? "preStimulus" : sample.phase;
            if (!(config[phase] is JObject payload)) { Warn("Saved phase missing: " + phase + " in " + reference); return null; }
            config = (JObject)payload.DeepClone();
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
                            if(IsNonExperimentPose(scene,file))continue;
                            return folder;
                        }
                    }
                }
                catch(InvalidDataException){}catch(IOException){}
            }
        }
        return null;
    }
    private static bool IsNonExperimentPose(string scene,string file)
    {
        scene=(scene??"").Trim();
        if(string.Equals(scene,"ControlScene",StringComparison.OrdinalIgnoreCase) || string.Equals(scene,"ReplayScene",StringComparison.OrdinalIgnoreCase))return true;
        if(scene.Length>0)return false;
        string name=Path.GetFileName(file);
        return name.IndexOf("_ControlScene_",StringComparison.OrdinalIgnoreCase)>=0 || name.IndexOf("_ReplayScene_",StringComparison.OrdinalIgnoreCase)>=0;
    }
    public static string CanonicalRig(string name)
    {
        if(string.IsNullOrEmpty(name))return name;
        if(name.StartsWith("VR",StringComparison.OrdinalIgnoreCase))
        {int n=2;while(n<name.Length && char.IsDigit(name[n]))n++;if(n>2)return name.Substring(0,n).ToUpperInvariant();}
        return name;
    }
    public static string[] CsvFiles(string directory) => Directory.GetFiles(directory,"*.csv*").Where(p => p.EndsWith(".csv",StringComparison.OrdinalIgnoreCase) || p.EndsWith(".csv.gz",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
    public static StreamReader OpenCsv(string path)
    {
        Stream stream = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
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
    public static string ReadCsvRecord(StreamReader reader)
    {
        string line = reader.ReadLine();
        if (line == null || line.IndexOf('"') < 0) return line;
        var record = new StringBuilder(line); bool quoted = false;
        while (true)
        {
            for (int i = 0; i < line.Length; i++)
                if (line[i] == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') i++;
                    else quoted = !quoted;
                }
            if (!quoted || (line = reader.ReadLine()) == null) return record.ToString();
            record.Append('\n').Append(line);
        }
    }
    public static string Cell(string[] header,string[] values,string key)
    { int index = Array.IndexOf(header,key); return index >= 0 && index < values.Length ? values[index] : ""; }
    public static string[] Csv(string row)
    {
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
