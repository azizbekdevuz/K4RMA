#!/usr/bin/env python3
"""Validate Unity script metadata without importing or modifying the project."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
assets = root / 'Assets'
guids = {}
for meta in assets.rglob('*.meta'):
    match = re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text(), re.M)
    if not match:
        raise SystemExit(f'Missing GUID: {meta.relative_to(root)}')
    guid = match.group(1)
    if guid in guids:
        raise SystemExit(f'Duplicate GUID: {meta} and {guids[guid]}')
    guids[guid] = meta
for script in assets.rglob('*.cs'):
    if not Path(str(script) + '.meta').exists():
        raise SystemExit(f'Missing script metadata: {script}')
for asset in assets.rglob('*'):
    if asset.suffix not in ('.unity', '.prefab', '.asset', '.mat'):
        continue
    for guid in re.findall(r'guid: ([a-f0-9]{32})', asset.read_text()):
        if guid not in guids and not guid.startswith('0000000000000000'):
            raise SystemExit(f'Unknown serialized asset reference {guid}: {asset}')
print(f'PASS: {len(list(assets.rglob("*.cs")))} scripts, {len(guids)} unique asset GUIDs')
