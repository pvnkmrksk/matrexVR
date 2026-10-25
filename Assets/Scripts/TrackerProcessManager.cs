using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public sealed class TrackerLaunchConfig
{
    public string executablePath = "";
    public string configFile = "";
    public string device = "";
    public bool headless;
}

/// <summary>Explicit, opt-in tracker launches. Never discovers, downloads or stops unrelated processes.</summary>
public sealed class TrackerProcessManager : MonoBehaviour
{
    private sealed class Owned { public Process process; public bool walking; public string backend; public string detail; }
    private readonly Dictionary<string,Owned> owned = new Dictionary<string,Owned>(StringComparer.Ordinal);
    private static TrackerProcessManager instance;
    public static TrackerProcessManager Instance
    {
        get { if(instance==null) { var host=new GameObject("Tracker apps"); DontDestroyOnLoad(host); instance=host.AddComponent<TrackerProcessManager>(); } return instance; }
    }
    public static string Status(string vrId)
    {
        if(instance==null || !instance.owned.TryGetValue(vrId,out var app)) return "Tracker not launched by MATREX";
        try { return app.process.HasExited?app.backend+" exited ("+app.process.ExitCode+")"+(string.IsNullOrWhiteSpace(app.detail)?"":" · "+app.detail): app.backend+" running · PID "+app.process.Id; }
        catch(InvalidOperationException) { return "Tracker stopped"; }
    }
    public static bool IsRunning(string vrId)
    {
        if(instance==null || !instance.owned.TryGetValue(vrId,out var app)) return false;
        try { return !app.process.HasExited; } catch(InvalidOperationException) { return false; }
    }
    private void OnEnable() => MainController.SystemConfigurationChanged += ReconcileConfiguredRigs;
    private void OnDisable() => MainController.SystemConfigurationChanged -= ReconcileConfiguredRigs;
    private void ReconcileConfiguredRigs()
    {
        if(MainController.Instance!=null) StopRemovedRigs(MainController.Instance.SystemConfigs.Keys);
    }
    public void StopRemovedRigs(IEnumerable<string> configuredRigIds)
    {
        var retained=new HashSet<string>(configuredRigIds??Array.Empty<string>(),StringComparer.Ordinal);
        foreach(string id in new List<string>(owned.Keys))
            if(!retained.Contains(id))
            {
                try { Stop(id); }
                catch(Exception error) { UnityEngine.Debug.LogWarning("Removed rig tracker stop failed for "+id+": "+error.Message); }
            }
    }
    public static string[] Arguments(SystemConfig rig)
    {
        if(rig==null || rig.tracker==null) throw new ArgumentException("Configure this rig's tracker executable and config first.");
        if(!TrackerBackend.IsFlight(rig.closedLoopMode) && !TrackerBackend.IsWalking(rig.closedLoopMode)) throw new ArgumentException("Optional launch supports Rekkefly and HejjéTrac.");
        if(rig.zmqPort<1 || rig.zmqPort>65535 || string.IsNullOrWhiteSpace(rig.vrId)) throw new ArgumentException("A rig ID and valid tracking port are required.");
        if(!Loopback(rig.zmqAddress)) throw new ArgumentException("Local tracker launch requires a localhost/127.0.0.1 tracking address.");
        string config=ResolveConfig(rig.tracker.configFile);
        if(!File.Exists(config)) throw new FileNotFoundException("Tracker calibration/config file does not exist.",config);
        if(TrackerBackend.IsFlight(rig.closedLoopMode))
        {
            var args=new List<string>{"--config",config,"--label",rig.vrId,"--zmq-port",rig.zmqPort.ToString(CultureInfo.InvariantCulture),"--supervised"};
            if(!string.IsNullOrWhiteSpace(rig.tracker.device)) { args.Add("--device");args.Add(rig.tracker.device); }
            if(rig.tracker.headless) args.Add("--headless");
            return args.ToArray();
        }
        if(rig.tracker.headless || !string.IsNullOrWhiteSpace(rig.tracker.device)) throw new ArgumentException("HejjéTrac device/source belongs in its calibration config; headless is a Rekkefly launch option.");
        var text=File.ReadAllText(config);
        string port=ConfigField(text,"zmq_port"), host=ConfigField(text,"zmq_host");
        if(!int.TryParse(port,NumberStyles.Integer,CultureInfo.InvariantCulture,out int configured) || configured!=rig.zmqPort)
            throw new ArgumentException("HejjéTrac config zmq_port must match this rig's tracking port ("+rig.zmqPort+"). MATREX does not rewrite calibration files.");
        if(!Loopback(string.IsNullOrWhiteSpace(host)?"127.0.0.1":host)) throw new ArgumentException("HejjéTrac config zmq_host must be loopback for local launch.");
        return new[]{"--config",config,"--windows","1","--control-stdio"};
    }
    public void Launch(SystemConfig rig)
    {
        if(rig==null) throw new ArgumentException("Select a configured rig first.");
        if(IsRunning(rig.vrId)) throw new InvalidOperationException("MATREX already owns a running tracker for "+rig.vrId+".");
        string[] args=Arguments(rig);
        string executable=rig.tracker.executablePath??"";
        if(!Path.IsPathFullyQualified(executable) || !File.Exists(executable)) throw new ArgumentException("Select an absolute installed tracker executable path. On macOS select the binary inside Contents/MacOS, rather than the .app folder.");
        string extension=Path.GetExtension(executable).ToLowerInvariant();
        if(extension==".bat" || extension==".cmd" || extension==".ps1" || extension==".sh") throw new ArgumentException("Select the native tracker executable directly; shell scripts are not launched.");
        if(owned.TryGetValue(rig.vrId,out var previous)) { previous.process.Dispose();owned.Remove(rig.vrId); }
        var start=new ProcessStartInfo { FileName=executable,Arguments=JoinArguments(args),WorkingDirectory=Path.GetDirectoryName(executable),UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true };
        var process=new Process { StartInfo=start,EnableRaisingEvents=true };
        var app=new Owned { process=process,walking=TrackerBackend.IsWalking(rig.closedLoopMode),backend=TrackerBackend.DisplayName(rig.closedLoopMode) };
        process.OutputDataReceived+=(_,e)=>{ if(e.Data!=null) app.detail=e.Data.Length>512?e.Data.Substring(0,512):e.Data; };
        process.ErrorDataReceived+=(_,e)=>{ if(e.Data!=null) app.detail=e.Data.Length>512?e.Data.Substring(0,512):e.Data; };
        try
        {
            if(!process.Start()) throw new InvalidOperationException("Tracker process did not start.");
            owned.Add(rig.vrId,app);process.BeginOutputReadLine();process.BeginErrorReadLine();
            // Native desktop starts paused under its typed stdio bridge. This queues start on session 1.
            if(app.walking) { process.StandardInput.WriteLine("{\"id\":1,\"command\":\"start\",\"session\":1}");process.StandardInput.Flush(); }
        }
        catch { if(owned.ContainsKey(rig.vrId)) Stop(rig.vrId);else process.Dispose();throw; }
    }
    public void Stop(string vrId)
    {
        if(!owned.TryGetValue(vrId,out var app)) return;
        var process=app.process;
        try
        {
            if(!process.HasExited)
            {
                try { process.StandardInput.WriteLine(app.walking?"{\"id\":2,\"command\":\"close\",\"session\":1}":"");process.StandardInput.Flush(); } catch(IOException) { } catch(InvalidOperationException) { }
                // Only the process explicitly launched for this rig is eligible for termination.
                if(!process.WaitForExit(750)) { process.Kill();process.WaitForExit(750); }
            }
        }
        finally { process.Dispose();owned.Remove(vrId); }
    }
    private void OnApplicationQuit() => StopAll();
    private void OnDestroy() { StopAll();if(instance==this) instance=null; }
    private void StopAll() { foreach(string id in new List<string>(owned.Keys)) { try { Stop(id); } catch(Exception error) { UnityEngine.Debug.LogWarning("Tracker stop failed for "+id+": "+error.Message); } } }
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
