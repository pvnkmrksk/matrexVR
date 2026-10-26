#!/usr/bin/env python3
"""Check exact replay native-pixel layout helpers offline; native clicks need a player test."""
from pathlib import Path
import os
import re
import shutil
import subprocess
import tempfile

ROOT=Path(__file__).resolve().parents[1]


def declaration(source,signature):
    start=source.index(signature);brace=source.index('{',start);arrow=source.find('=>',start,brace)
    if arrow>=0:return source[start:source.index(';',arrow)+1]
    depth,end=1,brace+1
    while depth:
        depth+=(source[end]=='{')-(source[end]=='}');end+=1
    return source[start:end]


PROGRAM=r"""
using System;using System.Collections.Generic;
struct Vector2{public float x,y;public Vector2(float a,float b){x=a;y=b;}}
struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero=>new Vector3();public static float Distance(Vector3 a,Vector3 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));}
struct Bounds{public Vector3 min,max;public Bounds(Vector3 center,Vector3 size){min=new Vector3(center.x-size.x/2,center.y-size.y/2,center.z-size.z/2);max=new Vector3(center.x+size.x/2,center.y+size.y/2,center.z+size.z/2);}public void Encapsulate(Vector3 p){min=new Vector3(Math.Min(min.x,p.x),Math.Min(min.y,p.y),Math.Min(min.z,p.z));max=new Vector3(Math.Max(max.x,p.x),Math.Max(max.y,p.y),Math.Max(max.z,p.z));}}
enum EventType{MouseDown,MouseUp,MouseMove,MouseDrag,ScrollWheel,Repaint,Layout,KeyDown,Used}
enum PointerType{Mouse,Touch,Pen}
class Event{public EventType type;public PointerType pointerType;public Vector2 mousePosition,delta;public int button;}
namespace UnityEngine.InputSystem{class PositionControl{public Vector2 value;public Vector2 ReadValue()=>value;}class Mouse{public bool added=true,enabled=true;public PositionControl position=new PositionControl();}}
class LineRenderer{public static int instances;public int positionCount,writes;public Vector3[] positions;public LineRenderer(){instances++;}public void SetPositions(Vector3[] p){writes++;positions=p;}}
class ReplaySessionArchive{public class Sample{public string ContextKey;public Vector3 position;}}
struct Rect {public float x,y,width,height;public Rect(float a,float b,float c,float d){x=a;y=b;width=c;height=d;}public bool Contains(float a,float b)=>a>=x&&a<x+width&&b>=y&&b<y+height;}
static class Mathf{public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);}
class Actual{
 private float maximumInterpolationDistance=50;
 __PRODUCTION__
 public static float Scale(float backing,float width)=>ReplayUiScale(backing,width);
 public static Rect Toolbar(float width,float height,float scale)=>ReplayToolbarRect(width,height,scale);
 public static void Route(Event e,UnityEngine.InputSystem.Mouse m,float height)=>NormalizeReplayPointer(e,m,height);
 public static (int,int) Range(List<ReplaySessionArchive.Sample> samples,int cursor,float jump){ReplayPathRange(samples,cursor,jump,out int start,out int end);return(start,end);}
 public object Path()=>new RecordedPath{line=new LineRenderer()};
 public bool Update(object path,List<ReplaySessionArchive.Sample> samples,int cursor)=>UpdateRecordedPath((RecordedPath)path,samples,cursor);
 public Vector3[] Points(object path)=>((RecordedPath)path).line.positions;
 public int Writes(object path)=>((RecordedPath)path).line.writes;
 public Bounds Bound(object path)=>((RecordedPath)path).bounds;
}
static class Regression{
 static int checks;static void Check(bool yes,string label){checks++;if(!yes)throw new Exception(label);}static bool Near(float a,float b)=>Math.Abs(a-b)<.001;
 static void Main(){
  Check(Near(Actual.Scale(2,3024),2),"Retina backing scale lost");Check(Near(Actual.Scale(1,1920),1),"standard monitor scale changed");Check(Near(Actual.Scale(3,3840),3),"three-times backing scale lost");Check(Near(Actual.Scale(2,1120),1),"minimum-width controls must fit");Check(Actual.Scale(2,0)>0,"startup width must not divide by zero");
  var bar=Actual.Toolbar(3024,1898,2);Check(Near(bar.x,16)&&Near(bar.y,1518.4f)&&Near(bar.width,2992)&&Near(bar.height,363.6f),"Retina toolbar is not in native pixel coordinates");Check(bar.Contains(100,1626),"actual native Play pointer must lie in toolbar");Check(bar.Contains(1852,1698),"actual native timeline pointer must lie in toolbar");Check(!bar.Contains(926,849),"a second pointer scale must not be applied");
  foreach(var screen in new[]{new[]{1920f,1080f,1f},new[]{3024f,1898f,2f},new[]{3840f,2160f,2f},new[]{1280f,720f,2f}}){float scale=Actual.Scale(screen[2],screen[0]);var r=Actual.Toolbar(screen[0],screen[1],scale);Check(screen[0]/scale>=1119.99f,"control row exceeds available width");Check(r.x>=0&&r.y>=0&&r.x+r.width<=screen[0]&&r.y+r.height<=screen[1],"toolbar escaped screen bounds");}
  var mouse=new UnityEngine.InputSystem.Mouse();mouse.position.value=new Vector2(96,273);
  foreach(var type in new[]{EventType.MouseDown,EventType.MouseUp,EventType.MouseMove,EventType.MouseDrag,EventType.ScrollWheel,EventType.Repaint}){var e=new Event{type=type,pointerType=PointerType.Mouse,mousePosition=new Vector2(858,1366),button=1,delta=new Vector2(2,3)};Actual.Route(e,mouse,1898);Check(Near(e.mousePosition.x,96)&&Near(e.mousePosition.y,1625),"actual stale legacy coordinate must use native Input System "+type);Check(e.button==1&&Near(e.delta.x,2)&&Near(e.delta.y,3),"routing must preserve event payload "+type);}
  foreach(var pointer in new[]{PointerType.Touch,PointerType.Pen}){var e=new Event{type=EventType.MouseDown,pointerType=pointer,mousePosition=new Vector2(858,1366)};Actual.Route(e,mouse,1898);Check(Near(e.mousePosition.x,858)&&Near(e.mousePosition.y,1366),"touch/pen must keep their own coordinates");}
  foreach(var type in new[]{EventType.Layout,EventType.KeyDown,EventType.Used}){var e=new Event{type=type,pointerType=PointerType.Mouse,mousePosition=new Vector2(858,1366)};Actual.Route(e,mouse,1898);Check(Near(e.mousePosition.x,858),"non-pointer input must remain untouched");}
  foreach(int device in new[]{0,1,2}){var e=new Event{type=EventType.MouseDown,pointerType=PointerType.Mouse,mousePosition=new Vector2(858,1366)};var m=device==0?null:new UnityEngine.InputSystem.Mouse();if(device==1)m.enabled=false;if(device==2)m.added=false;Actual.Route(e,m,1898);Check(Near(e.mousePosition.x,858),"unavailable mouse must preserve legacy fallback");}Actual.Route(null,mouse,1898);
  var samples=new List<ReplaySessionArchive.Sample>();foreach(var v in new[]{("A",0f),("A",7f),("A",14f),("B",0f),("B",7f),("C",1000f),("C",1007f)})samples.Add(new ReplaySessionArchive.Sample{ContextKey=v.Item1,position=new Vector3(v.Item2,0,0)});
  Check(Actual.Range(samples,1,50)==(0,2),"first condition must not connect to reset");Check(Actual.Range(samples,3,50)==(3,4),"second condition must exclude other worlds");Check(Actual.Range(samples,6,50)==(5,6),"new world must have independent path");Check(Actual.Range(samples,-10,50)==(0,2)&&Actual.Range(samples,100,50)==(5,6),"path cursor must clamp");Check(Actual.Range(null,0,50)==(-1,-1)&&Actual.Range(new List<ReplaySessionArchive.Sample>(),0,50)==(-1,-1),"empty paths must be safe");
  var jumps=new List<ReplaySessionArchive.Sample>();foreach(float x in new[]{0f,7f,200f,207f})jumps.Add(new ReplaySessionArchive.Sample{ContextKey="same",position=new Vector3(x,0,0)});Check(Actual.Range(jumps,0,50)==(0,1)&&Actual.Range(jumps,3,50)==(2,3),"large unlogged jumps must not create false chords");Check(Actual.Range(jumps,2,0)==(0,3),"disabled jump threshold must retain context grouping");
  var actual=new Actual();var path=actual.Path();int allocated=LineRenderer.instances;Check(actual.Update(path,samples,1),"first path must populate");Check(actual.Points(path).Length==3&&Near(actual.Points(path)[2].x,14),"current condition path must contain only its own poses");Check(!actual.Update(path,samples,2)&&actual.Writes(path)==1,"ordinary playback must reuse path cache");Check(actual.Update(path,samples,3)&&actual.Points(path).Length==2&&Near(actual.Bound(path).max.x,7),"new condition must replace path and fit bounds");Check(actual.Update(path,samples,1)&&LineRenderer.instances==allocated,"backward seek must reuse its renderer");
  var longSamples=new List<ReplaySessionArchive.Sample>();for(int i=0;i<7003;i++)longSamples.Add(new ReplaySessionArchive.Sample{ContextKey="long",position=new Vector3(i,0,0)});var longPath=actual.Path();actual.Update(longPath,longSamples,4000);var points=actual.Points(longPath);Check(points.Length<=5000&&Near(points[0].x,0)&&Near(points[points.Length-1].x,7002),"bounded decimation must retain endpoints");Check(Near(actual.Bound(longPath).max.x,7002),"fit bounds must use the complete condition");
  Console.WriteLine("PASS: "+checks+" exact-source native replay layout/pointer/path checks; no Unity/native click claim.");
 }
}
"""


