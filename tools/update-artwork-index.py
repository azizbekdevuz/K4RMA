#!/usr/bin/env python3
"""Generate the human-readable current image list from the runtime catalog."""
import argparse
import csv
import io
import json
from pathlib import Path
import re
import struct

root = Path(__file__).resolve().parents[1]
art = root / 'Assets/K4RMA/Art'
parser = argparse.ArgumentParser()
parser.add_argument('--check', action='store_true')
args = parser.parse_args()
entries = json.loads((root / 'Assets/K4RMA/Resources/KarmaImageCatalog.json').read_text())['images']
buffer = io.StringIO(newline='')
writer = csv.writer(buffer, lineterminator='\n')
writer.writerow(['category', 'id', 'path', 'usage', 'poses', 'width', 'height', 'guid'])
lines = ['# 현재 게임 이미지 목록', '', f'현재 게임에서 사용하는 PNG **{len(entries)}개**. 이 목록과 CSV는 `python3 tools/update-artwork-index.py`로 갱신한다.', '',
         '기준 목록: `Assets/K4RMA/Resources/KarmaImageCatalog.json`. 이미지 원본은 아래 경로의 PNG 한 벌로 관리한다.', '',
         '플레이어: `Actions.png` 한 장에 웅크리기를 포함한 20개 동작. 검기는 캐릭터 포즈와 별도 VFX를 조합한다.', '']
for category in dict.fromkeys(e['category'] for e in entries):
    lines += [f'## {category}', '', '| 파일 | 사용처 | 포즈 |', '|---|---|---|']
    for entry in (e for e in entries if e['category'] == category):
        path = root / entry['path']
        width, height = struct.unpack('>II', path.read_bytes()[16:24])
        guid = re.search(r'^guid: (\w+)$', Path(str(path) + '.meta').read_text(), re.M).group(1)
        poses = entry.get('poses', [])
        writer.writerow([category, entry['id'], entry['path'], entry['usage'], len(poses), width, height, guid])
        relative = path.relative_to(art).as_posix()
        lines += [f"| [{relative}]({relative}) | {entry['usage']} | {len(poses) or '단일 이미지'} |"]
    lines += ['']
lines += ['## 이름으로 선택하는 포즈', '']
for entry in entries:
    if entry.get('poses'):
        lines += [f"- `{entry['id']}`: " + ', '.join(entry['poses'])]
outputs = {'IMAGE_INDEX.md': '\n'.join(lines) + '\n', 'IMAGE_INDEX.csv': buffer.getvalue()}
for name, content in outputs.items():
    path = art / name
    if args.check:
        if not path.exists() or path.read_text() != content:
            raise SystemExit(f'Stale image list: {path}')
    else:
        path.write_text(content)
print(f'PASS: current image index ({len(entries)} files)')
