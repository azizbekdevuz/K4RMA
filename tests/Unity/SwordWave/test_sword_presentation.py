"""Offline source-execution and source-pixel checks; never starts Unity."""
import json
import os
import re
import subprocess
import shutil
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
    if modern or os.name != "nt":
        version = re.search(r"m_EditorVersion: (.+)", (ROOT / "ProjectSettings/ProjectVersion.txt").read_text())[1]
        if os.name == "nt":
            sdk = Path(os.environ["ProgramFiles"]) / "Unity/Hub/Editor" / version / "Editor/Data/DotNetSdk"
            dotnet = sdk / "dotnet.exe"
        else:
            dotnet = Path(shutil.which("dotnet") or "dotnet").resolve()
            sdk = dotnet.parent
        compiler = sorted(sdk.glob("sdk/*/Roslyn/bincore/csc.dll"))[-1]
        ref = sorted(sdk.glob("packs/Microsoft.NETCore.App.Ref/*/ref/net*"))[-1]
        references = sorted(ref.glob("*.dll"))
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
            result = subprocess.run([str(dotnet), str(compiler), "@" + str(response)], capture_output=True, text=True, encoding="utf-8")
            if result.returncode:
                raise AssertionError(result.stdout + result.stderr)
            (temp / "Probe.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"tfm": ref.name, "framework": {"name": "Microsoft.NETCore.App", "version": runtime}}}))
            result = subprocess.run([str(dotnet), str(temp / "Probe.dll")], capture_output=True, text=True, encoding="utf-8")
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
    def test_original_warning_boxes_keep_their_pattern_owners(self):
        patterns = ROOT / "Assets/Scripts/SwordWave/Patterns"
        melee = (patterns / "SWBasic.cs").read_text(encoding="utf-8-sig")
        self.assertEqual(method(melee, "IEnumerator Melee()").count("owner.Mark("), 2)
        self.assertIn("KarmaSwordAttackPolish.Warning(returnMark)", melee)
        for name in ("SwordWaveVolley.cs", "SW02.cs", "SW04.cs"):
            code = (patterns / name).read_text(encoding="utf-8-sig")
            self.assertIn("SetPath(", code)
            self.assertNotIn("FlowWarning(", code)
            self.assertNotIn("CounterWarning(", code)
        rain = (patterns / "SW05.cs").read_text(encoding="utf-8-sig")
        self.assertIn("marks[member] = KarmaSW05VFX.Warning(", rain)
        self.assertIn("marks[member]);", rain)

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

    def test_each_sword_form_has_its_own_ordered_combat_drawings(self):
        root = ROOT / "Assets/Arts/Resources/Motions12"
        rows = {tuple(row[:2]): row for row in (line.split("\t") for line in (root / "Catalog.txt").read_text().splitlines())}
        for form in ("Wave", "WaveRise", "WaveWard", "WaveFinal"):
            motions = ["Move", "Backstep", "Guard", "Windup", "SWBasic", "SWBasicRanged", "SW01", "SW02", "FollowThrough"]
            if form in ("WaveRise", "WaveFinal"):
                motions.append("SW03")
            if form in ("WaveWard", "WaveFinal"):
                motions.append("SW04")
            if form == "WaveFinal":
                motions.append("SW05")
            for motion in motions:
                with self.subTest(form=form, motion=motion):
                    row = rows[form, motion]
                    columns, lines, count, width, height = map(int, row[2:7])
                    path = ROOT / "Assets/Arts/SwordWave" / form / (motion + ".png")
                    names = re.findall(r"^      name: (.+)$", Path(str(path) + ".meta").read_text(), re.M)
                    self.assertEqual(names, [f"{motion}_{i:02d}" for i in range(count)])
                    with Image.open(path) as image:
                        self.assertEqual(image.size, (columns * width, lines * height))
                        self.assertEqual(image.mode, "RGBA")
                        for i in range(count):
                            x, y = i % columns * width, i // columns * height
                            alpha = image.getchannel("A").crop((x, y, x + width, y + height))
                            self.assertIsNotNone(alpha.getbbox())
                    self.assertEqual(len(row[10].split(",")), count)
                    self.assertTrue(all(float(ppu) > 0 for ppu in row[10].split(",")))

    def test_blade_sockets_belong_to_the_displayed_drawings(self):
        root = ROOT / "Assets/Arts/Resources/Motions12"
        rows = {tuple(row[:2]): row for row in (line.split("\t") for line in (root / "Catalog.txt").read_text().splitlines())}
        seen = set()
        for line in (root / "BladeSockets.txt").read_text().splitlines():
            key, *coords = line.split("\t")
            self.assertNotIn(key, seen)
            seen.add(key)
            form, name = key.split("/")
            motion, frame = name.rsplit("_", 1)
            self.assertLess(int(frame), int(rows[form, motion][4]))
            self.assertEqual(len(coords), 4)
            self.assertTrue(all(0 <= int(c) <= 96 for c in coords))
            self.assertNotEqual(coords[:2], coords[2:])
        for form in ("Wave", "WaveRise", "WaveWard", "WaveFinal"):
            for motion in ("SW01", "SW02", "Windup"):
                for i in range(int(rows[form, motion][4])):
                    self.assertIn(f"{form}/{motion}_{i:02d}", seen)

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
public static class KarmaBootstrap{public const int FramesPerSecond=12;}
public enum SwordEnergyPhase{Flight}
public static class Resources{public static T Load<T>(string s) where T:new(){object sprite=new Sprite{name=s,bounds=new Bounds{size=new Vector2(12.54f,12.54f)}};return (T)sprite;}}
public static class KarmaVisuals{static object material=new object();public static void ApplyMaterial(SpriteRenderer r){r.sharedMaterial=material;}}
public static class KarmaArtwork{public static Sprite Sprite(string group,string key){return new Sprite{name=group+"/"+key,bounds=new Bounds{size=new Vector2(2+key[key.Length-1]%3,4+key[key.Length-1]%2)}};}}
public class KarmaSwordVfxPool{static KarmaSwordVfxPool pool=new KarmaSwordVfxPool();public static KarmaSwordVfxPool For(KarmaGame g){return pool;}}
public static class KarmaSwordAttackPolish{public static void Core(KarmaBossAttack s){}}
public static class BasicRoute{RANGED}
public class KarmaSwordWaveAnimation{
 enum Playback{Flight}const int FrameCount=5; static readonly int[] FlightDrawings = { 0, 3 };
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
 Check(a.sprite.name==b.sprite.name&&a.sprite.name=="SwordBossVFX/SwordEnergy_SW01Flight_"+(frame*12/30%2==0?0:3),"both flights select the approved 12 fps drawing");
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

    def test_own_idle_and_motion_registration(self):
        for form in ("Wave", "WaveRise", "WaveWard", "WaveFinal"):
            idle = ROOT / "Assets/Arts/Reference" / (form + "_Idle.png")
            with Image.open(idle) as image:
                self.assertIsNotNone(image.getchannel("A").getbbox())
            for motion in ("SWBasic", "SWBasicRanged", "SW01", "SW02", "Move"):
                meta = (ROOT / "Assets/Arts/SwordWave" / form / (motion + ".png.meta")).read_text()
                self.assertIn("spriteMode: 2", meta)
                self.assertIn("enableMipMap: 0", meta)
                pivots = re.findall(r"^      pivot: (.+)$", meta, re.M)
                self.assertTrue(pivots)
                self.assertEqual(len(set(pivots)), 1)
        # Runtime snapshots check the composed renderer in both directions.
        body = (SWORD / "Visuals/KarmaCharacterAnimator.SwordSW02.cs").read_text()
        self.assertIn("picture.transform.localScale = pictureScale", method(body, "internal void RegisterSwordAppearance()"))

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
        root=ROOT/"Assets/Arts/SwordWave/Effects"
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
