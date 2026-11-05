using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>The verified endpoint rejected a command before accepting it into its worker queue.</summary>
public sealed class NativeRecorderCommandRejectedException : InvalidOperationException
{
    public string Command { get; }
    public NativeRecorderCommandRejectedException(string command,string message):base("Native tracker rejected "+command+": "+message){Command=command;}
}

/// <summary>Pure filesystem transport; it never launches another engine or touches calibration.</summary>
public sealed class NativeRecorderClient
{
    public readonly string DirectoryPath,Instance,Backend;
    public readonly int ProcessId;
    private readonly int timeoutMilliseconds;
    public NativeRecorderClient(string directory,string instance,string backend,int processId,int timeoutMilliseconds=15000)
    {
        if(timeoutMilliseconds<50||timeoutMilliseconds>30000)throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
        DirectoryPath=Path.GetFullPath(directory);Instance=instance;Backend=backend;ProcessId=processId;this.timeoutMilliseconds=timeoutMilliseconds;
    }
    public async Task<JObject> WaitReadyAsync(CancellationToken token=default)
    {
        var timer=Stopwatch.StartNew();string path=Path.Combine(DirectoryPath,"endpoint.json");
        while(timer.ElapsedMilliseconds<timeoutMilliseconds)
        {
            token.ThrowIfCancellationRequested();
            if(File.Exists(path))
            {
                var value=ReadObject(path);
                if((int?)value["protocolVersion"]!=1||(string)value["instance"]!=Instance||(int?)value["processId"]!=ProcessId||
                    !string.Equals((string)value["backend"],Backend,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Native recording endpoint identity/version differs from this owned tracker.");
                var capabilities=value["capabilities"] as JArray;
                if(capabilities==null||!capabilities.Values<string>().Contains("native-gui-control")||!capabilities.Values<string>().Contains("per-take-finalization")||!capabilities.Values<string>().Contains("owned-take-stop-v1"))
                    throw new InvalidDataException("Tracker does not acknowledge per-take recording finalization.");
                return value;
            }
            await Task.Delay(40,token).ConfigureAwait(false);
        }
        throw new TimeoutException("Tracker did not expose its recording-control endpoint. Update/reinitialize the bundled tracker or choose Manual recording control.");
    }
    public async Task<JObject> CallAsync(string command,JObject arguments=null,CancellationToken token=default)
    {
        string id=Guid.NewGuid().ToString("N"),request=Path.Combine(DirectoryPath,id+".request.json"),response=Path.Combine(DirectoryPath,id+".response.json"),temporary=request+".writing";
        var expires=DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);var timer=Stopwatch.StartNew();
        try
        {
            token.ThrowIfCancellationRequested();
            var payload=new JObject{["id"]=id,["instance"]=Instance,["command"]=command,["args"]=arguments??new JObject(),["expiresUtcMilliseconds"]=expires.ToUnixTimeMilliseconds()};
            File.WriteAllText(temporary,payload.ToString(Formatting.None),new UTF8Encoding(false));File.Move(temporary,request);
            while(timer.ElapsedMilliseconds<timeoutMilliseconds)
            {
                token.ThrowIfCancellationRequested();
                if(File.Exists(response))
                {
                    var reply=ReadObject(response);
                    if((string)reply["id"]!=id||(string)reply["instance"]!=Instance)throw new InvalidDataException("Native recording response identity mismatch.");
                    if(reply["ok"]?.Type!=JTokenType.Boolean)throw new InvalidDataException("Native reply has no valid acceptance flag; the command outcome remains unknown.");
                    if(!(bool)reply["ok"])throw new NativeRecorderCommandRejectedException(command,(string)reply["error"]);
                    return reply["result"] as JObject??throw new InvalidDataException("Native tracker response has no state object.");
                }
                await Task.Delay(25,token).ConfigureAwait(false);
            }
            throw new TimeoutException("Native tracker did not acknowledge "+command+". Its outcome is unknown; recording is not marked complete.");
        }
        finally
        {
            foreach(string path in new[]{temporary,request,response})try{if(File.Exists(path))File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
    public static JObject State(JObject response)=>response?["status"] as JObject??response;
    public static bool Recording(JObject state)=>state?["recording"]?.Type==JTokenType.Boolean?(bool)state["recording"]:(bool?)(state?["recording"] as JObject)?["active"]==true;
    public static string RecordingDirectory(JObject state)=>state?["recording"] is JObject recording?(string)recording["directory"]:(string)state?["recording_directory"];
    public static void RequireHealthy(JObject state)
    {
        // command_error may describe an earlier rejected take; fresh directory/receipt evidence establishes command completion.
        foreach(string key in new[]{"error"})if(!string.IsNullOrWhiteSpace((string)state?[key]))throw new InvalidOperationException("Native tracker: "+state[key]);
        if(!string.IsNullOrWhiteSpace((string)(state?["recording"] as JObject)?["error"]))throw new InvalidOperationException("Native recorder: "+state["recording"]["error"]);
        if((state?["media_recording"] as JObject)?["raw"] is JObject raw&&((string)raw["state"]=="failed"||!string.IsNullOrWhiteSpace((string)raw["warning"])))
            throw new InvalidOperationException("Native video recorder: "+raw.ToString(Formatting.None));
    }
    public static long SubmittedFrames(JObject state)
    {
        if(state?["recording"] is JObject recording)return(long?)recording["accepted"]??0;
        var raw=(state?["media_recording"] as JObject)?["raw"] as JObject;
        return(long?)raw?["submitted_frames"]??0;
    }
    public static bool IsInside(string candidate,string directory)
    {
        if(string.IsNullOrWhiteSpace(candidate)||string.IsNullOrWhiteSpace(directory))return false;
        string path=Path.GetFullPath(candidate),root=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        return path==root||path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal);
    }
    public static JObject ReadObject(string path)
    {
        if(new FileInfo(path).Length>1048576)throw new InvalidDataException("Native recording-control evidence exceeds1MiB.");
        using(var input=File.OpenText(path))using(var reader=new JsonTextReader(input){DateParseHandling=DateParseHandling.None})return JObject.Load(reader);
    }
    /// <summary>Only a completed native container receipt, never inactivity/file-size polling, proves closure.</summary>
    public static JObject Completion(string directory,string backend,bool allowEmpty=false)
    {
        string path=Path.Combine(directory,"completion.json");if(!File.Exists(path))return null;
        var value=ReadObject(path);
        if(Path.GetFullPath((string)value["directory"]??"")!=Path.GetFullPath(directory))throw new InvalidDataException("Native completion directory differs from the owned take.");
        if((string)value["state"]!="complete")throw new InvalidDataException("Native take failed finalization: "+value.ToString(Formatting.None));
        if(backend=="rekkefly")
        {
            if((string)value["takeId"]!=Path.GetFileName(directory)||(bool?)value["writersDrained"]!=true||value["error"]?.Type!=JTokenType.Null||!((long?)value["accepted"]>=0)||(long?)value["accepted"]!=(long?)value["written"])
                throw new InvalidDataException("Rekkefly completion does not prove all accepted frames were written and closed.");
        }
        else
        {
            if(value["numeric_error"]?.Type!=JTokenType.Null||value["completion_error"]?.Type!=JTokenType.Null)
                throw new InvalidDataException("HejjéTrac completion reports numeric/receipt errors.");
            if(value["media_warnings"] is JArray warnings&&warnings.Count>0)throw new InvalidDataException("HejjéTrac video finalization reported warnings: "+warnings.ToString(Formatting.None));
            var media=value["media_recording"] as JObject;
            if(media==null)
            {
                if(allowEmpty&&(long?)value["numeric_frames"]==0)return value;
                throw new InvalidDataException("HejjéTrac completion has no media finalization evidence.");
            }
            if((bool?)media["raw"]?["requested"]!=true)throw new InvalidDataException("HejjéTrac did not finalize the requested raw tracker video.");
            foreach(string key in new[]{"raw","debug"})if(media[key] is JObject channel&&(bool?)channel["requested"]==true)
                if((string)channel["state"]!="complete"||(bool?)channel["container_finalized"]!=true||(long?)channel["pending_frames"]!=0||
                    (long?)channel["submitted_frames"]!=(long?)channel["encoded_frames"]||!((long?)channel["encoded_frames"]>=0))
                    throw new InvalidDataException("HejjéTrac "+key+" media did not drain/finalize completely.");
        }
        return value;
    }
}
