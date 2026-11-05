#!/usr/bin/env python3
"""Production C# recorder client checks, or a probe against an explicitly launched native endpoint."""
import argparse,os,re,shutil,subprocess,tempfile
from pathlib import Path
from xml.sax.saxutils import escape
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--endpoint',type=Path);p.add_argument('--instance');p.add_argument('--pid',type=int);p.add_argument('--backend',choices=['rekkefly','hejjetrac']);p.add_argument('--output',type=Path)
a=p.parse_args();repo=Path(__file__).resolve().parents[1];dotnet=shutil.which('dotnet')
if not dotnet:raise SystemExit('Install .NET SDK6 or newer')
versions=[int(v) for v in re.findall(r'^(\d+)\.',subprocess.check_output([dotnet,'--list-sdks'],text=True),re.M) if int(v)>=6]
dll=next((repo/'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
real=a.endpoint is not None
if real and not all([a.instance,a.pid,a.backend,a.output]):p.error('Real endpoint probe requires --instance --pid --backend --output')
with tempfile.TemporaryDirectory(prefix='matrex-recorder-client-') as td:
 root=Path(td);paths=[repo/'Assets/Scripts/NativeRecorderClient.cs',repo/'tools/native_recording_control'/('Probe.cs' if real else 'ClientRegression.cs')]
 includes=''.join(f'<Compile Include="{escape(str(path))}"/>' for path in paths)
 project=root/'RecorderClient.csproj';project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{max(versions)}.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(dll))}</HintPath></Reference>{includes}</ItemGroup></Project>')
 config=root/'NuGet.Config';config.write_text('<configuration><packageSources><clear /></packageSources></configuration>')
 env=dict(os.environ,DOTNET_CLI_HOME=td,DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
 subprocess.run([dotnet,'restore',str(project),'--configfile',str(config)],env=env,check=True,stdout=subprocess.DEVNULL)
 command=[dotnet,'run','--no-restore','--project',str(project),'-p:UseSharedCompilation=false']
 if real:command+=['--',str(a.endpoint.resolve()),a.instance,str(a.pid),a.backend,str(a.output.resolve())]
 subprocess.run(command,env=env,check=True)
