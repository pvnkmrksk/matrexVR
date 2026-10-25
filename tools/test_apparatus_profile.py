#!/usr/bin/env python3
"""Exercise the production apparatus JSON model without Unity, hardware or native apps."""
from pathlib import Path
from xml.sax.saxutils import escape
import os
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
PROGRAM = r'''
using System;
using System.Linq;
using ConfigurationUI;
using Newtonsoft.Json.Linq;
static class Regression {
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static JObject Demo()=>JObject.Parse("{\"desktopQuickStart\":{\"version\":1,\"demo\":true},\"unknownRoot\":{\"keep\":9},\"configs\":[{\"vrId\":\"VR1\",\"speciesName\":\"Drosophila\",\"closedLoopMode\":\"Rekkefly\",\"desktopPreview\":true,\"displayOrder\":\"F\",\"ledPanelWidth\":3024,\"ledPanelHeight\":1898,\"startRow\":0,\"startCol\":0,\"horizontal\":true,\"zmqAddress\":\"127.0.0.1\",\"zmqPort\":9871,\"targetDisplay\":0,\"unknownRig\":\"keep\",\"tracker\":{\"configFile\":\"native-calibration.toml\"}}]}");
 static void Main(){
  Check(!DesktopApparatusProfile.IsConfigured(null),"null treated configured");
  Check(!DesktopApparatusProfile.IsConfigured(JObject.Parse("{\"apparatusConfiguration\":\"unknown future value\"}")),"malformed marker accepted");
  var original=Demo();var copy=(JObject)original.DeepClone();var before=original.ToString();
  DesktopApparatusProfile.Begin(copy,"led",true);
  Check(original.ToString()==before,"source demo changed by clone transaction");
  Check(!DesktopApparatusProfile.IsConfigured(copy),"draft prematurely completed");
  Check(copy["desktopQuickStart"]==null,"hardware keeps demo marker");
  var rig=(JObject)copy["configs"][0];
  Check((int)rig["ledPanelWidth"]==128&&(int)rig["ledPanelHeight"]==128,"fresh LED defaults not128");
  Check((string)rig["displayOrder"]=="DRBLFU"&&!(bool)rig["desktopPreview"],"fresh LED camera allowlist wrong");
  Check((string)rig["unknownRig"]=="keep"&&(int)copy["unknownRoot"]["keep"]==9,"unknown fields lost");
  Check((string)rig["tracker"]["configFile"]=="native-calibration.toml","existing native calibration changed");
  DesktopApparatusProfile.ResizeRigs(copy,4);
  var rows=DesktopApparatusProfile.Rigs(copy).ToList();
  Check(rows.Count==4,"VR count wrong");
  Check(rows.Select(r=>(string)r["vrId"]).Distinct().Count()==4,"VR identities collide");
  Check(rows.Select(r=>(int)r["zmqPort"]).Distinct().Count()==4,"new local inputs collide");
  Check(rows.Skip(1).All(r=>r["tracker"]==null),"new rigs reused another rig's calibration file");
  Check(rows.Select(r=>(int)r["startRow"]).SequenceEqual(new[]{0,1,2,3}),"new LED strips overlap");
  Check(DesktopApparatusProfile.Validate(copy).Count==0,"valid LED setup rejected");
  DesktopApparatusProfile.Complete(copy);
  Check(DesktopApparatusProfile.IsConfigured(copy),"completed marker absent");
  Check(DesktopApparatusProfile.ShouldPrepareCompanions(copy),"fresh setup does not opt into native trackers");
  Check(!DesktopApparatusProfile.ShouldLaunchBundledForRig(copy,rows[0]),"explicit external tracker replaced");
  Check(DesktopApparatusProfile.ShouldLaunchBundledForRig(copy,rows[1]),"eligible local modern rig omitted");
  rows[1]["zmqAddress"]="192.0.2.1";
  Check(!DesktopApparatusProfile.ShouldLaunchBundledForRig(copy,rows[1]),"remote tracker would spawn locally");
  rows[1]["zmqAddress"]="localhost";rows[1]["closedLoopMode"]="FicTrac";
  Check(!DesktopApparatusProfile.ShouldLaunchBundledForRig(copy,rows[1]),"legacy tracker replaced without operator choice");
  DesktopApparatusProfile.SetMovement(copy,"walking");
  Check(rows.All(r=>(string)r["closedLoopMode"]=="Hejjetrac"),"walking choice not reflected in system JSON");
  DesktopApparatusProfile.SetOrder(rows[0]," rblf ");
  Check(DesktopApparatusProfile.Order(rows[0])=="RBLF","four-face order normalization wrong");
  bool rejected=false;try{DesktopApparatusProfile.SetOrder(rows[0],"FF");}catch(ArgumentException){rejected=true;}
  Check(rejected&&DesktopApparatusProfile.Order(rows[0])=="RBLF","invalid order changed renderer layout");
  rows[2]["zmqPort"]=9872;
  Check(DesktopApparatusProfile.Validate(copy).Any(e=>e.Contains("same tracker endpoint")),"localhost aliases hid duplicate native bindings");
  copy[DesktopApparatusProfile.MarkerName]["launchBundledTrackers"]=false;
  Check(!DesktopApparatusProfile.Validate(copy).Any(e=>e.Contains("same tracker endpoint"))&&DesktopApparatusProfile.SharedInputWarnings(copy).Count>0,"intentional external shared input was rejected or hidden");
  copy[DesktopApparatusProfile.MarkerName]["launchBundledTrackers"]=true;rows[2]["zmqAddress"]="192.0.2.1";
  Check(!DesktopApparatusProfile.Validate(copy).Any(e=>e.Contains("same tracker endpoint")),"independent remote host incorrectly collides");
  DesktopApparatusProfile.ResizeRigs(copy,2);
  Check(DesktopApparatusProfile.Rigs(copy).Count()==2&&(string)copy["configs"][0]["tracker"]["configFile"]=="native-calibration.toml","resize lost retained calibration");
  var imported=Demo();imported.Remove("desktopQuickStart");var calibration=imported["configs"][0]["tracker"].ToString();
  DesktopApparatusProfile.Begin(imported,"led",false);
  Check((int)imported["configs"][0]["ledPanelWidth"]==3024,"imported geometry silently reset");
  Check(imported["configs"][0]["tracker"].ToString()==calibration,"imported calibration normalized");
  Check(!DesktopApparatusProfile.ShouldPrepareCompanions(imported),"imported external workflow auto-launches apps");
  DesktopApparatusProfile.SetPresentation(copy,"desktop");
  Check(DesktopApparatusProfile.Rigs(copy).All(r=>(bool)r["desktopPreview"]&&(string)r["displayOrder"]=="F"),"single-monitor preview does not render a single full view");
  Check(DesktopApparatusProfile.Validate(copy).Any(e=>e.Contains("separate output displays")),"multiple desktop VRs share a full-screen output without warning");
  copy["configs"][1]["targetDisplay"]=1;
  Check(DesktopApparatusProfile.Validate(copy).Any(e=>e.Contains("exceeds the configured display count")),"output index ignores declared display count");
  copy[DesktopApparatusProfile.MarkerName]["monitorCount"]=2;
  Check(!DesktopApparatusProfile.Validate(copy).Any(e=>e.Contains("separate output displays")||e.Contains("exceeds the configured display count")),"separate declared desktop outputs rejected");
  var mixed=Demo();DesktopApparatusProfile.Begin(mixed,"led",true);mixed["configs"][0]["ledPanelHeight"]=128;DesktopApparatusProfile.ResizeRigs(mixed,2);mixed["configs"][1]["ledPanelHeight"]=64;mixed["configs"][1]["startRow"]=2;DesktopApparatusProfile.ResizeRigs(mixed,3);
  Check((int)mixed["configs"][2]["startRow"]>=3,"mixed panel sizes overlap after adding rig");
  Check(DesktopApparatusProfile.FaceName('U')=="Up"&&DesktopApparatusProfile.FaceName('D')=="Down","face labels differ from renderer letters");
  string folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"matrex-starter-fork-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(folder);
  try {
   string reference="QuickStart/flying-night-sky-demo/night-sky.json",path=System.IO.Path.Combine(folder,reference);System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
   var environment=JObject.Parse("{\"closedLoopPosition\":false,\"autopilotEnabled\":true,\"objects\":[]}");System.IO.File.WriteAllText(path,environment.ToString());string originalBytes=System.IO.File.ReadAllText(path);
   var sequence=new JObject {["autoStart"]=false,["sequences"]=new JArray(new JObject {["sceneName"]="Choice_Selwyn",["duration"]=600,["parameters"]=new JObject {["configFile"]=reference}})};
   var fork=ApparatusStarterFiles.Fork(sequence,folder,(r,scene)=>environment,sequence,environment);
   Check(fork.FactoryStarter&&fork.CopiedSources.Count==1,"factory starter was not recognized");
   Check(System.IO.File.ReadAllText(path)==originalBytes,"fork rewrote the source demo experiment");
   var saved=JObject.Parse(System.IO.File.ReadAllText(fork.SequencePath));Check(((string)saved["sequences"][0]["parameters"]["configFile"]).StartsWith("Apparatus/Experiments/"),"daily sequence still references demo files");
   Check(ApparatusStarterFiles.Fingerprint(JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(folder,fork.ExperimentReference))))==ApparatusStarterFiles.Fingerprint(environment),"fork changed experiment settings");
   var edited=(JObject)environment.DeepClone();edited["operatorSetting"]=17;string second=System.IO.Path.Combine(folder,"edited");System.IO.Directory.CreateDirectory(second);
   var customized=ApparatusStarterFiles.Fork(sequence,second,(r,scene)=>edited,sequence,environment);Check(!customized.FactoryStarter,"edited starter would receive automatic motion defaults");
   var importedSequence=(JObject)sequence.DeepClone();importedSequence["sequences"][0]["parameters"]["configFile"]="Experiments/operator-authored.json";string third=System.IO.Path.Combine(folder,"imported");System.IO.Directory.CreateDirectory(third);
   var external=ApparatusStarterFiles.Fork(importedSequence,third,(r,scene)=>throw new Exception("custom reference must not be read or copied"),sequence,environment);
   Check(external.CopiedSources.Count==0&&!external.FactoryStarter&&(string)JObject.Parse(System.IO.File.ReadAllText(external.SequencePath))["sequences"][0]["parameters"]["configFile"]=="Experiments/operator-authored.json","imported experiment references were rewritten");
  } finally { System.IO.Directory.Delete(folder,true); }

  Console.WriteLine("PASS: "+checks+" apparatus profile checks; no hardware or native apps launched.");
 }
}
'''


