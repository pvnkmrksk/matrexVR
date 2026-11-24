using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

/// <summary>Lossless MATREX population frames with separately indexed, seekable blocks.</summary>
public static class PopulationRecording
{
    public const string Extension = ".matpop";
    private const ulong LegacyHeaderMagic = 0x3130504f5054414d; // MATPOP01
    private const ulong LegacyFooterMagic = 0x3130444e4554414d; // MATEND01
    private const ulong HeaderMagic = 0x3230504f5054414d; // MATPOP02
    private const ulong FooterMagic = 0x3230444e4554414d; // MATEND02
    // V2 keeps the original frame bytes inside independent gzip blocks. The
    // trailing directory sits between the identity table and the fixed footer.
    private const int FooterBytes = 48;
    private const int IndexEntryBytes = sizeof(long) * 2 + sizeof(int) * 3;
    private const int BlockHeaderBytes = sizeof(int) * 2;
    private const int FramesPerBlock = 16;
    private const int TargetBlockBytes = 256 * 1024;
    private const int MaxBlockBytes = 64 * 1024 * 1024;
    private const int MaxStoredBlockBytes = MaxBlockBytes + 1024 * 1024;
    private const int FrameHeaderBytes = sizeof(int) * 3 + sizeof(long);
    private const int MinFramePayloadBytes = 31;
    private const int MinSampleBytes = 30;

    public static bool IsFile(string path) => path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    public struct Identity
    {
        public int index;
        public string name, layer;
    }

    public struct Frame
    {
        public long ticks, nextOffset;
        public int unityFrame, count;
        public float sx, sy, sz, hexRadius;
        public string skyboxId, skyboxSampleUtc, phase;
        public int vrIndex, numberOfRings;
    }

    public struct Sample
    {
        public int id;
        public float x, y, z, rx, ry, rz, sx, sy, sz;
        public string animation;
        public float speed, visibilityPhase;
        public bool visible;
    }

    public struct Totals
    {
        public long frames, rows, firstTicks, lastTicks;
    }

    private struct FrameIndexEntry
    {
        public long blockOffset, ticks;
        public int offset, unityFrame, count;
    }

    private sealed class FrameWriter : BinaryWriter
    {
        public FrameWriter(Stream stream) : base(stream, Encoding.UTF8) { }

        // Mono's float overload allocates a byte array on every call. The int
        // overload reuses its buffer and writes the same little-endian bits.
        public override void Write(float value) => base.Write(BitConverter.SingleToInt32Bits(value));
    }

    // BufferedStream calls this on batches of already encoded bytes. The acquisition
    // worker authenticates exactly what it writes without rereading the finished file.
    private sealed class HashingFile : Stream
    {
        private readonly FileStream file;
        private readonly SHA256 hash = SHA256.Create();

        public HashingFile(string path)
        {
            try { file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read); }
            catch { hash.Dispose(); throw; }
        }

        public long BytesWritten { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => BytesWritten;
        public override long Position { get => BytesWritten; set => throw new NotSupportedException(); }
        public override void Flush() => file.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            file.Write(buffer, offset, count);
            hash.TransformBlock(buffer, offset, count, buffer, offset);
            BytesWritten += count;
        }

