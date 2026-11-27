#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

[InitializeOnLoad]
public static class KarmaGuardHeavyGlowChecks
{
    const string Key = "K4RMA.Guard.HeavyGlow";
    const string Output = "output/validation/guard-heavy-glow";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static bool started, recording;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static int checks, frames;
    static string runtimeError;
    static Camera camera;
    static RenderTexture target;
    static Texture2D pixels;
    static KarmaGuardHeavyGlowChecks() { EditorApplication.update += Pump; }
    [MenuItem("K4RMA/Guard/Check Heavy Glow %#F10")]
    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true; EditorApplication.isPaused = false;
        if (!started && Time.frameCount > 2) { started = true; Run(); }
    }
    static FieldInfo Field(object o, string n)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(n, Flags); if (f != null) return f; }
        throw new Exception(n);
    }
    static T Get<T>(object o, string n) => (T)Field(o,n).GetValue(o);
    static void Set(object o,string n,object value) => Field(o,n).SetValue(o,value);
    static object Call(object o,string n,params object[] args) => o.GetType().GetMethod(n,Flags).Invoke(o,args);
    static void Check(bool ok,string message) { checks++; if (!ok) throw new Exception(message); }
    static async Task Until(Func<bool> ready,float timeout=8)
    {
        double end=EditorApplication.timeSinceStartup+timeout;
        while(!ready()) { if(EditorApplication.timeSinceStartup>end)throw new Exception("Timeout"); await Task.Delay(1); }
    }
    static Task Wait(float time) { float end=Time.time+time; return Until(()=>Time.time>=end); }
    static void Reset(int facing, params Essence[] order)
    {
        game.StartRun();
        foreach(var e in order){game.Player.Essences.Preview(e);game.Player.Essences.Confirm(out _);}
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game,order.Length);
        Call(game,"LoadRoom",true); enemy=game.Enemies[0];
        enemy.transform.position=new Vector3(30,KarmaSpace.GuardianGroundY,0);
        Set(enemy,"engaged",true); Set(enemy,"timer",float.MaxValue);Set(enemy,"nextShield",float.MaxValue);
        Set(Get<object>(enemy,"behavior"),"PracticeHold",true);
        var body=game.Player.GetComponent<Rigidbody2D>(); body.bodyType=RigidbodyType2D.Kinematic;
        body.position=new Vector2(30+facing*4.3f,KarmaSpace.PlayerHalfHeight);
        game.Player.transform.position=body.position;
        typeof(KarmaPlayer).GetProperty("Facing").SetValue(game.Player,-facing);
        Call(game.Player,"FaceAction"); Set(game.Player,"invulnerableUntil",0f);
    }
    static void Force(int facing)
    {
        Call(enemy,"InterruptPattern"); Set(enemy,"selected",EnemyAttack.GD02);Set(enemy,"direction",(float)facing);
        Get<Transform>(enemy,"art").localScale=new Vector3(facing*KarmaSpace.GuardianArtScale,KarmaSpace.GuardianArtScale,1);
        Set(enemy,"running",true);Set(enemy,"patternStarted",enemy.FightSeconds);
        enemy.StartCoroutine((IEnumerator)Call(enemy,"RunPattern"));
    }
    static void Capture(string name,Camera view=null)
    {
        view=view??camera;
        var old=view.targetTexture;var active=RenderTexture.active;
        try {
            view.targetTexture=target;view.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
            File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());
        } finally {view.targetTexture=old;RenderTexture.active=active;}
    }
    static IEnumerator Record()
    {
        float next=Time.time;
        while(recording)
        {
            yield return new WaitForEndOfFrame();
            if(!recording||Time.time<next)continue;
            next=Time.time+1f/24;
            Capture("frames/"+(frames++).ToString("D4"));
        }
    }
    static void Log(string msg,string stack,LogType type)
    { if(type==LogType.Error||type==LogType.Exception)runtimeError=msg; }
    static void Finish(bool passed = true)
    {
        recording=false;Time.captureDeltaTime=0;
        Application.logMessageReceived-=Log; SessionState.EraseBool(Key);
        if(Application.isBatchMode)EditorApplication.Exit(passed ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
    static async void Run()
    {
        Directory.CreateDirectory(Output+"/frames");Application.logMessageReceived+=Log;
        try
        {
            game=UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            Time.captureDeltaTime=1f/120;
            target=new RenderTexture(1280,720,24);pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            camera=new GameObject("Heavy glow closeup camera").AddComponent<Camera>();
            camera.CopyFrom(game.GameCamera);camera.enabled=false;camera.orthographic=true;
            camera.orthographicSize=8;camera.aspect=1280f/720;
            camera.transform.position=new Vector3(30,5.5f,-10);
            Check(Mathf.Approximately(game.Config.BossTuning.guard.heavyParryCueSeconds,0.3f),"saved parry cue = 0.3 seconds");
            Check(!ShaderUtil.ShaderHasError(KarmaArtResource.Load<Shader>("Guard/Effects/KarmaGuardHeavyGlow")),"glow shader compiled");
            var orders=new[]{new[]{Essence.Guard},new[]{Essence.Guard,Essence.SwordWave},
                new[]{Essence.Guard,Essence.RisingSlash},new[]{Essence.Guard,Essence.SwordWave,Essence.RisingSlash}};
            foreach(var order in orders)
            foreach(int facing in new[]{-1,1})
            {
                Reset(facing,order);await Wait(0.1f);
                const string cueDrawing = "GD02_02";
                float healthBeforeStrike = game.Player.Health;
                var glow=enemy.GetComponentInChildren<KarmaGuardHeavyGlow>();
                var anim=Get<KarmaCharacterAnimator>(enemy,"animator");
                var picture = Get<SpriteRenderer>(anim, "picture");
                bool movie=order.Length==1&&facing==-1;
                if(movie){recording=true;game.StartCoroutine(Record());await Wait(0.3f);}
                float duration=(float)Call(enemy,"Warning",0.9f),begin=enemy.FightSeconds;
                Force(facing);
                Check(!enemy.TryQueueParry(),"charge cannot queue early parry");
                await Wait((duration-0.3f)*0.5f);
                Check(glow.Visible&&glow.Charge>0.05f&&glow.Charge<0.75f&&glow.Pulse==0,"progressive charge before cue");
                Check(picture.sprite.name == cueDrawing, "holds the raised drawing before the cue");
                if(movie)Capture("charge");
                await Until(()=>enemy.HeavyParryWindowOpen);
                float cue=enemy.FightSeconds;
                Check(glow.Pulse>0.6f,"flash coincides with open parry window");
                Check(Get<SpriteRenderer>(glow, "overlay").sprite == picture.sprite, "cue flash uses the raised sword drawing");
                if(movie)Capture("cue");
                await Until(() =>
                {
                    if (!enemy.HeavyParryWindowOpen) return true;
                    if (picture.sprite.name != cueDrawing || game.Player.Health != healthBeforeStrike)
                        throw new Exception("GD02 must hold its sword overhead throughout the cue: " + picture.sprite.name);
                    return false;
                });
                await Until(()=>Get<object>(enemy,"state").ToString()=="Attack");
                float hit=enemy.FightSeconds;
                Check(Mathf.Abs(hit-cue-0.3f)<0.035f,"parry cue lasts 0.3 seconds: "+(hit-cue));
                Check(Mathf.Abs(hit-begin-duration)<0.045f,"total windup unchanged: "+(hit-begin));
                Check(!enemy.HeavyParryWindowOpen&&glow.Pulse>0.6f,"downstroke has impact burst and closes queue");
                Check(picture.sprite.name == "GD02_04", "downstroke drawing on first active frame");
                Check(Get<SpriteRenderer>(glow, "overlay").sprite == picture.sprite, "impact flash uses the downstroke drawing");
                Check(game.Player.Health < healthBeforeStrike, "unparried GD02 hits on the downstroke");
                if(movie){Capture("impact");Capture("game-camera",game.GameCamera);}
                await Wait(1.5f * KarmaBootstrap.Step);
                Check(picture.sprite.name == "GD02_05", "follows through with the blade down");
                await Until(()=>Get<object>(enemy,"state").ToString()=="Recover");
                Check(!glow.Visible,"recovery cleans glow");
                if(movie){await Wait(0.3f);recording=false;}

                Reset(facing,order);await Wait(0.1f);
                glow=enemy.GetComponentInChildren<KarmaGuardHeavyGlow>();
                if(movie){recording=true;game.StartCoroutine(Record());await Wait(0.25f);}
                float hp=game.Player.Health;int parries=game.Player.ParryCount;
                Force(facing);await Until(()=>enemy.HeavyParryWindowOpen);
                Check(enemy.TryQueueParry(),"cue input accepted");
                await Until(()=>game.Player.ParryCount>parries); await Until(()=>enemy.Stunned);
                Check(game.Player.Health==hp&&enemy.Stunned,"cue input parries real hit without damage");
                Check(!glow.Visible,"parry interruption cleans sword glow");
                if(movie){await Wait(0.9f);recording=false;}
            }
            // The same strike is used by the shieldless GD05 trial.
            foreach(int facing in new[]{-1,1})
            {
                Reset(facing,Essence.Guard,Essence.SwordWave,Essence.RisingSlash);await Wait(0.1f);
                Call(enemy,"InterruptPattern");Set(enemy,"selected",EnemyAttack.GD05);Set(enemy,"running",true);
                var anim=Get<KarmaCharacterAnimator>(enemy,"animator");
                var shield=new GameObject("Trial shield test").AddComponent<SpriteRenderer>();anim.SetGuardTrialShield(shield);
                var heavy=Get<object>(Get<object>(enemy,"behavior"),"Heavy");
                enemy.StartCoroutine((IEnumerator)Call(heavy,"Strike",true,false));
                await Until(()=>enemy.HeavyParryWindowOpen);
                var glow=enemy.GetComponentInChildren<KarmaGuardHeavyGlow>();
                Check(glow.Visible&&glow.Pulse>0.6f,"shieldless GD05 cue visible");
                float pulse=glow.Pulse;game.TogglePause();await Task.Delay(100);
                Check(Mathf.Approximately(glow.Pulse,pulse),"pause freezes glow");game.TogglePause();
                Call(enemy,"InterruptPattern");await Wait(0.03f);
                Check(!glow.Visible,"GD05 interruption clears glow");
                UnityEngine.Object.Destroy(shield.gameObject);
            }
            Reset(1,Essence.Guard);await Wait(0.1f);Force(1);await Until(()=>enemy.HeavyParryWindowOpen);
            var lastGlow=enemy.GetComponentInChildren<KarmaGuardHeavyGlow>();enemy.TakeDamage(float.MaxValue,enemy.Position+Vector2.left);
            await Wait(0.03f);Check(!lastGlow.Visible,"death clears glow");
            Check(runtimeError==null,"no runtime errors: "+runtimeError);
            File.WriteAllText(Output+"/result.txt","PASS "+checks+" checks; captured frames="+frames);
            Finish();
        }
        catch(Exception e)
        {
            File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);Finish(false);
        }
    }
}
#endif
