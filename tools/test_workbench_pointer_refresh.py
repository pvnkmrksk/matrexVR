#!/usr/bin/env python3
"""Compile real refresh/hold methods with controlled UI scheduler and input stubs.

This checks release handling when a captured pointer-up never reaches the root.
It does not inject native mouse events, launch Unity, or validate actual rendering.
"""
from pathlib import Path
from test_system_display_routing import declaration
import os
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
PRELUDE = r'''
using System;using System.Collections.Generic;using System.Linq;using UnityEngine;using UnityEngine.UIElements;
namespace UnityEngine {public static class Time{public static int frameCount;}}
namespace UnityEngine.InputSystem {public sealed class Button{public bool isPressed;}public sealed class Mouse{public static Mouse current=new Mouse();public Button leftButton=new Button();}}
namespace UnityEngine.UIElements {
 public static class PointerType{public const string mouse="mouse";}
 public enum TrickleDown{TrickleDown}
 public sealed class PointerDownEvent{public int button;public string pointerType;}
 public sealed class PointerUpEvent{public int button;}
 public sealed class PointerCancelEvent{}
}
interface IVisualElementScheduledItem{void Pause();IVisualElementScheduledItem Every(int n);}
class Item:IVisualElementScheduledItem{public bool paused;public Action action;public void Pause(){paused=true;}public IVisualElementScheduledItem Every(int n)=>this;public void Tick(){if(!paused)action();}}
class Scheduler{public List<Item> items=new List<Item>();public IVisualElementScheduledItem Execute(Action a){var item=new Item{action=a};items.Add(item);return item;}public void Tick(){foreach(var item in items.ToArray())item.Tick();}}
class Root{public object panel=new object();public Scheduler schedule=new Scheduler();private Dictionary<Type,Delegate> callbacks=new Dictionary<Type,Delegate>();public void RegisterCallback<T>(Action<T> callback,TrickleDown phase){callbacks[typeof(T)]=callback;}public void Send<T>(T evt){((Action<T>)callbacks[typeof(T)])(evt);}}
class ListView{public int refreshes;public void RefreshItems(){refreshes++;}}
class ConfigDocument{}
class WorkbenchViewState{public static WorkbenchViewState Capture(Root root)=>new WorkbenchViewState();}
class Regression{
 private bool isActiveAndEnabled=true,preparing,editPointerHeld,editPointerUsesMouse;
 private int editPointerDownFrame=-1,viewGeneration,pendingPointerGeneration,pendingBlockRefreshGeneration,rebuilds;
 private Root root=new Root();private ListView blockList=new ListView(),pendingBlockRefreshList;
 private ConfigDocument selected=new ConfigDocument(),pendingPointerSelection,pendingBlockRefreshSelection;
 private IVisualElementScheduledItem pendingPointerRebuild,pendingBlockRefresh;
 private void RebuildEditedView(WorkbenchViewState state){rebuilds++;viewGeneration++;}
'''
TESTS = r'''
 static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static Regression New(){Time.frameCount=10;UnityEngine.InputSystem.Mouse.current=new UnityEngine.InputSystem.Mouse();var r=new Regression();r.InstallEditPointerTracking();return r;}
 void Down(string type="mouse"){root.Send(new PointerDownEvent{button=0,pointerType=type});}
 static void Main(){
  var r=New();r.Down();r.ScheduleBlockItemsRefresh();r.DeferPointerEditRebuild();r.root.schedule.Tick();
  Check(r.blockList.refreshes==0&&r.rebuilds==0,"initial pointer frame must survive input-state update lag");
  UnityEngine.InputSystem.Mouse.current.leftButton.isPressed=true;
  for(int n=0;n<5;n++){Time.frameCount++;r.root.schedule.Tick();}
  Check(r.blockList.refreshes==0&&r.rebuilds==0,"held pointer must protect the clicked row and whole form");
  UnityEngine.InputSystem.Mouse.current.leftButton.isPressed=false;Time.frameCount++;r.root.schedule.Tick();
  Check(r.blockList.refreshes==1&&r.rebuilds==1,"physical release without root PointerUp must unblock both queues");
  Check(!r.editPointerHeld,"physical release must repair the retained root flag");r.root.schedule.Tick();
  Check(r.blockList.refreshes==1&&r.rebuilds==1,"completed queues must stop");
  r=New();r.Down();UnityEngine.InputSystem.Mouse.current.leftButton.isPressed=true;
  for(int n=0;n<20;n++)r.ScheduleBlockItemsRefresh();
  Check(r.root.schedule.items.Count==1,"rapid edits must coalesce into one row refresh");
  r.root.Send(new PointerUpEvent{button=0});r.root.schedule.Tick();Check(r.blockList.refreshes==1,"normal root PointerUp must still release immediately");
  r=New();r.Down("touch");r.ScheduleBlockItemsRefresh();Time.frameCount++;r.root.schedule.Tick();
  Check(r.blockList.refreshes==0&&r.editPointerHeld,"an unpressed mouse must not cancel a touch gesture");
  r.root.Send(new PointerUpEvent{button=0});r.root.schedule.Tick();Check(r.blockList.refreshes==1,"touch release must refresh normally");
  r=New();r.Down();UnityEngine.InputSystem.Mouse.current=null;r.ScheduleBlockItemsRefresh();Time.frameCount++;r.root.schedule.Tick();
  Check(r.blockList.refreshes==0,"missing mouse device must retain event-based hold protection");r.root.Send(new PointerCancelEvent());r.root.schedule.Tick();Check(r.blockList.refreshes==1,"pointer cancellation must unblock without a mouse device");
  r=New();r.ScheduleBlockItemsRefresh();var old=r.blockList;r.blockList=new ListView();r.viewGeneration++;r.root.schedule.Tick();Check(old.refreshes==0&&r.blockList.refreshes==0,"stale generation must not recycle either view");
  r.ScheduleBlockItemsRefresh();r.selected=new ConfigDocument();r.root.schedule.Tick();Check(r.blockList.refreshes==0,"navigation must discard the old document refresh");
  r.ScheduleBlockItemsRefresh();r.preparing=true;r.root.schedule.Tick();Check(r.blockList.refreshes==0,"preparation must suppress pending row updates");
  r.preparing=false;r.ScheduleBlockItemsRefresh();r.root.panel=null;r.root.schedule.Tick();Check(r.blockList.refreshes==0,"detached panel must not refresh");
  r.root.panel=new object();r.ScheduleBlockItemsRefresh();r.isActiveAndEnabled=false;r.root.schedule.Tick();Check(r.blockList.refreshes==0,"disabled workbench must not refresh");
  Console.WriteLine("PASS: "+checks+" exact-source pointer-release/refresh checks; controlled scheduler/input boundaries, no native mouse events or Unity process.");
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
    production = '\n'.join(declaration(source, name) for name in (
        'private void InstallEditPointerTracking()', 'private bool IsEditPointerHeld()',
        'private void DeferPointerEditRebuild()', 'private void ScheduleBlockItemsRefresh()'))
    with tempfile.TemporaryDirectory(prefix='matrex-workbench-pointer-') as folder:
        temporary = Path(folder)
        env = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        (temporary/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (temporary/'Program.cs').write_text(PRELUDE+production+TESTS)
        project = temporary/'Regression.csproj'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup></Project>')
        result = subprocess.run([dotnet, 'build', str(project), '--configfile', str(temporary/'NuGet.Config'), '-p:UseSharedCompilation=false', '-v:q'], env=env, capture_output=True, text=True)
        if result.returncode:
            raise SystemExit(result.stdout+result.stderr)
        subprocess.run([dotnet, str(temporary/f'bin/Debug/net{version}.0/Regression.dll')], env=env, check=True)


if __name__ == '__main__':
    main()
