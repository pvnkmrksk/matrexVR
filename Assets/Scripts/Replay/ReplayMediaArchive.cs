using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

/// <summary>Verified, run-local camera media. Capture clocks are never inferred from filenames.</summary>
public sealed class ReplayMediaClip
{
    public string Id { get; internal set; }
    public string RigId { get; internal set; }
    public string RelativePath { get; internal set; }
    public string AbsolutePath { get; internal set; }
    public string Container { get; internal set; }
    public string Codec { get; internal set; }
    public string OriginalCodec { get; internal set; }
    public string FrameMetadataBasis { get; internal set; }
    public long FrameCount { get; internal set; }
    public double NominalFps { get; internal set; }
    public string SyncMode { get; internal set; }
    public bool Complete { get; internal set; }
    public string State { get; internal set; }
    public string SourceClock { get; internal set; }
    public string AssociationLabel { get; internal set; }
    internal double Offset;
    internal readonly List<ReplayMediaAnchor> Anchors=new List<ReplayMediaAnchor>();
}

public readonly struct ReplayMediaFrame
{
    public ReplayMediaClip Clip { get; }
    public long LocalFrame { get; }
    public double MediaSeconds { get; }
    public double AnchorReplaySeconds { get; }
    public double AgeSeconds { get; }
    public string AssociationLabel => Clip?.AssociationLabel;
    internal ReplayMediaFrame(ReplayMediaClip clip,long frame,double media,double anchor,double age)
    {Clip=clip;LocalFrame=frame;MediaSeconds=media;AnchorReplaySeconds=anchor;AgeSeconds=age;}
}

internal readonly struct ReplayMediaAnchor
{
    internal readonly double replay,media;
    internal readonly long frame;
    internal ReplayMediaAnchor(double replay,long frame,double media){this.replay=replay;this.frame=frame;this.media=media;}
}

public sealed class ReplayMediaArchive
{
    private readonly List<ReplayMediaClip> clips=new List<ReplayMediaClip>();
    private readonly List<string> warnings=new List<string>();
    private readonly Dictionary<string,string> videoStatus=new Dictionary<string,string>(StringComparer.Ordinal);
    public IReadOnlyList<ReplayMediaClip> Clips => clips;
    public IReadOnlyList<string> Warnings => warnings;
    public long OriginTicks { get; private set; }
    public static Task<ReplayMediaArchive> LoadAsync(string runDirectory,long originTicks,CancellationToken token=default)
        =>Task.Run(()=>Load(runDirectory,originTicks,token),token);

