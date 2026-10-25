#!/usr/bin/env python3
"""Stage pinned official dashboard packages at build time; runtime remains offline.

Existing local Mac builds can continue using just ``--output Companions``.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import tempfile

import companion_bundles

ROOT = Path(__file__).resolve().parents[1]
LOCK = ROOT / 'Assets/StreamingAssets/Companions/dashboard.lock.json'
LOCAL_LOCK = ROOT / 'tools/native_patches/dashboard-macos.lock.json'
CACHE = ROOT / '.release-work/dashboard-cache'
PLATFORMS = ('macos-arm64', 'macos-x64', 'windows-x64', 'linux-x64')
TARGETS = {'macOS': PLATFORMS[:2], 'Windows': ('windows-x64',), 'Linux': ('linux-x64',), 'all': PLATFORMS}


def platforms_for_target(target):
    if target in TARGETS:
        return list(TARGETS[target])
    if target in PLATFORMS:
        return [target]
    raise ValueError('Unsupported dashboard platform: ' + target)


def load_lock(path=LOCK):
    lock = json.loads(Path(path).read_text())
    validate_lock(lock)
    return lock


def validate_lock(lock):
    """Keep the selected binaries tied to the original official checksum manifest."""
    if lock.get('schemaVersion') != 1 or not re.fullmatch(r'[0-9a-f]{40}', lock.get('sourceRevision', '')):
        raise ValueError('Dashboard source revision is invalid')
    manifest = lock.get('checksumManifest', '')
    if hashlib.sha256(manifest.encode()).hexdigest() != lock.get('checksumManifestSha256'):
        raise ValueError('Dashboard checksum manifest changed')
    checksums = {}
    for line in manifest.splitlines():
        fields = line.split()
        if len(fields) != 2 or not re.fullmatch(r'[0-9a-f]{64}', fields[0]) or fields[1] in checksums:
            raise ValueError('Dashboard checksum manifest is invalid')
        checksums[fields[1]] = fields[0]
    if set(lock.get('platforms', {})) != set(PLATFORMS):
        raise ValueError('Dashboard lock must cover all four supported platform packages')
    for platform, asset in lock['platforms'].items():
        if checksums.get(asset['asset']) != asset['sha256']:
            raise ValueError('Dashboard archive is not pinned by its official checksum manifest')
        expected = 'DaariDeepa/DaariDeepa' + ('.exe' if platform.startswith('windows') else '')
        if asset['executable'] != expected:
            raise ValueError('Dashboard executable path is invalid for ' + platform)


def describe(lock, platform, root):
    asset = lock['platforms'][platform]
    executable = root / asset['executable']
    if not executable.is_file():
        raise ValueError('Dashboard executable is absent: ' + str(executable))
    if not platform.startswith('windows') and not executable.stat().st_mode & 0o111:
        raise ValueError('Dashboard executable mode was not preserved: ' + str(executable))
    return {
        'schemaVersion': 1, 'repository': lock['repository'],
        'sourceRevision': lock['sourceRevision'], 'sourceUrl': lock['sourceUrl'],
        'platform': platform, 'archiveUrl': asset['url'], 'archiveSha256': asset['sha256'],
        'checksumManifestUrl': lock['checksumManifestUrl'],
        'checksumManifestSha256': lock['checksumManifestSha256'],
        'executable': asset['executable'], 'executableSha256': companion_bundles.digest(executable),
    }


def stage(platforms, output, cache=CACHE, lock=None):
    """Add dashboard/<platform> beside the tracker catalog; never run a binary."""
    lock = load_lock() if lock is None else lock
    validate_lock(lock)
    if not platforms or len(set(platforms)) != len(platforms) or any(p not in PLATFORMS for p in platforms):
        raise ValueError('Choose distinct supported dashboard platforms')
    dashboard = Path(output) / 'dashboard'
    dashboard.mkdir(parents=True, exist_ok=True)
    entries = []
    for platform in platforms:
        asset = lock['platforms'][platform]
        destination = dashboard / platform
        marker = destination / 'dashboard-bundle.json'
        if destination.exists():
            if not marker.is_file():
                raise FileExistsError('Incomplete dashboard output; remove it before restaging: ' + str(destination))
            previous = json.loads(marker.read_text())
            current = describe(lock, platform, destination)
            if any(previous.get(key) != current[key] for key in ('sourceRevision', 'platform', 'archiveSha256', 'executableSha256')):
                raise FileExistsError('Dashboard output changed; remove it before restaging: ' + str(destination))
            # Upgrade provenance fields for older Mac payloads without recopying them.
            marker.write_text(json.dumps(current, indent=2) + '\n')
            entries.append(current)
            continue
        temporary = Path(tempfile.mkdtemp(prefix=platform + '.staging-', dir=dashboard))
        try:
            companion_bundles.extract(companion_bundles.download(asset, Path(cache)), temporary)
            current = describe(lock, platform, temporary)
            (temporary / 'dashboard-bundle.json').write_text(json.dumps(current, indent=2) + '\n')
            temporary.rename(destination)
            entries.append(current)
        finally:
            shutil.rmtree(temporary, ignore_errors=True)
    (dashboard / 'dashboard.lock.json').write_text(json.dumps(lock, indent=2) + '\n')
    (dashboard / 'SHA256SUMS.txt').write_text(lock['checksumManifest'])
    return entries


def stage_local(archive, output, lock=None):
    """Replace only the reviewed local arm64 payload, preserving other official platforms."""
    lock = json.loads(LOCAL_LOCK.read_text()) if lock is None else lock
    if (lock.get('schemaVersion') != 1 or lock.get('platform') != 'macos-arm64'
            or lock.get('published') is not False or lock.get('buildType') != 'local-source'
            or lock.get('repository') != 'pvnkmrksk/trajectory-dashboard'
            or not re.fullmatch(r'[0-9a-f]{40}', lock.get('sourceRevision', ''))
            or lock.get('executable') != 'DaariDeepa/DaariDeepa'):
        raise ValueError('Invalid local dashboard provenance')
    archive = Path(archive).resolve()
    if companion_bundles.digest(archive) != lock['archiveSha256']:
        raise ValueError('Local dashboard archive differs from its reviewed lock')
    dashboard = Path(output) / 'dashboard'
    dashboard.mkdir(parents=True, exist_ok=True)
    temporary = Path(tempfile.mkdtemp(prefix='macos-arm64.local-', dir=dashboard))
    try:
        companion_bundles.extract(archive, temporary)
        executable = temporary / lock['executable']
        if (not executable.is_file() or not executable.stat().st_mode & 0o111
                or companion_bundles.digest(executable) != lock['executableSha256']):
            raise ValueError('Local dashboard executable differs from its reviewed lock')
        current = dict(lock, archiveUrl=lock['sourceUrl'])
        (temporary / 'dashboard-bundle.json').write_text(json.dumps(current, indent=2) + '\n')
        destination = dashboard / lock['platform']
        if destination.exists():
            marker = destination / 'dashboard-bundle.json'
            if not marker.is_file():
                raise FileExistsError('Refusing to replace an unmarked dashboard payload')
            previous = json.loads(marker.read_text())
            repository = previous.get('repository', '').removeprefix('https://github.com/').removesuffix('.git')
            if repository != lock['repository'] or previous.get('platform') != lock['platform']:
                raise FileExistsError('Refusing to replace an unrelated dashboard payload')
            shutil.rmtree(destination)
        temporary.rename(destination)
        (dashboard / 'dashboard-local.lock.json').write_text(json.dumps(lock, indent=2) + '\n')
        return current
    finally:
        shutil.rmtree(temporary, ignore_errors=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--platform', choices=[*TARGETS, *PLATFORMS], default='macOS')
    parser.add_argument('--cache', type=Path, default=CACHE)
    parser.add_argument('--local-archive', type=Path, help='Apply the reviewed local Mac arm64 live-plot build after official staging')
    args = parser.parse_args()
    platforms = platforms_for_target(args.platform)
    if args.local_archive:
        if 'macos-arm64' not in platforms:
            raise ValueError('Local dashboard correction supports macos-arm64 only')
        platforms.remove('macos-arm64')
    if platforms:
        stage(platforms, args.output, args.cache)
    if args.local_archive:
        stage_local(args.local_archive, args.output)
    print('Verified dashboard payload:', args.output / 'dashboard')


if __name__ == '__main__':
    main()
