"""Production falling-warning geometry, clocks and cleanup; no Unity launch."""
import re
import unittest
from pathlib import Path

from test_sword_presentation import method, run_cs
from test_sw05_eruption_mesh import SHIM

ROOT = Path(__file__).resolve().parents[3]
BASE = ROOT / "Assets/Scripts/SwordWave"
ENERGY = BASE / "Visuals/KarmaSW05Energy.cs"


class FallWarningChecks(unittest.TestCase):
    def test_final_range_covers_gaps_and_preserves_ceiling(self):
        source=(BASE / "Patterns/KarmaSW05RainBlade.cs").read_text(encoding="utf-8-sig")
        shim=SHIM.split("static class KarmaSW05RainBlade",1)[0]
        code=shim+r"""
struct Rect {public float x,y,width,height;public Rect(float a,float b,float c,float d){x=a;y=b;width=c;height=d;}}
static class KarmaStage {public const float ArenaLeft=-15,ArenaRight=55;}
class KarmaSW05RainBlade {
 Vector2 landing;float explosionHeight=6.94f,width=2;public bool Landed=true;Rect? finalExplosion;
 public Vector2 PlantedBlastSize {get{return new Vector2(width,explosionHeight);}}
 COMMIT
 public static void Run(){
  foreach(int layout in new[]{0,1,2}){
   var swords=new List<KarmaSW05RainBlade>();
   for(int i=0;i<54;i++)swords.Add(new KarmaSW05RainBlade{landing=new Vector2(layout==0?-14.8f+i*1.3f:layout==1?i%2==0?-12:52:20,0)});
   swords.Add(null);swords.Add(new KarmaSW05RainBlade{Landed=false,landing=new Vector2(-1000,0)});
   CommitFinalRange(swords);Rect region=swords[0].finalExplosion.Value;
   if(region.x<-15||region.x+region.width>55.00001f||region.y!=0||Math.Abs(region.height-6.94f)>.00001f)throw new Exception("blast bounds changed");
   foreach(var b in swords)if(b!=null&&b.Landed){if(!b.finalExplosion.HasValue||b.finalExplosion.Value.x!=region.x||b.finalExplosion.Value.width!=region.width)throw new Exception("split damage zones");}
   if(layout==1 && (region.x!=-13||region.x+region.width!=53))throw new Exception("empty middle not filled");
   if(swords[55].finalExplosion.HasValue)throw new Exception("airborne sword enlarged final range");
  }
  CommitFinalRange(new List<KarmaSW05RainBlade>());
  Console.WriteLine("PASS continuous blast gaps, walls, ceiling and empty list");
 }}"""
        code=code.replace("COMMIT",method(source,"public static void CommitFinalRange("))
        self.assertIn("PASS continuous blast",run_cs(code,"KarmaSW05RainBlade",modern=True))


    def test_fall_only_wiring_and_unchanged_damage_contracts(self):
        pattern = (BASE / "Patterns/SW05.cs").read_text(encoding="utf-8-sig")
        rain = (BASE / "Patterns/KarmaSW05RainBlade.cs").read_text(encoding="utf-8-sig")
        vfx = (BASE / "Visuals/KarmaSW05VFX.cs").read_text(encoding="utf-8-sig")
        self.assertIn("marks[member] = KarmaSW05VFX.FallWarning(", pattern)
        self.assertNotIn("KarmaSW05VFX.Warning(", pattern)
        self.assertIn('PrepareSwordBody("UltimateStart", 0.8f)', pattern)
        self.assertIn("yield return owner.Wait(0.8f); owner.ClearPatternObjects();", pattern)
        self.assertIn("new Vector2(shot.size.y / Mathf.Abs(axis.y), 0.34f)", pattern)
        self.assertIn("shot.PositionAt(shot.life) + axis * (shot.size.x / 2)", pattern)
        self.assertIn("fallWarnings.BindFallWarning(member, attack)", pattern)
        self.assertIn("DetonationWarning = 0.60f", pattern)
        self.assertIn("ExplosionActiveSeconds = 0.12f", rain)
        self.assertIn("seconds = 0.75f", (BASE / "KarmaSwordBossBehavior.cs").read_text(encoding="utf-8-sig"))
        self.assertNotIn("EmphasizeExplosion(", rain)
        self.assertNotIn("KarmaSW05VFX.Warning(", method(rain, "public void ArmExplosion("))
        marker = method(vfx, "public static SpriteRenderer FallWarning(")
        self.assertIn("marker.enabled=false", marker)
        self.assertNotIn("Create(", marker)
        update = method(rain, "void Update()")
        self.assertLess(update.index("warning.gameObject.SetActive(false)"), update.index("KarmaSW05VFX.Strike("))
        self.assertIn("internal float FallWarningAge => age", rain)

    def test_production_batch_clocks_geometry_pool_and_lifecycle(self):
        source = ENERGY.read_text(encoding="utf-8-sig")
        shim = SHIM.split("static class KarmaSW05RainBlade", 1)[0]
        shim = shim.replace("public struct Color", "struct Color")
        shim = shim.replace("public float r,g,b,a;", "public static Color clear;public float r,g,b,a;")
        shim += r'''
enum EnemyAttack{SW05,Other}enum RunPhase{Combat,End}
class Node{public bool activeInHierarchy=true;public string name;public void SetActive(bool b){activeInHierarchy=b;}}
class Transform{public Node gameObject=new Node();public Vector3 position;}
class SpriteRenderer{public Node gameObject=new Node();public Transform transform=new Transform();public bool enabled=true;}
class KarmaEnemy{public bool PresentationActive=true,Stunned;public int CurrentPattern;}
class KarmaGame{public bool Paused;public RunPhase Phase=RunPhase.Combat;}
class KarmaSW05RainBlade{public float age;public bool Landed;public float FallWarningAge{get{return age;}}}
class KarmaAerialSwordPlanner{SHOT}
static class KarmaStage{public const float ArenaLeft=-15,ArenaRight=55;}
static class KarmaVisuals{public static SpriteRenderer Box(Transform root,string n,Vector2 at,Vector2 size,Color c,int order){return new SpriteRenderer{transform=new Transform{position=at}};}}
class KarmaSW05VFX{MARKER}
static class Time{public static float deltaTime=.01f;public static int frameCount=1;}
class Object{public static void Destroy(object o){}}
class Filter{public object sharedMesh;}class Display{public bool enabled=true;public int sortingOrder;}
class KarmaSW05Energy{
 class Storage{public int capacity=4096;public object mesh=new object();public Vector3[] vertices=new Vector3[16384];public Color[] colors=new Color[16384];}
 ENUM
 static List<Storage> spare=new List<Storage>(72);static int allocated;
 Storage buffer;Filter filter=new Filter();Display display=new Display();public Node gameObject=new Node();public string name;
 KarmaGame game;KarmaEnemy warningOwner;SpriteRenderer[] fallMarks;KarmaAerialSwordPlanner.Shot[] fallShots;KarmaSW05RainBlade[] fallBlades;
 int[] fallGroup,fallUrgent;float formationWarning,age,life;int count,seed=17,scarSiteCount,splinterCount,landingCount,sampledFrame=-1,splinterFrame=-1,detonationFrame=-1;Kind kind;public bool CapacityExceeded;
 Transform follow;KarmaSW05RainBlade rain;Vector2 at,size;
 static readonly Color Blue=new Color(.08f,.55f,1.1f,1),White=new Color(.75f,1.15f,1.4f,1);
 static KarmaSW05Energy Create(Transform root,KarmaGame g,Kind k,float seconds,int capacity){var fx=new KarmaSW05Energy{game=g,kind=k,life=seconds};if(spare.Count>0){fx.buffer=spare[spare.Count-1];spare.RemoveAt(spare.Count-1);}else{fx.buffer=new Storage();allocated++;}return fx;}
 void GroundStrikeEnergy(){}void LandingEnergy(){}void SplinterEnergy(){}void RefreshChargeAnchors(){}void Upload(){}void Summoning(){}void BladeEnergy(){}void PlantedEnergy(){}void ScarEnergy(){}void WardEnergy(){}void ScarGroundEnergy(){}void FinalEnergy(){}void Shard(Vector2 p,Vector2 a,float b,float c){}void Burst(Vector2 p,float a,float b,bool c,int d){}
 void Destroy(Node n){n.SetActive(false);OnDisable();}
 CREATE
 BIND
 HAS
 PROGRESS
 REMAINING
 WIDTH
 DRAW_ALL
 DRAW_ONE
 GROUND
 QUAD
 LINE
 FACET
 RIBBON
 LATE
 DISABLE
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 float Alpha(){float a=0;for(int v=0;v<count*4;v++)if(buffer.colors[v].b>.5f)a=Math.Max(a,buffer.colors[v].a);return a;}
 void ResetDraw(){count=0;CapacityExceeded=false;FallingWarnings();}
 static void Validate(KarmaSW05Energy p){Check(!p.CapacityExceeded&&p.count<=4096,"fixed batch buffer remains bounded");for(int v=0;v<p.count*4;v++){Check(p.buffer.vertices[v].x>=-15&&p.buffer.vertices[v].x<=55,"floor warning stays inside arena");Check(p.buffer.vertices[v].y>=.01f&&p.buffer.vertices[v].y<=.5f,"all warning geometry remains on the floor");var c=p.buffer.colors[v];Check(c.b>=c.r&&c.b>=c.g,"all warning ink belongs to the blue palette");}}
 public static void Run(){int cases=0,peak=0;
 foreach(int facing in new[]{-1,1})foreach(float center in new[]{-14.95f,20f,54.95f})foreach(float width in new[]{.5f,1.2f,2.5f})foreach(int layout in new[]{0,1,2}){
  var marks=new SpriteRenderer[18];var shots=new KarmaAerialSwordPlanner.Shot[18];var blades=new KarmaSW05RainBlade[18];var owner=new KarmaEnemy();var game=new KarmaGame();
  float vy=-(float)Math.Sqrt(.96f);var velocity=new Vector2(facing*.2f,vy);
  for(int i=0;i<18;i++){
   float x=layout==0?center:layout==1?-13+i*3.7f:center+(i%3)*.015f;
   marks[i]=KarmaSW05VFX.FallWarning(new Transform(),game,new Vector2(x,.18f),new Vector2(width,.34f));Check(!marks[i].enabled,"old rectangular renderer stays disabled");
   shots[i]=new KarmaAerialSwordPlanner.Shot{velocity=velocity,size=new Vector2(4,width*Math.Abs(vy)),life=.14f,delay=i*.001f};
   Vector2 axis=velocity.normalized,tip=new Vector2(x,.04f);shots[i].acceleration=axis*2200;shots[i].origin=tip-axis*2-velocity*.14f-shots[i].acceleration*(.14f*.14f*.5f);
   Vector2 landing=shots[i].PositionAt(shots[i].life)+axis*(shots[i].size.x/2);Check(Math.Abs(landing.x-x)<.00001f,"actual planner equation and marker share landing x");
   blades[i]=new KarmaSW05RainBlade();
  }
  var p=FallWarnings(new Transform(),game,owner,marks,shots,1.25f);Validate(p);float early=p.Alpha();
  p.age=.65f;p.ResetDraw();Check(p.Alpha()>early,"middle stage grows brighter");
  for(int i=0;i<18;i++)p.BindFallWarning(i,blades[i]);
  for(int i=0;i<18;i++)blades[i].age=(shots[i].delay+shots[i].life)*.85f;
  p.ResetDraw();Validate(p);Check(p.Alpha()>early*1.2f,"pre-impact glow strengthens within original warning");peak=Math.Max(peak,p.count);
  Check(Math.Abs(p.FallWarningWidth(0)-width)<.00001f,"transverse width / vertical axis matches original footprint");
  var saved=p.buffer;p.count=0;p.DrawFallWarning(new Vector2(center,.04f),width,.9f,.5f,1,0);
  float lo=Math.Max(-15,center-width/2),hi=Math.Min(55,center+width/2),left=float.MaxValue,right=float.MinValue;
  for(int v=0;v<p.count*4;v++){var point=p.buffer.vertices[v];Check(point.x>=lo&&point.x<=hi,"every glow vertex remains inside exact danger interval");left=Math.Min(left,point.x);right=Math.Max(right,point.x);}
  Check(Math.Abs(left-lo)<.00001f&&Math.Abs(right-hi)<.00001f,"ground brackets reaches exact left and right danger limits");
  game.Paused=true;float age=p.age;p.LateUpdate();Check(p.age==age,"pause freezes warning clock");game.Paused=false;
  for(int i=0;i<18;i++)blades[i].age=shots[i].delay+shots[i].life;
  Check(p.FallWarningProgress(0)==1,"terminal progress reads real projectile age");
  p.ResetDraw();int beforeLanding=p.count;marks[0].gameObject.SetActive(false);p.ResetDraw();if(layout==1)Check(p.count<beforeLanding,"only the landed site disappears");Check(!marks[0].enabled,"landing cannot re-enable square renderer");
  if(cases==0){p.count=0;p.DrawFallWarning(new Vector2(center,.04f),width,.9f,.5f,1,0);float single=p.buffer.colors[4].a;p.count=0;p.DrawFallWarning(new Vector2(center,.04f),width,.9f,.5f,.25f,0);Check(p.buffer.colors[4].a<single*.26f,"crowding reduces glyph glare while retaining footprint");for(int j=0;j<100;j++)p.LateUpdate();long memory=GC.GetAllocatedBytesForCurrentThread();for(int j=0;j<1000;j++)p.LateUpdate();Check(GC.GetAllocatedBytesForCurrentThread()==memory,"zero per-frame geometry allocations");}
  foreach(var mark in marks)mark.gameObject.SetActive(false);p.LateUpdate();Check(p.buffer==null&&!p.gameObject.activeInHierarchy,"last landing returns pooled warning mesh immediately");cases++;
 }
 foreach(int reason in new[]{0,1,2,3}){
  var owner=new KarmaEnemy();var game=new KarmaGame();var marks=new[]{new SpriteRenderer()};var shots=new[]{new KarmaAerialSwordPlanner.Shot{velocity=new Vector2(0,-1),size=new Vector2(4,1),life=.1f}};
  var p=FallWarnings(new Transform(),game,owner,marks,shots,1.25f);game.Paused=true;
  if(reason==0)owner.PresentationActive=false;if(reason==1)owner.CurrentPattern=1;if(reason==2)owner.Stunned=true;if(reason==3)game.Phase=RunPhase.End;
  p.LateUpdate();Check(p.buffer==null,"interrupt, death, pattern exit and phase cleanup precede pause");
 }
 // Coincident warnings choose the soonest impact, even when listed last.
 var duplicateMarks=new[]{new SpriteRenderer(),new SpriteRenderer()};
 var duplicateShots=new[]{new KarmaAerialSwordPlanner.Shot{velocity=new Vector2(0,-1),size=new Vector2(4,1),delay=.4f,life=.3f},new KarmaAerialSwordPlanner.Shot{velocity=new Vector2(0,-1),size=new Vector2(4,1),life=.1f}};
 var duplicate=FallWarnings(new Transform(),new KarmaGame(),new KarmaEnemy(),duplicateMarks,duplicateShots,1.25f);
 Check(duplicate.fallUrgent[0]==1,"earliest landing wins duplicate glyph independent of array order");
 duplicateMarks[1].gameObject.SetActive(false);duplicate.ResetDraw();Check(duplicate.fallUrgent[0]==0,"remaining sword keeps its own deadline after first landing");
 duplicateMarks[0].gameObject.SetActive(false);duplicate.LateUpdate();
 Check(allocated==1,"all groups reuse one 4096-quad storage");
 Console.WriteLine("PASS "+cases+" production warning batches; four cleanup cases; peak "+peak+"/4096 quads; pooled and allocation-free");
 }
}
'''
        planner = (BASE / "Patterns/KarmaAerialSwordPlanner.cs").read_text(encoding="utf-8-sig")
        vfx = (BASE / "Visuals/KarmaSW05VFX.cs").read_text(encoding="utf-8-sig")
        shim = shim.replace("SHOT", method(planner, "public sealed class Shot"))
        shim = shim.replace("MARKER", method(vfx, "public static SpriteRenderer FallWarning("))
        shim = shim.replace("ENUM", re.search(r"enum Kind \{[^}]+\}", source)[0])
        for token, signature in [
            ("CREATE", "public static KarmaSW05Energy FallWarnings("),
            ("BIND", "public void BindFallWarning("), ("HAS", "bool HasFallWarnings()"),
            ("REMAINING", "float FallWarningRemaining("), ("PROGRESS", "float FallWarningProgress("), ("WIDTH", "float FallWarningWidth("),
            ("DRAW_ALL", "void FallingWarnings()"), ("DRAW_ONE", "void DrawFallWarning("), ("GROUND", "void WarningGround("),
            ("QUAD", "void Quad("), ("LINE", "void Line("), ("FACET", "void Facet("), ("LATE", "void LateUpdate()"),
            ("RIBBON", "void RibbonSegment("),
            ("DISABLE", "void OnDisable()"),
        ]:
            shim = shim.replace(token, method(source, signature))
        self.assertIn("PASS 54 production warning batches", run_cs(shim, "KarmaSW05Energy", modern=True))


    def test_grounded_landing_geometry_and_independent_expiry(self):
        source=ENERGY.read_text(encoding="utf-8-sig")
        from test_sw05_eruption_mesh import method as mesh_method
        shim=SHIM.split("static class KarmaSW05RainBlade",1)[0]
        shim=shim.replace("public static Vector2 left", "public static Vector2 down{get{return new Vector2(0,-1);}}public static Vector2 left")
        code=shim+r"""
static class KarmaStage{public const float ArenaLeft=-15,ArenaRight=55;}
class Probe{
 const float LandingSeconds=.34f;
 SITE
 class Storage{public int capacity=4096;public Vector3[] vertices=new Vector3[16384];public Color[] colors=new Color[16384];public LandingSite[] landings=new LandingSite[54];}
 Storage buffer=new Storage();int landingCount,count,seed;float age;bool CapacityExceeded;
 static readonly Color Blue=new Color(.08f,.55f,1.1f,1),White=new Color(.75f,1.15f,1.4f,1);
 METHODS
 void Draw(){count=0;LandingEnergy();}
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 public static void Run(){int peak=0;
 foreach(float width in new[]{.5f,1.2f,3f})foreach(float x in new[]{-14.95f,20f,54.95f})foreach(float lean in new[]{-.2f,.2f}){
  var p=new Probe();p.landingCount=54;
  for(int i=0;i<54;i++)p.buffer.landings[i]=new LandingSite{at=new Vector2(x,.06f),axis=new Vector2(lean,-.98f),width=width,born=i*.001f,seed=i*977};
  for(int tick=0;tick<50;tick++){
   p.age=tick*.01f;p.Draw();peak=Math.Max(peak,p.count);Check(!p.CapacityExceeded,"54 landings fit one fixed mesh");
   for(int v=0;v<p.count*4;v++){var a=p.buffer.vertices[v];var c=p.buffer.colors[v];
    Check(!float.IsNaN(a.x)&&!float.IsNaN(a.y),"finite geometry");
    Check(a.x>=Math.Max(-15,x-width/2)-.00001f&&a.x<=Math.Min(55,x+width/2)+.00001f,"precise footprint at walls");
    Check(a.y>=.0149f&&a.y<.39f,"chips remain grounded, no tall landing spikes");
    Check(c.a>=0&&c.a<=1.001f,"bounded opacity");
   }
   if(tick==35)Check(p.landingCount>0&&p.landingCount<54,"earlier impacts expire while later impacts keep moving");
  }
  Check(p.landingCount==0&&p.count==0,"batch fully expires");
 }
 var steady=new Probe();steady.landingCount=54;steady.age=.1f;
 for(int i=0;i<54;i++)steady.buffer.landings[i]=new LandingSite{at=new Vector2(20,.06f),axis=new Vector2(.2f,-.98f),width=2,seed=i};
 for(int i=0;i<30;i++)steady.Draw();long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)steady.Draw();
 Check(GC.GetAllocatedBytesForCurrentThread()==before,"landing drawing has no managed allocations");
 Console.WriteLine("PASS landing geometry, 54-site capacity, walls, independent expiry, zero allocations; peak="+peak);
 }
}"""
        code=code.replace("SITE",method(source,"struct LandingSite"))
        code=code.replace("METHODS","\n".join(mesh_method(source,n) for n in ("LandingEnergy","ScarNoise","ScarLine","Quad","Shard","Direction")))
        self.assertIn("PASS landing geometry",run_cs(code,"Probe",modern=True))

if __name__ == "__main__":
    unittest.main()
