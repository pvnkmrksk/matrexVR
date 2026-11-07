using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Indexes population frames on disk for seeking without keeping every agent sample in memory.</summary>
public sealed class ReplayAgentStream : IDisposable
{
    public struct Pose
    {
        public int id;
        public bool visible;
        public string name, layer, animationState;
        public Vector3 position, rotation, scale;
    }
    private struct Frame { public double time; public long offset; public int count, unityFrame; }
    private readonly Dictionary<int,int> frameIndices = new Dictionary<int,int>();
    public bool HasUnityFrames { get; private set; }
    public bool HasScale { get; private set; }
    private struct Identity { public string name,layer; }
    private readonly List<Frame> frames = new List<Frame>();
    private readonly List<Identity> identities = new List<Identity>();
    private readonly List<Pose> current = new List<Pose>();
    private readonly string cachePath;
    private readonly BinaryReader reader;
    private readonly PopulationRecording.Reader packedReader;
    private int lastFrame = int.MinValue;
    public string SourceFile { get; }
    public bool HasRotations { get; }
    public string Kind { get; }
    public bool HasVisibility { get; }
    public bool HasAnimationState { get; }
    public bool UsesMemoryCache { get; }
    public long RowCount { get; private set; }
    public int FrameCount => frames.Count;
    public bool HasDuplicateNames { get; private set; }

