#!/usr/bin/env python3
"""Offline regressions of the production same-build configured-world reader.

Extracts the exact reader (no renderer simulation), compiles against the imported
Newtonsoft assembly, and writes synthetic recordings only in a temporary folder.
No NuGet fetch, Unity launch, or existing recording writes are performed.
"""
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape


REGRESSION = r'''
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class RunConfigArchive
{
    public static string Hash(byte[] bytes) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
}
public sealed class RecordedWorldStateWriter { public const string FileName = "world_state.jsonl"; }
public sealed class RecordedPendingDestroy {}
public sealed class FakeGameObject { public bool Active; public void SetActive(bool active) { Active = active; } }
public sealed class RecordedWorldStateTarget
{
    public string Key;
    public FakeGameObject gameObject = new FakeGameObject();
    public int Applied = -1;
    public T GetComponentInParent<T>(bool includeInactive) where T : class => null;
    public void Apply(JObject state) { Applied = (int)state["configuration"]["marker"]; }
}
public static class Regression
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Throws<T>(Action action, string message) where T : Exception
    { checks++; try { action(); } catch (T) { return; } throw new Exception(message); }
    private static void CheckWrittenEntry(JObject payload)
    {
        var expected = (JObject)payload.DeepClone();
        expected["sha256"] = ReplayPresentationJournal.HashEntry(expected);
        using (var writer = new StringWriter { NewLine = "\r\n" })
        {
            ReplayPresentationJournal.WriteEntry(writer, payload);
            string row = writer.ToString();
            Check(row == expected.ToString(Formatting.None) + writer.NewLine, "Single-pass writer preserves the exact acquisition bytes.");
            Check((string)ReplayPresentationJournal.ParseVerified(row.Substring(0, row.Length - 1))["sha256"] == (string)expected["sha256"], "Single-pass output verifies with the production reader.");
            Check(payload.Property("sha256") == null, "Single-pass writer does not mutate the captured state.");
        }
    }
    private static JObject Row(int frame, bool active = true, string payload = "")
    {
        var row = new JObject {
            ["presentationId"] = "p", ["frame"] = frame, ["shaderTime"] = frame / 60.0, ["timestamp"] = "2026-10-29T12:34:56.0000000+00:00",
            ["activeTargets"] = active ? new JArray("a", "b") : new JArray(),
            ["targets"] = active ? new JArray(new[] {"a", "b"}.Select(key => new JObject {
                ["key"] = key, ["configuration"] = new JObject { ["marker"] = frame, ["payload"] = key == "a" ? payload : "" }
            })) : new JArray()
        };
        row["sha256"] = ReplayPresentationJournal.HashEntry(row); return row;
    }
    private static string Write(string root, string name, IEnumerable<JObject> rows)
    {
        string folder = Path.Combine(root, name); Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder, RecordedWorldStateWriter.FileName), rows.Select(row => row.ToString(Formatting.None)), new UTF8Encoding(false));
        return folder;
    }
    private static string Hash(string folder) => RunConfigArchive.Hash(File.ReadAllBytes(Path.Combine(folder, RecordedWorldStateWriter.FileName)));
    public static void Main(string[] args)
    {
        string root = args[0];
        // Float JValues and parsed doubles have different round-trip formatting on
        // Unity Mono. Integrity must use emitted bytes, even for extreme numbers.
        object[] numbers = { 60.0312424f, .31088907312925168d, float.Epsilon, float.MaxValue,
            double.Epsilon, double.MaxValue, -0.0f, -0.0d, 1e-28d, 1e28d, -1e-28d, .1f, .1d };
        foreach (object number in numbers)
        {
            var numeric = new JObject { ["number"] = new JValue(number) };
            CheckWrittenEntry(numeric);
            numeric["sha256"] = ReplayPresentationJournal.HashEntry(numeric);
            string encoded = numeric.ToString(Formatting.None);
            for (int read = 0; read < 3; read++)
                Check((string)ReplayPresentationJournal.ParseVerified(encoded)["sha256"] == (string)numeric["sha256"], "Floating payload integrity must not depend on parse/format idempotence.");
            Check((string)ReplayPresentationJournal.ParseVerified(encoded + "\r")["sha256"] == (string)numeric["sha256"], "CRLF is a line ending, not payload content.");
        }
        string knownPayload = "{\"unityTime\":0.31088907312925168,\"shaderValue\":60.0312424}";
        string knownHash = RunConfigArchive.Hash(Encoding.UTF8.GetBytes(knownPayload));
        string knownRecord = knownPayload.Substring(0, knownPayload.Length - 1) + ",\"sha256\":\"" + knownHash + "\"}";
        Check((string)ReplayPresentationJournal.ParseVerified(knownRecord)["sha256"] == knownHash, "Real acquisition floating representations verify without rewriting their bytes.");
        Throws<InvalidDataException>(() => ReplayPresentationJournal.ParseVerified(knownRecord.Replace("60.0312424", "60.0312425")), "Changed numeric bytes must fail integrity.");
        var checksumOnly = new JObject(); checksumOnly["sha256"] = ReplayPresentationJournal.HashEntry(checksumOnly);
        Check(ReplayPresentationJournal.ParseVerified(checksumOnly.ToString(Formatting.None)).Count == 1, "Empty payload checksum is well-defined.");
        CheckWrittenEntry(new JObject());
        CheckWrittenEntry(new JObject { ["nested"] = new JObject { ["sha256"] = "\"}\\\nünicode" } });
        string normal = Write(root, "normal", Enumerable.Range(1, 24).Select(frame => Row(frame)));
        string sourceHash = Hash(normal);
        var a = new RecordedWorldStateTarget { Key = "a" }; var b = new RecordedWorldStateTarget { Key = "b" };
        using (var reader = new RecordedWorldStateReader(normal))
        {
            Check(reader.CachedRows == 0 && reader.ParsedRowCount == 0, "Indexing must not retain every parsed frame.");
            var configuration = reader.ConfigurationFor("p", 12, "a");
            Check((int)configuration["marker"] == 12, "Configuration index selects requested frame.");
            configuration["marker"] = -1;
            Check((int)reader.ConfigurationFor("p", 12, "a")["marker"] == 12, "Configuration callers cannot mutate cached evidence.");
            reader.ConfigurationFor("p", 12, "b");
            var clock = reader.Apply("p", 12, new[] { a, b });
            Check(reader.ParsedRowCount == 1 && reader.CachedRows == 1, "Targets and configuration share one parsed row.");
            Check(a.Applied == 12 && b.Applied == 12 && clock["targets"] == null, "Apply restores both targets and returns lightweight clock.");
            reader.Apply("p", 2, new[] { a, b });
            Check(a.Applied == 2 && b.Applied == 2, "Reverse seeking uses complete target checkpoints.");
            reader.Apply("p", 23, new[] { a, b });
            Check(a.Applied == 23, "Forward seek needs no preceding simulation.");
            Throws<InvalidDataException>(() => reader.Apply("p", 25, new[] { a, b }), "Missing exact frame must fail instead of using the previous frame.");
            Throws<InvalidDataException>(() => reader.Apply("unknown", 2, new[] { a, b }), "Missing presentation must fail.");
            Throws<InvalidDataException>(() => reader.Apply("p", 2, new[] { a }), "Missing controller target must fail.");
            foreach (int frame in Enumerable.Range(1, 24)) reader.ConfigurationFor("p", frame, "a");
            Check(reader.CachedRows == RecordedWorldStateReader.MaximumCachedRows, "LRU row count stays bounded at sixteen.");
            long before = reader.ParsedRowCount; reader.ConfigurationFor("p", 1, "a");
            Check(reader.ParsedRowCount == before + 1, "Evicted oldest row is reparsed on demand.");
        }
        Check(Hash(normal) == sourceHash, "Replay operations must leave recording bytes untouched.");
        string membership = Write(root, "membership", new[] { Row(1), Row(2, false), Row(3) });
        using (var reader = new RecordedWorldStateReader(membership))
        {
            reader.Apply("p", 2, new[] { a, b }); Check(!a.gameObject.Active && !b.gameObject.Active, "Tombstone membership removes objects.");
            reader.Apply("p", 1, new[] { a, b }); Check(a.gameObject.Active && b.gameObject.Active && a.Applied == 1, "Reverse seek restores prior membership.");
        }
        var unchanged = Row(2); unchanged["targets"] = new JArray(); unchanged["sha256"] = ReplayPresentationJournal.HashEntry(unchanged);
        string delta = Write(root, "delta", new[] { Row(1), unchanged, Row(3) });
        using (var reader = new RecordedWorldStateReader(delta))
        {
            reader.Apply("p", 3, new[] { a, b }); reader.Apply("p", 2, new[] { a, b });
            Check(a.Applied == 1 && b.Applied == 1, "Reverse seek into an unchanged frame restores its preceding complete checkpoint.");
        }
        // Seven five-MiB snapshots exceed the byte budget while remaining below the row limit.
        string budget = Write(root, "budget", Enumerable.Range(1, 7).Select(frame => Row(frame, true, new string('x', 5 * 1024 * 1024))));
        string budgetHash = Hash(budget);
        using (var reader = new RecordedWorldStateReader(budget))
        {
            foreach (int frame in Enumerable.Range(1, 7))
            {
                reader.Apply("p", frame, new[] { a, b });
                Check(reader.CachedSerializedBytes <= RecordedWorldStateReader.MaximumCachedSerializedBytes, "Serialized row payload exceeds cache byte budget.");
            }
            Check(reader.CachedRows < 7 && reader.CachedRows <= 16, "Byte budget evicts rows before the row limit.");
        }
        Check(Hash(budget) == budgetHash, "Large seek cache leaves source bytes untouched.");
        using (var cancellation = new CancellationTokenSource())
        {
            using (var reader = new RecordedWorldStateReader(normal, cancellation.Token))
            {
                cancellation.Cancel();
                Throws<OperationCanceledException>(() => reader.ConfigurationFor("p", 1, "a"), "Canceled seek must interrupt cached and uncached reads.");
            }
            Throws<OperationCanceledException>(() => new RecordedWorldStateReader(normal, cancellation.Token), "Canceled construction must fail before indexing.");
        }
        using (var cancellation = new CancellationTokenSource())
        {
            var indexing = Task.Run(() => { using (var reader = new RecordedWorldStateReader(budget, cancellation.Token)) { } });
            cancellation.CancelAfter(20);
            Throws<OperationCanceledException>(() => indexing.GetAwaiter().GetResult(), "Canceled indexing must stop a large source scan.");
            using (var exclusive = new FileStream(Path.Combine(budget, RecordedWorldStateWriter.FileName), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Check(exclusive.CanWrite, "Canceled construction leaked its source handle.");
        }
        // A deterministic invalid-hash constructor failure must close its source handle.
        var corrupt = Row(1); corrupt["shaderTime"] = -1;
        string bad = Write(root, "corrupt", new[] { corrupt });
        Throws<InvalidDataException>(() => new RecordedWorldStateReader(bad), "Invalid row hash must fail construction.");
        using (var exclusive = new FileStream(Path.Combine(bad, RecordedWorldStateWriter.FileName), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Check(exclusive.CanWrite, "Failed constructor leaked its source handle.");
        string missing = Write(root, "missing-checkpoint", new[] { Row(1, false) });
        var missingRow = Row(2, false); missingRow["activeTargets"] = new JArray("a"); missingRow["sha256"] = ReplayPresentationJournal.HashEntry(missingRow);
        File.AppendAllText(Path.Combine(missing, RecordedWorldStateWriter.FileName), missingRow.ToString(Formatting.None) + "\n");
        Throws<InvalidDataException>(() => new RecordedWorldStateReader(missing), "Membership without an initial checkpoint must fail.");
        string duplicate = Write(root, "duplicate", new[] { Row(1), Row(1) });
        Throws<InvalidDataException>(() => new RecordedWorldStateReader(duplicate), "Duplicate frame indices must fail.");
        string changed = Write(root, "changed", new[] { Row(1) });
        string changedHash = Hash(changed);
        using (var reader = new RecordedWorldStateReader(changed))
        {
            reader.ConfigurationFor("p", 1, "a");
            // Unix permits append through another descriptor; Windows correctly denies this read-shared source.
            try { File.AppendAllText(Path.Combine(changed, RecordedWorldStateWriter.FileName), " "); }
            catch (IOException) { }
            if (Hash(changed) != changedHash)
                Throws<InvalidDataException>(() => reader.ConfigurationFor("p", 1, "a"), "Modified source must invalidate even cached reads.");
        }
        Console.WriteLine("PASS " + checks + " same-build world-reader assertions (production reader, bounded cache, immutable sources).");
    }
}
'''


