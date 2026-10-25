#!/usr/bin/env python3
"""Build a pinned source revision in an isolated project and package desktop players.

Example: python3 tools/build_release.py --ref v0.3.0 --version 0.3.0 --platform all
Requires an activated matching Unity Editor and the selected Mono build modules.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
TARGETS = {
    'macOS': ('StandaloneOSX', 'OSXUniversal', 'MATREX VR.app'),
    'Windows': ('StandaloneWindows64', 'Win64', 'MATREX VR.exe'),
    'Linux': ('StandaloneLinux64', 'Linux64', 'MATREX VR.x86_64'),
}

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)

def prepare(ref, project):
    project.mkdir(parents=True, exist_ok=False)
    tracked = set(git('ls-tree', '-r', '--name-only', ref).decode().splitlines())
    if sys.platform == 'darwin':
        for folder in ('Assets', 'Packages', 'ProjectSettings'):
            subprocess.run(['cp', '-cR', str(ROOT / folder), str(project / folder)], check=True)
            for current, dirs, files in os.walk(project / folder, topdown=False):
                for name in files:
                    p = Path(current) / name
                    if p.relative_to(project).as_posix() not in tracked:
                        p.unlink()
                for name in dirs:
                    p = Path(current) / name
                    if not any(p.iterdir()):
                        p.rmdir()
        changed = [p for p in git('diff', '--no-renames', '--name-only', ref, '--', 'Assets', 'Packages', 'ProjectSettings').decode().splitlines() if p in tracked]
        for name in changed:
            p = project / name
            p.parent.mkdir(parents=True, exist_ok=True)
            if p.is_file():
                p.unlink()
        paths = changed
    else:
        paths = ['Assets', 'Packages', 'ProjectSettings']
    if paths:
        archive = subprocess.Popen(['git', 'archive', ref, '--', *paths], cwd=ROOT, stdout=subprocess.PIPE)
        try:
            subprocess.run(['tar', '-x', '-C', str(project)], stdin=archive.stdout, check=True)
        finally:
            archive.stdout.close()
        if archive.wait() != 0:
            raise RuntimeError('Could not extract pinned source revision')
    # The builder is intentionally injected into old tags without changing their source history.
    shutil.copy2(ROOT / 'Assets/Editor/MatrexReleaseBuilder.cs', project / 'Assets/Editor/MatrexReleaseBuilder.cs')
    assets = project / 'Assets/StreamingAssets'
    shutil.copy2(assets / 'Templates/system_config.template.json', assets / 'system_config.json')
    if sys.platform == 'darwin' and (ROOT / 'Library').is_dir():
        subprocess.run(['cp', '-cR', str(ROOT / 'Library'), str(project / 'Library')], check=True)
    (project / 'SOURCE-REVISION.txt').write_text(git('rev-parse', ref).decode())

def package(build, output, version, platform, revision, project, quickstart):
    label = 'macOS-universal' if platform == 'macOS' else platform
    name = f'MATREX-VR-{version}-{label}'
    stage = output / name
    stage.mkdir(parents=True, exist_ok=False)
    for p in build.iterdir():
        if 'DoNotShip' in p.name or p.suffix in ('.pdb', '.mdb'):
            continue
        if p.is_dir():
            shutil.copytree(p, stage / p.name, symlinks=True)
        else:
            shutil.copy2(p, stage / p.name)
    for p in stage.rglob('RunData'):
        if p.is_dir():
            shutil.rmtree(p)
    if platform == 'Linux':
        (stage / 'MATREX VR.x86_64').chmod(0o755)
    shutil.copy2(quickstart, stage / 'START-HERE.txt')
    for relative in ('Assets/ThirdParty/AstronomyEngine/LICENSE.txt', 'Assets/Resources/NightSky/source.txt'):
        source = project / relative
        if source.exists():
            target = stage / 'ThirdPartyNotices' / source.parent.name / source.name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
    report = json.loads((build / 'build-report.json').read_text())
    if report['result'] != 'Succeeded' or report['errors']:
        raise RuntimeError('Refusing to package an unsuccessful Unity build')
    manifest = dict(version=version, sourceRevision=revision, unityVersion='6000.3.16f1', platform=label,
                    publicSystemTemplate=True, builderSha256=hashlib.sha256((ROOT / 'Assets/Editor/MatrexReleaseBuilder.cs').read_bytes()).hexdigest(), build=report)
    (stage / 'release-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    archive = output / (name + '.zip')
    if platform == 'macOS' and sys.platform == 'darwin':
        subprocess.run(['/usr/bin/ditto', '-c', '-k', '--sequesterRsrc', '--keepParent', str(stage), str(archive)], check=True)
    else:
        with zipfile.ZipFile(archive, 'x', zipfile.ZIP_DEFLATED) as z:
            for p in stage.rglob('*'):
                if p.is_symlink():
                    info = zipfile.ZipInfo(p.relative_to(output).as_posix())
                    info.create_system = 3
                    info.external_attr = 0o120777 << 16
                    z.writestr(info, os.readlink(p))
                elif p.is_file():
                    z.write(p, p.relative_to(output))
    with zipfile.ZipFile(archive) as z:
        if z.testzip():
            raise RuntimeError('ZIP integrity failure')
        if any('/RunData/' in n or 'DoNotShip' in n for n in z.namelist()):
            raise RuntimeError('Recording/debug files found in public archive')
    digest = hashlib.sha256()
    with archive.open('rb') as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b''):
            digest.update(chunk)
    Path(str(archive) + '.sha256').write_text(digest.hexdigest() + '  ' + archive.name + '\n')
    print(archive, flush=True)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ref', default='HEAD')
    parser.add_argument('--version', required=True)
    parser.add_argument('--platform', choices=[*TARGETS, 'all'], default='all')
    parser.add_argument('--unity', type=Path)
    parser.add_argument('--workspace', type=Path)
    parser.add_argument('--quickstart', type=Path, default=ROOT / 'docs/release-quickstart.txt')
    args = parser.parse_args()
    if not re.fullmatch(r'\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?', args.version):
        parser.error('--version must be a semantic version such as 0.3.0')
    revision = git('rev-parse', '--verify', args.ref + '^{commit}').decode().strip()
    editor = args.unity or Path('/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity')
    if not editor.is_file() or not args.quickstart.is_file():
        parser.error('Supply an installed --unity Editor and an existing --quickstart text file')
    workspace = (args.workspace or ROOT / '.release-work' / ('release-' + args.version)).resolve()
    workspace.mkdir(parents=True, exist_ok=False)
    project = workspace / 'project'
    prepare(revision, project)
    for platform in (TARGETS if args.platform == 'all' else [args.platform]):
        target, switch, executable = TARGETS[platform]
        build = workspace / 'build' / platform
        build.mkdir(parents=True)
        log = workspace / (platform + '.log')
        subprocess.run([str(editor), '-batchmode', '-nographics', '-quit', '-projectPath', str(project), '-buildTarget', switch,
                        '-executeMethod', 'MatrexReleaseBuilder.Build', '-releaseVersion', args.version, '-releaseTarget', target,
                        '-releaseOutput', str(build / executable), '-logFile', str(log)], check=True)
        package(build, workspace, args.version, platform, revision, project, args.quickstart)

if __name__ == '__main__':
    main()
