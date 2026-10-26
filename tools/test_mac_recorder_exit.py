#!/usr/bin/env python3
"""Actual Darwin nonchild exit observation through the production managed P/Invoke.

Uses isolated double-fork command-line fixtures only, never a tracker/camera/UI.
"""
from pathlib import Path
import os,platform,re,shutil,subprocess,tempfile
ROOT=Path(__file__).resolve().parents[1]
NATIVE=r'''
#include <unistd.h>
#include <stdlib.h>
#include <stdio.h>
int main(int argc,char **argv){int mode=argc>1?atoi(argv[1]):0;pid_t target=fork();if(target<0)return 1;if(target==0){freopen("/dev/null","w",stdout);freopen("/dev/null","w",stderr);sleep(2);_exit(mode==1?23:0);}printf("%d\n",target);fflush(stdout);return 0;}
'''
PROGRAM=r'''
using System;using System.Diagnostics;using System.Runtime.InteropServices;using System.Threading;
static class Regression {
 [DllImport("/usr/lib/libSystem.B.dylib")]static extern int kill(int pid,int signal);
 static int checks;static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 static void Main(string[] args){
  for(int mode=0;mode<3;mode++){
   int pid;using(var launcher=Process.Start(new ProcessStartInfo{FileName=args[0],Arguments=mode.ToString(),UseShellExecute=false,RedirectStandardOutput=true})){
    pid=int.Parse(launcher.StandardOutput.ReadLine());Check(launcher.WaitForExit(3000)&&launcher.ExitCode==0,"fixture launcher must exit before observation");
   }
   long start;using(var process=Process.GetProcessById(pid))start=process.StartTime.ToUniversalTime().Ticks;
   Check(!MacAppLauncher.TryObserveExit(pid,start+1,_=>throw new Exception("wrong identity observed")),"reject PID reuse/changed start time");
   int status=int.MinValue;using(var done=new ManualResetEventSlim()){
    Check(MacAppLauncher.TryObserveExit(pid,start,value=>{status=value;done.Set();}),"register actual nonchild observer");
    if(mode==2)Check(kill(pid,9)==0,"kill only owned test fixture");
    Check(done.Wait(5000),"receive real detached exit status");
    Check(status==(mode==0?0:mode==1?23<<8:9),"clean/nonzero/signal wait status must stay distinct: "+status);
   }
   Check(!MacAppLauncher.TryObserveExit(pid,start,_=>throw new Exception("retroactive success")),"already exited process must remain unverified");
  }
  Check(!MacAppLauncher.TryObserveExit(0,1,_=>{}),"reject invalid PID");
  Console.WriteLine("PASS: "+checks+" actual production Darwin nonchild exit/identity checks; no tracker, camera, GUI or experiment data touched.");
 }
}
'''
def main():
 if platform.system()!='Darwin':raise SystemExit('This native validation requires macOS.')
 dotnet=shutil.which('dotnet') or '/usr/local/share/dotnet/dotnet';version=max(map(int,re.findall(r'^(\d+)\.',subprocess.check_output([dotnet,'--list-sdks'],text=True),re.M)))
 with tempfile.TemporaryDirectory(prefix='matrex-mac-exit-') as folder:
  temp=Path(folder);(temp/'fixture.c').write_text(NATIVE);subprocess.run(['clang','-Wall','-Wextra',str(temp/'fixture.c'),'-o',str(temp/'fixture')],check=True)
  (temp/'Program.cs').write_text(PROGRAM);shutil.copy2(ROOT/'Assets/Scripts/MacAppLauncher.cs',temp/'MacAppLauncher.cs')
  project=temp/'Regression.csproj';project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup></Project>');(temp/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>');env=dict(os.environ,DOTNET_CLI_HOME=folder,DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
  result=subprocess.run([dotnet,'build',str(project),'--configfile',str(temp/'NuGet.Config'),'-p:UseSharedCompilation=false','-v:q'],env=env,capture_output=True,text=True)
  if result.returncode:raise SystemExit(result.stdout+result.stderr)
  subprocess.run([dotnet,str(temp/f'bin/Debug/net{version}.0/Regression.dll'),str(temp/'fixture')],env=env,check=True)
if __name__=='__main__':main()
