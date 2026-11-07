using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>Seekable MATREX population frames. Fixed-width values preserve every sampled float.</summary>
public static class PopulationRecording
{
    public const string Extension = ".matpop";
    private const ulong HeaderMagic = 0x3130504f5054414d; // MATPOP01
    private const ulong FooterMagic = 0x3130444e4554414d; // MATEND01
    private const int FooterBytes = 48;

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
        private readonly MemoryStream buffer = new MemoryStream(65536);
        private readonly AsyncSwarmWriter.RowLayout layout;
        private readonly Dictionary<(int index, string name, string layer), int> ids =
            new Dictionary<(int index, string name, string layer), int>();
        private readonly List<Identity> identities = new List<Identity>();
        private Totals totals;
        public string Sha256 { get; private set; }
        public long BytesWritten => file.BytesWritten;

        public Writer(string path, string header, AsyncSwarmWriter.RowLayout layout)
        {
            file = new HashingFile(path);
            output = new BinaryWriter(new BufferedStream(file, 65536), Encoding.UTF8);
            frame = new FrameWriter(buffer);
            this.layout = layout;
            try
            {
                output.Write(HeaderMagic);
                output.Write((int)layout);
                output.Write(header);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Write(AsyncSwarmWriter.Frame value)
        {
            buffer.Position = 0;
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
            int size = checked((int)buffer.Position);
            output.Write(size);
            output.Write(buffer.GetBuffer(), 0, size);
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
            output.Flush();
            long offset = file.BytesWritten;
            output.Write(identities.Count);
            foreach (var identity in identities)
            {
                output.Write(identity.index);
                output.Write(identity.name);
                output.Write(identity.layer);
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

        public void Dispose()
        {
            output.Dispose();
            frame.Dispose();
            buffer.Dispose();
        }

        private static bool DifferentBits(float a, float b) =>
            BitConverter.SingleToInt32Bits(a) != BitConverter.SingleToInt32Bits(b);
    }

    public sealed class Reader : IDisposable
    {
        private readonly BinaryReader input;
        private readonly long frameEnd;
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
                if (input.ReadUInt64() != HeaderMagic) throw Invalid("Invalid header.");
                int layout = input.ReadInt32();
                if (layout < 0 || layout > (int)AsyncSwarmWriter.RowLayout.Band) throw Invalid("Unknown row layout.");
                Layout = (AsyncSwarmWriter.RowLayout)layout;
                Header = input.ReadString();
                FirstFrameOffset = input.BaseStream.Position;
                long footerOffset = input.BaseStream.Length - FooterBytes;
                if (footerOffset < FirstFrameOffset + sizeof(int)) throw Invalid("Missing footer.");
                input.BaseStream.Position = footerOffset;
                if (input.ReadUInt64() != FooterMagic) throw Invalid("Missing footer. The population stream may not have finished.");
                frameEnd = input.ReadInt64();
                Summary = new Totals { frames = input.ReadInt64(), rows = input.ReadInt64(),
                    firstTicks = input.ReadInt64(), lastTicks = input.ReadInt64() };
                if (frameEnd < FirstFrameOffset || frameEnd > footerOffset - sizeof(int) ||
                    Summary.frames < 0 || Summary.rows < 0 || Summary.firstTicks < 0 ||
                    Summary.lastTicks < Summary.firstTicks || Summary.lastTicks > DateTime.MaxValue.Ticks)
                    throw Invalid("Invalid frame totals or identity table offset.");
                if (!loadIdentities) return;
                input.BaseStream.Position = frameEnd;
                int count = input.ReadInt32();
                if (count < 0 || count > (footerOffset - input.BaseStream.Position) / 6)
                    throw Invalid("Invalid identity count.");
                Identities = new Identity[count];
                for (int i = 0; i < count; i++)
                    Identities[i] = new Identity { index = input.ReadInt32(), name = input.ReadString(), layer = input.ReadString() };
                if (input.BaseStream.Position != footerOffset) throw Invalid("Identity table size differs from footer.");
            }
            catch
            {
                input.Dispose();
                throw;
            }
        }

        public bool ReadFrame(long offset, out Frame frame, bool readPayload = false)
        {
            frame = default;
            if (offset == frameEnd) return false;
            if (offset < FirstFrameOffset || offset > frameEnd - 20) throw Invalid("Invalid frame offset.");
            input.BaseStream.Position = offset;
            int size = input.ReadInt32();
            frame.nextOffset = offset + sizeof(int) + size;
            if (size < 31 || frame.nextOffset > frameEnd) throw Invalid("Invalid frame size.");
            frame.unityFrame = input.ReadInt32();
            frame.ticks = input.ReadInt64();
            frame.count = input.ReadInt32();
            if (frame.ticks < 0 || frame.ticks > DateTime.MaxValue.Ticks || frame.count < 0 ||
                frame.count > (size - 31) / 30)
                throw Invalid("Invalid population frame timestamp or row count.");
            if (readPayload)
            {
                frame.skyboxId = input.ReadString();
                frame.skyboxSampleUtc = input.ReadString();
                frame.phase = input.ReadString();
                if (Layout == AsyncSwarmWriter.RowLayout.Kannadi)
                {
                    frame.vrIndex = input.ReadInt32();
                    frame.numberOfRings = input.ReadInt32();
                    frame.hexRadius = input.ReadSingle();
                }
                frame.sx = input.ReadSingle(); frame.sy = input.ReadSingle(); frame.sz = input.ReadSingle();
            }
            return true;
        }

        public Sample ReadSample(Frame frame)
        {
            var result = new Sample { id = input.ReadInt32(), x = input.ReadSingle(), y = input.ReadSingle(), z = input.ReadSingle(),
                rx = input.ReadSingle(), ry = input.ReadSingle(), rz = input.ReadSingle(), animation = ReadAnimation(input) };
            bool customScale = input.ReadBoolean();
            result.sx = customScale ? input.ReadSingle() : frame.sx;
            result.sy = customScale ? input.ReadSingle() : frame.sy;
            result.sz = customScale ? input.ReadSingle() : frame.sz;
            result.visible = true;
            if (Layout == AsyncSwarmWriter.RowLayout.Band)
            {
                result.speed = input.ReadSingle();
                result.visibilityPhase = input.ReadSingle();
                result.visible = input.ReadBoolean();
            }
            if (result.id < 0 || result.id >= Identities.Length || input.BaseStream.Position > frame.nextOffset)
                throw Invalid("Invalid population sample.");
            return result;
        }

        public void RequireFrameEnd(Frame frame)
        {
            if (input.BaseStream.Position != frame.nextOffset) throw Invalid("Frame samples differ from the stored frame size.");
        }

        public void Dispose() => input.Dispose();
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
