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
public static class KarmaGuardParryVisibilityChecks
{
    const string Key = "K4RMA.Guard.ParryVisibility";
    const string Output = "output/validation/guard-parry-clean";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static bool started, recording;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static int checks, frames;
    static string runtimeError;
    static Camera camera;
    static RenderTexture target;
    static Texture2D pixels;
    static KarmaGuardParryVisibilityChecks() { EditorApplication.update += Pump; }
    [MenuItem("K4RMA/Guard/Check Parry Visibility %#F9")]
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
    static async void Run()
    {
        Directory.CreateDirectory(Output+"/frames"); Application.logMessageReceived+=Log;
        try {
            game=UnityEngine.Object.FindAnyObjectByType<KarmaGame>();Time.captureDeltaTime=1f/120;
            target=new RenderTexture(1280,720,24);pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            camera=new GameObject("Parry visibility camera").AddComponent<Camera>();camera.CopyFrom(game.GameCamera);camera.enabled=false;
            camera.orthographicSize=8;camera.aspect=1280f/720;camera.transform.position=new Vector3(30,5.5f,-10);
            foreach(int facing in new[]{-1,1}) {
                Reset(facing,Essence.Guard);await Wait(0.15f);
                // Ordinary guarded hits retain their original pulse and damage reduction.
                float hpBefore=enemy.Health;
                enemy.TakeDamage(10);
                Check(Array.Exists(game.World.GetComponentsInChildren<KarmaCombatVFX>(),v=>v.name=="HitVFX/Ring"),"ordinary guarded hit retains ring");
                float shieldScale=game.Config.BossTuning.guard.shieldDamageMultiplier;
                Check(Mathf.Abs(hpBefore-enemy.Health-10*shieldScale)<0.01f,"ordinary guarded damage unchanged");
                await Wait(0.5f);
                recording=true;game.StartCoroutine(Record());await Wait(0.2f);
                float bossHp=enemy.Health,playerHp=game.Player.Health;
                Force(facing);await Until(()=>enemy.HeavyParryWindowOpen);game.Player.Essences.TryCounter();
                await Until(()=>game.Player.ParryCount>0);await Wait(0.025f);
                var fx=game.Player.GetComponentInChildren<KarmaPlayerParryFlash>();
                Check(fx!=null&&fx.Visible,"success effect enabled");
                Check(!Array.Exists(game.World.GetComponentsInChildren<KarmaCombatVFX>(),v=>v.name=="HitVFX/Ring"),"parry does not spawn lower-body ring");
                Check(!Array.Exists(game.World.GetComponentsInChildren<KarmaRemnantVFX>(),v=>Vector2.Distance(v.transform.position,enemy.Position)<0.2f),"parry does not spawn secondary block pulse");
                Check(enemy.GetComponent<KarmaGuardBarrierBreak>().Playing,"barrier shatter retained");
                Check(Get<float>(enemy,"shieldUntil")==0&&enemy.Stunned,"shield removal and stun retained");
                Check(game.Player.Health==playerHp,"parry still prevents player damage");
                Check(Mathf.Abs(bossHp-enemy.Health-game.Config.Techniques.counterDamage*shieldScale)<0.01f,"riposte damage unchanged");
                var glow=Get<SpriteRenderer>(fx,"glow");
                Capture("on-"+facing);var on=pixels.GetPixels32();
                glow.enabled=false;Capture("off-"+facing);var off=pixels.GetPixels32();glow.enabled=true;
                int changed=0;for(int i=0;i<on.Length;i++)if(Math.Abs(on[i].r-off[i].r)+Math.Abs(on[i].g-off[i].g)+Math.Abs(on[i].b-off[i].b)>30)changed++;
                Check(changed>15000,"success radiance occupies a readable screen area");
                Capture("game-camera-"+facing,game.GameCamera);
                File.AppendAllText(Output+"/diagnostic.txt","facing="+facing+" changedPixels="+changed+" bounds="+glow.bounds+" uv="+string.Join(",",glow.sprite.uv)+" order="+glow.sortingOrder+"\n");
                await Wait(0.4f);Check(!fx.Visible,"success effect expires at existing pose end");await Wait(0.5f);recording=false;
            }
            Check(runtimeError==null,"no runtime errors: "+runtimeError);
            File.WriteAllText(Output+"/result.txt","PASS "+checks+" frames="+frames);
        }catch(Exception e){File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);}
        recording=false;Time.captureDeltaTime=0;Application.logMessageReceived-=Log;SessionState.EraseBool(Key);EditorApplication.ExitPlaymode();
    }
}
#endif
