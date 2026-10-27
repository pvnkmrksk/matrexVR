#!/usr/bin/env python3
"""Exercise the production FPS update with deterministic frame and callback clocks.

Only Unity/device state is substituted in the executable harness. The complete
production file is also compiled against the installed Unity API assemblies.
No Editor or player is launched, so native pacing measurements can run alongside.
"""
from pathlib import Path
from xml.sax.saxutils import escape
import os
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def declaration(source, signature):
    start = source.index(signature)
    brace = source.index('{', start)
    arrow = source.find('=>', start, brace)
    if arrow >= 0:
        return source[start:source.index(';', arrow) + 1]
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


HARNESS = r'''
using System;
class Time { public static double unscaledTimeAsDouble,realtimeSinceStartupAsDouble; }
class Application { public static int targetFrameRate=60; }
class QualitySettings { public static int vSyncCount; }
class Canvas { public bool enabled=true; }
class Text { public string text; }
class ProcessClock {
 public TimeSpan TotalProcessorTime; public int reads;
 public void Refresh(){reads++;} public void Dispose(){}
}
class FrameTiming {
 public ulong frameStartTimestamp;
 public double cpuFrameTime,cpuMainThreadFrameTime,cpuRenderThreadFrameTime,cpuMainThreadPresentWaitTime,gpuFrameTime;
}
class FrameTimingManager { public static void CaptureFrameTimings(){} public static int GetLatestTimings(int count,FrameTiming[] target)=>0; }
namespace ConfigurationUI { class ConfigWorkbench { public static bool IsOpen; } }
class OperatorOverlayVisibility { public static bool Hidden; }
namespace UnityEngine.SceneManagement {
 class SceneManager { public static Scene GetActiveScene()=>new Scene(); }
 class Scene { public string name="TerrainReliefChoice"; }
}
class Actual {
 private readonly Actual instance;
 private readonly Canvas displayCanvas=new Canvas(); private readonly Text label=new Text();
 private double previousTime,elapsed,peakInterval,cpuMain,cpuRender,gpu,presentWait;
 private bool hasPreviousFrame,timingsEnabled;
 private int frames,timingSamples,gpuSamples; private ulong previousTimestamp;
 private string graphicsApi="test"; private readonly FrameTiming[] timings=new FrameTiming[1];
 private ProcessClock measuredProcess; private TimeSpan previousProcessCpu;
 public Actual(ProcessClock process=null){instance=this;measuredProcess=process;}
 public FrameStatistics Latest {get;private set;}
 __PRODUCTION__
 public void Frame(double start,double callbackDelay=0){Time.unscaledTimeAsDouble=start;Time.realtimeSinceStartupAsDouble=start+callbackDelay;Update();}
}
static class Regression {
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Near(double actual,double expected,string message){Check(Math.Abs(actual-expected)<1e-8,message+": "+actual+" != "+expected);}
 static Actual Cadence(double hz,int frameCount=180,double origin=0) {
  var actual=new Actual();actual.Frame(origin);
  for(int n=1;n<=frameCount;n++){
   actual.Frame(origin+n/hz);
   if(actual.Latest!=null) {
    Near(actual.Latest.framesPerSecond,hz,"constant cadence has count or denominator bias");
    Near(actual.Latest.frameMeanMs,1000/hz,"mean disagrees with frame cadence");
    Near(actual.Latest.framePeakMs,1000/hz,"constant cadence peak drifted");
   }
  }
  Check(actual.Latest!=null,"constant cadence never publishes");return actual;
 }
 static void Main(){
  var sixty=Cadence(60);Check(sixty.Latest.framesPerSecond.ToString("0")=="60","60Hz header incorrectly floors");
  var ntsc=Cadence(59.94);Check(ntsc.Latest.framesPerSecond.ToString("0")=="60","59.94Hz should round to60");
  Cadence(60,180,3600); // Long-running clock retains the same exact interval count.
  var delayed=new Actual();delayed.Frame(12,.35);
  Check(delayed.Latest==null,"first Update includes startup as a completed frame");
  for(int n=1;n<=29;n++)delayed.Frame(12+n/60.0);
  Check(delayed.Latest==null,"startup delay prematurely completes the first half-second window");
  for(int n=30;n<=32;n++)delayed.Frame(12+n/60.0);
  Near(delayed.Latest.framesPerSecond,60,"startup time contaminates first measured FPS");
  Near(delayed.Latest.framePeakMs,1000.0/60,"startup time contaminates measured peak");
  var callback=new Actual();callback.Frame(0,.2);
  Actual.FrameStatistics previous=null;
  for(int n=1;n<=180;n++) {
   double delay=(n%60>=30)?.012:0;
   callback.Frame(n/60.0,delay);
   if(callback.Latest!=null&&!ReferenceEquals(callback.Latest,previous)) {
    Near(callback.Latest.framesPerSecond,60,"in-frame callback work changes measured cadence");
    Near(callback.Latest.framePeakMs,1000.0/60,"callback phase creates a false peak");
    Near(callback.Latest.realtimeSeconds,n/60.0+delay,"telemetry lost actual wall-clock capture timestamp");
    previous=callback.Latest;
   }
  }
  Check(previous!=null,"callback phase test never published");
  var missed=new Actual();missed.Frame(0);
  for(int n=1;n<=30;n++)missed.Frame((n+(n==30?1:0))/60.0);
  Near(missed.Latest.framesPerSecond,30/(31/60.0),"a missed refresh was hidden or normalized to target");
  Near(missed.Latest.framePeakMs,2000.0/60,"missed refresh disappeared from peak");
  Check(missed.Latest.framesPerSecond.ToString("0")=="58","missed-refresh header should expose58FPS");
  var slow=new Actual();slow.Frame(0);
  for(int n=1;n<=30;n++)slow.Frame(n/60.0+(n==30?.005:0));
  Near(slow.Latest.framesPerSecond,30/.505,"a small actual delay was snapped to target");
  Check(slow.Latest.framesPerSecond.ToString("0")=="59","small-delay header should expose59FPS");
  var cpu=new ProcessClock{TotalProcessorTime=TimeSpan.FromSeconds(8)};
  var process=new Actual(cpu);process.Frame(12);
  for(int n=1;n<=32;n++){cpu.TotalProcessorTime=TimeSpan.FromSeconds(8+n*.001);process.Frame(12+n/60.0);}
  Near(process.Latest.processCpuMsPerFrame.Value,1,"startup CPU contaminates measured process work");
  Console.WriteLine("PASS: "+checks+" production-update FPS assertions; startup,60/59.94Hz,callback phase,missed refresh,CPU epoch and wall-clock timestamp.");
 }
}
'''

