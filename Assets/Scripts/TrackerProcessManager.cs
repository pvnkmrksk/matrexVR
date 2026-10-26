using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[Serializable]
public sealed class TrackerLaunchConfig
{
    public string executablePath = "";
    public string configFile = "";
    public string device = "";
    public bool headless;
    public bool strictCameraFallback;
    public bool saveVideo;
    public string recordingDirectory="";
    public string recordingPrefix="";
}

/// <summary>Independent native apps. A crash, scene change or Unity quit never closes a tracker.</summary>
public sealed class TrackerProcessManager : MonoBehaviour
{
    private sealed class Receipt
    {
        public string vrId,backend,executable,configFile,releaseTag,executableSha256;
        public int processId,port;
        public long startUtcTicks;
        public bool demo,strictCameraFallback;
        public bool saveVideo;
        public string recordingDirectory,recordingPrefix;
        [JsonIgnore] public bool unverified;
    }
    private static TrackerProcessManager instance;
    private static readonly object recordingWatchGate=new object();
    private static readonly HashSet<string> recordingWatches=new HashSet<string>(StringComparer.Ordinal);
    private static string StateRoot => Path.Combine(Application.persistentDataPath,"Companions","processes");
    public static TrackerProcessManager Instance
    {
        get { if(instance==null) { var host=new GameObject("Tracker apps"); DontDestroyOnLoad(host); instance=host.AddComponent<TrackerProcessManager>(); } return instance; }
    }
    private static string Key(string id)
    { using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(id??""))).Replace("-","").ToLowerInvariant(); }
    private static string ReceiptPath(string id)=>Path.Combine(StateRoot,Key(id)+".json");
    private static Receipt Read(string id)
    {
        CompanionMediaArchive.ConfigureReceiptRoot(StateRoot);
        try { string file=ReceiptPath(id);return File.Exists(file)?JsonConvert.DeserializeObject<Receipt>(File.ReadAllText(file)):null; }
        catch(IOException) { return null; } catch(JsonException) { return null; } catch(UnauthorizedAccessException) { return null; }
    }
    private static Process Match(Receipt receipt,bool requireExecutable)
    {
        if(receipt==null||receipt.processId<=0||receipt.startUtcTicks<=0) return null;
        Process process=null;
        try
        {
            process=Process.GetProcessById(receipt.processId);
            if(process.HasExited||process.StartTime.ToUniversalTime().Ticks!=receipt.startUtcTicks) { process.Dispose();return null; }
            if(!requireExecutable)return process; // UI liveness never reads/hashes a large executable.
            try
            {
                string actual=process.MainModule?.FileName;
                if(!string.IsNullOrEmpty(actual)&&!SameExecutable(actual,receipt.executable,receipt.executableSha256)) { process.Dispose();return null; }
                if(requireExecutable&&string.IsNullOrEmpty(actual)) { process.Dispose();return null; }
            }
            catch(Exception error) when(error is System.ComponentModel.Win32Exception || error is NotSupportedException)
            { if(requireExecutable) { process.Dispose();return null; } }
            return process;
        }
        catch(Exception error) when(error is System.ComponentModel.Win32Exception || error is NotSupportedException)
        { if(process!=null&&!requireExecutable) { receipt.unverified=true;return process; }process?.Dispose();return null; }
        catch(Exception error) when(error is ArgumentException || error is InvalidOperationException)
        { process?.Dispose();return null; }
    }
    private static bool SameExecutable(string actual,string expected,string sha)
    {
        if(string.Equals(Path.GetFullPath(actual),expected,RuntimeInformation.IsOSPlatform(OSPlatform.Windows)?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))return true;
        // macOS /var aliases and symlinked installations can expose another canonical path.
        return !string.IsNullOrEmpty(sha)&&File.Exists(actual)&&HashFile(actual)==sha;
    }
    private static string HashFile(string path) { using(var file=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant(); }
    public static string Status(string vrId)
    {
        var receipt=Read(vrId);using(var process=Match(receipt,false))
            return process==null?"Tracker window is not running":receipt.unverified?"Existing tracker process cannot be verified; inspect or close its own window":receipt.backend+(string.IsNullOrEmpty(receipt.releaseTag)?"":" "+receipt.releaseTag)+" running independently · PID "+receipt.processId+(receipt.demo?" · demo input":"");
    }
    public static bool IsRunning(string vrId) { using(var process=Match(Read(vrId),false))return process!=null; }
    public bool Show(string vrId)
    {
        using(var process=Match(Read(vrId),true))
        {
            if(process==null)return false;
            if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))return MacAppLauncher.Activate(process.Id);
            return false; // The operator can use the OS app switcher on other platforms.
        }
    }
    /// <summary>Startup-only provenance for a tracker this manager explicitly launched.
    /// Process ownership is not evidence of a physical camera or valid animal measurement.</summary>
    public static JObject RecordingProvenance(string vrId)
    {
        var receipt=Read(vrId);
        var result=new JObject { ["vrId"]=vrId,["observedUtc"]=DateTimeOffset.UtcNow,
            ["identityVerified"]=false,["processRunning"]=false,["actualInputSource"]="unverified" };
        if(receipt==null) { result["state"]="unmanaged-or-no-owned-launch";return result; }
        result["backend"]=receipt.backend;result["releaseTag"]=receipt.releaseTag;
        result["executableSha256"]=receipt.executableSha256;result["executable"]=receipt.executable;
        result["processId"]=receipt.processId;result["startUtcTicks"]=receipt.startUtcTicks;
        result["port"]=receipt.port;result["configFile"]=receipt.configFile;
        result["demoRequested"]=receipt.demo;result["strictCameraFallback"]=receipt.strictCameraFallback;
        result["savedVideoRequested"]=receipt.saveVideo;result["recordingDirectory"]=receipt.recordingDirectory;
        result["mediaState"]=receipt.saveVideo?"independent-recorder-stop-not-acknowledged":"not-requested";
        using(var live=Match(receipt,false))result["processRunning"]=live!=null;
        using(var verified=Match(receipt,true))result["identityVerified"]=verified!=null&&!receipt.unverified;
        result["state"]=(bool)result["identityVerified"]?"owned-process-identity-verified":
            (bool)result["processRunning"]?"owned-process-identity-unverified":"owned-launch-no-longer-running";
        return result;
    }
    public static string BundledAvailability(SystemConfig rig)=>CompanionCatalog.Availability(rig);
    public static bool CanLaunchBundled(SystemConfig rig)=>CompanionCatalog.IsAvailable(rig);
    public static void RequireRecordingTransitionReady(SystemConfig rig)
    {
        if(rig==null||string.IsNullOrWhiteSpace(rig.vrId))return;
        var receipt=Read(rig.vrId);
        using(var process=Match(receipt,false))if(process!=null&&receipt.saveVideo!=(rig.tracker?.saveVideo??false))
            throw new InvalidOperationException("Close this independent tracker explicitly before changing Save tracker video; its calibration and current recorder were left unchanged.");
    }
    public void LaunchPrepared(CompanionCatalog.Prepared prepared)
    {
        if(prepared==null||prepared.rig==null)throw new ArgumentException("Prepare the bundled tracker first.");
        LaunchInternal(prepared.rig,prepared);
    }
    public void LaunchBundled(SystemConfig rig,bool demo)
    {
        // Resolve/copy a version once. The resulting executable and configuration paths
        // stay stable across starts and Unity versions, including macOS privacy identity.
        var prepared=CompanionCatalog.Prepare(rig,demo);
        LaunchPrepared(prepared);
    }
    // Deliberately no subscription which stops apps on system/scene changes.
    private void OnEnable() { }
    private void OnDisable() { }
    public void StopRemovedRigs(IEnumerable<string> configuredRigIds) { }
    public static string[] Arguments(SystemConfig rig)
    {
        if(rig==null||rig.tracker==null)throw new ArgumentException("Configure this rig's tracker executable and config first.");
        if(!TrackerBackend.IsFlight(rig.closedLoopMode)&&!TrackerBackend.IsWalking(rig.closedLoopMode))throw new ArgumentException("Launch supports Rekkefly and HejjéTrac.");
        if(rig.zmqPort<1||rig.zmqPort>65535||string.IsNullOrWhiteSpace(rig.vrId))throw new ArgumentException("A rig ID and valid tracking port are required.");
        if(!Loopback(rig.zmqAddress))throw new ArgumentException("Local launch requires a loopback tracking address.");
        string config=ResolveConfig(rig.tracker.configFile);
        if(!File.Exists(config))throw new FileNotFoundException("Tracker calibration/config file does not exist.",config);
        if(TrackerBackend.IsFlight(rig.closedLoopMode))
        {
            var args=new List<string>{"--config",config,"--label",rig.vrId,"--zmq-port",rig.zmqPort.ToString(CultureInfo.InvariantCulture)};
            if(!string.IsNullOrWhiteSpace(rig.tracker.device)) { args.Add("--device");args.Add(rig.tracker.device); }
            if(rig.tracker.headless)args.Add("--headless");
            if(rig.tracker.strictCameraFallback)args.Add("--no-camera-fallback");
            if(rig.tracker.saveVideo)args.Add("--record");
            return args.ToArray();
        }
        if(rig.tracker.headless||!string.IsNullOrWhiteSpace(rig.tracker.device))throw new ArgumentException("HejjéTrac device/source belongs in its calibration config; headless is a Rekkefly option.");
        string text=File.ReadAllText(config),port=ConfigField(text,"zmq_port"),host=ConfigField(text,"zmq_host");
        if(!int.TryParse(port,NumberStyles.Integer,CultureInfo.InvariantCulture,out int configured)||configured!=rig.zmqPort)
            throw new ArgumentException("HejjéTrac config zmq_port must match rig port "+rig.zmqPort+".");
        if(!Loopback(string.IsNullOrWhiteSpace(host)?"127.0.0.1":host))throw new ArgumentException("HejjéTrac zmq_host must be loopback.");
        // The native desktop starts its explicit config immediately. Stdio bridge mode
        // pauses it and couples its lifetime to the pipe, so it is intentionally absent.
        return new[]{"--config",config,"--windows","1"};
    }
    public void Launch(SystemConfig rig)=>LaunchInternal(rig,null);
    private void LaunchInternal(SystemConfig rig,CompanionCatalog.Prepared prepared)
    {
        if(rig==null||string.IsNullOrWhiteSpace(rig.vrId))throw new ArgumentException("Select a configured rig first.");
        Directory.CreateDirectory(StateRoot);
        using(var launchLock=new FileStream(ReceiptPath(rig.vrId)+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
        {
            var previous=Read(rig.vrId);
            using(var existing=Match(previous,false))
                if(existing!=null)
                {
                    if(previous.unverified)throw new InvalidOperationException("An existing tracker process cannot be verified. Close its own window before trying Start again; another copy will not be spawned.");
                    string requestedConfig=rig.tracker!=null&&!string.IsNullOrWhiteSpace(rig.tracker.configFile)?ResolveConfig(rig.tracker.configFile):previous.configFile;
                    if(previous.port!=rig.zmqPort||previous.backend!=TrackerBackend.DisplayName(rig.closedLoopMode)||requestedConfig!=previous.configFile||previous.strictCameraFallback!=(rig.tracker?.strictCameraFallback??false)||previous.saveVideo!=(rig.tracker?.saveVideo??false)||(prepared!=null&&previous.executableSha256!=prepared.executableSha256))
                        throw new InvalidOperationException("This rig already has an independent tracker window using another source/configuration. Close it or choose Stop tracker before switching.");
                    RegisterMedia(previous);MonitorRecordingExit(previous);
                    return; // Start is idempotent, including after restarting Unity.
                }
            string[] args=Arguments(rig);string executable=rig.tracker.executablePath??"";
            if(!Path.IsPathFullyQualified(executable)||!File.Exists(executable))throw new ArgumentException("Select an absolute native tracker executable (inside Contents/MacOS for a Mac app).");
            string extension=Path.GetExtension(executable).ToLowerInvariant();
            if(extension==".bat"||extension==".cmd"||extension==".ps1"||extension==".sh")throw new ArgumentException("Select the native executable directly; shell scripts are not launched.");
            executable=Path.GetFullPath(executable);
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)&&extension!=".exe")throw new ArgumentException("Choose the native .exe, rather than a shortcut or command file.");
            string log=Path.Combine(StateRoot,"logs",Key(rig.vrId)+".log");
            bool macBundle=RuntimeInformation.IsOSPlatform(OSPlatform.OSX)&&MacAppLauncher.BundlePath(executable)!=null;
            if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)&&rig.tracker.strictCameraFallback&&!macBundle)
                throw new ArgumentException("Camera-only tracking on macOS requires the complete native .app bundle so camera permission belongs to the tracker.");
            using(var process=macBundle?MacAppLauncher.Launch(executable,args):Process.Start(DetachedStart(executable,args,Path.GetDirectoryName(executable),log)))
            {
                if(process==null)throw new InvalidOperationException("Tracker process did not start.");
                var receipt=new Receipt{vrId=rig.vrId,backend=TrackerBackend.DisplayName(rig.closedLoopMode),executable=executable,configFile=ResolveConfig(rig.tracker.configFile),port=rig.zmqPort,processId=process.Id,startUtcTicks=process.StartTime.ToUniversalTime().Ticks,demo=prepared?.demo??false,strictCameraFallback=rig.tracker.strictCameraFallback,releaseTag=prepared?.releaseTag,executableSha256=prepared?.executableSha256??HashFile(executable),
                    saveVideo=rig.tracker.saveVideo,recordingDirectory=rig.tracker.recordingDirectory,recordingPrefix=rig.tracker.recordingPrefix};
                string file=ReceiptPath(rig.vrId),temporary=file+".writing";
                File.WriteAllText(temporary,JsonConvert.SerializeObject(receipt,Formatting.Indented));
                if(File.Exists(file))File.Delete(file);File.Move(temporary,file);
                RegisterMedia(receipt);MonitorRecordingExit(receipt);
            }
        }
    }
    private static void RegisterMedia(Receipt receipt)
    {
        if(receipt==null)return;
        if(!receipt.saveVideo){CompanionMediaArchive.ClearLaunchedSource(receipt.vrId);return;}
        if(string.IsNullOrWhiteSpace(receipt.recordingDirectory))return;
        CompanionMediaArchive.RegisterLaunchedSource(new MediaRecordingSource{RigId=receipt.vrId,Backend=receipt.backend,DirectoryPath=receipt.recordingDirectory,
            ChildDirectoryPrefix=receipt.recordingPrefix??"",NotBeforeUtcTicks=receipt.startUtcTicks,TrackerSessionId=receipt.processId+"-"+receipt.startUtcTicks,
            ExecutableSha256=receipt.executableSha256,State="pending"});
    }
    private static void MonitorRecordingExit(Receipt receipt)
    {
        if(!receipt.saveVideo)return;
        string identity=receipt.processId+"-"+receipt.startUtcTicks;
        lock(recordingWatchGate)if(!recordingWatches.Add(identity))return;
        try
        {
            var process=Process.GetProcessById(receipt.processId);
            process.EnableRaisingEvents=true;
            process.Exited+=(sender,args)=>{try{if(process.ExitCode==0)CompanionMediaArchive.AcknowledgeStopped(receipt.vrId,identity);}catch(InvalidOperationException){}finally{process.Dispose();lock(recordingWatchGate)recordingWatches.Remove(identity);}};
            if(process.HasExited){try{if(process.ExitCode==0)CompanionMediaArchive.AcknowledgeStopped(receipt.vrId,identity);}finally{process.Dispose();lock(recordingWatchGate)recordingWatches.Remove(identity);}}
        }
        catch(Exception error) when(error is ArgumentException||error is InvalidOperationException||error is System.ComponentModel.Win32Exception) {lock(recordingWatchGate)recordingWatches.Remove(identity);}
    }
    public static ProcessStartInfo DetachedStart(string executable,IEnumerable<string> arguments,string workingDirectory,string logPath)
    {
        if(!Path.IsPathFullyQualified(executable)||!File.Exists(executable))throw new ArgumentException("Choose an existing absolute native executable.");
        string[] args=arguments.ToArray();
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new ProcessStartInfo{FileName=executable,Arguments=JoinArguments(args),WorkingDirectory=workingDirectory,UseShellExecute=true};
        if(!Path.IsPathFullyQualified(logPath))throw new ArgumentException("Detached native output needs an absolute diagnostic log path.");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath));
        if(File.Exists(logPath)&&new FileInfo(logPath).Length>4*1024*1024)
        { string old=logPath+".previous";if(File.Exists(old))File.Delete(old);File.Move(logPath,old); }
        // The fixed POSIX program only redirects streams and execs positional argv.
        // User values are never interpolated/evaluated as shell source. Exec preserves
        // the native PID, with no inherited Unity pipe or terminal hangup lifetime.
        string[] launchArgs=new[]{"-c","trap '' HUP; exec \"$@\" </dev/null >>\"$MATREX_NATIVE_LOG\" 2>&1","matrex-native",executable}.Concat(args).ToArray();
        var start=new ProcessStartInfo{FileName="/bin/sh",Arguments=JoinArguments(launchArgs),WorkingDirectory=workingDirectory,UseShellExecute=false,CreateNoWindow=false};
        start.EnvironmentVariables["MATREX_NATIVE_LOG"]=logPath;
        return start;
    }
    public void Stop(string vrId)
    {
        if(!Directory.Exists(StateRoot))return;
        using(var stopLock=new FileStream(ReceiptPath(vrId)+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            StopLocked(vrId);
    }
    private static void StopLocked(string vrId)
    {
        var receipt=Read(vrId);using(var process=Match(receipt,true))
        {
            if(process==null)
            {
                if(IsRunning(vrId))throw new InvalidOperationException("Cannot safely verify this tracker process. Close its own window instead.");
                return;
            }
            bool closing=false;
            try { closing=process.CloseMainWindow(); }
            catch(Exception error) when(error is InvalidOperationException || error is NotSupportedException || error is NotImplementedException) { }
            bool graceful=closing&&process.WaitForExit(1000);
            if(!graceful) { if(!process.HasExited)process.Kill();process.WaitForExit(1000); }
            if(graceful&&process.HasExited&&process.ExitCode==0)CompanionMediaArchive.AcknowledgeStopped(vrId,receipt.processId+"-"+receipt.startUtcTicks);
        }
        string file=ReceiptPath(vrId);if(File.Exists(file))File.Delete(file);
    }
    private void OnApplicationQuit() { }
    private void OnDestroy() { if(instance==this)instance=null; }
    public static string ResolveConfig(string file)
    { if(string.IsNullOrWhiteSpace(file)) throw new ArgumentException("Select a tracker calibration/config file.");return Path.GetFullPath(Path.IsPathRooted(file)?file:Path.Combine(RuntimePaths.ConfigRoot,file)); }
    private static bool Loopback(string host) => string.Equals(host,"localhost",StringComparison.OrdinalIgnoreCase) || host=="127.0.0.1" || host=="::1" || host=="[::1]";
    private static string ConfigField(string text,string key)
    { var matches=Regex.Matches(text,@"(?m)^\s*"+Regex.Escape(key)+@"\s*:\s*([^\r\n#]+)");return matches.Count==0?null:matches[matches.Count-1].Groups[1].Value.Trim(); }
    public static string JoinArguments(IEnumerable<string> arguments)
    {
        var result=new List<string>();
        foreach(string argument in arguments)
        {
            if(argument==null || argument.IndexOf('\0')>=0 || argument.IndexOf('\n')>=0 || argument.IndexOf('\r')>=0) throw new ArgumentException("Tracker arguments cannot contain control characters.");
            // ProcessStartInfo uses argv parsing, never a shell. Escape backslashes before quotes/end.
            result.Add("\""+Regex.Replace(argument,@"(\\*)""",m=>m.Groups[1].Value+m.Groups[1].Value+"\\\"")+new string('\\',TrailingSlashes(argument))+"\"");
        }
        return string.Join(" ",result);
    }
    private static int TrailingSlashes(string value) { int n=0;for(int i=value.Length-1;i>=0 && value[i]=='\\';i--)n++;return n; }
}
