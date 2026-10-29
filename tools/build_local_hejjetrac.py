#!/usr/bin/env python3
"""Build the pinned HejjéTrac native-GUI recording adapter for local Mac arm64 use.

Keeps the original attributed resources and independent native window. This is
a reviewed source overlay, not a relabeled upstream release. Cached executables
require explicit matching source/patch/binary provenance.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import plistlib
import shutil
import subprocess
import tempfile

import build_local_rekkefly as shared
import companion_bundles as bundles

ROOT = Path(__file__).resolve().parents[1]
LOCK = ROOT / 'tools/native_patches/hejjetrac-recording-macos.lock.json'


def load_lock():
    lock = json.loads(LOCK.read_text())
    patch = ROOT / lock['patch']
    if bundles.digest(patch) != lock['patchSha256']:
        raise ValueError('HejjéTrac recording patch differs from its reviewed lock')
    return lock, patch


def stage(companions, binary, lock, patch, template_archive=None):
    catalog_path = companions / 'catalog.json'
    catalog = json.loads(catalog_path.read_text())
    matches = [e for e in catalog['entries'] if e['backend'] == 'hejjetrac' and e['platform'] == 'macos-arm64']
    if len(matches) != 1:
        raise ValueError('Base catalog must contain one Mac arm64 HejjéTrac entry')
    entry = matches[0]
    if entry.get('localSourceBuild'):
        package = companions / entry['packageRoot']
        provenance = json.loads((package / 'MATREX-NATIVE-PROVENANCE.json').read_text())
        for key in ('sourceRevision', 'patchSha256'):
            if entry.get(key) != lock[key]:
                raise ValueError('Recreate the disposable companion payload before replacing a different HejjéTrac source overlay')
        if bundles.digest(package / entry['executable']) != entry['executableSha256'] or provenance['unsignedBuildExecutableSha256'] != bundles.digest(binary):
            raise ValueError('Existing HejjéTrac payload or selected executable changed')
        return entry
    archive = Path(template_archive) if template_archive else bundles.download(lock['templateArchive'], ROOT / '.release-work/companion-cache')
    if bundles.digest(archive) != lock['templateArchive']['sha256']:
        raise ValueError('HejjéTrac template archive checksum differs from its pinned release')
    tag = shared.local_tag(lock)
    package = companions / 'hejjetrac' / tag / 'macos-arm64'
    if package.exists():
        raise FileExistsError('Refusing to overwrite a native package: ' + str(package))
    package.parent.mkdir(parents=True, exist_ok=True)
    temporary = Path(tempfile.mkdtemp(prefix='macos-arm64.staging-', dir=package.parent))
    try:
        bundles.extract(archive, temporary)
        details = bundles.describe(temporary, 'hejjetrac', 'macos-arm64')
        executable = bundles.safe_destination(temporary, details['executable'])
        shutil.copy2(binary, executable)
        # The historical desktop spelling is a second copy/symlink of the same
        # desktop application. CLI binaries remain the pinned upstream CLI.
        alias = executable.with_name('hejjetrak')
        if alias.is_file() and not alias.is_symlink():
            shutil.copy2(binary, alias)
        app = next(p for p in executable.parents if p.suffix == '.app')
        plist_path = app / 'Contents/Info.plist'
        with plist_path.open('rb') as stream:
            info = plistlib.load(stream)
        info['CFBundleShortVersionString'] = lock['sourceVersion']
        info['CFBundleVersion'] = lock['sourceVersion']
        with plist_path.open('wb') as stream:
            plistlib.dump(info, stream)
        subprocess.run(['/usr/bin/codesign', '--force', '--sign', '-', '--timestamp=none', str(app)], check=True)
        subprocess.run(['/usr/bin/codesign', '--verify', '--strict', str(app)], check=True)
        provenance = dict(lock, buildType='Local native-GUI recording adapter; not an official release',
                          unsignedBuildExecutableSha256=bundles.digest(binary))
        (temporary / 'MATREX-NATIVE-PROVENANCE.json').write_text(json.dumps(provenance, indent=2) + '\n')
        shutil.copy2(patch, temporary / patch.name)
        details = bundles.describe(temporary, 'hejjetrac', 'macos-arm64')
        temporary.rename(package)
        entry['replacedReleaseTag'] = entry['releaseTag']
        entry['replacedArchiveSha256'] = entry.pop('archiveSha256', None)
        entry.update(details, releaseTag=tag, releaseUrl=lock['sourceUrl'], sourceRevision=lock['sourceRevision'],
                     patchSha256=lock['patchSha256'], localSourceBuild=True,
                     packageRoot=package.relative_to(companions).as_posix(), supportsRecordingControl=True)
        catalog['localNativeRecordingAdapters'] = dict(catalog.get('localNativeRecordingAdapters', {}), hejjetrac=provenance)
        catalog.setdefault('upstreamCatalogId', catalog['catalogId'])
        catalog['catalogId'] = hashlib.sha256((catalog['catalogId'] + ':' + lock['patchSha256']).encode()).hexdigest()[:16]
        writing = catalog_path.with_suffix('.json.writing')
        writing.write_text(json.dumps(catalog, indent=2) + '\n')
        writing.replace(catalog_path)
        return entry
    finally:
        shutil.rmtree(temporary, ignore_errors=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--companions', required=True, type=Path)
    parser.add_argument('--source-repo', type=Path, default=ROOT / '.integration/hejjetrak-recording-20261027')
    parser.add_argument('--cargo', type=Path, default=Path(shutil.which('cargo') or '/private/tmp/hejjetrac-cargo/bin/cargo'))
    parser.add_argument('--target-dir', type=Path, default=ROOT / '.release-work/native-target/hejjetrac')
    parser.add_argument('--template-archive', type=Path, help='Explicit cached v0.3.2 archive, still verified against the lock')
    parser.add_argument('--verified-binary', type=Path)
    parser.add_argument('--binary-provenance', type=Path)
    args = parser.parse_args()
    if (args.verified_binary is None) != (args.binary_provenance is None):
        parser.error('--verified-binary and --binary-provenance must be supplied together')
    if platform.system() != 'Darwin' or platform.machine() != 'arm64':
        parser.error('This adapter has been built and exercised only on Mac arm64')
    lock, patch = load_lock()
    source = ROOT / '.integration/native-build' / ('hejjetrac-' + shared.local_tag(lock))
    shared.prepare_source(shared.source_repository(args.source_repo, lock, backend='hejjetrac'), source, lock, patch)
    if args.verified_binary:
        binary = shared.verified_binary(args.verified_binary, args.binary_provenance, lock)
    else:
        environment = os.environ.copy()
        environment['PATH'] = str(args.cargo.parent) + os.pathsep + environment.get('PATH', '')
        for variable, candidate in [('CARGO_HOME', '/private/tmp/hejjetrac-cargo'), ('RUSTUP_HOME', '/private/tmp/hejjetrac-rustup')]:
            if variable not in environment and Path(candidate).is_dir():
                environment[variable] = candidate
        subprocess.run([str(args.cargo), 'build', '--release', '--locked', '-p', 'fictrac-desktop', '--target-dir', str(args.target_dir.resolve())], cwd=source, env=environment, check=True)
        binary = args.target_dir.resolve() / 'release/fictrac-desktop'
    print(json.dumps(stage(args.companions.resolve(), binary, lock, patch, args.template_archive), indent=2))


if __name__ == '__main__':
    main()
