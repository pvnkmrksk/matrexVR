#!/usr/bin/env python3
"""Actual Unity transport+telemetry probe in a new tiny project. Requires Unity, pyzmq and a built HejjéTrac checkout. No cameras/source config writes."""
import argparse,hashlib,json,os,re,shutil,socket,subprocess,tempfile,threading,time
from pathlib import Path
import zmq
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--unity',type=Path,required=True);p.add_argument('--hejjetrac-repo',type=Path,required=True);p.add_argument('--workdir',type=Path);p.add_argument('--report',type=Path);p.add_argument('--rekkefly-bin',type=Path);p.add_argument('--rekkefly-config',type=Path);args=p.parse_args()
repo=Path(__file__).resolve().parents[1];hej=args.hejjetrac_repo.resolve();project=args.workdir or Path(tempfile.mkdtemp(prefix='matrex-unity-wire-'))
if project.exists() and any(project.iterdir()) and not (project/'MATREX_WIRE_PROBE').exists():raise SystemExit('Use a new empty workdir; existing project is never modified.')
for name in ['Assets/Editor','Assets/Plugins','Packages','ProjectSettings']:(project/name).mkdir(parents=True,exist_ok=True)
(project/'MATREX_WIRE_PROBE').write_text('Disposable isolated transport probe\n');(project/'Packages/manifest.json').write_text('{"dependencies":{"com.unity.modules.jsonserialize":"1.0.0","com.unity.modules.terrain":"1.0.0"}}\n');shutil.copy2(repo/'ProjectSettings/ProjectVersion.txt',project/'ProjectSettings/ProjectVersion.txt')
for name in ['NetMQ','AsyncIO','NaCl']:shutil.copy2(repo/f'Assets/Plugins/{name}.dll',project/f'Assets/Plugins/{name}.dll')
shutil.copy2(next((repo/'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll')),project/'Assets/Plugins/Newtonsoft.Json.dll')
for name in ['ZmqListener','TrackerBackend','TrackerWireDecoder','ExperimentTelemetry','KineflyAutoTrim']:shutil.copy2(repo/f'Assets/Scripts/{name}.cs',project/f'Assets/{name}.cs')
shutil.copy2(repo/'Assets/Scripts/Loggers/AnimalMetadata.cs',project/'Assets/AnimalMetadata.cs')
for name in ['NativeWireProbe.cs','HeartbeatProbe.cs']:shutil.copy2(repo/'tools/native_tracker_probe'/name,project/'Assets/Editor'/name)
shutil.copy2(repo/'tools/native_tracker_probe/Stubs.cs',project/'Assets/Stubs.cs')
main=(repo/'Assets/Scripts/MainController.cs').read_text();enum=re.search(r'\[JsonConverter\(typeof\(TrackerBackendConverter\)\)\]\s*public enum ClosedLoopMode[^}]+}',main)[0];model=re.search(r'public class SystemConfig\s*\{[^}]+}',main)[0];(project/'Assets/Models.cs').write_text('using Newtonsoft.Json;\n'+enum+'\n'+model)
def editor(method,log):return subprocess.Popen([str(args.unity),'-batchmode','-nographics','-projectPath',str(project),'-executeMethod',method,'-logFile',str(project/log)])
def ready(name,proc):
 for _ in range(600):
  if (project/name).exists():return
  if proc.poll() is not None:raise RuntimeError(f'Editor exited before {name}: {proc.returncode}; see {project}')
  time.sleep(.5)
 raise RuntimeError('Editor ready timeout')
