#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public static class DesktopWorkspaceValidation
{
    public static void Run()
    {
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../desktop-workspace-validation"));
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Use a marked disposable project.");
        Directory.CreateDirectory(output);var checks=new List<string>();string failure=null;
        try
        {
            string user=Path.Combine(output,"user"),player=Path.Combine(output,"Configured player"),config=Path.Combine(player,"Config");
            Directory.CreateDirectory(user);Directory.CreateDirectory(config);
            string sentinel=Path.Combine(user,"workspace-sentinel.json");File.WriteAllText(sentinel,"operator selection remains unchanged");
            var method=typeof(DesktopQuickStart).GetMethod("ResolveWorkspaceRoot",BindingFlags.NonPublic|BindingFlags.Static);
            string Resolve(bool editor)=>(string)method.Invoke(null,new object[]{user,player,editor});
            void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);}
            string marker=Path.Combine(player,".matrex-configured-gui-build");if(File.Exists(marker))File.Delete(marker);
            Check(Resolve(false)==user,"Unmarked players retain global user selection");
            File.WriteAllText(Path.Combine(config,"system_config.json"),"{}");File.WriteAllText(Path.Combine(config,"sequenceConfig.json"),"{}");
            Check(Resolve(false)==user,"An unrelated Config directory cannot override user selection");
            File.WriteAllText(marker,"configured");
            Check(Resolve(false)==config,"Configured GUI player selects its own Config, including paths with spaces");
            Check(Resolve(true)==user,"Editor retains its existing isolated config behavior");
            Check(File.ReadAllText(sentinel)=="operator selection remains unchanged","Selecting a configured player preserves global operator preferences");
            File.Delete(Path.Combine(config,"sequenceConfig.json"));bool rejected=false;
            try{Resolve(false);}catch(TargetInvocationException e){rejected=e.InnerException is InvalidOperationException;}
            Check(rejected,"Incomplete configured players fail clearly rather than opening another experiment");
            Check(Resolve(true)==user,"Editor remains independent of an incomplete adjacent player profile");
        }
        catch(Exception error){failure=error.ToString();}
        File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(new{failure,checks},Formatting.Indented));
        EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
