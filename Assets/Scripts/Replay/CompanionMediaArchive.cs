using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public sealed class MediaRecordingSource
{
    public string RigId,Backend,DirectoryPath,SourceRevision,ExecutableSha256,TrackerSessionId,SourceClock;
    public string ChildDirectoryPrefix;
    public long NotBeforeUtcTicks;
    // Only an acknowledged stopped recorder or an explicitly closed imported folder may set this.
    public bool Finalized;
    public string State="pending";
}
public sealed class MediaImportRequest
{
    public string RunDirectory,SourceDirectory,RigId,ExpectedBackend;
    public string SyncMode="unavailable";
    public double? ManualOffsetSeconds;
    public bool SourceClosed;
    public long ReplayOriginTicks;
    internal List<MediaPoseAnchor> Anchors;
    internal MediaRecordingSource Source;
}
public sealed class MediaArchiveReceipt
{
    public bool Complete { get; internal set; }
    public bool Pending { get; internal set; }
    public int ImportedClips { get; internal set; }
    public readonly List<string> Warnings=new List<string>();
    public JObject ToJson()=>new JObject{["complete"]=Complete,["pending"]=Pending,["importedClips"]=ImportedClips,["warnings"]=new JArray(Warnings)};
}
internal sealed class MediaPoseAnchor
{
    internal string rig,frame,timestamp,clock,session,receiver;
    internal long ticks;
    internal int unityFrame,epoch;
}

/// <summary>Tracks requested detached recorder sources without owning or terminating their processes.</summary>
public static class CompanionMediaArchive
{
    private static readonly object gate=new object();
    private static readonly Dictionary<string,MediaRecordingSource> launched=new Dictionary<string,MediaRecordingSource>(StringComparer.Ordinal);
    private static readonly Dictionary<string,MediaRecordingSource> ownedSessions=new Dictionary<string,MediaRecordingSource>(StringComparer.Ordinal);
    private static string receiptRoot;
    public static void ConfigureReceiptRoot(string root){lock(gate)receiptRoot=Path.GetFullPath(root);}
    public static void RegisterLaunchedSource(MediaRecordingSource source)
    {
        if(source==null||string.IsNullOrWhiteSpace(source.RigId))return;
        lock(gate)
        {
            // Existing run sessions retain this same source instance through idempotent Start.
            if(launched.TryGetValue(source.RigId,out var existing)&&existing.TrackerSessionId==source.TrackerSessionId)return;
            launched[source.RigId]=source;
            if(!string.IsNullOrEmpty(source.TrackerSessionId))ownedSessions[source.TrackerSessionId]=source;
        }
    }
    public static void ClearLaunchedSource(string rigId){lock(gate)launched.Remove(rigId);}
    public static void AcknowledgeStopped(string rigId,string sessionId=null)
    {
        lock(gate)
        {
            MediaRecordingSource source;
            if(sessionId!=null){if(!ownedSessions.TryGetValue(sessionId,out source)||source.RigId!=rigId)return;}
            else if(!launched.TryGetValue(rigId,out source))return;
            source.Finalized=true;source.State="closed-after-graceful-exit";
            if(receiptRoot!=null)
            {
                try{Directory.CreateDirectory(receiptRoot);var evidence=JObject.FromObject(source);evidence["exitCode"]=0;evidence["closedUtc"]=DateTimeOffset.UtcNow;File.WriteAllText(MarkerPath(source.TrackerSessionId),evidence.ToString(Formatting.Indented));}
                catch(Exception error) when(error is IOException||error is UnauthorizedAccessException){source.State="closed-marker-not-persisted";}
            }
        }
    }
    private static string MarkerPath(string sessionId)
    {
        using(var sha=System.Security.Cryptography.SHA256.Create())return Path.Combine(receiptRoot,"media-closed-"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sessionId??""))).Replace("-","").ToLowerInvariant()+".json");
    }
    internal static MediaRecordingSource ResolveOwnedSource(string rigId,string sessionId)
    {
        lock(gate)
        {
            if(sessionId!=null&&ownedSessions.TryGetValue(sessionId,out var source)&&source.RigId==rigId)return source;
            if(receiptRoot==null)return null;
            string marker=MarkerPath(sessionId);
            if(!File.Exists(marker)||new FileInfo(marker).Length>65536)return null;
            var evidence=JObject.Parse(File.ReadAllText(marker));
            if((int?)evidence["exitCode"]!=0||(string)evidence["RigId"]!=rigId||(string)evidence["TrackerSessionId"]!=sessionId)return null;
            return evidence.ToObject<MediaRecordingSource>();
        }
    }
    public static CompanionMediaArchiveSession BeginSession(string runDirectory,string runId)
    {
        var result=new CompanionMediaArchiveSession(runDirectory,runId,false);
        lock(gate)foreach(var source in launched.Values)result.RegisterSource(source);
        return result;
    }
}

