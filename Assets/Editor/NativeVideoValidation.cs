using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Import and decode fresh generated native video without modifying its source recording.</summary>
public static class NativeVideoValidation
{
    private static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
        throw new ArgumentException("Missing required argument: " + name);
    }

    private static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    private static Dictionary<string, string> Snapshot(string source)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Source file is a symbolic link.");
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                result[Path.GetRelativePath(source, path)] = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        return result;
    }

    private static bool Inside(string child, string parent) => child.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public static void Run()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!File.Exists(Path.Combine(project, "KANNADI_VALIDATION_COPY"))) throw new InvalidOperationException("Disposable project only.");
        string output = null, source = null;
        bool outputCreated = false;
        Dictionary<string, string> before = null;
        var observations = new JArray();
        int checks = 0;
        Action<bool, string> check = (ok, message) => { checks++; if (!ok) throw new Exception(message); };
        try
        {
            source = Path.GetFullPath(Arg("-native-video-source")).TrimEnd(Path.DirectorySeparatorChar);
            output = Path.GetFullPath(Arg("-native-video-output")).TrimEnd(Path.DirectorySeparatorChar);
            check(Directory.Exists(source), "Native source recording must exist.");
            check(!Directory.Exists(output) && !File.Exists(output), "Validation output must be a fresh path.");
            check(!string.Equals(source, output, StringComparison.OrdinalIgnoreCase) && !Inside(source, output) && !Inside(output, source), "Source and output must be separate and may not contain each other.");
            check((File.GetAttributes(source) & FileAttributes.ReparsePoint) == 0, "Source must be a real recording directory.");
            foreach (string child in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
                check((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0, "Source child must not be a symbolic link.");
            before = Snapshot(source);
            check(before.ContainsKey("video-0000.avi") && before.ContainsKey("frames.jsonl"), "Fresh native AVI and timestamp sidecar must exist.");
            long nativeFrames = File.ReadLines(Path.Combine(source, "frames.jsonl")).Where(l => !string.IsNullOrWhiteSpace(l)).Select(JObject.Parse).LongCount(row => (int?)row["segment"] == 0);
            check(nativeFrames > 2, "Fresh native recording must contain enough timestamped frames.");
            Directory.CreateDirectory(output);
            outputCreated = true;
            string run = Path.Combine(output, "test-run");
            Directory.CreateDirectory(run);
            // This is a synthetic test timeline. No animal poses or consumed-input anchors are invented.
            long origin = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc).Ticks;
            File.WriteAllText(Path.Combine(run, "validation-context.json"), new JObject
            {
                ["testOnly"] = true,
                ["source"] = "Fresh native generated demo; no camera or animal measurement",
                ["synchronization"] = "Approximate operator offset of zero on a synthetic test timeline; no consumed-frame association",
                ["replayOriginTicks"] = origin.ToString(CultureInfo.InvariantCulture)
            }.ToString(Formatting.Indented));
            var receipt = MediaArchive.ImportAsync(new MediaImportRequest
            {
                RunDirectory = run, SourceDirectory = source, RigId = "VR1", ExpectedBackend = "Rekkefly",
                SourceClosed = true, SyncMode = "operator-offset", ManualOffsetSeconds = 0, ReplayOriginTicks = origin
            }).GetAwaiter().GetResult();
            check(receipt.Complete && !receipt.Pending && receipt.ImportedClips == 1, "Closed native source must import as one complete clip.");
            var archive = ReplayMediaArchive.LoadAsync(run, origin).GetAwaiter().GetResult();
            check(archive.Clips.Count == 1, "Imported archive must expose exactly one clip.");
            var clip = archive.Clips[0];
            check(clip.Complete && clip.State == "ready", "Imported native clip must verify successfully.");
            check(clip.SyncMode == "operator-offset" && clip.AssociationLabel == "Approximate operator offset", "Association must explicitly remain approximate.");
            check(clip.FrameCount == nativeFrames && clip.NominalFps > 0, "Encoded frame count must agree with fresh native timestamp sidecar.");
            check(!archive.TryResolveFrame(clip, -0.01, out _), "Negative media time must not resolve.");
            check(!archive.TryResolveFrame(clip, (clip.FrameCount + 1) / clip.NominalFps, out _), "Past-end media time must not resolve.");
            using (var decoder = ReplayAviFrameSource.Open(clip.AbsolutePath))
            {
                check(decoder.FrameCount == clip.FrameCount && decoder.Width > 0 && decoder.Height > 0, "Actual indexed AVI dimensions and count must agree with archive.");
                long[] order = { 0, clip.FrameCount / 2, clip.FrameCount - 1, clip.FrameCount / 2, 0 };
                var decodedHashes = new Dictionary<long, string>();
                foreach (long index in order)
                {
                    double replaySeconds = (index + .25) / clip.NominalFps;
                    check(archive.TryResolveFrame(clip, replaySeconds, out var target), "Operator-offset time must resolve while scrubbing.");
                    check(target.LocalFrame == index && Math.Abs(target.MediaSeconds - index / clip.NominalFps) < 1e-9, "Replay seconds must map to exact indexed native frame.");
                    var frame = decoder.ReadFrame(target.LocalFrame);
                    check(frame.Encoding == ReplayAviFrameSource.PixelEncoding.Jpeg, "Fresh native demo frame must be JPEG.");
                    var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    try
                    {
                        check(texture.LoadImage(frame.Bytes, false), "Unity must decode actual fresh native JPEG bytes.");
                        check(texture.width == decoder.Width && texture.height == decoder.Height && texture.width == frame.Width && texture.height == frame.Height, "Unity-decoded size must match AVI dimensions.");
                        byte[] png = texture.EncodeToPNG();
                        string decodedHash = Hash(png);
                        if (decodedHashes.TryGetValue(index, out string prior)) check(prior == decodedHash, "Backward seek must decode the exact same pixels.");
                        else decodedHashes.Add(index, decodedHash);
                        if (observations.Count == 0) File.WriteAllBytes(Path.Combine(output, "native-first-frame.png"), png);
                        observations.Add(new JObject
                        {
                            ["requestedReplaySeconds"] = replaySeconds, ["resolvedFrame"] = target.LocalFrame,
                            ["resolvedMediaSeconds"] = target.MediaSeconds, ["association"] = target.AssociationLabel,
                            ["width"] = texture.width, ["height"] = texture.height,
                            ["jpegSha256"] = Hash(frame.Bytes), ["decodedPngSha256"] = decodedHash
                        });
                    }
                    finally { UnityEngine.Object.DestroyImmediate(texture); }
                }
            }
            var after = Snapshot(source);
            check(before.Count == after.Count && before.All(entry => after.TryGetValue(entry.Key, out string value) && value == entry.Value), "Every original native recording file must remain byte-identical after import and scrubbing.");
            var result = new JObject
            {
                ["passed"] = true, ["checks"] = checks, ["unity"] = Application.unityVersion,
                ["scope"] = "Fresh native generated demo -> production media import -> verified archive -> indexed JPEG -> Unity Texture2D decode, forward/back scrubbing. No hardware, animal tracking, or consumed-input synchronization claim.",
                ["sourceDirectory"] = source, ["sourceHashesBefore"] = JObject.FromObject(before), ["sourceHashesAfter"] = JObject.FromObject(after),
                ["originalSourceUnchanged"] = true, ["testRunDirectory"] = run, ["syntheticReplayOriginTicks"] = origin.ToString(CultureInfo.InvariantCulture),
                ["importReceipt"] = receipt.ToJson(), ["archiveWarnings"] = new JArray(archive.Warnings),
                ["frameCount"] = clip.FrameCount, ["nominalFps"] = clip.NominalFps,
                ["syncMode"] = clip.SyncMode, ["association"] = clip.AssociationLabel, ["seeks"] = observations,
                ["preview"] = "native-first-frame.png"
            };
            File.WriteAllText(Path.Combine(output, "results.json"), result.ToString(Formatting.Indented));
            Debug.Log("NATIVE VIDEO VALIDATION PASSED: " + checks + " checks; output " + output);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            if (outputCreated && output != null && Directory.Exists(output))
            {
                bool? unchanged = null;
                try { var after = before == null ? null : Snapshot(source); unchanged = before == null ? (bool?)null : before.Count == after.Count && before.All(e => after.TryGetValue(e.Key, out string h) && e.Value == h); } catch { unchanged = false; }
                File.WriteAllText(Path.Combine(output, "results.json"), new JObject { ["passed"] = false, ["checks"] = checks, ["error"] = error.ToString(), ["originalSourceUnchanged"] = unchanged, ["seeks"] = observations }.ToString(Formatting.Indented));
            }
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
