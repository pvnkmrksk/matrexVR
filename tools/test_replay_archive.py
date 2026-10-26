#!/usr/bin/env python3
"""Offline regressions for production replay archives, frame caches and swarm writers.

Requires an installed .NET SDK and the project's already-imported Newtonsoft DLL.
Does not launch Unity, fetch NuGet packages, modify recordings or validate rendering.
"""
import os
import argparse
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inspect-run',type=Path,help='Read-only inspection of a real saved run instead of synthetic regressions.')
    parser.add_argument('--report',type=Path,help='Optional JSON summary path for --inspect-run.')
    args=parser.parse_args()
    repo = Path(__file__).resolve().parents[1]
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('Install .NET SDK 6 or newer for this optional regression check.')
    dll = next(iter((repo / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll')), None)
    if dll is None:
        raise SystemExit('Open this project in Unity once to import the pinned Newtonsoft dependency.')
    versions = [int(v) for v in re.findall(r'^(\d+)\.', subprocess.check_output([dotnet, '--list-sdks'], text=True), re.MULTILINE) if int(v) >= 6]
    paths = ['Assets/Scripts/Replay/RecordedPosePolicy.cs', 'Assets/Scripts/Replay/ReplaySessionArchive.cs', 'Assets/Scripts/Replay/ReplayAgentStream.cs', 'Assets/Scripts/Replay/ReplayStimulusState.cs', 'Assets/Scripts/Loggers/RecordedCsv.cs', 'Assets/Scripts/Swarm/AsyncSwarmWriter.cs', 'Assets/Scripts/Loggers/RunConfigArchive.cs', 'tools/replay_compatibility/Stubs.cs', 'tools/replay_compatibility/Regression.cs']
    with tempfile.TemporaryDirectory(prefix='matrex-replay-regression-') as tmp:
        root = Path(tmp)
        includes = ''.join(f'<Compile Include="{escape(str(repo / path))}"/>' for path in paths)
        project = root / 'ReplayRegression.csproj'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{max(versions)}.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(dll.resolve()))}</HintPath></Reference>{includes}</ItemGroup></Project>')
        config = root / 'NuGet.Config'
        config.write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        env = dict(os.environ, DOTNET_CLI_HOME=tmp, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
        subprocess.run([dotnet, 'restore', str(project), '--configfile', str(config)], env=env, check=True, stdout=subprocess.DEVNULL)
        command=[dotnet, 'run', '--no-restore', '--project', str(project), '-p:UseSharedCompilation=false']
        if args.inspect_run:
            command+=['--',str(args.inspect_run.resolve())]
            if args.report:command.append(str(args.report.resolve()))
        subprocess.run(command, env=env, check=True)


if __name__ == '__main__':
    main()