    private ReplayAgentStream(string source,string cacheDirectory,long originTicks,string[] header,StreamReader input)
    {
        SourceFile = source;
        HasRotations = Array.IndexOf(header,"RotationX") >= 0 && Array.IndexOf(header,"RotationY") >= 0 && Array.IndexOf(header,"RotationZ") >= 0;
        Kind = header.Contains("CloneIndex") ? "Kannadi" : header.Contains("VisibilityPhase") ? "Band" : "Swarm";
        // Older VisibilityPhase used global Unity time, while actual rendering used
        // a per-agent local timer. It cannot establish historical visibility exactly.
        HasVisibility=header.Contains("Visible");
        HasAnimationState=header.Contains("AnimationState");
        HasUnityFrames=header.Contains("UnityFrame");
        HasScale=header.Contains("ScaleX")&&header.Contains("ScaleY")&&header.Contains("ScaleZ");
        Directory.CreateDirectory(cacheDirectory);
        cachePath = Path.Combine(cacheDirectory,Guid.NewGuid().ToString("N")+".frames");
        try
        {
            using (var output = new BinaryWriter(new FileStream(cachePath,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536)))
            {
                var ids = new Dictionary<string,int>(StringComparer.Ordinal);
                var occurrences = new Dictionary<string,int>(StringComparer.Ordinal);
                Frame frame = default; long frameTicks = long.MinValue; int lastUnityFrame=-1; string row;
                int Column(string name) => Array.IndexOf(header, name);
                bool isKannadi = Kind == "Kannadi";
                int timestampColumn = Column("Timestamp"), frameColumn = Column("UnityFrame");
                int xColumn = Column(isKannadi ? "PositionX" : "X"), yColumn = Column(isKannadi ? "PositionY" : "Y"), zColumn = Column(isKannadi ? "PositionZ" : "Z");
                int nameColumn = Column(isKannadi ? "CloneName" : "Name"), layerColumn = Column(isKannadi ? "VRIndex" : "Layer");
                int visibleColumn = Column("Visible"), animationColumn = Column("AnimationState");
                int[] rotationColumns = { Column("RotationX"), Column("RotationY"), Column("RotationZ") };
                int[] scaleColumns = { Column("ScaleX"), Column("ScaleY"), Column("ScaleZ") };
                // All agents in a recorded frame normally share the exact timestamp text.
                string lastTimestamp = null;
                long ticks = 0;
                while ((row=ReplaySessionArchive.ReadCsvRecord(input)) != null)
                {
                    if (string.IsNullOrWhiteSpace(row)) continue;
                    string[] values=ReplaySessionArchive.Csv(row);
                    string Cell(int index) => index >= 0 && index < values.Length ? values[index] : "";
                    string timestamp = Cell(timestampColumn);
                    if (!string.Equals(timestamp, lastTimestamp, StringComparison.Ordinal))
                    {
                        if (!ReplaySessionArchive.TryTimestamp(timestamp, out ticks))
                            throw new InvalidDataException("Malformed population pose in " + Path.GetFileName(source));
                        lastTimestamp = timestamp;
                    }
                    if (!ReplaySessionArchive.TryNumber(Cell(xColumn),out float x) || !ReplaySessionArchive.TryNumber(Cell(yColumn),out float y) || !ReplaySessionArchive.TryNumber(Cell(zColumn),out float z))
                        throw new InvalidDataException("Malformed population pose in " + Path.GetFileName(source));
                    int unityFrame=ReplaySessionArchive.Integer(Cell(frameColumn),-1);
                    if (ticks != frameTicks || unityFrame != lastUnityFrame)
                    {
                        if (frame.count > 0) frames.Add(frame);
                        frameTicks=ticks; lastUnityFrame=unityFrame; occurrences.Clear();
                        frame = new Frame { time=(ticks-originTicks)/(double)TimeSpan.TicksPerSecond, offset=output.BaseStream.Position, unityFrame=unityFrame };
                    }
                    string name=Cell(nameColumn);
                    string layer=isKannadi?"SimulatedLocustsVR"+Cell(layerColumn):Cell(layerColumn);
                    string key=layer+"|"+name;
                    occurrences.TryGetValue(key,out int occurrence); occurrences[key]=occurrence+1;
                    if (occurrence>0) { key+="|duplicate:"+occurrence; HasDuplicateNames=true; }
                    if (!ids.TryGetValue(key,out int id)) {id=identities.Count;ids.Add(key,id);identities.Add(new Identity {name=name,layer=layer});}
                    output.Write(id);output.Write(x);output.Write(y);output.Write(z);
                    if (HasRotations)
                    {
                        foreach(int axis in rotationColumns)
                        {
                            if(!ReplaySessionArchive.TryNumber(Cell(axis),out float angle)) throw new InvalidDataException("Malformed population orientation in "+Path.GetFileName(source));
                            output.Write(angle);
                        }
                    }
                    bool visible=true;
                    if(HasVisibility)
                    {
                        string flag=Cell(visibleColumn);
                        if(bool.TryParse(flag,out bool boolean))visible=boolean;
                        else if(flag=="1" || flag=="0")visible=flag=="1";
                        else throw new InvalidDataException("Malformed recorded visibility in "+Path.GetFileName(source));
                    }
                    output.Write(visible);
                    if (HasAnimationState) output.Write(Cell(animationColumn) ?? "");
                    if(HasScale)foreach(int axis in scaleColumns)
                    {if(!ReplaySessionArchive.TryNumber(Cell(axis),out float scale))throw new InvalidDataException("Malformed population scale in "+source);output.Write(scale);}
                    frame.count++;RowCount++;
                }
                if(frame.count>0)frames.Add(frame);
            }
            frames.Sort((a,b)=>HasUnityFrames?a.unityFrame.CompareTo(b.unityFrame):a.time.CompareTo(b.time));
            if(HasUnityFrames)for(int i=0;i<frames.Count;i++)frameIndices[frames[i].unityFrame]=i;
            // Small processed runs are cheap to retain in RAM. Large recordings
            // keep the indexed binary stream; source CSVs are always read-only.
            UsesMemoryCache = new FileInfo(cachePath).Length <= 64L * 1024 * 1024;
            reader = UsesMemoryCache ? new BinaryReader(new MemoryStream(File.ReadAllBytes(cachePath), false)) :
                new BinaryReader(new FileStream(cachePath,FileMode.Open,FileAccess.Read,FileShare.Read,65536));
        }
        catch {if(File.Exists(cachePath))File.Delete(cachePath);throw;}
    }

