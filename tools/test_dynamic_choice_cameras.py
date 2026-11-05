#!/usr/bin/env python3
"""Run production dynamic-choice camera selection against saved four-rig designs.

Uses an offline .NET build with camera spies. Unity rendering is checked separately.
"""
from pathlib import Path
from test_system_display_routing import declaration
from xml.sax.saxutils import escape
import os
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
PRELUDE = r'''
using System;using System.IO;using System.Linq;using System.Collections.Generic;using Newtonsoft.Json.Linq;
enum CameraClearFlags { Skybox=1,SolidColor=2,Depth=3,Nothing=4 }
struct Color { public float r,g,b,a; public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;} }
class Camera { public CameraClearFlags clearFlags=CameraClearFlags.SolidColor;public Color backgroundColor; }
class Rig { public Camera[] cameras=Enumerable.Range(0,6).Select(_=>new Camera()).ToArray();
 public T[] GetComponentsInChildren<T>(bool includeInactive)=>cameras.Cast<T>().ToArray(); }
class Regression {
 readonly Dictionary<string,Rig> players=new();
 static Color ToColor(float[] value)=>new Color(value[0],value[1],value[2],value.Length>3?value[3]:1);
 static int checks;static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
'''
TESTS = r'''
 static void Main(string[] args){
  var r=new Regression();for(int i=1;i<=16;i++)r.players["VR"+i]=new Rig();
  foreach(string design in new[]{"PathAnglePilot","HistoricalReplicationPreview"}){
   var doc=JObject.Parse(File.ReadAllText(Path.Combine(args[0],"Assets/StreamingAssets/Experiments",design,"design.json")));
   var intertrial=doc["intertrial"]["camera"].ToObject<CameraSpec[]>();
   foreach(int index in new[]{1,5,8,16}){
    string id="VR"+index;var cameras=r.players[id].cameras;
    foreach(var camera in cameras)camera.clearFlags=CameraClearFlags.SolidColor;
    r.ApplyCameraSettings(id,intertrial);
    Check(cameras.All(c=>c.clearFlags==CameraClearFlags.Skybox),design+" intertrial must show skybox on every camera of "+id);
    foreach(var step in doc["steps"]){
     var specs=step["camera"].ToObject<CameraSpec[]>();r.ApplyCameraSettings(id,specs);
     Check(cameras.All(c=>c.clearFlags==specs[0].clearFlags),design+" trial clear flags reach "+id);
     Check(cameras.All(c=>Math.Abs(c.backgroundColor.r-specs[0].bgColor[0])<.0001),design+" trial background reaches "+id);
     r.ApplyCameraSettings(id,intertrial);
     Check(cameras.All(c=>c.clearFlags==CameraClearFlags.Skybox),design+" returns to intertrial skybox on "+id);
    }
   }
  }
  CameraSpec[] Legacy()=>Enumerable.Range(1,4).Select(i=>new CameraSpec{vrId="VR"+i,clearFlags=CameraClearFlags.Skybox}).ToArray();
  void Reset(){foreach(var camera in r.players["VR5"].cameras)camera.clearFlags=CameraClearFlags.Depth;}
  var nonuniform=Legacy();nonuniform[1].clearFlags=CameraClearFlags.Nothing;Reset();r.ApplyCameraSettings("VR5",nonuniform);
  Check(r.players["VR5"].cameras.All(c=>c.clearFlags==CameraClearFlags.Depth),"Do not infer settings from mixed per-rig clear flags");
  nonuniform=Legacy();nonuniform[1].bgColor=new[]{1f,0f,0f};Reset();r.ApplyCameraSettings("VR5",nonuniform);
  Check(r.players["VR5"].cameras.All(c=>c.clearFlags==CameraClearFlags.Depth),"Do not infer settings from mixed per-rig colors");
  Reset();r.ApplyCameraSettings("VR5",Legacy().Take(1).ToArray());
  Check(r.players["VR5"].cameras.All(c=>c.clearFlags==CameraClearFlags.Depth),"An explicit partial rig list remains partial");
  var duplicate=Legacy();duplicate[3].vrId="VR3";Reset();r.ApplyCameraSettings("VR5",duplicate);
  Check(r.players["VR5"].cameras.All(c=>c.clearFlags==CameraClearFlags.Depth),"Duplicate rig IDs do not imply a shared legacy default");
  var custom=Legacy().Concat(new[]{new CameraSpec{vrId="VR5",clearFlags=CameraClearFlags.Nothing}}).ToArray();r.ApplyCameraSettings("VR5",custom);
  Check(r.players["VR5"].cameras.All(c=>c.clearFlags==CameraClearFlags.Nothing),"Explicit VR5 settings win");
  r.ApplyCameraSettings("VR99",Legacy());Check(!r.players.ContainsKey("VR99"),"Camera application does not invent rigs");
  var defaults=r.BuildDefaultCameraSpecs();
  Check(defaults.Select(s=>s.vrId).OrderBy(s=>s).SequenceEqual(r.players.Keys.OrderBy(s=>s)),"Adaptive defaults cover all active rigs");
  foreach(var camera in r.players["VR16"].cameras)camera.clearFlags=CameraClearFlags.Skybox;
  r.ApplyCameraSettings("VR16",defaults);Check(r.players["VR16"].cameras.All(c=>c.clearFlags==CameraClearFlags.SolidColor),"Adaptive defaults reach VR16");
  Console.WriteLine("PASS: "+checks+" production dynamic-choice camera checks; saved protocols, six camera faces, VR1/5/8/16 and explicit overrides.");
 }
}
'''


def main():
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    version = max(map(int, re.findall(r'^(\d+)\.', subprocess.check_output([dotnet, '--list-sdks'], text=True), re.M)))
    source = (ROOT / 'Assets/Scripts/DynamicSequenceController.cs').read_text()
    signatures = ['public class CameraSpec', 'private void ApplyCameraSettings(string vrId, CameraSpec[] specs)',
                  'private CameraSpec[] BuildDefaultCameraSpecs()']
    helper = 'private static bool UniformLegacyCameraSettings(CameraSpec[] specs)'
    if helper in source:
        signatures.append(helper)
    production = '\n'.join(declaration(source, signature) for signature in signatures)
    dll = next((ROOT / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    with tempfile.TemporaryDirectory(prefix='matrex-dynamic-cameras-') as folder:
        temporary = Path(folder)
        env = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        (temporary / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (temporary / 'Program.cs').write_text(PRELUDE + production + TESTS)
        project = temporary / 'Regression.csproj'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(dll))}</HintPath></Reference></ItemGroup></Project>')
        result = subprocess.run([dotnet, 'build', str(project), '--configfile', str(temporary / 'NuGet.Config'), '-p:UseSharedCompilation=false', '-v:q'], env=env, capture_output=True, text=True)
        if result.returncode:
            raise SystemExit(result.stdout + result.stderr)
        subprocess.run([dotnet, str(temporary / f'bin/Debug/net{version}.0/Regression.dll'), str(ROOT)], env=env, check=True)


if __name__ == '__main__':
    main()
