"""Execute production SW05 mesh builders without starting Unity.

Only Unity's vector/math/buffer storage is substituted. The drawing methods
are extracted from the current C# source, not reimplemented as a Python model.
This checks emitted geometry and vertex colors, not GPU/render-pipeline output.
Run: python -X utf8 -B tests/Unity/SwordWave/test_sw05_eruption_mesh.py
"""
import json
from pathlib import Path
import re
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[3]
ENERGY = ROOT / "Assets/Runtime/SwordWave/Visuals/KarmaSW05Energy.cs"
HISTORICAL_REVISION = "036c22b"
METHODS = ("Noise", "Direction", "Quad", "Line", "Shard", "SwordShard", "Bolt", "Ring",
           "HazardVolumes", "FinalEnergy", "FinalEruption", "EruptionPlume", "ClipFinalRegion",
           "RibbonSegment", "Facet", "JudgementBlade", "FinalSite", "GroundSigil", "WarningGround")



def method(source, name):
    match = re.search(r"(?m)^        (?:static )?\w+ " + name + r"\(", source)
    if not match:
        raise AssertionError("Missing production mesh method: " + name)
    start = match.start()
    body = source.index(")", match.end()) + 1
    if source[body:].lstrip().startswith("=>"):
        end = source.index(";", body)
        return source[start:body] + " { return " + source[body:end].strip()[2:] + "; }"
    body = source.index("{", body)
    depth = 1
    end = body + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return source[start:end]


SHIM = r"""
using System;
using System.Collections.Generic;
using System.Globalization;
struct Vector2 {
    public float x,y;
    public Vector2(float x,float y){this.x=x;this.y=y;}
    public static Vector2 left {get{return new Vector2(-1,0);}}
    public static Vector2 right {get{return new Vector2(1,0);}}
    public static Vector2 up {get{return new Vector2(0,1);}}
    public float magnitude {get{return (float)Math.Sqrt(x*x+y*y);}}
    public Vector2 normalized {get{return magnitude<0.000001f?new Vector2(0,0):this/magnitude;}}
    public static Vector2 operator +(Vector2 a,Vector2 b){return new Vector2(a.x+b.x,a.y+b.y);}
    public static Vector2 operator -(Vector2 a,Vector2 b){return new Vector2(a.x-b.x,a.y-b.y);}
    public static Vector2 operator -(Vector2 a){return new Vector2(-a.x,-a.y);}
    public static Vector2 operator *(Vector2 a,float b){return new Vector2(a.x*b,a.y*b);}
    public static Vector2 operator *(float b,Vector2 a){return a*b;}
    public static Vector2 operator /(Vector2 a,float b){return a*(1/b);}
    public static Vector2 Lerp(Vector2 a,Vector2 b,float t){return a+(b-a)*Mathf.Clamp01(t);}
    public static Vector2 Scale(Vector2 a,Vector2 b){return new Vector2(a.x*b.x,a.y*b.y);}
    public static float Distance(Vector2 a,Vector2 b){return (a-b).magnitude;}
}
struct Vector3 {
    public float x,y,z;
    public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
    public static implicit operator Vector3(Vector2 a){return new Vector3(a.x,a.y,0);}
}
struct Color {
    public float r,g,b,a;
    public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
}
static class Mathf {
    public const float PI=(float)Math.PI;
    public static float Sin(float v){return (float)Math.Sin(v);}
    public static float Cos(float v){return (float)Math.Cos(v);}
    public static float Pow(float v,float n){return (float)Math.Pow(v,n);}
    public static float Exp(float v){return (float)Math.Exp(v);}
    public static float Repeat(float v,float n){return v-Floor(v/n)*n;}
    public static float Floor(float v){return (float)Math.Floor(v);}
    public static int FloorToInt(float v){return (int)Math.Floor(v);}
    public static int CeilToInt(float v){return (int)Math.Ceiling(v);}
    public static float Abs(float v){return Math.Abs(v);}
    public static float Sign(float v){return v>=0?1:-1;}
    public static float Min(float a,float b){return Math.Min(a,b);}
    public static int Min(int a,int b){return Math.Min(a,b);}
    public static float Max(float a,float b){return Math.Max(a,b);}
    public static int Max(int a,int b){return Math.Max(a,b);}
    public static float Clamp(float v,float a,float b){return Max(a,Min(v,b));}
    public static int Clamp(int v,int a,int b){return Max(a,Min(v,b));}
    public static float Clamp01(float v){return Clamp(v,0,1);}
    public static float Lerp(float a,float b,float t){return a+(b-a)*Clamp01(t);}
    public static float InverseLerp(float a,float b,float t){return a==b?0:Clamp01((t-a)/(b-a));}
    public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return a+(b-a)*t*t*(3-2*t);}
}
static class KarmaSW05RainBlade {public const float ExplosionActiveSeconds=0.12f;}
public class SW05MeshProbe {
    class Storage {public int capacity=16384;public Vector3[] vertices=new Vector3[65536];public Color[] colors=new Color[65536];}
    enum Kind { Charge, Detonation }
    Vector3[] sites=new Vector3[54],zones;
    Vector2[] chargeFeet=new Vector2[54],chargeHilts=new Vector2[54];
    Storage buffer=new Storage();
    static readonly Color Blue=new Color(.08f,.55f,1.1f,1),White=new Color(.75f,1.15f,1.4f,1);
    int count,seed=17;
    float age,life;
    Kind kind;
    public bool CapacityExceeded;
"""