API_STUBS = r'''
using System;
using UnityEngine;
class MainController {
 public static MainController Instance; public static event Action SystemConfigurationChanged;
 public SystemConfig GetSystemConfig(string rig)=>new SystemConfig();
}
class SystemConfig { public int targetDisplay; }
class OperatorOverlayVisibility { public static bool Hidden; }
class DesktopDisplay { public static float UiScale=1; }
class OperatorOverlayLayout {
 public static int Revision; public static void Invalidate(){} public static void Release(string owner){}
 public static Vector2 DisplaySize(int display)=>new Vector2(1920,1080);
 public static bool Reserve(string owner,int display,Rect desired,out Rect placed){placed=desired;return true;}
}
namespace ConfigurationUI { class ConfigWorkbench { public static bool IsOpen; } }
'''


def main():
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    sdk = subprocess.check_output([dotnet, '--list-sdks'], text=True)
    version = max(map(int, re.findall(r'^(\d+)\.', sdk, re.M)))
    engines = sorted(Path('/Applications/Unity/Hub/Editor').glob('*/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine'))
    ui = ROOT/'.local/project/Library/ScriptAssemblies/UnityEngine.UI.dll'
    if not engines or not ui.exists():
        raise SystemExit('Installed Unity API assemblies and prepared project UnityEngine.UI.dll are required.')
    source = (ROOT/'Assets/Scripts/fps.cs').read_text()
    methods = '\n'.join(declaration(source, name) for name in (
        'public sealed class FrameStatistics', 'private void Update()',
        'private static bool Valid', 'private static string Ms'))
    with tempfile.TemporaryDirectory(prefix='matrex-frame-rate-') as folder:
        temporary = Path(folder)
        environment = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        (temporary/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        for kind in ('Measurement', 'UnityApi'):
            project_dir = temporary/kind
            project_dir.mkdir()
            references = ''
            if kind == 'Measurement':
                (project_dir/'Program.cs').write_text(HARNESS.replace('__PRODUCTION__', methods))
            else:
                (project_dir/'fps.cs').write_text(source)
                (project_dir/'AppStubs.cs').write_text(API_STUBS)
                references = ''.join(f'<Reference Include="{escape(path.stem)}"><HintPath>{escape(str(path))}</HintPath></Reference>' for path in [*engines[-1].glob('*.dll'), ui])
            project = project_dir/(kind+'.csproj')
            output = 'Exe' if kind == 'Measurement' else 'Library'
            project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>{output}</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup><ItemGroup>{references}</ItemGroup></Project>')
            result = subprocess.run([dotnet, 'build', str(project), '--configfile', str(temporary/'NuGet.Config'), '-p:UseSharedCompilation=false', '-v:q'], env=environment, capture_output=True, text=True)
            if result.returncode:
                raise SystemExit(result.stdout+result.stderr)
            if kind == 'Measurement':
                subprocess.run([dotnet,str(project_dir/f'bin/Debug/net{version}.0/Measurement.dll')], env=environment,check=True)
            else:
                print('PASS: complete fps.cs compiled against installed Unity API assemblies.')


if __name__ == '__main__':
    main()
