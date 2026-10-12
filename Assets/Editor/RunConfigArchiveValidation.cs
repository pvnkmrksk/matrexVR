#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class RunConfigArchiveValidation
{
    private const string Key="RunConfigArchiveValidation.Active";
    private static int checks,stage,nextFrame;
    private static double deadline;
    private static bool expectedFailure;
    private static readonly List<string> errors=new List<string>();
    private static void Check(bool ok,string message) {checks++;if(!ok) throw new Exception(message);}
    static RunConfigArchiveValidation()
    {
        EditorApplication.playModeStateChanged+=state=> {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false)) return;
            checks=SessionState.GetInt(Key+".checks",0);deadline=EditorApplication.timeSinceStartup+100;
            EditorApplication.update+=Tick;SceneManager.sceneLoaded+=DisableInputs;Application.logMessageReceived+=OnLog;
        };
    }
    private static void DisableInputs(Scene scene,LoadSceneMode mode)
    {
        foreach(var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) listener.enabled=false;
    }
    private static void OnLog(string message,string stack,LogType type)
    {
        if(type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert) return;
        if(message=="No backup data file found." || stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        if(expectedFailure && message.Contains("Run configuration archive is incomplete")) return;
        errors.Add(message+"\n"+stack);
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable project only.");
        try {FileFixtures();} catch(Exception error) {Finish(error.ToString());return;}
        var system=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Templates/system_config.template.json")));
        system["telemetry"]["enabled"]=false;system["overheadCamera"]["enabled"]=false;
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),system.ToString());
        // Actual active sequence/experiment files are retained. Only live hardware is isolated.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetInt(Key+".checks",checks);SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    private static JObject Manifest(RunConfigArchive archive) => JObject.Parse(File.ReadAllText(archive.ManifestPath));
    private static void Write(string root,string file,string text)
    {
        string path=Path.Combine(root,file);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text,new UTF8Encoding(false));
    }
    private static void FileFixtures()
    {
        string root=Path.Combine(Path.GetTempPath(),"run-config-archive-"+Guid.NewGuid().ToString("N"));
        string streaming=Path.Combine(root,"StreamingAssets"),run=Path.Combine(root,"RunData");Directory.CreateDirectory(streaming);
        string external=Path.Combine(root,"external.json");File.WriteAllText(external,"{\"external\":true}");
        Write(streaming,"system_config.json","{\"configs\":[]}");
        string a="Examples/A/shared.json",b="Examples/B/shared.json";
        string original="{\"stimulus\":{\"swarm\":{\"trajectoryFile\":\"Data/moths.csv\"},\"uniformSkyColor\":{},\"skyboxPath\":\"unused.png\"},\"postStimulus\":{\"enabled\":false,\"configFile\":\"unused.json\"}}";
        Write(streaming,a,original);Write(streaming,b,"{\"designKind\":\"different\"}");
        Write(streaming,"Data/moths.csv","ID,X,Y,Z,time\n1,0,5,0,0\n1,0,5,1,1\n");Write(streaming,"Data/moths.metadata.json","{\"units\":\"m\"}");
        Write(streaming,"Archive/Legacy/old.json","{\"legacy\":true}");
        var sequence=new JObject{{"sequences",new JArray(new JObject{{"parameters",new JObject{{"configFile",a}}}},
            new JObject{{"parameters",new JObject{{"design",b}}}},new JObject{{"parameters",new JObject{{"configFile","old.json"}}}},
            new JObject{{"parameters",new JObject{{"configFile",external}}}})}};
        Write(streaming,"sequenceConfig.json",sequence.ToString());
        var archive=new RunConfigArchive(run,streaming,"fixed-time",Application.unityVersion);archive.CaptureStartup("ControlScene");archive.RequireComplete();
        var manifest=Manifest(archive);Check((bool)manifest["complete"] && manifest["files"].Count()==8,"Startup archives system, sequence, all experiment/design files, data and metadata.");
        Check(File.Exists(Path.Combine(run,"system_config.json")) && File.Exists(Path.Combine(run,"sequenceConfig.json")),"Canonical configs are visible in the run root.");
        Check(File.Exists(Path.Combine(run,"fixed-time_ControlScene_sequenceConfig.json")),"Legacy replay sequence naming remains available.");
        Check(File.ReadAllText(Path.Combine(run,"Configs/Files",a))==original && File.ReadAllText(Path.Combine(run,"Configs/Files",b)).Contains("different"),"Same-basename files retain distinct source paths.");
        Check(manifest["files"].Any(f=>(string)f["sourceRelativePath"]==Path.Combine("Archive","Legacy","old.json")),"Archive fallback is saved from its resolved location.");
        Check(manifest["files"].Any(f=>((string)f["readablePath"]).Contains("External")),"Absolute outside-StreamingAssets references stay inside the run bundle.");
        Check(manifest["unavailableOptionalReferences"].Count()==2,"Unused sky and disabled-phase missing files do not block a run.");
        Check(archive.ReadText(a,"experiment","Swarm")==original,"Reader returns the exact archived input text.");
        for(int i=0;i<3;i++) archive.ReadText("sequenceConfig.json","sequence","ControlScene");
        archive.RequireComplete();Check(Manifest(archive)["files"].Count()==8,"Repeated reloads do not overwrite or duplicate snapshots.");
        var first=Manifest(archive)["files"].Single(f=>((JArray)f["references"]).Any(r=>(string)r==a));string firstPath=(string)first["snapshotPath"];
        Write(streaming,a,"{\"changed\":true}");archive.ReadText(a,"experiment","Swarm");archive.RequireComplete();
        manifest=Manifest(archive);
        Check(manifest["files"].Count(f=>((JArray)f["references"]).Any(r=>(string)r==a))==2,"Edits between loads retain both exact versions.");
        Check(File.ReadAllText(Path.Combine(run,firstPath))==original && File.ReadAllText(Path.Combine(run,"Configs/Files",a))==original,"The first captured version cannot be overwritten by a later edit.");
        Check(manifest["files"].Where(f=>((JArray)f["references"]).Any(r=>(string)r==a)).All(f=>(bool)f["consumed"] && f["loads"].Any()),"Manifest identifies actually loaded versions and times.");
        sequence["extra"]=1;Write(streaming,"sequenceConfig.json",sequence.ToString());archive.ReadText("sequenceConfig.json","sequence","ControlScene");
        Check(Directory.GetFiles(run,"*_sequenceConfig.json").Length==2,"Changed sequence versions get distinct legacy copies without File.Copy collisions.");
        Write(streaming,"sequenceConfig.json","{\"sequences\":[{\"parameters\":{\"configFile\":\"missing.json\"}}]}");archive.ReadText("sequenceConfig.json","sequence","ControlScene");
        bool rejected=false;try {archive.RequireComplete();} catch(IOException) {rejected=true;}
        Check(rejected && !(bool)Manifest(archive)["complete"],"Missing required configs are visibly incomplete.");
        Write(streaming,"missing.json","{\"fixed\":true}");archive.ReadText("sequenceConfig.json","sequence","ControlScene");archive.RequireComplete();
        Check((bool)Manifest(archive)["complete"],"A corrected missing input can be captured on reload.");
        Write(streaming,"cycle-a.json","{\"configFile\":\"cycle-b.json\"}");Write(streaming,"cycle-b.json","{\"design\":\"cycle-a.json\"}");
        archive.ReadText("cycle-a.json","experiment","Swarm");archive.RequireComplete();Check((bool)Manifest(archive)["complete"],"Nested reference cycles terminate safely.");
        string saved=Path.Combine(run,firstPath);File.WriteAllText(saved,"corrupted");rejected=false;
        try {archive.RequireComplete();} catch(IOException) {rejected=true;}
        Check(rejected && !(bool)Manifest(archive)["complete"],"Snapshot corruption prevents a new run instead of silently trusting filenames.");
        string customStreaming=Path.Combine(root,"CustomStreaming");
        Write(customStreaming,"rig-special.json","{\"configs\":[]}");Write(customStreaming,"sequenceConfig.json","{\"sequences\":[]}");
        var custom=new RunConfigArchive(Path.Combine(root,"CustomRun"),customStreaming,"custom-time",Application.unityVersion);
        custom.CaptureStartup("ControlScene","rig-special.json");custom.RequireComplete();
        Check((bool)Manifest(custom)["complete"],"A custom system config is archived without requiring an unused default filename.");
        string optionalStreaming=Path.Combine(root,"OptionalStreaming");
        Write(optionalStreaming,"system_config.json","{\"configs\":[]}");
        Write(optionalStreaming,"parent.json","{\"configFile\":\"optional-child.json\"}");
        Write(optionalStreaming,"sequenceConfig.json","{\"disabled\":{\"enabled\":false,\"configFile\":\"parent.json\"}}");
        var optional=new RunConfigArchive(Path.Combine(root,"OptionalRun"),optionalStreaming,"optional-time",Application.unityVersion);
        optional.CaptureStartup("ControlScene");optional.RequireComplete();
        Check((bool)Manifest(optional)["complete"],"Nested files of a disabled parent remain optional.");
        Write(optionalStreaming,"sequenceConfig.json","{\"disabled\":{\"enabled\":false,\"configFile\":\"parent.json\"},\"active\":{\"configFile\":\"parent.json\"}}");
        optional.ReadText("sequenceConfig.json","sequence","ControlScene");rejected=false;
        try {optional.RequireComplete();} catch(IOException) {rejected=true;}
        Check(rejected,"An earlier optional occurrence cannot hide a missing required dependency of the same file.");
        foreach(var f in manifest["files"])
            Check(File.Exists(Path.Combine(run,(string)f["snapshotPath"])),"Captured files remain available for forensic inspection.");
        // The fixture folder is retained in tmp for inspection; historical sessions are never touched.
    }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Archive integration stage "+stage);
            if(Time.frameCount<nextFrame) return;
            if(stage==0) {SceneManager.LoadScene("ControlScene");stage=1;nextFrame=Time.frameCount+4;return;}
            var main=MainController.Instance;var master=MasterDataLogger.Instance;
            if(main==null || master==null || SceneManager.GetActiveScene().name=="ControlScene") return;
            Check(main.SequenceError==null,"Current experiment starts with a complete archive: "+main.SequenceError);
            var manifest=Manifest(master.ConfigArchive);
            Check((bool)manifest["complete"],"New actual run has a complete manifest.");
            Check(File.Exists(Path.Combine(master.directoryPath,"sequenceConfig.json")) && File.Exists(Path.Combine(master.directoryPath,"system_config.json")),"Actual session saves root configs before scene data.");
            Check(manifest["files"].Any(f=>((JArray)f["roles"]).Any(r=>(string)r=="experiment") && (bool)f["consumed"]),"The actual experiment loaded by the current sequence is archived.");
            foreach(var file in manifest["files"])
                Check(RunConfigArchive.Hash(File.ReadAllBytes(Path.Combine(master.directoryPath,(string)file["snapshotPath"])))==(string)file["sha256"],"Actual saved bytes match their hashes.");
            for(int i=0;i<3;i++) typeof(MainController).GetMethod("LoadSequenceConfiguration",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);
            Check(main.SequenceError==null,"Real sequence reload no longer fails on an existing destination.");
            var victim=Manifest(master.ConfigArchive)["files"].First();File.Delete(Path.Combine(master.directoryPath,(string)victim["snapshotPath"]));
            expectedFailure=true;main.StartSequence();
            Check(!main.SequenceRunning && main.SequenceError!=null && main.SequenceError.Contains("archive is incomplete"),"A missing snapshot prevents sequence start with a visible error.");
            Check(errors.Count==0,"No unexpected runtime errors: "+string.Join("; ",errors));Finish(null);
        }
        catch(Exception error) {Finish(error.ToString());}
    }
    private static void Finish(string failure)
    {
        EditorApplication.update-=Tick;SceneManager.sceneLoaded-=DisableInputs;Application.logMessageReceived-=OnLog;SessionState.SetBool(Key,false);
        File.WriteAllText(Path.Combine(Application.dataPath,"../run-config-archive-validation.json"),JsonConvert.SerializeObject(new{
            checks,failure,errors,unity=Application.unityVersion,note="Isolated filesystem fixtures plus actual current ControlScene experiment, with live tracking disabled. Includes missing/corrupt snapshots, nested references, legacy lookup, duplicate filenames, reloads and version preservation. Main RunData and user configs are not modified."
        },Formatting.Indented));EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