PROBE = r"""
    void Setup(int fixture) {
        if(fixture==0)zones=new[]{new Vector3(-15,55,6.94244f)};
        else if(fixture==1)zones=new[]{new Vector3(-15.672f,-0.328f,6.94244f),new Vector3(0.328f,15.672f,6.94244f)};
        else zones=new Vector3[54];
        for(int i=0;i<54;i++) {
            float x=fixture==0?-13.128f+i*66.256f/53:fixture==1?(i>=27?2.2f:-13.8f)+(i%27)*11.6f/26:-14+i*1.25f;
            float half=fixture==2?0.4f:1.872f;
            sites[i]=new Vector3(x,6.94244f/2,half*2);
            chargeFeet[i]=new Vector2(x,.12f);chargeHilts[i]=new Vector2(x,3.8f);
            if(fixture==2)zones[i]=new Vector3(x-half,x+half,6.94244f);
        }
    }
    string Metrics(float time,int fixture,int mode) {
        age=time;count=0;CapacityExceeded=false;Setup(fixture);
        kind=mode==1?Kind.Detonation:Kind.Charge;life=mode==1?.48f:.60f;
        if(mode==2)HazardVolumes(Mathf.Clamp01(age/life),false);else FinalEnergy();
        float top=0,bottom=1000,left=1000,right=-1000,alpha=0;
        int outside=0;
        var snapshot=new System.Text.StringBuilder();
        for(int q=0;q<count;q++) {
            float qLeft=1000,qRight=-1000;
            for(int k=0;k<4;k++) {
                int at=q*4+k;Vector3 p=buffer.vertices[at];Color c=buffer.colors[at];
                top=Math.Max(top,p.y);bottom=Math.Min(bottom,p.y);left=Math.Min(left,p.x);right=Math.Max(right,p.x);
                qLeft=Math.Min(qLeft,p.x);qRight=Math.Max(qRight,p.x);alpha=Math.Max(alpha,c.a);
                // Compare normal connected-region meshes to genuine historical
                // output after applying only the required current bounds clip.
                foreach(float v in new[]{Mathf.Clamp(p.x,-15,55),Mathf.Clamp(p.y,0,6.94244f),c.r,c.g,c.b,c.a})
                    snapshot.Append(v.ToString("R",CultureInfo.InvariantCulture)).Append(',');
            }
            bool contained=false;
            foreach(var zone in zones)if(qLeft>=zone.x-.0001f&&qRight<=zone.y+.0001f){contained=true;break;}
            if(!contained)outside++;
        }
        string hash;
        using(var sha=System.Security.Cryptography.SHA256.Create())
            hash=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(snapshot.ToString())));
        return "{\"time\":"+time.ToString("R",CultureInfo.InvariantCulture)
            +",\"fixture\":"+fixture+",\"mode\":"+mode+",\"quads\":"+count
            +",\"overflow\":"+(CapacityExceeded?"true":"false")+",\"outside\":"+outside
            +",\"top\":"+top.ToString("R",CultureInfo.InvariantCulture)
            +",\"bottom\":"+bottom.ToString("R",CultureInfo.InvariantCulture)
            +",\"width\":"+(count==0?0:right-left).ToString("R",CultureInfo.InvariantCulture)
            +",\"alpha\":"+alpha.ToString("R",CultureInfo.InvariantCulture)+",\"hash\":\""+hash+"\"}";
    }
    public static void Run() {
        var p=new SW05MeshProbe();
        foreach(int fixture in new[]{0,1,2})foreach(int mode in new[]{0,1,2})
        foreach(float t in new[]{0f,.04f,.12f,.16f,.24f,.32f,.42f,.479f,.48f,.60f})
            Console.WriteLine(p.Metrics(t,fixture,mode));
    }
}
"""


