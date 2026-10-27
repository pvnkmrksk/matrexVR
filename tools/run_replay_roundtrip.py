#!/usr/bin/env python3
"""Run actual Unity record/replay comparison in a marked disposable project."""
import argparse
import json
import math
import os
from pathlib import Path
import subprocess
import threading
import time

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--project',type=Path,required=True)
p.add_argument('--output',type=Path,required=True)
p.add_argument('--port',type=int,default=29921)
p.add_argument('--backend',choices=['Rekkefly','Hejjetrac','Kinefly','FicTrac'],default='Rekkefly')
p.add_argument('--external-publisher',action='store_true',help='Use the already-running native tracker PUB instead of the labelled synthetic fixture.')
p.add_argument('--unity',default='/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity')
args=p.parse_args();args.output.mkdir(parents=True,exist_ok=False)
stop=threading.Event();failure=[]
def publish():
 context=zmq.Context();pub=context.socket(zmq.PUB);pub.linger=0
 try:
  pub.bind(f'tcp://127.0.0.1:{args.port}');start=time.monotonic()
  while not stop.is_set():
   t=time.monotonic()-start;yaw=.1*math.sin(t*.6)
   pub.send_json({'x':.7+yaw/2,'y':.7-yaw/2,'z':0,'pitch':0,'yaw':yaw,'roll':0})
   stop.wait(1/120)
 except BaseException as error:failure.append(str(error))
 finally:pub.close();context.term()
thread=None
if not args.external_publisher:
 import zmq
 thread=threading.Thread(target=publish);thread.start();time.sleep(.1)
 if failure:raise SystemExit(failure[0])
command=[args.unity,'-batchmode','-projectPath',str(args.project.resolve()),'-executeMethod','ReplayRoundtripValidation.Run','-replay-output',str(args.output.resolve()),'-replay-port',str(args.port),'-replay-backend',args.backend,'-replay-require-motion','false' if args.external_publisher else 'true','--data-dir',str((args.output/'RunData').resolve()),'-logFile',str((args.output/'unity.log').resolve())]
try:
 process=subprocess.Popen(command)
 try:returncode=process.wait(timeout=240)
 except subprocess.TimeoutExpired:process.terminate();process.wait(timeout=15);raise
 result=json.loads((args.output/'results.json').read_text())
 result['publisher']='external native tracker' if args.external_publisher else 'Explicit synthetic native-format JSON PUB at120Hz (no camera claim)'
 (args.output/'results.json').write_text(json.dumps(result,indent=2)+'\n')
 print(json.dumps({k:v for k,v in result.items() if k not in ['comparisons','errors']},indent=2))
 if returncode or not result['passed'] or result['errors']:raise SystemExit(returncode or 1)
finally:
 stop.set()
 if thread:thread.join(timeout=3)
