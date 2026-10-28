#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;
using KarmaPrototype.Editor;

// Run through Pipeline script run in Play Mode; exercises actual Update/coroutine contacts.
[InitializeOnLoad]
public static class KarmaGuardPlayChecks
{
    const string BatchKey = "K4RMA.GuardPlayChecks.Batch";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static int assertions;
    static string runtimeError;
    static KarmaGuardPlayChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(BatchKey, false)) RunBatchChecks();
        };
        if (SessionState.GetBool(BatchKey, false)) EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
    }
    public static void RunBatch()
    {
        SessionState.SetBool(BatchKey, true);
        EditorApplication.EnterPlaymode();
    }
    static async void RunBatchChecks()
    {
        try { await Run(); SessionState.SetBool(BatchKey, false); EditorApplication.Exit(0); }
        catch (Exception error)
        {
            Directory.CreateDirectory("output/validation");
            File.WriteAllText("output/validation/guard-play-validation.txt", "FAIL: " + error + "\n");
            Debug.LogException(error);
            SessionState.SetBool(BatchKey, false); EditorApplication.Exit(1);
        }
    }
    static FieldInfo Field(object target, string name) { return target.GetType().GetField(name, Private); }
    static T Get<T>(object target, string name) { return (T)Field(target, name).GetValue(target); }
    static void Set(object target, string name, object value) { Field(target, name).SetValue(target, value); }
    static object Call(object target, string name, params object[] args)
    { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
    static void Check(bool ok, string label)
    { assertions++; if (!ok) throw new Exception(label); }

    public static async Task<string> Run()
    {
        Check(Application.isPlaying, "Play Mode required");
        KarmaGuardPractice.Stop();
        game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        Check(game != null, "K4RMA game exists");
        var original = game.Config;
        var config = UnityEngine.Object.Instantiate(original);
        Set(game, "config", config);
        config.playerHealth = 100000;
        bool background = Application.runInBackground;
        Application.runInBackground = true;
        assertions = 0;
        runtimeError = null;
        Application.logMessageReceived += CaptureError;
        try
        {
            Reset();
            Set(enemy, "running", true);
            Set(enemy, "nextShield", enemy.FightSeconds);
            await Seconds(0.25f);
            Check(Get<float>(enemy, "shieldWarningUntil") == 0, "GD01 defers generation during another attack");
            Set(enemy, "running", false);
            await Until(() => Get<float>(enemy, "shieldWarningUntil") > 0, 2);
            Set(enemy, "timer", 0f);
            await Seconds(0.2f);
            Check(!Get<bool>(enemy, "running"), "GD01 generation warning finishes before another attack starts");
            await Until(() => Get<bool>(enemy, "running") && Get<EnemyAttack>(enemy, "selected") == EnemyAttack.GD02, 3);
            Check(enemy.Armored, "GD01 shield reaches GD02 through natural attack selection");

            Reset();
            Place(new Vector2(5, KarmaSpace.GuardianGroundY));
            Set(game.Player, "invulnerableUntil", 0f);
            float health = game.Player.Health;
            Force(EnemyAttack.GD02);
            var warning = Get<Transform>(enemy, "effectsRoot").GetComponentInChildren<KarmaAttackWarning>();
            bool initialized = warning != null;
            if (warning != null)
                foreach (var line in warning.GetComponentsInChildren<LineRenderer>())
                    initialized &= line.widthMultiplier < KarmaSpace.Units(0.1f) && line.startColor.b < 0.5f;
            Check(initialized, "warning lines have their intended color and thickness on the first frame");
            await Until(() => Attacks().Length > 0, 3);
            Place(enemy.Position + Vector2.left * KarmaSpace.Units(2));
            await Until(() => game.Player.Health < health, 1);
            game.Player.Heal(config.playerHealth);
            await Until(() => State() == "Recover", 2);
            Check(Get<float>(enemy, "shieldUntil") > enemy.FightSeconds + 4,
                "GD02 remembers a later contact even if practice healing restores HP before recovery");

            Reset();
            Set(enemy, "selected", EnemyAttack.GD02);
            game.Player.BeginCounter();
            Set(game.Player, "invulnerableUntil", 0f);
            int parries = game.Player.ParryCount;
            enemy.TryAttackHit(game.Player.Position, game.Player.Position, Vector2.one, 10, new KarmaAttackContact(), 0);
            await Until(() => enemy.Stunned, 1);
            Check(game.Player.ParryCount == parries + 1 && !enemy.Armored, "GD02 real parry interrupts and breaks shield");

            Reset();
            Place(new Vector2(13, KarmaSpace.Units(1)));
            Set(game.Player, "invulnerableUntil", 0f);
            health = game.Player.Health;
            Force(EnemyAttack.GD03);
            await Until(() => enemy.Intent.Contains("회수"), 5);
            Check(Attacks().Length == 0, "GD03 return fragments cannot deal damage");
            var pieces = Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<SpriteRenderer>();
            int returns = 0;
            foreach (var piece in pieces)
                if (piece.name.StartsWith("회수 조각"))
                { returns++; Check(piece.sprite == KarmaArtwork.Sprite("GuardShard", "Ward"), "GD03 returning fragment uses bronze shield art"); }
            Check(returns == config.BossTuning.shieldFragments, "GD03 returns every shield fragment");
            Check(Mathf.Abs(health - game.Player.Health - config.enemyDamage * config.BossTuning.damageMultiplier * 0.8f) < 0.01f,
                "GD03 entire throw/fragment skill deals exactly one hit");
            await Until(() => !Get<bool>(enemy, "running"), 3);

            Reset();
            Place(new Vector2(15, KarmaSpace.Units(1)));
            Force(EnemyAttack.GD04);
            await Until(() => Wall() != null, 3);
            var wall = Wall();
            var bounds = wall.GetComponent<BoxCollider2D>().bounds;
            Check(Vector2.Distance(bounds.size, KarmaSpace.Units(new Vector2(1.4f, 6))) < 0.01f, "GD04 physical wall matches its warning footprint");
            Check(Mathf.Abs(game.Player.Position.x - bounds.center.x) >= bounds.extents.x + KarmaSpace.PlayerHalfWidth,
                "GD04 creation pushes an overlapping player outside the wall");
            Check(Attacks().Length == 0, "GD04 standing wall blocks movement without ongoing damage");
            float before = enemy.Health;
            enemy.TakeDamage(100, enemy.Position + Vector2.left);
            Check(Mathf.Abs(before - enemy.Health - 100 * config.BossTuning.shieldDamageMultiplier) < 0.01f, "GD04 reduces front attacks");
            before = enemy.Health;
            enemy.TakeDamage(100, enemy.Position + Vector2.right);
            Check(Mathf.Abs(before - enemy.Health - 100) < 0.01f, "GD04 permits full damage from behind");
            Place(new Vector2(bounds.min.x - KarmaSpace.PlayerHalfWidth - KarmaSpace.Units(0.2f), game.Player.Position.y));
            Physics2D.SyncTransforms();
            var stoppedAt = (Vector2)Call(game.Player, "MoveSlash", Vector2.right, KarmaSpace.Units(5));
            Check(stoppedAt.x <= bounds.min.x - KarmaSpace.PlayerHalfWidth + 0.05f, "GD04 blocks a piercing slash");
            Call(enemy, "InterruptPattern");
            Check(Wall() == null, "GD04 interruption removes the wall immediately");

            config.BossTuning.trialAttackCount = 5;
            config.BossTuning.trialAllowedHits = 0;
            Reset();
            typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.45f);
            Force(EnemyAttack.GD05);
            await Until(() => enemy.TrialActive, 3);
            var dome = UnityEngine.Object.FindAnyObjectByType<KarmaGuardDome>();
            Check(dome != null && dome.GetComponent<EdgeCollider2D>().pointCount == 65, "GD05 creates a semicircular room with a curved roof");
            Check(dome.Overlaps(new Vector2(20, dome.Radius * 0.5f), Vector2.zero)
                && !dome.Overlaps(new Vector2(20 + dome.Radius + 1, 0), Vector2.zero)
                && !dome.Overlaps(new Vector2(20, -1), Vector2.zero), "GD05 room blast follows the dome footprint");
            Check(Get<bool>(game.Player, "confined"), "GD05 confines the player during the trial");
            float clock = enemy.FightSeconds;
            float domeClock = dome.PhaseSeconds;
            game.TogglePause();
            await Task.Delay(150);
            Check(enemy.FightSeconds == clock && enemy.TrialActive && dome.PhaseSeconds == domeClock, "GD05 pause freezes combat, trial and dome clocks");
            game.TogglePause();
            var seen = new HashSet<string>();
            await Until(() =>
            {
                foreach (string cue in new[] { "낮은 파동", "봉인 낙하", "양쪽 협공", "바닥 봉인", "높낮이 연속" })
                    if (enemy.Intent.Contains(cue)) seen.Add(cue);
                return State() == "Recover";
            }, 22);
            Check(seen.Count == 5, "GD05 runs five different readable attack beats");
            clock = enemy.FightSeconds;
            Check(!enemy.TrialActive && !Get<bool>(game.Player, "confined")
                && UnityEngine.Object.FindAnyObjectByType<KarmaGuardDome>() == null, "GD05 success dissolves the room and releases arena restrictions");
            await Until(() => !Get<bool>(enemy, "running"), 7);
            Check(enemy.FightSeconds - clock >= config.BossTuning.trialStunSeconds - 0.08f,
                "GD05 success retains the full configured punish window even while enraged");

            Reset();
            Force(EnemyAttack.GD05);
            await Until(() => enemy.TrialActive, 3);
            Set(game.Player, "invulnerableUntil", 0f);
            enemy.TryAttackHit(game.Player.Position, game.Player.Position, Vector2.one, 10, new KarmaAttackContact(), 0);
            Set(game.Player, "invulnerableUntil", Time.time + 100);
            var trial = Get<KarmaTrialState>(enemy, "trial");
            Check(trial.Hits == 1 && !trial.Finished && enemy.TrialActive, "GD05 excess hits do not end the trial early");
            await Until(() => enemy.Intent.Contains("시험 실패"), 22);
            dome = UnityEngine.Object.FindAnyObjectByType<KarmaGuardDome>();
            Check(!enemy.TrialActive && Get<bool>(game.Player, "confined") && dome != null && dome.Charging,
                "GD05 failure keeps the room intact while charging its explosion");
            clock = dome.PhaseSeconds;
            game.TogglePause(); await Task.Delay(150);
            Check(dome.PhaseSeconds == clock && dome.Charging, "GD05 overload animation pauses with gameplay");
            game.TogglePause();
            health = game.Player.Health; Set(game.Player, "invulnerableUntil", 0f);
            await Until(() => dome.Exploding, 2);
            Check(dome.gameObject.activeInHierarchy && Attacks().Length == 0,
                "GD05 shell itself explodes without spawning a generic giant attack");
            await Seconds(0.25f);
            Check(Mathf.Abs(health - game.Player.Health - config.enemyDamage * config.BossTuning.damageMultiplier * config.BossTuning.trialExplosionMultiplier) < 0.01f,
                "GD05 room explosion deals exactly one configured hit");
            await Until(() => !Get<bool>(enemy, "running"), 5);
            Check(!Get<bool>(game.Player, "confined") && Attacks().Length == 0, "GD05 failure cleans up attacks and movement restrictions");

            config.BossTuning.trialAttackCount = 3;
            Reset(); Force(EnemyAttack.GD05);
            await Until(() => enemy.TrialActive, 3);
            Set(game.Player, "invulnerableUntil", 0f);
            enemy.TryAttackHit(game.Player.Position, game.Player.Position, Vector2.one, 10, new KarmaAttackContact(), 0);
            Set(game.Player, "invulnerableUntil", Time.time + 100);
            await Until(() => enemy.Intent.Contains("시험 실패"), 15);
            dome = UnityEngine.Object.FindAnyObjectByType<KarmaGuardDome>();
            await Until(() => dome.PhaseSeconds >= 0.9f, 2);
            health = game.Player.Health; parries = game.Player.ParryCount;
            Set(game.Player, "invulnerableUntil", 0f); game.Player.BeginCounter();
            await Until(() => dome.Exploding, 2); await Seconds(0.25f);
            Check(game.Player.ParryCount == parries + 1 && game.Player.Health == health,
                "GD05 room detonation retains a timed counter opportunity");
            await Until(() => !Get<bool>(enemy, "running"), 5);

            Reset();
            Force(EnemyAttack.GD05);
            await Until(() => enemy.TrialActive, 3);
            game.PlayerDied();
            Check(!enemy.TrialActive && !Get<bool>(game.Player, "confined") && Attacks().Length == 0
                && UnityEngine.Object.FindAnyObjectByType<KarmaGuardDome>() == null,
                "GD05 death cancels the trial and releases the player synchronously");

            KarmaGuardPractice.Start(3);
            enemy = game.Enemies[0];
            await Until(() => enemy.Intent.Contains("회수"), 7);
            Check(Get<EnemyAttack>(enemy, "selected") == EnemyAttack.GD03, "practice menu starts and runs the requested pattern");
            Check(game.Config == config && config.playerHealth == 100000, "practice leaves the existing configuration unchanged");
            KarmaGuardPractice.Stop();
            Check(runtimeError == null, "guard and practice paths emit no Unity runtime errors: " + runtimeError);

            string result = "PASS: " + assertions + " guard Play Mode assertions (GD01-GD05)";
            Directory.CreateDirectory("output/validation");
            File.WriteAllText("output/validation/guard-play-validation.txt", result + "\n");
            Debug.Log(result);
            return result;
        }
        finally
        {
            Application.logMessageReceived -= CaptureError;
            KarmaGuardPractice.Stop();
            if (game.Paused) game.TogglePause();
            game.ReturnToTitle();
            Set(game, "config", original);
            UnityEngine.Object.Destroy(config);
            Application.runInBackground = background;
        }
    }

    static void CaptureError(string message, string stack, LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            && (stack.Contains("Karma") || message.Contains("override geometry"))) runtimeError = message;
    }

    static void Reset()
    {
        game.StartRun();
        foreach (var essence in new[] { Essence.Ward, Essence.Flame, Essence.Dash }) game.Player.Essences.Sacrifice(essence);
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
        Call(game, "LoadRoom", true);
        enemy = game.Enemies[0];
        enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
        game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        Place(new Vector2(13, KarmaSpace.Units(1)));
        Set(game.Player, "invulnerableUntil", Time.time + 100);
        Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue); Set(enemy, "nextShield", float.MaxValue);
    }
    static void Place(Vector2 p) { game.Player.transform.position = p; game.Player.GetComponent<Rigidbody2D>().position = p; }
    static void Force(EnemyAttack pattern)
    {
        Call(enemy, "InterruptPattern");
        Set(enemy, "selected", pattern); Set(enemy, "direction", -1f); Set(enemy, "running", true);
        Set(enemy, "patternStarted", enemy.FightSeconds);
        if (pattern == EnemyAttack.GD02) Call(enemy, "RefreshAutoShield");
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static string State() { return Get<object>(enemy, "state").ToString(); }
    static KarmaBossAttack[] Attacks() { return Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<KarmaBossAttack>(); }
    static Transform Wall()
    {
        foreach (Transform child in Get<Transform>(enemy, "effectsRoot"))
            if (child.gameObject.activeInHierarchy && child.name.StartsWith("완성 방벽")) return child;
        return null;
    }
    static Task Seconds(float seconds)
    { float target = enemy.FightSeconds + seconds; return Until(() => enemy.FightSeconds >= target, seconds + 3); }
    static async Task Until(Func<bool> condition, float timeout)
    {
        double until = UnityEditor.EditorApplication.timeSinceStartup + timeout;
        while (!condition())
        {
            if (UnityEditor.EditorApplication.timeSinceStartup > until) throw new Exception("Guard check timed out: " + enemy.Intent);
            await Task.Delay(10);
        }
    }
}
#endif
