#!/usr/bin/env python3
"""Offline production decoder/backend and actual owned-process lifecycle tests (no camera, ZMQ or Unity render claim)."""
from pathlib import Path
from xml.sax.saxutils import escape
import os,re,shutil,subprocess,tempfile
root=Path(__file__).resolve().parents[1]
dotnet=shutil.which('dotnet')
if not dotnet:raise SystemExit('A local .NET SDK is required.')
version=max(map(int,re.findall(r'^(\d+)\.',subprocess.check_output([dotnet,'--list-sdks'],text=True),re.M)))
newtonsoft=next((root/'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
with tempfile.TemporaryDirectory(prefix='matrex-tracker-contracts-') as folder:
    tmp=Path(folder);mock=tmp/'mock';mock.mkdir();test=tmp/'test';test.mkdir()
    env=dict(os.environ,DOTNET_CLI_HOME=folder,DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
    (tmp/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
    (mock/'Mock.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup></Project>')
    (mock/'Program.cs').write_text('using System; class Mock { static void Main(string[] args) { string log=Environment.GetEnvironmentVariable("MATREX_TRACKER_ARGUMENT_LOG");if(log!=null)System.IO.File.WriteAllText(log,System.Text.Json.JsonSerializer.Serialize(args));while(true)System.Threading.Thread.Sleep(20); } }')
    main=(root/'Assets/Scripts/MainController.cs').read_text();enum=re.search(r'\[JsonConverter\(typeof\(TrackerBackendConverter\)\)\]\s*public enum ClosedLoopMode[^}]+}',main)[0];model=re.search(r'public class SystemConfig\s*\{[^}]+}',main)[0]
    (test/'Models.cs').write_text('using Newtonsoft.Json;\n'+enum+'\n'+model)
    sources=['Assets/Scripts/TrackerBackend.cs','Assets/Scripts/TrackerWireDecoder.cs','Assets/Scripts/MacAppLauncher.cs','Assets/Scripts/TrackerProcessManager.cs','Assets/Scripts/CompanionCatalog.cs','tools/tracker_contracts/Stubs.cs','tools/tracker_contracts/Regression.cs']
    items=''.join(f'<Compile Include="{escape(str(root/p))}" />' for p in sources)
    (test/'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(newtonsoft))}</HintPath></Reference>{items}</ItemGroup></Project>')
    for project in [mock/'Mock.csproj',test/'Test.csproj']:
        result=subprocess.run([dotnet,'build',str(project),'--configfile',str(tmp/'NuGet.Config'),'-p:UseSharedCompilation=false','-v:q'],env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
        if result.returncode:print(result.stdout);raise SystemExit(result.returncode)
    exe=mock/f'bin/Debug/net{version}.0/Mock'
    subprocess.run([dotnet,str(test/f'bin/Debug/net{version}.0/Test.dll'),str(exe),folder],env=env,check=True)
