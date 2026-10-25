using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using NetMQ;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
public static class NativeWireProbe
{
    static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static readonly List<object> cycles=new List<object>();
    static void Stop(ZmqListener listener){typeof(ZmqListener).GetMethod("StopListener",Hidden).Invoke(listener,null);UnityEngine.Object.DestroyImmediate(listener.gameObject);}
    static ZmqListener Open(int port)
    {
        var go=new GameObject("wire-rig");var listener=go.AddComponent<ZmqListener>();listener.address="127.0.0.1";listener.port=port;
        typeof(ZmqListener).GetField("backend",Hidden).SetValue(listener,ClosedLoopMode.Hejjetrac);
        typeof(ZmqListener).GetMethod("Start",Hidden).Invoke(listener,null);return listener;
    }
    public static void Run()
    {
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        try
        {
            int port=int.Parse(File.ReadAllText(root+"/port"));
            var process=Process.GetCurrentProcess();long before=process.WorkingSet64;int threadsBefore=process.Threads.Count;
            var listener=Open(port);Thread.Sleep(300);File.WriteAllText(root+"/ready","ready");
            var watch=Stopwatch.StartNew();bool sawJson=false,sawFT=false;
            while(watch.Elapsed.TotalSeconds<45)
            {
                var sample=listener.ReadRawSample();if(sample.hasPose){if(sample.ficTracRow)sawFT=true;else sawJson=true;}
                if(File.Exists(root+"/rekkefly-done") && !File.Exists(root+"/rekkefly-received.json"))
                    File.WriteAllText(root+"/rekkefly-received.json",JsonConvert.SerializeObject(new{sequence=sample.sequence,position=new{x=sample.position.x,y=sample.position.y,z=sample.position.z},rotation=new{x=sample.rotation.x,y=sample.rotation.y,z=sample.rotation.z},format=listener.WireFormat}));
                if(File.Exists(root+"/publishers-done"))break;Thread.Sleep(10);
            }
            Thread.Sleep(100);long packets=listener.ReadRawSample().sequence;string format=listener.WireFormat;Stop(listener);
            if(!sawJson || !sawFT || packets<350 || ZmqListener.ActiveWorkerCount!=0)throw new Exception("Native wire failure JSON="+sawJson+" FT="+sawFT+" packets="+packets+" workers="+ZmqListener.ActiveWorkerCount);
            for(int i=0;i<150;i++)
            {
                var current=Open(port);Thread.Sleep(5);Stop(current);
                if(ZmqListener.ActiveWorkerCount!=0)throw new Exception("Leaked listener worker at cycle "+i);
                if(i%25==0){process.Refresh();cycles.Add(new{cycle=i,bytes=process.WorkingSet64,threads=process.Threads.Count,workers=ZmqListener.ActiveWorkerCount});}
            }
            GC.Collect();GC.WaitForPendingFinalizers();Thread.Sleep(300);process.Refresh();
            File.WriteAllText(root+"/report.json",JsonConvert.SerializeObject(new{passed=true,netmq=typeof(NetMQ.Sockets.SubscriberSocket).Assembly.GetName().Version.ToString(),sawJson,sawFT,packets,format,cycles,workers=ZmqListener.ActiveWorkerCount,before,after=process.WorkingSet64,threadsBefore,threadsAfter=process.Threads.Count},Formatting.Indented));
            UnityEngine.Debug.Log("NATIVE_WIRE_PASSED");EditorApplication.Exit(0);
        }
        catch(Exception error){foreach(var listener in UnityEngine.Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) {try{Stop(listener);}catch{}}File.WriteAllText(root+"/report.json",JsonConvert.SerializeObject(new{passed=false,error=error.ToString()},Formatting.Indented));UnityEngine.Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
