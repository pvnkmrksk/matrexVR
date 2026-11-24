using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>Lossless binary projection of the animal CSV fields used in replay.</summary>
internal static class ReplayPoseCacheCodec
{
    private const ulong Magic = 0x3145534f5054414d; // MATPOSE1, little endian
    private const ulong FooterMagic = 0x31444e4554414d50; // PMATEND1, little endian
    private const int FooterBytes = sizeof(long) + sizeof(ulong);
    private const int RowsPerBlock = 4096;
    private const int TargetBlockBytes = 256 * 1024;
    private const int MaxBlockBytes = 2 * 1024 * 1024;
    private const int MaxEncodedBytes = MaxBlockBytes + 65536;
    private const int MaxStringBytes = 65536;
    private const int MaxWarnings = 4096;
    private const int RowBytes = 2 * sizeof(long) + 7 * sizeof(int);
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

    internal static void Write(Stream destination, string sourceKey,
        IReadOnlyDictionary<string, List<ReplaySessionArchive.Sample>> rigs, IReadOnlyList<string> warnings,
        CancellationToken cancellation)
    {
        using var writer = new Writer(destination);
        foreach (var rig in rigs)
            foreach (var sample in rig.Value)
                writer.Add(sample, cancellation);
        cancellation.ThrowIfCancellationRequested();
        writer.Complete(sourceKey, warnings);
    }

    internal sealed class Writer : IDisposable
    {
        private readonly BinaryWriter output;
        private readonly MemoryStream raw = new MemoryStream(TargetBlockBytes);
        private readonly MemoryStream encoded = new MemoryStream(65536);
        private readonly BinaryWriter payload;
        private readonly byte[] pose = new byte[RowBytes];
        private Metadata metadata;
        private int rowsInBlock, rowsInRun, runOffset;
        private long totalRows;
        private bool completed;

        internal Writer(Stream destination)
        {
            if (!destination.CanSeek || destination.Position != 0 || destination.Length != 0)
                throw new ArgumentException("Pose index output must be an empty seekable stream.", nameof(destination));
            output = new BinaryWriter(destination, Utf8, leaveOpen: true);
            payload = new BinaryWriter(raw, Utf8, leaveOpen: true);
            output.Write(Magic);
        }

        internal void Add(ReplaySessionArchive.Sample sample, CancellationToken cancellation = default)
        {
            if (completed) throw new InvalidOperationException("The pose index is complete.");
            if ((totalRows & 4095) == 0) cancellation.ThrowIfCancellationRequested();
            if (rowsInBlock == RowsPerBlock || raw.Length >= TargetBlockBytes)
                FlushRows();
            if (metadata == null || !metadata.Matches(sample))
            {
                FinishRun();
                metadata = new Metadata(sample);
                runOffset = (int)raw.Position;
                payload.Write(0);
                metadata.Write(payload);
            }

            BinaryPrimitives.WriteInt64LittleEndian(pose.AsSpan(0, 8), sample.timestampTicks);
            BinaryPrimitives.WriteInt64LittleEndian(pose.AsSpan(8, 8), BitConverter.DoubleToInt64Bits(sample.monotonicTime));
            BinaryPrimitives.WriteInt32LittleEndian(pose.AsSpan(16, 4), sample.unityFrame);
            WriteVector(pose.AsSpan(20, 12), sample.position);
            WriteVector(pose.AsSpan(32, 12), sample.rotation);
            raw.Write(pose, 0, pose.Length);
            rowsInRun++;
            rowsInBlock++;
            totalRows++;
        }

        internal void Complete(string sourceKey, IReadOnlyList<string> warnings)
        {
            if (completed) throw new InvalidOperationException("The pose index is complete.");
            if (string.IsNullOrEmpty(sourceKey)) throw new ArgumentException("A pose source key is required.", nameof(sourceKey));
            if (warnings.Count > MaxWarnings) throw new InvalidDataException("Too many pose index warnings.");
            FlushRows();
            long footerOffset = output.BaseStream.Position;
            WriteString(payload, sourceKey);
            payload.Write(totalRows);
            payload.Write(warnings.Count);
            foreach (string warning in warnings) WriteString(payload, warning);
            WriteCompressedPayload();
            output.Write(footerOffset);
            output.Write(FooterMagic);
            output.Flush();
            completed = true;
        }

