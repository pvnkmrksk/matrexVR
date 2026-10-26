using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityEngine
{
    public static class Application
    {
        public static string version="0.3.1-dev", unityVersion="fixture-unity", buildGUID="actual-player-guid", streamingAssetsPath;
    }
}

static class Regression
{
    static int checks;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static readonly string[] Header = { "Current Time", "VR", "Scene", "CurrentSequenceScene", "ConfigFile", "CurrentTrial", "CurrentStep",
        "GameObjectPosX", "GameObjectPosY", "GameObjectPosZ", "GameObjectRotX", "GameObjectRotY", "GameObjectRotZ", "experimentPhase", "unityFrame" };
    static string Row(int second, string rig="VR1", string scene="Choice", string logical="Choice", int cycle=4, int step=0, string phase="stimulus", string x="0") =>
        RecordedCsv.Join(new[] { "2026-10-26 10:00:" + second.ToString("D2") + ".0000000", rig, scene, logical, "", cycle.ToString(), step.ToString(), x, "0", "0", "0", "0", "0", phase, "10" });
    static void Finalized(string directory, JObject summary)
    {
        summary["metadataFiles"] = RecordingSummary.MetadataInventory(directory);
        summary["lifecycle"] = new JObject { ["status"]="finalized", ["writersDrained"]=true };
        File.WriteAllText(Path.Combine(directory,RecordingSummary.ManifestName),summary.ToString());
    }
    static void Main()
    {
        string root=Path.Combine(Path.GetTempPath(),"recording-summary-fixture-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string run=Path.Combine(root,"run"), configs=Path.Combine(root,"configs"); Directory.CreateDirectory(run);Directory.CreateDirectory(configs);
            File.WriteAllText(Path.Combine(configs,"system_config.json"),"{\"configs\":[{\"vrId\":\"VR1\",\"speciesName\":\"fly\",\"closedLoopMode\":\"Rekkefly\"},{\"vrId\":\"VR2\",\"speciesName\":\"beetle\",\"closedLoopMode\":\"Hejjetrac\"},{\"vrId\":\"VR3\",\"speciesName\":\"locust\"}]}");
            File.WriteAllText(Path.Combine(configs,"sequenceConfig.json"),"{\"sequences\":[{\"sceneName\":\"Choice\"},{\"sceneName\":\"Swarm\"}]}");
            var archive=new RunConfigArchive(run,configs,"fixture","unity-fixture");archive.CaptureStartup("ControlScene");archive.RequireComplete();
            File.WriteAllText(Path.Combine(run,"AnimalMetadata.json"),"{\"ExperimentName\":\"Real title\",\"ExperimenterName\":\"Operator\",\"Animals\":[{\"VR\":\"VR1\",\"AnimalID\":\"animal-A\",\"FlyID\":\"wrong\"}],\"Flies\":[{\"VR\":\"VR1\",\"FlyID\":\"duplicate-alias\"}]}");
            string first=Path.Combine(run,"fixture_Choice_VR1_.csv"), second=Path.Combine(run,"fixture_Choice_VR2_.csv");
            File.WriteAllText(first,RecordedCsv.Join(Header)+"\n"+string.Join("\n",new[]{Row(0),Row(1,phase:"post"),Row(2,cycle:9),Row(30,scene:"ControlScene"),Row(40,scene:"ReplayScene"),Row(50,logical:"Swarm",step:1),Row(59,scene:"Swarm",logical:"Swarm",step:1,phase:"idle"),Row(3,x:"NaN"),"bad,row"})+"\n");
            File.WriteAllText(second,RecordedCsv.Join(Header)+"\n"+Row(0,"VR2")+"\n"+Row(2,"VR2",cycle:9)+"\n");
            string hash=RecordingSummary.HashFile(first);int callbacks=0;
            var summary=RecordingSummary.Scan(run,true,default,(file,header,cells)=>callbacks++);
            Check(callbacks==5,"callback must see only five playable poses");
            Check((long)summary["poseRows"]["stored"]==11,"raw stored count includes malformed/excluded rows");
            Check((long)summary["poseRows"]["playable"]==5,"playable count");
            Check((long)summary["poseRows"]["rejected"]==2,"alignment and NaN rejected");
            Check((long)summary["poseRows"]["excluded"]["setupReplay"]==2,"setup/replay exclusions");
            Check((long)summary["poseRows"]["excluded"]["handoffIdle"]==2,"verified handoff/idle exclusions");
            Check((double)summary["wallClockSpanSeconds"]==2,"excluded tails must not extend duration");
            Check((int)summary["observed"]["sequenceCycleCount"]==2,"distinct cycles4,9 rather than max+1 or per-rig sum");
            Check((int)summary["observed"]["presentationsByRig"]["VR1"]==2,"phase changes do not invent presentations");
            Check(((JArray)summary["observed"]["rigIds"]).Count==2,"observed rigs distinct from configured rigs");
            Check(((JArray)summary["metadata"]["configuredAnimals"]).Count==3,"saved rig/species fallback");
            Check(((JArray)summary["metadata"]["identifiedAnimalIds"]).Count==1,"canonical Animals/AnimalID wins over both legacy aliases");
            Check((string)summary["metadata"]["configuredAnimals"][1]["SpeciesName"]=="beetle","archived species fallback");
            Check(((JArray)summary["metadata"]["plannedSceneNames"]).Count==2&&((JArray)summary["observed"]["sceneNames"]).Count==1,"planned and observed scenes separate");
            Check(hash==RecordingSummary.HashFile(first)&&!File.Exists(Path.Combine(run,RecordingSummary.ManifestName)),"scan must not mutate recordings");
            Check(!RecordingSummary.TryReadFinalized(run,out _,out _),"complete config archive is not finalized recording");
            Finalized(run,summary);Check(RecordingSummary.TryReadFinalized(run,out _,out _),"fresh finalized summary accepted");
            File.AppendAllText(second,Row(3,"VR2")+"\n");Check(!RecordingSummary.TryReadFinalized(run,out _,out _),"append invalidates cached summary");
            Finalized(run,RecordingSummary.Scan(run));
            File.WriteAllText(Path.Combine(run,"FlyMetaData.json"),"{}");Check(!RecordingSummary.TryReadFinalized(run,out _,out _),"new metadata invalidates cache");
            File.Delete(Path.Combine(run,"FlyMetaData.json"));
            string population=Path.Combine(run,"population.csv.gz");
            using(var writer=new StreamWriter(new GZipStream(File.Create(population),CompressionLevel.Fastest)))
            {writer.WriteLine("Timestamp,Name,Layer,X,Y,Z,NumberOfLocusts:1");for(int i=0;i<10000;i++)writer.WriteLine("2026-10-26 10:00:01.000,a,AgentsVR1,0,0,0");}
            var quick=RecordingSummary.Scan(run);var pop=((JArray)quick["files"]).OfType<JObject>().First(file=>(string)file["relativePath"]=="population.csv.gz");
            Check((bool)pop["rowsInspected"]==false&&pop["storedRows"].Type==JTokenType.Null,"library skips population rows without inventing zero count");
            var deep=RecordingSummary.Scan(run,false,default,null,true);pop=((JArray)deep["files"]).OfType<JObject>().First(file=>(string)file["relativePath"]=="population.csv.gz");
            Check((long)pop["storedRows"]==10000&&(long)pop["rejectedRows"]==0,"population header metadata is not mistaken for missing row fields");
            var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;
            try{RecordingSummary.Scan(run,false,cancelled.Token);}catch(OperationCanceledException){stopped=true;}
            Check(stopped,"cancelled scan terminates without writing");
            string legacy=Path.Combine(root,"legacy");Directory.CreateDirectory(legacy);
            var noTrial=Header.Where(name=>name!="CurrentTrial").ToArray();var cells=ReplaySessionArchive.Csv(Row(0));
            File.WriteAllText(Path.Combine(legacy,"legacy.csv"),RecordedCsv.Join(noTrial)+"\n"+RecordedCsv.Join(cells.Where((value,index)=>index!=5))+"\n");
            var old=RecordingSummary.Scan(legacy);Check(old["observed"]["sequenceCycleCount"].Type==JTokenType.Null,"legacy missing trial count stays unknown");
            Check((long)old["poseRows"]["playable"]==1,"legacy pose is preserved without invented exclusion evidence");
            UnityEngine.Application.streamingAssetsPath=configs;
            var identity=PlayerBuildIdentity.Capture();
            Check(identity["sourceRevision"].Type==JTokenType.Null&&(string)identity["buildGuid"]=="actual-player-guid","legacy build keeps actual buildGUID but unknown revision");
            string identityFile=Path.Combine(configs,"player_build_info.json");
            File.WriteAllText(identityFile,new JObject{["schemaVersion"]=1,["applicationVersion"]="0.3.1-dev",["sourceRevision"]=new string('f',40),["sourceState"]="working-tree-snapshot",["sourceTreeDirty"]=true}.ToString());
            identity=PlayerBuildIdentity.Capture();Check((string)identity["sourceRevision"]==new string('f',40)&&(bool)identity["sourceTreeDirty"],"source state is preserved without pretending dirty snapshot is clean");
            File.WriteAllText(identityFile,new JObject{["schemaVersion"]=1,["applicationVersion"]="old-version",["sourceRevision"]=new string('f',40)}.ToString());
            Check(PlayerBuildIdentity.Capture()["sourceRevision"].Type==JTokenType.Null,"mismatched embedded build info is not attributed to current player");
            string failurePath=Path.Combine(run,"directory-instead-of-file.csv.gz");Directory.CreateDirectory(failurePath);
            var failed=new AsyncSwarmWriter(failurePath,"Timestamp,Name");failed.WaitForCompletion();
            Check(AsyncSwarmWriter.ActiveWriterCount==0,"failed writer leaves active registry");
            var completions=AsyncSwarmWriter.CompletionsForDirectory(run);
            Check(completions.Length==1&&!string.IsNullOrEmpty(completions[0].Failure),"completed failed writer evidence survives until run finalization");
            string writtenPath=Path.Combine(run,"successful.csv.gz");
            var asyncWriter=new AsyncSwarmWriter(writtenPath,"Timestamp,Name,Layer,X,Y,Z,skyboxId,skyboxSampleUtc,experimentPhase,RotationX,RotationY,RotationZ");
            var frame=asyncWriter.Rent(1);frame.timestamp=DateTime.Now;frame.count=1;frame.agents[0]=new AsyncSwarmWriter.Agent{name="a",layer="AgentsVR1"};
            Check(asyncWriter.Submit(frame),"fixture frame accepted");asyncWriter.WaitForCompletion();
            using(var reader=ReplaySessionArchive.OpenCsv(writtenPath))Check(reader.ReadToEnd().Split('\n').Length>=3,"writer completion includes gzip trailer and buffered row");
            completions=AsyncSwarmWriter.CompletionsForDirectory(run,true);
            Check(completions.Length==2&&completions.Any(item=>item.WrittenRows==1&&item.Failure==null),"successful and historical failed writer summaries remain distinct");
            Check(AsyncSwarmWriter.CompletionsForDirectory(run).Length==0,"finalization can release compact per-run writer evidence");
            string finalRun=Path.Combine(root,"final"), zip=Path.Combine(root,"final.zip");Directory.CreateDirectory(finalRun);
            File.WriteAllText(Path.Combine(finalRun,"pose.csv"),RecordedCsv.Join(Header)+"\n"+Row(0)+"\n"+Row(2)+"\n");
            string preserved=RecordingSummary.HashFile(Path.Combine(finalRun,"pose.csv"));
            var finalizer=new RecordingSessionFinalizer(finalRun,zip,identity);
            var drainGate=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var mediaGate=new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);int visited=0;
            var finishing=finalizer.FinalizeAsync("stopped",token=>drainGate.Task,token=>mediaGate.Task,()=>new JArray(),(f,h,c)=>Interlocked.Increment(ref visited));
            Check(ReferenceEquals(finishing,finalizer.FinalizeAsync("completed",token=>Task.CompletedTask,token=>Task.FromResult(new JObject()),()=>new JArray())),"finalization is idempotent and preserves original end reason");
            Check(!finishing.IsCompleted&&!File.Exists(zip),"summary/archive cannot finish ahead of writer barrier");
            drainGate.SetResult(true);Check(SpinWait.SpinUntil(()=>Volatile.Read(ref visited)==2,10000),"scan begins only after writer drain");
            Check(!finishing.IsCompleted&&!File.Exists(zip),"media barrier completes before final manifest/ZIP");
            mediaGate.SetResult(new JObject{["complete"]=false,["pending"]=true});var finalized=finishing.GetAwaiter().GetResult();
            Check((string)finalized["lifecycle"]["status"]=="finalized"&&(bool)finalized["lifecycle"]["writersDrained"],"CSV finalization truthfully reflects completed drain");
            Check((string)finalized["lifecycle"]["endReason"]=="stopped"&&(string)finalized["lifecycle"]["sequenceOutcome"]=="stopped","manual stop does not become completed protocol");
            Check((bool)finalized["media"]["pending"]&&!(bool)finalized["media"]["complete"],"pending independent camera stays explicitly incomplete");
            Check(RecordingSummary.TryReadFinalized(finalRun,out _,out _),"finalized output passes freshness policy");
            using(var zipped=ZipFile.OpenRead(zip))using(var reader=new StreamReader(zipped.GetEntry(RecordingSummary.ManifestName).Open()))
                Check((string)JObject.Parse(reader.ReadToEnd())["lifecycle"]["status"]=="finalized","ZIP includes finalized summary, not its opening placeholder");
            Check(preserved==RecordingSummary.HashFile(Path.Combine(finalRun,"pose.csv")),"finalization preserves original CSV bytes");
            string failedRun=Path.Combine(root,"failed-final");Directory.CreateDirectory(failedRun);
            var failedFinalizer=new RecordingSessionFinalizer(failedRun,Path.Combine(root,"failed.zip"),identity);
            var failedResult=failedFinalizer.FinalizeAsync("error",token=>Task.CompletedTask,token=>Task.FromResult(new JObject()),
                ()=>new JArray(new JObject{["failure"]="earlier disk write failed",["droppedFrames"]=2})).GetAwaiter().GetResult();
            Check((string)failedResult["lifecycle"]["status"]=="error"&&!(bool)failedResult["lifecycle"]["writersDrained"]&&!File.Exists(Path.Combine(root,"failed.zip")),"historical completed writer failure prevents successful drain/archive claim");
            string interrupted=Path.Combine(root,"interrupted");Directory.CreateDirectory(interrupted);
            var interruptedFinalizer=new RecordingSessionFinalizer(interrupted,Path.Combine(root,"interrupted.zip"),identity);
            var never=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var waiting=interruptedFinalizer.FinalizeAsync("stopped",token=>never.Task,token=>Task.FromResult(new JObject()),()=>new JArray());
            interruptedFinalizer.MarkShutdown(false,new JObject{["pending"]=true,["complete"]=false});never.SetResult(true);waiting.GetAwaiter().GetResult();
            var onDisk=JObject.Parse(File.ReadAllText(Path.Combine(interrupted,RecordingSummary.ManifestName)));
            Check((string)onDisk["lifecycle"]["status"]=="open"&&!(bool)onDisk["lifecycle"]["writersDrained"]&&!File.Exists(Path.Combine(root,"interrupted.zip")),"shutdown cannot promise pending media/summary/archive completion");
            Console.WriteLine("PASS: "+checks+" production recording-summary checks; read-only scans and no Unity rendering claim.");
        }
        finally {Directory.Delete(root,true);}
    }
}
