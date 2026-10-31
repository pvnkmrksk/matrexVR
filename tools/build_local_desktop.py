#!/usr/bin/env python3
"""Build the current workspace as one stable local Mac app, without publishing a release."""
from pathlib import Path
import argparse
import json
import platform
import shutil
import subprocess
import sys
import shlex

ROOT = Path(__file__).resolve().parents[1]
UNITY = Path('/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity')


def run(*args, **kwargs):
    subprocess.run([str(a) for a in args], check=True, **kwargs)


def prepare(project, library):
    project = project.resolve()
    if project == ROOT or ROOT.is_relative_to(project) or any(project.is_relative_to(ROOT / name) for name in ('Assets', 'Packages', 'ProjectSettings')):
        raise ValueError('Use a disposable local build project outside source asset folders.')
    if project.exists() and any(project.iterdir()) and not (project / 'KANNADI_VALIDATION_COPY').is_file():
        raise ValueError('Refusing to replace an existing project without a diagnostic-copy marker.')
    project.mkdir(parents=True, exist_ok=True)
    (project / 'KANNADI_VALIDATION_COPY').write_text('Disposable local desktop build.\n')
    paths = subprocess.check_output(['git', 'ls-files', '--cached', '--others', '--exclude-standard', '-z'], cwd=ROOT).decode().split('\0')
    wanted = {p for p in paths if p.startswith(('Assets/', 'Packages/', 'ProjectSettings/')) and not p.startswith('Assets/RunData/')}
    for directory in ('Assets', 'Packages', 'ProjectSettings'):
        target = project / directory
        if target.exists():
            shutil.rmtree(target)
        run('cp', '-cR', ROOT / directory, target)
        for path in sorted(target.rglob('*'), reverse=True):
            if path.is_file() and path.relative_to(project).as_posix() not in wanted:
                path.unlink()
            elif path.is_dir() and not any(path.iterdir()):
                path.rmdir()
    if not (project / 'Library').exists() and library.is_dir():
        run('cp', '-cR', library, project / 'Library')
    (project / 'KANNADI_VALIDATION_COPY').write_text('Local desktop build; no source configuration writes.\n')
    assets = project / 'Assets/StreamingAssets'
    shutil.copy2(assets / 'Templates/system_config.template.json', assets / 'system_config.json')
    (assets / 'sequenceConfig.json').write_text('{"autoStart":false,"loop":false,"sequences":[]}\n')
    import build_identity
    revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    dirty = subprocess.check_output(['git', 'status', '--porcelain', '--untracked-files=all', '--', 'Assets', 'Packages', 'ProjectSettings'], cwd=ROOT, text=True).splitlines()
    (project / 'SOURCE-REVISION.txt').write_text(revision + '\n')
    build_identity.write(project, revision, 'working-tree-snapshot', dirty)


def stage_mcp_adapter(app):
    """Bundle the self-contained official-SDK adapter; the endpoint remains opt-in."""
    target = Path(app) / 'Contents/Resources/MCP'
    target.mkdir(parents=True, exist_ok=True)
    for name in ('matrex_control_mcp.py', 'matrex_control_mcp.py.lock'):
        shutil.copy2(ROOT / 'tools' / name, target / name)


