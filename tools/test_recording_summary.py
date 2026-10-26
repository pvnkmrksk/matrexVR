#!/usr/bin/env python3
"""Compile and test production recording summaries offline; never launch Unity or modify real runs."""
from pathlib import Path
import argparse
import os
import re
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inspect-run',type=Path,help='Read-only summary/hash audit of an existing run instead of synthetic regressions')
    parser.add_argument('--report',type=Path,help='Optional JSON output outside the inspected run')
    args=parser.parse_args()
    repo = Path(__file__).resolve().parents[1]
    dotnet = shutil.which('dotnet') or '/usr/local/share/dotnet/dotnet'
    versions = [int(v) for v in re.findall(r'^(\d+)\.', subprocess.check_output([dotnet, '--list-sdks'], text=True), re.MULTILINE) if int(v) >= 6]
    dll = next((repo / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    paths = ['Assets/Scripts/Replay/ReplaySessionArchive.cs', 'Assets/Scripts/Replay/RecordedPosePolicy.cs',
             'Assets/Scripts/Loggers/RunConfigArchive.cs', 'Assets/Scripts/Loggers/RecordedCsv.cs',
             'Assets/Scripts/Loggers/RecordingSummary.cs', 'tools/replay_compatibility/Stubs.cs',
             'Assets/Scripts/Loggers/PlayerBuildIdentity.cs', 'Assets/Scripts/Swarm/AsyncSwarmWriter.cs',
             'Assets/Scripts/Loggers/RecordingSessionFinalizer.cs',
             'tools/recording_summary/Regression.cs']
    with tempfile.TemporaryDirectory(prefix='matrex-recording-summary-') as temporary:
        root = Path(temporary)
        includes = ''.join(f'<Compile Include="{escape(str(repo / path))}"/>' for path in paths)
        source=(repo/'Assets/Scripts/Loggers/MasterDataLogger.cs').read_text()
        start=source.index('internal static bool ShouldRetainMediaSource(')
        opening=source.index('{',start);depth=1;end=opening+1
        while depth:
            depth+=(source[end]=='{')-(source[end]=='}');end+=1
        ownership=root/'ActualMediaOwnership.cs'
        ownership.write_text('using Newtonsoft.Json.Linq;\nstatic class ActualMediaOwnership {\n'+source[start:end]+'\n}\n')
        includes+=f'<Compile Include="{escape(str(ownership))}"/>'
        project = root / 'RecordingSummary.csproj'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{max(versions)}.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(dll.resolve()))}</HintPath></Reference>{includes}</ItemGroup></Project>')
        config = root / 'NuGet.Config'
        config.write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        env = dict(os.environ, DOTNET_CLI_HOME=temporary, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
        subprocess.run([dotnet, 'restore', str(project), '--configfile', str(config)], env=env, check=True, stdout=subprocess.DEVNULL)
        command=[dotnet, 'run', '--no-restore', '--project', str(project), '-p:UseSharedCompilation=false']
        if args.inspect_run:
            command+=['--',str(args.inspect_run.resolve())]
            if args.report:
                if args.report.resolve().is_relative_to(args.inspect_run.resolve()):
                    parser.error('--report must be outside the read-only run folder')
                command.append(str(args.report.resolve()))
        subprocess.run(command, env=env, check=True)


if __name__ == '__main__':
    main()