    private ReplayAgentStream(string source, long originTicks)
    {
        SourceFile = source;
        HasRotations = HasAnimationState = HasUnityFrames = HasScale = true;
        packedReader = new PopulationRecording.Reader(source);
        Kind = packedReader.Layout.ToString();
        HasVisibility = packedReader.Layout == AsyncSwarmWriter.RowLayout.Band;
        try
        {
            long offset = packedReader.FirstFrameOffset;
            while (packedReader.ReadFrame(offset, out var frame))
            {
                if (frames.Count != 0 && frame.unityFrame <= frames[frames.Count - 1].unityFrame)
                    throw new InvalidDataException("Population frames are duplicated or out of order in " + source);
                frameIndices.Add(frame.unityFrame, frames.Count);
                frames.Add(new Frame { unityFrame = frame.unityFrame, offset = offset, count = frame.count,
                    time = (frame.ticks - originTicks) / (double)TimeSpan.TicksPerSecond });
                RowCount += frame.count;
                offset = frame.nextOffset;
            }
            if (frames.Count != packedReader.Summary.frames || RowCount != packedReader.Summary.rows)
                throw new InvalidDataException("Population frame or row totals differ from the stored footer in " + source);
        }
        catch
        {
            packedReader.Dispose();
            throw;
        }
    }

    public static ReplayAgentStream TryOpen(string source,string cacheDirectory,long originTicks)
    {
        if (PopulationRecording.IsFile(source)) return new ReplayAgentStream(source, originTicks);
        using(var input=ReplaySessionArchive.OpenCsv(source))
        {
            string first=ReplaySessionArchive.ReadCsvRecord(input);if(first==null)return null;
            string[] header=ReplaySessionArchive.Csv(first.TrimStart('\ufeff'));
            if(!header.Contains("Timestamp") || !(header.Contains("Name") || header.Contains("CloneName")))return null;
            return new ReplayAgentStream(source,cacheDirectory,originTicks,header,input);
        }
    }

    public IReadOnlyList<Pose> Sample(double seconds)
    {
        int index=-1;
        if(frames.Count>0 && seconds>=frames[0].time && seconds<=frames[frames.Count-1].time+.05)
        {
            int lo=0,hi=frames.Count-1;
            while(lo<hi){int mid=(lo+hi+1)/2;if(frames[mid].time<=seconds)lo=mid;else hi=mid-1;}
            index=lo;
        }
        return ReadFrame(index);
    }
    public IReadOnlyList<Pose> SampleUnityFrame(int frame) => ReadFrame(frameIndices.TryGetValue(frame,out var index)?index:-1);
    private IReadOnlyList<Pose> ReadFrame(int index)
    {
        if(index==lastFrame)return current;
        lastFrame=index;current.Clear();
        if(index<0)return current;
        Frame frame=frames[index];
        if (packedReader != null)
        {
            if (!packedReader.ReadFrame(frame.offset, out var stored, true))
                throw new InvalidDataException("Population frame is missing in " + SourceFile);
            for (int i = 0; i < stored.count; i++)
            {
                var sample = packedReader.ReadSample(stored);
                var identity = packedReader.Identities[sample.id];
                current.Add(new Pose { id = sample.id, name = identity.name, layer = identity.layer, visible = sample.visible,
                    position = new Vector3(sample.x, sample.y, sample.z), rotation = new Vector3(sample.rx, sample.ry, sample.rz),
                    scale = new Vector3(sample.sx, sample.sy, sample.sz), animationState = sample.animation });
            }
            packedReader.RequireFrameEnd(stored);
            return current;
        }
        reader.BaseStream.Position=frame.offset;
        for(int i=0;i<frame.count;i++)
        {
            int id=reader.ReadInt32();var identity=identities[id];
            var pose=new Pose {id=id,name=identity.name,layer=identity.layer,position=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle())};
            if(HasRotations)pose.rotation=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
            pose.visible=reader.ReadBoolean();current.Add(pose);
            if (HasAnimationState) pose.animationState=reader.ReadString();
            pose.scale=HasScale?new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()):Vector3.one;
            current[current.Count-1]=pose;
        }
        return current;
    }

    public void Dispose()
    {
        packedReader?.Dispose();
        reader?.Dispose();
        if(File.Exists(cachePath))File.Delete(cachePath);
    }
}
