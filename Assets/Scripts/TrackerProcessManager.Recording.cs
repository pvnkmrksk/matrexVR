using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public sealed partial class TrackerProcessManager
{
    private sealed class RecordingTake
    {
        public string RigId,Backend,RequestedRoot,Directory,TakeId,StopRequestId,State="preparing",Error;
        public Receipt Owner;public NativeRecorderClient Client;public bool StartSent,StartQueued,ManualOverride;
        public MediaRecordingSource Media;
    }
    private sealed class RecordingRun
    {
        public string Id,Directory,Failure;
        public MasterDataLogger Logger;
        public readonly CancellationTokenSource Cancellation=new CancellationTokenSource();
        public readonly List<RecordingTake> Takes=new List<RecordingTake>();
        public Task BeginTask,EndTask;public bool Ending;
    }
    private static readonly object recordingGate=new object();
    private static readonly Dictionary<string,RecordingRun> recordingRuns=new Dictionary<string,RecordingRun>(StringComparer.Ordinal);
    private static readonly Dictionary<string,JObject> recordingStates=new Dictionary<string,JObject>(StringComparer.Ordinal);
    public static JObject RecordingState(string vrId)
    {lock(recordingGate)return recordingStates.TryGetValue(vrId??"",out var value)?(JObject)value.DeepClone():new JObject{["state"]="idle",["requested"]=false,["acknowledged"]=false};}
    public static string RecordingStatus(string vrId)
    {
        var state=RecordingState(vrId);string value=(string)state["state"];
        if(value=="manual")return "Manual recording · controlled in the tracker";
        if(value=="disabled")return "Tracker video disabled";
        if(value=="recording")return "Recording acknowledged · "+state["takeId"];
        if(value=="complete")return "Recording finalized · native writers drained";
        if(value=="override")return "Stopped in native tracker · automatic restart disabled";
        if(value=="failed")return "Recorder needs attention · "+state["error"];
        return value=="idle"?"Recorder idle":value=="stopping"?"Closing native recording…":"Waiting for native recording acknowledgement…";
    }
    private static void SetRecordingState(RecordingRun run,RecordingTake take,string state,JObject evidence=null)
    {
        take.State=state;
        var value=new JObject{["schemaVersion"]=1,["utc"]=DateTimeOffset.UtcNow,["runId"]=run.Id,["vrId"]=take.RigId,["backend"]=take.Backend,
            ["state"]=state,["requested"]=true,["acknowledged"]=state=="recording"||state=="complete",["takeId"]=take.TakeId,["directory"]=take.Directory,
            ["stopRequestId"]=take.StopRequestId,["processId"]=take.Owner?.processId,["controlInstance"]=take.Owner?.controlInstance,["error"]=take.Error,["native"]=evidence?.DeepClone()};
        lock(recordingGate)
        {
            recordingStates[take.RigId]=value;
            try{File.AppendAllText(Path.Combine(run.Directory,"native_recording_control.jsonl"),value.ToString(Formatting.None)+"\n");}
            catch(Exception error)when(error is IOException||error is UnauthorizedAccessException){run.Failure="Could not preserve native recording-control evidence: "+error.Message;}
        }
    }
    public static Task BeginExperimentRecordingAsync(IEnumerable<SystemConfig> configurations,string runDirectory,string runId,CancellationToken token=default)
    {
        if(string.IsNullOrWhiteSpace(runId)||!Path.IsPathFullyQualified(runDirectory))throw new ArgumentException("Native recording requires the immutable VR run ID and absolute directory.");
        _=Instance; // Reconnect polling must exist even when this Unity process did not launch the native window.
        lock(recordingGate)
        {
            if(recordingRuns.TryGetValue(runId,out var existing))return existing.BeginTask;
            string identityPath=Path.Combine(runDirectory,"native_recording_run.json");
            if(File.Exists(identityPath)&&(string)NativeRecorderClient.ReadObject(identityPath)["runId"]!=runId)throw new InvalidDataException("Create a new VR recording session before starting another native take.");
            if(!File.Exists(identityPath))File.WriteAllText(identityPath,new JObject{["schemaVersion"]=1,["runId"]=runId,["createdUtc"]=DateTimeOffset.UtcNow}.ToString(Formatting.Indented));
            var run=new RecordingRun{Id=runId,Directory=Path.GetFullPath(runDirectory),Logger=MasterDataLogger.Instance};
            recordingRuns[runId]=run;run.BeginTask=BeginRunAsync(run,(configurations??Array.Empty<SystemConfig>()).ToArray(),token);return run.BeginTask;
        }
    }
    private static async Task BeginRunAsync(RecordingRun run,SystemConfig[] rigs,CancellationToken token)
    {
        using(var linked=CancellationTokenSource.CreateLinkedTokenSource(token,run.Cancellation.Token))
        try
        {
            foreach(var rig in rigs.Where(rig=>rig!=null))
            {
                if(!(rig.tracker?.saveVideo??true))
                {lock(recordingGate)recordingStates[rig.vrId]=new JObject{["state"]="disabled",["requested"]=false,["acknowledged"]=false};continue;}
                if((rig.tracker?.recordingControl??TrackerRecordingControlMode.Experiment)==TrackerRecordingControlMode.Manual)
                {lock(recordingGate)recordingStates[rig.vrId]=new JObject{["state"]="manual",["requested"]=true,["acknowledged"]=false};continue;}
                var take=new RecordingTake{RigId=rig.vrId,Backend=TrackerBackend.IsFlight(rig.closedLoopMode)?"rekkefly":"hejjetrac"};run.Takes.Add(take);
                SetRecordingState(run,take,"preparing");
                take.Owner=Read(rig.vrId);
                using(var process=Match(take.Owner,true))if(process==null||take.Owner.unverified||string.IsNullOrEmpty(take.Owner.controlDirectory)||string.IsNullOrEmpty(take.Owner.controlInstance))
                    throw new InvalidOperationException(rig.vrId+": automatic experiment recording needs a verified, control-capable tracker initialized by MATREX. Initialize the bundled tracker, turn off tracker video, or explicitly choose Manual recording control.");
                take.Client=new NativeRecorderClient(take.Owner.controlDirectory,take.Owner.controlInstance,take.Backend,take.Owner.processId);
                await take.Client.WaitReadyAsync(linked.Token);
                var before=NativeRecorderClient.State(await take.Client.CallAsync("status",token:linked.Token));NativeRecorderClient.RequireHealthy(before);
                if(NativeRecorderClient.Recording(before))throw new InvalidOperationException(rig.vrId+": a native/manual take is already recording. It was left unchanged; stop it explicitly or choose Manual control.");
                if((bool?)before["paused"]==true)
                {
                    SetRecordingState(run,take,"resuming-tracking",before);
                    await take.Client.CallAsync("start",token:linked.Token);
                }
                if((bool?)before["ended"]==true)throw new InvalidOperationException(rig.vrId+": tracker input ended; restart its source before acquisition.");
                string root=Path.Combine(StateRoot,"takes",run.Id,rig.vrId+"-"+Guid.NewGuid().ToString("N"));
                linked.Token.ThrowIfCancellationRequested();
                take.RequestedRoot=root;take.StartSent=true;SetRecordingState(run,take,"starting");
                // Once sent, settle command delivery even if Stop cancels preparation. End then queues an ownership-fenced stop after it.
                await take.Client.CallAsync("record_start",new JObject{["directory"]=root,["record_options"]=new JObject{["raw"]=true,["debug"]=false},["recording_tuning"]=new JObject{["encoding"]="mjpeg"}},CancellationToken.None);
                take.StartQueued=true;
                var timer=Stopwatch.StartNew();
                while(true)
                {
                    linked.Token.ThrowIfCancellationRequested();
                    var state=NativeRecorderClient.State(await take.Client.CallAsync("status",token:linked.Token));NativeRecorderClient.RequireHealthy(state);
                    string directory=NativeRecorderClient.RecordingDirectory(state);
                    if(NativeRecorderClient.Recording(state)&&NativeRecorderClient.IsInside(directory,root)&&NativeRecorderClient.SubmittedFrames(state)>0)
                    {
                        take.Directory=Path.GetFullPath(directory);take.TakeId=Path.GetFileName(take.Directory);
                        take.Media=new MediaRecordingSource{RigId=take.RigId,Backend=take.Backend,RunId=run.Id,DirectoryPath=take.Directory,ChildDirectoryPrefix=null,
                            TrackerSessionId=run.Id+":"+take.RigId+":"+take.TakeId,ExecutableSha256=take.Owner.executableSha256,NotBeforeUtcTicks=0,State="recording-acknowledged"};
                        CompanionMediaArchive.RegisterLaunchedSource(take.Media);
                        if(run.Logger!=null&&run.Logger.directoryPath==run.Directory)run.Logger.RegisterCompanionSource(take.Media);
                        SetRecordingState(run,take,"recording",state);break;
                    }
                    if(timer.Elapsed.TotalSeconds>=15)throw new TimeoutException(take.RigId+": native worker did not acknowledge a fresh recording take and first video frame: "+state.ToString(Formatting.None));
                    await Task.Delay(40,linked.Token);
                }
            }
            if(run.Failure!=null)throw new IOException(run.Failure);
        }
        catch(Exception error)
        {
            if(error is OperationCanceledException&&linked.IsCancellationRequested)
            {
                foreach(var take in run.Takes.Where(take=>take.State!="recording"))SetRecordingState(run,take,"cancelling");
                throw;
            }
            run.Failure=error.Message;
            foreach(var take in run.Takes.Where(take=>take.State!="recording")){take.Error=error.Message;SetRecordingState(run,take,"failed");}
            throw;
        }
    }
    public static Task EndExperimentRecordingAsync(string runId,string endReason,CancellationToken token=default)
    {
        lock(recordingGate)
        {
            if(!recordingRuns.TryGetValue(runId??"",out var run))return Task.CompletedTask;
            if(run.EndTask!=null)return run.EndTask;
            run.Ending=true;run.Cancellation.Cancel();run.EndTask=EndRunAsync(run,endReason,token);return run.EndTask;
        }
    }
    private static async Task EndRunAsync(RecordingRun run,string reason,CancellationToken token)
    {
        var errors=new List<string>();
        try{await(run.BeginTask??Task.CompletedTask);}catch(OperationCanceledException)when(run.Ending){}catch(Exception error){errors.Add(error.Message);}
        // Dispatch every rig's stop before waiting for any encoder to drain.
        var failures=await Task.WhenAll(run.Takes.Where(take=>take.StartSent).Select(take=>EndTakeAsync(run,take,token)));
        errors.AddRange(failures.Where(error=>error!=null));
        if(run.Failure!=null)errors.Add(run.Failure);
        lock(recordingGate)recordingRuns.Remove(run.Id);
        run.Cancellation.Dispose();
        if(errors.Count>0)throw new InvalidOperationException(string.Join("; ",errors.Distinct()));
    }
    private static async Task<string> EndTakeAsync(RecordingRun run,RecordingTake take,CancellationToken token)
    {
        try
        {
            take.StopRequestId=Guid.NewGuid().ToString("N");SetRecordingState(run,take,"stopping");
            JObject completion=string.IsNullOrEmpty(take.Directory)?null:NativeRecorderClient.Completion(take.Directory,take.Backend,take.Media==null);
            var stopArgs=new JObject{["expected_directory"]=take.Directory??take.RequestedRoot,["stop_request_id"]=take.StopRequestId};
            if(completion==null)await take.Client.CallAsync("record_stop",stopArgs,token);
            var timer=Stopwatch.StartNew();long nextRetry=500;
            while(completion==null)
            {
                token.ThrowIfCancellationRequested();
                if(!string.IsNullOrEmpty(take.Directory))completion=NativeRecorderClient.Completion(take.Directory,take.Backend,take.Media==null);
                if(completion!=null)break;
                var state=NativeRecorderClient.State(await take.Client.CallAsync("status",token:token));
                string directory=NativeRecorderClient.RecordingDirectory(state);
                if(take.Directory==null&&NativeRecorderClient.IsInside(directory,take.RequestedRoot)){take.Directory=Path.GetFullPath(directory);take.TakeId=Path.GetFileName(directory);}
                if(NativeRecorderClient.IsInside(directory,take.RequestedRoot))
                {
                    NativeRecorderClient.RequireHealthy(state);
                    // A full finalizer queue can reject Stop. Retrying this ownership-fenced idempotent stop cannot touch a different take.
                    if(NativeRecorderClient.Recording(state)&&timer.ElapsedMilliseconds>=nextRetry)
                    {await take.Client.CallAsync("record_stop",stopArgs,token);nextRetry=timer.ElapsedMilliseconds+500;}
                }
                if(timer.Elapsed.TotalSeconds>=30)throw new TimeoutException(take.RigId+": native take did not prove finalized containers/writers; media remains unacknowledged. Last state: "+state.ToString(Formatting.None));
                await Task.Delay(50,token);
            }
            if(take.Media==null&&take.Directory!=null)
            {
                take.Media=new MediaRecordingSource{RigId=take.RigId,Backend=take.Backend,RunId=run.Id,DirectoryPath=take.Directory,ChildDirectoryPrefix=null,
                    TrackerSessionId=run.Id+":"+take.RigId+":"+take.TakeId,ExecutableSha256=take.Owner.executableSha256,NotBeforeUtcTicks=0,State="closed-unpresented-take"};
                CompanionMediaArchive.RegisterLaunchedSource(take.Media);
                if(run.Logger!=null&&run.Logger.directoryPath==run.Directory)run.Logger.RegisterCompanionSource(take.Media);
            }
            CompanionMediaArchive.AcknowledgeFinalizedTake(take.RigId,take.Media.TrackerSessionId,completion);
            if((string)completion["control_stop_id"]!=take.StopRequestId||(long?)completion["capture_pause_count"]>0)take.ManualOverride=true;
            SetRecordingState(run,take,take.ManualOverride?"override":"complete",completion);
            return take.ManualOverride?take.RigId+": native recording was paused/stopped outside the matching VR stop request; automatic restart was disabled.":null;
        }
        catch(Exception error){take.Error=error.Message;SetRecordingState(run,take,"failed");return error.Message;}
    }
    private float nextRecordingPoll;
    private bool recordingPollActive;
    private void Update()
    {
        if(recordingPollActive||UnityEngine.Time.unscaledTime<nextRecordingPoll)return;
        nextRecordingPoll=UnityEngine.Time.unscaledTime+.4f;
        RecordingRun[] runs;lock(recordingGate)runs=recordingRuns.Values.Where(run=>!run.Ending).ToArray();
        if(runs.Length>0)_=PollRecordersAsync(runs);
    }
    private async Task PollRecordersAsync(RecordingRun[] runs)
    {
        recordingPollActive=true;
        try
        {
            foreach(var run in runs)foreach(var take in run.Takes.ToArray().Where(take=>take.State=="recording"))
            {
                try
                {
                    var state=NativeRecorderClient.State(await take.Client.CallAsync("status"));
                    if(run.Ending||take.State!="recording")continue;
                    NativeRecorderClient.RequireHealthy(state);
                    if(!NativeRecorderClient.Recording(state)||(bool?)state["paused"]==true||NativeRecorderClient.RecordingDirectory(state)!=take.Directory)
                        throw new InvalidOperationException("The native GUI paused/stopped/replaced the requested take; automatic restart is disabled.");
                }
                catch(Exception error)
                {
                    if(run.Ending)continue;
                    take.ManualOverride=true;take.Error=error.Message;SetRecordingState(run,take,"override");
                }
            }
        }
        finally{recordingPollActive=false;}
    }
}
