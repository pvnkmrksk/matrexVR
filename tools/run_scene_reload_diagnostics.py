#!/usr/bin/env python3
"""Run the disposable Unity forced-reload diagnostic with four real local ZMQ PUBs.

Uses an already-installed libzmq via ctypes; no packages are downloaded. Raw logs,
process samples and new Unity crash reports stay in the requested output folder.
"""
import argparse
import ctypes
import hashlib
from datetime import datetime, timezone
import json
import math
import os
import re
from pathlib import Path
import signal
import subprocess
import threading
import time


def utc():
    return datetime.now(timezone.utc).isoformat()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--count', type=int, default=200)
    parser.add_argument('--scenes', default='Choice_desync,Swarm')
    parser.add_argument('--profile', choices=('standard', 'terrain-night-sky'), default='standard')
    parser.add_argument('--duration', type=float, default=2.0)
    parser.add_argument('--base-port', type=int, default=29871)
    parser.add_argument('--headless', action='store_true')
    parser.add_argument('--source-sha')
    parser.add_argument('--pause-before-reload', type=float, default=0, help='Pause PUB delivery this many seconds before the configured row end; resume on the next scene-loaded event.')
    parser.add_argument('--hang-timeout', type=float, default=60, help='Capture native stacks and stop only this diagnostic child if its main-thread progress files stop changing for this many seconds.')
    parser.add_argument('--unity', type=Path, default=Path('/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity'))
    parser.add_argument('--libzmq', type=Path, default=Path('/Users/pavan/.local/share/uv/tools/sam-track/lib/python3.12/site-packages/cv2/.dylibs/libzmq.5.dylib'))
    args = parser.parse_args()
    project, output = args.project.resolve(), args.output.resolve()
    if not (project / 'KANNADI_VALIDATION_COPY').is_file():
        parser.error('A marked disposable project copy is required.')
    output.mkdir(parents=True, exist_ok=False)
    errors, stop = [], threading.Event()
    ready = threading.Event()
    diag_dir = Path.home() / 'Library/Logs/DiagnosticReports'
    prior = {p.name for p in diag_dir.glob('*')} if diag_dir.exists() else set()
    inventory = subprocess.check_output(['ps', '-axo', 'pid,ppid,rss,vsz,%cpu,etime,command'], text=True)
    (output / 'processes-before.txt').write_text('\n'.join(line for line in inventory.splitlines() if any(s in line for s in ('Unity.app/Contents/MacOS/Unity', 'UnityPackageManager', 'Unity.Licensing'))))

    def publisher():
        context, sockets = None, []
        try:
            lib = ctypes.CDLL(str(args.libzmq))
            lib.zmq_ctx_new.restype = ctypes.c_void_p
            lib.zmq_socket.argtypes, lib.zmq_socket.restype = [ctypes.c_void_p, ctypes.c_int], ctypes.c_void_p
            lib.zmq_bind.argtypes = [ctypes.c_void_p, ctypes.c_char_p]
            lib.zmq_setsockopt.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p, ctypes.c_size_t]
            lib.zmq_send.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_int]
            lib.zmq_close.argtypes = [ctypes.c_void_p]
            lib.zmq_ctx_term.argtypes = [ctypes.c_void_p]
            context = lib.zmq_ctx_new()
            for i in range(4):
                sock = lib.zmq_socket(context, 1)
                if not sock:
                    raise RuntimeError('Could not create synthetic PUB socket')
                sockets.append(sock)
                for option, value in ((17, 0), (23, 64)):
                    integer = ctypes.c_int(value)
                    lib.zmq_setsockopt(sock, option, ctypes.byref(integer), ctypes.sizeof(integer))
                endpoint = f'tcp://127.0.0.1:{args.base_port+i}'.encode()
                if lib.zmq_bind(sock, endpoint) != 0:
                    raise RuntimeError(f'Could not bind {endpoint.decode()}')
            ready.set()
            started, next_tick, next_report, sample = time.monotonic(), time.monotonic(), time.monotonic(), 0
            last_load_utc, next_events_read = None, 0
            with (output / 'publisher.jsonl').open('w') as writer:
                while not stop.is_set():
                    t = time.monotonic() - started
                    if args.pause_before_reload > 0 and time.monotonic() >= next_events_read:
                        event_path=output/'events.jsonl'
                        if event_path.exists():
                            with event_path.open('rb') as stream:
                                stream.seek(max(0,event_path.stat().st_size-16384)); tail=stream.read().decode('utf-8',errors='replace')
                            for line in reversed(tail.splitlines()):
                                try:
                                    event=json.loads(line)
                                except (ValueError,TypeError):
                                    continue
                                if event.get('kind')=='scene-loaded-before-Start':
                                    stamp=re.sub(r'\.(\d+)',lambda m:'.'+m.group(1).ljust(6,'0')[:6],event['utc'])
                                    last_load_utc=datetime.fromisoformat(stamp).timestamp();break
                        next_events_read=time.monotonic()+.04
                    paused=bool(args.pause_before_reload > 0 and last_load_utc and time.time()-last_load_utc >= args.duration-args.pause_before_reload)
                    if not paused:
                        for i, sock in enumerate(sockets):
                            packet = json.dumps({'x': .2*math.sin(t*.3+i), 'y': .2*math.cos(t*.2+i), 'z': 0., 'roll': 0., 'pitch': 0., 'yaw': .03*math.sin(t*.7+i), 'syntheticSample': sample}, separators=(',', ':')).encode()
                            topic = b'synthetic_pose'
                            if lib.zmq_send(sock, topic, len(topic), 2) < 0 or lib.zmq_send(sock, packet, len(packet), 0) < 0:
                                raise RuntimeError('Synthetic PUB send failed')
                        sample += 1
                    if time.monotonic() >= next_report:
                        writer.write(json.dumps({'utc': utc(), 'samplesPerPort': sample, 'ports': list(range(args.base_port, args.base_port+4)), 'elapsedSeconds': t, 'pausedForTeardown': paused})+'\n'); writer.flush(); next_report += 1
                    next_tick += 1/120
                    stop.wait(max(0, next_tick-time.monotonic()))
        except BaseException as error:
            errors.append(repr(error)); ready.set()
        finally:
            if sockets:
                for sock in sockets:
                    lib.zmq_close(sock)
            if context:
                lib.zmq_ctx_term(context)

    worker = threading.Thread(target=publisher, name='synthetic-four-port-publisher', daemon=True)
    worker.start(); ready.wait(10)
    if errors or not ready.is_set():
        raise RuntimeError('Publisher startup: '+str(errors))
    revision_file = project/'SOURCE-REVISION.txt'
    revision = args.source_sha or (revision_file.read_text().strip() if revision_file.exists() else subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip())
    command = [str(args.unity), '-batchmode', '-projectPath', str(project), '-executeMethod', 'SceneReloadDiagnostics.Run', '-logFile', str(output/'unity.log'), '-soak-output', str(output), '-soak-count', str(args.count), '-soak-scenes', args.scenes, '-soak-profile', args.profile, '-soak-duration', str(args.duration), '-soak-base-port', str(args.base_port), '-soak-source-sha', revision]
    if args.headless:
        command.insert(2, '-nographics')
    runtime_hashes = {str(path.relative_to(project)): hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted((project/'Assets/Scripts').rglob('*.cs'))}
    plugin_hashes = {str(path.relative_to(project)): hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted((project/'Assets/Plugins').glob('*.dll'))}
    metadata = {'startedUtc': utc(), 'command': command, 'project': str(project), 'sourceRevision': revision, 'diagnosticsSha256': hashlib.sha256((project/'Assets/Editor/SceneReloadDiagnostics.cs').read_bytes()).hexdigest(), 'runtimeSha256': runtime_hashes, 'pluginsSha256': plugin_hashes, 'profile': args.profile, 'publisherPorts': list(range(args.base_port, args.base_port+4)), 'publisherRateHz': 120, 'pauseBeforeReloadSeconds': args.pause_before_reload, 'headless': args.headless, 'forceFullSceneReload': True}
    (output/'launch.json').write_text(json.dumps(metadata, indent=2))
    process = subprocess.Popen(command, cwd=project, stdout=(output/'launcher.stdout').open('w'), stderr=subprocess.STDOUT)
    (output/'unity.pid').write_text(str(process.pid))
    started = time.monotonic()
    print(json.dumps({'output': str(output), 'unityPid': process.pid, 'startedUtc': utc()}), flush=True)
    supervisor_failure = None
    try:
        with (output/'process.jsonl').open('w') as writer:
            while process.poll() is None:
                row = {'utc': utc(), 'elapsedSeconds': time.monotonic()-started, 'pid': process.pid}
                try:
                    values = subprocess.check_output(['ps', '-p', str(process.pid), '-o', 'rss=,vsz=,%cpu='], text=True).split()
                    if len(values) >= 3:
                        row.update(rssBytes=int(values[0])*1024, virtualBytes=int(values[1])*1024, cpuPercent=float(values[2]))
                    threads = subprocess.check_output(['ps', '-M', '-p', str(process.pid), '-o', 'pid='], text=True)
                    row['osThreadCount'] = len([line for line in threads.splitlines() if line.strip()])
                    files = subprocess.run(['/usr/sbin/lsof', '-n', '-P', '-p', str(process.pid), '-F', 'f'], capture_output=True, text=True)
                    row['fileDescriptorCount'] = sum(1 for line in files.stdout.splitlines() if line.startswith('f') and line[1:2].isdigit())
                    row['freeDiskBytes'] = os.statvfs(output).f_bavail * os.statvfs(output).f_frsize
                except (subprocess.SubprocessError, ValueError) as error:
                    row['sampleError'] = str(error)
                writer.write(json.dumps(row)+'\n'); writer.flush()
                progress=[path.stat().st_mtime for path in (output/'metrics.jsonl',output/'events.jsonl') if path.exists() and path.stat().st_size]
                if progress and time.time()-max(progress)>args.hang_timeout:
                    hang=dict(utc=utc(),secondsWithoutMainThreadProgress=time.time()-max(progress),lastProcessSample=row)
                    for command,name in ((['/usr/bin/sample',str(process.pid),'3'],'hang-native-stack.txt'),(['/usr/bin/vmmap','-summary',str(process.pid)],'hang-memory-map.txt')):
                        try:
                            result=subprocess.run(command,capture_output=True,text=True,timeout=20)
                            (output/name).write_text(result.stdout+'\n'+result.stderr)
                        except (subprocess.SubprocessError,OSError) as error:
                            hang[name]=str(error)
                    (output/'hang.json').write_text(json.dumps(hang,indent=2))
                    raise RuntimeError('Diagnostic Unity main thread stopped making progress; native stack and memory map captured.')
                if errors:
                    raise RuntimeError('Publisher failed while Unity was running: '+str(errors))
                time.sleep(2)
    except BaseException as error:
        supervisor_failure=repr(error)
        if process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
    finally:
        stop.set(); worker.join(5)
    code = process.wait()
    if code not in (0, 1):
        # macOS CrashReporter can write the report a few seconds after the child exits.
        time.sleep(5)
    metadata.update(completedUtc=utc(), elapsedSeconds=time.monotonic()-started, unityExitCode=code, publisherErrors=errors, supervisorFailure=supervisor_failure)
    new_crashes = []
    for file in diag_dir.glob('*') if diag_dir.exists() else []:
        if file.name not in prior and file.is_file() and ('Unity' in file.name or 'mono' in file.name.lower()):
            target = output / 'crashes' / file.name; target.parent.mkdir(exist_ok=True); target.write_bytes(file.read_bytes()); new_crashes.append(str(target))
    metadata['newCrashReports'] = new_crashes
    (output/'completion.json').write_text(json.dumps(metadata, indent=2))
    print(json.dumps({key:value for key,value in metadata.items() if key!='runtimeSha256'}, indent=2), flush=True)
    raise SystemExit(code)


if __name__ == '__main__':
    main()
