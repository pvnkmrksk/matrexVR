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
using System;
struct Rect {public float x,y,width,height;public Rect(float a,float b,float c,float d){x=a;y=b;width=c;height=d;}public bool Contains(float a,float b)=>a>=x&&a<x+width&&b>=y&&b<y+height;}
static class Mathf{public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);}
static class Actual{
 __PRODUCTION__
 public static float Scale(float backing,float width)=>ReplayUiScale(backing,width);
 public static Rect Toolbar(float width,float height,float scale)=>ReplayToolbarRect(width,height,scale);
}
static class Regression{
 static int checks;static void Check(bool yes,string label){checks++;if(!yes)throw new Exception(label);}static bool Near(float a,float b)=>Math.Abs(a-b)<.001;
 static void Main(){
  Check(Near(Actual.Scale(2,3024),2),"Retina backing scale lost");Check(Near(Actual.Scale(1,1920),1),"standard monitor scale changed");Check(Near(Actual.Scale(3,3840),3),"three-times backing scale lost");Check(Near(Actual.Scale(2,1120),1),"minimum-width controls must fit");Check(Actual.Scale(2,0)>0,"startup width must not divide by zero");
  var bar=Actual.Toolbar(3024,1898,2);Check(Near(bar.x,16)&&Near(bar.y,1518.4f)&&Near(bar.width,2992)&&Near(bar.height,363.6f),"Retina toolbar is not in native pixel coordinates");Check(bar.Contains(100,1626),"actual native Play pointer must lie in toolbar");Check(bar.Contains(1852,1698),"actual native timeline pointer must lie in toolbar");Check(!bar.Contains(926,849),"a second pointer scale must not be applied");
  foreach(var screen in new[]{new[]{1920f,1080f,1f},new[]{3024f,1898f,2f},new[]{3840f,2160f,2f},new[]{1280f,720f,2f}}){float scale=Actual.Scale(screen[2],screen[0]);var r=Actual.Toolbar(screen[0],screen[1],scale);Check(screen[0]/scale>=1119.99f,"control row exceeds available width");Check(r.x>=0&&r.y>=0&&r.x+r.width<=screen[0]&&r.y+r.height<=screen[1],"toolbar escaped screen bounds");}
  Console.WriteLine("PASS: "+checks+" exact-source native replay layout checks; no Unity/native click claim.");
 }
}
"""


def main():
    source=(ROOT/'Assets/Scripts/Replay/ReplayController.cs').read_text()
    on_gui=source[source.index('private void OnGUI()'):source.index('private static string Clock')]
    assert 'GUI.matrix=Matrix4x4.identity' in on_gui and 'Matrix4x4.TRS' not in on_gui,'Replay controls must use default native coordinates.'
    assert not re.search(r'GUILayout\.(?:Width|MinWidth)\(\d+\)',on_gui),'An unscaled control width remains.'
    assert 'rigMarkerPrefab: {fileID: 0}' in (ROOT/'Assets/Scenes/ReplayScene.unity').read_text(),'Default replay marker must not be the Lemon prefab.'
    production=declaration(source,'private static float ReplayUiScale(')+declaration(source,'private static Rect ReplayToolbarRect(')
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
