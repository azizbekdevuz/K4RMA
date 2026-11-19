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
public static class KarmaGuardParryFeedbackChecks
{
    const string Key = "K4RMA.Guard.ParryFeedback";
    const string Output = "output/validation/guard-parry-feedback";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static bool started, recording;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static int checks, frames;
    static string runtimeError;
    static Camera camera;
    static RenderTexture target;
    static Texture2D pixels;
    static KarmaGuardParryFeedbackChecks() { EditorApplication.update += Pump; }
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
        try
        {
            game=UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            // Offline capture cadence only; the game still executes every real combat frame.
            Time.captureDeltaTime=1f/48;
            target=new RenderTexture(1280,720,24);pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            camera=new GameObject("Parry feedback closeup camera").AddComponent<Camera>();
            camera.CopyFrom(game.GameCamera);camera.enabled=false;camera.orthographic=true;
            camera.orthographicSize=8;camera.aspect=1280f/720;
            camera.transform.position=new Vector3(30,5.5f,-10);
            int startFrame=Time.frameCount;
            var orders=new[]{new[]{Essence.Guard},new[]{Essence.Guard,Essence.SwordWave},
                new[]{Essence.Guard,Essence.RisingSlash},new[]{Essence.Guard,Essence.SwordWave,Essence.RisingSlash}};
            foreach(var order in orders)
            foreach(int facing in new[]{-1,1})
            {
                Reset(facing,order);await Wait(0.15f);
                bool movie=order.Length==1&&facing==-1;
                if(movie){Capture("01-shield-intact");recording=true;game.StartCoroutine(Record());await Wait(0.4f);}
                float hp=game.Player.Health; var bounds=game.Player.DamageBounds;
                var fx=enemy.GetComponent<KarmaGuardBarrierBreak>();
                Force(facing);
                await Until(()=>enemy.TryQueueParry());
                int count=game.Player.ParryCount;
                await Until(()=>fx.Playing);
                Check(game.Player.ParryCount==count+1,"actual GD02 parry resolved once");
                Check(game.Player.Health==hp,"successful parry takes no damage");
                Check(!Get<bool>(enemy,"pendingParry")&&enemy.Stunned,"existing stun entered");
                Check(Get<float>(enemy,"shieldUntil")==0,"shield protection ends immediately");
                Check(fx.PlayCount==1,"one barrier break per success");
                Check(!Get<SpriteRenderer>(enemy,"shieldHalo").enabled,"old shield renderer hidden");
                var flash=game.Player.GetComponentInChildren<KarmaPlayerParryFlash>();
                Check(flash!=null&&flash.Visible,"successful player parry flash visible");
                Check(!ShaderUtil.ShaderHasError(Resources.Load<Shader>("Player/ParryFlash")),"flash shader compiled");
                Check(game.Player.DamageBounds.size==bounds.size,"player collision size unchanged");
                if(movie)
                {
                    Capture("02-parry-cracks");Capture("game-camera-parry",game.GameCamera);
                    await Wait(0.08f);Capture("03-flash-and-break");
                    await Wait(0.12f);Capture("04-fragments");
                }
                else if(order.Length==1)
                {
                    float age=fx.Age;game.TogglePause();await Task.Delay(120);
                    Check(Mathf.Approximately(fx.Age,age),"pause freezes fragment motion");game.TogglePause();
                    Capture("mirrored-parry");
                }
                await Wait(0.8f);
                Check(!fx.Playing&&!flash.Visible,"both effects finish without residue");
                if(movie){Capture("05-cleared");await Wait(0.3f);recording=false;}
            }
            Reset(1,Essence.Guard);await Wait(0.1f);
            var barrier=enemy.GetComponent<KarmaGuardBarrierBreak>();
            Set(enemy,"shieldUntil",0f);await Wait(0.05f);
            Force(1);await Until(()=>enemy.TryQueueParry());int parries=game.Player.ParryCount;
            await Until(()=>game.Player.ParryCount>parries);await Wait(0.05f);
            Check(barrier.PlayCount==0,"no shatter when GD01 was absent");
            var playerAnim=Get<KarmaCharacterAnimator>(game.Player,"animator");
            playerAnim.Hurt();await Wait(0.03f);
            Check(!game.Player.GetComponentInChildren<KarmaPlayerParryFlash>(true).Visible,"hurt interrupts flash");
            // Normal guarding and a missed counter must not show the success effect.
            game.Player.BeginGuard(0.3f);game.Player.TakeDamage(3f);await Wait(0.03f);
            Check(!game.Player.GetComponentInChildren<KarmaPlayerParryFlash>(true).Visible,"ordinary guard does not flash");
            game.Player.BeginCounter();await Wait(0.4f);
            Check(!game.Player.GetComponentInChildren<KarmaPlayerParryFlash>(true).Visible,"missed counter does not flash");
            Reset(1,Essence.Guard);await Wait(0.1f);
            barrier=enemy.GetComponent<KarmaGuardBarrierBreak>();
            Force(1);await Until(()=>enemy.TryQueueParry());await Until(()=>barrier.Playing);
            enemy.TakeDamage(float.MaxValue,enemy.Position+Vector2.left);await Wait(0.04f);
            Check(!barrier.Playing,"death cancels fragments");
            game.StartRun();await Wait(0.06f);
            Check(UnityEngine.Object.FindObjectsByType<KarmaGuardBarrierBreak>(FindObjectsSortMode.None).Length==0,"room reset releases old fragments");
            Check(Time.frameCount>startFrame+30&&frames>24,"live advancing gameplay captured");
            Check(runtimeError==null,"no Unity errors: "+runtimeError);
            File.WriteAllText(Output+"/result.txt","PASS "+checks+" checks; captured frames="+frames);
            SessionState.EraseBool(Key);EditorApplication.Exit(0);
        }
        catch(Exception e)
        {
            recording=false;File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);
            SessionState.EraseBool(Key);EditorApplication.Exit(1);
        }
    }
}
#endif
