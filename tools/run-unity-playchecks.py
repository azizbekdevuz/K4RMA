#!/usr/bin/env python3
"""Run boss Play Mode regression checks in a disposable copy, leaving authored scenes untouched."""
import argparse
from contextlib import nullcontext
import json
from pathlib import Path
import shutil
import subprocess
import os
import re
import tempfile

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--unity', type=Path, help='Unity executable')
parser.add_argument('--licensing-ipc', help='Use an existing Unity Licensing Client channel')
parser.add_argument('--output-dir', type=Path, default=root / 'output/validation')
parser.add_argument('--suite', choices=('play', 'cadence'), default='play',
    help='Run the integrated Play Mode checks (default) or the separate AI cadence checks')
parser.add_argument('--timeout', type=float, default=900,
    help='Maximum Editor runtime, including the isolated project import (seconds)')
parser.add_argument('--project-dir', type=Path,
    help='Optional reusable isolated project below output/validation; original project is never opened')
parser.add_argument('--runtime-source', type=Path,
    help='Cadence only: frozen Assets/K4RMA/Runtime below output/validation, copied into the isolated project')
args = parser.parse_args()
cadence = args.suite == 'cadence'
validation = (root / 'output/validation').resolve()
runtime_source = args.runtime_source.resolve() if args.runtime_source else None
if runtime_source:
    if not cadence:
        raise SystemExit('--runtime-source is supported only with --suite cadence')
    if (not runtime_source.is_relative_to(validation) or not runtime_source.is_dir()
            or runtime_source.parts[-3:] != ('Assets', 'K4RMA', 'Runtime')):
        raise SystemExit('--runtime-source must be a frozen Assets/K4RMA/Runtime below output/validation')
version = (root / 'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ')[1]
editor = args.unity or (Path(os.environ.get('ProgramFiles', 'C:/Program Files')) /
    f'Unity/Hub/Editor/{version}/Editor/Unity.exe' if os.name == 'nt' else
    Path(f'/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity'))
if not editor.exists():
    raise SystemExit('Unity executable not found. Pass --unity /path/to/Unity')
args.output_dir.mkdir(parents=True, exist_ok=True)
log = args.output_dir.resolve() / ('unity-cadence.log' if cadence else 'unity-boss-play.log')
report_name = 'wave-cadence-validation.json' if cadence else 'boss-play-validation.txt'
report = args.output_dir.resolve() / report_name
play_test_files = tuple(sorted(path.name for path in (root / 'tests/Unity').glob('*.cs')))
# Cadence has its own entry point and does not participate in the default suite.
test_files = ('KarmaWaveCadenceChecks.cs',) if cadence else play_test_files
for name in test_files:
    if not (root / 'tests/Unity' / name).is_file():
        raise SystemExit(f'Missing {args.suite} suite dependency: tests/Unity/{name}')
entry_point = 'KarmaWaveCadenceChecks.Run' if cadence else 'KarmaBossPlayChecks.Run'
if args.project_dir:
    isolated = args.project_dir.resolve()
    if not isolated.is_relative_to(validation) or isolated == validation:
        raise SystemExit('--project-dir must be a dedicated directory below output/validation')
    marker = isolated / '.karma-isolated-project'
    if isolated.exists() and any(isolated.iterdir()) and not marker.exists():
        raise SystemExit('Refusing to overwrite a directory not created by this runner')
    isolated.mkdir(parents=True, exist_ok=True)
    marker.write_text(str(root), encoding='utf-8')
    context = nullcontext(str(isolated))
else:
    context = tempfile.TemporaryDirectory(prefix='karma-playchecks-')
with context as folder:
    project = Path(folder).resolve()
    if runtime_source and (runtime_source.is_relative_to(project) or project.is_relative_to(runtime_source)):
        raise SystemExit('--runtime-source must be separate from the disposable project being overwritten')
    for name in ('Assets', 'Packages', 'ProjectSettings', 'image'):
        destination = project / name
        # Only generated source trees are replaced; imported Library data survives.
        # Resolve each recursive-delete target before touching it, including on Windows.
        if destination.exists():
            resolved = destination.resolve()
            if destination.is_symlink() or not resolved.is_relative_to(project) or resolved == project:
                raise SystemExit(f'Unsafe isolated source directory: {destination}')
            shutil.rmtree(destination)
        shutil.copytree(root / name, destination)
    if runtime_source:
        destination = project / 'Assets/K4RMA/Runtime'
        resolved = destination.resolve()
        if (destination.is_symlink() or not resolved.is_relative_to(project)
                or resolved == project or resolved.is_relative_to(root / 'Assets')):
            raise SystemExit(f'Unsafe isolated runtime destination: {destination}')
        shutil.rmtree(destination)
        shutil.copytree(runtime_source, destination)
        print(f'Runtime source (isolated cadence only): {runtime_source}')
    (project / 'Assets/Editor').mkdir(exist_ok=True)
    for name in test_files:
        shutil.copy2(root / 'tests/Unity' / name, project / 'Assets/Editor' / name)
    generated = project / report_name
    generated.unlink(missing_ok=True)  # A previous verdict can never satisfy this run.
    report.unlink(missing_ok=True)
    log.unlink(missing_ok=True)
    result = subprocess.run([str(editor), '-batchmode', '-nographics', '-projectPath', str(project),
        '-executeMethod', entry_point, '-logFile', str(log)]
        + (['-licensingIpc', args.licensing_ipc] if args.licensing_ipc else []), timeout=args.timeout)
    if generated.exists():
        shutil.copy2(generated, report)
    else:
        raise SystemExit(f'Unity did not produce a verdict. Read {log}')
    if not log.is_file():
        raise SystemExit('Unity did not produce a fresh Editor log')
    # A gameplay PASS must never hide an unhandled Editor/runtime exception.
    # The failure-path gameplay probes do not intentionally log exceptions.
    logged_error = re.search(r'(?m)^(?:[\w.+`]*Exception:|Assertion failed\b)|\berror CS\d{4}\b',
        log.read_text(encoding='utf-8', errors='replace'))
    if logged_error:
        raise SystemExit(f'Unity logged an unhandled error ({logged_error.group(0)}). Read {log}')
    verdict = report.read_text(encoding='utf-8')
    data = json.loads(verdict) if cadence else None
    passed = data.get('result') == 'PASS' if cadence else verdict.startswith('PASS:')
    if result.returncode or not passed:
        raise SystemExit(f'Play checks failed. Read {log}')
    if cadence:
        print(f'PASS: cadence, {len(data.get("stages", []))} rows, {data.get("assertions", 0)} assertions')
        print(f'Report: {report}')
    else:
        print(verdict.strip())
    print(f'Log: {log}')
