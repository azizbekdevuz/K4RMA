"""Build reproducible rigid cutouts from canonical RisingSlash pixels.

Run without --apply for previews only. --apply writes the reviewed rig atlases
and Resources data, preserving existing asset GUIDs. Requires Pillow and numpy.
Source rects, silhouettes, pivots and PPU come from the existing Unity metadata.
"""
import argparse, json, math, re, uuid
from pathlib import Path
import numpy as np
from PIL import Image, ImageChops, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/K4RMA/Arts/Bosses/RisingSlash/Resources/K4RMAArtwork/Characters/Guardians"
DEST = ROOT / "Assets/K4RMA/Arts/Bosses/RisingSlash/Resources/RisingAnimation"
OUT = ROOT / "output/rising-animation"

def source(form, pose):
    rel = "Base/Rise.png" if form == "Rise" else "Inherited/" + form + ".png"
    path = SOURCE / rel
    text = path.with_suffix(".png.meta").read_text(encoding="utf8")
    block = re.search(r"      name: " + pose + r"\n(.*?)(?=\n    - serializedVersion:|\n    outline:)", text, re.S)[1]
    x,y,w,h = map(int, re.search(r"        x: (.*?)\n        y: (.*?)\n        width: (.*?)\n        height: (.*?)\n", block).groups())
    px,py = map(float, re.search(r"      pivot: {x: (.*?), y: (.*?)}", block).groups())
    image = Image.open(path).convert("RGBA")
    crop = image.crop((x,image.height-y-h,x+w,image.height-y))
    silhouette = Image.new("L", crop.size)
    outline = block.split("      outline:")[1].split("      physicsShape:")[0]
    for contour in outline.split("      - - "):
        points = [(float(a)+w/2,h/2-float(b)) for a,b in re.findall(r"{x: (.*?), y: (.*?)}", contour)]
        if len(points)>2: ImageDraw.Draw(silhouette).polygon(points,fill=255)
    if silhouette.getbbox():
        crop.putalpha(ImageChops.multiply(crop.getchannel("A"), silhouette))
    return crop, [w*px,h*(1-py)], float(re.search(r"spritePixelsToUnits: (.*)",text)[1])

def cutouts(rig):
    image, pivot, ppu = source(rig["form"],rig.get("sourcePose",rig["pose"]))
    pivot = rig.get("groundPivot", pivot)
    masks=[]
    for part in rig["parts"]:
        mask=Image.new("L",image.size)
        if part["polygon"]: ImageDraw.Draw(mask).polygon(part["polygon"],fill=255)
        masks.append(mask)
    remainder=next(i for i,p in enumerate(rig["parts"]) if not p["polygon"])
    occupied=Image.new("L",image.size)
    for i in reversed(range(len(masks))):
        if i==remainder:continue
        original=masks[i]
        masks[i]=ImageChops.subtract(original,occupied)
        occupied=ImageChops.lighter(occupied,original)
    union=Image.new("L",image.size)
    for i,mask in enumerate(masks):
        if i!=remainder: union=ImageChops.lighter(union,mask)
    masks[remainder]=ImageChops.invert(union)
    for i,part in enumerate(rig["parts"]):
        if part.get("cap",0)>0:
            x,y=part["pivot"];r=part["cap"]
            for idx in [i,part["parent"]]:
                if idx>=0: ImageDraw.Draw(masks[idx]).ellipse((x-r,y-r,x+r,y+r),fill=255)
    layers=[]
    for mask in masks:
        layer=image.copy()
        layer.putalpha(ImageChops.multiply(image.getchannel("A"),mask))
        layers.append(layer)
    return image,layers,pivot,ppu

def matrix(x,y,a=0):
    a=math.radians(a);c,s=math.cos(a),math.sin(a)
    return np.array([[c,-s,x],[s,c,y],[0,0,1.]])

def sample(rig,t):
    keys=rig["keys"];right=next((i for i in range(1,len(keys)) if t<=keys[i]["time"]),len(keys)-1)
    a,b=keys[right-1],keys[right];u=max(0,min(1,(t-a["time"])/(b["time"]-a["time"])))
    u=u*u*(3-2*u)
    return [x+(y-x)*u for x,y in zip(a["angles"],b["angles"])]

