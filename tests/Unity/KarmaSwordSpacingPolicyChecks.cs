#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KarmaPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Isolated Play Mode policy fixtures, NOT a real-player or actual-scene report.
// Reflection deliberately controls actor positions, AI state and the combat
// clock to cover boundaries. Sprite geometry, J damage, production Update,
// pattern coroutines, recovery and selection methods remain the real code.
[InitializeOnLoad]
public static class KarmaSwordSpacingPolicyChecks
{
    const string ActiveKey = "K4RMA.SwordSpacingPolicy.Active";
    const string ExitKey = "K4RMA.SwordSpacingPolicy.Exit";
    const string ExitCodeKey = "K4RMA.SwordSpacingPolicy.ExitCode";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const float Step = 0.05f, Epsilon = 0.0002f;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static KarmaPlayer player;
    static KarmaCharacterAnimator animator;
    static Transform art;
    static Rigidbody2D body;
    static IEnumerator suite;
    static Report report;
    static bool finishing;
    static double deadline;
    static string section, runtimeError;

    [Serializable] sealed class GeometryRow
    {
        public int playerSide;
        public float damageBefore, damageAfter, actualJDamageAt8p25;
        public float expectedMoveBoundary, lastOverlappingGap, firstNonoverlappingGap;
        public Bounds incomingDamageBounds;
    }
    [Serializable] sealed class PolicyRow
    {
        public string name, note;
        public float startGap, endGap, bossTravel, elapsed, pressureTarget, nextRetreat;
        public int frames;
    }
    [Serializable] sealed class Report
    {
        public string result, utc, error;
        public string scope = "Isolated reflection-controlled Play Mode policy fixtures. Not actual-scene gameplay evidence and not proof of player-perceived ranged combat.";
        public string method = "Real KarmaGame/Player/Enemy initialization with SwordWave inheritance; original sprites, attack damage and tuning. Fixtures explicitly place actors and set private AI fields/combat clocks. Production J attack, DamageBounds overlap, Enemy.Update, RunPattern/Recovery, ground movement, interruption and attack selection execute unchanged. A kinematic stationary player separates policy checks from input, pursuit and camera observations.";
        public int assertions, headlessViewWarnings;
        public List<GeometryRow> geometry = new List<GeometryRow>();
        public List<PolicyRow> policy = new List<PolicyRow>();
    }