        private void FinishRun()
        {
            if (rowsInRun == 0) return;
            BinaryPrimitives.WriteInt32LittleEndian(raw.GetBuffer().AsSpan(runOffset, sizeof(int)), rowsInRun);
            rowsInRun = 0;
        }

        private void FlushRows()
        {
            if (rowsInBlock == 0) return;
            FinishRun();
            output.Write(rowsInBlock);
            WriteCompressedPayload();
            rowsInBlock = 0;
            metadata = null;
        }

        private void WriteCompressedPayload()
        {
            if (raw.Length == 0 || raw.Length > MaxBlockBytes)
                throw new InvalidDataException("Pose index block exceeds its allowed size.");
            encoded.SetLength(0);
            using (var compressor = new GZipStream(encoded, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
                compressor.Write(raw.GetBuffer(), 0, (int)raw.Length);
            if (encoded.Length > MaxEncodedBytes)
                throw new InvalidDataException("Compressed pose index block exceeds its allowed size.");
            output.Write((int)encoded.Length);
            output.Write((int)raw.Length);
            output.Write(encoded.GetBuffer(), 0, (int)encoded.Length);
            raw.SetLength(0);
        }

        public void Dispose()
        {
            payload.Dispose();
            raw.Dispose();
            encoded.Dispose();
            output.Dispose();
        }
    }

    internal static (Dictionary<string, List<ReplaySessionArchive.Sample>> Rigs, List<string> Warnings) Read(
        Stream source, string expectedSourceKey, CancellationToken cancellation, Action<long, long> reportProgress = null)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!source.CanSeek || source.Position != 0 || source.Length < sizeof(ulong) + FooterBytes)
            throw new InvalidDataException("Missing pose index header or footer.");
        long length = source.Length;
        using var input = new BinaryReader(source, Utf8, leaveOpen: true);
        if (input.ReadUInt64() != Magic) throw new InvalidDataException("Unsupported pose index format.");
        source.Position = length - FooterBytes;
        long footerOffset = input.ReadInt64();
        if (input.ReadUInt64() != FooterMagic || footerOffset < sizeof(ulong) || footerOffset > length - FooterBytes - 8)
            throw new InvalidDataException("Invalid pose index footer.");

        byte[] encoded = Array.Empty<byte>(), raw = Array.Empty<byte>();
        source.Position = footerOffset;
        int footerLength = ReadCompressedPayload(input, length - FooterBytes, ref encoded, ref raw);
        if (source.Position != length - FooterBytes)
            throw new InvalidDataException("Pose index footer has trailing bytes.");

        var footer = new PayloadReader(raw.AsSpan(0, footerLength));
        if (!string.Equals(footer.ReadString(), expectedSourceKey, StringComparison.Ordinal))
            throw new InvalidDataException("Pose index was built from different source files.");
        long totalRows = footer.ReadInt64();
        int warningCount = footer.ReadInt32();
        if (totalRows < 0 || warningCount < 0 || warningCount > MaxWarnings)
            throw new InvalidDataException("Invalid pose index footer counts.");
        var warnings = new List<string>(warningCount);
        for (int i = 0; i < warningCount; i++) warnings.Add(footer.ReadString());
        footer.RequireEnd();

        source.Position = sizeof(ulong);
        var rigs = new Dictionary<string, List<ReplaySessionArchive.Sample>>(StringComparer.Ordinal);
        long rowsRead = 0;
        while (source.Position < footerOffset)
        {
            cancellation.ThrowIfCancellationRequested();
            if (footerOffset - source.Position < 3 * sizeof(int))
                throw new InvalidDataException("Truncated pose index block.");
            int rowsInBlock = input.ReadInt32();
            if (rowsInBlock <= 0 || rowsInBlock > RowsPerBlock || rowsInBlock > totalRows - rowsRead)
                throw new InvalidDataException("Invalid pose index block row count.");
            int rawLength = ReadCompressedPayload(input, footerOffset, ref encoded, ref raw);
            ReadRows(raw.AsSpan(0, rawLength), rowsInBlock, rigs);
            rowsRead += rowsInBlock;
            reportProgress?.Invoke(source.Position, length);
        }
        if (rowsRead != totalRows) throw new InvalidDataException("Pose index row count differs from the footer.");
        reportProgress?.Invoke(length, length);
        return (rigs, warnings);
    }

