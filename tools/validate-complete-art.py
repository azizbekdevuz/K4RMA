#!/usr/bin/env python3
"""Check current image files, native metadata, and sprite import recipes."""
import json
from pathlib import Path
import struct
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
entries = json.loads((root / 'Assets/K4RMA/Resources/KarmaImageCatalog.json').read_text(encoding='utf-8'))['images']
plan = json.loads((root / 'tools/sprite-slices.json').read_text(encoding='utf-8'))['sheets']
expected = {e['path'] for e in entries}
actual = {p.relative_to(root).as_posix() for p in (root / 'Assets').rglob('*') if p.suffix.lower() in ('.png', '.jpeg', '.jpg')}
assert len({e['id'] for e in entries}) == len(entries) == len(expected) == 47
assert actual == expected, f'Image catalog mismatch: missing={expected-actual}, unused={actual-expected}'
by_path = {e['path']: e for e in entries}
for entry in entries:
    path = root / entry['path']
    assert path.read_bytes()[:8] == b'\x89PNG\r\n\x1a\n', path
    assert Path(str(path) + '.meta').is_file(), path
    assert entry['resource'] == entry['path'].split('/Resources/')[1][:-4], entry
assert len(plan) == 18 and sum(len(s['sprites']) for s in plan) == 170
for sheet in plan:
    assert sheet['source'] == sheet['asset'] in expected, sheet['id']
    entry = by_path[sheet['asset']]
    names = [s['name'] for s in sheet['sprites']]
    assert len(names) == len(set(names)) and names == entry['poses'], sheet['id']
    width, height = struct.unpack('>II', (root / sheet['asset']).read_bytes()[16:24])
    for sprite in sheet['sprites']:
        assert sprite['width'] > 0 and sprite['height'] > 0
        assert 0 <= sprite['x'] <= width-sprite['width'] and 0 <= sprite['y'] <= height-sprite['height'], sprite['name']
        assert 0 <= sprite['pivotX'] <= 1 and 0 <= sprite['pivotY'] <= 1
player = next(s for s in plan if s['id'] == 'Player')
assert len(player['sprites']) == 20 and 'Crouch' in {s['name'] for s in player['sprites']} and 'SwordWave' not in {s['name'] for s in player['sprites']}
assert len([e for e in entries if e['id'].startswith('inherited/') and len(e['poses']) == 16]) == 9
assert len([e for e in entries if e['id'].startswith('revival/') and len(e['poses']) == 6]) == 4
subprocess.run([sys.executable, str(root / 'tools/update-artwork-index.py'), '--check'], check=True)
print('PASS: 47 current images, 18 sprite sheets/170 poses, 9 inherited forms, 4 revival sheets; no retired images')
