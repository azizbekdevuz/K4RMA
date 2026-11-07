"""Original Rising Slash ribbons/sparks; rerun from any working directory.

Preserves each existing PNG's dimensions, alpha format, Unity GUID and pivot.
Paths describe the sword sweep, not a hitbox; contact sparks are event-driven.
"""
from pathlib import Path
from math import sin, cos, pi
from PIL import Image, ImageDraw
import argparse
import os
from io import BytesIO
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--family', nargs='+', choices=['Rush','High','Low','Fin','Hit','Warn'])
selected = parser.parse_args().family

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/K4RMA/Arts/Bosses/RisingSlash/Effects'
S = 3


def polygon(draw, points, color):
    draw.polygon([(round(x*S), round(y*S)) for x, y in points], fill=color)


def ribbon(draw, cx, cy, rx, ry, start, end, width, color):
    outer, inner = [], []
    for i in range(65):
        t = i / 64
        a = (start + (end-start)*t) * pi / 180
        taper = sin(pi*t)**0.65
        outer.append((cx + rx*cos(a), cy + ry*sin(a)))
        inner.append((cx + (rx-width*taper)*cos(a), cy + (ry-width*taper)*sin(a)))
    polygon(draw, outer + inner[::-1], color)


def save(name, index, paint):
    if selected and name not in selected:
        return
    path = Path(os.path.relpath(ART / f'{name}{index}.png'))
    with Image.open(path) as source:
        size = source.size
    image = Image.new('RGBA', (size[0]*S, size[1]*S))
    paint(ImageDraw.Draw(image), *size)
    encoded = BytesIO()
    image.resize(size, Image.Resampling.LANCZOS).save(encoded, format='PNG')
    # Atomic replacement also works while an Editor holds an imported file open.
    staging = Path(os.path.relpath(ROOT / 'output'))
    staging.mkdir(exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=staging, suffix='.png', delete=False) as stream:
        stream.write(encoded.getvalue())
        temporary = stream.name
    try:
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def blade(name, count, cx_ratio, cy_ratio, rx_ratio, ry_ratio, sweeps, widths):
    for frame in range(count):
        def paint(d, w, h, frame=frame):
            # The stroke crosses the sprite, so scaling the frame to a hitbox
            # keeps the blade on the sword path instead of a canvas corner.
            cx, cy = w * cx_ratio, h * cy_ratio
            rx, ry = w * rx_ratio, h * ry_ratio
            start, end = sweeps[frame]
            width = widths[frame]
            alpha = [210, 255, 150, 80, 36][frame]
            color = (147, 215, 218, alpha)
            if frame < 3:
                ribbon(d, cx, cy, rx, ry, start, end, width, color)
                ribbon(d, cx, cy, rx * 0.9, ry * 0.88, start + 8, end - 10, max(4, width * 0.28), (255, 241, 205, alpha))
            else:
                span = (end - start) / 5
                for segment in range(4):
                    a0 = start + segment * span
                    ribbon(d, cx, cy, rx - segment * 4, ry - segment * 3, a0, a0 + 22, max(4, width), color)
        save(name, frame, paint)


# High: overhead crescent. Low: blade along the bottom. Fin: the full committed sweep.
blade('High', 4, 0.50, 0.60, 0.46, 0.40,
      [(206, 286), (196, 352), (248, 392), (286, 418)], [18, 30, 14, 7])
blade('Low', 4, 0.50, 0.76, 0.46, 0.30,
      [(18, 78), (8, 172), (28, 204), (48, 214)], [16, 26, 12, 6])
blade('Fin', 5, 0.48, 0.56, 0.47, 0.44,
      [(200, 278), (188, 356), (228, 396), (268, 424), (304, 442)], [20, 36, 16, 8, 4])
for i in range(6):
    def rush(d, w, h, i=i):
        # Rearward ribbons. The tail stays on the left edge so the wake
        # does not collapse into a blob at the leading tip.
        for j in range(3):
            y = h * (0.30 + j * 0.18)
            tail = 3 + j * 8
            tip = w * (0.48 + i * 0.09) - j * 10
            half = max(7, 14 - i - j * 2)
            polygon(d, [(tail, y - half * 0.25), (tip, y - half),
                        (tip - 16, y + 1), (tail, y + half * 0.35)],
                    (171, 210, 204, max(50, 220 - i * 26 - j * 20)))
        for j in range(3):
            x = w * (0.06 + j * 0.10) + i * 5
            y = h * (0.72 + j * 0.06)
            polygon(d, [(x, y), (x + 16, y - 5), (x + 28, y + 1), (x + 8, y + 6)],
                    (207, 186, 151, max(30, 140 - i * 18)))
    save('Rush', i, rush)
for i in range(5):
    def hit(d,w,h):
        cx,cy=w*.5,h*.5
        for j in range(7):
            a=(j*360/7+16)*pi/180
            inner=2+i*8; outer=[24,61,72,83,88][i]*(.6+(j%3)*.2)
            width=[7,5,3,2,1][i]
            polygon(d,[(cx+cos(a)*inner,cy+sin(a)*inner),
                       (cx+cos(a)*outer-sin(a)*width,cy+sin(a)*outer+cos(a)*width),
                       (cx+cos(a)*(outer+12),cy+sin(a)*(outer+12)),
                       (cx+cos(a)*outer+sin(a)*width,cy+sin(a)*outer-cos(a)*width)],
                       (255,231-j*4,174,max(0,255-i*53)))
        if i < 2:
            polygon(d,[(cx-6,cy),(cx,cy-13),(cx+7,cy),(cx,cy+10)],(255,255,239,255))
    save('Hit',i,hit)
for i in range(4):
    def warn(d,w,h):
        cx,cy=w*.5,h*.5; extent=[9,18,29,13][i]
        polygon(d,[(cx-extent,cy),(cx-3,cy-2),(cx,cy-extent*.6),(cx+3,cy-2),
                   (cx+extent,cy),(cx+3,cy+2),(cx,cy+extent*.6),(cx-3,cy+2)],(255,219,152,220))
        ribbon(d,cx,cy,extent*1.5,extent*1.2,195,320,2,(185,216,206,100))
    save('Warn',i,warn)
print('Replaced Rush/High/Low/Fin/Hit/Warn frames; dimensions and import metadata preserved.')
