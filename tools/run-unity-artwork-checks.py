#!/usr/bin/env python3
"""Validate serialized artwork references in a clean Unity project.

--initialize creates the initial settings/slices and copies only those assets
back. Without it, a fresh import checks the exact files a teammate pulls.
"""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--initialize', action='store_true')
parser.add_argument('--unity', type=Path)
args = parser.parse_args()
version = (root / 'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ')[1]
editor = args.unity or Path(f'/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity')
if not editor.exists():
    raise SystemExit('Unity executable not found. Pass --unity /path/to/Unity')
output = root / 'output/validation/artwork'
output.mkdir(parents=True, exist_ok=True)
log = output / ('initialize.log' if args.initialize else 'fresh-import.log')
method = 'KarmaPrototype.Editor.KarmaArtworkSetup.InitializeAndValidate' if args.initialize else 'KarmaPrototype.Editor.KarmaArtworkValidation.Run'
with tempfile.TemporaryDirectory(prefix='karma-artwork-', ignore_cleanup_errors=True) as folder:
    project = Path(folder)
    for name in ('Assets', 'Packages', 'ProjectSettings'):
        shutil.copytree(root / name, project / name)
    if args.initialize and 'com.unity.2d.sprite' not in json.loads((project / 'Packages/manifest.json').read_text())['dependencies']:
        bootstrap = project / 'Assets/Editor/KarmaSpritePackageInstaller.cs'
        bootstrap.parent.mkdir(exist_ok=True)
        bootstrap.write_text('''using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
public static class KarmaSpritePackageInstaller
{
    static AddRequest request;
    static double deadline;
    public static void Install()
    {
        request = Client.Add("com.unity.2d.sprite");
        deadline = EditorApplication.timeSinceStartup + 180;
        EditorApplication.update += Poll;
    }
    static void Poll()
    {
        if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= Poll;
        if (request.IsCompleted && request.Status == StatusCode.Success)
        {
            Debug.Log("Installed " + request.Result.name + "@" + request.Result.version);
            EditorApplication.Exit(0);
        }
        else { Debug.LogError(request.Error?.message ?? "Package install timed out"); EditorApplication.Exit(1); }
    }
}
''')
        install_log = output / 'package-install.log'
        subprocess.run([str(editor), '-batchmode', '-nographics', '-projectPath', str(project),
            '-executeMethod', 'KarmaSpritePackageInstaller.Install', '-logFile', str(install_log)], check=True, timeout=240)
        for name in ('manifest.json', 'packages-lock.json'):
            shutil.copy2(project / 'Packages' / name, root / 'Packages' / name)
    result = subprocess.run([str(editor), '-batchmode', '-nographics', '-quit', '-projectPath', str(project),
        '-executeMethod', method, '-logFile', str(log)], timeout=240)
    verdict = project / 'artwork-validation.txt'
    if result.returncode or not verdict.exists() or not verdict.read_text().startswith('PASS:'):
        shutil.copy2(project / 'Assets/K4RMA/Resources/K4RMAArtwork/PlayerAnimation.png.meta', output / 'failed-player-import.txt')
        raise SystemExit(f'Artwork validation failed. Read {log}')
    if args.initialize:
        files = list((project / 'Assets/K4RMA/Resources/K4RMAArtwork').glob('*.png.meta'))
        files += [project / 'Assets/K4RMA/Resources/KarmaArtworkSettings.asset',
                  project / 'Assets/K4RMA/Resources/KarmaArtworkSettings.asset.meta']
        for source in files:
            shutil.copy2(source, root / source.relative_to(project))
    shutil.copy2(verdict, output / ('initialize.txt' if args.initialize else 'fresh-import.txt'))
    print(verdict.read_text().strip())
    print(f'Log: {log}')
