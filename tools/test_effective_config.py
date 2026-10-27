#!/usr/bin/env python3
"""Compile default-resolution and damaged-document behavior from production C#.

No Unity process, camera, network, or user configuration is modified.
"""
from pathlib import Path
import os
import re
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]
STUBS = r'''
using System;using System.IO;using System.Text;using Newtonsoft.Json.Linq;using ConfigurationUI;
namespace UnityEngine { public class TextAsset { public string text; } public static class Resources { public static T Load<T>(string name)=>default; } }
public static class RunConfigArchive { public static string Decode(byte[] bytes)=>Encoding.UTF8.GetString(bytes).TrimStart('\ufeff'); public static string Hash(byte[] bytes)=>Convert.ToBase64String(bytes); }
class Regression {
static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
static void Main(){
 var schema=JObject.Parse(@"{'type':'object','default':{'speed':999},'properties':{'speed':{'type':'number','default':3,'minimum':0},'active':{'type':'boolean','default':true},'options':{'type':['object','null'],'default':{'enabled':false},'properties':{'enabled':{'type':'boolean','default':false},'gain':{'type':'number','default':2}}},'optional':{'type':['string','null'],'default':null},'phase':{'type':'object','properties':{'enabled':{'type':'boolean','default':false}}}}}");
 var source=LosslessJson.Parse(@"{'unknown':{'a':1.23456789123456789123}}".Replace('\'', '"'));
 string before=source.ToString(Newtonsoft.Json.Formatting.None);var resolved=ConfigSchema.EffectiveValue(source,schema,true);
 Check((double)resolved["speed"]==3,"root template overrode runtime field default");
 Check((bool)resolved["active"],"missing true default was not visible");
 Check(!(bool)resolved["options"]["enabled"]&&(double)resolved["options"]["gain"]==2,"nested defaults were not complete");
 Check(resolved["phase"]==null,"absent optional phase was invented");
 Check(resolved["optional"].Type==JTokenType.Null,"declared null default was lost");
 Check(resolved["unknown"]["a"].ToString()=="1.23456789123456789123","unknown precision was normalized");
 Check(source.ToString(Newtonsoft.Json.Formatting.None)==before,"viewing defaults mutated source JSON");
 var invalid=LosslessJson.Parse(@"{'speed':'bad','active':42,'options':{'gain':-4}}".Replace('\'', '"'));
 var corrected=ConfigSchema.EffectiveValue(invalid,schema,true);
 Check((double)corrected["speed"]==3&&(bool)corrected["active"],"invalid field types did not show defaults");
 Check((double)corrected["options"]["gain"]==-4,"valid nested explicit override was replaced");
 Check(ConfigSchema.Validate(invalid,schema).Count==2,"invalid explicit values no longer block validation");
 var bounds=LosslessJson.Parse(@"{'speed':-2}".Replace('\'', '"'));Check((double)ConfigSchema.EffectiveValue(bounds,schema,true)["speed"]==3,"out-of-range value did not show default");
 Check((double)ConfigSchema.EffectiveValue(LosslessJson.Parse(@"{'speed':0}".Replace('\'', '"')),schema,true)["speed"]==0,"zero was treated as absent");
 Check(!(bool)ConfigSchema.EffectiveValue(LosslessJson.Parse(@"{'active':false}".Replace('\'', '"')),schema,true)["active"],"false was treated as absent");
 Check(ConfigSchema.EffectiveValue(LosslessJson.Parse(@"{'options':null}".Replace('\'', '"')),schema,true)["options"].Type==JTokenType.Null,"explicit permitted null was overwritten");
 var systemSchema=JObject.Parse(@"{'$id':'urn:matrex:config:system','type':'object','properties':{'targetDisplay':{'type':'integer','default':1},'configs':{'type':'array','items':{'type':'object','properties':{'vrId':{'type':'string','default':'VR1'},'targetDisplay':{'type':'integer','default':1}}}}}}");
 var routing=LosslessJson.Parse(@"{'targetDisplay':0,'configs':[{'vrId':'VR1'},{'vrId':'VR2','targetDisplay':2}]}".Replace('\'', '"'));
 var effectiveRouting=ConfigSchema.EffectiveValue(routing,systemSchema,true);
 Check(((JObject)effectiveRouting["configs"][0]).Property("targetDisplay")==null,"effective system JSON materialized an omitted rig display instead of preserving global/CLI inheritance");
 Check(effectiveRouting["configs"][1]["targetDisplay"].ToString()=="2","effective system JSON changed explicit rig routing");
 foreach(int fallbackDisplay in new[]{0,1,4})Check((effectiveRouting["configs"][0]["targetDisplay"]==null?fallbackDisplay:(int)effectiveRouting["configs"][0]["targetDisplay"])==fallbackDisplay,"materialized defaults changed runtime inherited display "+fallbackDisplay);
 Check(routing["configs"][0]["targetDisplay"]==null&&routing["targetDisplay"].ToString()=="0","effective routing mutated source");
 string directory=Path.Combine(Path.GetTempPath(),"matrex-effective-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
 try{
  string path=Path.Combine(directory,"blank.json");File.WriteAllText(path,"   \n");var blank=new ConfigDocument(path,"choice");
  Check(blank.Root is JObject&&blank.LoadError==null,"blank JSON did not show inherited defaults");blank.Save();Check(File.ReadAllText(path).Trim()=="{}","saving blank config left unreadable JSON for runtime");
  path=Path.Combine(directory,"broken.json");File.WriteAllText(path,"{broken");var damaged=new ConfigDocument(path,"choice");
  Check(damaged.LoadError!=null&&damaged.Root is JObject,"damaged file prevented default form from opening");
  bool rejected=false;try{damaged.Save();}catch(IOException){rejected=true;}Check(rejected&&File.ReadAllText(path)=="{broken","merely opening damaged input overwrote it");
  damaged.ApplyJson("{}");Check(damaged.Dirty&&damaged.LoadError==null&&damaged.CanUndo,"explicit empty repair was not dirty/undoable");
  damaged.Undo();Check(damaged.LoadError!=null&&damaged.SourceText=="{broken"&&!damaged.Dirty,"Undo did not restore original malformed diagnostic/source state");
  rejected=false;try{damaged.Save();}catch(IOException){rejected=true;}Check(rejected&&File.ReadAllText(path)=="{broken","Undo of repair made malformed input saveable");
  damaged.Redo();Check(damaged.LoadError==null&&damaged.Dirty,"Redo did not restore valid empty repair state");
  damaged.RevertToSaved();Check(damaged.LoadError!=null&&damaged.SourceText=="{broken"&&!damaged.Dirty,"Revert lost the saved malformed source and diagnostics");
  damaged.Undo();Check(damaged.LoadError==null&&damaged.Dirty,"Undo of Revert did not recover repaired draft");
  damaged.Save();Check(File.ReadAllText(path).Trim()=="{}"&&File.ReadAllText(path+".bak")=="{broken","repair did not retain original backup");
  File.WriteAllText(path,"{stillbroken");Check(damaged.CheckExternalChange()&&damaged.LoadError!=null,"external damage was not visible without losing the workspace");
  damaged.Undo();Check(damaged.LoadError==null&&damaged.Dirty&&damaged.Root.ToString(Newtonsoft.Json.Formatting.None)=="{}","Undo after external damage did not restore a saveable valid empty document");
  damaged.Redo();Check(damaged.LoadError!=null&&damaged.SourceText=="{stillbroken"&&!damaged.Dirty,"Redo after external damage did not restore exact invalid source");
  File.WriteAllText(path,"{otherbroken");Check(damaged.CheckExternalChange(),"second external malformed source was not loaded");
  damaged.Undo();Check(damaged.LoadError!=null&&damaged.SourceText=="{stillbroken"&&damaged.Dirty,"Undo between equally invalid DOM fallbacks lost original source bytes");
  damaged.Redo();Check(damaged.SourceText=="{otherbroken"&&!damaged.Dirty,"Redo between malformed sources did not restore accepted baseline");
  File.WriteAllText(path,"{\"speed\":7}");Check(damaged.CheckExternalChange()&&damaged.LoadError==null,"external repair did not resynchronize the form");
  damaged.Undo();Check(damaged.LoadError!=null&&damaged.SourceText=="{otherbroken"&&damaged.Dirty,"Undo of external repair did not restore prior malformed source");damaged.Redo();
  Check(damaged.LoadError==null&&!damaged.Dirty&&damaged.Root["speed"].ToString()=="7","Redo of external repair did not restore clean valid state");
 }finally{Directory.Delete(directory,true);}
 Console.WriteLine("PASS: "+checks+" production effective-default/document checks; no Unity rendering or native input exercised.");
}}
'''

