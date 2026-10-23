#!/usr/bin/env python3
"""Check complete artwork source coverage and Sprite rectangles without launching Unity."""
from pathlib import Path
import json
import re
import struct
import subprocess
import sys
root=Path(__file__).resolve().parents[1]
registry=(root/'Assets/K4RMA/Runtime/Presentation/KarmaArtSources.cs').read_text()
files=re.findall(r'^\s*"(K4RMA_[^"]+\.(?:png|jpeg|jpg))",$',registry,re.M)
actual={p.relative_to(root/'image').as_posix() for p in (root/'image').rglob('*') if p.suffix.lower() in ('.png','.jpeg','.jpg')}
assert len(files)==len(set(files)) and set(files)==actual, 'Source registry differs from image originals'
dimensions={}
for relative in files:
    path=root/'image'/relative
    if path.suffix=='.png':
        header=path.read_bytes()[:24]
        assert header[:8]==b'\x89PNG\r\n\x1a\n',relative
        dimensions[relative]=struct.unpack('>II',header[16:24])
index=json.loads((root/'image/K4RMA_Production_Art_v1/00_ArtDirection/image-file-index.json').read_text())
for item in index:
    key='K4RMA_Production_Art_v1/'+item['file']
    if key in dimensions: assert tuple(item['size'])==dimensions[key],key+' dimensions changed; review atlas rectangles'
mapping={"Player":"01_Player/player-keyposes.png","Neutral":"02_Guardians/guardian-neutral-keyposes.png","Wave":"02_Guardians/guardian-wave-keyposes.png","Rise":"02_Guardians/guardian-rise-keyposes.png","Ward":"02_Guardians/guardian-ward-keyposes.png","PlayerFX":"04_VFX/player-attack-vfx.png","GuardianFX":"04_VFX/guardian-attack-vfx.png","PersonalFX":"04_VFX/personalization-vfx.png","Hit":"04_VFX/hit-vfx.png","Props":"06_Props/props-foreground-atlas.png","Hud":"07_UI/ui-hud-atlas.png"}
source=(root/'Assets/K4RMA/Runtime/Presentation/KarmaArtAtlas.cs').read_text()
count=0
for name,relative in mapping.items():
    match=re.search(r'static readonly Rect\[\] '+name+r'Rects=\{(.*?)\};',source,re.S)
    assert match,name+' missing'
    width,height=dimensions['K4RMA_Production_Art_v1/'+relative]
    for literal in re.findall(r'new Rect\(([^)]+)\)',match[1]):
        x,y,w,h=map(float,literal.split(','))
        assert x>=0 and y>=0 and w>0 and h>0 and x+w<=width and y+h<=height,(relative,literal)
        count+=1
# Runtime-generated regular regions are limited to the deliberately uniform icon/variant layouts.
assert dimensions['K4RMA_Production_Art_v1/07_UI/skill-icons-lineart.png']==(1536,1024)
assert dimensions['K4RMA_Production_Art_v1/03_Variants/guardian-inheritance-atlas.png']==(1536,1024)
subprocess.run([sys.executable,str(root/'tools/validate-source.py')],check=True)
print(f'PASS: {len(files)} source images, {count} reviewed rectangles, icon/variant layouts')
