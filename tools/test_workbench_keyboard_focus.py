#!/usr/bin/env python3
"""Exercise the production focus fallback with attached/detached controls; no native input claim."""
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
namespace UnityEngine.UIElements {
 public enum DisplayStyle { Flex, None }
 public enum Visibility { Visible, Hidden }
 public interface IVisualElementScheduledItem { void Pause(); }
 public sealed class Style { public DisplayStyle display=DisplayStyle.Flex;public Visibility visibility=Visibility.Visible; }
 public sealed class FocusController { public object focusedElement; }
 public sealed class Panel { public FocusController focusController=new FocusController(); }
 public sealed class Scheduler {
  private sealed class Item:IVisualElementScheduledItem { public Action callback;public bool paused;public void Pause(){paused=true;}public void Run(){if(!paused)callback();} }
  private readonly List<Item> pending=new List<Item>();
  public IVisualElementScheduledItem Execute(Action action){var item=new Item{callback=action};pending.Add(item);return item;}
  public void Drain(){var items=pending.ToArray();pending.Clear();foreach(var item in items)item.Run();}
 }
 public sealed class VisualElement {
  public Panel panel;public bool enabledInHierarchy=true;public Style resolvedStyle=new Style();public Scheduler schedule=new Scheduler();
  public readonly List<VisualElement> children=new List<VisualElement>();public int focusCalls;
  public bool Contains(VisualElement other){foreach(var child in children)if(child==other||child.Contains(other))return true;return false;}
  public void Focus(){focusCalls++;panel.focusController.focusedElement=this;}
 }
}
public sealed class FocusRegression {
 private bool isActiveAndEnabled=true;
 private UnityEngine.UIElements.VisualElement root,modal;
 private UnityEngine.UIElements.IVisualElementScheduledItem keyboardFocusFallback;
'''
TESTS = r'''
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static FocusRegression Fresh(){return new FocusRegression{root=new UnityEngine.UIElements.VisualElement{panel=new UnityEngine.UIElements.Panel()}};}
 public static void Main(){
  var page=Fresh();page.ScheduleKeyboardFocus();Check(page.root.focusCalls==0,"fallback ran before rebuild completion");page.root.schedule.Drain();
  Check(page.root.panel.focusController.focusedElement==page.root,"empty initial page has no keyboard target");
  var input=new UnityEngine.UIElements.VisualElement{panel=page.root.panel};page.root.children.Add(input);input.Focus();
  page.ScheduleKeyboardFocus();page.root.schedule.Drain();Check(page.root.panel.focusController.focusedElement==input,"fallback stole focus from an attached text control");
  page.root.children.Remove(input);input.panel=null;page.ScheduleKeyboardFocus();page.root.schedule.Drain();
  Check(page.root.panel.focusController.focusedElement==page.root,"removed navigation control left stale focus");
  var foreign=new UnityEngine.UIElements.VisualElement{panel=page.root.panel};foreign.Focus();page.RestoreKeyboardFocus();
  Check(page.root.panel.focusController.focusedElement==page.root,"another document's stale control blocked keyboard recovery");
  var disabled=new UnityEngine.UIElements.VisualElement{panel=page.root.panel,enabledInHierarchy=false};page.root.children.Add(disabled);disabled.Focus();page.RestoreKeyboardFocus();
  Check(page.root.panel.focusController.focusedElement==page.root,"disabled control blocked keyboard recovery");
  var hidden=new UnityEngine.UIElements.VisualElement{panel=page.root.panel};page.root.children.Add(hidden);hidden.resolvedStyle.display=UnityEngine.UIElements.DisplayStyle.None;hidden.Focus();page.RestoreKeyboardFocus();
  Check(page.root.panel.focusController.focusedElement==page.root,"display:none control blocked keyboard recovery");
  hidden.resolvedStyle.display=UnityEngine.UIElements.DisplayStyle.Flex;hidden.resolvedStyle.visibility=UnityEngine.UIElements.Visibility.Hidden;hidden.Focus();page.RestoreKeyboardFocus();
  Check(page.root.panel.focusController.focusedElement==page.root,"invisible control blocked keyboard recovery");
  page.modal=new UnityEngine.UIElements.VisualElement{panel=page.root.panel};var close=new UnityEngine.UIElements.VisualElement{panel=page.root.panel};page.modal.children.Add(close);page.root.children.Add(page.modal);close.Focus();
  page.ScheduleKeyboardFocus();page.root.schedule.Drain();Check(page.root.panel.focusController.focusedElement==close,"fallback escaped the modal focus trap");
  page.root.panel.focusController.focusedElement=null;int before=page.root.focusCalls;page.RestoreKeyboardFocus();Check(page.root.focusCalls==before,"fallback raced the modal's scheduled initial focus");
  page.root.children.Remove(page.modal);page.modal=null;page.ScheduleKeyboardFocus();page.root.schedule.Drain();Check(page.root.panel.focusController.focusedElement==page.root,"closing a modal left keyboard focus empty");
  page.root.panel.focusController.focusedElement=null;before=page.root.focusCalls;page.ScheduleKeyboardFocus();page.ScheduleKeyboardFocus();page.root.schedule.Drain();Check(page.root.focusCalls==before+1,"successive rebuilds queued competing focus changes");
  before=page.root.focusCalls;page.RestoreKeyboardFocus();Check(page.root.focusCalls==before,"already-focused workbench was focused redundantly");
  page.root.panel.focusController.focusedElement=null;page.isActiveAndEnabled=false;page.RestoreKeyboardFocus();Check(page.root.focusCalls==before,"inactive workbench stole focus");
  page.isActiveAndEnabled=true;page.root.panel=null;page.RestoreKeyboardFocus();Check(page.root.focusCalls==before,"detached panel was accessed while restoring focus");
  page.root=null;page.RestoreKeyboardFocus();checks++;
  Console.WriteLine("PASS: "+checks+" production workbench focus recovery checks; controlled focus/scheduler fakes, no native key delivery claim.");
 }
}
'''


def main():
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    version = max(map(int, re.findall(r'^(\d+)\.', subprocess.check_output([dotnet, '--list-sdks'], text=True), re.M)))
    source = (ROOT / 'Assets/Scripts/ConfigurationUI/ConfigWorkbench.Accessibility.cs').read_text()
    production = '\n'.join(declaration(source, signature) for signature in ('private void ScheduleKeyboardFocus()', 'private void RestoreKeyboardFocus()'))
    # Lifecycle wiring is separate from the simulated focus-controller cases.
    main_source = (ROOT / 'Assets/Scripts/ConfigurationUI/ConfigWorkbench.cs').read_text()
    builder = (ROOT / 'Assets/Scripts/ConfigurationUI/ConfigWorkbench.Builder.cs').read_text()
    assert 'root.focusable=true;root.tabIndex=-1;' in declaration(source, 'private void InstallKeyboardNavigation()')
    assert 'ScheduleKeyboardFocus();' in declaration(main_source, 'private void BuildContent()')
    assert 'keyboardFocusFallback?.Pause();' in declaration(main_source, 'private void OnDestroy()')
    assert 'ScheduleKeyboardFocus();' in declaration(builder, 'private void CloseModal()')
    with tempfile.TemporaryDirectory(prefix='matrex-keyboard-focus-') as folder:
        temporary = Path(folder)
        (temporary / 'Program.cs').write_text('using UnityEngine.UIElements;\n' + PRELUDE + production + TESTS)
        (temporary / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        project = temporary / 'Regression.csproj'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup></Project>')
        env = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        result = subprocess.run([dotnet, 'build', str(project), '--configfile', str(temporary / 'NuGet.Config'), '-p:UseSharedCompilation=false', '-v:q'], env=env, capture_output=True, text=True)
        if result.returncode:
            raise SystemExit(result.stdout + result.stderr)
        subprocess.run([dotnet, str(temporary / f'bin/Debug/net{version}.0/Regression.dll')], env=env, check=True)
    print('PASS: 4 lifecycle source guards connect focus recovery to setup, page rebuilds, modal close and cleanup.')


if __name__ == '__main__':
    main()