    private static int ReadCompressedPayload(BinaryReader input, long boundary, ref byte[] encoded, ref byte[] raw)
    {
        int encodedLength = input.ReadInt32(), rawLength = input.ReadInt32();
        if (encodedLength <= 0 || encodedLength > MaxEncodedBytes || encodedLength > boundary - input.BaseStream.Position ||
            rawLength <= 0 || rawLength > MaxBlockBytes)
            throw new InvalidDataException("Invalid pose index block size.");
        if (encoded.Length < encodedLength) Array.Resize(ref encoded, encodedLength);
        if (raw.Length < rawLength) Array.Resize(ref raw, rawLength);
        ReadExactly(input.BaseStream, encoded, encodedLength);
        using var block = new MemoryStream(encoded, 0, encodedLength, writable: false);
        using var decompressor = new GZipStream(block, CompressionMode.Decompress);
        ReadExactly(decompressor, raw, rawLength);
        if (decompressor.ReadByte() != -1)
            throw new InvalidDataException("Pose index block expands past its declared size.");
        return rawLength;
    }

    private static void ReadExactly(Stream stream, byte[] destination, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(destination, offset, count - offset);
            if (read == 0) throw new EndOfStreamException("Truncated pose index block.");
            offset += read;
        }
    }

    private static void ReadRows(ReadOnlySpan<byte> bytes, int rowsInBlock,
        Dictionary<string, List<ReplaySessionArchive.Sample>> rigs)
    {
        var input = new PayloadReader(bytes);
        while (rowsInBlock > 0)
        {
            int runLength = input.ReadInt32();
            if (runLength <= 0 || runLength > rowsInBlock)
                throw new InvalidDataException("Invalid pose index metadata run length.");
            var metadata = new Metadata(ref input);
            if (!rigs.TryGetValue(metadata.Rig, out var samples))
                rigs.Add(metadata.Rig, samples = new List<ReplaySessionArchive.Sample>());
            var run = input.Read(runLength * RowBytes);
            for (int offset = 0; offset < run.Length; offset += RowBytes)
                samples.Add(metadata.ReadSample(run.Slice(offset, RowBytes)));
            rowsInBlock -= runLength;
        }
        input.RequireEnd();
    }

    private static void WriteString(BinaryWriter output, string value)
    {
        if (value == null)
        {
            output.Write(-1);
            return;
        }
        int count = Utf8.GetByteCount(value);
        if (count > MaxStringBytes) throw new InvalidDataException("Pose index text exceeds its allowed size.");
        output.Write(count);
        if (count != 0) output.Write(Utf8.GetBytes(value));
    }

    private static void WriteVector(Span<byte> bytes, Vector3 value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(0, 4), BitConverter.SingleToInt32Bits(value.x));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(4, 4), BitConverter.SingleToInt32Bits(value.y));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(8, 4), BitConverter.SingleToInt32Bits(value.z));
    }

    private static Vector3 ReadVector(ReadOnlySpan<byte> bytes) => new Vector3(
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(0, 4))),
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4, 4))),
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8, 4))));

    private ref struct PayloadReader
    {
        private ReadOnlySpan<byte> remaining;

        internal PayloadReader(ReadOnlySpan<byte> bytes) { remaining = bytes; }

        internal ReadOnlySpan<byte> Read(int count)
        {
            if (count < 0 || count > remaining.Length) throw new InvalidDataException("Truncated pose index payload.");
            var bytes = remaining.Slice(0, count);
            remaining = remaining.Slice(count);
            return bytes;
        }

        internal int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(Read(sizeof(int)));
        internal long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(Read(sizeof(long)));

        internal string ReadString()
        {
            int count = ReadInt32();
            if (count == -1) return null;
            if (count < 0 || count > MaxStringBytes)
                throw new InvalidDataException("Invalid pose index text size.");
            try { return Utf8.GetString(Read(count)); }
            catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid pose index text encoding.", error); }
        }

        internal void RequireEnd()
        {
            if (remaining.Length != 0) throw new InvalidDataException("Pose index payload has trailing bytes.");
        }
    }

    private sealed class Metadata
    {
        internal readonly string Rig;
        private readonly string recordedRigName, scene, configFile, stepName, phase, skyboxId, presentationId, blackSide;
        private readonly int sequenceStep, stepIndex, trial, loopIndex, cumulativeStep;
        private readonly bool swap;
        private readonly int? grayBits;

        internal Metadata(ReplaySessionArchive.Sample sample)
        {
            Rig = sample.rig;
            recordedRigName = sample.recordedRigName;
            scene = sample.scene;
            configFile = sample.configFile;
            stepName = sample.stepName;
            phase = sample.phase;
            skyboxId = sample.skyboxId;
            presentationId = sample.presentationId;
            blackSide = sample.blackSideAtTrialStart;
            sequenceStep = sample.sequenceStep;
            stepIndex = sample.stepIndex;
            trial = sample.trial;
            loopIndex = sample.loopIndex;
            cumulativeStep = sample.cumulativeStep;
            swap = sample.swapEvent;
            grayBits = FloatBits(sample.grayAtTrialStart);
        }

        internal Metadata(ref PayloadReader input)
        {
            Rig = input.ReadString();
            if (string.IsNullOrWhiteSpace(Rig)) throw new InvalidDataException("Pose index row has no animal rig.");
            recordedRigName = input.ReadString();
            scene = input.ReadString();
            configFile = input.ReadString();
            stepName = input.ReadString();
            phase = input.ReadString();
            skyboxId = input.ReadString();
            presentationId = input.ReadString();
            blackSide = input.ReadString();
            sequenceStep = input.ReadInt32();
            stepIndex = input.ReadInt32();
            trial = input.ReadInt32();
            loopIndex = input.ReadInt32();
            cumulativeStep = input.ReadInt32();
            byte flags = input.Read(1)[0];
            if (flags > 3) throw new InvalidDataException("Invalid pose index metadata flags.");
            swap = (flags & 1) != 0;
            grayBits = (flags & 2) != 0 ? input.ReadInt32() : (int?)null;
        }

        internal bool Matches(ReplaySessionArchive.Sample sample) =>
            Rig == sample.rig && recordedRigName == sample.recordedRigName && scene == sample.scene &&
            configFile == sample.configFile && stepName == sample.stepName && phase == sample.phase &&
            skyboxId == sample.skyboxId && presentationId == sample.presentationId && blackSide == sample.blackSideAtTrialStart &&
            sequenceStep == sample.sequenceStep && stepIndex == sample.stepIndex && trial == sample.trial &&
            loopIndex == sample.loopIndex && cumulativeStep == sample.cumulativeStep && swap == sample.swapEvent &&
            grayBits == FloatBits(sample.grayAtTrialStart);

        internal void Write(BinaryWriter output)
        {
            WriteString(output, Rig);
            WriteString(output, recordedRigName);
            WriteString(output, scene);
            WriteString(output, configFile);
            WriteString(output, stepName);
            WriteString(output, phase);
            WriteString(output, skyboxId);
            WriteString(output, presentationId);
            WriteString(output, blackSide);
            output.Write(sequenceStep);
            output.Write(stepIndex);
            output.Write(trial);
            output.Write(loopIndex);
            output.Write(cumulativeStep);
            output.Write((byte)((swap ? 1 : 0) | (grayBits.HasValue ? 2 : 0)));
            if (grayBits.HasValue) output.Write(grayBits.Value);
        }

        internal ReplaySessionArchive.Sample ReadSample(ReadOnlySpan<byte> row) => new ReplaySessionArchive.Sample
        {
            timestampTicks = BinaryPrimitives.ReadInt64LittleEndian(row.Slice(0, 8)),
            monotonicTime = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(row.Slice(8, 8))),
            unityFrame = BinaryPrimitives.ReadInt32LittleEndian(row.Slice(16, 4)),
            position = ReadVector(row.Slice(20, 12)),
            rotation = ReadVector(row.Slice(32, 12)),
            rig = Rig,
            recordedRigName = recordedRigName,
            scene = scene,
            configFile = configFile,
            stepName = stepName,
            phase = phase,
            skyboxId = skyboxId,
            presentationId = presentationId,
            blackSideAtTrialStart = blackSide,
            sequenceStep = sequenceStep,
            stepIndex = stepIndex,
            trial = trial,
            loopIndex = loopIndex,
            cumulativeStep = cumulativeStep,
            swapEvent = swap,
            grayAtTrialStart = grayBits.HasValue ? BitConverter.Int32BitsToSingle(grayBits.Value) : (float?)null
        };

        private static int? FloatBits(float? value) =>
            value.HasValue ? BitConverter.SingleToInt32Bits(value.Value) : (int?)null;
    }
}
