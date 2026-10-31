#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using KarmaPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Real-scene observer: no actor placement, progression/private-state writes,
// health changes, direct damage calls or production Update invocation. All
// progression and protection use existing keyboard input. The only reflective
// write subscribes to the runner's optional isolated-copy diagnostic callback.
[InitializeOnLoad]
public static class KarmaSwordSceneChecks
{
    const string ScenePath = "Assets/K4RMA/Scenes/KarmaPrototype.unity";
    const string SessionKey = "K4RMA.SwordScene.Active", ExitKey = "K4RMA.SwordScene.Exit", CodeKey = "K4RMA.SwordScene.Code";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    const float CaptureStep = 1f / 60;
    static readonly string[] Labels = { "15-approach", "8-attack", "5-pursuit", "retreat-then-attack", "wall", "sudden-distance", "SW04-concurrent", "SW05-ground-return" };
    [Serializable] sealed class Goal { public string phaseLabel; public bool achieved; public float firstAt = -1; public string evidence; }
    [Serializable] sealed class Counter { public string label; public int count; public float requestedAbs, actualAbs; }
    [Serializable] sealed class ConfigRecord
    {
        public string path, guid, fileHash, runtimeJson, type;
        public int instanceId; public bool persistent, ownsConfig;
        public float playerHealth, playerSpeed, enemySpeed, preferred, retreatStart, retreatStop, retreatSpeed, retreatDistance, retreatDuration, retreatCooldown;
    }
    [Serializable] sealed class Sample
    {
        public int frame, enemyId, stage;
        public float time, observed, fightTime, delta, playerHealth, bossHealth, bossMaxHealth, gap, timer, nextRetreat, remaining, locomotionSpeed, gait;
        public string point, phaseLabel, phase, primary, essences, action, state, intent, pose, input;
        public bool running, armored, invulnerable, testInvincible, facingFlip, approaching, motionEnabled;
        public Vector3 boss, art, picture, player, camera, viewport, pictureViewport, artScale, boundsCenter, boundsSize;
        public float cameraSize, cameraAspect;
    }
    [Serializable] sealed class AiEvent
    {
        public float time, observed, requested, actual, gap;
        public int frame, enemyId, stage;
        public string kind, phaseLabel, state, action, primary, intent;
        public Vector2 boss, player;
        public bool running, armored;
    }
    [Serializable] sealed class Row
    {
        public string name, mode, status = "acquiring-natural-progression", error, targetOrder, startPhase;
        public int targetStage, retries, deaths, defeatedBosses, stageChanges, actualFrames, retreatCommands, movementFrames, guardMovementFrames, guardAttackStarts, ultimateStarts;
        public int protectionActivatedAtStage = -1;
        public int guardMovingPoseGuardFrames, guardMovingPoseMoveFrames, targetFrames;
        public float requestedSeconds, observedSeconds, acquisitionSeconds, targetAliveSeconds, retreatActualDistance, approachActualDistance, worldTravel, viewportTravel;
        public bool targetReached, protectionActivated, hookPresent;
        public ConfigRecord config;
        public List<Goal> goals = new List<Goal>();
        public List<Counter> counters = new List<Counter>();
        public List<Counter> targetCounters = new List<Counter>();
        public List<Sample> timeline = new List<Sample>();
        public List<AiEvent> events = new List<AiEvent>();
        public List<string> captures = new List<string>();
    }
    [Serializable] sealed class Report
    {
        public string result, utc, scene, sceneHash, graphicsDevice, graphicsType, observerMethod, error, captureDirectory;
        public int assertions, headlessViewWarnings;
        public float nominalDelta = CaptureStep;
        public bool diagnosticHookPresent;
        public List<Row> scenarios = new List<Row>();
    }
    sealed class Spec
    {
        public string name; public int stage; public bool protect; public float seconds;
        public Spec(string n, int s, bool p, float duration) { name = n; stage = s; protect = p; seconds = duration; }
    }
    static readonly Spec[] Specs = {
        new Spec("natural-health-short", 1, false, 30),
        new Spec("eight-phases-protected-diagnostic", 3, true, 120),
        new Spec("SW04-protected-diagnostic", 2, true, 120),
        new Spec("SW05-ground-return-protected-diagnostic", 3, true, 120)
    };
    static KarmaGame game;
    static Keyboard keyboard;
    static Report report;
    static Row row;
    static Spec spec;
    static int index, pulseFrame, previousStage = -1, previousEnemyId, previousPatternEnemy;
    static string previousPhase, phaseLabel;
    static float acquisitionStarted, observationStarted = -1, previousPattern = -1, pendingRetreatEnd = -1, lastGap, farPeak;
    static float previousObserved, protectedAt, nextTimelineAt, lastChasePress = -100, lastRequestedStop;
    static float measuredCombatSeconds, clockPreviousFight;
    static int clockEnemyId;
    static Vector3 previousLateRoot, previousViewport;
    static bool resetting = true, finishing, sawGuardMovement, sawGuardAttack, sawUltimate, sawUltimateRecovery, sawNearFive, recordTimelineThisFrame, chaseHeld;
    static double deadline;
    static string runtimeError, currentInput;
    static StreamWriter frames;
    static FieldInfo hook;
    static Action<KarmaEnemy, string, float, float> previousHook;
    static InputSettings.UpdateMode previousInput;
    static InputSettings.BackgroundBehavior previousBackground;
    static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
    static readonly HashSet<string> captureKeys = new HashSet<string>();

