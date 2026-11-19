"""Execute visual-only Basic origin math, preserve the attack contract."""
from pathlib import Path
import re
import unittest
from test_sword_presentation import method, run_cs

ROOT = Path(__file__).resolve().parents[3]
SWORD = ROOT / 'Assets/Scripts/SwordWave'


class BasicTorsoChecks(unittest.TestCase):
    def test_basic_facing_matches_flight_in_all_quadrants(self):
        source=(SWORD/'Visuals/KarmaSwordWaveAnimation.cs').read_text(encoding='utf-8-sig')
        angle=re.search(r'internal static float BasicFlightAngle\([^;]+;',source)[0]
        code=r'''
using System;
struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
static class Mathf{public const float Rad2Deg=180f/(float)Math.PI;public static float Atan2(float y,float x)=>(float)Math.Atan2(y,x);}
class Probe{
 ANGLE
 public static void Run(){int count=0;
  for(int degrees=-180;degrees<=180;degrees+=5){
   float a=degrees/180f*(float)Math.PI;var velocity=new Vector2((float)Math.Cos(a),(float)Math.Sin(a));
   float turn=BasicFlightAngle(velocity)/180f*(float)Math.PI;
   float flip=velocity.x<0?-1:1;
   if(Math.Abs(flip*Math.Cos(turn)-velocity.x)>.00001||Math.Abs(flip*Math.Sin(turn)-velocity.y)>.00001)throw new Exception("render axis differs from flight");
   count++;
  }Console.WriteLine("PASS "+count+" mirrored flight angles");
 }
}'''.replace('ANGLE',angle)
        self.assertIn('PASS 73 mirrored flight angles',run_cs(code,'Probe',modern=True))
        flight=method(source,'void ApplyFlight()')
        self.assertIn('picture.transform.rotation = Quaternion.Euler(0, 0, BasicFlightAngle(velocity))',flight)
        self.assertLess(flight.index('BasicFlightAngle(velocity)'),flight.index('ApplyBasicVisualOrigin();'))

    def test_speed_matches_sw01_without_shortening_cast_wait(self):
        source=(SWORD/'KarmaSwordBossBehavior.cs').read_text(encoding='utf-8-sig')
        volley=(SWORD/'Patterns/SwordWaveVolley.cs').read_text(encoding='utf-8-sig')
        pattern=(SWORD/'Patterns/SWBasic.cs').read_text(encoding='utf-8-sig')
        self.assertIn("Mathf.Max(KarmaSpace.Scale, owner.Tuning.sword.swordSpeed * 1.3f)", volley)
        speed=method(source,'public float BasicSwordSpeed')
        wait=method(source,'public float BasicReleaseWait')
        life=method(source,'public float BasicSwordLife')
        self.assertIn('owner.Wait(sword.BasicReleaseWait)',pattern)
        code = "using System; static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);} static class KarmaSpace{public const float Scale=2;public static float Units(float n)=>n*2;} class SettingsData{public float swordSpeed;} public class Probe{SettingsData Settings=new SettingsData();" + speed+wait+life + """
        public static void Run(){var p=new Probe();foreach(float setting in new[]{0f,8f,32f,40f,64f}){
        p.Settings.swordSpeed=setting;
        if(Math.Abs(p.BasicSwordSpeed-Math.Max(2,setting*1.3f))>.00001f)throw new Exception("SW01 speed");
        if(Math.Abs(p.BasicReleaseWait-10/Math.Max(10,setting*.8f))>.00001f)throw new Exception("old casting cadence");
        if(Math.Abs(p.BasicSwordSpeed*p.BasicSwordLife-10)>.00001f)throw new Exception("selection range");
        }Console.WriteLine("PASS speed and cadence");}}"""
        self.assertIn('PASS speed and cadence',run_cs(code,'Probe',modern=True))

    def test_production_warning_and_projectile_share_aimed_path(self):
        wave=(SWORD/'Visuals/KarmaSwordWaveAnimation.cs').read_text(encoding='utf-8-sig')
        pattern=(SWORD/'Patterns/SWBasic.cs').read_text(encoding='utf-8-sig')
        behavior=(SWORD/'KarmaSwordBossBehavior.cs').read_text(encoding='utf-8-sig')
        geometry=(SWORD/'Patterns/KarmaBossProjectileGeometry.cs').read_text(encoding='utf-8-sig')
        code=r'''
using System;using System.Collections;
public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public float sqrMagnitude=>x*x+y*y;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);public Vector2 normalized=>magnitude>0?this*(1/magnitude):new Vector2();public static Vector2 right=>new Vector2(1,0);public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);public static Vector2 operator*(Vector2 a,float n)=>new Vector2(a.x*n,a.y*n);public static float Distance(Vector2 a,Vector2 b)=>(a-b).magnitude;}
static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static float Sign(float a)=>a<0?-1:1;}
static class KarmaStage{public const float ArenaLeft=-15,ArenaRight=55;}static class KarmaSpace{public static float Units(float n)=>n*2;}
class KarmaGame{public Player Player=new Player();}class Player{public Vector2 Position;}
class Node{public bool active=true;public void SetActive(bool n){active=n;}}
class Transform{public Node gameObject=new Node();}class Marker{public Node gameObject=new Node();public Transform transform=new Transform();}
class Animator{public bool RangedBasicBody;public void PrepareBasicAttack(float n,bool b){}public void Attack(Vector2 n){}public void BeginBasicRelease(float n){}}
enum ActionState{Telegraph,Attack}class KarmaBossAttack{}
class KarmaEnemy{public Vector2 Position,markFrom,markTo,markSize,shotFrom,shotVelocity,shotSize;public float direction,damage,shotLife;public int shots;public ActionState state;public KarmaGame game=new KarmaGame();public object art;public Animator animator=new Animator();public float Warning(float n)=>n;public Marker PathMark(Vector2 a,Vector2 b,Vector2 size){markFrom=a;markTo=new SwordBossBehavior().WarningEndpoint(a,b);markSize=size;return new Marker();}public object Wait(float n)=>n;public object Recovery(float n)=>n;public KarmaBossAttack Attack(Vector2 a,Vector2 b,Vector2 size,float life,float d,object parent){shotFrom=a;shotVelocity=b;shotSize=size;shotLife=life;damage=d;shots++;return new KarmaBossAttack();}}
struct Bounds{public Vector2 center;}
enum EnemyAttack{SW01}class Settings{public float basicDamageMultiplier=1.2f;}
class SwordBossBehavior{public bool BasicMeleeSelected;public Vector2 BasicSwordSize=new Vector2(2,3);public float BasicSwordSpeed=41.6f,BasicSwordLife=10f/41.6f,BasicReleaseWait=.390625f,BasicWaveRange=30;public Vector2 reachOrigin,reachVelocity;public bool BasicMeleeCanReach(Vector2 a,Vector2 b)=>false;public bool SwordShotCanReach(Vector2 a,Vector2 b,Vector2 z,float life,Vector2 target){reachOrigin=a;reachVelocity=b;return true;}public Bounds SwordPlayerBounds;public object PersistentRoot;AIM ENDPOINT public Vector2 SwordSize(EnemyAttack e)=>new Vector2(3,4);}
static class KarmaSwordAttackPolish{public static object marker; public static void Warning(object m){marker=m;}}
static class KarmaSwordBasicPresentation{public static void Ranged(object g,object o,object s,Vector2 v,Vector2 a,Vector2 b){}}
enum KarmaSfxId{SwWaveLaunch}static class KarmaAudio{public static void Play(KarmaSfxId id,object lifetimeOwner){}}
static class KarmaSwordWaveAnimation{ORIGIN public static Vector2 BasicLaunchOrigin(KarmaEnemy o,Vector2 from,float facing)=>BasicTorsoOrigin(from,4,facing);public static void AlignBasicVisual(object s){}public static void Charge(object g,object o,object a,object m,float w,bool h,bool basicRanged){}}
GEOMETRY
public class Probe{
 KarmaEnemy owner;SwordBossBehavior sword=new SwordBossBehavior();Settings Settings=new Settings();static void Destroy(object n){}IEnumerator Melee(){yield break;}
 EXECUTE
 CAN_REACH
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 public static void Run(){int cases=0;
 foreach(int facing in new[]{-1,1})foreach(float distance in new[]{4f,8f,16f})foreach(float targetY in new[]{1f,2.5f,5f}){
 var p=new Probe();p.owner=new KarmaEnemy{Position=new Vector2(20,.3f),direction=facing};p.sword.SwordPlayerBounds=new Bounds{center=new Vector2(20+facing*distance,targetY)};
 p.owner.game.Player.Position=p.sword.SwordPlayerBounds.center;var run=p.Execute();while(run.MoveNext()){}
 var o=p.owner;Check(p.CanReach(o.Position,p.sword.SwordPlayerBounds.center),"can reach from waist");Check(Vector2.Distance(p.sword.reachOrigin,o.shotFrom)<.00001f&&Vector2.Distance(p.sword.reachVelocity,o.shotVelocity)<.00001f,"selection geometry uses same origin and aim");Check(o.shots==1&&o.damage==1.2f,"one unchanged-damage shot");Check(Vector2.Distance(o.shotFrom,o.markFrom)<.00001f,"warning and real launch origins match");
 Vector2 end=o.shotFrom+o.shotVelocity*o.shotLife;Check(Vector2.Distance(end,o.markTo)<.00001f,"same warning and boundary endpoint");
 Check(KarmaSwordAttackPolish.marker!=null,"original aimed-path marker is active");
 var target=p.sword.SwordPlayerBounds.center;float at=(target.x-o.shotFrom.x)/o.shotVelocity.x;Check(at>=0&&at<=o.shotLife,"target reached before boundary");Check(Math.Abs(o.shotFrom.y+o.shotVelocity.y*at-target.y)<.00001f,"aim intersects player center rather than passing overhead");
 Check(Math.Abs(o.shotVelocity.magnitude-41.6f)<.0001f,"speed preserved");Check(o.shotSize.x==o.markSize.x&&o.shotSize.y==o.markSize.y,"same collision and warning dimensions");cases++;
 }Console.WriteLine("PASS "+cases+" production warning/launch/player-intersection paths");}
}'''
        origin=re.search(r'internal static Vector2 BasicTorsoOrigin\([^;]+;',wave)[0]
        code=code.replace('ORIGIN',origin).replace('AIM',method(behavior,'public static Vector2 SwordAim(')).replace('ENDPOINT',method(behavior,'public override Vector2 WarningEndpoint(').replace('public override','public')).replace('CAN_REACH',method(pattern,'public override bool CanReach(').replace('public override','public')).replace('EXECUTE',method(pattern,'public override IEnumerator Execute()').replace('public override','public')).replace('GEOMETRY',method(geometry,'internal static class KarmaBossProjectileGeometry'))
        self.assertIn('PASS 18 production warning/launch/player-intersection paths',run_cs(code,'Probe',modern=True))

    def test_actual_origin_and_render_offset(self):
        source = (SWORD/'Visuals/KarmaSwordWaveAnimation.cs').read_text(encoding='utf-8-sig')
        def expression(name):
            match = re.search(r'internal static (?:Vector2|float) '+name+r'\([^;]+;', source)
            return match[0]
        code = r'''
using System;
public struct Vector2{public float x,y;public Vector2(float a,float b){x=a;y=b;}public static Vector2 operator+(Vector2 a,Vector2 b){return new Vector2(a.x+b.x,a.y+b.y);}}
struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 operator-(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}public static Vector3 operator+(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}public static Vector3 operator*(Vector3 a,float n){return new Vector3(a.x*n,a.y*n,a.z*n);}}
static class Mathf{public static float Sign(float n){return n<0?-1:1;}public static float Clamp01(float n){return Math.Max(0,Math.Min(1,n));}public static float SmoothStep(float a,float b,float n){n=Clamp01(n);return a+(b-a)*n*n*(3-2*n);}}
class Transform{public Vector3 position,scale=new Vector3(1,1,1);public Vector3 TransformVector(Vector3 v){return new Vector3(v.x*scale.x,v.y*scale.y,v.z*scale.z);}}
class Renderer{public bool flipX;public Transform transform=new Transform();}
public class Probe{
 Transform transform=new Transform();Renderer picture=new Renderer();bool basicVisualOrigin;float age;
 ORIGIN
 APPLY
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 public static void Run(){int cases=0;
 foreach(float height in new[]{2.5f,3.31f,5.6f,7f})foreach(int facing in new[]{-1,1})foreach(float x in new[]{-14f,20f,54f}){
 var origin=BasicTorsoOrigin(new Vector2(x,.3f),height,facing);
 Check(Math.Abs((origin.x-x)*facing-height*.18f)<.00001f,"mirrored torso front");Check(Math.Abs(origin.y-(.3f+height*.55f))<.00001f,"upper torso height");
 var p=new Probe();p.picture.flipX=facing<0;p.picture.transform.scale=new Vector3(facing*1.4f,1.8f,1);p.transform.position=new Vector3(origin.x,origin.y,0);p.basicVisualOrigin=true;
 p.ApplyBasicVisualOrigin();var center=p.picture.transform.TransformVector(new Vector3(0,0,0));Check(Math.Abs(p.picture.transform.position.x+center.x-origin.x)<.00001f&&Math.Abs(p.picture.transform.position.y+center.y-origin.y)<.00001f,"visible crescent center at waist");
 for(int i=0;i<=96;i++){p.age=i/96f;p.transform.position=new Vector3(origin.x+facing*10*p.age,origin.y-2*p.age,0);var root=p.transform.position;p.ApplyBasicVisualOrigin();Check(p.transform.position.x==root.x&&p.transform.position.y==root.y,"root never moved");var first=p.picture.transform.position;p.ApplyBasicVisualOrigin();Check(p.picture.transform.position.x==first.x,"offset never compounds");}
 Check(Math.Abs(p.picture.transform.position.x+center.x-p.transform.position.x)<.00001f&&Math.Abs(p.picture.transform.position.y+center.y-p.transform.position.y)<.00001f,"visual follows actual collision lane at all times");cases++;
 }Console.WriteLine("PASS "+cases+" torso origins and render-only transitions");}
}'''
        code=code.replace('ORIGIN',expression('BasicTorsoOrigin')).replace('APPLY',method(source,'void ApplyBasicVisualOrigin()'))
        self.assertIn('PASS 24 torso origins',run_cs(code,'Probe',modern=True))
        apply=method(source,'void ApplyBasicVisualOrigin()')
        self.assertNotIn('localScale',apply)
        self.assertNotIn('color',apply)

    def test_attack_and_asset_wiring(self):
        source=(SWORD/'Patterns/SWBasic.cs').read_text(encoding='utf-8-sig')
        self.assertIn('owner.Attack(origin, velocity, sword.BasicSwordSize, flightLife,',source)
        self.assertIn('Settings.basicDamageMultiplier, parent: sword.PersistentRoot)',source)
        self.assertIn('AlignBasicVisual(shot)',source)
        self.assertEqual(source.count('AlignBasicVisual('),1)
        self.assertIn('BasicLaunchOrigin(owner, owner.Position, owner.direction)',source)
        self.assertIn('BasicLaunchOrigin(owner, from, facing)',source)
        self.assertIn('owner.PathMark(origin, origin + velocity * flightLife, sword.BasicSwordSize)',source)
        self.assertIn('SwordAim(origin, sword.SwordPlayerBounds.center, owner.direction)',source)
        self.assertNotIn('AlignBasicVisual(',source.split('IEnumerator Melee()')[1])
        flight=(SWORD/'Visuals/KarmaSwordWaveAnimation.cs').read_text(encoding='utf-8-sig')
        self.assertIn('FlightDrawings[', method(flight, 'void ApplyFlight()'))
        self.assertIn('picture.transform.position = transform.position', method(flight, 'void ApplyBasicVisualOrigin()'))



if __name__=='__main__':
    unittest.main()
