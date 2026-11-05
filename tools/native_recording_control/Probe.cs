using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class NativeRecordingProbe
{
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
    static async Task<JObject> Poll(NativeRecorderClient client,Func<JObject,bool> condition,string label)
    {
        var timer=Stopwatch.StartNew();JObject state=null;
        while(timer.Elapsed.TotalSeconds<20)
        {
            state=NativeRecorderClient.State(await client.CallAsync("status"));
            if(condition(state))return state;
            if((bool?)state["ended"]==true)throw new Exception(label+": native source ended "+state);
            await Task.Delay(25);
        }
        throw new Exception(label+": timeout "+state);
    }
    public static async Task<int> Main(string[] args)
    {
        string output=Path.GetFullPath(args[4]);Directory.CreateDirectory(output);var trace=new JArray();
        try
        {
            var client=new NativeRecorderClient(args[0],args[1],args[3],int.Parse(args[2]));
            var endpoint=await client.WaitReadyAsync();Check((string)endpoint["instance"]==args[1],"Exact native endpoint instance/PID/capabilities");
            await client.CallAsync("pause");var idle=await Poll(client,s=>(bool?)s["paused"]==true,"pause");
            Check(!NativeRecorderClient.Recording(idle),"Tracking initialization does not start a recording");
            string previous=null;
            for(int takeIndex=0;takeIndex<2;takeIndex++)
            {
                long start=(long?)idle["frames"]??0;
                string requested=Path.Combine(output,"take-"+takeIndex+"-"+Guid.NewGuid().ToString("N"));
                var ack=await client.CallAsync("record_start",new JObject{["directory"]=requested,["record_options"]=new JObject{["raw"]=true,["debug"]=false}});
                Check((bool?)ack["queued"]==true,"Native record start reports queue acceptance rather than file completion");
                var recording=await Poll(client,s=>NativeRecorderClient.Recording(s)&&NativeRecorderClient.IsInside(NativeRecorderClient.RecordingDirectory(s),requested),"record start");
                string directory=NativeRecorderClient.RecordingDirectory(recording);
                Check(directory!=previous,"Each acknowledged take has a distinct immutable directory");previous=directory;
                Check(NativeRecorderClient.Completion(directory,args[3])==null,"An active recorder has no completed receipt");
                Check(NativeRecorderClient.SubmittedFrames(recording)==0,"Armed while paused is distinct from first-frame recording acknowledgement");
                await client.CallAsync("start");await Poll(client,s=>(long?)s["frames"]>=start+30,"source advancement");
                await client.CallAsync("pause");var paused=await Poll(client,s=>(bool?)s["paused"]==true,"pause during take");
                Check(NativeRecorderClient.Recording(paused),"Pausing source is distinct from stopping/draining the recorder");
                Check(NativeRecorderClient.SubmittedFrames(paused)>0,"First raw video frame is explicitly observable before acquisition can be acknowledged");
                // The worker must not stop another recording, including a stale/canceled caller's take.
                await client.CallAsync("record_stop",new JObject{["expected_directory"]=Path.Combine(output,"another-take"),["stop_request_id"]=Guid.NewGuid().ToString("N")});
                await Poll(client,s=>s["command_error"]?.Type==JTokenType.String,"ownership fence");
                var protectedState=NativeRecorderClient.State(await client.CallAsync("status"));
                Check(NativeRecorderClient.Recording(protectedState)&&NativeRecorderClient.RecordingDirectory(protectedState)==directory,"Wrong-take stop is rejected without replacing/stopping the current recording");
                string stopId=Guid.NewGuid().ToString("N");
                await client.CallAsync("record_stop",new JObject{["expected_directory"]=directory,["stop_request_id"]=stopId});
                var timer=Stopwatch.StartNew();JObject completion=null;
                while(completion==null&&timer.Elapsed.TotalSeconds<20){completion=NativeRecorderClient.Completion(directory,args[3]);if(completion==null)await Task.Delay(25);}
                Check(completion!=null,"Native completion proves encoder/container finalization without terminating tracking");
                Check((string)completion["control_stop_id"]==stopId,"Capture worker correlates finalization to the exact owned stop request");
                Check((long?)completion["capture_pause_count"]==1,"Native pause during take remains explicit even if client polling misses it");
                var stopped=await Poll(client,s=>!NativeRecorderClient.Recording(s),"stop");
                Check((bool?)stopped["ended"]!=true,"Tracking engine remains available after recording finalization");
                Check(Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories).Any(p=>p.EndsWith(".avi")),"Actual camera/video frames produced AVI media");
                Check(!Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories).Any(p=>p.EndsWith(".partial")),"No partial media remains after acknowledgement");
                trace.Add(new JObject{["take"]=takeIndex,["sourceBefore"]=start,["sourceAfter"]=stopped["frames"],["directory"]=directory,["completion"]=completion});
                // Nonrecorded gap: input counters move while the finalized take stays byte-identical.
                var files=Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories).ToDictionary(p=>p,p=>Hash(p));
                long gap=(long?)stopped["frames"]??0;await client.CallAsync("start");await Poll(client,s=>(long?)s["frames"]>=gap+5,"nonrecorded gap");
                await client.CallAsync("pause");idle=await Poll(client,s=>(bool?)s["paused"]==true,"gap pause");
                Check(!NativeRecorderClient.Recording(idle)&&files.All(p=>File.Exists(p.Key)&&Hash(p.Key)==p.Value),"Tracking advances in nonrecorded gap without modifying the closed take");
            }
            File.WriteAllText(Path.Combine(output,"results.json"),new JObject{["passed"]=true,["checks"]=checks,["endpoint"]=endpoint,["takes"]=trace}.ToString(Formatting.Indented));
            Console.WriteLine("PASS: "+checks+" real native "+args[3]+" lifecycle/ownership/finalization checks");return 0;
        }
        catch(Exception error)
        {File.WriteAllText(Path.Combine(output,"results.json"),new JObject{["passed"]=false,["checks"]=checks,["failure"]=error.ToString(),["takes"]=trace}.ToString(Formatting.Indented));Console.Error.WriteLine(error);return 1;}
    }
    static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())using(var input=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(input));}
}
