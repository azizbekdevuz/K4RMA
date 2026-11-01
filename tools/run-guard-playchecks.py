#!/usr/bin/env python3
"""Run GD01–GD05 in a disposable Unity project without changing authored scenes."""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--unity', type=Path, help='Unity executable')
parser.add_argument('--licensing-ipc', help='Existing Licensing Client channel, without the Unity- prefix')
args = parser.parse_args()
version = (root / 'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ')[1]
editor = args.unity or Path(f'/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity')
if not editor.is_file():
    raise SystemExit('Pass --unity /path/to/Unity')
output = root / 'output/validation'
output.mkdir(parents=True, exist_ok=True)
log = output / 'unity-guard-play.log'
report = output / 'guard-play-validation.txt'
with tempfile.TemporaryDirectory(prefix='karma-guard-checks-') as folder:
    project = Path(folder)
    for name in ('Assets', 'Packages', 'ProjectSettings'):
        shutil.copytree(root / name, project / name)
    (project / 'Assets/Editor').mkdir(exist_ok=True)
    shutil.copy2(root / 'tests/Unity/KarmaGuardPlayChecks.cs', project / 'Assets/Editor/KarmaGuardPlayChecks.cs')
    command = [str(editor), '-batchmode', '-nographics', '-projectPath', str(project),
               '-executeMethod', 'KarmaGuardPlayChecks.RunBatch', '-logFile', str(log)]
    if args.licensing_ipc:
        command += ['-licensingIpc', args.licensing_ipc]
    # The check owns EditorApplication.Exit after its asynchronous Play Mode checks.
    result = subprocess.run(command, timeout=600)
    generated = project / 'output/validation/guard-play-validation.txt'
    if not generated.exists():
        raise SystemExit(f'Unity produced no verdict. Read {log}')
    shutil.copy2(generated, report)
    print(report.read_text().strip())
    if result.returncode or not report.read_text().startswith('PASS:'):
        raise SystemExit(f'Play checks failed. Read {log}')
    print(f'Log: {log}')
