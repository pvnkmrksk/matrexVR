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
        string temp=args[1];RuntimePaths.ConfigRoot=temp;
        try
        {
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
            Check(launch.SequenceEqual(new[]{"--config",config,"--label","VR10","--zmq-port","9871","--supervised","--device","Camera β \"2\"","--headless"}),"Rekkefly launch uses authoritative rig/port and supervised shutdown");
            rig.closedLoopMode=ClosedLoopMode.Hejjetrac;rig.tracker.device="";rig.tracker.headless=false;
            Check(TrackerProcessManager.Arguments(rig).SequenceEqual(new[]{"--config",config,"--windows","1","--control-stdio"}),"HejjéTrac launch uses typed native stdio control");
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
                    Reject(()=>manager.Launch(rig),"Duplicate owned launch rejected: "+mode);
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
                    Check(!TrackerProcessManager.IsRunning(rig.vrId),"Applying a removed/renamed rig stops its owned tracker: "+mode);
                    Check(!unrelated.HasExited,"Removing a rig preserves unrelated same-executable processes: "+mode);
                    manager.Stop(rig.vrId);Check(!TrackerProcessManager.IsRunning(rig.vrId),"Native owned process shutdown completes: "+mode);
                    Check(!unrelated.HasExited,"Unrelated same-executable process survives Stop: "+mode);
                }
                typeof(TrackerProcessManager).GetMethod("OnDisable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(manager,null);
                unrelated.StandardInput.WriteLine();unrelated.StandardInput.Flush();Check(unrelated.WaitForExit(2000),"Fixture unrelated process shuts down cleanly");
            }
            Console.WriteLine(JsonConvert.SerializeObject(new{checks=checks.Count,passed=checks},Formatting.Indented));return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
