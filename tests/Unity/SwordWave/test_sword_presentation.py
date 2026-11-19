"""Offline source-execution and source-pixel checks; never starts Unity."""
import math
import json
import os
import re
import subprocess
import tempfile
import unittest
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
SWORD = ROOT / "Assets/Scripts/SwordWave"


def method(source, signature):
    start = source.index(signature)
    opening = source.index("{", start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return source[start:end]


def run_cs(code, entry, modern=False):
    code = "#pragma warning disable 0414,0169,0649\n" + code
    if modern:
        # Compile unchanged production C# (including out-var and null-conditional
        # syntax) with Unity's bundled SDK, without starting the Unity Editor.
        version = re.search(r"m_EditorVersion: (.+)", (ROOT / "ProjectSettings/ProjectVersion.txt").read_text())[1]
        sdk = Path(os.environ["ProgramFiles"]) / "Unity/Hub/Editor" / version / "Editor/Data/DotNetSdk"
        compiler = next(sdk.glob("sdk/*/Roslyn/bincore/csc.dll"))
        references = sorted(sdk.glob("packs/Microsoft.NETCore.App.Ref/*/ref/net8.0/*.dll"))
        if not references:
            raise AssertionError("Missing bundled .NET reference pack")
        runtime = sorted((sdk / "shared/Microsoft.NETCore.App").iterdir())[-1].name
        with tempfile.TemporaryDirectory(prefix="karma-source-check-") as directory:
            temp = Path(directory)
            source = temp / "Probe.cs"
            source.write_text(code + "\nclass OfflineEntry { static void Main() { " + entry + ".Run(); } }", encoding="utf-8")
            response = temp / "compile.rsp"
            response.write_text('\n'.join(["-noconfig", "-nostdlib+", "-target:exe", '-out:"' + str(temp / "Probe.dll") + '"']
                                          + ['-r:"' + str(p) + '"' for p in references] + ['"' + str(source) + '"']), encoding="utf-8")
            result = subprocess.run([str(sdk / "dotnet.exe"), str(compiler), "@" + str(response)], capture_output=True, text=True, encoding="utf-8")
            if result.returncode:
                raise AssertionError(result.stdout + result.stderr)
            (temp / "Probe.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"tfm": "net8.0", "framework": {"name": "Microsoft.NETCore.App", "version": runtime}}}))
            result = subprocess.run([str(sdk / "dotnet.exe"), str(temp / "Probe.dll")], capture_output=True, text=True, encoding="utf-8")
            if result.returncode:
                raise AssertionError(result.stdout + result.stderr)
            return result.stdout
    command = "[Console]::InputEncoding=New-Object System.Text.UTF8Encoding;$c=[Console]::In.ReadToEnd();Add-Type -TypeDefinition $c -ErrorAction Stop;[" + entry + "]::Run()"
    result = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", command],
                            input=code, text=True, encoding="utf-8", capture_output=True)
    if result.returncode:
        raise AssertionError(result.stderr + result.stdout)
    return result.stdout


class PresentationChecks(unittest.TestCase):
    def test_melee_warning_geometry_direction_and_scope(self):
        from test_sw05_eruption_mesh import SHIM
        source = (SWORD / "Visuals/KarmaSwordAttackPolish.cs").read_text(encoding="utf-8-sig")
        shim = SHIM.split("static class KarmaSW05RainBlade", 1)[0]
        code = shim + r"""
class Renderer{public bool enabled=true,forceRenderingOff;}
class Transform{public Vector3 position;}
class Probe{
 const int Points=9,Strokes=16,VerticesPerStroke=Points*4;
 class Buffers{public Vector2[] path=new Vector2[Points];public Vector3[] vertices=new Vector3[Strokes*VerticesPerStroke];public Color[] colors=new Color[Strokes*VerticesPerStroke];}
 Buffers buffer=new Buffers();Renderer source=new Renderer();Renderer[] warningLines=new[]{new Renderer(),new Renderer()};Transform transform=new Transform(),scope=new Transform();
 float age,life=.4f,facing=1;Vector2 size=new Vector2(6.6f,3.6f);int strikeIndex,stroke;
 STROKE
 PAINT
 static void Check(bool b,string why){if(!b)throw new Exception(why);}
 public static void Run(){int cases=0;
 foreach(int face in new[]{-1,1})foreach(int strike in new[]{0,1}){
  var p=new Probe{facing=face,strikeIndex=strike};float previous=0;
  for(int frame=0;frame<=24;frame++){
   p.age=p.life*frame/24;p.stroke=0;p.PaintWarning();
   Check(p.stroke==6,"fixed six ribbons, no growing allocations");
   Check(!p.source.enabled&&p.source.forceRenderingOff&&!p.warningLines[0].enabled,"generic lane hidden");
   for(int i=0;i<p.stroke*VerticesPerStroke;i++){var v=p.buffer.vertices[i];var c=p.buffer.colors[i];Check(!float.IsNaN(v.x)&&!float.IsNaN(v.y),"finite vertices");Check(Math.Abs(v.x)<=p.size.x*.5f&&Math.Abs(v.y)<=p.size.y*.5f,"warning inside committed hitbox");Check(c.a>=0&&c.a<=.751f,"bounded warning opacity");}
   float head=p.buffer.vertices[VerticesPerStroke+(Points-1)*4].y;
   if(frame>0)Check(strike==0?head<=previous+.00001f:head>=previous-.00001f,"stroke previews actual swing direction");previous=head;cases++;
  }
 }Console.WriteLine("PASS "+cases+" melee warning frames");}
}
"""
        code = code.replace("STROKE", method(source, "void Stroke("))
        code = code.replace("PAINT", method(source, "void PaintWarning()"))
        self.assertIn("PASS 100 melee warning frames", run_cs(code, "Probe", modern=True))
        pattern = (SWORD / "Patterns/SWBasic.cs").read_text(encoding="utf-8-sig")
        self.assertEqual(method(pattern, "IEnumerator Melee()").count("MeleeWarning("), 2)
        self.assertNotIn("FlowWarning(", method(pattern, "IEnumerator Melee()"))

    def test_attack_sweep_matches_advertised_polygon_with_player_body(self):
        rules=(ROOT / "Assets/Scripts/Core/KarmaBossRules.cs").read_text(encoding="utf-8-sig")
        code=rules+r"""
public static class ContactProbe {
 struct P{public double x,y;public P(double a,double b){x=a;y=b;}}
 // Independent polygon clipping oracle, not the gameplay SAT implementation.
 static bool ClipHit(P[] polygon,double px,double py,double hx,double hy){
  var p=new System.Collections.Generic.List<P>(polygon);
  for(int side=0;side<4;side++){
   var q=new System.Collections.Generic.List<P>();
   for(int i=0;i<p.Count;i++){
    P a=p[i],b=p[(i+1)%p.Count];double da=side==0?a.x-px+hx:side==1?px+hx-a.x:side==2?a.y-py+hy:py+hy-a.y;
    double db=side==0?b.x-px+hx:side==1?px+hx-b.x:side==2?b.y-py+hy:py+hy-b.y;
    if(da>=0)q.Add(a);if((da>=0)!=(db>=0)){double t=da/(da-db);q.Add(new P(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t));}
   }p=q;if(p.Count==0)return false;
  }return true;
 }
 public static void Run(){int cases=0;var random=new System.Random(41);
  // Static basic melee/SW03/blasts plus horizontal and diagonal projectiles.
  foreach(float length in new[]{1.1f,3.2f,6.6f,12f})foreach(float thickness in new[]{.6f,1.8f,3.6f,6.94f})
  foreach(float distance in new[]{0f,.1f,2f,35f})foreach(float angle in new[]{0f,.3f,1.2f,2.4f,3.1415926f,4.1f}){
   float ux=(float)System.Math.Cos(angle),uy=(float)System.Math.Sin(angle),nx=-uy,ny=ux;
   float ax=12,ay=3,bx=ax+ux*distance,by=ay+uy*distance;
   var polygon=new[]{new P(ax-ux*length/2-nx*thickness/2,ay-uy*length/2-ny*thickness/2),new P(ax-ux*length/2+nx*thickness/2,ay-uy*length/2+ny*thickness/2),new P(bx+ux*length/2+nx*thickness/2,by+uy*length/2+ny*thickness/2),new P(bx+ux*length/2-nx*thickness/2,by+uy*length/2-ny*thickness/2)};
   for(int i=0;i<80;i++){
    float px=ax+(float)(random.NextDouble()*50-15),py=ay+(float)(random.NextDouble()*35-10),hx=.55f,hy=.95f;
    bool hit=KarmaPrototype.KarmaBossGeometry.SweptOrientedBox(ax,ay,bx,by,px,py,ux,uy,length,thickness,hx,hy);
    if(hit!=ClipHit(polygon,px,py,hx,hy))throw new System.Exception("damage outside advertised swept polygon");
    if(angle==0&&hit!=KarmaPrototype.KarmaBossGeometry.SweptBox(ax,ay,bx,by,px,py,length/2+hx,thickness/2+hy))throw new System.Exception("static/horizontal geometry disagrees");
    cases++;
   }
  }System.Console.WriteLine("PASS "+cases+" swept contacts against independently clipped warning polygons");
 }}"""
        self.assertIn("PASS 30720 swept contacts",run_cs(code,"ContactProbe",modern=True))


    def test_flowing_path_geometry_stays_finite_and_inside_footprint(self):
        from test_sw05_eruption_mesh import SHIM
        source = (SWORD / "Visuals/KarmaSwordAttackPolish.cs").read_text(encoding="utf-8-sig")
        flow = source.split("public sealed class KarmaSwordPathWarning", 1)[1].split("// A room-local", 1)[0]
        shim = SHIM.split("static class KarmaSW05RainBlade", 1)[0]
        shim = shim.replace("public float x,y,z;", "public static Vector3 zero;public float x,y,z;")
        shim = shim.replace("public float x,y;", "public float x,y;public float sqrMagnitude{get{return x*x+y*y;}}")
        code = shim + r'''
struct Bounds { public Bounds(Vector3 center,Vector3 size){} }
class Mesh { public void RecalculateBounds(){} public Bounds bounds;public void SetVertices(Vector3[] a){}public void SetColors(Color[] a){} }
class SpriteRenderer { public bool enabled=true,forceRenderingOff; }
class KarmaGame {}
class LineRenderer { public bool enabled=true; }
class Transform { public Vector3 lossyScale;public Vector2 InverseTransformVector(Vector2 v){return new Vector2(v.x/lossyScale.x,v.y/lossyScale.y);} }
public class FlowProbe {
 const int Segments=48,Capacity=Segments*6;
 Vector3[] vertices=new Vector3[Capacity*4];Color[] colors=new Color[Capacity*4];
 Mesh mesh=new Mesh();SpriteRenderer source=new SpriteRenderer();LineRenderer[] genericLines={new LineRenderer(),new LineRenderer()};
 Transform transform=new Transform();float age,direction;int count;
 Vector2 pathAlong=Vector2.right,pathAcross=Vector2.up;float pathWidth,strength=1;
 void SetPath(KarmaGame director,SpriteRenderer marker,float facing){source=marker;direction=facing;Paint();}
 COUNTER_PATH
 PROJECT
 QUAD
 ENVELOPE
 PAINT
 public static void Run(){var p=new FlowProbe();int cases=0;
  foreach(float width in new[]{.1f,4f,70f})foreach(float height in new[]{.1f,2f,9f})foreach(float facing in new[]{-1f,1f})foreach(float t in new[]{0f,.2f,.7f,2f}){
   p.transform.lossyScale=new Vector3(width,height,1);p.age=t;p.direction=facing;p.Paint();
   if(p.count!=Capacity||p.source.enabled||!p.source.forceRenderingOff)throw new Exception("old rectangular picture or buffer mismatch");
   foreach(var line in p.genericLines)if(line.enabled)throw new Exception("old box outline remains visible");
   float lo=1,hi=-1;
   for(int i=0;i<p.count*4;i++){var v=p.vertices[i];var c=p.colors[i];
    if(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(c.a)||Math.Abs(v.x)>.50001f||Math.Abs(v.y)>.50001f)throw new Exception("invalid or misleading warning footprint");
    if(c.a>.231f)throw new Exception("warning crest is too opaque");
    if(Math.Abs(v.y)<.35f && c.a>.031f)throw new Exception("central fill obscures play");
    if(c.b<c.r||c.b<c.g)throw new Exception("unexpected warning palette");lo=Math.Min(lo,v.y);hi=Math.Max(hi,v.y);
   }
   if(lo>-.5f||hi<.5f)throw new Exception("danger width was visually narrowed");cases++;
  }
  int diagonalCases=0;
  for(int angle=0;angle<12;angle++)foreach(float length in new[]{.1f,12f,60f})foreach(float width in new[]{.3f,4f}){
   float radians=angle*Mathf.PI/6;Vector2 axis=new Vector2(Mathf.Cos(radians),Mathf.Sin(radians)),normal=new Vector2(-axis.y,axis.x);
   Vector2 size=new Vector2(7,width),from=new Vector2(0,0),to=axis*length;
   p.transform.lossyScale=new Vector3(Math.Abs(axis.x)*(length+size.x)+Math.Abs(normal.x)*width,Math.Abs(axis.y)*(length+size.x)+Math.Abs(normal.y)*width,1);
   p.age=.37f;float trackingAlpha=0;
   foreach(bool locked in new[]{false,true}){
    p.SetCounterPath(null,p.source,from,to,size,locked);float peak=0,loAlong=1000,hiAlong=-1000,loAcross=1000,hiAcross=-1000;
    for(int i=0;i<p.count*4;i++){
     var v=p.vertices[i];float wx=v.x*p.transform.lossyScale.x,wy=v.y*p.transform.lossyScale.y;
     float along=wx*axis.x+wy*axis.y,across=wx*normal.x+wy*normal.y;
     if(float.IsNaN(v.x)||float.IsNaN(v.y)||Math.Abs(v.x)>.50001f||Math.Abs(v.y)>.50001f||Math.Abs(along)>(length+size.x)*.5f+.0001f||Math.Abs(across)>width*.5f+.0001f)throw new Exception("diagonal warning leaked into safe corners");
     loAlong=Math.Min(loAlong,along);hiAlong=Math.Max(hiAlong,along);loAcross=Math.Min(loAcross,across);hiAcross=Math.Max(hiAcross,across);peak=Math.Max(peak,p.colors[i].a);
    }
    if(hiAlong-loAlong<length+size.x-.0001f||hiAcross-loAcross<width-.0001f)throw new Exception("counter warning lost its end caps or width");
    if(!locked)trackingAlpha=peak;else if(peak<trackingAlpha*1.4f)throw new Exception("locked direction is not emphasized");
    if(p.direction!=1)throw new Exception("counter flow opposes the oriented launch axis");diagonalCases++;
   }
  }
  long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<120;i++)p.Paint();
  if(GC.GetAllocatedBytesForCurrentThread()!=before)throw new Exception("per-frame managed allocations");
  Console.WriteLine("PASS "+cases+" flowing path geometry cases; PASS "+diagonalCases+" diagonal counter cases; zero frame allocations");
 }
}'''
        envelope = re.search(r"float Envelope\([^;]+;", flow)[0]
        project = re.search(r"Vector2 Project\([^;]+;", flow)[0]
        code = code.replace("COUNTER_PATH", method(flow, "public void SetCounterPath(").replace("public void", "void"))
        code = code.replace("PROJECT", project).replace("QUAD", method(flow, "void Quad(")).replace("ENVELOPE", envelope + method(flow, "float EdgeGlint(")).replace("PAINT", method(flow, "void Paint()"))
        result = run_cs(code, "FlowProbe", modern=True)
        self.assertIn("PASS 72 flowing path geometry cases", result)
        self.assertIn("PASS 144 diagonal counter cases", result)

    def test_120_second_close_selection_simulation(self):
        source = (SWORD / "KarmaSwordBossBehavior.cs").read_text(encoding="utf-8-sig")
        selection = method(source, "bool TrySelectSwordAttack(")
        # Baseline is the exact previous selection condition, not a different
        # random policy. Same seed, cooldown, durations and close target in both.
        added = "\n                    || !counter.Formed && BasicMeleeCanReach(owner.Position, owner.game.Player.Position)"
        self.assertIn(added, selection)
        baseline = selection.replace(added, "").replace("TrySelectSwordAttack", "BeforeSelection")
        movement = (SWORD / "KarmaSwordBossMovement.cs").read_text(encoding="utf-8-sig")
        retreat = method(movement, "bool RetreatSword(").split("                    if (!ChooseRetreat", 1)[0]
        retreat = retreat.replace("RetreatSword", "RetreatOpportunity") + "return true; } return false; }"
        hold = "                    if (chargeRemaining < 0 && !behavior.EvasionOnly && !pressure && behavior.CloseBasicReady\n                        && SwordRetreatClearance(-1, 6) >= 6 && SwordRetreatClearance(1, 6) >= 6) return false;\n"
        self.assertIn(hold, retreat)
        before_retreat = retreat.replace(hold, "").replace("RetreatOpportunity", "BeforeRetreat")
        close_ready = source.split("public bool CloseBasicReady =>", 1)[1].split(";", 1)[0]
        profile = (ROOT / "Assets/Data/Resources/KarmaSwordBossProfile.asset").read_text()
        weights = (ROOT / "Assets/Data/Resources/KarmaSwordPatternWeights.asset").read_text()
        def number(name):
            return re.search(r"\b"+name+r": ([\d.]+)", profile+weights).group(1)+"f"
        values = "".join("public float "+n+"="+number(n)+";" for n in
                         ("swordBasicCooldown","meleeBasicCooldown","waveBasicCooldown","pressureRetreatStartDistance"))
        weight_values = "".join("public float "+n+"="+number(n)+";" for n in
                                ("swBasicWeight","sw01Weight","sw02Weight","sw03Weight","sw04Weight","closeBasicMultiplier"))
        code = r'''
using System;using System.Collections.Generic;
enum EnemyAttack{SWBasic,SW01,SW02,SW03,SW04,SW05}
struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
static class Random{public static System.Random rng;public static float value{get{return (float)rng.NextDouble();}}}
static class KarmaSpace{public static float Units(float v){return v*2;}}
static class Mathf{public static float Abs(float a){return Math.Abs(a);}public static int Min(int a,int b){return Math.Min(a,b);}public static float Min(float a,float b){return Math.Min(a,b);}public static float Max(float a,float b){return Math.Max(a,b);}public static int Clamp(int a,int b,int c){return Math.Max(b,Math.Min(a,c));}}
class Player{public Vector2 Position=new Vector2(24,2);}
class Game{public Player Player=new Player();}
class Owner{public EnemyAttack previous=EnemyAttack.SW02,selected;public Vector2 Position=new Vector2(20,2);public float FightSeconds;public Game game=new Game();public List<EnemyAttack> attacks=new List<EnemyAttack>{EnemyAttack.SW01,EnemyAttack.SW02};}
class Ground{public bool Running=false;}
class Counter{public bool Formed=false;}
class Ultimate{public bool ThresholdPending=false;}
class Weights{ WEIGHTS public bool allowRepeatFallback=true;}
class SettingsData{VALUES public int swordSkillsBetweenBasics=1;public Weights Weights=new Weights();}
class Movement{public void OnSelected(){}public void OnCompleted(){}}
public class SelectionProbe{
 Owner owner=new Owner();Ground ground=new Ground();Counter counter=new Counter();Ultimate ultimate=new Ultimate();SettingsData Settings=new SettingsData();Movement movement=new Movement();
 bool swordBasicDue=true,basicAfterRetreat=false,skillAfterRetreat=false,BasicMeleeSelected=true;float nextSwordBasic;int consecutiveBasics,swordSkillsSinceBasic,attacksSinceGround;
 SelectionProbe behavior{get{return this;}}public bool CloseBasicReady{get{return CLOSE;}}
 public float BasicMeleeRange=6.6f,BasicWaveRange=10;public bool EvasionOnly{get{return ground.Running;}}
 float swordRetreatRemaining=0,swordPressureTarget=0,nextSwordRetreat=0;bool swordPressurePending=false,swordAttackedSinceRetreat=false;
 float SwordSpacingDistance(){return 9.5f;}float SwordRetreatClearance(float side,float length){return length;}
 RETREAT
 BEFORE_RETREAT
 void SelectBasicMode(){BasicMeleeSelected=true;}void ClearFinalDetonation(){}
 bool BasicMeleeCanReach(Vector2 a,Vector2 b){return Math.Abs(a.x-b.x)<=6;}
 bool SwordAttackCanReach(EnemyAttack a,Vector2 b){return true;}
 // This fixture unlocks only SW01/SW02; SW01 lacks room at a four-unit gap.
 bool SwordCandidateAvailable(EnemyAttack a,Vector2 from,float distance,bool sight,bool escape){return a==EnemyAttack.SW02;}
 SELECTION
 BASELINE
 SELECTED
 COMPLETED
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 static int[] Sim(bool after,int seed,bool spacing=false){
 Random.rng=new System.Random(seed);var p=new SelectionProbe();int basics=0,skills=0,chain=0;
 while(p.owner.FightSeconds<120){
 if(spacing&&(after?p.RetreatOpportunity(1f/60,3):p.BeforeRetreat(1f/60,3))){
 // Open-floor pursuer follows at a three-unit gap without attacking.
 // The source movement gates decide whether to retreat; one .6s step
 // represents the existing travel/acceleration/settling budget.
 p.nextSwordRetreat=p.owner.FightSeconds+2.4f;p.owner.FightSeconds+=.6f;
 p.swordAttackedSinceRetreat=false;p.skillAfterRetreat=true;
 }
 EnemyAttack next;bool picked=after?p.TrySelectSwordAttack(3,true,true,out next):p.BeforeSelection(3,true,true,out next);
 if(!picked){p.owner.FightSeconds+=.1f;continue;}
 p.owner.selected=next;p.OnSelected();p.owner.previous=next;
 if(next==EnemyAttack.SWBasic){basics++;chain++;Check(chain<=2,"no unbounded basics");}
 else{skills++;chain=0;}
 // Unenraged open-floor event model. Existing minimum recovery and .22
 // decision delay; no wall/pressure/movement model is claimed here.
 p.owner.FightSeconds+=next==EnemyAttack.SWBasic?.4f+21f/24+8f/24+.38f+.22f:.34f+.8f+.10f+.605f+.22f;
 p.OnCompleted();
 p.swordAttackedSinceRetreat=true;
 }
 Check(skills>0,"special attacks still available");return new[]{basics,skills};
 }
 public static void Run(){
 int before=0,after=0;for(int seed=0;seed<20;seed++){var b=Sim(false,seed);var a=Sim(true,seed);before+=b[0];after+=a[0];}
 Console.WriteLine("aggregate melee "+before+" -> "+after);Check(after>before*1.05f,"close-frequency increase before retreat gating");
 var p=new SelectionProbe();EnemyAttack next;p.ground.Running=true;Check(!p.TrySelectSwordAttack(4,true,true,out next),"no basics during SW03");
 p.ground.Running=false;p.owner.attacks.Add(EnemyAttack.SW05);p.ultimate.ThresholdPending=true;Check(p.TrySelectSwordAttack(4,true,true,out next)&&next==EnemyAttack.SW05,"SW05 priority");
 p.owner.selected=EnemyAttack.SW03;p.OnSelected();Check(p.attacksSinceGround==0,"ground selection resets starvation count");
 for(int i=0;i<4;i++){p.owner.selected=EnemyAttack.SW02;p.OnSelected();Check(p.attacksSinceGround==Math.Min(3,i+1),"ordinary attacks accumulate capped starvation count");}
 p.ultimate.ThresholdPending=false;p.skillAfterRetreat=true;p.owner.previous=EnemyAttack.SWBasic;Check(p.TrySelectSwordAttack(4,true,true,out next)&&next==EnemyAttack.SW02,"skill follow-up survives");
 var oneBefore=Sim(false,7);var oneAfter=Sim(true,7);
 Console.WriteLine("120s seed7 melee "+oneBefore[0]+" -> "+oneAfter[0]+"; skills "+oneBefore[1]+" -> "+oneAfter[1]+"; 20-seed mean melee "+before/20f+" -> "+after/20f);
 before=after=0;for(int seed=0;seed<20;seed++){before+=Sim(false,seed,true)[0];after+=Sim(true,seed,true)[0];}
 oneBefore=Sim(false,7,true);oneAfter=Sim(true,7,true);
 Check(after>before*1.2f,"close attacks are not repeatedly preempted by unforced retreats");
 Console.WriteLine("120s with production retreat gates seed7 melee "+oneBefore[0]+" -> "+oneAfter[0]+"; skills "+oneBefore[1]+" -> "+oneAfter[1]+"; 20-seed mean "+before/20f+" -> "+after/20f);
 }
}'''
        for token,value in [("SELECTION",selection),("BASELINE",baseline),("BEFORE_RETREAT",before_retreat),("RETREAT",retreat),("CLOSE",close_ready),
                            ("SELECTED",method(source,"public override void OnSelected()").replace(" override", "")),
                            ("COMPLETED",method(source,"public override void OnCompleted()").replace(" override", "")),
                            ("VALUES",values),("WEIGHTS",weight_values)]:
            code=code.replace(token,value)
        print(run_cs(code,"SelectionProbe").strip())

    def test_ground_skill_participates_in_close_selection(self):
        source = (SWORD / "KarmaSwordBossBehavior.cs").read_text(encoding="utf-8-sig")
        code = r'''
using System;using System.Collections.Generic;
enum EnemyAttack{SWBasic,SW01,SW02,SW03,SW04,SW05}
struct Vector2{}
static class Random{static System.Random rng=new System.Random(43);public static float value{get{return (float)rng.NextDouble();}}}
static class KarmaSpace{public static float Units(float n){return n*2;}}
static class Mathf{public static float Max(float a,float b){return Math.Max(a,b);}}
class Player{public Vector2 Position;}
class Game{public Player Player=new Player();}
class Owner{public EnemyAttack previous=(EnemyAttack)(-1);public float FightSeconds=10;public Vector2 Position;public Game game=new Game();public List<EnemyAttack> attacks=new List<EnemyAttack>{EnemyAttack.SW01,EnemyAttack.SW02,EnemyAttack.SW03,EnemyAttack.SW04,EnemyAttack.SW05};}
class Ground{public bool Running;}
class Counter{public bool Formed;public float NextReadyAt;}
class Ultimate{public bool ThresholdPending;}
class Weights{public float swBasicWeight=3,sw01Weight=3,sw02Weight=3,sw03Weight=3,sw04Weight=3,closeBasicMultiplier=2;public bool allowRepeatFallback=true;}
class SettingsData{public Weights Weights=new Weights();}
class Base{protected virtual bool Eligible(EnemyAttack attack,float distance,bool sight,bool escape){return true;}}
class ScarSelectionProbe:Base{
 Owner owner=new Owner();Ground ground=new Ground();Counter counter=new Counter();Ultimate ultimate=new Ultimate();SettingsData Settings=new SettingsData();
 float nextSwordBasic=float.PositiveInfinity;bool skillAfterRetreat=true,swordBasicDue,basicAfterRetreat,reachable=true;int consecutiveBasics,attacksSinceGround;
 bool BasicMeleeCanReach(Vector2 from,Vector2 target){return true;}
 bool SwordAttackCanReach(EnemyAttack attack,Vector2 target){return true;}
 bool SwordAttackCanReachFrom(EnemyAttack attack,Vector2 from,Vector2 target){return reachable;}
 ROOM
 ELIGIBLE
 CANDIDATE
 SELECT
 static void Check(bool value,string why){if(!value)throw new Exception(why);}
 public static void Run(){var p=new ScarSelectionProbe();EnemyAttack next;int closeCount=0;
  foreach(float distance in new[]{2f,4f,7.99f,8f,12f}){
   int scars=0;
   for(int i=0;i<6000;i++){Check(p.TrySelectSwordAttack(distance,true,true,out next),"selection remains possible");if(next==EnemyAttack.SW03)scars++;if(distance<8)Check(next!=EnemyAttack.SW01,"SW01 still needs wave room");}
   Check(scars>1200&&scars<2400,"ground skill participates without monopolizing pool");if(distance<8)closeCount+=scars;
  }
  p.ground.Running=true;Check(!p.TrySelectSwordAttack(4,true,true,out next),"active ground phase blocks new attacks");p.ground.Running=false;
  p.counter.Formed=true;Check(!p.SwordCandidateAvailable(EnemyAttack.SW03,p.owner.Position,4,true,true),"ward excludes ground cast");p.counter.Formed=false;
  p.Settings.Weights.sw03Weight=0;Check(!p.SwordCandidateAvailable(EnemyAttack.SW03,p.owner.Position,4,true,true),"zero weight disables ground cast");p.Settings.Weights.sw03Weight=3;
  p.reachable=false;Check(!p.SwordCandidateAvailable(EnemyAttack.SW03,p.owner.Position,4,true,true),"real reach check still applies");p.reachable=true;
  p.owner.previous=EnemyAttack.SW03;for(int i=0;i<200;i++){p.TrySelectSwordAttack(4,true,true,out next);Check(next!=EnemyAttack.SW03,"immediate repeat blocked");}
  p.nextSwordBasic=0;p.skillAfterRetreat=false;p.swordBasicDue=true;Check(p.TrySelectSwordAttack(4,true,true,out next)&&next==EnemyAttack.SWBasic,"due basic retains priority");
  p.ultimate.ThresholdPending=true;Check(p.TrySelectSwordAttack(4,true,true,out next)&&next==EnemyAttack.SW05,"health ultimate retains priority");
  p.ultimate.ThresholdPending=false;p.owner.previous=EnemyAttack.SW02;p.attacksSinceGround=3;
  foreach(bool final in new[]{false,true}){
   if(!final)p.owner.attacks.Remove(EnemyAttack.SW05);else p.owner.attacks.Add(EnemyAttack.SW05);
   Check(p.TrySelectSwordAttack(4,true,true,out next)&&next==EnemyAttack.SW03,"overdue SW03 beats due close basic in inherited and final forms");
   p.counter.Formed=true;Check(p.TrySelectSwordAttack(4,true,true,out next)&&next!=EnemyAttack.SW03,"overdue cast still waits for ward");p.counter.Formed=false;
   p.owner.attacks.Remove(EnemyAttack.SW03);Check(p.TrySelectSwordAttack(4,true,true,out next)&&next!=EnemyAttack.SW03,"never unlock unowned skill");p.owner.attacks.Add(EnemyAttack.SW03);
  }
  Console.WriteLine("PASS close SW03 selections "+closeCount+"/18000; distance, ward, reach, weight, repeat and priority guards");
 }
}'''
        for token, signature in (("ROOM", "public bool HasWaveRoom("),
                                 ("ELIGIBLE", "protected override bool Eligible("),
                                 ("CANDIDATE", "bool SwordCandidateAvailable("),
                                 ("SELECT", "bool TrySelectSwordAttack(")):
            code = code.replace(token, method(source, signature))
        result = run_cs(code, "ScarSelectionProbe", modern=True)
        self.assertIn("PASS close SW03 selections", result)
        print(result.strip())

    def test_actual_body_dispatch_and_blade_directions(self):
        # Execute production dispatch, ownership and frame selection together.
        # Sprite names below are the six real imported subasset identities.
        main = (SWORD / "Visuals/KarmaCharacterAnimator.Sword.cs").read_text(encoding="utf-8-sig")
        body = (SWORD / "Visuals/KarmaCharacterAnimator.SwordSW02.cs").read_text(encoding="utf-8-sig")
        code = r'''
using System;using System.Collections.Generic;
enum EnemyAttack{SWBasic,SW01,SW02,SW03,SW04,SW05}
static class Time{public static float time=10;}
static class Mathf{public static int Min(int a,int b){return Math.Min(a,b);}public static float Min(float a,float b){return Math.Min(a,b);}public static float Max(float a,float b){return Math.Max(a,b);}}
class Sprite{public string name;public Sprite(string n){name=n;}}
struct Vector3{public static Vector3 zero,up;public static Vector3 operator +(Vector3 a,Vector3 b){return a;}public static Vector3 operator *(Vector3 a,float b){return a;}}
class Transform{public Vector3 localPosition;}
class Renderer{public Sprite sprite;public Transform transform=new Transform();}
class BodyOwner{public int CurrentPattern=0;public bool Stunned=false;}
public class PoseProbe{
 Vector3 SwordFoot,SwordVisualHome,pictureHome;float swordReferenceSole;Sprite[] basicReturnFrames; Sprite[] swordSkillFrames; int swordLinkLowFrame=10; float basicLinkAt=10,basicLinkSeconds=.6f; float BasicLinkAge {get{return Time.time-basicLinkAt;}} int GroundStrikeFrame(){return 14;} void ApplySwordSkillFrame(int i){picture.sprite=swordSkillFrames[i];}
 Renderer picture=new Renderer();Sprite[] sw02Frames;Dictionary<string,Sprite> poses=new Dictionary<string,Sprite>();
 bool basicReleaseStarted=true,basicPresentationActive=true,SW02UsesIntermediateFrame=false,SwordBodyVisible,swordWalkAllowed;
 float SwordGroundProgress,SwordAscentProgress; float hurtUntil=0,deathAt=-1,swordBodyReleasedAt=10,swordPreviousRelease=-100,swordRecoveryAt=10;
 float SwordBodyAge=.39f,SwordBodyDuration=.4f,basicStrikeSeconds=3f/24,SwordReleaseAge=0,basicReleasedAt=10,swordBodyAt=10;
 float BasicReleaseAge{get{return Math.Max(0,Time.time-basicReleasedAt);}}
 const float BasicSwingSeconds=3f/24;int WaveMotionRevision;bool GuardianPhaseOverridesPose;string sheet="Wave";BodyOwner sw02Owner=new BodyOwner();
 int BasicStrikeIndex=0,SwordStrikeIndex=0,swordPreviousStrike=0;
 string SwordBodyAction="BasicMelee",CurrentPose="Slash";
 bool BasicBodyActive{get{return basicPresentationActive&&(SwordBodyAction=="BasicMelee"||SwordBodyAction=="BasicRanged");}}
 bool SwordBodyReleased{get{return !float.IsNegativeInfinity(swordBodyReleasedAt)||Time.time-swordPreviousRelease<3f/24;}}
 static float SwordFrameTime(float t){return (float)Math.Floor(Math.Max(0,t)*24)/24;}
 void RegisterSwordBodySprite(){}
 void ConfigureBasicReturnFrames(){}
 APPLY_COMBAT
 APPLY_BASIC
 void ShowPose(string pose){picture.sprite=poses[pose];CurrentPose=pose;}
 RELEASE
 PREPARE
 GROUND_RELEASE
 BEGIN
 LATE
 FRAME
 OWNER
 DISPATCH
 static void Check(bool yes,string why){if(!yes)throw new Exception(why);}
 void Sample(float age,int expected){basicReleasedAt=Time.time-age;picture.sprite=poses["Slash"];ApplySwordBodyPresentation("Attack",EnemyAttack.SWBasic);Check(picture.sprite==(expected>=10?swordSkillFrames[expected-10]:expected<6?sw02Frames[expected]:expected==9?poses["Idle"]:basicReturnFrames[expected-6]),"actual selected sprite "+age+" "+picture.sprite.name);}
 public static void Run(){
 foreach(string form in new[]{"Wave","WaveRise","WaveWard","WaveFinal"})foreach(int facing in new[]{-1,1}){
 Time.time=10;
 var p=new PoseProbe();p.sw02Frames=new[]{new Sprite("Prepare01"),new Sprite("Prepare02"),new Sprite("Swing01"),new Sprite("Swing02"),new Sprite("Recover01"),new Sprite("Recover02")};
 p.basicReturnFrames=new[]{new Sprite("LinkLow"),new Sprite("ReturnCross"),new Sprite("ReturnFinish")};
 p.swordSkillFrames=new[]{new Sprite("FollowLow"),new Sprite("ReturnLoad"),new Sprite("GroundRaise"),new Sprite("GroundDrop"),new Sprite("GroundStrike"),new Sprite("GroundRecoil"),new Sprite("ShieldBrace"),new Sprite("SwordCommand")};p.poses["Idle"]=new Sprite("Idle");p.poses["Slash"]=new Sprite("Slash");
 p.SW02UsesIntermediateFrame=true; // previous skill may have sampled before this coroutine event
 p.basicReleaseStarted=false;p.ApplySwordBodyPresentation("Telegraph",EnemyAttack.SWBasic);Check(p.picture.sprite==p.sw02Frames[1],"overhead load");
 p.BeginBasicRelease();Check(p.picture.sprite==p.sw02Frames[2],"first event immediately selects high cut");p.picture.sprite=p.poses["Slash"];p.SampleSwordBasicContactPose();Check(p.picture.sprite==p.sw02Frames[2],"late renderer restores contact after competing pose");p.Sample(.001f,2);p.Sample(1.1f/24,3);p.Sample(2.1f/24,4);p.Sample(3.1f/24,10);p.basicLinkAt=Time.time-2f/24;p.Sample(4.9f/24,11);p.Sample(8.9f/24,11);p.Sample(20.9f/24,11);
 p.BeginBasicRelease(3f/24,1);Check(p.picture.sprite==p.swordSkillFrames[1],"second event continues the low loaded pose");p.Sample(.001f,11);p.Sample(1.1f/24,7);p.Sample(2.1f/24,8);p.Sample(5.1f/24,16);p.Sample(8.1f/24,0);p.Sample(9.1f/24,9);
 p.picture.sprite=p.poses["Slash"];p.basicReleasedAt=Time.time-2.1f/24;p.SampleSwordBasicContactPose();Check(p.picture.sprite==p.basicReturnFrames[2],"LateUpdate restores authored return after logical overwrite");
 p.SwordBodyAction="BasicRanged";p.basicReleaseStarted=false;p.SwordBodyAge=.30f;p.ApplySwordBodyPresentation("Telegraph",EnemyAttack.SWBasic);Check(p.picture.sprite==p.sw02Frames[1],"ranged loaded");p.SwordBodyAge=.39f;p.ApplySwordBodyPresentation("Telegraph",EnemyAttack.SWBasic);Check(p.picture.sprite==p.sw02Frames[2],"ranged lead-in before unchanged release");p.BeginBasicRelease(2f/24);p.Sample(.001f,3);p.Sample(2.1f/24,3);p.Sample(4.1f/24,4);p.Sample(6.1f/24,5);p.Sample(8.1f/24,9);
 p.CurrentPose="Idle";p.picture.sprite=p.poses["Idle"];p.ApplySwordBodyPresentation("Idle",EnemyAttack.SWBasic);Check(p.picture.sprite==p.poses["Idle"],"idle cannot retain stale attack frame");
 // Both casts execute in the release tick: the next warning must preserve
 // the first swing, then finish recovery before loading the next forward cut.
 Time.time=10;p.SW02UsesIntermediateFrame=false;p.sw02Owner.CurrentPattern=(int)EnemyAttack.SW01;
 p.swordLinkLowFrame=form=="WaveRise"||form=="WaveWard"?11:10;
 foreach(float warning in new[]{.42f,.54f}){
  p.PrepareSwordBody("Volley",.65f);p.ReleaseSwordBody("Volley");p.PrepareSwordBody("Volley",warning,1);float start=Time.time;
  float[] ages={.001f,3.1f/24,5.1f/24,7.1f/24,warning-.001f};
  Sprite[] expected={p.sw02Frames[3],p.sw02Frames[4],p.sw02Frames[5],p.sw02Frames[1],p.sw02Frames[2]};
  for(int i=0;i<ages.Length;i++){Time.time=start+ages[i];p.SwordBodyAge=SwordFrameTime(ages[i]);p.ApplySwordBodyPresentation("Telegraph",EnemyAttack.SW01);Check(p.picture.sprite==expected[i],"SW01 follow-through/link/load "+form+" "+ages[i]);}
  Time.time=start+warning;p.ReleaseSwordBody("Volley",1);Check(p.picture.sprite==p.sw02Frames[3],"second forward release is immediate");
  foreach(float age in new[]{.001f,1.1f/24,2.1f/24}){Time.time=start+warning+age;p.SwordReleaseAge=SwordFrameTime(age);p.ApplySwordBodyPresentation("Attack",EnemyAttack.SW01);Check(p.picture.sprite==p.sw02Frames[3],"second forward cut remains readable");}
  p.swordRecoveryAt=Time.time;p.ApplySwordBodyPresentation("Recover",EnemyAttack.SW01);Check(p.picture.sprite==p.sw02Frames[4],"recovery follows forward cut");
 }
 p.hurtUntil=20;p.picture.sprite=new Sprite("Hurt");p.ReleaseSwordGroundStrike();Check(p.picture.sprite.name=="Hurt","ground contact cannot override an active hurt pose");p.ApplySwordBodyPresentation("Attack",EnemyAttack.SWBasic);Check(p.picture.sprite.name=="Hurt"&&p.SwordBodyAction==null,"hurt cancels body ownership");
 }
 Console.WriteLine("PASS actual body dispatch: basic cuts and ranged, SW01 linked volley, late overwrite, Idle and Hurt ownership");
 }
}'''
        skills=(SWORD/"Visuals/KarmaCharacterAnimator.SwordSkills.cs").read_text(encoding="utf-8")
        code=code.replace("GROUND_RELEASE",method(skills,"internal void ReleaseSwordGroundStrike()"))
        for token, source, signature in [("APPLY_COMBAT", body, "void ApplySwordBodyFrame("),
                                          ("APPLY_BASIC", main, "void ApplyBasicBodyFrame("),
                                          ("FRAME", main, "int BasicBodyFrame()"),
                                          ("LATE", main, "internal void SampleSwordBasicContactPose()"),
                                          ("BEGIN", main, "public void BeginBasicRelease("),
                                          ("RELEASE", body, "internal void ReleaseSwordBody("),
                                          ("PREPARE", body, "internal void PrepareSwordBody("),
                                          ("OWNER", body, "bool SwordBodyCanOverride("),
                                          ("DISPATCH", body, "void ApplySwordBodyPresentation(")]:
            code = code.replace(token, method(source, signature))
        self.assertIn("PASS actual body dispatch", run_cs(code, "PoseProbe"))
        energy = (SWORD / "Visuals/KarmaSwordWaveAnimation.cs").read_text(encoding="utf-8-sig")
        rows = re.findall(r"\{ (new Vector2.+?) \}", energy.split("BladeSockets =", 1)[1].split("internal static bool BladePoints", 1)[0])
        for row in rows:
            points = [tuple(map(int, xy)) for xy in re.findall(r"new Vector2\((\d+),(\d+)\)", row)]
            angles = [math.degrees(math.atan2(points[i*3][1]-points[i*3+2][1], abs(points[i*3+2][0]-points[i*3][0]))) for i in (2,3,4)]
            self.assertGreater(angles[0], 30)
            self.assertLess(abs(angles[1]), 10)
            self.assertLess(angles[2], -25)
            self.assertGreater(angles[0]-angles[2], 60)

    def test_basic_return_resources_and_actual_blade_pixels(self):
        root = ROOT / "Assets/Arts/Resources/SwordWave"
        source = (SWORD / "Visuals/KarmaSwordWaveAnimation.cs").read_text(encoding="utf-8-sig")
        rows = re.findall(r"\{ (new Vector2.+?) \}", source.split("BasicReturnSockets =", 1)[1].split("static bool BasicReturnBladePoints", 1)[0])
        forms = ["Wave", "WaveRise", "WaveWard", "WaveFinal"]
        crowns = [172, 89, 70, 71]
        idle_heights = [331, 257, 280, 253]
        def wood(a):
            rgb = a[:,:,:3].astype(float)
            mask = (a[:,:,3]>220)&(rgb[:,:,0]>rgb[:,:,1]*1.14)&(rgb[:,:,1]>rgb[:,:,2]*1.1)&(rgb[:,:,0]>90)&(rgb[:,:,0]<245)
            return np.median(rgb[mask], axis=0)
        for form, row, crown, idle_height in zip(forms, rows, crowns, idle_heights):
            meta = (root / "SWBasic" / (form+"_Return.png.meta")).read_text()
            self.assertEqual(re.findall(r"      name: (\w+)", meta), ["LinkLow", "ReturnCross", "ReturnFinish"])
            ppu = float(re.search(r"spritePixelsToUnits: ([\d.]+)", meta)[1])
            pivots = [float(v) for v in re.findall(r"      pivot: \{x: 0.5, y: ([\d.]+)\}",meta)]
            self.assertEqual(len(set(pivots)), 1, "single fixed foot anchor per sheet")
            idle_meta = (root / "Forms" / (form+".png.meta")).read_text()
            idle_ppu = float(re.search(r"spritePixelsToUnits: ([\d.]+)", idle_meta)[1])
            rect = idle_meta.split("name: Idle",1)[1]
            x,y,w,h = [int(re.search(r"\b"+k+r": (\d+)",rect)[1]) for k in ("x","y","width","height")]
            idle_image = np.array(Image.open(root / "Forms" / (form+".png")))
            idle = idle_image[idle_image.shape[0]-y-h:idle_image.shape[0]-y, x:x+w]
            image = Image.open(root / "SWBasic" / (form+"_Return.png"))
            self.assertEqual(image.mode, "RGBA")
            self.assertEqual(image.size, (2244,748))
            pixels = np.array(image)
            points = [tuple(map(int, xy)) for xy in re.findall(r"new Vector2\((\d+),(\d+)\)", row)]
            for frame in range(3):
                cell = pixels[:,frame*748:(frame+1)*748]
                np.testing.assert_allclose(wood(cell), wood(idle), atol=16, err_msg=form+str(frame))
                sole = np.where((cell[:,:,3]>240).any(axis=1))[0][-1]
                self.assertAlmostEqual(748-pivots[frame]*748, sole, delta=1)
                self.assertAlmostEqual((sole-crown)/ppu, idle_height/idle_ppu, delta=.02)
                for px,py in points[frame*3:frame*3+3]:
                    rgb = cell[py,px,:3].astype(int)
                    self.assertGreater(cell[py,px,3], 180)
                    self.assertGreater(rgb.min(),150, "socket must touch the visible steel")
                    self.assertLess(rgb.max()-rgb.min(),35)
            self.assertLess(points[5][0], points[3][0], "return crosses leftward, not reversed first cut")
            self.assertLess(points[8][1], points[6][1], "return finishes high-left")

    def test_basic_selected_image_keeps_flight_footprint(self):
        source = (SWORD / "Visuals/KarmaSwordAttackPolish.cs").read_text(encoding="utf-8-sig")
        wave = (SWORD / "Visuals/KarmaSwordWaveAnimation.cs").read_text(encoding="utf-8-sig")
        code = r'''
using System;using System.Collections.Generic;
public struct Vector2{public float x,y;public float sqrMagnitude=>x*x+y*y;public Vector2(float x,float y){this.x=x;this.y=y;}public static implicit operator Vector2(Vector3 v){return new Vector2(v.x,v.y);}}
public struct Vector3{public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
public struct Quaternion{public float angle;public static Quaternion identity;public static Quaternion Euler(float x,float y,float z)=>new Quaternion{angle=z};}
public static class Mathf{public const float Rad2Deg=180f/(float)Math.PI;public static float Atan2(float y,float x)=>(float)Math.Atan2(y,x);}
public struct Color{public static Color white;}
public class Transform{public Vector3 position,localScale;public Quaternion localRotation,rotation;}
public class Bounds{public Vector2 size;}
public class Sprite{public string name;public Bounds bounds=new Bounds();}
public class SpriteRenderer{public Sprite sprite;public Transform transform=new Transform();public Color color;public object sharedMaterial;public bool flipX,flipY;}
public class Node{public KarmaSwordWaveAnimation flight;public T AddComponent<T>() where T:new(){var v=new T();flight=v as KarmaSwordWaveAnimation;return v;}}
public class KarmaBossAttack{public SpriteRenderer Visual=new SpriteRenderer();public Node gameObject=new Node();public Transform transform=new Transform();}
public class KarmaGame{}
public class KarmaEnemy{public int CurrentPattern;}
public enum EnemyAttack{SWBasic,SW01,SW02}
public enum SwordEnergyPhase{Flight}
public static class Resources{public static T Load<T>(string s) where T:new(){object sprite=new Sprite{name=s,bounds=new Bounds{size=new Vector2(12.54f,12.54f)}};return (T)sprite;}}
public static class KarmaVisuals{static object material=new object();public static void ApplyMaterial(SpriteRenderer r){r.sharedMaterial=material;}}
public static class KarmaArtwork{public static Sprite Sprite(string group,string key){return new Sprite{name=group+"/"+key,bounds=new Bounds{size=new Vector2(2+key[key.Length-1]%3,4+key[key.Length-1]%2)}};}}
public class KarmaSwordVfxPool{static KarmaSwordVfxPool pool=new KarmaSwordVfxPool();public static KarmaSwordVfxPool For(KarmaGame g){return pool;}}
public static class KarmaSwordAttackPolish{public static void Core(KarmaBossAttack s){}}
public static class BasicRoute{RANGED}
public class KarmaSwordWaveAnimation{
 enum Playback{Flight}const int FrameCount=5;const float FlightFPS=30;
 static string[] Clips={"Charge","SW01Flight","SW02Flight","Impact"};static Sprite[,] frames=new Sprite[4,5];static Sprite blueCrescent,basicCrescent;
 KarmaGame game;KarmaEnemy owner;SpriteRenderer picture;int pattern,clip;Vector2 velocity,size;bool heavy;Playback playback;float age;KarmaSwordVfxPool echoes;
 void ApplyBasicVisualOrigin(){}
 public bool energy;void AttachEnergy(SwordEnergyPhase phase){energy=true;}
 public static void Release(KarmaGame g,KarmaEnemy o,Vector2 p,Vector2 s,bool h,float f){}
 FRAME
 INITIALIZE
 FLIGHT
 PAINT
 APPLY
 ANGLE
 public void Sample(float t){age=t;ApplyFlight();}
 public static void Run(){
 foreach(int facing in new[]{-1,1})foreach(float y in new[]{0f,2f}){
 var game=new KarmaGame();var basic=new KarmaBossAttack();var sw01=new KarmaBossAttack();
 var size=new Vector2(3,5);var collision=new Vector2(2.7f,4.5f);var velocity=new Vector2(facing*10,y);
 BasicRoute.Ranged(game,new KarmaEnemy{CurrentPattern=0},basic,velocity,collision,size);
 Flight(game,new KarmaEnemy{CurrentPattern=1},sw01,velocity,size);
 for(int frame=0;frame<15;frame++){
 basic.gameObject.flight.Sample(frame/30f);sw01.gameObject.flight.Sample(frame/30f);
 var a=basic.Visual;var b=sw01.Visual;
 Check(a.sprite.name=="SwordWave/SWBasic/SWBasic_BlueCrescent"&&b.sprite.name.Contains("SW01Flight"),"Basic selected image stays distinct from animated SW01");
 Check(Object.ReferenceEquals(a.sharedMaterial,b.sharedMaterial),"same material");

 Check(Math.Abs(a.sprite.bounds.size.x*a.transform.localScale.x-3*1.12f/.65f)<.0001f,"world width with common unit parent");
 Check(Math.Abs(a.sprite.bounds.size.y*a.transform.localScale.y-5/.81f)<.0001f,"world height with common unit parent");
 Check(a.flipX==(facing<0)&&a.flipX==b.flipX&&!a.flipY,"same facing");
 Check(!basic.gameObject.flight.energy&&sw01.gameObject.flight.energy&&Object.ReferenceEquals(basic.gameObject.flight.echoes,sw01.gameObject.flight.echoes),"Basic image is not redrawn by procedural SW01 geometry");
 }
 }Console.WriteLine("PASS Basic selected image preserves flight footprint: frames, dimensions, material, facing, energy and echo pool");}
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
}'''
        code = code.replace("RANGED", method(source, "public static void Ranged("))
        code = code.replace("ANGLE", re.search(r'internal static float BasicFlightAngle\([^;]+;', wave)[0])
        for token, signature in [("FRAME", "static Sprite Frame("), ("INITIALIZE", "void Initialize("),
                                 ("FLIGHT", "public static void Flight("), ("PAINT", "void Paint("), ("APPLY", "void ApplyFlight()")]:
            code = code.replace(token, method(wave, signature))
        self.assertIn("PASS Basic selected image preserves flight footprint", run_cs(code, "KarmaSwordWaveAnimation", modern=True))
        # Both production shots use the same persistent parent; no Basic mesh
        # component can overwrite Flight's renderer later in the frame.
        basic = (SWORD / "Patterns/SWBasic.cs").read_text(encoding="utf-8-sig")
        behavior = (SWORD / "KarmaSwordBossBehavior.cs").read_text(encoding="utf-8-sig")
        self.assertIn("parent: sword.PersistentRoot", basic)
        self.assertIn("owner, persistentRoot, origin, velocity, size, life", method(behavior, "public KarmaBossAttack ReleaseSW01("))
        self.assertIn("KarmaSwordWaveAnimation.Flight(game, owner, shot, velocity, size)", method(source, "public static void Projectile("))
        self.assertNotIn("Projectile(game", method(source, "public static void Ranged("))

    def test_own_idle_art_registration_and_palette(self):
        source = (SWORD / "Visuals/KarmaCharacterAnimator.SwordSW02.cs").read_text(encoding="utf-8-sig")
        self.assertNotIn("swordFrameScales", source)
        self.assertNotIn("swordFrameTints", source)
        self.assertIn('Resources.LoadAll<Sprite>("SwordWave/Forms/" + idle.texture.name + "_Combat")', source)
        self.assertIn("picture.transform.localScale = pictureScale;", source)
        self.assertIn("picture.color = color;", method(source, "void ApplySwordBodyColor(Color color)"))
        self.assertNotIn("UsesSwordAppearanceCorrection", source)
        root = ROOT / "Assets/Arts/Resources/SwordWave/Forms"
        centers = {"Wave": [255,290,251,246,269,265], "WaveRise": [272,301,267,266,289,279],
                   "WaveWard": [255,275,223,254,264,267], "WaveFinal": [275,294,261,266,284,280]}
        idle_body = {"Wave":331, "WaveRise":257, "WaveWard":280, "WaveFinal":253}
        def wood(pixels):
            rgb = pixels[:,:,:3].astype(float)
            mask = (pixels[:,:,3]>220)&(rgb[:,:,0]>rgb[:,:,1]*1.14)&(rgb[:,:,1]>rgb[:,:,2]*1.1)&(rgb[:,:,0]>90)&(rgb[:,:,0]<245)
            return np.median(rgb[mask], axis=0)
        for name, columns in centers.items():
            idle_meta = (root/(name+".png.meta")).read_text()
            ppu_idle = float(re.search(r"spritePixelsToUnits: ([\d.]+)",idle_meta).group(1))
            rect = idle_meta.split("name: Idle",1)[1]
            x,y,w,h = [int(re.search(r"\b"+k+r": (\d+)",rect).group(1)) for k in ("x","y","width","height")]
            with Image.open(root/(name+".png")) as im:
                idle = np.array(im)[im.height-y-h:im.height-y,x:x+w]
            meta = (root/(name+"_Combat.png.meta")).read_text()
            ppu = float(re.search(r"spritePixelsToUnits: ([\d.]+)",meta).group(1))
            pivots = [float(v) for v in re.findall(r"      pivot: \{x: 0.5, y: ([\d.]+)\}",meta)]
            self.assertEqual(len(pivots),6)
            self.assertEqual(re.findall(r"      name: (\w+)",meta),["Prepare01","Prepare02","Swing01","Swing02","Recover01","Recover02"])
            with Image.open(root/(name+"_Combat.png")) as im:
                self.assertEqual(im.mode,"RGBA")
                self.assertEqual(im.size,(1536,1024))
                pixels=np.array(im)
            for j,cx in enumerate(columns):
                cell=pixels[j//3*512:(j//3+1)*512,j%3*512:(j%3+1)*512]
                # Real source palette, with no renderer tint to hide differences.
                np.testing.assert_allclose(wood(cell),wood(idle),atol=16,err_msg=name+str(j))
                sole=int(np.where((cell[:,:,3]>240).any(axis=1))[0][-1])
                self.assertAlmostEqual(512-pivots[j]*512,sole,delta=1)
                region=cell[60:180,cx-5:cx+6].astype(float)
                mask=(region[:,:,3]>220)&(region[:,:,0]>region[:,:,1]*1.12)&(region[:,:,1]>region[:,:,2]*1.1)&(region[:,:,0]>90)
                crown=int(np.where(mask.sum(axis=1)>=4)[0][0])+60
                ratio=((sole-crown)/ppu)/(idle_body[name]/ppu_idle)
                # A low cut flexes the knees; do not enlarge its crouched body.
                self.assertGreater(ratio,.90,(name,j,ratio))
                self.assertLess(ratio,1.07,(name,j,ratio))

    def test_skill_body_calibration_matches_source_pixels(self):
        source = (SWORD / "Visuals/KarmaCharacterAnimator.SwordSW02.cs").read_text(encoding="utf-8-sig")
        samples = np.array([list(map(float, v)) for v in re.findall(
            r"new Vector4\(([\d.]+)f,([\d.]+)f,([\d.]+)f,([\d.]+)f\)", source)]).reshape(4,9,4)
        shader = (ROOT / "Assets/Arts/Resources/SwordWave/Shared/SwordBodyConsistent.shader").read_text()
        self.assertIn("LinearToSRGB(rgb)", shader)
        self.assertIn("SRGBToLinear(rgb)", shader)
        centers = [[255,290,251,246,269,265],[272,301,267,266,289,279],
                   [255,275,223,254,264,267],[275,294,261,266,284,280]]
        def wood(a):
            rgb=a[:,:,:3].astype(float)
            mask=(a[:,:,3]>220)&(rgb[:,:,0]>rgb[:,:,1]*1.14)&(rgb[:,:,1]>rgb[:,:,2]*1.1)&(rgb[:,:,0]>90)&(rgb[:,:,0]<245)
            return rgb,mask,np.median(rgb[mask],axis=0)
        for form,name in enumerate(("Wave","WaveRise","WaveWard","WaveFinal")):
            root=ROOT/"Assets/Arts/Resources/SwordWave"
            meta=(root/"Forms"/(name+".png.meta")).read_text()
            rect=meta.split("name: Idle",1)[1]
            x,y,w,h=[int(re.search(r"\b"+k+r": (\d+)",rect)[1]) for k in ("x","y","width","height")]
            with Image.open(root/"Forms"/(name+".png")) as im:
                idle=np.array(im)[im.height-y-h:im.height-y,x:x+w]
            target=wood(idle)[2]
            for index in range(9):
                path=root/("Forms" if index<6 else "SWBasic")/(name+("_Combat.png" if index<6 else "_Return.png"))
                with Image.open(path) as im:
                    a=np.array(im);cellsize=512 if index<6 else im.height
                    j=index if index<6 else index-6
                    cell=a[j//3*cellsize:(j//3+1)*cellsize,j%3*cellsize:(j%3+1)*cellsize]
                rgb,mask,median=wood(cell)
                np.testing.assert_array_equal(median,samples[form,index,1:])
                # Execute the shader's fully selected wood gain in sRGB space.
                corrected=rgb[mask]*(target/samples[form,index,1:])
                np.testing.assert_allclose(np.median(corrected,axis=0),target,atol=.01)
                sole=np.where((cell[:,:,3]>240).any(axis=1))[0][-1]
                if index<6:
                    cx=centers[form][index];region=cell[60:180,cx-5:cx+6].astype(float)
                    crownmask=(region[:,:,3]>220)&(region[:,:,0]>region[:,:,1]*1.12)&(region[:,:,1]>region[:,:,2]*1.1)&(region[:,:,0]>90)
                    crown=np.where(crownmask.sum(axis=1)>=4)[0][0]+60
                else: crown=np.where(mask.sum(axis=1)>=4)[0][0]
                self.assertEqual(sole-crown,samples[form,index,0])

    def test_new_skill_sheet_import_and_palette(self):
        source=(SWORD/"Visuals/KarmaCharacterAnimator.SwordSkills.cs").read_text(encoding="utf-8")
        table=source.split("swordSkillWood =",1)[1].split("bool RegisterSwordSkillAppearance",1)[0]
        expected=np.array([list(map(float,v)) for v in re.findall(r"new Vector3\((\d+),(\d+),(\d+)\)",table)]).reshape(4,8,3)
        names=["FollowLow","ReturnLoad","GroundRaise","GroundDrop","GroundStrike","GroundRecoil","ShieldBrace","SwordCommand"]
        for form,name in enumerate(("Wave","WaveRise","WaveWard","WaveFinal")):
            path=ROOT/"Assets/Arts/Resources/SwordWave/Forms"/(name+"_Skills.png")
            meta=path.with_suffix(".png.meta").read_text()
            self.assertEqual(re.findall(r"^      name: (.+)$",meta,re.M),names)
            self.assertEqual(meta.count("    outline: []"),9)
            with Image.open(path) as im:
                self.assertEqual(im.mode,"RGBA")
                a=np.array(im)
            self.assertGreater(np.mean(a[:,:,3]<8),.40)
            for i in range(8):
                c=a[round(i//4*443.5):round((i//4+1)*443.5),round(i%4*443.5):round((i%4+1)*443.5)]
                rgb=c[:,:,:3].astype(float)
                mask=(c[:,:,3]>220)&(rgb[:,:,0]>rgb[:,:,1]*1.14)&(rgb[:,:,1]>rgb[:,:,2]*1.1)&(rgb[:,:,0]>90)&(rgb[:,:,0]<245)
                np.testing.assert_array_equal(np.median(rgb[mask],axis=0),expected[form,i])
                rect=meta.split("name: "+names[i]+"\n",1)[1]
                pivot=float(re.search(r"pivot: \{x: 0.5, y: ([\d.]+)",rect)[1])
                self.assertGreater(pivot,.04)
                self.assertLess(pivot,.09)

    def test_skill_appearance_production_registration(self):
        source=(SWORD/"Visuals/KarmaCharacterAnimator.SwordSW02.cs").read_text(encoding="utf-8-sig")
        table=source.split("static readonly Vector4[,] swordBodySamples =",1)[1].split("Material swordBaseMaterial",1)[0]
        code=r'''
using System;using System.Collections.Generic;
public struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 operator*(Vector3 v,float s){return new Vector3(v.x*s,v.y*s,v.z*s);}}
public struct Vector4{public float x,y,z,w;public Vector4(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}}
public class Texture{public string name;}
public class Sprite{public Texture texture=new Texture();public float pixelsPerUnit;}
public class Transform{public Vector3 localScale;}
public class Material{}
public static class Resources{public static T Load<T>(string path) where T:new(){return new T();}}
public class MaterialPropertyBlock{public Vector4 value;public void SetVector(int id,Vector4 v){value=v;}}
public struct Color{public float r,g,b;public Color(float x,float y,float z){r=x;g=y;b=z;}public static bool operator==(Color a,Color b){return a.r==b.r&&a.g==b.g&&a.b==b.b;}public static bool operator!=(Color a,Color b){return !(a==b);}public override bool Equals(object o){return o is Color&&(Color)o==this;}public override int GetHashCode(){return r.GetHashCode();}}
public class SpriteRenderer{public Color color;public Sprite sprite;public Transform transform=new Transform();public Material sharedMaterial;public MaterialPropertyBlock properties;public void SetPropertyBlock(MaterialPropertyBlock b){properties=b;}public void GetPropertyBlock(MaterialPropertyBlock b){if(properties!=null)b.value=properties.value;}}
public enum EnemyAttack{SWBasic,SW01,SW02,SW03,SW04,SW05}
public class Owner{public int CurrentPattern;}
public class AppearanceProbe{
 static readonly Vector4[,] swordBodySamples = TABLE
 SpriteRenderer picture=new SpriteRenderer();Vector3 pictureScale;Owner sw02Owner=new Owner();Dictionary<string,Sprite> poses=new Dictionary<string,Sprite>();Sprite[] sw02Frames,basicReturnFrames,swordSkillFrames;
 Material swordBaseMaterial=new Material(),swordBodyMaterial=new Material();MaterialPropertyBlock swordBaseProperties=new MaterialPropertyBlock(),swordBodyProperties=new MaterialPropertyBlock();static int SwordWoodGain=1;
 Color pictureColor=new Color(1,1,1);
 void ApplyDamageFlash(){} // Flash cadence is exercised separately below.
 CONFIGURE
 REGISTER
 COLOR
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 public static void Run(){int cases=0;string[] names={"Wave","WaveRise","WaveWard","WaveFinal"};
 foreach(int form in new[]{0,1,2,3})foreach(int attack in new[]{0,1,2,3,4,5})foreach(int facing in new[]{-1,1}){
 var p=new AppearanceProbe();p.picture.sharedMaterial=p.swordBaseMaterial;p.picture.color=form==3?new Color(1,.88f,.73f):new Color(1,1,1);var approved=p.picture.color;p.ConfigureSwordAppearance();Check(p.pictureColor==approved,"post-spawn color captured before any skill");p.sw02Owner.CurrentPattern=attack;p.ApplySwordBodyColor(p.pictureColor);Check(p.picture.color==p.pictureColor,"spawn tint survives every target skill");var hurt=new Color(1,.45f,.45f);p.ApplySwordBodyColor(hurt);Check(p.picture.color==hurt,"hurt tint remains independent");p.ApplySwordBodyColor(p.pictureColor);Check(p.picture.color==approved,"hurt returns to approved idle tint");p.pictureScale=new Vector3(facing*1.7f,1.7f,1);
 var idle=new Sprite{pixelsPerUnit=100};idle.texture.name=names[form];p.poses["Idle"]=idle;p.sw02Frames=new Sprite[6];p.basicReturnFrames=new Sprite[3];p.swordSkillFrames=new Sprite[8];
 for(int f=0;f<17;f++){
 var sprite=new Sprite{pixelsPerUnit=f>=9?256:f<6?136:192};if(f>=9)p.swordSkillFrames[f-9]=sprite;else if(f<6)p.sw02Frames[f]=sprite;else p.basicReturnFrames[f-6]=sprite;
 p.picture.sprite=sprite;p.RegisterSwordAppearance();var wood=f>=9?swordSkillWood[form,f-9]:new Vector3(); var sample=f>=9?new Vector4(swordSkillHeights[form],wood.x,wood.y,wood.z):swordBodySamples[form,f];
 float expected=swordIdleHeights[form]/idle.pixelsPerUnit*1.7f;
 int referenceFrame=f<6?0:6;float referencePixels=f>=9?swordSkillHeights[form]:swordBodySamples[form,referenceFrame].x;
 Check(Math.Abs(referencePixels/sprite.pixelsPerUnit*p.picture.transform.localScale.y-expected)<.00001f,"sheet ready body equals approved Idle");
 // Crouching/follow-through must not zoom the torso to restore standing height.
 float expectedScale=expected/(referencePixels/sprite.pixelsPerUnit);
 Check(Math.Abs(p.picture.transform.localScale.y-expectedScale)<.00001f,"same physical drawing scale across all poses in each sheet");
 if(f==4&&sample.x<referencePixels)Check(sample.x/sprite.pixelsPerUnit*p.picture.transform.localScale.y<expected,"low follow-through stays lower instead of becoming a larger boss");
 Check(p.picture.transform.localScale.x*facing>0,"facing preserved");var gain=p.picture.properties.value;
 Check(Math.Abs(gain.x*sample.y-swordIdleWood[form].x)<.001f&&Math.Abs(gain.y*sample.z-swordIdleWood[form].y)<.001f&&Math.Abs(gain.z*sample.w-swordIdleWood[form].z)<.001f,"wood maps to own Idle tone");
 // Repeated registration after LateUpdate never compounds scale or gain.
 var scale=p.picture.transform.localScale;var material=p.picture.sharedMaterial;
 for(int repeat=0;repeat<20;repeat++){p.RegisterSwordAppearance();Check(p.picture.transform.localScale.y==scale.y&&p.picture.sharedMaterial==material,"idempotent after pose overwrite");}
 p.picture.sprite=idle;p.RegisterSwordAppearance();Check(p.picture.sharedMaterial==p.swordBaseMaterial&&p.picture.properties==p.swordBaseProperties,"Idle/interruption restores original material and properties");Check(p.picture.transform.localScale.y==1.7f,"Idle scale restored");
 p.picture.sprite=sprite;p.RegisterSwordAppearance();Check(p.picture.transform.localScale.y==scale.y,"skill resume is absolute");cases++;
 }
 p.picture.sprite=idle;p.RegisterSwordAppearance();Check(p.picture.sharedMaterial==p.swordBaseMaterial,"Idle restores original material");
 }Console.WriteLine("PASS "+cases+" production pose registrations, facing, palette, idle and SWBasic-SW05 transitions");}
}'''
        skill=(SWORD/"Visuals/KarmaCharacterAnimator.SwordSkills.cs").read_text(encoding="utf-8")
        skill_table=skill.split("static readonly float[] swordSkillHeights",1)[1].split("bool RegisterSwordSkillAppearance",1)[0]
        code=code.replace(" CONFIGURE", " static readonly float[] swordSkillHeights"+skill_table+method(skill,"bool RegisterSwordSkillAppearance(")+"\n CONFIGURE")
        code=code.replace("CONFIGURE",method(source,"void ConfigureSwordAppearance()")).replace("TABLE",table).replace("REGISTER",method(source,"void RegisterSwordAppearance()")).replace("COLOR",method(source,"void ApplySwordBodyColor(Color color)"))
        self.assertIn("PASS 816 production pose registrations",run_cs(code,"AppearanceProbe",modern=True))
        self.assertIn("RegisterSwordAppearance();",method(source,"void RegisterSwordBodySprite()"))
        self.assertIn("pictureColor = picture.color;",method(source,"void ConfigureSwordAppearance()"))
        motion=(SWORD/"Visuals/KarmaWaveMotion.cs").read_text(encoding="utf-8-sig")
        self.assertIn("animator.SampleSwordBasicContactPose();\n            animator.RegisterSwordAppearance();",motion)
        self.assertLess(motion.index("animator.RegisterSwordAppearance();"),motion.index("if (game != null && game.Paused) return;"))

    def test_late_body_color_preserves_shared_damage_flash_and_restores_idle(self):
        local = (SWORD / "Visuals/KarmaCharacterAnimator.SwordSW02.cs").read_text(encoding="utf-8-sig")
        common = (ROOT / "Assets/Scripts/Presentation/KarmaCharacterAnimator.cs").read_text(encoding="utf-8-sig")
        code = r'''
using System;
struct Color {
 public float r,g,b;public Color(float x,float y,float z){r=x;g=y;b=z;}
 public static Color operator*(Color a,Color b){return new Color(a.r*b.r,a.g*b.g,a.b*b.b);}
}
class SpriteRenderer{public Color color;}
static class Time{public static float time;}
static class Mathf{public static int FloorToInt(float n){return (int)Math.Floor(n);}}
public class FlashProbe {
 SpriteRenderer picture=new SpriteRenderer();Color pictureColor;
 float damageFlashAt,damageFlashUntil;
 bool DamageFlashVisible{get{return Time.time<damageFlashUntil;}}
 APPLY
 FLASH
 public static void Run(){
  foreach(float green in new[]{1f,.88f})foreach(int facing in new[]{-1,1})foreach(int skill in new[]{0,1,2,3,4,5}){
   var p=new FlashProbe();p.pictureColor=new Color(1,green,green==1?1:.73f);p.damageFlashAt=10;p.damageFlashUntil=10.7f;
   // Shared hit feedback is one 0.18-second pulse, not periodic blinking.
   // Sample either side of its end and the longer feedback lifetime.
   foreach(float age in new[]{0f,.06f,.12f,.179f,.181f,.24f,.48f,.699f,.701f,.71f}){
    Time.time=10+age;bool red=age<.18f;
    for(int repeat=0;repeat<4;repeat++){
     p.ApplySwordBodyColor(p.pictureColor);var c=p.picture.color;
     if(Math.Abs(c.g-green*(red?.45f:1))>.00001f||Math.Abs(c.b-p.pictureColor.b*(red?.45f:1))>.00001f)
      throw new Exception("LateUpdate must preserve shared flash without compounding tint");
    }
   }
  }
  Console.WriteLine("PASS shared flash on final SwordWave body sample, cadence and Idle restore");
 }
}'''
        code = code.replace("APPLY", method(local, "void ApplySwordBodyColor(Color color)"))
        code = code.replace("FLASH", method(common, "internal void ApplyDamageFlash()"))
        self.assertIn("PASS shared flash", run_cs(code, "FlashProbe", modern=True))

    def test_basic_surface_cannot_darken_background(self):
        root=ROOT/"Assets/Arts/Resources/SwordWave/Shared"
        shader=(root/"SWBasicCrescent.shader").read_text()
        self.assertIn("Blend SrcAlpha One, One OneMinusSrcAlpha",shader)
        self.assertNotIn("SW05CrimsonPalette",shader)
        with Image.open(root/"Sword_Crescent_v6.png") as im:
            pixels=np.array(im).astype(float)/255
        rgb=pixels[:,:,:3];energy=rgb.max(axis=2)
        def smooth(a,b,x):
            t=np.clip((x-a)/(b-a),0,1);return t*t*(3-2*t)
        alpha=pixels[:,:,3]*smooth(.08,.42,energy)
        core=smooth(.3,.9,rgb[:,:,:2].min(axis=2))
        color=np.array([.04,.38,1.0])+(np.array([.86,1.08,1.2])-np.array([.04,.38,1]))*core[:,:,None]
        emitted=color*alpha[:,:,None]
        self.assertTrue((emitted>=0).all())
        self.assertTrue((emitted[:,:,2]>=emitted[:,:,0]).all())
        self.assertGreater(np.mean(alpha>.5),.03)
        self.assertLess(alpha[0,0],.001)
        polish=(SWORD/"Visuals/KarmaSwordAttackPolish.cs").read_text()
        basic=polish.split("public sealed class KarmaSwordBasicPresentation",1)[1].split("// SW04-only",1)[0]
        self.assertIn("KarmaSwordWaveAnimation.Flight(", method(basic, "public static void Ranged("))
        wave=(SWORD/"Visuals/KarmaSwordWaveAnimation.cs").read_text(encoding="utf-8-sig")
        self.assertIn("picture.color = Color.white;", method(wave, "void Initialize("))
        self.assertIn("sr.sharedMaterial = e.material; e.surface.Restart(4);",polish)


class MovementChecks(unittest.TestCase):
    def test_real_approach_brakes_before_handoff_and_reversal(self):
        source = (SWORD / "KarmaSwordBossMovement.cs").read_text(encoding="utf-8-sig")
        code = r'''
using System;
struct Vector2{public float x;public Vector2(float x){this.x=x;}}
static class Mathf{
 public static float Abs(float a){return Math.Abs(a);}public static float Sign(float a){return a<0?-1:1;}
 public static int Min(int a,int b){return Math.Min(a,b);}public static float Min(float a,float b){return Math.Min(a,b);}public static float Max(float a,float b){return Math.Max(a,b);}
 public static float Sqrt(float a){return (float)Math.Sqrt(a);}
 public static float MoveTowards(float a,float b,float d){return Abs(b-a)<=d?b:a+Sign(b-a)*d;}
}
class Config{public float enemyMoveSpeed=9,moveSpeed=10;}
class Game{public Config Config=new Config();}
class Tuning{public float moveMultiplier=1;}
class Owner{public Vector2 Position;public float FightSeconds,timer;public Game game=new Game();public Tuning Tuning=new Tuning();}
class SettingsData{public float swordPreferredDistance=11.75f,swordApproachStartDistance=10.5f,movementScale=1,swordRetreatDeceleration=72,swordRetreatAcceleration=56;}
class Behavior{public float BasicWaveRange=16;public bool candidate=true;public bool HasSwordCandidate(Vector2 p){return candidate;}}
public class ApproachProbe{
 Owner owner=new Owner();Behavior behavior=new Behavior();SettingsData Settings=new SettingsData();
 bool swordApproaching;float approachSpeed,swordApproachUntil,nextSwordApproach,wallReturnUntil,wallReturnDirection;
 float MoveSwordOnGround(float step){owner.Position.x+=step;return Math.Abs(step);}
 APPROACH
 FINISH
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 public static void Run(){int cases=0;
 foreach(float dt in new[]{1f/30,1f/60,1f/144,.1f})foreach(int side in new[]{-1,1}){
  var p=new ApproachProbe();bool stopped=false;float previous=0;
  for(int i=0;i<1000;i++){
   p.owner.FightSeconds=i*dt;bool moving=p.ApproachSword(dt,side*30);
   if(!moving){Check(Math.Abs(previous)<=72*dt*2+.001f,"handoff cannot truncate full walk speed");stopped=true;break;}
   Check(Math.Abs(p.approachSpeed-previous)<=72*dt+.001f,"bounded acceleration and braking");previous=p.approachSpeed;
  }
  Check(stopped&&p.owner.FightSeconds<1.2f+dt*1.1f,"reachable attack receives its existing turn");cases++;
  p=new ApproachProbe();p.behavior.candidate=false;
  for(int i=0;i<100;i++){
   p.owner.FightSeconds=i*dt;previous=p.approachSpeed;p.ApproachSword(dt,side*(i<30?30:-30));
   Check(Math.Abs(p.approachSpeed-previous)<=72*dt+.001f,"player crossing brakes through zero");
  }
  Check(p.approachSpeed*side<0,"walking eventually follows the new side");cases++;
  p=new ApproachProbe();p.behavior.candidate=false;
  for(int i=0;i<500;i++){
   p.owner.FightSeconds=i*dt;p.ApproachSword(dt,side*25-p.owner.Position.x);
   Check(Math.Abs(p.owner.Position.x)<=25-11.75f+.001f,"ground step never crosses preferred stop distance");
  }
  Check(Math.Abs(p.approachSpeed)<.02f,"approach settles at its preferred gap");cases++;
 }
 Console.WriteLine("PASS "+cases+" production approach handoff, reversal and distance cases");
 }
}'''
        code = code.replace("APPROACH", method(source, "bool ApproachSword("))
        code = code.replace("FINISH", method(source, "void FinishSwordApproach("))
        self.assertIn("PASS 24 production approach", run_cs(code, "ApproachProbe", modern=True))


if __name__ == "__main__":
    unittest.main()
