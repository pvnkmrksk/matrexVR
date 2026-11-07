using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>The exact inputs handed to a build's own controllers, before any visual reconstruction.</summary>
public sealed class ReplayPresentationJournal
{
    public const int SchemaVersion = 1;
    public const string ContractName = "replay_contract.json";
    public const string FileName = "presentations.jsonl";
    private readonly Dictionary<string,JObject> entries = new Dictionary<string,JObject>(StringComparer.Ordinal);
    public bool IsCurrentContract { get; }
    public JObject Contract { get; }

    public ReplayPresentationJournal(string directory, JObject currentPlayer = null)
    {
        string path = Path.Combine(directory, ContractName);
        if (!File.Exists(path))
        {
            if(File.Exists(Path.Combine(directory,FileName))||File.Exists(Path.Combine(directory,"world_state.jsonl")))
                throw new InvalidDataException("Same-build replay contract is missing from a recording with new-format evidence.");
            return;
        }
        Contract = Parse(File.ReadAllText(path));
        if ((int?)Contract["schemaVersion"] != SchemaVersion) throw new InvalidDataException("Unsupported same-build replay contract version.");
        if(currentPlayer!=null)RequireSameBuild(Contract["player"] as JObject, currentPlayer);
        IsCurrentContract = true;
        string journal = Path.Combine(directory, FileName);
        if (!File.Exists(journal)) throw new InvalidDataException("The recording's presentation journal is missing.");
        foreach (string line in File.ReadLines(journal))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var entry = ParseVerified(line);
            string id = (string)entry["id"];
            if (string.IsNullOrWhiteSpace(id) || entries.ContainsKey(id))
                throw new InvalidDataException("The presentation journal contains a duplicate, missing identifier or invalid hash.");
            entries.Add(id, entry);
        }
    }
    public void ValidateBuild(JObject current) { if(IsCurrentContract)RequireSameBuild(Contract["player"] as JObject,current); }
    public JObject For(string id)
    {
        if (!IsCurrentContract) return null;
        return (JObject)FindEntry(id).DeepClone();
    }
    internal void RequireEntry(string id) => FindEntry(id);
    private JObject FindEntry(string id)
    {
        if (string.IsNullOrEmpty(id) || !entries.TryGetValue(id, out var entry))
            throw new InvalidDataException("Recorded frame has no matching presentation. This build cannot faithfully replay incomplete controller inputs.");
        return entry;
    }
    /// <summary>Verify the exact compact JSON payload written by acquisition, before parsing numbers.
    /// The checksum is appended as the final top-level property by both journal writers.
    /// Reformatting parsed float/double values is not a portable representation: Unity Mono can
    /// parse 60.0312424 then format it as 60.031242399999996. Integrity covers recorded bytes.
    /// </summary>
    public static JObject ParseVerified(string text)
    {
        // The world reader retains CR in a CRLF line; it belongs to the line ending.
        if (text != null && text.EndsWith("\r", StringComparison.Ordinal)) text = text.Substring(0, text.Length - 1);
        const string marker = ",\"sha256\":\"";
        int index = text?.LastIndexOf(marker, StringComparison.Ordinal) ?? -1;
        bool onlyChecksum = index < 0 && text != null && text.StartsWith("{\"sha256\":\"", StringComparison.Ordinal);
        int hashStart = onlyChecksum ? "{\"sha256\":\"".Length : index + marker.Length;
        if (text == null || text.Length == 0 || text[0] != '{' || (!onlyChecksum && index < 1) ||
            hashStart + 64 + 2 != text.Length || !text.EndsWith("\"}", StringComparison.Ordinal))
            throw new InvalidDataException("Replay evidence must end with its acquisition SHA-256 checksum.");
        string expected = text.Substring(hashStart, 64);
        string payload = onlyChecksum ? "{}" : text.Substring(0, index) + "}";
        if (RunConfigArchive.Hash(System.Text.Encoding.UTF8.GetBytes(payload)) != expected)
            throw new InvalidDataException("Replay evidence payload SHA-256 is invalid.");
        var value = Parse(text);
        if ((string)value["sha256"] != expected) throw new InvalidDataException("Replay evidence checksum is not a top-level property.");
        return value;
    }

    public static string HashEntry(JObject source)
    {
        var copy = (JObject)source.DeepClone(); copy.Remove("sha256");
        return RunConfigArchive.Hash(System.Text.Encoding.UTF8.GetBytes(copy.ToString(Formatting.None)));
    }

    internal static void WriteEntry(TextWriter output, JObject entry)
    {
        if (entry.Property("sha256") != null)
            throw new ArgumentException("Replay evidence already contains a checksum.", nameof(entry));
        string payload = entry.ToString(Formatting.None);
        string hash = RunConfigArchive.Hash(System.Text.Encoding.UTF8.GetBytes(payload));
        output.Write(payload.AsSpan(0, payload.Length - 1));
        if (entry.Count != 0) output.Write(',');
        output.Write("\"sha256\":\"");
        output.Write(hash);
        output.WriteLine("\"}");
    }

    public static JObject Parse(string text)
    {
        using(var input = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None })
            return JObject.Load(input);
    }
    public static void RequireSameBuild(JObject recorded, JObject current)
    {
        if (recorded == null || current == null) throw new InvalidDataException("The same-build replay identity is missing.");
        string editorIdentity = (string)recorded["editorRuntimeIdentity"];
        if(!string.IsNullOrEmpty(editorIdentity)||(string)current["editorRuntimeIdentity"]!=null)
        {
            if(!string.IsNullOrEmpty(editorIdentity)&&editorIdentity==(string)current["editorRuntimeIdentity"]&&
                (string)recorded["unityVersion"]==(string)current["unityVersion"])return;
            throw new InvalidDataException("This Editor recording belongs to a different runtime session. Use the acquisition player build for portable same-build recordings.");
        }
        string recordedGuid = (string)recorded["buildGuid"], currentGuid = (string)current["buildGuid"];
        bool Usable(string value) => !string.IsNullOrWhiteSpace(value) && value.Any(character => character != '0' && character != '-');
        if (Usable(recordedGuid) && Usable(currentGuid))
        {
            if (recordedGuid != currentGuid) throw new InvalidDataException("This recording requires its acquisition build (" + recordedGuid + "). The running build is " + currentGuid + ".");
            return;
        }
        // Editor regression runs have no player GUID. A source fingerprint is
        // still exact; a revision/version alone does not establish same-build.
        string fingerprint = (string)recorded["runtimeSourceFingerprintSha256"];
        if (!string.IsNullOrEmpty(fingerprint) && fingerprint == (string)current["runtimeSourceFingerprintSha256"] &&
            (string)recorded["unityVersion"] == (string)current["unityVersion"]) return;
        throw new InvalidDataException("Cannot establish the acquisition build identity for the same-build replay contract.");
    }
}