def history(path):
    return subprocess.check_output(["git", "--no-optional-locks", "show", HISTORICAL_REVISION + ":" + path], cwd=ROOT)


def samples(source):
    from test_sword_presentation import run_cs
    code = SHIM + "\n".join(method(source, name) for name in METHODS) + PROBE
    output = run_cs(code, "SW05MeshProbe", modern=True)
    return [json.loads(line) for line in output.splitlines() if line.startswith("{")]


class SW05RestoredExplosionChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = ENERGY.read_text(encoding="utf-8")
        cls.samples = samples(cls.source)
        if len(cls.samples) != 90:
            raise AssertionError("Expected 90 production samples")

    def test_release_covers_region_without_point_fans_or_airborne_rings(self):
        draw = method(self.source, "FinalEruption")
        self.assertNotIn("FinalSite(", draw)
        self.assertNotIn("JudgementBlade(", draw)
        self.assertNotIn("GroundSigil(", draw)
        self.assertNotIn("for(int ribbon=", draw)
        self.assertIn("Kind.Charge, seconds, 16384", self.source)
        self.assertNotIn("HazardVolumes(", method(self.source, "FinalEnergy"))
        self.assertIn("display.sortingOrder = 4", self.source)
        for fixture in (0, 1, 2):
            peak = next(s for s in self.samples if s["fixture"] == fixture and s["mode"] == 1 and s["time"] == 0)
            self.assertGreater(peak["top"], 3.5)
            self.assertGreater(peak["alpha"], .8)
            self.assertGreater(peak["quads"], 100)

    def test_release_lights_every_ground_interval_on_first_frame(self):
        from test_sword_presentation import run_cs
        probe = PROBE[:PROBE.index("    public static void Run()")]
        probe += r"""
    public static void Run(){var p=new SW05MeshProbe();p.kind=Kind.Detonation;p.life=.48f;
     for(int fixture=0;fixture<3;fixture++){p.Setup(fixture);p.age=0;p.count=0;p.FinalEnergy();
      foreach(var zone in p.zones)for(int i=0;i<100;i++){
       float x=Mathf.Lerp(zone.x,zone.y,(i+.5f)/100);bool lit=false;
       for(int q=0;q<p.count;q++){
        float lo=1000,hi=-1000;int floor=0;
        for(int k=0;k<4;k++){int v=q*4+k;var point=p.buffer.vertices[v];var color=p.buffer.colors[v];
         if(point.y<=.3f&&color.a>.4f&&color.b>.5f){lo=Math.Min(lo,point.x);hi=Math.Max(hi,point.x);floor++;}}
        if(floor>=2&&x>=lo&&x<=hi){lit=true;break;}
       }
       if(!lit)throw new Exception("unlit ground interval "+x);
      }
     }
     Console.WriteLine("PASS every danger interval ignites on release");
    }}
"""
        output=run_cs(SHIM+"\n".join(method(self.source,n) for n in METHODS)+probe,"SW05MeshProbe",modern=True)
        self.assertIn("PASS every danger interval",output)

    def test_all_eruption_crests_share_height_and_growth(self):
        from test_sword_presentation import run_cs
        code = SHIM.replace("int count,seed=17;", "List<float> tops=new List<float>();int count,seed=17;")
        for name in METHODS:
            body = method(self.source, name)
            if name == "EruptionPlume":
                body = body.replace("const int bands=8,lanes=4;", "if(height>4.5f)tops.Add(origin.y+height);const int bands=8,lanes=4;")
            code += body
        code += PROBE[:PROBE.index("    public static void Run()")]
        code += r'''
    public static void Run(){var p=new SW05MeshProbe();p.kind=Kind.Detonation;p.life=.48f;
     foreach(float time in new[]{0f,.04f,.08f,.12f})foreach(int fixture in new[]{0,1,2}){
      p.Setup(fixture);p.age=time;p.count=0;p.tops.Clear();p.FinalEnergy();
      if(p.tops.Count<2)throw new Exception("missing simultaneous eruption crests");
      foreach(float h in p.tops)
       if(Math.Abs(h-6.94244f)>.0001f)throw new Exception("eruption ceiling differs between cells");
     }
     Console.WriteLine("PASS uniform eruption crests across 12 growth/layout cases");
    }}'''
        self.assertIn("PASS uniform eruption crests", run_cs(code, "SW05MeshProbe", modern=True))

    def test_charge_omits_range_overlay_but_keeps_charge_energy(self):
        charge = method(self.source, "FinalEnergy")
        self.assertNotIn("HazardVolumes(", charge)
        self.assertNotIn("JudgementBlade(", charge)
        self.assertNotIn("Shard(", charge)
        self.assertIn("WarningGround(", charge)
        for s in self.samples:
            if s["mode"] == 0:
                with self.subTest(sample=s):
                    self.assertLessEqual(s["top"], 6.943)
                    self.assertGreater(s["quads"], 0)
                    self.assertEqual(s["outside"], 0)

    def test_zone_bounds_and_54_site_buffer_are_preserved(self):
        for s in self.samples:
            with self.subTest(sample=s):
                self.assertFalse(s["overflow"])
                self.assertLessEqual(s["quads"], 16384)
                self.assertEqual(s["outside"], 0)
                self.assertLessEqual(s["top"], 6.943)
                if s["quads"]:
                    self.assertGreaterEqual(s["bottom"], 0)

    def test_original_fade_has_no_delayed_residual_pass(self):
        for name in ("MagneticFieldBlast", "PressurePetal", "ResidualMagneticField", "LocalResidualElectricity", "FinalDetonation"):
            self.assertNotIn("void " + name + "(", self.source)
        for s in self.samples:
            if s["mode"] == 1:
                self.assertEqual(s["quads"] == 0, s["time"] >= .48)
        peak = next(s for s in self.samples if s["mode"] == 1 and s["fixture"] == 0 and s["time"] == 0)
        tail = next(s for s in self.samples if s["mode"] == 1 and s["fixture"] == 0 and s["time"] == .42)
        self.assertLess(tail["alpha"], peak["alpha"] * .2)

    def test_final_geometry_reuses_storage_without_frame_allocations(self):
        from test_sword_presentation import run_cs
        probe = PROBE[:PROBE.index("    public static void Run()")]
        probe += r"""
    public static void Run(){
        var p=new SW05MeshProbe();p.kind=Kind.Detonation;p.life=.48f;int maximum=0;
        for(int fixture=0;fixture<3;fixture++){
            p.Setup(fixture);
            for(int i=0;i<100;i++){p.count=0;p.age=(i%24)/50f;p.FinalEnergy();}
            long start=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<1000;i++){
                p.count=0;p.age=(i%24)/50f;p.FinalEnergy();maximum=Math.Max(maximum,p.count);
                if(p.CapacityExceeded)throw new Exception("overflow");
            }
            if(GC.GetAllocatedBytesForCurrentThread()!=start)throw new Exception("per-frame allocation");
        }
        Console.WriteLine("PASS pooled final mesh; zero frame allocations; peak "+maximum+"/16384 quads");
    }
}
"""
        output=run_cs(SHIM+"\n".join(method(self.source,n) for n in METHODS)+probe,"SW05MeshProbe",modern=True)
        self.assertIn("PASS pooled final mesh; zero frame allocations",output)
        print(output.strip())

    def test_original_packed_art_guid_and_shader_are_reused(self):
        name = "SW05_FinalDetonation_8f.png"
        old_path = "Assets/K4RMA/Arts/Bosses/SwordWave/Effects/" + name
        archived = ROOT / "Assets/Arts/Backup/SwordWave/Effects/SwordWave_SW05_FinalDetonation_8f.png"
        self.assertEqual(history(old_path), archived.read_bytes())
        for retired in (name, "SW05_FinalOvercharge_6f.png"):
            self.assertFalse((ROOT / "Assets/Arts/SwordWave/Effects" / retired).exists())
        old_shader = "Assets/K4RMA/Arts/Bosses/SwordWave/Resources/SW05/SW05Celestial.shader"
        current = (ROOT / "Assets/Arts/SwordWave/Effects/SW05Celestial.shader").read_text(encoding="utf-8")
        self.assertEqual(history(old_shader).decode("utf-8").splitlines(), current.splitlines())


