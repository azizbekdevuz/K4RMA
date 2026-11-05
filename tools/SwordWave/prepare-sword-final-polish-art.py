"""Prepare versioned attack-only sprites; never rewrite character or old assets.
The crescent is generated with imagegen; the other sprites are authored here.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import json, math, random, shutil, uuid

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/K4RMA/Arts/Bosses/SwordWave/Effects/Variants'
OUT = ROOT / 'output/validation/sword-final-polish-20261102'
SOURCE = Path('C:/Users/곽현성/.codex/generated_images/01a19a48-0077-7712-8007-89789bf4c03b/exec-888deb9c-0b86-4f4c-a809-434d79b58f1d.png')
random.seed(1102)
ART.mkdir(parents=True, exist_ok=True)
OUT.mkdir(parents=True, exist_ok=True)
shutil.copyfile(SOURCE, ART / 'Sword_Crescent_v6.png')
stats = {}

def save(name, size, draw):
    im = Image.new('RGBA', size)
    d = ImageDraw.Draw(im)
    draw(d, *size)
    glow = im.filter(ImageFilter.GaussianBlur(7))
    glow.putalpha(glow.getchannel('A').point(lambda a: int(a * .22)))
    Image.alpha_composite(glow, im).save(ART / (name + '.png'))

BLUE = (130, 12, 24, 210)  # deep crimson outer edge
CYAN = (255, 55, 40, 255)  # scarlet filaments
WHITE = (255, 232, 220, 255)  # white-hot red core

def scar(d,w,h):
    points=[(32,128)]
    for x in range(64,w-32,32): points.append((x,128+random.randint(-17,17)))
    points.append((w-32,128))
    d.line(points,fill=BLUE,width=26,joint='curve');d.line(points,fill=CYAN,width=10,joint='curve');d.line(points,fill=WHITE,width=3)
    for i in range(2,len(points)-2,2):
        x,y=points[i]; sign=-1 if i%4 else 1
        branch=[(x,y),(x-18,y+sign*27),(x+7,y+sign*49),(x-12,y+sign*72)]
        d.line(branch,fill=BLUE,width=7);d.line(branch,fill=CYAN,width=3)
        d.polygon([(x+10,y-5),(x+48,y-7),(x+22,y-1)],fill=WHITE)

def rising(d,w,h):
    for x,top,width in [(67,170,32),(133,35,46),(197,240,27)]:
        hull=[(x-width,h-35),(x-width//2,top+140),(x,top),(x+width//2,top+170),(x+width,h-35),(x,h-160)]
        # Dense rounds retain a precise bright extent while the broad red
        # energy body lets the character and background remain readable.
        d.polygon(hull,fill=(130,12,24,72))
        d.line(hull+[hull[0]],fill=(255,45,32,150),width=1)
        d.polygon([(x-width//3,h-45),(x-5,top+140),(x,top),(x+10,top+170),(x+width//3,h-45)],fill=(255,55,40,190))
        d.line([(x-2,h-75),(x+2,top+105),(x,top+20)],fill=WHITE,width=5)
        for y in range(top+200,h-100,140):
            d.polygon([(x-width,y+50),(x-width//2,y),(x-width//3,y+65)],fill=(255,65,45,160))
    for i in range(16):
        x=random.randint(38,w-38);y=random.randint(240,h-60)
        d.polygon([(x,y),(x+3,y-40),(x+7,y-3),(x+2,y+16)],fill=(255,115,85,150))

def counter(d,w,h):
    d.polygon([(30,128),(198,76),(670,88),(986,128),(670,166),(198,180),(124,140)],fill=BLUE)
    d.polygon([(87,128),(310,106),(810,116),(986,128),(805,139),(300,151)],fill=CYAN)
    d.polygon([(160,128),(680,122),(986,128),(680,132)],fill=WHITE)
    for i in range(11):
        x=80+i*60;y=80 if i%2 else 174
        d.polygon([(x,y),(x+45,y+(-8 if i%2 else 8)),(x+13,y+5)],fill=(255,55,40,210))

def impact(d,w,h):
    cx,cy=w/2,h/2
    for angle in [35,145,215,325]:
        a=math.radians(angle);u=(math.cos(a),math.sin(a));n=(-u[1],u[0]);length=210
        p=[(cx-u[0]*28+n[0]*20,cy-u[1]*28+n[1]*20),(cx+u[0]*length,cy+u[1]*length),(cx-u[0]*28-n[0]*20,cy-u[1]*28-n[1]*20)]
        d.polygon(p,fill=BLUE); d.line([(cx,cy),(cx+u[0]*length,cy+u[1]*length)],fill=WHITE,width=5)
    for i in range(18):
        a=i*2.399;r=70+i*7;x=cx+math.cos(a)*r;y=cy+math.sin(a)*r
        d.polygon([(x-3,y),(x+math.cos(a)*26,y+math.sin(a)*26),(x+4,y+5)],fill=CYAN)
    d.ellipse((cx-12,cy-12,cx+12,cy+12),fill=WHITE)

def release(d,w,h):
    d.arc((90,90,w-90,h-90),-75,75,fill=BLUE,width=24)
    d.arc((90,90,w-90,h-90),-75,75,fill=WHITE,width=5)
    for i in range(10):
        y=90+i*34
        d.polygon([(70,y),(460,y+(256-y)*.4),(110,y+6)],fill=CYAN if i%3 else WHITE)

def ring(d,w,h):
    for angle in range(0,360,60):
        d.arc((52,52,w-52,h-52),angle+5,angle+48,fill=BLUE,width=12)
        d.arc((58,58,w-58,h-58),angle+5,angle+48,fill=CYAN,width=3)
        a=math.radians(angle);x=w/2+math.cos(a)*203;y=h/2+math.sin(a)*203
        d.line([(x,y),(x+math.cos(a)*26,y+math.sin(a)*26)],fill=WHITE,width=4)
    d.arc((82,82,w-82,h-82),20,150,fill=(255,55,40,150),width=3)
    d.arc((82,82,w-82,h-82),200,330,fill=(255,55,40,150),width=3)

save('SW03_GroundScar_v6',(1024,256),scar)
save('SW03_RisingBlade_v6',(256,1536),rising)
save('SW04_CounterBlade_v6',(1024,256),counter)
save('Sword_Impact_v6',(512,512),impact)
save('Sword_Release_v6',(512,512),release)
save('Sword_GuardRing_v6',(512,512),ring)
save('Sword_Shard_v6',(128,32),lambda d,w,h:d.polygon([(4,16),(38,10),(124,16),(38,22)],fill=WHITE))

for name in ['Sword_Crescent_v6','SW03_GroundScar_v6','SW03_RisingBlade_v6','SW04_CounterBlade_v6','Sword_Impact_v6','Sword_Release_v6','Sword_GuardRing_v6','Sword_Shard_v6']:
    im=Image.open(ART/(name+'.png')).convert('RGBA')
    alpha=im.getchannel('A');bbox=alpha.point(lambda a:255 if a>=128 else 0).getbbox()
    assert bbox and alpha.getextrema()[0]==0
    l,t,r,b=bbox;w,h=im.size
    stats[name]={'width':w,'height':h,'core':[l,h-b,r-l,b-t],'pivot':[(l+r)/2/w,(2*h-t-b)/2/h],'alphaRange':alpha.getextrema()}
    meta=ART/(name+'.png.meta')
    guid=next((s.split(': ')[1] for s in meta.read_text().splitlines() if s.startswith('guid: ')),None) if meta.exists() else uuid.uuid4().hex
    px,py=stats[name]['pivot']
    meta.write_text(f'''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  maxTextureSize: 2048
  textureSettings:
    filterMode: 1
    aniso: 1
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 9
  spritePivot: {{x: {px}, y: {py}}}
  spritePixelsToUnits: 100
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 100
    overridden: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    spriteID: {uuid.uuid5(uuid.NAMESPACE_URL,name).hex}
    internalID: 0
    secondaryTextures: []
    nameFileIdTable: {{}}
  userData: Sword attack polish; alpha128 core aligned to unchanged damage rectangle
  assetBundleName:
  assetBundleVariant:
''',encoding='utf8')
(OUT/'art-stats.json').write_text(json.dumps(stats,indent=2),encoding='utf8')
print(json.dumps(stats,indent=2))