def main():
    source=(ROOT/'Assets/Scripts/Replay/ReplayController.cs').read_text()
    on_gui=source[source.index('private void OnGUI()'):source.index('private static string Clock')]
    assert 'GUI.matrix=Matrix4x4.identity' in on_gui and 'Matrix4x4.TRS' not in on_gui,'Replay controls must use default native coordinates.'
    assert not re.search(r'GUILayout\.(?:Width|MinWidth)\(\d+\)',on_gui),'An unscaled control width remains.'
    assert 'rigMarkerPrefab: {fileID: 0}' in (ROOT/'Assets/Scenes/ReplayScene.unity').read_text(),'Default replay marker must not be the Lemon prefab.'
    production=''.join(declaration(source,signature) for signature in ('private static float ReplayUiScale(','private static Rect ReplayToolbarRect(','private static bool UsesModernMousePosition(','private static Vector2 ReplayMousePosition(','private static void NormalizeReplayPointer(','private static void ReplayPathRange(','private sealed class RecordedPath','private bool UpdateRecordedPath('))
    dotnet=shutil.which('dotnet')
    if not dotnet:raise SystemExit('A local .NET SDK is required.')
    version=max(map(int,re.findall(r'^(\d+)\.',subprocess.check_output([dotnet,'--list-sdks'],text=True),re.M)))
    with tempfile.TemporaryDirectory(prefix='matrex-replay-native-ui-') as temporary:
        folder=Path(temporary);(folder/'Program.cs').write_text(PROGRAM.replace('__PRODUCTION__',production))
        (folder/'Regression.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'+f'<TargetFramework>net{version}.0</TargetFramework></PropertyGroup></Project>')
        (folder/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        env=dict(os.environ,DOTNET_CLI_HOME=str(folder),DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        result=subprocess.run([dotnet,'build',str(folder/'Regression.csproj'),'--configfile',str(folder/'NuGet.Config'),'-p:UseSharedCompilation=false','-v:q'],env=env,capture_output=True,text=True)
        if result.returncode:raise SystemExit(result.stdout+result.stderr)
        subprocess.run([dotnet,str(folder/f'bin/Debug/net{version}.0/Regression.dll')],env=env,check=True)


if __name__=='__main__':main()
