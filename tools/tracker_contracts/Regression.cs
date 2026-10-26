using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
public static class Regression
{
    static readonly List<string> checks=new List<string>();
    static void Check(bool value,string message) {if(!value)throw new Exception(message);checks.Add(message);}
    static void Reject(Action action,string message) {bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}catch(FormatException){rejected=true;}catch(JsonException){rejected=true;}catch(InvalidOperationException){rejected=true;}Check(rejected,message);}
    public static int Main(string[] args)
    {
        string temp=args[1];RuntimePaths.ConfigRoot=temp;UnityEngine.Application.persistentDataPath=Path.Combine(temp,"persistent");
        try
        {
            if(args.Length>2)
            {
                string id=args[2]=="--parent-exit"?"VR-PARENT-EXIT":"VR-PARENT-KILL";
                string parentConfig=Path.Combine(temp,"independent-parent.toml");File.WriteAllText(parentConfig,"mock config");
                TrackerProcessManager.Instance.Launch(new SystemConfig{vrId=id,closedLoopMode=ClosedLoopMode.Rekkefly,zmqAddress="localhost",zmqPort=9881,tracker=new TrackerLaunchConfig{executablePath=args[0],configFile=parentConfig,headless=true}});
                File.WriteAllText(Path.Combine(temp,id+".ready"),"ready");
                if(args[2]=="--parent-exit")return 0;
                while(true)Thread.Sleep(50);
            }
            foreach(var entry in new[]{("FicTrac",0),("Kinefly",1),("Tirbala",2),("Hejjetrac",3),("Rekkefly",4),("HejjéTrac",3),("hejjetrak",3),("0",0),("1",1),("3",3),("4",4)})
            {var mode=TrackerBackend.Parse(entry.Item1);Check((int)mode==entry.Item2,"Stable backend value: "+entry.Item1);Check((int)JsonConvert.DeserializeObject<ClosedLoopMode>(char.IsDigit(entry.Item1[0])?entry.Item1:JsonConvert.SerializeObject(entry.Item1))==entry.Item2,"Backend JSON: "+entry.Item1);}
            Reject(()=>TrackerBackend.Parse("5"),"Reject unknown backend integer");Reject(()=>JsonConvert.DeserializeObject<ClosedLoopMode>("null"),"Reject null backend");
            Check(TrackerBackend.IsFlight(ClosedLoopMode.Rekkefly)&&TrackerBackend.IsFlight(ClosedLoopMode.Kinefly),"Both flight names share wire semantics");
            Check(TrackerBackend.IsWalking(ClosedLoopMode.Hejjetrac)&&TrackerBackend.IsWalking(ClosedLoopMode.FicTrac),"Both walking names share wire semantics");
            Check(TrackerBackend.DisplayName(ClosedLoopMode.FicTrac)=="HejjéTrac"&&TrackerBackend.DisplayName(ClosedLoopMode.Kinefly)=="Rekkefly","Canonical display labels preserve old enum values");
            var columns=Enumerable.Repeat("0",25).ToArray();columns[0]="17";columns[14]="1.25";columns[15]="-2.5";columns[16]=(Math.PI/2).ToString("R",CultureInfo.InvariantCulture);columns[22]="18";
            string ft="FT, "+string.Join(", ",columns)+"\n";
            foreach(var mode in new[]{ClosedLoopMode.FicTrac,ClosedLoopMode.Hejjetrac})
            {var sample=TrackerWireDecoder.Decode(ft,mode);Check(sample.ficTracRow&&sample.position.x==1.25f&&sample.position.y==-2.5f&&sample.position.z==0,"FT sphere-radian forward/east positions: "+mode);Check(Math.Abs(sample.rotation.y-Math.PI/2)<1e-6&&Math.Abs(sample.orientation.eulerAngles.y-90)<1e-4,"FT heading radians convert to quaternion degrees: "+mode);}
            Reject(()=>TrackerWireDecoder.Decode(ft,ClosedLoopMode.Rekkefly),"Reject walking rows on flight input");
            Reject(()=>TrackerWireDecoder.Decode("FT, 1, 2",ClosedLoopMode.Hejjetrac),"Reject incomplete FT rows");
            columns[14]="NaN";Reject(()=>TrackerWireDecoder.Decode("FT, "+string.Join(",",columns),ClosedLoopMode.Hejjetrac),"Reject nonfinite FT positions");columns[14]="1e300";Reject(()=>TrackerWireDecoder.Decode("FT, "+string.Join(",",columns),ClosedLoopMode.Hejjetrac),"Reject overflowing runtime FT positions");columns[14]="1";columns[22]="-1";Reject(()=>TrackerWireDecoder.Decode("FT, "+string.Join(",",columns),ClosedLoopMode.Hejjetrac),"Reject invalid FT sequence");
            foreach(var mode in new[]{ClosedLoopMode.Kinefly,ClosedLoopMode.Rekkefly,ClosedLoopMode.FicTrac})
            {var sample=TrackerWireDecoder.Decode("{\"x\":0.3,\"y\":-0.2,\"yaw\":0.5}",mode);Check(sample.position.x==.3f&&sample.position.y==-.2f&&sample.rotation.y==.5f&&!sample.ficTracRow,"Legacy JSON raw flight radians preserved: "+mode);}
            Reject(()=>TrackerWireDecoder.Decode("{\"yaw\":\"NaN\"}",ClosedLoopMode.Kinefly),"Reject nonfinite JSON pose");
            Check(TrackerWireDecoder.IsPayload("  FT, 0")&&!TrackerWireDecoder.IsPayload("tracking-topic"),"Single-frame discriminator preserves separate legacy topic behavior");
            string config=Path.Combine(temp,"rig config β \"quoted\".txt");File.WriteAllText(config,"zmq_host : 127.0.0.1\nzmq_port : 9871\n");
            var rig=new SystemConfig{vrId="VR10",closedLoopMode=ClosedLoopMode.Rekkefly,zmqAddress="localhost",zmqPort=9871,tracker=new TrackerLaunchConfig{executablePath=args[0],configFile=config,device="Camera β \"2\"",headless=true}};
            var launch=TrackerProcessManager.Arguments(rig);
            Check(launch.SequenceEqual(new[]{"--config",config,"--label","VR10","--zmq-port","9871","--device","Camera β \"2\"","--headless"}),"Rekkefly launch uses authoritative rig/port and independent lifetime");
            rig.tracker.strictCameraFallback=true;
            Check(TrackerProcessManager.Arguments(rig).Last()=="--no-camera-fallback"&&!TrackerProcessManager.Arguments(rig).Contains("--supervised"),"Camera-only launch disables fallback without stdin supervision");
            rig.tracker.strictCameraFallback=false;
            rig.tracker.saveVideo=true;
            Check(TrackerProcessManager.Arguments(rig).Last()=="--record","Saved camera video is explicit opt-in on detached flight launch");
            rig.tracker.saveVideo=false;
            rig.closedLoopMode=ClosedLoopMode.Hejjetrac;rig.tracker.device="";rig.tracker.headless=false;
            Check(TrackerProcessManager.Arguments(rig).SequenceEqual(new[]{"--config",config,"--windows","1"}),"HejjéTrac native acquisition starts without stdin control");
            rig.zmqPort=9872;Reject(()=>TrackerProcessManager.Arguments(rig),"HejjéTrac calibration port mismatch rejected without rewriting");rig.zmqPort=9871;
            rig.zmqAddress="192.168.1.1";Reject(()=>TrackerProcessManager.Arguments(rig),"Local launch rejects remote input address");rig.zmqAddress="localhost";
            File.WriteAllText(config,"zmq_port : 9871\nzmq_host : 0.0.0.0\n");Reject(()=>TrackerProcessManager.Arguments(rig),"Local launch rejects non-loopback tracker bind");File.WriteAllText(config,"zmq_port : 9871\nzmq_host : 127.0.0.1\n");
            Reject(()=>TrackerProcessManager.JoinArguments(new[]{"unsafe\nargument"}),"Arguments reject control characters");
            using(var unrelated=Process.Start(new ProcessStartInfo{FileName=args[0],UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true}))
            {
                var manager=TrackerProcessManager.Instance;
                typeof(TrackerProcessManager).GetMethod("OnEnable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(manager,null);
                foreach(var mode in new[]{ClosedLoopMode.Rekkefly,ClosedLoopMode.Hejjetrac})
                {
                    rig.closedLoopMode=mode;string log=Path.Combine(temp,"argv.json");Environment.SetEnvironmentVariable("MATREX_TRACKER_ARGUMENT_LOG",log);manager.Launch(rig);for(int wait=0;wait<100 && !File.Exists(log);wait++)Thread.Sleep(10);
                    Check(JArray.Parse(File.ReadAllText(log)).Select(t=>(string)t).SequenceEqual(TrackerProcessManager.Arguments(rig)),"Native argv preserves spaces, Unicode and quotes without shell: "+mode);File.Delete(log);
                    Check(TrackerProcessManager.IsRunning(rig.vrId),"Native owned mock process runs: "+mode);
                    var owned=TrackerProcessManager.RecordingProvenance(rig.vrId);
                    Check((bool)owned["identityVerified"]&&(bool)owned["processRunning"]&&(string)owned["backend"]==TrackerBackend.DisplayName(mode)&&(string)owned["configFile"]==config&&((string)owned["executableSha256"]).Length==64,"Run provenance captures exact owned process and config: "+mode);
                    Check((string)owned["actualInputSource"]=="unverified","Process ownership never claims a measured camera source: "+mode);
                    string before=TrackerProcessManager.Status(rig.vrId);manager.Launch(rig);Check(TrackerProcessManager.Status(rig.vrId)==before,"Repeated Start reuses the same native process: "+mode);
                    string backend=TrackerBackend.DisplayName(mode);
                    rig.closedLoopMode=mode==ClosedLoopMode.Rekkefly?ClosedLoopMode.Hejjetrac:ClosedLoopMode.Rekkefly;
                    Check(TrackerProcessManager.Status(rig.vrId).StartsWith(backend+" running"),"Owned process status retains its original backend after selection edits: "+mode);
                    rig.closedLoopMode=mode;
                    var configuredTracker=rig.tracker;rig.tracker=null;
                    manager.StopRemovedRigs(new[]{rig.vrId});
                    Check(TrackerProcessManager.IsRunning(rig.vrId),"Cleared launch settings do not lose ownership of a retained rig: "+mode);
                    rig.tracker=configuredTracker;
                    MainController.Instance=new MainController{SystemConfigs=new Dictionary<string,SystemConfig>{{"VR11",new SystemConfig{vrId="VR11"}}}};
                    MainController.RaiseConfigurationChanged();
                    Check(TrackerProcessManager.IsRunning(rig.vrId),"Configuration removal never kills an independent tracker: "+mode);
                    typeof(TrackerProcessManager).GetMethod("OnApplicationQuit",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(manager,null);
                    Check(TrackerProcessManager.IsRunning(rig.vrId),"Unity quit leaves native tracker running: "+mode);
                    typeof(TrackerProcessManager).GetMethod("OnDestroy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(manager,null);
                    manager=TrackerProcessManager.Instance;manager.Launch(rig);
                    Check(TrackerProcessManager.Status(rig.vrId)==before,"A new Unity manager reuses the persisted process receipt: "+mode);
                    Check(!unrelated.HasExited,"Removing a rig preserves unrelated same-executable processes: "+mode);
                    manager.Stop(rig.vrId);Check(!TrackerProcessManager.IsRunning(rig.vrId),"Native owned process shutdown completes: "+mode);
                    var stopped=TrackerProcessManager.RecordingProvenance(rig.vrId);
                    Check(!(bool)stopped["identityVerified"]&&!(bool)stopped["processRunning"],"Stopped or unmanaged tracker is not a verified recording source: "+mode);
                    Check(!unrelated.HasExited,"Unrelated same-executable process survives Stop: "+mode);
                }
                typeof(TrackerProcessManager).GetMethod("OnDisable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(manager,null);
                unrelated.Kill();Check(unrelated.WaitForExit(2000),"Fixture unrelated process shuts down cleanly");
            }
            string originalBundle=Environment.GetEnvironmentVariable("MATREX_COMPANION_DIR");
            try
            {
                string fixtureBundle=Path.Combine(temp,"recording-catalog");Directory.CreateDirectory(fixtureBundle);var entries=new JArray();
                foreach(string backend in new[]{"rekkefly","hejjetrac"})
                {
                    string package=Path.Combine(fixtureBundle,backend);Directory.CreateDirectory(package);File.Copy(args[0],Path.Combine(package,"tracker"));
                    string template=backend=="rekkefly"?"[recording]\nenabled = false\nformat = \"png\"\ndirectory = \"original\"\n[output]\nzmq_enabled = true\n":"save_raw : n\nsave_debug : n\nvid_codec : h264\nzmq_port : 9871\n";
                    File.WriteAllText(Path.Combine(package,"demo.txt"),template);File.WriteAllText(Path.Combine(package,"camera.txt"),template);
                    string hash;using(var sha=System.Security.Cryptography.SHA256.Create())using(var input=File.OpenRead(args[0]))hash=BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();
                    entries.Add(new JObject{["backend"]=backend,["platform"]=CompanionCatalog.Platform,["releaseTag"]="fixture",["packageRoot"]=backend,["executable"]="tracker",["executableSha256"]=hash,["demoConfig"]="demo.txt",["cameraConfig"]="camera.txt",["sampleVideo"]="sample.avi",["supportsNoCameraFallback"]=true});
                }
                File.WriteAllText(Path.Combine(fixtureBundle,"catalog.json"),new JObject{["schemaVersion"]=1,["entries"]=entries}.ToString());Environment.SetEnvironmentVariable("MATREX_COMPANION_DIR",fixtureBundle);
                foreach(var mode in new[]{ClosedLoopMode.Rekkefly,ClosedLoopMode.Hejjetrac})
                {
                    var cameraRig=new SystemConfig{vrId="VR-VIDEO-"+mode,closedLoopMode=mode,zmqPort=9894,zmqAddress="localhost",tracker=new TrackerLaunchConfig()};
                    var canonical=CompanionCatalog.Prepare(cameraRig,true);string canonicalText=File.ReadAllText(canonical.rig.tracker.configFile);
                    Check(!canonical.rig.tracker.saveVideo,"Video defaults off: "+mode);cameraRig.tracker.saveVideo=true;var video=CompanionCatalog.Prepare(cameraRig,true);
                    Check(video.rig.tracker.saveVideo&&video.rig.tracker.configFile!=canonical.rig.tracker.configFile,"Opt-in video uses separate calibration profile: "+mode);
                    Check(File.ReadAllText(canonical.rig.tracker.configFile)==canonicalText,"Opt-in video preserves canonical calibration bytes: "+mode);
                    string recording=File.ReadAllText(video.rig.tracker.configFile);
                    Check(mode==ClosedLoopMode.Rekkefly?recording.Contains("\"mjpeg\"")&&recording.Contains("enabled = true"):recording.Contains("save_raw : y")&&recording.Contains("vid_codec : mjpg"),"Both bundled recorders prefer directly readable intra-frame AVI: "+mode);
                    Check(Path.IsPathFullyQualified(video.rig.tracker.recordingDirectory),"Native recording root is explicit for later collection: "+mode);
                    File.AppendAllText(video.rig.tracker.configFile,"\n# video calibration edit\n");var again=CompanionCatalog.Prepare(cameraRig,true);
                    Check(File.ReadAllText(again.rig.tracker.configFile).Contains("video calibration edit"),"Repeated preparation keeps operator recording calibration: "+mode);
                }
            }
            finally{Environment.SetEnvironmentVariable("MATREX_COMPANION_DIR",originalBundle);}
            if(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MATREX_COMPANION_DIR")))
            {
                foreach(var mode in new[]{ClosedLoopMode.Rekkefly,ClosedLoopMode.Hejjetrac})
                {
                    var bundled=new SystemConfig{vrId="VR-BUNDLE",closedLoopMode=mode,zmqAddress="localhost",zmqPort=9893};
                    Check(CompanionCatalog.IsAvailable(bundled),"Pinned native bundle is available for host architecture: "+mode);
                    var first=CompanionCatalog.Prepare(bundled,true);string content=File.ReadAllText(first.rig.tracker.configFile);
                    File.AppendAllText(first.rig.tracker.configFile,"\n# operator calibration note β\n");
                    var preparation=CompanionCatalog.PrepareAsync(bundled,true);
                    Check(preparation.Wait(10000),"Asynchronous bundle preparation completes without a Unity-thread dependency: "+mode);
                    var again=preparation.GetAwaiter().GetResult();
                    Check(first.rig.tracker.executablePath==again.rig.tracker.executablePath,"Repeated bundle preparation reuses a stable installed app identity: "+mode);
                    Check(File.ReadAllText(again.rig.tracker.configFile).Contains("operator calibration note β"),"Bundle preparation preserves existing operator calibration: "+mode);
                    Check(!again.rig.tracker.executablePath.Contains(".installing"),"Native app installs atomically before launch: "+mode);
                    Check(!again.rig.tracker.strictCameraFallback,"Explicit demo does not request strict camera acquisition: "+mode);
                    if(TrackerBackend.IsFlight(mode)&&!again.supportsNoCameraFallback)
                        Reject(()=>CompanionCatalog.Prepare(bundled,false),"Old bundled flight tracker cannot silently substitute sample data for camera input");
                    else
                    {
                        var live=CompanionCatalog.Prepare(bundled,false);
                        Check(live.rig.tracker.configFile!=again.rig.tracker.configFile,"Live calibration is separate from hardware-free demo: "+mode);
                        Check(live.rig.tracker.strictCameraFallback==TrackerBackend.IsFlight(mode),"Capable flight tracker starts camera-only while walking keeps its native source: "+mode);
                    }
                    Check(!TrackerProcessManager.Arguments(again.rig).Any(a=>a=="--supervised"||a=="--control-stdio"),"Bundled GUI launch has no parent-lifetime coupling: "+mode);
                }
            }
            foreach(string policy in new[]{"--parent-exit","--parent-kill"})
            {
                string id=policy=="--parent-exit"?"VR-PARENT-EXIT":"VR-PARENT-KILL",ready=Path.Combine(temp,id+".ready");
                string host=Process.GetCurrentProcess().MainModule.FileName;
                string assembly=System.Reflection.Assembly.GetExecutingAssembly().Location;
                using(var parent=Process.Start(new ProcessStartInfo{FileName=host,Arguments=TrackerProcessManager.JoinArguments(new[]{assembly,args[0],temp,policy}),UseShellExecute=false}))
                {
                    for(int wait=0;wait<200&&!File.Exists(ready);wait++)Thread.Sleep(10);
                    Check(File.Exists(ready),"Separate launcher process created its native companion: "+policy);
                    if(policy=="--parent-kill")parent.Kill();
                    Check(parent.WaitForExit(5000),"Launcher process ended: "+policy);
                    Check(TrackerProcessManager.IsRunning(id),"Native companion survives actual launcher exit/crash: "+policy);
                    TrackerProcessManager.Instance.Stop(id);
                    Check(!TrackerProcessManager.IsRunning(id),"Explicit Stop cleans up detached native fixture: "+policy);
                }
            }
            Console.WriteLine(JsonConvert.SerializeObject(new{checks=checks.Count,passed=checks},Formatting.Indented));return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
