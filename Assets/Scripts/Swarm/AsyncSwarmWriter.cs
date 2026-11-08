using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Vector3 = UnityEngine.Vector3;

/// <summary>Bounded, pooled snapshots. Encoding and file I/O run on one worker.</summary>
public sealed class AsyncSwarmWriter
{
    public enum RowLayout { Swarm, Kannadi, Band }
    /// <summary>The rented frame owns this capture until its worker returns the frame to the pool.</summary>
    public interface IAnimationSnapshot { void WriteCsvCell(TextWriter output); }
    public interface IPackedAnimationSnapshot : IAnimationSnapshot { void WritePacked(BinaryWriter output); }
    public struct Agent { public string name, layer, animationState; public IAnimationSnapshot animationSnapshot; public int index; public float x, y, z, rx, ry, rz, sx, sy, sz, speed, visibilityPhase; public bool visible; }

    internal readonly struct StockIdentity
    {
        internal readonly string Name, Layer;
        internal readonly int Index, State;
        internal readonly Vector3 Euler, Scale;

        internal StockIdentity(string name, string layer, int index, int state, Vector3 euler, Vector3 scale)
        {
            Name = name;
            Layer = layer;
            Index = index;
            State = state;
            Euler = euler;
            Scale = scale;
        }
    }

    internal struct StockSample
    {
        internal Vector3 Position;
        internal float Phase, Speed;
    }

    public sealed class Frame
    {
        public Agent[] agents;
        internal StockIdentity[] stockIdentities;
        internal StockSample[] stockSamples;
        internal Action<Frame> prepareForWrite;
        public int count;
        public int unityFrame;
        public DateTime timestamp;
        public string skyboxId, skyboxSampleUtc, phase;
        public int vrIndex, numberOfRings;
        public float hexRadius;
    }
    private const int Capacity = 240;
    private readonly ConcurrentQueue<Frame> pending = new ConcurrentQueue<Frame>();
    private readonly ConcurrentQueue<Frame> pool = new ConcurrentQueue<Frame>();
    private readonly AutoResetEvent signal = new AutoResetEvent(false);
    private readonly object signalGate = new object();
    private bool signalDisposed;
    private readonly Thread worker;
    private volatile bool completing;
    private volatile string failure;
    private int pendingCount, peakPendingCount;
    private long droppedFrames, writtenRows, writtenFrames;
    private static readonly object activeLock = new object();
    private static readonly HashSet<AsyncSwarmWriter> active = new HashSet<AsyncSwarmWriter>();
    public sealed class CompletionSummary
    {
        public string Path, Failure, Sha256;
        public long WrittenRows, WrittenFrames, DroppedFrames, BytesWritten, LastWriteUtcTicks;
        public int PeakPendingFrames;
    }
    // Retain only compact evidence, never the writer/thread/frame pool, across scene reloads.
    private static readonly Dictionary<string, CompletionSummary> completed = new Dictionary<string, CompletionSummary>(StringComparer.Ordinal);
    public int PendingFrames => Volatile.Read(ref pendingCount);
    public long DroppedFrames => Interlocked.Read(ref droppedFrames);
    public long WrittenRows => Interlocked.Read(ref writtenRows);
    public string Failure => failure;
    public static int ActiveWriterCount { get { lock (activeLock) return active.Count; } }

    public static CompletionSummary[] CompletionsForDirectory(string directory, bool remove = false)
    {
        directory = System.IO.Path.GetFullPath(directory);
        lock (activeLock)
        {
            var result = new List<CompletionSummary>();
            foreach (var pair in completed)
                if (System.IO.Path.GetDirectoryName(pair.Key) == directory) result.Add(pair.Value);
            if (remove) foreach (var item in result) completed.Remove(item.Path);
            return result.ToArray();
        }
    }