    static KarmaSwordSpacingPolicyChecks()
    {
        EditorApplication.playModeStateChanged += OnState;
        if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Pump;
        if (SessionState.GetBool(ExitKey, false)) EditorApplication.delayCall += ExitEditor;
    }
    public static void Run()
    {
        SessionState.SetBool(ActiveKey, true); SessionState.SetBool(ExitKey, false);
        Application.runInBackground = true;
        EditorApplication.update -= Pump; EditorApplication.update += Pump;
        EditorApplication.delayCall += () => { EditorApplication.delayCall += BeginPlay; };
    }
    static void BeginPlay()
    {
        if (!SessionState.GetBool(ActiveKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isUpdating || EditorApplication.isCompiling)
        { EditorApplication.delayCall += BeginPlay; return; }
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Finish(error); }
    }
    static void Pump()
    {
        EditorApplication.isPaused = false;
        Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
    }
    static void OnState(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            if (SessionState.GetBool(ExitKey, false)) EditorApplication.delayCall += ExitEditor;
            else if (SessionState.GetBool(ActiveKey, false)) Finish(new Exception("Policy fixture Play Mode ended unexpectedly"));
            return;
        }
        if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(ActiveKey, false) || game != null) return;
        try
        {
            report = new Report { utc = DateTime.UtcNow.ToString("o") };
            Application.logMessageReceived += CaptureError;
            Time.captureDeltaTime = Step;
            // The empty-scene bootstrap can already have created the real game.
            // Reuse it: a second enabled controller would keep writing the
            // global timeScale after this fixture disables its own controller.
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            if (game == null) game = new GameObject("Isolated sword spacing policy game").AddComponent<KarmaGame>();
            game.StartRun(); player = game.Player;
            player.Essences.Sacrifice(Essence.SwordWave);
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 1);
            Call(game, "LoadRoom", true);
            enemy = game.Enemies[0]; player = game.Player;
            body = player.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic; body.interpolation = RigidbodyInterpolation2D.None;
            art = Get<Transform>(enemy, "art"); animator = Get<KarmaCharacterAnimator>(enemy, "animator");
            // Every Enemy.Update below is called explicitly. Coroutines still
            // run on this active GameObject during the recovery fixture.
            enemy.enabled = false; player.enabled = false; game.enabled = false;
            deadline = EditorApplication.timeSinceStartup + 120;
            suite = Suite();
            var driver = new GameObject("Sword spacing policy fixture driver").AddComponent<KarmaSwordSpacingPolicyDriver>();
            driver.Step = Tick;
        }
        catch (Exception error) { Finish(error); }
    }
    static void Tick()
    {
        if (finishing) return;
        try
        {
            if (runtimeError != null) throw new Exception(runtimeError);
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Policy fixtures exceeded 120-second deadline");
            if (!suite.MoveNext()) Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    static FieldInfo Field(object value, string name) => value.GetType().GetField(name, Private)
        ?? throw new MissingFieldException(value.GetType().Name, name);
    static T Get<T>(object value, string name) => (T)Field(value, name).GetValue(value);
    static void Set(object value, string name, object field) => Field(value, name).SetValue(value, field);
    static object Call(object value, string name, params object[] args)
    {
        var method = value.GetType().GetMethod(name, Private)
            ?? throw new MissingMethodException(value.GetType().Name, name);
        return method.Invoke(value, args);
    }
    static T Property<T>(object value, string name) => (T)value.GetType().GetProperty(name, Private).GetValue(value);
    static void Check(bool condition, string message)
    {
        report.assertions++;
        if (!condition) throw new Exception(section + ": " + message);
    }
    static void State(string state) => Set(enemy, "state", Enum.Parse(Field(enemy, "state").FieldType, state));
    static void Clock(float time) => Set(enemy, "<FightSeconds>k__BackingField", time);
    static void PlayerAt(float x, float y = -1)
    {
        var position = new Vector2(x, y < 0 ? KarmaSpace.PlayerHalfHeight + 0.015f : y);
        body.position = position; player.transform.position = position; body.linearVelocity = Vector2.zero;
        Set(player, "<Facing>k__BackingField", x < enemy.Position.x ? 1 : -1);
        Physics2D.SyncTransforms();
    }
    static void Pose(string pose)
    {
        Set(animator, "hurtUntil", -1f); Set(animator, "deathAt", -1f);
        Call(animator, "ShowPose", pose);
    }
    static void Reset(float gap = 8.25f, int side = -1, float bossX = 20)
    {
        Call(game, "ResetHitFeedback");
        Set(game, "<Paused>k__BackingField", false); Time.timeScale = 1;
        Call(enemy, "InterruptPattern");
        // Disabling automatic Updates invokes the production OnDisable ->
        // CancelCombat path. Explicitly re-arm only this isolated fixture;
        // otherwise every real damage/pressure call correctly rejects it.
        Set(enemy, "stopped", false);
        enemy.transform.position = new Vector3(bossX, KarmaSpace.GuardianGroundY, 0);
        Clock(100); State("Approach");
        Set(enemy, "engaged", true); Set(enemy, "running", false); Set(enemy, "timer", 100f);
        Set(enemy, "pendingParry", false); Set(enemy, "stunnedUntil", 0f);
        Set(enemy, "patternStarted", 100f); Set(enemy, "selected", EnemyAttack.SW02); Set(enemy, "previous", EnemyAttack.SW01);
        Set(enemy, "swordAttackedSinceRetreat", true); Set(enemy, "nextSwordRetreat", 0f);
        Set(enemy, "swordRetreatRemaining", 0f); Set(enemy, "swordRetreatTarget", 0f);
        Set(enemy, "swordPressurePending", false); Set(enemy, "swordPressureTarget", 0f);
        Set(enemy, "swordApproaching", false); Set(enemy, "nextSwordApproach", 1000f);
        Set(enemy, "swordBasicDue", true); Set(enemy, "nextSwordBasic", 0f);
        Set(enemy, "direction", (float)side);
        art.localScale = new Vector3(side * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
        PlayerAt(bossX + side * gap);
        Set(player, "nextSword", -100f); Set(player, "normalSwordPressureUntil", -1f);
        Set(player, "airSlashRunning", false); Set(player, "airSlashBufferedUntil", 0f);
        Set(player, "jumpBufferedUntil", 0f); Set(player, "guardUntil", -1f); Set(player, "stunUntil", -1f);
        Set(animator, "guardianPreviousPosition", art.position);
        Pose("Move");
    }
    static float Gap => Mathf.Abs(enemy.Position.x - player.Position.x);
    static bool Retreat(float dt = Step) => (bool)Call(enemy, "RetreatSword", dt, player.Position.x - enemy.Position.x);
    static bool Overlap => (bool)Call(player, "SwordAreaOverlaps", enemy);
    static void Remember() => Call(enemy, "RememberSwordPressure");
    static void Update() => Call(enemy, "Update");

    static IEnumerator Suite()
    {
        section = "fixture";
        Check(UnityEngine.Object.FindObjectsByType<KarmaGame>().Length == 1,
            "the isolated fixture has exactly one game controller and no competing time-scale writer");
        Check(Time.deltaTime > 0, "positive Play Mode delta is available");
        Check(enemy.PrimaryType == Essence.SwordWave && game.StageIndex == 1, "real initializer creates stage-one SwordWave inheritance");
        Check(Mathf.Abs(game.Config.BossTuning.swordBasicCooldown - 2.4f) < Epsilon, "basic cooldown tuning remains 2.4 seconds");
        GeometryChecks();
        PressureChecks();
        ActionLocks();
        section = "zero delta";
        Reset(); Remember();
        // captureDeltaTime supplies a fixed scaled delta even at timeScale 0.
        // Disable that fixture clock for one real paused frame so Update sees
        // the same zero delta as ordinary hit-stop, then restore the clock.
        Time.captureDeltaTime = 0;
        Time.timeScale = 0;
        yield return null;
        Check(Time.deltaTime == 0, "zero-delta fixture uses an actual paused time-scale frame");
        Vector2 frozen = enemy.Position;
        Update();
        Check(enemy.Position == frozen && Get<float>(enemy, "nextSwordRetreat") == 0,
            "zero-delta Update neither moves nor consumes a retreat command");
        Time.timeScale = 1;
        Time.captureDeltaTime = Step;
        yield return null;
        Check(Time.deltaTime > 0, "positive delta resumes after the zero-delta fixture");
        BudgetAndCooldownChecks();
        WallAndAttackChecks();
        SelectionChecks();
        // A real production basic pattern must finish its whole stationary
        // recovery before spacing receives the root. This fixture advances
        // actual Play Mode frames instead of inventing a completed token.
        section = "full production pattern and recovery";
        Reset(8.25f); Remember();
        Set(enemy, "swordAttackedSinceRetreat", false);
        Set(enemy, "selected", EnemyAttack.SWBasic); Set(enemy, "running", true);
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
        Vector2 patternRoot = enemy.Position;
        float recoverySeconds = 0; int frames = 0, recoveryFrames = 0;
        while (Get<bool>(enemy, "running"))
        {
            Check(++frames < 100, "production basic pattern terminates");
            string state = Get<object>(enemy, "state").ToString();
            if (state == "Recover") { recoverySeconds += Time.deltaTime; recoveryFrames++; }
            Update();
            Check(enemy.Position == patternRoot, state + " retains stationary root ownership");
            Check(Get<float>(enemy, "nextSwordRetreat") == 0, state + " does not accept a retreat before pattern completion");
            yield return null;
        }
        Check(recoveryFrames > 0 && recoverySeconds >= 0.38f - Epsilon,
            "actual pattern provides its existing complete recovery window");
        Check(Get<bool>(enemy, "swordPressurePending") && Get<bool>(enemy, "swordAttackedSinceRetreat"),
            "pressure survives attack/recovery and completion restores exactly the retreat permission");
        Update();
        Check(enemy.Position.x > patternRoot.x + Epsilon && Get<float>(enemy, "nextSwordRetreat") > enemy.FightSeconds,
            "the first free Update converts saved pressure into actual ground retreat");
        report.policy.Add(new PolicyRow { name = "production-recovery-before-retreat", elapsed = recoverySeconds,
            frames = recoveryFrames, bossTravel = enemy.Position.x - patternRoot.x,
            note = "Real RunPattern/BasicSwordWave/Recovery coroutines on actual Play Mode frames; stationary kinematic player." });
        Call(enemy, "InterruptPattern");
        section = "combat end";
        Remember(); Check(Get<bool>(enemy, "swordPressurePending"), "active combat can queue pressure before cancellation");
        enemy.CancelCombat();
        Check(!Get<bool>(enemy, "swordPressurePending") && Get<float>(enemy, "swordPressureTarget") == 0,
            "combat cancellation clears pending pressure and its remembered target");
    }
    static void GeometryChecks()
    {
        section = "real J and incoming-hit geometry";
        foreach (int side in new[] { -1, 1 })
        {
            Reset(8.25f, side);
            Bounds beforeBounds = enemy.DamageBounds;
            float health = enemy.Health;
            Check(Overlap, side + " ordinary J geometry overlaps at the old retreat stop 8.25");
            bool ready = (bool)Call(player, "CanSword");
            float nextSword = Get<float>(player, "nextSword");
            bool stopped = Get<bool>(enemy, "stopped"), armored = enemy.Armored, invulnerable = enemy.Invulnerable;
            Call(player, "Attack");
            float damage = health - enemy.Health;
            var geometry = new GeometryRow { playerSide = side, damageBefore = health, damageAfter = enemy.Health,
                actualJDamageAt8p25 = damage, expectedMoveBoundary = 10.608f, incomingDamageBounds = beforeBounds };
            report.geometry.Add(geometry);
            Check(Mathf.Abs(damage - game.Config.swordDamage) < Epsilon,
                side + " actual ordinary J at 8.25 deals the unchanged natural sword damage; actual=" + damage
                + " expected=" + game.Config.swordDamage + " health=" + health + "->" + enemy.Health
                + " CanSwordBefore=" + ready + " nextSwordBefore=" + nextSword + " time=" + Time.time
                + " stopped=" + stopped + " armored=" + armored + " invulnerable=" + invulnerable
                + " playerHealth=" + player.Health + " combat=" + game.IsCombat);
            Check(Get<bool>(enemy, "swordPressurePending") && Get<float>(enemy, "swordPressureTarget") > 10.6f,
                side + " actual normal-J hit records spacing pressure before Hurt");
            Call(game, "ResetHitFeedback");
            Pose("Move");
            float lastHit = -1, firstMiss = -1;
            for (int hundredths = 1000; hundredths <= 1120; hundredths++)
            {
                float gap = hundredths / 100f;
                PlayerAt(enemy.Position.x + side * gap);
                bool hit = Overlap;
                if (hit) { Check(firstMiss < 0, "Move geometry has no disconnected hit after its first miss"); lastHit = gap; }
                else if (firstMiss < 0) firstMiss = gap;
            }
            Check(Mathf.Abs(lastHit - 10.60f) < 0.011f && Mathf.Abs(firstMiss - 10.61f) < 0.011f,
                side + " original Move DamageBounds put the J boundary near the measured 10.608, not 8.25");
            PlayerAt(enemy.Position.x + side * 10.558f); Check(Overlap, side + " J reaches immediately inside the measured Move boundary");
            PlayerAt(enemy.Position.x + side * 10.658f); Check(!Overlap, side + " J does not reach immediately outside the measured Move boundary");
            geometry.lastOverlappingGap = lastHit; geometry.firstNonoverlappingGap = firstMiss;
        }
    }
    static void PressureChecks()
    {
        section = "pressure survives pose changes and active stun";
        Reset(11.5f); Pose("Slash");
        Check(Overlap, "wide original Slash pose is a real J target at 11.5");
        float before = enemy.Health;
        Call(player, "Attack");
        Check(enemy.Health < before && Get<bool>(enemy, "swordPressurePending"), "wide-pose actual hit queues pressure");
        float remembered = Get<float>(enemy, "swordPressureTarget");
        Call(game, "ResetHitFeedback");
        Set(enemy, "running", true); State("Attack");
        animator.GuardianPose("Attack", EnemyAttack.SW01, false, false);
        Check(!Overlap, "Hurt narrows the actual incoming-hit pose enough to remove instantaneous J overlap");
        Vector2 root = enemy.Position;
        Update();
        Check(enemy.Position == root && Get<bool>(enemy, "swordPressurePending"),
            "pose shrink during a committed attack cannot erase the real previous J pressure");
        Check(Get<float>(enemy, "swordPressureTarget") >= remembered - Epsilon,
            "Hurt/smaller current bounds cannot shrink the remembered target");
        Call(enemy, "InterruptPatternCore", true); State("Stunned");
        Set(enemy, "stunnedUntil", enemy.FightSeconds + 2);
        Update();
        Check(enemy.Position == root && Get<bool>(enemy, "swordPressurePending")
            && Get<float>(enemy, "swordPressureTarget") >= remembered - Epsilon,
            "active stun preserves pending pressure without moving or shrinking its target");
        // Exercise the real pendingParry -> InterruptPatternCore(true) call site.
        State("Attack"); Set(enemy, "running", true); Set(enemy, "pendingParry", true);
        Update();
        Check(enemy.Stunned && Get<bool>(enemy, "swordPressurePending"),
            "the production parry interruption retains pressure even though Stunned is assigned after interruption");
        Call(enemy, "InterruptPattern");
        Check(!Get<bool>(enemy, "swordPressurePending") && Get<float>(enemy, "swordPressureTarget") == 0,
            "ordinary interruption explicitly clears pressure and target");
        State("Approach"); Remember(); Set(enemy, "running", true); State("Attack");
        PlayerAt(enemy.Position.x - 25);
        Update();
        Check(!Get<bool>(enemy, "swordPressurePending") && Get<float>(enemy, "swordPressureTarget") == 0,
            "genuinely leaving the remembered horizontal threat band clears stale pressure");
        report.policy.Add(new PolicyRow { name = "wide-hit-Hurt-stun-memory", pressureTarget = remembered,
            note = "Actual J landed in Slash; subsequent Hurt bounds no longer overlap; request survives production Update and parry interruption." });
    }
    static void ActionLocks()
    {
        section = "root ownership and normal versus continuous attacks";
        Reset();
        Call(player, "Attack"); Call(game, "ResetHitFeedback");
        Check(Property<bool>(player, "AttackCommitted") && !Property<bool>(player, "ContinuousSwordAttack"),
            "fixture is inside a normal J animation commitment, without a continuous aerial sweep");
        Vector2 root = enemy.Position;
        Update();
        Check(enemy.Position.x > root.x + Epsilon && Get<float>(enemy, "nextSwordRetreat") > enemy.FightSeconds,
            "normal J has already hit and does not starve the first free retreat Update");
        Reset(); Remember(); Set(player, "airSlashRunning", true);
        root = enemy.Position; Update();
        Check(enemy.Position == root && Get<float>(enemy, "nextSwordRetreat") == 0
            && Get<bool>(enemy, "swordPressurePending"), "continuous aerial sweep defers, rather than consumes, the retreat request");
        Set(player, "airSlashRunning", false); Update();
        Check(enemy.Position.x > root.x + Epsilon, "ending the continuous sweep releases the queued retreat");
        foreach (string state in new[] { "Telegraph", "Attack", "Recover" })
        {
            Reset(); Remember(); State(state); Set(enemy, "running", true);
            root = enemy.Position; Update();
            Check(enemy.Position == root && Get<float>(enemy, "nextSwordRetreat") == 0
                && Get<bool>(enemy, "swordPressurePending"), state + " retains root ownership and queues pressure without accepting movement");
        }
        Reset(); Remember(); State("Stunned"); Set(enemy, "stunnedUntil", enemy.FightSeconds + 2);
        root = enemy.Position; Update();
        Check(enemy.Position == root && Get<float>(enemy, "nextSwordRetreat") == 0, "active stun is a stationary player opportunity");
        Reset(); Remember(); game.TogglePause(); root = enemy.Position; float clock = enemy.FightSeconds;
        Update();
        Check(enemy.Position == root && enemy.FightSeconds == clock && Get<float>(enemy, "nextSwordRetreat") == 0,
            "game pause freezes root, combat clock and retreat acceptance");
        game.TogglePause();
    }
    static void BudgetAndCooldownChecks()
    {
        section = "bounded retreat, cooldown and completed-attack gate";
        Reset(1); Remember();
        var tuning = game.Config.BossTuning;
        float budget = tuning.swordRetreatDistance, duration = tuning.swordRetreatDuration;
        float startX = enemy.Position.x, startGap = Gap, startTime = enemy.FightSeconds;
        float travel = 0; int frames = 0;
        do
        {
            float before = enemy.Position.x;
            Retreat(); frames++;
            float delta = enemy.Position.x - before;
            Check(delta >= -Epsilon && delta <= tuning.swordRetreatSpeed * Step + Epsilon, "retreat applies a bounded away-facing step through real ground collision checks");
            Check(Mathf.Abs(enemy.Position.y - KarmaSpace.GuardianGroundY) < Epsilon, "retreat remains grounded");
            travel += Mathf.Abs(delta);
            if (Get<float>(enemy, "swordRetreatRemaining") <= 0) break;
            Clock(startTime + frames * Step);
            // Deliberately adversarial kinematic pursuit keeps the budget test
            // from ending early at its target; it is not gameplay telemetry.
            PlayerAt(player.Position.x + game.Config.moveSpeed * Step);
            Check(frames < Mathf.CeilToInt(duration / Step) + 2, "retreat does not become an unbounded escape loop");
        } while (true);
        Check(travel <= budget + Epsilon && travel >= budget - Epsilon,
            "an extreme point-blank pursuing fixture consumes, but cannot exceed, the configured distance budget");
        Check(enemy.FightSeconds - startTime <= duration + Epsilon, "the movement command stays within its configured duration");
        Check(Mathf.Abs(Get<float>(enemy, "nextSwordRetreat") - startTime - 5) < Epsilon,
            "a command starts the unchanged five-second retreat cooldown");
        Check(!Get<bool>(enemy, "swordAttackedSinceRetreat"), "one command consumes the completed-attack permission");
        report.policy.Add(new PolicyRow { name = "pursuit-budget-fixture", startGap = startGap, endGap = Gap,
            bossTravel = travel, elapsed = enemy.FightSeconds - startTime, frames = frames,
            nextRetreat = Get<float>(enemy, "nextSwordRetreat"), note = "Explicit kinematic pursuit at configured player speed from an extreme one-unit gap; a distance-budget policy test, not actual gameplay or a spacing success claim." });
        PlayerAt(enemy.Position.x - 4); Remember();
        Set(enemy, "swordAttackedSinceRetreat", true); Clock(startTime + 4.99f);
        float x = enemy.Position.x;
        Check(!Retreat() && Mathf.Abs(enemy.Position.x - x) < Epsilon && Get<bool>(enemy, "swordPressurePending"),
            "cooldown defers a new real request without consuming it");
        Clock(startTime + 5.01f); Set(enemy, "swordAttackedSinceRetreat", false);
        Check(!Retreat() && Mathf.Abs(enemy.Position.x - x) < Epsilon && Get<bool>(enemy, "swordPressurePending"),
            "elapsed cooldown alone cannot permit consecutive escapes before an attack completes");
        Set(enemy, "swordAttackedSinceRetreat", true);
        Check(Retreat() && enemy.Position.x > x + Epsilon, "both elapsed cooldown and a completed attack release the saved request");
        Reset(4); Remember(); Retreat();
        float until = Get<float>(enemy, "swordRetreatUntil"); x = enemy.Position.x;
        Check(Mathf.Abs(until - 100 - duration) < Epsilon, "new command has the configured retreat deadline");
        Check(Get<float>(enemy, "swordRetreatRemaining") > 0, "deadline fixture still has unused distance budget");
        Clock(until + 0.001f);
        Check(!Retreat() && enemy.Position.x == x && Get<float>(enemy, "swordRetreatRemaining") == 0,
            "deadline ends movement even with an unused budget");
    }
    static void WallAndAttackChecks()
    {
        section = "wall weakness and attack availability";
        foreach (int wall in new[] { -1, 1 })
        {
            float bossX = wall < 0 ? KarmaStage.BossLeftWall + 0.2f : KarmaStage.BossRightWall - 0.2f;
            Reset(8.25f, -wall, bossX); Remember();
            Vector2 root = enemy.Position;
            Check(!Retreat() && enemy.Position == root, wall + " arena wall blocks the requested retreat without reversing or teleporting it");
            Check(!Get<bool>(enemy, "swordAttackedSinceRetreat") && Get<float>(enemy, "swordRetreatRemaining") == 0,
                wall + " blocked attempt consumes its one escape instead of retrying forever");
            Call(enemy, "BeginAttack");
            Check(Get<bool>(enemy, "running") && Get<EnemyAttack>(enemy, "selected") == EnemyAttack.SWBasic,
                wall + " blocked retreat still yields to a reachable ordinary attack");
            Check(enemy.Position == root, wall + " attack warning preserves the cornered punish opportunity");
            Call(enemy, "InterruptPattern");
        }
    }
    static EnemyAttack SelectAt(float distance, out bool found)
    {
        var args = new object[] { distance, true, true, EnemyAttack.SWBasic };
        found = (bool)enemy.GetType().GetMethod("TrySelectSwordAttack", Private).Invoke(enemy, args);
        return (EnemyAttack)args[3];
    }
    static void SelectionChecks()
    {
        section = "basic range/cooldown and ranged resumption";
        Reset();
        PlayerAt(enemy.Position.x - 9.99f, enemy.Position.y);
        Check((bool)Call(enemy, "SwordAttackCanReach", EnemyAttack.SWBasic, player.Position), "basic remains selectable just inside ten world units");
        PlayerAt(enemy.Position.x - 10.01f, enemy.Position.y);
        Check(!(bool)Call(enemy, "SwordAttackCanReach", EnemyAttack.SWBasic, player.Position), "basic cannot extend beyond ten world units");
        PlayerAt(enemy.Position.x - 8.25f, enemy.Position.y);
        Call(enemy, "BeginAttack");
        Check(Get<EnemyAttack>(enemy, "selected") == EnemyAttack.SWBasic, "priority basic uses the real attack selector when the player closes");
        float next = Get<float>(enemy, "nextSwordBasic");
        Check(Mathf.Abs(next - enemy.FightSeconds - 2.4f) < Epsilon, "starting the real basic schedules exactly its original 2.4-second cooldown");
        Call(enemy, "InterruptPattern"); Set(enemy, "swordBasicDue", true);
        Clock(next - 0.01f);
        bool found; EnemyAttack choice = SelectAt(Gap, out found);
        Check(found && choice != EnemyAttack.SWBasic, "other ranged skills remain available while the basic cooldown is pending");
        Clock(next + 0.01f); Set(enemy, "swordBasicDue", true);
        choice = SelectAt(Gap, out found);
        Check(found && choice == EnemyAttack.SWBasic, "the ready basic resumes through actual selection after cooldown");
        PlayerAt(enemy.Position.x - 11.75f, enemy.Position.y);
        choice = SelectAt(Gap, out found);
        Check(found && (choice == EnemyAttack.SW01 || choice == EnemyAttack.SW02),
            "outside basic range, the real selector resumes existing SW01/SW02 instead of extending basic range");
    }
    static void CaptureError(string message, string stack, LogType type)
    {
        if (type == LogType.Error && (message == "No graphic device is available to initialize the view."
                || message == "No graphic device is available to show the window.")
            && stack != null && System.Text.RegularExpressions.Regex.IsMatch(stack,
                @"UnityEditor\.(GUIView|ContainerWindow|EditorApplicationLayout|WindowLayout)\b"))
        { if (report != null) report.headlessViewWarnings++; return; }
        if (!finishing && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) runtimeError = message + "\n" + stack;
    }
    static void Finish(Exception error)
    {
        if (finishing) return; finishing = true;
        Application.logMessageReceived -= CaptureError; EditorApplication.update -= Pump;
        Time.timeScale = 1; Time.captureDeltaTime = 0;
        if (report == null) report = new Report { utc = DateTime.UtcNow.ToString("o") };
        report.result = error == null ? "PASS" : "FAIL";
        report.error = error == null ? null : error.ToString();
        File.WriteAllText("sword-spacing-policy-validation.json", JsonUtility.ToJson(report, true));
        string verdict = error == null ? "PASS: " + report.assertions + " isolated sword spacing policy assertions; actual-scene gameplay is a separate required validation."
            : "FAIL: " + error;
        File.WriteAllText("sword-spacing-policy-validation.txt", verdict + "\n");
        SessionState.SetBool(ActiveKey, false); SessionState.SetBool(ExitKey, true); SessionState.SetInt(ExitCodeKey, error == null ? 0 : 1);
        if (error == null) Debug.Log(verdict); else Debug.LogException(error);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); else EditorApplication.delayCall += ExitEditor;
    }
    static void ExitEditor()
    {
        if (!SessionState.GetBool(ExitKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall += ExitEditor; return; }
        int code = SessionState.GetInt(ExitCodeKey, 1); SessionState.SetBool(ExitKey, false); EditorApplication.Exit(code);
    }
}
[DefaultExecutionOrder(-10000)]
public sealed class KarmaSwordSpacingPolicyDriver : MonoBehaviour
{
    public Action Step;
    void Update() { Step?.Invoke(); }
}
#endif
