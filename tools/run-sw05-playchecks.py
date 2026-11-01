#!/usr/bin/env python3
"""Run SW05 aerial sword rain in a disposable Unity project without changing authored scenes."""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile
from contextlib import nullcontext

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--work-project', type=Path, help='Reuse a disposable validation project and its import cache')
parser.add_argument('--unity', type=Path, help='Unity executable')
parser.add_argument('--licensing-ipc', help='Existing Licensing Client channel, without the Unity- prefix')
args = parser.parse_args()
version = (root / 'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ')[1]
editor = args.unity or Path(f'/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity')
if not editor.is_file():
    raise SystemExit('Pass --unity /path/to/Unity')
output = root / 'output/validation'
output.mkdir(parents=True, exist_ok=True)
log = output / 'unity-sw05-play.log'
report = output / 'sw05-play-validation.txt'
with (nullcontext(str(args.work_project)) if args.work_project else tempfile.TemporaryDirectory(prefix='karma-sw05-checks-')) as folder:
    project = Path(folder).resolve()
    if project == root or root in project.parents or project in root.parents:
        raise SystemExit('Validation project must be outside the source repository')
    # Retain the import cache, but never retain deleted/renamed source assets.
    copied_assets = project / 'Assets'
    if copied_assets.exists():
        if copied_assets.resolve() == (root / 'Assets').resolve():
            raise SystemExit('Validation Assets must not point at the source Assets')
        shutil.rmtree(copied_assets)
    for name in ('Assets', 'Packages', 'ProjectSettings'):
        shutil.copytree(root / name, project / name, dirs_exist_ok=True)
    (project / 'Assets/Editor').mkdir(exist_ok=True)
    shutil.copy2(root / 'tests/Unity/KarmaSW05PlayChecks.cs', project / 'Assets/Editor/KarmaSW05PlayChecks.cs')
    generated = project / 'output/validation/sw05-play-validation.txt'
    generated.unlink(missing_ok=True)
    command = [str(editor), '-batchmode', '-projectPath', str(project),
               '-executeMethod', 'KarmaSW05PlayChecks.RunBatch', '-logFile', str(log)]
    if args.licensing_ipc:
        command += ['-licensingIpc', args.licensing_ipc]
    # The check owns EditorApplication.Exit after its asynchronous Play Mode checks.
    result = subprocess.run(command, timeout=600)
    if not generated.exists():
        raise SystemExit(f'Unity produced no verdict. Read {log}')
    shutil.copy2(generated, report)
    for capture in (project / 'output/validation').glob('sw05-*.png'):
        shutil.copy2(capture, output / capture.name)
    print(report.read_text().strip())
    if result.returncode or not report.read_text().startswith('PASS:'):
        raise SystemExit(f'Play checks failed. Read {log}')
    print(f'Log: {log}')
