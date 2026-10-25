#!/usr/bin/env python3
"""Compile with installed Unity references and its bundled C# compiler; no package restore."""
import argparse
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument('--unity-contents', type=Path, help='Path to Unity.app/Contents (macOS)')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
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
        print(f'PASS: Unity {target} compile (warnings treated as errors)')