def main():
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    versions = re.findall(r'^(\d+)\.', subprocess.check_output([dotnet, '--list-sdks'], text=True), re.M)
    version = max(map(int, versions))
    dll = next((ROOT/'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    with tempfile.TemporaryDirectory(prefix='matrex-effective-config-') as temporary:
        folder = Path(temporary)
        (folder/'Program.cs').write_text(STUBS)
        files = ['LosslessJson.cs', 'ConfigSchema.cs', 'ConfigDocument.cs']
        sources = ''.join(f'<Compile Include="{escape(str(ROOT/"Assets/Scripts/ConfigurationUI"/name))}"/>' for name in files)
        project = folder/'Regression.csproj'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(dll))}</HintPath></Reference>{sources}</ItemGroup></Project>')
        config = folder/'NuGet.Config'
        config.write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        env = dict(os.environ, DOTNET_CLI_HOME=temporary, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        result = subprocess.run([dotnet, 'build', str(project), '--configfile', str(config), '-p:UseSharedCompilation=false', '-v:q'], env=env, capture_output=True, text=True)
        if result.returncode:
            raise SystemExit(result.stdout+result.stderr)
        subprocess.run([dotnet, str(folder/f'bin/Debug/net{version}.0/Regression.dll')], env=env, check=True)

if __name__ == '__main__':
    main()
