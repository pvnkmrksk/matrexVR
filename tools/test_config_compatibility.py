#!/usr/bin/env python3
"""Run production JSON contract regressions with a local .NET SDK, offline.

Uses the pinned Newtonsoft DLL already present in a Unity-imported checkout.
Pass --newtonsoft PATH to run against another copy. No NuGet packages are fetched.
These checks cover data contracts, not Unity rendering or physical tracker input.
"""
import argparse
from pathlib import Path
import os
import re
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--newtonsoft", type=Path)
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[1]
    dotnet = shutil.which("dotnet")
    if not dotnet:
        parser.error("Install a .NET SDK (6 or newer) to run this optional regression check.")
    dll = args.newtonsoft
    if dll is None:
        dll = next(iter((repo / "Library/PackageCache").glob("com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll")), None)
    if dll is None or not dll.is_file():
        parser.error("Open the project in Unity once, or supply --newtonsoft PATH.")
    sdks = subprocess.check_output([dotnet, "--list-sdks"], text=True)
    versions = [int(n) for n in re.findall(r"^(\d+)\.", sdks, re.MULTILINE) if int(n) >= 6]
    if not versions:
        parser.error("A .NET SDK 6 or newer is required.")
    paths = ["Assets/Scripts/ConfigurationUI/LosslessJson.cs", "Assets/Scripts/ConfigurationUI/ConfigDocument.cs", "Assets/Scripts/Loggers/AnimalMetadata.cs", "Assets/Scripts/Swarm/KannadiConfig.cs", "tools/config_compatibility/Stubs.cs", "tools/config_compatibility/Regression.cs"]
    with tempfile.TemporaryDirectory(prefix="matrex-config-contracts-") as tmp:
        root = Path(tmp)
        project = root / "Regression.csproj"
        references = "".join(f'<Compile Include="{escape(str(repo / name))}"/>' for name in paths)
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{max(versions)}.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(dll.resolve()))}</HintPath></Reference>{references}</ItemGroup></Project>')
        config = root / "NuGet.Config"
        config.write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        env = dict(os.environ, DOTNET_CLI_HOME=tmp, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1", DOTNET_GENERATE_ASPNET_CERTIFICATE="false")
        subprocess.run([dotnet, "restore", str(project), "--configfile", str(config)], env=env, check=True, stdout=subprocess.DEVNULL)
        subprocess.run([dotnet, "run", "--no-restore", "--project", str(project), "-p:UseSharedCompilation=false", "--", str(repo)], env=env, check=True)


if __name__ == "__main__":
    main()
