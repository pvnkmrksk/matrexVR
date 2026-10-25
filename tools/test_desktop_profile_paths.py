#!/usr/bin/env python3
"""Exercise production desktop profile initialization without Unity or native apps."""
from pathlib import Path
from xml.sax.saxutils import escape
import os
import re
import shutil
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[1]
STUBS = r'''
using System;
using System.IO;
using Newtonsoft.Json.Linq;
namespace UnityEngine {
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType kind) {} }
    public static class Application { public static bool isEditor; public static string streamingAssetsPath, persistentDataPath, dataPath; }
    public struct Resolution { public int width, height; }
    public static class Screen { public static Resolution currentResolution = new Resolution {width=3024,height=1964}; }
    public static class Debug { public static void LogWarning(string message) { Console.Error.WriteLine(message); } }
    public struct Vector3 {
        public float x,y,z;
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3 {x=a.x-b.x,y=a.y-b.y,z=a.z-b.z};
    }
    public sealed class TerrainData { public Vector3 size; }
    public sealed class Transform { public Vector3 position; }
    public sealed class Terrain {
        public static Terrain[] activeTerrains = new Terrain[0];
        public TerrainData terrainData; public Transform transform;
        public float SampleHeight(Vector3 position) => 0;
    }
}
namespace ConfigurationUI {
    public sealed class Document { public JObject Root; public string Path; }
    public sealed class ConfigWorkspace {
        public Document System;
        public void SelectPrimary(string kind,string path) {}
        public void RefreshReferences() {}
    }
}
public static class Regression {
    static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    public static void Main(string[] args) {
        string profile=args[1], assets=args[5], kind=args[6];
        UnityEngine.Application.isEditor=kind=="editor";
        UnityEngine.Application.streamingAssetsPath=assets;
        UnityEngine.Application.persistentDataPath=Path.GetDirectoryName(profile);
        UnityEngine.Application.dataPath=Path.GetDirectoryName(assets);
        Check(RuntimePaths.ConfigRoot==Path.GetFullPath(profile),"wrong config root");
        Check(RuntimePaths.RunDataRoot==Path.GetFullPath(args[3]),"wrong recording root");
        string system=File.ReadAllText(Path.Combine(profile,"system_config.json"));
        string sequence=File.ReadAllText(Path.Combine(profile,"sequenceConfig.json"));
        if(kind=="fresh") {
            Check(JObject.Parse(system)["desktopQuickStart"] is JObject,"new profile lacks ready desktop configuration");
            Check(JObject.Parse(sequence)["sequences"] is JArray rows && rows.Count==1,"new profile lacks starter sequence");
            Check(File.Exists(Path.Combine(profile,"QuickStart/flying-night-sky-demo/night-sky.json")),"starter world missing");
        } else {
            if(kind=="sequence-only"||kind=="both"||kind=="editor")Check(sequence=="{\"operatorSequence\":true}\n","operator sequence was changed");
            if(kind=="system-only"||kind=="both"||kind=="editor")Check(system=="{\"operatorSystem\":true}\n","operator system was changed");
            Check(!File.Exists(Path.Combine(profile,"QuickStart/flying-night-sky-demo/system.json")),"existing/partial profile unexpectedly reseeded");
        }
        Check(!File.Exists(Path.Combine(profile,"ignored.meta")),"Unity metadata copied into writable profile");
        Console.WriteLine(kind+": production profile initialization passed");
    }
}
'''


def main():
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    sdk = subprocess.check_output([dotnet, '--list-sdks'], text=True)
    version = max(map(int, re.findall(r'^(\d+)\.', sdk, re.M)))
    newtonsoft = next((ROOT / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    with tempfile.TemporaryDirectory(prefix='matrex-desktop-profile-') as folder:
        temporary = Path(folder)
        env = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        (temporary / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (temporary / 'Stubs.cs').write_text(STUBS)
        sources = ['Assets/Scripts/RuntimePaths.cs', 'Assets/Scripts/DesktopQuickStart.cs']
        items = ''.join(f'<Compile Include="{escape(str(ROOT / source))}" />' for source in sources)
        project = temporary / 'Regression.csproj'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(newtonsoft))}</HintPath></Reference>{items}</ItemGroup></Project>')
        result = subprocess.run([dotnet, 'build', str(project), '--configfile', str(temporary / 'NuGet.Config'), '-p:UseSharedCompilation=false', '-v:q'], env=env, capture_output=True, text=True)
        if result.returncode:
            raise SystemExit(result.stdout + result.stderr)
        assets = temporary / 'bundled'
        (assets / 'Templates').mkdir(parents=True)
        (assets / 'Templates/system_config.template.json').write_text('{"template":true}\n')
        (assets / 'system_config.json').write_text('{"bundledSystem":true}\n')
        (assets / 'sequenceConfig.json').write_text('{"bundledSequence":true}\n')
        (assets / 'ignored.meta').write_text('Unity import metadata\n')
        dll = temporary / f'bin/Debug/net{version}.0/Regression.dll'
        for kind in ('fresh', 'sequence-only', 'system-only', 'both', 'editor'):
            profile = temporary / kind
            profile.mkdir()
            if kind in ('system-only', 'both', 'editor'):
                (profile / 'system_config.json').write_text('{"operatorSystem":true}\n')
            if kind in ('sequence-only', 'both', 'editor'):
                (profile / 'sequenceConfig.json').write_text('{"operatorSequence":true}\n')
            subprocess.run([dotnet, str(dll), '--config-dir', str(profile), '--data-dir', str(temporary / (kind + '-recordings')), '--assets', str(assets), kind], env=env, check=True)


if __name__ == '__main__':
    main()