    static T Read<T>(object target, string name, T fallback = default(T))
    {
        if (target == null) return fallback;
        var field = target.GetType().GetField(name, Flags);
        return field == null ? fallback : (T)field.GetValue(target);
    }
    static KarmaEnemy Enemy => game != null && game.Enemies.Count > 0 ? game.Enemies[0] : null;
    static float Observed
    {
        get
        {
            if (observationStarted < 0) return -1;
            var enemy = Enemy;
            float pending = enemy != null && game.IsCombat && game.StageIndex == spec.stage
                && enemy.GetEntityId().GetHashCode() == clockEnemyId
                ? Mathf.Max(0, enemy.FightSeconds - clockPreviousFight) : 0;
            return measuredCombatSeconds + pending;
        }
    }
    static string State(KarmaEnemy e) => Read<object>(e, "state")?.ToString() ?? "none";
    static string Action(KarmaEnemy e) => e == null ? "none" : Read<EnemyAttack>(e, "selected").ToString();
    static string Essences => game.Player == null ? "none" : string.Join(",", game.Player.Essences.SacrificeOrder);
    static void Check(bool ok, string message) { report.assertions++; if (!ok) throw new Exception(message); }
    static string Hash(string path)
    { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
    static KarmaSwordSceneChecks()
    {
        EditorApplication.playModeStateChanged += OnState;
        if (SessionState.GetBool(SessionKey, false)) EditorApplication.update += Pump;
        if (SessionState.GetBool(ExitKey, false)) EditorApplication.delayCall += ExitEditor;
    }
    public static void Run()
    {
        SessionState.SetBool(SessionKey, true); SessionState.SetBool(ExitKey, false);
        Application.runInBackground = true; PlayerSettings.runInBackground = true;
        EditorApplication.update -= Pump; EditorApplication.update += Pump;
        EditorApplication.delayCall += Begin;
    }
    static void Begin()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Begin; return; }
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Pump()
    {
        EditorApplication.isPaused = false; EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying && game == null && Time.frameCount > 0) OnState(PlayModeStateChange.EnteredPlayMode);
    }
    static void OnState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (SessionState.GetBool(ExitKey, false)) EditorApplication.delayCall += ExitEditor;
            else if (SessionState.GetBool(SessionKey, false)) Finish(new Exception("Actual scene Play Mode ended unexpectedly"));
            return;
        }
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey, false) || game != null) return;
        try
        {
            report = new Report { utc = DateTime.UtcNow.ToString("o"), scene = ScenePath, sceneHash = Hash(ScenePath),
                graphicsDevice = SystemInfo.graphicsDeviceName, graphicsType = SystemInfo.graphicsDeviceType.ToString(),
                captureDirectory = "scene-captures-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                observerMethod = "Actual serialized scene and Config asset; 60 Hz nominal capture. Observed time and all eight phases use accumulated target-boss FightSeconds deltas, excluding hit-stop and time in other rooms/menus; enemy IDs and natural retries distinguish multiple lives. Acquisition timeout alone uses unscaled time. Keyboard-only progression and retries. No HP, actor Transform, AI field, timer or essence writes. Natural-health row never uses P. Protected diagnostic rows toggle the existing P input, possibly from stage one to permit real UI progression; activation stage is recorded and boss HP stays natural. Full-frame CSV records preUpdate/postUpdate/LateUpdate; JSON timeline samples all three points once per combat second. Goals may remain unachieved without making observer execution a false gameplay PASS. Optional runner-only hook records exact calls and movement reasons. GPU captures use the actual GameCamera without repositioning it." };
            Application.logMessageReceived += CaptureError;
            var games = UnityEngine.Object.FindObjectsByType<KarmaGame>();
            Check(games.Length == 1, "Actual scene must contain exactly one bootstrapped KarmaGame");
            game = games[0];
            Check(game.gameObject.scene.path == ScenePath, "Observed game belongs to the authored actual scene");
            Check(AssetDatabase.GetAssetPath(game.Config) == "Assets/K4RMA/Settings/KarmaConfig.asset",
                "Actual scene must use the serialized Config asset, not CreateInstance fallback");
            Check(!Read<bool>(game, "ownsConfig"), "Actual scene must not own a generated fallback Config");
            previousInput = InputSystem.settings.updateMode;
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); KarmaInput.Held(KarmaAction.Attack);
            frames = new StreamWriter("sword-scene-frames.csv", false, new System.Text.UTF8Encoding(false), 65536);
            frames.WriteLine("row,frame,point,time,observed,phaseLabel,phase,stage,primary,input,bossX,bossY,artX,artY,pictureX,pictureY,playerX,playerY,cameraX,cameraY,viewportX,viewportY,pictureViewportX,pictureViewportY,state,action,intent,pose,running,armored,remaining,nextRetreat,locomotionSpeed,gait,playerHealth,bossHealth,testInvincible");
            Time.captureDeltaTime = CaptureStep;
            hook = typeof(KarmaEnemy).GetField("SwordSceneTrace", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            report.diagnosticHookPresent = hook != null;
            if (hook != null)
            {
                previousHook = (Action<KarmaEnemy, string, float, float>)hook.GetValue(null);
                hook.SetValue(null, previousHook + OnAiTrace);
            }
            deadline = EditorApplication.timeSinceStartup + 1800;
            new GameObject("Actual scene input observer").AddComponent<KarmaSwordSceneBeforeDriver>().Step = Before;
            new GameObject("Actual scene post-Update observer").AddComponent<KarmaSwordScenePostDriver>().Step = Post;
            new GameObject("Actual scene late observer").AddComponent<KarmaSwordSceneLateDriver>().Step = Late;
            NextRow();
        }
        catch (Exception error) { Finish(error); }
    }
    static ConfigRecord ConfigSnapshot()
    {
        var config = game.Config; var tuning = config.BossTuning; string path = AssetDatabase.GetAssetPath(config);
        return new ConfigRecord { path = path, guid = AssetDatabase.AssetPathToGUID(path), fileHash = Hash(path),
            runtimeJson = JsonUtility.ToJson(config), type = config.GetType().FullName, instanceId = config.GetEntityId().GetHashCode(),
            persistent = EditorUtility.IsPersistent(config), ownsConfig = Read<bool>(game, "ownsConfig"),
            playerHealth = config.playerHealth, playerSpeed = config.moveSpeed, enemySpeed = config.enemyMoveSpeed * tuning.moveMultiplier,
            preferred = tuning.swordPreferredDistance, retreatStart = Read<float>(tuning, "swordRetreatStartDistance"),
            retreatStop = Read<float>(tuning, "swordRetreatStopDistance"), retreatSpeed = Read<float>(tuning, "swordRetreatSpeed"),
            retreatDistance = Read<float>(tuning, "swordRetreatDistance"), retreatDuration = Read<float>(tuning, "swordRetreatDuration"), retreatCooldown = tuning.swordRetreatCooldown };
    }
    static void NextRow()
    {
        spec = Specs[index]; row = new Row { name = spec.name, mode = spec.protect ? "P-key protected player diagnostic; natural boss HP" : "natural player and boss HP",
            targetStage = spec.stage, targetOrder = "SwordWave,Guard,RisingSlash", requestedSeconds = spec.seconds,
            startPhase = game.Phase.ToString(), config = ConfigSnapshot(), hookPresent = hook != null };
        foreach (string label in Labels) row.goals.Add(new Goal { phaseLabel = label });
        report.scenarios.Add(row); acquisitionStarted = Time.unscaledTime; observationStarted = -1;
        previousPhase = game.Phase.ToString(); previousStage = game.StageIndex; previousEnemyId = 0; previousPatternEnemy = 0;
        previousPattern = -1; previousObserved = -1; resetting = true; nextTimelineAt = 0;
        pendingRetreatEnd = -1; sawGuardMovement = sawGuardAttack = sawUltimate = sawUltimateRecovery = sawNearFive = false;
        lastGap = farPeak = 0; protectedAt = -1;
        measuredCombatSeconds = clockPreviousFight = 0; clockEnemyId = 0;
        chaseHeld = false; lastChasePress = -100; lastRequestedStop = -1;
        Debug.Log("SCENE ROW START " + spec.name + " CONFIG " + JsonUtility.ToJson(row.config));
    }
    static void Send(List<Key> keys)
    { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray())); InputSystem.Update(); }
    static void UiKey(List<Key> keys, Key key) { if ((pulseFrame & 1) == 0) keys.Add(key); }
    static void Control(List<Key> keys)
    {
        if (resetting)
        {
            phaseLabel = "ui-reset";
            if (game.Phase == RunPhase.Title) resetting = false;
            else if (game.Phase == RunPhase.Choosing)
            {
                // Escape only cancels the preview in Choosing. Complete one
                // real remaining choice, wait for the room transition, then
                // pause Combat and use the real title action.
                UiKey(keys, game.PendingChoice.HasValue ? Key.Enter : RemainingChoice());
            }
            else if (game.Phase == RunPhase.Transition) return;
            else if (game.Paused || game.Phase == RunPhase.Defeat || game.Phase == RunPhase.Victory) UiKey(keys, Key.T);
            else UiKey(keys, Key.Escape);
            return;
        }
        if (game.Phase == RunPhase.Title) { phaseLabel = "title-enter"; UiKey(keys, Key.Enter); return; }
        if (game.Phase == RunPhase.Defeat)
        { phaseLabel = "natural-defeat-retry"; UiKey(keys, Key.R); return; }
        if (game.Phase == RunPhase.Victory) { phaseLabel = "natural-victory-restart"; UiKey(keys, Key.R); return; }
        if (game.Phase == RunPhase.Choosing)
        {
            phaseLabel = "natural-choice";
            if (game.PendingChoice.HasValue) UiKey(keys, Key.Enter);
            else UiKey(keys, RemainingChoice());
            return;
        }
        if (game.Phase != RunPhase.Combat || Enemy == null || game.Player == null) { phaseLabel = "natural-transition"; return; }
        float dx = Enemy.Position.x - game.Player.Position.x, gap = Mathf.Abs(dx);
        if (spec.protect && !game.Player.TestInvincible)
        { phaseLabel = "existing-P-key-progression-diagnostic"; UiKey(keys, Key.P); return; }
        if (spec.protect && game.Player.TestInvincible && !row.protectionActivated)
        { row.protectionActivated = true; row.protectionActivatedAtStage = game.StageIndex; }
        bool atTarget = game.StageIndex == spec.stage;
        if (!atTarget)
        {
            phaseLabel = "natural-progression-combat";
            MoveToward(keys, dx, gap, 1.8f); keys.Add(Key.J);
            // Existing active skills aid normal progression; they are keyboard
            // inputs, not damage/state shortcuts. Protected rows use the
            // separate P branch above; the natural-health row never does.
            if (pulseFrame % 30 < 3) { keys.Add(Key.Q); keys.Add(Key.E); }
            return;
        }
        Check(Enemy.PrimaryType == Essence.SwordWave, "Target stage was reached through SwordWave-first real UI inheritance");
        if (!Read<bool>(Enemy, "engaged"))
        {
            // Reach the real activation region before a combat-clock phase
            // can wait at range fifteen with FightSeconds still frozen.
            phaseLabel = "enter-engagement"; keys.Add(dx < 0 ? Key.A : Key.D); return;
        }
        if (observationStarted < 0)
        {
            observationStarted = Time.unscaledTime; row.targetReached = true; row.status = "observing";
            row.acquisitionSeconds = Time.unscaledTime - acquisitionStarted;
            previousObserved = 0; measuredCombatSeconds = 0;
            clockPreviousFight = Enemy.FightSeconds; clockEnemyId = Enemy.GetEntityId().GetHashCode();
            nextTimelineAt = 0;
        }
        if (spec.protect && !game.Player.TestInvincible)
        { phaseLabel = "existing-P-key-diagnostic"; UiKey(keys, Key.P); return; }
        if (game.Player.TestInvincible && protectedAt < 0) { protectedAt = Observed; row.protectionActivated = true; }
        float elapsed = Observed;
        int phaseIndex = index == 1 ? Mathf.Clamp((int)(elapsed / 15), 0, 7) : index == 2 ? 6 : index == 3 ? 7 : 1;
        phaseLabel = Labels[phaseIndex];
        switch (phaseIndex)
        {
            case 0: MoveToward(keys, dx, gap, 15); break;
            case 1: MoveToward(keys, dx, gap, 8); break;
            case 2: MoveToward(keys, dx, gap, 5); break;
            case 3: MoveToward(keys, dx, gap, 3); break;
            case 4: MoveToward(keys, dx, gap, 3); break;
            case 5:
                if (elapsed % 15 < 7 && gap < 16) keys.Add(dx < 0 ? Key.D : Key.A);
                break;
            default: MoveToward(keys, dx, gap, 5); break;
        }
        // Sparse ordinary swings trigger defenses but do not artificially
        // keep the natural-health boss alive. Deaths remain real transitions.
        if ((phaseIndex >= 1 && phaseIndex <= 3 || phaseIndex >= 6) && elapsed % 8 < 0.10f) keys.Add(Key.J);
    }
    static Key RemainingChoice()
    {
        var essences = game.Player.Essences;
        if (essences.HasActive(Essence.SwordWave)) return Key.Digit1;
        if (essences.HasActive(Essence.Guard)) return Key.Digit3;
        return Key.Digit2;
    }
    static void MoveToward(List<Key> keys, float dx, float gap, float stop)
    {
        if (Mathf.Abs(lastRequestedStop - stop) > 0.01f) { chaseHeld = false; lastRequestedStop = stop; }
        if (chaseHeld && gap <= stop) chaseHeld = false;
        if (!chaseHeld && gap > stop + 0.8f && Time.time - lastChasePress > 0.35f)
        { chaseHeld = true; lastChasePress = Time.time; }
        // A stop/resume band and minimum repress interval avoid manufacturing
        // rapid double-tap piercing skills with an oscillating input bot.
        if (chaseHeld) keys.Add(dx < 0 ? Key.A : Key.D);
    }
    static void Before()
    {
        if (finishing) return;
        try
        {
            if (runtimeError != null) throw new Exception(runtimeError);
            Check(EditorApplication.timeSinceStartup < deadline, "Actual-scene observer wall-clock deadline");
            pulseFrame++; var keys = new List<Key>(); Control(keys); currentInput = string.Join("+", keys); Send(keys);
            float timelineClock = observationStarted < 0 ? Time.unscaledTime : Observed;
            recordTimelineThisFrame = timelineClock >= nextTimelineAt;
            if (recordTimelineThisFrame) nextTimelineAt = timelineClock + 1;
            CaptureSample("preUpdate");
        }
        catch (Exception error) { Finish(error); }
    }
    static void Post() { if (!finishing) TrySample("postUpdate"); }
    static void TrySample(string point) { try { CaptureSample(point); } catch (Exception error) { Finish(error); } }
    static Sample CaptureSample(string point)
    {
        var e = Enemy; var player = game.Player; var camera = game.GameCamera;
        var art = Read<Transform>(e, "art"); var animator = Read<KarmaCharacterAnimator>(e, "animator");
        var picture = animator == null ? null : animator.GetComponentInChildren<SpriteRenderer>(true);
        var motion = art == null ? null : art.GetComponent<KarmaWaveMotion>();
        var bounds = e == null ? default(Bounds) : e.DamageBounds;
        var sample = new Sample { frame = Time.frameCount, time = Time.unscaledTime, observed = Observed, delta = Time.deltaTime,
            point = point, phaseLabel = phaseLabel, phase = game.Phase.ToString(), stage = game.StageIndex, input = currentInput,
            enemyId = e == null ? 0 : e.GetEntityId().GetHashCode(), primary = e == null ? "none" : e.PrimaryType?.ToString() ?? "Neutral",
            essences = Essences, fightTime = e == null ? 0 : e.FightSeconds, state = State(e), action = Action(e), intent = e == null ? "none" : e.Intent,
            running = Read<bool>(e, "running"), armored = e != null && e.Armored, invulnerable = e != null && e.Invulnerable,
            boss = e == null ? Vector3.zero : e.transform.position, art = art == null ? Vector3.zero : art.position,
            artScale = art == null ? Vector3.zero : art.localScale, picture = picture == null ? Vector3.zero : picture.transform.position,
            player = player == null ? Vector3.zero : player.transform.position, playerHealth = player == null ? 0 : player.Health,
            bossHealth = e == null ? 0 : e.Health, bossMaxHealth = e == null ? 0 : e.MaxHealth,
            gap = e == null || player == null ? 0 : Mathf.Abs(e.Position.x - player.Position.x), testInvincible = player != null && player.TestInvincible,
            camera = camera == null ? Vector3.zero : camera.transform.position, cameraSize = camera == null ? 0 : camera.orthographicSize,
            cameraAspect = camera == null ? 0 : camera.aspect, viewport = camera == null || e == null ? Vector3.zero : camera.WorldToViewportPoint(e.Position),
            pictureViewport = camera == null || picture == null ? Vector3.zero : camera.WorldToViewportPoint(picture.transform.position),
            pose = animator == null ? "none" : animator.CurrentPose, facingFlip = picture != null && picture.flipX,
            motionEnabled = motion != null && motion.enabled, locomotionSpeed = Read<float>(motion, "locomotionSpeed"), gait = Read<float>(motion, "gaitPhase"),
            boundsCenter = bounds.center, boundsSize = bounds.size, timer = Read<float>(e, "timer"), nextRetreat = Read<float>(e, "nextSwordRetreat"),
            remaining = Read<float>(e, "swordRetreatRemaining"), approaching = Read<bool>(e, "swordApproaching") };
        if (recordTimelineThisFrame) row.timeline.Add(sample);
        WriteFrame(sample);
        return sample;
    }
    static void OnAiTrace(KarmaEnemy e, string label, float requested, float actual)
    {
        if (finishing || row == null || game == null || e == null) return;
        Count(row.counters, label, requested, actual);
        bool measured = observationStarted >= 0 && game.StageIndex == spec.stage;
        if (measured) Count(row.targetCounters, label, requested, actual);
        if (label == "retreat-command" && measured) row.retreatCommands++;
        if (label == "retreat-ended" && measured) pendingRetreatEnd = Observed;
        if (label == "move-applied" && measured)
        {
            if (Read<float>(e, "swordRetreatRemaining") > 0) row.retreatActualDistance += Mathf.Abs(actual);
            else if (Read<bool>(e, "swordApproaching")) row.approachActualDistance += Mathf.Abs(actual);
            if (Mathf.Abs(actual) > 0.0001f) row.movementFrames++;
            if (e.Armored && Mathf.Abs(actual) > 0.0001f) { row.guardMovementFrames++; sawGuardMovement = true; }
        }
        if (label == "update" || label == "spacing-evaluate" || label == "retreat-check" || label == "approach-check") return;
        row.events.Add(new AiEvent { time = Time.unscaledTime, observed = Observed, frame = Time.frameCount,
            kind = label, phaseLabel = phaseLabel, enemyId = e.GetEntityId().GetHashCode(), stage = game.StageIndex,
            primary = e.PrimaryType?.ToString() ?? "Neutral", state = State(e), action = Action(e), intent = e.Intent,
            requested = requested, actual = actual, boss = e.Position, player = game.Player == null ? Vector2.zero : game.Player.Position,
            gap = game.Player == null ? 0 : Mathf.Abs(e.Position.x - game.Player.Position.x), running = Read<bool>(e, "running"), armored = e.Armored });
    }
    static void Count(List<Counter> counters, string label, float requested, float actual)
    {
        Counter counter = counters.Find(item => item.label == label);
        if (counter == null) { counter = new Counter { label = label }; counters.Add(counter); }
        counter.count++; counter.requestedAbs += Mathf.Abs(requested); counter.actualAbs += Mathf.Abs(actual);
    }
    static void Mark(int goal, string evidence)
    {
        if (row.goals[goal].achieved) return;
        row.goals[goal].achieved = true; row.goals[goal].firstAt = Observed; row.goals[goal].evidence = evidence;
    }
    static void Late()
    {
        if (finishing) return;
        try
        {
            // Commit exactly the production combat-clock increment. A new
            // boss starts a fresh source clock and must not add its setup age.
            var clockEnemy = Enemy;
            if (observationStarted >= 0 && clockEnemy != null && game.StageIndex == spec.stage)
            {
                int entityId = clockEnemy.GetEntityId().GetHashCode();
                if (entityId == clockEnemyId && game.IsCombat)
                    measuredCombatSeconds += Mathf.Max(0, clockEnemy.FightSeconds - clockPreviousFight);
                clockEnemyId = entityId; clockPreviousFight = clockEnemy.FightSeconds;
            }
            else clockEnemyId = 0;
            Sample sample = CaptureSample("LateUpdate"); row.actualFrames++;
            string phase = game.Phase.ToString();
            if (phase != previousPhase)
            {
                if (phase == "Defeat") { row.deaths++; row.retries++; }
                if (phase == "Choosing" || phase == "Victory") row.defeatedBosses++;
                row.events.Add(new AiEvent { kind = "phase:" + previousPhase + "->" + phase, phaseLabel = phaseLabel,
                    time = Time.unscaledTime, observed = Observed, frame = Time.frameCount, stage = game.StageIndex });
                previousPhase = phase;
            }
            if (previousStage != game.StageIndex) { row.stageChanges++; previousStage = game.StageIndex; }
            if (observationStarted >= 0)
            {
                row.observedSeconds = Observed;
                var e = Enemy;
                if (e != null && game.StageIndex == spec.stage)
                {
                    row.targetFrames++;
                    float deltaTime = previousObserved < 0 ? 0 : Observed - previousObserved;
                    if (game.IsCombat && e.Alive) row.targetAliveSeconds += deltaTime;
                    float movement = previousEnemyId == e.GetEntityId().GetHashCode() ? sample.boss.x - previousLateRoot.x : 0;
                    row.worldTravel += Mathf.Abs(movement);
                    if (sample.armored && Mathf.Abs(movement) > 0.0001f)
                    {
                        if (sample.pose == "Guard") row.guardMovingPoseGuardFrames++;
                        if (sample.pose == "Move") row.guardMovingPoseMoveFrames++;
                    }
                    if (previousEnemyId == e.GetEntityId().GetHashCode()) row.viewportTravel += Mathf.Abs(sample.viewport.x - previousViewport.x);
                    if (sample.gap <= 5.5f) sawNearFive = true;
                    if (lastGap >= 14.5f && sample.gap < lastGap && movement * (sample.player.x - sample.boss.x) > 0)
                        Mark(0, "Actual root approached a player at least 14.5 world units away");
                    float pattern = Read<float>(e, "patternStarted");
                    bool started = sample.running && (previousPatternEnemy != e.GetEntityId().GetHashCode() || pattern != previousPattern);
                    if (started)
                    {
                        row.events.Add(new AiEvent { kind = "attack-start", time = Time.unscaledTime, observed = Observed,
                            frame = Time.frameCount, enemyId = e.GetEntityId().GetHashCode(), stage = game.StageIndex, state = sample.state,
                            action = sample.action, gap = sample.gap, boss = e.Position, player = game.Player.Position, armored = e.Armored });
                        if (sample.gap >= 7 && sample.gap <= 9) Mark(1, "Actual AI attack selection at a 7-9 world-unit gap");
                        if (pendingRetreatEnd >= 0 && Observed >= pendingRetreatEnd) Mark(3, "New attack started after an observed retreat end");
                        if (Read<Transform>(e, "swordGuardRoot") != null) { row.guardAttackStarts++; sawGuardAttack = true; }
                        if (sample.action == "SW05") { row.ultimateStarts++; sawUltimate = true; }
                    }
                    if (sawNearFive && movement * (sample.player.x - sample.boss.x) < -0.0001f && sample.intent.Contains("후퇴"))
                        Mark(2, "Actual retreat displacement after the player closed to about five world units");
                    bool nearWall = sample.boss.x <= KarmaStage.BossLeftWall + 0.8f || sample.boss.x >= KarmaStage.BossRightWall - 0.8f;
                    if (nearWall && started) Mark(4, "Boss selected an attack at an actual arena wall position");
                    if (phaseLabel == Labels[5]) farPeak = Mathf.Max(farPeak, sample.gap);
                    if (farPeak >= 14.5f && sample.gap <= 9.5f && sample.approaching) Mark(5, "Actual approach recovered from the player's real move to a distant gap");
                    if (sawGuardMovement && sawGuardAttack) Mark(6, "Armored world movement and an attack with the independent sword guard root were both observed");
                    if (sawUltimate && sample.action == "SW05" && sample.state == "Recover") sawUltimateRecovery = true;
                    if (sawUltimateRecovery && started && sample.action != "SW05" && Mathf.Abs(sample.boss.y - KarmaSpace.GuardianGroundY) < 0.05f)
                        Mark(7, "SW05 completed through ground recovery and a later grounded AI attack started");
                    previousPattern = pattern; previousPatternEnemy = e.GetEntityId().GetHashCode(); lastGap = sample.gap;
                    previousEnemyId = e.GetEntityId().GetHashCode(); previousLateRoot = sample.boss; previousViewport = sample.viewport;
                }
                else previousEnemyId = 0;
                previousObserved = Observed;
                TryCapture();
                if (Observed >= spec.seconds) EndRow();
            }
            else if (Time.unscaledTime - acquisitionStarted > 240)
            { row.error = "Target stage not reached within 240 seconds of real keyboard progression"; EndRow(); }
        }
        catch (Exception error) { Finish(error); }
    }
    static void WriteFrame(Sample s)
    {
        if (frames == null) return;
        object[] values = { index, s.frame, s.point, s.time, s.observed, s.phaseLabel, s.phase, s.stage, s.primary, s.input,
            s.boss.x, s.boss.y, s.art.x, s.art.y, s.picture.x, s.picture.y, s.player.x, s.player.y,
            s.camera.x, s.camera.y, s.viewport.x, s.viewport.y, s.pictureViewport.x, s.pictureViewport.y,
            s.state, s.action, s.intent, s.pose, s.running, s.armored, s.remaining, s.nextRetreat,
            s.locomotionSpeed, s.gait, s.playerHealth, s.bossHealth, s.testInvincible };
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) frames.Write(',');
            string value = Convert.ToString(values[i], System.Globalization.CultureInfo.InvariantCulture) ?? "";
            frames.Write('"'); frames.Write(value.Replace("\"", "\"\"")); frames.Write('"');
        }
        frames.WriteLine();
    }
    static void TryCapture()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || game.GameCamera == null || Enemy == null) return;
        string captureKey = index + "-" + phaseLabel;
        if (Observed < 2 || !captureKeys.Add(captureKey)) return;
        Directory.CreateDirectory(report.captureDirectory);
        string path = report.captureDirectory + "/scene-" + captureKey + ".png";
        var camera = game.GameCamera; float aspect = camera.aspect;
        int width = 960, height = Mathf.Max(1, Mathf.RoundToInt(width / Mathf.Max(0.1f, aspect)));
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        target.antiAliasing = 1; target.Create(); var previous = RenderTexture.active; Texture2D image = null;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0, false); image.Apply(false, false);
            File.WriteAllBytes(path, image.EncodeToPNG()); row.captures.Add(path);
            Check(Mathf.Abs(camera.aspect - aspect) < 0.0001f, "GPU observer must preserve the actual camera aspect");
        }
        finally { RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); if (image != null) UnityEngine.Object.DestroyImmediate(image); }
    }
    static void EndRow()
    {
        row.acquisitionSeconds = observationStarted < 0 ? Time.unscaledTime - acquisitionStarted : row.acquisitionSeconds;
        row.status = !row.targetReached ? "target-not-reached" : row.goals.Exists(goal => !goal.achieved) ? "measured-with-unachieved-goals" : "all-goals-observed";
        Write("RUNNING", null); index++;
        frames?.Flush();
        if (index == Specs.Length) Finish(null); else NextRow();
    }
    static void Write(string result, Exception error)
    {
        report.result = result; report.error = error?.ToString();
        File.WriteAllText("sword-scene-validation.json", JsonUtility.ToJson(report));
    }
    static void CaptureError(string message, string stack, LogType type)
    {
        if (type == LogType.Error && (message == "No graphic device is available to initialize the view." || message == "No graphic device is available to show the window.")
            && stack != null && System.Text.RegularExpressions.Regex.IsMatch(stack, @"UnityEditor\.(GUIView|ContainerWindow|EditorApplicationLayout|WindowLayout)\b"))
        { report.headlessViewWarnings++; return; }
        if (!finishing && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) runtimeError = message + "\n" + stack;
    }
    static void Finish(Exception error)
    {
        if (finishing) return; finishing = true; EditorApplication.update -= Pump; Application.logMessageReceived -= CaptureError;
        if (report == null) report = new Report();
        frames?.Dispose(); frames = null;
        if (hook != null) hook.SetValue(null, previousHook);
        if (keyboard != null)
        {
            InputSystem.RemoveDevice(keyboard); keyboard = null; InputSystem.settings.updateMode = previousInput;
            InputSystem.settings.backgroundBehavior = previousBackground; InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
        }
        Write(error == null ? "PASS" : "FAIL", error);
        string result = error == null ? "PASS: actual-scene observer completed " + report.scenarios.Count + " measurement rows; inspect individual achieved goals and statuses"
            : "FAIL: " + error;
        File.WriteAllText("sword-scene-validation.txt", result + "\n");
        SessionState.SetBool(SessionKey, false); SessionState.SetBool(ExitKey, true); SessionState.SetInt(CodeKey, error == null ? 0 : 1);
        if (error == null) Debug.Log(result); else Debug.LogException(error);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); else EditorApplication.delayCall += ExitEditor;
    }
    static void ExitEditor()
    {
        if (!SessionState.GetBool(ExitKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall += ExitEditor; return; }
        int code = SessionState.GetInt(CodeKey, 1); SessionState.SetBool(ExitKey, false); EditorApplication.Exit(code);
    }
}
[DefaultExecutionOrder(-20000)] public sealed class KarmaSwordSceneBeforeDriver : MonoBehaviour
{ public Action Step; void Update() { Step?.Invoke(); } }
[DefaultExecutionOrder(10000)] public sealed class KarmaSwordScenePostDriver : MonoBehaviour
{ public Action Step; void Update() { Step?.Invoke(); } }
[DefaultExecutionOrder(20000)] public sealed class KarmaSwordSceneLateDriver : MonoBehaviour
{ public Action Step; void LateUpdate() { Step?.Invoke(); } }
#endif
