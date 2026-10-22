#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KarmaPrototype;

// Copy into an isolated project's Assets/Editor before -executeMethod KarmaBossPlayChecks.Run.
// Uses real Play Mode updates, coroutines, Sprite.Create and player damage resolution.
[InitializeOnLoad]
public static class KarmaBossPlayChecks
{
    const string Key = "K4RMA.BossPlayChecks";
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly EnemyAttack[] Cases = {
        EnemyAttack.GroundStrike, EnemyAttack.SW01, EnemyAttack.SW02, EnemyAttack.SW03, EnemyAttack.SW04, EnemyAttack.SW05,
        EnemyAttack.UP01, EnemyAttack.UP02, EnemyAttack.UP03, EnemyAttack.UP04, EnemyAttack.UP05,
        EnemyAttack.GD02, EnemyAttack.GD03, EnemyAttack.GD04, EnemyAttack.GD05, EnemyAttack.GD05 };
    static KarmaGame game;
    static KarmaEnemy enemy;
    static int index, assertions, ticks, lifecycleStage, waitFrame;
    static float pauseFight;
    static bool sawShieldWarning;
    static bool started, sawRecovery, sawFullShield, responseTriggered, sawResponse, injectedFailure, sawTrialFailure;
    static double deadline;
    static float startedAt;
    static string warningError;
    static bool geometryChecked;
    static FieldInfo Field(object o, string name) => o.GetType().GetField(name, Flags);
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o,value);
    static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method,Flags).Invoke(o,args);
    static void Check(bool ok, string label) { assertions++; if (!ok) throw new Exception(label); }
    static KarmaBossPlayChecks()
    {
        EditorApplication.playModeStateChanged += OnState;
        if(SessionState.GetBool(Key,false)) EditorApplication.update += AutoTick;
    }
    static void AutoTick()
    {
        EditorApplication.isPaused = false;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        if(EditorApplication.isPlaying && game==null && Time.frameCount>0) OnState(PlayModeStateChange.EnteredPlayMode);
    }
    public static void Run()
    {
        Application.logMessageReceived += CaptureWarningError;
        Debug.Log("K4RMA PLAY CHECK START");
        Application.runInBackground = true;
        PlayerSettings.runInBackground = true;
        SessionState.SetBool(Key,true);
        EditorApplication.update += AutoTick;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false) || game!=null) return;
        Debug.Log("K4RMA PLAY CHECK INITIALIZE frame="+Time.frameCount);
        try
        {
            Application.logMessageReceived -= CaptureWarningError;
            Application.logMessageReceived += CaptureWarningError;
            Time.captureDeltaTime = 0.05f;
            Application.runInBackground = true;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            if (game == null) game = new GameObject("Play check game").AddComponent<KarmaGame>();
            game.Config.playerHealth = 1000000;
            game.StartRun();
            game.Player.Essences.Sacrifice(Essence.Flame);
            game.Player.Essences.Sacrifice(Essence.Dash);
            game.Player.Essences.Sacrifice(Essence.Ward);
            Call(game,"LoadRoom",true);
            enemy = game.Enemies[0];
            game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            deadline = EditorApplication.timeSinceStartup + 180;
            // OverrideGeometry must run inside Unity's player loop.
            new GameObject("Play check driver").AddComponent<KarmaBossPlayCheckDriver>().Step = Tick;
        }
        catch(Exception e) { Finish(e); }
    }
    static void Tick()
    {
        try
        {
            if (warningError != null) throw new Exception(warningError);
            if (!geometryChecked) { WarningGeometryChecks(); geometryChecked = true; }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play checks exceeded wall-clock deadline");
            if (!EditorApplication.isPlaying) throw new Exception("Play mode ended unexpectedly");
            if (index >= Cases.Length) { if(LifecycleChecks()) Finish(null); return; }
            if (++ticks % 200 == 0) Debug.Log("CHECK TICK frame="+Time.frameCount+" time="+Time.time+" paused="+EditorApplication.isPaused+" state="+Get<object>(enemy,"state"));
            if (!started) StartCase();
            Set(game.Player,"invulnerableUntil",Time.time + 100);
            var attack = Cases[index];
            sawRecovery |= Get<object>(enemy,"state").ToString()=="Recover";
            sawFullShield |= enemy.Invulnerable;
            if (attack == EnemyAttack.SW04 && Get<bool>(enemy,"swordWard") && !responseTriggered)
            { enemy.TakeDamage(1); responseTriggered = true; }
            if (attack == EnemyAttack.SW04 && responseTriggered && enemy.GetComponentInParent<KarmaGame>() != null)
                sawResponse |= game.World.GetComponentsInChildren<KarmaBossAttack>().Length > 0;
            if (index == Cases.Length-1 && enemy.TrialActive && !injectedFailure)
            {
                var trial = Get<KarmaTrialState>(enemy,"trial");
                for(int i=0;i<=trial.Allowed;i++) trial.Hit();
                injectedFailure = true;
            }
            sawTrialFailure |= enemy.Intent.Contains("시험 실패");
            if (Get<bool>(enemy,"running")) return;
            Check(sawRecovery,attack+" completed through recovery");
            Check(game.World.GetComponentsInChildren<KarmaBossAttack>().Length==0,attack+" no hostile residue after recovery");
            Check(!enemy.Invulnerable && !enemy.TrialActive,attack+" shield/trial released");
            Check(Mathf.Abs(enemy.Position.y-1.15f)<0.01f,attack+" landed");
            if(attack==EnemyAttack.SW05 || attack==EnemyAttack.UP04) Check(sawFullShield,attack+" protected airborne phase");
            if(attack==EnemyAttack.SW04) Check(sawResponse,"SW04 attack caused telegraphed retaliation");
            if(index==Cases.Length-1) Check(sawTrialFailure,"GD05 failure resolved after full sequence");
            Check(Time.time-startedAt < 29,attack+" no watchdog recovery");
            Debug.Log("PLAY PASS "+index+" "+attack);
            index++; started=false;
        }
        catch(Exception e) { Finish(e); }
    }
    static void CaptureWarningError(string message, string stack, LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            && (message.Contains("Invalid vertex array") || stack.Contains("KarmaAttackWarning")))
            warningError = message;
    }
    static void WarningGeometryChecks()
    {
        var marker = KarmaVisuals.Box(null, "Warning geometry check", Vector2.zero, Vector2.one, Color.white);
        var original = marker.sprite;
        var warning = marker.gameObject.AddComponent<KarmaAttackWarning>();
        var from = new Vector2(20, 1.15f);
        var size = new Vector2(1.3f, 1.9f);
        var offsets = new[] { Vector2.zero, new Vector2(7, 0), new Vector2(0, 5),
            new Vector2(7, 5), new Vector2(-7, 5), new Vector2(7, -5), new Vector2(-7, -5) };
        foreach (var offset in offsets)
        {
            var to = from + offset;
            warning.SetPath(marker, from, to, size);
            Check(warningError == null, "warning geometry accepted by Unity");
            var vertices = marker.sprite.vertices;
            Check(vertices.Length == (offset.x != 0 && offset.y != 0 ? 6 : 4), "warning retains swept hull");
            var actual = marker.bounds;
            var min = Vector2.Min(from, to) - size / 2;
            var max = Vector2.Max(from, to) + size / 2;
            Check(Vector2.Distance(actual.min, min) < 0.0001f && Vector2.Distance(actual.max, max) < 0.0001f,
                "warning covers endpoint boxes without pivot shift");
        }
        var shape = marker.sprite;
        for (int i = 0; i < 100; i++)
            warning.SetPath(marker, from, new Vector2(13 + i * 0.017f, 7.31f - i * 0.021f), size);
        Check(warningError == null && marker.sprite == shape, "tracking warning reuses valid geometry");
        Check(original.vertices.Length == 4, "shared box sprite remains intact");
        UnityEngine.Object.Destroy(marker.gameObject);
    }
    static void StartCase()
    {
        Call(enemy,"InterruptPattern");
        enemy.transform.position = new Vector3(20,1.15f,0);
        game.Player.transform.position = new Vector3(13,1,0);
        game.Player.GetComponent<Rigidbody2D>().position = new Vector2(13,1);
        Set(enemy,"engaged",true); Set(enemy,"selected",Cases[index]); Set(enemy,"direction",-1f);
        Set(enemy,"running",true); Set(enemy,"patternStarted",enemy.FightSeconds);
        Set(enemy,"nextShield",float.MaxValue);
        if(Cases[index]==EnemyAttack.GD02) Set(enemy,"shieldUntil",enemy.FightSeconds+5);
        enemy.StartCoroutine((IEnumerator)Call(enemy,"RunPattern"));
        sawRecovery=sawFullShield=responseTriggered=sawResponse=injectedFailure=sawTrialFailure=false;
        startedAt=Time.time; started=true;
    }
    static bool LifecycleChecks()
    {
        if(lifecycleStage==0)
        {
            Call(enemy,"InterruptPattern");
            typeof(KarmaEnemy).GetProperty("PrimaryType").SetValue(enemy,(Essence?)Essence.Ward);
            Set(enemy,"engaged",true); Set(enemy,"running",true);
            Set(enemy,"patternStarted",enemy.FightSeconds);
            Set(enemy,"nextShield",enemy.FightSeconds+0.2f);
            lifecycleStage++; return false;
        }
        if(lifecycleStage==1)
        {
            sawShieldWarning |= Get<float>(enemy,"shieldWarningUntil")>0;
            if(!enemy.Armored) return false;
            Check(sawShieldWarning,"GD01 full generation warning before automatic shield");
            Check(Get<float>(enemy,"shieldUntil")>enemy.FightSeconds,"GD01 finite active duration");
            pauseFight=enemy.FightSeconds; waitFrame=Time.frameCount;
            game.TogglePause(); lifecycleStage++; return false;
        }
        if(lifecycleStage==2)
        {
            if(Time.frameCount<waitFrame+10) return false;
            Check(enemy.FightSeconds==pauseFight && enemy.Armored,"pause freezes shield and combat clock");
            game.TogglePause(); Call(enemy,"InterruptPattern");
            Set(enemy,"selected",EnemyAttack.GD02); Set(enemy,"engaged",true);
            Set(game.Player,"invulnerableUntil",0f); game.Player.BeginCounter();
            int parries=game.Player.ParryCount;
            enemy.TryAttackHit(game.Player.Position,game.Player.Position,new Vector2(2,2),12,new KarmaAttackContact(),0);
            Check(game.Player.ParryCount==parries+1,"GD02 real damage resolution parried");
            Check(Get<bool>(enemy,"pendingParry"),"GD02 queued interrupt");
            waitFrame=Time.frameCount; lifecycleStage++; return false;
        }
        if(lifecycleStage==3)
        {
            if(Time.frameCount<waitFrame+2) return false;
            Check(enemy.Stunned && !enemy.Armored,"GD02 parry cancels pattern and breaks shield");
            Set(game.Player,"invulnerableUntil",0f);
            var hit=new KarmaAttackContact();
            enemy.TryAttackHit(game.Player.Position,game.Player.Position,new Vector2(2,2),12,hit,1);
            Check(game.Player.Stunned,"GD03 real stun applied");
            float until=Get<float>(game.Player,"stunUntil");
            enemy.TryAttackHit(game.Player.Position,game.Player.Position,new Vector2(2,2),12,hit,1);
            Check(Get<float>(game.Player,"stunUntil")==until,"GD03 stun not refreshed");
            Call(enemy,"InterruptPattern"); Set(enemy,"selected",EnemyAttack.GD05);
            Set(enemy,"running",true); Set(enemy,"patternStarted",enemy.FightSeconds);
            enemy.StartCoroutine((IEnumerator)Call(enemy,"RunPattern"));
            lifecycleStage++; return false;
        }
        if(lifecycleStage==4)
        {
            if(!enemy.TrialActive) return false;
            Check(Get<bool>(game.Player,"confined"),"GD05 confines before attacks");
            var effects=Get<Transform>(enemy,"effectsRoot");
            game.PlayerDied();
            Check(!Get<bool>(game.Player,"confined"),"death releases confinement synchronously");
            Check(effects.GetComponentsInChildren<KarmaBossAttack>().Length==0,"death disables live attacks synchronously");
            Check(!enemy.TrialActive && !enemy.Invulnerable,"death clears trial/shield");
            game.StartRun(); enemy=game.Enemies[0];
            Check(!game.Player.Stunned && !enemy.TrialActive && !enemy.Armored,"restart has no temporary state");
            effects=Get<Transform>(enemy,"effectsRoot");
            KarmaBossAttack.Spawn(game,enemy,effects,enemy.Position,Vector2.zero,Vector2.one,10,1,Color.white,null,0);
            enemy.TakeDamage(enemy.MaxHealth*2);
            Check(effects.GetComponentsInChildren<KarmaBossAttack>().Length==0,"boss death cancels owned attack");
            Check(game.Phase==RunPhase.Choosing,"boss death advances normal progression");
            return true;
        }
        return false;
    }
    static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; EditorApplication.update -= AutoTick; SessionState.SetBool(Key,false);
        string result = error==null ? "PASS: "+Cases.Length+" pattern runs, "+assertions+" Play Mode assertions" : "FAIL: "+error;
        File.WriteAllText("boss-play-validation.txt",result+"\n");
        if(error==null) Debug.Log(result); else Debug.LogException(error);
        EditorApplication.Exit(error==null ? 0 : 1);
    }
}
public sealed class KarmaBossPlayCheckDriver : MonoBehaviour
{
    public Action Step;
    void Update() { Step?.Invoke(); }
}
#endif