class PooledSplinterChecks(unittest.TestCase):
    def test_actual_fragments_have_bounded_geometry_expiry_and_no_frame_allocations(self):
        from test_sword_presentation import run_cs
        source = ENERGY.read_text(encoding="utf-8")
        shim = SHIM[:SHIM.index("static class KarmaSW05RainBlade")]
        shim = shim.replace("public static Vector2 up", "public static Vector2 down {get{return new Vector2(0,-1);}}public static Vector2 up")
        start = source.index("        struct Splinter")
        fragment = source[start:source.index("        int splinterCount", start)]
        code = shim + "class Probe {\n" + fragment + r"""
 class Storage {public int capacity=768;public Vector3[] vertices=new Vector3[3072];public Color[] colors=new Color[3072];public Splinter[] splinters=new Splinter[384];}
 Storage buffer=new Storage();int count,splinterCount;float age;bool CapacityExceeded;
 static readonly Color Blue=new Color(.08f,.55f,1.1f),White=new Color(.75f,1.15f,1.4f);
 METHODS
 void Fill(){splinterCount=384;age=0;for(int i=0;i<384;i++)buffer.splinters[i]=new Splinter{
  origin=new Vector2(i%2==0?-.99f:.99f,.12f),velocity=new Vector2(i%2==0?-12:12,3+i%11),
  born=0,life=.24f+(i%24)*.01f,length=.4f,left=-1,right=1,floor=0,ceiling=3};}
 void Draw(){count=0;CapacityExceeded=false;SplinterEnergy();}
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 public static void Run(){var p=new Probe();p.Fill();int peak=0;
  for(int frame=0;frame<=60;frame++){p.age=frame/120f;p.Draw();peak=Math.Max(peak,p.count);
   Check(!p.CapacityExceeded && p.count<=768,"capacity");
   for(int i=0;i<p.count*4;i++){var v=p.buffer.vertices[i];var c=p.buffer.colors[i];
    Check(v.x>=-1 && v.x<=1 && v.y>=0 && v.y<=3,"whole facets clipped to safe bounds");
    Check(c.a>=0 && c.a<=1,"finite transparency");}
  }
  Check(p.splinterCount==0 && p.count==0,"expiry within 500ms");
  p.Fill();for(int i=0;i<100;i++)p.Draw();long before=GC.GetAllocatedBytesForCurrentThread();
  for(int i=0;i<1000;i++)p.Draw();
  Check(GC.GetAllocatedBytesForCurrentThread()==before,"frame allocation");
  Console.WriteLine("PASS 384 fragments / "+peak+" quads; safe bounds, expiry, zero frame allocations");
 }
}
"""
        code = code.replace("METHODS", "\n".join(method(source,n) for n in ("Shard","SplinterEnergy")))
        output = run_cs(code,"Probe",modern=True)
        self.assertIn("PASS 384 fragments / 768 quads",output)
        print(output.strip())


if __name__ == "__main__":
    unittest.main()
