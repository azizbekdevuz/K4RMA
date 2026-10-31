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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Copy this identical observer into the reused isolated project's Assets/Editor
// for both runtimes, then -executeMethod KarmaSwordMovementChecks.Run. Only
// scenario setup places actors; every measured movement uses the real Updates,
// coroutines, Input System, Rigidbody2D and production attack/damage methods.
[InitializeOnLoad]
public static class KarmaSwordMovementChecks
{
    const string ActiveKey = "K4RMA.SwordMovement.Active";
    const string ReadyRangeKey = "K4RMA.SwordMovement.ReadyRange";
    const string ExitKey = "K4RMA.SwordMovement.Exit";
    const string ExitCodeKey = "K4RMA.SwordMovement.ExitCode";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const float Step = 0.05f, Epsilon = 0.0001f;

    [Serializable] sealed class Trace
    {
        public float time, bossX, bossY, playerX, playerY, horizontalGap, retreatRemaining, nextRetreat;
        public string state, intent, attack, pose;
        public bool running, shield, visibleFlipX;
    }
    [Serializable] sealed class Retreat
    {
        public float start, end = -1, fromX, toX, playerX, direction, distance, duration, attackResumedAt = -1;
        public int actualMovementFrames;
        public bool censoredAtWindow;
        public string outcome;
    }
    [Serializable] sealed class Pattern
    {
        public string attack;
        public float start, end = -1, startGap;
        public int projectiles;
        public bool reachableAtSelection, guardActive, guardRootAlive;
    }
    [Serializable] sealed class Cast
    {
        public float direction, distance, bossY, colliderMinY, colliderMaxY;
        public string collider;
        public Vector2 normal;
    }
    [Serializable] sealed class Row
    {
        public string scenario, category, inheritance, phase, playerControl;
        public float requestedSeconds, seconds, initialGap, finalGap, playerSpeed, bossMoveSpeed, playerSwordReach;
        public int frames, spacingEligibleFrames, runningFrames, recoveryFrames;
        public int swBasic, sw01, sw02, sw03, sw04, sw05, groundStrike, basicProjectiles, counterProjectiles;
        public int retreatCommands, retreatsWithActualMovement, completedRetreats, zeroDisplacementCommands;
        public int approachMovementEpisodes, approachMovementFrames, approachIntentFrames, horizontalReversals;
        public float retreatDistance, approachDistance, approachSeconds, minimumRetreatInterval = -1;
        public float attackRangeSeconds, attackRangeRatio, preferredBandSeconds, preferredBandRatio;
        public float firstAttackAt = -1, firstAttackGap = -1, longestStationaryCombat, recoverySeconds;
        public float maxRecoveryDisplacement, maxTelegraphDisplacement, maxGroundMovementSpeed;
        public float guardMovementDistance, guardMovementSeconds, maxCeilingError;
        public int actionsDuringGuard, basicsDuringGuard, sw01DuringGuard, sw02DuringGuard, retreatCommandsDuringUltimate;
        public int meleeInputs, meleeHits, meleeHitsDuringRecovery, recoveryMeleeOpportunities, unreachableSelections;
        public int directionalSlashActivations, airSlashActivations;
        public float meleeDamage, firstMeleeHitAt = -1, longestPursuitWithoutMeleeHit, playerActualTravel;
        public List<Retreat> retreats = new List<Retreat>();
        public List<Pattern> patterns = new List<Pattern>();
        public List<Trace> trace = new List<Trace>();
        public List<Cast> initialRetreatCasts = new List<Cast>();
    }
    [Serializable] sealed class Report
    {
        public string result, runtime, utc, error;
        public string method = "Same observer for baseline and modified. Accepted retreat commands are changes of private nextSwordRetreat; actual retreat movement is signed world Transform displacement away from the player, associated with that command. Approach episodes require actual signed displacement toward the player outside an attack. No measured-frame actor teleportation. Pursuit uses a virtual Keyboard, real KarmaPlayer.Update and FixedUpdate, horizontal chase with a 1.8/2.8 stop/resume band, and held ordinary J attacks. Setup raises only fixture health, without changing attack damage, range or timings. All long AI rows run at least 120 fight seconds. Baseline records metrics without improvement assertions.";
        public string spacingEligibility = "Counted before the production Enemy.Update at the observer's 0.05-second frame cadence: spacingEligibleFrames means running was false and state was neither Recover nor Stunned. runningFrames includes all action-owned telegraph, attack and recovery frames; recoveryFrames is the subset whose before-state was Recover. These are observed opportunities to reach spacing AI, not an instrumented private-method call counter.";
        public int assertions, headlessViewWarnings;
        public float captureDeltaTime = Step;
        public List<Row> scenarios = new List<Row>();
    }
    sealed class Spec
    {
        public string name, category;
        public float gap, seconds;
        public int order, wall;
        public bool chase, enrage, initialGuard;
        public Spec(string n, string c, float d, float s, int o = 0, bool pursuit = false, int edge = 0, bool rage = false, bool guard = false)
        { name = n; category = c; gap = d; seconds = s; order = o; chase = pursuit; wall = edge; enrage = rage; initialGuard = guard; }
    }
    static readonly Essence[][] Orders = {
        new[] { Essence.SwordWave },
        new[] { Essence.SwordWave, Essence.RisingSlash },
        new[] { Essence.SwordWave, Essence.Guard },
        new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard },
        new[] { Essence.SwordWave, Essence.Guard, Essence.RisingSlash }
    };
    static readonly Spec[] Specs = {
        new Spec("close-3", "close", 3, 24), new Spec("close-5", "close", 5, 24), new Spec("close-6", "close", 6, 24),
        new Spec("band-7", "band", 7, 30), new Spec("band-8", "band", 8, 30), new Spec("band-9", "band", 9, 30),
        new Spec("far-12", "far", 12, 30), new Spec("far-16", "far", 16, 30),
        new Spec("left-wall", "wall", 3, 24, edge: -1), new Spec("right-wall", "wall", 3, 24, edge: 1),
        new Spec("guard-concurrent-movement", "guard", 5, 40, 2, true, guard: true),
        new Spec("pursuit-SwordWave", "long", 8, 120, 0, true),
        new Spec("pursuit-SwordWave-RisingSlash", "long", 8, 120, 1, true),
        new Spec("pursuit-SwordWave-Guard", "long", 8, 120, 2, true),
        new Spec("pursuit-final-RisingSlash-Guard", "long", 8, 120, 3, true),
        new Spec("pursuit-final-Guard-RisingSlash", "long", 8, 120, 4, true),
        new Spec("pursuit-final-enraged", "long", 8, 120, 3, true, rage: true)
    };
    static readonly Spec[] ReadyRangeSpecs = {
        new Spec("far-ready-12", "far", 12, 12),
        new Spec("far-ready-16", "far", 16, 12)
    };
    static Spec[] SelectedSpecs => SessionState.GetBool(ReadyRangeKey, false) ? ReadyRangeSpecs : Specs;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static Keyboard keyboard;
    static Report report;
    static Row row;
    static Spec spec;
    static Pattern currentPattern;
    static Retreat currentRetreat;
    static int index, lastMoveSign;
    static bool finishing, setupPending, modified, movingPlayer, wasApproaching;
    static bool beforeRunning, beforeGuard;
    static Vector2 beforeBoss, beforePlayer, lastMeasuredPlayer;
    static float beforeNextRetreat, beforePatternStart, beforeNextSword, beforeHealth;
    static float sampledAt, lastPatternStart = -1, lastRetreatStart = -1, stationarySince, lastMeleeHit, nextTrace;
    static string beforeState;
    static EnemyAttack beforeSelected;
    static double deadline;
    static string runtimeError;
    static InputSettings.UpdateMode previousInputMode;
    static InputSettings.BackgroundBehavior previousBackground;
    static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
    static readonly HashSet<KarmaBossAttack> observedShots = new HashSet<KarmaBossAttack>();

    static FieldInfo Field(object o, string name) => o.GetType().GetField(name, Private)
        ?? throw new MissingFieldException(o.GetType().Name, name);
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Private).Invoke(o, args);
    static void Check(bool ok, string message)
    { report.assertions++; if (!ok) throw new Exception((row == null ? "Movement" : row.scenario) + ": " + message); }
    static string State => Get<object>(enemy, "state").ToString();

    static KarmaSwordMovementChecks()
    {
        EditorApplication.playModeStateChanged += OnState;
        if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Pump;
        if (SessionState.GetBool(ExitKey, false)) EditorApplication.delayCall += ExitEditor;
    }
    public static void Run()
    {
        SessionState.SetBool(ReadyRangeKey, false);
        StartChecks();
    }
    public static void RunReadyRange()
    {
        SessionState.SetBool(ReadyRangeKey, true);
        StartChecks();
    }
    static void StartChecks()
    {
        SessionState.SetBool(ActiveKey, true); SessionState.SetBool(ExitKey, false);
        Application.runInBackground = true; PlayerSettings.runInBackground = true;
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        EditorApplication.update -= Pump; EditorApplication.update += Pump;
        EditorApplication.delayCall += () => { EditorApplication.delayCall += BeginPlay; };
    }
    static void BeginPlay()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += BeginPlay; return; }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Pump()
    {
        EditorApplication.isPaused = false; Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying && game == null && Time.frameCount > 0) OnState(PlayModeStateChange.EnteredPlayMode);
    }
    static void OnState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (SessionState.GetBool(ExitKey, false)) EditorApplication.delayCall += ExitEditor;
            else if (SessionState.GetBool(ActiveKey, false)) Finish(new Exception("Play Mode ended unexpectedly"));
            return;
        }
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(ActiveKey, false) || game != null) return;
        try
        {
            modified = typeof(KarmaBossTuning).GetField("swordRetreatStartDistance") != null;
            report = new Report { runtime = modified ? "modified" : "baseline", utc = DateTime.UtcNow.ToString("o") };
            Application.logMessageReceived += CaptureError;
            Time.captureDeltaTime = Step;
            previousInputMode = InputSystem.settings.updateMode;
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            KarmaInput.Held(KarmaAction.Attack);
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            if (game == null) game = new GameObject("Sword movement measurement game").AddComponent<KarmaGame>();
            game.Config.playerHealth = 1000000;
            deadline = EditorApplication.timeSinceStartup + 1500;
            new GameObject("Sword movement input driver").AddComponent<KarmaSwordMovementInputDriver>().Step = Before;
            new GameObject("Sword movement coordinate observer").AddComponent<KarmaSwordMovementObserver>().Step = After;
            setupPending = true;
        }
        catch (Exception error) { Finish(error); }
    }
    static void CaptureError(string message, string stack, LogType type)
    {
        // Unity's batch-mode layout code can report these two known messages
        // without a graphics device. Keep the allowance exact and restricted
        // to the Editor view/layout stack; gameplay errors remain fatal.
        if (type == LogType.Error
            && (message == "No graphic device is available to initialize the view."
                || message == "No graphic device is available to show the window.")
            && stack != null && System.Text.RegularExpressions.Regex.IsMatch(stack,
                @"UnityEditor\.(GUIView|ContainerWindow|EditorApplicationLayout|WindowLayout)\b"))
        {
            if (report != null) report.headlessViewWarnings++;
            return;
        }
        if (!finishing && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            runtimeError = message + "\n" + stack;
    }
    static void Send(bool attack, int direction)
    {
        var keys = new List<Key>();
        if (attack) keys.Add(Key.J);
        if (direction < 0) keys.Add(Key.A); else if (direction > 0) keys.Add(Key.D);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
        InputSystem.Update();
    }
    static void Setup()
    {
        spec = SelectedSpecs[index]; Send(false, 0);
        game.StartRun();
        foreach (var essence in Orders[spec.order]) game.Player.Essences.Sacrifice(essence);
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, Orders[spec.order].Length);
        Call(game, "LoadRoom", true); enemy = game.Enemies[0];
        Call(enemy, "InterruptPattern");
        Set(enemy, "<MaxHealth>k__BackingField", 1000000f);
        Set(enemy, "<Health>k__BackingField", spec.enrage ? 450000f : 1000000f);
        Set(enemy, "engaged", true); Set(enemy, "timer", SessionState.GetBool(ReadyRangeKey, false) ? 0f : 1.05f);
        Set(enemy, "nextSwordRetreat", 0f); Set(enemy, "swordAttackedSinceRetreat", true);
        Set(enemy, "state", Enum.Parse(Field(enemy, "state").FieldType, "Approach"));
        game.Config.BossTuning.aerialSwordSeed = 20261030;
        float bossX = spec.wall < 0 ? KarmaStage.BossLeftWall + 0.2f
            : spec.wall > 0 ? KarmaStage.BossRightWall - 0.2f : 20;
        float side = spec.wall > 0 ? -1 : 1;
        enemy.transform.position = new Vector3(bossX, KarmaSpace.GuardianGroundY, 0);
        Set(enemy, "direction", side);
        var body = game.Player.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic; body.interpolation = RigidbodyInterpolation2D.None;
        body.position = new Vector2(bossX + side * spec.gap, KarmaSpace.PlayerHalfHeight + 0.01f);
        game.Player.transform.position = body.position; body.linearVelocity = Vector2.zero;
        lastMeasuredPlayer = body.position;
        Physics2D.SyncTransforms();
        row = new Row {
            scenario = spec.name, category = spec.category, inheritance = string.Join(",", Orders[spec.order]),
            phase = spec.enrage ? "enraged" : "normal", requestedSeconds = spec.seconds, initialGap = spec.gap,
            playerControl = spec.chase ? "Actual Input System horizontal pursuit and held J; no jumping or active skills. Guard fixture delays J until 1.8 seconds." : "No input; real dynamic grounded player remains stationary.",
            playerSpeed = game.Config.moveSpeed, bossMoveSpeed = game.Config.enemyMoveSpeed * game.Config.BossTuning.moveMultiplier,
            playerSwordReach = game.Config.Techniques.longSlashReach
        };
        report.scenarios.Add(row);
        InitialCasts();
        currentPattern = null; currentRetreat = null; lastPatternStart = -1; lastRetreatStart = -1;
        stationarySince = sampledAt = lastMeleeHit = nextTrace = 0; lastMoveSign = 0;
        movingPlayer = wasApproaching = false; observedShots.Clear();
        if (spec.initialGuard)
        {
            // Only the isolated concurrency fixture starts the original SW04
            // coroutine explicitly. All subsequent choices use the real AI.
            Set(enemy, "selected", EnemyAttack.SW04); Set(enemy, "running", true);
            Set(enemy, "patternStarted", enemy.FightSeconds);
            enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
        }
        setupPending = false;
        Debug.Log("MOVEMENT START " + report.runtime + " " + spec.name);
    }
    static void InitialCasts()
    {
        foreach (float direction in new[] { -1f, 1f })
            foreach (var hit in Physics2D.BoxCastAll(enemy.Position, KarmaSpace.Units(new Vector2(0.8f, 1.6f)), 0,
                Vector2.right * direction, 0.5f))
                if (hit.collider.GetComponent<KarmaSurface>() != null)
                    row.initialRetreatCasts.Add(new Cast { direction = direction, collider = hit.collider.name,
                        distance = hit.distance, bossY = enemy.Position.y, colliderMinY = hit.collider.bounds.min.y,
                        colliderMaxY = hit.collider.bounds.max.y, normal = hit.normal });
    }
    static void Before()
    {
        if (finishing) return;
        try
        {
            if (runtimeError != null) throw new Exception(runtimeError);
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Movement observer exceeded 1500-second wall-clock deadline");
            if (setupPending) Setup();
            float dx = enemy.Position.x - game.Player.Position.x, gap = Mathf.Abs(dx);
            if (spec.chase)
            {
                if (movingPlayer && gap <= 1.8f) movingPlayer = false;
                else if (!movingPlayer && gap >= 2.8f) movingPlayer = true;
            }
            bool attack = spec.chase && (!spec.initialGuard || enemy.FightSeconds >= 1.8f);
            Send(attack, spec.chase && movingPlayer ? (dx < 0 ? -1 : 1) : 0);
            beforeBoss = enemy.Position; beforePlayer = game.Player.Position;
            beforeRunning = Get<bool>(enemy, "running"); beforeState = State;
            beforeGuard = Get<bool>(enemy, "swordGuard"); beforeSelected = Get<EnemyAttack>(enemy, "selected");
            beforeNextRetreat = Get<float>(enemy, "nextSwordRetreat");
            beforePatternStart = Get<float>(enemy, "patternStarted");
            beforeNextSword = Get<float>(game.Player, "nextSword"); beforeHealth = enemy.Health;
        }
        catch (Exception error) { Finish(error); }
    }
    static void After()
    {
        if (finishing || setupPending || enemy == null) return;
        try
        {
            float now = enemy.FightSeconds, dt = now - sampledAt;
            Check(dt >= 0 && dt <= Step * 1.6f, "samples follow real consecutive combat-clock frames");
            sampledAt = now; row.seconds = now; row.frames++;
            if (beforeRunning) row.runningFrames++;
            if (beforeState == "Recover") row.recoveryFrames++;
            if (!beforeRunning && beforeState != "Recover" && beforeState != "Stunned") row.spacingEligibleFrames++;
            float delta = enemy.Position.x - beforeBoss.x, distance = Mathf.Abs(delta);
            float gap = Mathf.Abs(game.Player.Position.x - enemy.Position.x);
            bool running = Get<bool>(enemy, "running"), guard = Get<bool>(enemy, "swordGuard");
            EnemyAttack selected = Get<EnemyAttack>(enemy, "selected");
            string state = State;
            // FixedUpdate precedes this frame's early Update snapshot. Compare
            // successive late samples to include actual Rigidbody displacement.
            row.playerActualTravel += Mathf.Abs(game.Player.Position.x - lastMeasuredPlayer.x);
            lastMeasuredPlayer = game.Player.Position;
            Check(enemy.Position.x >= KarmaStage.BossLeftWall - 0.01f && enemy.Position.x <= KarmaStage.BossRightWall + 0.01f,
                "actual boss root remains inside its arena walls");
            float nextRetreat = Get<float>(enemy, "nextSwordRetreat");
            if (nextRetreat > beforeNextRetreat + Epsilon)
            {
                EndRetreat(now - dt, false);
                currentRetreat = new Retreat { start = now - dt, fromX = beforeBoss.x, toX = enemy.Position.x,
                    playerX = beforePlayer.x, direction = Get<float>(enemy, "swordRetreatDirection") };
                row.retreats.Add(currentRetreat); row.retreatCommands++;
                if (lastRetreatStart >= 0)
                {
                    float interval = currentRetreat.start - lastRetreatStart;
                    row.minimumRetreatInterval = row.minimumRetreatInterval < 0 ? interval : Mathf.Min(row.minimumRetreatInterval, interval);
                }
                lastRetreatStart = currentRetreat.start;
                if (beforeRunning && beforeSelected == EnemyAttack.SW05) row.retreatCommandsDuringUltimate++;
            }
            bool retreatMovement = currentRetreat != null && delta * currentRetreat.direction > Epsilon
                && !beforeRunning;
            if (retreatMovement)
            {
                if (currentRetreat.actualMovementFrames == 0) row.retreatsWithActualMovement++;
                currentRetreat.actualMovementFrames++; currentRetreat.distance += distance;
                currentRetreat.toX = enemy.Position.x; row.retreatDistance += distance;
                Check(!enemy.Invulnerable, "ordinary retreat adds no invulnerability");
            }
            if (currentRetreat != null && Get<float>(enemy, "swordRetreatRemaining") <= Epsilon) EndRetreat(now, false);
            // The baseline can Move and BeginAttack in the very same Update.
            // Include that real final approach step even though running is now
            // true; excluding it would erase the exact single-step defect.
            bool groundMovement = distance > Epsilon && !beforeRunning
                && Mathf.Abs(beforeBoss.y - KarmaSpace.GuardianGroundY) < 0.01f
                && Mathf.Abs(enemy.Position.y - KarmaSpace.GuardianGroundY) < 0.01f;
            bool approach = groundMovement && !retreatMovement && delta * (beforePlayer.x - beforeBoss.x) > 0;
            if (approach)
            {
                if (!wasApproaching) row.approachMovementEpisodes++;
                row.approachMovementFrames++; row.approachDistance += distance; row.approachSeconds += dt;
            }
            wasApproaching = approach;
            if (!running && enemy.Intent.Contains("접근")) row.approachIntentFrames++;
            if (groundMovement)
            {
                int sign = delta > 0 ? 1 : -1;
                if (lastMoveSign != 0 && sign != lastMoveSign) row.horizontalReversals++;
                lastMoveSign = sign;
                row.maxGroundMovementSpeed = Mathf.Max(row.maxGroundMovementSpeed, distance / Mathf.Max(dt, Epsilon));
                stationarySince = now;
                if (guard || beforeGuard) { row.guardMovementDistance += distance; row.guardMovementSeconds += dt; }
            }
            else if (selected != EnemyAttack.SW05)
                row.longestStationaryCombat = Mathf.Max(row.longestStationaryCombat, now - stationarySince);
            else stationarySince = now;
            if ((bool)Call(enemy, "SwordAttackCanReach", EnemyAttack.SWBasic, game.Player.Position)) row.attackRangeSeconds += dt;
            if (gap >= 7 && gap <= 9 && Mathf.Abs(enemy.Position.y - KarmaSpace.GuardianGroundY) < 0.1f) row.preferredBandSeconds += dt;
            if (beforeRunning && running && state == "Recover" && beforeState == "Recover")
            { row.recoverySeconds += dt; row.maxRecoveryDisplacement = Mathf.Max(row.maxRecoveryDisplacement, Vector2.Distance(enemy.Position, beforeBoss)); }
            if (beforeRunning && running && beforeSelected == selected && beforePatternStart == Get<float>(enemy, "patternStarted")
                && beforeState == "Telegraph" && state == "Telegraph" && selected != EnemyAttack.SW05)
                row.maxTelegraphDisplacement = Mathf.Max(row.maxTelegraphDisplacement, Vector2.Distance(enemy.Position, beforeBoss));
            if (running && Get<float>(enemy, "patternStarted") != lastPatternStart)
                ObservePattern(now, gap, guard, selected);
            ObserveShots();
            if (currentPattern != null && !running && currentPattern.end < 0)
            {
                currentPattern.end = now;
                if (currentPattern.attack == "SW05") Check(currentPattern.projectiles == 105, "a completed central aerial cast still releases exactly 105 swords");
                if (currentPattern.attack == "SWBasic") Check(currentPattern.projectiles == 1, "completed basic wave retains one projectile");
                if (currentPattern.attack == "SW01") Check(currentPattern.projectiles == 2, "completed SW01 retains two projectiles");
                if (currentPattern.attack == "SW02") Check(currentPattern.projectiles == 1, "completed SW02 retains one projectile");
            }
            if (selected == EnemyAttack.SW05 && enemy.Invulnerable)
                row.maxCeilingError = Mathf.Max(row.maxCeilingError, Vector2.Distance(enemy.Position, new Vector2(20, 12)));
            if (Get<float>(game.Player, "nextSword") > beforeNextSword + Epsilon)
            {
                string playerPose = Get<KarmaCharacterAnimator>(game.Player, "animator").CurrentPose;
                bool ordinary = playerPose != "Pierce" && playerPose != "AirSlash";
                if (playerPose == "Pierce") row.directionalSlashActivations++;
                else if (playerPose == "AirSlash") row.airSlashActivations++;
                if (ordinary) row.meleeInputs++;
                if (ordinary && enemy.Health < beforeHealth)
                {
                    row.meleeHits++; row.meleeDamage += beforeHealth - enemy.Health;
                    if (row.firstMeleeHitAt < 0) row.firstMeleeHitAt = now;
                    row.longestPursuitWithoutMeleeHit = Mathf.Max(row.longestPursuitWithoutMeleeHit, now - lastMeleeHit);
                    lastMeleeHit = now;
                    if (state == "Recover" || beforeState == "Recover") row.meleeHitsDuringRecovery++;
                }
            }
            if (state == "Recover" && !enemy.Invulnerable && OrdinarySwordOverlaps()) row.recoveryMeleeOpportunities++;
            if (spec.chase) row.longestPursuitWithoutMeleeHit = Mathf.Max(row.longestPursuitWithoutMeleeHit, now - lastMeleeHit);
            if (now >= nextTrace) { TraceFrame(now, gap, running, guard, selected); nextTrace += 1; }
            if (now + Epsilon >= spec.seconds) CompleteRow();
        }
        catch (Exception error) { Finish(error); }
    }
    static void ObservePattern(float now, float gap, bool guard, EnemyAttack selected)
    {
        lastPatternStart = Get<float>(enemy, "patternStarted");
        bool reachable = (bool)Call(enemy, "SwordAttackCanReach", selected, game.Player.Position);
        bool guardRoot = Get<Transform>(enemy, "swordGuardRoot") != null;
        currentPattern = new Pattern { attack = selected.ToString(), start = now, startGap = gap,
            reachableAtSelection = reachable, guardActive = guard, guardRootAlive = guardRoot };
        row.patterns.Add(currentPattern);
        if (row.firstAttackAt < 0) { row.firstAttackAt = now; row.firstAttackGap = gap; }
        switch (selected)
        {
            case EnemyAttack.SWBasic: row.swBasic++; break;
            case EnemyAttack.SW01: row.sw01++; break;
            case EnemyAttack.SW02: row.sw02++; break;
            case EnemyAttack.SW03: row.sw03++; break;
            case EnemyAttack.SW04: row.sw04++; break;
            case EnemyAttack.SW05: row.sw05++; break;
            default: row.groundStrike++; break;
        }
        if (!reachable && (selected == EnemyAttack.SWBasic || selected == EnemyAttack.SW01 || selected == EnemyAttack.SW02)) row.unreachableSelections++;
        if (guardRoot)
        {
            row.actionsDuringGuard++;
            if (selected == EnemyAttack.SWBasic) row.basicsDuringGuard++;
            if (selected == EnemyAttack.SW01) row.sw01DuringGuard++;
            if (selected == EnemyAttack.SW02) row.sw02DuringGuard++;
        }
        foreach (var retreat in row.retreats)
            if (retreat.end >= 0 && retreat.attackResumedAt < 0) retreat.attackResumedAt = now;
    }
    static void ObserveShots()
    {
        Transform root = Get<Transform>(enemy, "effectsRoot"), guard = Get<Transform>(enemy, "swordGuardRoot");
        foreach (var shot in root.GetComponentsInChildren<KarmaBossAttack>())
        {
            if (!observedShots.Add(shot) || Get<Vector2>(shot, "velocity").sqrMagnitude < 0.01f) continue;
            if (guard != null && shot.transform.IsChildOf(guard)) { row.counterProjectiles++; continue; }
            if (currentPattern != null)
            {
                currentPattern.projectiles++;
                if (currentPattern.attack == "SWBasic") row.basicProjectiles++;
            }
        }
    }
    static bool OrdinarySwordOverlaps()
    {
        float reach = game.Config.Techniques.longSlashReach;
        float width = reach + KarmaSpace.PlayerHalfWidth;
        Vector2 center = game.Player.Position + Vector2.right * game.Player.Facing * (reach - KarmaSpace.PlayerHalfWidth) * 0.5f;
        return enemy.OverlapsDamageArea(center, new Vector2(width, game.Config.Techniques.longSlashHeight));
    }
    static void EndRetreat(float now, bool censored)
    {
        if (currentRetreat == null) return;
        currentRetreat.end = now; currentRetreat.duration = Mathf.Max(0, now - currentRetreat.start);
        currentRetreat.censoredAtWindow = censored;
        currentRetreat.outcome = censored ? "censored-at-measurement-window" : currentRetreat.distance > Epsilon ? "moved-and-ended" : "command-without-world-displacement";
        if (!censored)
        {
            if (currentRetreat.distance > Epsilon) row.completedRetreats++;
            else row.zeroDisplacementCommands++;
        }
        currentRetreat = null;
    }
    static void TraceFrame(float now, float gap, bool running, bool guard, EnemyAttack selected)
    {
        var animator = Get<KarmaCharacterAnimator>(enemy, "animator");
        var renderer = animator.GetComponentInChildren<SpriteRenderer>();
        row.trace.Add(new Trace { time = now, bossX = enemy.Position.x, bossY = enemy.Position.y,
            playerX = game.Player.Position.x, playerY = game.Player.Position.y, horizontalGap = gap,
            retreatRemaining = Get<float>(enemy, "swordRetreatRemaining"), nextRetreat = Get<float>(enemy, "nextSwordRetreat"),
            state = State, intent = enemy.Intent, attack = selected.ToString(), pose = animator.CurrentPose,
            running = running, shield = guard, visibleFlipX = renderer != null && renderer.flipX });
    }
    static void CompleteRow()
    {
        EndRetreat(enemy.FightSeconds, true);
        row.finalGap = Mathf.Abs(game.Player.Position.x - enemy.Position.x);
        row.attackRangeRatio = row.attackRangeSeconds / Mathf.Max(row.seconds, Epsilon);
        row.preferredBandRatio = row.preferredBandSeconds / Mathf.Max(row.seconds, Epsilon);
        Check(row.seconds + Epsilon >= row.requestedSeconds, "the entire requested measurement window executed");
        Check(row.groundStrike == 0, "sword AI never substitutes another boss type's ground strike");
        Check(row.maxRecoveryDisplacement < 0.001f, "recovery leaves a stationary ordinary-melee opportunity");
        Check(row.maxTelegraphDisplacement < 0.001f, "locked grounded attack telegraphs do not drift");
        Check(row.retreatCommandsDuringUltimate == 0 && row.maxCeilingError < 0.001f, "SW05 central ceiling movement has priority over ordinary spacing");
        if (modified)
        {
            foreach (var retreat in row.retreats)
            {
                Check(retreat.distance <= 3.05f && retreat.duration <= 0.7f, "each accepted retreat has bounded actual travel and duration");
                if (!retreat.censoredAtWindow && retreat.distance > Epsilon)
                    Check(retreat.actualMovementFrames >= 2, "a real retreat is visible across more than one rendered frame");
            }
            if (row.minimumRetreatInterval >= 0)
                Check(row.minimumRetreatInterval + Step * 2 >= game.Config.BossTuning.swordRetreatCooldown, "accepted retreat commands honor cooldown");
            Check(row.unreachableSelections == 0, "basic and regular sword selections use actual attack reach");
            if (spec.category == "close") Check(row.retreatCommands > 0 && row.completedRetreats > 0 && row.retreatDistance > 0.5f,
                "stationary close target produces a commanded retreat with measurable root displacement");
            if (spec.category == "band") Check(row.swBasic > 0 && row.sw01 > 0 && row.sw02 > 0 && row.horizontalReversals <= 4,
                "preferred-band combat uses existing basic/SW01/SW02 without repeated left-right oscillation");
            if (spec.category == "far") Check(row.approachDistance > 0.5f && row.approachMovementEpisodes > 0 && row.firstAttackGap <= 10.6f,
                "a distant stationary target sees real approach before a valid engagement-distance attack");
            if (spec.category == "wall") Check(row.firstAttackAt >= 0 && row.firstAttackAt < 8 && row.retreatCommands <= 1 + row.seconds / 4.5f,
                "blocked wall retreat yields to attacks and cannot spin commands indefinitely");
            if (spec.category == "guard") Check(row.guardMovementDistance > 0.2f && row.actionsDuringGuard > 0,
                "independent SW04 permits actual ordinary spacing and subsequent attack selection");
            if (spec.category == "long")
            {
                Check(row.meleeHits > 0 && row.meleeHitsDuringRecovery > 0 && row.longestPursuitWithoutMeleeHit < 35,
                    "120-second physical pursuit retains actual ordinary-melee hits and recovery counterattacks");
                Check(row.swBasic > 0 && row.sw01 + row.sw02 + row.sw03 + row.sw04 + row.sw05 > 0,
                    "long pursuit retains ordinary waves and inherited skills");
                if (spec.order >= 3) Check(row.sw05 > 0, "both final inheritance orders retain the central aerial ultimate");
            }
        }
        Debug.Log("MOVEMENT ROW " + JsonUtility.ToJson(row));
        WriteReport("RUNNING", null);
        enemy.CancelCombat(); index++;
        if (index >= SelectedSpecs.Length) Finish(null); else setupPending = true;
    }
    static void WriteReport(string result, Exception error)
    {
        report.result = result; report.error = error == null ? null : error.ToString();
        File.WriteAllText("sword-movement-validation.json", JsonUtility.ToJson(report, true));
    }
    static void Finish(Exception error)
    {
        if (finishing) return;
        finishing = true; EditorApplication.update -= Pump; Application.logMessageReceived -= CaptureError;
        if (report == null) report = new Report();
        if (keyboard != null)
        {
            InputSystem.RemoveDevice(keyboard); keyboard = null;
            InputSystem.settings.updateMode = previousInputMode;
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
        }
        WriteReport(error == null ? "PASS" : "FAIL", error);
        string verdict = error == null ? "PASS: " + report.runtime + " movement measurement, " + report.scenarios.Count + " scenarios, " + report.assertions + " assertions"
            : "FAIL: " + error;
        File.WriteAllText("sword-movement-validation.txt", verdict + "\n");
        SessionState.SetBool(ActiveKey, false); SessionState.SetBool(ExitKey, true);
        SessionState.SetInt(ExitCodeKey, error == null ? 0 : 1);
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
[DefaultExecutionOrder(-20000)]
public sealed class KarmaSwordMovementInputDriver : MonoBehaviour
{
    public Action Step;
    void Update() { Step?.Invoke(); }
}
[DefaultExecutionOrder(20000)]
public sealed class KarmaSwordMovementObserver : MonoBehaviour
{
    public Action Step;
    void LateUpdate() { Step?.Invoke(); }
}
#endif
