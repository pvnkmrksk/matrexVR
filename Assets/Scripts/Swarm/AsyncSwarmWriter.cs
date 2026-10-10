using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Threading;

/// <summary>Bounded, pooled snapshots. Formatting, gzip and file I/O run on one worker.</summary>
public sealed class AsyncSwarmWriter
{
    public struct Agent { public string name, layer; public float x, y, z; }
    public sealed class Frame
    {
        public Agent[] agents;
        public int count;
        public DateTime timestamp;
        public string skyboxId, skyboxSampleUtc, phase;
    }
    private const int Capacity = 240;
    private readonly ConcurrentQueue<Frame> pending = new ConcurrentQueue<Frame>();
    private readonly ConcurrentQueue<Frame> pool = new ConcurrentQueue<Frame>();
    private readonly AutoResetEvent signal = new AutoResetEvent(false);
    private readonly Thread worker;
    private volatile bool completing;
    private volatile string failure;
    private int pendingCount;
    private long droppedFrames, writtenRows;
    private static readonly object activeLock = new object();
    private static readonly HashSet<AsyncSwarmWriter> active = new HashSet<AsyncSwarmWriter>();
    public int PendingFrames => Volatile.Read(ref pendingCount);
    public long DroppedFrames => Interlocked.Read(ref droppedFrames);
    public long WrittenRows => Interlocked.Read(ref writtenRows);
    public string Failure => failure;

    public AsyncSwarmWriter(string path, string header)
    {
        lock (activeLock) active.Add(this);
        worker = new Thread(() => Write(path, header)) { IsBackground = true, Name = "Swarm CSV writer" };
        worker.Start();
    }
    public Frame Rent(int count)
    {
        if (!pool.TryDequeue(out Frame frame)) frame = new Frame();
        if (frame.agents == null || frame.agents.Length < count) frame.agents = new Agent[count];
        frame.count = 0;
        return frame;
    }
    public bool Submit(Frame frame)
    {
        if (completing || failure != null || PendingFrames >= Capacity)
        { Interlocked.Increment(ref droppedFrames); pool.Enqueue(frame); return false; }
        Interlocked.Increment(ref pendingCount);
        pending.Enqueue(frame); signal.Set(); return true;
    }
    public void Complete() { completing = true; signal.Set(); }
    public static void DrainAll()
    {
        AsyncSwarmWriter[] writers;
        lock (activeLock) { writers = new AsyncSwarmWriter[active.Count]; active.CopyTo(writers); }
        foreach (var writer in writers) writer.Complete();
        foreach (var writer in writers) writer.worker.Join();
    }
    public void WaitForCompletion() { Complete(); worker.Join(); }

    private void Write(string path, string header)
    {
        try
        {
            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 65536))
            using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
            using (var output = new StreamWriter(gzip, new System.Text.UTF8Encoding(false), 65536))
            {
                output.WriteLine(header);
                while (true)
                {
                    while (pending.TryDequeue(out Frame frame))
                    {
                        string timestamp = frame.timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                        for (int i = 0; i < frame.count; i++)
                        {
                            Agent a = frame.agents[i];
                            output.WriteLine(FormattableString.Invariant($"{timestamp},{Csv(a.name)},{Csv(a.layer)},{a.x},{a.y},{a.z},{Csv(frame.skyboxId)},{Csv(frame.skyboxSampleUtc)},{Csv(frame.phase)}"));
                        }
                        Interlocked.Add(ref writtenRows, frame.count);
                        Interlocked.Decrement(ref pendingCount);
                        pool.Enqueue(frame);
                    }
                    if (completing && pending.IsEmpty) break;
                    signal.WaitOne(100);
                }
            }
        }
        catch (Exception error)
        {
            failure = error.ToString();
            UnityEngine.Debug.LogError("Swarm logging failed: " + failure);
        }
        finally
        {
            while (pending.TryDequeue(out _)) { Interlocked.Decrement(ref pendingCount); Interlocked.Increment(ref droppedFrames); }
            lock (activeLock) active.Remove(this);
        }
    }
    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.IndexOfAny(CsvCharacters) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
    private static readonly char[] CsvCharacters = { ',', '"', '\r', '\n' };
}
