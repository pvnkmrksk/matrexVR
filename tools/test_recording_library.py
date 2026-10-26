#!/usr/bin/env python3
"""Offline production library discovery/cache checks; no Unity, camera, process or source-run writes."""
from pathlib import Path
from xml.sax.saxutils import escape
import os,re,shutil,subprocess,tempfile
ROOT=Path(__file__).resolve().parents[1]
PROGRAM=r'''
using System;using System.IO;using System.Linq;using System.Text;using System.Threading;using Newtonsoft.Json.Linq;
static class Regression {
 static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 const string Header="Current Time,VR,Scene,CurrentStep,ConfigFile,CurrentTrial,GameObjectPosX,GameObjectPosY,GameObjectPosZ,GameObjectRotX,GameObjectRotY,GameObjectRotZ";
 static string Row(string time,string vr,int trial,string scene="Choice")=>"2026-10-26 10:00:"+time+","+vr+","+scene+",0,choice.json,"+trial+",1,2,3,0,40,0\n";
 static string Make(string root,string id){string path=Path.Combine(root,id);Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"system_config.json"),"{\"configs\":[{\"vrId\":\"VR1\",\"speciesName\":\"moth\",\"closedLoopMode\":\"Rekkefly\"}]}");File.WriteAllText(Path.Combine(path,"sequenceConfig.json"),"{\"sequences\":[{\"sceneName\":\"Choice\",\"duration\":600,\"parameters\":{\"configFile\":\"choice.json\"}}]}");File.WriteAllText(Path.Combine(path,"choice.json"),"{\"objects\":[]}");File.WriteAllText(Path.Combine(path,"AnimalMetadata.json"),"{\"ExperimentName\":\"Forest run\",\"Animals\":[{\"VR\":\"VR1\",\"AnimalID\":\"animal A\"}]}");return path;}
 static string Snapshot(string root)=>string.Join("\n",Directory.GetFiles(root,"*",SearchOption.AllDirectories).OrderBy(x=>x).Select(path=>path+":"+RunConfigArchive.Hash(File.ReadAllBytes(path))));
 static void Main(){string root=Path.Combine(Path.GetTempPath(),"matrex-library-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string cache=Path.Combine(root,"outside-cache");string runs=Path.Combine(root,"RunData");Directory.CreateDirectory(runs);
 try{
  string a=Make(runs,"20261026_100000");string csv=Path.Combine(a,"poses.csv");File.WriteAllText(csv,Header+"\n"+Row("00.000","VR1",0)+Row("01.000","VR2",0)+Row("02.000","VR1",1)+Row("03.000","VR2",1));
  string setup=Make(runs,"20261026_110000");File.WriteAllText(Path.Combine(setup,"poses.csv"),Header+"\n"+Row("00.000","VR1",0,"ControlScene"));
  Directory.CreateDirectory(Path.Combine(runs,"20261026_120000"));Directory.CreateDirectory(Path.Combine(runs,"Backup"));Directory.CreateDirectory(Path.Combine(runs,".hidden"));File.WriteAllText(Path.Combine(runs,"duplicate.zip"),"not a directory");
  var found=RecordingLibrary.Discover(runs);Check(found.Count==2&&found.All(e=>e.Title=="Forest run"),"discovery includes metadata-only/backup/zip or loses experiment names");
  string original=Snapshot(runs);var first=RecordingLibrary.Inspect(a,cache);
  Check(first.CanReplay&&first.PlayableRows==4,"readable recording is not replayable");Check((int)first.Summary["observed"]["sequenceCycleCount"]==2,"parallel rigs multiplied cycle count");
  Check((double)first.Summary["wallClockSpanSeconds"]==3,"planned duration was substituted for observed span");Check(((JArray)first.Summary["metadata"]["identifiedAnimalIds"]).Single().ToString()=="animal A","animal identity metadata lost");
  Check(!first.Finalized&&first.Summary["lifecycle"]==null,"read-only legacy scan invented finalized lifecycle");
  Check(RecordingLibrary.Inspect(setup,cache).Omit,"setup-only rows clutter final library");Check(Snapshot(runs)==original,"library discovery/indexing changed original dataset files");
  Check(RecordingLibrary.IsFinalizingRecording(a,a,true),"active source was replayable before writer/media finalization");
  Check(RecordingLibrary.IsFinalizingRecording(Path.Combine(a,"..",Path.GetFileName(a))+Path.DirectorySeparatorChar,a,true),"external browse alias bypassed active source finalization");
  Check(!RecordingLibrary.IsFinalizingRecording(setup,a,true),"finalizing current source blocked an older recording");
  Check(!RecordingLibrary.IsFinalizingRecording(a,a,false),"completed finalization did not re-enable the selected source");
  Check(!RecordingLibrary.IsFinalizingRecording(a,null,true),"missing active source blocked a readable external recording");
  var again=RecordingLibrary.Inspect(a,cache);Check(again.Cached&&again.CanReplay,"unchanged source did not use external summary cache");
  File.AppendAllText(csv,Row("04.000","VR1",2));var grown=RecordingLibrary.Inspect(a,cache);Check(!grown.Cached&&grown.PlayableRows==5&&(int)grown.Summary["observed"]["sequenceCycleCount"]==3,"appended recording did not invalidate cached summary");
  File.WriteAllText(Path.Combine(a,"AnimalMetadata.json"),"{\"ExperimentName\":\"Updated recording\"}");Check(RecordingLibrary.Inspect(a,cache).Title=="Updated recording","updated metadata did not invalidate cached title");
  string unsafeCache=Path.Combine(a,".cache");RecordingLibrary.Inspect(a,unsafeCache);Check(!Directory.Exists(unsafeCache),"cache wrote inside original recording");
  var summary=RecordingSummary.Scan(a);summary["metadataFiles"]=RecordingSummary.MetadataInventory(a);summary["lifecycle"]=new JObject{["status"]="finalized",["writersDrained"]=true};File.WriteAllText(Path.Combine(a,RecordingSummary.ManifestName),summary.ToString());
  Check(RecordingLibrary.Inspect(a,cache).Finalized,"valid finalization manifest not used");File.AppendAllText(csv,Row("05.000","VR1",3));Check(!RecordingLibrary.Inspect(a,cache).Finalized,"stale finalized manifest was accepted after source growth");
  string external=Make(root,"external-recording");File.WriteAllText(Path.Combine(external,"poses.csv"),Header+"\n"+Row("00.000","VR1",0));Check(RecordingLibrary.Inspect(external,cache).CanReplay,"external folder inspection requires it to be under local RunData");
  bool canceled=false;try{RecordingLibrary.Inspect(a,cache,new CancellationToken(true));}catch(OperationCanceledException){canceled=true;}Check(canceled,"already canceled inspection ignored cancellation");
  string bad=Make(runs,"20261026_130000");File.WriteAllText(Path.Combine(bad,"poses.csv"),Header+"\n"+Row("00.000","VR1",0));File.WriteAllText(Path.Combine(bad,"config_manifest.json"),"bad JSON");Check(!RecordingLibrary.Inspect(bad,cache).CanReplay,"corrupt archived metadata was presented as ready");Check(RecordingLibrary.Discover(runs).Any(e=>e.DirectoryPath==a),"one corrupt recording hid all other recordings");
  Console.WriteLine("PASS: "+checks+" recording library checks; no source-run writes or native apps.");
 }finally{Directory.Delete(root,true);}}
}
'''
def main():
 dotnet=shutil.which('dotnet')
 if not dotnet:raise SystemExit('A local .NET SDK is required.')
 version=max(map(int,re.findall(r'^(\d+)\.',subprocess.check_output([dotnet,'--list-sdks'],text=True),re.M)))
 dll=next((ROOT/'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
 paths=['Assets/Scripts/Replay/RecordingLibrary.cs','Assets/Scripts/Loggers/RecordingSummary.cs','Assets/Scripts/Replay/RecordedPosePolicy.cs','Assets/Scripts/Replay/ReplaySessionArchive.cs','Assets/Scripts/Loggers/RunConfigArchive.cs','tools/replay_compatibility/Stubs.cs']
 with tempfile.TemporaryDirectory(prefix='matrex-library-check-') as folder:
  temp=Path(folder);(temp/'Program.cs').write_text(PROGRAM);(temp/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
  includes=''.join(f'<Compile Include="{escape(str(ROOT/path))}" />' for path in paths)
  project=temp/'Regression.csproj';project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(dll))}</HintPath></Reference>{includes}</ItemGroup></Project>')
  env=dict(os.environ,DOTNET_CLI_HOME=folder,DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
  result=subprocess.run([dotnet,'build',str(project),'--configfile',str(temp/'NuGet.Config'),'-p:UseSharedCompilation=false','-v:q'],env=env,capture_output=True,text=True)
  if result.returncode:raise SystemExit(result.stdout+result.stderr)
  subprocess.run([dotnet,str(temp/f'bin/Debug/net{version}.0/Regression.dll')],env=env,check=True)
if __name__=='__main__':main()
