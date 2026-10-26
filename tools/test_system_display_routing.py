#!/usr/bin/env python3
"""Compile the exact production display parser and data contracts offline; no Unity/native calls."""
from pathlib import Path
from xml.sax.saxutils import escape
import os
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def declaration(source, signature):
    start = source.index(signature)
    brace = source.index('{', start)
    depth = 1
    end = brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


PROGRAM = r'''
using System;
using Newtonsoft.Json.Linq;
static class Regression {
 static int checks;
 static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
 static void Main(){
  Check(MainController.DefaultTargetDisplay(new JObject(),new string[0])==1,"legacy omitted global default changed");
  var root=JObject.Parse("{\"targetDisplay\":3}");
  Check(MainController.DefaultTargetDisplay(root,new string[0])==3,"global fallback ignored");
  Check(MainController.DefaultTargetDisplay(root,new[]{"app","-DISPLAY","4"})==4,"CLI fallback ignored");
  Check(MainController.DefaultTargetDisplay(root,new[]{"app","-display","bad"})==3,"invalid CLI discarded global fallback");
  var rigs=JArray.Parse("[{\"vrId\":\"VR1\",\"targetDisplay\":0,\"closedLoopMode\":\"Rekkefly\"},{\"vrId\":\"VR2\",\"targetDisplay\":2},{\"vrId\":\"VR3\",\"closedLoopMode\":\"FicTrac\",\"tracker\":{\"configFile\":\"calibration.toml\"}},{\"vrId\":\"VR4\",\"targetDisplay\":-1}]");
  string before=rigs.ToString();var parsed=MainController.ParseSystemConfigurations(rigs,4);
  Check(parsed[0].targetDisplay==0,"explicit primary output overwritten");
  Check(parsed[1].targetDisplay==2,"explicit secondary output overwritten");
  Check(parsed[2].targetDisplay==4,"omitted output did not inherit fallback");
  Check(parsed[3].targetDisplay==-1,"explicit invalid output silently normalized instead of validated later");
  Check(parsed[0].closedLoopMode==ClosedLoopMode.Rekkefly&&parsed[2].closedLoopMode==ClosedLoopMode.FicTrac,"production backend conversion changed");
  Check(parsed[2].tracker.configFile=="calibration.toml","native calibration reference changed");
  Check(rigs.ToString()==before,"parser rewrote source JSON");
  Check(MainController.ParseSystemConfigurations(new JArray(),0).Length==0,"empty array parsing changed");
  bool rejected=false;try{MainController.ParseSystemConfigurations(JArray.Parse("[{\"targetDisplay\":null}]"),4);}catch(Newtonsoft.Json.JsonException){rejected=true;}
  Check(rejected,"explicit null output was silently treated as omitted");
  Console.WriteLine("PASS: "+checks+" production display routing checks; no Unity or native process launched.");
 }
}
'''


def main():
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    version = max(map(int, re.findall(r'^(\d+)\.', subprocess.check_output([dotnet, '--list-sdks'], text=True), re.M)))
    newtonsoft = next((ROOT / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    source = (ROOT / 'Assets/Scripts/MainController.cs').read_text()
    parser = '\n'.join(declaration(source, signature) for signature in ('public static int DefaultTargetDisplay(', 'public static SystemConfig[] ParseSystemConfigurations('))
    contracts = source[source.index('[JsonConverter(typeof(TrackerBackendConverter))]'):]
    contracts += '\n'+declaration((ROOT / 'Assets/Scripts/Keyboard.cs').read_text(), 'public class ManualControlConfig')
    contracts += '\n'+declaration((ROOT / 'Assets/Scripts/TrackerProcessManager.cs').read_text(), 'public sealed class TrackerLaunchConfig')
    with tempfile.TemporaryDirectory(prefix='matrex-display-routing-') as folder:
        temporary = Path(folder)
        (temporary / 'Production.cs').write_text('using Newtonsoft.Json; using Newtonsoft.Json.Linq; public static class MainController {\n'+parser+'\n}\n'+contracts)
        (temporary / 'Program.cs').write_text(PROGRAM)
        (temporary / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        production = ROOT / 'Assets/Scripts/TrackerBackend.cs'
        project = temporary / 'Regression.csproj'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(newtonsoft))}</HintPath></Reference><Compile Include="{escape(str(production))}" /></ItemGroup></Project>')
        env = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        result = subprocess.run([dotnet,'build',str(project),'--configfile',str(temporary/'NuGet.Config'),'-p:UseSharedCompilation=false','-v:q'],env=env,capture_output=True,text=True)
        if result.returncode:
            raise SystemExit(result.stdout+result.stderr)
        subprocess.run([dotnet,str(temporary/f'bin/Debug/net{version}.0/Regression.dll')],env=env,check=True)


if __name__ == '__main__':
    main()
