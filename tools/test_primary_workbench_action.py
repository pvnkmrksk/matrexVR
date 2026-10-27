#!/usr/bin/env python3
"""Check exact production footer dispatch/caption methods offline with UI and validation/action-boundary fakes."""
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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace ConfigurationUI { public static class ConfigWorkbench { public static bool IsOpen; } }
namespace UnityEngine {
 public sealed class Canvas { public bool isActiveAndEnabled=true; }
 public sealed class GameObject { public bool activeInHierarchy=true; public object input;public T GetComponent<T>() where T:class=>input as T;public T GetComponentInParent<T>() where T:class=>null; }
}
namespace UnityEngine.UI { public sealed class InputField { public bool isActiveAndEnabled=true,isFocused=true;public bool IsInteractable()=>true; } }
namespace TMPro { public sealed class TMP_InputField { public bool isActiveAndEnabled=true,isFocused=true;public bool IsInteractable()=>true; } }
namespace UnityEngine.EventSystems { public sealed class EventSystem { public static EventSystem current;public GameObject currentSelectedGameObject; } }
public static class ExperimentInput {
 public sealed class ActionState { public bool released;public bool WasReleasedThisFrame()=>released; }
 public static readonly ActionState Cancel=new ActionState();private static ActionState Action(string name)=>Cancel;
 INPUT_PRODUCTION
}
public sealed class MainController {
 private bool sequenceStarted;public int escapes;private void HandleBogongGainInput(){}private void ManageTimerAndTransitions(){}private void HandleEscape(){escapes++;}public void Tick()=>Update();
 CONTROLLER_PRODUCTION
}
public sealed class Label { public string text; }
public sealed class Button { public string tooltip; public Label label=new Label();public T Q<T>() where T:class=>label as T; }
public sealed class WorkspaceState { public bool configured,quick,demo; public int validationCalls; public List<string> errors=new List<string>(); public List<string> Validate(){validationCalls++;return new List<string>(errors);} }
public static class DesktopQuickStart {
 public static bool IsQuickStart(WorkspaceState workspace)=>workspace.quick;
 public static bool IsDemo(WorkspaceState workspace)=>workspace.demo;
}
public sealed class ConfigWorkbench {
 private WorkspaceState Workspace=new WorkspaceState();
 private bool ApparatusConfigured=>Workspace.configured;
 private bool replayLibraryMode;private int replays;
 private void OpenLibrarySelection(){replays++;}
 private void RefreshLibraryFooter(){start.label.text="Replay selected";}
 private Button start=new Button();private int saved,started;private bool home;
 private List<string> hardwareErrors=new List<string>();
 private List<string> ApparatusConfigurationErrors()=>new List<string>(hardwareErrors);
 private void FinishApparatusConfiguration(){saved++;Workspace.configured=true;home=true;}
 private void StartExperiment(){started++;}
'''
TESTS = r'''
 static int checks;
 static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
 public static void Main(){
  var library=new ConfigWorkbench();library.replayLibraryMode=true;library.RefreshStartCaption();library.RunPrimaryAction();Check(library.start.label.text=="Replay selected"&&library.replays==1&&library.started==0&&library.saved==0,"recording library footer started acquisition or completed hardware");
  var draft=new ConfigWorkbench();draft.RefreshStartCaption();
  draft.Workspace.errors.Add("empty sequence");Check(draft.PrimaryActionErrors().Count==0&&draft.Workspace.validationCalls==0,"empty sequence disabled hardware completion");
  draft.Workspace.errors.Clear();draft.Workspace.errors.Add("unavailable experiment");Check(draft.PrimaryActionErrors().Count==0&&draft.Workspace.validationCalls==0,"missing experiment disabled hardware completion");
  draft.hardwareErrors.Add("invalid tracker port");Check(draft.PrimaryActionErrors().Count==1,"draft completion ignored system/profile errors");draft.hardwareErrors.Clear();
  Check(draft.start.label.text=="Use configuration"&&draft.start.tooltip.Contains("does not start"),"draft caption/tooltip imply experiment start");
  draft.RunPrimaryAction();Check(draft.saved==1&&draft.started==0&&draft.home,"draft primary did not save and return home without starting");
  Check(draft.PrimaryActionErrors().Count==1&&draft.Workspace.validationCalls==1,"configured Start bypassed sequence/experiment validation");
  draft.RefreshStartCaption();Check(draft.start.label.text=="Start experiment","completion did not restore normal Start");
  draft.RunPrimaryAction();Check(draft.started==1&&draft.saved==1,"configured primary did not start exactly once");
  var demo=new ConfigWorkbench();demo.Workspace.quick=true;demo.Workspace.demo=true;demo.RefreshStartCaption();
  Check(demo.start.label.text=="Start demo","demo caption changed");demo.RunPrimaryAction();Check(demo.started==1&&demo.saved==0,"demo tried to complete apparatus");
  var live=new ConfigWorkbench();live.Workspace.quick=true;live.RefreshStartCaption();live.RunPrimaryAction();
  Check(live.start.label.text=="Start experiment"&&live.started==1&&live.saved==0,"live QuickStart dispatch changed");
  var dispatcher=new MainController();ConfigurationUI.ConfigWorkbench.IsOpen=true;ExperimentInput.Cancel.released=true;
  Check(ExperimentInput.IsEditingText&&!ExperimentInput.Released("Cancel"),"regression fixture no longer reproduces the ordinary text guard during setup");
  dispatcher.Tick();Check(dispatcher.escapes==1,"actual MainController.Update input gate swallowed setup Escape before its handler");
  ExperimentInput.Cancel.released=false;dispatcher.Tick();Check(dispatcher.escapes==1,"setup Escape fired without a real release edge");
  ConfigurationUI.ConfigWorkbench.IsOpen=false;ExperimentInput.Cancel.released=true;
  EventSystem.current=new EventSystem{currentSelectedGameObject=new GameObject{input=new InputField()}};
  dispatcher.Tick();Check(dispatcher.escapes==1,"runtime text fields lost their existing Escape guard");
  EventSystem.current=null;dispatcher.Tick();Check(dispatcher.escapes==2,"ordinary experiment/replay Escape no longer reaches its handler");
  Console.WriteLine("PASS: "+checks+" production primary-action checks; UI/validation/action boundaries mocked, no native app launched.");
 }
}
'''

def main():
    dotnet=shutil.which('dotnet')
    if not dotnet:raise SystemExit('A local .NET SDK is required.')
    version=max(map(int,re.findall(r'^(\d+)\.',subprocess.check_output([dotnet,'--list-sdks'],text=True),re.M)))
    source=(ROOT/'Assets/Scripts/ConfigurationUI/ConfigWorkbench.Session.cs').read_text()
    inputs=(ROOT/'Assets/Scripts/ExperimentInput.cs').read_text()
    input_production=declaration(inputs,'public static bool IsEditingText')
    for signature in ('public static bool Released(string name) =>','public static bool CancelReleased =>'):
        start=inputs.index(signature);input_production+='\n'+inputs[start:inputs.index(';',start)+1]
    controller=declaration((ROOT/'Assets/Scripts/MainController.cs').read_text(),'void Update()')
    properties=[]
    for signature in ('private bool CurrentDemo =>','private bool ApparatusDraft =>'):
        start=source.index(signature);properties.append(source[start:source.index(';',start)+1])
    production='\n'.join(properties+[declaration(source,'private List<string> PrimaryActionErrors()'),declaration(source,'private void RunPrimaryAction()'),declaration(source,'private void RefreshStartCaption()')])
    with tempfile.TemporaryDirectory(prefix='matrex-primary-action-') as folder:
        temporary=Path(folder);(temporary/'Program.cs').write_text(PRELUDE.replace('INPUT_PRODUCTION',input_production).replace('CONTROLLER_PRODUCTION',controller)+production+TESTS)
        (temporary/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        project=temporary/'Regression.csproj';project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup></Project>')
        env=dict(os.environ,DOTNET_CLI_HOME=folder,DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        result=subprocess.run([dotnet,'build',str(project),'--configfile',str(temporary/'NuGet.Config'),'-p:UseSharedCompilation=false','-v:q'],env=env,capture_output=True,text=True)
        if result.returncode:raise SystemExit(result.stdout+result.stderr)
        subprocess.run([dotnet,str(temporary/f'bin/Debug/net{version}.0/Regression.dll')],env=env,check=True)

if __name__=='__main__':main()