public sealed class CompanionMediaArchiveSession
{
    private readonly string directory,runId;
    private readonly List<MediaRecordingSource> sources=new List<MediaRecordingSource>();
    private readonly List<MediaPoseAnchor> anchors=new List<MediaPoseAnchor>();
    private readonly List<string> warnings=new List<string>();
    private readonly bool collectWithoutSources;
    private long origin=long.MaxValue;
    private Task<MediaArchiveReceipt> finalization;
    private const int MaximumAnchors=2000000;
    internal CompanionMediaArchiveSession(string directory,string runId,bool collectWithoutSources=true){this.directory=Path.GetFullPath(directory);this.runId=runId;this.collectWithoutSources=collectWithoutSources;}
    public void RegisterSource(MediaRecordingSource source){if(source!=null&&!sources.Contains(source))sources.Add(source);}
    public void RetainRigs(IEnumerable<string> configuredRigIds)
    {
        if(finalization!=null)throw new InvalidOperationException("Camera sources cannot change after finalization starts.");
        var ids=new HashSet<string>((configuredRigIds??Array.Empty<string>()).Select(ReplaySessionArchive.CanonicalRig),StringComparer.Ordinal);
        sources.RemoveAll(source=>!ids.Contains(ReplaySessionArchive.CanonicalRig(source.RigId)));
    }
    public void SetReplayOrigin(long originTicks){origin=originTicks;}
    /// <summary>Called by the shared playable-pose scanner, after its saved-evidence/finite-pose checks.</summary>
    public void ObservePose(string file,string[] header,string[] cells)
    {
        string Cell(string key)=>ReplaySessionArchive.Cell(header,cells,key);
        if(!ReplaySessionArchive.TryTimestamp(Cell("Current Time"),out long ticks))return;
        origin=Math.Min(origin,ticks);
        if(sources.Count==0&&!collectWithoutSources)return;
        string frame=Cell("trackerInputFrame");
        if(!ulong.TryParse(frame,NumberStyles.None,CultureInfo.InvariantCulture,out _)||!string.Equals(Cell("trackerAppliedThisFrame"),"true",StringComparison.OrdinalIgnoreCase))return;
        if(anchors.Count>=MaximumAnchors){if(warnings.Count==0)warnings.Add("Input-frame anchor limit reached; some camera associations are unavailable.");return;}
        // The text timestamp and poseWallTicks must describe one and the same captured DateTime.
        if(!long.TryParse(Cell("poseWallTicks"),NumberStyles.None,CultureInfo.InvariantCulture,out long exact)||exact!=ticks)return;
        anchors.Add(new MediaPoseAnchor{ticks=ticks,rig=ReplaySessionArchive.CanonicalRig(Cell("VR")),frame=frame,timestamp=Cell("trackerInputTimestamp"),clock=Cell("trackerInputClock"),
            session=Cell("trackerInputSessionId"),receiver=Cell("trackerReceiverSessionId"),epoch=ReplaySessionArchive.Integer(Cell("trackerInputEpoch")),unityFrame=ReplaySessionArchive.Integer(Cell("unityFrame"),-1)});
    }
    public MediaArchiveReceipt PendingReceipt(string reason)
    {var receipt=new MediaArchiveReceipt{Complete=sources.Count==0,Pending=sources.Count>0};if(sources.Count>0)receipt.Warnings.Add(reason);return receipt;}
    public Task<MediaArchiveReceipt> FinalizeAsync(CancellationToken token=default)
    {
        if(finalization!=null)return finalization;
        if(sources.Count==0)return finalization=Task.FromResult(new MediaArchiveReceipt{Complete=true});
        return finalization=Task.Run(async()=>
        {
            var receipt=new MediaArchiveReceipt{Complete=true};receipt.Warnings.AddRange(warnings);
            foreach(var source in sources)
            {
                token.ThrowIfCancellationRequested();
                if(!source.Finalized)
                {
                    receipt.Complete=false;receipt.Pending=true;receipt.Warnings.Add(MediaArchive.PendingRecorderWarning(source.RigId));
                    continue;
                }
                string[] directories=source.ChildDirectoryPrefix==null?new[]{source.DirectoryPath}:Directory.Exists(source.DirectoryPath)?Directory.GetDirectories(source.DirectoryPath)
                    .Where(path=>Path.GetFileName(path).StartsWith(source.ChildDirectoryPrefix,StringComparison.Ordinal)&&Directory.GetCreationTimeUtc(path).Ticks>=source.NotBeforeUtcTicks-TimeSpan.TicksPerSecond*2).ToArray():Array.Empty<string>();
                if(directories.Length==0){receipt.Complete=false;receipt.Warnings.Add(source.RigId+": no finalized native recording directory was found.");}
                foreach(string recording in directories)
                {
                    var imported=await MediaArchive.ImportAsync(new MediaImportRequest{RunDirectory=directory,SourceDirectory=recording,RigId=source.RigId,ExpectedBackend=source.Backend,
                        SourceClosed=true,SyncMode="consumed-frame-id",ReplayOriginTicks=origin==long.MaxValue?0:origin,Anchors=anchors,Source=source},token).ConfigureAwait(false);
                    receipt.Complete&=imported.Complete;receipt.Pending|=imported.Pending;receipt.ImportedClips+=imported.ImportedClips;receipt.Warnings.AddRange(imported.Warnings);
                    if(imported.Complete)source.State="collected";
                }
            }
            if(receipt.Pending)MediaArchive.WritePending(directory,runId,origin==long.MaxValue?0:origin,sources,receipt.Warnings);
            return receipt;
        },token);
    }
    internal List<MediaPoseAnchor> SnapshotAnchors()=>anchors;
    internal long Origin=>origin==long.MaxValue?0:origin;
}

