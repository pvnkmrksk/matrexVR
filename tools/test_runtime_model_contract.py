#!/usr/bin/env python3
"""Run the production GLB preflight against embedded and unsupported asset cases.

This is a format/archivability check, not a renderer or cross-platform claim.
"""
from pathlib import Path
from xml.sax.saxutils import escape
import os
import re
import shutil
import subprocess
import tempfile
from test_replay_scene_isolation import declaration

ROOT = Path(__file__).resolve().parents[1]
SOURCE = r'''
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
static class Regression {
 static int checks;
 static void Check(bool result,string label){checks++;if(!result)throw new Exception(label);}
 static byte[] Glb(JObject json){var text=Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));int length=(text.Length+3)&~3;using var memory=new MemoryStream();using var writer=new BinaryWriter(memory);writer.Write(0x46546c67u);writer.Write(2u);writer.Write((uint)(20+length));writer.Write((uint)length);writer.Write(0x4e4f534au);writer.Write(text);for(int i=text.Length;i<length;i++)writer.Write((byte)32);return memory.ToArray();}
 static JObject Scene()=>new JObject{["asset"]=new JObject{["version"]="2.0"},["buffers"]=new JArray(new JObject{["byteLength"]=0})};
 static void Reject(byte[] bytes,string label,string name="animal.glb"){bool rejected=false;try{ExternalModelLibrary.ValidateEmbeddedGlb(name,bytes);}catch(ArgumentException){rejected=true;}catch(InvalidDataException){rejected=true;}Check(rejected,label);}
 static void Main(){
  var plain=Scene();ExternalModelLibrary.ValidateEmbeddedGlb("Models/animal.GLB",Glb(plain));Check(true,"Self-contained GLB and case-insensitive extension accepted");
  var embedded=Scene();embedded["images"]=new JArray(new JObject{["uri"]="data:image/png;base64,AA=="});ExternalModelLibrary.ValidateEmbeddedGlb("tree.glb",Glb(embedded));Check(true,"Embedded data URI is archivable");
  var view=Scene();view["images"]=new JArray(new JObject{["bufferView"]=0,["mimeType"]="image/png"});ExternalModelLibrary.ValidateEmbeddedGlb("tree.glb",Glb(view));Check(true,"Embedded image buffer view accepted");
  foreach(string uri in new[]{"texture.png","../outside.bin","https://example.invalid/model.bin","file:///tmp/a.bin"}){var j=Scene();j["images"]=new JArray(new JObject{["uri"]=uri});Reject(Glb(j),"Nonembedded dependency rejected: "+uri);}
  foreach(string extension in new[]{"KHR_draco_mesh_compression","KHR_texture_basisu","EXT_meshopt_compression"}){
   var j=Scene();j["extensionsRequired"]=new JArray(extension);Reject(Glb(j),"Missing required decoder rejected: "+extension);
   j.Remove("extensionsRequired");j["extensionsUsed"]=new JArray(extension);ExternalModelLibrary.ValidateEmbeddedGlb("fallback.glb",Glb(j));Check(true,"Optional extension retains uncompressed fallback: "+extension);
  }
  foreach(string name in new[]{"model.fbx","model.gltf","model.unitypackage"})Reject(Glb(plain),"Non-GLB source requires export: "+name,name);
  Reject(null,"Missing bytes rejected");Reject(new byte[19],"Short header rejected");
  var corrupt=Glb(plain);corrupt[4]=1;Reject(corrupt,"Wrong version rejected");
  corrupt=Glb(plain);corrupt[8]++;Reject(corrupt,"Inconsistent total length rejected");
  corrupt=Glb(plain);Array.Copy(BitConverter.GetBytes(uint.MaxValue),0,corrupt,12,4);Reject(corrupt,"Truncated JSON rejected");
  var refs=JObject.Parse("{objects:[{enabled:false,modelPath:'hidden.glb'},{modelPath:'tree.glb'},{nested:{kannadiModelPath:'animal.glb'}}],other:'no.glb'}");
  Check(ExternalModelLibrary.CollectReferences(refs).SequenceEqual(new[]{"tree.glb","animal.glb"}),"Only active nested model references are preloaded");
  Console.WriteLine("PASS: "+checks+" production GLB format/dependency/decoder/reference checks; no render claim.");
 }
}
'''

def main():
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise SystemExit("A local .NET SDK is required.")
    sdk = max(map(int, re.findall(r"^(\d+)\.", subprocess.check_output([dotnet, "--list-sdks"], text=True), re.M)))
    newtonsoft = next((ROOT / "Library/PackageCache").glob("com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll"))
    production = (ROOT / "Assets/Scripts/ExternalModelLibrary.cs").read_text()
    methods = declaration(production, "public static void ValidateEmbeddedGlb(") + declaration(production, "public static IEnumerable<string> CollectReferences(")
    with tempfile.TemporaryDirectory(prefix="matrex-model-contract-") as folder:
        temp = Path(folder)
        (temp / "Program.cs").write_text(SOURCE + "\nstatic class ExternalModelLibrary {" + methods + "}\n")
        (temp / "NuGet.Config").write_text("<configuration><packageSources><clear /></packageSources></configuration>")
        project = temp / "Regression.csproj"
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{sdk}.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(newtonsoft))}</HintPath></Reference></ItemGroup></Project>')
        env = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1")
        result = subprocess.run([dotnet, "build", str(project), "--configfile", str(temp / "NuGet.Config"), "-p:UseSharedCompilation=false", "-v:q"], env=env, capture_output=True, text=True)
        if result.returncode:
            print(result.stdout + result.stderr)
            raise SystemExit(result.returncode)
        subprocess.run([dotnet, str(temp / f"bin/Debug/net{sdk}.0/Regression.dll")], env=env, check=True)

if __name__ == "__main__":
    main()