    public AsyncSwarmWriter(string path, string header, RowLayout layout = RowLayout.Swarm)
    {
        path = System.IO.Path.GetFullPath(path); // Invalid paths fail on the caller, not an unhandled worker finally block.
        lock (activeLock) active.Add(this);
        worker = new Thread(() => Write(path, header, layout)) { IsBackground = true, Name = "Population recording writer" };
        worker.Start();
    }
    public Frame Rent(int count)
    {
        Frame frame = RentFrame();
        EnsureAgents(frame, count);
        frame.count = 0;
        return frame;
    }

    internal Frame RentStock(StockIdentity[] identities, Action<Frame> prepareForWrite)
    {
        Frame frame = RentFrame();
        if (frame.stockSamples == null || frame.stockSamples.Length < identities.Length)
            frame.stockSamples = new StockSample[identities.Length];
        frame.stockIdentities = identities;
        frame.prepareForWrite = prepareForWrite;
        frame.count = identities.Length;
        return frame;
    }

    private Frame RentFrame() => pool.TryDequeue(out Frame frame) ? frame : new Frame();

    private static void EnsureAgents(Frame frame, int count)
    {
        if (frame.agents == null || frame.agents.Length < count) frame.agents = new Agent[count];
    }

    private void ReturnFrame(Frame frame)
    {
        frame.stockIdentities = null;
        frame.prepareForWrite = null;
        pool.Enqueue(frame);
    }

    public bool Submit(Frame frame)
    {
        lock (signalGate)
        {
            if (completing || signalDisposed || failure != null || PendingFrames >= Capacity)
            { Interlocked.Increment(ref droppedFrames); ReturnFrame(frame); return false; }
            int count = Interlocked.Increment(ref pendingCount);
            peakPendingCount = Math.Max(peakPendingCount, count);
            pending.Enqueue(frame);
            signal.Set();
            return true;
        }
    }
    public void Complete()
    {
        lock (signalGate)
        {
            completing = true;
            if (!signalDisposed) signal.Set();
        }
    }
    public static void DrainAll()
    {
        AsyncSwarmWriter[] writers;
        lock (activeLock) { writers = new AsyncSwarmWriter[active.Count]; active.CopyTo(writers); }
        foreach (var writer in writers) writer.Complete();
        foreach (var writer in writers) writer.worker.Join();
    }
    public void WaitForCompletion() { Complete(); worker.Join(); }

