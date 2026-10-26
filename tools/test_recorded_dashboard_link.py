#!/usr/bin/env python3
"""Test production replay-link clock/queue/handshake policy with Unity boundaries stubbed.

No Unity, browser or native dashboard is launched. HTTP endpoints and the
browser global-clock adapter are covered in the dashboard repository's suite.
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
using System;using System.IO;using System.Collections;using System.Collections.Generic;using System.Reflection;using System.Threading;using System.Threading.Tasks;using System.Security.Cryptography;using System.Text;using Newtonsoft.Json.Linq;
namespace UnityEngine{public class MonoBehaviour{protected static void DontDestroyOnLoad(object value){}public object StartCoroutine(IEnumerator e)=>null;}public class GameObject{public GameObject(string name){}public T AddComponent<T>() where T:new()=>new T();}public static class Application{public static string persistentDataPath=Path.GetTempPath();public static void OpenURL(string url)=>throw new Exception("No browser should launch");}public static class Debug{public static void LogWarning(object value){}}}
public static class DesktopDashboard{internal static Task<string> PrepareBundledExecutableAsync()=>Task.FromException<string>(new IOException("Fixture does not launch native processes"));}
public static class TrackerProcessManager{public static System.Diagnostics.ProcessStartInfo DetachedStart(string a,IEnumerable<string>b,string c,string d)=>throw new Exception("No native process should launch");}
class Regression{
 static int checks;static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
 static object Get(object o,string field)=>o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 static void Main(){
  var link=new RecordedDashboardLink();link.UpdateClock(12,true,2,10);Check((double)Get(link,"seconds")==10,"clock clamped to run duration");Check((bool)Get(link,"playing")&&(double)Get(link,"rate")==2,"Unity remains authoritative for play/rate");
  link.UpdateClock(-3,false,1000,10);Check((double)Get(link,"seconds")==0&&(double)Get(link,"rate")==64,"negative seek and unsupported rate bounded");long revision=(long)Get(link,"revision");link.UpdateClock(double.NaN,true,1,10);Check((long)Get(link,"revision")==revision,"nonfinite owner clock ignored");link.UpdateClock(1,true,0,10);Check((long)Get(link,"revision")==revision,"zero rate cannot produce invalid protocol state");
  var queue=(Queue<RecordedReplayCommand>)Get(link,"commands");queue.Enqueue(new RecordedReplayCommand("seek",3,"request-a",7));Check(link.TryTakeCommand(out var command)&&command.Kind=="seek"&&command.Seconds==3&&command.RequestId=="request-a","command payload retained");Check((long)Get(link,"ack")==7,"dequeued request acknowledged monotonically");queue.Enqueue(new RecordedReplayCommand("pause",0,"request-b",5));link.TryTakeCommand(out _);Check((long)Get(link,"ack")==7,"ack never moves backwards");Check(!link.TryTakeCommand(out _),"empty queue produces no phantom command");queue.Enqueue(new RecordedReplayCommand("play",0,"request-c",8));link.CloseSession();Check(!link.TryTakeCommand(out _)&&!link.IsReady,"closing session clears commands without touching independent viewer");
  string token=new string('a',32),id;using(var sha=SHA256.Create())id=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(token))).Replace("-","").ToLowerInvariant().Substring(0,16);
  var info=new JObject{["protocol"]="matrex.replay.v1",["version"]=1,["enabled"]=true,["instanceId"]=id};var method=typeof(RecordedDashboardLink).GetMethod("Compatible",BindingFlags.Static|BindingFlags.NonPublic);bool Match(JObject value,string owner)=>(bool)method.Invoke(null,new object[]{value,owner});
  Check(Match(info,token),"matching protocol and owner instance accepted");Check(!Match(info,new string('b',32)),"another owner never reuses unrelated service");info["version"]=0;Check(!Match(info,token),"old binary capability rejected");info["version"]=1;info["enabled"]=false;Check(!Match(info,token),"ordinary analysis/live viewer cannot impersonate replay service");Check(!Match(info,null),"missing persisted owner nonce refused");
  var accept=typeof(RecordedDashboardLink).GetMethod("AcceptResponse",BindingFlags.Instance|BindingFlags.NonPublic);var sessionField=typeof(RecordedDashboardLink).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic);sessionField.SetValue(link,"old-session");var late=new JObject{["commands"]=new JArray(new JObject{["sessionId"]="old-session",["sequence"]=20,["kind"]="seek",["seconds"]=9,["requestId"]="late"})};link.CloseSession();sessionField.SetValue(link,"new-session");Check(!(bool)accept.Invoke(link,new object[]{late,"old-session",CancellationToken.None})&&!link.IsReady&&!link.TryTakeCommand(out _),"late old-session response cannot resurrect ready state or seek new run");using(var canceled=new CancellationTokenSource()){canceled.Cancel();Check(!(bool)accept.Invoke(link,new object[]{late,"new-session",canceled.Token}),"canceled response cannot mutate active link");}var current=new JObject{["commands"]=new JArray(new JObject{["sessionId"]="new-session",["sequence"]=21,["kind"]="seek",["seconds"]=4,["requestId"]="current"})};Check((bool)accept.Invoke(link,new object[]{current,"new-session",CancellationToken.None})&&link.IsReady&&link.TryTakeCommand(out command)&&command.RequestId=="current","new session accepts only its own command after stale completion");Check(typeof(RecordedDashboardLink).GetField("token",BindingFlags.Instance|BindingFlags.NonPublic)==null,"request owner nonce is immutable per pump rather than mutable shared session state");
  Console.WriteLine("PASS: "+checks+" production recorded dashboard clock/command/handshake checks. No Unity/process/network launch.");
 }
}
'''


def main():
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise SystemExit("A local .NET SDK is required.")
    version = max(map(int, re.findall(r"^(\d+)\.", subprocess.check_output([dotnet, "--list-sdks"], text=True), re.M)))
    dll = next((ROOT / "Library/PackageCache").glob("com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll"))
    with tempfile.TemporaryDirectory(prefix="matrex-replay-link-regression-") as folder:
        tmp = Path(folder)
        env = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1")
        (tmp / "NuGet.Config").write_text("<configuration><packageSources><clear /></packageSources></configuration>")
        (tmp / "Program.cs").write_text(PROGRAM)
        sources = ["Assets/Scripts/Replay/RecordedDashboardLink.cs", "Assets/Scripts/Replay/ReplayMediaArchive.cs"]
        includes = "".join(f'<Compile Include="{escape(str(ROOT / path))}"/>' for path in sources)
        (tmp / "Regression.csproj").write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{escape(str(dll.resolve()))}</HintPath></Reference>{includes}</ItemGroup></Project>')
        result = subprocess.run([dotnet, "build", str(tmp / "Regression.csproj"), "--configfile", str(tmp / "NuGet.Config"), "-p:UseSharedCompilation=false", "-v:q"], env=env, capture_output=True, text=True)
        if result.returncode:
            raise SystemExit(result.stdout + result.stderr)
        subprocess.run([dotnet, str(tmp / f"bin/Debug/net{version}.0/Regression.dll")], env=env, check=True)


if __name__ == "__main__":
    main()
