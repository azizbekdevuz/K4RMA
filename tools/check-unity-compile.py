#!/usr/bin/env python3
"""Compile this Unity project.

The default run opens the repository in the installed Unity editor and fails if
that editor reports C# errors. A Roslyn command against a hand-built file list
is not full-project compilation and must not be reported as Safe Mode clearance.
Use --roslyn-only only as a local syntax check.
"""
import argparse
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument('--unity-contents', type=Path, help='Path to Unity.app/Contents (macOS) or Editor/Data (Windows)')
parser.add_argument('--unity-editor', type=Path, help='Unity executable. Defaults to the editor for ProjectSettings/ProjectVersion.txt')
parser.add_argument('--roslyn-only', action='store_true', help='Compile a script list with csc. This does not clear Safe Mode')
parser.add_argument('--log', type=Path, help='Editor log path')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
version = (root / 'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ')[1]


def editor_executable():
    if args.unity_editor:
        return args.unity_editor
    candidates = [
        Path(f'C:/Program Files/Unity/Hub/Editor/{version}/Editor/Unity.exe'),
        Path(f'/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity'),
    ]
    return next((path for path in candidates if path.exists()), None)


def compile_with_editor():
    editor = editor_executable()
    if editor is None:
        raise SystemExit(f'Unity {version} editor not found. Pass --unity-editor. Roslyn-only is not a substitute.')
    log = args.log or (root / 'output/validation/unity-editor-compile.log')
    log.parent.mkdir(parents=True, exist_ok=True)
    if log.exists():
        log.unlink()
    result = subprocess.run([
        str(editor), '-batchmode', '-nographics', '-quit',
        '-projectPath', str(root), '-logFile', str(log),
    ])
    text = log.read_text(encoding='utf-8', errors='replace') if log.exists() else ''
    errors = [line for line in text.splitlines() if ': error CS' in line]
    blocked = 'another Unity instance' in text or 'is already open' in text.lower()
    if blocked:
        raise SystemExit(f'Unity could not open this project because another editor has it open. Log: {log}')
    if result.returncode or errors or 'Scripts have compiler errors' in text:
        preview = '\n'.join(errors[:30]) or 'Unity exited without a clean script compile.'
        raise SystemExit(f'FAIL: full-project Unity compile\n{preview}\nLog: {log}')
    if 'Exiting batchmode successfully' not in text and 'Batchmode quit successfully' not in text:
        raise SystemExit(f'FAIL: Unity did not report a successful batch quit. Log: {log}')
    print(f'PASS: Unity {version} full-project compile. Safe Mode script errors were not reported.')
    print(f'Log: {log}')


if not args.roslyn_only:
    compile_with_editor()
    raise SystemExit(0)

contents = args.unity_contents
if contents is None:
    candidates = list(Path('/Applications/Unity/Hub/Editor').glob('*/Unity.app/Contents'))
    version = (root / 'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ')[1]
    contents = next((p for p in candidates if p.parts[-3] == version), None)
if contents is None:
    raise SystemExit('Unity not found. Pass --unity-contents /path/to/Unity.app/Contents')
sdk = contents / 'Resources/Scripting/DotNetSdk'
compiler = next((sdk / 'sdk').glob('*/Roslyn/bincore/csc.dll'))
refs = list((sdk / 'packs/NETStandard.Library.Ref').glob('*/ref/netstandard2.1/*.dll'))
refs += list((contents / 'Resources/Scripting/Managed/UnityEngine').glob('*.dll'))
input_system = root / 'Library/ScriptAssemblies/Unity.InputSystem.dll'
if not input_system.exists():
    raise SystemExit('Import the project in Unity first to compile the Input System package')
refs.append(input_system)
if not refs:
    raise SystemExit('Unity reference assemblies not found')
with tempfile.TemporaryDirectory(prefix='karma-compile-') as temp:
    for target in ('player', 'editor'):
        target_refs = refs + (list((contents / 'Managed').glob('UnityEditor*.dll')) if target == 'editor' else [])
        if target == 'editor':
            # Sprite Editor is supplied by the 2D Sprite package. Prefer this
            # project's compiled package, then the installed Editor template cache.
            sprite_editor = root / 'Library/ScriptAssemblies/Unity.2D.Sprite.Editor.dll'
            candidates = list((contents / 'Resources/PackageManager/ProjectTemplates/libcache').glob('*/ScriptAssemblies/Unity.2D.Sprite.Editor.dll'))
            if sprite_editor.exists():
                target_refs.append(sprite_editor)
            elif candidates:
                target_refs.append(candidates[0])
        sources = [p for p in (root / 'Assets/K4RMA').rglob('*.cs') if target == 'editor' or 'Editor' not in p.parts]
        flags = ['-nologo', '-target:library', '-langversion:9', '-warnaserror+', '-define:ENABLE_INPUT_SYSTEM', f'-out:{temp}/Karma.{target}.dll']
        if target == 'editor':
            flags += ['-define:UNITY_EDITOR,ENABLE_INPUT_SYSTEM']
        flags += [f'-r:{p}' for p in target_refs] + [str(p) for p in sources]
        response = Path(temp) / f'{target}.rsp'
        response.write_text('\n'.join(f'"{arg}"' for arg in flags))
        subprocess.run([str(sdk / 'dotnet'), str(compiler), '@' + str(response)], check=True)
        print(f'PASS: Roslyn {target} subset only. This does not prove the Unity project leaves Safe Mode.')
