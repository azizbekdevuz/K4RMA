#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

// Only edited skill presentation: one cast per skill, first SW03 round and
// first SW05 rain group. No AI, movement campaign or full boss regressions.
[InitializeOnLoad]
public static class KarmaSwordSkillVfxChecks
{
    const string Folder = "output/validation/sword-skill-vfx-20261105";
    const string Key = "K4RMA.SkillVfx";
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static KarmaGame game; static bool started, baseline, focused, sw01Only; static string failure;
    static readonly List<string> results = new List<string>();
    static Type Fixture => typeof(KarmaSwordPlayChecks);
    static KarmaEnemy Enemy => (KarmaEnemy)Fixture.GetField("enemy", F).GetValue(null);
    static object Get(object target, string name)
    { for (Type t = target.GetType(); t != null; t = t.BaseType) { var field=t.GetField(name,F); if(field!=null)return field.GetValue(target); } throw new MissingFieldException(name); }
    static void Set(object target, string name, object value)
    { for(Type t=target.GetType();t!=null;t=t.BaseType) {var f=t.GetField(name,F);if(f!=null){f.SetValue(target,value);return;}} throw new MissingFieldException(name); }
    static Transform Root => (Transform)Get(Enemy,"effectsRoot");
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static async Task Until(Func<bool> done)
    { double limit=EditorApplication.timeSinceStartup+90;while(!done()){Check(failure==null,failure);Check(EditorApplication.timeSinceStartup<limit,"skill render timeout");await Task.Delay(5);} }
    [Serializable] public sealed class Frame {public int tick;public string state;public Vector3 position,center,size;}
    [Serializable] public sealed class Shot {public int tick;public string kind;public Vector3 position;public Vector2 velocity,size;public float damage,life;}
    [Serializable] public sealed class Cast {public string key;public int endTick;public List<Shot> shots=new List<Shot>();public List<Frame> frames=new List<Frame>();}
    [Serializable] public sealed class Data {public List<Cast> casts=new List<Cast>();}
    static KarmaSwordSkillVfxChecks(){EditorApplication.update+=Pump;}
    static void Pump(){if(!SessionState.GetBool(Key,false))return;EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.isPlaying){Application.runInBackground=true;EditorApplication.isPaused=false;}if(!started&&EditorApplication.isPlaying&&Time.frameCount>2){started=true;Run();}}
    public static void RunBaseline()=>Begin(true);
    public static void RunUpdated()=>Begin(false);
    public static void RunSW04Baseline()=>Begin(true,true);
    public static void RunRefinedUpdated()=>Begin(false,true);
    public static void RunSW01Baseline()=>Begin(true,true,true);
    public static void RunSW01Updated()=>Begin(false,true,true);
    static void Begin(bool before,bool selected=false,bool first=false)
    {SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Baseline",before);SessionState.SetBool(Key+".Focused",selected);SessionState.SetBool(Key+".SW01Only",first);EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");EditorApplication.EnterPlaymode();}
    static void Capture(string name)
    {
        var camera=game.GameCamera;var rt=new RenderTexture(1600,700,24);var image=new Texture2D(1600,700,TextureFormat.RGBA32,false);
        var old=camera.targetTexture;var active=RenderTexture.active;var rect=camera.rect;float aspect=camera.aspect;
        try{camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);camera.pixelRect=new Rect(0,0,1600,700);camera.aspect=1600f/700;
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,700),0,0);image.Apply();File.WriteAllBytes(Folder+"/"+(baseline?"before":"after")+"/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=old;camera.rect=rect;camera.aspect=aspect;RenderTexture.active=active;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(rt);}
    }
    static async Task<Cast> Observe(EnemyAttack pattern,bool melee=false)
    {
        Fixture.GetMethod("Reset",F).Invoke(null,new object[]{Essence.SwordWave});
        Fixture.GetMethod("Place",F).Invoke(null,new object[]{20f+(melee?5:pattern==EnemyAttack.SWBasic?9:15)});
        await Task.Delay(60);Set(Enemy,"running",true);Set(Enemy,"timer",float.MaxValue);
        var data=new Cast{key=pattern+(melee?"-melee":"")};int tick=0,first=-1;bool prepared=false,formed=false,triggered=false,forceBegun=pattern!=EnemyAttack.SW01;
        var seen=new HashSet<UnityEngine.Object>();var probe=new GameObject("focused skill VFX observation").AddComponent<KarmaSwordDashFrameProbe>();
        probe.After=()=>
        {
            if(!forceBegun)return;
            try
            {
                var b=Enemy.DamageBounds;data.frames.Add(new Frame{tick=tick,state=Get(Enemy,"state").ToString(),position=Enemy.Position,center=b.center,size=b.size});
                foreach(var shot in Root.GetComponentsInChildren<KarmaBossAttack>())if(seen.Add(shot))
                {
                    data.shots.Add(new Shot{tick=tick,kind="projectile",position=shot.transform.position,velocity=(Vector2)Get(shot,"velocity"),size=(Vector2)Get(shot,"size"),damage=(float)Get(shot,"damage"),life=(float)Get(shot,"remaining")});
                    Check(shot.Visual.sprite!=null,"loaded skill sprite");
                    if(first<0){first=tick;Capture(data.key+"-release");}
                }
                foreach(var blade in Root.GetComponentsInChildren<KarmaSW05RainBlade>())if(seen.Add(blade))
                {
                    var shot=(KarmaAerialSwordPlanner.Shot)Get(blade,"shot");
                    data.shots.Add(new Shot{tick=tick,kind="rain",position=blade.transform.position,velocity=shot.velocity,size=shot.size,damage=(float)Get(blade,"damage"),life=shot.life});
                    Check(blade.Visual.sprite!=null,"loaded SW05 sword");if(first<0){first=tick;Capture(data.key+"-release");}
                }
                if(!prepared&&tick>=20){prepared=true;Capture(data.key+"-prepare");}
                var blades=Root.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.name.Contains("회전 검막")||s.name=="Forming aerial sword / no damage").ToArray();
                if(!formed&&blades.Length>=4&&tick>=50){formed=true;Capture(data.key+"-formation");}
                if(pattern==EnemyAttack.SW04&&blades.Length==4&&!triggered){triggered=true;Enemy.TakeDamage(1);}
                if(first>=0&&(tick==first+3||tick==first+8||tick==first+14))Capture(data.key+"-motion-"+(tick-first));
                bool end=pattern==EnemyAttack.SW03?first>=0&&tick>=first+14:pattern==EnemyAttack.SW05?first>=0&&tick>=first+24:pattern==EnemyAttack.SW04?data.shots.Count==4&&tick>=data.shots[3].tick+8:!(bool)Get(Enemy,"running");
                if(end){data.endTick=tick;probe.After=null;}
                else if(!(bool)Get(Enemy,"running"))Set(Enemy,"running",true);
                tick++;
            }catch(Exception e){failure=e.ToString();probe.After=null;}
        };
        if(pattern==EnemyAttack.SW04)typeof(KarmaEnemy).GetProperty("FightSeconds").SetValue(Enemy,0f);
        UnityEngine.Random.InitState(73105+(int)pattern);
        if(pattern==EnemyAttack.SW01)
            // Put both inputs before the same Update, rather than sampling an
            // old spawn Move/Idle pose when Task.Delay resumes mid player loop.
            probe.Before=()=>{typeof(KarmaEnemy).GetProperty("FightSeconds").SetValue(Enemy,0f);Fixture.GetMethod("Force",F).Invoke(null,new object[]{pattern});forceBegun=true;probe.Before=null;};
        else Fixture.GetMethod("Force",F).Invoke(null,new object[]{pattern});
        await Until(()=>probe.After==null);UnityEngine.Object.Destroy(probe.gameObject);Check(failure==null,failure);
        int expected=pattern==EnemyAttack.SW01?2:pattern==EnemyAttack.SW03?20:pattern==EnemyAttack.SW04?4:pattern==EnemyAttack.SW05?data.shots.Count:1;
        Check(data.shots.Count==expected&&expected>0,"shot count "+data.key+"="+data.shots.Count);
        if(pattern==EnemyAttack.SW05)Check(expected>=16&&expected<=18,"first dense rain group");
        if(pattern==EnemyAttack.SW04){KarmaSwordAttackPolish.ParrySuccess(game,Enemy,game.Player.Position);await Task.Delay(25);Capture("SW04-parry-success");}
        Enemy.GetType().GetMethod("InterruptPattern",F).Invoke(Enemy,null);Set(Enemy,"running",true);
        results.Add("PASS: "+data.key+" shots="+data.shots.Count+", first release="+data.shots[0].tick+", observed="+data.frames.Count+" frames");return data;
    }
    static void Compare(Cast a,Cast b)
    {
        AlignAttackStart(a); AlignAttackStart(b);
        Check(a.key==b.key&&a.endTick==b.endTick&&a.shots.Count==b.shots.Count&&a.frames.Count==b.frames.Count,"unchanged timing/count "+b.key);
        for(int i=0;i<a.shots.Count;i++){var x=a.shots[i];var y=b.shots[i];Check(x.tick==y.tick&&Vector3.Distance(x.position,y.position)<0.0001f&&x.velocity==y.velocity&&x.size==y.size&&Mathf.Approximately(x.damage,y.damage)&&Mathf.Approximately(x.life,y.life),"unchanged projectile "+b.key+"/"+i);}
        for(int i=0;i<a.frames.Count;i++){var x=a.frames[i];var y=b.frames[i];Check(x.state==y.state&&Vector3.Distance(x.position,y.position)<0.0001f&&Vector3.Distance(x.center,y.center)<0.0001f&&Vector3.Distance(x.size,y.size)<0.0001f,"unchanged physical frame "+b.key+"/"+i);}
    }
    static void AlignAttackStart(Cast cast)
    {
        // Some editor player-loop entries include one pre-trigger Approach
        // sample. Keep the raw capture, but compare attacks from Telegraph t=0.
        // No actual attack frame, geometry value or tolerance is discarded.
        int offset=0;while(offset<cast.frames.Count&&cast.frames[offset].state=="Approach")offset++;
        if(offset==0)return;
        cast.frames.RemoveRange(0,offset);foreach(var frame in cast.frames)frame.tick-=offset;
        foreach(var shot in cast.shots)shot.tick-=offset;cast.endTick-=offset;
    }
    public static void VerifySaved()
    {
        try
        {
            var a=JsonUtility.FromJson<Data>(File.ReadAllText(Folder+"/baseline.json"));
            var b=JsonUtility.FromJson<Data>(File.ReadAllText(Folder+"/updated.json"));
            var lines=new List<string>{"PASS: exact attack-phase geometry/timing comparison for seven edited variants; shader/Sprite loading verified"};
            for(int i=0;i<a.casts.Count;i++)
            {Compare(a.casts[i],b.casts[i]);lines.Add("PASS: "+b.casts[i].key+" shots="+b.casts[i].shots.Count+", first release="+b.casts[i].shots[0].tick+", physical samples="+b.casts[i].frames.Count);}
            foreach(string name in new[]{"KarmaSwordSkillPolish","KarmaSwordRisePolish","SW05/SW05Celestial"})
            {var material=Resources.Load<Material>(name);Check(material!=null&&!ShaderUtil.ShaderHasError(material.shader),name+" loads/compiles");}
            foreach(string pose in new[]{"Sword_Crescent_v6","SW03_GroundScar_v6","SW03_RisingBlade_v6","SW04_CounterBlade_v6","SW05_JudgementSword","Sword_Release_v6","Sword_Impact_v6"})
                Check(KarmaWaveArtwork.Sprite(pose)!=null,pose+" loads");
            File.WriteAllText(Folder+"/comparison-baseline.json",JsonUtility.ToJson(a,true));
            File.WriteAllText(Folder+"/comparison-updated.json",JsonUtility.ToJson(b,true));
            File.WriteAllLines(Folder+"/result.txt",lines);EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(Folder+"/result.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static async void Run()
    {
        int exit=0;baseline=SessionState.GetBool(Key+".Baseline",false);focused=SessionState.GetBool(Key+".Focused",false);sw01Only=SessionState.GetBool(Key+".SW01Only",false);Directory.CreateDirectory(Folder+"/"+(baseline?"before":"after"));var data=new Data();
        try
        {
            game=UnityEngine.Object.FindAnyObjectByType<KarmaGame>();Fixture.GetField("game",F).SetValue(null,game);Application.targetFrameRate=120;Time.captureDeltaTime=1f/60;game.GameCamera.aspect=1600f/700;
            var config=UnityEngine.Object.Instantiate(game.Config);Set(game,"config",config);config.playerHealth=100000;config.BossTuning.sword.aerialSwordSeed=5005;
            if(focused)
            {
                data=JsonUtility.FromJson<Data>(File.ReadAllText(Folder+"/"+(baseline?"baseline":"updated")+".json"));
                foreach(var p in sw01Only?new[]{EnemyAttack.SW01}:baseline?new[]{EnemyAttack.SW04}:new[]{EnemyAttack.SW01,EnemyAttack.SW02,EnemyAttack.SW04})
                {var cast=await Observe(p);data.casts[data.casts.FindIndex(c=>c.key==cast.key)]=cast;}
                results.Add("PASS: focused records updated; other completed casts retained without rerunning");
            }
            else
            {
                foreach(var p in new[]{EnemyAttack.SWBasic,EnemyAttack.SW01,EnemyAttack.SW02,EnemyAttack.SW03,EnemyAttack.SW04,EnemyAttack.SW05})data.casts.Add(await Observe(p));
                data.casts.Add(await Observe(EnemyAttack.SWBasic,true));
            }
            if(!baseline){var old=JsonUtility.FromJson<Data>(File.ReadAllText(Folder+"/baseline.json"));for(int i=0;i<data.casts.Count;i++)Compare(old.casts[i],data.casts[i]);
                var material=Resources.Load<Material>("KarmaSwordSkillPolish");Check(material!=null&&!ShaderUtil.ShaderHasError(material.shader),"skill material compiles and loads");
                foreach(string name in new[]{"KarmaSwordRisePolish","SW05/SW05Celestial"}){material=Resources.Load<Material>(name);Check(material!=null&&!ShaderUtil.ShaderHasError(material.shader),name+" compiles");}}
            results.Insert(0,"PASS: "+(baseline?"baseline":"updated geometry/timing comparison")+"; edited skills only; first SW03 round / first SW05 rain group");
        }catch(Exception e){results.Insert(0,"FAIL: "+e);Debug.LogException(e);exit=1;}
        File.WriteAllText(Folder+"/"+(baseline?"baseline":"updated")+".json",JsonUtility.ToJson(data,true));File.WriteAllLines(Folder+"/"+(baseline?"baseline-result":"result")+".txt",results);Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(exit);
    }
}
#endif
