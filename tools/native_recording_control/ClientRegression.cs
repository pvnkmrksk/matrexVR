using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class ClientRegression
{
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
    static async Task Reject(Func<Task> action,string label){bool failed=false;try{await action();}catch(Exception){failed=true;}Check(failed,label);}
    static void Write(string path,JObject value){string temp=path+".tmp";File.WriteAllText(temp,value.ToString(Formatting.None));File.Move(temp,path);}
    static JObject Endpoint(string instance,int pid)=>new JObject{["protocolVersion"]=1,["instance"]=instance,["backend"]="rekkefly",["processId"]=pid,["capabilities"]=new JArray("native-gui-control","per-take-finalization","owned-take-stop-v1")};
    static async Task Answer(string root,Func<JObject,JObject> reply)
    {
        while(true)
        {
            var path=Directory.GetFiles(root,"*.request.json").FirstOrDefault();
            if(path!=null){var request=NativeRecorderClient.ReadObject(path);Write(Path.Combine(root,(string)request["id"]+".response.json"),reply(request));return;}
            await Task.Delay(2);
        }
    }
    public static async Task<int> Main()
    {
        string root=Path.Combine(Path.GetTempPath(),"native-recorder-client-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string instance=Guid.NewGuid().ToString("N");int pid=System.Diagnostics.Process.GetCurrentProcess().Id;
        try
        {
            string descriptor=Path.Combine(root,"endpoint.json");Write(descriptor,Endpoint(instance,pid));var client=new NativeRecorderClient(root,instance,"rekkefly",pid,80);
            Check((string)(await client.WaitReadyAsync())["instance"]==instance,"Exact endpoint nonce/PID/capability handshake");
            await Reject(()=>new NativeRecorderClient(root,Guid.NewGuid().ToString("N"),"rekkefly",pid,80).WaitReadyAsync(),"Foreign endpoint is rejected");
            await Reject(()=>new NativeRecorderClient(root,instance,"rekkefly",pid+1,80).WaitReadyAsync(),"Reused/foreign PID is rejected");
            File.Delete(descriptor);var missing=Endpoint(instance,pid);missing["capabilities"]=new JArray("native-gui-control");Write(descriptor,missing);
            await Reject(()=>client.WaitReadyAsync(),"Inactive-only legacy recorder cannot claim completion capability");
            File.Delete(descriptor);Write(descriptor,Endpoint(instance,pid));
            var peer=Answer(root,request=>new JObject{["id"]=request["id"],["instance"]=instance,["ok"]=true,["result"]=new JObject{["requestArgs"]=request["args"],["recording"]=false}});
            var result=await client.CallAsync("status",new JObject{["enabled"]=true});await peer;
            Check((bool?)result["requestArgs"]?["enabled"]==true,"Typed command arguments reach an actual filesystem peer");
            Check(!Directory.GetFiles(root,"*.request.json").Any()&&!Directory.GetFiles(root,"*.response.json").Any(),"Completed transport artifacts are removed");
            peer=Answer(root,request=>new JObject{["id"]=request["id"],["instance"]="foreign",["ok"]=true,["result"]=new JObject()});
            await Reject(()=>client.CallAsync("status"),"Foreign response nonce is rejected");await peer;
            peer=Answer(root,request=>new JObject{["id"]=request["id"],["instance"]=instance,["ok"]=false,["error"]="writer failed"});
            bool commandRejected=false;try{await client.CallAsync("record_start");}catch(NativeRecorderCommandRejectedException error){commandRejected=error.Command=="record_start";}
            Check(commandRejected,"A negative native reply identifies an unaccepted command, distinct from an ambiguous timeout");await peer;
            peer=Answer(root,request=>new JObject{["id"]=request["id"],["instance"]=instance,["result"]=new JObject()});
            bool malformedUnknown=false;try{await client.CallAsync("record_start");}catch(InvalidDataException){malformedUnknown=true;}Check(malformedUnknown,"A malformed acceptance reply remains an ambiguous outcome rather than a definitive pre-queue rejection");await peer;
            await Reject(()=>client.CallAsync("status"),"Missing response has a bounded timeout");
            var canceled=new CancellationTokenSource();canceled.Cancel();await Reject(()=>client.CallAsync("record_start",token:canceled.Token),"Canceled commands are not sent");
            Check(!Directory.GetFiles(root,"*.request.json").Any(),"Timeout and cancellation leave no executable stale request");
            string take=Path.Combine(root,"take-id");Directory.CreateDirectory(take);Check(NativeRecorderClient.Completion(take,"rekkefly")==null,"No receipt proves no closure, even if a tracker reports inactive");
            string completion=Path.Combine(take,"completion.json");
            JObject R()=>new JObject{["directory"]=take,["takeId"]="take-id",["state"]="complete",["writersDrained"]=true,["accepted"]=7,["written"]=7,["error"]=JValue.CreateNull()};
            void Save(JObject value){if(File.Exists(completion))File.Delete(completion);Write(completion,value);}
            var value=R();value["writersDrained"]=false;Save(value);await Reject(()=>Task.Run(()=>NativeRecorderClient.Completion(take,"rekkefly")),"Inactive-but-draining Rekkefly take is rejected");
            value=R();value["written"]=6;Save(value);await Reject(()=>Task.Run(()=>NativeRecorderClient.Completion(take,"rekkefly")),"Dropped/unwritten accepted frames reject completion");
            value=R();value["takeId"]="another-take";Save(value);await Reject(()=>Task.Run(()=>NativeRecorderClient.Completion(take,"rekkefly")),"Foreign take receipt is rejected");
            Save(R());Check(NativeRecorderClient.Completion(take,"rekkefly")!=null,"Matched drained accepted==written receipt proves completed take");
            var h=new JObject{["directory"]=take,["state"]="complete",["numeric_error"]=JValue.CreateNull(),["completion_error"]=JValue.CreateNull(),["media_recording"]=new JObject{["raw"]=new JObject{["requested"]=true,["state"]="complete",["container_finalized"]=true,["pending_frames"]=0,["submitted_frames"]=11,["encoded_frames"]=11}}};
            Save(h);Check(NativeRecorderClient.Completion(take,"hejjetrac")!=null,"HejjéTrac numerical/media/container completion is verified");
            h["media_recording"]["raw"]["container_finalized"]=false;Save(h);await Reject(()=>Task.Run(()=>NativeRecorderClient.Completion(take,"hejjetrac")),"Unclosed HejjéTrac container is rejected");
            Check(NativeRecorderClient.Recording(new JObject{["recording"]=true})&&NativeRecorderClient.Recording(new JObject{["recording"]=new JObject{["active"]=true}}),"Both actual native status shapes are interpreted");
            var unarmed=new JObject{["recording"]=false,["media_recording"]=JValue.CreateNull(),["error"]=JValue.CreateNull(),["command_error"]="earlier rejected take"};
            NativeRecorderClient.RequireHealthy(unarmed);
            Check(NativeRecorderClient.SubmittedFrames(unarmed)==0,"Unarmed nullable native media and stale command errors are normal preflight state");
            Console.WriteLine("PASS: "+checks+" production native-recorder transport/finalization checks");return 0;
        }
        finally{Directory.Delete(root,true);}
    }
}
