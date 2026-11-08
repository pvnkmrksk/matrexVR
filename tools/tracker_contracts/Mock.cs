using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

// Isolated executable for real process ownership / filesystem transport checks.
// Frame counts are simulated here; actual native video tests are separate.
class Mock
{
    static void Write(string path,JsonObject value)
    {string temporary=path+".writing";File.WriteAllText(temporary,value.ToJsonString());File.Move(temporary,path,true);}
    static void Main(string[] args)
    {
        if (args.SequenceEqual(new[]{"--help"}))
        {
            Console.WriteLine(AppContext.BaseDirectory.Contains("legacy-tracker")
                ? "Tracker options: --control-stdio --control-bind"
                : "Tracker options: --control-dir <DIRECTORY> --control-instance <INSTANCE> --no-camera-fallback");
            return;
        }
        string Argument(string name){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:null;}
        string log=Environment.GetEnvironmentVariable("MATREX_TRACKER_ARGUMENT_LOG");if(log!=null)File.WriteAllText(log,JsonSerializer.Serialize(args));
        string directory=Argument("--control-dir"),instance=Argument("--control-instance"),config=Argument("--config");
        string fixture=config!=null&&File.Exists(config)?File.ReadAllText(config):"";
        bool enabled=directory!=null&&fixture.Contains("fixture-endpoint");
        bool unresponsive=fixture.Contains("unresponsive"),rejectStart=fixture.Contains("reject-start"),zeroFrames=fixture.Contains("zero-frames");
        string take=null;bool recording=false;
        JsonObject State()=>new JsonObject{["queued"]=false,["paused"]=false,["ended"]=false,["error"]=null,["recording"]=new JsonObject{["active"]=recording,["directory"]=take,["accepted"]=recording&&!zeroFrames?1:0,["written"]=recording&&!zeroFrames?1:0,["error"]=null}};
        if(enabled)
        {
            Directory.CreateDirectory(directory);
            Write(Path.Combine(directory,"endpoint.json"),new JsonObject{["protocolVersion"]=1,["instance"]=instance,["backend"]="rekkefly",["processId"]=Environment.ProcessId,["capabilities"]=new JsonArray("native-gui-control","per-take-finalization","owned-take-stop-v1")});
        }
        while(true)
        {
            if(enabled&&!unresponsive)foreach(string path in Directory.EnumerateFiles(directory,"*.request.json"))
            {
                var request=JsonNode.Parse(File.ReadAllText(path)).AsObject();string id=(string)request["id"],command=(string)request["command"],error=null;
                File.AppendAllText(Path.Combine(directory,"requests.jsonl"),command+"\n");
                var arguments=request["args"].AsObject();
                if(command=="record_start")
                {
                    if(rejectStart)error="Fixture rejected before queue acceptance";
                    else{take=Path.Combine((string)arguments["directory"],"fixture-take-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(take);recording=true;}
                }
                if(command=="record_stop"&&recording)
                {
                    if(take.StartsWith((string)arguments["expected_directory"],StringComparison.Ordinal))
                    {recording=false;Write(Path.Combine(take,"completion.json"),new JsonObject{["directory"]=take,["takeId"]=Path.GetFileName(take),["state"]="complete",["writersDrained"]=true,["accepted"]=zeroFrames?0:1,["written"]=zeroFrames?0:1,["error"]=null,["control_stop_id"]=(string)arguments["stop_request_id"],["capture_pause_count"]=0});}
                    else error="Wrong take";
                }
                var state=State();state["queued"]=command!="status";
                Write(Path.Combine(directory,id+".response.json"),new JsonObject{["id"]=id,["instance"]=instance,["ok"]=error==null,["error"]=error,["result"]=state});
                File.Delete(path);
            }
            System.Threading.Thread.Sleep(5);
        }
    }
}
