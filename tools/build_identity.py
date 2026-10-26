"""Describe the prepared source snapshot; never infer a running player's identity from today's checkout."""
import hashlib
import json
from pathlib import Path
import re


def write(project, revision, state, dirty_paths=()):
    project = Path(project)
    files = {}
    for path in sorted((project / 'Assets').rglob('*.cs')):
        relative = path.relative_to(project)
        if 'Editor' not in relative.parts:
            files[relative.as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    fingerprint = hashlib.sha256(json.dumps(files, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    info = {'schemaVersion': 1, 'sourceRevision': revision if re.fullmatch(r'[0-9a-f]{40}', revision or '') else None,
            'sourceState': state, 'sourceTreeDirty': bool(dirty_paths), 'runtimeSourceFingerprintSha256': fingerprint,
            'runtimeSourceFileCount': len(files),
            'fingerprintScope': 'Prepared non-Editor C# files under Assets, sorted path-to-SHA256 compact JSON; packages, scenes and settings are outside this fingerprint.',
            'dirtyPathsAtPreparation': list(dirty_paths)}
    target = project / 'Assets/StreamingAssets/player_build_info.json'
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(info, indent=2) + '\n')
    return info