def main():
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    sdk = subprocess.check_output([dotnet, '--list-sdks'], text=True)
    version = max(map(int, re.findall(r'^(\d+)\.', sdk, re.M)))
    newtonsoft = next((ROOT / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    with tempfile.TemporaryDirectory(prefix='matrex-apparatus-model-') as folder:
        temporary = Path(folder)
        environment = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        (temporary / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (temporary / 'Program.cs').write_text(PROGRAM)
        project = temporary / 'Regression.csproj'
        production = ROOT / 'Assets/Scripts/ConfigurationUI/DesktopApparatusProfile.cs'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(newtonsoft))}</HintPath></Reference><Compile Include="{escape(str(production))}" /><Compile Include="{escape(str(ROOT / "Assets/Scripts/ConfigurationUI/ApparatusStarterFiles.cs"))}" /><Compile Include="{escape(str(ROOT / "Assets/Scripts/ConfigurationUI/LosslessJson.cs"))}" /></ItemGroup></Project>')
        result = subprocess.run([dotnet, 'build', str(project), '--configfile', str(temporary / 'NuGet.Config'), '-p:UseSharedCompilation=false', '-v:q'], env=environment, capture_output=True, text=True)
        if result.returncode:
            raise SystemExit(result.stdout + result.stderr)
        subprocess.run([dotnet, str(temporary / f'bin/Debug/net{version}.0/Regression.dll')], env=environment, check=True)


if __name__ == '__main__':
    main()
