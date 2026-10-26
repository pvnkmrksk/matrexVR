#!/usr/bin/env python3
"""Check exact production footer dispatch/caption methods offline with UI and action-boundary fakes."""
from pathlib import Path
from test_system_display_routing import declaration
import os
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
PRELUDE = r'''
using System;
public sealed class Label { public string text; }
public sealed class Button { public string tooltip; public Label label=new Label();public T Q<T>() where T:class=>label as T; }
public sealed class WorkspaceState { public bool configured,quick,demo; }
public static class DesktopQuickStart {
 public static bool IsQuickStart(WorkspaceState workspace)=>workspace.quick;
 public static bool IsDemo(WorkspaceState workspace)=>workspace.demo;
}
public sealed class ConfigWorkbench {
 private WorkspaceState Workspace=new WorkspaceState();
 private bool ApparatusConfigured=>Workspace.configured;
 private Button start=new Button();private int saved,started;private bool home;
 private void FinishApparatusConfiguration(){saved++;Workspace.configured=true;home=true;}
 private void StartExperiment(){started++;}
'''
TESTS = r'''
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 public static void Main(){
  var draft=new ConfigWorkbench();draft.RefreshStartCaption();
  Check(draft.start.label.text=="Use configuration"&&draft.start.tooltip.Contains("does not start"),"draft caption/tooltip imply experiment start");
  draft.RunPrimaryAction();Check(draft.saved==1&&draft.started==0&&draft.home,"draft primary did not save and return home without starting");
  draft.RefreshStartCaption();Check(draft.start.label.text=="Start experiment","completion did not restore normal Start");
  draft.RunPrimaryAction();Check(draft.started==1&&draft.saved==1,"configured primary did not start exactly once");
  var demo=new ConfigWorkbench();demo.Workspace.quick=true;demo.Workspace.demo=true;demo.RefreshStartCaption();
  Check(demo.start.label.text=="Start demo","demo caption changed");demo.RunPrimaryAction();Check(demo.started==1&&demo.saved==0,"demo tried to complete apparatus");
  var live=new ConfigWorkbench();live.Workspace.quick=true;live.RefreshStartCaption();live.RunPrimaryAction();
  Check(live.start.label.text=="Start experiment"&&live.started==1&&live.saved==0,"live QuickStart dispatch changed");
  Console.WriteLine("PASS: seven production primary-action checks; UI/action boundaries mocked, no native app launched.");
 }
}
'''

def main():
    dotnet=shutil.which('dotnet')
    if not dotnet:raise SystemExit('A local .NET SDK is required.')
    version=max(map(int,re.findall(r'^(\d+)\.',subprocess.check_output([dotnet,'--list-sdks'],text=True),re.M)))
    source=(ROOT/'Assets/Scripts/ConfigurationUI/ConfigWorkbench.Session.cs').read_text()
    properties=[]
    for signature in ('private bool CurrentDemo =>','private bool ApparatusDraft =>'):
        start=source.index(signature);properties.append(source[start:source.index(';',start)+1])
    production='\n'.join(properties+[declaration(source,'private void RunPrimaryAction()'),declaration(source,'private void RefreshStartCaption()')])
    with tempfile.TemporaryDirectory(prefix='matrex-primary-action-') as folder:
        temporary=Path(folder);(temporary/'Program.cs').write_text(PRELUDE+production+TESTS)
        (temporary/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        project=temporary/'Regression.csproj';project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup></Project>')
        env=dict(os.environ,DOTNET_CLI_HOME=folder,DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        result=subprocess.run([dotnet,'build',str(project),'--configfile',str(temporary/'NuGet.Config'),'-p:UseSharedCompilation=false','-v:q'],env=env,capture_output=True,text=True)
        if result.returncode:raise SystemExit(result.stdout+result.stderr)
        subprocess.run([dotnet,str(temporary/f'bin/Debug/net{version}.0/Regression.dll')],env=env,check=True)

if __name__=='__main__':main()
