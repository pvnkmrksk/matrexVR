using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Indexes recorded population frames on disk so a long gzip run can be scrubbed without keeping every agent sample in RAM.</summary>
public sealed class ReplayAgentStream : IDisposable
{
    public struct Pose
    {
        public int id;
        public string name, layer;
        public Vector3 position, rotation;
    }
    private struct Frame { public double time; public long offset; public int count; }
    private struct Identity { public string name,layer; }
    private readonly List<Frame> frames = new List<Frame>();
    private readonly List<Identity> identities = new List<Identity>();
    private readonly List<Pose> current = new List<Pose>();
    private readonly string cachePath;
    private readonly BinaryReader reader;
    private int lastFrame = int.MinValue;
    public string SourceFile { get; }
    public bool HasRotations { get; }
    public string Kind { get; }
    public long RowCount { get; private set; }
    public int FrameCount => frames.Count;
    public bool HasDuplicateNames { get; private set; }

    private ReplayAgentStream(string source,string cacheDirectory,long originTicks,string[] header,StreamReader input)
    {
        SourceFile = source;
        HasRotations = Array.IndexOf(header,"RotationX") >= 0 && Array.IndexOf(header,"RotationY") >= 0 && Array.IndexOf(header,"RotationZ") >= 0;
        Kind = header.Contains("CloneIndex") ? "Kannadi" : header.Contains("VisibilityPhase") ? "Band" : "Swarm";
        Directory.CreateDirectory(cacheDirectory);
        cachePath = Path.Combine(cacheDirectory,Guid.NewGuid().ToString("N")+".frames");
        try
        {
            using (var output = new BinaryWriter(new FileStream(cachePath,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536)))
            {
                var ids = new Dictionary<string,int>(StringComparer.Ordinal);
                var occurrences = new Dictionary<string,int>(StringComparer.Ordinal);
                Frame frame = default; long frameTicks = long.MinValue; string row;
                string xKey = Kind == "Kannadi" ? "PositionX" : "X", yKey = Kind == "Kannadi" ? "PositionY" : "Y", zKey = Kind == "Kannadi" ? "PositionZ" : "Z";
                while ((row=input.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(row)) continue;
                    string[] values=ReplaySessionArchive.Csv(row);
                    string Cell(string key) => ReplaySessionArchive.Cell(header,values,key);
                    if (!ReplaySessionArchive.TryTimestamp(Cell("Timestamp"),out long ticks) ||
                        !ReplaySessionArchive.TryNumber(Cell(xKey),out float x) || !ReplaySessionArchive.TryNumber(Cell(yKey),out float y) || !ReplaySessionArchive.TryNumber(Cell(zKey),out float z))
                        throw new InvalidDataException("Malformed population pose in " + Path.GetFileName(source));
                    if (ticks != frameTicks)
                    {
                        if (frame.count > 0) frames.Add(frame);
                        frameTicks=ticks; occurrences.Clear();
                        frame = new Frame { time=(ticks-originTicks)/(double)TimeSpan.TicksPerSecond, offset=output.BaseStream.Position };
                    }
                    string name=Cell(Kind=="Kannadi"?"CloneName":"Name");
                    string layer=Kind=="Kannadi"?"SimulatedLocustsVR"+Cell("VRIndex"):Cell("Layer");
                    string key=layer+"|"+name;
                    occurrences.TryGetValue(key,out int occurrence); occurrences[key]=occurrence+1;
                    if (occurrence>0) { key+="|duplicate:"+occurrence; HasDuplicateNames=true; }
                    if (!ids.TryGetValue(key,out int id)) {id=identities.Count;ids.Add(key,id);identities.Add(new Identity {name=name,layer=layer});}
                    output.Write(id);output.Write(x);output.Write(y);output.Write(z);
                    if (HasRotations)
                    {
                        foreach(string axis in new[]{"RotationX","RotationY","RotationZ"})
                        {
                            if(!ReplaySessionArchive.TryNumber(Cell(axis),out float angle)) throw new InvalidDataException("Malformed population orientation in "+Path.GetFileName(source));
                            output.Write(angle);
                        }
                    }
                    frame.count++;RowCount++;
                }
                if(frame.count>0)frames.Add(frame);
            }
            frames.Sort((a,b)=>a.time.CompareTo(b.time));
            reader=new BinaryReader(new FileStream(cachePath,FileMode.Open,FileAccess.Read,FileShare.Read,65536));
        }
        catch {if(File.Exists(cachePath))File.Delete(cachePath);throw;}
    }

    public static ReplayAgentStream TryOpen(string source,string cacheDirectory,long originTicks)
    {
        using(var input=ReplaySessionArchive.OpenCsv(source))
        {
            string first=input.ReadLine();if(first==null)return null;
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
        if(index==lastFrame)return current;
        lastFrame=index;current.Clear();
        if(index<0)return current;
        Frame frame=frames[index];reader.BaseStream.Position=frame.offset;
        for(int i=0;i<frame.count;i++)
        {
            int id=reader.ReadInt32();var identity=identities[id];
            var pose=new Pose {id=id,name=identity.name,layer=identity.layer,position=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle())};
            if(HasRotations)pose.rotation=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
            current.Add(pose);
        }
        return current;
    }

    public void Dispose()
    {
        reader?.Dispose();
        if(File.Exists(cachePath))File.Delete(cachePath);
    }
}
