using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
public static class HeartbeatProbe
{
    public static void Run()
    {
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        try
        {
            var main=new GameObject("Context fixture").AddComponent<MainController>();MainController.Instance=main;
            main.SystemConfigs.Add("VR10",new SystemConfig{vrId="VR10",speciesName="default species",closedLoopMode=ClosedLoopMode.Hejjetrac,displayOrder="LFR",targetDisplay=1,ledPanelWidth=128,ledPanelHeight=128,zmqPort=9871});
            main.CurrentAnimalMetadata=new AnimalData{ExperimentName="Native heartbeat experiment",Comments="Private operator note must stay private",Animals=new List<AnimalMetadata>{new AnimalMetadata{VR="VR10",AnimalID="animal β",SpeciesName="Agrotis infusa"}}};
            main.step=new SequenceStep{parameters=new Dictionary<string,object>{{"configFile",@"C:\private\experiment.json"},{"design",@"\\private-server\share\design.csv"}}};
            main.TelemetrySettings=new TelemetryConfig{enabled=true,bindAddress="127.0.0.1",port=int.Parse(File.ReadAllText(root+"/telemetry-port")),topic="matrex.telemetry.v2",rateHz=.2f,contextIntervalSeconds=.25f};
            var component=new GameObject("Telemetry").AddComponent<ExperimentTelemetry>();component.Configure();Thread.Sleep(500);
            var late=typeof(ExperimentTelemetry).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
            var close=typeof(ExperimentTelemetry).GetMethod("Close",BindingFlags.NonPublic|BindingFlags.Instance);
            File.WriteAllText(root+"/heartbeat-ready","ready");var watch=Stopwatch.StartNew();
            while(watch.Elapsed.TotalSeconds<3){late.Invoke(component,null);Thread.Sleep(10);}
            close.Invoke(component,null);
            int before=Process.GetCurrentProcess().Threads.Count;
            for(int cycle=0;cycle<25;cycle++){component.Configure();Thread.Sleep(5);close.Invoke(component,null);}
            Thread.Sleep(100);int after=Process.GetCurrentProcess().Threads.Count;
            if(after>before+1)throw new Exception("Publisher thread growth: "+before+"→"+after);
            File.WriteAllText(root+"/heartbeat-native.json",JsonConvert.SerializeObject(new{passed=true,seconds=3,snapshotRateHz=.2,contextIntervalSeconds=.25,publisherCycles=25,threadsBefore=before,threadsAfter=after},Formatting.Indented));
            EditorApplication.Exit(0);
        }
        catch(Exception error){File.WriteAllText(root+"/heartbeat-native.json",JsonConvert.SerializeObject(new{passed=false,error=error.ToString()},Formatting.Indented));UnityEngine.Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
