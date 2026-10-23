#!/usr/bin/env python3
"""Generate render meshes that exclude adjacent poses inside overlapping rectangles.

Requires Pillow. Writes ONLY the slice plan, never the source PNGs or Unity .meta.
The Unity importer applies these outlines through ISpriteOutlineDataProvider.
"""
import json
from collections import defaultdict
from pathlib import Path
from PIL import Image, ImageFilter

root = Path(__file__).resolve().parents[1]
path = root / 'tools/sprite-slices.json'
plan = json.loads(path.read_text())


def simplify(points, tolerance=1.0):
    if len(points) < 3:
        return points
    ax, ay = points[0]
    bx, by = points[-1]
    dx, dy = bx - ax, by - ay
    denominator = dx * dx + dy * dy
    best, split = 0, 0
    for i, (x, y) in enumerate(points[1:-1], 1):
        t = max(0, min(1, ((x - ax) * dx + (y - ay) * dy) / denominator)) if denominator else 0
        distance = (x - ax - t * dx) ** 2 + (y - ay - t * dy) ** 2
        if distance > best:
            best, split = distance, i
    if best <= tolerance * tolerance:
        return [points[0], points[-1]]
    return simplify(points[:split + 1], tolerance)[:-1] + simplify(points[split:], tolerance)


def contour(alpha):
    width, height = alpha.size
    pixels = alpha.tobytes()
    remaining = {i for i, value in enumerate(pixels) if value >= 96}
    largest = set()
    while remaining:
        first = remaining.pop()
        component, queue = {first}, [first]
        while queue:
            i = queue.pop()
            x, y = i % width, i // width
            neighbors = []
            if x > 0: neighbors.append(i - 1)
            if x + 1 < width: neighbors.append(i + 1)
            if y > 0: neighbors.append(i - width)
            if y + 1 < height: neighbors.append(i + width)
            for n in neighbors:
                if n in remaining:
                    remaining.remove(n)
                    component.add(n)
                    queue.append(n)
        if len(component) > len(largest): largest = component
    mask = Image.new('L', (width, height))
    data = bytearray(width * height)
    for i in largest: data[i] = 255
    mask.frombytes(bytes(data))
    # Retain antialiased edge pixels, and bridge diagonal-only connections.
    data = mask.filter(ImageFilter.MaxFilter(7)).tobytes()
    edges = defaultdict(list)
    for i, value in enumerate(data):
        if not value: continue
        x, y = i % width, i // width
        if y == 0 or not data[i - width]: edges[x, y].append((x + 1, y))
        if x + 1 == width or not data[i + 1]: edges[x + 1, y].append((x + 1, y + 1))
        if y + 1 == height or not data[i + width]: edges[x + 1, y + 1].append((x, y + 1))
        if x == 0 or not data[i - 1]: edges[x, y + 1].append((x, y))
    loops = []
    while edges:
        first = next(iter(edges))
        current, points = first, [first]
        while current in edges:
            following = edges[current].pop()
            if not edges[current]: del edges[current]
            points.append(following)
            current = following
            if current == first: break
        if len(points) >= 4:
            area = abs(sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(points, points[1:])))
            loops.append((area, points))
    points = max(loops)[1]
    middle = len(points) // 2
    return simplify(points[:middle + 1])[:-1] + simplify(points[middle:])[:-1]


for sheet in plan['sheets']:
    if sheet['id'] not in ('Player', 'Neutral', 'Wave', 'Rise', 'Ward', 'Inheritance'):
        continue
    image = Image.open(root / sheet['source']).convert('RGBA')
    for sprite in sheet['sprites']:
        x, y, width, height = (sprite[key] for key in ('x', 'y', 'width', 'height'))
        sprite['outline'] = contour(image.crop((x, y, x + width, y + height)).getchannel('A'))
lines = ['{"sheets": [']
for index, sheet in enumerate(plan['sheets']):
    fields = {key: value for key, value in sheet.items() if key != 'sprites'}
    lines.append('  ' + json.dumps(fields, ensure_ascii=False)[:-1] + ', "sprites": [')
    for i, sprite in enumerate(sheet['sprites']):
        lines.append('    ' + json.dumps(sprite, ensure_ascii=False) + (',' if i + 1 < len(sheet['sprites']) else ''))
    lines.append('  ]}' + (',' if index + 1 < len(plan['sheets']) else ''))
lines.append(']}')
path.write_text('\n'.join(lines) + '\n')
print('Updated character/part render outlines in tools/sprite-slices.json')