for name in ['ready','publishers-done','heartbeat-ready','report.json','heartbeat-native.json','rekkefly-done','rekkefly-received.json']:(project/name).unlink(missing_ok=True)
ctx=zmq.Context();pub=ctx.socket(zmq.PUB);pub.setsockopt(zmq.LINGER,0);port=pub.bind_to_random_port('tcp://127.0.0.1');(project/'port').write_text(str(port));proc=editor('NativeWireProbe.Run','unity-wire.log')
try:
 ready('ready',proc);time.sleep(.5)
 for i in range(100):
  payload=json.dumps(dict(x=.3,y=.1,yaw=.2))
  if i<25:pub.send_multipart([b'tracker',payload.encode()])
  elif i<50:pub.send_string('separate-legacy-topic');pub.send_string(payload)
  else:pub.send_string(payload)
  time.sleep(.01)
 pub.close();ctx.term();time.sleep(.3)
 rekkefly=None
 if args.rekkefly_bin:
  if not args.rekkefly_config:raise ValueError('--rekkefly-config is required with --rekkefly-bin')
  cfg=project/'rekkefly.toml';config=args.rekkefly_config.read_text().replace('autosave = true','autosave = false');config=re.sub(r'(\[output\]\s*(?:#[^\n]*\n)*)enabled = true',r'\1enabled = false',config);cfg.write_text(config)
  rctx=zmq.Context();rsub=rctx.socket(zmq.SUB);rsub.setsockopt(zmq.LINGER,0);rsub.subscribe(b'');rsub.connect(f'tcp://127.0.0.1:{port}');rpackets=[];rdone=threading.Event()
  def receive_rekkefly():
   while not rdone.is_set():
    if rsub.poll(25):rpackets.append(json.loads(rsub.recv()))
  rt=threading.Thread(target=receive_rekkefly);rt.start()
  rk=subprocess.Popen([str(args.rekkefly_bin),'--config',str(cfg),'--label','VR10','--zmq-port',str(port),'--headless','--supervised','--frames','400','--json'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
  try:
   rk.wait(timeout=25);stats=json.loads(rk.stdout.read());assert rk.returncode==0 and stats['frames']==400,stats
  finally:
   if rk.poll() is None:rk.terminate();rk.wait(timeout=5)
   rk.stdin.close();rdone.set();rt.join();rsub.close();rctx.term()
  (project/'rekkefly-done').write_text('done')
  for _ in range(200):
   if (project/'rekkefly-received.json').exists():break
   time.sleep(.01)
  observed=json.loads((project/'rekkefly-received.json').read_text());assert observed['sequence']>=450 and len(rpackets)>=350,(observed,len(rpackets))
  assert all(abs(v['yaw']-(v['x']-v['y']))<1e-9 for v in rpackets)
  rekkefly=dict(stats=stats,unityReceived=observed['sequence']-100,libzmqReceived=len(rpackets),sample=observed,binarySha256=hashlib.sha256(args.rekkefly_bin.read_bytes()).hexdigest(),configFixture='Unmodified marker/detector/source values from supplied demo config; temporary copy disables UDP/autosave; no cameras/source config writes')
 agent=subprocess.Popen([str(hej/'target/release/fictrac'),'agent'],cwd=hej,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,bufsize=1);trace=[]
 def request(command,session=None,**values):
  value=dict(id=len(trace)+1,command=command,args=values)
  if session is not None:value['session']=session
  agent.stdin.write(json.dumps(value)+'\n');agent.stdin.flush();response=json.loads(agent.stdout.readline());trace.append(response)
  if not response.get('ok'):raise RuntimeError(response)
  return response['result']
 try:
  text=(hej/'examples/benchmark/config.txt').read_text()+f'\nsock_port : -1\nzmq_host : 127.0.0.1\nzmq_port : {port}\n';opened=request('open',text=text,input=str(hej/'examples/sample.mp4'));session=opened['session'];time.sleep(1.5);request('run',session,frames=300);publisher=request('status',session);time.sleep(.3);request('close',session);agent.stdin.close();agent.wait(timeout=10)
 finally:
  if agent.poll() is None:agent.terminate();agent.wait(timeout=10)
 (project/'publishers-done').write_text('done');proc.wait(timeout=180)
 if proc.returncode:raise RuntimeError(f'Unity input probe failed; see {project}')
finally:
 if proc.poll() is None:proc.terminate();proc.wait(timeout=20)
 try:pub.close();ctx.term()
 except Exception:pass
# A modern external subscriber observes actual production publisher output/timers.
reservation=socket.socket();reservation.bind(('127.0.0.1',0));telemetry_port=reservation.getsockname()[1];reservation.close();(project/'telemetry-port').write_text(str(telemetry_port))
ctx=zmq.Context();sub=ctx.socket(zmq.SUB);sub.setsockopt(zmq.LINGER,0);sub.subscribe(b'matrex.telemetry.v2');sub.connect(f'tcp://127.0.0.1:{telemetry_port}');packets=[];done=threading.Event()
def receive():
 while not done.is_set():
  if sub.poll(25):
   frames=sub.recv_multipart();packets.append((time.monotonic(),frames[0].decode(),json.loads(frames[1])))
thread=threading.Thread(target=receive);thread.start();proc=editor('HeartbeatProbe.Run','unity-heartbeat.log')
try:ready('heartbeat-ready',proc);proc.wait(timeout=120)
finally:
 done.set();thread.join();sub.close();ctx.term()
 if proc.poll() is None:proc.terminate();proc.wait(timeout=20)
(project/'telemetry-packets.json').write_text(json.dumps(packets,indent=2,ensure_ascii=False))
contexts=[v for _,t,v in packets if t=='matrex.telemetry.v2.context'];snapshots=[v for _,t,v in packets if t=='matrex.telemetry.v2']
assert proc.returncode==0 and len(contexts)>=8 and len(snapshots)==1,(proc.returncode,len(contexts),len(snapshots))
for v in contexts:
 assert v['schemaVersion']==1 and v['kind']=='matrex.context' and v['animals'][0]['animalId']=='animal β' and v['animals'][0]['speciesName']=='Agrotis infusa' and v['animals'][0]['backend']=='HejjéTrac'
 assert v['experiment']['configFile']=='experiment.json' and v['experiment']['design']=='design.csv'
 assert '/private/' not in json.dumps(v) and '/Users/' not in json.dumps(v) and 'Private operator' not in json.dumps(v)
assert snapshots[0]['schemaVersion']==2
report=json.loads((project/'report.json').read_text());report.update(platform=os.uname().sysname,fixture='Production Unity listener,25 topic+JSON +25 separate topic/JSON +50 singleJSON +300 actual HejjéTrac FT rows',publisherStatus=publisher['outputs'],rekkefly=rekkefly,heartbeat=dict(json.loads((project/'heartbeat-native.json').read_text()),receivedContexts=len(contexts),receivedSnapshots=len(snapshots),firstContext=contexts[0]),hejjetracBinarySha256=hashlib.sha256((hej/'target/release/fictrac').read_bytes()).hexdigest(),transport=json.loads((repo/'Assets/Plugins/tracker-transport-manifest.json').read_text()),scope='Native no-graphics Editor probe on this machine; no camera or other-platform runtime claim. Production consumers/timers/publisher copied unchanged; Main/rig state uses explicit fixture stubs.')
result=json.dumps(report,indent=2,ensure_ascii=False)+'\n';(project/'combined-report.json').write_text(result)
if args.report:args.report.write_text(result)
print(result)
