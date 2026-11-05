#!/usr/bin/env python3
"""Run the pilot checks against a disposable copy of a committed Git revision."""
import argparse
import datetime
import json
from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity', required=True, type=Path, help='Activated Unity 6000.3.16f1 executable')
    parser.add_argument('--revision', default='HEAD', help='Committed source revision to validate')
    parser.add_argument('--output', type=Path, help='New output directory (must not exist)')
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[1]
    unity = args.unity.expanduser().resolve()
    if not unity.is_file():
        parser.error(f'Unity executable not found: {unity}')
    revision = subprocess.check_output(['git', 'rev-parse', '--verify', f'{args.revision}^{{commit}}'], cwd=repo, text=True).strip()
    output = (args.output or repo / 'validation-output' / datetime.datetime.now().strftime('%Y%m%d-%H%M%S')).resolve()
    output.mkdir(parents=True, exist_ok=False)
    suites = [('runtime', 'PathPilotValidation.Run'), ('appearance', 'PathPilotAppearanceValidation.Run'), ('ui', 'PathPilotUiValidation.Run')]
    results = []
    with tempfile.TemporaryDirectory(prefix='matrex-path-pilot-') as temporary:
        project = Path(temporary) / 'project'
        project.mkdir()
        archive = Path(temporary) / 'source.tar'
        with archive.open('wb') as stream:
            subprocess.run(['git', 'archive', '--format=tar', revision], cwd=repo, stdout=stream, check=True)
        subprocess.run(['tar', '-xf', str(archive), '-C', str(project)], check=True)
        (project / 'ANT_PILOT_VALIDATION_COPY').write_text('Disposable validation source copy\n')
        for suite, method in suites:
            print(f'Running {suite} checks at {revision[:12]}', flush=True)
            command = [str(unity), '-batchmode', '-projectPath', str(project), '-executeMethod', method,
                       '-logFile', str(output / f'{suite}.log'), '--data-dir', str(output / 'recordings')]
            if suite == 'runtime':
                command += ['-pilot-output', str(project / 'validation-output/runtime')]
            if suite == 'appearance':
                command += ['-quit']
            try:
                process = subprocess.run(command, timeout=900)
                code = process.returncode
            except subprocess.TimeoutExpired:
                code = 'timeout'
            source = project / 'validation-output' / suite
            if source.exists():
                shutil.copytree(source, output / suite, dirs_exist_ok=True)
            report = output / suite / 'results.json'
            passed = code == 0 and report.is_file() and json.loads(report.read_text()).get('passed') is True
            results.append({'suite': suite, 'exitCode': code, 'passed': passed})
            if not passed:
                break
    (output / 'summary.json').write_text(json.dumps({'revision': revision, 'results': results}, indent=2) + '\n')
    print(f'Results: {output}', flush=True)
    return 0 if len(results) == len(suites) and all(item['passed'] for item in results) else 1


if __name__ == '__main__':
    raise SystemExit(main())
