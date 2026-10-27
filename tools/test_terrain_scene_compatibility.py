#!/usr/bin/env python3
"""Exercise production pre-start terrain compatibility and phase resolution, offline.

Compiles the exact ConfigWorkspace guard and production phase parser against the
pinned Newtonsoft DLL. No Unity player, tracker, or user profile is started.
"""
from pathlib import Path
from xml.sax.saxutils import escape
import os
import re
import shutil
import subprocess
import tempfile

from test_system_display_routing import declaration

ROOT = Path(__file__).resolve().parents[1]
PROGRAM = r'''
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConfigurationUI;
using Newtonsoft.Json.Linq;
static class Regression {
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static JObject Terrain(string visual="RS",string collision="RS")=>new JObject{["rugosity"]=new JObject{["enabled"]=true,["visualSurface"]=visual,["collisionSurface"]=collision}};
 static List<string> Run(JObject config,string scene="Rugosity_RS",JObject patch=null,string reference="experiment.json"){
  var step=new JObject{["sceneName"]=scene,["parameters"]=new JObject{["configFile"]=reference}};
  if(patch!=null)step["parameters"]["rugosity"]=patch;
  var workspace=new ConfigWorkspace(new JObject{["sequences"]=new JArray(step)},config);
  string before=config.ToString(),sequenceBefore=workspace.Sequence.Root.ToString();
  var errors=workspace.TerrainErrors();
  Check(config.ToString()==before&&workspace.Sequence.Root.ToString()==sequenceBefore,"Compatibility inspection preserves both authoritative JSON documents");
  return errors;
 }
 static JObject Read(string repo,string name)=>JObject.Parse(File.ReadAllText(Path.Combine(repo,"Assets/StreamingAssets/Examples/Rugosity",name+".json")));
 static void Main(string[] args){
  string repo=args[0];ExperimentConfigFiles.Root=repo;
  var observed=Run(Read(repo,"visual-flat_navigation-terrain"));
  Check(observed.Count==1&&observed[0].Contains("collisionSurface='RoughSmooth'")&&observed[0].Contains("Sequence block 1 (Rugosity_RS)")&&observed[0].Contains("TerrainCueLab")&&observed[0].Contains("TerrainChoice"),"Reject the observed new-config/legacy-scene pair with block, role and repair guidance");
  Check(Run(Terrain("Flat","RoughSmooth"),reference:"invalid\0experiment.json").Count==0,"Invalid file paths do not throw from the compatibility pass; the reference validator owns the diagnostic");
  foreach(string file in new[]{"traditional-rs","visual-flat-hidden-rs","visual-flat-tilt-only","visual-rs-fixed-height","visual-rs-level-agl","flat-control"})
   if(File.Exists(Path.Combine(repo,"Assets/StreamingAssets/Examples/Rugosity",file+".json")))Check(Run(Read(repo,file)).Count==0,"Retain legacy configuration "+file);
  var shared=Read(repo,"terrain-treatment");
  foreach(string treatment in new[]{"matched-terrain","visual-terrain_navigation-flat","visual-flat_navigation-terrain","matched-flat"})
   foreach(JObject step in (JArray)Read(repo,"sequence-treatment-"+treatment)["sequences"])
    Check(Run(shared,(string)step["sceneName"],step["parameters"]["rugosity"] as JObject).Count==0,"Accept real sixteen-case sequence row "+treatment+"/"+step["sceneName"]);
  Check(Run(Terrain("RoughSmooth","RoughSmooth"),"TerrainCueLab").Count==0,"Current terrain lab accepts its registered surface family");
  Check(Run(Terrain("RoughSmooth","RoughSmooth"),"MyCustomTerrain").Count==0,"Custom scenes retain runtime-owned surface registration");
  Check(Run(Terrain("CustomSurface","CustomSurface")).Count==0,"Do not prohibit custom extensions to a legacy scene");
  Check(Run(Terrain(),patch:JObject.Parse("{\"enabled\":false,\"collisionSurface\":\"RoughSmooth\"}")).Count==0,"Disabled rugosity does not require surface resolution");
  var disabled=Terrain("RoughSmooth","RoughSmooth");disabled["rugosity"]["enabled"]=false;
  Check(Run(disabled,patch:JObject.Parse("{\"enabled\":true}")).Count==1,"Sequence override enabling rugosity is checked");
  Check(Run(Terrain("RoughSmooth","RoughSmooth"),patch:JObject.Parse("{\"visualSurface\":\"RS\",\"collisionSurface\":\"Flat\"}")).Count==0,"Effective sequence overrides can make a legacy pairing compatible");
  Check(Run(Terrain("RoughSmooth","Flat"),patch:JObject.Parse("{\"visualSurface\":null}")).Count==1,"Null sequence override preserves the production inherited surface");
  Check(Run(Terrain("Flat","Flat"),patch:JObject.Parse("{\"collisionSurface\":\"SmoothRough\"}")).Count==1,"Sequence override introducing a new surface is rejected");
  foreach(string role in new[]{"heightSurface","normalSurface"}){
   var config=Terrain();config["rugosity"][role]="RoughRough";
   Check(Run(config).Count==1,"Active explicit "+role+" is resolved and checked");
  }
  foreach(var modes in new[]{JObject.Parse("{\"heightMode\":\"Absolute\",\"orientationMode\":\"WorldUp\"}"),JObject.Parse("{\"heightMode\":1,\"orientationMode\":0}")}){
   var config=Terrain();((JObject)config["rugosity"]).Merge(modes);config["rugosity"]["heightSurface"]="SmoothSmooth";config["rugosity"]["normalSurface"]="RoughRough";
   Check(Run(config).Count==0,"Inactive explicit height/normal surfaces do not generate false incompatibility errors");
  }
  Check(Run(new JObject()).Count==0,"Omitted settings retain stock scene defaults");
  var omitted=Terrain("RoughSmooth","Flat");((JObject)omitted["rugosity"]).Remove("enabled");
  Check(Run(omitted).Count==1,"Omitted enabled inherits enabled stock legacy world");
  var phase=new JObject{["preStimulus"]=new JObject{["enabled"]=false,["durationSeconds"]=1,["rugosity"]=Terrain("RoughSmooth","RoughSmooth")["rugosity"]},["stimulus"]=new JObject{["durationSeconds"]=1,["rugosity"]=Terrain()["rugosity"]}};
  Check(Run(phase).Count==0,"Disabled incompatible phases are not run or rejected");
  phase["stimulus"]["rugosity"] = Terrain("Flat","SmoothRough")["rugosity"];
  Check(Run(phase).Count==1,"Enabled phase uses its own effective terrain settings");
  Check(Run(phase,patch:JObject.Parse("{\"collisionSurface\":\"RS\"}")).Count==0,"Sequence override takes precedence over phase payload");
  Console.WriteLine("PASS: "+checks+" production terrain scene compatibility and preservation checks; actual phase parser, no native input or rendering claim.");
 }
}
'''


