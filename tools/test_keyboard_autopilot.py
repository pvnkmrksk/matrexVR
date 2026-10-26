#!/usr/bin/env python3
"""Test exact production Keyboard.cs control flow with controlled Unity/input stubs.

This verifies manual-input/autopilot separation and arithmetic at the input and
transform boundary. It does not launch Unity, reproduce Unity quaternion physics,
validate terrain constraints, or establish physical tracking accuracy.
"""
from pathlib import Path
from xml.sax.saxutils import escape
import os
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
PROGRAM = r'''
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace UnityEngine {
 public class SerializeField:Attribute{} public class DefaultExecutionOrder:Attribute{public DefaultExecutionOrder(int n){}}
 public enum Space{World,Self}
 public class MonoBehaviour {public Transform transform=new Transform();public object gameObject;public T GetComponent<T>() where T:class{return null;} public static T[] FindObjectsOfType<T>(bool x){return Array.Empty<T>();}}
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}public static Vector2 zero=>new Vector2();}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b); public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}
 public struct Quaternion {public Vector3 e;public static Quaternion Euler(float x,float y,float z)=>new Quaternion{e=new Vector3(x,y,z)};public static Quaternion Inverse(Quaternion a)=>new Quaternion{e=a.e*-1};public static Quaternion operator*(Quaternion a,Quaternion b)=>new Quaternion{e=a.e+b.e};public static Vector3 operator*(Quaternion q,Vector3 v){float y=q.e.y*(float)Math.PI/180;return new Vector3(v.x*(float)Math.Cos(y)+v.z*(float)Math.Sin(y),v.y,v.z*(float)Math.Cos(y)-v.x*(float)Math.Sin(y));}public static float Angle(Quaternion a,Quaternion b)=>Math.Abs(a.e.x-b.e.x)+Math.Abs(a.e.y-b.e.y)+Math.Abs(a.e.z-b.e.z);}
 public class Transform {public Vector3 position;public Quaternion rotation;public Vector3 eulerAngles=>rotation.e;public void Translate(Vector3 v,Space s){position+=s==Space.Self?rotation*v:v;}public void Rotate(Vector3 v,Space s){rotation=new Quaternion{e=rotation.e+v};}}
 public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static float Clamp(float a,float b,float c)=>Math.Clamp(a,b,c);}
 public static class Time{public static float deltaTime=.5f;}
}
public class MainController{public static MainController Instance;public static event Action SystemConfigurationChanged;public bool TryGetSystemConfigForGameObject(object g,out SystemConfig c){c=null;return false;}}
public class SystemConfig{public ManualControlConfig manualControls;}
public class RugosityRig{public bool ControlsPose;}
public class ClosedLoop{public void ApplyManualRotation(Quaternion q){}}
public static class ExperimentInput {
 public static bool IsEditingText,ControlHeld;public static Vector2 Move;public static HashSet<string> pressed=new HashSet<string>(),held=new HashSet<string>();public static Dictionary<string,float> axis=new Dictionary<string,float>();
 public static bool Pressed(string k)=>pressed.Contains(k);public static bool Held(string k)=>held.Contains(k);public static float Axis(string k)=>axis.TryGetValue(k,out var v)?v:0;
 public static void Reset(){IsEditingText=ControlHeld=false;Move=Vector2.zero;pressed.Clear();held.Clear();axis.Clear();}
}
class Regression {
 static int checks;static void Check(bool b,string label){checks++;if(!b)throw new Exception(label);}
 static void Set(Keyboard k,string name,object value)=>typeof(Keyboard).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(k,value);
 static Keyboard Rig(bool manual,bool auto){ExperimentInput.Reset();var k=new Keyboard();Set(k,"manualEnabled",manual);Set(k,"allowVerticalTranslation",true);Set(k,"allowPitchAndRoll",true);k.SetAutopilotSpeed(7);k.SetAutopilotMode(auto);return k;}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.00001f;
 static void At(Keyboard k,float x,float y,float z,string label)=>Check(Near(k.transform.position.x,x)&&Near(k.transform.position.y,y)&&Near(k.transform.position.z,z),label);
 static void HumanInput(){ExperimentInput.Move=new Vector2(1,1);ExperimentInput.axis["Yaw"]=1;ExperimentInput.axis["Vertical"]=1;ExperimentInput.axis["Pitch"]=1;ExperimentInput.axis["Roll"]=1;}
 static void Main(){
  var k=Rig(false,true);HumanInput();ExperimentInput.ControlHeld=true;ExperimentInput.pressed.Add("Pause");ExperimentInput.held.Add("SpeedModifier");ExperimentInput.pressed.Add("SpeedUp");k.ApplyInput(.5f);At(k,0,0,3.5f,"disabled manual must retain configured7u/s autopilot");Check(k.AutopilotEnabled,"disabled manual toggled autopilot");Check(Quaternion.Angle(k.transform.rotation,new Quaternion())==0,"disabled manual rotated from human input");
  k=Rig(false,false);HumanInput();k.ApplyInput(1);At(k,0,0,0,"disabled manual and autopilot off must not move");Check(Quaternion.Angle(k.transform.rotation,new Quaternion())==0,"disabled manual/off rotated");
  k=Rig(true,true);HumanInput();ExperimentInput.IsEditingText=true;k.ApplyInput(.5f);At(k,0,0,3.5f,"focused text must not stop experiment autopilot");Check(Quaternion.Angle(k.transform.rotation,new Quaternion())==0,"focused text rotated from human input");
  k=Rig(true,false);HumanInput();ExperimentInput.IsEditingText=true;k.ApplyInput(.5f);At(k,0,0,0,"focused text leaked manual movement");
  k=Rig(true,false);ExperimentInput.Move=new Vector2(1,1);ExperimentInput.axis["Yaw"]=.5f;k.ApplyInput(.5f);At(k,5,0,5,"normal manual movement changed");Check(Near(k.transform.rotation.e.y,2.5f),"normal manual yaw changed");
  k=Rig(true,true);ExperimentInput.Move=new Vector2(1,1);k.ApplyInput(.5f);At(k,5,0,3.5f,"autopilot with lateral input and positive-forward input changed");
  k=Rig(true,true);ExperimentInput.Move=new Vector2(0,-1);k.ApplyInput(.5f);At(k,0,0,-1.5f,"manual reverse with autopilot changed");
  k=Rig(true,true);HumanInput();ExperimentInput.held.Add("SpeedModifier");ExperimentInput.pressed.Add("SpeedUp");k.ApplyInput(.5f);At(k,0,0,0,"speed modifier must retain no simultaneous movement");Check(Quaternion.Angle(k.transform.rotation,new Quaternion())==0,"speed adjustment rotated");k.SetAutopilotMode(false);ExperimentInput.Reset();ExperimentInput.Move=new Vector2(0,1);k.ApplyInput(.5f);At(k,0,0,5.5f,"manual speed modifier no longer increments speed by1");
  k=Rig(true,false);ExperimentInput.ControlHeld=true;ExperimentInput.pressed.Add("Pause");k.ApplyInput(.5f);Check(k.AutopilotEnabled,"normal CtrlSpace toggle broken");At(k,0,0,3.5f,"normal CtrlSpace autopilot motion broken");
  Console.WriteLine("PASS: "+checks+" exact Keyboard.cs regression checks. Controlled Unity/input stubs; no Unity or native process launched.");
 }
}
'''


def main():
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    sdk = subprocess.check_output([dotnet, '--list-sdks'], text=True)
    version = max(map(int, re.findall(r'^(\d+)\.', sdk, re.M)))
    with tempfile.TemporaryDirectory(prefix='matrex-keyboard-regression-') as folder:
        temporary = Path(folder)
        env = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        (temporary/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (temporary/'Program.cs').write_text(PROGRAM)
        source = ROOT/'Assets/Scripts/Keyboard.cs'
        project = temporary/'Regression.csproj'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include="{escape(str(source))}" /></ItemGroup></Project>')
        result = subprocess.run([dotnet, 'build', str(project), '--configfile', str(temporary/'NuGet.Config'), '-p:UseSharedCompilation=false', '-v:q'], env=env, capture_output=True, text=True)
        if result.returncode:
            raise SystemExit(result.stdout+result.stderr)
        subprocess.run([dotnet, str(temporary/f'bin/Debug/net{version}.0/Regression.dll')], env=env, check=True)


if __name__ == '__main__':
    main()