    private void Write(string path, string header, RowLayout layout)
    {
        string sha256 = null;
        long bytesWritten = 0, lastWriteUtcTicks = 0;
        try
        {
            if (PopulationRecording.IsFile(path))
            {
                using (var output = new PopulationRecording.Writer(path, header, layout))
                {
                    Drain(output.Write);
                    output.Finish();
                    sha256 = output.Sha256;
                    bytesWritten = output.BytesWritten;
                }
                var sealedFile = new FileInfo(path);
                if (sealedFile.Length != bytesWritten) throw new IOException("Packed population length changed while closing " + path);
                lastWriteUtcTicks = sealedFile.LastWriteTimeUtc.Ticks;
                return;
            }
            bool animation=header.IndexOf(",AnimationState",StringComparison.Ordinal)>=0;
            bool unityFrame=header.IndexOf(",UnityFrame",StringComparison.Ordinal)>=0;
            bool scale=header.IndexOf(",ScaleX",StringComparison.Ordinal)>=0;
            var numberBuffer=new char[64];
            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 65536))
            using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
            using (var output = new StreamWriter(gzip, new System.Text.UTF8Encoding(false), 65536))
            {
                output.WriteLine(header);
                Drain(frame =>
                {
                    string timestamp = frame.timestamp.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture);
                    for (int i = 0; i < frame.count; i++)
                    {
                        Agent a = frame.agents[i];
                        output.Write(timestamp);
                        if(layout==RowLayout.Kannadi)
                        {
                            Number(output,frame.vrIndex,numberBuffer);Number(output,a.index,numberBuffer);Cell(output,a.name);
                            Number(output,a.x,numberBuffer);Number(output,a.y,numberBuffer);Number(output,a.z,numberBuffer);
                            Number(output,a.rx,numberBuffer);Number(output,a.ry,numberBuffer);Number(output,a.rz,numberBuffer);
                            Number(output,frame.numberOfRings,numberBuffer);Number(output,frame.hexRadius,numberBuffer);
                            Cell(output,frame.skyboxId);Cell(output,frame.skyboxSampleUtc);Cell(output,frame.phase);
                        }
                        else
                        {
                            Cell(output,a.name);Cell(output,a.layer);
                            Number(output,a.x,numberBuffer);Number(output,a.y,numberBuffer);Number(output,a.z,numberBuffer);
                            Cell(output,frame.skyboxId);Cell(output,frame.skyboxSampleUtc);Cell(output,frame.phase);
                            Number(output,a.rx,numberBuffer);Number(output,a.ry,numberBuffer);Number(output,a.rz,numberBuffer);
                        }
                        if (animation)
                        {
                            if(a.animationSnapshot!=null)a.animationSnapshot.WriteCsvCell(output);
                            else Cell(output,a.animationState);
                        }
                        if(unityFrame)Number(output,frame.unityFrame,numberBuffer);
                        if(scale){Number(output,a.sx,numberBuffer);Number(output,a.sy,numberBuffer);Number(output,a.sz,numberBuffer);}
                        output.WriteLine();
                    }
                });
            }
        }
        catch (Exception error)
        {
            failure = error.ToString();
            UnityEngine.Debug.LogError("Swarm logging failed: " + failure);
        }
        finally
        {
            lock (signalGate)
            {
                signalDisposed = true;
                signal.Dispose();
                while (pending.TryDequeue(out _))
                {
                    Interlocked.Decrement(ref pendingCount);
                    Interlocked.Increment(ref droppedFrames);
                }
            }
            lock (activeLock)
            {
                string fullPath = System.IO.Path.GetFullPath(path);
                completed[fullPath] = new CompletionSummary { Path = fullPath, Failure = failure,
                    WrittenRows = WrittenRows, WrittenFrames = Interlocked.Read(ref writtenFrames),
                    PeakPendingFrames = peakPendingCount, DroppedFrames = DroppedFrames,
                    Sha256 = failure == null ? sha256 : null, BytesWritten = bytesWritten, LastWriteUtcTicks = lastWriteUtcTicks };
                active.Remove(this);
            }
        }
    }
    private void Drain(Action<Frame> write)
    {
        while (true)
        {
            while (pending.TryDequeue(out Frame frame))
            {
                try
                {
                    frame.prepareForWrite?.Invoke(frame);
                    write(frame);
                    Interlocked.Add(ref writtenRows, frame.count);
                    Interlocked.Increment(ref writtenFrames);
                }
                catch
                {
                    Interlocked.Increment(ref droppedFrames);
                    throw;
                }
                finally
                {
                    Interlocked.Decrement(ref pendingCount);
                    ReturnFrame(frame);
                }
            }
            if (completing && pending.IsEmpty) return;
            signal.WaitOne(100);
        }
    }

    private static void Number(StreamWriter output,float value,char[] buffer)
    {
        output.Write(',');
        if(!value.TryFormat(buffer,out int length,"R",CultureInfo.InvariantCulture))throw new InvalidDataException("Population number exceeds the encoding buffer.");
        output.Write(buffer,0,length);
    }
    private static void Number(StreamWriter output,int value,char[] buffer)
    {
        output.Write(',');
        if(!value.TryFormat(buffer,out int length,default,CultureInfo.InvariantCulture))throw new InvalidDataException("Population integer exceeds the encoding buffer.");
        output.Write(buffer,0,length);
    }
    private static void Cell(StreamWriter output,string value)
    {
        output.Write(',');if(string.IsNullOrEmpty(value))return;
        if(value.IndexOfAny(CsvCharacters)<0){output.Write(value);return;}
        output.Write('"');int start=0;
        for(int i=0;i<value.Length;i++)if(value[i]=='"')
        {output.Write(value.AsSpan(start,i-start));output.Write("\"\"");start=i+1;}
        output.Write(value.AsSpan(start));output.Write('"');
    }
    private static readonly char[] CsvCharacters = { ',', '"', '\r', '\n' };
}
