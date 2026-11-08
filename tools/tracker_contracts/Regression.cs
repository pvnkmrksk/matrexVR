using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
            var rig=new SystemConfig{vrId="VR10",closedLoopMode=ClosedLoopMode.Rekkefly,zmqAddress="localhost",zmqPort=9871,tracker=new TrackerLaunchConfig{executablePath=args[0],configFile=config,device="Camera β \"2\"",headless=true,saveVideo=false}};
            var launch=TrackerProcessManager.Arguments(rig);
            Check(launch.SequenceEqual(new[]{"--config",config,"--label","VR10","--zmq-port","9871","--device","Camera β \"2\"","--headless"}),"Rekkefly launch uses authoritative rig/port and independent lifetime");
            rig.tracker.strictCameraFallback=true;
            Check(TrackerProcessManager.Arguments(rig).Last()=="--no-camera-fallback"&&!TrackerProcessManager.Arguments(rig).Contains("--supervised"),"Camera-only launch disables fallback without stdin supervision");
            rig.tracker.strictCameraFallback=false;
            rig.tracker.saveVideo=true;
            Check(!TrackerProcessManager.Arguments(rig).Contains("--record"),"Automatic experiment video never starts at tracker initialization");
            rig.tracker.recordingControl=TrackerRecordingControlMode.Manual;Check(TrackerProcessManager.Arguments(rig).Last()=="--record","Explicit manual video requests launch-time flight recording");rig.tracker.recordingControl=TrackerRecordingControlMode.Experiment;
            rig.tracker.saveVideo=false;
            rig.closedLoopMode=ClosedLoopMode.Hejjetrac;rig.tracker.device="";rig.tracker.headless=false;
            Check(TrackerProcessManager.Arguments(rig).SequenceEqual(new[]{"--config",config,"--windows","1"}),"HejjéTrac native acquisition starts without stdin control");
            rig.zmqPort=9872;Reject(()=>TrackerProcessManager.Arguments(rig),"HejjéTrac calibration port mismatch rejected without rewriting");rig.zmqPort=9871;
            rig.zmqAddress="192.168.1.1";Reject(()=>TrackerProcessManager.Arguments(rig),"Local launch rejects remote input address");rig.zmqAddress="localhost";
            File.WriteAllText(config,"zmq_port : 9871\nzmq_host : 0.0.0.0\n");Reject(()=>TrackerProcessManager.Arguments(rig),"Local launch rejects non-loopback tracker bind");File.WriteAllText(config,"zmq_port : 9871\nzmq_host : 127.0.0.1\n");
            Reject(()=>TrackerProcessManager.JoinArguments(new[]{"unsafe\nargument"}),"Arguments reject control characters");
            var legacyDirectory=Directory.CreateDirectory(Path.Combine(temp,"legacy-tracker")).FullName;
            foreach(string file in Directory.EnumerateFiles(Path.GetDirectoryName(args[0])))
                File.Copy(file,Path.Combine(legacyDirectory,Path.GetFileName(file)));
            string legacyExecutable=Path.Combine(legacyDirectory,Path.GetFileName(args[0]));
            var stockRig=new SystemConfig{vrId="VR-STOCK",closedLoopMode=ClosedLoopMode.Hejjetrac,zmqAddress="127.0.0.1",zmqPort=9871,
                tracker=new TrackerLaunchConfig{executablePath=legacyExecutable,configFile=config,saveVideo=true}};
            var legacyTracker=TrackerProcessManager.PrepareInstalledAsync(stockRig,false).GetAwaiter().GetResult();
            Check(!legacyTracker.supportsRecordingControl&&legacyTracker.recordingWarning!=null,"Older HejjéTrac CLI can track without receiving unsupported mailbox arguments");
            try
            {
                TrackerProcessManager.Instance.LaunchPrepared(legacyTracker);
                var receipt=Directory.EnumerateFiles(Path.Combine(UnityEngine.Application.persistentDataPath,"Companions","processes"),"*.json").Select(path=>JObject.Parse(File.ReadAllText(path))).Single(value=>(string)value["vrId"]==stockRig.vrId);
                Check(TrackerProcessManager.IsRunning(stockRig.vrId)&&receipt["controlDirectory"]?.Type==JTokenType.Null,"Legacy tracker launches independently with automatic video unavailable");
            }
            finally {TrackerProcessManager.Instance.Stop(stockRig.vrId);}
            var modern=TrackerProcessManager.PrepareInstalledAsync(new SystemConfig{vrId="VR-CAPABLE",closedLoopMode=ClosedLoopMode.Hejjetrac,zmqAddress="localhost",zmqPort=9871,
                tracker=new TrackerLaunchConfig{executablePath=args[0],configFile=config,saveVideo=true}},false).GetAwaiter().GetResult();
            Check(modern.supportsRecordingControl&&modern.recordingWarning==null,"The current upstream mailbox flags enable owned automatic video");
            Check(File.ReadAllText(config)=="zmq_port : 9871\nzmq_host : 127.0.0.1\n","Installed discovery and launch leave explicit calibration untouched");
            Check(TrackerInstallation.Find(ClosedLoopMode.FicTrac)==""&&TrackerInstallation.Find(ClosedLoopMode.Kinefly)=="","Legacy input names remain externally managed unless the operator selects an executable");
            Reject(()=>TrackerInstallation.ResolveSelection("relative-launcher",ClosedLoopMode.Hejjetrac),"Relative native launchers cannot resolve via the working directory");
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string installDirectory=Directory.CreateDirectory(Path.Combine(temp,"installed")).FullName;
                string installed=Path.Combine(installDirectory,"rekkefly");
                foreach(string file in Directory.EnumerateFiles(Path.GetDirectoryName(args[0])))
                    File.Copy(file,Path.Combine(installDirectory,Path.GetFileName(file)));
                File.Copy(args[0],installed);
                string previousPath=Environment.GetEnvironmentVariable("PATH");
                try
                {
                    Environment.SetEnvironmentVariable("PATH",installDirectory+Path.PathSeparator+previousPath);
                    Check(TrackerInstallation.Find(ClosedLoopMode.Rekkefly)==installed,"A native executable in an OS PATH directory is detected");
                    var resolved=TrackerProcessManager.PrepareInstalledAsync(new SystemConfig{vrId="VR-DETECTED",closedLoopMode=ClosedLoopMode.Rekkefly,zmqAddress="localhost",zmqPort=9871,
                        tracker=new TrackerLaunchConfig{configFile=config,saveVideo=true}},false).GetAwaiter().GetResult();
                    Check(resolved.rig.tracker.executablePath==installed&&resolved.rig.tracker.configFile==config,"Detection fills the effective launcher while retaining the independent calibration");
                }
                finally {Environment.SetEnvironmentVariable("PATH",previousPath);}
            }
            using(var unrelated=Process.Start(new ProcessStartInfo{FileName=args[0],UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true}))
            {
                var manager=TrackerProcessManager.Instance;
                typeof(TrackerProcessManager).GetMethod("OnEnable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(manager,null);
                foreach(var mode in new[]{ClosedLoopMode.Rekkefly,ClosedLoopMode.Hejjetrac})
                {
                    rig.closedLoopMode=mode;string log=Path.Combine(temp,"argv.json");Environment.SetEnvironmentVariable("MATREX_TRACKER_ARGUMENT_LOG",log);manager.Launch(rig);for(int wait=0;wait<100 && !File.Exists(log);wait++)Thread.Sleep(10);
                    Check(JArray.Parse(File.ReadAllText(log)).Select(t=>(string)t).Take(TrackerProcessManager.Arguments(rig).Length).SequenceEqual(TrackerProcessManager.Arguments(rig)),"Native argv preserves spaces, Unicode and quotes without shell: "+mode);File.Delete(log);
                    Check(TrackerProcessManager.IsRunning(rig.vrId),"Native owned mock process runs: "+mode);
                    var owned=TrackerProcessManager.RecordingProvenance(rig.vrId);
                    Check((bool)owned["identityVerified"]&&(bool)owned["processRunning"]&&(string)owned["backend"]==TrackerBackend.DisplayName(mode)&&(string)owned["configFile"]==config&&((string)owned["executableSha256"]).Length==64,"Run provenance captures exact owned process and config: "+mode);
                    Check((string)owned["actualInputSource"]=="unverified","Process ownership never claims a measured camera source: "+mode);
                    rig.tracker.saveVideo=true;TrackerProcessManager.RequireRecordingTransitionReady(rig);rig.tracker.saveVideo=false;string before=TrackerProcessManager.Status(rig.vrId);manager.Launch(rig);Check(TrackerProcessManager.Status(rig.vrId)==before,"Repeated Start reuses the same native process: "+mode);
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
            var unmanaged=new SystemConfig{vrId="VR-UNMANAGED",closedLoopMode=ClosedLoopMode.Rekkefly,tracker=new TrackerLaunchConfig{saveVideo=true}};
            string unsupportedId=Guid.NewGuid().ToString("N"),unsupportedRun=Path.Combine(temp,"unmanaged-run");Directory.CreateDirectory(unsupportedRun);
            var unsupported=TrackerProcessManager.BeginExperimentRecordingAsync(new[]{unmanaged},unsupportedRun,unsupportedId).GetAwaiter().GetResult();
            Check(unsupported.Warnings.Count==1&&unsupported.AcknowledgedRigCount==0&&(string)TrackerProcessManager.RecordingState(unmanaged.vrId)["state"]=="unavailable","Missing automatic recorder returns an explicit warning without blocking VR startup or claiming recording");
            TrackerProcessManager.EndExperimentRecordingAsync(unsupportedId,"stopped").GetAwaiter().GetResult();Check((string)TrackerProcessManager.RecordingState(unmanaged.vrId)["state"]=="unavailable","A recorder that never started stays unavailable without failing VR finalization");
            var endpointRigs=new List<SystemConfig>();
            try
            {
                foreach(string fixture in new[]{"unresponsive-a","unresponsive-b","healthy","reject-start","zero-frames"})
                {
                    string fixtureConfig=Path.Combine(temp,fixture+".toml");File.WriteAllText(fixtureConfig,"fixture-endpoint "+fixture);
                    var fixtureRig=new SystemConfig{vrId="VR-"+fixture,closedLoopMode=ClosedLoopMode.Rekkefly,zmqAddress="localhost",zmqPort=9860+endpointRigs.Count,tracker=new TrackerLaunchConfig{executablePath=args[0],configFile=fixtureConfig,saveVideo=true,headless=true}};
                    endpointRigs.Add(fixtureRig);TrackerProcessManager.Instance.Launch(fixtureRig);
                }
                string stateRoot=Path.Combine(UnityEngine.Application.persistentDataPath,"Companions/processes");
                for(int wait=0;wait<200;wait++)
                {if(endpointRigs.All(rig=>Directory.EnumerateFiles(stateRoot,"*.json").Select(path=>JObject.Parse(File.ReadAllText(path))).Where(value=>(string)value["vrId"]==rig.vrId).Any(value=>File.Exists(Path.Combine((string)value["controlDirectory"],"endpoint.json")))))break;Thread.Sleep(5);}
                string independentId=Guid.NewGuid().ToString("N"),independentRun=Path.Combine(temp,"independent-recorders");Directory.CreateDirectory(independentRun);
                var elapsed=Stopwatch.StartNew();
                var independent=TrackerProcessManager.BeginExperimentRecordingAsync(new[]{endpointRigs[0],endpointRigs[1],unmanaged,endpointRigs[2],endpointRigs[3]},independentRun,independentId).GetAwaiter().GetResult();
                Check(elapsed.Elapsed.TotalSeconds<1.8,"Two silent endpoints share a bounded parallel wait rather than delaying startup serially");
                Check(independent.Warnings.Count==4&&independent.AcknowledgedRigCount==1,"Missing, silent and rejected recorders produce independent warnings while a healthy later rig still starts");
                Check((string)TrackerProcessManager.RecordingState("VR-healthy")["state"]=="recording"&&(bool)TrackerProcessManager.RecordingState("VR-healthy")["acknowledged"],"Only the actual acknowledged filesystem peer is labeled recording");
                var savedStart=JObject.Parse(File.ReadAllText(Path.Combine(independentRun,"native_recording_start.json")));
                Check((string)savedStart["runId"]==independentId&&savedStart["warnings"].Count()==4,"Saved run report retains all recorder warnings and its immutable GUID");
                TrackerProcessManager.EndExperimentRecordingAsync(independentId,"stopped").GetAwaiter().GetResult();
                Check((string)TrackerProcessManager.RecordingState("VR-healthy")["state"]=="complete"&&(string)TrackerProcessManager.RecordingState("VR-reject-start")["state"]=="unavailable","End drains the exact accepted take without treating a definitive unaccepted Start as an unresolved recorder");
                var healthy=endpointRigs[2];
                var healthyReceipt=Directory.EnumerateFiles(stateRoot,"*.json").Select(path=>JObject.Parse(File.ReadAllText(path))).Single(value=>(string)value["vrId"]==healthy.vrId);
                string requestLog=Path.Combine((string)healthyReceipt["controlDirectory"],"requests.jsonl"),savedProfile=RuntimePaths.ConfigRoot;
                string otherConfig=Path.Combine(temp,"different-calibration.toml"),otherExecutable=Path.Combine(temp,"different-Mock");File.WriteAllText(otherConfig,"fixture-endpoint healthy");File.Copy(args[0],otherExecutable);
                var mismatches=new (string name,Action<SystemConfig> change)[]{
                    ("port",changed=>changed.zmqPort++),
                    ("remote-address",changed=>changed.zmqAddress="192.0.2.77"),
                    ("calibration",changed=>changed.tracker.configFile=otherConfig),
                    ("executable",changed=>changed.tracker.executablePath=otherExecutable),
                    ("device",changed=>changed.tracker.device="DifferentCamera"),
                    ("backend",changed=>changed.closedLoopMode=ClosedLoopMode.Hejjetrac),
                    ("camera-only",changed=>changed.tracker.strictCameraFallback=true),
                    ("profile",changed=>RuntimePaths.ConfigRoot=Path.Combine(temp,"another-profile")),
                    ("legacy-profile",changed=>RuntimePaths.ConfigRoot=Path.Combine(temp,"another-profile"))};
                foreach(var mismatch in mismatches)
                {
                    var desired=JsonConvert.DeserializeObject<SystemConfig>(JsonConvert.SerializeObject(healthy));
                    int beforeCommands=File.ReadAllLines(requestLog).Length;mismatch.change(desired);
                    string ownedReceiptPath=Directory.EnumerateFiles(stateRoot,"*.json").Single(path=>(string)JObject.Parse(File.ReadAllText(path))["vrId"]==healthy.vrId),savedReceipt=File.ReadAllText(ownedReceiptPath);
                    if(mismatch.name=="legacy-profile"){var legacy=JObject.Parse(savedReceipt);legacy.Remove("configRoot");File.WriteAllText(ownedReceiptPath,legacy.ToString());}
                    string mismatchId=Guid.NewGuid().ToString("N"),mismatchRun=Path.Combine(temp,"identity-"+mismatch.name);Directory.CreateDirectory(mismatchRun);
                    try
                    {
                        var report=TrackerProcessManager.BeginExperimentRecordingAsync(new[]{desired},mismatchRun,mismatchId).GetAwaiter().GetResult();
                        Check(report.Warnings.Count==1&&report.AcknowledgedRigCount==0&&(string)TrackerProcessManager.RecordingState(healthy.vrId)["state"]=="unavailable","Desired "+mismatch.name+" mismatch leaves the old owned tracker unacknowledged and warns while VR can continue");
                        TrackerProcessManager.EndExperimentRecordingAsync(mismatchId,"stopped").GetAwaiter().GetResult();
                        Check(File.ReadAllLines(requestLog).Length==beforeCommands,"Desired "+mismatch.name+" mismatch sends zero native commands, including status/start/stop");
                    }
                    finally{RuntimePaths.ConfigRoot=savedProfile;File.WriteAllText(ownedReceiptPath,savedReceipt);}
                }
                var aliasRig=JsonConvert.DeserializeObject<SystemConfig>(JsonConvert.SerializeObject(healthy));aliasRig.closedLoopMode=ClosedLoopMode.Kinefly;
                string aliasId=Guid.NewGuid().ToString("N"),aliasRun=Path.Combine(temp,"normalized-owner");Directory.CreateDirectory(aliasRun);
                var aliasReport=TrackerProcessManager.BeginExperimentRecordingAsync(new[]{aliasRig},aliasRun,aliasId).GetAwaiter().GetResult();
                Check(aliasReport.AcknowledgedRigCount==1&&aliasReport.Warnings.Count==0,"Unchanged owner still records when the flight backend uses its normalized legacy alias");
                TrackerProcessManager.EndExperimentRecordingAsync(aliasId,"stopped").GetAwaiter().GetResult();
                string operatorId=Guid.NewGuid().ToString("N"),operatorRun=Path.Combine(temp,"operator-recorder-stop");Directory.CreateDirectory(operatorRun);
                TrackerProcessManager.BeginExperimentRecordingAsync(new[]{healthy},operatorRun,operatorId).GetAwaiter().GetResult();
                int stopsBefore=File.ReadAllLines(requestLog).Count(line=>line=="record_stop");
                var overrideState=TrackerProcessManager.StopRecorderForOperatorAsync(healthy.vrId).GetAwaiter().GetResult();
                Check((string)overrideState["state"]=="override"&&(bool?)overrideState["native"]?["writersDrained"]==true,
                    "Agent/manual recorder Stop drains the exact owned take and latches override without quitting tracking");
                bool warned=false;try{TrackerProcessManager.EndExperimentRecordingAsync(operatorId,"stopped").GetAwaiter().GetResult();}catch(InvalidOperationException){warned=true;}
                Check(warned&&File.ReadAllLines(requestLog).Count(line=>line=="record_stop")==stopsBefore+1,
                    "Experiment Stop reuses the settled manual-stop task and sends no duplicate native stop");
                string emptyId=Guid.NewGuid().ToString("N"),emptyRun=Path.Combine(temp,"zero-frame-run");Directory.CreateDirectory(emptyRun);
                var empty=TrackerProcessManager.BeginExperimentRecordingAsync(new[]{endpointRigs[4]},emptyRun,emptyId).GetAwaiter().GetResult();
                Check(empty.Warnings.Count==1&&empty.AcknowledgedRigCount==0,"An armed recorder without a first frame becomes a warning after the shared preparation budget");
                TrackerProcessManager.EndExperimentRecordingAsync(emptyId,"stopped").GetAwaiter().GetResult();
                var emptyState=TrackerProcessManager.RecordingState(endpointRigs[4].vrId);
                Check((string)emptyState["state"]=="closed-unacknowledged"&&(bool?)emptyState["acknowledged"]==false&&(string)emptyState["native"]?["state"]=="complete","Physical closure of an empty take never becomes a false first-frame recording acknowledgement");
                Check(CompanionMediaArchive.LaunchedSources[endpointRigs[4].vrId].NoVideoFrames,"A closed empty take is retained as artifacts rather than a fabricated replay clip");

            }
            finally{foreach(var fixtureRig in endpointRigs)TrackerProcessManager.Instance.Stop(fixtureRig.vrId);}
            unmanaged.tracker.recordingControl=TrackerRecordingControlMode.Manual;
            string manualId=Guid.NewGuid().ToString("N"),manualRun=Path.Combine(temp,"manual-run");Directory.CreateDirectory(manualRun);
            TrackerProcessManager.BeginExperimentRecordingAsync(new[]{unmanaged},manualRun,manualId).GetAwaiter().GetResult();
            Check((string)TrackerProcessManager.RecordingState(unmanaged.vrId)["state"]=="manual"&&!(bool)TrackerProcessManager.RecordingState(unmanaged.vrId)["acknowledged"],"Explicit external Manual mode is operator-controlled without a false acknowledgement");
            TrackerProcessManager.EndExperimentRecordingAsync(manualId,"stopped").GetAwaiter().GetResult();
            unmanaged.tracker.saveVideo=false;
            for(int repetition=0;repetition<120;repetition++){string id=Guid.NewGuid().ToString("N"),dir=Path.Combine(temp,"repeat-"+id);Directory.CreateDirectory(dir);TrackerProcessManager.BeginExperimentRecordingAsync(new[]{unmanaged},dir,id).GetAwaiter().GetResult();TrackerProcessManager.EndExperimentRecordingAsync(id,"stopped").GetAwaiter().GetResult();}
            var retained=(System.Collections.IDictionary)typeof(TrackerProcessManager).GetField("recordingRuns",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
            Check(retained.Count==0,"120 settled repetitions retain no recording run or cancellation structures");
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
                    entries.Add(new JObject{["backend"]=backend,["platform"]=CompanionCatalog.Platform,["releaseTag"]="fixture",["packageRoot"]=backend,["executable"]="tracker",["executableSha256"]=hash,["demoConfig"]="demo.txt",["cameraConfig"]="camera.txt",["sampleVideo"]="sample.avi",["supportsNoCameraFallback"]=true,["supportsRecordingControl"]=true});
                }
                File.WriteAllText(Path.Combine(fixtureBundle,"catalog.json"),new JObject{["schemaVersion"]=1,["entries"]=entries}.ToString());Environment.SetEnvironmentVariable("MATREX_COMPANION_DIR",fixtureBundle);
                foreach(var mode in new[]{ClosedLoopMode.Rekkefly,ClosedLoopMode.Hejjetrac})
                {
                    var cameraRig=new SystemConfig{vrId="VR-VIDEO-"+mode,closedLoopMode=mode,zmqPort=9894,zmqAddress="localhost",tracker=new TrackerLaunchConfig()};
                    var canonical=CompanionCatalog.Prepare(cameraRig,true);File.AppendAllText(canonical.rig.tracker.configFile,"\n# shared calibration sentinel\n");string canonicalText=File.ReadAllText(canonical.rig.tracker.configFile);
                    Check(canonical.rig.tracker.saveVideo,"Video defaults on for bundled recorders: "+mode);cameraRig.tracker.saveVideo=true;var video=CompanionCatalog.Prepare(cameraRig,true);
                    Check(video.rig.tracker.saveVideo&&video.rig.tracker.configFile==canonical.rig.tracker.configFile,"Default video keeps one canonical calibration profile: "+mode);
                    Check(File.ReadAllText(canonical.rig.tracker.configFile).Contains("shared calibration sentinel"),"Recording option changes preserve canonical calibration edits: "+mode);
                    string recording=File.ReadAllText(video.rig.tracker.configFile);
                    Check(mode==ClosedLoopMode.Rekkefly?recording.Contains("\"mjpeg\"")&&recording.Contains("enabled = false"):recording.Contains("save_raw : n")&&recording.Contains("vid_codec : mjpg"),"Both bundled recorders prepare AVI options without recording at initialization: "+mode);
                    Check(Path.IsPathFullyQualified(video.rig.tracker.recordingDirectory),"Native recording root is explicit for later collection: "+mode);
                    File.AppendAllText(video.rig.tracker.configFile,"\n# video calibration edit\n");var again=CompanionCatalog.Prepare(cameraRig,true);
                    Check(File.ReadAllText(again.rig.tracker.configFile).Contains("video calibration edit"),"Repeated preparation keeps operator recording calibration: "+mode);cameraRig.tracker.saveVideo=false;var off=CompanionCatalog.Prepare(cameraRig,true);Check(!off.rig.tracker.saveVideo&&off.rig.tracker.configFile==again.rig.tracker.configFile&&File.ReadAllText(off.rig.tracker.configFile).Contains("video calibration edit"),"Explicit video opt-out retains the same newly edited calibration: "+mode);
                    cameraRig.tracker=null;var omitted=CompanionCatalog.Prepare(cameraRig,true);Check(omitted.rig.tracker.saveVideo,"Omitted tracker object uses the same video-on runtime default: "+mode);
                    var catalogEntry=entries.OfType<JObject>().Single(entry=>(string)entry["backend"]==(mode==ClosedLoopMode.Rekkefly?"rekkefly":"hejjetrac"));
                    catalogEntry["supportsRecordingControl"]=false;File.WriteAllText(Path.Combine(fixtureBundle,"catalog.json"),new JObject{["schemaVersion"]=1,["entries"]=entries}.ToString());
                    cameraRig.tracker=new TrackerLaunchConfig{saveVideo=true};var unavailable=CompanionCatalog.Prepare(cameraRig,true);
                    Check(!string.IsNullOrEmpty(unavailable.recordingWarning)&&!unavailable.supportsRecordingControl,"A companion without recording control prepares input with a warning instead of blocking: "+mode);
                    unavailable.rig.tracker.executablePath=args[0]; // Use the fixture apphost beside its runtime DLLs.
                    try{TrackerProcessManager.Instance.LaunchPrepared(unavailable);for(int wait=0;wait<200&&(bool?)TrackerProcessManager.RecordingProvenance(cameraRig.vrId)["identityVerified"]!=true;wait++)Thread.Sleep(5);Check(!CompanionMediaArchive.LaunchedSources.ContainsKey(cameraRig.vrId),"Unsupported automatic recording never creates a fake pending launched-media source: "+mode);}finally{TrackerProcessManager.Instance.Stop(cameraRig.vrId);}
                    catalogEntry["supportsRecordingControl"]=true;File.WriteAllText(Path.Combine(fixtureBundle,"catalog.json"),new JObject{["schemaVersion"]=1,["entries"]=entries}.ToString());

                }
            }
            finally{Environment.SetEnvironmentVariable("MATREX_COMPANION_DIR",originalBundle);}
            if(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MATREX_COMPANION_DIR")))
            {
                foreach(var mode in new[]{ClosedLoopMode.Rekkefly,ClosedLoopMode.Hejjetrac})
                {
                    var bundled=new SystemConfig{vrId="VR-BUNDLE",closedLoopMode=mode,zmqAddress="localhost",zmqPort=9893,tracker=new TrackerLaunchConfig{saveVideo=false}};
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
