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
    private static void InspectRun(string directory,string report)
    {
        var archive=new ReplaySessionArchive(directory);archive.LoadRigSamples();
        var contexts=archive.Rigs.Values.SelectMany(samples=>samples).GroupBy(sample=>sample.ContextKey).Select(group=>group.First()).ToArray();
        int restored=contexts.Count(sample=>archive.ExperimentFor(sample)!=null);
        string population=ReplaySessionArchive.CsvFiles(directory).FirstOrDefault(path=>path.EndsWith(".gz",StringComparison.OrdinalIgnoreCase));
        object agents=null;string cache=Path.Combine(Path.GetTempPath(),"matrex-legacy-inspect-"+Guid.NewGuid().ToString("N"));
        try
        {
            if(population!=null)using(var stream=ReplayAgentStream.TryOpen(population,cache,archive.OriginTicks))
                if(stream!=null)agents=new{stream.Kind,stream.RowCount,stream.FrameCount,stream.HasRotations,stream.HasVisibility};
        }
        finally {if(Directory.Exists(cache))Directory.Delete(cache,true);}
        string json=Newtonsoft.Json.JsonConvert.SerializeObject(new{rigs=archive.Rigs.Count,rows=archive.Rigs.Values.Sum(samples=>samples.Count),archive.Duration,archive.FirstExperimentTime,contexts=contexts.Length,restoredConfigurations=restored,representativePopulation=agents,warnings=archive.Warnings},Newtonsoft.Json.Formatting.Indented);
        if(report!=null)File.WriteAllText(report,json);Console.WriteLine(json);
    }
    private static string Row(string time,string rig,string scene,int step,int x,int yaw,string config="choice.json")=>
        "2026-01-01 "+time+","+rig+","+scene+","+step+","+config+","+x+",0,0,0,"+yaw+",0,0,condition,stimulus,";
    private static void Gzip(string path,string text)
    {using(var stream=new StreamWriter(new GZipStream(File.Create(path),CompressionLevel.Fastest)))stream.Write(text);}
    public static void Main(string[] arguments)
    {
        if(arguments.Length>0){InspectRun(arguments[0],arguments.Length>1?arguments[1]:null);return;}
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
            string setup=Path.Combine(root,"setup-and-replay.csv");
            File.WriteAllText(setup,Header+"\n"+Row("09:00:00.000","VR99","ControlScene",0,999,0)+"\n"+Row("12:00:00.000","VR99","ReplayScene",0,999,0)+"\n");
            byte[] setupBytes=File.ReadAllBytes(setup);
            var archive=new ReplaySessionArchive(root);archive.LoadRigSamples();
            Check(archive.Rigs.Count==2,"both animal IDs load, including VR5");
            Check(archive.Rigs["VR1"].Count==4,"all scene CSVs append to the animal track");
            Check(archive.Duration==10 && archive.Rigs["VR1"][2].time==9,"per-file timestamp origins must not overlap scene logs");
            Check(archive.Rigs["VR5"][0].time==2,"different animals share acquisition origin");
            Check(archive.Rigs["VR5"][0].configFile=="Choice, quoted.json","quoted CSV names survive");
            Check(archive.Rigs["VR1"][2].position.x==19,"shuffled rows are sorted");
            Check(!archive.Rigs.ContainsKey("VR99") && archive.Warnings.Any(w=>w.Contains("excluded 2 setup/replay pose rows")),"setup/replay rows neither create animal tracks nor silently enter playback");
            Check(File.ReadAllBytes(setup).SequenceEqual(setupBytes),"filtering never rewrites original recordings");
            string legacyScenes=Path.Combine(root,"legacy-blank-scenes");Directory.CreateDirectory(legacyScenes);
            File.WriteAllText(Path.Combine(legacyScenes,"20260101_100000_Choice_VR1_.csv"),Header+"\n"+Row("10:00:00.000","VR1","",0,1,0)+"\n");
            File.WriteAllText(Path.Combine(legacyScenes,"20260101_090000_ControlScene_VR1_.csv"),Header+"\n"+Row("09:00:00.000","VR1","",0,999,0)+"\n");
            Gzip(Path.Combine(legacyScenes,"20260101_120000_ReplayScene_VR1_.csv.gz"),Header+"\n"+Row("12:00:00.000","VR1","",0,999,0)+"\n");
            string noSceneHeader=Header.Replace(",Scene","");
            string noSceneRow=string.Join(",",Row("13:00:00.000","VR1","",0,999,0).Split(',').Where((_,index)=>index!=2));
            File.WriteAllText(Path.Combine(legacyScenes,"20260101_130000_ReplayScene_VR1_.csv"),noSceneHeader+"\n"+noSceneRow+"\n");
            var legacySceneArchive=new ReplaySessionArchive(legacyScenes);legacySceneArchive.LoadRigSamples();
            Check(legacySceneArchive.Rigs["VR1"].Count==1 && legacySceneArchive.Duration==0 && legacySceneArchive.Rigs["VR1"][0].position.x==1,"blank/missing Scene uses reserved legacy filename fallback without dropping ordinary legacy experiment rows");
            string handoff=Path.Combine(root,"modern-handoff");Directory.CreateDirectory(handoff);
            byte[] sequenceBytes=Encoding.UTF8.GetBytes("{\"sequences\":[{\"sceneName\":\"Choice_Selwyn\"},{\"sceneName\":\"Choice\"}]}");string sequenceHash=RunConfigArchive.Hash(sequenceBytes);
            File.WriteAllBytes(Path.Combine(handoff,"saved-sequence.json"),sequenceBytes);
            File.WriteAllText(Path.Combine(handoff,"config_manifest.json"),new JObject{["complete"]=true,["files"]=new JArray(new JObject{["sourceRelativePath"]="sequenceConfig.json",["snapshotPath"]="saved-sequence.json",["sha256"]=sequenceHash,["roles"]=new JArray("sequence")})}.ToString());
            string modernHeader=Header+",CurrentSequenceScene,unityFrame";
            string modernRows=string.Join("\n",new[]{
                Row("10:00:00.000","VR1","Choice_Selwyn",0,0,0).Replace(",condition,stimulus,",",condition,preStimulus,")+",Choice_Selwyn,100",
                Row("10:00:01.000","VR1","Choice_Selwyn",0,1,0).Replace(",condition,stimulus,",",condition,postStimulus,")+",Choice_Selwyn,101",
                Row("10:00:02.000","VR1","Choice_Selwyn",0,2,0).Replace(",condition,stimulus,",",condition,assessment,")+",Choice_Selwyn,102",
                Row("10:00:03.000","VR1","Choice_Selwyn",0,3,0)+",Choice_Selwyn,103",
                Row("10:00:04.000","VR1","Choice_Selwyn",1,999,0)+",Choice,104",
                Row("10:00:05.000","VR1","Choice",1,999,0).Replace(",condition,stimulus,",",condition,idle,")+",Choice,105",
                Row("10:00:06.000","VR1","Choice",1,6,0)+",Choice,106",
                Row("10:00:07.000","VR1","Choice historical alias",1,7,0)+",Choice,107"});
            string modernFile=Path.Combine(handoff,"poses.csv");File.WriteAllText(modernFile,modernHeader+"\n"+modernRows+"\n");byte[] modernOriginal=File.ReadAllBytes(modernFile);
            var handoffArchive=new ReplaySessionArchive(handoff);handoffArchive.LoadRigSamples();
            Check(handoffArchive.Rigs["VR1"].Count==6&&!handoffArchive.Rigs["VR1"].Any(sample=>sample.unityFrame==104||sample.unityFrame==105),"verified handoff and idle rows are excluded");
            Check(new[]{"preStimulus","postStimulus","assessment"}.All(phase=>handoffArchive.Rigs["VR1"].Any(sample=>sample.phase==phase)),"same-scene pre/post/assessment exposures remain replayable");
            Check(handoffArchive.Rigs["VR1"].Any(sample=>sample.scene=="Choice historical alias")&&handoffArchive.Warnings.Any(w=>w.Contains("excluded 2 verified idle/scene-handoff")),"legacy aliases are not guessed and exclusions are disclosed");
            Check(File.ReadAllBytes(modernFile).SequenceEqual(modernOriginal),"handoff filtering preserves exact recording bytes");
            File.Delete(Path.Combine(handoff,"config_manifest.json"));var legacyHandoff=new ReplaySessionArchive(handoff);legacyHandoff.LoadRigSamples();
            Check(legacyHandoff.Rigs["VR1"].Count==8,"unverified legacy context is not discarded as modern handoff data");
            Check(archive.ExperimentFor(archive.Rigs["VR1"][0])?["objects"] is JArray,"legacy run's own config is loaded");
            string terrainRun=Path.Combine(root,"terrain-inline-treatments");Directory.CreateDirectory(terrainRun);
            var terrainBase=new JObject{["autopilotSpeed"]=1,["rugosity"]=new JObject{["enabled"]=true,["visualSurface"]="base",["collisionSurface"]="base",["normalSurface"]="inherited"}};
            File.WriteAllText(Path.Combine(terrainRun,"terrain.json"),terrainBase.ToString());
            var terrainRows=new JArray();
            foreach(string treatment in new[]{"RoughSmooth","SmoothRough","SmoothSmooth","RoughRough"})terrainRows.Add(new JObject{["sceneName"]="TerrainCueLab",["parameters"]=new JObject{["configFile"]="terrain.json",["autopilotSpeed"]=99,["rugosity"]=new JObject{["visualSurface"]=treatment,["collisionSurface"]="Flat",["normalSurface"]=null}}});
            var terrainSequence=new JObject{["randomise"]=false,["sequences"]=terrainRows};File.WriteAllText(Path.Combine(terrainRun,"sequenceConfig.json"),terrainSequence.ToString());
            var terrainArchive=new ReplaySessionArchive(terrainRun);
            for(int i=0;i<terrainRows.Count;i++)
            {
                var restored=terrainArchive.ExperimentFor(new ReplaySessionArchive.Sample{scene="TerrainCueLab",sequenceStep=i,configFile="terrain.json"});
                Check((string)restored["rugosity"]["visualSurface"]==(string)terrainRows[i]["parameters"]["rugosity"]["visualSurface"] && (string)restored["rugosity"]["collisionSurface"]=="Flat","Replay restores per-row terrain treatment while all four rows share one base file: "+i);
                Check((string)restored["rugosity"]["normalSurface"]=="inherited" && (int)restored["autopilotSpeed"]==1,"Terrain patch ignores null and does not merge unrelated inline parameters: "+i);
            }
            Check(JToken.DeepEquals(JObject.Parse(File.ReadAllText(Path.Combine(terrainRun,"terrain.json"))),terrainBase),"Treatment restoration does not rewrite saved shared base JSON");
            var phaseBase=new JObject{["stimulus"]=terrainBase.DeepClone()};File.WriteAllText(Path.Combine(terrainRun,"terrain-phased.json"),phaseBase.ToString());
            terrainRows.Add(new JObject{["sceneName"]="TerrainCueLab",["parameters"]=new JObject{["configFile"]="terrain-phased.json",["rugosity"]=new JObject{["visualSurface"]="phase-patch"}}});
            File.WriteAllText(Path.Combine(terrainRun,"sequenceConfig.json"),terrainSequence.ToString());
            var phaseArchive=new ReplaySessionArchive(terrainRun);var phaseRestored=phaseArchive.ExperimentFor(new ReplaySessionArchive.Sample{scene="TerrainCueLab",sequenceStep=4,phase="stimulus",configFile="terrain-phased.json"});
            Check((string)phaseRestored["rugosity"]["visualSurface"]=="phase-patch" && (string)phaseRestored["rugosity"]["normalSurface"]=="inherited","Terrain override is applied after saved phase extraction");
            terrainSequence["randomise"]=true;File.WriteAllText(Path.Combine(terrainRun,"sequenceConfig.json"),terrainSequence.ToString());
            var ambiguousTerrain=new ReplaySessionArchive(terrainRun);
            Check(ambiguousTerrain.ExperimentFor(new ReplaySessionArchive.Sample{scene="TerrainCueLab",sequenceStep=0,configFile="terrain.json"})==null && ambiguousTerrain.Warnings.Any(w=>w.Contains("randomized terrain treatment is ambiguous")),"Unlogged randomized source-row order cannot silently select the wrong shared-base terrain patch");
            Reject(()=>archive.SafePath("../outside.json"),"traversal accepted");
            Reject(()=>archive.SafePath("Configs/Files/../../../outside.json"),"nested traversal accepted");
            Check(ReplaySessionArchive.Csv("a,\"b,c\",\"say \"\"hello\"\"\"").SequenceEqual(new[]{"a","b,c","say \"hello\""}),"CSV quoting/double quote parsing");
            var previousCulture=CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");
                var csv=new StringBuilder();RecordedCsv.Append(csv,1.25f);csv.Append(',');RecordedCsv.Append(csv,"config, quoted\nline");
                csv.Append(',');RecordedCsv.Append(csv,"say \"hello\"");
                Check(csv.ToString().StartsWith("1.25,"),"recorded numeric fields use invariant culture on a decimal-comma Mac");
                string multiline=Path.Combine(root,"multiline-record.csv");File.WriteAllText(multiline,csv.ToString());
                using(var input=ReplaySessionArchive.OpenCsv(multiline))
                {
                    var fields=ReplaySessionArchive.Csv(ReplaySessionArchive.ReadCsvRecord(input));
                    Check(fields.Length==3 && fields[1]=="config, quoted\nline" && fields[2]=="say \"hello\"","quoted multiline CSV roundtrips without shifting pose columns");
                }
            }
            finally {CultureInfo.CurrentCulture=previousCulture;}
            Check(ReplaySessionArchive.Integer("3.0",-1)==3 && ReplaySessionArchive.Integer("3.5",-1)==-1,"historical integer-valued float step fields are accepted without rounding");
            string invalid=Path.Combine(root,"misaligned-run");Directory.CreateDirectory(invalid);
            File.WriteAllText(Path.Combine(invalid,"valid.csv"),Header+"\n"+Row("10:00:00.000","VR1","Choice",0,0,0)+"\n"+Row("10:00:01.000","VR1","Choice",0,1,0)+",shifted\n");
            var invalidArchive=new ReplaySessionArchive(invalid);invalidArchive.LoadRigSamples();
            Check(invalidArchive.Rigs["VR1"].Count==1 && invalidArchive.Warnings.Any(w=>w.Contains("column count")),"misaligned legacy locale/delimiter rows are not silently interpreted as different poses");
            string swaps=Path.Combine(root,"swap-evidence");Directory.CreateDirectory(swaps);
            string swapHeader=Header+",swapElapsedSec,grayAtTrialStart,blackSideAtTrialStart";
            File.WriteAllText(Path.Combine(swaps,"evidence.csv"),swapHeader+"\n"+
                Row("10:00:00.000","VR1","Choice_desync",0,0,0)+",,0.35,left\n"+
                Row("10:00:01.000","VR1","Choice_desync",0,1,0)+",1,,\n"+
                Row("10:00:02.000","VR1","Choice_desync",0,2,0)+",,,\n");
            var evidence=new ReplaySessionArchive(swaps);evidence.LoadRigSamples();
            Check(!evidence.Rigs["VR1"][0].colorsSwapped && evidence.Rigs["VR1"][1].colorsSwapped && evidence.Rigs["VR1"][2].colorsSwapped,"recorded swap event persists in later samples but reverses when seeking before it");
            Check(evidence.Rigs["VR1"][2].grayAtTrialStart==.35f && evidence.Rigs["VR1"][2].blackSideAtTrialStart=="left","adaptive onset evidence survives sparse event rows");
            var adaptive=JObject.Parse("{\"steps\":[{\"name\":\"template\",\"objects\":[{\"polar\":{\"angle\":-20,\"radius\":60}},{\"polar\":{\"angle\":20,\"radius\":60}}]}]}");
            var adaptiveSample=new ReplaySessionArchive.Sample{stepName="AdaptiveDecision_Normal_0001_blackL",grayAtTrialStart=.35f,blackSideAtTrialStart="left"};
            var resolved=ReplayStimulusState.Step(adaptive,adaptiveSample,out string adaptiveWarning);
            Check(adaptiveWarning==null && (float)resolved["objects"][0]["color"][0]==0 && (float)resolved["objects"][1]["color"][0]==.35f,"adaptive condition restores logged black side and learned gray instead of rerandomizing");
            adaptiveSample.stepName="AdaptiveDecision_Control_noBar_0005";
            Check(((JArray)ReplayStimulusState.Step(adaptive,adaptiveSample,out _)["objects"]).Count==0,"adaptive no-bar control reconstructs an empty field");
            adaptiveSample.stepName="AdaptiveDecision_Control_singleBlack0deg_0005";
            resolved=ReplayStimulusState.Step(adaptive,adaptiveSample,out _);
            Check(((JArray)resolved["objects"]).Count==1 && (float)resolved["objects"][0]["polar"]["angle"]==0,"adaptive center-bar control restores exact recorded condition kind");
            string prefixed=Path.Combine(root,"legacy-prefixed");Directory.CreateDirectory(prefixed);
            File.WriteAllText(Path.Combine(prefixed,"20250101_100000_ControlScene_sequenceConfig.json"),"{\"sequences\":[{\"parameters\":{\"configFile\":\"Choice_00.json\"}}]}");
            File.WriteAllText(Path.Combine(prefixed,"20250101_100000_ControlScene_Choice_00.json"),"{\"objects\":[]}");
            var legacyPrefixed=new ReplaySessionArchive(prefixed);
            Check(legacyPrefixed.Resolve("Choice_00.json")==Path.Combine(prefixed,"20250101_100000_ControlScene_Choice_00.json"),"pre-manifest timestamp/scene-prefixed experiment copies resolve inside the recording");
            File.WriteAllText(Path.Combine(prefixed,"20250101_100001_ControlScene_Choice_00.json"),"{\"objects\":[],\"changed\":true}");
            Reject(()=>new ReplaySessionArchive(prefixed).Resolve("Choice_00.json"),"ambiguous differing legacy config versions accepted");
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
            string band=Path.Combine(root,"band-visibility.csv.gz");
            Gzip(band,"Timestamp,Name,Layer,X,Y,Z,RotationX,RotationY,RotationZ,VisibilityPhase,Visible,VisibleOnDuration:0.1,VisibleOffDuration:0.2\n"+
                "2026-01-01 10:00:00.000,band,SimulatedLocustsVR1,1,2,3,0,0,0,0.05,true\n"+
                "2026-01-01 10:00:01.000,band,SimulatedLocustsVR1,1,2,3,0,0,0,0.25,false\n");
            using(var stream=ReplayAgentStream.TryOpen(band,cache,archive.OriginTicks))
            {
                Check(stream.Kind=="Band" && stream.HasVisibility && stream.Sample(0)[0].visible,"band's recorded on phase is visible");
                Check(!stream.Sample(1)[0].visible && stream.Sample(0)[0].visible,"band's recorded off phase and backward seek preserve visibility");
            }
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
            string profile=Path.Combine(root,"native-profile"),native=Path.Combine(root,"native-app-config"),nativeRun=Path.Combine(root,"native-recording");
            Directory.CreateDirectory(profile);Directory.CreateDirectory(native);
            byte[] originalSystem=Encoding.UTF8.GetBytes("{\"configs\":[{\"vrId\":\"VR1\"}]}\r\n");
            File.WriteAllBytes(Path.Combine(profile,"system_config.json"),originalSystem);
            File.WriteAllText(Path.Combine(profile,"sequenceConfig.json"),"{\"sequences\":[]}");
            string calibration=Path.Combine(native,"calibration.JSON"),sidecar=Path.Combine(native,"unrelated-secret.txt"),stale=Path.Combine(native,"stale.toml"),media=Path.Combine(native,"camera.avi");
            File.WriteAllText(sidecar,"must not be copied");File.WriteAllText(stale,"stale = true");File.WriteAllText(media,"not a text configuration");
            byte[] calibrationBytes=new byte[]{0xef,0xbb,0xbf}.Concat(Encoding.UTF8.GetBytes(new JObject{["precision"]="1.0000000000000000000001",["configFile"]=sidecar}.ToString()+"\r\n")).ToArray();
            File.WriteAllBytes(calibration,calibrationBytes);
            var nativeArchive=new RunConfigArchive(nativeRun,profile,"20260101_100000","test");nativeArchive.CaptureStartup("ControlScene");
            nativeArchive.CaptureCompanions(new[]{
                new JObject{["vrId"]="VR1",["backend"]="Rekkefly",["identityVerified"]=true,["matchesConfiguredRig"]=true,["configFile"]=calibration,["releaseTag"]="local-test",["executableSha256"]=new string('a',64),["demoRequested"]=false,["strictCameraFallback"]=true,["actualInputSource"]="unverified"},
                new JObject{["vrId"]="VR2",["identityVerified"]=false,["matchesConfiguredRig"]=true,["configFile"]=sidecar},
                new JObject{["vrId"]="VR3",["identityVerified"]=true,["matchesConfiguredRig"]=false,["configFile"]=stale},
                new JObject{["vrId"]="VR4",["identityVerified"]=true,["matchesConfiguredRig"]=true,["configFile"]=Path.Combine(native,"missing.toml")},
                new JObject{["vrId"]="VR5",["identityVerified"]=true,["matchesConfiguredRig"]=true,["configFile"]=media},
                new JObject{["vrId"]="VR6",["state"]="unmanaged-or-no-owned-launch",["identityVerified"]=false}
            },"ControlScene");
            nativeArchive.RequireComplete();
            var nativeManifest=JObject.Parse(File.ReadAllText(nativeArchive.ManifestPath));
            var nativeEvidence=(JArray)nativeManifest["companionProvenance"];
            Check(nativeEvidence.Count==6 && (bool)nativeEvidence[0]["snapshotAvailable"],"native run records only supplied owned launches and captures matching calibration");
            Check((string)nativeEvidence[0]["actualInputSource"]=="unverified" && (bool)nativeEvidence[0]["strictCameraFallback"],"process ownership and strict launch policy never fabricate observed physical source");
            Check((string)nativeEvidence[0]["releaseTag"]=="local-test" && (string)nativeEvidence[0]["executableSha256"]==new string('a',64),"native release and executable identity are retained");
            string nativeSnapshot=Path.Combine(nativeRun,(string)nativeEvidence[0]["configSnapshotPath"]);
            Check(nativeSnapshot.EndsWith(".JSON",StringComparison.Ordinal),"native snapshot references retain extension case on case-sensitive filesystems");
            Check(File.ReadAllBytes(nativeSnapshot).SequenceEqual(calibrationBytes),"native configuration snapshot preserves BOM, CRLF and exact bytes");
            Check((string)nativeEvidence[0]["configSha256"]==RunConfigArchive.Hash(calibrationBytes),"native calibration snapshot has a verifiable SHA-256 digest");
            Check(nativeManifest["files"].Count(file=>((JArray)file["roles"]).Any(role=>(string)role=="tracker-calibration"))==1,"only one verified matching text calibration enters file archive");
            Check(!nativeManifest["files"].Any(file=>(string)file["sourcePath"]==sidecar || (string)file["sourcePath"]==stale || (string)file["sourcePath"]==media),"native calibration references, stale rigs and camera media are never recursively archived");
            Check(!(bool)nativeEvidence[1]["snapshotAvailable"] && !(bool)nativeEvidence[2]["snapshotAvailable"],"unverified and mismatched process identities cannot authorize configuration capture");
            Check((string)nativeEvidence[3]["configurationStatus"]=="unavailable" && (string)nativeEvidence[4]["configurationStatus"]=="unavailable","missing calibration and nontext input are explicitly unavailable");
            Check((bool)nativeManifest["complete"] && ((JArray)nativeManifest["unavailableOptionalReferences"]).Count==2,"missing optional native evidence is transparent without corrupting the main experiment archive");
            Check(((string)nativeEvidence[0]["configurationScope"]).Contains("in-memory"),"snapshot scope distinguishes on-disk text from native in-memory settings");
            byte[] startupEvidence=File.ReadAllBytes(Path.Combine(nativeRun,"companion_provenance.json"));
            File.WriteAllText(calibration,"changed after run startup");nativeArchive.CaptureCompanions(new JObject[0],"Choice");
            Check(File.ReadAllBytes(nativeSnapshot).SequenceEqual(calibrationBytes) && File.ReadAllBytes(Path.Combine(nativeRun,"companion_provenance.json")).SequenceEqual(startupEvidence),"subsequent native config edits cannot rewrite run startup evidence");
            Check(File.ReadAllBytes(Path.Combine(profile,"system_config.json")).SequenceEqual(originalSystem),"native provenance never inserts launch details into the lossless primary system file");
            var replayWithNative=new ReplaySessionArchive(nativeRun);
            Check(replayWithNative.Resolve("system_config.json")!=null,"native provenance remains compatible with manifest-based replay");
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
