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
    for name in ('Assets', 'Packages', 'ProjectSettings'):
        shutil.copytree(root / name, project / name)
    (project / 'Assets/Editor').mkdir(exist_ok=True)
    shutil.copy2(root / 'tests/Unity/KarmaBossPlayChecks.cs', project / 'Assets/Editor/KarmaBossPlayChecks.cs')
    result = subprocess.run([str(editor), '-batchmode', '-nographics', '-projectPath', str(project),
        '-executeMethod', 'KarmaBossPlayChecks.Run', '-logFile', str(log)], timeout=300)
    generated = project / 'boss-play-validation.txt'
    if generated.exists():
        shutil.copy2(generated, report)
        print(report.read_text().strip())
    else:
        raise SystemExit(f'Unity did not produce a verdict. Read {log}')
    if result.returncode or not report.read_text().startswith('PASS:'):
        raise SystemExit(f'Play checks failed. Read {log}')
    print(f'Log: {log}')
