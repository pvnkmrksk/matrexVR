using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;

public static class Regression
{
    private static int checks;
    private static void Check(bool condition,string description){checks++;if(!condition)throw new Exception(description);}
    private static void Reject(Action action,string description)
    {try{action();}catch(InvalidDataException){checks++;return;}throw new Exception(description);}
    private const string Header="Current Time,VR,Scene,CurrentStep,ConfigFile,GameObjectPosX,GameObjectPosY,GameObjectPosZ,GameObjectRotX,GameObjectRotY,GameObjectRotZ,stepIndex,stepName,experimentPhase,skyboxId";
    private static string Row(string time,string rig,string scene,int step,int x,int yaw,string config="choice.json")=>
        "2026-01-01 "+time+","+rig+","+scene+","+step+","+config+","+x+",0,0,0,"+yaw+",0,0,condition,stimulus,";
    private static void Gzip(string path,string text)
    {using(var stream=new StreamWriter(new GZipStream(File.Create(path),CompressionLevel.Fastest)))stream.Write(text);}
    public static void Main()
    {
        string root=Path.Combine(Path.GetTempPath(),"matrex-replay-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root,"sequenceConfig.json"),"{\"sequences\":[{\"parameters\":{\"configFile\":\"choice.json\"}}]}");
            File.WriteAllText(Path.Combine(root,"system_config.json"),"{\"configs\":[{\"vrId\":\"VR1\"}]}");
            File.WriteAllText(Path.Combine(root,"choice.json"),"{\"objects\":[]}");
            File.WriteAllText(Path.Combine(root,"a-later.csv"),Header+"\n"+Row("10:00:10.000","VR1","Swarm",1,20,1)+"\n"+Row("10:00:09.000","VR1","Swarm",1,19,359)+"\n");
            File.WriteAllText(Path.Combine(root,"z-earlier.csv"),Header+"\n"+Row("10:00:00.000","VR1","Choice",0,0,0)+"\n"+Row("10:00:01.000","VR1","Choice",0,1,1)+"\n");
            Gzip(Path.Combine(root,"second-rig.csv.gz"),Header+"\n"+Row("10:00:02.000","VR5","Choice",0,2,10,"\"Choice, quoted.json\"")+"\n");
            File.WriteAllText(Path.Combine(root,"unrelated.csv"),"Timestamp,skyboxId\n2026-01-01 10:00:00.000,foo\n");
            var archive=new ReplaySessionArchive(root);archive.LoadRigSamples();
            Check(archive.Rigs.Count==2,"both animal IDs load, including VR5");
            Check(archive.Rigs["VR1"].Count==4,"all scene CSVs append to the animal track");
            Check(archive.Duration==10 && archive.Rigs["VR1"][2].time==9,"per-file timestamp origins must not overlap scene logs");
            Check(archive.Rigs["VR5"][0].time==2,"different animals share acquisition origin");
            Check(archive.Rigs["VR5"][0].configFile=="Choice, quoted.json","quoted CSV names survive");
            Check(archive.Rigs["VR1"][2].position.x==19,"shuffled rows are sorted");
            Check(archive.ExperimentFor(archive.Rigs["VR1"][0])?["objects"] is JArray,"legacy run's own config is loaded");
            Reject(()=>archive.SafePath("../outside.json"),"traversal accepted");
            Reject(()=>archive.SafePath("Configs/Files/../../../outside.json"),"nested traversal accepted");
            Check(ReplaySessionArchive.Csv("a,\"b,c\",\"say \"\"hello\"\"\"").SequenceEqual(new[]{"a","b,c","say \"hello\""}),"CSV quoting/double quote parsing");
            string aliases=Path.Combine(root,"rig-aliases");Directory.CreateDirectory(aliases);
            File.WriteAllText(Path.Combine(aliases,"a.csv"),Header+"\n"+Row("10:00:00.000","VR1","Choice",0,0,0)+"\n");
            File.WriteAllText(Path.Combine(aliases,"b.csv"),Header+"\n"+Row("10:00:03.000","VR1 Swarm","Swarm",1,3,0)+"\n");
            File.WriteAllText(Path.Combine(aliases,"c.csv"),Header+"\n"+Row("10:00:05.000","VR1 Kannadi","Kannadi",2,5,0)+"\n");
            var aliasesArchive=new ReplaySessionArchive(aliases);aliasesArchive.LoadRigSamples();
            Check(aliasesArchive.Rigs.Count==1 && aliasesArchive.Rigs["VR1"].Count==3,"scene-specific rig labels merge into one animal");
            Check(aliasesArchive.Rigs["VR1"][2].time==5 && aliasesArchive.Rigs["VR1"][2].recordedRigName=="VR1 Kannadi","normalized chronology preserves original scene label");
            string defaults=Path.Combine(root,"default-run-folders");Directory.CreateDirectory(defaults);
            string backup=Path.Combine(defaults,"Backup"),run=Path.Combine(defaults,"20260101_100000"),empty=Path.Combine(defaults,"20260102_100000");
            Directory.CreateDirectory(backup);Directory.CreateDirectory(run);Directory.CreateDirectory(empty);
            File.WriteAllText(Path.Combine(backup,"old.csv"),Header+"\n"+Row("10:00:00.000","VR1","Choice",0,0,0));
            File.WriteAllText(Path.Combine(run,"animals.csv"),Header+"\n"+Row("10:00:00.000","VR1","Choice",0,0,0));
            Check(ReplaySessionArchive.NewestRunFolder(defaults)==run,"default replay skips Backup and newer folders without pose CSVs");
            string idle=Path.Combine(defaults,"20260103_100000");Directory.CreateDirectory(idle);
            File.WriteAllText(Path.Combine(idle,"control.csv"),Header+"\n"+Row("10:00:00.000","VR1","ControlScene",0,0,0));
            Check(ReplaySessionArchive.NewestRunFolder(defaults)==run,"new current idle Control-only run does not hide the last real experiment");
            File.Delete(Path.Combine(run,"animals.csv"));Check(ReplaySessionArchive.NewestRunFolder(defaults)==null,"folder picker/path UI stays available when no actual runs exist");
            Reject(()=>ReplaySessionArchive.Csv("a,\"broken"),"malformed quote accepted");
            Check(!ReplaySessionArchive.TryNumber("NaN",out _) && !ReplaySessionArchive.TryNumber("Infinity",out _),"nonfinite pose rejected");
            string agents=Path.Combine(root,"agents.csv.gz");
            Gzip(agents,"Timestamp,Name,Layer,X,Y,Z,skyboxId,skyboxSampleUtc,experimentPhase,RotationX,RotationY,RotationZ,Count:2\n"+
                "2026-01-01 10:00:00.000,a,AgentsVR5,1,2,3,,,stimulus,0,359,0\n"+
                "2026-01-01 10:00:00.000,b,AgentsVR5,2,3,4,,,stimulus,0,90,0\n"+
                "2026-01-01 10:00:02.000,a,AgentsVR5,4,5,6,,,stimulus,0,1,0\n");
            string cache=Path.Combine(root,"cache");
            using(var stream=ReplayAgentStream.TryOpen(agents,cache,archive.OriginTicks))
            {
                Check(stream.HasRotations && stream.FrameCount==2 && stream.RowCount==3,"population pose frames are indexed");
                var first=stream.Sample(0);Check(first.Count==2 && first[0].rotation.y==359,"saved orientation survives gzip/cache");
                var later=stream.Sample(2);Check(later.Count==1 && later[0].position.x==4,"forward scrub reads indexed frame");
                Check(stream.Sample(0).Count==2,"backward scrub restores expired population");
                Check(stream.Sample(3).Count==0,"population vanishes outside recording");
            }
            Check(!Directory.GetFiles(cache).Any(),"disk cache removed on disposal");
            string old=Path.Combine(root,"legacy-agents.csv.gz");
            Gzip(old,"Timestamp,Name,Layer,X,Y,Z,skyboxId,skyboxSampleUtc,experimentPhase\n2026-01-01 10:00:00.000,old,SimulatedLocustsVR1,1,2,3,,,stimulus\n");
            using(var stream=ReplayAgentStream.TryOpen(old,cache,archive.OriginTicks))Check(!stream.HasRotations && stream.Sample(0).Count==1,"legacy nine-column population still loads");
            string saved=Path.Combine(root,"Configs","Snapshots");Directory.CreateDirectory(saved);
            byte[] bytes=Encoding.UTF8.GetBytes("{\"objects\":[],\"marker\":123}");string hash=RunConfigArchive.Hash(bytes);
            File.WriteAllBytes(Path.Combine(saved,hash+".json"),bytes);
            JObject entry=new JObject{["sourceRelativePath"]="choice.json",["sourcePath"]="/a/different/computer/choice.json",["sha256"]=hash,["snapshotPath"]="Configs/Snapshots/"+hash+".json",["references"]=new JArray("choice.json"),["roles"]=new JArray("experiment")};
            File.WriteAllText(Path.Combine(root,"config_manifest.json"),new JObject{["complete"]=true,["files"]=new JArray(entry)}.ToString());
            var hashed=new ReplaySessionArchive(root);
            Check(hashed.Resolve("choice.json")==Path.Combine(saved,hash+".json"),"immutable snapshot used instead of mutable local copy");
            Check(hashed.Resolve("/a/different/computer/choice.json")==Path.Combine(saved,hash+".json"),"external original path resolves inside saved archive");
            Check(hashed.Resolve("system_config.json")==null,"manifest-backed replay never substitutes unverified/live JSON");
            File.WriteAllText(Path.Combine(saved,hash+".json"),"corruption");
            Reject(()=>new ReplaySessionArchive(root).Resolve("choice.json"),"corrupted config was accepted");
            File.WriteAllBytes(Path.Combine(saved,hash+".json"),bytes);
            entry["snapshotPath"]="../outside.json";
            File.WriteAllText(Path.Combine(root,"config_manifest.json"),new JObject{["files"]=new JArray(entry)}.ToString());
            Reject(()=>new ReplaySessionArchive(root).Resolve("choice.json"),"manifest path traversal accepted");
            for(int i=0;i<150;i++)
            {
                string path=Path.Combine(root,"writer-"+i+".csv.gz");
                var writer=new AsyncSwarmWriter(path,"Timestamp,Name,Layer,X,Y,Z,skyboxId,skyboxSampleUtc,experimentPhase,RotationX,RotationY,RotationZ");
                var frame=writer.Rent(1);frame.timestamp=new DateTime(2026,1,1,10,0,0);frame.count=1;
                frame.agents[0]=new AsyncSwarmWriter.Agent{name="agent,quoted",layer="AgentsVR5",x=i,rx=1,ry=2,rz=3};
                Check(writer.Submit(frame),"writer accepted frame "+i);writer.Complete();writer.Complete();writer.WaitForCompletion();
                Check(writer.Failure==null && writer.WrittenRows==1 && writer.PendingFrames==0,"writer drained "+i);
                Check((bool)typeof(AsyncSwarmWriter).GetField("signalDisposed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(writer),"native wait handle disposed "+i);
                using(var stream=ReplaySessionArchive.OpenCsv(path))
                {stream.ReadLine();var cells=ReplaySessionArchive.Csv(stream.ReadLine());Check(cells.Length==12 && cells[1]=="agent,quoted" && cells[11]=="3","writer columns and quotation "+i);}
            }
            Check(AsyncSwarmWriter.ActiveWriterCount==0,"no retained async writer after 150 cycles");
            Console.WriteLine("PASS: "+checks+" replay/archive/writer regressions; 150 async writer lifecycle cycles.");
        }
        finally{Directory.Delete(root,true);}
    }
}
