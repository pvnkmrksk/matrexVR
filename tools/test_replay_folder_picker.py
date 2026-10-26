#!/usr/bin/env python3
"""Compile the full Mac picker source; test dispatch with controlled context/dialog boundaries.

No native panel is opened. Cocoa visibility/ownership still needs a real player
test; this verifies queued main-thread execution, cancellation, faults and reuse.
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
using System;using System.Collections.Concurrent;using System.IO;using System.Reflection;using System.Threading;using System.Threading.Tasks;
namespace UnityEngine{public enum RuntimeInitializeLoadType{BeforeSceneLoad}public class RuntimeInitializeOnLoadMethodAttribute:Attribute{public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType value){}}}
public static class RuntimePaths{public static string RunDataRoot=>"/synthetic/runs";}
sealed class QueuedContext:SynchronizationContext{
 public bool reject;public ConcurrentQueue<Action> queue=new ConcurrentQueue<Action>();
 public override void Post(SendOrPostCallback callback,object state){if(reject)throw new InvalidOperationException("context unavailable");queue.Enqueue(()=>callback(state));}
 public void Pump(){if(!queue.TryDequeue(out var action))throw new Exception("nothing queued");action();}
}
static class Regression{
 const BindingFlags Private=BindingFlags.Static|BindingFlags.NonPublic;static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static Task<string> Dispatch(SynchronizationContext context,Func<string> choose)=>(Task<string>)typeof(ReplayFolderPicker).GetMethod("ChooseOnContext",Private).Invoke(null,new object[]{context,choose});
 static void Main(){
  var context=new QueuedContext();int main=Thread.CurrentThread.ManagedThreadId,calls=0,executed=-1;
  var selected=Dispatch(context,()=>{calls++;executed=Thread.CurrentThread.ManagedThreadId;return "/runs/animal β";});
  Check(calls==0&&!selected.IsCompleted&&context.queue.Count==1,"request must post instead of opening a panel inside OnGUI");
  var continuation=selected.ContinueWith(_=>Thread.CurrentThread.ManagedThreadId,CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
  context.Pump();Check(selected.Result=="/runs/animal β"&&calls==1,"selected Unicode folder must complete once");
  Check(executed==main,"dialog boundary must execute on the captured main context");
  Check(continuation.GetAwaiter().GetResult()!=main,"completion continuations must not run inside the modal return callback");
  var cancel=Dispatch(context,()=>null);context.Pump();Check(cancel.IsCompleted&&!cancel.IsFaulted&&!cancel.IsCanceled&&cancel.Result==null,"native cancellation must complete as an empty selection");
  var cancelled=Dispatch(context,()=>throw new OperationCanceledException());context.Pump();Check(!cancelled.IsFaulted&&!cancelled.IsCanceled&&cancelled.Result==null,"cancel exception must release caller UI as an empty selection");
  var failure=Dispatch(context,()=>throw new IOException("native panel failed"));context.Pump();Check(failure.IsFaulted&&failure.Exception.GetBaseException() is IOException,"native failure must reach the caller rather than leave a pending task");
  var retry=Dispatch(context,()=>"/runs/retry");context.Pump();Check(retry.Result=="/runs/retry","a prior failure must not block another picker");
  var rejected=Dispatch(new QueuedContext{reject=true},()=>{calls++;return "wrong";});Check(rejected.IsFaulted&&calls==1,"Post failure must fault without opening a dialog");
  var absent=Dispatch(null,()=>{calls++;return "wrong";});Check(absent.IsFaulted&&calls==1,"missing main context must fail promptly");
  var created=Task.Factory.StartNew(()=>Dispatch(context,()=>Thread.CurrentThread.ManagedThreadId.ToString()));var background=created.GetAwaiter().GetResult();
  Check(!background.IsCompleted&&context.queue.Count==1,"background caller must still queue to main context");context.Pump();Check(background.Result==main.ToString(),"background request must execute on main thread");
  var beforeCapture=ReplayFolderPicker.ChooseAsync();Check(beforeCapture.IsFaulted,"Mac API must not fall back to an arbitrary background context");
  SynchronizationContext.SetSynchronizationContext(context);typeof(ReplayFolderPicker).GetMethod("CaptureMacMainContext",Private).Invoke(null,null);SynchronizationContext.SetSynchronizationContext(null);
  var first=ReplayFolderPicker.ChooseAsync();var same=ReplayFolderPicker.ChooseAsync();Check(ReferenceEquals(first,same)&&context.queue.Count==1,"concurrent Browse requests must share one pending app-owned panel");
  // Complete the controlled dialog boundary without invoking any Cocoa function.
  typeof(ReplayFolderPicker).GetField("macPickerTask",Private).SetValue(null,Task.FromResult<string>(null));
  var again=ReplayFolderPicker.ChooseAsync();Check(!ReferenceEquals(first,again)&&context.queue.Count==2,"completed/cancelled panel must permit a fresh request");
  Console.WriteLine("PASS: "+checks+" full-source Mac picker dispatch/cancel/fault/reuse checks; native panel and Unity context are controlled boundaries, no dialog/process opened.");
 }
}
'''


def main():
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    sdk = subprocess.check_output([dotnet, '--list-sdks'], text=True)
    version = max(map(int, re.findall(r'^(\d+)\.', sdk, re.M)))
    with tempfile.TemporaryDirectory(prefix='matrex-folder-picker-') as folder:
        temporary = Path(folder)
        env = dict(os.environ, DOTNET_CLI_HOME=folder, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        (temporary/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (temporary/'Program.cs').write_text(PROGRAM)
        project = temporary/'Regression.csproj'
        source = ROOT/'Assets/Scripts/Replay/ReplayFolderPicker.cs'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework><DefineConstants>UNITY_STANDALONE_OSX</DefineConstants></PropertyGroup><ItemGroup><Compile Include="{escape(str(source))}" /></ItemGroup></Project>')
        result = subprocess.run([dotnet,'build',str(project),'--configfile',str(temporary/'NuGet.Config'),'-p:UseSharedCompilation=false','-v:q'],env=env,capture_output=True,text=True)
        if result.returncode:
            raise SystemExit(result.stdout+result.stderr)
        subprocess.run([dotnet,str(temporary/f'bin/Debug/net{version}.0/Regression.dll')],env=env,check=True)


if __name__ == '__main__':
    main()