def render(rig,layers,pivot,ppu,t,size=640):
    scale=100/ppu
    image=Image.new("RGBA",(size,size),(38,40,48,255))
    angles=sample(rig,t);mats=[]
    # Every form uses its own ground pivot and PPU, never a shared pixel offset.
    view=matrix(size*.45-pivot[0]*scale,size*.80-pivot[1]*scale) @ np.diag([scale,scale,1])
    for i,part in enumerate(rig["parts"]):
        x,y=part["pivot"];parent=part["parent"]
        if parent<0: mat=matrix(x,y,angles[i])
        else:
            px,py=rig["parts"][parent]["pivot"];mat=mats[parent]@matrix(x-px,y-py,angles[i])
        mats.append(mat)
        inv=np.linalg.inv(view@mat@matrix(-x,-y))
        layer=layers[i].transform(image.size,Image.Transform.AFFINE,tuple(inv[:2].flatten()),Image.Resampling.BICUBIC)
        image.alpha_composite(layer)
    return image

def meta(path,contents):
    path=Path(str(path)+".meta")
    if not path.exists():
        contents="\n".join(line.rstrip() for line in contents.splitlines())+"\n"
        path.write_text("fileFormatVersion: 2\nguid: "+uuid.uuid4().hex+"\n"+contents,encoding="utf8")

def folders(path):
    path.mkdir(parents=True,exist_ok=True)
    while path!=ROOT/"Assets" and path.is_relative_to(ROOT/"Assets"):
        meta(path,"folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
        path=path.parent

def build(rig,apply):
    image,layers,pivot,ppu=cutouts(rig)
    name=rig["form"]+"_"+rig["pose"]
    frames=[render(rig,layers,pivot,ppu,i/24) for i in range(24)]
    frames[0].save(OUT/(name+".gif"),save_all=True,append_images=frames[1:],duration=[40,40,40,50,40,40]*4,loop=0,disposal=2)
    sheet=Image.new("RGBA",(640*4,640*2),(38,40,48,255))
    for i,n in enumerate([0,3,6,9,12,15,18,23]):sheet.alpha_composite(frames[n],((i%4)*640,(i//4)*640))
    sheet.resize((1280,640)).save(OUT/(name+"_contact.png"))
    if not apply:return
    # Tight individual parts keep memory bounded. Padding prevents bilinear bleed.
    atlas=Image.new("RGBA",(512,1024));x=y=4;row=0;entries=[]
    for i,(part,layer) in enumerate(zip(rig["parts"],layers)):
        box=layer.getbbox()
        if not box:raise ValueError(name+": empty "+part["name"])
        crop=layer.crop(box);w,h=crop.size
        if x+w+4>512:x=4;y+=row+8;row=0
        if y+h+4>1024:raise ValueError(name+": atlas overflow")
        atlas.alpha_composite(crop,(x,y))
        entries.append(dict(name=part["name"],parent=part["parent"],
            pivot=dict(x=part["pivot"][0],y=part["pivot"][1]),
            origin=dict(x=box[0],y=box[1]),
            rect=dict(x=x,y=1024-y-h,width=w,height=h)))
        x+=w+8;row=max(row,h)
    used_height=2 ** math.ceil(math.log2(y+row+4))
    atlas=atlas.crop((0,0,512,used_height))
    for entry in entries:entry["rect"]["y"]-=1024-used_height
    folders(DEST)
    path=DEST/(rig.get("atlas",name)+".png")
    if not path.exists() or Image.open(path).size!=atlas.size or Image.open(path).tobytes()!=atlas.tobytes():
        atlas.save(path,optimize=True)
    meta(path,"TextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 1\n  isReadable: 0\n  maxTextureSize: 1024\n  textureSettings:\n    filterMode: 1\n    aniso: 1\n    wrapU: 1\n    wrapV: 1\n  nPOTScale: 0\n  textureType: 0\n  textureShape: 1\n  alphaUsage: 1\n  alphaIsTransparency: 1\n  platformSettings:\n  - serializedVersion: 4\n    buildTarget: DefaultTexturePlatform\n    maxTextureSize: 1024\n    textureFormat: -1\n    textureCompression: 0\n    overridden: 0\n")
    data=dict(form=rig["form"],pose=rig["pose"],atlas=rig.get("atlas",name),sourcePose=rig.get("sourcePose",rig["pose"]),ppu=ppu,pivot=dict(x=pivot[0],y=pivot[1]),parts=entries,keys=rig["keys"])
    path=DEST/(name+".json");path.write_text(json.dumps(data,indent=2)+"\n",encoding="utf8")
    meta(path,"TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    print(name,len(entries),"parts",int(np.count_nonzero(np.asarray(image.getchannel("A")))),"source pixels")

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--apply",action="store_true")
    parser.add_argument("--form")
    parser.add_argument("--pose")
    args=parser.parse_args();OUT.mkdir(parents=True,exist_ok=True)
    rigs=json.loads((Path(__file__).with_name("rigs.json")).read_text(encoding="utf8"))
    for rig in rigs:
        if (args.form is None or rig["form"]==args.form) and (args.pose is None or rig["pose"]==args.pose):build(rig,args.apply)
if __name__=="__main__":main()
