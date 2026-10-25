#!/usr/bin/env python3
"""Review-sized effect slices, including detached highlights and complete tails.

Only writes Sprite Editor input metadata. Source PNG pixels remain unchanged.
Requires Pillow; apply with tools/unity/ImportSpriteSheets.cs in the live Unity Editor.
"""
import json
from collections import defaultdict
from pathlib import Path
from PIL import Image, ImageFilter

root = Path(__file__).resolve().parents[1]
path = root / 'tools/sprite-slices.json'
plan = json.loads(path.read_text())
reviewed = {
    'GuardianVFX': [
        (0, 80, 480, 230), (480, 110, 390, 200), (835, 0, 330, 355), (1175, 0, 360, 355),
        (20, 350, 390, 335), (420, 350, 310, 335), (735, 445, 525, 190), (1200, 360, 335, 325),
        (0, 680, 475, 340), (445, 685, 320, 335), (775, 635, 375, 385), (1120, 825, 415, 90)],
    'PlayerVFX': [
        (16, 115, 375, 180), (400, 75, 405, 250), (800, 95, 430, 210), (1205, 0, 330, 355),
        (16, 365, 400, 310), (470, 360, 275, 310), (865, 335, 250, 345), (1185, 350, 335, 340),
        (85, 680, 315, 320), (335, 695, 505, 310), (820, 775, 455, 145), (1235, 695, 300, 300)],
    'PersonalizationVFX': [
        (0, 110, 640, 200), (550, 0, 270, 395), (800, 125, 450, 205), (1265, 65, 245, 280),
        (0, 455, 595, 190), (485, 395, 390, 255), (865, 325, 365, 330), (1215, 380, 320, 300),
        (0, 765, 675, 185), (600, 625, 315, 399), (850, 705, 375, 305), (1185, 645, 350, 375)],
    'HitVFX': [
        (40, 110, 355, 295), (415, 0, 570, 465), (880, 0, 490, 475), (1390, 110, 380, 325),
        (20, 490, 355, 315), (410, 500, 450, 315), (940, 490, 380, 355), (1370, 520, 400, 335)]}


def simplify(points, tolerance=0.8):
    if len(points) < 3:
        return points
    ax, ay = points[0]; bx, by = points[-1]
    dx, dy = bx - ax, by - ay
    denominator = dx * dx + dy * dy
    best, split = 0, 0
    for i, (x, y) in enumerate(points[1:-1], 1):
        t = max(0, min(1, ((x - ax) * dx + (y - ay) * dy) / denominator)) if denominator else 0
        distance = (x - ax - t * dx) ** 2 + (y - ay - t * dy) ** 2
        if distance > best: best, split = distance, i
    if best <= tolerance * tolerance:
        return [points[0], points[-1]]
    return simplify(points[:split + 1], tolerance)[:-1] + simplify(points[split:], tolerance)


def components(image):
    width, height = image.size
    remaining = {i for i, alpha in enumerate(image.getchannel('A').tobytes()) if alpha >= 48}
    result = []
    while remaining:
        first = remaining.pop(); queue, pixels = [first], []
        while queue:
            i = queue.pop(); pixels.append(i)
            x, y = i % width, i // width
            for n in (i - 1 if x else -1, i + 1 if x + 1 < width else -1,
                      i - width if y else -1, i + width if y + 1 < height else -1):
                if n in remaining: remaining.remove(n); queue.append(n)
        if len(pixels) < 5: continue
        xs = [i % width for i in pixels]; ys = [i // width for i in pixels]
        result.append((pixels, (min(xs), min(ys), max(xs) + 1, max(ys) + 1)))
    return result


def outlines(mask):
    width, height = mask.size; data = mask.tobytes(); edges = defaultdict(list)
    for i, value in enumerate(data):
        if not value: continue
        x, y = i % width, i // width
        if y == 0 or not data[i - width]: edges[x, y].append((x + 1, y))
        if x + 1 == width or not data[i + 1]: edges[x + 1, y].append((x + 1, y + 1))
        if y + 1 == height or not data[i + width]: edges[x + 1, y + 1].append((x, y + 1))
        if x == 0 or not data[i - 1]: edges[x, y + 1].append((x, y))
    paths = []
    while edges:
        first = next(iter(edges)); current, points = first, [first]
        while current in edges:
            following = edges[current].pop()
            if not edges[current]: del edges[current]
            points.append(following); current = following
            if current == first: break
        area = sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(points, points[1:]))
        # Ignore interior holes: the texture's own alpha already keeps them clear.
        if area < 16 or points[-1] != first: continue
        middle = len(points) // 2
        paths.append(simplify(points[:middle + 1])[:-1] + simplify(points[middle:])[:-1])
    if not paths: raise ValueError('Effect has no render outline')
    return paths


