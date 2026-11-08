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

// Exercises actual Update/coroutine contacts in a disposable Unity project.
[InitializeOnLoad]
public static class KarmaGuardPlayChecks
{
    const string BatchKey = "K4RMA.GuardPlayChecks.Batch";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static int assertions;
    static string runtimeError;
    static bool started;
    static KarmaGuardPlayChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(BatchKey, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        // Unity Search may enable Error Pause during its initial index build.
        // Clear only pre-test startup pauses; runtime errors are captured below.
        if (!started && EditorApplication.isPlaying) { Application.runInBackground = true; EditorApplication.isPaused = false; }
        if (!started && EditorApplication.isPlaying && Time.frameCount > 1)
        { started = true; RunBatchChecks(); }

    }
    public static void RunBatch()
    {
        Debug.Log("Starting guard Play Mode validation");
        SessionState.SetBool(BatchKey, true);
        EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
        EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
        var gameView = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        gameView.Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    public static void RunFeedbackBatch()
    {
        SessionState.SetBool(BatchKey + ".FeedbackOnly", true);
        RunBatch();
    }
    public static void RunTrialBatch()
    {
        SessionState.SetBool(BatchKey + ".TrialOnly", true);
        RunBatch();
    }
    public static void RunGD05Batch()
    {
        SessionState.SetBool(BatchKey + ".GD05Only", true);
        RunBatch();
    }
    public static void RunGD03Batch()
    {
        SessionState.SetBool(BatchKey + ".GD03Only", true);
        RunBatch();
    }
    public static void RunGD04Batch()
    {
        SessionState.SetBool(BatchKey + ".GD04Only", true);
        RunBatch();
    }
    public static void RunGD04PreviewBatch()
    {
        SessionState.SetBool(BatchKey + ".GD04Preview", true);
        RunBatch();
    }
    static async void RunBatchChecks()
    {
        bool trialOnly = SessionState.GetBool(BatchKey + ".TrialOnly", false);
        SessionState.EraseBool(BatchKey + ".TrialOnly");
        bool feedbackOnly = SessionState.GetBool(BatchKey + ".FeedbackOnly", false);
        SessionState.EraseBool(BatchKey + ".FeedbackOnly");
        bool gd04Preview = SessionState.GetBool(BatchKey + ".GD04Preview", false);
        SessionState.EraseBool(BatchKey + ".GD04Preview");
        bool gd04Only = SessionState.GetBool(BatchKey + ".GD04Only", false);
        SessionState.EraseBool(BatchKey + ".GD04Only");
        bool gd03Only = SessionState.GetBool(BatchKey + ".GD03Only", false);
        SessionState.EraseBool(BatchKey + ".GD03Only");
        bool gd05Only = SessionState.GetBool(BatchKey + ".GD05Only", false);
        SessionState.EraseBool(BatchKey + ".GD05Only");
        try { Debug.Log(await (trialOnly ? RunChecks(false, trialOnly: true) : feedbackOnly ? RunChecks(false, feedbackOnly: true) : gd04Preview ? RunChecks(false, gd04PreviewOnly: true) : gd04Only ? RunGD04Only() : gd03Only ? RunGD03Only() : gd05Only ? RunGD05Only() : Run())); SessionState.SetBool(BatchKey, false); EditorApplication.Exit(0); }
        catch (Exception error)
        {
            Directory.CreateDirectory("output/validation");
            File.WriteAllText("output/validation/" + (gd04Preview ? "gd04-preview-validation.txt" : gd04Only ? "gd04-validation.txt" : gd03Only ? "gd03-boomerang-validation.txt" : gd05Only ? "gd05-validation.txt" : "guard-play-validation.txt"), "FAIL: " + error + "\n");
            Debug.LogException(error);
            SessionState.SetBool(BatchKey, false); EditorApplication.Exit(1);
        }
    }
    static object Pattern(string key)
    {
        var behavior = typeof(KarmaEnemy).GetField("behavior", Private).GetValue(enemy);
        if (key == "GD01") return behavior.GetType().GetField("shield", Private).GetValue(behavior);
        var patterns = (System.Collections.IDictionary)behavior.GetType().BaseType.GetField("patterns", Private).GetValue(behavior);
        return patterns[Enum.Parse(typeof(EnemyAttack), key)];
    }
    static FieldInfo Field(object target, string name) { return target.GetType().GetField(name, Private); }
    static T Get<T>(object target, string name)
    {
        if (ReferenceEquals(target, enemy) && name == "heavyParryQueued") target = Pattern("GD02");
        return (T)Field(target, name).GetValue(target);
    }
    static void Set(object target, string name, object value) { Field(target, name).SetValue(target, value); }
    static object Call(object target, string name, params object[] args)
    {
        if (ReferenceEquals(target, enemy))
        {
            if (name == "BreakAutoShield") { Set(enemy, "shieldUntil", 0f); Set(enemy, "shieldWarningUntil", 0f); Set(enemy, "nextShield", enemy.FightSeconds + game.Config.BossTuning.guard.autoShieldPeriod); return null; }
            if (name == "RefreshAutoShield") { Set(enemy, "shieldUntil", float.PositiveInfinity); Set(enemy, "shieldWarningUntil", 0f); return null; }
            if (name.StartsWith("Trial")) target = Pattern("GD05");
        }
        return target.GetType().GetMethod(name, Private | BindingFlags.Public).Invoke(target, args);
    }
    static void Check(bool ok, string label)
    { assertions++; if (!ok) throw new Exception(label); }

    public static Task<string> Run() => RunChecks(false);
    public static Task<string> RunGD03Only() => RunChecks(true);
    public static Task<string> RunMoltenOnly() => RunChecks(false, true);
    public static Task<string> RunGD04Only() => RunChecks(false, gd04Only: true);
    public static Task<string> RunGD05Only() => RunChecks(false, gd05Only: true);
    static async Task<string> RunChecks(bool gd03Only, bool moltenOnly = false, bool gd04Only = false, bool gd05Only = false, bool gd04PreviewOnly = false, bool feedbackOnly = false, bool trialOnly = false)
    {
        Check(Application.isPlaying, "Play Mode required");
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
            EditorApplication.isPaused = false;
            int frame = Time.frameCount;
            double frameDeadline = EditorApplication.timeSinceStartup + 10;
            while (Time.frameCount <= frame && EditorApplication.timeSinceStartup < frameDeadline)
            { EditorApplication.QueuePlayerLoopUpdate(); await Task.Delay(50); }
            Check(Time.frameCount > frame, "guard checks run on an advancing player loop");
            if (trialOnly)
            {
                await CheckTrialOutcomes();
                Check(runtimeError == null, "GD05 outcome paths emit no game runtime errors: " + runtimeError);
                return "PASS: " + assertions + " GD05 actual-hit accounting, success damage opening, failure blast and cancellation assertions";
            }
            if (feedbackOnly)
            {
                await CheckAttackContinuations();
                await CheckShieldReturnParry();
                await CheckSlamRearChain();
                await CheckGuardDome();
                await CheckTrialOutcomes();
                Check(runtimeError == null, "Guard continuation and trial outcomes emit no game runtime errors: " + runtimeError);
                return "PASS: " + assertions + " Guard immediate follow-up, retained parries and trial success/failure assertions";
            }
            if (gd04PreviewOnly)
            {
                await CaptureSlamPreview(original.playerHealth);
                Check(runtimeError == null, "GD04 preview emits no game runtime errors: " + runtimeError);
                string previewResult = "PASS: " + assertions + " GD04 real frame capture, physical airborne and existing rear chain preview assertions";
                File.WriteAllText("output/validation/gd04-preview-validation.txt", previewResult + "\n");
                return previewResult;
            }
            if (gd05Only)
            {
                await CheckGuardDome();
                await CheckTrialOutcomes();
                Check(runtimeError == null, "GD05 emits no game runtime errors: " + runtimeError);
                string gd05Result = "PASS: " + assertions + " GD05 ricochet, thresholds, actual-hit counting, success opening and failure explosion assertions";
                Directory.CreateDirectory("output/validation");
                File.WriteAllText("output/validation/gd05-validation.txt", gd05Result + "\n");
                return gd05Result;
            }
            if (gd04Only)
            {
                await CheckGuardShieldAppearance();
                await CheckStationaryEruptions();
                await CheckGroundSlam();
                await CheckSlamDamageAndAirborne();
                await CheckFarEruptionJump();
                await CheckSlamRearChain();
                await CheckAttackContinuations();
                Check(runtimeError == null, "GD04 presentation and chain emit no game runtime errors: " + runtimeError);
                string gd04Result = "PASS: " + assertions + " GD01 restored appearance and GD04 stationary near-to-far eruption, damage, jump and rear-chain assertions";
                Directory.CreateDirectory("output/validation");
                File.WriteAllText("output/validation/gd04-validation.txt", gd04Result + "\n");
                return gd04Result;
            }
            if (moltenOnly)
            {
                await CheckMoltenHeavy();
                Check(runtimeError == null, "GD02 molten presentation emits no game runtime errors: " + runtimeError);
                string moltenResult = "PASS: " + assertions + " GD02 molten timing, form, facing and cleanup assertions";
                Directory.CreateDirectory("output/validation");
                File.WriteAllText("output/validation/gd02-molten-validation.txt", moltenResult + "\n");
                return moltenResult;
            }
            if (gd03Only)
            {
                await CheckShieldBoomerang();
                await CheckShieldReturnParry();
                await CheckShieldCancellation();
                Check(runtimeError == null, "GD03 boomerang and reflected return emit no game runtime errors: " + runtimeError);
                string shieldResult = "PASS: " + assertions + " GD03 arena-edge boomerang, pass damage, return parry and cleanup assertions";
                Directory.CreateDirectory("output/validation");
                File.WriteAllText("output/validation/gd03-boomerang-validation.txt", shieldResult + "\n");
                return shieldResult;
            }
            Reset();
            new KarmaGuardOwnershipChecks().CheckSettings(Check);
            new KarmaGuardSelectionChecks().Run(enemy, game);
            Check(true, "Guard keeps the saved profiles and 2000 weighted pattern selections");
            await KarmaGuardBasicDodgeChecks.Run();
            await CheckGuardShieldAppearance();
            await CheckMoltenHeavy();
            await CheckPersistentShield();
            Reset();
            Call(enemy, "BreakAutoShield");
            Set(enemy, "running", true);
            Set(enemy, "nextShield", enemy.FightSeconds);
            await Seconds(0.25f);
            Check(Get<float>(enemy, "shieldWarningUntil") == 0, "GD01 defers generation during another attack");
            Set(enemy, "running", false);
            await Until(() => Get<float>(enemy, "shieldWarningUntil") > 0, 2);
            Set(enemy, "timer", 0f);
            await Seconds(0.2f);
            Check(!Get<bool>(enemy, "running"), "GD01 generation warning finishes before another attack starts");
            await Until(() => enemy.AutoShieldActive, 2);
            Check(enemy.Armored, "GD01 reforms after its complete generation warning");

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
            Check(enemy.AutoShieldActive, "GD02 hit and subsequent healing keep GD01 active through recovery");

            Reset();
            Set(enemy, "selected", EnemyAttack.GD02);
            game.Player.BeginCounter();
            Set(game.Player, "invulnerableUntil", 0f);
            int parries = game.Player.ParryCount;
            enemy.TryAttackHit(game.Player.Position, game.Player.Position, Vector2.one, 10, new KarmaAttackContact(breaksShieldOnParry: true), 0);
            await Until(() => enemy.Stunned, 1);
            Check(game.Player.ParryCount == parries + 1 && !enemy.Armored, "GD02 real parry interrupts and breaks shield");
            Check(!enemy.AutoShieldActive && Get<float>(enemy, "nextShield") > enemy.FightSeconds + 7.8f,
                "GD02 parry starts the regeneration delay with GD01 absent");

            await CheckShieldBoomerang();
            await CheckShieldReturnParry();
            await CheckShieldCancellation();

            await CheckStationaryEruptions();
            await CheckGroundSlam();
            await CheckSlamDamageAndAirborne();
            await CheckFarEruptionJump();
            await CheckSlamRearChain();

            await CheckGuardDome();
            await CheckTrialOutcomes();
            await CheckAttackContinuations();
            Debug.Log(await KarmaGuardCameraChecks.Run());

            Check(runtimeError == null, "guard paths emit no Unity runtime errors: " + runtimeError);

            string result = "PASS: " + assertions + " guard Play Mode assertions (GD01-GD05)";
            Directory.CreateDirectory("output/validation");
            File.WriteAllText("output/validation/guard-play-validation.txt", result + "\n");
            Debug.Log(result);
            return result;
        }
        finally
        {
            Application.logMessageReceived -= CaptureError;
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

    static async Task CheckMoltenHeavy()
    {
        var orders = new[] {
            new[] { Essence.Guard },
            new[] { Essence.Guard, Essence.SwordWave },
            new[] { Essence.Guard, Essence.RisingSlash },
            new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash },
            new[] { Essence.Guard, Essence.RisingSlash, Essence.SwordWave }
        };
        foreach (var order in orders)
        foreach (int facing in new[] { -1, 1 })
        {
            Reset(order);
            Place(new Vector2(enemy.Position.x + facing * 16, KarmaSpace.PlayerHalfHeight));
            string form = order.Length == 3 ? "WardFinal" : order.Length == 1 ? "Ward"
                : order[1] == Essence.SwordWave ? "WardWave" : "WardRise";
            var animator = Get<Transform>(enemy, "art").GetComponent<KarmaCharacterAnimator>();
            var picture = Get<SpriteRenderer>(animator, "picture");
            Force(EnemyAttack.GD02, facing);
            var normal = KarmaArtwork.Sprite("GuardHeavy" + form, "Windup");
            var molten = KarmaArtwork.Sprite("GuardHeavyMolten" + form, "Windup");
            Check(picture.sprite == normal && !enemy.HeavyParryWindowOpen, "GD02 starts with the original cool weapon: " + form);
            Vector3 scale = picture.transform.localScale, position = picture.transform.localPosition;
            await Until(() => enemy.HeavyParryWindowOpen, 2);
            var effects = Get<Transform>(enemy, "effectsRoot");
            Check(molten != null && picture.sprite == molten && effects.Find("GD02 parry cue") == null,
                "GD02 signals the parry window with the molten weapon and no hand glow: " + form);
            Check(normal.rect == molten.rect && normal.pivot == molten.pivot && normal.pixelsPerUnit == molten.pixelsPerUnit,
                "GD02 molten sheet preserves its original frame, foot pivot and resolution: " + form);
            Check(picture.transform.localScale == scale && picture.transform.localPosition == position && enemy.Facing == facing,
                "GD02 heat swap preserves body scale, position and facing: " + form);
            float clock = enemy.FightSeconds;
            game.TogglePause(); await Task.Delay(80);
            Check(enemy.FightSeconds == clock && picture.sprite == molten, "GD02 molten cue stays fixed during pause");
            game.TogglePause();
            await Until(() => animator.CurrentPose == "GD02/Downstroke", 2);
            Check(picture.sprite == KarmaArtwork.Sprite("GuardHeavyMolten" + form, "Downstroke"), "GD02 downstroke keeps the molten blade");
            await Until(() => animator.CurrentPose == "GD02/FollowThrough", 1);
            Check(picture.sprite == KarmaArtwork.Sprite("GuardHeavyMolten" + form, "FollowThrough"), "GD02 follow-through keeps the molten blade");
            await Until(() => State() == "Recover", 3);
            await Seconds(0.04f);
            Check(!enemy.HeavyParryWindowOpen && !Array.Exists(KarmaArtwork.Sprites("GuardHeavyMolten" + form),
                sprite => sprite == picture.sprite), "GD02 recovery clears the overheated presentation");
            Check(animator.CurrentPose == "Idle" || animator.CurrentPose == "Guard",
                "GD02 missed heavy recovers without a false hurt/recoil pose: " + form);
            Vector3 idleScale = picture.transform.localScale;
            Check(scale.x < idleScale.x && Mathf.Approximately(scale.x, scale.y),
                "GD02 larger source artwork is normalized to the standing body without stretching: " + form);
            animator.Hurt(); await Seconds(0.03f);
            Check(animator.CurrentPose == "Hurt", "GD02 recovery still shows an actual damage reaction");
        }
    }

    static SpriteRenderer WholeShield()
    {
        foreach (var sr in Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<SpriteRenderer>())
            if (sr.name.StartsWith("GD03 큰 방패")) return sr;
        return null;
    }

    static async Task CheckShieldBoomerang()
    {
        foreach (int facing in new[] { -1, 1 })
        {
            Reset(new[] { Essence.Guard, Essence.SwordWave });
            Vector2 playerAt = new Vector2(20 + facing * 12, KarmaSpace.PlayerHalfHeight);
            Place(playerAt); Set(game.Player, "invulnerableUntil", 0f);
            float health = game.Player.Health, bossHealth = enemy.Health;
            float damage = game.Config.enemyDamage * game.Config.BossTuning.damageMultiplier
                * game.Config.BossTuning.guard.damageScale * game.Config.BossTuning.guard.shieldThrowDamageMultiplier;
            Force(EnemyAttack.GD03, facing);
            await Until(() => WholeShield() != null, 2);
            var shield = WholeShield();
            Check(shield.sprite == KarmaArtwork.Sprite("GuardBossVFX", "WholeHot"), "GD03 reuses the existing whole heated shield");
            Vector3 from = shield.transform.position;
            await Seconds(0.05f);
            Check((shield.transform.position.x - from.x) * facing > 0.5f, "GD03 flies along its locked direction");
            Vector3 frozen = shield.transform.position, rotation = shield.transform.eulerAngles;
            float clock = enemy.FightSeconds;
            game.TogglePause(); await Task.Delay(100);
            Check(enemy.FightSeconds == clock && shield.transform.position == frozen && shield.transform.eulerAngles == rotation,
                "GD03 pause freezes both travel and spin");
            game.TogglePause();
            await Until(() => game.Player.Health < health, 2);
            Check(Mathf.Abs(health - game.Player.Health - damage) < 0.02f && !game.Player.Stunned,
                "GD03 outward pass deals exactly one damage-only hit");
            health = game.Player.Health;
            Set(game.Player, "invulnerableUntil", 0f);
            Place(playerAt + Vector2.up * 10);
            await Until(() => shield.name.EndsWith("복귀"), 3);
            float edge = facing < 0 ? KarmaStage.ArenaLeft + KarmaSpace.Units(0.9f)
                : KarmaStage.ArenaRight - KarmaSpace.Units(0.9f);
            Check(Mathf.Abs(shield.transform.position.x - edge) < 0.75f
                && Mathf.Abs(shield.transform.position.y - from.y) < 0.01f,
                "GD03 reaches the arena edge at its locked height even after the player moves");
            Check(WholeShield() == shield && Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<TrailRenderer>().Length == 1,
                "GD03 turns the same whole shield around without splitting or replacement");
            Check(game.Player.Health == health, "GD03 moving away after the outward hit adds no extra contacts");
            Place(playerAt); Set(game.Player, "invulnerableUntil", 0f);
            await Until(() => game.Player.Health < health, 3);
            Check(Mathf.Abs(health - game.Player.Health - damage) < 0.02f && !game.Player.Stunned,
                "GD03 return pass has an independent damage-only contact");
            health = game.Player.Health;
            Set(game.Player, "invulnerableUntil", 0f);
            await Until(() => !Get<bool>(enemy, "running"), 4);
            Check(game.Player.Health == health && enemy.Health == bossHealth && !enemy.Stunned,
                "GD03 cannot hit twice on one pass or damage the boss without a parry");
            Check(WholeShield() == null && enemy.AutoShieldActive,
                "GD03 normal catch clears the shield and preserves GD01");
        }
        foreach (int facing in new[] { -1, 1 })
        {
            Reset(new[] { Essence.Guard, Essence.SwordWave });
            enemy.transform.position = new Vector3(facing < 0 ? KarmaStage.BossLeftWall : KarmaStage.BossRightWall,
                KarmaSpace.GuardianGroundY, 0);
            Place(new Vector2(KarmaStage.ArenaCenter, 12));
            Force(EnemyAttack.GD03, facing);
            await Until(() => !Get<bool>(enemy, "running"), 4);
            Check(WholeShield() == null && !enemy.Stunned, "GD03 can finish a throw next to either arena wall");
        }
    }

    static async Task CheckShieldReturnParry()
    {
        foreach (bool returnPass in new[] { false, true })
        foreach (bool shielded in new[] { false, true })
        {
            Reset(new[] { Essence.Guard, Essence.SwordWave });
            int facing = shielded ? 1 : -1;
            if (!shielded) Call(enemy, "BreakAutoShield");
            Vector2 playerAt = new Vector2(20 + facing * 14, KarmaSpace.PlayerHalfHeight);
            Place(playerAt + (returnPass ? Vector2.up * 10 : Vector2.zero));
            Force(EnemyAttack.GD03, facing);
            await Until(() => WholeShield() != null, 2);
            var shield = WholeShield();
            if (returnPass)
            {
                await Until(() => shield.name.EndsWith("복귀"), 3);
                Place(playerAt);
            }
            await Until(() => Mathf.Abs(shield.transform.position.x - playerAt.x) < 4, 3);
            float playerHealth = game.Player.Health, bossHealth = enemy.Health;
            int parries = game.Player.ParryCount;
            Set(game.Player, "invulnerableUntil", 0f); game.Player.BeginCounter();
            await Until(() => game.Player.ParryCount > parries, 1);
            Check(game.Player.Health == playerHealth && enemy.Health == bossHealth && !enemy.Stunned
                && !Get<bool>(enemy, "pendingParry") && game.Projectiles.Count == 0,
                "GD03 parry protects the player and defers boss damage and stun until the shield returns");
            Check(!enemy.AutoShieldActive && !Get<SpriteRenderer>(enemy, "shieldHalo").enabled
                && Get<float>(enemy, "nextShield") >= enemy.FightSeconds + game.Config.BossTuning.guard.autoShieldPeriod - 0.06f,
                "GD03 outward and return parries immediately break GD01 and start its existing regeneration delay");
            Check(WholeShield() == shield && shield.name.EndsWith("반사"),
                "GD03 both passes turn the actual parried shield toward the boss");
            Check(shield.GetComponent<TrailRenderer>().startWidth >= KarmaSpace.Units(0.24f),
                "GD03 reflected shield has the stronger gold trail");
            Vector3 atPause = shield.transform.position;
            float clock = enemy.FightSeconds;
            game.TogglePause(); await Task.Delay(80);
            Check(enemy.FightSeconds == clock && shield.transform.position == atPause,
                "GD03 reflected return also freezes during pause");
            game.TogglePause();
            Place(playerAt + Vector2.up * 10);
            await Until(() => enemy.Stunned, 2);
            float expected = game.Config.Techniques.counterDamage;
            Check(Mathf.Abs(bossHealth - enemy.Health - expected) < 0.02f && WholeShield() == null,
                "GD03 reflected impact deals counter damage once with GD01 already broken");
            Check(game.Player.Health == playerHealth && !enemy.AutoShieldActive,
                "GD03 reflected shield cannot damage the player or recreate GD01");
            float stunAt = enemy.FightSeconds;
            await Seconds(0.3f);
            Check(enemy.Stunned, "GD03 return impact holds a real boss stun rather than one frame of recoil");
            await Until(() => !enemy.Stunned, 3);
            Check(enemy.FightSeconds - stunAt >= game.Config.BossTuning.guard.parryStunSeconds - 0.08f,
                "GD03 reflected stun lasts the configured duration");
            await Until(() => !Get<bool>(enemy, "running"), 1);
            Check(Mathf.Abs(bossHealth - enemy.Health - expected) < 0.02f && game.Player.ParryCount == parries + 1,
                "GD03 completes after exactly one counter and one reflected hit");
        }
    }

    static async Task CheckShieldCancellation()
    {
        foreach (string phase in new[] { "outward", "return", "reflected" })
        {
            Reset(new[] { Essence.Guard, Essence.SwordWave });
            Vector2 playerAt = new Vector2(34, KarmaSpace.PlayerHalfHeight);
            Place(playerAt + (phase == "reflected" ? Vector2.zero : Vector2.up * 10));
            Force(EnemyAttack.GD03, 1);
            await Until(() => WholeShield() != null, 2);
            var shield = WholeShield();
            if (phase == "return") await Until(() => shield.name.EndsWith("복귀"), 3);
            if (phase == "reflected")
            {
                await Until(() => Mathf.Abs(shield.transform.position.x - playerAt.x) < 4, 3);
                Set(game.Player, "invulnerableUntil", 0f); game.Player.BeginCounter();
                await Until(() => shield.name.EndsWith("반사"), 1);
            }
            float bossHealth = enemy.Health;
            Call(enemy, "InterruptPattern");
            await Seconds(0.3f);
            Check(WholeShield() == null && Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<TrailRenderer>().Length == 0
                && enemy.Health == bossHealth && !enemy.Stunned && enemy.AutoShieldActive == (phase != "reflected"),
                "GD03 interruption removes all flight and delayed damage in " + phase);
        }
        Reset(new[] { Essence.Guard, Essence.SwordWave });
        Force(EnemyAttack.GD03);
        await Until(() => WholeShield() != null, 2);
        game.PlayerDied(); await Task.Delay(80);
        Check(WholeShield() == null && !Get<bool>(enemy, "running"), "GD03 player death cancels the flying shield");
        Reset(new[] { Essence.Guard, Essence.SwordWave });
        Force(EnemyAttack.GD03);
        await Until(() => WholeShield() != null, 2);
        var effects = Get<Transform>(enemy, "effectsRoot");
        enemy.TakeDamage(enemy.MaxHealth * 4);
        await Task.Delay(80);
        Check(effects == null && game.Enemies.Count == 0, "GD03 boss death clears the shield and its trail");
    }

    static SpriteRenderer SlamFan()
    {
        var root = Get<Transform>(enemy, "effectsRoot").Find("GD04 전방 충격파");
        if (root != null && root.gameObject.activeInHierarchy)
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>())
                if (sr.enabled && sr.name.StartsWith("GD04 지면 상승 ")) return sr;
        return null;
    }
    static async Task CheckGuardShieldAppearance()
    {
        foreach (var order in new[] { new[] { Essence.Guard }, new[] { Essence.Guard, Essence.RisingSlash },
            new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash } })
        {
            Reset(order);
            await Seconds(0.03f);
            var halo = Get<SpriteRenderer>(enemy, "shieldHalo");
            Check(halo.enabled && halo.sprite == KarmaArtwork.Sprite("GuardBossVFX", "Shield"),
                "GD01 uses its restored dedicated image rather than the shared procedural ring");
            Check(Vector3.Distance(halo.transform.localPosition, Vector3.up * KarmaSpace.Units(1.2f)) < 0.001f,
                "GD01 restores the demo's vertical placement");
            var expectedSize = KarmaSpace.Units(new Vector2(4.4f, 3.8f)) * KarmaSpace.GuardianSizeBoost;
            Check(Vector2.Distance(halo.bounds.size, expectedSize) < 0.01f && game.Config.BossTuning.guard.autoShieldPeriod == 8,
                "GD01 restores the demo's dimensions and preserves its current regeneration period");
            float hp = enemy.Health;
            enemy.TakeDamage(100);
            Check(Mathf.Abs(hp - enemy.Health - 100 * game.Config.BossTuning.guard.shieldDamageMultiplier) < 0.02f,
                "GD01 visual restoration preserves damage reduction");
            Call(enemy, "BreakAutoShield"); await Seconds(0.03f);
            Check(!halo.enabled && !enemy.AutoShieldActive, "GD01 image disappears when its shield is broken");
        }
    }
    static async Task CheckStationaryEruptions()
    {
        foreach (int facing in new[] { -1, 1 })
        {
            Reset(new[] { Essence.Guard, Essence.RisingSlash });
            Place(new Vector2(20 + facing * 12, 12));
            Force(EnemyAttack.GD04, facing);
            var warnings = Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<KarmaAttackWarning>();
            Check(warnings.Length == 3, "GD04 announces the three damage footprints");
            for (int i = 1; i < warnings.Length; i++)
                Check(warnings[i].GetComponent<SpriteRenderer>().bounds.size.x > warnings[i - 1].GetComponent<SpriteRenderer>().bounds.size.x
                    && warnings[i].GetComponent<SpriteRenderer>().bounds.size.y > warnings[i - 1].GetComponent<SpriteRenderer>().bounds.size.y,
                    "GD04 announced damage footprints increase in width and height with distance");
            await Until(() => SlamFan() != null, 2);
            var root = Get<Transform>(enemy, "effectsRoot").Find("GD04 전방 충격파");
            var slabs = new List<SpriteRenderer>();
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>())
                if (sr.name.StartsWith("GD04 지면 상승 ")) slabs.Add(sr);
            Check(slabs.Count == 3 && slabs[0].enabled && !slabs[2].enabled,
                "GD04 starts at the nearest fixed tile while the far ground is still inactive");
            for (int i = 1; i < slabs.Count; i++)
                Check(slabs[i].transform.localScale.y > slabs[i - 1].transform.localScale.y,
                    "GD04 keeps the original wide artwork and increases its visual scale with distance");
            float[] xs = slabs.ConvertAll(sr => sr.transform.position.x).ToArray();
            float firstY = slabs[0].transform.position.y;
            await Seconds(0.06f);
            Check(slabs[0].transform.position.y > firstY + 0.2f, "GD04 first tile physically rises from below the floor");
            await Until(() => slabs[2].enabled, 1);
            await Seconds(0.14f);
            Check(slabs.TrueForAll(sr => sr.enabled && Mathf.Abs(sr.color.a - 1) < 0.001f)
                && (slabs[2].transform.parent.position.x - slabs[0].transform.parent.position.x) * facing > 0,
                "GD04 keeps all earlier ground fully raised until the final eruption has risen");
            float[] peaks = slabs.ConvertAll(sr => sr.transform.position.y).ToArray();
            for (int i = 0; i < slabs.Count; i++)
            {
                Check(Mathf.Abs(slabs[i].transform.position.x - xs[i]) < 0.001f, "GD04 ground never travels horizontally");
                var mask = slabs[i].transform.parent.GetComponentInChildren<SpriteMask>();
                Check(mask.transform.lossyScale.x >= KarmaStage.ArenaWidth - 0.01f,
                    "GD04 mask clips the floor and arena walls without chopping the wide sprite into damage tiles");
            }
            await Until(() => slabs[2].color.a < 0.9f, 0.5f);
            for (int i = 0; i < slabs.Count; i++)
                Check(slabs[i].enabled && slabs[i].transform.position.y < peaks[i] - 0.02f
                    && Mathf.Abs(slabs[i].color.a - slabs[2].color.a) < 0.001f,
                    "GD04 all ground sinks and fades together after the final eruption");
            Call(enemy, "InterruptPattern");
            Check(SlamFan() == null, "GD04 interruption clears all masks and stationary eruptions");
        }
    }
    static async Task CheckGroundSlam()
    {
        foreach (var order in new[] {
            new[] { Essence.Guard, Essence.RisingSlash },
            new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash }
        })
        foreach (int facing in new[] { -1, 1 })
        {
            Reset(order);
            Place(enemy.Position + Vector2.right * (facing * 12));
            var animator = Get<Transform>(enemy, "art").GetComponent<KarmaCharacterAnimator>();
            var picture = Get<SpriteRenderer>(animator, "picture");
            string form = order.Length == 3 ? "WardFinal" : "WardRise";
            Force(EnemyAttack.GD04, facing);
            foreach (string pose in new[] { "Windup", "Descent", "Contact", "Rebound" })
            {
                await Until(() => animator.CurrentPose == "GD04/" + pose, 2);
                Check(picture.sprite == KarmaArtwork.Sprite("GuardSlam" + form, pose),
                    "GD04 retains the matching overhead shield slam frames: " + form + "/" + pose);
                if (pose == "Contact") Check(SlamFan() != null, "GD04 creates the fan at shield-floor contact");
            }
            var fan = SlamFan();
            Check(fan.sprite == KarmaArtwork.Sprite("GuardSlamFan", "Impact") && fan.transform.localScale.x * facing > 0,
                "GD04 uses the approved final-rupture ground-break effect and mirrors it with the attack");
            Check(fan.sprite.texture.width == 1672 && fan.sprite.texture.height == 941 && fan.sprite.pivot == new Vector2(1672 * 0.08f, 941 * 0.45f),
                "GD04 imports the new transparent rupture texture while preserving the existing sprite pivot and reference");
            Check(Mathf.Approximately(Mathf.Abs(fan.transform.localScale.x), fan.transform.localScale.y),
                "GD04 rupture slabs preserve their original aspect ratio throughout emergence");
            float floorLocalY = (fan.sprite.rect.height * 0.15f - fan.sprite.pivot.y) / fan.sprite.pixelsPerUnit;
            Check(fan.transform.TransformPoint(new Vector3(0, floorLocalY, 0)).y <= 0.06f
                && fan.maskInteraction == SpriteMaskInteraction.VisibleInsideMask
                && fan.transform.parent.GetComponentInChildren<SpriteMask>() != null,
                "GD04 stone rises through an above-ground mask without exposing the underground image");
            Check(Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<Collider2D>().Length == 0 && Attacks().Length == 0,
                "GD04 creates no obstacle, staircase or generic parryable hitbox");
            Vector3 scale = fan.transform.localScale;
            float clock = enemy.FightSeconds;
            game.TogglePause(); await Task.Delay(80);
            Check(enemy.FightSeconds == clock && fan.transform.localScale == scale, "GD04 pause freezes its pose and fan expansion");
            game.TogglePause();
            Call(enemy, "InterruptPattern");
            Check(SlamFan() == null && Get<int>(animator, "groundSlamFrame") == -1,
                "GD04 interruption clears the effect and motion override");
        }
    }
    static async Task CheckSlamDamageAndAirborne()
    {
        foreach (int facing in new[] { -1, 1 })
        {
            Reset(new[] { Essence.Guard, Essence.RisingSlash });
            Call(enemy, "BreakAutoShield");
            var player = game.Player;
            var body = player.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            Place(new Vector2(20 + facing * (facing < 0 ? 8 : 16), KarmaSpace.PlayerHalfHeight));
            Set(player, "invulnerableUntil", 0f);
            float health = player.Health, bossHealth = enemy.Health;
            int parries = player.ParryCount;
            var animator = Get<Transform>(enemy, "art").GetComponent<KarmaCharacterAnimator>();
            Force(EnemyAttack.GD04, facing);
            await Until(() => animator.CurrentPose == "GD04/Descent", 2);
            await Until(() => animator.CurrentPose == "GD04/Contact", 1);
            await Seconds(0.07f);
            Check(player.Health == health, "GD04 distant target is safe until the visible front arrives");
            player.BeginCounter();
            await Until(() => player.Health < health, 1);
            float damage = game.Config.enemyDamage * game.Config.BossTuning.damageMultiplier
                * game.Config.BossTuning.guard.damageScale * game.Config.BossTuning.guard.slamDamageMultiplier;
            Check(Mathf.Abs(health - player.Health - damage) < 0.02f && player.ParryCount == parries && enemy.Health == bossHealth,
                "GD04 area damage cannot be parried and has one configured hit at near and far distances");
            Check(player.Stunned && body.linearVelocity.y > 0 && player.Airborne,
                "GD04 hit interrupts the player and applies real upward velocity");
            await Seconds(0.12f);
            Check(player.Position.y > KarmaSpace.PlayerHalfHeight + 0.5f, "GD04 physically lifts the player above the flat floor");
            Check(!enemy.AutoShieldActive, "GD04 damage does not recreate GD01 under the chosen existing-period rule");
            health = player.Health;
            await Until(() => !Get<bool>(enemy, "running"), 4);
            Check(player.Health == health && SlamFan() == null, "GD04 has no repeat hit, heat damage or lingering effect");
        }
        foreach (string protection in new[] { "guard", "invulnerability", "jump", "out of range" })
        {
            Reset(new[] { Essence.Guard, Essence.RisingSlash });
            Place(new Vector2(30, protection == "jump" ? 12 : KarmaSpace.PlayerHalfHeight));
            Set(game.Player, "invulnerableUntil", 0f);
            if (protection == "guard") game.Player.BeginGuard(2);
            if (protection == "invulnerability") Set(game.Player, "invulnerableUntil", Time.time + 2);
            if (protection == "out of range") Place(new Vector2(48, KarmaSpace.PlayerHalfHeight));
            float health = game.Player.Health;
            Force(EnemyAttack.GD04, 1);
            await Until(() => !Get<bool>(enemy, "running"), 4);
            Check(game.Player.Health == health && !game.Player.Stunned,
                "GD04 preserves existing defenses and misses a clear dodge: " + protection);
        }
        foreach (int facing in new[] { -1, 1 })
        {
            Reset(new[] { Essence.Guard, Essence.RisingSlash });
            Place(new Vector2(20 + facing * 8, KarmaSpace.PlayerHalfHeight));
            var player = game.Player;
            var body = player.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            Set(player, "invulnerableUntil", 0f);
            float health = player.Health;
            var animator = Get<Transform>(enemy, "art").GetComponent<KarmaCharacterAnimator>();
            Force(EnemyAttack.GD04, facing);
            await Until(() => animator.CurrentPose == "GD04/Descent", 2);
            Set(player, "jumpBufferedUntil", Time.time + 0.12f);
            await Until(() => player.Airborne, 0.5f);
            await Until(() => animator.CurrentPose == "GD04/Rebound", 1);
            await Seconds(0.5f);
            Check(player.Health == health && !player.Stunned,
                "GD04 near and middle eruptions can be cleared by the normal physics jump in either direction");
        }
    }
    static async Task CheckFarEruptionJump()
    {
        float jumpPeak = game.Config.jumpSpeed * game.Config.jumpSpeed / (2 * Mathf.Abs(Physics2D.gravity.y) * game.Config.gravityScale);
        Check(game.Config.BossTuning.guard.slamFinalHeight > jumpPeak + 0.5f,
            "GD04 saved far damage height exceeds the ordinary jump apex");
        foreach (int facing in new[] { -1, 1 })
        {
            Reset(new[] { Essence.Guard, Essence.RisingSlash });
            var player = game.Player;
            var body = player.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Dynamic;
            Place(new Vector2(20 + facing * 15, KarmaSpace.PlayerHalfHeight));
            Set(player, "invulnerableUntil", 0f);
            float health = player.Health;
            Force(EnemyAttack.GD04, facing);
            var warnings = Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<KarmaAttackWarning>();
            Check(warnings.Length == 3, "GD04 announces each differently sized damage footprint before the slam");
            await Until(() => SlamFan() != null, 2);
            await Seconds(0.23f);
            Set(player, "jumpBufferedUntil", Time.time + 0.12f);
            await Until(() => player.Airborne, 0.5f);
            await Until(() => player.Health < health, 1.5f);
            Check(player.GetComponent<Collider2D>().bounds.min.y > jumpPeak * 0.85f && player.Stunned,
                "GD04 final rupture catches a real normal jump even near its apex: " + facing);
            await Until(() => !Get<bool>(enemy, "running"), 2);
        }
        // A nearby arena wall shortens the range, so it must not create a tall far-distance eruption beside the boss.
        Reset(new[] { Essence.Guard, Essence.RisingSlash });
        enemy.transform.position = new Vector3(47, KarmaSpace.GuardianGroundY, 0);
        Place(new Vector2(49, 12)); Force(EnemyAttack.GD04, 1);
        var closeWarnings = Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<KarmaAttackWarning>();
        foreach (var warning in closeWarnings)
            Check(warning.GetComponent<SpriteRenderer>().bounds.size.y < game.Config.BossTuning.guard.slamFinalHeight * 0.6f,
                "Short wall-side attacks retain close-range heights");
        Call(enemy, "InterruptPattern");
    }

    static async Task CheckSlamRearChain()
    {
        foreach (var order in new[] {
            new[] { Essence.Guard, Essence.RisingSlash },
            new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash }
        })
        foreach (int facing in new[] { -1, 1 })
        {
            Reset(order);
            Place(enemy.Position + new Vector2(facing * 4, -0.3f));
            Force(EnemyAttack.GD04, facing);
            await Until(() => SlamFan() != null, 2);
            var root = Get<Transform>(enemy, "effectsRoot").Find("GD04 전방 충격파");
            var slabs = new List<SpriteRenderer>();
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>())
                if (sr.name.StartsWith("GD04 지면 상승 ")) slabs.Add(sr);
            Place(enemy.Position + new Vector2(-facing * 4, -0.3f));
            Set(game.Player, "invulnerableUntil", 0f);
            await Seconds(0.3f);
            Check(Get<EnemyAttack>(enemy, "selected") == EnemyAttack.GD04 && root.gameObject.activeInHierarchy
                && !enemy.HeavyParryWindowOpen, "GD04 rear movement cannot interrupt the ground sequence");
            await Until(() => slabs[2].enabled, 1);
            await Seconds(0.14f);
            Check(slabs.TrueForAll(sr => sr.enabled) && Get<EnemyAttack>(enemy, "selected") == EnemyAttack.GD04,
                "The final ground eruption completes before the rear heavy is selected");
            await Until(() => enemy.HeavyParryWindowOpen, 2);
            Check(!root.gameObject.activeInHierarchy, "GD04 ground has lowered before the chained GD02 begins");
            Check(Get<EnemyAttack>(enemy, "selected") == EnemyAttack.GD02 && enemy.Facing == -facing
                && Get<EnemyAttack>(enemy, "previous") == EnemyAttack.GD02 && enemy.AutoShieldActive,
                "GD04 rear dodge with GD01 chains GD02 only after the complete ground sequence");
            float health = game.Player.Health;
            int parries = game.Player.ParryCount;
            game.Player.Essences.TryCounter();
            await Until(() => enemy.Stunned, 2);
            Check(game.Player.Health == health && game.Player.ParryCount == parries + 1 && !enemy.AutoShieldActive,
                "The chained GD02 retains its shield-breaking parry and boss stun");
        }
        foreach (string scenario in new[] { "no shield", "front", "far rear", "returned front", "shield broken" })
        {
            Reset(new[] { Essence.Guard, Essence.RisingSlash });
            if (scenario == "no shield") Call(enemy, "BreakAutoShield");
            Place(enemy.Position + new Vector2(4, -0.3f));
            Force(EnemyAttack.GD04, 1);
            await Until(() => SlamFan() != null, 2);
            if (scenario != "front") Place(enemy.Position + new Vector2(scenario == "far rear" ? -20 : -4, -0.3f));
            if (scenario == "returned front" || scenario == "shield broken")
            {
                await Seconds(0.3f);
                if (scenario == "returned front") Place(enemy.Position + new Vector2(4, -0.3f));
                else Call(enemy, "BreakAutoShield");
            }
            await Until(() => !Get<bool>(enemy, "running"), 4);
            Check(Get<EnemyAttack>(enemy, "selected") == EnemyAttack.GD04,
                "GD04 must not force GD02 outside the original rear-chain condition: " + scenario);
        }
        Reset(new[] { Essence.Guard, Essence.RisingSlash });
        Place(enemy.Position + new Vector2(8, -0.3f)); Force(EnemyAttack.GD04, 1);
        await Until(() => SlamFan() != null, 2);
        Place(enemy.Position + new Vector2(-4, -0.3f));
        Call(enemy, "InterruptPattern"); await Seconds(1.2f);
        Check(SlamFan() == null && !enemy.HeavyParryWindowOpen, "GD04 cancellation leaves no delayed knock-up or heavy chain");
        Reset(new[] { Essence.Guard, Essence.RisingSlash });
        Force(EnemyAttack.GD04);
        await Until(() => SlamFan() != null, 2);
        game.PlayerDied(); await Task.Delay(80);
        Check(SlamFan() == null && !Get<bool>(enemy, "running"), "GD04 death clears its effect and sequence");
    }
    static readonly List<(Texture2D texture, string path)> capturedFrames = new List<(Texture2D, string)>();
    static async Task CaptureGameFrame(string filename, bool airborne = false)
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        enemy.StartCoroutine(CaptureAtFrameEnd(filename, airborne, done));
        Check(await Task.WhenAny(done.Task, Task.Delay(5000)) == done.Task, "Game view must render for capture: " + filename);
        await done.Task;
    }
    static IEnumerator CaptureAtFrameEnd(string filename, bool airborne, TaskCompletionSource<bool> done)
    {
        yield return new WaitForEndOfFrame();
        if (airborne)
        {
            var art = Get<Transform>(game.Player, "art");
            while (game.Player.Airborne && !art.gameObject.activeInHierarchy) yield return new WaitForEndOfFrame();
        }
        try
        {
            if (airborne) Check(game.Player.Airborne && Get<Transform>(game.Player, "art").gameObject.activeInHierarchy,
                "Airborne frame must contain the visible player during the actual physical launch");
            var texture = ScreenCapture.CaptureScreenshotAsTexture(2);
            Check(texture != null, "Actual Game view texture captured: " + filename);
            capturedFrames.Add((texture, "output/validation/gd04-eruption-preview/" + filename + ".png"));
            done.SetResult(true);
        }
        catch (Exception error) { done.SetException(error); }
    }
    static async Task CaptureSlamPreview(float authoredPlayerHealth)
    {
        float testHealth = game.Config.playerHealth;
        game.Config.playerHealth = authoredPlayerHealth;
        capturedFrames.Clear();
        try
        {
            Reset(new[] { Essence.Guard, Essence.RisingSlash });
            var player = game.Player;
            player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
            Place(new Vector2(31, KarmaSpace.PlayerHalfHeight)); Set(player, "invulnerableUntil", 0f);
            var animator = Get<Transform>(enemy, "art").GetComponent<KarmaCharacterAnimator>();
            await Seconds(0.04f);
            await CaptureGameFrame("00-restored-shield");
            Force(EnemyAttack.GD04, 1);
            await Seconds(0.2f);
            await CaptureGameFrame("01-windup");
            await Until(() => SlamFan() != null, 2);
            await Seconds(0.20f);
            await CaptureGameFrame("02-near-eruption");
            // Restart between phases so GPU readback cannot skip the next eruption.
            Reset(new[] { Essence.Guard, Essence.RisingSlash });
            player = game.Player;
            player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
            Place(new Vector2(31, KarmaSpace.PlayerHalfHeight)); Set(player, "invulnerableUntil", 0f);
            Force(EnemyAttack.GD04, 1);
            await Until(() => SlamFan() != null, 2);
            await Seconds(0.42f);
            await CaptureGameFrame("03-middle-eruptions");
            await Until(() => player.Stunned && player.Airborne, 1);
            await Seconds(0.08f);
            await Until(() => Get<Transform>(player, "art").gameObject.activeInHierarchy, 0.3f);
            await CaptureGameFrame("04-airborne", true);
            Check(player.Airborne && player.Position.y > KarmaSpace.PlayerHalfHeight + 0.5f,
                "Preview captures real player knock-up, not a staged sprite move");
            Reset(new[] { Essence.Guard, Essence.RisingSlash });
            Place(new Vector2(31, KarmaSpace.PlayerHalfHeight));
            Force(EnemyAttack.GD04, 1);
            await Until(() => SlamFan() != null, 2);
            var eruptionRoot = Get<Transform>(enemy, "effectsRoot").Find("GD04 전방 충격파");
            SpriteRenderer farSlab = null;
            foreach (var sr in eruptionRoot.GetComponentsInChildren<SpriteRenderer>())
                if (sr.name.StartsWith("GD04 지면 상승 ")) farSlab = sr;
            await Until(() => farSlab.enabled, 1);
            await Seconds(0.14f);
            await CaptureGameFrame("05-far-eruption");
            Reset(new[] { Essence.Guard, Essence.RisingSlash });
            Place(new Vector2(31, KarmaSpace.PlayerHalfHeight)); Force(EnemyAttack.GD04, 1);
            await Until(() => SlamFan() != null, 2);
            eruptionRoot = Get<Transform>(enemy, "effectsRoot").Find("GD04 전방 충격파");
            foreach (var sr in eruptionRoot.GetComponentsInChildren<SpriteRenderer>())
                if (sr.name.StartsWith("GD04 지면 상승 ")) farSlab = sr;
            await Until(() => farSlab.enabled, 1);
            await Until(() => farSlab.color.a < 0.7f, 0.8f);
            await CaptureGameFrame("05b-all-lowering");
            Reset(new[] { Essence.Guard, Essence.RisingSlash });
            Place(enemy.Position + new Vector2(4, -0.3f)); Force(EnemyAttack.GD04, 1);
            await Until(() => SlamFan() != null, 2);
            Place(enemy.Position + new Vector2(-4, -0.3f));
            await Until(() => enemy.HeavyParryWindowOpen, 2);
            await CaptureGameFrame("06-rear-gd02-chain");
        }
        finally
        {
            game.Config.playerHealth = testHealth;
            Directory.CreateDirectory("output/validation/gd04-eruption-preview");
            // Encode only after collecting the live frames so disk/GPU waits cannot advance past the knock-up.
            foreach (var frame in capturedFrames)
            {
                File.WriteAllBytes(frame.path, frame.texture.EncodeToPNG());
                UnityEngine.Object.Destroy(frame.texture);
            }
            capturedFrames.Clear();
        }
    }

    static async Task CheckPersistentShield()
    {
        var orders = new[] {
            new[] { Essence.Guard }, new[] { Essence.Guard, Essence.SwordWave }, new[] { Essence.Guard, Essence.RisingSlash },
            new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash }, new[] { Essence.Guard, Essence.RisingSlash, Essence.SwordWave }
        };
        foreach (var order in orders)
        {
            Reset(order);
            Check(enemy.AutoShieldActive && Get<SpriteRenderer>(enemy, "shieldHalo").enabled,
                "GD01 starts active and visible in " + KarmaPatternCatalog.FusionSummary(order));
            Place(new Vector2(5, KarmaSpace.Units(1)));
            Force(EnemyAttack.GD02);
            await Until(() => !Get<bool>(enemy, "running"), 3);
            Check(enemy.AutoShieldActive, "dodging GD02 preserves GD01 through recovery and pattern completion");
        }
        foreach (var primary in new[] { Essence.SwordWave, Essence.RisingSlash })
        {
            Reset(new[] { primary });
            Check(!enemy.AutoShieldActive, "GD01 is not granted to " + primary + " primary bosses");
        }
        Reset();
        Call(enemy, "InterruptPattern");
        Check(enemy.AutoShieldActive, "generic interruption cannot break GD01");
        Set(enemy, "running", true);
        await Seconds(6);
        Check(enemy.AutoShieldActive, "GD01 has no five-second expiration or expiration countdown");
        float health = enemy.Health;
        enemy.TakeDamage(100, enemy.Position + Vector2.right);
        Check(Mathf.Abs(health - enemy.Health - 100 * game.Config.BossTuning.guard.shieldDamageMultiplier) < 0.01f,
            "persistent GD01 still applies damage reduction");
        Force(EnemyAttack.GD04);
        Check(enemy.AutoShieldActive, "GD04 entry cannot break GD01");
        await Until(() => !Get<bool>(enemy, "running"), 6);
        Check(enemy.AutoShieldActive, "GD04 recovery cannot break GD01");
        Reset();
        Set(enemy, "nextUltimate", 0f);
        var selected = new HashSet<EnemyAttack>();
        for (int i = 0; i < 10; i++)
        {
            Call(enemy, "BeginAttack"); selected.Add(Get<EnemyAttack>(enemy, "selected"));
            Call(enemy, "InterruptPattern");
        }
        Check(selected.Contains(EnemyAttack.GD02) && selected.Contains(EnemyAttack.GD03)
            && selected.Contains(EnemyAttack.GD04) && !selected.Contains(EnemyAttack.GD05),
            "persistent GD01 does not force GD02 forever or starve inherited attacks");
        game.PlayerDied();
        Check(!enemy.AutoShieldActive && !Get<SpriteRenderer>(enemy, "shieldHalo").enabled,
            "ending combat cleans up GD01 state and visual");
    }

    static async Task CheckGuardDome()
    {
        Reset();
        Set(enemy, "nextUltimate", 0f);
        Call(enemy, "BeginAttack");
        Check(Get<EnemyAttack>(enemy, "selected") != EnemyAttack.GD05, "GD05 cannot roll at high health");
        Call(enemy, "InterruptPattern");

        Place(new Vector2(33, KarmaSpace.PlayerHalfHeight));
        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.5f);
        Call(enemy, "BeginAttack");
        Check(Get<EnemyAttack>(enemy, "selected") == EnemyAttack.GD05 && enemy.Invulnerable,
            "GD05 takes priority at 50 percent and protects the boss during room formation");
        await Until(() => enemy.TrialActive, 3);
        var player = game.Player;
        var effects = Get<Transform>(enemy, "effectsRoot");
        var shield = effects.Find("GD05 튕기는 방패");
        var dome = UnityEngine.Object.FindAnyObjectByType<KarmaGuardDome>();
        Check(shield != null && shield.GetComponent<SpriteRenderer>().sprite == KarmaArtwork.Sprite("GuardDomeShield", "Ricochet")
            && dome != null && dome.GetComponent<EdgeCollider2D>().pointCount == 65
            && Get<bool>(player, "confined") && enemy.Invulnerable, "GD05 forms the dome and keeps the boss invulnerable");
        float protectedHp = enemy.Health;
        enemy.TakeDamage(enemy.MaxHealth);
        Check(enemy.Health == protectedHp && !enemy.Stunned, "GD05 room rejects lethal incoming attacks");
        float startX = enemy.Position.x;
        await Until(() => enemy.HeavyParryWindowOpen, 3);
        Check(enemy.Position.x > startX + 2 && enemy.Facing == 1, "GD05 pursues the player before starting the heavy strike");
        enemy.TakeDamage(enemy.MaxHealth, player.Position);
        Check(enemy.Health == protectedHp && enemy.Invulnerable, "GD05 chasing heavy remains invulnerable");
        float bossHp = enemy.Health, playerHp = player.Health;
        int parries = player.ParryCount;
        Vector3 shieldAt = shield.position;
        float clock = enemy.FightSeconds;
        game.TogglePause(); await Task.Delay(180);
        Check(enemy.FightSeconds == clock && shield.position == shieldAt, "GD05 pause freezes both the shield and heavy strike clock");
        game.TogglePause();
        player.Essences.TryCounter();
        await Until(() => enemy.Stunned, 3);
        Check(player.ParryCount == parries + 1 && player.Health == playerHp && enemy.Health == bossHp
            && enemy.TrialActive && !Get<bool>(enemy, "pendingParry"), "GD05 heavy parry deals zero damage to both and keeps the room alive");
        enemy.TakeDamage(enemy.MaxHealth);
        Check(enemy.Health == bossHp && enemy.Invulnerable && enemy.Stunned, "GD05 parry stun allows no boss health damage");
        player.BeginGuard(100); // Keep this geometry/clean-survival scenario independent of later incidental hits.
        shieldAt = shield.position;
        float stunX = enemy.Position.x;
        await Seconds(0.25f);
        Check(enemy.Stunned && Mathf.Approximately(enemy.Position.x, stunX) && Vector3.Distance(shield.position, shieldAt) > 1,
            "GD05 shield continues flying while only the boss is stunned");
        await Until(() => !enemy.Stunned, 2);

        const float physicalRadius = 1.8f;
        bool reversedX = false, reversedY = false, inside = true;
        Vector2 previous = shield.position, previousDelta = Vector2.zero;
        double deadline = EditorApplication.timeSinceStartup + 14;
        while (enemy.TrialActive && EditorApplication.timeSinceStartup < deadline)
        {
            Vector2 position = shield.position;
            Vector2 delta = position - previous;
            if (delta.sqrMagnitude > 0.000001f)
            {
                reversedX |= delta.x * previousDelta.x < 0;
                reversedY |= delta.y * previousDelta.y < 0;
                previousDelta = delta;
                previous = position;
            }
            inside &= position.y >= physicalRadius - 0.025f
                && (position - (Vector2)dome.transform.position).magnitude <= dome.Radius - physicalRadius + 0.025f;
            await Task.Delay(10);
        }
        Check(inside && reversedX && reversedY, "GD05 shield stays inside the floor and curved roof and reflects on both axes");
        Check(!enemy.TrialActive && !Get<bool>(player, "confined") && enemy.Invulnerable, "GD05 protects the boss until dome dissolution finishes: active=" + enemy.TrialActive
            + ", confined=" + Get<bool>(player, "confined") + ", invulnerable=" + enemy.Invulnerable);
        await Until(() => enemy.Stunned && !enemy.Invulnerable, 2);
        float opening = enemy.FightSeconds, openingHp = enemy.Health;
        enemy.TakeDamage(1);
        Check(enemy.Health < openingHp && !enemy.AutoShieldActive && Get<bool>(enemy, "running"),
            "GD05 successful survival opens a genuinely vulnerable damage window without GD01");
        await Until(() => !Get<bool>(enemy, "running"), 5);
        Check(enemy.FightSeconds - opening >= game.Config.BossTuning.guard.trialSuccessStunSeconds - 0.1f,
            "GD05 success holds the configured four-second damage window");
        Check(!enemy.Invulnerable && !enemy.Stunned, "GD05 success completes with normal boss vulnerability");
        Check(shield == null && UnityEngine.Object.FindAnyObjectByType<KarmaGuardDome>() == null, "GD05 removes the projectile and dome after the phase");

        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.3f);
        Call(enemy, "BeginAttack");
        Check(Get<EnemyAttack>(enemy, "selected") != EnemyAttack.GD05, "GD05 cannot repeat above the second threshold");
        Call(enemy, "InterruptPattern");
        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.25f);
        Call(enemy, "BeginAttack");
        Check(Get<EnemyAttack>(enemy, "selected") == EnemyAttack.GD05, "GD05 takes priority again at 25 percent despite its cooldown");
        Call(enemy, "InterruptPattern");
        Call(enemy, "BeginAttack");
        Check(Get<EnemyAttack>(enemy, "selected") != EnemyAttack.GD05, "GD05 is exhausted after two uses");

        Reset(new[] { Essence.Guard });
        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.2f);
        Call(enemy, "BeginAttack");
        Check(Get<EnemyAttack>(enemy, "selected") != EnemyAttack.GD05, "GD05 remains a final-form attack");

        Reset();
        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.2f);
        for (int i = 0; i < 3; i++)
        {
            Call(enemy, "BeginAttack");
            Check((Get<EnemyAttack>(enemy, "selected") == EnemyAttack.GD05) == (i < 2),
                "GD05 records both thresholds when one hit crosses both");
            Call(enemy, "InterruptPattern");
        }

        Reset();
        Place(new Vector2(25, KarmaSpace.Units(1.65f)));
        Set(game.Player, "invulnerableUntil", 0f);
        Force(EnemyAttack.GD05);
        playerHp = game.Player.Health;
        await Until(() => game.Player.Health < playerHp, 4);
        float damage = game.Config.enemyDamage * game.Config.BossTuning.damageMultiplier * game.Config.BossTuning.guard.damageScale
            * game.Config.BossTuning.guard.trialDamageMultiplier;
        Check(Mathf.Abs(playerHp - game.Player.Health - damage) < 0.02f, "GD05 flying shield deals its own contact damage");
        game.PlayerDied();
        Check(!enemy.TrialActive && !Get<bool>(game.Player, "confined") && Attacks().Length == 0,
            "GD05 death clears the room and frees the player");
        Check(!KarmaGuardGeometry.SweptCircle(1.8f, 1.8f, 1.8f, 1.8f, 1, 0, 0, 1, 1)
            && KarmaGuardGeometry.SweptCircle(-3, 1.5f, 3, 1.5f, 1, 0, 0, 1, 1),
            "GD05 round sweep rejects square corners and detects a fast crossing");
    }


    static async Task CheckAttackContinuations()
    {
        foreach (var pattern in new[] { EnemyAttack.GD03, EnemyAttack.GD04 })
        foreach (int facing in new[] { -1, 1 })
        foreach (bool shielded in new[] { false, true })
        {
            Reset();
            if (!shielded) Call(enemy, "BreakAutoShield");
            Place(new Vector2(20 + facing * 12, 12));
            var animator = Get<Transform>(enemy, "art").GetComponent<KarmaCharacterAnimator>();
            animator.GuardianPose("Recover", pattern, false, shielded);
            Check(animator.CurrentPose == (shielded ? "Guard" : "Idle"), "Guard miss has no false recoil: " + pattern);
            Force(pattern, facing);
            bool recovered = false, staggered = false;
            float attackAt = enemy.FightSeconds;
            await Until(() =>
            {
                recovered |= State() == "Recover" || animator.CurrentPose == "Recover";
                staggered |= enemy.Stunned;
                return !Get<bool>(enemy, "running");
            }, 7);
            Check(!recovered && !staggered, "Unparried GD03/GD04 ends without recovery or stun: " + pattern);
            Check(Get<float>(enemy, "timer") <= 0.221f, "GD03/GD04 uses only the host minimum decision interval");
            if (pattern == EnemyAttack.GD04)
                Check(enemy.FightSeconds - attackAt < 2.1f, "GD04 finishes after the last ground eruption without an extra recovery wait");
            await Until(() => Get<bool>(enemy, "running"), 0.8f);
            Check(State() == "Telegraph" || State() == "Attack", "A normal next attack starts promptly after GD03/GD04");
        }
    }

    static async Task CheckTrialOutcomes()
    {
        var settings = game.Config.BossTuning.guard;
        float baseDamage = game.Config.enemyDamage * game.Config.BossTuning.damageMultiplier * settings.damageScale;
        Check(settings.trialDuration == 9 && settings.trialFailureHits == 3
            && settings.trialExplosionDamageMultiplier == 1 && settings.trialSuccessStunSeconds == 4,
            "GD05 uses the approved saved nine-second, three-hit, one-hit blast and four-second opening settings");
        settings.trialDuration = 2;
        try
        {
            foreach (int hits in new[] { 0, 2, 3, 4 })
            {
                Reset();
                Place(new Vector2(20, 18));
                var player = game.Player;
                player.BeginGuard(100);
                Force(EnemyAttack.GD05);
                await Until(() => enemy.TrialActive, 3);
                var trial = Get<KarmaTrialState>(Pattern("GD05"), "trial");
                Check(trial.Hits == 0, "Every GD05 room starts with a fresh hit count");
                Vector2 at = player.Position;
                var ignored = new KarmaAttackContact();
                Set(player, "guardUntil", 0f);
                Set(player, "invulnerableUntil", Time.time + 10);
                Call(enemy, "TryGuardCircleHit", at, at, 1f, baseDamage, ignored);
                Set(player, "invulnerableUntil", 0f);
                player.BeginGuard(100);
                Call(enemy, "TryGuardCircleHit", at, at, 1f, baseDamage, new KarmaAttackContact());
                Set(player, "guardUntil", 0f);
                player.ToggleTestInvincibility();
                Call(enemy, "TryGuardCircleHit", at, at, 1f, baseDamage, new KarmaAttackContact());
                player.ToggleTestInvincibility();
                player.BeginCounter();
                enemy.TryAttackHit(at, at, Vector2.one, baseDamage, new KarmaAttackContact(parryWithoutRiposte: true), 0,
                    sourcePattern: EnemyAttack.GD02);
                Check(trial.Hits == 0 && enemy.Invulnerable, "Guard, invulnerability, test invulnerability and a heavy parry add no trial hits");
                player.BeginGuard(100);
                await Seconds(0.5f);
                if (hits >= 3) await Seconds(1.55f);
                Set(player, "guardUntil", 0f);
                for (int i = 0; i < hits; i++)
                {
                    Set(player, "invulnerableUntil", 0f);
                    at = player.Position;
                    var contact = new KarmaAttackContact();
                    float health = player.Health;
                    if (i % 2 == 0)
                        Call(enemy, "TryGuardCircleHit", at, at, 1f, baseDamage, contact);
                    else enemy.TryAttackHit(at, at, Vector2.one, baseDamage, contact, 0, sourcePattern: EnemyAttack.GD02);
                    Check(player.Health < health && trial.Hits == i + 1, "Actual shield and heavy damage each add exactly one trial hit");
                    Call(enemy, "TryGuardCircleHit", at, at, 1f, baseDamage, contact);
                    Check(trial.Hits == i + 1, "A consumed contact cannot count twice");
                }
                player.BeginGuard(100);
                float healthBeforeEnding = player.Health;
                await Until(() => !enemy.TrialActive, 4);
                Check(trial.Finished && trial.Hits == hits && trial.Passed == (hits < 3), "GD05 outcome uses the strict three-hit boundary");
                if (hits < 3)
                {
                    await Until(() => enemy.Stunned && !enemy.Invulnerable, 2);
                    float hp = enemy.Health;
                    enemy.TakeDamage(1);
                    Check(enemy.Health < hp && player.Health == healthBeforeEnding && !Get<bool>(player, "confined"),
                        "Zero/two hits produce a damage opening and no explosion penalty");
                    float clock = enemy.FightSeconds;
                    game.TogglePause(); await Task.Delay(100);
                    Check(enemy.FightSeconds == clock && enemy.Stunned, "Pause freezes the success opening");
                    game.TogglePause();
                    if (hits == 0)
                    {
                        enemy.TakeDamage(enemy.MaxHealth * 4);
                        await Task.Delay(30);
                        Check(game.Enemies.Count == 0, "Boss can be defeated during the earned trial opening");
                    }
                    else
                    {
                        Call(enemy, "InterruptPattern");
                        Check(!enemy.Invulnerable && !Get<bool>(enemy, "running") && !trial.Active,
                            "Interrupting a success opening clears the trial and invulnerability");
                    }
                }
                else
                {
                    Check(enemy.Invulnerable && Get<bool>(player, "confined") && State() == "Telegraph"
                        && player.Health == healthBeforeEnding, "Failure stops the attacks and announces a blast before applying damage");
                    int parries = player.ParryCount;
                    await Until(() => player.Health < healthBeforeEnding, 2);
                    Check(Mathf.Abs(healthBeforeEnding - player.Health - baseDamage * settings.trialExplosionDamageMultiplier) < 0.02f
                        && player.ParryCount == parries && !enemy.Stunned, "Failure explosion deals one extra basic-hit amount and grants no boss stun");
                    var dome = UnityEngine.Object.FindAnyObjectByType<KarmaGuardDome>();
                    Check(dome != null && Get<bool>(dome, "exploding"), "Failure visually explodes the room shell");
                    float afterExplosion = player.Health;
                    await Until(() => !Get<bool>(enemy, "running"), 3);
                    Check(!enemy.Invulnerable && !Get<bool>(player, "confined") && player.Health == afterExplosion,
                        "Failure ends without duplicate blast damage, confinement or leftover invulnerability: hp=" + player.Health
                        + ", afterBlast=" + afterExplosion + ", invulnerable=" + enemy.Invulnerable + ", confined=" + Get<bool>(player, "confined"));
                }
            }
            Reset(); Place(new Vector2(20, 18));
            game.Player.BeginGuard(100);
            Force(EnemyAttack.GD05);
            await Until(() => enemy.TrialActive, 3);
            var pending = Get<KarmaTrialState>(Pattern("GD05"), "trial");
            for (int i = 0; i < 3; i++) pending.Hit();
            await Until(() => !enemy.TrialActive, 4);
            float safeHp = game.Player.Health;
            Call(enemy, "InterruptPattern");
            await Seconds(1.1f);
            Check(game.Player.Health == safeHp && !pending.Active && !Get<bool>(game.Player, "confined")
                && UnityEngine.Object.FindAnyObjectByType<KarmaGuardDome>() == null,
                "Interrupting the failure tell cancels the delayed blast and releases the player");
        }
        finally { settings.trialDuration = 9; }
    }

    static void Reset(Essence[] order = null)
    {
        game.StartRun();
        order = order ?? new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash };
        foreach (var essence in order) { game.Player.Essences.Preview(essence); game.Player.Essences.Confirm(out _); }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, order.Length);
        Call(game, "LoadRoom", true);
        enemy = game.Enemies[0];
        enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
        game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        Place(new Vector2(13, KarmaSpace.Units(1)));
        Set(game.Player, "invulnerableUntil", Time.time + 100);
        Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue); Set(enemy, "nextShield", float.MaxValue);
    }
    static void Place(Vector2 p) { game.Player.transform.position = p; game.Player.GetComponent<Rigidbody2D>().position = p; }
    static void Force(EnemyAttack pattern, int facing = -1)
    {
        Call(enemy, "InterruptPattern");
        Set(enemy, "selected", pattern); Set(enemy, "direction", (float)facing); Set(enemy, "running", true);
        Set(enemy, "patternStarted", enemy.FightSeconds);
        if (pattern == EnemyAttack.GD02) Call(enemy, "RefreshAutoShield");
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static string State() { return Get<object>(enemy, "state").ToString(); }
    static KarmaBossAttack[] Attacks() { return Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<KarmaBossAttack>(); }
    static Task Seconds(float seconds)
    { float target = enemy.FightSeconds + seconds; return Until(() => enemy.FightSeconds >= target, seconds + 3); }
    static async Task Until(Func<bool> condition, float timeout)
    {
        double until = UnityEditor.EditorApplication.timeSinceStartup + timeout;
        while (!condition())
        {
            if (UnityEditor.EditorApplication.timeSinceStartup > until) throw new Exception("Guard check timed out: pattern=" + Get<EnemyAttack>(enemy, "selected") + ", state=" + State());
            await Task.Delay(10);
        }
    }
}
#endif