def write_preview_entrypoints(app):
    """Put the local test app, editable config and launcher in one visible folder."""
    if app.parent.resolve() != (ROOT / 'Preview').resolve():
        return
    folder = app.parent
    (folder / 'Config').mkdir(exist_ok=True)
    (folder / 'RunData').mkdir(exist_ok=True)
    launcher = folder / 'Open MATREX VR.command'
    launcher.write_text('#!/bin/zsh\nset -e\npreview_dir="${0:A:h}"\n' +
        'open -n "$preview_dir"/' + shlex.quote(app.name) +
        ' --args --config-dir "$preview_dir/Config" --data-dir "$preview_dir/RunData" -configure\n')
    launcher.chmod(0o755)
    (folder / 'README.txt').write_text(
        'MATREX VR local development preview\n\n'
        'Double-click Open MATREX VR.command to use the visible Config and RunData folders.\n'
        'The app itself is MATREX VR.app; no Unity Editor is needed to run it.\n'
        'Config contains editable experiment/system/sequence JSONs. RunData contains new recordings.\n'
        'Rebuilding preserves Config and RunData.\n')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prepare-only', action='store_true')
    parser.add_argument('--project', type=Path, default=ROOT / '.local/project')
    parser.add_argument('--app', type=Path, default=ROOT / 'Preview/MATREX VR.app')
    parser.add_argument('--version', default='0.5.0-dev', help='Application version embedded in the local player and its recordings')
    parser.add_argument('--library', type=Path, default=ROOT / '.release-work/project-v0.3/Library')
    parser.add_argument('--skip-companions', action='store_true', help='Advanced development-only opt-out')
    parser.add_argument('--skip-native-camera-fix', action='store_true', help='Advanced development-only opt-out; bundled flight camera input then remains unavailable')
    parser.add_argument('--verified-native-binary', type=Path, help='Explicit previously built unsigned native correction, verified against --native-binary-provenance instead of invoking Cargo')
    parser.add_argument('--native-binary-provenance', type=Path, help='Prior MATREX-NATIVE-PROVENANCE.json for --verified-native-binary; source/patch integrity is still checked')
    parser.add_argument('--verified-hejjetrac-binary', type=Path, help='Explicit unsigned HejjéTrac adapter executable, paired with --hejjetrac-binary-provenance')
    parser.add_argument('--hejjetrac-binary-provenance', type=Path)
    parser.add_argument('--skip-hejjetrac-recording-control', action='store_true', help='Advanced opt-out: automatic HejjéTrac recording will be unavailable')
    parser.add_argument('--local-dashboard-archive', type=Path, default=ROOT / '.integration/local-dashboard/DaariDeepa-macos-arm64.zip')
    parser.add_argument('--skip-local-dashboard', action='store_true', help='Advanced development-only opt-out; uses the older published dashboard renderer')
    args = parser.parse_args()
    # Unity resolves relative output paths against its disposable project, rather
    # than the shell's directory. Normalize before passing paths across processes.
    args.project = args.project.resolve()
    args.app = args.app.resolve()
    args.library = args.library.resolve()
    args.local_dashboard_archive = args.local_dashboard_archive.resolve()
    if (args.verified_native_binary is None) != (args.native_binary_provenance is None):
        parser.error('--verified-native-binary and --native-binary-provenance must be supplied together')
    if args.verified_native_binary is not None and (args.skip_companions or args.skip_native_camera_fix or platform.machine() != 'arm64'):
        parser.error('Verified native reuse requires Apple Silicon companion packaging with the native camera correction enabled')
    if (args.verified_hejjetrac_binary is None) != (args.hejjetrac_binary_provenance is None):
        parser.error('--verified-hejjetrac-binary and --hejjetrac-binary-provenance must be supplied together')
    if args.verified_hejjetrac_binary is not None and (args.skip_companions or args.skip_hejjetrac_recording_control or platform.machine() != 'arm64'):
        parser.error('Verified HejjéTrac reuse requires the Mac arm64 recording adapter enabled')
    local_dashboard = platform.machine() == 'arm64' and not args.skip_companions and not args.skip_local_dashboard
    if local_dashboard and not args.prepare_only and not args.local_dashboard_archive.is_file():
        parser.error('Reviewed local dashboard archive is absent: '+str(args.local_dashboard_archive)+'. See tools/native_patches/README.md for the pinned source build.')
    prepare(args.project, args.library)
    if args.prepare_only:
        print(args.project)
        return
    args.app.parent.mkdir(parents=True, exist_ok=True)
    run(UNITY, '-batchmode', '-quit', '-projectPath', args.project,
        '-executeMethod', 'MatrexReleaseBuilder.Build', '-releaseTarget', 'StandaloneOSX',
        '-releaseVersion', args.version, '-releaseOutput', args.app, '-localDesktopBuild',
        '-logFile', ROOT / '.local/build.log')
    if not args.skip_companions:
        payload = args.app / 'Contents/Companions'
        if payload.exists():
            shutil.rmtree(payload)  # Immutable build output; calibration/installations live outside the app.
        run(sys.executable, ROOT / 'tools/companion_bundles.py', '--platform', 'macOS', '--output', args.app / 'Contents/Companions')
        if platform.machine() == 'arm64' and not args.skip_native_camera_fix:
            native_args = ['--verified-binary', args.verified_native_binary, '--binary-provenance', args.native_binary_provenance] if args.verified_native_binary is not None else []
            run(sys.executable, ROOT / 'tools/build_local_rekkefly.py', '--companions', args.app / 'Contents/Companions', *native_args)
        if platform.machine() == 'arm64' and not args.skip_hejjetrac_recording_control:
            hj_args = ['--verified-binary', args.verified_hejjetrac_binary, '--binary-provenance', args.hejjetrac_binary_provenance] if args.verified_hejjetrac_binary is not None else []
            run(sys.executable, ROOT / 'tools/build_local_hejjetrac.py', '--companions', args.app / 'Contents/Companions', *hj_args)
        dashboard_args = ['--local-archive', args.local_dashboard_archive] if local_dashboard else []
        run(sys.executable, ROOT / 'tools/stage_local_dashboard.py', '--output', args.app / 'Contents/Companions', *dashboard_args)
    stage_mcp_adapter(args.app)
    # Seal after embedding. Upstream tracker signatures are retained; no recursive re-signing.
    run('/usr/bin/codesign', '--force', '--sign', '-', '--timestamp=none', args.app)
    run('/usr/bin/codesign', '--verify', '--strict', args.app)
    write_preview_entrypoints(args.app)
    print('Local app:', args.app)
    if args.app.parent == (ROOT / 'Preview').resolve():
        print('Double-click:', args.app.parent / 'Open MATREX VR.command')
    print('Build log:', ROOT / '.local/build.log')


if __name__ == '__main__':
    main()
