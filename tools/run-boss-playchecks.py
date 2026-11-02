#!/usr/bin/env python3
"""Validate boss ownership and existing Guard/SW05 behavior in a disposable project."""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile
import time
import uuid
from contextlib import nullcontext

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--work-project', type=Path)
parser.add_argument('--unity', type=Path, help='Override the Unity executable')
parser.add_argument('--licensing-ipc')
parser.add_argument('--suite', choices=('all', 'isolation', 'guard', 'sw05'), default='all')
args = parser.parse_args()
version = (root / 'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ')[1]
editor = args.unity or Path(f'/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity')
output = root / 'output/validation'
output.mkdir(parents=True, exist_ok=True)
suites = [('isolation', 'KarmaBossIsolationChecks', 'boss-isolation-validation.txt'),
          ('guard', 'KarmaGuardPlayChecks', 'guard-play-validation.txt'),
          ('sw05', 'KarmaSW05PlayChecks', 'sw05-play-validation.txt')]
with (nullcontext(str(args.work_project)) if args.work_project else tempfile.TemporaryDirectory(prefix='karma-boss-checks-')) as folder:
    project = Path(folder).resolve()
    if project == root or root in project.parents or project in root.parents:
        raise SystemExit('Validation project must be outside the source repository')
    assets = project / 'Assets'
    if assets.exists():
        if assets.resolve() == (root / 'Assets').resolve():
            raise SystemExit('Validation Assets must not point at source Assets')
        shutil.rmtree(assets)
    for name in ('Assets', 'Packages', 'ProjectSettings'):
        shutil.copytree(root / name, project / name, dirs_exist_ok=True)
    (assets / 'Editor').mkdir(exist_ok=True)
    for _, check, _ in suites:
        shutil.copy2(root / f'tests/Unity/{check}.cs', assets / f'Editor/{check}.cs')
    for key, check, filename in suites:
        if args.suite not in ('all', key):
            continue
        report = project / 'output/validation' / filename
        report.unlink(missing_ok=True)
        log = output / f'unity-boss-{key}.log'
        licensing = None
        channel = args.licensing_ipc
        try:
            # A client closes its pipe when the last Editor disconnects. Give each
            # suite its own client instead of reusing a closed or stale channel.
            if not channel:
                channel = 'LicenseClient-k4rma-' + uuid.uuid4().hex
                client = editor.parents[1] / 'Helpers/UnityLicensingClient.app/Contents/MacOS/Unity.Licensing.Client'
                licensing = subprocess.Popen([str(client), '--namedPipe', 'Unity-' + channel],
                                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                time.sleep(2)
            command = [str(editor), '-batchmode', '-projectPath', str(project),
                       '-executeMethod', f'{check}.RunBatch', '-logFile', str(log),
                       '-licensingIpc', channel]
            if key != 'sw05':
                command.insert(2, '-nographics')
            result = subprocess.run(command, timeout=600)
            if not report.exists():
                raise SystemExit(f'{key}: no verdict; read {log}')
            shutil.copy2(report, output / filename)
            if key == 'sw05':
                for capture in report.parent.glob('sw05-*.png'):
                    shutil.copy2(capture, output / capture.name)
            print(report.read_text().strip(), flush=True)
            if result.returncode or not report.read_text().startswith('PASS:'):
                raise SystemExit(f'{key}: failed; read {log}')
        finally:
            if licensing is not None:
                licensing.terminate()
                try:
                    licensing.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    licensing.kill()
                    licensing.wait()
