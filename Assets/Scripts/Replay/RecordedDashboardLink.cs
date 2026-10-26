using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

public readonly struct RecordedReplayCommand
{
    public string Kind { get; }
    public double Seconds { get; }
    public string RequestId { get; }
    internal long Sequence { get; }
    internal RecordedReplayCommand(string kind,double seconds,string id,long sequence){Kind=kind;Seconds=seconds;RequestId=id;Sequence=sequence;}
}

/// <summary>An optional independent recorded viewer. No acquisition telemetry or render-thread HTTP.</summary>
public sealed class RecordedDashboardLink : MonoBehaviour
{
    private const string Url="http://127.0.0.1:8061",Protocol="matrex.replay.v1";
    private static RecordedDashboardLink instance;
    private readonly object gate=new object();
    private readonly Queue<RecordedReplayCommand> commands=new Queue<RecordedReplayCommand>();
    private string run,session,stateRoot,status="Not connected",error;
    private long origin,revision,ack,received;
    private double seconds,duration,rate=1;
    private bool playing,ready;
    private CancellationTokenSource cancellation;
    public string Status {get{lock(gate)return status;}}
    public string Error {get{lock(gate)return error;}}
    public bool IsReady {get{lock(gate)return ready;}}

    public static RecordedDashboardLink Open(string runDirectory,long originTicks)
    {
        if(instance==null){var host=new GameObject("Independent recorded trajectory viewer");DontDestroyOnLoad(host);instance=host.AddComponent<RecordedDashboardLink>();}
        instance.CloseSession();
        string directory=Path.GetFullPath(runDirectory),id=Guid.NewGuid().ToString("N");
        instance.stateRoot=Path.Combine(Application.persistentDataPath,"CompanionState");
        instance.cancellation=new CancellationTokenSource();
        lock(instance.gate){instance.run=directory;instance.origin=originTicks;instance.session=id;instance.status="Preparing recorded trajectories…";instance.error=null;instance.ready=false;instance.revision=instance.ack=instance.received=0;instance.seconds=instance.duration=0;instance.rate=1;instance.playing=false;}
        instance.StartCoroutine(instance.Prepare(directory,id,originTicks,instance.cancellation.Token));return instance;
    }
    public void UpdateClock(double seconds,bool playing,double rate,double duration)
    {
        if(!ReplayMediaArchive.Finite(seconds)||!ReplayMediaArchive.Finite(duration)||!ReplayMediaArchive.Finite(rate)||duration<0||rate<=0)return;
        lock(gate){this.seconds=Math.Max(0,Math.Min(duration,seconds));this.playing=playing;this.rate=Math.Min(64,rate);this.duration=duration;revision++;}
    }
    public bool TryTakeCommand(out RecordedReplayCommand command)
    {
        lock(gate)
        {
            if(commands.Count==0){command=default;return false;}
            command=commands.Dequeue();ack=Math.Max(ack,command.Sequence);return true;
        }
    }
    public void CloseSession()
    {
        cancellation?.Cancel();cancellation?.Dispose();cancellation=null;
        lock(gate){session=null;commands.Clear();ready=false;status="Recorded trajectory link closed";}
    }
    private System.Collections.IEnumerator Prepare(string directory,string id,long start,CancellationToken stop)
    {
        Task<string> prepare=DesktopDashboard.PrepareBundledExecutableAsync();
        while(!prepare.IsCompleted){if(stop.IsCancellationRequested)yield break;yield return null;}
        if(stop.IsCancellationRequested)yield break;
        if(prepare.IsFaulted){Fail(prepare.Exception.GetBaseException().Message,id);yield break;}
        var connection=Task.Run(()=>Connect(prepare.Result,stop),stop);
        while(!connection.IsCompleted){if(stop.IsCancellationRequested)yield break;yield return null;}
        if(stop.IsCancellationRequested)yield break;
        if(connection.IsFaulted){Fail(connection.Exception.GetBaseException().Message,id);yield break;}
        bool current;
        lock(gate){current=!stop.IsCancellationRequested&&session==id;if(current){ready=true;status="Linked recorded trajectories · independent viewer";}}
        if(!current)yield break;
        string ownerToken=connection.Result;
        Application.OpenURL(Url+"/replay/#token="+ownerToken);
        _=Task.Run(()=>Pump(directory,id,start,ownerToken,stop),stop);
    }
    private string Connect(string executable,CancellationToken stop)
    {
        Directory.CreateDirectory(stateRoot);
        string receiptPath=Path.Combine(stateRoot,"recorded-dashboard-process.json");
        using(var launchGate=new FileStream(Path.Combine(stateRoot,"recorded-dashboard-start.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
        {
            JObject receipt=null;
            if(File.Exists(receiptPath))try{receipt=JObject.Parse(File.ReadAllText(receiptPath));}catch(Newtonsoft.Json.JsonException){}
            JObject capability=Probe(stop);
            if(capability!=null)
            {
                string previousToken=(string)receipt?["token"];
                if(!Compatible(capability,previousToken))throw new IOException("Port 8061 is running another or older viewer. Close that recorded viewer explicitly, then retry; no second copy was started.");
                return previousToken;
            }
            if(receipt!=null)
            {
                try{using(var previous=Process.GetProcessById((int)receipt["pid"]))if(!previous.HasExited&&previous.StartTime.ToUniversalTime().Ticks==(long)receipt["startedUtcTicks"])
                    throw new IOException("The existing recorded viewer has not become ready. Inspect recorded-dashboard.log or close its own process before retrying; no duplicate was started.");}
                catch(ArgumentException){}
            }
            stop.ThrowIfCancellationRequested();string token=Guid.NewGuid().ToString("N");
            var start=TrackerProcessManager.DetachedStart(executable,new[]{"--no-browser","--host","127.0.0.1","--port","8061","--replay-token",token},Path.GetDirectoryName(executable),Path.Combine(stateRoot,"recorded-dashboard.log"));
            using(var process=Process.Start(start))
            {
                if(process==null)throw new IOException("Recorded viewer did not start.");
                string temporary=receiptPath+".writing";File.WriteAllText(temporary,new JObject{["pid"]=process.Id,["startedUtcTicks"]=process.StartTime.ToUniversalTime().Ticks,["token"]=token,["executable"]=executable,["protocol"]=Protocol}.ToString());
                if(File.Exists(receiptPath))File.Replace(temporary,receiptPath,null);else File.Move(temporary,receiptPath);
                for(int attempt=0;attempt<100;attempt++)
                {
                    stop.ThrowIfCancellationRequested();
                    if(process.HasExited)throw new IOException("Bundled recorded viewer exited (code "+process.ExitCode+"). This package may lack the replay API; inspect recorded-dashboard.log and update its dashboard. No retry process was spawned.");
                    capability=Probe(stop);
                    if(capability!=null){if(!Compatible(capability,token))throw new IOException("Bundled viewer lacks recorded replay support. Update its packaged dashboard; no retry process was spawned.");return token;}
                    if(stop.WaitHandle.WaitOne(200))stop.ThrowIfCancellationRequested();
                }
            }
            throw new IOException("Recorded viewer did not become ready. Your replay continues independently; inspect recorded-dashboard.log before retrying.");
        }
    }
    private static bool Compatible(JObject capability,string token)
    {
        if(string.IsNullOrEmpty(token))return false;
        string identity;using(var sha=SHA256.Create())identity=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(token))).Replace("-","").ToLowerInvariant().Substring(0,16);
        return (string)capability["protocol"]==Protocol&&(int?)capability["version"]==1&&(bool?)capability["enabled"]==true&&(string)capability["instanceId"]==identity;
    }
    private JObject Probe(CancellationToken stop)
    {
        try{return Request("GET","/api/replay/capabilities",null,stop);}
        catch(WebException error)
        {
            if(error.Response!=null)throw new IOException("Port 8061 is occupied by a service without the required recorded replay API; no duplicate viewer was started.",error);
            return null;
        }
    }
    private async Task Pump(string directory,string id,long start,string ownerToken,CancellationToken stop)
    {
        int failures=0;
        while(!stop.IsCancellationRequested)
        {
            JObject clock;
            lock(gate){if(stop.IsCancellationRequested||session!=id)break;clock=new JObject{["sessionId"]=id,["runDirectory"]=directory,["originTicks"]=start.ToString(CultureInfo.InvariantCulture),["seconds"]=seconds,["playing"]=playing,["rate"]=rate,["duration"]=duration,["revision"]=revision,["ackCommand"]=ack};}
            try
            {
                var response=Request("POST","/api/replay/state",clock,stop,ownerToken);
                if((string)response["protocol"]!=Protocol)throw new IOException("Recorded viewer protocol changed.");
                if(!AcceptResponse(response,id,stop))break;
                failures=0;
            }
            catch(OperationCanceledException){break;}
            catch(Exception problem){lock(gate){if(stop.IsCancellationRequested||session!=id)break;ready=false;error=problem.Message;status="Recorded plots disconnected; replay continues";}if(++failures>=10)break;}
            try{await Task.Delay(100,stop).ConfigureAwait(false);}catch(OperationCanceledException){break;}
        }
    }
    private bool AcceptResponse(JObject response,string id,CancellationToken stop)
    {
        lock(gate)
        {
            if(stop.IsCancellationRequested||session!=id)return false;
            foreach(var item in (response["commands"] as JArray??new JArray()))
            {
                long sequence=(long?)item["sequence"]??0;string kind=(string)item["kind"];double value=(double?)item["seconds"]??double.NaN;
                if(sequence<=received||(string)item["sessionId"]!=id||!ReplayMediaArchive.Finite(value)||(kind!="seek"&&kind!="play"&&kind!="pause"))continue;
                if(commands.Count>=64)throw new IOException("Recorded viewer command queue is full.");
                commands.Enqueue(new RecordedReplayCommand(kind,value,(string)item["requestId"],sequence));received=sequence;
            }
            ready=true;error=null;status="Linked recorded trajectories · Unity clock";return true;
        }
    }
    private JObject Request(string method,string path,JObject payload,CancellationToken stop,string ownerToken=null)
    {
        stop.ThrowIfCancellationRequested();
        var request=(HttpWebRequest)WebRequest.Create(Url+path);request.Method=method;request.Timeout=1200;request.ReadWriteTimeout=1200;request.Proxy=null;
        if(ownerToken!=null)request.Headers["X-Matrex-Replay-Token"]=ownerToken;
        using(stop.Register(()=>request.Abort()))
        {
            if(payload!=null){byte[] bytes=Encoding.UTF8.GetBytes(payload.ToString(Newtonsoft.Json.Formatting.None));request.ContentType="application/json";request.ContentLength=bytes.Length;using(var output=request.GetRequestStream())output.Write(bytes,0,bytes.Length);}
            using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream()))return JObject.Parse(reader.ReadToEnd());
        }
    }
    private void Fail(string message,string id){lock(gate){if(session!=id)return;ready=false;error=message;status="Recorded plots unavailable: "+message;}UnityEngine.Debug.LogWarning(Status);}
    private void OnDestroy(){CloseSession();if(instance==this)instance=null;}
}