/// <summary>Copies explicitly closed media; mutable native recordings are never silently snapshotted as complete.</summary>
public static class MediaArchive
{
    private static readonly object manifestGate=new object();
    private static readonly HashSet<string> allowed=new HashSet<string>(new[]{".avi",".mp4",".webm",".jsonl",".dat",".csv",".txt",".toml",".png",".json"},StringComparer.OrdinalIgnoreCase);
    public static Task<MediaArchiveReceipt> ImportAsync(MediaImportRequest request,CancellationToken token=default)
        =>Task.Run(()=>Import(request,token),token);
    public static Task<MediaArchiveReceipt> SetOperatorOffsetAsync(string runDirectory,string clipId,double offset,CancellationToken token=default)
        =>SetOperatorOffsetCore(runDirectory,clipId,offset,null,null,token);
    public static Task<MediaArchiveReceipt> SetOperatorOffsetAsync(string runDirectory,string clipId,double offset,long frameCount,double nominalFps,CancellationToken token=default)
        =>SetOperatorOffsetCore(runDirectory,clipId,offset,frameCount,nominalFps,token);
    private static Task<MediaArchiveReceipt> SetOperatorOffsetCore(string runDirectory,string clipId,double offset,long? frameCount,double? nominalFps,CancellationToken token)
        =>Task.Run(()=>
        {
            if(!ReplayMediaArchive.Finite(offset))throw new ArgumentException("Camera offset must be finite.");
            if(frameCount.HasValue&&(frameCount<=0||!nominalFps.HasValue||nominalFps<=0||!ReplayMediaArchive.Finite(nominalFps.Value)))throw new ArgumentException("Decoder frame count/rate must be explicitly positive and finite.");
            string run=Path.GetFullPath(runDirectory);token.ThrowIfCancellationRequested();
            lock(manifestGate)
            {
                var document=JObject.Parse(File.ReadAllText(Path.Combine(run,"media_manifest.json")));
                var matches=(document["clips"] as JArray??new JArray()).OfType<JObject>().Where(c=>(string)c["id"]==clipId).ToArray();
                if(matches.Length!=1)throw new InvalidDataException("Select one uniquely identified camera clip.");
                var clip=matches[0];ReplayMediaArchive.SafePath(run,(string)clip["path"]);
                if((bool?)clip["complete"]!=true)throw new InvalidDataException("Pending footage cannot be linked as a completed clip.");
                if(frameCount.HasValue){clip["frameCount"]=frameCount.Value;clip["nominalFps"]=nominalFps.Value;clip["frameMetadataBasis"]="native-decoder-prepared";}
                double rate=(double?)clip["nominalFps"]??0;
                if(((long?)clip["frameCount"]??0)<=0||rate<=0||!ReplayMediaArchive.Finite(rate))throw new InvalidDataException("An explicit encoded frame count and frame rate are required; no default FPS is guessed.");
                clip["sync"]=new JObject{["mode"]="operator-offset",["manualOffsetSeconds"]=offset,["association"]="Approximate operator offset",["mediaTimeBasis"]=frameCount.HasValue?"native-decoder-frame-rate":"encoded-frame-rate",["modifiedUtc"]=DateTimeOffset.UtcNow};
                token.ThrowIfCancellationRequested();WriteManifest(run,document);
                var receipt=new MediaArchiveReceipt{Complete=true};WarnExistingZip(run,receipt);return receipt;
            }
        },token);
    internal static string PendingRecorderWarning(string rigId)=>rigId+": independent camera recorder has no acknowledged stop; footage remains pending at its original location.";
    public static async Task<MediaArchiveReceipt> CollectPendingAsync(string runDirectory,CancellationToken token=default)
    {
        string run=Path.GetFullPath(runDirectory),path=Path.Combine(run,"media_manifest.json");
        if(!File.Exists(path))return new MediaArchiveReceipt{Complete=true};
        var manifest=JObject.Parse(await Task.Run(()=>File.ReadAllText(path),token).ConfigureAwait(false));
        var session=new CompanionMediaArchiveSession(run,Path.GetFileName(run));
        foreach(var source in (manifest["sources"] as JArray??new JArray()).OfType<JObject>())
        {
            if((string)source["state"]=="collected")continue;
            string rig=(string)source["rigId"],identity=(string)source["trackerSessionId"];
            var owned=CompanionMediaArchive.ResolveOwnedSource(rig,identity);
            session.RegisterSource(owned??new MediaRecordingSource{RigId=rig,TrackerSessionId=identity,State="unverified-pending",DirectoryPath=(string)source["sourceDirectory"]});
        }
        await Task.Run(()=>RecordingSummary.Scan(run,false,token,session.ObservePose),token).ConfigureAwait(false);
        var receipt=await session.FinalizeAsync(token).ConfigureAwait(false);
        if(receipt.Complete)
        {
            lock(manifestGate)
            {
                var current=JObject.Parse(File.ReadAllText(path));
                var resolved=new HashSet<string>(StringComparer.Ordinal);
                foreach(var source in (current["sources"] as JArray??new JArray()).OfType<JObject>())
                {source["state"]="collected";resolved.Add(PendingRecorderWarning((string)source["rigId"]));}
                // This exact status is resolved only after every pending source was collected.
                // Other evidence, including the preserved older ZIP snapshot, remains visible.
                current["warnings"]=new JArray((current["warnings"] as JArray??new JArray()).Where(w=>w.Type!=JTokenType.String||!resolved.Contains((string)w)));
                WriteManifest(run,current);
            }
        }
        return receipt;
    }