for sheet in plan['sheets']:
    if sheet['id'] not in reviewed: continue
    image = Image.open(root / sheet['source']).convert('RGBA'); width, height = image.size
    parts = components(image); boxes = reviewed[sheet['id']]
    primary = []
    for x, y, w, h in boxes:
        scores = [sum(x <= p % width < x + w and y <= p // width < y + h for p in pixels)
                  for pixels, bounds in parts]
        primary.append(max(range(len(parts)), key=lambda i: scores[i]))
    if len(set(primary)) != len(boxes): raise ValueError('Ambiguous reviewed effect: ' + sheet['id'])
    groups = [set(parts[i][0]) for i in primary]
    for i, (pixels, bounds) in enumerate(parts):
        if i in primary: continue
        cx, cy = (bounds[0] + bounds[2]) / 2, (bounds[1] + bounds[3]) / 2
        def distance(j):
            x0, y0, x1, y1 = parts[primary[j]][1]
            dx, dy = max(x0 - cx, 0, cx - x1), max(y0 - cy, 0, cy - y1)
            return dx * dx + dy * dy + 0.025 * ((cx - (x0 + x1) / 2) ** 2 + (cy - (y0 + y1) / 2) ** 2)
        groups[min(range(len(groups)), key=distance)].update(pixels)
    for sprite, pixels in zip(sheet['sprites'], groups):
        xs, ys = [p % width for p in pixels], [p // width for p in pixels]
        x0, y0 = max(0, min(xs) - 12), max(0, min(ys) - 12)
        x1, y1 = min(width, max(xs) + 13), min(height, max(ys) + 13)
        data = bytearray((x1 - x0) * (y1 - y0))
        for p in pixels: data[(p // width - y0) * (x1 - x0) + p % width - x0] = 255
        mask = Image.frombytes('L', (x1 - x0, y1 - y0), bytes(data)).filter(ImageFilter.MaxFilter(9))
        sprite.update(x=x0, y=y0, width=x1 - x0, height=y1 - y0, pivotX=0.5, pivotY=0.5,
                      outlines=outlines(mask))
        sprite.pop('outline', None)
        # Regression: no selected source pixel can be truncated by the slice.
        assert all(x0 <= p % width < x1 and y0 <= p // width < y1 for p in pixels)
        print(f"{sheet['id']}/{sprite['name']}: {x1 - x0} x {y1 - y0}, {len(sprite['outlines'])} contours")

lines = ['{"sheets": [']
for index, sheet in enumerate(plan['sheets']):
    fields = {key: value for key, value in sheet.items() if key != 'sprites'}
    lines.append('  ' + json.dumps(fields, ensure_ascii=False)[:-1] + ', "sprites": [')
    for i, sprite in enumerate(sheet['sprites']):
        lines.append('    ' + json.dumps(sprite, ensure_ascii=False) + (',' if i + 1 < len(sheet['sprites']) else ''))
    lines.append('  ]}' + (',' if index + 1 < len(plan['sheets']) else ''))
lines.append(']}'); path.write_text('\n'.join(lines) + '\n')
