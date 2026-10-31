#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
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

// Authored-scene measurement, driven exclusively through the existing keyboard
// inputs. Reflection is read-only. No actor placement, HP/config/AI writes,
// direct damage calls, injected game progression, or production Update calls.
[InitializeOnLoad]
public static class KarmaSwordSpacingChecks
{
    const string ScenePath = "Assets/K4RMA/Scenes/KarmaPrototype.unity";
    const string ActiveKey = "K4RMA.SwordSpacing.Active", ExitKey = "K4RMA.SwordSpacing.Exit", CodeKey = "K4RMA.SwordSpacing.Code";
    const string VideoStagesKey = "K4RMA.SwordSpacing.VideoStages";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    sealed class Spec
    {
        public string name, mode; public int stage; public float seconds; public bool protect;
        public Spec(string name, string mode, int stage, float seconds, bool protect = true)
        { this.name = name; this.mode = mode; this.stage = stage; this.seconds = seconds; this.protect = protect; }
    }
    static readonly Spec[] Specs = {
        new Spec("sustained-chase-J", "chase", 1, 45),
        new Spec("sustained-chase-jump-J", "jump", 1, 45),
        new Spec("rangehold-release-J", "release", 1, 30),
        new Spec("far-approach", "far", 1, 20),
        new Spec("SW04-chase-J", "guard", 2, 35),
        new Spec("natural-survival-J", "chase", 1, 20, false),
        new Spec("wall-pursuit-then-J", "wall", 1, 80)
    };
    static readonly Spec[] VideoSpecs = {
        new Spec("video-stage2-chase-jump-J", "video-jump", 2, 45),
        new Spec("video-stage3-chase-jump-J", "video-jump", 3, 45),
        new Spec("video-stage3-rangehold-release-J", "release", 3, 30)
    };
    static Spec[] ActiveSpecs => SessionState.GetBool(VideoStagesKey, false) ? VideoSpecs : Specs;
    [Serializable] sealed class Event
    {
        public string kind, phase, action, state, input; public int frame, bossId, stage;
        public float observed, unscaled, gap, clearance, bossX, playerX, bossHealth, playerHealth;
    }
    [Serializable] sealed class Episode
    {
        public int bossId, commandFrame; public string inputAtCommand, actionAtCommand, stateAtCommand, endReason;
        public float commandAt, commandUnscaled, startGap, startClearance, startBossX, startPlayerX, target, requestedDistance;
        public float firstMovementAt = -1, endAt = -1, endUnscaled = -1, endGap, endClearance, endBossX, endPlayerX;
        public float commandToMovement = -1, commandToComplete = -1, commandToCompleteUnscaled = -1;
        public float bossTravel, playerTravel, relativeGapGain, maximumGap, maximumClearance;
        public float firstOutsideAt = -1, outsideHorizontalSeconds, outsideSwordAreaSeconds, outsideAfterEndSeconds;
        public float reacquiredAt = -1, reacquireAfterEnd = -1, reacquireAfterFirstOutside = -1;
        public bool ended, outsideAtCompletion, successfulHorizontalSeparation, immediateJReachAtCompletion, nearWallAtCommand, censored;
        public int JStartsAfterEnd, JHitsAfterEnd, attacksAfterEnd;
        public List<string> resumedAttacks = new List<string>();
    }
    [Serializable] sealed class Row
    {
        public string name, mode, status, error, configJson, configHash, essenceOrder, inputPolicy;
        public int stage, frames, lives, deaths, targetBossDeaths, stageChanges, protectionFirstStage = -1;
        public int JStarts, JHits, lethalJHits, attackStarts, retreatCommands, completedRetreats, successfulRetreats;
        public int approachStarts, guardAttackStarts, guardMovementFrames, guardMovingMovePoseFrames;
        public float requestedSeconds, observedSeconds, acquisitionSeconds, minimumGap = float.MaxValue, meanGap;
        public float gapTimeIntegral, measuredSeconds, outsideHorizontalSeconds, outsideActualSwordSeconds;
        public float pressurePendingSeconds, pendingWhileRunningSeconds, attackCommittedSeconds, bossStunSeconds, playerStunSeconds;
        public float hitStopUnscaledSeconds, pausedUnscaledSeconds, recoverySeconds, recoveryReachableSeconds;
        public float wallSeconds, wallReachableSeconds, bossTravel, playerTravel, meanViewportGap, viewportGapIntegral;
        public float protectionActivatedAt = -1;
        public bool playerProtected, reachedTarget, sawSWBasic, sawSW01, sawSW02, sawSW04, sawBossStun, wallReached;
        public float firstWallReachedAt = -1;
        public List<int> bossIds = new List<int>();
        public List<Event> events = new List<Event>();
        public List<Episode> retreats = new List<Episode>();
        public List<string> captures = new List<string>();
    }
    [Serializable] sealed class Report
    {
        public string result, error, utc, scene, sceneHash, graphicsDevice, graphicsType, captureDirectory, methodology;
        public string clockMode;
        public float timeCaptureDeltaTime;
        public int assertions, captureCount, headlessViewWarnings;
        public bool graphicsAvailable, optionalTraceHookUsed = false;
        public List<Row> scenarios = new List<Row>();
    }
    sealed class Sample
    {
        public int frame, bossId, stage, facing; public string phase, label, state, action, pose, input, intent;
        public float unscaled, observed, fight, dt, unscaledDt, gap, distance, horizontalClearance, actualClearance, verticalClearance;
        public float timeScale;
        public float playerHealth, bossHealth, bossMaxHealth, timer, nextRetreat, remaining, target, retreatDirection, retreatUntil;
        public float nextApproach, nextSword, patternStarted, cameraSize, cameraAspect, viewportGap;
        public bool targetBoss, running, pending, committed, pressure, overlap, towardOverlap, armored, swordGuard, guardRoot;
        public bool stunned, playerStunned, paused, protectedPlayer, approaching, nearWall, airborne, continuousAirSlash;
        public Vector3 boss, player, camera, bossViewport, playerViewport, bossScreen, playerScreen, boundsCenter, boundsSize, swordCenter, swordSize;
    }
    static KarmaGame game; static Keyboard keyboard; static Report report; static Row row; static Spec spec;
    static StreamWriter frames; static int index, pulse, previousStage = -1;
    static bool resetting, finishing, chaseHeld; static int chaseDirection;
    static string currentInput, label, runtimeError, previousPhase;
    static float lastMovePress = -100, requestedStop = -1, rowStarted, observed, clockFight, releaseUntil, nextJumpAt;
    static int clockBossId; static double deadline;
    static int videoDirection, videoBossId; static float videoCrossedAt = -1;
    static Sample previous, before; static Episode episode;
    static float firstCaptureAt = -1, retreatCaptureAt = -1; static bool capturedStart, capturedProgress, capturedEnd, capturedAfter;
    static Episode threatCaptureEpisode;
    static bool capturedThreatProgress, capturedThreatEnd, capturedThreatAfter, reportedThreatProgressMissing;
    static InputSettings.UpdateMode previousUpdate; static InputSettings.BackgroundBehavior previousBackground;
    static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
    static readonly MethodInfo SwordOverlap = typeof(KarmaPlayer).GetMethod("SwordAreaOverlaps", Flags);
    static readonly HashSet<string> captureKeys = new HashSet<string>();
    static KarmaEnemy Enemy => game != null && game.Enemies.Count > 0 ? game.Enemies[0] : null;
    static T Read<T>(object target, string name, T fallback = default(T))
    {
        if (target == null) return fallback;
        var type = target.GetType(); var field = type.GetField(name, Flags);
        if (field != null) return (T)field.GetValue(target);
        var property = type.GetProperty(name, Flags); return property == null ? fallback : (T)property.GetValue(target, null);
    }
    static void Check(bool condition, string message) { report.assertions++; if (!condition) throw new Exception(message); }
    static string Hash(string path)
    { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
    static KarmaSwordSpacingChecks()
    {
        EditorApplication.playModeStateChanged += OnState;
        if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Pump;
        if (SessionState.GetBool(ExitKey, false)) EditorApplication.delayCall += ExitEditor;
    }
    public static void Run()
    { SessionState.SetBool(VideoStagesKey, false); StartObservation(); }
    public static void RunVideoStages()
    { SessionState.SetBool(VideoStagesKey, true); StartObservation(); }
    static void StartObservation()
    {
        SessionState.SetBool(ActiveKey, true); SessionState.SetBool(ExitKey, false);
        Application.runInBackground = true; PlayerSettings.runInBackground = true;
        EditorApplication.update -= Pump; EditorApplication.update += Pump; EditorApplication.delayCall += Begin;
    }
    static void Begin()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Begin; return; }
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); EditorApplication.EnterPlaymode();
    }
    static void Pump()
    {
        EditorApplication.isPaused = false; EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying && game == null && Time.frameCount > 0) OnState(PlayModeStateChange.EnteredPlayMode);
    }
    static void OnState(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            if (SessionState.GetBool(ExitKey, false)) EditorApplication.delayCall += ExitEditor;
            else if (SessionState.GetBool(ActiveKey, false)) Finish(new Exception("Actual scene Play Mode ended unexpectedly"));
            return;
        }
        if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(ActiveKey, false) || game != null) return;
        try
        {
            report = new Report { utc = DateTime.UtcNow.ToString("o"), scene = ScenePath, sceneHash = Hash(ScenePath),
                graphicsDevice = SystemInfo.graphicsDeviceName, graphicsType = SystemInfo.graphicsDeviceType.ToString(),
                graphicsAvailable = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null,
                captureDirectory = "spacing-captures-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"),
                methodology = "Authored serialized KarmaPrototype scene and KarmaConfig asset. Existing keyboard UI progresses SwordWave first, then Guard, then RisingSlash. All actor/HP/config/AI access is read-only; no diagnostic hook writes. Protected rows toggle existing P using keyboard during acquisition and record activation; boss HP remains natural. Natural-survival row toggles P off and uses natural player/boss HP. Target-stage J is held continuously; acquisition can also use existing Q/E inputs. Natural target deaths cause keyboard-only title/reprogression and measured FightSeconds accumulates across boss IDs, excluding hit-stop/menu time. Every LateUpdate goes to CSV, including acquisition, input, real reflected SwordAreaOverlaps, toward-boss potential box overlap, signed horizontal box-to-DamageBounds clearance, actual HP loss, world/screen/camera, attack/stun/pending/retreat fields. J hits are HP decreases on a newly committed ordinary J, with no target-stage active-skill inputs; lethal departures are separately inferred. Horizontal clearance > 0 defines genuine distance outside J reach independent of jump height/facing. Episode windows continue after retreat until reacquisition/new command/boss loss/row end; censored flags identify incomplete windows. PASS means observer execution only, never gameplay success. GPU captures preserve actual camera and aspect, with at most 30 PNG captures total. No material/shader/asset generation." };
            Application.logMessageReceived += CaptureError;
            var games = UnityEngine.Object.FindObjectsByType<KarmaGame>();
            Check(games.Length == 1, "One actual bootstrapped scene game required"); game = games[0];
            Check(game.gameObject.scene.path == ScenePath, "Game must belong to authored scene");
            Check(AssetDatabase.GetAssetPath(game.Config) == "Assets/K4RMA/Settings/KarmaConfig.asset", "Actual serialized config required");
            Check(!Read<bool>(game, "ownsConfig"), "Generated fallback config forbidden");
            Check(SwordOverlap != null, "Real player SwordAreaOverlaps query required");
            previousUpdate = InputSystem.settings.updateMode; previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); KarmaInput.Held(KarmaAction.Attack);
            bool realClock = SessionState.GetBool(VideoStagesKey, false);
            report.clockMode = realClock ? "real-clock-normal-timeScale" : "fixed-capture-diagnostic";
            report.timeCaptureDeltaTime = realClock ? 0 : 1f / 60;
            Time.captureDeltaTime = report.timeCaptureDeltaTime;
            report.methodology += realClock
                ? " Canonical video-stage clock: captureDeltaTime=0; ordinary Unity deltaTime/timeScale and real hit-stop remain in effect. FightSeconds excludes actual zero-delta hit-stop frames. GPU captures may increase frame cost; every actual delta and unscaled delta is recorded."
                : " Supplementary seven-row diagnostic clock: captureDeltaTime=1/60. Fixed capture time can override the ordinary zero-delta effect of timeScale=0, so this run does not certify real-play hit-stop behavior and is not the canonical video-stage comparison.";
            frames = new StreamWriter("sword-spacing-frames.csv", false, new System.Text.UTF8Encoding(false), 65536);
            frames.WriteLine("row,name,frame,unscaled,observed,fight,dt,unscaledDt,target,phase,stage,label,input,bossId,bossX,bossY,playerX,playerY,centerGap,distance2d,horizontalClearance,actualFacingClearance,verticalClearance,actualSwordOverlap,towardSwordOverlap,playerFacing,boundsX,boundsY,boundsW,boundsH,swordX,swordY,swordW,swordH,bossHealth,bossMaxHealth,playerHealth,playerProtected,state,action,pose,intent,running,pressurePending,playerAttackCommitted,playerSwordPressure,retreatRemaining,retreatTarget,retreatDirection,retreatUntil,nextRetreat,timer,patternStarted,approaching,nextApproach,playerNextSword,armored,swordGuard,guardRoot,bossStunned,playerStunned,paused,airborne,nearWall,cameraX,cameraY,cameraSize,cameraAspect,bossViewportX,bossViewportY,playerViewportX,playerViewportY,viewportGap,bossScreenX,bossScreenY,playerScreenX,playerScreenY,continuousAirSlash,timeScale");
            deadline = EditorApplication.timeSinceStartup + 2400;
            new GameObject("Spacing observer keyboard driver").AddComponent<KarmaSwordSpacingBeforeDriver>().Step = Before;
            new GameObject("Spacing observer LateUpdate sampler").AddComponent<KarmaSwordSpacingLateDriver>().Step = Late;
            NextRow();
        }
        catch (Exception error) { Finish(error); }
    }
    static void NextRow()
    {
        spec = ActiveSpecs[index]; string path = AssetDatabase.GetAssetPath(game.Config);
        row = new Row { name = spec.name, mode = spec.mode, stage = spec.stage, requestedSeconds = spec.seconds,
            playerProtected = spec.protect, status = "acquiring-natural-progression", configJson = JsonUtility.ToJson(game.Config),
            configHash = Hash(path), essenceOrder = "SwordWave,Guard,RisingSlash",
            inputPolicy = spec.mode == "video-jump" ? "Hold J and a horizontal direction continuously; never stop at gap 2.5. Initially move toward the boss, continue the same direction for at least 0.35 target-combat seconds after crossing the boss center, then reverse toward the boss. Reset the crossing delay if the boss moves back ahead. Space is pressed every 1.3 target-combat seconds. Direction represses are separated by the crossing delay and traversal, avoiding rapid double-tap remnant synthesis. No active-skill inputs in the target stage." : null };
        report.scenarios.Add(row); rowStarted = Time.unscaledTime; observed = clockFight = 0; clockBossId = 0;
        previous = before = null; episode = null; resetting = true; chaseHeld = false; requestedStop = -1;
        previousStage = game.StageIndex; previousPhase = game.Phase.ToString(); releaseUntil = nextJumpAt = 0;
        videoDirection = videoBossId = 0; videoCrossedAt = -1;
        firstCaptureAt = retreatCaptureAt = -1; capturedStart = capturedProgress = capturedEnd = capturedAfter = false;
        threatCaptureEpisode = null;
        capturedThreatProgress = capturedThreatEnd = capturedThreatAfter = reportedThreatProgressMissing = false;
        Debug.Log("SPACING ROW START " + row.name + " protected=" + spec.protect + " config=" + row.configJson);
    }
    static void UiKey(List<Key> keys, Key key) { if ((pulse & 1) == 0) keys.Add(key); }
    static Key RemainingChoice()
    {
        if (game.Player.Essences.HasActive(Essence.SwordWave)) return Key.Digit1;
        if (game.Player.Essences.HasActive(Essence.Guard)) return Key.Digit3;
        return Key.Digit2;
    }
    static void MoveToward(List<Key> keys, float dx, float gap, float stop)
    {
        if (Mathf.Abs(requestedStop - stop) > 0.01f) { chaseHeld = false; requestedStop = stop; }
        int direction = dx < 0 ? -1 : 1;
        if (chaseHeld && (gap <= stop || direction != chaseDirection)) chaseHeld = false;
        if (!chaseHeld && gap > stop + 0.45f && Time.time - lastMovePress > 0.4f)
        { chaseHeld = true; chaseDirection = direction; lastMovePress = Time.time; }
        // Prevent oscillatory movement inputs from synthesizing the existing
        // SwordWave remnant double-tap piercing skill.
        if (chaseHeld) keys.Add(chaseDirection < 0 ? Key.A : Key.D);
    }
    static void Control(List<Key> keys)
    {
        if (!resetting && game.StageIndex > spec.stage && game.Phase != RunPhase.Title
            && game.Phase != RunPhase.Transition) resetting = true;
        if (resetting)
        {
            label = "keyboard-title-reset";
            if (game.Phase == RunPhase.Title) resetting = false;
            else if (game.Phase == RunPhase.Choosing) UiKey(keys, game.PendingChoice.HasValue ? Key.Enter : RemainingChoice());
            else if (game.Phase == RunPhase.Transition) return;
            else if (game.Paused || game.Phase == RunPhase.Defeat || game.Phase == RunPhase.Victory) UiKey(keys, Key.T);
            else UiKey(keys, Key.Escape);
            return;
        }
        if (game.Phase == RunPhase.Title) { label = "title-enter"; UiKey(keys, Key.Enter); return; }
        if (game.Phase == RunPhase.Defeat || game.Phase == RunPhase.Victory) { label = "natural-retry"; UiKey(keys, Key.R); return; }
        if (game.Phase == RunPhase.Choosing) { label = "SwordWave-first-real-choice"; UiKey(keys, game.PendingChoice.HasValue ? Key.Enter : RemainingChoice()); return; }
        var e = Enemy; var p = game.Player;
        if (game.Phase != RunPhase.Combat || e == null || p == null) { label = "natural-transition"; return; }
        if (p.TestInvincible != spec.protect) { label = spec.protect ? "existing-P-enable" : "existing-P-disable"; UiKey(keys, Key.P); return; }
        if (spec.protect && row.protectionActivatedAt < 0)
        { row.protectionActivatedAt = observed; row.protectionFirstStage = game.StageIndex; }
        float dx = e.Position.x - p.Position.x, gap = Mathf.Abs(dx);
        if (game.StageIndex != spec.stage)
        {
            label = "natural-progression-J-Q-E"; MoveToward(keys, dx, gap, 2.5f); keys.Add(Key.J);
            if (pulse % 30 < 3) { keys.Add(Key.Q); keys.Add(Key.E); }
            return;
        }
        Check(e.PrimaryType == Essence.SwordWave, "Natural SwordWave-first progression required");
        if (!Read<bool>(e, "engaged")) { label = "enter-engagement"; keys.Add(dx < 0 ? Key.A : Key.D); return; }
        if (!row.reachedTarget)
        { row.reachedTarget = true; row.status = "observing"; row.acquisitionSeconds = Time.unscaledTime - rowStarted; }
        if (spec.mode == "video-jump")
        {
            int desired = dx < 0 ? -1 : 1;
            int bossId = e.GetEntityId().GetHashCode();
            if (videoDirection == 0 || videoBossId != bossId)
            { videoDirection = desired; videoBossId = bossId; videoCrossedAt = -1; }
            if (desired != videoDirection)
            {
                if (videoCrossedAt < 0) videoCrossedAt = observed;
                if (observed - videoCrossedAt >= 0.35f)
                { videoDirection = desired; videoCrossedAt = -1; }
            }
            else videoCrossedAt = -1;
            label = videoCrossedAt >= 0 ? "video-cross-center-continue-jump-J" : "video-continuous-crossing-pursuit-jump-J";
            keys.Add(videoDirection < 0 ? Key.A : Key.D); keys.Add(Key.J);
            if (observed >= nextJumpAt) { keys.Add(Key.Space); nextJumpAt = observed + 1.3f; }
            return;
        }
        if (spec.mode == "release")
        {
            label = observed < releaseUntil ? "rangehold-stop-after-J" : "rangehold-8.25-pressure-J";
            if (observed >= releaseUntil) MoveToward(keys, dx, gap, 8.1f); else chaseHeld = false;
            keys.Add(Key.J); return;
        }
        if (spec.mode == "far")
        {
            float cycle = observed % 10;
            label = cycle < 3 ? "move-away-to-16" : "stationary-far-approach-observation";
            if (cycle < 3 && gap < 16) keys.Add(dx < 0 ? Key.D : Key.A);
            if (cycle >= 7) keys.Add(Key.J);
            return;
        }
        if (spec.mode == "wall")
        {
            // Preserve the natural boss long enough to reach the real wall.
            // This is the same input policy for baseline and improved builds:
            // chase with no J until x53.4 (or the symmetric left threshold),
            // then hold J for the remainder of this natural boss life.
            bool atWall = e.Position.x >= KarmaStage.BossRightWall - 0.5f || e.Position.x <= KarmaStage.BossLeftWall + 0.5f;
            if (atWall && !row.wallReached)
            { row.wallReached = true; row.firstWallReachedAt = observed; AddEvent("wall-target-reached-before-J", ReadSample()); }
            label = row.wallReached ? "wall-continuous-pursuit-J" : "wall-approach-no-J";
            MoveToward(keys, dx, gap, 2.5f);
            if (row.wallReached) keys.Add(Key.J);
            return;
        }
        bool wall = e.Position.x <= KarmaStage.BossLeftWall + 1 || e.Position.x >= KarmaStage.BossRightWall - 1;
        label = wall ? "wall-continuous-pursuit-J" : spec.mode == "jump" ? "continuous-pursuit-jump-J" : "continuous-pursuit-J";
        MoveToward(keys, dx, gap, 2.5f); keys.Add(Key.J);
        if (spec.mode == "jump" && observed >= nextJumpAt) { keys.Add(Key.Space); nextJumpAt = observed + 1.3f; }
    }
    static void Before()
    {
        if (finishing) return;
        try
        {
            if (runtimeError != null) throw new Exception(runtimeError);
            Check(EditorApplication.timeSinceStartup < deadline, "Actual scene observer wall-clock deadline");
            pulse++; var keys = new List<Key>(); Control(keys); currentInput = string.Join("+", keys);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray())); InputSystem.Update();
            before = ReadSample();
        }
        catch (Exception error) { Finish(error); }
    }
    static Sample ReadSample()
    {
        var e = Enemy; var p = game.Player; var c = game.GameCamera;
        var animator = Read<KarmaCharacterAnimator>(e, "animator");
        Bounds bounds = e == null ? default(Bounds) : e.DamageBounds;
        float reach = game.Config.Techniques.longSlashReach, halfWidth = KarmaSpace.PlayerHalfWidth;
        Vector2 swordSize = new Vector2(reach + halfWidth, game.Config.Techniques.longSlashHeight);
        Vector2 swordCenter = p == null ? Vector2.zero : p.Position + Vector2.right * p.Facing * (reach - halfWidth) * 0.5f;
        Vector2 towardCenter = p == null || e == null ? swordCenter : p.Position + Vector2.right * (e.Position.x < p.Position.x ? -1 : 1) * (reach - halfWidth) * 0.5f;
        float clearance = p == null || e == null ? 0 : Mathf.Abs(bounds.center.x - p.Position.x) - bounds.extents.x - reach;
        float actualClearance = Mathf.Max(bounds.min.x - (swordCenter.x + swordSize.x / 2), swordCenter.x - swordSize.x / 2 - bounds.max.x);
        float vertical = Mathf.Abs(swordCenter.y - bounds.center.y) - swordSize.y / 2 - bounds.extents.y;
        return new Sample { frame = Time.frameCount, unscaled = Time.unscaledTime, observed = observed, fight = e == null ? 0 : e.FightSeconds,
            dt = Time.deltaTime, unscaledDt = Time.unscaledDeltaTime, timeScale = Time.timeScale, phase = game.Phase.ToString(), stage = game.StageIndex, label = label, input = currentInput,
            bossId = e == null ? 0 : e.GetEntityId().GetHashCode(), targetBoss = e != null && game.StageIndex == spec.stage && e.PrimaryType == Essence.SwordWave && Read<bool>(e, "engaged"),
            boss = e == null ? Vector3.zero : e.transform.position, player = p == null ? Vector3.zero : p.transform.position,
            gap = e == null || p == null ? 0 : Mathf.Abs(e.Position.x - p.Position.x), distance = e == null || p == null ? 0 : Vector2.Distance(e.Position, p.Position),
            facing = p == null ? 0 : p.Facing, horizontalClearance = clearance, actualClearance = actualClearance, verticalClearance = vertical,
            overlap = e != null && p != null && (bool)SwordOverlap.Invoke(p, new object[] { e }),
            towardOverlap = e != null && p != null && e.OverlapsDamageArea(towardCenter, swordSize),
            boundsCenter = bounds.center, boundsSize = bounds.size, swordCenter = swordCenter, swordSize = swordSize,
            playerHealth = p == null ? 0 : p.Health, bossHealth = e == null ? 0 : e.Health, bossMaxHealth = e == null ? 0 : e.MaxHealth,
            state = Read<object>(e, "state")?.ToString() ?? "none", action = Read<EnemyAttack>(e, "selected").ToString(),
            pose = animator == null ? "none" : animator.CurrentPose, intent = e == null ? "none" : e.Intent,
            running = Read<bool>(e, "running"), pending = Read<bool>(e, "swordPressurePending"), committed = Read<bool>(p, "AttackCommitted"), pressure = Read<bool>(p, "SwordPressureActive"),
            remaining = Read<float>(e, "swordRetreatRemaining"), target = Read<float>(e, "swordRetreatTarget"), retreatDirection = Read<float>(e, "swordRetreatDirection"),
            retreatUntil = Read<float>(e, "swordRetreatUntil"), nextRetreat = Read<float>(e, "nextSwordRetreat"), timer = Read<float>(e, "timer"), patternStarted = Read<float>(e, "patternStarted"),
            approaching = Read<bool>(e, "swordApproaching"), nextApproach = Read<float>(e, "nextSwordApproach"), nextSword = Read<float>(p, "nextSword"),
            armored = e != null && e.Armored, swordGuard = Read<bool>(e, "swordGuard"), guardRoot = Read<Transform>(e, "swordGuardRoot") != null,
            stunned = e != null && e.Stunned, playerStunned = p != null && p.Stunned, paused = game.Paused, protectedPlayer = p != null && p.TestInvincible,
            airborne = p != null && p.Airborne, continuousAirSlash = Read<bool>(p, "airSlashRunning"),
            nearWall = e != null && (e.Position.x <= KarmaStage.BossLeftWall + 1 || e.Position.x >= KarmaStage.BossRightWall - 1),
            camera = c == null ? Vector3.zero : c.transform.position, cameraSize = c == null ? 0 : c.orthographicSize, cameraAspect = c == null ? 0 : c.aspect,
            bossViewport = c == null || e == null ? Vector3.zero : c.WorldToViewportPoint(e.Position), playerViewport = c == null || p == null ? Vector3.zero : c.WorldToViewportPoint(p.Position),
            bossScreen = c == null || e == null ? Vector3.zero : c.WorldToScreenPoint(e.Position), playerScreen = c == null || p == null ? Vector3.zero : c.WorldToScreenPoint(p.Position),
            viewportGap = c == null || p == null || e == null ? 0 : Mathf.Abs(c.WorldToViewportPoint(e.Position).x - c.WorldToViewportPoint(p.Position).x) };
    }
    static void AddEvent(string kind, Sample s)
    {
        row.events.Add(new Event { kind = kind, frame = s.frame, observed = s.observed, unscaled = s.unscaled, bossId = s.bossId, stage = s.stage,
            phase = s.phase, state = s.state, action = s.action, input = s.input, gap = s.gap, clearance = s.horizontalClearance,
            bossX = s.boss.x, playerX = s.player.x, bossHealth = s.bossHealth, playerHealth = s.playerHealth });
    }
    static void Late()
    {
        if (finishing) return;
        try
        {
            Sample s = ReadSample();
            float dt = s.targetBoss && s.phase == "Combat" && !s.paused && s.bossId == clockBossId ? Mathf.Max(0, s.fight - clockFight) : 0;
            if (s.targetBoss) { clockBossId = s.bossId; clockFight = s.fight; } else clockBossId = 0;
            observed += dt; s.observed = observed; WriteFrame(s);
            if (s.phase != previousPhase)
            {
                AddEvent("phase:" + previousPhase + "->" + s.phase, s);
                if (s.phase == "Defeat") row.deaths++;
                if (previous != null && previous.targetBoss && (s.phase == "Choosing" || s.phase == "Victory")) row.targetBossDeaths++;
                previousPhase = s.phase;
            }
            if (previousStage != s.stage) { row.stageChanges++; previousStage = s.stage; }
            if (!s.targetBoss || s.phase != "Combat")
            {
                if (episode != null) CloseEpisode(s, "target-lost", true);
                // A target-stage ordinary J can kill the boss before the late
                // sample. Separate this inferred lethal hit from read-back HP.
                if (before != null && before.targetBoss && before.phase == "Combat" && s.nextSword > before.nextSword + 0.001f && before.bossHealth > 0 && (s.phase == "Choosing" || s.phase == "Victory"))
                { row.JStarts++; row.JHits++; row.lethalJHits++; AddEvent("lethal-J-hit-inferred-from-natural-progression", before); }
            }
            else Measure(s, dt);
            row.observedSeconds = observed; previous = s;
            if (observed >= spec.seconds) EndRow();
            else if (Time.unscaledTime - rowStarted > 720) { row.error = "Target observation not completed within 720 unscaled seconds of keyboard progression"; EndRow(); }
        }
        catch (Exception error) { Finish(error); }
    }
    static void Measure(Sample s, float dt)
    {
        row.frames++;
        if (!row.bossIds.Contains(s.bossId)) { row.bossIds.Add(s.bossId); row.lives++; AddEvent("target-life-start", s); }
        row.minimumGap = Mathf.Min(row.minimumGap, s.gap); row.gapTimeIntegral += s.gap * dt;
        row.viewportGapIntegral += s.viewportGap * dt; row.measuredSeconds += dt;
        if (s.horizontalClearance > 0) row.outsideHorizontalSeconds += dt;
        if (!s.overlap) row.outsideActualSwordSeconds += dt;
        if (s.pending) row.pressurePendingSeconds += dt;
        if (s.pending && s.running) row.pendingWhileRunningSeconds += dt;
        if (s.committed) row.attackCommittedSeconds += dt;
        if (s.stunned) { row.bossStunSeconds += dt; row.sawBossStun = true; }
        if (s.playerStunned) row.playerStunSeconds += dt;
        if (s.paused) row.pausedUnscaledSeconds += s.unscaledDt;
        if (!s.paused && s.dt <= 0) row.hitStopUnscaledSeconds += s.unscaledDt;
        if (s.state == "Recover") { row.recoverySeconds += dt; if (s.towardOverlap && !s.armored) row.recoveryReachableSeconds += dt; }
        if (s.nearWall) { row.wallSeconds += dt; if (s.towardOverlap) row.wallReachableSeconds += dt; }
        bool same = previous != null && previous.targetBoss && previous.bossId == s.bossId;
        float bossTravel = same ? Mathf.Abs(s.boss.x - previous.boss.x) : 0;
        float playerTravel = same ? Mathf.Abs(s.player.x - previous.player.x) : 0;
        row.bossTravel += bossTravel; row.playerTravel += playerTravel;
        if (s.swordGuard && bossTravel > 0.0001f) { row.guardMovementFrames++; if (s.pose == "Move") row.guardMovingMovePoseFrames++; }
        bool newJ = before != null && before.targetBoss && before.bossId == s.bossId && s.nextSword > before.nextSword + 0.001f;
        bool hitJ = newJ && s.bossHealth < before.bossHealth - 0.001f;
        if (newJ)
        {
            row.JStarts++; if (hitJ) row.JHits++;
            AddEvent(hitJ ? "J-start-and-HP-hit" : "J-start-without-HP-hit", s);
            if (spec.mode == "release" && s.gap <= 8.6f && observed >= releaseUntil) releaseUntil = observed + 1.35f;
        }
        bool attack = s.running && (!same || !previous.running || Mathf.Abs(s.patternStarted - previous.patternStarted) > 0.001f);
        if (attack)
        {
            row.attackStarts++; AddEvent("attack-start", s);
            if (s.action == "SWBasic") row.sawSWBasic = true;
            if (s.action == "SW01") row.sawSW01 = true;
            if (s.action == "SW02") row.sawSW02 = true;
            if (s.action == "SW04") row.sawSW04 = true;
            if (s.guardRoot) row.guardAttackStarts++;
        }
        if (s.approaching && (!same || !previous.approaching)) { row.approachStarts++; AddEvent("approach-start", s); }
        if (same && previous.approaching && !s.approaching) AddEvent("approach-end", s);
        bool command = same && s.nextRetreat > previous.nextRetreat + 0.001f;
        if (command)
        {
            if (episode != null) CloseEpisode(previous, "next-command", !episode.ended);
            episode = new Episode { bossId = s.bossId, commandFrame = s.frame, commandAt = observed, commandUnscaled = s.unscaled,
                inputAtCommand = s.input, actionAtCommand = s.action, stateAtCommand = s.state, target = s.target,
                requestedDistance = game.Config.BossTuning.swordRetreatDistance, startGap = previous.gap, startClearance = previous.horizontalClearance,
                startBossX = previous.boss.x, startPlayerX = previous.player.x, maximumGap = s.gap, maximumClearance = s.horizontalClearance,
                nearWallAtCommand = s.nearWall };
            row.retreats.Add(episode); row.retreatCommands++; AddEvent("retreat-command-inferred-nextCooldown-advance", s);
            if (!capturedStart) { Capture("first-retreat-start", s); capturedStart = true; retreatCaptureAt = observed; }
            if (SessionState.GetBool(VideoStagesKey, false) && !resetting && threatCaptureEpisode == null
                && episode.startClearance <= 0)
            {
                // Retain the precise episode; a new life cannot supply its end.
                threatCaptureEpisode = episode;
                Capture("first-threat-retreat-start", s);
                AddEvent("first-threat-retreat-capture-selected", s);
            }
        }
        if (episode != null && episode.bossId == s.bossId)
        {
            if (!episode.ended)
            {
                episode.bossTravel += bossTravel; episode.playerTravel += playerTravel;
                if (episode.firstMovementAt < 0 && bossTravel > 0.0001f)
                { episode.firstMovementAt = observed; episode.commandToMovement = observed - episode.commandAt; }
            }
            episode.maximumGap = Mathf.Max(episode.maximumGap, s.gap); episode.maximumClearance = Mathf.Max(episode.maximumClearance, s.horizontalClearance);
            if (s.horizontalClearance > 0)
            {
                if (episode.firstOutsideAt < 0) episode.firstOutsideAt = observed;
                episode.outsideHorizontalSeconds += dt;
                if (episode.ended) episode.outsideAfterEndSeconds += dt;
            }
            if (!s.overlap) episode.outsideSwordAreaSeconds += dt;
            if (!episode.ended && s.remaining <= 0.001f) CompleteRetreat(s);
            if (episode.ended)
            {
                if (newJ) episode.JStartsAfterEnd++; if (hitJ) episode.JHitsAfterEnd++;
                if (attack) { episode.attacksAfterEnd++; if (!episode.resumedAttacks.Contains(s.action)) episode.resumedAttacks.Add(s.action); }
                if (episode.firstOutsideAt >= 0 && s.horizontalClearance <= 0 && s.towardOverlap && episode.reacquiredAt < 0)
                {
                    episode.reacquiredAt = observed; episode.reacquireAfterEnd = Mathf.Max(0, observed - episode.endAt);
                    episode.reacquireAfterFirstOutside = observed - episode.firstOutsideAt; AddEvent("J-reach-reacquired-after-retreat", s);
                }
            }
        }
        if (firstCaptureAt < 0 && observed >= 1) { Capture("target-start", s); firstCaptureAt = observed; }
        if (retreatCaptureAt >= 0 && !capturedProgress && observed >= retreatCaptureAt + 0.18f)
        { Capture("first-retreat-progress", s); capturedProgress = true; }
        if (capturedEnd && !capturedAfter && episode != null && episode.endAt >= 0 && observed >= episode.endAt + 0.4f)
        { Capture("first-retreat-after", s); capturedAfter = true; }
        CaptureThreatRetreat(s);
    }
    static void CaptureThreatRetreat(Sample s)
    {
        var selected = threatCaptureEpisode;
        if (selected == null || s.bossId != selected.bossId || !s.targetBoss || resetting) return;
        if (!capturedThreatProgress && !selected.ended && s.frame > selected.commandFrame && s.remaining > 0.001f)
        {
            // Use an actual following movement frame, never an invented pose.
            Capture("first-threat-retreat-progress", s); capturedThreatProgress = true;
        }
        if (selected.ended && !capturedThreatEnd)
        {
            if (!capturedThreatProgress && !reportedThreatProgressMissing)
            {
                AddEvent("first-threat-retreat-progress-unavailable-no-intermediate-frame", s);
                reportedThreatProgressMissing = true;
            }
            Capture("first-threat-retreat-end", s); capturedThreatEnd = true;
        }
        if (selected.ended && !capturedThreatAfter && observed >= selected.endAt + 0.4f)
        { Capture("first-threat-retreat-after", s); capturedThreatAfter = true; }
    }
    static void CompleteRetreat(Sample s)
    {
        episode.ended = true; episode.endAt = observed; episode.endUnscaled = s.unscaled;
        episode.commandToComplete = observed - episode.commandAt; episode.commandToCompleteUnscaled = s.unscaled - episode.commandUnscaled;
        episode.endGap = s.gap; episode.endClearance = s.horizontalClearance; episode.endBossX = s.boss.x; episode.endPlayerX = s.player.x;
        episode.relativeGapGain = s.gap - episode.startGap; episode.outsideAtCompletion = s.horizontalClearance > 0;
        episode.immediateJReachAtCompletion = s.towardOverlap; episode.successfulHorizontalSeparation = episode.outsideAtCompletion;
        episode.endReason = s.nearWall ? "wall-or-budget-observed" : s.fight >= s.retreatUntil ? "deadline-observed" : "remaining-ended-observed";
        row.completedRetreats++; if (episode.successfulHorizontalSeparation) row.successfulRetreats++;
        AddEvent("retreat-completed", s);
        if (!capturedEnd) { Capture("first-retreat-end", s); capturedEnd = true; }
    }
    static void CloseEpisode(Sample s, string reason, bool censored)
    {
        if (episode == null) return;
        if (!episode.ended) { episode.endReason = reason; episode.censored = true; }
        if (censored || episode.reacquiredAt < 0) episode.censored = true;
        episode = null;
    }
    static void WriteFrame(Sample s)
    {
        object[] values = { index, row.name, s.frame, s.unscaled, s.observed, s.fight, s.dt, s.unscaledDt, s.targetBoss, s.phase, s.stage, s.label, s.input, s.bossId,
            s.boss.x, s.boss.y, s.player.x, s.player.y, s.gap, s.distance, s.horizontalClearance, s.actualClearance, s.verticalClearance, s.overlap, s.towardOverlap, s.facing,
            s.boundsCenter.x, s.boundsCenter.y, s.boundsSize.x, s.boundsSize.y, s.swordCenter.x, s.swordCenter.y, s.swordSize.x, s.swordSize.y,
            s.bossHealth, s.bossMaxHealth, s.playerHealth, s.protectedPlayer, s.state, s.action, s.pose, s.intent, s.running, s.pending, s.committed, s.pressure,
            s.remaining, s.target, s.retreatDirection, s.retreatUntil, s.nextRetreat, s.timer, s.patternStarted, s.approaching, s.nextApproach, s.nextSword,
            s.armored, s.swordGuard, s.guardRoot, s.stunned, s.playerStunned, s.paused, s.airborne, s.nearWall, s.camera.x, s.camera.y, s.cameraSize, s.cameraAspect,
            s.bossViewport.x, s.bossViewport.y, s.playerViewport.x, s.playerViewport.y, s.viewportGap, s.bossScreen.x, s.bossScreen.y, s.playerScreen.x, s.playerScreen.y, s.continuousAirSlash, s.timeScale };
        for (int i = 0; i < values.Length; i++)
        { if (i > 0) frames.Write(','); string value = Convert.ToString(values[i], Invariant) ?? ""; frames.Write('"'); frames.Write(value.Replace("\"", "\"\"")); frames.Write('"'); }
        frames.WriteLine();
    }
    static void Capture(string kind, Sample s)
    {
        if (!report.graphicsAvailable || report.captureCount >= 30 || game.GameCamera == null) return;
        string key = index + "-" + kind; if (!captureKeys.Add(key)) return;
        Directory.CreateDirectory(report.captureDirectory);
        string path = report.captureDirectory + "/spacing-" + key + "-" + s.frame + ".png";
        var camera = game.GameCamera; float aspect = camera.aspect; int width = 960, height = Mathf.Max(1, Mathf.RoundToInt(width / Mathf.Max(0.1f, aspect)));
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); target.antiAliasing = 1; target.Create();
        var active = RenderTexture.active; Texture2D image = null;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0, false); image.Apply(false, false);
            File.WriteAllBytes(path, image.EncodeToPNG()); row.captures.Add(path); report.captureCount++;
            Check(Mathf.Abs(camera.aspect - aspect) < 0.0001f, "GPU measurement preserves actual camera aspect");
        }
        finally { RenderTexture.active = active; target.Release(); UnityEngine.Object.DestroyImmediate(target); if (image != null) UnityEngine.Object.DestroyImmediate(image); }
    }
    static void EndRow()
    {
        CloseEpisode(previous, "row-end", true);
        row.meanGap = row.measuredSeconds > 0 ? row.gapTimeIntegral / row.measuredSeconds : 0;
        row.meanViewportGap = row.measuredSeconds > 0 ? row.viewportGapIntegral / row.measuredSeconds : 0;
        if (row.minimumGap == float.MaxValue) row.minimumGap = 0;
        row.status = row.error != null ? "incomplete-measurement" : !row.reachedTarget ? "target-not-reached"
            : spec.mode == "wall" && !row.wallReached ? "measured-wall-target-unachieved" : "measured-review-behavior-separately";
        frames.Flush(); Write("RUNNING", null); index++;
        if (index >= ActiveSpecs.Length) Finish(null); else NextRow();
    }
    static void CaptureError(string message, string stack, LogType type)
    {
        if (type == LogType.Error && (message == "No graphic device is available to initialize the view." || message == "No graphic device is available to show the window.")
            && stack != null && System.Text.RegularExpressions.Regex.IsMatch(stack, @"UnityEditor\.(GUIView|ContainerWindow|EditorApplicationLayout|WindowLayout)\b"))
        { report.headlessViewWarnings++; return; }
        if (!finishing && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) runtimeError = message + "\n" + stack;
    }
    static void Write(string result, Exception error)
    {
        report.result = result; report.error = error?.ToString();
        File.WriteAllText("sword-spacing-validation.json", JsonUtility.ToJson(report, true));
    }
    static void Finish(Exception error)
    {
        if (finishing) return; finishing = true; EditorApplication.update -= Pump; Application.logMessageReceived -= CaptureError;
        if (report == null) report = new Report(); frames?.Dispose(); frames = null;
        if (keyboard != null)
        {
            InputSystem.RemoveDevice(keyboard); keyboard = null; InputSystem.settings.updateMode = previousUpdate;
            InputSystem.settings.backgroundBehavior = previousBackground; InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
        }
        bool incomplete = report.scenarios.Exists(item => item.error != null || !item.reachedTarget || item.observedSeconds + 0.05f < item.requestedSeconds);
        string result = error != null ? "FAIL" : incomplete ? "INCOMPLETE" : "OBSERVER_COMPLETED"; Write(result, error);
        File.WriteAllText("sword-spacing-validation.txt", result + ": authored scene keyboard observation only; compare per-retreat relative-gap/clearance and attack-opportunity metrics before judging gameplay.\n" + (error == null ? "" : error.ToString()));
        SessionState.SetBool(ActiveKey, false); SessionState.SetBool(ExitKey, true); SessionState.SetInt(CodeKey, error == null && !incomplete ? 0 : 1);
        if (error == null) Debug.Log("SPACING " + result); else Debug.LogException(error);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); else EditorApplication.delayCall += ExitEditor;
    }
    static void ExitEditor()
    {
        if (!SessionState.GetBool(ExitKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall += ExitEditor; return; }
        int code = SessionState.GetInt(CodeKey, 1); SessionState.SetBool(ExitKey, false); EditorApplication.Exit(code);
    }
}
[DefaultExecutionOrder(-20000)] public sealed class KarmaSwordSpacingBeforeDriver : MonoBehaviour
{ public Action Step; void Update() { Step?.Invoke(); } }
[DefaultExecutionOrder(20000)] public sealed class KarmaSwordSpacingLateDriver : MonoBehaviour
{ public Action Step; void LateUpdate() { Step?.Invoke(); } }
#endif
