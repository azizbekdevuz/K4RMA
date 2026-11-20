"""SW03 image fit and real mesh generation; GPU/playback checks remain separate."""
import re
import unittest
from pathlib import Path

import numpy as np
from PIL import Image
from test_sword_presentation import method, run_cs
from test_sw05_eruption_mesh import SHIM, method as mesh_method

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / 'Assets/Arts/SwordWave/Effects'
CODE = ROOT / 'Assets/Runtime/SwordWave/Visuals'


class ScarPolishChecks(unittest.TestCase):
    def test_transparency_fit_palette_and_profile(self):
        with Image.open(ART / 'SW03_RisingBlade_v7.png') as image:
            self.assertEqual(image.mode, 'RGBA')
            self.assertEqual(image.size, (1024, 1536))
            pixels = np.array(image)
        y, x = np.where(pixels[:, :, 3] > 128)
        self.assertEqual((x.min(), 1535-y.max(), x.max()-x.min()+1, y.max()-y.min()+1), (161, 160, 701, 1354))
        self.assertEqual(int(pixels[700, 200, 3]), 0, 'no painted halo outside sword')
        self.assertGreater(int(pixels[700, 430, :3].min()), 140, 'silver steel stays bright')
        self.assertGreater(int(pixels[700, 512, 1]), 240, 'cyan-white fuller')
        source = (CODE / 'KarmaSwordScarRise.cs').read_text(encoding='utf-8-sig')
        self.assertIn('new Rect(156,155,713,1362)', source)
        self.assertIn('new Vector2(512,256)', source)
        self.assertIn('contactSize.y*(704f/1354)*VisualWidthScale', source)
        self.assertIn('new Vector3(fitX, fit, 1)', source)
        self.assertIn('new Vector2(fitX, fit)', source)
        expression = re.search(r'return y<0.05f\?.*?;', source, re.S)[0]
        shader = (ART / 'KarmaSwordRisePolish.shader').read_text(encoding='utf-8-sig')
        expected = re.sub(r'(?<=\d)f\b', '', expression.replace('return ', 'float width=').replace('Mathf.Lerp', 'lerp'))
        self.assertIn(expected, shader, 'GPU aura and CPU bolts use the same outline')
        self.assertIn('clip(i.worldY - _FloorY + 0.005)', shader)

    def test_fast_continuous_rise_starts_at_floor(self):
        source = (CODE / 'KarmaSwordScarRise.cs').read_text(encoding='utf-8-sig')
        shim = SHIM.split('static class KarmaSW05RainBlade')[0]
        code = shim + 'class Probe { public const float RiseSeconds=.14f;'
        code += 'public static float VisualEmbedDepth(float h){return 101f*h/1362;}'
        code += method(source, 'public static float VisualRiseHeight(')
        code += method(source, 'public static float VisibleTipHeight(')
        code += r'''
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 public static void Run(){foreach(float h in new[]{3f,6.94f,10f}){
  Check(VisibleTipHeight(0,h)==0,"no first-frame pop");
  float last=0;for(int i=0;i<=140;i++){
   float next=VisibleTipHeight(i/1000f,h);
   Check(next>=last && next-last<h*.012f,"continuous monotonic rise");last=next;
  }
  Check(VisibleTipHeight(1f/30,h)<h*.16f,"low frame rate emergence remains readable");
  Check(VisibleTipHeight(.07f,h)>h*.4f && VisibleTipHeight(.07f,h)<h*.55f,"rise retains visible middle");
  Check(Math.Abs(VisibleTipHeight(.14f,h)-(h-VisualEmbedDepth(h)))<.00001f,"fast seating with original socket");
  Check(Math.Abs(VisibleTipHeight(.14f,h)-VisibleTipHeight(.65f,h))<.00001f,"no late drift");
 }Console.WriteLine("PASS continuous rise");}}
'''
        self.assertIn('PASS continuous rise', run_cs(code, 'Probe', modern=True))

    def test_retraction_is_continuous_and_finishes_inside_round_gap(self):
        source = (CODE / 'KarmaSwordScarRise.cs').read_text(encoding='utf-8-sig')
        code = SHIM.split('static class KarmaSW05RainBlade')[0]
        code += 'class Probe { public const float BladeFadeSeconds=.22f;'
        code += method(source, 'public static float RetractionDistance(')
        code += r'''
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 public static void Run(){foreach(float h in new[]{3f,6.94f,10f}){
  Check(RetractionDistance(0,h)==0,"continuous handoff");
  Check(RetractionDistance(1f/30,h)<h*.07f,"no first-frame drop");
  Check(Math.Abs(RetractionDistance(.11f,h)-h*.5f)<.00001f,"visible midpoint");
  float last=0;for(int i=0;i<=300;i++){
   float next=RetractionDistance(i/1000f,h);
   Check(next>=last && next-last<h*.007f,"continuous downward movement");last=next;
  }
  Check(Math.Abs(RetractionDistance(.22f,h)-h)<.00001f,"below floor before next warning");
 }Console.WriteLine("PASS continuous sinking");}}
'''
        self.assertIn('PASS continuous sinking', run_cs(code, 'Probe', modern=True))

    def test_actual_mesh_capacity_finite_colors_and_no_allocations(self):
        source = (CODE / 'KarmaSW05Energy.cs').read_text(encoding='utf-8-sig')
        rise = (CODE / 'KarmaSwordScarRise.cs').read_text(encoding='utf-8-sig')
        shim = SHIM.split('static class KarmaSW05RainBlade')[0]
        shim = shim.replace('public static float Sin', 'public static float Sqrt(float v){return (float)Math.Sqrt(v);}public static float Sin')
        names = ('Quad', 'Line', 'Shard', 'Ring', 'ScarNoise', 'ScarLine', 'ScarArc', 'ScarEdge', 'ScarEnergy')
        code = shim + '\nstatic class KarmaSwordScarRise { public const float AuraDelay=.045f,RiseSeconds=.14f,BladeFadeSeconds=.22f;'
        code += method(rise, 'public static float BladeHalfWidth(')
        code += method(rise, 'public static float RetractionDistance(') + '}\n'
        code += r'''
class Probe {
 enum Kind { ScarWarning, ScarFade, Scar }
 class Storage {public int capacity=512; public Vector3[] vertices=new Vector3[2048];public Color[] colors=new Color[2048];}
 Storage buffer=new Storage(); int count,seed;float age,life=.22f,charge,scarFloor;Vector2 at,size;Kind kind;bool CapacityExceeded;
 static readonly Color Blue=new Color(.08f,.55f,1.1f,1),White=new Color(.75f,1.15f,1.4f,1);
 static Vector2 Direction(float angle)=>new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
 METHODS
 void Draw(){count=0;CapacityExceeded=false;ScarEnergy();}
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 public static void Run(){var p=new Probe();int peak=0;
  foreach(var mode in new[]{Kind.ScarWarning,Kind.Scar,Kind.ScarFade})
  for(int site=0;site<80;site++)for(int tick=0;tick<=60;tick++){
   p.kind=mode;p.seed=site*937;p.age=tick/120f;p.charge=tick/60f;
   p.size=mode==Kind.ScarWarning?new Vector2(1.1f,1):new Vector2(2.3f,6);
   p.at=new Vector2(site*.4f,0);p.buffer.capacity=mode==Kind.Scar?512:256;p.Draw();
   Check(!p.CapacityExceeded,"mesh budget exceeded");peak=Math.Max(peak,p.count);
   for(int i=0;i<p.count*4;i++){
    var v=p.buffer.vertices[i];var c=p.buffer.colors[i];
    Check(!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y),"finite geometry");
    Check(c.a>=0&&c.a<=1.001f,"bounded transparency");
    if(mode==Kind.ScarFade)Check(v.y>=p.scarFloor,"residue remains above fixed floor");
    if(mode==Kind.ScarWarning)Check(Math.Abs(v.x-p.at.x)<p.size.x*.55f,"warning stays in ground footprint");
   }
  }
  p.kind=Kind.Scar;p.age=.12f;p.buffer.capacity=512;for(int i=0;i<50;i++)p.Draw();
  long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)p.Draw();
  Check(GC.GetAllocatedBytesForCurrentThread()==before,"no drawing allocations");
  Console.WriteLine("PASS SW03 mesh phases, 80 seeds, capacity and allocation checks; peak="+peak);
 }
}'''
        code = code.replace('METHODS', '\n'.join(mesh_method(source, name) for name in names))
        self.assertIn('PASS SW03 mesh phases', run_cs(code, 'Probe', modern=True))


if __name__ == '__main__':
    unittest.main()