def main():
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    version = max(map(int, re.findall(r'^(\d+)\.', subprocess.check_output([dotnet, '--list-sdks'], text=True), re.M)))
    newtonsoft = next((ROOT / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    source = (ROOT / 'Assets/Scripts/ConfigurationUI/ConfigWorkspace.cs').read_text()
    guard = '\n'.join(declaration(source, signature) for signature in (
        'private void ValidateTerrainSceneCompatibility(', 'private static string TerrainSurfaceCompatibilityError('))
    blocks = (ROOT / 'Assets/Scripts/ConfigurationUI/ExperimentBlocks.cs').read_text()
    # Include the real expression-bodied flag/phase predicates, not test copies.
    predicates = '\n'.join(line.strip() for line in blocks.splitlines()
                           if line.strip().startswith(('public static bool Flag(', 'public static bool Phased(')))
    phase = declaration((ROOT / 'Assets/Scripts/ExperimentPhases.cs').read_text(), 'public sealed class ExperimentPhase')
    lifecycle = declaration(source, 'public List<string> Validate(')
    assert 'ValidateTerrainSceneCompatibility(errors);' in lifecycle
    workbench = (ROOT / 'Assets/Scripts/ConfigurationUI/ConfigWorkbench.cs').read_text()
    start = declaration(workbench, 'public void StartExperiment()')
    assert start.index('var errors=Workspace.Validate();') < start.index('BeginCompanionPreparation(true)')
    assert 'start.SetEnabled(errors.Count==0 && !rawPending)' in workbench
    production = '''using System;using System.Collections.Generic;using System.IO;using System.Linq;using Newtonsoft.Json.Linq;
public class HeadingReferenceConfig { public bool enabled;public void Validate(){}public void WithinDuration(double seconds,out string warning){warning=null;} }
public static class ExperimentConfigFiles {public static string Root;public static string Resolve(string value)=>Path.Combine(Root,value);}
namespace ConfigurationUI {
public class ConfigDocument {public string Path;public JToken Root;}
public class ConfigWorkspace {
 public ConfigDocument Sequence;public List<ConfigDocument> Documents=new List<ConfigDocument>();
 public ConfigWorkspace(JObject sequence,JObject config){Sequence=new ConfigDocument{Root=sequence};Documents.Add(new ConfigDocument{Path=Path.GetFullPath(ExperimentConfigFiles.Resolve("experiment.json")),Root=config});}
 public List<string> TerrainErrors(){var errors=new List<string>();ValidateTerrainSceneCompatibility(errors);return errors;}
''' + guard + '\n}\npublic static class ExperimentBlocks {\n' + predicates + '\n}\n}\n' + phase
    with tempfile.TemporaryDirectory(prefix='matrex-terrain-compatibility-') as folder:
        temporary = Path(folder)
        (temporary / 'Production.cs').write_text(production)
        (temporary / 'Program.cs').write_text(PROGRAM)
        (temporary / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        project = temporary / 'Regression.csproj'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(newtonsoft))}</HintPath></Reference></ItemGroup></Project>')
        env = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        result = subprocess.run([dotnet, 'build', str(project), '--configfile', str(temporary / 'NuGet.Config'), '-p:UseSharedCompilation=false', '-v:q'], env=env, capture_output=True, text=True)
        if result.returncode:
            raise SystemExit(result.stdout + result.stderr)
        subprocess.run([dotnet, str(temporary / f'bin/Debug/net{version}.0/Regression.dll'), str(ROOT)], env=env, check=True)
    print('PASS: 3 source wiring guards verify GUI Start gating and validation before experiment companion preparation.')


if __name__ == '__main__':
    main()
