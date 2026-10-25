#!/usr/bin/env python3
"""Run production overlay placement with real managed Unity Rect math, no GPU/device.

Only display state, the controller singleton and frame-clear calls are substituted.
The allocator and visibility implementation are compiled unchanged; viewport order
and pixel-rectangle methods are copied verbatim from the production source.
"""
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
using System.Collections.Generic;
using UnityEngine;
public static class HarnessScreen { public static int width=1920,height=1080; }
public class HarnessDisplay {
 public int renderingWidth=1920,renderingHeight=1080; public bool active=true;
 public static HarnessDisplay[] displays={new HarnessDisplay(),new HarnessDisplay()};
}
public sealed class MainController {
 public static MainController Instance;
 public Dictionary<string,SystemConfig> SystemConfigs=new Dictionary<string,SystemConfig>();
}
public sealed class SystemConfig {
 public bool desktopPreview,horizontal=true;public int targetDisplay,ledPanelWidth=128,ledPanelHeight=128,startRow,startCol;
 public string displayOrder="DRBLFU";
}
public static class DisplayFrameClear {public static int requests;public static void ClearOnce(int display){requests++;}}
'''
PROGRAM = r'''
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
static class Regression {
 static int checks,cases;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static bool Inside(Rect a,Rect b)=>a.xMin>=b.xMin&&a.yMin>=b.yMin&&a.xMax<=b.xMax&&a.yMax<=b.yMax;
 static void Safe(Rect placed,Rect bounds,IEnumerable<Rect> blocked,string message){
  Check(Inside(placed,bounds)&&placed.width>0&&placed.height>0,message+": outside display");
  Check(!blocked.Any(b=>b.Overlaps(placed)),message+": covers protected pixels");
 }
 static void Geometry(){
  var bounds=new Rect(0,0,1000,800);Rect actual;
  var requested=new Rect(50,60,320,80);
  Check(OperatorOverlayLayout.TryPlace(requested,bounds,new Rect[0],out actual)&&actual==requested,"unblocked request should stay exact");
  Check(!OperatorOverlayLayout.TryPlace(new Rect(0,0,1001,50),bounds,new Rect[0],out actual),"oversized width must omit");
  Check(!OperatorOverlayLayout.TryPlace(new Rect(0,0,1,801),bounds,new Rect[0],out actual),"oversized height must omit");
  Check(!OperatorOverlayLayout.TryPlace(new Rect(0,0,0,50),bounds,new Rect[0],out actual),"zero width must omit");
  Check(!OperatorOverlayLayout.TryPlace(new Rect(0,0,20,20),bounds,new[]{bounds},out actual),"fully occupied display must omit");
  var blocked=new[]{new Rect(0,0,128,128),new Rect(128,0,128,128),new Rect(256,0,128,128)};
  Check(OperatorOverlayLayout.TryPlace(new Rect(12,12,420,190),bounds,blocked,out actual),"HUD must relocate past lower LED strip");Safe(actual,bounds,blocked,"lower LED strip");
  var shifted=new Rect(40,70,500,400);var wall=new Rect(180,70,120,260);
  Check(OperatorOverlayLayout.TryPlace(new Rect(-100,-100,100,100),shifted,new[]{wall},out actual),"out-of-bounds desired location should relocate");Safe(actual,shifted,new[]{wall},"nonzero display origin");
  // Deterministic random layouts catch interval overlap/partial-offscreen errors.
  var random=new System.Random(947);
  for(int n=0;n<1500;n++){
   var obstacles=Enumerable.Range(0,random.Next(0,25)).Select(_=>new Rect(random.Next(-200,1100),random.Next(-200,900),random.Next(1,300),random.Next(1,220))).ToArray();
   var desired=new Rect(random.Next(-150,1100),random.Next(-150,850),random.Next(20,420),random.Next(20,220));
   bool fits=OperatorOverlayLayout.TryPlace(desired,bounds,obstacles,out actual);
   bool repeat=OperatorOverlayLayout.TryPlace(desired,bounds,obstacles,out var again);
   Check(fits==repeat&&actual==again,"placement changed with identical inputs");
   if(fits){Safe(actual,bounds,obstacles,"random layout "+n);Check(actual.size==desired.size,"placement silently scales widget");}
  }
 }
 static void Matrix(){
  foreach(int count in new[]{1,4,8})foreach(int faces in new[]{4,6})foreach(int pixels in new[]{64,128})foreach(bool horizontal in new[]{true,false}){
   cases++;MainController.Instance=new MainController();
   for(int i=0;i<count;i++)MainController.Instance.SystemConfigs["VR"+(i+1)]=new SystemConfig{displayOrder=faces==4?"RBLF":"DRBLFU",ledPanelWidth=pixels,ledPanelHeight=pixels,startRow=horizontal?i:0,startCol=horizontal?0:i,horizontal=horizontal};
   var blocked=OperatorOverlayLayout.ProtectedRects(0,1920,1080);
   Check(blocked.Count==count*faces,"protected face count differs from rendering allow-list");
   var first=blocked[0];Check(Math.Abs(first.x)<.001&&Math.Abs(first.y-(1080-pixels))<.001&&Math.Abs(first.width-pixels)<.001&&Math.Abs(first.height-pixels)<.001,"LED coordinates do not preserve top-left pixel config");
   for(int slot=0;slot<faces;slot++){var r=blocked[slot];Check(Math.Abs(r.x-(horizontal?slot*pixels:0))<.001&&Math.Abs(r.y-(1080-pixels*(horizontal?1:slot+1)))<.001,"LED horizontal/vertical slots differ");}
   var owners=new[]{"test-status","test-fps","test-overview"};foreach(var owner in owners)OperatorOverlayLayout.Release(owner);
   var widgets=new[]{new Rect(12,12,550,230),new Rect(1598,1010,310,58),new Rect(1120,20,600,400)};
   var occupied=new List<Rect>(blocked);var display=new Rect(0,0,1920,1080);
   for(int i=0;i<widgets.Length;i++)if(OperatorOverlayLayout.Reserve(owners[i],0,widgets[i],out var placed)){
    Safe(placed,display,occupied,"matrix "+cases+" widget "+i);int revision=OperatorOverlayLayout.Revision;
    Check(OperatorOverlayLayout.Reserve(owners[i],0,widgets[i],out var repeated)&&repeated==placed&&OperatorOverlayLayout.Revision==revision,"stable reservation moves or invalidates every frame");occupied.Add(placed);
   }
   Check(OperatorOverlayLayout.ProtectedRects(1,1920,1080).Count==0,"another display incorrectly inherits LED exclusion");
   Check(OperatorOverlayLayout.Reserve("other-display",1,widgets[0],out var separate)&&separate==widgets[0],"display-local reservations collide across outputs");OperatorOverlayLayout.Release("other-display");
   foreach(var owner in owners)OperatorOverlayLayout.Release(owner);
  }
  MainController.Instance=new MainController();
  MainController.Instance.SystemConfigs["VR1"]=new SystemConfig{desktopPreview=true};
  Check(OperatorOverlayLayout.ProtectedRects(0,1920,1080).Count==0,"desktop preview falsely reserves LED panel pixels");
  MainController.Instance.SystemConfigs["VR1"]=new SystemConfig{displayOrder="F",ledPanelWidth=1920,ledPanelHeight=1080};
  Check(!OperatorOverlayLayout.Reserve("test-full",0,new Rect(12,12,100,100),out _),"operator widget covers full LED display");
 }
 static void Visibility(){
  int notifications=0;OperatorOverlayVisibility.Changed+=()=>notifications++;
  OperatorOverlayVisibility.SetHidden(false);Check(notifications==0,"unchanged visible state emits notification");
  OperatorOverlayVisibility.Toggle();Check(OperatorOverlayVisibility.Hidden&&notifications==1&&DisplayFrameClear.requests==2,"Tab hide does not clear each active display once");
  OperatorOverlayVisibility.SetHidden(true);Check(notifications==1&&DisplayFrameClear.requests==2,"repeated hidden state retriggers clear");
  OperatorOverlayVisibility.Toggle();Check(!OperatorOverlayVisibility.Hidden&&notifications==2&&DisplayFrameClear.requests==2,"Tab show incorrectly adds a washout");
 }
 static void Main(){Geometry();Matrix();Visibility();Console.WriteLine("PASS: "+checks+" actual-source Unity.Rect placement/visibility assertions; "+cases+" LED configurations; no Unity process or device launched.");}
}
'''


def method(source, signature):
    start = source.index(signature)
    cursor = source.index('{', start)
    depth = 1
    end = cursor + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


def main():
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    sdk = subprocess.check_output([dotnet, '--list-sdks'], text=True)
    version = max(map(int, re.findall(r'^(\d+)\.', sdk, re.M)))
    engines = list(Path('/Applications/Unity/Hub/Editor').glob('*/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine/UnityEngine.CoreModule.dll'))
    if not engines:
        raise SystemExit('A local Unity editor CoreModule is required for actual managed Rect/Mathf geometry.')
    core = sorted(engines)[-1]
    viewport = (ROOT/'Assets/Scripts/ViewportSetter.cs').read_text()
    viewport_methods = '\n'.join(method(viewport, name) for name in ('public static string NormalizeDisplayOrder', 'public static Rect PanelRect'))
    with tempfile.TemporaryDirectory(prefix='matrex-overlay-layout-') as folder:
        temporary = Path(folder)
        environment = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        (temporary/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (temporary/'Stubs.cs').write_text(STUBS+'\npublic static class ViewportSetter {\n'+viewport_methods+'\n}')
        (temporary/'Program.cs').write_text(PROGRAM)
        (temporary/'OperatorOverlayLayout.cs').write_text('using Screen=HarnessScreen;\nusing Display=HarnessDisplay;\n'+(ROOT/'Assets/Scripts/OperatorOverlayLayout.cs').read_text())
        project = temporary/'Regression.csproj'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="UnityEngine.CoreModule"><HintPath>{escape(str(core))}</HintPath></Reference></ItemGroup></Project>')
        result = subprocess.run([dotnet, 'build', str(project), '--configfile', str(temporary/'NuGet.Config'), '-p:UseSharedCompilation=false', '-v:q'], env=environment, capture_output=True, text=True)
        if result.returncode:
            raise SystemExit(result.stdout+result.stderr)
        subprocess.run([dotnet,str(temporary/f'bin/Debug/net{version}.0/Regression.dll')], env=environment,check=True)


if __name__ == '__main__':
    main()
