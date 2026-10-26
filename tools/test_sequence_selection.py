#!/usr/bin/env python3
"""Check production row mutations against immediate/deferred inspector boundaries.

Actual methods and Newtonsoft JSON are used; the UI rebuild callback is controlled.
No Unity/player process or native input is involved.
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
using System;using Newtonsoft.Json.Linq;
class ConfigDocument{public JObject Root;}
class WorkspaceState{public ConfigDocument Sequence;}
static class ExperimentBlocks{public static JArray Steps(JToken root)=> (JArray)root["sequences"];}
class Regression{
 private WorkspaceState Workspace=new WorkspaceState();private int activeBlock,observedIndex;
 private string observedLabel;private bool immediate=true;private Action pending;
 private JArray BuilderSteps=>(JArray)Workspace.Sequence.Root["sequences"];
 private void Observe(){observedIndex=activeBlock;observedLabel=activeBlock>=0&&activeBlock<BuilderSteps.Count?(string)BuilderSteps[activeBlock]["label"]:null;}
 private void StudioEdit(ConfigDocument doc,Action<JToken> change){change(doc.Root);if(immediate)Observe();else pending=Observe;}
'''
TESTS = r'''
 static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static Regression Rows(int active,bool immediate=true){return new Regression{activeBlock=active,immediate=immediate,Workspace=new WorkspaceState{Sequence=new ConfigDocument{Root=JObject.Parse("{\"sequences\":[{\"label\":\"A\"},{\"label\":\"B\"},{\"label\":\"C\"},{\"label\":\"D\"}]} ")}}};}
 static void Main(){
  var r=Rows(3);r.RemoveBlock(3);Check(r.BuilderSteps.Count==3&&r.observedIndex==2&&r.observedLabel=="C","immediate removal of last row must show its surviving neighbor");
  r.RemoveBlock(2);Check(r.BuilderSteps.Count==2&&r.observedIndex==1&&r.observedLabel=="B","second last-row removal must not show the generic config overview");
  r=Rows(3);r.RemoveBlock(0);Check(r.observedIndex==0&&r.observedLabel=="B","removing an earlier row must select its surviving successor before rebuilding");
  r=Rows(0);r.DuplicateBlock(1);Check(r.BuilderSteps.Count==5&&r.observedIndex==2&&r.observedLabel=="B","immediate duplication must show the duplicate rather than the old selection");
  r=Rows(3,false);r.RemoveBlock(3);r.pending();Check(r.observedIndex==2&&r.observedLabel=="C","deferred removal selection changed");
  r=Rows(0,false);r.DuplicateBlock(2);r.pending();Check(r.observedIndex==3&&r.observedLabel=="C","deferred duplicate selection changed");
  r=Rows(0);r.BuilderSteps.Clear();r.BuilderSteps.Add(new JObject{["label"]="only"});r.RemoveBlock(0);Check(r.BuilderSteps.Count==0&&r.activeBlock==0&&r.observedLabel==null,"empty sequence selection must remain bounded");
  Console.WriteLine("PASS: "+checks+" production sequence-selection checks; immediate/deferred UI callback boundaries, actual JSON mutations.");
 }
}
'''


def main():
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    sdk = subprocess.check_output([dotnet, '--list-sdks'], text=True)
    version = max(map(int, re.findall(r'^(\d+)\.', sdk, re.M)))
    source = (ROOT/'Assets/Scripts/ConfigurationUI/ConfigWorkbench.Builder.cs').read_text()
    production = '\n'.join(declaration(source, name) for name in ('public void DuplicateBlock(int index)', 'public void RemoveBlock(int index)'))
    newtonsoft = next((ROOT/'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    with tempfile.TemporaryDirectory(prefix='matrex-sequence-selection-') as folder:
        temporary = Path(folder)
        env = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        (temporary/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (temporary/'Program.cs').write_text(PRELUDE+production+TESTS)
        project = temporary/'Regression.csproj'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(newtonsoft))}</HintPath></Reference></ItemGroup></Project>')
        result = subprocess.run([dotnet, 'build', str(project), '--configfile', str(temporary/'NuGet.Config'), '-p:UseSharedCompilation=false', '-v:q'], env=env, capture_output=True, text=True)
        if result.returncode:
            raise SystemExit(result.stdout+result.stderr)
        subprocess.run([dotnet, str(temporary/f'bin/Debug/net{version}.0/Regression.dll')], env=env, check=True)


if __name__ == '__main__':
    main()
