#!/usr/bin/env python3
"""Validate boss ownership and Guard/Sword combat in a disposable project."""
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
parser.add_argument('--visible', action='store_true', help='Run Play Mode checks in a visible Editor when batch mode cannot advance frames')
parser.add_argument('--suite', choices=('all', 'isolation', 'guard', 'sw05', 'sword', 'rising', 'integration', 'player-cc', 'player24', 'audio', 'audio-release', 'tutorial-skip', 'rising-barrier', 'sword-refresh', 'sword-scar', 'build'), nargs='+', default=['all'])
args = parser.parse_args()
subprocess.run(['python3', '-m', 'unittest', 'discover', '-s', str(root / 'tests'),
                '-p', 'test_boss_boundaries.py'], check=True)
version = (root / 'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ')[1]
editor = args.unity or Path(f'/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity')
output = root / 'output/validation'
output.mkdir(parents=True, exist_ok=True)
suites = [('isolation', 'KarmaBossIsolationChecks', 'boss-isolation-validation.txt'),
          ('guard', 'KarmaGuardPlayChecks', 'guard-play-validation.txt'),
          ('sw05', 'KarmaSW05PlayChecks', 'sw05-play-validation.txt'),
          ('sword', 'KarmaSwordFinalPolishChecks', 'sword-play-validation.txt'),
          ('rising', 'KarmaRisingPlayChecks', 'rising-play-validation.txt'),
          ('integration', 'KarmaIntegrationChecks', 'integration.txt'),
          ('player-cc', 'KarmaPlayerCrowdControlChecks', 'player-cc.txt'),
          ('audio', 'KarmaSfxCompletionChecks', 'sfx-completion.txt'),
          ('audio-release', 'KarmaSfxCompletionChecks', 'sfx-release.txt'),
          ('tutorial-skip', 'KarmaCinematicSkipChecks', 'cinematic-skip.txt'),
          ('rising-barrier', 'KarmaRisingBarrierChecks', 'rising-barrier/result.txt'),
          ('sword-refresh', 'KarmaSwordSkillRefreshChecks', None),
          ('sword-scar', 'KarmaSwordSkillRefreshChecks', None),
          ('build', 'KarmaBuildChecks', 'build.txt'),
          ('player24', 'KarmaPlayer24Checks', 'player24/result.txt')]
with (nullcontext(str(args.work_project)) if args.work_project else tempfile.TemporaryDirectory(prefix='karma-boss-checks-')) as folder:
    project = Path(folder).resolve()
    if project == root or root in project.parents or project in root.parents:
        raise SystemExit('Validation project must be outside the source repository')
    marker = project / '.k4rma-validation-project'
    if project.exists() and any(project.iterdir()):
        if not marker.is_file() or marker.read_text().strip() != str(root):
            raise SystemExit('Refusing to overwrite a directory not created by this validation runner')
    if (project / 'Temp/UnityLockfile').exists():
        raise SystemExit('Close the validation Editor before replacing its project files')
    project.mkdir(parents=True, exist_ok=True)
    marker.write_text(str(root) + '\n')
    for name in ('Assets', 'Packages', 'ProjectSettings'):
        if (project / name).is_symlink():
            raise SystemExit(f'Validation {name} must not be a symbolic link')
    assets = project / 'Assets'
    if assets.exists():
        if assets.resolve() == (root / 'Assets').resolve():
            raise SystemExit('Validation Assets must not point at source Assets')
        shutil.rmtree(assets)
    for name in ('Assets', 'Packages', 'ProjectSettings'):
        shutil.copytree(root / name, project / name, dirs_exist_ok=True)
    (assets / 'Editor').mkdir(exist_ok=True)
    for check in (root / 'tests/Unity').rglob('*.cs'):
        shutil.copy2(check, assets / 'Editor' / check.name)
    failures = []
    selected = suites if 'all' in args.suite else [next(row for row in suites if row[0] == key)
                                                 for key in dict.fromkeys(args.suite)]
    if any(row[0] == 'player24' for row in selected[:-1]):
        raise SystemExit('Run player24 last: it rebuilds optional animation assets in the validation copy')
    for key, check, filename in selected:
        report = project / 'output/validation' / (filename or key + '.txt')
        report.unlink(missing_ok=True)
        destination = output / (filename or key + '.txt')
        destination.unlink(missing_ok=True)
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
            method = {'sword-refresh': 'RunShockAndDetonation', 'sword-scar': 'RunActiveSW03Only',
                      'audio-release': 'RunReleaseBatch'}.get(key, 'RunBatch')
            if key == 'audio-release':
                (project / 'output/validation/sfx-completion.txt').unlink(missing_ok=True)
            command = [str(editor), '-batchmode', '-projectPath', str(project),
                       '-executeMethod', f'{check}.{method}', '-logFile', str(log),
                       '-licensingIpc', channel]
            if args.visible and key not in ('isolation', 'build'):
                command.remove('-batchmode')
            if key == 'isolation':
                command.insert(2, '-nographics')
            started_at = time.time()
            try:
                result = subprocess.run(command, timeout=600)
            except subprocess.TimeoutExpired:
                failures.append(f'{key}: timed out after 600 seconds; read {log}')
                print(failures[-1], flush=True)
                continue
            if key == 'sword':
                reports = sorted(path for path in (project / 'output/final-sword-polish').glob('*/result.txt')
                                 if path.stat().st_mtime >= started_at)
                if reports:
                    report = reports[-1]
            if key == 'audio-release':
                report = project / 'output/validation/sfx-completion.txt'
            if key in ('sword-refresh', 'sword-scar') and result.returncode == 0:
                label = 'SHOCK AND FINAL DETONATION' if key == 'sword-refresh' else 'ACTIVE SW03'
                passed = f'REFRESH PASS: {label} CHECKS COMPLETE' in log.read_text()
                if passed:
                    report.parent.mkdir(parents=True, exist_ok=True)
                    report.write_text(f'PASS: {label} checks\n')
            if not report.exists():
                failures.append(f'{key}: no verdict; read {log}')
                print(failures[-1], flush=True)
                continue
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(report, destination)
            if key == 'sw05':
                for capture in report.parent.glob('sw05-*.png'):
                    shutil.copy2(capture, output / capture.name)
            print(report.read_text().strip(), flush=True)
            if result.returncode or not report.read_text().startswith('PASS:'):
                failures.append(f'{key}: failed; read {log}')
        finally:
            if licensing is not None:
                licensing.terminate()
                try:
                    licensing.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    licensing.kill()
                    licensing.wait()

    if failures:
        raise SystemExit('\n'.join(failures))