    private static MediaArchiveReceipt Import(MediaImportRequest request,CancellationToken token)
    {
        if(request==null)throw new ArgumentNullException(nameof(request));
        if(request.SyncMode!="consumed-frame-id"&&request.SyncMode!="operator-offset"&&request.SyncMode!="unavailable")throw new ArgumentException("Unknown camera synchronization mode.");
        if(request.SyncMode=="operator-offset"&&(!request.ManualOffsetSeconds.HasValue||!ReplayMediaArchive.Finite(request.ManualOffsetSeconds.Value)))throw new ArgumentException("An explicit finite manual offset is required.");
        string run=Path.GetFullPath(request.RunDirectory),source=Path.GetFullPath(request.SourceDirectory);
        if(!Directory.Exists(run)||!Directory.Exists(source))throw new DirectoryNotFoundException("The run and camera recording folders must both exist.");
        if(string.Equals(source,run,StringComparison.OrdinalIgnoreCase)||source.StartsWith(run+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||run.StartsWith(source+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Camera source and run folders must be separate; neither may contain the other.");
        var receipt=new MediaArchiveReceipt{Complete=request.SourceClosed,Pending=!request.SourceClosed};
        if(!request.SourceClosed){receipt.Warnings.Add("Camera source is still open or not confirmed closed; no files were copied.");return receipt;}
        WarnExistingZip(run,receipt);
        var inputFiles=EnumerateSafe(source).Take(50001).ToArray();
        if(inputFiles.Length>50000)throw new InvalidDataException("Camera recording contains too many files.");
        if(inputFiles.Any(path=>path.EndsWith(".partial",StringComparison.OrdinalIgnoreCase))){receipt.Complete=false;receipt.Pending=true;receipt.Warnings.Add("Native .partial media exists; close/finalize the recorder before import.");return receipt;}
        var anchors=request.Anchors;
        long origin=request.ReplayOriginTicks;
        if(anchors==null)
        {
            var observer=new CompanionMediaArchiveSession(run,Path.GetFileName(run));
            RecordingSummary.Scan(run,false,token,observer.ObservePose);anchors=observer.SnapshotAnchors();
            if(origin==0)origin=observer.Origin;
        }
        if(origin<=0)throw new InvalidDataException("The run has no valid recorded replay origin.");
        string rig=SafeName(request.RigId),session=DateTime.UtcNow.ToString("yyyyMMddTHHmmss",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
        string relative="media/"+rig+"/"+session,destination=ReplayMediaArchive.SafePath(run,relative),temporary=destination+".importing";
        Directory.CreateDirectory(temporary);
        try
        {
            var artifacts=new JArray();var copies=new List<string>();
            foreach(string input in inputFiles)
            {
                token.ThrowIfCancellationRequested();if(!allowed.Contains(Path.GetExtension(input)))continue;
                if(copies.Count>=50000)throw new InvalidDataException("Camera recording contains too many files.");
                string child=input.Substring(source.TrimEnd(Path.DirectorySeparatorChar).Length+1).Replace(Path.DirectorySeparatorChar,'/');
                string target=ReplayMediaArchive.SafePath(temporary,child);Directory.CreateDirectory(Path.GetDirectoryName(target));
                var before=new FileInfo(input);long bytes=before.Length,write=before.LastWriteTimeUtc.Ticks;
                using(var from=new FileStream(input,FileMode.Open,FileAccess.Read,FileShare.Read))using(var to=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {var buffer=new byte[128*1024];int count;while((count=from.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();to.Write(buffer,0,count);}}
                var after=new FileInfo(input);
                if(after.Length!=bytes||after.LastWriteTimeUtc.Ticks!=write)throw new IOException("Camera source changed during import; no completed archive was published.");
                string hash=ReplayMediaArchive.Hash(target,token);
                if(hash!=ReplayMediaArchive.Hash(input,token))throw new IOException("Camera source changed while being verified.");
                artifacts.Add(new JObject{["path"]=relative+"/"+child,["byteLength"]=bytes,["sha256"]=hash});copies.Add(child);
            }
            var clips=new JArray();
            foreach(string child in copies.Where(IsVideo))
            {
                string full=ReplayMediaArchive.SafePath(temporary,child);var info=ReadVideoInfo(full,token);
                string backend=request.ExpectedBackend??(copies.Any(p=>Path.GetFileName(p)=="frames.jsonl")?"Rekkefly":"unknown");
                var clip=new JObject{["id"]=rig+"-"+session+"-"+clips.Count,["rigId"]=request.RigId,["path"]=relative+"/"+child,["container"]=Path.GetExtension(child).TrimStart('.').ToLowerInvariant(),["codec"]=info.codec,["originalCodec"]=info.originalCodec,["frameMetadataBasis"]=info.verified?"validated-intra-frame-index":info.frames>0?"container-header-only":"unknown",
                    ["frameCount"]=info.frames,["nominalFps"]=info.fps,["complete"]=true,["state"]="ready",["byteLength"]=new FileInfo(full).Length,["sha256"]=ReplayMediaArchive.Hash(full,token),
                    ["tracker"]=new JObject{["backend"]=backend,["sourceRevision"]=request.Source?.SourceRevision,["executableSha256"]=request.Source?.ExecutableSha256,["sessionId"]=request.Source?.TrackerSessionId,["sourceClock"]=request.Source?.SourceClock},
                    ["sync"]=new JObject{["mode"]="unavailable",["association"]="Unsynchronized camera footage",["mediaTimeBasis"]=info.fps>0?"encoded-frame-rate":"unknown"}};
                var mapped=request.SyncMode=="consumed-frame-id"?MapFrames(temporary,child,request.RigId,anchors,origin,info,token):new List<JObject>();
                if(mapped.Count>0)
                {
                    string index=child+".index.jsonl",indexFull=ReplayMediaArchive.SafePath(temporary,index);
                    using(var writer=new StreamWriter(indexFull,false,new UTF8Encoding(false)))foreach(var row in mapped)writer.WriteLine(row.ToString(Formatting.None));
                    clip["indexPath"]=relative+"/"+index;clip["indexSha256"]=ReplayMediaArchive.Hash(indexFull,token);
                    clip["sync"]["mode"]="consumed-frame-id";clip["sync"]["association"]="software-consumed-input";
                    clip["tracker"]["sourceClock"]=(string)mapped[0]["sourceClock"];
                }
                else if(request.SyncMode=="operator-offset")
                {clip["sync"]["mode"]="operator-offset";clip["sync"]["manualOffsetSeconds"]=request.ManualOffsetSeconds;clip["sync"]["association"]="Approximate operator offset";}
                else if(request.SyncMode=="consumed-frame-id")receipt.Warnings.Add(Path.GetFileName(child)+": no unambiguous consumed-input frame mapping; clip remains unsynchronized.");
                clips.Add(clip);
            }
            if(clips.Count==0)receipt.Warnings.Add("No supported camera video files were found; native logs/configuration were archived.");
            token.ThrowIfCancellationRequested();Directory.CreateDirectory(Path.GetDirectoryName(destination));Directory.Move(temporary,destination);
            lock(manifestGate)
            {
                var manifest=ReadManifest(run,origin);var existing=(JArray)manifest["clips"];foreach(var clip in clips)existing.Add(clip);
                ((JArray)manifest["artifacts"]).Merge(artifacts);((JArray)manifest["warnings"]).Merge(new JArray(receipt.Warnings));WriteManifest(run,manifest);
            }
            receipt.ImportedClips=clips.Count;return receipt;
        }
        catch{if(Directory.Exists(temporary))Directory.Delete(temporary,true);throw;}
    }

    private static IEnumerable<string> EnumerateSafe(string root)
    {
        var pending=new Stack<string>();pending.Push(root);int visited=0;
        while(pending.Count>0)
        {
            string directory=pending.Pop();
            if(++visited>50000)throw new InvalidDataException("Camera recording contains too many folders.");
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Camera imports cannot follow symbolic links.");
            foreach(string file in Directory.GetFiles(directory)){if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Camera imports cannot follow symbolic links.");yield return file;}
            foreach(string child in Directory.GetDirectories(directory))pending.Push(child);
        }
    }
    private static void WarnExistingZip(string run,MediaArchiveReceipt receipt)
    {
        if(File.Exists(run.TrimEnd(Path.DirectorySeparatorChar)+".zip"))receipt.Warnings.Add("The run folder now contains updated camera metadata/media. Its existing ZIP is an older snapshot and was preserved; it does not include these updates.");
    }
    private static bool IsVideo(string path)=>new[]{".avi",".mp4",".webm"}.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase);
    private static string SafeName(string value)=>new string((value??"unknown").Select(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'?c:'_').ToArray());
    private sealed class VideoInfo{internal long frames;internal double fps;internal string codec="unknown",originalCodec="unknown";internal bool verified;}
    private static VideoInfo ReadVideoInfo(string path,CancellationToken token)
    {
        var info=new VideoInfo();if(!path.EndsWith(".avi",StringComparison.OrdinalIgnoreCase))return info;
        try
        {
            using(var native=ReplayAviFrameSource.Open(path,token))return new VideoInfo{frames=native.FrameCount,fps=native.NominalFramesPerSecond,codec=native.Codec.Equals("MJPG",StringComparison.OrdinalIgnoreCase)?"mjpeg":native.Codec.ToLowerInvariant(),originalCodec=native.Codec,verified=true};
        }
        catch(InvalidDataException) { /* Other codecs remain importable for the platform VideoPlayer, with no verified frame join. */ }
        using(var reader=new BinaryReader(File.OpenRead(path)))
        {
            if(reader.BaseStream.Length<12||new string(reader.ReadChars(4))!="RIFF")return info;reader.ReadUInt32();if(new string(reader.ReadChars(4))!="AVI ")return info;
            ReadChunks(reader,Math.Min(reader.BaseStream.Length,4*1024*1024),info,0);
        }
        return info;
    }
    private static void ReadChunks(BinaryReader reader,long end,VideoInfo info,int depth)
    {
        if(depth>8)return;
        while(reader.BaseStream.Position+8<=end)
        {
            string id=new string(reader.ReadChars(4));uint size=reader.ReadUInt32();long start=reader.BaseStream.Position,next=start+size+(size&1);
            if(next>end||next<start)break;
            if(id=="LIST"&&size>=4){string kind=new string(reader.ReadChars(4));if(kind=="movi")return;ReadChunks(reader,start+size,info,depth+1);}
            else if(id=="avih"&&size>=40){uint micros=reader.ReadUInt32();reader.BaseStream.Position=start+16;info.frames=reader.ReadUInt32();if(micros>0)info.fps=1000000.0/micros;}
            else if(id=="strh"&&size>=36){string type=new string(reader.ReadChars(4)),codec=new string(reader.ReadChars(4));if(type=="vids"){reader.BaseStream.Position=start+20;uint scale=reader.ReadUInt32(),rate=reader.ReadUInt32();reader.ReadUInt32();info.frames=reader.ReadUInt32();if(scale>0)info.fps=rate/(double)scale;info.originalCodec=codec.Trim();info.codec=codec.Trim().Equals("MJPG",StringComparison.OrdinalIgnoreCase)?"mjpeg":codec.Trim().ToLowerInvariant();}}
            reader.BaseStream.Position=next;
        }
    }
    private sealed class SourceFrame{internal long local;internal double stamp;internal string clock,frame;}
    private static List<JObject> MapFrames(string root,string child,string rig,List<MediaPoseAnchor> anchors,long origin,VideoInfo video,CancellationToken token)
    {
        var result=new List<JObject>();if(!video.verified||video.frames<=0||video.fps<=0)return result;
        var lookup=new Dictionary<string,List<SourceFrame>>(StringComparer.Ordinal);
        string path=Path.Combine(Path.GetDirectoryName(Path.Combine(root,child)),"frames.jsonl");
        if(File.Exists(path))
        {
            string name=Path.GetFileNameWithoutExtension(child),suffix=name.StartsWith("video-",StringComparison.Ordinal)?name.Substring(6):"";
            if(!int.TryParse(suffix,out int segment))return result;
            var locals=new HashSet<long>();
            foreach(string line in File.ReadLines(path))
            {
                token.ThrowIfCancellationRequested();if(string.IsNullOrWhiteSpace(line))continue;if(line.Length>32768)throw new InvalidDataException("Camera frame stamp is too large.");
                var row=JObject.Parse(line);if((int?)row["segment"]!=segment)continue;
                long local=(long?)row["frame_in_segment"]??-1;string frame=row["sequence"]?.ToString();
                if(local<0||local>=video.frames||!locals.Add(local)||string.IsNullOrEmpty(frame)||!double.TryParse(row["timestamp_ns"]?.ToString(Formatting.None),NumberStyles.Float,CultureInfo.InvariantCulture,out double stamp)||!ReplayMediaArchive.Finite(stamp))return new List<JObject>();
                AddSource(lookup,new SourceFrame{local=local,frame=frame,stamp=stamp/1e9,clock="unix-host-receive-seconds"});
                if(lookup.Count>4000000)throw new InvalidDataException("Camera frame stamp limit exceeded.");
            }
            if(locals.Count!=video.frames)return result;
        }
        else
        {
            string folder=Path.GetDirectoryName(Path.Combine(root,child)),epoch=Path.GetFileName(folder);
            if(!epoch.EndsWith("-media",StringComparison.Ordinal))return result;
            path=Path.Combine(Path.GetDirectoryName(folder),epoch.Substring(0,epoch.Length-6)+"-all-frames.dat");if(!File.Exists(path))return result;
            long local=0;
            foreach(string line in File.ReadLines(path))
            {
                token.ThrowIfCancellationRequested();if(string.IsNullOrWhiteSpace(line))continue;string[] fields=line.Split(',');
                if(fields.Length<25||!double.TryParse(fields[24].Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out double stamp))return new List<JObject>();
                AddSource(lookup,new SourceFrame{local=local++,frame=fields[0].Trim(),stamp=stamp,clock="tracker-source-milliseconds"});if(local>4000000)throw new InvalidDataException("Camera native log limit exceeded.");
            }
            if(local!=video.frames)return result; // No guessed ordinal match when logs and decoded clips differ.
        }
        foreach(var anchor in anchors.Where(a=>ReplaySessionArchive.CanonicalRig(a.rig)==ReplaySessionArchive.CanonicalRig(rig)).OrderBy(a=>a.ticks))
        {
            if(!lookup.TryGetValue(anchor.frame,out var candidates)||!double.TryParse(anchor.timestamp,NumberStyles.Float,CultureInfo.InvariantCulture,out double stamp))continue;
            // Native JSON timestamps round to binary64 seconds; FT text preserves native source ms.
            double tolerance=anchor.clock=="unix-host-receive-seconds"?.000002:.000001;
            var matches=candidates.Where(f=>f.clock==anchor.clock&&Math.Abs(f.stamp-stamp)<=tolerance).ToArray();if(matches.Length!=1)continue;
            var frame=matches[0];result.Add(new JObject{["replaySeconds"]=(anchor.ticks-origin)/(double)TimeSpan.TicksPerSecond,["mediaFrame"]=frame.local,["mediaSeconds"]=frame.local/video.fps,
                ["poseWallTicks"]=anchor.ticks.ToString(CultureInfo.InvariantCulture),["unityFrame"]=anchor.unityFrame,["sourceFrame"]=anchor.frame,["sourceTimestamp"]=anchor.timestamp,["sourceClock"]=anchor.clock,
                ["sourceSessionId"]=anchor.session,["receiverSessionId"]=anchor.receiver,["sourceEpoch"]=anchor.epoch});
        }
        return result;
    }
    private static void AddSource(Dictionary<string,List<SourceFrame>> lookup,SourceFrame frame){if(!lookup.TryGetValue(frame.frame,out var list))lookup[frame.frame]=list=new List<SourceFrame>();list.Add(frame);}
    private static JObject ReadManifest(string run,long origin)
    {
        string path=Path.Combine(run,"media_manifest.json");
        if(File.Exists(path))
        {
            var existing=JObject.Parse(File.ReadAllText(path));
            if((int?)existing["schemaVersion"]!=1||(string)existing["timeline"]?["originTicks"]!=origin.ToString(CultureInfo.InvariantCulture))throw new InvalidDataException("Existing camera archive uses a different replay origin.");
            return existing;
        }
        return new JObject{["schemaVersion"]=1,["runId"]=Path.GetFileName(run),["timeline"]=new JObject{["clock"]="recorded-wallclock-ticks",["originTicks"]=origin.ToString(CultureInfo.InvariantCulture),["ticksPerSecond"]=TimeSpan.TicksPerSecond},["clips"]=new JArray(),["artifacts"]=new JArray(),["sources"]=new JArray(),["warnings"]=new JArray()};
    }
    internal static void WritePending(string run,string runId,long origin,IEnumerable<MediaRecordingSource> sources,IEnumerable<string> warnings)
    {
        lock(manifestGate)
        {
            var manifest=ReadManifest(run,origin);manifest["sources"]=new JArray(sources.Select(s=>new JObject{["rigId"]=s.RigId,["backend"]=s.Backend,["state"]=s.State=="collected"?"collected":s.Finalized?"closed":"pending",["sourceDirectory"]=s.DirectoryPath,["trackerSessionId"]=s.TrackerSessionId,["sourceRevision"]=s.SourceRevision}));
            manifest["warnings"]=new JArray(warnings.Distinct());WriteManifest(run,manifest);
        }
    }
    private static void WriteManifest(string run,JObject document)
    {
        string path=Path.Combine(run,"media_manifest.json"),temporary=path+".writing";File.WriteAllText(temporary,document.ToString(Formatting.Indented),new UTF8Encoding(false));
        if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
    }
}
