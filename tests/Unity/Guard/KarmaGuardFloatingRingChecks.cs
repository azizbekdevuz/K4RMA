#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using KarmaPrototype;

// A focused live-render audit; uses the same original-renderer bounds comparison as GuardAppearanceChecks.
[InitializeOnLoad]
public static class KarmaGuardFloatingRingChecks
{
    const string Key = "K4RMA.Guard.FloatingRingReview";
    const string Output = "output/validation/guard-floating-ring";
    static bool running, recording;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static int checks, frames;
    static string runtimeError;
    static RenderTexture target;
    static Texture2D pixels;
    static MediaEncoder encoder;
    static float recordAt;
    static Camera detail;
    static KarmaGuardFloatingRingChecks() { EditorApplication.update += Pump; }
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
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        if (Time.frameCount > 2 && !running) { running = true; Review(); }
    }
    static FieldInfo Field(object o, string name)
    {
        for (Type t=o.GetType(); t!=null; t=t.BaseType)
        { var f=t.GetField(name, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); if(f!=null)return f; }
        throw new Exception(name);
    }
    static T Get<T>(object o,string n) => (T)Field(o,n).GetValue(o);
    static void Set(object o,string n,object v) => Field(o,n).SetValue(o,v);
    static object Call(object o,string n,params object[] a) => o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,a);
    static void Check(bool value,string message) { checks++; if(!value)throw new Exception(message); }
    static async Task Until(Func<bool> condition,float seconds=5)
    {
        double deadline=EditorApplication.timeSinceStartup+seconds;
        while(!condition()) { if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout"); await Task.Delay(10); }
    }
    static Task Wait(float seconds) { float end=Time.time+seconds; return Until(()=>Time.time>=end); }
    static void Reset(params Essence[] order)
    {
        game.StartRun();
        foreach(var e in order) { game.Player.Essences.Preview(e); game.Player.Essences.Confirm(out _); }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game,order.Length);
        Call(game,"LoadRoom",true); enemy=game.Enemies[0];
        enemy.transform.position=new Vector3(20,KarmaSpace.GuardianGroundY,0);
        game.Player.GetComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Kinematic;
        game.Player.transform.position=new Vector3(34,12,0);
        game.Player.GetComponent<Rigidbody2D>().position=new Vector2(34,12);
        Set(game.Player,"invulnerableUntil",Time.time+100);
        Set(enemy,"engaged",true); Set(enemy,"timer",float.MaxValue); Set(enemy,"nextShield",float.MaxValue);
        Set(Get<object>(enemy,"behavior"),"PracticeHold",true);
    }
    static void Force(EnemyAttack attack,int facing)
    {
        Call(enemy,"InterruptPattern"); Set(enemy,"selected",attack); Set(enemy,"direction",(float)facing);
        Get<Transform>(enemy,"art").localScale=new Vector3(facing*KarmaSpace.GuardianArtScale,KarmaSpace.GuardianArtScale,1);
        Set(enemy,"running",true); Set(enemy,"patternStarted",enemy.FightSeconds);
        enemy.StartCoroutine((IEnumerator)Call(enemy,"RunPattern"));
    }
    static void Capture(Camera camera,string name=null)
    {
        var old=camera.targetTexture; var active=RenderTexture.active;
        try { camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
            if(name!=null)File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());
        } finally { camera.targetTexture=old; RenderTexture.active=active; }
    }
    static IEnumerator Record()
    {
        float next=Time.realtimeSinceStartup;
        while(recording)
        {
            yield return new WaitForEndOfFrame();
            if(!recording||Time.realtimeSinceStartup<next)continue;
            next=Time.realtimeSinceStartup+1f/24;
            Capture(game.GameCamera);
            encoder.AddFrame(pixels,new MediaTime((long)((Time.realtimeSinceStartup-recordAt)*1000000),1000000,1)); frames++;
        }
    }
    static void Log(string message,string stack,LogType type) { if(type==LogType.Error||type==LogType.Exception)runtimeError=message; }
    static async void Review()
    {
        Directory.CreateDirectory(Output);
        Application.logMessageReceived+=Log;
        try
        {
            game=UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            target=new RenderTexture(1280,720,24); pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            detail=new GameObject("Guard ring review camera").AddComponent<Camera>();
            detail.orthographic=true; detail.orthographicSize=9; detail.aspect=1280f/720;
            detail.clearFlags=CameraClearFlags.SolidColor; detail.backgroundColor=new Color(0.08f,0.09f,0.11f);
            detail.cullingMask=1<<31; detail.enabled=false;
            foreach(var order in new[] {new[]{Essence.Guard},new[]{Essence.Guard,Essence.RisingSlash},new[]{Essence.Guard,Essence.SwordWave,Essence.RisingSlash}})
            {
                Reset(order); await Wait(0.05f);
                Check(enemy.GetComponentInChildren<KarmaGuardFloatingRing>()==null,"other Guard forms unchanged");
            }
            Reset(Essence.Guard,Essence.SwordWave); await Wait(0.1f);
            var animator=Get<KarmaCharacterAnimator>(enemy,"animator"); var picture=Get<SpriteRenderer>(animator,"picture");
            var ring=picture.GetComponent<KarmaGuardFloatingRing>();
            Check(ring!=null && ring.MappedPoseCount==17,"visible WardWave shield faces and GD03 rear windup mapped");
            Check(!ShaderUtil.ShaderHasError(picture.sharedMaterial.shader),"shader compiles");
            encoder=new MediaEncoder(Output+"/WardWave-floating-ring.mp4",new VideoTrackAttributes{width=1280,height=720,frameRate=new MediaRational(24),includeAlpha=false});
            recording=true; recordAt=Time.realtimeSinceStartup; game.StartCoroutine(Record());
            float a=ring.Angle; await Wait(0.5f);
            Check(Mathf.DeltaAngle(a,ring.Angle)>4 && Mathf.DeltaAngle(a,ring.Angle)<15 && ring.Charge==0,"slow idle rotation without sword energy");
            int layer=picture.gameObject.layer; picture.gameObject.layer=31;
            detail.transform.position=new Vector3(enemy.Position.x,8,-10);
            var material=picture.sharedMaterial; picture.sharedMaterial=KarmaVisuals.DefaultMaterial;
            Capture(detail,"original-idle"); picture.sharedMaterial=material; Capture(detail,"floating-idle");
            // Inspect a complete revolution, including the previously missed lower-left strap seam.
            game.TogglePause();
            var renderRing=(Action)Delegate.CreateDelegate(typeof(Action),ring,ring.GetType().GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic));
            float savedAngle=ring.Angle;
            Vector3 closeup=picture.transform.TransformPoint(new Vector3(
                (282-picture.sprite.rect.x-picture.sprite.pivot.x)/picture.sprite.pixelsPerUnit,
                (1254-173-picture.sprite.rect.y-picture.sprite.pivot.y)/picture.sprite.pixelsPerUnit,0));
            detail.transform.position=new Vector3(closeup.x,closeup.y,-10); detail.orthographicSize=3.6f;
            foreach(int degrees in new[]{0,45,90,135,180,225,270,315})
            foreach(float energy in new[]{0f,1f})
            {
                Set(ring,"angle",(float)degrees); Set(ring,"charge",energy); renderRing();
                Capture(detail,"rim-"+degrees+"-"+(energy>0?"charged":"idle"));
            }
            Set(ring,"angle",savedAngle); Set(ring,"charge",0f); renderRing();
            detail.orthographicSize=9; detail.transform.position=new Vector3(enemy.Position.x,8,-10);
            game.TogglePause();
            picture.gameObject.layer=layer;
            foreach(int facing in new[]{1,-1})
            {
                // Exercise both the armored front view and the unarmored rear windup.
                if(facing<0) Set(enemy,"shieldUntil",0f);
                Force(EnemyAttack.GD03,facing); float start=enemy.FightSeconds;
                await Wait(0.35f);
                Check(ring.Charge>0.8f,"GD03 tell accelerates ring");
                Check(picture.sharedMaterial.shader.name=="K4RMA/Guard/FloatingRing","prepared shield has visible ring material");
                var bounds=enemy.DamageBounds; var sprite=picture.sprite; var scale=picture.transform.localScale;
                Check(bounds==picture.bounds,"tell retains original damage bounds");
                picture.gameObject.layer=31; Capture(detail,"prepared-"+facing); picture.gameObject.layer=layer;
                float pausedAngle=ring.Angle,pausedCharge=ring.Charge;
                game.TogglePause(); await Task.Delay(180);
                Check(ring.Angle==pausedAngle && ring.Charge==pausedCharge,"pause freezes ring"); game.TogglePause();
                await Until(()=>Get<SpriteRenderer>(animator,"guardThrownShield")!=null);
                Check(enemy.FightSeconds-start>0.6f && enemy.FightSeconds-start<1.05f,"original GD03 tell duration");
                await Wait(0.06f);
                Check(ring.Charge==0 && picture.sharedMaterial.shader.name!="K4RMA/Guard/FloatingRing","release clears hand ring and charge");
                var flight=Get<SpriteRenderer>(animator,"guardThrownShield");
                Check(flight.sharedMaterial.shader.name=="K4RMA/Guard/FloatingRing","thrown shield retains ring");
                Capture(game.GameCamera,"flight-"+facing);
                await Until(()=>!Get<bool>(enemy,"running"),8);
                await Wait(0.08f);
                Check(ring.Charge==0 && Get<SpriteRenderer>(animator,"guardThrownShield")==null,"catch restores idle ring");
            }
            // A real interruption cancels charge on the next presentation frame.
            Force(EnemyAttack.GD03,1); await Wait(0.2f); Call(enemy,"InterruptPattern"); await Wait(0.05f);
            Check(ring.Charge==0,"interrupt clears charge");
            // Original heavy poses and original sprite bounds remain untouched by the material.
            animator.PrepareHeavyStrike(); animator.GuardianPose("Telegraph",EnemyAttack.GD02,false,true);
            Check(Get<SpriteRenderer>(animator,"picture").sprite.name=="Windup","heavy sprite preserved");
            animator.EndHeavyStrike();
            // Inspect every mapped pose without letting the AI overwrite the requested frame.
            game.TogglePause();
            var update=(Action)Delegate.CreateDelegate(typeof(Action),ring,ring.GetType().GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic));
            Vector3 bodyScale=Get<Vector3>(animator,"pictureScale");
            foreach(string folder in new[]{"Forms","HeavyStrike","MoltenStrike","Stagger"})
            foreach(var pose in Resources.LoadAll<Sprite>("Guard/"+folder+"/WardWave"))
            {
                if(pose.name=="Reference")continue;
                picture.sprite=pose;
                picture.transform.localScale=folder=="HeavyStrike"||folder=="MoltenStrike"
                    ? bodyScale*(pose.pixelsPerUnit/(89.34426f*1.35f))
                    : folder=="Stagger" ? Get<Vector3>(animator,"staggerScale") : bodyScale;
                var before=picture.bounds; update();
                Check(picture.bounds==before && picture.sprite==pose,"sprite and bounds preserved: "+folder+"/"+pose.name);
                picture.gameObject.layer=31; Capture(detail,"pose-"+folder+"-"+pose.name); picture.gameObject.layer=layer;
            }
            picture.transform.localScale=bodyScale;
            picture.sprite=Array.Find(Resources.LoadAll<Sprite>("Guard/Forms/WardWave"),s=>s.name=="Idle");
            update();
            long allocations=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<120;i++)update();
            allocations=GC.GetAllocatedBytesForCurrentThread()-allocations;
            Check(allocations==0,"no managed allocations in 120 warmed presentation updates: "+allocations);
            game.TogglePause();
            // Exercise an actual GD03 parry/contact, not a forced presentation-only state.
            game.Player.transform.position=new Vector3(34,KarmaSpace.PlayerHalfHeight,0);
            game.Player.GetComponent<Rigidbody2D>().position=new Vector2(34,KarmaSpace.PlayerHalfHeight);
            Force(EnemyAttack.GD03,1);
            await Until(()=>Get<SpriteRenderer>(animator,"guardThrownShield")!=null);
            await Until(()=>Mathf.Abs(Get<SpriteRenderer>(animator,"guardThrownShield").transform.position.x-game.Player.Position.x)<4);
            Set(game.Player,"invulnerableUntil",0f); game.Player.BeginCounter();
            await Until(()=>Get<SpriteRenderer>(animator,"guardThrownShield")!=null && Get<SpriteRenderer>(animator,"guardThrownShield").name.EndsWith("반사"),1);
            await Until(()=>enemy.Stunned,3); await Wait(0.05f);
            Check(ring.Charge==0 && Get<SpriteRenderer>(animator,"guardThrownShield")==null,"actual parry returns shield and clears charge");
            Check(animator.CurrentPose.StartsWith("Stagger/"),"parry retains original stagger pose");
            Force(EnemyAttack.GD03,1); await Wait(0.2f);
            enemy.TakeDamage(float.MaxValue,enemy.Position+Vector2.left);
            await Wait(0.08f); float deadAngle=ring.Angle; await Wait(0.08f);
            Check(ring.Charge==0 && ring.Angle==deadAngle,"death stops rotation and clears charge");
            recording=false; encoder.Dispose(); encoder=null;
            Check(frames>24,"video contains advancing gameplay frames");
            Check(runtimeError==null,"no runtime errors: "+runtimeError);
            File.WriteAllText(Output+"/result.txt","PASS: "+checks+" floating ring checks; video frames="+frames+"\n");
            Debug.Log("PASS: "+checks+" floating ring checks");
            SessionState.EraseBool(Key); EditorApplication.Exit(0);
        }
        catch(Exception e)
        {
            recording=false; if(encoder!=null){encoder.Dispose();encoder=null;}
            File.WriteAllText(Output+"/failure.txt",e.ToString()); Debug.LogException(e);
            SessionState.EraseBool(Key); EditorApplication.Exit(1);
        }
    }
}
#endif
