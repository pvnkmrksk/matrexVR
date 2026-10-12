using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Immutable config/input snapshots with a manifest. File IO runs only at load/phase boundaries.</summary>
public sealed class RunConfigArchive
{
    private sealed class Snapshot
    {
        public string sourcePath, sourceRelativePath, sha256, snapshotPath, readablePath;
        public long bytes;
        public bool consumed;
        public DateTimeOffset firstCapturedUtc=DateTimeOffset.UtcNow;
        public List<object> loads=new List<object>();
        public SortedSet<string> references=new SortedSet<string>(), roles=new SortedSet<string>();
    }
    private readonly string directory, streaming, timestamp, engineVersion;
    private readonly Dictionary<string,Snapshot> snapshots=new Dictionary<string,Snapshot>();
    private readonly Dictionary<string,string> errors=new Dictionary<string,string>();
    private readonly SortedSet<string> unavailableOptional=new SortedSet<string>();
    private readonly object gate=new object();
    public string ManifestPath => Path.Combine(directory,"config_manifest.json");

    public RunConfigArchive(string sessionDirectory,string streamingDirectory,string sessionTimestamp,string unityVersion)
    {
        directory=Path.GetFullPath(sessionDirectory);streaming=Path.GetFullPath(streamingDirectory);
        timestamp=sessionTimestamp;engineVersion=unityVersion;
    }
    public void CaptureStartup(string scene,string systemReference="system_config.json",string sequenceReference="sequenceConfig.json")
    {
        lock(gate)
        {
            CaptureFile(systemReference,"system",false,true,scene,new HashSet<string>());
            CaptureFile(sequenceReference,"sequence",false,true,scene,new HashSet<string>());
            WriteManifest();
        }
    }
    public byte[] ReadBytes(string reference,string role,string scene)
    {
        lock(gate)
        {
            string path=Resolve(reference);
            try
            {
                byte[] bytes=File.ReadAllBytes(path);
                CaptureBytes(path,reference,role,bytes,true,scene,new HashSet<string>(),true);
                WriteManifest();
                return bytes;
            }
            catch(Exception error)
            {
                errors[path]=error.Message;
                TryWriteManifest();
                throw new IOException("Cannot archive loaded "+role+" file '"+reference+"': "+error.Message,error);
            }
        }
    }
    public string ReadText(string reference,string role,string scene) => Decode(ReadBytes(reference,role,scene));
    public static string Decode(byte[] bytes)
    {
        using(var reader=new StreamReader(new MemoryStream(bytes),Encoding.UTF8,true)) return reader.ReadToEnd();
    }
    public static string Hash(byte[] bytes)
    {
        using(var hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
    }
    public void RequireComplete()
    {
        lock(gate)
        {
            foreach(var file in snapshots.Values)
            {
                string path=Path.Combine(directory,file.snapshotPath);
                if(!File.Exists(path) || Hash(File.ReadAllBytes(path))!=file.sha256)
                    errors[file.sourcePath]="Saved snapshot is missing or fails its SHA-256 check: "+file.snapshotPath;
            }
            WriteManifest();
            if(errors.Count>0) throw new IOException("Run configuration archive is incomplete: "+string.Join("; ",errors.OrderBy(p=>p.Key).Select(p=>p.Key+": "+p.Value)));
        }
    }
    private string Resolve(string reference)
    {
        if(string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("Configuration reference is empty.");
        string path=Path.GetFullPath(Path.IsPathRooted(reference)?reference:Path.Combine(streaming,reference));
        if(File.Exists(path) || Path.IsPathRooted(reference)) return path;
        string old=Path.GetFullPath(Path.Combine(streaming,"Archive","Legacy",reference));
        return File.Exists(old)?old:path;
    }
    private void CaptureFile(string reference,string role,bool consumed,bool required,string scene,HashSet<string> visited)
    {
        string path=Resolve(reference);
        // An optional occurrence must not suppress a later required occurrence of the same file.
        if(!visited.Add(path+"|"+required)) return;
        try {CaptureBytes(path,reference,role,File.ReadAllBytes(path),consumed,scene,visited,required);}
        catch(Exception error)
        {
            if(required) errors[path]=error.Message;
            else unavailableOptional.Add(reference+": "+error.Message);
        }
    }
    private void CaptureBytes(string path,string reference,string role,byte[] bytes,bool consumed,string scene,HashSet<string> visited,bool required)
    {
        string hash=Hash(bytes),key=path+"|"+hash;
        string relative=Path.GetRelativePath(streaming,path);
        bool external=relative==".." || relative.StartsWith(".."+Path.DirectorySeparatorChar) || Path.IsPathRooted(relative);
        string readable=external?Path.Combine("External",Hash(Encoding.UTF8.GetBytes(path)).Substring(0,16),Path.GetFileName(path)):relative;
        string extension=Path.GetExtension(path);
        if(extension.Length>12 || extension.IndexOfAny(Path.GetInvalidFileNameChars())>=0) extension=".bin";
        string snapshotPath=Path.Combine("Configs","Snapshots",hash+extension);
        WriteImmutable(Path.Combine(directory,snapshotPath),bytes);
        string readablePath=Path.Combine("Configs","Files",readable);
        WriteFirst(Path.Combine(directory,readablePath),bytes);
        if(!snapshots.TryGetValue(key,out Snapshot entry))
        {
            entry=new Snapshot {sourcePath=path,sourceRelativePath=relative,sha256=hash,snapshotPath=snapshotPath,
                readablePath=readablePath,bytes=bytes.LongLength};snapshots.Add(key,entry);
        }
        entry.consumed|=consumed;entry.references.Add(reference);entry.roles.Add(role);
        if(consumed) entry.loads.Add(new {utc=DateTimeOffset.UtcNow,reference,role,scene});
        errors.Remove(path);
        if(role=="sequence" || role=="system")
        {
            string name=role=="sequence"?"sequenceConfig.json":"system_config.json";
            WriteFirst(Path.Combine(directory,name),bytes);
            string alias=timestamp+"_"+SafeName(scene)+"_"+name;
            string aliasPath=Path.Combine(directory,alias);
            if(File.Exists(aliasPath) && Hash(File.ReadAllBytes(aliasPath))!=hash)
                aliasPath=Path.Combine(directory,timestamp+"_"+SafeName(scene)+"_"+hash.Substring(0,12)+"_"+name);
            WriteImmutable(aliasPath,bytes);
        }
        if(string.Equals(extension,".json",StringComparison.OrdinalIgnoreCase))
            CaptureReferences(JToken.Parse(Decode(bytes)),scene,visited,required);
        if(role=="trajectory")
        {
            string metadata=Path.ChangeExtension(path,".metadata.json");
            if(File.Exists(metadata)) CaptureFile(metadata,"metadata",false,false,scene,visited);
        }
    }
    private void CaptureReferences(JToken token,string scene,HashSet<string> visited,bool required)
    {
        if(token is JObject obj)
        {
            if(obj["enabled"]?.Type==JTokenType.Boolean && !(bool)obj["enabled"]) required=false;
            foreach(var property in obj.Properties())
            {
                string role=property.Name=="configFile"?"experiment":property.Name=="design"?"design":
                    property.Name=="trajectoryFile"?"trajectory":property.Name=="skyboxPath"?"skybox":null;
                if(role!=null && property.Value.Type==JTokenType.String && !string.IsNullOrWhiteSpace((string)property.Value))
                {
                    bool needed=required && !(role=="skybox" && obj["uniformSkyColor"]!=null && obj["uniformSkyColor"].Type!=JTokenType.Null);
                    CaptureFile((string)property.Value,role,false,needed,scene,visited);
                }
                else CaptureReferences(property.Value,scene,visited,required);
            }
        }
        else if(token is JArray array) foreach(var child in array) CaptureReferences(child,scene,visited,required);
    }
    private static string SafeName(string text)
    {
        if(string.IsNullOrEmpty(text)) return "Startup";
        return string.Join("_",text.Split(Path.GetInvalidFileNameChars()));
    }
    private static void WriteFirst(string path,byte[] bytes)
    {
        if(!File.Exists(path)) WriteImmutable(path,bytes);
    }
    private static void WriteImmutable(string path,byte[] bytes)
    {
        if(File.Exists(path))
        {
            if(Hash(File.ReadAllBytes(path))!=Hash(bytes)) throw new IOException("Refusing to overwrite a different saved snapshot: "+path);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {File.WriteAllBytes(temporary,bytes);File.Move(temporary,path);}
        finally {if(File.Exists(temporary)) File.Delete(temporary);}
    }
    private void TryWriteManifest() {try {WriteManifest();} catch { /* Original IO failure is propagated to the controller. */ }}
    private void WriteManifest()
    {
        Directory.CreateDirectory(directory);
        string json=JsonConvert.SerializeObject(new {schemaVersion=1,updatedUtc=DateTimeOffset.UtcNow,
            unityVersion=engineVersion,complete=errors.Count==0,files=snapshots.Values.OrderBy(f=>f.sourcePath).ThenBy(f=>f.sha256),
            errors=errors.OrderBy(p=>p.Key).Select(p=>new {source=p.Key,error=p.Value}),unavailableOptionalReferences=unavailableOptional},Formatting.Indented);
        string temporary=ManifestPath+".tmp";
        File.WriteAllText(temporary,json,new UTF8Encoding(false));
        // Atomic replacement preserves the previous manifest if the write fails.
        if(File.Exists(ManifestPath)) File.Replace(temporary,ManifestPath,null);
        else File.Move(temporary,ManifestPath);
    }
}
