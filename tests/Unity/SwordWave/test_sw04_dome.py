"""Execute the production SW04 mesh and lifecycle without launching Unity."""
import unittest
from pathlib import Path

from test_sword_presentation import method, run_cs

ROOT = Path(__file__).resolve().parents[3]
VISUAL = ROOT / "Assets/Runtime/SwordWave/Visuals/KarmaSwordWardVfx.cs"


class WardChecks(unittest.TestCase):
    def test_natural_expiry_has_no_shock_or_discharge(self):
        source = (ROOT / "Assets/Runtime/SwordWave/Patterns/SW04.cs").read_text(encoding="utf-8-sig")
        cleanup = method(source, "void ClearSwordGuard(")
        self.assertIn("wardVfx?.Finish();", cleanup)
        self.assertNotIn("ApplyShock", source)
        self.assertNotIn("Discharge", source)
        self.assertNotIn("PaintDischarge", VISUAL.read_text(encoding="utf-8-sig"))

    def test_no_gameplay_or_body_writes_and_existing_event_routes(self):
        source = VISUAL.read_text(encoding="utf-8-sig")
        for token in ("AddComponent<Collider", "AddComponent<KarmaBossAttack", ".TakeDamage(",
                      ".Stun(", "body.color =", "body.transform.localScale ="):
            self.assertNotIn(token, source)
        self.assertIn("FrameIndex=(int)Mathf.Floor(age*KarmaBootstrap.FramesPerSecond)%KarmaBootstrap.FramesPerSecond", source)
        self.assertIn("swordGuard", (ROOT / "Assets/Runtime/SwordWave/Patterns/SW04.cs").read_text(encoding="utf-8-sig"))
        pattern = (ROOT / "Assets/Runtime/SwordWave/Patterns/SW04.cs").read_text(encoding="utf-8-sig")
        self.assertIn("new KarmaAttackContact[4]", pattern)
        orbit = method(pattern, "void UpdateSwordGuardBlades(")
        self.assertIn("blades[i].enabled = true", orbit)
        self.assertNotIn("wardVfx.Countering", orbit)
        self.assertIn("wardVfx.BladeOrbitPosition(i, orbitTime)", orbit)
        self.assertIn("Mathf.Atan2(tangent.y, tangent.x)", orbit)
        self.assertIn("i == aiming", orbit)
        self.assertNotIn("KarmaSpace.Units(1.6f)", orbit)
        self.assertNotIn("if(!countering)", method(source, "void Paint()"))
        self.assertIn("return 0.35f;", pattern)
        self.assertIn("Mathf.Max(0.3f, Settings.counterWarning)", pattern)
        self.assertIn("Mathf.Max(0.16f, Settings.swordGuardResponseCooldown - GuardTrackingTime - GuardLockTime)", pattern)
        self.assertIn("wardVfx?.Absorb();", pattern)
        self.assertIn("KarmaSwordWardVfx.Prepare(owner.game, owner, owner.Warning(0.7f))", pattern)
        self.assertNotIn("KarmaSwordAttackPolish.Charge(", pattern)
        self.assertIn("wardVfx?.Released(origin,aim);", pattern)
        cleanup = method(pattern, "void ClearSwordGuard(")
        self.assertIn("if (!stopRoutine && owner.Active && owner.game.IsCombat)", cleanup)
        self.assertIn("wardVfx?.Finish();", cleanup)
        parry = method(pattern, "public void OnParried(")
        self.assertLess(parry.index("volley.Contacts[later].TryConsume()"), parry.index("ReturnParriedBlade(volley, parryAt)"))
        self.assertIn("slot.gameObject.SetActive(false)", parry)
        behavior = (ROOT / "Assets/Runtime/SwordWave/KarmaSwordBossBehavior.cs").read_text(encoding="utf-8-sig")
        self.assertIn("ParryStunSeconds { get { return 2; } }", behavior)
        self.assertIn("owner.game.StageIndex == 3 || owner.running && owner.selected == EnemyAttack.SW05", behavior)
        self.assertIn("if (parried) { counter.OnParried(contact); }", behavior)
        self.assertIn("owner.fullShield || counter.Guarding", method(behavior,"public override Color ShieldColor"))
        self.assertIn("? Color.clear : base.ShieldColor", method(behavior,"public override Color ShieldColor"))
        self.assertNotIn("WardBurst(transform,game,Center", method(source,"public void Absorb()"))
        self.assertNotIn("WardBurst(",method(source,"public void Collapse()"))
        feedback=method(behavior,"public override void ReducedDamageFeedback(")
        self.assertNotIn("KarmaRemnantVFX.Impact(",feedback)
        self.assertIn("counter.GuardImpact()",feedback)

    def test_aiming_blade_follows_dash_until_release(self):
        pattern=(ROOT / "Assets/Runtime/SwordWave/Patterns/SW04.cs").read_text(encoding="utf-8-sig")
        update=method(pattern,"void UpdateSwordGuardBlades(")
        routine=method(pattern,"IEnumerator MaintainSwordGuard(")
        locked=routine[routine.index('float lockedUntil'):routine.index('shots[i] = owner.Attack')]
        self.assertIn('origin = blades[i].transform.position;',locked)
        self.assertIn('UpdateSwordGuardPathMark(mark, origin,',locked)
        self.assertNotIn('aim =',locked)
        self.assertEqual(routine.count('UpdateSwordGuardBlades(blades, i, aimingOrbitAt);'),2)
        self.assertNotIn('origin =',locked[locked.rindex('yield return null;'):])
        self.assertNotIn('CounterFlight(',VISUAL.read_text(encoding='utf-8-sig'))
        code=r'''
using System;
struct Vector2{public float x,y;public Vector2(float a,float b){x=a;y=b;}public float sqrMagnitude=>x*x+y*y;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);}
struct Quaternion{public static Quaternion RotateTowards(Quaternion a,Quaternion b,float speed)=>b;public float z;public static Quaternion Euler(float x,float y,float z)=>new Quaternion{z=z};}
struct Color{public float a;public Color(float r,float g,float b,float a){this.a=a;}}
class Transform{public Vector2 position;public Quaternion rotation;}
class SpriteRenderer{public Transform transform=new Transform();public bool enabled;public Color color;}
static class Time{public const float deltaTime=1f/60;}
static class Mathf{public static float Clamp01(float x)=>Clamp(x,0,1);public const float Rad2Deg=57.29578f;public static float Clamp(float x,float a,float b)=>Math.Max(a,Math.Min(x,b));public static float Atan2(float y,float x)=>(float)Math.Atan2(y,x);}
static class KarmaStage{public const float ArenaLeft=-15,ArenaRight=55;}static class KarmaSpace{public static float Units(float x)=>x*2;}
enum EnemyAttack{SW04}class Sword{public Vector2 SwordSize(EnemyAttack a)=>new Vector2(1,1);}
class Volley{public bool Returning;}class Owner{public float FightSeconds;public Vector2 Position;}
class Ward{public Owner owner;public Vector2 BladeOrbitPosition(int i,float t)=>new Vector2(owner.Position.x+(float)Math.Cos(t+i)*3,owner.Position.y+4+(float)Math.Sin(t+i));public void ApplyBladeOrbitDepth(int i,float t){}public float BladeOrbitAlpha(int i,float t)=>.8f;}
public class Probe{const float GuardTrackingTime=.35f;Owner owner=new Owner();Sword sword=new Sword();Ward wardVfx;float swordGuardUntil=100;bool Guarding=true;Volley activeVolley;
UPDATE
static void Check(bool b,string m){if(!b)throw new Exception(m);}
public static void Run(){foreach(int facing in new[]{-1,1})foreach(int selected in new[]{0,1,2,3}){
 var p=new Probe();p.wardVfx=new Ward{owner=p.owner};p.owner.Position=new Vector2(20,1);p.owner.FightSeconds=1;
 var blades=new SpriteRenderer[4];for(int i=0;i<4;i++)blades[i]=new SpriteRenderer();
 blades[selected].transform.rotation=Quaternion.Euler(0,0,37);blades[selected].color=new Color(1,1,1,1);
 p.UpdateSwordGuardBlades(blades,selected,.5f);var before=blades[selected].transform.position;
 for(int step=1;step<=20;step++){
 p.owner.Position=new Vector2(20+facing*step*.4f,1+step*.025f);p.owner.FightSeconds=1+step*.05f;
 p.UpdateSwordGuardBlades(blades,selected,.5f);var at=blades[selected].transform.position;
 Check(Math.Abs(at.x-before.x-facing*step*.4f)<.0001f&&Math.Abs(at.y-before.y-step*.025f)<.0001f,"locked surface point follows dash and height");
 Check(blades[selected].transform.rotation.z==37&&blades[selected].color.a==1,"committed direction and cue remain intact");
 }
 p.owner.Position=new Vector2(33,2);p.UpdateSwordGuardBlades(blades,selected,.5f);
 var release=blades[selected].transform.position;var expected=p.wardVfx.BladeOrbitPosition(selected,.5f+.35f/3);
 Check(Math.Abs(release.x-expected.x)<.0001f&&Math.Abs(release.y-expected.y)<.0001f,"release-frame dash uses current dome position");
 }Console.WriteLine("PASS dash attachment");}}
'''.replace('UPDATE',update)
        self.assertIn('PASS dash attachment',run_cs(code,'Probe',modern=True))

    def test_production_mesh_attachment_events_cleanup_and_pool(self):
        source = VISUAL.read_text(encoding="utf-8-sig")
        mocks = r'''
namespace UnityEngine {
 public class Object { public static void Destroy(Object o){var g=o as GameObject;if(g!=null)g.SetActive(false);} }
 public class Component:Object {
  public GameObject gameObject;public Transform transform{get{return gameObject.transform;}}
  public T GetComponent<T>() where T:class{return gameObject.GetComponent<T>();}
  public T GetComponentInParent<T>() where T:class{for(var t=transform;t!=null;t=t.parent){var v=t.GetComponent<T>();if(v!=null)return v;}return null;}
  public T[] GetComponentsInChildren<T>(){return System.Array.Empty<T>();}
 }
 public class MonoBehaviour:Component{}
 public class GameObject:Object {
  public bool activeInHierarchy=true;public Transform transform;public System.Collections.Generic.List<Component> components=new System.Collections.Generic.List<Component>();
  public GameObject(string name=""){transform=new Transform{gameObject=this};}
  public T AddComponent<T>()where T:Component,new(){var c=new T{gameObject=this};components.Add(c);return c;}
  public T GetComponent<T>()where T:class{foreach(var c in components)if(c is T)return c as T;return null;}
  public void SetActive(bool active){activeInHierarchy=active;if(!active){foreach(var c in components){var m=c.GetType().GetMethod("OnDisable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);if(m!=null)m.Invoke(c,null);}foreach(var t in transform.children.ToArray())t.gameObject.SetActive(false);}}
 }
 public class Transform {
  public GameObject gameObject;public Transform parent,found;public Vector3 position,localScale=new Vector3(1,1,1),lossyScale=new Vector3(1,1,1);public Quaternion rotation;
  public System.Collections.Generic.List<Transform> children=new System.Collections.Generic.List<Transform>();
  public void SetParent(Transform t,bool world){if(parent!=null)parent.children.Remove(this);parent=t;t.children.Add(this);}
  public Transform Find(string path){return found;}
  public T GetComponent<T>()where T:class{return gameObject.GetComponent<T>();}
  public T GetComponentInParent<T>()where T:class{for(var t=this;t!=null;t=t.parent){var v=t.GetComponent<T>();if(v!=null)return v;}return null;}
  public Vector3 TransformPoint(Vector3 p){return position+new Vector3(p.x*lossyScale.x,p.y*lossyScale.y,p.z*lossyScale.z);}
 }
 public struct Vector2 {
  public float x,y;public Vector2(float a,float b){x=a;y=b;}public static Vector2 up{get{return new Vector2(0,1);}}
  public Vector2 normalized{get{float m=(float)System.Math.Sqrt(x*x+y*y);return m>0?this*(1/m):new Vector2();}}
  public static Vector2 operator+(Vector2 a,Vector2 b){return new Vector2(a.x+b.x,a.y+b.y);}public static Vector2 operator-(Vector2 a,Vector2 b){return new Vector2(a.x-b.x,a.y-b.y);}
  public static Vector2 operator*(Vector2 a,float s){return new Vector2(a.x*s,a.y*s);}public static Vector2 operator/(Vector2 a,float s){return a*(1/s);}
  public static Vector2 Scale(Vector2 a,Vector2 b){return new Vector2(a.x*b.x,a.y*b.y);}public static implicit operator Vector3(Vector2 a){return new Vector3(a.x,a.y,0);}public static implicit operator Vector2(Vector3 a){return new Vector2(a.x,a.y);}
 }
 public struct Vector3 {
  public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero;
  public static Vector3 operator+(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}
 }
 public struct Quaternion{public static Quaternion identity;}
 public struct Color{public float r,g,b,a;public Color(float x,float y,float z,float alpha){r=x;g=y;b=z;a=alpha;}}
 public struct Bounds {public Vector3 center,size;public Bounds(Vector3 c,Vector3 s){center=c;size=s;}public bool Contains(Vector3 p){return System.Math.Abs(p.x-center.x)<=size.x/2&&System.Math.Abs(p.y-center.y)<=size.y/2;}}
 public class Material{}
 public class Mesh:Object {public static int allocated;public Vector3[] vertices;public Color[] colors;public int[] triangles;public int occupied;public string name;public Bounds bounds;public Mesh(){allocated++;}public void MarkDynamic(){}public void SetVertices(Vector3[] v){vertices=v;}public void SetColors(Color[] c){colors=c;}public void SetSubMesh(int n,Rendering.SubMeshDescriptor d,Rendering.MeshUpdateFlags f){occupied=d.vertexCount;}}
 public class MeshFilter:Component{public Mesh sharedMesh;}
 public class MeshRenderer:Component{public bool enabled=true,receiveShadows;public int sortingOrder,sortingLayerID;public Material sharedMaterial;public Rendering.ShadowCastingMode shadowCastingMode;}
 public class SpriteRenderer:Component{public bool enabled=true;public Color color;public int sortingOrder=5,sortingLayerID=3;}
 public class TrailRenderer:Component{public bool enabled;}
 public static class Time{public static float deltaTime=.01f;}
 public static class Mathf{
  public const float PI=(float)System.Math.PI;
  public static float Abs(float a){return System.Math.Abs(a);}public static float Min(float a,float b){return System.Math.Min(a,b);}public static float Max(float a,float b){return System.Math.Max(a,b);}public static float Clamp01(float a){return Max(0,Min(1,a));}
  public static float Floor(float a){return (float)System.Math.Floor(a);}public static float Cos(float a){return (float)System.Math.Cos(a);}public static float Sin(float a){return (float)System.Math.Sin(a);}public static float Sqrt(float a){return (float)System.Math.Sqrt(a);}
  public static float Repeat(float a,float b){return a-Floor(a/b)*b;}public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return a+(b-a)*t*t*(3-2*t);}
 public static float Asin(float a){return (float)System.Math.Asin(a);}public static float Lerp(float a,float b,float t){return a+(b-a)*Clamp01(t);}
 }
 public class DefaultExecutionOrder:System.Attribute{public DefaultExecutionOrder(int n){}}
 public enum RuntimeInitializeLoadType{SubsystemRegistration}public class RuntimeInitializeOnLoadMethod:System.Attribute{public RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType t){}}
}
namespace UnityEngine.Rendering {
 public enum ShadowCastingMode{Off}public enum MeshUpdateFlags{DontRecalculateBounds}
 public struct SubMeshDescriptor {public int firstVertex,vertexCount;public UnityEngine.Bounds bounds;public SubMeshDescriptor(int a,int b){firstVertex=vertexCount=0;bounds=new UnityEngine.Bounds();}}
}
namespace KarmaPrototype {
 public enum RunPhase{Combat,End}
 public class KarmaGame{public bool IsCombat=true;public Transform World=new GameObject().transform;public bool Paused;public RunPhase Phase=RunPhase.Combat;}
 public enum EnemyAttack{SW04}
 public class KarmaEnemy:MonoBehaviour{public float FightSeconds;public bool Alive=true;public int CurrentPattern;public bool PresentationActive=true,Stunned;public int Facing=1;public Vector2 Position{get{return transform.position;}}}
 public enum KarmaSfxId{SwEnergyCharge}public static class KarmaAudio{public static void Play(KarmaSfxId id,float seconds,Component owner){}}
 public class KarmaCharacterAnimator:MonoBehaviour{public float SwordChargeBodyHeight=5;public Vector3 SwordVisualHome,SwordFoot;}
 public class KarmaBossAttack:MonoBehaviour{public SpriteRenderer Visual=new GameObject().AddComponent<SpriteRenderer>();}
 public static class KarmaCombatVFX{public static Material GlowMaterial=new Material();}public static class KarmaVisuals{public static Material DefaultMaterial=new Material();}
 public static class KarmaBootstrap{public const int FramesPerSecond=12;}
 public static class KarmaSpace{public const float GuardianGroundY=2.3f;}
 public static class KarmaSwordAttackPolish{public static void Core(KarmaBossAttack s){}}
 public static class KarmaSW05VFX{public static void ApplySword(SpriteRenderer s,Vector2 size,Vector2 velocity){}}
 public static class KarmaSwordSkillSurface{public static void Apply(SpriteRenderer s,int n){}}
 public static class KarmaSW05Energy{public static int bursts,style;public static void WardBurst(Transform root,KarmaGame g,Vector2 p,Vector2 a,float r,int s){bursts++;style=s;}}
 public static class WardProbe {
  static object Get(object o,string n){return o.GetType().GetField(n,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(o);}
  static void Tick(KarmaSwordWardVfx v){v.GetType().GetMethod("LateUpdate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(v,null);}
  static void Check(bool b,string m){if(!b)throw new System.Exception(m);}
  static Mesh MeshOf(KarmaSwordWardVfx v,string n){return (Mesh)Get(Get(v,n),"mesh");}
  static int peak;
  static void Geometry(KarmaSwordWardVfx v){foreach(string n in new[]{"shell","detail"}){var buffer=Get(v,n);var mesh=(Mesh)buffer.GetType().GetField("mesh").GetValue(buffer);int count=(int)buffer.GetType().GetField("count").GetValue(buffer);peak=System.Math.Max(peak,count);Check(count<2048,"buffer headroom");for(int i=0;i<count*4;i++){Check(mesh.bounds.Contains(mesh.vertices[i]),"all used vertices inside dynamic bounds");Check(!float.IsNaN(mesh.vertices[i].x),"finite geometry");}}}
  public static void Run(){int cases=0;
   foreach(float h in new[]{3.7f,4.6f,5.5f,6.1f})foreach(int facing in new[]{-1,1})foreach(int end in new[]{0,1,2,3,4,5}){
    var game=new KarmaGame();var owner=new GameObject().AddComponent<KarmaEnemy>();owner.Facing=facing;owner.transform.position=new Vector3(20,2.3f,0);
    var art=new GameObject().transform;art.position=new Vector3(20,0,0);art.lossyScale=new Vector3(facing,1,1);var animator=art.gameObject.AddComponent<KarmaCharacterAnimator>();animator.SwordChargeBodyHeight=h;
    var body=new GameObject().AddComponent<SpriteRenderer>();body.transform.SetParent(art,false);owner.transform.found=body.transform;
    var root=new GameObject().transform;root.SetParent(game.World,false);root.lossyScale=new Vector3(facing,1.7f,1);
    var blades=new SpriteRenderer[4];for(int i=0;i<4;i++){blades[i]=new GameObject().AddComponent<SpriteRenderer>();blades[i].enabled=false;}
    var preparation=KarmaSwordWardVfx.Prepare(game,owner,.7f);Check((float)Get(preparation,"age")==0,"windup begins at ground within original warning");
    for(int j=0;j<25;j++)Tick(preparation);Check((float)Get(preparation,"age")<.7f,"unfolding adds no attack wait");
    for(int j=0;j<50;j++)Tick(preparation);
    Check(Get(preparation,"shell")!=null,"completed warning survives until pattern hands over the same shell");
    var formationMesh=Get(preparation,"shell");float formationAge=(float)Get(preparation,"age");
    preparation.Form(root,blades);
    Check(Get(preparation,"shell")==formationMesh&&(float)Get(preparation,"age")==formationAge,"formation preserves mesh and effect phase");
    Check(preparation.BladeOrbitAlpha(0,1)==0,"new swords start transparent");
    for(int j=0;j<20;j++)Tick(preparation);
    Check(preparation.BladeOrbitAlpha(0,1)>.48f,"swords smoothly materialize on the existing shell");
    preparation.gameObject.SetActive(false);
    var v=KarmaSwordWardVfx.Create(root,game,owner,blades);int allocated=Mesh.allocated;
    for(int blade=0;blade<4;blade++){
     float minX=1,maxX=-1,minY=1,maxY=0;var last=v.BladeOrbitPosition(blade,0);
     for(int step=0;step<=720;step++){
      var p=v.BladeOrbitPosition(blade,step/24f);float x=(p.x-20)/(.62f*h),y=(p.y-.48f*h)/(.62f*h);
      Check(x*x+y*y<=1.0001f&&p.y>0&&p.y<1.10f*h,"all orbit points stay in the body-centred sphere above floor");
      Check(System.Math.Abs(p.x-last.x)<h*.2f&&System.Math.Abs(p.y-last.y)<h*.2f,"continuous orbit without random frame jumps");last=p;
      var opposite=v.BladeOrbitPosition((blade+2)%4,step/24f);
      Check(System.Math.Abs(p.x+opposite.x-40)<.0001f&&System.Math.Abs(p.y+opposite.y-.96f*h)<.0001f,"paired swords remain half a turn apart");
      var repeat=v.BladeOrbitPosition(blade,step/24f+Mathf.PI*2/1.65f);
      Check(System.Math.Abs(p.x-repeat.x)<.0001f&&System.Math.Abs(p.y-repeat.y)<.0001f,"constant period without random drift");
      v.ApplyBladeOrbitDepth(blade,step/24f);
      float scale=blades[blade].transform.localScale.x;
      Check(scale>=.759f&&scale<=1.041f,"absolute perspective scale stays bounded across repeated updates");
      float depth=Mathf.Sin(step/24f*1.65f*(blade%2==0?1:-1)+(blade%2)*Mathf.PI*.5f+(blade/2)*Mathf.PI);
      if(depth<0)Check(blades[blade].sortingOrder<body.sortingOrder,"rear swords pass behind body");
      else if(depth>0)Check(blades[blade].sortingOrder>body.sortingOrder,"front swords pass in front");
      float alpha=v.BladeOrbitAlpha(blade,step/24f);
      Check(alpha>=.969f&&alpha<=1.001f,"front and rear sword cores remain nearly opaque");
      minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
     }
     Check(minX<-.65f&&maxX>.65f&&minY<-.4f&&maxY>.4f,"tilted rings cover horizontal and vertical motion");
    }
    var independentA=v.BladeOrbitPosition(0,1);var independentB=v.BladeOrbitPosition(1,1);
    Check(System.Math.Abs(independentA.x-independentB.x)+System.Math.Abs(independentA.y-independentB.y)>.01f,"swords have independent orbit phases");
    for(int step=0;step<96;step++){
     var rim=(Vector2)typeof(KarmaSwordWardVfx).GetMethod("Dome",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(v,new object[]{step/95f,1f});
     float dx=rim.x-20,dy=rim.y-.48f*h;
     Check(System.Math.Abs(dx*dx+dy*dy-(.62f*h)*(.62f*h))<.0001f,"shield preserves circular curvature instead of stretching an ellipse");
     Check(rim.y>=-.00001f&&rim.y<=1.10f*h,"shield meets floor and clears the head");
     if(step==0||step==95)Check(System.Math.Abs(rim.y)<.00001f,"both shell edges meet the floor");
    }
    Check(((MeshRenderer)Get(v,"back")).sortingOrder<body.sortingOrder&&((MeshRenderer)Get(v,"front")).sortingOrder>body.sortingOrder,"shell behind body, fine detail above");
    if(cases==0){
     var buffer=Get(v,"detail");var type=buffer.GetType();
     var crackle=typeof(KarmaSwordWardVfx).GetMethod("Crackle",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
     type.GetMethod("Begin").Invoke(buffer,null);crackle.Invoke(v,new object[]{1f,0f,1f});
     int bolts=(int)type.GetField("count").GetValue(buffer);Check(bolts>=100&&bolts<=200,"bounded short electric forks and surface seams");
     var electricPoints=(Vector3[])type.GetField("vertices").GetValue(buffer);float firstX=electricPoints[0].x;
     type.GetMethod("Begin").Invoke(buffer,null);crackle.Invoke(v,new object[]{1f,3/24f,1f});
     Check(System.Math.Abs(electricPoints[0].x-firstX)>.001f,"crackle changes shape between bursts");
     var trail=typeof(KarmaSwordWardVfx).GetMethod("BladeTrails",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
     for(int i=0;i<4;i++){blades[i].enabled=true;blades[i].transform.position=new Vector3(24+i,3+i,0);}
     var trailBack=Get(v,"shell");type.GetMethod("Begin").Invoke(trailBack,null);
     type.GetMethod("Begin").Invoke(buffer,null);trail.Invoke(v,new object[]{1f});
     Check((int)type.GetField("count").GetValue(buffer)+(int)type.GetField("count").GetValue(trailBack)==96,"four visible swords each have one short trail");
     var points=(Vector3[])type.GetField("vertices").GetValue(buffer);
     Check(System.Math.Abs((points[0].x+points[1].x)/2-24)<.001f&&System.Math.Abs((points[0].y+points[1].y)/2-3)<.001f,"trail starts at actual sword slot");
     v.Aim(0,new Vector2(24,3),new Vector2(1,0),new Vector2(7,5),false);
     Check(blades[0].transform.localScale.x==1&&blades[0].sortingOrder==5,"aim restores exact authored scale and sorting");
     type.GetMethod("Begin").Invoke(trailBack,null);
     type.GetMethod("Begin").Invoke(buffer,null);trail.Invoke(v,new object[]{1f});
     Check((int)type.GetField("count").GetValue(buffer)+(int)type.GetField("count").GetValue(trailBack)==72,"aiming blade has no misleading orbit trail");
     Check(blades[0].transform.position.x==24&&blades[0].transform.position.y==3,"cosmetics never move counter origin");
     typeof(KarmaSwordWardVfx).GetField("aiming",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(v,-1);
    }
    for(int i=0;i<35;i++){Tick(v);Geometry(v);}Check(Mesh.allocated==allocated,"no meshes allocated while animating");
    var shell=Get(v,"shell");var shellMesh=(Mesh)shell.GetType().GetField("mesh").GetValue(shell);
    var hexBuffer=Get(v,"detail");var hexType=hexBuffer.GetType();
    hexType.GetMethod("Begin").Invoke(hexBuffer,null);
    typeof(KarmaSwordWardVfx).GetMethod("Hexagons",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(v,new object[]{1f,0f,1f});
    int hexCount=(int)hexType.GetField("count").GetValue(hexBuffer);var hexColors=(Color[])hexType.GetField("colors").GetValue(hexBuffer);
    Check(hexCount>200,"connected curved hex lattice covers shield");
    for(int i=0;i<hexCount*4;i++)Check(hexColors[i].a>=.048f&&hexColors[i].a<.23f,"grid remains visible and translucent at rest");
    for(int i=0;i<24*16*4;i++)Check(shellMesh.colors[i].a>=.035f&&shellMesh.colors[i].a<.15f,"pale continuous membrane leaves the opaque swords and body readable");
    Check(shellMesh.colors[0].a>shellMesh.colors[7*4].a,"denser outer surface conveys rounded barrier");
    float before=(float)Get(v,"age");int frame=v.FrameIndex;Check(frame==(int)System.Math.Floor(before*12)%12,"guard animation uses the 12fps clock");game.Paused=true;Tick(v);Check((float)Get(v,"age")==before&&v.FrameIndex==frame,"pause freezes dome");game.Paused=false;
    art.position=new Vector3(43,2,0);Tick(v);var foot=(Vector2)Get(v,"foot");Check(foot.x==43&&foot.y==2,"registered support follows boss translation and facing");
    int bursts=KarmaSW05Energy.bursts;v.Absorb();Check(v.Countering&&KarmaSW05Energy.bursts==bursts,"absorb stays on dome without duplicate circular burst");foreach(var b in blades)Check(b.enabled,"existing counter blades remain visible");
    for(int i=0;i<4;i++){v.Aim(i,new Vector2(42+i,3),new Vector2(facing,0),new Vector2(7,5),false);Tick(v);v.Aim(i,new Vector2(42+i,3),new Vector2(facing,0),new Vector2(7,5),true);for(int t=0;t<20;t++)Tick(v);Geometry(v);v.Released(new Vector2(42+i,3),new Vector2(facing,0));Geometry(v);}
    Check(KarmaSW05Energy.bursts==bursts+4,"four release cues, no extra attacks or bursts");
    if(cases==0){
     var tick=(System.Action)System.Delegate.CreateDelegate(typeof(System.Action),v,typeof(KarmaSwordWardVfx).GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic));
     for(int j=0;j<100;j++)tick();long memory=System.GC.GetAllocatedBytesForCurrentThread();for(int j=0;j<1000;j++)tick();
     Check(System.GC.GetAllocatedBytesForCurrentThread()==memory,"no per-frame managed geometry allocations");
    }
    if(end==0){
     var beforeDetail=Get(v,"detail");int beforeCount=(int)beforeDetail.GetType().GetField("count").GetValue(beforeDetail);
     v.Finish();Check(v.transform.parent==game.World,"normal fade survives canceled slot root");
     Geometry(v);
     var dischargeBuffer=Get(v,"detail");var dischargeType=dischargeBuffer.GetType();
     int dischargeCount=(int)dischargeType.GetField("count").GetValue(dischargeBuffer);
     Check(dischargeCount<=beforeCount,"natural expiry adds no discharge geometry");
     foreach(var blade in blades)Check(blade.transform.parent==v.transform,"harmless swords survive slot cleanup for dissolve");
     for(int i=0;i<4;i++)Tick(v);
     Geometry(v);
     Check(Get(v,"shell")!=null&&blades[0].color.a>0&&blades[0].color.a<.96f,"shell and unused swords share the brief exit flash");
     for(int i=0;i<10;i++)Tick(v);
    }
    if(end==1){v.Collapse();v.Collapse();Check(KarmaSW05Energy.bursts==bursts+4,"parry adds shards without circular burst");Check(!((MeshRenderer)Get(v,"front")).enabled&&!((MeshRenderer)Get(v,"back")).enabled,"parry removes shield synchronously");v.gameObject.SetActive(false);
     KarmaSwordWardVfx shards=null;int tails=0;foreach(var child in game.World.children){var w=child.GetComponent<KarmaSwordWardVfx>();if(w!=null&&(bool)Get(w,"dispersing")){shards=w;tails++;}}
     Check(tails==1,"single parry shatter even if collapse repeats");
     var panels=Get(shards,"shell");var panelType=panels.GetType();
     Check((int)panelType.GetField("count").GetValue(panels)==32*7,"32 chipped seven-sided fragments triangulated at break");
     var shapes=(Vector2[])Get(shards,"fracturePoints");
     Check(shapes.Length==224,"each fragment owns its polygon");
     var beforeShape=shapes[0];
     for(int j=0;j<32;j++){
      float minSize=float.MaxValue,maxSize=0;
      for(int k=0;k<7;k++){var point=shapes[j*7+k];float size=point.x*point.x+point.y*point.y;minSize=Mathf.Min(minSize,size);maxSize=Mathf.Max(maxSize,size);}
      Check(maxSize/minSize>2,"chipped contours have unequal radii rather than rectangular panels");
     }
     typeof(KarmaSwordWardVfx).GetMethod("BuildFracture",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(v,null);
     var nextShape=((Vector2[])Get(v,"fracturePoints"))[0];
     Check(beforeShape.x!=nextShape.x||beforeShape.y!=nextShape.y,"next break generates fresh polygon shapes");
     Geometry(shards);owner.Stunned=true;
     for(int j=0;j<5;j++){Tick(shards);Geometry(shards);}
     Check(Get(shards,"detail")!=null,"electric shards bridge into existing boss stun");
     float shatterAge=(float)Get(shards,"age");game.Paused=true;Tick(shards);
     Check((float)Get(shards,"age")==shatterAge,"pause freezes shatter flight");game.Paused=false;
     for(int j=0;j<25;j++){Tick(shards);Geometry(shards);}
     Check(Get(shards,"detail")!=null,"panels remain readable beyond the old 0.28 second cutoff");
     var panelColors=(Color[])panelType.GetField("colors").GetValue(panels);
     float remainingAlpha=0;for(int k=0;k<(int)panelType.GetField("count").GetValue(panels)*4;k++)remainingAlpha=Mathf.Max(remainingAlpha,panelColors[k].a);
     Check(remainingAlpha>0&&remainingAlpha<.30f,"remaining panels fade while lower fragments settle below the floor");
     for(int j=0;j<26;j++){Tick(shards);Geometry(shards);}
     for(int j=0;j<5;j++)Tick(shards);
     Check(Get(shards,"detail")==null,"parry shards expire normally");
    }
    if(end==2)root.gameObject.SetActive(false);
    if(end==3){owner.PresentationActive=false;game.Paused=true;Tick(v);}
    if(end==4){owner.Stunned=true;Tick(v);}
    if(end==5){game.Phase=RunPhase.End;Tick(v);}
    Check(Get(v,"shell")==null&&Get(v,"detail")==null,"buffers returned on end, parry, interrupt, death, stun and phase exit");
    Check(((MeshFilter)Get(v,"frontFilter")).sharedMesh==null,"no dangling pooled mesh");cases++;
   }
   Check(Mesh.allocated==4,"four meshes reused for shield and parry tail across every cast");
   System.Console.WriteLine("PASS "+cases+" production ward lifecycle cases; peak "+peak+"/2048 quads; four pooled shield/shatter meshes");
  }
 }
}
'''
        self.assertIn("PASS 48 production ward lifecycle cases", run_cs(source + mocks, "KarmaPrototype.WardProbe", modern=True))


if __name__ == "__main__":
    unittest.main()
