#!/usr/bin/env python3
"""Exercise the actual desktop startup coroutine without a native app or GPU."""
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
using System.Collections;
using System.Collections.Generic;
namespace UnityEngine {
 public sealed class DefaultExecutionOrder:Attribute {public DefaultExecutionOrder(int value){}}
 public enum RuntimeInitializeLoadType {SubsystemRegistration,BeforeSceneLoad}
 public sealed class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType value){}}
 public class MonoBehaviour {protected static void DontDestroyOnLoad(object value){}public void StartCoroutine(IEnumerator routine){}}
 public sealed class GameObject {public GameObject(string name){}public T AddComponent<T>()where T:new()=>new T();}
 public static class Application {public static bool isEditor=true,isBatchMode,isFocused=true;}
 public static class Time {public static double realtimeSinceStartupAsDouble;public static float unscaledTime=>(float)realtimeSinceStartupAsDouble;}
 public static class PlayerPrefs {public static int display;public static int GetInt(string name,int fallback)=>display;public static void SetInt(string name,int value){display=value;}public static void Save(){}}
 public static class Mathf {public static int Clamp(int v,int low,int high)=>Math.Max(low,Math.Min(high,v));public static float Clamp(float v,float low,float high)=>Math.Max(low,Math.Min(high,v));}
 public struct DisplayInfo:IEquatable<DisplayInfo> {public int id,width,height;public string name;public bool Equals(DisplayInfo other)=>id==other.id&&width==other.width&&height==other.height&&name==other.name;}
 public struct Vector2Int {public static Vector2Int zero=>default;}
 public enum FullScreenMode {FullScreenWindow,Windowed}
 public struct Resolution {public int width,height;}
 public sealed class AsyncOperation {public bool isDone;}
 public static class Debug {public static List<string> logs=new List<string>(),warnings=new List<string>();public static void Log(string value){logs.Add(value);}public static void LogWarning(string value){warnings.Add(value);}}
 public static class Screen {
  public static List<DisplayInfo> layout=new List<DisplayInfo>();public static DisplayInfo mainWindowDisplayInfo;
  public static Resolution currentResolution;public static int width,height,moves,resolutions;public static float dpi=192;
  public static bool throwMove,throwResolution;public static FullScreenMode fullScreenMode;
  public static AsyncOperation operation;public static void GetDisplayLayout(List<DisplayInfo> target){target.Clear();target.AddRange(layout);}
  public static AsyncOperation MoveMainWindowTo(DisplayInfo target,Vector2Int position){moves++;if(throwMove)throw new InvalidOperationException("native move rejected");return operation;}
  public static void SetResolution(int w,int h,FullScreenMode mode){if(throwResolution)throw new InvalidOperationException("native resize rejected");resolutions++;width=w;height=h;fullScreenMode=mode;}
 }
}
'''
PROGRAM = r'''
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
static class Regression {
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static DisplayInfo Panel(int id,int width=3024,int height=1964)=>new DisplayInfo{id=id,name="Display "+id,width=width,height=height};
 static DesktopDisplay Reset(int count=1,int selected=0){
  Time.realtimeSinceStartupAsDouble=0;PlayerPrefs.display=selected;Screen.layout.Clear();for(int i=0;i<count;i++)Screen.layout.Add(Panel(i));
  Screen.mainWindowDisplayInfo=Panel(0);Screen.currentResolution=new Resolution{width=3024,height=1964};Screen.width=3024;Screen.height=1898;
  Screen.moves=Screen.resolutions=0;Screen.throwMove=Screen.throwResolution=false;Screen.operation=new AsyncOperation();Screen.fullScreenMode=FullScreenMode.FullScreenWindow;Debug.logs.Clear();Debug.warnings.Clear();return new DesktopDisplay();
 }
 static IEnumerator Apply(DesktopDisplay display)=>(IEnumerator)typeof(DesktopDisplay).GetMethod("Apply",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(display,null);
 static bool Changing(DesktopDisplay display)=>(bool)typeof(DesktopDisplay).GetField("changing",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(display);
 static int Drain(IEnumerator routine){int ticks=0;while(routine.MoveNext()){Check(routine.Current==null,"Apply must never yield an unbounded native AsyncOperation");Time.realtimeSinceStartupAsDouble+=.5;Check(++ticks<20,"native window move can never stall startup indefinitely");}return ticks;}
 static void Main(string[] args){
  var display=Reset();
  if(args.Contains("--windowed")){Drain(Apply(display));Check(Screen.fullScreenMode==FullScreenMode.Windowed&&Screen.resolutions==0&&!Changing(display),"advanced source-only windowed exit must release changing state");Console.WriteLine("PASS: source-only windowed exit");return;}
  Drain(Apply(display));Check(Screen.moves==0,"single-monitor startup must not submit a native move");Check(Screen.resolutions==1&&!Changing(display),"single-monitor startup must reach fullscreen and release changing state");Check(Debug.logs.Any(line=>line.Contains("Native fullscreen")),"single-monitor startup must reach its completion diagnostic");
  display=Reset(1,99);Drain(Apply(display));Check(Screen.moves==0&&Screen.resolutions==1,"disconnected remembered monitor must clamp to the only display without a move");
  display=Reset(2,0);Drain(Apply(display));Check(Screen.moves==0&&Screen.resolutions==1,"already-selected monitor must not submit a redundant native move");
  display=Reset(2,1);var routine=Apply(display);Check(routine.MoveNext()&&Changing(display)&&Screen.moves==1&&Screen.resolutions==0,"genuine monitor change should wait for native completion");
  Time.realtimeSinceStartupAsDouble=1;Screen.operation.isDone=true;Screen.mainWindowDisplayInfo=Panel(1,2560,1440);Screen.currentResolution=new Resolution{width=2560,height=1440};Drain(routine);Check(Screen.width==2560&&Screen.height==1440&&Screen.resolutions==1&&!Changing(display),"completed move must use the destination native resolution");
  display=Reset(2,1);Drain(Apply(display));Check(Screen.moves==1&&Screen.resolutions==1&&!Changing(display),"a never-completing move must fall back to usable fullscreen");Check(Debug.warnings.Any(line=>line.Contains("timed out")),"a timed-out native move must be diagnosed");
  display=Reset(2,1);Screen.throwMove=true;Drain(Apply(display));Check(Screen.resolutions==1&&!Changing(display)&&Debug.warnings.Any(line=>line.Contains("native move rejected")),"native move exceptions must not block fullscreen");
  display=Reset();Screen.currentResolution=default;Drain(Apply(display));Check(Screen.width==3024&&Screen.height==1964,"invalid resolution must use current display dimensions");
  display=Reset();Screen.throwResolution=true;bool failed=false;try{Drain(Apply(display));}catch(InvalidOperationException){failed=true;}Check(failed&&!Changing(display),"native resize exceptions must release changing state for a later retry");Screen.throwResolution=false;Drain(Apply(display));Check(Screen.resolutions==1&&!Changing(display),"a failed resize must remain retryable");
  display=Reset(2,1);routine=Apply(display);Check(routine.MoveNext()&&Changing(display),"pending move must enter changing state");((IDisposable)routine).Dispose();Check(!Changing(display),"disposing a pending startup coroutine must release changing state");
  Console.WriteLine("PASS: "+checks+" actual-source desktop coroutine checks; single/current monitor bypass, completed/stuck/throwing native move, native fallback and cleanup; no native app/GPU launched.");
 }
}
'''


def main():
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    sdk = subprocess.check_output([dotnet, '--list-sdks'], text=True)
    version = max(map(int, re.findall(r'^(\d+)\.', sdk, re.M)))
    with tempfile.TemporaryDirectory(prefix='matrex-desktop-display-') as folder:
        temporary = Path(folder)
        env = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        (temporary/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (temporary/'Stubs.cs').write_text(STUBS)
        (temporary/'Program.cs').write_text(PROGRAM)
        project = temporary/'Regression.csproj'
        source = ROOT/'Assets/Scripts/DesktopDisplay.cs'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include="{escape(str(source))}" /></ItemGroup></Project>')
        for windowed in (False,True):
            command = [dotnet,'build',str(project),'--configfile',str(temporary/'NuGet.Config'),'-p:UseSharedCompilation=false','-v:q']
            if windowed:
                command.append('-p:DefineConstants=MATREX_ALLOW_WINDOWED')
            result = subprocess.run(command,env=env,capture_output=True,text=True)
            if result.returncode:
                raise SystemExit(result.stdout+result.stderr)
            subprocess.run([dotnet,str(temporary/f'bin/Debug/net{version}.0/Regression.dll'),*(['--windowed'] if windowed else [])],env=env,check=True)


if __name__ == '__main__':
    main()
