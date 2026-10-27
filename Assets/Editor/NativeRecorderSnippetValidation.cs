using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Verify closed real recorder outputs, frame counts and reversible Unity decoding.</summary>
public static class NativeRecorderSnippetValidation
{
    public static void Run()
    {
        if (!File.Exists("KANNADI_VALIDATION_COPY")) throw new InvalidOperationException("Disposable project only.");
        var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-snippet-cases");
        if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Provide -snippet-cases JSON.");
        var request = JObject.Parse(File.ReadAllText(args[index + 1]));
        string output = Path.GetFullPath((string)request["output"]);
        if (Directory.Exists(output)) throw new IOException("Use a new validation output path.");
        Directory.CreateDirectory(output); int checks = 0; var records = new List<object>();
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        try
        {
            foreach (var entry in request["cases"].OfType<JObject>())
            {
                string name = (string)entry["name"], source = Path.GetFullPath((string)entry["source"]);
                long count = (long)entry["frames"]; var before = Snapshot(source);
                string run = Path.Combine(output, name); Directory.CreateDirectory(run);
                long origin = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc).Ticks;
                var receipt = MediaArchive.ImportAsync(new MediaImportRequest { RunDirectory = run, SourceDirectory = source, RigId = "VR1", ExpectedBackend = (string)entry["backend"], SourceClosed = true, SyncMode = "operator-offset", ManualOffsetSeconds = 0, ReplayOriginTicks = origin }).GetAwaiter().GetResult();
                Check(receipt.Complete && !receipt.Pending && receipt.ImportedClips == 1, "Finalized native snippet imports as exactly one completed clip.");
                var archive = ReplayMediaArchive.LoadAsync(run, origin).GetAwaiter().GetResult();
                var clip = archive.Clips.Single();
                Check(clip.State == "ready" && clip.Complete, "Imported clip verifies its checksum and encoded index.");
                Check(clip.FrameCount == count, "Actual encoded frame count equals native recorder's accepted/written frame count.");
                Check(clip.AssociationLabel == "Approximate operator offset", "No consumed-input or exposure synchronization is invented.");
                var frames = new List<object>(); var hashes = new Dictionary<long, string>();
                using (var decoder = ReplayAviFrameSource.Open(clip.AbsolutePath))
                {
                    Check(decoder.FrameCount == count, "Indexed decoder confirms exact native snippet length.");
                    foreach (long frameIndex in new[] { 0L, count / 2, count - 1, count / 2, 0L })
                    {
                        Check(archive.TryResolveFrame(clip, (frameIndex + .25) / clip.NominalFps, out var resolved) && resolved.LocalFrame == frameIndex, "Forward/reverse replay clock resolves the exact local video frame.");
                        var frame = decoder.ReadFrame(frameIndex);
                        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                        try
                        {
                            Check(frame.Encoding == ReplayAviFrameSource.PixelEncoding.Jpeg && texture.LoadImage(frame.Bytes), "Unity decodes actual native MJPEG pixels.");
                            Check(texture.width == decoder.Width && texture.height == decoder.Height, "Decoded dimensions match encoded dimensions.");
                            byte[] png = texture.EncodeToPNG(); string digest = Hash(png);
                            if (hashes.TryGetValue(frameIndex, out var previous)) Check(previous == digest, "Reverse seek returns exactly the same decoded pixels.");
                            else hashes[frameIndex] = digest;
                            if (frameIndex == 0 && frames.Count == 0) File.WriteAllBytes(Path.Combine(run, "first-frame.png"), png);
                            frames.Add(new { frame = frameIndex, width = texture.width, height = texture.height, pngSha256 = digest });
                        }
                        finally { UnityEngine.Object.DestroyImmediate(texture); }
                    }
                }
                var after = Snapshot(source);
                Check(before.Count == after.Count && before.All(pair => after.TryGetValue(pair.Key, out var value) && value == pair.Value), "Original recorder files remain byte-identical after archive import and seeking.");
                records.Add(new { name, source, expectedFrames = count, encodedFrames = clip.FrameCount, nominalFps = clip.NominalFps, clip.AssociationLabel, frames, sourceHashesBefore = before, sourceHashesAfter = after });
            }
            File.WriteAllText(Path.Combine(output, "results.json"), JsonConvert.SerializeObject(new { checks, failure = (string)null, cases = records, scope = "Real finalized generated/prerecorded native videos -> production archive import -> indexed MJPEG -> Unity pixels and reverse seeks. Synthetic approximate timeline only; no fresh consumed-input or exposure-sync claim." }, Formatting.Indented));
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "results.json"), JsonConvert.SerializeObject(new { checks, failure = error.ToString(), cases = records }, Formatting.Indented));
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
    private static string Hash(byte[] bytes)
    { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    private static Dictionary<string, string> Snapshot(string source)
    {
        var result = new Dictionary<string, string>();
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        { using (var stream = File.OpenRead(file)) using (var hash = SHA256.Create()) result[Path.GetRelativePath(source, file)] = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        return result;
    }
}
