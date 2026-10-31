using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Best-effort recorder preparation. Warnings never imply the VR experiment failed to start.</summary>
public sealed class NativeRecordingStartReport
{
    public string RunId { get; }
    public IReadOnlyList<JObject> Rigs { get; }
    public IReadOnlyList<string> Warnings { get; }
    public bool Cancelled { get; }
    public int AcknowledgedRigCount => Rigs.Count(rig => (bool?)rig["acknowledged"] == true);
    internal NativeRecordingStartReport(string runId,IEnumerable<JObject> rigs,IEnumerable<string> warnings,bool cancelled)
    {
        RunId=runId;Rigs=Array.AsReadOnly(rigs.Select(rig=>(JObject)rig.DeepClone()).ToArray());
        Warnings=Array.AsReadOnly(warnings.Where(value=>!string.IsNullOrWhiteSpace(value)).Distinct().ToArray());Cancelled=cancelled;
    }
}

public sealed partial class TrackerProcessManager
{
    private const int RecordingPreparationBudgetMilliseconds=2000;
    private sealed class RecordingTake
    {
        public string RigId,Backend,RequestedRoot,Directory,TakeId,StopRequestId,State="preparing",Error;
        public SystemConfig Desired;public Receipt Owner;public NativeRecorderClient Client;public bool StartSent,StartQueued,ManualOverride,OperatorStopRequested,FirstFrameAcknowledged,Requested=true,Automatic=true;
        public MediaRecordingSource Media;
        public Task<string> EndTask;
    }
    private sealed class RecordingRun
    {
        public string Id,Directory,ConfigRoot,EvidenceFailure;
        public MasterDataLogger Logger;
        public readonly CancellationTokenSource Cancellation=new CancellationTokenSource();
        public readonly List<RecordingTake> Takes=new List<RecordingTake>();
        public Task<NativeRecordingStartReport> BeginTask;public Task EndTask;public bool Ending;
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
        if(value=="override")return "Stopped by operator · automatic restart disabled";
        if(value=="closed-unacknowledged")return "Native take closed · first-frame recording was not acknowledged";
        if(value=="unavailable")return "Tracker video unavailable · VR continues · "+state["error"];
        if(value=="failed")return "Recorder needs attention · "+state["error"];
        return value=="idle"?"Recorder idle":value=="stopping"?"Closing native recording…":"Waiting for native recording acknowledgement…";
    }
    private static void SetRecordingState(RecordingRun run,RecordingTake take,string state,JObject evidence=null)
    {
        take.State=state;
        var value=new JObject{["schemaVersion"]=1,["utc"]=DateTimeOffset.UtcNow,["runId"]=run.Id,["vrId"]=take.RigId,["backend"]=take.Backend,
            ["state"]=state,["requested"]=take.Requested,["controlMode"]=take.Automatic?"Experiment":"Manual",["acknowledged"]=(state=="recording"||state=="complete")&&take.FirstFrameAcknowledged,["takeId"]=take.TakeId,["directory"]=take.Directory,
            ["requestedTrackingAddress"]=take.Desired?.zmqAddress,["requestedTrackingPort"]=take.Desired?.zmqPort,["ownedTrackingPort"]=take.Owner?.port,
            ["requestedConfigRoot"]=run.ConfigRoot,["ownedConfigRoot"]=take.Owner?.configRoot,
            ["stopRequestId"]=take.StopRequestId,["processId"]=take.Owner?.processId,["controlInstance"]=take.Owner?.controlInstance,["error"]=take.Error,["native"]=evidence?.DeepClone()};
        lock(recordingGate)
        {
            recordingStates[take.RigId]=value;
            try{File.AppendAllText(Path.Combine(run.Directory,"native_recording_control.jsonl"),value.ToString(Formatting.None)+"\n");}
            catch(Exception error)when(error is IOException||error is UnauthorizedAccessException){run.EvidenceFailure="Could not preserve native recording-control evidence: "+error.Message;}
        }
    }
    public static Task<NativeRecordingStartReport> BeginExperimentRecordingAsync(IEnumerable<SystemConfig> configurations,string runDirectory,string runId,CancellationToken token=default)
    {
        if(string.IsNullOrWhiteSpace(runId)||!Path.IsPathFullyQualified(runDirectory))throw new ArgumentException("Native recording requires the immutable VR run ID and absolute directory.");
        _=Instance;
        lock(recordingGate)
        {
            if(recordingRuns.TryGetValue(runId,out var existing))return existing.BeginTask;
            string identityPath=Path.Combine(runDirectory,"native_recording_run.json");
            if(File.Exists(identityPath)&&(string)NativeRecorderClient.ReadObject(identityPath)["runId"]!=runId)throw new InvalidDataException("Create a new VR recording session before starting another native take.");
            if(!File.Exists(identityPath))File.WriteAllText(identityPath,new JObject{["schemaVersion"]=1,["runId"]=runId,["createdUtc"]=DateTimeOffset.UtcNow}.ToString(Formatting.Indented));
            var run=new RecordingRun{Id=runId,Directory=Path.GetFullPath(runDirectory),ConfigRoot=Path.GetFullPath(RuntimePaths.ConfigRoot),Logger=MasterDataLogger.Instance};
            recordingRuns[runId]=run;run.BeginTask=BeginRunAsync(run,(configurations??Array.Empty<SystemConfig>()).ToArray(),token);return run.BeginTask;
        }
    }
    private static async Task<NativeRecordingStartReport> BeginRunAsync(RecordingRun run,SystemConfig[] rigs,CancellationToken token)
    {
        using(var linked=CancellationTokenSource.CreateLinkedTokenSource(token,run.Cancellation.Token))
        {
            var starts=new List<Task>();
            // Every available rig starts independently; no unavailable rig can skip another rig's attempt.
            foreach(var rig in rigs.Where(rig=>rig!=null))
            {
                var take=new RecordingTake{Desired=JsonConvert.DeserializeObject<SystemConfig>(JsonConvert.SerializeObject(rig)),RigId=rig.vrId,Backend=TrackerBackend.IsFlight(rig.closedLoopMode)?"rekkefly":"hejjetrac",
                    Requested=rig.tracker?.saveVideo??true,Automatic=(rig.tracker?.recordingControl??TrackerRecordingControlMode.Experiment)==TrackerRecordingControlMode.Experiment};
                run.Takes.Add(take);
                if(!take.Requested){SetRecordingState(run,take,"disabled");continue;}
                if(!take.Automatic){SetRecordingState(run,take,"manual");continue;}
                starts.Add(BeginTakeAsync(run,take,linked.Token));
            }
            await Task.WhenAll(starts);
            var report=new NativeRecordingStartReport(run.Id,run.Takes.Select(take=>RecordingState(take.RigId)),
                run.Takes.Where(take=>take.State=="unavailable").Select(take=>take.RigId+": tracker video unavailable; VR continues. "+take.Error),linked.IsCancellationRequested);
            try
            {
                File.WriteAllText(Path.Combine(run.Directory,"native_recording_start.json"),new JObject{["schemaVersion"]=1,["runId"]=run.Id,
                    ["policy"]="best-effort-independent-recorders",["cancelled"]=report.Cancelled,["warnings"]=new JArray(report.Warnings),["rigs"]=new JArray(report.Rigs)}.ToString(Formatting.Indented));
            }
            catch(Exception error)when(error is IOException||error is UnauthorizedAccessException){run.EvidenceFailure="Could not preserve recorder start report: "+error.Message;}
            return report;
        }
    }
    private static bool SameRecordingPath(string left,string right)
    {
        if(string.IsNullOrWhiteSpace(left)||string.IsNullOrWhiteSpace(right))return false;
        var comparison=System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar),Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar),comparison);
    }
    private static void RequireDesiredRecorderIdentity(RecordingRun run,RecordingTake take)
    {
        var desired=take.Desired;var owner=take.Owner;
        if(!Loopback(desired.zmqAddress))throw new InvalidOperationException("This rig uses a remote tracking address; its local owned tracker was left unchanged.");
        if(owner==null)throw new InvalidOperationException("No verified control-capable tracker is running. Initialize its window, choose Manual control, or turn off tracker video.");
        if(!string.Equals(owner.backend,TrackerBackend.DisplayName(desired.closedLoopMode),StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The owned tracker uses another backend; it was left unchanged.");
        if(owner.port!=desired.zmqPort)throw new InvalidOperationException("The owned tracker uses another input port; it was left unchanged.");
        if(owner.recordingControl!=TrackerRecordingControlMode.Experiment)throw new InvalidOperationException("The owned tracker uses manual recording control; it was left unchanged.");
        var requested=desired.tracker;
        if(!string.IsNullOrWhiteSpace(requested?.executablePath)&&(!Path.IsPathFullyQualified(requested.executablePath)||!SameRecordingPath(requested.executablePath,owner.executable)))
            throw new InvalidOperationException("The selected executable differs from the owned tracker; it was left unchanged.");
        bool explicitCalibration=!string.IsNullOrWhiteSpace(requested?.configFile);
        if(explicitCalibration&&!SameRecordingPath(ResolveConfig(requested.configFile),owner.configFile))
            throw new InvalidOperationException("The selected calibration/config differs from the owned tracker; it was left unchanged.");
        if(!string.IsNullOrWhiteSpace(requested?.device)&&!string.Equals(requested.device,owner.device,StringComparison.Ordinal))
            throw new InvalidOperationException("The selected device differs from the owned tracker; it was left unchanged.");
        if(requested?.headless==true&&!owner.headless)throw new InvalidOperationException("The selected headless mode differs from the owned tracker; it was left unchanged.");
        if(requested?.strictCameraFallback==true&&!owner.strictCameraFallback)throw new InvalidOperationException("The owned tracker does not match the requested camera-only mode; it was left unchanged.");
        if(!string.IsNullOrWhiteSpace(owner.configRoot))
        {
            if(!SameRecordingPath(owner.configRoot,run.ConfigRoot))throw new InvalidOperationException("The owned tracker belongs to another configuration profile; it was left unchanged. Reinitialize it explicitly for this profile.");
        }
        else if(!NativeRecorderClient.IsInside(owner.configFile,run.ConfigRoot))
            throw new InvalidOperationException("This legacy tracker receipt cannot be associated with the current configuration profile; it was left unchanged.");
    }
    private static async Task BeginTakeAsync(RecordingRun run,RecordingTake take,CancellationToken token)
    {
        using(var bounded=CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            bounded.CancelAfter(RecordingPreparationBudgetMilliseconds);
            try
            {
                SetRecordingState(run,take,"preparing");
                bounded.Token.ThrowIfCancellationRequested();
                take.Owner=Read(take.RigId);
                RequireDesiredRecorderIdentity(run,take);
                using(var process=Match(take.Owner,true))if(process==null||take.Owner.unverified||string.IsNullOrEmpty(take.Owner.controlDirectory)||string.IsNullOrEmpty(take.Owner.controlInstance))
                    throw new InvalidOperationException("No verified control-capable tracker is running. Initialize its window, choose Manual control, or turn off tracker video.");
                // A call is short and the attempt has a shared two-second budget; an already-sent Start settles within one additional second.
                take.Client=new NativeRecorderClient(take.Owner.controlDirectory,take.Owner.controlInstance,take.Backend,take.Owner.processId,1000);
                await take.Client.WaitReadyAsync(bounded.Token);
                var before=NativeRecorderClient.State(await take.Client.CallAsync("status",token:bounded.Token));NativeRecorderClient.RequireHealthy(before);
                if(NativeRecorderClient.Recording(before))throw new InvalidOperationException("A native/manual take is already recording and was left unchanged.");
                if((bool?)before["ended"]==true)throw new InvalidOperationException("Tracker input ended; restart its source when ready.");
                if((bool?)before["paused"]==true)
                {
                    SetRecordingState(run,take,"resuming-tracking",before);
                    await take.Client.CallAsync("start",token:bounded.Token);
                }
                string root=Path.Combine(StateRoot,"takes",run.Id,take.RigId+"-"+Guid.NewGuid().ToString("N"));
                bounded.Token.ThrowIfCancellationRequested();
                take.RequestedRoot=root;take.StartSent=true;SetRecordingState(run,take,"starting");
                // Once sent, settle bounded command delivery so End can queue an ownership-fenced stop after it.
                await take.Client.CallAsync("record_start",new JObject{["directory"]=root,["record_options"]=new JObject{["raw"]=true,["debug"]=false},["recording_tuning"]=new JObject{["encoding"]="mjpeg"}},CancellationToken.None);
                take.StartQueued=true;
                while(true)
                {
                    bounded.Token.ThrowIfCancellationRequested();
                    var state=NativeRecorderClient.State(await take.Client.CallAsync("status",token:bounded.Token));NativeRecorderClient.RequireHealthy(state);
                    string directory=NativeRecorderClient.RecordingDirectory(state);
                    if(NativeRecorderClient.Recording(state)&&NativeRecorderClient.IsInside(directory,root)&&NativeRecorderClient.SubmittedFrames(state)>0)
                    {
                        take.FirstFrameAcknowledged=true;take.Directory=Path.GetFullPath(directory);take.TakeId=Path.GetFileName(take.Directory);
                        take.Media=new MediaRecordingSource{RigId=take.RigId,Backend=take.Backend,RunId=run.Id,DirectoryPath=take.Directory,ChildDirectoryPrefix=null,
                            TrackerSessionId=run.Id+":"+take.RigId+":"+take.TakeId,ExecutableSha256=take.Owner.executableSha256,NotBeforeUtcTicks=0,State="recording-acknowledged"};
                        CompanionMediaArchive.RegisterLaunchedSource(take.Media);
                        if(run.Logger!=null&&run.Logger.directoryPath==run.Directory)run.Logger.RegisterCompanionSource(take.Media);
                        SetRecordingState(run,take,"recording",state);return;
                    }
                    await Task.Delay(40,bounded.Token);
                }
            }
            catch(OperationCanceledException)when(token.IsCancellationRequested)
            {SetRecordingState(run,take,"cancelling");}
            catch(Exception error)
            {
                // A negative transport reply was never accepted into the native queue; there is no owned take to stop.
                if(error is NativeRecorderCommandRejectedException rejected&&rejected.Command=="record_start")take.StartSent=false;
                take.Error=error is OperationCanceledException?"Recorder preparation exceeded its two-second budget.":error.Message;
                SetRecordingState(run,take,"unavailable");
            }
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
        try{await((Task)run.BeginTask??Task.CompletedTask);}catch(OperationCanceledException)when(run.Ending){}catch(Exception error){errors.Add(error.Message);}
        // Dispatch every rig's stop before waiting for any encoder to drain.
        var failures=await Task.WhenAll(run.Takes.Where(take=>take.StartSent).Select(take=>EndTakeAsync(run,take,token)));
        errors.AddRange(failures.Where(error=>error!=null));
        if(run.EvidenceFailure!=null)errors.Add(run.EvidenceFailure);
        lock(recordingGate)recordingRuns.Remove(run.Id);
        run.Cancellation.Dispose();
        if(errors.Count>0)throw new InvalidOperationException(string.Join("; ",errors.Distinct()));
    }
    public static async Task<JObject> StopRecorderForOperatorAsync(string vrId)
    {
        RecordingRun run;RecordingTake take;
        lock(recordingGate)
        {
            run=recordingRuns.Values.SingleOrDefault(value=>!value.Ending&&value.Takes.Any(item=>item.RigId==vrId&&item.State=="recording"));
            take=run?.Takes.SingleOrDefault(item=>item.RigId==vrId&&item.State=="recording");
            if(take==null)throw new InvalidOperationException("No acknowledged experiment-owned recorder is active for "+vrId+". Manual mode remains controlled in the native tracker.");
            take.ManualOverride=true;take.OperatorStopRequested=true;
        }
        await EndTakeAsync(run,take,CancellationToken.None);
        if(take.State=="failed")throw new InvalidOperationException(take.Error);
        return RecordingState(vrId);
    }
    private static Task<string> EndTakeAsync(RecordingRun run,RecordingTake take,CancellationToken token)
    {lock(recordingGate)return take.EndTask??(take.EndTask=EndTakeCoreAsync(run,take,token));}
    private static async Task<string> EndTakeCoreAsync(RecordingRun run,RecordingTake take,CancellationToken token)
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
                    TrackerSessionId=run.Id+":"+take.RigId+":"+take.TakeId,ExecutableSha256=take.Owner.executableSha256,NotBeforeUtcTicks=0,
                    NoVideoFrames=take.Backend=="rekkefly"?(long?)completion["accepted"]==0:(long?)(completion["media_recording"] as JObject)?["raw"]?["encoded_frames"]==0||completion["media_recording"]?.Type==JTokenType.Null,State="closed-unpresented-take"};
                CompanionMediaArchive.RegisterLaunchedSource(take.Media);
                if(run.Logger!=null&&run.Logger.directoryPath==run.Directory)run.Logger.RegisterCompanionSource(take.Media);
            }
            CompanionMediaArchive.AcknowledgeFinalizedTake(take.RigId,take.Media.TrackerSessionId,completion);
            if((string)completion["control_stop_id"]!=take.StopRequestId||(long?)completion["capture_pause_count"]>0)take.ManualOverride=true;
            SetRecordingState(run,take,take.ManualOverride?"override":take.FirstFrameAcknowledged?"complete":"closed-unacknowledged",completion);
            return take.ManualOverride?take.RigId+(take.OperatorStopRequested?": operator requested a manual recorder stop; automatic restart was disabled.":": native recording was paused/stopped outside the matching VR stop request; automatic restart was disabled."):null;
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
