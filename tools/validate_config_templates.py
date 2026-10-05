#!/usr/bin/env python3
"""Check reference JSON, scene/file wiring and key safety constraints without Unity."""
import json
import re
import hashlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SA = ROOT / 'Assets/StreamingAssets'
TEMPLATES = SA / 'Templates'
scenes = set(re.findall(r'enabled: 1\s+path: Assets/Scenes/([^\n]+)\.unity', (ROOT / 'ProjectSettings/EditorBuildSettings.asset').read_text()))
count = 0
for path in sorted(list(TEMPLATES.glob('*.json')) + list((SA / 'Examples').rglob('*.json'))):
    obj = json.loads(path.read_text())
    count += 1
    if 'sequences' in obj:
        for step in obj['sequences']:
            assert step['sceneName'] in scenes, (path, step['sceneName'])
            assert step['duration'] > 0, path
            for key in ['configFile', 'design']:
                if key in step['parameters']:
                    assert (SA / step['parameters'][key]).is_file(), (path, key)
    if 'headingReference' in obj:
        reference = obj['headingReference']
        assert isinstance(reference.get('enabled', False), bool), path
        if reference.get('enabled', False):
            assert reference.get('windowSeconds', 180) > 0, path
    if 'telemetry' in obj and obj['telemetry'].get('enabled', True):
        telemetry = obj['telemetry']
        assert 1 <= telemetry.get('port', 9880) <= 65535, path
        assert 0 < telemetry.get('rateHz', 20) <= 240, path
        assert telemetry.get('port', 9880) not in [rig['zmqPort'] for rig in obj['configs']], path
    if 'configs' in obj:
        target = obj.get('targetFrameRate', 120)
        assert target == -1 or target > 0, path
        assert 0 <= obj.get('vSyncCount', 0) <= 4, path
        ids = [rig['vrId'] for rig in obj['configs']]
        assert len(ids) == len(set(ids)), path
        for rig in obj['configs']:
            assert rig['sphereDiameter'] > 0, path
            assert set(rig['displayOrder']) <= set('RBLFUD'), path
    if 'stimuli' in obj:
        assert obj['stimuli'], path
        for stimulus in obj['stimuli']:
            assert stimulus['duration'] > 0 and stimulus['frequency'] > 0, path
            assert 0 <= stimulus['contrast'] <= 1 and 0 <= stimulus['dutyCycle'] <= 1, path
            assert stimulus['rotationAxis'] in ['Yaw', 'Pitch', 'Roll'], path
    if 'vrConfigs' in obj:
        assert all(1 <= vr['vrIndex'] <= 4 for vr in obj['vrConfigs']), path
    if 'aglHeight' in obj:
        assert obj['aglHeight'] >= 0 and obj['windSpeed'] >= 0, path
    if 'autopilotSpeed' in obj:
        assert isinstance(obj.get('autopilotEnabled', False), bool), path
        assert obj['autopilotSpeed'] >= 0, path
    if 'agentVisual' in obj:
        assert obj['agentVisual'] in ['ScenePrefab', 'Bogong'], path
    if 'dimension' in obj:
        assert obj['dimension'] in ['2D', '3D'], path
    if 'density' in obj and obj['density'] is not None:
        assert obj['density'] >= 0, path
    if 'spawnVolumeSize' in obj:
        size = obj['spawnVolumeSize']
        assert size is None or all(size[key] > 0 for key in ['x', 'y', 'z']), path
    if 'bogongVisual' in obj:
        visual = obj['bogongVisual']
        assert visual['sizeMode'] in ['World', 'Angular'], path
        assert visual['size'] > 0 and visual['angularSizeDegrees'] > 0, path
        assert 0 <= visual['metallic'] <= 1 and 0 <= visual['smoothness'] <= 1, path
        assert visual['flickerFrequencyHz'] >= 0 and 0 <= visual['flickerDutyCycle'] <= 1, path
    if 'nightSky' in obj:
        sky = obj['nightSky']
        assert -90 <= sky.get('latitude', 47.6896) <= 90, path
        assert -180 <= sky.get('longitude', 9.1881) <= 180, path
        assert 1 <= sky.get('updateIntervalMinutes', 30) <= 60, path
        assert sky.get('imageWidth', 4096) in [1024, 2048, 4096], path

# The shipped runnable sequences use the experiment-file hierarchy for Swarm.
for sequence_path in [SA / 'sequenceConfig.json', SA / 'Kannadi' / 'sequenceConfig.json']:
    if not sequence_path.is_file():
        continue
    sequence = json.loads(sequence_path.read_text())
    for step in sequence.get('sequences', []):
        for key in ('configFile', 'design'):
            reference = step.get('parameters', {}).get(key)
            if reference:
                assert (SA / reference).is_file() or (SA / 'Archive/Legacy' / reference).is_file(), (sequence_path, reference)
        if step.get('sceneName') != 'Swarm':
            continue
        params = step.get('parameters', {})
        assert params.get('configFile'), (sequence_path, 'Swarm configFile')
        assert (SA / params['configFile']).is_file(), (sequence_path, params['configFile'])
        assert not any(key in params for key in ['numberOfLocusts', 'spawnAreaSize', 'mu', 'kappa', 'locustSpeed']), (sequence_path, 'legacy inline Swarm setting')

# New controls must not quietly fall back to legacy calls in project-owned runtime code.
for path in (ROOT / 'Assets/Scripts').rglob('*.cs'):
    code = '\n'.join(line.split('//')[0] for line in path.read_text().splitlines())
    assert not re.search(r'(?<!\w)Input\.(GetKey|GetAxis|GetMouseButton|mousePosition)', code), path

print(f'PASS: {count} JSON templates/examples, scene/file references, ranges and Input System migration')

# The archive preserves original contents, including historical invalid/unsupported configs.
manifest = json.loads((ROOT / 'docs/config-archive-manifest.json').read_text())
for record in manifest['archived']:
    archived = SA / record['archive']
    assert archived.is_file(), archived
    assert hashlib.sha256(archived.read_bytes()).hexdigest() == record['sha256'], archived
    if record['operation'] == 'move':
        assert not (SA / record['original']).exists(), record['original']
for path, digest in manifest['preservedSha256'].items():
    # The machine-specific hardware file is intentionally not required in another checkout.
    if path.endswith('/system_config.json'):
        continue
    assert (ROOT / path).is_file(), path
    assert hashlib.sha256((ROOT / path).read_bytes()).hexdigest() == digest, path
assert {p.name for p in SA.glob('*.json')} <= {'system_config.json', 'sequenceConfig.json'}
print(f"PASS: {len(manifest['archived'])} archived file hashes and preserved Kannadi/active sequence")

for record in manifest.get('otherArchives', []):
    target = ROOT / record['archive']
    assert target.is_file() and hashlib.sha256(target.read_bytes()).hexdigest() == record['sha256'], target
    assert not (ROOT / record['original']).exists(), record['original']
print(f"PASS: {len(manifest.get('otherArchives', []))} earlier locust inputs consolidated into archive")