    private static ReplayMediaArchive Load(string runDirectory,long originTicks,CancellationToken token)
    {
        var result=new ReplayMediaArchive{OriginTicks=originTicks};
        string root=Path.GetFullPath(runDirectory),manifest=Path.Combine(root,"media_manifest.json");
        result.ReadSavedVideoStatus(root);
        if(!File.Exists(manifest))return result;
        if(new FileInfo(manifest).Length>8*1024*1024)throw new InvalidDataException("Camera media manifest is too large.");
        var document=JObject.Parse(File.ReadAllText(manifest));
        foreach(var source in (document["sources"] as JArray??new JArray()).OfType<JObject>())
            if((string)source["state"]!="collected")result.videoStatus[CanonicalRig((string)source["rigId"]) ]="Tracker footage is pending. Close the independent tracker normally, then choose Collect media. If its stop could not be acknowledged, use Import video folder and confirm the source has finished saving.";
        if((int?)document["schemaVersion"]!=1)throw new InvalidDataException("Unsupported camera media manifest version.");
        string clock=(string)document["timeline"]?["clock"];
        if(clock!="recorded-wallclock-ticks" || !long.TryParse((string)document["timeline"]?["originTicks"],NumberStyles.None,CultureInfo.InvariantCulture,out long savedOrigin)||savedOrigin!=originTicks)
            throw new InvalidDataException("Camera media timeline does not match this replay's recorded wall-clock origin.");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var entry in (document["clips"] as JArray??new JArray()).OfType<JObject>())
        {
            token.ThrowIfCancellationRequested();
            var clip=new ReplayMediaClip{Id=(string)entry["id"],RigId=(string)entry["rigId"],RelativePath=(string)entry["path"],Container=(string)entry["container"]??"unknown",Codec=(string)entry["codec"]??"unknown",
                OriginalCodec=(string)entry["originalCodec"]??(string)entry["codec"],FrameMetadataBasis=(string)entry["frameMetadataBasis"]??"unknown",
                FrameCount=(long?)entry["frameCount"]??0,NominalFps=(double?)entry["nominalFps"]??0,Complete=(bool?)entry["complete"]==true,State=(string)entry["state"]??"unavailable",SyncMode=(string)entry["sync"]?["mode"]??"unavailable",
                SourceClock=(string)entry["tracker"]?["sourceClock"],AssociationLabel=(string)entry["sync"]?["association"]??"Unsynchronized camera footage"};
            result.clips.Add(clip);
            try
            {
                if(string.IsNullOrWhiteSpace(clip.Id)||!ids.Add(clip.Id))throw new InvalidDataException("Missing or duplicate camera clip ID.");
                if(!clip.Complete){result.warnings.Add(clip.Id+": camera recording is pending or incomplete.");continue;}
                clip.AbsolutePath=SafePath(root,clip.RelativePath);
                if(!File.Exists(clip.AbsolutePath))throw new FileNotFoundException("Camera clip is missing.");
                long bytes=(long?)entry["byteLength"]??-1;
                string hash=(string)entry["sha256"];
                if(new FileInfo(clip.AbsolutePath).Length!=bytes||string.IsNullOrWhiteSpace(hash)||Hash(clip.AbsolutePath,token)!=hash.ToLowerInvariant())throw new InvalidDataException("Camera clip checksum/length does not match its archive.");
                if(clip.FrameCount<0||!Finite(clip.NominalFps)||clip.NominalFps<0)throw new InvalidDataException("Invalid camera frame metadata.");
                string index=(string)entry["indexPath"];
                if(!string.IsNullOrWhiteSpace(index))
                {
                    string indexPath=SafePath(root,index);
                    if((string)entry["indexSha256"]!=Hash(indexPath,token))throw new InvalidDataException("Camera frame index checksum does not match its archive.");
                    using(var reader=new StreamReader(indexPath))
                    {
                        string line;double previous=double.NegativeInfinity,previousMedia=double.NegativeInfinity;long previousFrame=-1;
                        while((line=reader.ReadLine())!=null)
                        {
                            token.ThrowIfCancellationRequested();if(string.IsNullOrWhiteSpace(line))continue;
                            if(line.Length>32768||clip.Anchors.Count>=4000000)throw new InvalidDataException("Camera frame index exceeds supported bounds.");
                            var row=JObject.Parse(line);double replay=(double?)row["replaySeconds"]??double.NaN,media=(double?)row["mediaSeconds"]??double.NaN;long frame=(long?)row["mediaFrame"]??-1;
                            if(!Finite(replay)||!Finite(media)||replay<0||media<0||replay<previous||media<previousMedia||frame<previousFrame||frame<0||frame>=clip.FrameCount)throw new InvalidDataException("Invalid or unordered camera frame index.");
                            if(replay==previous)
                            {if(frame!=previousFrame||media!=previousMedia)throw new InvalidDataException("Ambiguous camera frames share one replay timestamp.");continue;}
                            clip.Anchors.Add(new ReplayMediaAnchor(replay,frame,media));previous=replay;previousMedia=media;previousFrame=frame;
                        }
                    }
                }
                if(clip.SyncMode=="operator-offset")
                {
                    if(!double.TryParse(entry["sync"]?["manualOffsetSeconds"]?.ToString(Newtonsoft.Json.Formatting.None),NumberStyles.Float,CultureInfo.InvariantCulture,out clip.Offset)||!Finite(clip.Offset))throw new InvalidDataException("An explicit finite manual camera offset is required.");
                    clip.AssociationLabel="Approximate operator offset";
                }
                else if(clip.SyncMode=="consumed-frame-id")
                {
                    if(clip.Anchors.Count==0)throw new InvalidDataException("Consumed-frame association has no verified input anchors.");
                    clip.AssociationLabel="Software consumed-input frame association";
                }
                else if(clip.SyncMode!="unavailable")throw new InvalidDataException("Unknown camera synchronization mode.");
                clip.State="ready";
            }
            catch(Exception error) when(!(error is OperationCanceledException))
            {clip.Complete=false;clip.State="unavailable";clip.Anchors.Clear();result.warnings.Add(clip.Id+": "+error.Message);}
        }
        foreach(string warning in (document["warnings"] as JArray??new JArray()).Values<string>())result.warnings.Add(warning);
        return result;
    }

    private static string CanonicalRig(string rig)=>(rig??"").Trim().ToUpperInvariant();
    private void ReadSavedVideoStatus(string root)
    {
        string path=Path.Combine(root,"companion_provenance.json");
        if(!File.Exists(path))return;
        try
        {
            if(new FileInfo(path).Length>2*1024*1024)return;
            var saved=JObject.Parse(File.ReadAllText(path));
            foreach(var tracker in (saved["trackers"] as JArray??new JArray()).OfType<JObject>())
            {
                string backend=(string)tracker["configuredBackend"]??(string)tracker["backend"]??"external tracker";
                string id=CanonicalRig((string)tracker["vrId"]);
                if(tracker["savedVideoRequested"]?.Type==JTokenType.Boolean)
                    videoStatus[id]=(bool)tracker["savedVideoRequested"]
                        ? "Tracker video was requested, but no completed clip is in this run. Close the tracker normally and choose Collect media, or import its completed video folder."
                        : "Tracker video was switched off for this recording. Enable Save tracker video before a future run and restart an already open tracker. Existing completed footage can be added with Import video folder.";
                else
                    videoStatus[id]=backend+" supplied no archived camera footage. A tracking input stream alone cannot reconstruct camera video. Import an existing completed video folder; legacy footage needs an explicit offset for approximate synchronization.";
            }
        }
        catch(Exception error) when(error is IOException || error is UnauthorizedAccessException || error is Newtonsoft.Json.JsonException)
        { /* Missing provenance must never stop a valid pose or media replay. */ }
    }
    public string MissingVideoStatus(string rigId)
    {
        return videoStatus.TryGetValue(CanonicalRig(rigId),out string status)?status:
            "No tracker footage was archived for this animal. Enable Save tracker video before a future supported bundled-tracker run, or import an existing completed video folder. Tracking data alone does not contain camera pixels.";
    }

    /// <summary>Follow the selected animal and timeline without substituting another animal's camera.</summary>
    public int SelectClip(string rigId,double replaySeconds,int preferred=-1)
    {
        bool Matches(ReplayMediaClip value)=>value.Complete && value.State=="ready" &&
            (string.IsNullOrWhiteSpace(rigId)||CanonicalRig(value.RigId)==CanonicalRig(rigId));
        bool Covers(ReplayMediaClip value)=>value.SyncMode!="unavailable" && TryResolveFrame(value,replaySeconds,out _);
        if(preferred>=0 && preferred<clips.Count && Matches(clips[preferred]) && Covers(clips[preferred]))return preferred;
        for(int i=0;i<clips.Count;i++)if(Matches(clips[i]) && Covers(clips[i]))return i;
        if(preferred>=0 && preferred<clips.Count && Matches(clips[preferred]))return preferred;
        for(int i=0;i<clips.Count;i++)if(Matches(clips[i]))return i;
        return -1;
    }

    /// <summary>Returns the preceding associated input frame; AgeSeconds exposes stale/gap association.</summary>
    public bool TryResolveFrame(ReplayMediaClip clip,double replaySeconds,out ReplayMediaFrame target)
    {
        target=default;
        if(clip==null||!clips.Contains(clip)||!clip.Complete||clip.State!="ready"||!Finite(replaySeconds)||clip.SyncMode=="unavailable")return false;
        if(clip.SyncMode=="operator-offset")
        {
            double media=replaySeconds-clip.Offset;
            if(clip.NominalFps<=0||clip.FrameCount<=0||media<0||media>=clip.FrameCount/clip.NominalFps)return false;
            long frame=(long)Math.Floor(media*clip.NominalFps);
            target=new ReplayMediaFrame(clip,frame,frame/clip.NominalFps,replaySeconds,0);return true;
        }
        var anchors=clip.Anchors;
        if(anchors.Count==0||replaySeconds<anchors[0].replay||replaySeconds>anchors[anchors.Count-1].replay)return false;
        int lo=0,hi=anchors.Count-1;
        while(lo<hi){int mid=lo+(hi-lo+1)/2;if(anchors[mid].replay<=replaySeconds)lo=mid;else hi=mid-1;}
        var anchor=anchors[lo];target=new ReplayMediaFrame(clip,anchor.frame,anchor.media,anchor.replay,replaySeconds-anchor.replay);return true;
    }

    internal static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    internal static string SafePath(string root,string relative)
    {
        if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.IndexOf('\\')>=0||relative.Split('/').Any(p=>p==".."||p=="."))throw new InvalidDataException("Camera paths must stay inside the run folder.");
        string full=Path.GetFullPath(Path.Combine(root,relative));
        if(!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidDataException("Camera path escapes the run folder.");
        string current=full;
        while(current.Length>root.Length){if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Camera archive paths cannot follow symbolic links.");current=Path.GetDirectoryName(current);}
        return full;
    }
    internal static string Hash(string path,CancellationToken token=default)
    {
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create())
        {var buffer=new byte[128*1024];int count;while((count=stream.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();sha.TransformBlock(buffer,0,count,null,0);}sha.TransformFinalBlock(Array.Empty<byte>(),0,0);return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();}
    }
}
