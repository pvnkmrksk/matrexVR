#!/usr/bin/env python3
"""Exercise exact production library polling/pointer recovery offline. No Unity/native/UI launch."""
from pathlib import Path
from test_system_display_routing import declaration
import os,re,shutil,subprocess,tempfile
ROOT=Path(__file__).resolve().parents[1]
PRELUDE=r'''
using System;using System.Collections.Generic;using System.Collections.Concurrent;using System.Linq;using System.Threading.Tasks;using UnityEngine;
namespace UnityEngine {public static class Time {public static int frameCount;} }
namespace UnityEngine.InputSystem {public sealed class ButtonState {public bool isPressed;}public sealed class Mouse {public static Mouse current=new Mouse();public ButtonState leftButton=new ButtonState();}}
public enum DisplayStyle {None,Flex}public sealed class Styles {public DisplayStyle display;}public sealed class Widget {public string text,value;public Styles style=new Styles();}
public sealed class Entry {public string DirectoryPath;public bool Omit;}
public sealed class ConfigWorkbench {
 private bool replayLibraryMode=true,libraryRunning=true,editPointerHeld=true,editPointerUsesMouse=true,libraryFreshnessRunning;
 private int editPointerDownFrame=10,libraryGeneration=1,libraryPickerGeneration;private Task<string> libraryPicker;
 private string libraryMessage,libraryDirectoryToken,librarySelected;
 private readonly Dictionary<string,Entry> libraryEntries=new Dictionary<string,Entry>();
 private readonly HashSet<string> libraryExternal=new HashSet<string>(),libraryChecked=new HashSet<string>();
 private readonly List<Entry> libraryVisible=new List<Entry>();private Widget libraryEmpty=new Widget(),libraryList=new Widget(),librarySearch=new Widget();
 private sealed class LibraryUpdate {public int Version;public List<Entry> Initial;public Entry Entry;public string Message;public bool Done,FreshnessResult,NeedsRefresh;public string DirectoryToken;}
 private readonly ConcurrentQueue<LibraryUpdate> libraryUpdates=new ConcurrentQueue<LibraryUpdate>();
 private int rowRefreshes;
 private void InspectExternalFolder(string path){}private void RefreshLibraryFooter(){}private void StartLibraryScan(){}private void RefreshLibraryRows(){rowRefreshes++;}private void RefreshLibraryDetails(){}private void CheckLibraryFreshness(){}
'''
TESTS=r'''
 static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 public static void Main(){
  var panel=new ConfigWorkbench();panel.libraryUpdates.Enqueue(new LibraryUpdate{Version=1,Done=true,Message="Indexed"});
  Time.frameCount=10;UnityEngine.InputSystem.Mouse.current.leftButton.isPressed=false;panel.PollRecordingLibrary();
  Check(panel.libraryRunning&&panel.libraryUpdates.Count==1,"pointer-down frame recycled clicked content");
  Time.frameCount=11;UnityEngine.InputSystem.Mouse.current.leftButton.isPressed=true;panel.PollRecordingLibrary();
  Check(panel.libraryRunning&&panel.libraryUpdates.Count==1,"physical held click failed to defer row refresh");
  // The captured navigation button consumed PointerUp; no root callback clears the flag.
  UnityEngine.InputSystem.Mouse.current.leftButton.isPressed=false;panel.PollRecordingLibrary();
  Check(!panel.editPointerHeld,"missed root pointer-up left library stuck forever");
  Check(!panel.libraryRunning&&panel.libraryUpdates.Count==0&&panel.libraryMessage=="Indexed"&&panel.rowRefreshes==1,"completed worker updates were not applied after physical release");
  panel.libraryUpdates.Enqueue(new LibraryUpdate{Version=0,Done=true,Message="Stale"});panel.PollRecordingLibrary();
  Check(panel.libraryMessage=="Indexed","cancelled generation changed current library status");
  panel.libraryRunning=true;panel.RefreshLibraryEmptyState();
  Check(panel.libraryEmpty.text=="Reading saved recordings…"&&panel.libraryList.style.display==DisplayStyle.None,"active scan displayed contradictory empty-library/ListView message");
  panel.libraryRunning=false;panel.RefreshLibraryEmptyState();Check(panel.libraryEmpty.text.StartsWith("No recordings found"),"completed empty scan lacks useful empty state");
  panel.librarySearch.value="missing";panel.RefreshLibraryEmptyState();Check(panel.libraryEmpty.text=="No recordings match your search.","search empty state suggests there are no recordings");
  panel.libraryVisible.Add(new Entry());panel.RefreshLibraryEmptyState();Check(panel.libraryEmpty.style.display==DisplayStyle.None&&panel.libraryList.style.display==DisplayStyle.Flex,"discovered rows stayed hidden after indexing");
  Console.WriteLine("PASS: "+checks+" production library poll/pointer/empty-state checks; no Unity/native/UI launch.");
 }
}
'''
def main():
 dotnet=shutil.which('dotnet')
 if not dotnet:raise SystemExit('A local .NET SDK is required.')
 version=max(map(int,re.findall(r'^(\d+)\.',subprocess.check_output([dotnet,'--list-sdks'],text=True),re.M)))
 ui=(ROOT/'Assets/Scripts/ConfigurationUI/ConfigWorkbench.ReplayLibrary.cs').read_text()
 builder=(ROOT/'Assets/Scripts/ConfigurationUI/ConfigWorkbench.Builder.cs').read_text()
 production='\n'.join([declaration(builder,'private bool IsEditPointerHeld()'),declaration(ui,'private void PollRecordingLibrary()'),declaration(ui,'private void RefreshLibraryEmptyState()')])
 with tempfile.TemporaryDirectory(prefix='matrex-library-poll-') as folder:
  temp=Path(folder);(temp/'Program.cs').write_text(PRELUDE+production+TESTS);(temp/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
  project=temp/'Regression.csproj';project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup></Project>')
  env=dict(os.environ,DOTNET_CLI_HOME=folder,DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
  result=subprocess.run([dotnet,'build',str(project),'--configfile',str(temp/'NuGet.Config'),'-p:UseSharedCompilation=false','-v:q'],env=env,capture_output=True,text=True)
  if result.returncode:raise SystemExit(result.stdout+result.stderr)
  subprocess.run([dotnet,str(temp/f'bin/Debug/net{version}.0/Regression.dll')],env=env,check=True)
if __name__=='__main__':main()
