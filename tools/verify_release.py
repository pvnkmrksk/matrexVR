#!/usr/bin/env python3
"""Check packaged player identity, executable architecture, and release asset digests."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile

from build_release import TARGETS, git
from companion_bundles import digest


def verify_package(workspace, version, revision, platform, required_files):
    target, _, executable = TARGETS[platform]
    label = 'macOS-universal' if platform == 'macOS' else platform
    name = f'MATREX-VR-{version}-{label}'
    archive = workspace / (name + '.zip')
    declared, filename = Path(str(archive) + '.sha256').read_text().split()
    if filename != archive.name or digest(archive) != declared:
        raise ValueError(f'Checksum mismatch for {archive.name}')

    if platform == 'macOS':
        player = executable + '/Contents/MacOS/MATREX VR'
        streaming_assets = executable + '/Contents/Resources/Data/StreamingAssets'
        architecture = ('Mach-O', 'arm64', 'x86_64')
    else:
        player = executable
        streaming_assets = 'MATREX VR_Data/StreamingAssets'
        architecture = ('PE32+', 'x86-64') if platform == 'Windows' else ('ELF 64-bit', 'x86-64')
    with zipfile.ZipFile(archive) as package:
        corrupt = package.testzip()
        if corrupt:
            raise ValueError(f'Corrupt member in {archive.name}: {corrupt}')
        manifest = json.loads(package.read(f'{name}/release-manifest.json'))
        identity = json.loads(package.read(f'{name}/{streaming_assets}/player_build_info.json'))
        quickstart = package.read(f'{name}/START-HERE.txt')
        build = manifest['build']
        if (manifest['version'], manifest['sourceRevision'], manifest['platform']) != (version, revision, label):
            raise ValueError(f'Unexpected release identity in {archive.name}')
        if build['version'] != version or build['target'] != target or build['result'] != 'Succeeded' or build['errors']:
            raise ValueError(f'Invalid build report in {archive.name}')
        if (identity['applicationVersion'], identity['sourceRevision'], identity['sourceState']) != (
                version, revision, 'pinned-git-snapshot'):
            raise ValueError(f'Unexpected embedded player identity in {archive.name}')
        if version.encode() not in quickstart.splitlines()[0] or hashlib.sha256(quickstart).hexdigest() != manifest['quickstartSha256']:
            raise ValueError(f'Incorrect quickstart in {archive.name}')
        for path in required_files:
            package.getinfo(f'{name}/{streaming_assets}/{path}')
        packaged_player = hashlib.sha256(package.read(f'{name}/{player}')).hexdigest()

    staged_player = workspace / name / player
    if digest(staged_player) != packaged_player:
        raise ValueError(f'Packaged executable differs from the staged player in {archive.name}')
    executable_type = subprocess.check_output(['file', '-b', str(staged_player)], text=True).strip()
    if not all(token in executable_type for token in architecture):
        raise ValueError(f'Unexpected architecture in {archive.name}: {executable_type}')
    if platform == 'macOS':
        subprocess.run(['codesign', '--verify', '--strict', str(workspace / name / executable)], check=True)
    return dict(archive=archive.name, sha256=declared, bytes=archive.stat().st_size,
                buildGuid=build['buildGuid'], sceneCount=len(build['scenes']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--workspace', type=Path, required=True)
    parser.add_argument('--version', required=True)
    parser.add_argument('--ref', default='HEAD')
    parser.add_argument('--platform', choices=[*TARGETS, 'all'], default='all')
    parser.add_argument('--require-streaming-asset', action='append', default=[])
    args = parser.parse_args()
    revision = git('rev-parse', '--verify', args.ref + '^{commit}').decode().strip()
    platforms = TARGETS if args.platform == 'all' else [args.platform]
    verified = {}
    for platform in platforms:
        verified[platform] = verify_package(args.workspace, args.version, revision, platform, args.require_streaming_asset)
        print(f'PASS: {platform} archive, architecture, build identity and required assets', flush=True)
    report = args.workspace / 'verification.json'
    report.write_text(json.dumps(dict(version=args.version, sourceRevision=revision, platforms=verified), indent=2) + '\n')
    print(report)


if __name__ == '__main__':
    main()
