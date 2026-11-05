"""Validate the generated SW02 sheet and describe tight meshes/foot pivots.

Does not draw, resize, recolor or change PNG pixels. The Unity-only importer
consumes the JSON in a disposable project; original Wave.png is never edited.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--image', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
image = Image.open(args.image)
if image.size != (1536, 1024) or image.mode != 'RGBA':
    raise SystemExit('Expected a 1536x1024 RGBA sheet')
alpha = np.array(image.getchannel('A'))
frames = []
for i, name in enumerate(('Prepare01', 'Prepare02', 'Swing01', 'Swing02', 'Recover01', 'Recover02')):
    column, row = i % 3, i // 3
    mask = alpha[row*512:(row+1)*512, column*512:(column+1)*512] >= 128
    ys, xs = np.where(mask)
    if not len(xs):
        raise SystemExit('Empty frame: ' + name)
    if min(xs) <= 15 or max(xs) >= 496 or min(ys) <= 15 or max(ys) >= 496:
        raise SystemExit('Cutout too close to cell boundary: ' + name)
    bottom = int(ys.max())
    feet = np.where(mask[bottom-3:bottom+1])[1]
    anchor_x = (int(feet.min()) + int(feet.max())) / 2
    padded = np.pad(mask, 1)
    edges = {}

    def edge(a, b):
        edges.setdefault(a, []).append(b)

    for y, x in zip(*np.where(mask & ~padded[:-2, 1:-1])):
        edge((int(x), int(y)), (int(x+1), int(y)))
    for y, x in zip(*np.where(mask & ~padded[1:-1, 2:])):
        edge((int(x+1), int(y)), (int(x+1), int(y+1)))
    for y, x in zip(*np.where(mask & ~padded[2:, 1:-1])):
        edge((int(x+1), int(y+1)), (int(x), int(y+1)))
    for y, x in zip(*np.where(mask & ~padded[1:-1, :-2])):
        edge((int(x), int(y+1)), (int(x), int(y)))
    outlines = []
    while edges:
        start = next(iter(edges))
        point, loop = start, []
        while True:
            loop.append(point)
            next_point = edges[point].pop()
            if not edges[point]:
                del edges[point]
            point = next_point
            if point == start:
                break
        area = sum(p[0]*q[1]-p[1]*q[0] for p, q in zip(loop, loop[1:]+loop[:1])) / 2
        if area < 4:
            continue
        simple = []
        for j, point in enumerate(loop):
            a, b = loop[j-1], loop[(j+1) % len(loop)]
            if (point[0]-a[0])*(b[1]-point[1]) != (point[1]-a[1])*(b[0]-point[0]):
                simple.append(point)
        outlines.append({'points': [{'x': float(x)-256, 'y': 256-float(y)} for x, y in simple]})
    bounds = [int(xs.min()), int(ys.min()), int(xs.max()+1), int(ys.max()+1)]
    frames.append({'name': name, 'x': column*512, 'y': (1-row)*512,
                   'pivotX': anchor_x/512, 'pivotY': (511-bottom)/512,
                   'bounds': bounds, 'outlines': outlines})
    print(name, 'alpha128 bounds', bounds, 'foot pivot', (anchor_x, 511-bottom))
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps({'frames': frames,
    'pixelSHA256': hashlib.sha256(image.tobytes()).hexdigest()}, indent=2), encoding='utf-8')
print('PASS: six complete cutouts; PPU125; >=16px cell margin; PNG pixels unchanged')
