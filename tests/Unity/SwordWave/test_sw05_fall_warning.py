"""Production falling-warning geometry, clocks and cleanup; no Unity launch."""
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


    def test_original_ground_marker_lasts_until_landing(self):
        pattern = (BASE / "Patterns/SW05.cs").read_text(encoding="utf-8-sig")
        rain = (BASE / "Patterns/KarmaSW05RainBlade.cs").read_text(encoding="utf-8-sig")
        vfx = (BASE / "Visuals/KarmaSW05VFX.cs").read_text(encoding="utf-8-sig")
        self.assertIn("marks[member] = KarmaSW05VFX.Warning(", pattern)
        self.assertIn("shot.PositionAt(shot.life) + axis * (shot.size.x / 2)", pattern)
        self.assertIn("new Vector2(shot.size.y / Mathf.Abs(axis.y), 0.34f)", pattern)
        self.assertIn("fx.warning = true", method(vfx, "public static SpriteRenderer Warning("))
        flight = method(rain, "protected override void AdvanceFlight(")
        self.assertIn("warning.gameObject.SetActive(false)", flight)
        self.assertIn("DetonationWarning = 0.60f", pattern)
        self.assertIn("ExplosionActiveSeconds = 0.12f", rain)

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
