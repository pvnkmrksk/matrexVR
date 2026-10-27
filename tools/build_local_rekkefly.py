#!/usr/bin/env python3
"""Build the pinned local Mac camera correction, then replace only its catalog entry.

This does not publish or relabel an official release. Building requires a Rust
toolchain and the pinned upstream source. An explicitly selected cached binary
can instead be reused with its matching prior build-provenance receipt; source
and patch integrity are still checked before staging.
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

import companion_bundles

ROOT = Path(__file__).resolve().parents[1]
LOCK = ROOT / 'tools/native_patches/rekkefly-macos.lock.json'


def load_lock():
    lock = json.loads(LOCK.read_text())
    patch = ROOT / lock['patch']
    if companion_bundles.digest(patch) != lock['patchSha256']:
        raise ValueError('Local native patch changed without updating its reviewed lock')
    return lock, patch


def local_tag(lock):
    return 'local-' + lock['sourceRevision'][:8] + '-' + lock['patchSha256'][:12]


def source_repository(preferred, lock):
    """Read a suitable local clone, otherwise fetch into our disposable bare cache."""
    if preferred.is_dir():
        result = subprocess.run(['git', '-C', str(preferred), 'cat-file', '-e', lock['sourceRevision'] + '^{commit}'],
                                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        if result.returncode == 0:
            return preferred
    cached = ROOT / '.integration/native-upstream/rekkefly.git'
    if not cached.exists():
        cached.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(['git', 'init', '--bare', str(cached)], check=True, stdout=subprocess.DEVNULL)
    result = subprocess.run(['git', '-C', str(cached), 'cat-file', '-e', lock['sourceRevision'] + '^{commit}'],
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    if result.returncode != 0:
        subprocess.run(['git', '-C', str(cached), 'fetch', '--depth', '1', lock['repository'] + '.git', lock['sourceRevision']], check=True)
    return cached


def prepare_source(repository, output, lock, patch):
    marker = output / 'MATREX-NATIVE-SOURCE.json'
    identity = {'sourceRevision': lock['sourceRevision'], 'patchSha256': lock['patchSha256']}
    if output.exists():
        saved = json.loads(marker.read_text()) if marker.is_file() else {}
        if any(saved.get(key) != value for key, value in identity.items()):
            raise FileExistsError('Refusing an unmarked native source directory: ' + str(output))
        for relative, expected in saved['filesSha256'].items():
            if companion_bundles.digest(output / relative) != expected:
                raise ValueError('Prepared native source changed; remove the disposable source folder: ' + relative)
        return output
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = Path(tempfile.mkdtemp(prefix=output.name + '.staging-', dir=output.parent))
    try:
        archive = subprocess.Popen(['git', '-C', str(repository), 'archive', lock['sourceRevision']], stdout=subprocess.PIPE)
        try:
            subprocess.run(['tar', '-x', '-C', str(temporary)], stdin=archive.stdout, check=True)
        finally:
            archive.stdout.close()
        if archive.wait() != 0:
            raise RuntimeError('Could not extract the pinned Rekkefly commit from the local clone')
        environment = os.environ.copy()
        environment['GIT_CEILING_DIRECTORIES'] = str(temporary.parent.resolve())
        subprocess.run(['git', 'apply', '--check', str(patch)], cwd=temporary, env=environment, check=True)
        subprocess.run(['git', 'apply', str(patch)], cwd=temporary, env=environment, check=True)
        identity['filesSha256'] = {p.relative_to(temporary).as_posix(): companion_bundles.digest(p)
                                   for p in sorted(temporary.rglob('*')) if p.is_file()}
        (temporary / marker.name).write_text(json.dumps(identity, indent=2) + '\n')
        temporary.rename(output)
        return output
    finally:
        shutil.rmtree(temporary, ignore_errors=True)


def build(source, cargo, target, environment):
    subprocess.run([str(cargo), 'build', '--release', '--locked', '--target-dir', str(target)],
                   cwd=source, env=environment, check=True)
    return target / 'release/rekkefly'


def verified_binary(binary, provenance_file, lock):
    """Accept only bytes explicitly paired with a matching locked build receipt.

    The receipt is evidence from a previous local build, not a substitute for
    source verification: main also runs prepare_source before this path.
    """
    binary = binary.resolve(strict=True)
    if not binary.is_file():
        raise ValueError('The verified native binary must be a regular file')
    provenance = json.loads(provenance_file.read_text())
    if not isinstance(provenance, dict):
        raise ValueError('Native binary provenance must be a JSON object')
    for field in ('repository', 'sourceRevision', 'patchSha256', 'platform', 'sourceVersion'):
        if provenance.get(field) != lock[field]:
            raise ValueError('Cached native binary provenance does not match the pinned ' + field)
    if provenance.get('unsignedBuildExecutableSha256') != companion_bundles.digest(binary):
        raise ValueError('Cached native binary bytes do not match the supplied build provenance')
    return binary


def stage(companions, binary, lock, patch):
    catalog_file = companions / 'catalog.json'
    catalog = json.loads(catalog_file.read_text())
    matches = [e for e in catalog['entries'] if e['backend'] == 'rekkefly' and e['platform'] == 'macos-arm64']
    if len(matches) != 1:
        raise ValueError('The base bundle must contain one official Mac arm64 Rekkefly entry')
    entry = matches[0]
    tag = local_tag(lock)
    if entry.get('localSourceBuild'):
        if entry.get('patchSha256') != lock['patchSha256'] or entry.get('sourceRevision') != lock['sourceRevision']:
            raise ValueError('Remove the previous local source payload before applying a different patch')
        executable = companions / entry['packageRoot'] / entry['executable']
        if companion_bundles.digest(executable) != entry['executableSha256']:
            raise ValueError('The previously staged local native executable changed')
        provenance_file = companions / entry['packageRoot'] / 'MATREX-NATIVE-PROVENANCE.json'
        provenance = json.loads(provenance_file.read_text())
        if provenance['unsignedBuildExecutableSha256'] != companion_bundles.digest(binary):
            raise ValueError('This correction was built with different bytes; recreate the disposable companion payload before replacing it')
        return entry
    package = companions / 'rekkefly' / tag / 'macos-arm64'
    if package.exists():
        raise FileExistsError('Refusing to overwrite a native package: ' + str(package))
    source = companion_bundles.safe_destination(companions, entry['packageRoot'])
    temporary = package.with_name(package.name + '.staging')
    temporary.parent.mkdir(parents=True, exist_ok=True)
    try:
        shutil.copytree(source, temporary, symlinks=True)
        executable = companion_bundles.safe_destination(temporary, entry['executable'])
        shutil.copy2(binary, executable)
        # The upstream complete package also exposes a CLI copy beside its app.
        for extra in temporary.glob('*/rekkefly'):
            if extra.is_file():
                shutil.copy2(binary, extra)
        app = next(p for p in executable.parents if p.suffix == '.app')
        plist_file = app / 'Contents/Info.plist'
        with plist_file.open('rb') as stream:
            info = plistlib.load(stream)
        info['CFBundleShortVersionString'] = lock['sourceVersion']
        info['CFBundleVersion'] = lock['sourceVersion']
        with plist_file.open('wb') as stream:
            plistlib.dump(info, stream)
        subprocess.run(['/usr/bin/codesign', '--force', '--sign', '-', '--timestamp=none', str(app)], check=True)
        subprocess.run(['/usr/bin/codesign', '--verify', '--strict', str(app)], check=True)
        provenance = dict(lock, buildType='Local source correction; not an official release',
                          upstreamReleaseTag=entry['releaseTag'], upstreamArchiveSha256=entry.get('archiveSha256'),
                          unsignedBuildExecutableSha256=companion_bundles.digest(binary))
        (temporary / 'MATREX-NATIVE-PROVENANCE.json').write_text(json.dumps(provenance, indent=2) + '\n')
        shutil.copy2(patch, temporary / patch.name)
        details = companion_bundles.describe(temporary, 'rekkefly', 'macos-arm64')
        temporary.rename(package)
        entry.update(details)
        entry['upstreamReleaseTag'] = entry['releaseTag']
        entry['upstreamArchiveSha256'] = entry.pop('archiveSha256', None)
        entry.update(releaseTag=tag, releaseUrl=lock['sourceUrl'], sourceRevision=lock['sourceRevision'],
                     patchSha256=lock['patchSha256'], localSourceBuild=True,
                     packageRoot=package.relative_to(companions).as_posix(),
                     supportsNoCameraFallback=True, supportsSourceProvenance=True)
        catalog['localNativeCorrection'] = provenance
        catalog['upstreamCatalogId'] = catalog['catalogId']
        catalog['catalogId'] = hashlib.sha256((catalog['upstreamCatalogId'] + ':' + lock['patchSha256']).encode()).hexdigest()[:16]
        writing = catalog_file.with_suffix('.json.writing')
        writing.write_text(json.dumps(catalog, indent=2) + '\n')
        writing.replace(catalog_file)
        return entry
    finally:
        shutil.rmtree(temporary, ignore_errors=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--companions', type=Path, required=True)
    parser.add_argument('--source-repo', type=Path, default=ROOT.parent / 'rekkefly')
    parser.add_argument('--cargo', type=Path, default=Path(shutil.which('cargo') or '/private/tmp/fictrac-cargo/bin/cargo'))
    parser.add_argument('--target-dir', type=Path, default=ROOT / '.release-work/native-target/rekkefly')
    parser.add_argument('--verified-binary', type=Path, help='Explicit previously built unsigned executable; requires --binary-provenance and skips Cargo only after source/provenance verification')
    parser.add_argument('--binary-provenance', type=Path, help='Prior MATREX-NATIVE-PROVENANCE.json matching the selected binary and the pinned source/patch lock')
    args = parser.parse_args()
    if (args.verified_binary is None) != (args.binary_provenance is None):
        parser.error('--verified-binary and --binary-provenance must be supplied together')
    if platform.system() != 'Darwin' or platform.machine() != 'arm64':
        parser.error('The local correction is built and tested only for Mac arm64; official other-platform entries remain unchanged')
    lock, patch = load_lock()
    source = ROOT / '.integration/native-build' / ('rekkefly-' + local_tag(lock))
    prepare_source(source_repository(args.source_repo, lock), source, lock, patch)
    environment = os.environ.copy()
    environment['PATH'] = str(args.cargo.parent) + os.pathsep + environment.get('PATH', '')
    # Reuse this machine's already provisioned local toolchain when no normal
    # Rust installation is configured. Other machines use ordinary Cargo.
    for variable, candidate in [('CARGO_HOME', '/private/tmp/matrex-rekkefly-cargo'), ('RUSTUP_HOME', '/private/tmp/fictrac-rustup')]:
        if variable not in environment and Path(candidate).is_dir():
            environment[variable] = candidate
    binary = verified_binary(args.verified_binary, args.binary_provenance, lock) if args.verified_binary is not None else build(source, args.cargo, args.target_dir.resolve(), environment)
    result = stage(args.companions.resolve(), binary, lock, patch)
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
