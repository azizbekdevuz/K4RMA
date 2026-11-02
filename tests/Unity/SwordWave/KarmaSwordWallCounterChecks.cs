#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

[InitializeOnLoad]
public static class KarmaSwordWallCounterChecks
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Key = "K4RMA.WallScarCounter";
    static bool started, baseline;
    static KarmaGame game;
    static string runId, runtimeError;
    static readonly List<string> results = new List<string>();
    static string Output => "output/wall-counter/" + runId;
    static KarmaEnemy Enemy => (KarmaEnemy)typeof(KarmaSwordPlayChecks).GetField("enemy", F).GetValue(null);
    static object Behavior => Get(Enemy, "behavior");
    static object Movement => Get(Behavior, "movement");
    static object Counter => Get(Behavior, "counter");
    static Transform Art => (Transform)Get(Enemy, "art");
    static Transform Root => (Transform)Get(Enemy, "effectsRoot");
    static object Get(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, F); if (field != null) return field.GetValue(target); }
        throw new MissingFieldException(name);
    }
    static void Set(object target, string name, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, F); if (field != null) { field.SetValue(target, value); return; } }
        throw new MissingFieldException(name);
    }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, F).Invoke(target, args);
    static async Task Invoke(Type type, string name, params object[] args)
    { var result = type.GetMethod(name, F).Invoke(null, args); if (result is Task task) await task; }
    static void Reset() => typeof(KarmaSwordPlayChecks).GetMethod("Reset", F).Invoke(null, new object[] { Essence.SwordWave });
    static void Place(float x) => typeof(KarmaSwordPlayChecks).GetMethod("Place", F).Invoke(null, new object[] { x });
    static void Force(EnemyAttack pattern) => typeof(KarmaSwordPlayChecks).GetMethod("Force", F).Invoke(null, new object[] { pattern });
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Log(string message) { results.Add("PASS: " + message); Debug.Log(message); }
    static async Task Until(Func<bool> done, double seconds = 180)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (!done()) { Check(EditorApplication.timeSinceStartup < end, "Play Mode timeout"); await Task.Delay(5); }
    }
    static Task Seconds(float seconds) { float end = Enemy.FightSeconds + seconds; return Until(() => Enemy.FightSeconds >= end); }
    static void Shot(string name)
    {
        typeof(KarmaSwordPlayChecks).GetMethod("Snapshot", F).Invoke(null, new object[] { "wall-counter-" + name });
        File.Copy("output/validation/wall-counter-" + name + ".png", Output + "/" + name + ".png", true);
    }
    static KarmaSwordWallCounterChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying) EditorApplication.isPaused = false;
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2) { started = true; Run(); }
    }
    public static void RunBaseline() { Begin(true); }
    public static void RunUpdated() { Begin(false); }
    static void Begin(bool before)
    {
        if (!before) typeof(KarmaSwordPlayChecks).GetMethod("CompilePlayerScripts", F).Invoke(null, null);
        SessionState.SetBool(Key + ".Baseline", before); SessionState.SetBool(Key, true);
        SessionState.SetString(Key + ".Run", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static async void Run()
    {
        int exit = 0; baseline = SessionState.GetBool(Key + ".Baseline", false);
        runId = SessionState.GetString(Key + ".Run", "missing"); Directory.CreateDirectory(Output);
        try
        {
            Application.runInBackground = true; Application.targetFrameRate = 120; Time.captureDeltaTime = 1f / 60;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            foreach (var type in new[] { typeof(KarmaSwordPlayChecks), typeof(KarmaSwordRetreatDashPlayChecks), typeof(KarmaSwordOverhaulPlayChecks), typeof(KarmaSwordPolishPlayChecks) })
                type.GetField("game", F).SetValue(null, game);
            Set(game, "config", UnityEngine.Object.Instantiate(game.Config)); game.Config.playerHealth = 1000000;
            typeof(KarmaSwordOverhaulPlayChecks).GetField("updated", F).SetValue(null, true);
            SessionState.SetString("K4RMA.OverhaulChecks.Run", runId); Directory.CreateDirectory("output/overhaul-updated/" + runId);
            typeof(KarmaSwordPolishPlayChecks).GetField("runId", F).SetValue(null, runId); Directory.CreateDirectory("output/polish/" + runId);
            Application.logMessageReceived += (message, stack, type) =>
            { if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert) && stack.Contains("Assets/K4RMA/")) runtimeError = message + "\n" + stack; };
            await MeasureScars(false); await MeasureScars(true);
            if (!baseline)
            {
                CheckWallLoops(); CheckObstacles(); await CheckRenderedEscape(-1); await CheckRenderedEscape(1);
                for (int index = 0; index < 4; index++) { await CheckParry(index, false); await CheckParry(index, true); }
                await CheckNonParry(); await CheckReuseAndStale();
                await Invoke(typeof(KarmaSwordPlayChecks), "CheckBasics");
                await Invoke(typeof(KarmaSwordPlayChecks), "CheckSwordDamageFeedback");
                await Invoke(typeof(KarmaSwordPlayChecks), "CheckSW04");
                await Invoke(typeof(KarmaSwordPlayChecks), "CheckForcedInterruption");
                await Invoke(typeof(KarmaSwordPolishPlayChecks), "CheckDamageFlash");
                await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckCurves");
                foreach (int facing in new[] { -1, 1 })
                {
                    await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckGhosts", facing);
                    await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckWaves", facing);
                    await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckSW01Jump", facing);
                }
                await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckScars", false);
                await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckScars", true);
                await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckScarEscape");
                await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckScarHitAndCancel");
                File.Copy("output/validation/sword-player-compilation.txt", Output + "/player-compilation.txt", true);
                foreach (string folder in new[] { "output/polish/" + runId, "output/overhaul-updated/" + runId })
                    foreach (string file in Directory.GetFiles(folder)) File.Copy(file, Output + "/" + Path.GetFileName(file), true);
                Log("existing basic/SW01/SW02/5-round SW03/SW04/ghost/damage feedback/parry/stun/death regressions");
            }
            Check(runtimeError == null, "no game runtime exception: " + runtimeError);
            results.Insert(0, "PASS: " + (baseline ? "baseline SW03 duration" : "wall / five-round SW03 / ordered SW04 parry") + " Play Mode");
        }
        catch (Exception failure) { results.Insert(0, "FAIL: " + failure); Debug.LogException(failure); exit = 1; }
        File.WriteAllLines(Output + "/result.txt", results); Time.captureDeltaTime = 0;
        SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
    [Serializable] sealed class ScarMetrics { public bool enraged; public int rounds, blades; public float seconds; public List<float> starts = new List<float>(); }
    static async Task MeasureScars(bool enraged)
    {
        Reset(); Place(-13.8f); if (enraged) typeof(KarmaEnemy).GetProperty("Health").SetValue(Enemy, Enemy.MaxHealth * 0.1f);
        UnityEngine.Random.InitState(6103); float start = Enemy.FightSeconds; Force(EnemyAttack.SW03);
        var metrics = new ScarMetrics { enraged = enraged }; bool rising = false;
        while ((bool)Get(Enemy, "running"))
        {
            var attacks = Root.GetComponentsInChildren<KarmaBossAttack>();
            if (attacks.Length > 0 && !rising)
            {
                Check(attacks.Length == 20, "exactly twenty blades per round"); metrics.rounds++; metrics.blades += attacks.Length;
                metrics.starts.Add(Enemy.FightSeconds - start); rising = true;
                if (!baseline) Shot("scar-" + enraged + "-round-" + metrics.rounds);
            }
            if (attacks.Length == 0) rising = false;
            await Task.Delay(5);
        }
        metrics.seconds = Enemy.FightSeconds - start;
        Check(metrics.rounds == (baseline ? enraged ? 4 : 3 : 5), "correct normal/enraged round count");
        Check(Root.GetComponentsInChildren<KarmaBossAttack>().Length == 0, "fifth round cleanup");
        Set(Enemy, "running", true);
        File.WriteAllText(Output + "/scar-" + enraged + ".json", JsonUtility.ToJson(metrics, true));
        Log("SW03 " + (enraged ? "enraged" : "normal") + ": " + metrics.rounds + " x20 = " + metrics.blades + ", duration " + metrics.seconds.ToString("F3") + "s");
    }
    static void ArrangeWall(int wall)
    {
        Reset(); float x = wall < 0 ? KarmaStage.BossLeftWall + 0.6f : KarmaStage.BossRightWall - 0.6f;
        Enemy.transform.position = new Vector3(x, KarmaSpace.GuardianGroundY, 0); Place(x - wall * 3.5f);
        Call(Behavior, "OnCompleted"); Physics2D.SyncTransforms();
    }
    static void Tick(float dt)
    {
        typeof(KarmaEnemy).GetProperty("FightSeconds").SetValue(Enemy, Enemy.FightSeconds + dt);
        Call(Behavior, "TickMovement", dt, game.Player.Position.x - Enemy.Position.x);
    }
    static void CheckWallLoops()
    {
        var rows = new List<string> { "wall,repeat,travel,frames" };
        foreach (int wall in new[] { -1, 1 })
        {
            ArrangeWall(wall); Check(Enemy.GetComponent<Collider2D>() == null, "existing boss root has no player-solid collider");
            for (int run = 0; run < 50; run++)
            {
                float x = wall < 0 ? KarmaStage.BossLeftWall + 0.6f : KarmaStage.BossRightWall - 0.6f;
                Enemy.transform.position = new Vector3(x, KarmaSpace.GuardianGroundY, 0); Place(x - wall * 3.5f);
                typeof(KarmaEnemy).GetProperty("FightSeconds").SetValue(Enemy, Enemy.FightSeconds + 6);
                Call(Behavior, "OnCompleted"); float invulnerable = (float)Get(game.Player, "invulnerableUntil");
                int frames = 0; float previous = x, maxSpeed = 0;
                do
                {
                    Tick(1f / 60); frames++;
                    float step = Enemy.Position.x - previous;
                    Check(step * wall <= 0.001f, "wall escape moves only toward arena interior");
                    maxSpeed = Mathf.Max(maxSpeed, Mathf.Abs(step) * 60); previous = Enemy.Position.x;
                    Check(frames < 180, "escape cannot stick");
                } while ((float)Get(Movement, "swordRetreatRemaining") > 0);
                float travel = Mathf.Abs(Enemy.Position.x - x);
                Check(travel > 15.9f && maxSpeed <= 32.04f, "actual sixteen-unit continuous slide");
                Check((float)Get(game.Player, "invulnerableUntil") == invulnerable, "no new player invulnerability");
                // A pursuer crossing behind cannot immediately send it back to the wall.
                Place(Enemy.Position.x - wall * 3.5f); Call(Behavior, "OnCompleted");
                float stopped = Enemy.Position.x; for (int i = 0; i < 60; i++) Tick(1f / 60);
                Check(Mathf.Abs(Enemy.Position.x - stopped) < 0.001f, "escape cannot bounce immediately back toward same wall");
                rows.Add(wall + "," + run + "," + travel + "," + frames);
            }
        }
        File.WriteAllLines(Output + "/wall-100.csv", rows); Log("50 left + 50 right wall escapes: real 16-unit slides, no wallward step, no new invulnerability, no immediate return");
    }
    static void CheckObstacles()
    {
        foreach (int wall in new[] { -1, 1 })
        {
            ArrangeWall(wall); float start = Enemy.Position.x;
            var blocker = new GameObject("plain non-surface wall escape obstruction");
            blocker.transform.position = new Vector3(start - wall * 2, 3, 0); blocker.AddComponent<BoxCollider2D>().size = new Vector2(1, 6);
            Physics2D.SyncTransforms(); for (int i = 0; i < 120; i++) Tick(1f / 60);
            Check(Enemy.Position.x == start && !(bool)Get(Movement, "swordWallEscaping"), "blocked escape yields combat instead of forcing through generic Collider2D");
            UnityEngine.Object.DestroyImmediate(blocker);
            ArrangeWall(wall); var body = Enemy.gameObject.AddComponent<BoxCollider2D>(); body.size = new Vector2(1.6f, 3.2f); Physics2D.SyncTransforms();
            Tick(1f / 60); Check(!(bool)Get(Movement, "swordWallEscaping"), "if a solid boss-player collider exists, crossing is rejected");
            UnityEngine.Object.DestroyImmediate(body);
        }
        Log("generic obstruction and actual solid-player collision reject unsafe relocation; no collision disabling added");
    }
    static async Task CheckRenderedEscape(int wall)
    {
        ArrangeWall(wall); await Seconds(0.05f); Set(Enemy, "timer", 100f); Set(Enemy, "running", false);
        var images = Art.GetComponent<KarmaSwordRetreatAfterimages>(); var pool = (Array)Get(images, "pool");
        var probe = new GameObject("wall escape actual render observer").AddComponent<KarmaSwordDashFrameProbe>();
        int peak = 0; bool captured = false; string error = null; float start = Enemy.FightSeconds;
        probe.After = () =>
        {
            try
            {
                int active = 0; foreach (var echo in pool) if ((bool)Get(echo, "active")) active++;
                peak = Mathf.Max(peak, active);
                if (active >= 4 && !captured) { captured = true; Shot("wall-escape-" + wall); }
            } catch (Exception e) { error = e.ToString(); }
        };
        await Until(() => Enemy.FightSeconds - start > 1.3f || error != null); probe.After = null; UnityEngine.Object.Destroy(probe.gameObject);
        Check(error == null && captured && peak >= 4, "forward/backward relocation emits real trail: " + error);
        Set(Enemy, "timer", 0f); await Until(() => (bool)Get(Enemy, "running"));
        Log("wall " + wall + " rendered continuous cyan trail peak " + peak + ", natural attack resumes");
    }
    static object Volley => Get(Counter, "activeVolley");
    static KarmaBossAttack[] Shots => (KarmaBossAttack[])Get(Volley, "Shots");
    static KarmaAttackContact[] Contacts => (KarmaAttackContact[])Get(Volley, "Contacts");
    static async Task StartCounter(bool reset = true)
    {
        if (reset) Reset(); Place(50); Force(EnemyAttack.SW04); await Until(() => Volley != null);
        Enemy.TakeDamage(1);
    }
    static async Task HoldShots(int count)
    {
        await Until(() =>
        {
            if (Volley == null) return false;
            int released = 0;
            foreach (var shot in Shots) if (shot != null)
            { released++; Set(shot, "velocity", Vector2.zero); Set(shot, "remaining", 30f); shot.transform.position = new Vector3(30, 16, 0); }
            return released >= count;
        });
    }
    static KarmaBossAttack OtherShot()
    {
        return KarmaBossAttack.Spawn(game, Enemy, Root, new Vector2(25, 18), Vector2.zero, Vector2.one, 30, 1,
            Color.white, null, 0, sourcePattern: EnemyAttack.SWBasic);
    }
    static void RealParry(KarmaBossAttack shot)
    {
        Set(game.Player, "invulnerableUntil", 0f); Set(game.Player, "guardUntil", 0f); game.Player.BeginCounter();
        shot.transform.position = game.Player.GetComponent<Collider2D>().bounds.center;
        Set(shot, "velocity", Vector2.zero); int before = game.Player.ParryCount;
        Call(shot, "Update"); Check(game.Player.ParryCount == before + 1, "real ConsumeCounter parry success path");
    }
    static async Task CheckParry(int index, bool allFlying)
    {
        await StartCounter(); await HoldShots(allFlying ? 4 : index + 1);
        var volley = Volley; var shots = Shots; var contacts = Contacts; var slots = (Transform[])Get(volley, "Slots");
        var earlier = new List<KarmaBossAttack>(); for (int i = 0; i < index; i++) earlier.Add(shots[i]);
        var other = OtherShot(); RealParry(shots[index]);
        Check((int)Get(volley, "LastAllowed") == index, "only the parried sequence caps this volley");
        for (int later = index + 1; later < 4; later++)
        {
            Check(slots[later] == null || !slots[later].gameObject.activeInHierarchy, "later collision and VFX inactive synchronously");
            Check(contacts[later].Consumed, "later ledger cannot damage even in same frame");
            float health = game.Player.Health; Set(game.Player, "invulnerableUntil", 0f);
            Enemy.TryAttackHit(game.Player.Position, game.Player.Position, Vector2.one * 10, 1, contacts[later], 0, sourcePattern: EnemyAttack.SW04);
            Check(game.Player.Health == health, "canceled later contact causes no damage");
        }
        foreach (var shot in earlier) Check(shot != null && shot.gameObject.activeInHierarchy, "earlier projectile is not retroactively removed");
        Check(other != null && other.gameObject.activeInHierarchy && !Enemy.Stunned, "other patterns and existing local-parry boss state preserved");
        Shot("counter-parry-" + (index + 1) + "-" + allFlying);
        await Seconds(0.3f);
        for (int later = index + 1; later < 4; later++) Check(shots[later] == null || !shots[later].gameObject.activeInHierarchy, "pending releases stay canceled");
        foreach (var shot in shots) if (shot != null) Set(shot, "remaining", 0f);
        await Until(() => Volley == null);
        Check(!(bool)Get(Counter, "guardResponseRunning"), "counter state ends normally");
        Log("parry " + (index + 1) + " " + (allFlying ? "all four flying" : "later shots pending") + ": later only canceled synchronously; earlier/other retained; state drains");
    }
    static async Task CheckNonParry()
    {
        foreach (bool guarding in new[] { false, true })
        {
            await StartCounter(); await HoldShots(1); var volley = Volley; var shot = Shots[0];
            Set(game.Player, "invulnerableUntil", 0f); if (guarding) game.Player.BeginGuard(1);
            shot.transform.position = game.Player.GetComponent<Collider2D>().bounds.center; int parries = game.Player.ParryCount; Call(shot, "Update");
            Check(game.Player.ParryCount == parries && (int)Get(volley, "LastAllowed") == 3, "ordinary hit/guard never cancels volley");
            await HoldShots(4); Check(Shots[3] != null, "non-parry keeps four normal releases");
        }
        Log("failed/no parry and guard keep all four releases");
    }
    static async Task CheckReuseAndStale()
    {
        await StartCounter(); await HoldShots(1); var oldContact = Contacts[0]; int oldId = (int)Get(Volley, "Id"); RealParry(Shots[0]);
        Set(Shots[0], "remaining", 0f); await Until(() => Volley == null);
        await StartCounter(false); await HoldShots(1); int id = (int)Get(Volley, "Id");
        Call(Behavior, "CounterParried", oldContact);
        Check(id > oldId && (int)Get(Volley, "LastAllowed") == 3, "stale volley identity cannot cancel a reused ward");
        await HoldShots(4); Check(Shots[3] != null, "reuse resets all four shots");
        Log("reused SW04 creates fresh group ID and four releases; stale contact ignored");
    }
}
#endif
