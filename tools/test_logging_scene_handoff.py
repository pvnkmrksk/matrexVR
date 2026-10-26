#!/usr/bin/env python3
"""Exercise the exact production LateUpdate capture gate offline; no Unity/native calls."""
from pathlib import Path
import os
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def declaration(source, signature):
    start = source.index(signature)
    brace = source.index('{', start)
    depth, end = 1, brace+1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


PROGRAM = r"""
using System;
class Step {public string sceneName="Choice";}
class Main {public string ExperimentPhase="stimulus";public bool SequenceRunning=true,SceneTransitionPending;public Step step=new Step();public Step GetCurrentSequenceStep()=>step;}
class Scene {public string name="Choice";}
class GameObject {public Scene scene=new Scene();}
class Actual {
 public bool isLogging=true;public object logFile=new object();public Main mainController=new Main();public GameObject gameObject=new GameObject();public string line="";public int prepared,written;
 private void PrepareLogData(){prepared++;}private void LogData(string text){written++;}
 public void Tick()=>LateUpdate();
 __PRODUCTION__
}
static class Regression {
 static int checks;
 static void Check(bool yes,string label){checks++;if(!yes)throw new Exception(label);}
 static void Sample(Actual logger,bool expected,string label){logger.Tick();Check(logger.written==(expected?1:0)&&logger.prepared==(expected?1:0),label);}
 static void Main(){
  Sample(new Actual(),true,"stable active experiment must record");
  var a=new Actual();a.mainController.step.sceneName="Choice_Selwyn";Sample(a,false,"outgoing scene must not record the next scene metadata");
  a=new Actual();a.mainController.SceneTransitionPending=true;Sample(a,false,"same-scene full reload must suppress outgoing samples");
  a=new Actual();a.mainController.SequenceRunning=false;Sample(a,false,"stopped sequence must not append a mislabeled idle tail");
  a=new Actual();a.mainController.step=null;Sample(a,false,"absent sequence context must not record");
  a=new Actual();a.gameObject.scene.name="";Sample(a,false,"invalid source scene must not record");
  a=new Actual();a.mainController.step.sceneName="choice";Sample(a,true,"case-compatible scene loading must still record");
  a=new Actual();a.mainController=null;Sample(a,true,"standalone loggers without a sequence owner retain historical behavior");
  a=new Actual();a.isLogging=false;Sample(a,false,"explicitly stopped logger must remain stopped");
  a=new Actual();a.logFile=null;Sample(a,false,"uninitialized logger must not record");
  a=new Actual();a.mainController.SceneTransitionPending=true;a.Tick();a.mainController.SceneTransitionPending=false;Sample(a,true,"new loaded scene resumes after transition acknowledgment");
  // Phase labels do not gate capture. Pre/post stimulus and assessment are valid
  // while the sequence is running in its matching stable scene.
  foreach(string phase in new[]{"preStimulus","stimulus","postStimulus","assessment"}){a=new Actual();a.mainController.ExperimentPhase=phase;Sample(a,true,"running same-scene "+phase+" exposure remains recorded");}
  Console.WriteLine("PASS: "+checks+" exact production LateUpdate handoff checks with controlled dependencies; no Unity/native process.");
 }
}
"""


def main():
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    version = max(map(int,re.findall(r'^(\d+)\.',subprocess.check_output([dotnet,'--list-sdks'],text=True),re.M)))
    source = (ROOT/'Assets/Scripts/Loggers/DataLogger.cs').read_text()
    production = declaration(source,'protected virtual void LateUpdate(')+declaration(source,'private static bool CanCaptureContext(')
    with tempfile.TemporaryDirectory(prefix='matrex-log-handoff-') as temporary:
        folder=Path(temporary)
        (folder/'Program.cs').write_text(PROGRAM.replace('__PRODUCTION__',production))
        (folder/'Regression.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'+f'<TargetFramework>net{version}.0</TargetFramework></PropertyGroup></Project>')
        (folder/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        env=dict(os.environ,DOTNET_CLI_HOME=str(folder),DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        result=subprocess.run([dotnet,'build',str(folder/'Regression.csproj'),'--configfile',str(folder/'NuGet.Config'),'-p:UseSharedCompilation=false','-v:q'],env=env,capture_output=True,text=True)
        if result.returncode:raise SystemExit(result.stdout+result.stderr)
        subprocess.run([dotnet,str(folder/f'bin/Debug/net{version}.0/Regression.dll')],env=env,check=True)


if __name__=='__main__':
    main()
