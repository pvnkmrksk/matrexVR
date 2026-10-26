#!/usr/bin/env python3
"""Exercise exact production logger startup/close methods against real temporary files.

Unity object lookup is substituted; no player, camera or hardware is launched.
"""
from pathlib import Path
from test_replay_scene_isolation import declaration
import os,re,shutil,subprocess,tempfile
ROOT=Path(__file__).resolve().parents[1]
PRELUDE=r'''
using System;using System.Text;using System.IO;using System.IO.Compression;using System.Collections;using System.Collections.Generic;using UnityEngine;
namespace UnityEngine {
 public class MonoBehaviour { public GameObject gameObject=new GameObject();public Dictionary<Type,object> components=new Dictionary<Type,object>();protected T FindObjectOfType<T>() where T:class=>MasterDataLogger.Instance as T;protected T GetComponent<T>() where T:class=>components.TryGetValue(typeof(T),out var c)?c as T:null;protected void StartCoroutine(IEnumerator routine){} }
 public class GameObject {public string name="VR1";}public static class Debug {public static void Log(object o){}public static void LogError(object o){}public static void LogWarning(object o){}}
 public struct LayerMask {public int value;}public static class Application {public static string persistentDataPath,temporaryCachePath;}
}
namespace UnityEngine.SceneManagement {public struct Scene {public string name;}public static class SceneManager {public static Scene GetActiveScene()=>new Scene{name="Choice"};}}
public class MasterDataLogger {public static MasterDataLogger Instance;public bool AcceptingWrites;public string directoryPath,timestamp="session";}
public class MainController {}public class ZmqListener {}public static class Debugger {public static void Log(string s,int i){}}
public class BandSpawner {public GameObject instancePrefab=new GameObject();public int numberOfInstances=1;public float spawnLengthX,spawnLengthZ,mu,kappa,speed,visibleOffDuration,visibleOnDuration,boundaryLengthX,boundaryLengthZ,rotationAngle;public string gridType;}
public class Kannadi {public int numberOfRings=1;public float hexRadius;}
public class LocustSpawner {public int numberOfLocusts=1;public float spawnAreaSize,mu,kappa,locustSpeed,locustSpeedStdDev,kappaElevation;public string directionDistribution;}
public static class RuntimeRigRegistry {public static int ParseIndex(string n)=>1;public static string LayerLabel(int i)=>"VR1";}
public class AsyncSwarmWriter {public static int Opened;public bool Completed;public AsyncSwarmWriter(string path,string header){Opened++;File.WriteAllText(path,header);}public void Complete(){Completed=true;}}
'''
SHIMS={
'DataLogger':r'''public class DataLogger:MonoBehaviour {protected string directoryPath,line;protected string logPath {get;private set;}protected StreamWriter logFile;protected bool logInitialized;private bool closedForArchive;protected List<string> bufferedLines;protected bool isLogging,isBuffering,isFirstLine;protected ZmqListener zmq;protected MainController mainController;public string PathWritten=>logPath;public void Begin()=>Start();public void Append()=>LogData("\npose");private void AddColumns(params string[] headers){}protected virtual string BuildHeader()=>"Time,X,Y";private IEnumerator FlushBufferedLinesRoutine(){yield break;}''',
'BandLogger':r'''public class BandLogger:MonoBehaviour {public LayerMask targetLayerMask;private string directoryPath,logPath;private StreamWriter logFile;private BandSpawner bandSpawner;private bool closedForArchive;public void Begin()=>Start();private string GetLayerNames(LayerMask m)=>"VR1";private string GetUniqueFileName(string d,string a,string b)=>a+Guid.NewGuid().ToString("N")+b;private void LogAllInstances(){}''',
'KannadiLogger':r'''public class KannadiLogger:MonoBehaviour {private string directoryPath,logPath;private StreamWriter logFile;private Kannadi kannadi;private int vrIndex;private bool closedForArchive;public void Begin()=>Start();''',
'SwarmLogger':r'''public class SwarmLogger:MonoBehaviour {public LayerMask locustLayerMask;private LocustSpawner spawner;private AsyncSwarmWriter writer;private bool closed;public string LogPath {get;private set;}public void Begin()=>Start();'''
}
TESTS=r'''
static class Regression {
 static int checks;static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 static int Files(string path)=>Directory.GetFiles(path).Length;
 static void Main(){var root=Path.Combine(Path.GetTempPath(),"recording-seal-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
  Application.persistentDataPath=Path.Combine(root,"persistent");Application.temporaryCachePath=Path.Combine(root,"cache");
  var a=Path.Combine(root,"run-a");var b=Path.Combine(root,"run-b");Directory.CreateDirectory(a);Directory.CreateDirectory(b);
  MasterDataLogger.Instance=new MasterDataLogger{AcceptingWrites=true,directoryPath=a};
  var data=new DataLogger();data.Begin();Check(Files(a)==1,"fresh pose logger must create a file");data.Append();data.Append();data.CloseForArchive();string original=File.ReadAllText(data.PathWritten);Check(original.Contains("pose"),"closing must drain buffered real poses");
  data.InitLog();data.Begin();data.Append();data.CloseForArchive();Check(File.ReadAllText(data.PathWritten)==original,"closed pose logger rewrote its finalized file");
  var inactive=new DataLogger();inactive.gameObject.name="Deferred";inactive.CloseForArchive();inactive.Begin();inactive.InitLog();Check(Files(a)==1,"inactive logger closed before Start reopened a file");
  MasterDataLogger.Instance.AcceptingWrites=false;var late=new DataLogger();late.gameObject.name="Late";late.Begin();var lateDirect=new DataLogger();lateDirect.gameObject.name="LateDirect";lateDirect.InitLog();Check(Files(a)==1,"new pose component after finalization reopened the sealed session");
  foreach(bool sealedSession in new[]{true,false}){
   MasterDataLogger.Instance.AcceptingWrites=!sealedSession;
   var band=new BandLogger();band.components[typeof(BandSpawner)]=new BandSpawner();var mirror=new KannadiLogger();mirror.components[typeof(Kannadi)]=new Kannadi();var swarm=new SwarmLogger();swarm.components[typeof(LocustSpawner)]=new LocustSpawner();
   if(!sealedSession){band.CloseForArchive();mirror.CloseForArchive();swarm.CloseForArchive();}
   int before=Files(a),writers=AsyncSwarmWriter.Opened;band.Begin();mirror.Begin();swarm.Begin();Check(Files(a)==before&&AsyncSwarmWriter.Opened==writers,"late population Start created files/async writers (sealed="+sealedSession+")");
  }
  MasterDataLogger.Instance.AcceptingWrites=true;MasterDataLogger.Instance.directoryPath=b;data.Begin();data.InitLog();Check(Files(b)==0,"closed old logger followed a new session");
  var next=new DataLogger();next.Begin();next.CloseForArchive();Check(Files(b)==1,"fresh session logger failed to open");
  // Normal scene reload uses new component instances while the same session remains open.
  var trial=new DataLogger();trial.Begin();trial.Append();trial.Append();trial.CloseForArchive();Check(File.ReadAllText(trial.PathWritten).Contains("pose"),"trial reload could no longer append recorded rows");
  var newBand=new BandLogger();newBand.components[typeof(BandSpawner)]=new BandSpawner();var newMirror=new KannadiLogger();newMirror.components[typeof(Kannadi)]=new Kannadi();var newSwarm=new SwarmLogger();newSwarm.components[typeof(LocustSpawner)]=new LocustSpawner();int count=Files(b);newBand.Begin();newMirror.Begin();newSwarm.Begin();newBand.CloseForArchive();newMirror.CloseForArchive();newSwarm.CloseForArchive();Check(Files(b)==count+3,"new active population loggers stopped recording");
  var sky=new NightSkyArchive();Check(sky.Resolve()==Path.Combine(b,"Skyboxes"),"open sky must archive exact stimulus to run");MasterDataLogger.Instance.AcceptingWrites=false;string cache=sky.Resolve();Check(cache.StartsWith(Application.temporaryCachePath)&&!cache.StartsWith(b),"sealed sky selected a run path");
  MasterDataLogger.Instance.AcceptingWrites=true;sky.Resolve();MasterDataLogger.Instance.AcceptingWrites=false;sky.Recheck();Check(sky.Directory.StartsWith(Application.temporaryCachePath),"sky with cached path reopened sealed run");MasterDataLogger.Instance.AcceptingWrites=true;sky.Resolve();MasterDataLogger.Instance.directoryPath=a;sky.Recheck();Check(sky.Directory.StartsWith(Application.temporaryCachePath),"old sky leaked into rotated run");MasterDataLogger.Instance=null;Check(new NightSkyArchive().Resolve().StartsWith(Application.persistentDataPath),"direct scene audit trail lost");
  Console.WriteLine("PASS: "+checks+" production logger seal/start/drain/rotation/sky-path checks with real temporary files; no Unity/native acquisition.");
 }finally{Directory.Delete(root,true);}}
}
'''
def main():
 master=(ROOT/'Assets/Scripts/Loggers/MasterDataLogger.cs').read_text()
 finalize=declaration(master,'public Task<JObject> FinalizeSessionAsync(')
 assert finalize.index('AcceptingWrites = false')<finalize.index('CloseWriters()'),'Master must close the session gate before writer discovery'
 assert 'AcceptingWrites = true' in declaration(master,'private void BeginRecordingEvidence()')
 assert 'AcceptingWrites = false' in declaration(master,'private List<string> CloseWriters()')
 source=PRELUDE
 paths={'DataLogger':'Assets/Scripts/Loggers/DataLogger.cs','BandLogger':'Assets/Scripts/BandLogger.cs','KannadiLogger':'Assets/Scripts/Swarm/KannadiLogger.cs','SwarmLogger':'Assets/Scripts/Swarm/SwarmLogger.cs'}
 for name,path in paths.items():
  s=(ROOT/path).read_text();source+=SHIMS[name]
  start='protected virtual void Start()' if name=='DataLogger' else 'private void Start()' if name=='SwarmLogger' else 'void Start()'
  source+=declaration(s,start)+declaration(s,'public void CloseForArchive()')
  if name=='DataLogger':source+=declaration(s,'public virtual void InitLog()')+declaration(s,'protected virtual void LogData(')+declaration(s,'void FlushBufferedLinesImmediate()')+declaration(s,'void WriteLogLine(')
  source+='}\n'
 sky=(ROOT/'Assets/Scripts/NightSky/NightSkyController.cs').read_text()
 guard=sky[sky.index('        var recorder = MasterDataLogger.Instance;',sky.index('private void Commit(')):sky.index('        Directory.CreateDirectory(archiveDirectory);',sky.index('private void Commit('))]
 source+='public class NightSkyArchive {private string archiveDirectory,archiveSessionDirectory;public string Directory=>archiveDirectory;public string Resolve()=>archiveDirectory=ResolveArchiveDirectory();public void Recheck(){'+guard+'}\n'+declaration(sky,'private string ResolveArchiveDirectory(')+'}\n'+TESTS
 dotnet=shutil.which('dotnet') or '/usr/local/share/dotnet/dotnet';version=max(map(int,re.findall(r'^(\d+)\.',subprocess.check_output([dotnet,'--list-sdks'],text=True),re.M)))
 with tempfile.TemporaryDirectory(prefix='matrex-seal-test-') as folder:
  temp=Path(folder);(temp/'Program.cs').write_text(source);(temp/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>');project=temp/'Regression.csproj';project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup></Project>');env=dict(os.environ,DOTNET_CLI_HOME=folder,DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
  result=subprocess.run([dotnet,'build',str(project),'--configfile',str(temp/'NuGet.Config'),'-p:UseSharedCompilation=false','-v:q'],env=env,capture_output=True,text=True)
  if result.returncode:raise SystemExit(result.stdout+result.stderr)
  subprocess.run([dotnet,str(temp/f'bin/Debug/net{version}.0/Regression.dll')],env=env,check=True)
if __name__=='__main__':main()
