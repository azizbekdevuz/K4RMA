#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

// Focused coverage for dash distance/handoff, recovery, and missed SW01/SW02 flight.
[InitializeOnLoad]
public static class KarmaSwordTempoRangeChecks
{
    const string Key = "K4RMA.SwordTempoRange";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static KarmaGame game;
    static bool started;
    static string error;
    static readonly List<string> results = new List<string>();
    static KarmaEnemy Enemy => (KarmaEnemy)typeof(KarmaSwordPlayChecks).GetField("enemy", F).GetValue(null);
    static object Behavior => Get(Enemy, "behavior");
    static object Movement => Get(Behavior, "movement");
    static Transform Root => (Transform)Get(Enemy, "effectsRoot");
    static object Get(object o, string n) { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, F); if (f != null) return f.GetValue(o); } throw new MissingFieldException(n); }
    static void Set(object o, string n, object v) { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, F); if (f != null) { f.SetValue(o, v); return; } } throw new MissingFieldException(n); }
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, F).Invoke(o, a);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Log(string message) { results.Add("PASS: " + message); Debug.Log(message); }
    static void Reset()
    {
        typeof(KarmaSwordPlayChecks).GetMethod("Reset", F).Invoke(null, new object[] { Essence.SwordWave });
        Set(Movement, "nextSwordRetreat", float.MaxValue);
        Set(Movement, "nextSwordApproach", float.MaxValue);
    }
    static void Place(float x) => typeof(KarmaSwordPlayChecks).GetMethod("Place", F).Invoke(null, new object[] { x });
    static void Force(EnemyAttack pattern) => typeof(KarmaSwordPlayChecks).GetMethod("Force", F).Invoke(null, new object[] { pattern });
    static async Task Until(Func<bool> done)
    {
        double end = EditorApplication.timeSinceStartup + 45;
        while (!done()) { Check(error == null, error); Check(EditorApplication.timeSinceStartup < end, "focused check timed out"); await Task.Delay(5); }
        Check(error == null, error);
    }
    static Task Seconds(float seconds) { float end = Enemy.FightSeconds + seconds; return Until(() => Enemy.FightSeconds >= end); }
    static void Shot(string name) => typeof(KarmaSwordPlayChecks).GetMethod("Snapshot", F).Invoke(null, new object[] { "tempo-range-" + name });
    static KarmaSwordTempoRangeChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true; EditorApplication.isPaused = false;
        if (!started && Time.frameCount > 2) { started = true; Run(); }
    }
    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static async Task Dash(int direction, bool wall)
    {
        Reset();
        float from = wall ? direction > 0 ? KarmaStage.BossLeftWall + 0.6f : KarmaStage.BossRightWall - 0.6f : 20;
        Enemy.transform.position = new Vector3(from, KarmaSpace.GuardianGroundY, 0);
        Place(from - direction * 3.5f);
        Set(Enemy, "running", false); Set(Enemy, "timer", float.MaxValue);
        Set(Movement, "nextSwordRetreat", 0f); Call(Behavior, "OnCompleted");
        Physics2D.SyncTransforms();
        bool sawMove = false, done = false, captured = false;
        float actual = 0, lean = 0;
        int peakEchoes = 0;
        var probe = new GameObject("focused dash handoff observer").AddComponent<KarmaSwordDashFrameProbe>();
        probe.After = () =>
        {
            try
            {
                float remaining = (float)Get(Movement, "swordRetreatRemaining");
                if (remaining > 0)
                {
                    sawMove = true;
                    var motion = ((Transform)Get(Enemy, "art")).GetComponent<KarmaWaveMotion>();
                    lean = Mathf.Max(lean, Mathf.Abs((float)Get(motion, "motionAngle")));
                    var echoes = ((Transform)Get(Enemy, "art")).GetComponent<KarmaSwordRetreatAfterimages>();
                    int count = 0;
                    foreach (var echo in (Array)Get(echoes, "pool")) if ((bool)Get(echo, "active")) count++;
                    peakEchoes = Mathf.Max(peakEchoes, count);
                    if (!captured && Mathf.Abs(Enemy.Position.x - from) > 8 && count >= 3)
                    { captured = true; Shot((wall ? "wall48-" : "dash32-") + direction); }
                }
                else if (sawMove)
                {
                    actual = (Enemy.Position.x - from) * direction;
                    Check((bool)Get(Enemy, "running") && (EnemyAttack)Get(Enemy, "selected") != EnemyAttack.SWBasic,
                        "dash finish must start an eligible skill in the same frame");
                    done = true;
                }
            }
            catch (Exception e) { error = e.ToString(); }
        };
        try { await Until(() => done); }
        finally { probe.After = null; UnityEngine.Object.Destroy(probe.gameObject); }
        Check(Mathf.Abs(actual - (wall ? 48 : 32)) < 0.01f, "doubled actual dash displacement: " + actual);
        Check(captured && peakEchoes >= 3 && peakEchoes <= 8 && lean > 3, "dash has bounded trailing artwork and measured torso lean");
        Log((wall ? "wall escape" : "ordinary dash") + " direction " + direction + ": " + actual + " units, same-frame skill, visible lean/echoes");
    }
    sealed class Flight
    {
        public KarmaBossAttack shot;
        public float lastX, halfWidth, speed;
        public bool ended;
    }
    static async Task Boundary(EnemyAttack pattern, int direction)
    {
        Reset();
        Enemy.transform.position = new Vector3(direction > 0 ? -12 : 52, KarmaSpace.GuardianGroundY, 0);
        Place(direction > 0 ? 48 : -8);
        Set(game.Player, "invulnerableUntil", 0f); Set(game.Player, "guardUntil", 0f);
        float health = game.Player.Health;
        Force(pattern);
        var flights = new List<Flight>();
        bool nextSkill = false, captured = false;
        float recoveryAt = -1, recoverySeconds = -1;
        var probe = new GameObject("focused missed projectile boundary observer").AddComponent<KarmaSwordDashFrameProbe>();
        probe.Before = () =>
        {
            Set(Enemy, "timer", float.MaxValue);
            Set(Movement, "nextSwordRetreat", float.MaxValue);
            Set(Movement, "nextSwordApproach", float.MaxValue);
        };
        probe.After = () =>
        {
            try
            {
                foreach (var shot in Root.GetComponentsInChildren<KarmaBossAttack>())
                {
                    if ((EnemyAttack)Get(shot, "sourcePattern") != pattern || flights.Exists(f => f.shot == shot)) continue;
                    flights.Add(new Flight { shot = shot, speed = Mathf.Abs(((Vector2)Get(shot, "velocity")).x),
                        halfWidth = pattern == EnemyAttack.SW02 ? ((Vector2)Get(shot, "size")).x / 2 : 0 });
                }
                foreach (var f in flights)
                {
                    if (f.ended) continue;
                    if (f.shot == null || !f.shot.gameObject.activeInHierarchy)
                    {
                        f.ended = true;
                        float edge = f.lastX + direction * f.halfWidth;
                        float wallX = direction > 0 ? KarmaStage.ArenaRight : KarmaStage.ArenaLeft;
                        Check(Mathf.Abs(wallX - edge) <= f.speed / 60f + 0.03f,
                            pattern + " disappeared before the arena boundary: edge=" + edge);
                    }
                    else
                    {
                        f.lastX = f.shot.transform.position.x;
                        if (!captured && direction > 0 && Mathf.Abs(f.lastX - KarmaStage.ArenaCenter) < 2)
                        { captured = true; Shot(pattern + "-full-flight"); }
                    }
                }
                if (!nextSkill && Get(Enemy, "state").ToString() == "Recover" && recoveryAt < 0)
                    recoveryAt = Enemy.FightSeconds;
                if (!nextSkill && !(bool)Get(Enemy, "running"))
                {
                    recoverySeconds = Enemy.FightSeconds - recoveryAt;
                    Check(flights.Exists(f => !f.ended), "boss recovers while missed projectile still has flight remaining");
                    // Begin a real new cast, including its normal recovery cleanup.
                    Set(Enemy, "attacks", new List<EnemyAttack> { EnemyAttack.SW04 });
                    Set(Behavior, "nextSwordBasic", float.MaxValue);
                    Call(Enemy, "BeginAttack");
                    Check((bool)Get(Enemy, "running") && (EnemyAttack)Get(Enemy, "selected") == EnemyAttack.SW04,
                        "a new skill starts while the old wave continues");
                    nextSkill = true;
                }
            }
            catch (Exception e) { error = e.ToString(); }
        };
        try
        {
            // Walk to the rear boundary after SW02 has locked; no parry or immunity.
            await Seconds(0.5f);
            float half = game.Player.GetComponent<Collider2D>().bounds.extents.x;
            Place(direction > 0 ? KarmaStage.ArenaLeft + half + 0.05f : KarmaStage.ArenaRight - half - 0.05f);
            await Until(() => nextSkill && flights.Count == (pattern == EnemyAttack.SW01 ? 2 : 1) && flights.TrueForAll(f => f.ended));
        }
        finally { probe.Before = probe.After = null; UnityEngine.Object.Destroy(probe.gameObject); }
        Check(game.Player.Health == health && game.Player.ParryCount == 0, "miss is genuine evasion with no damage or parry");
        float baseRecovery = pattern == EnemyAttack.SW01 ? 1 : 1.1f;
        float expected = Mathf.Max(0.38f, baseRecovery * game.Config.BossTuning.recoveryMultiplier * game.Config.BossTuning.sword.recoveryScale);
        Check(recoveryAt >= 0 && Mathf.Abs(recoverySeconds - expected) < 0.06f, "actual reduced recovery duration: " + recoverySeconds);
        Check(direction < 0 || captured, "in-game projectile artwork capture");
        Log(pattern + " direction " + direction + ": missed shots reach boundary across next skill/recovery; recovery=" + recoverySeconds.ToString("F3") + "s");
    }
    static async Task Cleanup()
    {
        Reset(); Force(EnemyAttack.SW01);
        await Until(() => Root.GetComponentsInChildren<KarmaBossAttack>().Length > 0);
        Call(Enemy, "InterruptPattern"); Set(Enemy, "running", true);
        Check(Root.GetComponentsInChildren<KarmaBossAttack>().Length == 0, "interruption clears persistent flight synchronously");
        Reset(); typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 2); Force(EnemyAttack.SW01);
        await Until(() => Root.GetComponentsInChildren<KarmaBossAttack>().Length > 0);
        var shot = Root.GetComponentsInChildren<KarmaBossAttack>()[0];
        Set(game.Player, "invulnerableUntil", 0f); game.Player.BeginCounter();
        Vector2 at = game.Player.GetComponent<Collider2D>().bounds.center;
        Enemy.TryAttackHit(at, at, Vector2.one, 1, (KarmaAttackContact)Get(shot, "contact"), 0, sourcePattern: EnemyAttack.SW01);
        await Until(() => Enemy.Stunned);
        Check(Root.GetComponentsInChildren<KarmaBossAttack>().Length == 0, "pre-final parry still clears persistent projectiles");
        Reset(); Force(EnemyAttack.SW02);
        await Until(() => Root.GetComponentsInChildren<KarmaBossAttack>().Length > 0);
        Enemy.CancelCombat();
        Check(Root.GetComponentsInChildren<KarmaBossAttack>().Length == 0, "combat cancellation clears persistent flight");
        Log("persistent attacks clean up on interruption, pre-final parry and combat cancellation");
    }
    static async void Run()
    {
        int exit = 0;
        try
        {
            Application.runInBackground = true; Application.targetFrameRate = 120; Time.captureDeltaTime = 1f / 60;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            typeof(KarmaSwordPlayChecks).GetField("game", F).SetValue(null, game);
            Set(game, "config", UnityEngine.Object.Instantiate(game.Config)); game.Config.playerHealth = 1000000;
            Application.logMessageReceived += (m, s, t) => { if ((t == LogType.Exception || t == LogType.Error || t == LogType.Assert) && s.Contains("Assets/K4RMA/")) error = m + "\n" + s; };
            typeof(KarmaSwordPlayChecks).GetMethod("CheckNewArtworkImports", F).Invoke(null, null);
            foreach (int direction in new[] { -1, 1 }) { await Dash(direction, false); await Dash(direction, true); }
            foreach (var pattern in new[] { EnemyAttack.SW01, EnemyAttack.SW02 })
                foreach (int direction in new[] { -1, 1 }) await Boundary(pattern, direction);
            await Cleanup();
            results.Insert(0, "PASS: focused sword dash/tempo/boundary/artwork checks");
        }
        catch (Exception e) { exit = 1; results.Insert(0, "FAIL: " + e); Debug.LogException(e); }
        Directory.CreateDirectory("output/validation");
        File.WriteAllLines("output/validation/sword-tempo-range.txt", results);
        Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
}
#endif
