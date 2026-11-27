"""Execute visual-only Basic origin math, preserve the attack contract."""
from pathlib import Path
import re
import unittest
from test_sword_presentation import method, run_cs

ROOT = Path(__file__).resolve().parents[3]
SWORD = ROOT / 'Assets/Runtime/SwordWave'


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
        source = (SWORD / "Visuals/KarmaSwordWaveAnimation.cs").read_text(encoding="utf-8-sig")
        code = r'''
using System;
struct Vector2 {
    public float x, y;
    public Vector2(float x,float y){this.x=x;this.y=y;}
    public float magnitude => (float)Math.Sqrt(x*x+y*y);
    public Vector2 normalized => magnitude == 0 ? new Vector2() : this*(1/magnitude);
    public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
    public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
    public static Vector2 operator*(Vector2 a,float k)=>new Vector2(a.x*k,a.y*k);
    public static float Distance(Vector2 a,Vector2 b)=>(a-b).magnitude;
    public static Vector2 Lerp(Vector2 a,Vector2 b,float t)=>a+(b-a)*t;
}
struct Vector3 {
    public float x,y,z;
    public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
    public static implicit operator Vector2(Vector3 v)=>new Vector2(v.x,v.y);
    public static implicit operator Vector3(Vector2 v)=>new Vector3(v.x,v.y,0);
    public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
}
static class Mathf {
    public static float Sign(float n)=>Math.Sign(n);
    public static float Abs(float n)=>Math.Abs(n);
    public static float Max(float a,float b)=>Math.Max(a,b);
    public static float Clamp01(float v)=>Math.Clamp(v,0f,1f);
    public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b);
}
static class KarmaStage {public const float ArenaLeft=-15, ArenaRight=55;}
static class KarmaBootstrap {public const float Step=1f/12;}
enum EnemyAttack {SWBasic, SW01}
class Transform {
    public Vector3 position;
    public SpriteRenderer sword;
    public Transform Find(string path)=>sword==null?null:this;
    public T GetComponent<T>() where T:class=>sword as T;
}
class SpriteRenderer {public Transform transform=new Transform(); public Vector2 bend,tip;}
class KarmaBossAttack {
    public Transform transform=new Transform(); public KarmaSwordWaveAnimation art;
    public T GetComponent<T>() where T:class=>art as T;
}
class KarmaSwordWaveAnimation {
    KarmaBossAttack owner=new KarmaBossAttack();
    Transform transform;
    SpriteRenderer picture=new SpriteRenderer();
    int pattern; bool basicVisualOrigin;
    Vector2 basicFlightOrigin,basicBladeOffset,velocity;
    float basicJoinDistance;
    static bool BladePoints(SpriteRenderer r,out Vector2 heel,out Vector2 bend,out Vector2 tip) {
        heel=default;bend=r?.bend??default;tip=r?.tip??default;return r!=null;
    }
    ORIGIN
    ALIGN
    APPLY
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    public static void Run() {
        int count=0;
        foreach(int sign in new[]{-1,1}) foreach(float dy in new[]{-.65f,0,.65f})
        foreach(float speed in new[]{2f,41.6f,80f}) foreach(float start in new[]{-14.8f,20,54.8f}){
            var shot=new KarmaBossAttack();
            var p=shot.art=new KarmaSwordWaveAnimation{transform=shot.transform};
            var axis=new Vector2(sign,dy)*(1/(float)Math.Sqrt(1+dy*dy));
            p.velocity=axis*speed;
            var origin=BasicTorsoOrigin(new Vector2(start-sign*.72f,0),4,sign);
            Check(Math.Abs(origin.x-start)<.00001f&&Math.Abs(origin.y-2.2f)<.00001f,"waist origin");
            var steel=new SpriteRenderer{bend=origin+new Vector2(sign*2,.9f),tip=origin+new Vector2(sign*4,-.4f)};
            p.owner.transform.sword=steel;
            shot.transform.position=new Vector3(origin.x,origin.y,1.25f);
            AlignBasicVisual(shot);
            var expected=Vector2.Lerp(steel.bend,steel.tip,.45f);
            expected.x=Mathf.Clamp(expected.x,KarmaStage.ArenaLeft,KarmaStage.ArenaRight);
            Check(Vector2.Distance(p.picture.transform.position,expected)<.00002f,"first drawing touches steel");
            var previous=expected;
            // A later wrist pose cannot pull a released projectile back to the owner.
            steel.bend=steel.tip=origin-new Vector2(20*sign,4);
            for(int i=0;i<=180;i++){
                float distance=p.basicJoinDistance*1.2f*i/180;
                var root=origin+axis*distance;
                if(root.x<KarmaStage.ArenaLeft||root.x>KarmaStage.ArenaRight)break;
                shot.transform.position=new Vector3(root.x,root.y,1.25f);
                AlignBasicVisual(shot);p.ApplyBasicVisualOrigin();
                Vector2 shown=p.picture.transform.position;
                Check(Vector2.Distance(shot.transform.position,root)<.00001f&&shot.transform.position.z==1.25f,"contact path fixed");
                Check(p.picture.transform.position.z==1.25f&&shown.x>=KarmaStage.ArenaLeft&&shown.x<=KarmaStage.ArenaRight,"same layer and inside arena");
                Check((shown.x-previous.x)*axis.x+(shown.y-previous.y)*axis.y>=-.00002f,"visible crescent advances");
                p.ApplyBasicVisualOrigin();
                Check(Vector2.Distance(p.picture.transform.position,shown)<.00001f,"no compounding on repeated render");
                if(distance>=p.basicJoinDistance)Check(Vector2.Distance(shown,root)<.00002f,"rejoins real shot");
                previous=shown;
            }
            count++;
        }
        Console.WriteLine("PASS "+count+" blade origins and directed transitions");
    }
}'''
        origin = re.search(r'internal static Vector2 BasicTorsoOrigin\([^;]+;', source)[0]
        code = code.replace('ORIGIN', origin).replace('ALIGN', method(source, 'internal static void AlignBasicVisual('))
        code = code.replace('APPLY', method(source, 'void ApplyBasicVisualOrigin()'))
        self.assertIn('PASS 54 blade origins', run_cs(code, 'KarmaSwordWaveAnimation', modern=True))

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
        self.assertIn('picture.transform.position = shown', method(flight, 'void ApplyBasicVisualOrigin()'))



if __name__=='__main__':
    unittest.main()
