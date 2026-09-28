#!/usr/bin/env python3
"""Check reference JSON, scene/file wiring and key safety constraints without Unity."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SA = ROOT / 'Assets/StreamingAssets'
TEMPLATES = SA / 'Templates'
scenes = set(re.findall(r'enabled: 1\s+path: Assets/Scenes/([^\n]+)\.unity', (ROOT / 'ProjectSettings/EditorBuildSettings.asset').read_text()))
count = 0
for path in sorted(TEMPLATES.glob('*.json')):
    obj = json.loads(path.read_text())
    count += 1
    if 'sequences' in obj:
        for step in obj['sequences']:
            assert step['sceneName'] in scenes, (path, step['sceneName'])
            assert step['duration'] > 0, path
            for key in ['configFile', 'design']:
                if key in step['parameters']:
                    assert (SA / step['parameters'][key]).is_file(), (path, key)
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

# New controls must not quietly fall back to legacy calls in project-owned runtime code.
for path in (ROOT / 'Assets/Scripts').rglob('*.cs'):
    code = '\n'.join(line.split('//')[0] for line in path.read_text().splitlines())
    assert not re.search(r'(?<!\w)Input\.(GetKey|GetAxis|GetMouseButton|mousePosition)', code), path

print(f'PASS: {count} JSON templates/examples, scene/file references, ranges and Input System migration')
