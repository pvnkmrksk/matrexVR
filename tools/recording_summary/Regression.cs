using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            Console.WriteLine("PASS: "+checks+" production recording-summary checks; read-only scans and no Unity rendering claim.");
        }
        finally {Directory.Delete(root,true);}
    }
}