def main():
    repo = Path(__file__).resolve().parents[1]
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('Install .NET SDK 6 or newer for this offline regression.')
    dll = next(iter((repo / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll')), None)
    if dll is None:
        raise SystemExit('Open this project in Unity once to import the pinned Newtonsoft dependency.')
    versions = [int(value) for value in re.findall(r'^(\d+)\.', subprocess.check_output([dotnet, '--list-sdks'], text=True), re.MULTILINE) if int(value) >= 6]
    production = (repo / 'Assets/Scripts/Replay/RecordedWorldState.cs').read_text()
    reader = production[production.index('public sealed class RecordedWorldStateReader : IDisposable'):]
    with tempfile.TemporaryDirectory(prefix='matrix-world-reader-') as temporary:
        root = Path(temporary)
        (root / 'Reader.cs').write_text('using System; using System.Collections.Generic; using System.IO; using System.Linq; using System.Text; using System.Threading; using Newtonsoft.Json.Linq;\n' + reader)
        (root / 'Regression.cs').write_text(REGRESSION)
        project = root / 'WorldReader.csproj'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{max(versions)}.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(dll.resolve()))}</HintPath></Reference><Compile Include="{escape(str(repo / "Assets/Scripts/Replay/ReplayPresentationJournal.cs"))}"/></ItemGroup></Project>')
        configuration = root / 'NuGet.Config'
        configuration.write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        env = dict(os.environ, DOTNET_CLI_HOME=temporary, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
        subprocess.run([dotnet, 'restore', str(project), '--configfile', str(configuration)], env=env, check=True, stdout=subprocess.DEVNULL)
        subprocess.run([dotnet, 'run', '--no-restore', '--project', str(project), '-p:UseSharedCompilation=false', '--', str(root / 'recordings')], env=env, check=True)
        mono_root = next(iter(Path('/Applications/Unity/Hub/Editor').glob('*/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge')), None)
        if mono_root is not None:
            executable = root / 'WorldReaderMono.exe'
            subprocess.run([str(mono_root / 'bin/mono'), str(mono_root / 'lib/mono/4.5/csc.exe'),
                '-nologo', '-out:' + str(executable), '-r:' + str(dll.resolve()),
                '-r:' + str(mono_root / 'lib/mono/4.5/Facades/netstandard.dll'), str(root / 'Reader.cs'),
                str(root / 'Regression.cs'), str(repo / 'Assets/Scripts/Replay/ReplayPresentationJournal.cs')], check=True)
            shutil.copyfile(dll, root / 'Newtonsoft.Json.dll')
            print('Checking the same production reader under Unity Mono:', flush=True)
            subprocess.run([str(mono_root / 'bin/mono'), str(executable), str(root / 'mono-recordings')], check=True)


if __name__ == '__main__':
    main()
