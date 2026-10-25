#!/usr/bin/env python3
"""Run boss Play Mode regression checks in a disposable copy, leaving authored scenes untouched."""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--unity', type=Path, help='Unity executable')
parser.add_argument('--licensing-ipc', help='Use an existing Unity Licensing Client channel')
parser.add_argument('--output-dir', type=Path, default=root / 'output/validation')
args = parser.parse_args()
version = (root / 'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ')[1]
editor = args.unity or Path(f'/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity')
if not editor.exists():
    raise SystemExit('Unity executable not found. Pass --unity /path/to/Unity')
args.output_dir.mkdir(parents=True, exist_ok=True)
log = args.output_dir.resolve() / 'unity-boss-play.log'
report = args.output_dir.resolve() / 'boss-play-validation.txt'
with tempfile.TemporaryDirectory(prefix='karma-playchecks-') as folder:
    project = Path(folder)
    for name in ('Assets', 'Packages', 'ProjectSettings', 'image'):
        shutil.copytree(root / name, project / name)
    (project / 'Assets/Editor').mkdir(exist_ok=True)
    for name in ('KarmaBossPlayChecks.cs', 'KarmaMigrationChecks.cs'):
        shutil.copy2(root / 'tests/Unity' / name, project / 'Assets/Editor' / name)
    result = subprocess.run([str(editor), '-batchmode', '-nographics', '-projectPath', str(project),
        '-executeMethod', 'KarmaBossPlayChecks.Run', '-logFile', str(log)]
        + (['-licensingIpc', args.licensing_ipc] if args.licensing_ipc else []), timeout=720)
    generated = project / 'boss-play-validation.txt'
    if generated.exists():
        shutil.copy2(generated, report)
        print(report.read_text().strip())
    else:
        raise SystemExit(f'Unity did not produce a verdict. Read {log}')
    if result.returncode or not report.read_text().startswith('PASS:'):
        raise SystemExit(f'Play checks failed. Read {log}')
    print(f'Log: {log}')
