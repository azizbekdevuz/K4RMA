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
public static class KarmaRisingAnimationChecks
{
    const string Key = "K4RMA.RisingAnimationChecks";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static bool started;
    static string error;
    static int assertions;
    static KarmaRisingAnimationChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || started || Time.frameCount < 3) return;
        started = true; Run();
    }
    public static void RunSafetyBatch()
    {
        SessionState.SetBool(Key + ".Safety", true);
        RunBatch();
    }
    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static async Task Wait(Func<bool> done)
    {
        double until = EditorApplication.timeSinceStartup + 45;
        while (!done())
        {
            if (EditorApplication.timeSinceStartup > until) throw new Exception("Animation check timeout");
            if (error != null) throw new Exception(error);
            await Task.Delay(10);
        }
    }
    static void Reset(Essence[] order)
    {
        game.StartRun();
        foreach (var kind in order) { game.Player.Essences.Preview(kind); game.Player.Essences.Confirm(out _); }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, order.Length);
        Call(game, "LoadRoom", true); enemy = game.Enemies[0];
        enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY);
        Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue);
        Set(Get(enemy, "behavior"), "PracticeHold", true);
        var body = game.Player.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
        body.position = game.Player.transform.position = new Vector2(26, KarmaSpace.PlayerHalfHeight + 0.02f);
        Set(game.Player, "invulnerableUntil", Time.time + 1000);
    }
    static void Force(EnemyAttack pattern)
    {
        Call(enemy, "InterruptPattern"); Set(enemy, "selected", pattern); Set(enemy, "direction", 1f);
        Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static void Capture(KarmaCharacterAnimator animator, string path)
    {
        var cameraObject = new GameObject("Animation validation camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 10.5f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.14f,.15f,.18f);
        camera.transform.position = animator.transform.position + new Vector3(0,3.7f,-20);
        var render = RenderTexture.GetTemporary(512,512,24);
        var old = RenderTexture.active; camera.targetTexture = render;
        camera.Render(); RenderTexture.active = render;
        var image = new Texture2D(512,512,TextureFormat.RGBA32,false);
        image.ReadPixels(new Rect(0,0,512,512),0,0); image.Apply();
        File.WriteAllBytes(path,image.EncodeToPNG());
        RenderTexture.active = old; camera.targetTexture = null; RenderTexture.ReleaseTemporary(render);
        UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(cameraObject);
    }
    static async void Run()
    {
        int exit=0, patternCount=0;
        bool safetyOnly=SessionState.GetBool(Key+".Safety",false);
        try
        {
            Application.runInBackground = true;
            game=UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            Application.logMessageReceived += Log;
            Directory.CreateDirectory("output/rising-animation/unity");
            var orders = new[] {
                new[]{Essence.RisingSlash},
                new[]{Essence.RisingSlash,Essence.SwordWave},
                new[]{Essence.RisingSlash,Essence.Guard},
                new[]{Essence.RisingSlash,Essence.SwordWave,Essence.Guard}
            };
            var poses=new[]{"Idle","Dash","Slash","Rising","AirSlash","Fall","Landing","Hurt"};
            foreach(var order in orders)
            {
                Reset(order); await Task.Delay(50);
                var animator=enemy.GetComponentInChildren<KarmaCharacterAnimator>();
                var source=(SpriteRenderer)Get(animator,"picture");
                var motion=Get(animator,"risingMotion");
                string form=source.sprite.texture.name;
                if (!safetyOnly) foreach(var pose in poses)
                {
                    Call(animator,"PlayRisingMotion",pose,1f,0f,1f);
                    Check(source.forceRenderingOff,form+"/"+pose+" uses its rig");
                    var bounds=source.bounds;
                    Check(enemy.DamageBounds==bounds,form+"/"+pose+" preserves original damage bounds");
                    var root=(GameObject)Get(Get(motion,"active"),"root");
                    var renderers=root.GetComponentsInChildren<SpriteRenderer>();
                    Check(renderers.Length>=2 && Array.TrueForAll(renderers,r=>r.sprite!=null && r.sprite.rect.width>0),
                        form+"/"+pose+" has complete articulated parts");
                    for(int i=0;i<24;i++)
                    {
                        Call(motion,"Sample",form,pose,i/24f,1f,0f,1f);
                        Check((int)motion.GetType().GetProperty("SampleIndex",Flags).GetValue(motion)==i,
                            form+"/"+pose+" exact 24 FPS sample "+i);
                        Check(enemy.DamageBounds==bounds,form+"/"+pose+" sample preserves hitbox");
                        Capture(animator,"output/rising-animation/unity/"+form+"_"+pose+"_"+i.ToString("00")+".png");
                    }
                    Call(motion,"Sample",form,pose,0f,1f,0f,1f);
                    var joint=renderers[renderers.Length-1].transform.parent;
                    var held=joint.localRotation;
                    Call(motion,"Sample",form,pose,.02f,1f,0f,1f);
                    Check(joint.localRotation==held,form+"/"+pose+" holds within a sample");
                    Call(animator,"EndRisingMotion");
                    Check(!source.forceRenderingOff,form+"/"+pose+" restores original renderer");
                }
                // Same original art transform mirrors every cutout; bounds remain canonical.
                var art=(Transform)Get(enemy,"art"); var scale=art.localScale;
                foreach(float facing in new[]{1f,-1f})
                {
                    art.localScale=new Vector3(Mathf.Abs(scale.x)*facing,scale.y,scale.z);
                    Call(animator,"PlayRisingMotion","Slash",1f,.6f,1f);
                    var rigRoot=(GameObject)Get(Get(motion,"active"),"root");
                    Check(Mathf.Sign(rigRoot.transform.localScale.x)==facing,form+" facing "+facing);
                    Check(enemy.DamageBounds==source.bounds,form+" facing preserves damage bounds");
                    if(facing<0)Capture(animator,"output/rising-animation/unity/"+form+"_FacingLeft.png");
                }
                art.localScale=scale;Call(animator,"EndRisingMotion");
                // Real coroutine phases, not just isolated pose requests.
                var patterns=new System.Collections.Generic.List<EnemyAttack>{
                    EnemyAttack.GroundStrike,EnemyAttack.UP01,EnemyAttack.UP02};
                if(Array.IndexOf(order,Essence.SwordWave)>=0) patterns.Add(EnemyAttack.UP03);
                if(Array.IndexOf(order,Essence.Guard)>=0) patterns.Add(EnemyAttack.UP04);
                if(order.Length==3) patterns.Add(EnemyAttack.UP05);
                if(safetyOnly && order.Length<3)patterns.Clear();
                foreach(var pattern in patterns)
                {
                    patternCount++;
                    Reset(order); await Task.Delay(20);
                    animator=enemy.GetComponentInChildren<KarmaCharacterAnimator>();
                    source=(SpriteRenderer)Get(animator,"picture");motion=Get(animator,"risingMotion");
                    if(pattern==EnemyAttack.UP01 || pattern==EnemyAttack.UP04)
                        enemy.transform.position+=Vector3.up*KarmaSpace.Units(3);
                    Force(pattern);
                    var seen=new System.Collections.Generic.HashSet<string>();
                    bool release=false;int capture=0,lastSample=-1;string lastMotion=null;
                    var timing=new System.Text.StringBuilder();
                    float recordingStart=Time.time;
                    await Wait(()=>{
                        string current=(string)motion.GetType().GetProperty("Motion",Flags).GetValue(motion);
                        if(current!=null)seen.Add(current);
                        release |= (string)Get(animator,"risingClip")=="Slash" &&
                            Get(enemy,"state").ToString()=="Attack" && source.sprite.name=="Slash";
                        int sample=(int)motion.GetType().GetProperty("SampleIndex",Flags).GetValue(motion);
                        if(order.Length==3 && current!=null && (current!=lastMotion || sample!=lastSample))
                        {
                            string file="live_"+pattern+"_"+(capture++).ToString("000")+".png";
                            timing.AppendLine(file+","+(Time.time-recordingStart).ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                            Capture(animator,"output/rising-animation/unity/"+file);
                            lastMotion=current;lastSample=sample;
                        }
                        return !(bool)Get(enemy,"running");
                    });
                    if(order.Length==3)File.WriteAllText("output/rising-animation/unity/live_"+pattern+".csv",timing.ToString());
                    if(pattern==EnemyAttack.GroundStrike) Check(release,form+" actual basic slash release");
                    if(pattern==EnemyAttack.UP01 || pattern==EnemyAttack.UP04 || pattern==EnemyAttack.UP02)
                        Check(seen.Contains(form+"_Fall") && seen.Contains(form+"_Landing"),form+"/"+pattern+" descent and landing");
                    if(pattern==EnemyAttack.UP02 || pattern==EnemyAttack.UP03)
                        Check(seen.Contains(form+"_Rising"),form+"/"+pattern+" upward weapon action");
                    if(pattern==EnemyAttack.UP05)Check(seen.Contains(form+"_AirSlash"),form+" actual crossing presentation");
                    Check(!enemy.Invulnerable && !enemy.Armored,form+"/"+pattern+" defense cleanup unchanged");
                    Check(Mathf.Abs(enemy.Position.y-KarmaSpace.GuardianGroundY)<.05f,form+"/"+pattern+" returns to ground");
                    Check(enemy.DamageBounds==source.bounds,form+"/"+pattern+" keeps original contact bounds");
                }
                Reset(order);await Task.Delay(20);
                animator=enemy.GetComponentInChildren<KarmaCharacterAnimator>();
                source=(SpriteRenderer)Get(animator,"picture");motion=Get(animator,"risingMotion");
                Set(enemy,"state",Enum.Parse(enemy.GetType().GetField("state",Flags).FieldType,"Attack"));
                Call(animator,"PlayRisingMotion","Slash",1f,.3f,1f);
                game.TogglePause();
                int pausedSample=(int)motion.GetType().GetProperty("SampleIndex",Flags).GetValue(motion);
                await Task.Delay(120);
                Check((int)motion.GetType().GetProperty("SampleIndex",Flags).GetValue(motion)==pausedSample,form+" pause holds visual clock");
                game.TogglePause();
                await Wait(()=>(int)motion.GetType().GetProperty("SampleIndex",Flags).GetValue(motion)>pausedSample);
                Check(true,form+" resume continues visual clock");
                Call(enemy,"InterruptPattern");
                Check(Get(motion,"active")==null && !source.forceRenderingOff,form+" interruption clears rig immediately");
                // Exercise the production pending-parry transition, including its interrupt callback.
                Set(enemy,"pendingParry",true);
                await Wait(()=>Get(enemy,"state").ToString()=="Stunned");
                Check((string)motion.GetType().GetProperty("Motion",Flags).GetValue(motion)==form+"_Hurt",form+" parry replaces attack with stagger");
                Check(Get(animator,"risingClip")==null,form+" parry clears phase request");
                Reset(order);await Task.Delay(20);
                animator=enemy.GetComponentInChildren<KarmaCharacterAnimator>();
                source=(SpriteRenderer)Get(animator,"picture");motion=Get(animator,"risingMotion");
                float health=enemy.Health;enemy.TakeDamage(1);
                Check(Mathf.Approximately(enemy.Health,health-1),form+" damage value unchanged");
                await Task.Delay(50);
                Check((string)motion.GetType().GetProperty("Motion",Flags).GetValue(motion)==form+"_Hurt",form+" actual hurt feedback");
                Call(animator,"EndRisingMotion");
                // Real lethal damage detaches surviving artwork before destroying the boss.
                enemy.TakeDamage(1000000);
                await Task.Delay(40);
                Check(animator!=null && animator.CurrentPose=="Death",form+" survives actual boss death");
                int deathCapture=0,lastDeathSample=-1;
                double deathLimit=EditorApplication.timeSinceStartup+1.2;
                await Wait(()=>{
                    if(EditorApplication.timeSinceStartup>deathLimit)return true;
                    int sample=(int)motion.GetType().GetProperty("SampleIndex",Flags).GetValue(motion);
                    if(sample!=lastDeathSample && source.forceRenderingOff)
                    {
                        Capture(animator,"output/rising-animation/unity/"+form+"_Collapse_"+(deathCapture++).ToString("00")+".png");
                        lastDeathSample=sample;
                    }
                    return !source.forceRenderingOff;
                });
                Check(deathCapture>0,form+" canonical collapse rendered");
                Check(!source.forceRenderingOff,form+" original fallen sprite restored");
                Reset(order);await Task.Delay(20);
                animator=enemy.GetComponentInChildren<KarmaCharacterAnimator>();
                source=(SpriteRenderer)Get(animator,"picture");
                animator.Empower(Essence.RisingSlash,.5f,null,false,form);
                await Task.Delay(100);
                Check(!source.forceRenderingOff && animator.CurrentPose.StartsWith("Revive",StringComparison.Ordinal),
                    form+" original revival presentation stays intact");
            }
            Check(error==null,"no runtime error");
            Debug.Log("PASS: "+assertions+" RisingSlash animation assertions; "+(safetyOnly ? "safety pass" : "32 rigs")+", "+patternCount+" live pattern/form combinations, facing, pause, interruption, parry, damage, collapse and revival");
        }
        catch(Exception e) { exit=1; Debug.LogException(e); }
        SessionState.SetBool(Key,false); SessionState.SetBool(Key+".Safety",false);
        EditorApplication.Exit(exit);
    }
    static void Log(string message,string stack,LogType type)
    {
        if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert) error=message;
    }
}
#endif
