using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Read-only recording discovery and summaries. Only the separately supplied cache directory is writable.</summary>
public static class RecordingLibrary
{
    private const long MaximumJsonBytes=4*1024*1024;
    public sealed class Entry
    {
        public string DirectoryPath { get; internal set; }
        public string Id { get; internal set; }
        public string Title { get; internal set; }
        public string DateLabel { get; internal set; }
        public JObject Summary { get; internal set; }
        public string Error { get; internal set; }
        public bool Indexed { get; internal set; }
        public bool Cached { get; internal set; }
        public bool Finalized { get; internal set; }
        public string RecordingState { get; internal set; }="unknown";
        public string FreshnessToken { get; internal set; }
        private string searchText;
        public string SearchText {get {if(searchText==null)searchText=Title+" "+Id+" "+Summary?["metadata"]+" "+Summary?["observed"];return searchText;}}
        public bool ChangedDuringScan => (bool?)Summary?["changedDuringScan"]==true;
        public long PlayableRows => (long?)Summary?["poseRows"]?["playable"]??0;
        public bool CanReplay => Indexed&&PlayableRows>0&&string.IsNullOrEmpty(Error)&&!ChangedDuringScan;
        public bool Omit => Indexed&&Summary!=null&&PlayableRows==0&&(long?)Summary["poseRows"]?["rejected"]==0;
    }
    public static IReadOnlyList<Entry> Discover(string root,CancellationToken cancellation=default)
    {
        root=Path.GetFullPath(root);if(!Directory.Exists(root))return Array.Empty<Entry>();
        var entries=new List<Entry>();
        foreach(string directory in Directory.EnumerateDirectories(root).OrderByDescending(path=>Path.GetFileName(path),StringComparer.Ordinal))
        {
            cancellation.ThrowIfCancellationRequested();string name=Path.GetFileName(directory);
            if(name.StartsWith(".",StringComparison.Ordinal)||name.Equals("Backup",StringComparison.OrdinalIgnoreCase)||name.Equals("Cache",StringComparison.OrdinalIgnoreCase))continue;
            try {if(HasPoseHeader(directory,cancellation))entries.Add(Preview(directory));}
            catch(OperationCanceledException){throw;}
            catch(Exception error){var entry=Preview(directory);entry.Error="Cannot inspect recording: "+error.Message;entry.Indexed=true;entries.Add(entry);}
        }
        return entries;
    }
    public static Entry Inspect(string directory,string cacheDirectory,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();var entry=Preview(directory);entry.Indexed=true;
        try
        {
            cancellation.ThrowIfCancellationRequested();CheckMetadataSize(entry.DirectoryPath);
            string fingerprint=Fingerprint(entry.DirectoryPath,cancellation);entry.FreshnessToken=fingerprint;
            entry.RecordingState=(string)ReadSmall(Path.Combine(entry.DirectoryPath,RecordingSummary.ManifestName))?["lifecycle"]?["status"]??"unknown";
            JObject summary;
            if(RecordingSummary.TryReadFinalized(entry.DirectoryPath,out summary,out _))entry.Finalized=true;
            else
            {
                summary=ReadCache(entry.DirectoryPath,cacheDirectory,fingerprint);
                if(summary!=null)entry.Cached=true;
                else
                {
                    summary=RecordingSummary.Scan(entry.DirectoryPath,false,cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    if(fingerprint!=Fingerprint(entry.DirectoryPath,cancellation))summary["changedDuringScan"]=true;
                    if((bool?)summary["changedDuringScan"]!=true)WriteCache(entry.DirectoryPath,cacheDirectory,fingerprint,summary);
                }
            }
            cancellation.ThrowIfCancellationRequested();
            entry.Summary=(JObject)summary.DeepClone();
            if(fingerprint!=Fingerprint(entry.DirectoryPath,cancellation)){entry.Summary["changedDuringScan"]=true;entry.Finalized=false;}
            string title=(string)summary["metadata"]?["experimentName"];
            if(!string.IsNullOrWhiteSpace(title))entry.Title=title;
            string first=(string)summary["wallClockStartLocal"];
            if(DateTime.TryParse(first,CultureInfo.InvariantCulture,DateTimeStyles.None,out var started))entry.DateLabel=started.ToString("dd MMM yyyy · HH:mm",CultureInfo.InvariantCulture);
            // Replay uses these exact saved config checks too. Missing legacy JSON
            // remains a warning; corrupt saved snapshots cannot be presented as ready.
            new ReplaySessionArchive(entry.DirectoryPath);
        }
        catch(OperationCanceledException){throw;}
        catch(Exception error){entry.Error=error.Message;}
        return entry;
    }
    private static Entry Preview(string directory)
    {
        string path=Path.GetFullPath(directory),id=Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        string date=DateTime.TryParseExact(id.Length>=15?id.Substring(0,15):id,"yyyyMMdd_HHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed)?parsed.ToString("dd MMM yyyy · HH:mm",CultureInfo.InvariantCulture):"Date unavailable";
        var entry=new Entry{DirectoryPath=path,Id=id,Title=id,DateLabel=date};
        try
        {
            var metadata=ReadSmall(Path.Combine(path,"AnimalMetadata.json"))??ReadSmall(Path.Combine(path,"FlyMetaData.json"));
            string title=(string)metadata?["ExperimentName"];if(!string.IsNullOrWhiteSpace(title))entry.Title=title;
        }
        catch { /* Full inspection will report malformed/oversized metadata. */ }
        return entry;
    }
    private static bool HasPoseHeader(string directory,CancellationToken cancellation)
    {
        foreach(string file in ReplaySessionArchive.CsvFiles(directory))
        {
            cancellation.ThrowIfCancellationRequested();
            using(var input=ReplaySessionArchive.OpenCsv(file))
            {
                var text=new StringBuilder();int character;
                while(text.Length<65536&&(character=input.Read())>=0&&character!='\n'){if((text.Length&4095)==0)cancellation.ThrowIfCancellationRequested();text.Append((char)character);}
                if(text.Length==0||text.Length>=65536)continue;string first=text.ToString().TrimEnd('\r');
                var header=ReplaySessionArchive.Csv(first.TrimStart('\ufeff'));
                if(new[]{"Current Time","VR","GameObjectPosX","GameObjectPosY","GameObjectPosZ","GameObjectRotX","GameObjectRotY","GameObjectRotZ"}.All(header.Contains))return true;
            }
        }
        return false;
    }
    private static void CheckMetadataSize(string directory)
    {
        foreach(string name in new[]{"config_manifest.json","AnimalMetadata.json","FlyMetaData.json","system_config.json","sequenceConfig.json",RecordingSummary.ManifestName})
        {
            var file=new FileInfo(Path.Combine(directory,name));
            if(file.Exists&&file.Length>MaximumJsonBytes)throw new IOException(name+" exceeds the library metadata size limit. Open this folder directly in replay for advanced inspection.");
        }
    }
    private static JObject ReadSmall(string file)
    {
        var info=new FileInfo(file);if(!info.Exists)return null;
        if(info.Length>MaximumJsonBytes)throw new IOException("Metadata exceeds the size limit.");
        return JObject.Parse(File.ReadAllText(file));
    }
    public static string GetFreshnessToken(string directory,CancellationToken cancellation=default)=>Fingerprint(Path.GetFullPath(directory),cancellation);
    private static string Fingerprint(string directory,CancellationToken cancellation)
    {
        var files=new List<string>(ReplaySessionArchive.CsvFiles(directory));
        files.AddRange(new[]{"config_manifest.json","AnimalMetadata.json","FlyMetaData.json","system_config.json","sequenceConfig.json",RecordingSummary.ManifestName}.Select(name=>Path.Combine(directory,name)).Where(File.Exists));
        string snapshots=Path.Combine(directory,"Configs","Snapshots");
        if(Directory.Exists(snapshots))files.AddRange(Directory.EnumerateFiles(snapshots));
        var text=new StringBuilder();
        foreach(string file in files.OrderBy(path=>path,StringComparer.Ordinal))
        {
            cancellation.ThrowIfCancellationRequested();var info=new FileInfo(file);
            text.Append(Path.GetRelativePath(directory,file)).Append('\t').Append(info.Length).Append('\t').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
        }
        return Digest(text.ToString());
    }
    private static string CachePath(string directory,string cacheDirectory)
    {
        if(string.IsNullOrWhiteSpace(cacheDirectory))return null;
        string root=Path.GetFullPath(cacheDirectory),source=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        if(root==source||root.StartsWith(source+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))return null;
        return Path.Combine(root,Digest(source)+".json");
    }
    private static JObject ReadCache(string directory,string cacheDirectory,string fingerprint)
    {
        try
        {
            string path=CachePath(directory,cacheDirectory);if(path==null)return null;
            var cached=ReadSmall(path);
            if((int?)cached?["version"]!=1||(string)cached["directory"]!=directory||(string)cached["fingerprint"]!=fingerprint)return null;
            return cached["summary"] as JObject;
        }
        catch {return null;}
    }
    private static void WriteCache(string directory,string cacheDirectory,string fingerprint,JObject summary)
    {
        string path=CachePath(directory,cacheDirectory);if(path==null)return;
        string temporary=null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            File.WriteAllText(temporary,new JObject{["version"]=1,["directory"]=directory,["fingerprint"]=fingerprint,["summary"]=summary}.ToString(Formatting.None),new UTF8Encoding(false));
            if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
        }
        catch { /* A read-only/unavailable cache must not prevent replay discovery. */ }
        finally {if(temporary!=null&&File.Exists(temporary))try{File.Delete(temporary);}catch{}}
    }
    private static string Digest(string text)
    {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
}
