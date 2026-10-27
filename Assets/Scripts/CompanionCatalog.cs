using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>Offline, checksum-pinned native packages. Immutable apps and editable calibration live separately.</summary>
public static class CompanionCatalog
{
    public sealed class Entry
    {
        public string backend,platform,releaseTag,repository,releaseUrl,archiveSha256,packageRoot;
        public string executable,executableSha256,demoConfig,cameraConfig,sampleVideo;
        public bool supportsNoCameraFallback,supportsSourceProvenance;
    }
    private sealed class Catalog { public int schemaVersion; public string catalogId; public List<Entry> entries; }
    public sealed class Prepared
    {
        public SystemConfig rig;
        public string backend,releaseTag,executableSha256;
        public bool demo,supportsNoCameraFallback,supportsSourceProvenance;
    }
    public static string Platform
    {
        get
        {
            string arch=RuntimeInformation.OSArchitecture==Architecture.Arm64?"arm64":"x64";
            if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macos-"+arch;
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows-"+(RuntimeInformation.ProcessArchitecture==Architecture.Arm64?"arm64":"x64");
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux-"+(RuntimeInformation.ProcessArchitecture==Architecture.Arm64?"arm64":"x64");
            return "unsupported";
        }
    }
    public static string Availability(SystemConfig rig)
    {
        try { string root;var entry=Find(rig,out root);return TrackerBackend.DisplayName(rig.closedLoopMode)+" "+entry.releaseTag+" included · opens independently"; }
        catch(Exception error) { return error.Message; }
    }
    public static bool IsAvailable(SystemConfig rig)
    { try { string root;Find(rig,out root);return true; } catch(Exception) { return false; } }
    private static Entry Find(SystemConfig rig,out string root)
    {
        if(rig==null) throw new ArgumentException("Choose flying or walking first.");
        string backend=TrackerBackend.IsFlight(rig.closedLoopMode)?"rekkefly":TrackerBackend.IsWalking(rig.closedLoopMode)?"hejjetrac":null;
        if(backend==null) throw new ArgumentException("Bundled tracking supports flying or walking.");
        var candidates=new List<string>();
        string supplied=Environment.GetEnvironmentVariable("MATREX_COMPANION_DIR");
        if(!string.IsNullOrWhiteSpace(supplied)) candidates.Add(supplied);
        candidates.Add(Path.Combine(Application.dataPath,"Companions"));
        candidates.Add(Path.GetFullPath(Path.Combine(Application.dataPath,"../Companions")));
        root=candidates.FirstOrDefault(p=>File.Exists(Path.Combine(p,"catalog.json")));
        if(root==null) throw new FileNotFoundException("Native trackers are not included in this source checkout. Use a bundled build or select an installed tracker in advanced settings.");
        root=Path.GetFullPath(root);
        var catalog=JsonConvert.DeserializeObject<Catalog>(File.ReadAllText(Path.Combine(root,"catalog.json")));
        if(catalog==null || catalog.schemaVersion!=1 || catalog.entries==null) throw new InvalidDataException("Unsupported companion catalog.");
        var entry=catalog.entries.FirstOrDefault(e=>e.backend==backend&&e.platform==Platform);
        if(entry==null) throw new FileNotFoundException("This bundle does not include "+backend+" for "+Platform+".");
        if(!Regex.IsMatch(entry.executableSha256??"", "^[0-9a-f]{64}$")) throw new InvalidDataException("Companion executable checksum is missing.");
        Child(Child(root,entry.packageRoot),entry.executable);
        return entry;
    }
    public static Prepared Prepare(SystemConfig source,bool demo)
    {
        TrackerProcessManager.RequireRecordingTransitionReady(source);
        string bundleRoot;var entry=Find(source,out bundleRoot);
        var clone=JsonConvert.DeserializeObject<SystemConfig>(JsonConvert.SerializeObject(source));
        return PrepareCore(clone,demo,entry,bundleRoot,Application.persistentDataPath,RuntimePaths.ConfigRoot);
    }
    public static Task<Prepared> PrepareAsync(SystemConfig source,bool demo)
    {
        TrackerProcessManager.RequireRecordingTransitionReady(source);
        // Every Unity path/property is captured on the caller's main thread. Only
        // hashing, copying and plain configuration I/O run on the worker.
        string bundleRoot;var entry=Find(source,out bundleRoot);
        string persistent=Application.persistentDataPath,configRoot=RuntimePaths.ConfigRoot;
        var clone=JsonConvert.DeserializeObject<SystemConfig>(JsonConvert.SerializeObject(source));
        return Task.Run(()=>PrepareCore(clone,demo,entry,bundleRoot,persistent,configRoot));
    }
    private static Prepared PrepareCore(SystemConfig source,bool demo,Entry entry,string bundleRoot,string persistent,string configRoot)
    {
        if(entry.backend=="rekkefly"&&!demo&&!entry.supportsNoCameraFallback)
            throw new InvalidOperationException("This bundled tracker cannot guarantee camera-only input. Use a patched compatible build, or configure an external tracker and verify its actual source.");
        string installed=Install(bundleRoot,entry,persistent);
        string trackerFolder=Path.Combine(configRoot,"Trackers");Directory.CreateDirectory(trackerFolder);
        string id=Regex.Replace(source.vrId??"VR1","[^A-Za-z0-9_-]","_");
        string config=Path.Combine(trackerFolder,id+"."+entry.backend+"."+(demo?"demo":"camera")+(entry.backend=="rekkefly"?".toml":".txt"));
        if(!File.Exists(config))
        {
            string text=File.ReadAllText(Child(installed,demo?entry.demoConfig:entry.cameraConfig));
            if(entry.backend=="rekkefly")
            {
                text=Toml(text,"","instance",Quote((demo?"Demo · ":"")+source.vrId));
                text=Toml(text,"output","enabled","false");
                text=Toml(text,"output","zmq_enabled","true");
                text=Toml(text,"output","zmq_bind",Quote("tcp://127.0.0.1:"+source.zmqPort));
                text=Toml(text,"recording","enabled","false");
            }
            else
            {
                text=Field(text,"src_fn",demo?Child(installed,entry.sampleVideo):"0");
                text=Field(text,"rust_instance_name",(demo?"Demo · ":"")+source.vrId);
                text=Field(text,"save_raw","n");text=Field(text,"save_debug","n");
                text=Field(text,"output_fn",Path.Combine(trackerFolder,id+".hejjetrac.output"));
                text=Field(text,"zmq_host","127.0.0.1");text=Field(text,"zmq_port",source.zmqPort.ToString(CultureInfo.InvariantCulture));
            }
            WriteAtomic(config,"# Created once by MATREX VR. Native tracker owns calibration edits.\n"+text,createOnly:true);
        }
        bool saveVideo=source.tracker?.saveVideo??true;
        string recordingDirectory="",recordingPrefix="";
        string recordingText=File.ReadAllText(config),updatedRecording=recordingText;
        if(entry.backend=="rekkefly")
        {
            updatedRecording=Toml(recordingText,"recording","enabled",saveVideo?"true":"false");
            if(saveVideo)
            {
                recordingDirectory=Path.Combine(persistent,"Companions","recordings","rekkefly",id);
                updatedRecording=Toml(Toml(updatedRecording,"recording","format",Quote("mjpeg")),"recording","directory",Quote(recordingDirectory));
            }
        }
        else
        {
            updatedRecording=Field(Field(recordingText,"save_raw",saveVideo?"y":"n"),"save_debug","n");
            if(saveVideo)
            {
                updatedRecording=Field(updatedRecording,"vid_codec","mjpg");
                string user=Environment.GetEnvironmentVariable("USERPROFILE")??Environment.GetEnvironmentVariable("HOME");
                recordingDirectory=string.IsNullOrEmpty(user)?Path.Combine(Directory.GetCurrentDirectory(),"hejjetrak-recordings"):Path.Combine(user,RuntimeInformation.IsOSPlatform(OSPlatform.OSX)?"Movies":"Videos","hejjetrak");
                recordingPrefix=id+".hejjetrac.output-";
            }
        }
        // One calibration per rig/source mode. Only recording fields change; native
        // calibration edits never diverge between recording and non-recording copies.
        if(updatedRecording!=recordingText)WriteAtomic(config,updatedRecording,expected:recordingText);
        if(entry.backend=="hejjetrac")
        {
            // This generated profile's transport follows its rig; geometry and calibration text stay untouched.
            string text=File.ReadAllText(config),updated=Field(Field(text,"zmq_port",source.zmqPort.ToString(CultureInfo.InvariantCulture)),"zmq_host","127.0.0.1");
            if(text!=updated) WriteAtomic(config,updated,expected:text);
        }
        var clone=source; // Snapshot was already cloned on the caller's main thread.
        clone.tracker=new TrackerLaunchConfig{executablePath=Child(installed,entry.executable),configFile=config,headless=false,strictCameraFallback=entry.supportsNoCameraFallback&&!demo,saveVideo=saveVideo,recordingDirectory=recordingDirectory,recordingPrefix=recordingPrefix};
        return new Prepared{rig=clone,backend=entry.backend,releaseTag=entry.releaseTag,executableSha256=entry.executableSha256,demo=demo,supportsNoCameraFallback=entry.supportsNoCameraFallback,supportsSourceProvenance=entry.supportsSourceProvenance};
    }
    private static void WriteAtomic(string path,string text,string expected=null,bool createOnly=false)
    {
        string temporary=path+".matrex-writing";
        using(var configLock=new FileStream(path+".matrex-lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
        {
            if(createOnly&&File.Exists(path))return;
            if(expected!=null&&File.ReadAllText(path)!=expected)throw new IOException("The native calibration changed while preparing. Save its edits, then retry Start.");
            try
            {
                File.WriteAllText(temporary,text);
                if(File.Exists(path))File.Replace(temporary,path,null);
                else File.Move(temporary,path);
            }
            finally { if(File.Exists(temporary))File.Delete(temporary); }
        }
    }
    private static string Install(string bundle,Entry entry,string persistent)
    {
        string source=Child(bundle,entry.packageRoot);
        string root=Path.Combine(persistent,"Companions",entry.backend,entry.releaseTag+"-"+entry.executableSha256.Substring(0,12),entry.platform);
        string executable=Child(root,entry.executable);
        if(File.Exists(executable))
        {
            if(Sha(executable)!=entry.executableSha256) throw new InvalidDataException("Installed tracker changed. Close it and remove this incomplete installation before retrying: "+root);
            return root;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(root));
        using(var installLock=new FileStream(root+".install.lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
        {
            if(File.Exists(executable)&&Sha(executable)==entry.executableSha256) return root;
            if(Directory.Exists(root)) throw new InvalidDataException("Incomplete companion installation: "+root);
            string temporary=root+".installing";
            if(Directory.Exists(temporary)) Directory.Delete(temporary,true);
            Directory.CreateDirectory(temporary);
            try
            {
                if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) CopyNative("/usr/bin/ditto",new[]{source,temporary});
                else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) CopyNative("/bin/cp",new[]{"-a",source+"/.",temporary});
                else CopyFiles(source,temporary);
                if(Sha(Child(temporary,entry.executable))!=entry.executableSha256) throw new InvalidDataException("Bundled tracker executable checksum mismatch.");
                File.WriteAllText(Path.Combine(temporary,"matrex-install.json"),JsonConvert.SerializeObject(entry,Formatting.Indented));
                Directory.Move(temporary,root);
            }
            catch { if(Directory.Exists(temporary))Directory.Delete(temporary,true);throw; }
        }
        return root;
    }
    private static void CopyNative(string program,string[] args)
    {
        using(var process=Process.Start(new ProcessStartInfo{FileName=program,Arguments=TrackerProcessManager.JoinArguments(args),UseShellExecute=false,CreateNoWindow=true}))
        { if(process==null||!process.WaitForExit(60000)||process.ExitCode!=0) throw new IOException("Could not install bundled native tracker."); }
    }
    private static void CopyFiles(string source,string destination)
    {
        foreach(string directory in Directory.GetDirectories(source,"*",SearchOption.AllDirectories))Directory.CreateDirectory(Path.Combine(destination,Path.GetRelativePath(source,directory)));
        foreach(string file in Directory.GetFiles(source,"*",SearchOption.AllDirectories))File.Copy(file,Path.Combine(destination,Path.GetRelativePath(source,file)),false);
    }
    private static string Child(string root,string relative)
    {
        if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative))throw new InvalidDataException("Invalid companion path.");
        string parent=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        string result=Path.GetFullPath(Path.Combine(parent,relative));
        if(!result.StartsWith(parent,StringComparison.Ordinal))throw new InvalidDataException("Companion path leaves its package.");
        return result;
    }
    private static string Sha(string path) { using(var input=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant(); }
    private static string Quote(string value)=>JsonConvert.SerializeObject(value);
    private static string Field(string text,string key,string value)
    {
        string pattern=@"(?m)^(\s*"+Regex.Escape(key)+@"\s*:\s*)[^\r\n]*";
        return Regex.IsMatch(text,pattern)?Regex.Replace(text,pattern,m=>m.Groups[1].Value+value):text.TrimEnd()+"\n"+key+" : "+value+"\n";
    }
    private static string Toml(string text,string section,string key,string value)
    {
        var lines=text.Replace("\r\n","\n").Split('\n');string current="";
        for(int i=0;i<lines.Length;i++)
        {
            var header=Regex.Match(lines[i],@"^\s*\[([^\]]+)\]");if(header.Success)current=header.Groups[1].Value;
            if(current==section&&Regex.IsMatch(lines[i],@"^\s*"+Regex.Escape(key)+@"\s*=")) { lines[i]=key+" = "+value;return string.Join("\n",lines); }
        }
        if(section.Length==0)return key+" = "+value+"\n"+text;
        return text.TrimEnd()+"\n["+section+"]\n"+key+" = "+value+"\n";
    }
}
