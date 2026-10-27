#!/usr/bin/env python3
"""Bounded local terrain profiling with independent real native tracker processes.

This drives the opt-in performance API, never a camera or an animal. Native video
is finalized with the tracker's normal SIGINT handler; source code/configuration
and existing recordings remain untouched.
"""
import argparse
import hashlib
import json
from pathlib import Path
import resource
import signal
import subprocess
import time


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, required=True)
    parser.add_argument('--native-binary', type=Path, required=True)
    parser.add_argument('--native-config', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--seconds', type=float, default=4)
    parser.add_argument('--phases', default='all-60,all-uncapped,tracking-off,logging-off,all-60-repeat')
    args = parser.parse_args()
    allowed_phases = {'all-60', 'all-uncapped', 'tracking-off', 'logging-off', 'all-60-repeat'}
    phases = args.phases.split(',')
    if not 1 <= args.seconds <= 10 or not 1 <= len(phases) <= 5 or any(phase not in allowed_phases for phase in phases):
        parser.error('Use 1–10 seconds and at most five supported performance phases.')
    args.app = args.app.resolve(); args.output = args.output.resolve()
    args.native_binary = args.native_binary.resolve(); args.native_config = args.native_config.resolve()
    if args.output.exists():
        raise SystemExit('Choose a new output directory; existing evidence is never replaced.')
    if not all(path.exists() for path in (args.app, args.native_binary, args.native_config)):
        raise SystemExit('The app, native executable and config must already exist.')
    args.output.mkdir(parents=True)
    identity = json.loads((args.app / 'Contents/Resources/Data/StreamingAssets/player_build_info.json').read_text())
    evidence = {'scope': 'Standalone software measurements with independent native generated Rekkefly input, no physical acquisition. Engine FPS is not physical LED timing. Focus is reported per phase. Native recording starts before Unity and is stopped after it; this does not implement run-scoped recording control.',
                'app': str(args.app), 'identity': identity, 'nativeBinary': str(args.native_binary),
                'nativeSha256': sha(args.native_binary), 'configSha256': sha(args.native_config), 'cases': []}
    for count in (1, 4):
        for record in (False, True):
            name = f'{count}-rig-video-{"on" if record else "off"}'
            folder = args.output / name
            folder.mkdir()
            children, logs = [], []
            usage_before = resource.getrusage(resource.RUSAGE_CHILDREN)
            started = time.monotonic()
            try:
                for index in range(1, count + 1):
                    native = folder / 'native' / f'VR{index}'
                    native.mkdir(parents=True)
                    config = native / 'input.toml'
                    config.write_bytes(args.native_config.read_bytes())
                    log = (native / 'console.log').open('w')
                    logs.append(log)
                    command = [str(args.native_binary), '--config', str(config), '--headless', '--demo',
                               '--label', name + '-VR' + str(index), '--zmq-port', str(30000 + index),
                               '--frames', '10000', '--json']
                    if record:
                        command.append('--record')
                    children.append(subprocess.Popen(command, stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT))
                command = ['/usr/bin/open', '-n', '-W', str(args.app), '--args',
                           '--config-dir', str(folder / 'Config'), '--data-dir', str(folder / 'RunData'),
                           '--performance-probe', str(folder), '--performance-scene', 'TerrainChoice_RoughLeft_SmoothRight',
                           '--performance-layout', 'led', '--performance-rigs', str(count),
                           '--performance-seconds', str(args.seconds), '--performance-phases', args.phases,
                           '-logFile', str(folder / 'player.log')]
                print('RUNNING ' + name, flush=True)
                result = subprocess.run(command, timeout=150)
                if result.returncode:
                    raise RuntimeError('Local player launch failed: ' + str(result.returncode))
            finally:
                for child in children:
                    if child.poll() is None:
                        child.send_signal(signal.SIGINT)
                for child in children:
                    try:
                        child.wait(timeout=30)
                    except subprocess.TimeoutExpired:
                        child.terminate(); child.wait(timeout=10)
                for log in logs:
                    log.close()
            usage_after = resource.getrusage(resource.RUSAGE_CHILDREN)
            profile = json.loads((folder / 'results.json').read_text())
            native_results = []
            for index, child in enumerate(children, 1):
                log = folder / 'native' / f'VR{index}' / 'console.log'
                rows = [json.loads(line) for line in log.read_text().splitlines() if line.startswith('{')]
                if not rows:
                    raise RuntimeError('Native tracker did not return final status: ' + str(log))
                state = rows[-1]
                state['exitCode'] = child.returncode
                if child.returncode or state['recording']['error']:
                    raise RuntimeError('Native tracker/recorder failed: ' + json.dumps(state))
                if record and (state['recording']['accepted'] < 1 or state['recording']['accepted'] != state['recording']['written']):
                    raise RuntimeError('Recorder did not finalize every accepted frame.')
                if not record and state['recording']['accepted']:
                    raise RuntimeError('Video-off native tracker unexpectedly recorded frames.')
                native_results.append(state)
            case = {'name': name, 'rigs': count, 'saveNativeVideo': record, 'wallSeconds': time.monotonic() - started,
                    'nativeAndLauncherCpuSeconds': (usage_after.ru_utime + usage_after.ru_stime) - (usage_before.ru_utime + usage_before.ru_stime),
                    'native': native_results, 'failures': profile['failures'], 'results': profile['results']}
            if profile['failures']:
                raise RuntimeError('Performance probe failure: ' + json.dumps(profile['failures']))
            evidence['cases'].append(case)
            (args.output / 'summary.json').write_text(json.dumps(evidence, indent=2) + '\n')
            print('DONE ' + name + ' ' + json.dumps({row['phase']: round(row['fps'], 2) for row in profile['results']}), flush=True)
    print(args.output / 'summary.json', flush=True)


if __name__ == '__main__':
    main()
