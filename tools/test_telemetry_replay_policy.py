#!/usr/bin/env python3
"""Compile exact telemetry capture gates with controlled scene/worker boundaries.

Checks both publication paths and replay-reader name compatibility without Unity,
ZMQ, a browser, native input or configuration writes.
"""
from pathlib import Path
from test_logging_scene_handoff import declaration
import os
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
PROGRAM = r'''
using System;using System.IO;
static class Time{public static double realtimeSinceStartupAsDouble;}
class Scene{public string name;}
static class SceneManager{public static string name;public static Scene GetActiveScene()=>new Scene{name=name};}
static class Debug{public static void LogError(string message){throw new Exception(message);}}
class Worker{public string Failure;public int snapshots,contexts;public void Submit(object value){snapshots++;}public void SubmitContext(object value){contexts++;}}
class Settings{public double rateHz=20,contextIntervalSeconds=2;}
class Actual{
 public Worker worker=new Worker();private Settings settings=new Settings();private bool reportedWorkerFailure;
 private double nextPublish,nextContext;private long sequence,contextSequence;private string state;
 public int snapshotsCaptured,contextsCaptured,closed;
 private object Capture(long number){snapshotsCaptured++;return number;}private object CaptureContext(long number){contextsCaptured++;return number;}
 private void Close(){closed++;worker=null;}
 public void Tick()=>LateUpdate();public static bool Allowed(string name)=>CanPublishScene(name);
 __TELEMETRY__
}
static class Reader{
 public static bool Reserved(string scene)=>IsNonExperimentPose(scene,"VR1.csv");
 __READER__
}
static class Regression{
 static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Case(string scene,bool allowed){
  SceneManager.name=scene;Time.realtimeSinceStartupAsDouble=10;var actual=new Actual();var worker=actual.worker;actual.Tick();
  Check(Actual.Allowed(scene)==allowed,"scene policy mismatch: "+scene);
  Check(actual.worker.snapshots==(allowed?1:0)&&actual.worker.contexts==(allowed?1:0),"pose/context publication differs: "+scene);
  Check(actual.snapshotsCaptured==(allowed?1:0)&&actual.contextsCaptured==(allowed?1:0),"capture work was not skipped: "+scene);
  Check(ReferenceEquals(actual.worker,worker)&&actual.closed==0,"scene gate closed/replaced the bound worker: "+scene);
 }
 static void Main(){
  foreach(string scene in new[]{"ReplayScene","replayscene","REPLAYSCENE"," ReplayScene\t"}){Case(scene,false);Check(Reader.Reserved(scene),"replay policy disagrees with reserved reader scene matching");}
  foreach(string scene in new[]{"ControlScene","Choice","Choice_desync","Choice_Selwyn","Swarm","Kannadi","Optomotor","TerrainCueLab","CustomExperiment","ReplaySceneCustom"})Case(scene,true);
  Check(Reader.Reserved("ControlScene")&&Actual.Allowed("ControlScene"),"idle setup telemetry must remain permitted even though it is not replay input");
  var a=new Actual();var original=a.worker;SceneManager.name="ReplayScene";Time.realtimeSinceStartupAsDouble=20;a.Tick();
  SceneManager.name="ControlScene";Time.realtimeSinceStartupAsDouble=21;a.Tick();
  Check(ReferenceEquals(a.worker,original)&&a.closed==0,"return from replay replaced the live worker");
  Check(a.worker.snapshots==1&&a.worker.contexts==1,"return to setup must resume pose/context immediately");
  Time.realtimeSinceStartupAsDouble=21.01;a.Tick();Check(a.worker.snapshots==1&&a.worker.contexts==1,"normal telemetry rate limiting changed");
  Time.realtimeSinceStartupAsDouble=21.06;a.Tick();Check(a.worker.snapshots==2&&a.worker.contexts==1,"pose and heartbeat cadence must remain independent");
  SceneManager.name="ReplayScene";Time.realtimeSinceStartupAsDouble=24;a.Tick();Check(a.worker.snapshots==2&&a.worker.contexts==1,"re-entering replay must stop both paths");
  SceneManager.name="Choice";Time.realtimeSinceStartupAsDouble=25;a.Tick();Check(a.worker.snapshots==3&&a.worker.contexts==2,"experimental scenes must resume on the same bound worker");
  Console.WriteLine("PASS: "+checks+" exact telemetry/replay policy checks; controlled scene/worker boundaries, no Unity/ZMQ process.");
 }
}
'''


def main():
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    sdk = subprocess.check_output([dotnet, '--list-sdks'], text=True)
    version = max(map(int, re.findall(r'^(\d+)\.', sdk, re.M)))
    telemetry = (ROOT/'Assets/Scripts/ExperimentTelemetry.cs').read_text()
    start = telemetry.index('private static bool CanPublishScene(')
    policy = telemetry[start:telemetry.index(';',start)+1]
    production = declaration(telemetry,'private void LateUpdate()')+'\n'+policy
    reader = declaration((ROOT/'Assets/Scripts/Replay/ReplaySessionArchive.cs').read_text(),'private static bool IsNonExperimentPose(')
    with tempfile.TemporaryDirectory(prefix='matrex-telemetry-replay-') as folder:
        temporary = Path(folder)
        env = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        (temporary/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (temporary/'Program.cs').write_text(PROGRAM.replace('__TELEMETRY__',production).replace('__READER__',reader))
        project = temporary/'Regression.csproj'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup></Project>')
        result = subprocess.run([dotnet, 'build', str(project), '--configfile', str(temporary/'NuGet.Config'), '-p:UseSharedCompilation=false', '-v:q'], env=env, capture_output=True, text=True)
        if result.returncode:
            raise SystemExit(result.stdout+result.stderr)
        subprocess.run([dotnet, str(temporary/f'bin/Debug/net{version}.0/Regression.dll')], env=env, check=True)


if __name__ == '__main__':
    main()