        public string FinishHash()
        {
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { file.Dispose(); }
                finally { hash.Dispose(); }
            }
            base.Dispose(disposing);
        }
    }

    public sealed class Writer : IDisposable
    {
        private readonly HashingFile file;
        private readonly BinaryWriter output;
        private readonly BinaryWriter frame;
        private readonly MemoryStream frameBuffer = new MemoryStream(65536);
        private readonly MemoryStream block = new MemoryStream(65536);
        private readonly MemoryStream encodedBlock = new MemoryStream(65536);
        private readonly AsyncSwarmWriter.RowLayout layout;
        private readonly Dictionary<(int index, string name, string layer), int> ids =
            new Dictionary<(int index, string name, string layer), int>();
        private readonly List<Identity> identities = new List<Identity>();
        private readonly List<FrameIndexEntry> frameIndex = new List<FrameIndexEntry>();
        private long blockOffset;
        private int blockFrames;
        private Totals totals;
        public string Sha256 { get; private set; }
        public long BytesWritten => file.BytesWritten;

        public Writer(string path, string header, AsyncSwarmWriter.RowLayout layout)
        {
            file = new HashingFile(path);
            output = new BinaryWriter(new BufferedStream(file, 65536), Encoding.UTF8);
            frame = new FrameWriter(frameBuffer);
            this.layout = layout;
            try
            {
                output.Write(HeaderMagic);
                output.Write((int)layout);
                output.Write(header);
                output.Flush();
                blockOffset = file.BytesWritten;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Write(AsyncSwarmWriter.Frame value)
        {
            frameBuffer.Position = 0;
            frame.Write(0); // Length prefix filled after writing the frame.
            long ticks = value.timestamp.Ticks;
            frame.Write(value.unityFrame);
            frame.Write(ticks);
            frame.Write(value.count);
            frame.Write(value.skyboxId ?? "");
            frame.Write(value.skyboxSampleUtc ?? "");
            frame.Write(value.phase ?? "");
            if (layout == AsyncSwarmWriter.RowLayout.Kannadi)
            {
                frame.Write(value.vrIndex);
                frame.Write(value.numberOfRings);
                frame.Write(value.hexRadius);
            }

            var defaultScale = value.count == 0 ? default : value.agents[0];
            WriteVector(frame, defaultScale.sx, defaultScale.sy, defaultScale.sz);
            for (int i = 0; i < value.count; i++)
            {
                var agent = value.agents[i];
                string layer = agent.layer ?? "";
                var key = (agent.index, agent.name ?? "", layer);
                if (!ids.TryGetValue(key, out int id))
                {
                    id = identities.Count;
                    ids.Add(key, id);
                    identities.Add(new Identity { index = key.index, name = key.Item2, layer = layer });
                }
                frame.Write(id);
                WriteVector(frame, agent.x, agent.y, agent.z);
                WriteVector(frame, agent.rx, agent.ry, agent.rz);
                if (agent.animationSnapshot is AsyncSwarmWriter.IPackedAnimationSnapshot snapshot)
                    snapshot.WritePacked(frame);
                else if (agent.animationSnapshot != null)
                    throw new InvalidDataException("The animation snapshot does not support binary population recording.");
                else if (string.IsNullOrEmpty(agent.animationState))
                    frame.Write((byte)0);
                else
                {
                    frame.Write((byte)3);
                    frame.Write(agent.animationState);
                }

                bool customScale = DifferentBits(agent.sx, defaultScale.sx) || DifferentBits(agent.sy, defaultScale.sy) || DifferentBits(agent.sz, defaultScale.sz);
                frame.Write(customScale);
                if (customScale) WriteVector(frame, agent.sx, agent.sy, agent.sz);
                if (layout == AsyncSwarmWriter.RowLayout.Band)
                {
                    frame.Write(agent.speed);
                    frame.Write(agent.visibilityPhase);
                    frame.Write(agent.visible);
                }
            }
            int frameBytes = checked((int)frameBuffer.Position);
            if (frameBytes > MaxBlockBytes) throw Invalid("A recorded population frame exceeds the block size limit.");
            if (blockFrames == FramesPerBlock || blockFrames > 0 && block.Length + frameBytes > TargetBlockBytes)
                WriteBlock();

            frameBuffer.Position = 0;
            frame.Write(frameBytes - sizeof(int));
            frameIndex.Add(new FrameIndexEntry { blockOffset = blockOffset, offset = (int)block.Position,
                unityFrame = value.unityFrame, count = value.count, ticks = ticks });
            block.Write(frameBuffer.GetBuffer(), 0, frameBytes);
            blockFrames++;
            totals.rows += value.count;
            if (totals.frames++ == 0) totals.firstTicks = totals.lastTicks = ticks;
            else
            {
                totals.firstTicks = Math.Min(totals.firstTicks, ticks);
                totals.lastTicks = Math.Max(totals.lastTicks, ticks);
            }
        }

        public void Finish()
        {
            WriteBlock();
            output.Flush();
            long offset = file.BytesWritten;
            output.Write(identities.Count);
            foreach (var identity in identities)
            {
                output.Write(identity.index);
                output.Write(identity.name);
                output.Write(identity.layer);
            }
            foreach (var entry in frameIndex)
            {
                output.Write(entry.blockOffset);
                output.Write(entry.offset);
                output.Write(entry.unityFrame);
                output.Write(entry.ticks);
                output.Write(entry.count);
            }
            output.Write(FooterMagic);
            output.Write(offset);
            output.Write(totals.frames);
            output.Write(totals.rows);
            output.Write(totals.firstTicks);
            output.Write(totals.lastTicks);
            output.Flush();
            Sha256 = file.FinishHash();
        }

        private void WriteBlock()
        {
            if (blockFrames == 0) return;
            int rawLength = (int)block.Length;
            encodedBlock.SetLength(0);
            using (var compressor = new GZipStream(encodedBlock, CompressionLevel.Fastest, true))
                compressor.Write(block.GetBuffer(), 0, rawLength);

            int storedLength = (int)encodedBlock.Length;
            output.Write(rawLength);
            output.Write(storedLength);
            output.Write(encodedBlock.GetBuffer(), 0, storedLength);
            blockOffset += BlockHeaderBytes + storedLength;
            block.SetLength(0);
            blockFrames = 0;
        }

        public void Dispose()
        {
            output.Dispose();
            frame.Dispose();
            frameBuffer.Dispose();
            block.Dispose();
            encodedBlock.Dispose();
        }

        private static bool DifferentBits(float a, float b) =>
            BitConverter.SingleToInt32Bits(a) != BitConverter.SingleToInt32Bits(b);
    }

    public sealed class Reader : IDisposable
    {
        private readonly BinaryReader input;
        private readonly bool packed;
        private readonly long dataStart, frameEnd, indexStart;
        private BinaryReader blockInput, sampleInput;
        private long loadedBlock = -1;
        private int loadedBlockLength;
        private long sampleEnd, sampleNextOffset;
        private byte[] encoded = Array.Empty<byte>();
        private byte[] decoded = Array.Empty<byte>();

        // A v1 cursor is a byte offset. A v2 cursor is a frame ordinal in the footer.
        public long FirstFrameOffset { get; }
        public AsyncSwarmWriter.RowLayout Layout { get; }
        public string Header { get; }
        public Totals Summary { get; }
        public Identity[] Identities { get; }

        public Reader(string path, bool loadIdentities = true)
        {
            input = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536), Encoding.UTF8);
            try
            {
                ulong magic = input.ReadUInt64();
                if (magic != HeaderMagic && magic != LegacyHeaderMagic) throw Invalid("Invalid header.");
                packed = magic == HeaderMagic;
                int layout = input.ReadInt32();
                if (layout < 0 || layout > (int)AsyncSwarmWriter.RowLayout.Band) throw Invalid("Unknown row layout.");
                Layout = (AsyncSwarmWriter.RowLayout)layout;
                Header = input.ReadString();
                dataStart = input.BaseStream.Position;
                FirstFrameOffset = packed ? 0 : dataStart;
                long footerOffset = input.BaseStream.Length - FooterBytes;
                if (footerOffset < dataStart + sizeof(int)) throw Invalid("Missing footer.");
                input.BaseStream.Position = footerOffset;
                if (input.ReadUInt64() != (packed ? FooterMagic : LegacyFooterMagic))
                    throw Invalid("Missing footer. The population stream may not have finished.");
                frameEnd = input.ReadInt64();
                Summary = new Totals { frames = input.ReadInt64(), rows = input.ReadInt64(),
                    firstTicks = input.ReadInt64(), lastTicks = input.ReadInt64() };
                if (frameEnd < dataStart || frameEnd > footerOffset - sizeof(int) ||
                    Summary.frames < 0 || Summary.rows < 0 || Summary.firstTicks < 0 ||
                    Summary.lastTicks < Summary.firstTicks || Summary.lastTicks > DateTime.MaxValue.Ticks)
                    throw Invalid("Invalid frame totals or identity table offset.");
                if (packed && Summary.frames > (footerOffset - frameEnd - sizeof(int)) / IndexEntryBytes)
                    throw Invalid("Invalid frame directory size.");
                indexStart = packed ? footerOffset - Summary.frames * IndexEntryBytes : footerOffset;
                if (!loadIdentities) return;
                input.BaseStream.Position = frameEnd;
                int count = input.ReadInt32();
                if (count < 0 || count > (indexStart - input.BaseStream.Position) / 6)
                    throw Invalid("Invalid identity count.");
                Identities = new Identity[count];
                for (int i = 0; i < count; i++)
                    Identities[i] = new Identity { index = input.ReadInt32(), name = input.ReadString(), layer = input.ReadString() };
                if (input.BaseStream.Position != indexStart) throw Invalid("Identity table size differs from footer.");
            }
            catch
            {
                input.Dispose();
                throw;
            }
        }

        public bool ReadFrame(long offset, out Frame frame, bool readPayload = false)
        {
            if (packed) return ReadIndexedFrame(offset, out frame, readPayload);
            frame = default;
            if (offset == frameEnd) return false;
            if (offset < dataStart || offset > frameEnd - FrameHeaderBytes) throw Invalid("Invalid frame offset.");
            input.BaseStream.Position = offset;
            frame = ReadFrameRecord(input, frameEnd);
            if (readPayload) ReadPayloadHeader(input, frame.nextOffset, ref frame);
            return true;
        }

        private bool ReadIndexedFrame(long offset, out Frame frame, bool readPayload)
        {
            frame = default;
            if (offset == Summary.frames) return false;
            if (offset < 0 || offset > Summary.frames) throw Invalid("Invalid frame index.");

            input.BaseStream.Position = indexStart + offset * IndexEntryBytes;
            long blockOffset = input.ReadInt64();
            int blockFrameOffset = input.ReadInt32();
            frame.unityFrame = input.ReadInt32();
            frame.ticks = input.ReadInt64();
            frame.count = input.ReadInt32();
            frame.nextOffset = offset + 1;
            if (blockOffset < dataStart || blockOffset > frameEnd - BlockHeaderBytes || blockFrameOffset < 0 ||
                blockFrameOffset > MaxBlockBytes - FrameHeaderBytes ||
                frame.ticks < 0 || frame.ticks > DateTime.MaxValue.Ticks || frame.count < 0 ||
                frame.count > (MaxBlockBytes - MinFramePayloadBytes) / MinSampleBytes)
                throw Invalid("Invalid frame directory entry.");
            if (!readPayload) return true;

            LoadBlock(blockOffset);
            if (blockFrameOffset > loadedBlockLength - FrameHeaderBytes) throw Invalid("Invalid frame offset within block.");
            blockInput.BaseStream.Position = blockFrameOffset;
            Frame stored = ReadFrameRecord(blockInput, loadedBlockLength);
            if (frame.unityFrame != stored.unityFrame || frame.ticks != stored.ticks || frame.count != stored.count)
                throw Invalid("Frame payload does not match the directory.");
            ReadPayloadHeader(blockInput, stored.nextOffset, ref frame);
            return true;
        }

        private void LoadBlock(long offset)
        {
            if (offset == loadedBlock) return;
            loadedBlock = -1;
            input.BaseStream.Position = offset;
            int rawLength = input.ReadInt32();
            int encodedLength = input.ReadInt32();
            if (rawLength < sizeof(int) + MinFramePayloadBytes || rawLength > MaxBlockBytes ||
                encodedLength <= 0 || encodedLength > MaxStoredBlockBytes ||
                encodedLength > frameEnd - input.BaseStream.Position)
                throw Invalid("Invalid population block lengths.");
            if (encoded.Length < encodedLength) encoded = new byte[encodedLength];
            if (decoded.Length < rawLength)
            {
                decoded = new byte[rawLength];
                blockInput?.Dispose();
                blockInput = new BinaryReader(new MemoryStream(decoded, false), Encoding.UTF8);
            }
            ReadExactly(input.BaseStream, encoded, encodedLength);
            try
            {
                using (var buffer = new MemoryStream(encoded, 0, encodedLength, false))
                using (var decompressor = new GZipStream(buffer, CompressionMode.Decompress))
                {
                    ReadExactly(decompressor, decoded, rawLength);
                    if (decompressor.ReadByte() != -1) throw Invalid("Decoded block exceeds its recorded length.");
                }
            }
            // Unity's Mono reports gzip checksum failures as IOException.
            catch (IOException error) { throw new InvalidDataException("MATREX population stream: Invalid compressed block.", error); }
            loadedBlockLength = rawLength;
            loadedBlock = offset;
        }

        private static void ReadExactly(Stream stream, byte[] buffer, int length)
        {
            int position = 0;
            while (position < length)
            {
                int count = stream.Read(buffer, position, length - position);
                if (count == 0) throw Invalid("Truncated population block.");
                position += count;
            }
        }

        private static Frame ReadFrameRecord(BinaryReader source, long maxEnd)
        {
            int size = source.ReadInt32();
            var frame = new Frame { nextOffset = source.BaseStream.Position + size };
            if (size < MinFramePayloadBytes || frame.nextOffset > maxEnd) throw Invalid("Invalid frame size.");
            frame.unityFrame = source.ReadInt32();
            frame.ticks = source.ReadInt64();
            frame.count = source.ReadInt32();
            if (frame.ticks < 0 || frame.ticks > DateTime.MaxValue.Ticks || frame.count < 0 ||
                frame.count > (size - MinFramePayloadBytes) / MinSampleBytes)
                throw Invalid("Invalid population frame timestamp or row count.");
            return frame;
        }

        private void ReadPayloadHeader(BinaryReader source, long end, ref Frame frame)
        {
            frame.skyboxId = source.ReadString();
            frame.skyboxSampleUtc = source.ReadString();
            frame.phase = source.ReadString();
            if (Layout == AsyncSwarmWriter.RowLayout.Kannadi)
            {
                frame.vrIndex = source.ReadInt32();
                frame.numberOfRings = source.ReadInt32();
                frame.hexRadius = source.ReadSingle();
            }
            frame.sx = source.ReadSingle(); frame.sy = source.ReadSingle(); frame.sz = source.ReadSingle();
            if (source.BaseStream.Position > end) throw Invalid("Population header exceeds its frame size.");
            sampleInput = source;
            sampleEnd = end;
            sampleNextOffset = frame.nextOffset;
        }

        public Sample ReadSample(Frame frame)
        {
            var source = sampleInput;
            var result = new Sample { id = source.ReadInt32(), x = source.ReadSingle(), y = source.ReadSingle(), z = source.ReadSingle(),
                rx = source.ReadSingle(), ry = source.ReadSingle(), rz = source.ReadSingle(), animation = ReadAnimation(source) };
            bool customScale = source.ReadBoolean();
            result.sx = customScale ? source.ReadSingle() : frame.sx;
            result.sy = customScale ? source.ReadSingle() : frame.sy;
            result.sz = customScale ? source.ReadSingle() : frame.sz;
            result.visible = true;
            if (Layout == AsyncSwarmWriter.RowLayout.Band)
            {
                result.speed = source.ReadSingle();
                result.visibilityPhase = source.ReadSingle();
                result.visible = source.ReadBoolean();
            }
            if (result.id < 0 || result.id >= Identities.Length || source.BaseStream.Position > sampleEnd)
                throw Invalid("Invalid population sample.");
            return result;
        }

        public void RequireFrameEnd(Frame frame)
        {
            if (sampleInput == null || frame.nextOffset != sampleNextOffset || sampleInput.BaseStream.Position != sampleEnd)
                throw Invalid("Frame samples differ from the stored frame size.");
        }

        public void Dispose()
        {
            blockInput?.Dispose();
            input.Dispose();
        }
    }

    private static void WriteVector(BinaryWriter output, float x, float y, float z)
    {
        output.Write(x); output.Write(y); output.Write(z);
    }

    private static string Number(float number) => number.ToString("R", CultureInfo.InvariantCulture);

    private static string ReadAnimation(BinaryReader input)
    {
        switch (input.ReadByte())
        {
            case 0: return "";
            case 1:
                string speed = Number(input.ReadSingle());
                string state = input.ReadInt32().ToString(CultureInfo.InvariantCulture);
                string time = Number(input.ReadSingle());
                string weight = Number(input.ReadSingle());
                string transitioning = input.ReadBoolean() ? "true" : "false";
                return $"{{\"version\":1,\"animators\":[{{\"path\":\"\",\"speed\":{speed},\"layers\":[{{\"state\":{state},\"time\":{time},\"weight\":{weight},\"transitioning\":{transitioning}}}],\"parameters\":[]}}],\"legacy\":[]}}";
            case 2:
                string clock = Number(input.ReadSingle());
                string phase = Number(input.ReadSingle());
                return $"{{\"version\":1,\"animators\":[],\"legacy\":[],\"procedural\":{{\"time\":{clock},\"phase\":{phase}}}}}";
            case 3: return input.ReadString();
            default: throw Invalid("Unknown animation payload.");
        }
    }

    private static InvalidDataException Invalid(string message) => new InvalidDataException("MATREX population stream: " + message);
}
