"""Offline SW01/SW02 asset, blade-socket and event contract checks."""
import re
import unittest
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
VISUAL = ROOT / "Assets/Scripts/SwordWave/Visuals/KarmaSwordWaveAnimation.cs"
MOTIONS = ROOT / "Assets/Arts/Resources/Motions12"
FORMS = ("Wave", "WaveRise", "WaveWard", "WaveFinal")


def motion_catalog():
    rows = [line.split("\t") for line in (MOTIONS / "Catalog.txt").read_text().splitlines()]
    return {(row[0], row[1]): row for row in rows}


def blade_endpoints():
    rows = [line.split("\t") for line in (MOTIONS / "BladeSockets.txt").read_text().splitlines()]
    return {key: tuple(map(int, coords)) for key, *coords in rows}


class EnergyChecks(unittest.TestCase):
    def test_charge_mesh_clips_at_both_walls_without_moving_anchor(self):
        from test_sword_presentation import method, run_cs
        from test_sw05_eruption_mesh import SHIM
        source = VISUAL.read_text(encoding="utf-8-sig")
        shim = SHIM.split("static class KarmaSW05RainBlade", 1)[0]
        code = shim + "static class KarmaStage {public const float ArenaLeft=-15,ArenaRight=55;}"
        code += r"""
class ClipProbe {
 const int VerticesPerStroke=52;int stroke=4;
 Vector3 center;Vector3[] vertices=new Vector3[208];Color[] colors=new Color[208];
 CLIP
 public static void Run(){var p=new ClipProbe();
  foreach(float x in new[]{-15f,-14f,20f,54f,55f})foreach(int facing in new[]{-1,1}){
   p.center=new Vector3(x,3,0);
   for(int frame=0;frame<24;frame++){
    for(int i=0;i<208;i++){p.vertices[i]=new Vector3(facing*(i/207f*16-8),i*.01f,0);p.colors[i]=new Color(.1f,.5f,1,1);}
    p.ClipChargeToArena();
    for(int i=0;i<208;i++){var v=p.vertices[i];if(v.x+x<-15.00001f||v.x+x>55.00001f)throw new Exception("electricity behind wall");
     if(Math.Abs(v.y-i*.01f)>.00001f)throw new Exception("charge detached vertically");
     if(v.x+x>-14.8f&&v.x+x<54.8f&&p.colors[i].a<.999f)throw new Exception("interior charge dimmed");}
    if(p.center.x!=x)throw new Exception("weapon anchor moved");
   }
  }Console.WriteLine("PASS 240 charge wall frames");
 }}"""
        code = code.replace("CLIP", method(source, "void ClipChargeToArena()"))
        self.assertIn("PASS 240 charge wall frames", run_cs(code, "ClipProbe", modern=True))


    def test_transparent_dedicated_projectile(self):
        from test_sword_presentation import method
        source = VISUAL.read_text(encoding="utf-8-sig")
        drawing_ids = re.search(r"FlightDrawings = \{ ([\d, ]+) \}", source)[1]
        library = (ROOT / "Assets/Data/Resources/BossArtwork/KarmaSwordBossSprites.asset").read_text()
        group = library.split("  - id: SwordBossVFX\n", 1)[1].split("  - id:", 1)[0]
        references = re.findall(r"fileID: (-?\d+), guid: (\w+)", group)
        poses = group.split("    poses:\n", 1)[1].splitlines()
        pose_refs = dict(zip((line.strip()[2:] for line in poses), references))
        sheet = ROOT / "Assets/Arts/SwordWave/Effects/SW01_SW02_EnergyFrames.png"
        meta = Path(str(sheet) + ".meta").read_text()
        guid = re.search(r"^guid: (.+)$", meta, re.M)[1]
        with Image.open(sheet) as image:
            self.assertEqual(image.mode, "RGBA")
            for frame in map(int, drawing_ids.split(",")):
                name = f"SwordEnergy_SW02Flight_{frame}"
                sprite = meta.split("      name: " + name + "\n", 1)[1].split("    - serializedVersion", 1)[0]
                file_id = re.search(r"internalID: (\d+)", sprite)[1]
                self.assertEqual(pose_refs[name], (file_id, guid))
                x, y, w, h = map(int, re.findall(r"^        (?:x|y|width|height): (\d+)$", sprite, re.M)[:4])
                alpha = image.crop((x, image.height-y-h, x+w, image.height-y)).getchannel("A")
                self.assertLess(alpha.getpixel((0, 0)), 8)
                self.assertGreater(sum(alpha.histogram()[220:]), w * h * 0.05)
        self.assertIn("Paint(clip, FlightDrawings[", method(source, "void ApplyFlight()"))

    def test_sockets_touch_each_authored_blade(self):
        catalog = motion_catalog()
        images = {}
        for key, (x1, y1, x2, y2) in blade_endpoints().items():
            form, frame = key.split("/")
            motion, index = frame.rsplit("_", 1)
            row = catalog[form, motion]
            cols, _, _, width, height = map(int, row[2:7])
            if (form, motion) not in images:
                with Image.open(ROOT / "Assets/Arts/SwordWave" / form / (motion + ".png")) as sheet:
                    images[form, motion] = sheet.convert("RGBA")
            image = images[form, motion]
            for x, y in ((x1, y1), (x2, y2)):
                px = int((int(index) % cols + x / 96) * width)
                py = int((int(index) // cols + y / 96) * height)
                patch = image.crop((px-width//36, py-height//36, px+width//36, py+height//36))
                pixels = (patch.getpixel((x, y)) for x in range(patch.width) for y in range(patch.height))
                steel = max((min(pixel[:3]) for pixel in pixels if pixel[3] > 90), default=0)
                self.assertGreaterEqual(steel, 140, (key, x, y))

    def test_speed_and_original_warning_contract(self):
        sw02 = (ROOT / "Assets/Scripts/SwordWave/Patterns/SW02.cs").read_text(encoding="utf-8-sig")
        self.assertIn("return Mathf.Max(0.1f, Settings.swordSpeed * 1.25f * 1.3f);", sw02)
        self.assertIn("TrackingSeconds = 0.34f, ReleaseSeconds = 0.10f", sw02)
        self.assertIn("yield return TrackWarning(marker, warning)", sw02)
        self.assertIn("Settings.aimedDamageMultiplier", sw02)
        sw01 = (ROOT / "Assets/Scripts/SwordWave/Patterns/SwordWaveVolley.cs").read_text(encoding="utf-8-sig")
        self.assertIn("Mathf.Max(KarmaSpace.Scale, owner.Tuning.sword.swordSpeed * 1.3f)", sw01)
        self.assertIn("owner.Warning(0.65f) : Mathf.Max(0.42f, 0.54f * owner.Tempo)", sw01)
        self.assertIn("else yield return TrackSW01(owner, sword, high, mark, waveSize, warning, advertised)", sw01)

    def test_charge_uses_blade_and_actual_release(self):
        source = VISUAL.read_text(encoding="utf-8-sig")
        charge = source.split("void Charge(float t)", 1)[1].split("void Flight()", 1)[0]
        self.assertIn("ChargeCore(at, axis, radius", charge)
        self.assertNotIn("Glint(", charge)
        self.assertIn("2.1f - u * 1.25f", charge)
        self.assertIn("Mathf.Lerp(0.15f, 0.4f, t)", charge)
        self.assertIn("Mathf.Lerp(0.4f, 1, t)", charge)
        self.assertIn("picture.enabled = false", source.split("void ApplyCharge()", 1)[1])
        release = source.split("public static void Release(", 1)[1].split("public static void Impact(", 1)[0]
        self.assertIn("StopCharge(owner)", release)
        self.assertIn("BladePoints(renderer", release)

    def test_basic_ranged_shares_charge_and_melee_uses_own_warning(self):
        from test_sword_presentation import method
        source = (ROOT / "Assets/Scripts/SwordWave/Patterns/SWBasic.cs").read_text(encoding="utf-8-sig")
        execute = method(source, "public override IEnumerator Execute()")
        self.assertIn("KarmaSwordWaveAnimation.Charge(", execute)
        self.assertIn("basicRanged: true", execute)
        self.assertIn("owner.PathMark(origin, origin + velocity * flightLife, sword.BasicSwordSize)", execute)
        self.assertIn("owner.animator.PrepareBasicAttack(warning, false)", execute)
        self.assertIn("yield return owner.Wait(warning)", execute)
        self.assertNotIn("KarmaSwordBasicPresentation.Charge(", execute)
        self.assertIn("KarmaSwordAttackPolish.Warning(returnMark)", method(source, "IEnumerator Melee()"))
        self.assertNotIn("FlowWarning(", method(source, "IEnumerator Melee()"))

    def test_restored_speed_boundary_life_and_reach(self):
        from test_sword_presentation import method, run_cs
        sw02 = (ROOT / "Assets/Scripts/SwordWave/Patterns/SW02.cs").read_text(encoding="utf-8-sig")
        geometry = (ROOT / "Assets/Scripts/SwordWave/Patterns/KarmaBossProjectileGeometry.cs").read_text(encoding="utf-8-sig")
        contact = (ROOT / "Assets/Scripts/Core/KarmaBossRules.cs").read_text(encoding="utf-8-sig")
        behavior = (ROOT / "Assets/Scripts/SwordWave/KarmaSwordBossBehavior.cs").read_text(encoding="utf-8-sig")
        code = r'''
using System;
public struct Vector2{
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public float sqrMagnitude{get{return x*x+y*y;}}public Vector2 normalized{get{return this*(1/(float)Math.Sqrt(sqrMagnitude));}}
 public static Vector2 right{get{return new Vector2(1,0);}}
 public static Vector2 operator+(Vector2 a,Vector2 b){return new Vector2(a.x+b.x,a.y+b.y);}public static Vector2 operator-(Vector2 a,Vector2 b){return new Vector2(a.x-b.x,a.y-b.y);}public static Vector2 operator*(Vector2 a,float s){return new Vector2(a.x*s,a.y*s);}
}
struct Bounds{public Vector2 center,extents;}
static class Mathf{public static float Max(float a,float b){return Math.Max(a,b);}public static float Min(float a,float b){return Math.Min(a,b);}}
static class KarmaSpace{public static float Units(float n){return n*2;}}
static class KarmaStage{public const float ArenaLeft=0,ArenaRight=80;}
class SettingsData{public float swordSpeed=32;}
class Player{public Vector2 Position;}
class Game{public Player Player=new Player();}
class Owner{public Game game=new Game();public bool Enraged;}
BOX
GEOMETRY
public class SpeedProbe{
 SettingsData Settings=new SettingsData();Owner owner=new Owner();
 Bounds SwordPlayerBounds{get{return new Bounds{center=owner.game.Player.Position,extents=new Vector2(.6f,1.6f)};}}
 SPEED
 REACH
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 public static void Run(){int cases=0;foreach(bool enraged in new[]{false,true})foreach(int facing in new[]{-1,1})foreach(float x in new[]{10f,40f,70f}){
 var p=new SpeedProbe();p.owner.Enraged=enraged;
 float original=Math.Max(.1f,p.Settings.swordSpeed*1.25f*1.3f),previous=original*.25f,current=p.AimedSlashSpeed;
 Check(original==52&&previous==13&&current==52,"normal and enraged original 52 -> slowed 13 -> restored 52");
 var origin=new Vector2(x,4);var size=new Vector2(6,8);var oldVelocity=new Vector2(facing*previous,0);var velocity=new Vector2(facing*current,0);
 float oldLife=KarmaBossProjectileGeometry.AimedSlashBoundaryLife(origin,oldVelocity,size,float.MaxValue);
 float life=KarmaBossProjectileGeometry.AimedSlashBoundaryLife(origin,velocity,size,float.MaxValue);
 Check(Math.Abs(life/oldLife-.25f)<.00001f,"existing boundary lifetime automatically returns to one quarter");
 Check(Math.Abs(oldVelocity.x*oldLife-velocity.x*life)<.0001f,"same boundary endpoint and range");
 foreach(float targetX in new[]{0f,20f,40f,60f,80f}){
  var target=new Vector2(targetX,4);p.owner.game.Player.Position=target;
  Check(p.SwordShotCanReach(origin,oldVelocity,size,oldLife,target)==p.SwordShotCanReach(origin,velocity,size,life,target),"same production reach decision");
 }
 cases++;
 }Console.WriteLine("PASS "+cases+" normal/enraged speed, boundary lifetime and reach cases");}
}'''
        for token, source, signature in [
            ("BOX", contact, "public static class KarmaBossGeometry"),
            ("GEOMETRY", geometry, "internal static class KarmaBossProjectileGeometry"),
            ("SPEED", sw02, "float AimedSlashSpeed"),
            ("REACH", behavior, "public bool SwordShotCanReach("),
        ]:
            code = code.replace(token, method(source, signature))
        self.assertIn("PASS 12 normal/enraged speed", run_cs(code, "SpeedProbe"))

    def test_basic_core_isolated_from_progressive_skill_blade_fill(self):
        from test_sword_presentation import method
        source = VISUAL.read_text(encoding="utf-8-sig")
        charge = method(source, "void Charge(float t)")
        self.assertIn("if (!smallCharge) { BladeFill(t); return; }", charge)
        self.assertIn("if (!smallCharge) { BladeFill(1, 1-t); BladeChargeDischarge(t); return; }", method(source,"void ChargeRelease("))
        fill=method(source,"void BladeFill(")
        self.assertIn("path[p]=OnBlade(front*p/(Points-1))",fill)
        self.assertNotIn("ChargeCore(",fill)
        self.assertNotIn("ChargeDisc(",fill)
        self.assertIn("ChargeCore(at, axis, radius", charge)
        self.assertIn("ChargeRadius(focus)", charge)
        self.assertIn("bladePixelSize * (smallCharge ? 1.5f : 3)", charge)
        self.assertIn("OnBlade(", charge)
        self.assertIn("ChargeDisc(at, axis, radius * 1.35f", source)
        self.assertIn("ChargeDisc(at, axis, radius * 0.34f", source)
        self.assertIn("chargeBodyHeight * (heavy ? 0.39f : 0.30f)", source)
        self.assertIn("energy.ConfigureBlade(heel - (Vector2)transform.position", source)
        self.assertIn("[DefaultExecutionOrder(360)]", source)
        draw = method(source, "public void Draw(")
        self.assertIn("ChargeRadius(1) * 6.4f", draw)
        self.assertIn("mesh.bounds = bounds", draw)
        for signature in ["void Charge(float t)", "void ChargeRelease(", "void ChargeDisc(", "void ChargeCore("]:
            body = method(source, signature)
            self.assertNotRegex(body, r"new (?:GameObject|Mesh|Material|Vector[23]\[|Color\[)")

    def test_charge_geometry_converges_on_transformed_blades(self):
        from test_sword_presentation import method, run_cs
        source = VISUAL.read_text(encoding="utf-8-sig")
        catalog = motion_catalog()
        sockets = blade_endpoints()
        rows = []
        for form in FORMS:
            values = []
            for frame in range(6):
                x1, y1, x2, y2 = sockets[f"{form}/SW02_{frame:02d}"]
                values.extend((f"new Vector2({x1},{y1})", f"new Vector2({x2},{y2})"))
            rows.append("{" + ",".join(values) + "}")
        table = "= {" + ",".join(rows) + "};"
        ppus = [float(catalog[form, "SW02"][7]) for form in FORMS]
        code = r'''
using System;using System.Collections.Generic;
public struct Vector2{
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public float magnitude{get{return (float)Math.Sqrt(x*x+y*y);}}public Vector2 normalized{get{return magnitude>0?this/magnitude:new Vector2();}}
 public static Vector2 right{get{return new Vector2(1,0);}}public static Vector2 up{get{return new Vector2(0,1);}}
 public static Vector2 operator+(Vector2 a,Vector2 b){return new Vector2(a.x+b.x,a.y+b.y);}public static Vector2 operator-(Vector2 a,Vector2 b){return new Vector2(a.x-b.x,a.y-b.y);}public static Vector2 operator*(Vector2 a,float s){return new Vector2(a.x*s,a.y*s);}public static Vector2 operator/(Vector2 a,float s){return a*(1/s);}
 public static Vector2 Lerp(Vector2 a,Vector2 b,float t){return a+(b-a)*t;}public static float Distance(Vector2 a,Vector2 b){return (a-b).magnitude;}public static float Dot(Vector2 a,Vector2 b){return a.x*b.x+a.y*b.y;}
 public static implicit operator Vector3(Vector2 v){return new Vector3(v.x,v.y);}
}
public struct Vector3{public float x,y;public Vector3(float x,float y){this.x=x;this.y=y;}public static implicit operator Vector2(Vector3 v){return new Vector2(v.x,v.y);}}
struct Color{public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}}
class Transform{public float sx,angle;public Vector2 TransformPoint(Vector2 p){p=new Vector2(p.x*sx,p.y*1.7f);float c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);return new Vector2(p.x*c-p.y*s,p.x*s+p.y*c);}}
class Rect{public float width,height;}
class Sprite{public Rect rect=new Rect();public Vector2 pivot=new Vector2(256,256);public float pixelsPerUnit;}
class SpriteRenderer{public Sprite sprite=new Sprite();public bool flipX,flipY;public Transform transform=new Transform();}
static class Mathf{
 public const float PI=(float)Math.PI;public static float Cos(float n){return (float)Math.Cos(n);}public static float Sqrt(float n){return (float)Math.Sqrt(n);}public static float Sin(float n){return (float)Math.Sin(n);}public static float Abs(float n){return Math.Abs(n);}public static float Max(float a,float b){return Math.Max(a,b);}public static float Min(float a,float b){return Math.Min(a,b);}public static int Min(int a,int b){return Math.Min(a,b);}public static int Max(int a,int b){return Math.Max(a,b);}
 public static float Lerp(float a,float b,float t){return a+(b-a)*t;}public static float Repeat(float a,float b){return a-(float)Math.Floor(a/b)*b;}public static float Clamp01(float t){return Math.Max(0,Math.Min(1,t));}public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return Lerp(a,b,t*t*(3-2*t));}
}
public class ChargeProbe{
 const int Points=13,Strokes=96,Lanes=4,VerticesPerStroke=Points*Lanes;
 Vector3[] vertices=new Vector3[Strokes*VerticesPerStroke];Color[] colors=new Color[Strokes*VerticesPerStroke];Vector2[] path=new Vector2[Points];
 static readonly Vector2[,] BladeSockets SOCKETS
 Vector2 bladeHeel,bladeBend,bladeTip;float bladePixelSize,chargeBodyHeight,chargeSecondsLeft,age,power,facing;int stroke;bool heavy,aiming,smallCharge;uint seed=747796405,arcSeed=747796405;
 static readonly Color Cyan=new Color(.14f,1.1f,2,1),White=new Color(1.25f,1.7f,2.1f,1);
 INTENSITY
 class Arc{public Vector2 from,to;public float width,alpha;}
 List<Arc> bolts=new List<Arc>(),packets=new List<Arc>();
 void BladeBolt(Vector2 from,Vector2 to,float width,float alpha,float kink,int key){bolts.Add(new Arc{from=from,to=to,width=width,alpha=alpha});DrawBladeBolt(from,to,width,alpha,kink,key);}
 void ElectricRibbon(float width,float alpha){packets.Add(new Arc{from=path[0],to=path[Points-1],width=width,alpha=alpha});DrawElectricRibbon(width,alpha);}
 CONFIGURE
 ON_BLADE
 NOISE
 FLICKER
 CRACKLE
 ALPHA
 RIBBON
 BOLT
 ELECTRIC
 CHARGE
 FILL_PROGRESS
 FILL
 ACCENTS
 VENT
 CHARGE_CENTER
 CHARGE_RADIUS
 CHARGE_DISC
 CHARGE_CORE
 CHARGE_RELEASE
 SEGMENT
 POINT
 void Sample(float seconds,float progress){age=seconds;stroke=0;bolts.Clear();packets.Clear();Array.Clear(colors,0,colors.Length);Charge(progress);}
 static void Check(bool ok,string m){if(!ok)throw new Exception(m);}
 float CoreArea(){
  Vector2 axis=(bladeBend-bladeHeel).normalized,normal=new Vector2(-axis.y,axis.x),at=ChargeCenter();
  float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
  for(int i=18*VerticesPerStroke;i<25*VerticesPerStroke;i++)if(colors[i].a>.05f){
   Vector2 v=(Vector2)vertices[i]-at;float x=Vector2.Dot(v,axis),y=Vector2.Dot(v,normal);
   minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
  }return (maxX-minX)*(maxY-minY);
 }
 void BoundsCheck(){
  Vector2 at=smallCharge?ChargeCenter():(bladeHeel+bladeTip)*.5f;float extent=smallCharge?ChargeRadius(1)*3.2f:Vector2.Distance(bladeHeel,bladeTip)/2+chargeBodyHeight*.3f;
  for(int i=0;i<stroke*VerticesPerStroke;i++)if(colors[i].a>.001f){Vector2 v=(Vector2)vertices[i]-at;Check(Math.Abs(v.x)<extent&&Math.Abs(v.y)<extent,"occupied charge/release vertices fit actual dynamic bounds");}
 }
 public static void Run(){int cases=0,maxStrokes=0;
 float[] ppus={PPUS};
 foreach(int form in new[]{0,1,2,3})foreach(int frame in new[]{0,1,2,3,4,5})foreach(int sign in new[]{-1,1})foreach(bool flip in new[]{false,true})foreach(float angle in new[]{0f,.47f})foreach(int phase in new[]{0,1,2,3}){
  var renderer=new SpriteRenderer();renderer.sprite.pixelsPerUnit=ppus[form];renderer.flipX=flip;renderer.transform.sx=sign*1.7f;renderer.transform.angle=angle;
  renderer.sprite.rect.width=new float[]{WIDTHS}[form];renderer.sprite.rect.height=new float[]{HEIGHTS}[form];
  var heel=ChargeBladePoint(renderer,BladeSockets[form,frame*2]);var tip=ChargeBladePoint(renderer,BladeSockets[form,frame*2+1]);var bend=Vector2.Lerp(heel,tip,.5f);
  var p=new ChargeProbe{heavy=phase==1||phase==2,aiming=phase==1,power=phase>0?1:.72f,facing=sign};
  float pixel=2*14.0625f/720;p.ConfigureBlade(new Vector2(),bend-heel,tip-heel,pixel,4,phase==3);p.Sample(0,0);
  if(p.smallCharge){
  float early=p.CoreArea(),earlyRadius=p.ChargeRadius(phase==0?.18f:.15f);
  Check(early>earlyRadius*earlyRadius*.25f,"early white core is filled area, not thin sword outline");p.BoundsCheck();
  p.Sample(.01f,.5f);Check(p.bolts.Count==(p.smallCharge?3:p.heavy?19:10),"bounded thick intake bolts and streams");
  var arcs=p.bolts.ToArray();Vector2 at=p.ChargeCenter();
  Check(Vector2.Distance(arcs[0].to,at)<p.ChargeRadius(1)*.3f,"lightning feeds hand core");
  Check(arcs[0].width/pixel>(p.smallCharge?1.4f:2),"thick current has multiple projected pixels");
  p.Sample(.015f,.5f);float firstU=Mathf.Repeat(.01f*(p.heavy?2.4f:3)+Noise(p.arcSeed+7),1);
  if(firstU+.005f*(p.heavy?2.4f:3)<1)Check(Vector2.Distance(p.bolts[0].from,p.bolts[0].to)<Vector2.Distance(arcs[0].from,arcs[0].to),"current contracts into core before cycle resets");
  p.Sample(.01f,.95f);if(phase!=1)Check(p.CoreArea()>early*1.5f,"late core area grows as well as brightness");
  maxStrokes=Math.Max(maxStrokes,p.stroke);Check(p.stroke<Strokes,"charge leaves fixed buffer headroom");p.BoundsCheck();
  Vector2 shift=new Vector2(7,-3);p.ConfigureBlade(shift,bend-heel+shift,tip-heel+shift,pixel,4,phase==3);p.Sample(.01f,.5f);
  Check(Vector2.Distance(p.bolts[0].to,arcs[0].to+shift)<.0001f,"core follows translated hand immediately");
  p.aiming=false;p.chargeSecondsLeft=1;p.Sample(.01f,1);float full=p.CoreArea();
  p.chargeSecondsLeft=0;p.Sample(.01f,1);Check(p.CoreArea()<full*.15f,"core contraction completes inside preparation");p.BoundsCheck();
  p.stroke=0;p.bolts.Clear();Array.Clear(p.colors,0,p.colors.Length);p.ChargeRelease(0);Check(p.bolts.Count>0,"discharge begins on exact launch event");p.BoundsCheck();
  p.stroke=0;p.bolts.Clear();Array.Clear(p.colors,0,p.colors.Length);p.ChargeRelease(.7f);p.BoundsCheck();
  foreach(var arc in p.bolts)Check((arc.to.x-arc.from.x)*sign>0,"discharge follows committed slash direction");
  p.stroke=0;p.bolts.Clear();Array.Clear(p.colors,0,p.colors.Length);p.ChargeRelease(1);foreach(var color in p.colors)Check(color.a<.001f,"no visible core remains after release");} else {
  p.Sample(0,0);int earlyStroke=p.stroke;p.BoundsCheck();
  float earlyFront=p.BladeFillProgress(0);p.Sample(.1f,.5f);float middle=p.BladeFillProgress(.5f);p.Sample(.2f,1);float late=p.BladeFillProgress(1);
  Check(earlyFront<middle&&middle<late,"charge front advances guard to tip");
  Check(p.stroke==(p.heavy?42:30),"layered sheath, travelling packets, tip focus and bounded sparks");Check(p.stroke<Strokes,"fixed buffer headroom");
  for(int i=0;i<Points;i++)Check(Vector2.Distance((Vector2)p.vertices[i*Lanes+1],p.OnBlade(late*i/(Points-1)))<.8f,"light follows actual blade");
  p.BoundsCheck();var before=p.bolts[0].from;Vector2 shift=new Vector2(7,-3);p.ConfigureBlade(shift,bend-heel+shift,tip-heel+shift,pixel,4);p.Sample(.2f,1);Check(Vector2.Distance(p.bolts[0].from,before+shift)<.0001f,"moving blade attachment");p.BoundsCheck();
  p.aiming=false;p.stroke=0;p.bolts.Clear();Array.Clear(p.colors,0,p.colors.Length);p.ChargeRelease(0);Check(p.bolts.Count>0,"release on actual shot event");maxStrokes=Math.Max(maxStrokes,p.stroke);p.BoundsCheck();
  p.stroke=0;Array.Clear(p.colors,0,p.colors.Length);p.ChargeRelease(1);foreach(var color in p.colors)Check(color.a<.001f,"release cleans fully");
  maxStrokes=Math.Max(maxStrokes,p.stroke);maxStrokes=Math.Max(maxStrokes,earlyStroke);
 }cases++;
 }
 var light=new ChargeProbe{heavy=false,chargeBodyHeight=4};var heavy=new ChargeProbe{heavy=true,chargeBodyHeight=4};Check(heavy.ChargeRadius(1)>light.ChargeRadius(1)*1.25f,"SW02 core has more area than SW01");
 var basic=new ChargeProbe{chargeBodyHeight=4,smallCharge=true};Check(Math.Abs(basic.ChargeRadius(1)/light.ChargeRadius(1)-.375f)<.00001f,"basic charge is 37.5 percent of reduced SW01");Check(Math.Abs(light.ChargeRadius(1)/(4*.30f*1.25f)-.60f)<.00001f,"SW01 retains 60 percent of former dimensions");Check(Math.Abs(heavy.ChargeRadius(1)/(4*.39f*1.25f)-.60f)<.00001f,"SW02 retains 60 percent of former dimensions");
 Check(basic.ChargeIntensity<light.ChargeIntensity&&light.ChargeIntensity<heavy.ChargeIntensity,"strict tier brightness");
 Console.WriteLine("PASS "+cases+" blade charge geometry cases; max "+maxStrokes+"/96 strokes; filled area, intake, moving hand, contraction, discharge and dynamic bounds");}
}'''
        code = code.replace("INTENSITY", re.search(r"float ChargeIntensity => [^;]+;", source)[0])
        code = code.replace("SOCKETS", table).replace("PPUS", ",".join(str(p) + "f" for p in ppus))
        code = code.replace("WIDTHS", ",".join(catalog[f, "SW02"][5] for f in FORMS))
        code = code.replace("HEIGHTS", ",".join(catalog[f, "SW02"][6] for f in FORMS))
        for token, signature in [("CONFIGURE", "public void ConfigureBlade("), ("ON_BLADE", "Vector2 OnBlade("),
                                 ("NOISE", "static float Noise("), ("FLICKER", "float Flicker("), ("CRACKLE", "float CrackleFlicker("),
                                 ("ALPHA", "static Color Alpha("), ("RIBBON", "void Ribbon("),
                                 ("BOLT", "void BladeBolt("), ("ELECTRIC", "void ElectricRibbon("),
                                 ("CHARGE_CENTER", "Vector2 ChargeCenter()"), ("CHARGE_RADIUS", "float ChargeRadius("),
                                 ("CHARGE_DISC", "void ChargeDisc("), ("CHARGE_CORE", "void ChargeCore("),
                                 ("CHARGE_RELEASE", "void ChargeRelease("), ("SEGMENT", "void Segment("),
                                 ("CHARGE", "void Charge(float t)"), ("FILL_PROGRESS", "float BladeFillProgress("), ("FILL", "void BladeFill("), ("ACCENTS", "void BladeChargeAccents("), ("VENT", "void BladeChargeDischarge("), ("POINT", "static Vector2 ChargeBladePoint(")]:
            body = method(source, signature)
            if token == "BOLT":
                body = body.replace("void BladeBolt(", "void DrawBladeBolt(", 1)
            if token == "ELECTRIC":
                body = body.replace("void ElectricRibbon(", "void DrawElectricRibbon(", 1)
            code = code.replace(token, body)
        self.assertIn("PASS 768 blade charge geometry cases; max", run_cs(code, "ChargeProbe", modern=True))
        # Remove observation wrappers before measuring geometry allocations.
        # Unity's mesh upload is covered by compilation, not this GC probe.
        allocation = code.replace(method(code, "void BladeBolt("), "")
        allocation = allocation.replace(method(allocation, "void ElectricRibbon("), "")
        allocation = allocation.replace("void DrawBladeBolt(", "void BladeBolt(").replace("void DrawElectricRibbon(", "void ElectricRibbon(")
        allocation = allocation.replace(method(allocation, "public static void Run()"), r'''
 public static void Run(){
  var p=new ChargeProbe{heavy=true,power=1,facing=1};
  p.ConfigureBlade(new Vector2(),new Vector2(1,1),new Vector2(2,2),.04f,4);
  for(int i=0;i<500;i++){p.Sample(i*.01f,.9f);p.stroke=0;p.ChargeRelease(.7f);}
  long before=GC.GetAllocatedBytesForCurrentThread();
  for(int i=0;i<2000;i++){p.Sample(i*.01f,.9f);p.stroke=0;p.ChargeRelease(.7f);}
  long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
  Check(bytes==0,"charge and release geometry must not allocate per frame: "+bytes);
  Console.WriteLine("PASS zero managed geometry allocations over 2000 charge/release samples");
 }''')
        self.assertIn("PASS zero managed geometry allocations", run_cs(allocation, "ChargeProbe", modern=True))

    def test_charge_release_events_and_immediate_cleanup(self):
        from test_sword_presentation import method, run_cs
        source = VISUAL.read_text(encoding="utf-8-sig")
        code = r'''
using System;using System.Collections.Generic;
public struct Vector2{
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero;
 public static Vector2 operator+(Vector2 a,Vector2 b){return new Vector2(a.x+b.x,a.y+b.y);}public static Vector2 operator-(Vector2 a,Vector2 b){return new Vector2(a.x-b.x,a.y-b.y);}public static Vector2 operator*(Vector2 a,float b){return new Vector2(a.x*b,a.y*b);}
 public static implicit operator Vector3(Vector2 p){return new Vector3(p.x,p.y,0);}public static implicit operator Vector2(Vector3 p){return new Vector2(p.x,p.y);}
}
public struct Vector3{public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 operator-(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}}
public struct Bounds{public Vector3 center,size,extents;}
public class Component{public GameObject gameObject;public Transform transform{get{return gameObject.transform;}}public bool enabled=true;}
public class Transform{public GameObject gameObject;public Vector3 position;public SpriteRenderer sword;public void SetParent(Transform t,bool world){}public Transform Find(string n){return sword!=null?sword.transform:null;}public T GetComponent<T>() where T:class{return sword as T;}public T GetComponentInParent<T>() where T:class{return null;}}
public class GameObject{
 public bool activeInHierarchy=true;public Transform transform;List<Component> components=new List<Component>();
 public GameObject(string n=""){transform=new Transform{gameObject=this};}
 public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};components.Add(c);if(c is SpriteRenderer)transform.sword=c as SpriteRenderer;return c;}
 public void SetActive(bool active){activeInHierarchy=active;if(!active)foreach(var c in components)if(c is KarmaSwordWaveAnimation)((KarmaSwordWaveAnimation)c).Deactivate();}
 public void DestroyNow(){SetActive(false);foreach(var c in components)if(c is KarmaSwordWaveAnimation)((KarmaSwordWaveAnimation)c).Destroyed();}
}
public class SpriteRenderer:Component{public int sortingLayerID,sortingOrder;public bool flipX;public Bounds bounds=new Bounds{extents=new Vector3(2,3,0)};}
public enum EnemyAttack{SWBasic,SW01,SW02}public enum RunPhase{Combat,End}public enum SwordEnergyPhase{Charge,ChargeRelease}
public class KarmaCharacterAnimator{public string SwordBodyAction="Volley";public float SwordChargeBodyHeight=4;}
public class Camera{public bool orthographic=true;public int pixelHeight=720;public float orthographicSize=14.0625f;}
public class KarmaGame{public Transform World=new GameObject().transform;public RunPhase Phase=RunPhase.Combat;public bool Paused;public Camera GameCamera=new Camera();}
public class KarmaEnemy{public int CurrentPattern,Facing=1;public bool PresentationActive=true,Stunned;public Transform transform=new GameObject().transform;public Bounds DamageBounds;}
public static class Mathf{public static float Max(float a,float b){return Math.Max(a,b);}public static float Min(float a,float b){return Math.Min(a,b);}public static int Min(int a,int b){return Math.Min(a,b);}public static float Abs(float a){return Math.Abs(a);}public static float Sign(float a){return Math.Sign(a);}public static float Clamp(float a,float min,float max){return Math.Max(min,Math.Min(max,a));}}
public static class Time{public static float deltaTime=.01f;}
public static class KarmaSpace{public static float Units(float n){return n*2;}}public static class KarmaStage{public const float ArenaLeft=0,ArenaRight=80;}
public class KarmaSwordVfxPool{public void Cancel(Transform t){}public void Echo(KarmaEnemy o,SpriteRenderer r,float a,float b,Transform t){}}
public class KarmaSwordWaveEnergyPool{
 public class Rig{public bool Active=true,Discharging;public Vector2 anchor;public void BeginChargeRelease(float direction){Discharging=true;}public void ConfigureRelease(Vector2 at,float sweep){anchor=at;}public void ConfigureBlade(Vector2 h,Vector2 b,Vector2 t,float p,float body,bool small,float remaining){}public void ConfigureVisual(Bounds b,Vector3 p,float u){}public void Draw(Vector3 a,float t,float d,float dt){}}
 public void Return(Rig r){r.Active=false;}
}
public class KarmaSwordWaveAnimation:Component{
 enum Playback{Charge,Flight,Burst,ChargeRelease}Playback playback;KarmaGame game;KarmaEnemy owner;SpriteRenderer picture,swordPicture;KarmaCharacterAnimator swordAnimator;Transform chargeScope;
 Vector2 size,velocity;float age,duration,nextEcho,chargeBodyHeight;int pattern,first,count;bool heavy,aiming,terminal,basicRanged;
 KarmaSwordVfxPool echoes;KarmaSwordWaveEnergyPool energyPool;KarmaSwordWaveEnergyPool.Rig energy;
 static Dictionary<KarmaEnemy,KarmaSwordWaveAnimation> charges=new Dictionary<KarmaEnemy,KarmaSwordWaveAnimation>();
 static int bursts;static KarmaSwordWaveAnimation lastBurst;
 void Initialize(KarmaGame g,KarmaEnemy o,SpriteRenderer r){game=g;owner=o;picture=r;pattern=o.CurrentPattern;}
 void AttachEnergy(SwordEnergyPhase p){energyPool=new KarmaSwordWaveEnergyPool();energy=new KarmaSwordWaveEnergyPool.Rig();DrawEnergy(0);}
 static void Destroy(GameObject g){g.DestroyNow();}
 static bool BladePoints(SpriteRenderer r,out Vector2 heel,out Vector2 bend,out Vector2 tip){int sign=r!=null&&r.flipX?-1:1;heel=new Vector2(sign*2,1);bend=new Vector2(sign*3,2);tip=new Vector2(sign*4,3);return r!=null;}
 static KarmaSwordWaveAnimation Burst(KarmaGame g,KarmaEnemy o,Vector2 at,Vector2 s,int first,int count,float seconds,bool flip,float angle=0,bool heavy=false){bursts++;var b=new GameObject().AddComponent<KarmaSwordWaveAnimation>();b.Initialize(g,o,b.gameObject.AddComponent<SpriteRenderer>());b.playback=Playback.Burst;b.energy=new KarmaSwordWaveEnergyPool.Rig();return lastBurst=b;}
 void ApplyFlight(){}void Paint(int a,int b,Vector2 s,Vector2 f){}
 CHARGE
 APPLY
 DRAW
 RELEASE
 RETURN
 STOP
 TAIL_EVENT
 LATE
 DISABLE
 DESTROY
 public void Deactivate(){OnDisable();}public void Destroyed(){OnDestroy();}
 static void Check(bool ok,string m){if(!ok)throw new Exception(m);}
 public static void Run(){int cases=0;
 foreach(int pattern in new[]{0,1,2})foreach(int facing in new[]{-1,1}){
  var game=new KarmaGame();var owner=new KarmaEnemy{CurrentPattern=pattern,Facing=facing};var art=new GameObject().transform;
  var sword=new GameObject().AddComponent<SpriteRenderer>();sword.flipX=facing<0;art.sword=owner.transform.sword=sword;
  int before=bursts,releases=pattern==1?2:1;KarmaSwordWaveAnimation previousTail=null;
  for(int i=0;i<releases;i++){
   if(pattern==2){Charge(game,owner,art,new GameObject().transform,.34f,true,true);var old=charges[owner];var oldEnergy=old.energy;Charge(game,owner,art,new GameObject().transform,.8f,true);Check(!oldEnergy.Active&&charges.Count==1,"tracking-to-lock replacement has one live charge");var current=charges[owner];old.OnDestroy();Check(charges[owner]==current,"old cleanup cannot remove replacement");}
   else {if(pattern==0){Charge(game,owner,art,new GameObject().transform,.4f,false);Check(!charges.ContainsKey(owner),"SWBasic without explicit ranged mode never charges");}
    Charge(game,owner,art,new GameObject().transform,i==0?.585f:.54f,false,basicRanged:pattern==0);}
   var charge=charges[owner];var rig=charge.energy;
   if(previousTail!=null){Check(previousTail.energy.Active,"first discharge overlaps only the start of second preparation");for(int tick=0;tick<25;tick++)previousTail.LateUpdate();Check(charges[owner]==charge&&rig.Active,"first tail expiry cannot remove second charge");}
   Check(!charge.picture.enabled&&rig.Active&&bursts==before+i,"attached mesh only, no premature release");
   game.Paused=true;charge.LateUpdate();Check(charge.age==0&&rig.Active,"pause preserves charge clock");game.Paused=false;
   Release(game,owner,new Vector2(10,2),new Vector2(3,5),pattern==2,facing);
   Check(rig.Active&&rig.Discharging&&!charges.ContainsKey(owner),"same rig transitions from charge into short cosmetic release");
   game.Paused=true;charge.LateUpdate();Check(charge.age==0&&rig.Active,"pause freezes contraction as well as charge");game.Paused=false;
   if(pattern==1&&i==0)previousTail=charge;
   else {for(int tick=0;tick<25;tick++)charge.LateUpdate();Check(!rig.Active&&!charge.gameObject.activeInHierarchy,"short discharge expires without an extra attack wait");}
   Check(lastBurst.energy.anchor.x==(pattern==0?10:facing*4)&&lastBurst.energy.anchor.y==(pattern==0?2:3),"same event discharges at rendered blade tip");cases++;
  }
  Check(bursts-before==releases,"SW01 two releases / SW02 one release, no new wait");
  for(int reason=0;reason<10;reason++){
   var scope=new GameObject();owner.CurrentPattern=pattern;owner.PresentationActive=true;owner.Stunned=false;game.Phase=RunPhase.Combat;
   Charge(game,owner,art,scope.transform,.6f,pattern==2,basicRanged:pattern==0);var charge=charges[owner];var rig=charge.energy;int countBefore=bursts;
   if(reason==0)scope.SetActive(false);if(reason==1)owner.PresentationActive=false;if(reason==2)owner.Stunned=true;if(reason==3)game.Phase=RunPhase.End;if(reason==4)owner.CurrentPattern=99;
   if(reason>=5){Release(game,owner,new Vector2(10,2),new Vector2(3,5),pattern==2,facing);countBefore=bursts;
    if(reason==5){charge.swordAnimator=new KarmaCharacterAnimator{SwordBodyAction=null};if(pattern==0)owner.CurrentPattern=99;}if(reason==6)owner.PresentationActive=false;if(reason==7)owner.Stunned=true;if(reason==8)game.Phase=RunPhase.End;if(reason==9)owner.CurrentPattern=99;
   }
   charge.LateUpdate();Check(!rig.Active&&!charges.ContainsKey(owner),"interruption/death/scope/pattern cleanup returns rig");Check(bursts==countBefore,"cleanup never synthesizes a release");cases++;
  }
 }
 Console.WriteLine("PASS "+cases+" charge lifecycle/release cases: two SW01 events, one SW02 event, tracking replacement, facing, pause, interruption and death");}
}'''
        for token, signature in [("CHARGE", "public static void Charge("), ("APPLY", "void ApplyCharge()"),
                                 ("DRAW", "void DrawEnergy("), ("RELEASE", "public static void Release("),
                                 ("RETURN", "void ReturnEnergy()"), ("STOP", "static void StopCharge("),
                                 ("TAIL_EVENT", "static void ReleaseCharge("),
                                 ("LATE", "void LateUpdate()"), ("DISABLE", "void OnDisable()"), ("DESTROY", "void OnDestroy()")]:
            code = code.replace(token, method(source, signature))
        self.assertIn("PASS 68 charge lifecycle/release cases", run_cs(code, "KarmaSwordWaveAnimation", modern=True))
        volley = (ROOT / "Assets/Scripts/SwordWave/Patterns/SwordWaveVolley.cs").read_text(encoding="utf-8-sig")
        aimed = (ROOT / "Assets/Scripts/SwordWave/Patterns/SW02.cs").read_text(encoding="utf-8-sig")
        self.assertIn("for (int i = 0; i < shots; i++)", volley)
        self.assertIn("if (!scars) KarmaSwordWaveAnimation.Charge", volley)
        self.assertIn("sword.WaveLaunchVFX(origin, velocity, waveSize, false, high)", volley)
        self.assertIn("sword.WaveLaunchVFX(origin, velocity, size, true)", aimed)

    def test_sw03_and_sw05_remove_only_requested_range_cues(self):
        base = ROOT / "Assets/Scripts/SwordWave"
        sw03 = (base / "Patterns/SW03.cs").read_text(encoding="utf-8-sig")
        self.assertNotIn("KarmaSwordAttackPolish.Charge(", sw03)
        self.assertNotIn("KarmaSwordGroundSlam", sw03)
        self.assertIn("var marks = MarkRound(warningRoot, size, warning)", sw03)
        self.assertIn("foreach (var mark in marks) mark.SetWarningProgress(progress)", sw03)
        self.assertIn("ReleaseRound(releaseRoot, size, active, contacts)", sw03)
        blade = (base / "Patterns/KarmaSW05RainBlade.cs").read_text(encoding="utf-8-sig")
        arm = blade.split("public void ArmExplosion(", 1)[1].split("public void Explode(", 1)[0]
        self.assertNotIn("KarmaSW05VFX.Warning", arm)
        self.assertIn("planted.ChargePlanted(seconds)", arm)
        self.assertNotIn("KarmaSW05VFX.Explosion(transform.parent, game, owner, at, size)", blade)
        self.assertIn("sharedContact, 0, sourcePattern: EnemyAttack.SW05", blade)
        final = (base / "Patterns/SW05.cs").read_text(encoding="utf-8-sig")
        self.assertIn("KarmaSW05Energy.Prepare(owner.effectsRoot, owner.game, planted, DetonationWarning)", final)
        self.assertIn("ignition.Ignite()", final)
        self.assertIn("ExplosionActiveSeconds = 0.12f", blade)
        sw05 = (base / "Patterns/SW05.cs").read_text(encoding="utf-8-sig")
        self.assertIn("DetonationWarning = 0.60f", sw05)
        self.assertIn("yield return owner.Wait(DetonationWarning)", sw05)
        behavior = (base / "KarmaSwordBossBehavior.cs").read_text(encoding="utf-8-sig")
        self.assertIn("seconds = 0.75f", behavior)
        self.assertIn("return pattern == EnemyAttack.SW04 || pattern == EnemyAttack.SW05 && IsFinalDetonation(contact)", behavior)

    def test_production_parry_callback_matrix(self):
        source = (ROOT / "Assets/Scripts/SwordWave/KarmaSwordBossBehavior.cs").read_text(encoding="utf-8-sig")
        from test_sword_presentation import method
        callback = method(source, "public override void OnAttackParried(bool stun)").replace("public override", "public")
        returned = method(source, "public void OnReturnedCounterHit()")
        self.assertIn("parryPendingBeforeContact = owner.pendingParry", source)
        self.assertIn("if (parried) { counter.OnParried(contact); }", source)
        # Compile the actual callback in memory, substituting only host storage.
        # Includes immune contacts arriving while SW04 stun is already queued.
        code = r"""
using System;
using System.Collections.Generic;
enum EnemyAttack { SW01, SW02, SW04, SW05 }
class Transform { public Node gameObject = new Node(); }
class Node { public bool active = true; public void SetActive(bool v){active=v;} }
class Animator { public bool cancelled; public void CancelSwordBodyPresentation(){cancelled=true;} }
class Game { public int StageIndex; }
class Owner {
 public Game game=new Game(); public bool running, pendingParry; public bool Stunned=false;
 public bool Active=true, stopped; public EnemyAttack selected;
 public Animator animator=new Animator(); public List<Transform> effectsRoot=new List<Transform>{new Transform()};
 public void StopAllCoroutines(){stopped=true;}
}
class Feedback { public bool armed; public void ArmElectrocution(){armed=true;} }
public class ParryProbe {
 Owner owner=new Owner(); Feedback stunFeedback=new Feedback();
 bool parryPendingBeforeContact;
 CALLBACK
 RETURNED
 static void Check(bool value){if(!value)throw new Exception("parry matrix failed");}
 public static void Run(){int cases=0;
 foreach(int stage in new[]{0,1,2,3})
 foreach(bool ultimate in new[]{false,true})
 foreach(EnemyAttack contactPattern in new[]{EnemyAttack.SW01,EnemyAttack.SW02,EnemyAttack.SW04,EnemyAttack.SW05})
 foreach(bool queued in new[]{false,true}) {
  bool electrical=contactPattern==EnemyAttack.SW04;
  var p=new ParryProbe();p.owner.game.StageIndex=stage;
  p.owner.running=true;p.owner.selected=ultimate?EnemyAttack.SW05:EnemyAttack.SW01;
  p.parryPendingBeforeContact=queued;p.owner.pendingParry=true;
  p.OnAttackParried(false);
  bool immune=stage==3||ultimate;
  Check(p.owner.pendingParry==(!immune&&queued));
  Check(!p.stunFeedback.armed&&!p.owner.stopped&&!p.owner.animator.cancelled);
  Check(p.owner.effectsRoot[0].gameObject.active);
  if(electrical)p.OnReturnedCounterHit();
  Check(p.owner.pendingParry==(!immune&&(electrical||queued)));
  bool changed=!immune&&electrical&&!queued;
  Check(p.stunFeedback.armed==changed && p.owner.stopped==changed && p.owner.animator.cancelled==changed);
  Check(p.owner.effectsRoot[0].gameObject.active!=changed);
  cases++;
 }
 foreach(int reason in new[]{0,1,2}) {
  var p=new ParryProbe();p.owner.Active=reason!=0;p.owner.Stunned=reason==1;p.owner.pendingParry=reason==2;
  p.OnReturnedCounterHit();Check(!p.owner.stopped&&!p.stunFeedback.armed&&p.owner.effectsRoot[0].gameObject.active);cases++;
 }
 foreach(bool queued in new[]{false,true}) {
  var p=new ParryProbe();p.parryPendingBeforeContact=queued;p.owner.pendingParry=true;
  p.OnAttackParried(true);Check(p.owner.pendingParry==queued&&!p.owner.stopped&&!p.stunFeedback.armed);cases++;
 }
 Console.WriteLine("PASS "+cases+" production parry scenarios");
 }
}
""".replace("CALLBACK", callback).replace("RETURNED", returned)
        from test_sword_presentation import run_cs
        self.assertIn("PASS 69 production parry scenarios", run_cs(code, "ParryProbe", modern=True))


class BasicChecks(unittest.TestCase):
    def test_basic_reuses_sw01_flight_and_keeps_charge_attached(self):
        source = (ROOT / "Assets/Scripts/SwordWave/Visuals/KarmaSwordAttackPolish.cs").read_text(encoding="utf-8-sig")
        basic = source.split("public sealed class KarmaSwordBasicPresentation", 1)[1].split("// SW04-only", 1)[0]
        ranged = basic.split("public static void Ranged(", 1)[1].split("public static void Projectile", 1)[0]
        self.assertIn("KarmaSwordWaveAnimation.Flight(game, owner, shot, velocity, sw01Footprint)", ranged)
        self.assertNotIn("Projectile(game", ranged)
        self.assertNotIn("0.95f", ranged)
        self.assertNotIn("AddComponent", ranged)
        charge = VISUAL.read_text(encoding="utf-8-sig")
        self.assertIn("BladePoints(swordPicture, out var heel", charge)
        self.assertIn("transform.position = heel", charge)
        motion = (ROOT / "Assets/Scripts/SwordWave/Visuals/KarmaWaveMotion.cs").read_text(encoding="utf-8-sig")
        self.assertIn("spriteHomeRotation * (foot - rotation * foot)", motion)

    def test_production_melee_timeline_damage_budget_and_pose_sequence(self):
        source = (ROOT / "Assets/Scripts/SwordWave/Patterns/SWBasic.cs").read_text(encoding="utf-8-sig")
        melee = source.split("            IEnumerator Melee()", 1)[1].split("            public override bool CanReach", 1)[0]
        animator = (ROOT / "Assets/Scripts/SwordWave/Visuals/KarmaCharacterAnimator.Sword.cs").read_text(encoding="utf-8-sig")
        self.assertIn('PrepareSwordBody(melee ? "BasicMelee" : "BasicRanged"', animator)
        self.assertIn("ShowSwordPatternMotion(EnemyAttack.SWBasic)", animator)
        core = (ROOT / "Assets/Scripts/Core/KarmaBossRules.cs").read_text(encoding="utf-8-sig")
        consume = core.split("public bool TryConsume()", 1)[1].split("\n", 1)[0]
        code = r"""
using System;using System.Collections;using System.Collections.Generic;
struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 zero {get{return new Vector2();}} public static Vector2 right{get{return new Vector2(1,0);}}
 public static Vector2 operator+(Vector2 a,Vector2 b){return new Vector2(a.x+b.x,a.y+b.y);}
 public static Vector2 operator*(Vector2 a,float b){return new Vector2(a.x*b,a.y*b);}
}
enum ActionState{Telegraph,Attack}
class KarmaAttackContact {public bool Consumed=false;public bool TryConsume() CONSUME }
class Node {public void SetActive(bool active){}}
class Marker {public Node gameObject=new Node();public object transform=new object();}
class Visual {public bool flipX=false;}
class Shot {public Node gameObject=new Node();public Visual Visual=new Visual();public KarmaAttackContact contact;public float at,life,damage;public Vector2 center;}
class Animator {public void BeginBasicLink(float t){}public List<int> strikes=new List<int>();public void PrepareBasicAttack(float t,bool melee){}public void PlayPose(string p,float t){}public void BeginBasicRelease(float t,int strike){strikes.Add(strike);}}
class Sword {public Vector2 BasicMeleeSize=new Vector2(6.6f,3.6f);public float BasicMeleeOffset=3.2f;}
class SettingsData {public float basicDamageMultiplier=1;}
class Config {public float hitInvulnerability=.7f;}
class Game {public Config Config=new Config();}
class Owner {
 public Vector2 Position=new Vector2(20,2);public float direction=1,clock=0;public float FightSeconds{get{return clock;}}public bool Active=true;
 public Game game=new Game();public ActionState state=ActionState.Telegraph;public Animator animator=new Animator();public List<Shot> shots=new List<Shot>();public List<Vector2> warnings=new List<Vector2>();
 public float Warning(float t){return t;}
 public Marker Mark(string n,Vector2 c,Vector2 z){warnings.Add(c);return new Marker();}
 public IEnumerator Wait(float t){yield return t;}
 public IEnumerator Recovery(float t){yield return t;}
 public Shot Attack(Vector2 c,Vector2 v,Vector2 z,float life,float damage,KarmaAttackContact contact){
 var shot=new Shot{at=clock,life=life,damage=damage,contact=contact,center=c};shots.Add(shot);return shot;}
}
static class KarmaSwordAttackPolish{public static void Warning(Marker m){}public static void Projectile(object g,Owner o,Shot s,Vector2 v,Vector2 z){}}
static class KarmaSwordBasicPresentation{public static void MeleeWarning(object g,Owner o,Marker t,Vector2 z,float w,int s=0){}}
static class Mathf {public static float Min(float a,float b){return Math.Min(a,b);}public static float Max(float a,float b){return Math.Max(a,b);}public static float Ceil(float v){return (float)Math.Ceiling(v);}}
public class BasicProbe {
 Owner owner=new Owner();Sword sword=new Sword();SettingsData Settings=new SettingsData();
 static void Destroy(object o){}
 KarmaAttackContact firstMeleeContact;float firstMeleeResumeAt;
 IEnumerator Melee() MELEE
 static void Check(bool v,string message){if(!v)throw new Exception(message);}
 static void Drive(IEnumerator e,Owner o){while(e.MoveNext()){var nested=e.Current as IEnumerator;if(nested!=null)Drive(nested,o);else if(e.Current is float)o.clock+=(float)e.Current;}}
 public static void Run(){
 foreach(int facing in new[]{-1,1}) {
 var p=new BasicProbe();p.owner.direction=facing;Drive(p.Melee(),p.owner);var hits=p.owner.shots;
 Check(p.owner.warnings.Count==2 && p.owner.warnings[0].x==p.owner.warnings[1].x,"both cuts advertise the same fixed area");
 Check(hits.Count==2,"two contact windows");Check(Math.Abs(hits[0].at-.4f)<.0001f,"original preparation");
 Check(Math.Abs(hits[1].at-(hits[0].at+hits[0].life)-2f/24)<.0001f,"missed opener links after two visible warning frames");
 Check(Math.Abs(p.owner.clock-(.4f+.125f+2f/24+8f/24+.5f))<.0001f,"shortened whiff combo duration");
 Check(hits[0].life==3f/24&&hits[1].life==3f/24,"active windows");
 Check(!Object.ReferenceEquals(hits[0].contact,hits[1].contact),"independent contact records");
 Check(hits[0].damage==.5f&&hits[1].damage==.5f,"each hit owns D/2");
 float damage=0;foreach(var shot in hits)if(shot.contact.TryConsume())damage+=shot.damage;
 foreach(var shot in hits)Check(!shot.contact.TryConsume(),"no duplicate damage per cut");
 Check(damage==1,"total damage capped at original multiplier");
 Check(hits[0].center.x==20+facing*3.2f&&hits[1].center.x==hits[0].center.x,"mirrored fixed warning footprint");
 Check(p.owner.animator.strikes[0]==0&&p.owner.animator.strikes[1]==1,"both real release events");
 var miss=new BasicProbe();Drive(miss.Melee(),miss.owner);Check(miss.owner.shots[1].contact.TryConsume(),"return cut hits if opener misses");
 var cancelled=new BasicProbe();cancelled.owner.Active=false;Drive(cancelled.Melee(),cancelled.owner);Check(cancelled.owner.shots.Count==0,"cancelled windup never releases");
 }
 Console.WriteLine("PASS production melee timeline, mirrored contacts, damage cap, cancellation and two release events");
 }
}
""".replace("CONSUME", consume).replace("MELEE", melee)
        from test_sword_presentation import run_cs
        self.assertIn("PASS production melee timeline", run_cs(code, "BasicProbe", modern=True))

    def test_two_health_losses_use_production_damage_and_defenses(self):
        # Execute the real coroutine, collision/consumption and player damage
        # methods. Only Unity objects and presentation callbacks are stand-ins.
        from test_sword_presentation import method, run_cs
        scripts = ROOT / "Assets/Scripts"
        read = lambda path: (scripts / path).read_text(encoding="utf-8-sig")
        code = r'''
using System;using System.Collections;using System.Collections.Generic;using System.Linq;
struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 zero{get{return new Vector2();}}public static Vector2 right{get{return new Vector2(1,0);}}public static Vector2 one{get{return new Vector2(1,1);}}
 public Vector2 normalized{get{float l=(float)Math.Sqrt(x*x+y*y);return l>0?this*(1/l):zero;}}
 public static Vector2 operator+(Vector2 a,Vector2 b){return new Vector2(a.x+b.x,a.y+b.y);}
 public static Vector2 operator-(Vector2 a,Vector2 b){return new Vector2(a.x-b.x,a.y-b.y);}
 public static Vector2 operator*(Vector2 a,float f){return new Vector2(a.x*f,a.y*f);}
 public static float Distance(Vector2 a,Vector2 b){var d=a-b;return (float)Math.Sqrt(d.x*d.x+d.y*d.y);}
}
struct Vector3{public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
struct Color{public Color(float r,float g,float b){}}
struct Bounds{public Vector2 center,extents;}
static class Time{public static float time;}
static class Mathf{public static float Min(float a,float b){return Math.Min(a,b);}public static float Max(float a,float b){return Math.Max(a,b);}public static float Ceil(float v){return (float)Math.Ceiling(v);}}
enum EnemyAttack{SWBasic}enum ActionState{Telegraph,Attack}enum Essence{Guard}
enum KarmaSfxId{CombatPerfectParry,CombatGuardBlock,PlDamage,CombatBladeHit}
static class KarmaAudio{public static void Play(KarmaSfxId id){}}
static class KarmaSpace{public const float Scale=2;public static float Units(float n){return n*2;}public static Vector2 Units(Vector2 n){return n*2;}}
class Node{public bool active=true;public Node parent{get{return this;}}public Vector3 localScale;public void SetActive(bool v){active=v;}}
class Visual{public bool flipX;public Node gameObject=new Node();public Node transform=new Node();}
class Marker{public Node gameObject=new Node();public Node transform=new Node();}
static class KarmaCombatVFX{public static Visual SpawnSprite(object w,Vector2 p,string g,string n,Vector2 s,float t){return null;}}
static class KarmaVisuals{public static void Flash(object w,Vector2 p,Vector2 s,Color c){}}
static class KarmaProjectile{public static void Spawn(params object[] values){}}
class Animator{public void BeginBasicLink(float t){}public void PlayPlayerAction(string p,float t){}public void Hurt(){}public void StartDamageFlash(float t){}public void PrepareBasicAttack(float t,bool m){}public void PlayPose(string p,float t){}public void BeginBasicRelease(float t,int s){}}
class EssencesStub{public void NotifyUnique(Essence e){}}
class Tutorial{public void GuardBlocked(){}}
class TechniqueConfig{public float counterDamage=3;}
class Config{public Color guardColor;public float hitInvulnerability=.7f;public TechniqueConfig Techniques=new TechniqueConfig();}
class Game{public Config Config=new Config();public Player Player;public bool AllowsPlayerActions=true,Paused;public float DamageTaken;public object World=new object();public List<Owner> Enemies=new List<Owner>();public Tutorial Tutorial;public void PlayerDied(){AllowsPlayerActions=false;}}
CONTACT
GEOMETRY
TECHNIQUES
class Player{
 public Game game;public float Health=160,invulnerableUntil;public bool Shielded,TestInvincible;public int ParryCount,Facing=1;
 public Vector2 Position;public Bounds DamageBounds{get{return new Bounds{center=Position,extents=new Vector2(.6f,1.6f)};}}
 public KarmaTechniqueState techniques=new KarmaTechniqueState();Animator animator=new Animator();EssencesStub Essences=new EssencesStub();
 void FaceAction(){}void CancelAirSlash(){}public void Stun(float t){}public void BeginCounterAt(float t){techniques.ArmCounter(t,1);}
 public void TakeCrowdControlHit(float a,int kind,float seconds,Vector2 p,bool c,float? hitAt){throw new Exception("unexpected crowd control");}
 DAMAGE
}
class Behavior{
 public bool ConsumeQueuedParry(){return false;}public void ContactStarting(EnemyAttack p,KarmaAttackContact c){}
 public bool TryPlayerCrowdControl(EnemyAttack p,KarmaAttackContact c,out int kind,out float seconds){kind=0;seconds=0;return false;}
 public Action<KarmaAttackContact,bool,bool> resolved;public bool CustomHitVisual(EnemyAttack p){return true;}public void ContactResolved(EnemyAttack p,KarmaAttackContact c,Vector2 at,bool parried,bool damaged){if(resolved!=null)resolved(c,parried,damaged);}
 public void OnAttackParried(bool s){}public void OnPlayerDamaged(){}
}
class Shot{public Visual Visual=new Visual();public Node gameObject=new Node();public KarmaAttackContact contact;public float at,life,damage;public Vector2 center,size;}
 class Owner{
 public Game game=new Game();public Vector2 Position=new Vector2(20,2);public float direction=1,clock;public bool Active=true,pendingParry;public int? PrimaryType;
 public float FightSeconds{get{return clock;}}public ActionState state;public EnemyAttack selected;public Behavior behavior=new Behavior();public Animator animator=new Animator();
 public List<Shot> shots=new List<Shot>();public List<Vector2> warnings=new List<Vector2>();public List<float> damageTimes=new List<float>(),losses=new List<float>();public int mode;public bool lateFirst;
 public Owner(){game.Player=new Player{game=game};}
 public bool Alive{get{return Active;}}public Vector2 ClosestDamagePoint(Vector2 p){return Position;}public Bounds DamageBounds{get{return new Bounds{center=Position};}}public void TakeDamage(float n){}
 public float Warning(float n){return n;}public Marker Mark(string n,Vector2 c,Vector2 z){warnings.Add(c);return new Marker();}
 public IEnumerator Wait(float n){yield return n;}public IEnumerator Recovery(float n){yield return n;}
 public Shot Attack(Vector2 c,Vector2 v,Vector2 z,float life,float damage,KarmaAttackContact contact){var s=new Shot{at=clock,center=c,size=z,life=life,damage=damage*16.1f,contact=contact};shots.Add(s);if(mode==5&&shots.Count==1)game.Player.techniques.ArmCounter(clock,1);return s;}
 TRY_HIT
 RESOLVE
 public void Tick(float t){clock=Time.time=t;
  if(mode==6&&shots.Count>0&&t>=shots[0].at+shots[0].life+.05f)Active=false;
  if(!Active)return;
  for(int i=0;i<shots.Count;i++){
   var s=shots[i];if(!s.gameObject.active||t<s.at||t>=s.at+s.life)continue;
   if(lateFirst&&i==0&&t<s.at+s.life-.00002f)continue;
   var p=game.Player;p.Position=mode==3&&i==0?new Vector2(100,100):s.center;
   p.Shielded=mode==8||(mode==1&&i==0)||(mode==2&&i==1);
   if(mode==4&&i==0)p.invulnerableUntil=s.at+.6f;
   float before=p.Health;
   TryAttackHit(s.center,s.center,s.size,s.damage,s.contact,0);
   if(p.Health<before){damageTimes.Add(t);losses.Add(before-p.Health);}
  }
 }
 public void Advance(float duration){float start=clock,end=start+duration;for(int i=0;i<25;i++)Tick(start+duration*i/25);Tick(end-.00001f);clock=Time.time=end;}
}
class Sword{public Vector2 BasicMeleeSize=new Vector2(6.6f,3.6f);public float BasicMeleeOffset=3.2f;}
class SettingsData{public float basicDamageMultiplier=.5f;}
static class KarmaSwordAttackPolish{public static void Warning(Marker m){}public static void Projectile(Game g,Owner o,Shot s,Vector2 v,Vector2 z){}}
static class KarmaSwordBasicPresentation{public static void MeleeWarning(Game g,Owner o,Marker n,Vector2 z,float t,int s=0){}}
public class HealthProbe{
 Owner owner=new Owner();Sword sword=new Sword();SettingsData Settings=new SettingsData();static void Destroy(object o){}
 KarmaAttackContact firstMeleeContact;float firstMeleeResumeAt;
 CALLBACK
 MELEE
 void Drive(IEnumerator e){while(owner.Active&&e.MoveNext()){var nested=e.Current as IEnumerator;if(nested!=null)Drive(nested);else if(e.Current is float)owner.Advance((float)e.Current);}}
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 public static void Run(){int cases=0;
 foreach(int facing in new[]{-1,1})foreach(bool late in new[]{false,true})foreach(float immunity in new[]{0f,.7f,1.2f})foreach(int mode in new[]{0,1,2,3,4,5,6,7,8}){
  var p=new HealthProbe();p.owner.behavior.resolved=p.ContactResolved;p.owner.direction=facing;p.owner.mode=mode;p.owner.lateFirst=late;p.owner.game.Config.hitInvulnerability=immunity;
  if(mode==7)p.owner.Active=false;p.Drive(p.Melee());var o=p.owner;var hits=o.shots;
  if(mode==7){Check(hits.Count==0,"windup interruption");cases++;continue;}
  if(mode==6){Check(hits.Count==1,"interruption prevents second emission");cases++;continue;}
  Check(hits.Count==2&&!Object.ReferenceEquals(hits[0].contact,hits[1].contact),"two independent contacts");
  if(hits[0].contact.DealtDamage)Check(hits[1].at-o.damageTimes[0]>=immunity+.0009f,"return cut respects actual early or latest first-hit immunity deadline");
  else if(mode==5)Check(hits[1].at-hits[0].at<.26f,"late parry keeps its brief protection before the return cut");
  else Check(Math.Abs(hits[1].at-hits[0].at-(5f/24))<.0001f,"miss/guard links promptly without extra damage");
  Check(!hits[0].gameObject.active&&!hits[1].gameObject.active,"both contacts explicitly retired");
  int expected=mode==8||mode==4?0:mode==0?2:1;
  Check(o.damageTimes.Count==expected,"number of real Health decreases, mode "+mode+" late "+late);
  foreach(float loss in o.losses)Check(Math.Abs(loss-4.025f)<.0001f,"fractional D/2, no catch-up damage");
  Check(o.game.DamageTaken<=8.0501f,"combo total capped at D");
  if(mode==0){Check(o.damageTimes[1]>o.damageTimes[0]+immunity,"distinct Health loss times after i-frames");Check(Math.Abs(o.game.Player.Health-(160-8.05f))<.0001f,"two losses sum to D");}
  if(mode==5)Check(o.game.Player.ParryCount==1&&!hits[0].contact.DealtDamage,"parried contact consumes no health");
  foreach(var h in hits)if(h.contact.Consumed)Check(!h.contact.TryConsume(),"repeat overlap cannot deal a second hit");cases++;
 }
 Console.WriteLine("PASS "+cases+" production health/contact scenarios: early/latest contact, fractional damage, immunity, miss, dodge, guard, parry and interruption");}
}'''
        for token, path, signature in [
            ("CONTACT", "Core/KarmaBossRules.cs", "public sealed class KarmaAttackContact"),
            ("GEOMETRY", "Core/KarmaBossRules.cs", "public static class KarmaBossGeometry"),
            ("TECHNIQUES", "Core/KarmaTechniqueState.cs", "public sealed class KarmaTechniqueState"),
            ("DAMAGE", "Player/KarmaPlayer.Defense.cs", "internal void TakeDamage(float damage, bool counterDamage"),
            ("TRY_HIT", "Combat/KarmaEnemy.Presentation.cs", "public void TryAttackHit("),
            ("RESOLVE", "Combat/KarmaEnemy.Presentation.cs", "void ResolveAttackHit("),
            ("MELEE", "SwordWave/Patterns/SWBasic.cs", "IEnumerator Melee()"),
            ("CALLBACK", "SwordWave/Patterns/SWBasic.cs", "public void ContactResolved("),
        ]:
            code = code.replace(token, method(read(path), signature))
        self.assertIn("PASS 108 production health/contact scenarios", run_cs(code, "HealthProbe", modern=True))

if __name__ == "__main__":
    unittest.main()
