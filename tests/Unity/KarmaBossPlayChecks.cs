#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;
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
        EnemyAttack.GD02, EnemyAttack.GD03, EnemyAttack.GD04, EnemyAttack.GD05, EnemyAttack.GD05,
        EnemyAttack.GroundStrike, EnemyAttack.SW02, EnemyAttack.UP01, EnemyAttack.UP02, EnemyAttack.GD02,
        EnemyAttack.SW01, EnemyAttack.SW03, EnemyAttack.SW04,
        EnemyAttack.SW04, EnemyAttack.SW04, EnemyAttack.SW04, EnemyAttack.SW05,
        EnemyAttack.SW05, EnemyAttack.SW05, EnemyAttack.SW05, EnemyAttack.SW05, EnemyAttack.SW04, EnemyAttack.SW04 };
    const int FailureCase = 15, EnrageStart = 16;
    const int LateWardCase = 23, InterruptWardCase = 24, DeathWardCase = 25, RestartWardCase = 26;
    const int InterruptAerialCase = 28, PlayerDeathAerialCase = 29, RestartAerialCase = 30;
    const int DeathAerialCase = 31;
    const int NoTriggerWardCase = 32, StunWardCase = 33;
    const string FormingAerialName = "Forming aerial sword / no damage";
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
    static bool swordCombatChecked, aimedSlashChecked;
    static readonly HashSet<KarmaBossAttack> movingAttacks = new HashSet<KarmaBossAttack>(), scarAttacks = new HashSet<KarmaBossAttack>();
    static readonly HashSet<KarmaAttackWarning> pathWarnings = new HashSet<KarmaAttackWarning>();
    static readonly HashSet<string> wavePhases = new HashSet<string>();
    static readonly Dictionary<int, Vector2> lockedWaveAims = new Dictionary<int, Vector2>();
    static readonly HashSet<Transform> waveSwings = new HashSet<Transform>();
    sealed class ScarCue
    {
        public KarmaBossAttack shot;
        public KarmaAttackContact contact;
        public Vector2 origin;
        public float launchedAt, stoppedAt = -1;
        public bool checkedSustainedDamage, checkedLateContact;
    }
    static readonly List<ScarCue> scarCues = new List<ScarCue>();
    static float scarSustainStartedAt = -1;
    static KarmaBossAttack latestWaveShot;
    static float lastWardShot, bladeObservedAt;
    static Vector3 bladeDuringWarning;
    static bool observingWardWarning, sawWardWarning, sawWardRotation;
    sealed class WardCue
    {
        public KarmaBossAttack shot;
        public KarmaAttackContact contact;
        public Vector2 origin, aim, player;
        public float trackingAt, lockedAt, launchedAt;
    }
    static readonly List<WardCue> wardCues = new List<WardCue>();
    static WardCue aimingWard;
    static float wardStartedAt, wardShieldOff, wardRecoveryAt;
    static int wardOrbitMinimum;
    sealed class AerialCue
    {
        public SpriteRenderer formingSword;
        public Vector2 origin, aim;
        public Quaternion rotation;
        public Vector3 readyScale;
        public float firstSeen, alpha, formed, pathLength, launchLife, launchAt, stoppedAt;
        public Vector2 finalPosition;
        public float maxProjectileStep;
        public bool grew, projectileStopped;
        public int launchFrame;
        public KarmaAttackContact contact;
    }
    static readonly Dictionary<int, AerialCue> aerialCues = new Dictionary<int, AerialCue>();
    static int[] aerialGroupSizes = new int[0];
    static int aerialPeakAttacks, aerialPeakObjects;
    static ProfilerRecorder allocationRecorder, frameRecorder;
    static readonly List<float> aerialFrameMs = new List<float>();
    static long aerialGcBytes, aerialMaxGc;
    static int aerialGcCollections, aerialPeakWarnings;
    static float aerialFightStarted, aerialShieldOn, aerialShieldOff, aerialRecoveryAt;
    [Serializable]
    sealed class AerialMeasurement
    {
        public bool enraged;
        public int seed, total;
        public int[] counts;
        public float[] releaseTimes, flightLifetimes, travelDistances;
        public float duration, shieldOn, shieldOff, shieldDuration, recoveryAt, recoveryDuration, tailAfterLastRelease, maxEndpointError;
        public int simulatedFps, peakProjectiles, peakObjects, peakWarnings, gcCollections;
        public long gcBytes, maxFrameGcBytes;
        public float mainThreadP95Ms, mainThreadMaxMs;
    }
    [Serializable]
    sealed class AerialMeasurements
    {
        public string result, utc;
        public AerialMeasurement[] cases;
    }
    static readonly List<AerialMeasurement> aerialMeasurements = new List<AerialMeasurement>();
    static readonly HashSet<int> randomAerialSeeds = new HashSet<int>();
    static bool aerialPauseActive, aerialPauseDone;
    static bool aerialFormationPauseDone, aerialProjectilePauseDone;
    static int aerialPauseFrame;
    static float aerialPauseFight, aerialPauseRemaining, aerialPauseAlpha;
    static Vector3 aerialPauseShotPosition, aerialPauseBladePosition, aerialPauseBladeScale;
    static Quaternion aerialPauseBladeRotation;
    static KarmaBossAttack aerialPauseShot;
    static SpriteRenderer aerialPauseBlade;
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
        aerialMeasurements.Clear();
        Application.logMessageReceived += CaptureWarningError;
        Debug.Log("K4RMA PLAY CHECK START");
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.runInBackground = true;
        PlayerSettings.runInBackground = true;
        SessionState.SetBool(Key,true);
        EditorApplication.update += AutoTick;
        // Let the Editor's delayed Search database initialization finish first.
        EditorApplication.delayCall += () => { EditorApplication.delayCall += BeginPlay; };
    }
    static void BeginPlay()
    {
        if (EditorApplication.isUpdating || EditorApplication.isCompiling)
        { EditorApplication.delayCall += BeginPlay; return; }
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
            KarmaMigrationChecks.Run(game);
            game.Config.playerHealth = 1000000;
            game.StartRun();
            game.Player.Essences.Sacrifice(Essence.SwordWave);
            game.Player.Essences.Sacrifice(Essence.RisingSlash);
            game.Player.Essences.Sacrifice(Essence.Guard);
            Call(game,"LoadRoom",true);
            enemy = game.Enemies[0];
            game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            deadline = EditorApplication.timeSinceStartup + 600;
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
            if (!geometryChecked)
            {
                PlayerUtilityChecks(); SpatialChecks(); CameraChecks(); BoundaryChecks(); ProgressionReloadChecks(); WarningGeometryChecks();
                BossDamageBoundsChecks(); WavePosePriorityChecks(); WaveMotionChecks();
                assertions += KarmaWavePolicyChecks.Run(game, enemy);
                assertions += KarmaWaveVFXChecks.Run(game, enemy);
                assertions += KarmaWaveMotionPolishChecks.Run(game, enemy);
                assertions += KarmaWaveGuardianAppearanceChecks.Run(game, enemy);
                assertions += KarmaSwordWardEscapeChecks.Run(game, enemy);
                assertions += KarmaWaveExpansionChecks.Run(game, enemy);
                ScarInterruptChecks();
                geometryChecked = true;
            }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play checks exceeded wall-clock deadline");
            if (!EditorApplication.isPlaying) throw new Exception("Play mode ended unexpectedly");
            if (index >= Cases.Length)
            {
                if (!aimedSlashChecked)
                {
                    if (!KarmaAimedSlashChecks.Tick(game, out int aimedChecks)) return;
                    assertions += aimedChecks; aimedSlashChecked = true; enemy = game.Enemies[0];
                }
                if (!swordCombatChecked)
                {
                    if (!KarmaSwordCombatChecks.Tick(game, out int checks)) return;
                    assertions += checks; swordCombatChecked = true; enemy = game.Enemies[0];
                }
                if(LifecycleChecks()) Finish(null); return;
            }
            if (++ticks % 200 == 0) Debug.Log("CHECK TICK frame="+Time.frameCount+" time="+Time.time+" paused="+EditorApplication.isPaused+" state="+Get<object>(enemy,"state"));
            if (!started) StartCase();
            if (Cases[index] == EnemyAttack.SW04 && !Get<bool>(enemy, "running")) Set(enemy, "timer", float.MaxValue);
            Set(game.Player,"invulnerableUntil",Time.time + 100);
            var attack = Cases[index];
            ObserveSwordPattern(attack);
            if (attack == EnemyAttack.SW05 && AerialPauseCheck()) return;
            if (index >= InterruptWardCase && index <= RestartWardCase || index == StunWardCase)
            {
                if (SwordWardCleanupCheck()) { index++; started = false; }
                return;
            }
            if (index >= InterruptAerialCase && index <= DeathAerialCase)
            {
                if (AerialCleanupCheck()) { index++; started = false; }
                return;
            }
            sawRecovery |= Get<object>(enemy,"state").ToString()=="Recover";
            sawFullShield |= enemy.Invulnerable;
            if (attack == EnemyAttack.SW04 && index != NoTriggerWardCase && Get<bool>(enemy,"swordGuard"))
            {
                if (index == LateWardCase)
                {
                    float remaining = Get<float>(enemy,"swordGuardUntil") - enemy.FightSeconds;
                    if (!responseTriggered && remaining > 0 && remaining <= 0.1f)
                    { enemy.TakeDamage(1); responseTriggered = true; }
                }
                else
                {
                    // Keep hitting throughout the ward: a multi-hit must respect both cooldowns.
                    float before = enemy.Health, readyAt = Get<float>(enemy,"nextGuardResponse");
                    bool pending = Get<bool>(enemy,"guardResponsePending");
                    for (int i = 0; i < 10; i++) enemy.TakeDamage(1);
                    Check(Mathf.Abs(before - enemy.Health - 10 * Mathf.Clamp01(game.Config.BossTuning.shieldDamageMultiplier)) < 0.01f,
                        "SW04 multi-hit damage still uses the configured reduction");
                    if (Get<bool>(enemy,"guardResponseRunning"))
                        Check(!Get<bool>(enemy,"guardResponsePending"), "additional hits cannot queue a duplicate four-sword sequence");
                    if (!pending && enemy.FightSeconds < readyAt)
                        Check(!Get<bool>(enemy,"guardResponsePending"), "SW04 multi-hit cannot queue during cooldown");
                    responseTriggered = true;
                }
            }
            if (attack == EnemyAttack.SW04 && responseTriggered && enemy.GetComponentInParent<KarmaGame>() != null)
                sawResponse |= game.World.GetComponentsInChildren<KarmaBossAttack>().Length > 0;
            if (index == FailureCase && enemy.TrialActive && !injectedFailure)
            {
                var trial = Get<KarmaTrialState>(enemy,"trial");
                for(int i=0;i<=trial.Allowed;i++) trial.Hit();
                injectedFailure = true;
            }
            sawTrialFailure |= enemy.Intent.Contains("시험 실패");
            if (Get<bool>(enemy,"running") || attack == EnemyAttack.SW04 && Get<Transform>(enemy,"swordGuardRoot") != null) return;
            Check(sawRecovery,attack+" completed through recovery");
            Check(game.World.GetComponentsInChildren<KarmaBossAttack>().Length==0,attack+" no hostile residue after recovery");
            Check(!enemy.Invulnerable && !enemy.TrialActive,attack+" shield/trial released");
            Check(Mathf.Abs(enemy.Position.y-KarmaSpace.GuardianGroundY)<0.01f,attack+" landed");
            if(attack==EnemyAttack.SW05 || attack==EnemyAttack.UP04) Check(sawFullShield,attack+" protected airborne phase");
            if (attack == EnemyAttack.SW05)
                Check(aerialGroupSizes.Length == 7 && movingAttacks.Count == ExpectedAerialCount()
                    && movingAttacks.Count == 105,
                    "SW05 releases exactly seven fifteen-sword volleys in normal and enrage phases");
            if(attack==EnemyAttack.SW01)
            {
                Check(movingAttacks.Count == 2, "SW01 exactly two shots before recovery, including enrage");
                Check(scarAttacks.Count == 0, "SW01 has no SW03 follow-up");
            }
            if(attack==EnemyAttack.SW01 || attack==EnemyAttack.SW02)
            {
                int shots = attack == EnemyAttack.SW01 ? 2 : 1;
                Check(movingAttacks.Count == shots, attack + " completes its requested shot count in normal and enrage phases");
                Check(scarAttacks.Count == 0, attack + " presentation creates no additional stationary damage");
                Check(waveSwings.Count == shots, attack + " launches exactly one harmless sword-swing effect per shot");
                for (int i = 0; i < shots; i++)
                {
                    Check(wavePhases.Contains(i + "/Windup"), attack + " shot " + i + " shows a preparation pose");
                    Check(wavePhases.Contains(i + "/Slash"), attack + " shot " + i + " shows a pre-release swing");
                    Check(wavePhases.Contains(i + "/Wave"), attack + " shot " + i + " shows the projectile release pose");
                    if (attack == EnemyAttack.SW02)
                        Check(wavePhases.Contains(i + "/Guard") && lockedWaveAims.ContainsKey(i),
                            "SW02 shot " + i + " distinguishes tracking preparation from direction lock");
                }
                Check(wavePhases.Contains("Recovery"), attack + " shows FollowThrough throughout the counterattack window");
            }
            if(attack==EnemyAttack.SW03)
            {
                Check(movingAttacks.Count == (enemy.Enraged ? 4 : 3), "SW03 original volley retained");
                Check(scarAttacks.Count == 2, "SW03 original paired scar rise completes before recovery");
                Check(wavePhases.Contains("ScarWarning") && wavePhases.Contains("ScarRise") && wavePhases.Contains("Recovery"),
                    "SW03 visibly connects its scar warning, rising strike and recovery");
                Check(scarSustainStartedAt >= 0 && scarCues.Count == scarAttacks.Count,
                    "SW03 enters the planted hold after the unchanged rising phase");
                foreach (var cue in scarCues)
                {
                    Check(cue.checkedSustainedDamage && cue.checkedLateContact && cue.contact.Consumed,
                        "every SW03 planted scar remains dangerous and permits only one contact");
                    Check(cue.stoppedAt >= 0 && Mathf.Abs(cue.stoppedAt - cue.launchedAt - 3.25f) <= Time.deltaTime * 3,
                        "SW03 visual and actual attack last the original 0.25 rise plus three planted seconds");
                    Check(cue.stoppedAt - scarSustainStartedAt >= 3 - Time.deltaTime * 3
                        && cue.stoppedAt - scarSustainStartedAt <= 3 + Time.deltaTime * 3,
                        "SW03 keeps its planted damage for three seconds before removing it");
                    Check(cue.shot == null || !cue.shot.gameObject.activeInHierarchy,
                        "SW03 recovery has removed every planted blade and its live damage together");
                }
                var tuning = game.Config.BossTuning;
                float tempo = enemy.Enraged ? tuning.enrageTempo : 1;
                int volley = enemy.Enraged ? 4 : 3;
                float previousDuration = Mathf.Max(0.28f, 0.65f * tuning.warningMultiplier * tempo)
                    + (volley - 1) * Mathf.Max(0.42f, 0.54f * tempo)
                    + Mathf.Clamp(tuning.swordLifetime, 0.5f, 2)
                    + Mathf.Max(0.28f, 0.75f * tuning.warningMultiplier * tempo) + 0.25f
                    + Mathf.Max(0.38f, tuning.recoveryMultiplier * tempo);
                Check(Mathf.Abs(Time.time - startedAt - previousDuration - 3) <= Time.deltaTime * 15,
                    "SW03 total pattern grows by exactly the requested three-second hold within coroutine rounding");
                Debug.Log("WAVE DURATION SW03 " + (enemy.Enraged ? "enraged" : "normal")
                    + " measured=" + (Time.time - startedAt) + " previousScheduled=" + previousDuration
                    + " newScheduled=" + (previousDuration + 3));
            }
            if(attack==EnemyAttack.SW04)
            {
                if (index==NoTriggerWardCase)
                {
                    Check(!responseTriggered && movingAttacks.Count==0 && wardCues.Count==0,"unattacked ward expires without a retaliatory sword");
                    Check(Mathf.Abs(wardShieldOff-wardStartedAt-3)<=Time.deltaTime*2,"unattacked ward retains its original three-second defense");
                    Debug.Log("SW04 NO TRIGGER duration="+(Time.time-startedAt));
                    index++;started=false;return;
                }
                Check(responseTriggered, "SW04 damage was injected while the ward was active");
                Check(!Get<bool>(enemy,"swordGuard") && !enemy.Armored && !Get<bool>(enemy,"guardResponsePending"),
                    "SW04 recovery releases defense and queued response");
                Check(sawResponse && movingAttacks.Count == 4 && wardCues.Count == 4,
                    "one trigger completes exactly four individual aimed launches, including a near-expiry trigger");
                Check(sawWardWarning && sawWardRotation && wardOrbitMinimum == 0,
                    "remaining ward swords rotate while the four consumed swords count down to zero");
                var contacts = new HashSet<KarmaAttackContact>();
                for (int i = 0; i < wardCues.Count; i++)
                {
                    var cue = wardCues[i]; contacts.Add(cue.contact);
                    Check(cue.lockedAt - cue.trackingAt >= 0.35f - Time.deltaTime * 2,
                        "each sword has a full independent tracking stage");
                    Check(cue.launchedAt - cue.lockedAt >= 0.3f - Time.deltaTime,
                        "each sword has a readable fixed orange direction before release");
                    if (i > 0) Check(cue.launchedAt - wardCues[i-1].launchedAt >= 0.8f - Time.deltaTime * 2,
                        "four shots are sequential with the original response interval as a minimum");
                    if (i > 0) Check(Vector2.Distance(cue.player, wardCues[i-1].player) > 1,
                        "each new shot reacquires a different player position");
                    Debug.Log("SW04 TIMING case=" + index + " shot=" + i + " track=" + (cue.trackingAt-wardStartedAt)
                        + " lock=" + (cue.lockedAt-wardStartedAt) + " launch=" + (cue.launchedAt-wardStartedAt)
                        + " aimPlayer=" + cue.player + " origin=" + cue.origin + " aim=" + cue.aim);
                }
                Check(contacts.Count == 4, "all four actual ward shots have independent hit ledgers");
                Check(wardShieldOff - wardStartedAt <= game.Config.BossTuning.swordGuardDuration + Time.deltaTime * 2,
                    "committed retaliation never extends ward damage reduction beyond the original deadline");
                Check(wardRecoveryAt >= wardStartedAt && wardRecoveryAt < wardCues[0].launchedAt,
                    "formation releases the action slot before the independently maintained counter shots");
                Debug.Log("SW04 DURATION case=" + index + " total=" + (Time.time-startedAt) + " defense=" + (wardShieldOff-wardStartedAt)
                    + " recovery=" + (enemy.FightSeconds-wardRecoveryAt) + " late=" + (index==LateWardCase));
                float before = enemy.Health;
                enemy.TakeDamage(1);
                Check(Mathf.Abs(before - enemy.Health - 1) < 0.001f, "SW04 full damage restored after ward recovery");
            }
            if(attack==EnemyAttack.SW05)
            {
                Check(movingAttacks.Count == ExpectedAerialCount(),
                    "SW05 presentation emits every independently planned sword");
                Check(scarAttacks.Count == 0, "SW05 presentation adds no stationary attack");
                Check(wavePhases.Contains("Jump") && wavePhases.Contains("AirGuard") && wavePhases.Contains("AirRelease")
                    && wavePhases.Contains("Descent") && wavePhases.Contains("Recovery"),
                    "SW05 connects jump, shielded guard/release, unshielded descent and recovery");
                Check(aerialCues.Count == movingAttacks.Count,
                    "SW05 forms exactly one harmless ornament before each normal/enraged sword");
                foreach (var cue in aerialCues.Values)
                    Check(cue.grew && cue.formed >= 0.8f, "SW05 each sword visibly forms before release");
                int first = 0;
                var octants = new HashSet<int>();
                for (int group = 0; group < aerialGroupSizes.Length; group++)
                {
                    int members = aerialGroupSizes[group];
                    Check(members == 15, "SW05 each default group has exactly fifteen swords");
                    for (int member = 0; member < members; member++)
                    {
                        var cue = aerialCues[first + member];
                        Check(cue.launchFrame == aerialCues[first].launchFrame,
                            "SW05 every member of a volley launches in the same frame");
                        octants.Add(Mathf.FloorToInt(Mathf.Repeat(Mathf.Atan2(cue.aim.y, cue.aim.x) * Mathf.Rad2Deg + 22.5f, 360) / 45));
                    }
                    if (group > 0) Check(aerialCues[first].launchFrame > aerialCues[first - 1].launchFrame,
                        "SW05 distinct random groups retain separate release frames");
                    first += members;
                }
                Check(octants.Count >= 6, "SW05 actual projectiles cover cardinal and diagonal sectors around 360 degrees");
                var contacts = new HashSet<KarmaAttackContact>();
                foreach (var cue in aerialCues.Values) contacts.Add(cue.contact);
                Check(contacts.Count == movingAttacks.Count, "SW05 every projectile owns an independent collision/contact ledger");
                if (index == 5) Check(aerialPauseDone, "SW05 normal sequence pauses both shielded formation and live projectile phases");
                CheckAerialFinalReach();
                Debug.Log("SW05 LIVE OBJECTS seed=" + game.Config.BossTuning.aerialSwordSeed + " swords=" + movingAttacks.Count
                    + " groups=" + string.Join(",", aerialGroupSizes) + " peakAttacks=" + aerialPeakAttacks + " peakObjects=" + aerialPeakObjects);
            }
            if(index==FailureCase) Check(sawTrialFailure,"GD05 failure resolved after full sequence");
            if(index>=EnrageStart) Check(enemy.Enraged,"enhanced sequence ran below half health");
            Check(Time.time-startedAt < 29,attack+" no watchdog recovery");
            Debug.Log("PLAY PASS "+index+" "+attack);
            index++; started=false;
        }
        catch(Exception e) { Finish(e); }
    }
    static void PlayerUtilityChecks()
    {
        var player = game.Player;
        var animator = player.GetComponentInChildren<KarmaCharacterAnimator>();
        var renderer = animator.GetComponentInChildren<SpriteRenderer>();
        animator.Pose(0, false, 0, true);
        var crouch = Resources.Load<Sprite>("K4RMAArtwork/PlayerCrouchV2");
        Check(crouch != null && renderer.sprite == crouch && animator.CurrentPose == "Crouch", "new crouch sprite is displayed");
        Check(crouch.bounds.size.y < KarmaArtwork.Sprite("Player", "Idle").bounds.size.y, "crouch art is shorter than standing art");
        animator.Pose(0, true, 3, true);
        Check(animator.CurrentPose == "Jump", "jump takes priority over crouch");
        animator.Pose(0, false);
        Check(animator.CurrentPose != "Crouch", "releasing crouch restores normal pose");
        Set(player, "invulnerableUntil", 0f);
        float health = player.Health, damage = game.DamageTaken;
        Check(!player.TestInvincible, "test invincibility starts disabled");
        player.ToggleTestInvincibility();
        player.TakeDamage(100);
        Check(player.Health == health && game.DamageTaken == damage, "test invincibility blocks health and damage statistics");
        player.ResetForRoom(player.Position, false);
        Check(player.TestInvincible, "test mode survives room reset");
        player.ToggleTestInvincibility();
        player.TakeDamage(10);
        Check(player.Health == health - 10 && game.DamageTaken == damage + 10, "disabling test mode restores damage");
        player.Heal(10);
        Set(player, "invulnerableUntil", 0f);
        game.DamageTaken = damage;
    }
    static void CaptureWarningError(string message, string stack, LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            && (message.Contains("Invalid vertex array") || stack.Contains("KarmaAttackWarning")))
            warningError = message;
    }
    static KarmaCharacterAnimator PoseProbe(string sheet, out GameObject root, out SpriteRenderer picture)
    {
        root = new GameObject("Isolated " + sheet + " pose check");
        var artwork = new GameObject("Pose check picture"); artwork.transform.SetParent(root.transform, false);
        picture = artwork.AddComponent<SpriteRenderer>();
        var idle = KarmaArtwork.Sprite(sheet, "Idle");
        Check(idle != null, sheet + " pose probe uses the actual existing sprite sheet");
        var animator = root.AddComponent<KarmaCharacterAnimator>();
        animator.ConfigureSprites(picture, new[] { idle });
        animator.ConfigureSheet(sheet);
        return animator;
    }
    static void WavePosePriorityChecks()
    {
        GameObject root; SpriteRenderer picture;
        var animator = PoseProbe("Wave", out root, out picture);
        try
        {
            animator.GuardianPose("Telegraph", EnemyAttack.SW01, false, false);
            var before = picture.sprite;
            animator.PlayPose("Slash", 10);
            Check(picture.sprite == before, "Wave PlayPose records the cue without bypassing guardian priority");
            animator.GuardianPose("Telegraph", EnemyAttack.SW01, false, false);
            Check(animator.CurrentPose == "Slash", "SW01 resolves the queued telegraph pose");
            animator.GuardianPose("Attack", EnemyAttack.SW02, false, false);
            Check(animator.CurrentPose == "Slash", "SW02 resolves the queued attack pose");
            animator.GuardianPose("Recover", EnemyAttack.SW01, false, false);
            Check(animator.CurrentPose == "FollowThrough", "recovery outranks a pending sword attack cue");
            animator.PlayPose("Slash", 10);
            animator.GuardianPose("Stunned", EnemyAttack.SW02, false, false);
            Check(animator.CurrentPose == "Hurt", "stun outranks a pending sword attack cue");
            animator.Hurt();
            animator.GuardianPose("Attack", EnemyAttack.SW01, false, false);
            animator.PlayPose("Wave", 10);
            Check(animator.CurrentPose == "Hurt", "a new Wave cue cannot immediately overwrite a hurt sprite");
            animator.GuardianPose("Attack", EnemyAttack.SW02, false, false);
            Check(animator.CurrentPose == "Hurt", "hurt outranks a pending sword attack cue");
            Set(animator, "hurtUntil", -1f);
            foreach (var attack in new[] { EnemyAttack.SW03, EnemyAttack.SW04, EnemyAttack.SW05 })
            {
                animator.PlayPose("Wave", 10);
                animator.GuardianPose("Attack", attack, false, false);
                Check(animator.CurrentPose == "Slash", attack + " keeps its original pose instead of SW01/SW02 cues");
                animator.GuardianPose("Attack", attack, true, false);
                Check(animator.CurrentPose == "Rising", attack + " keeps its original airborne pose");
                animator.PlayWavePhase("Guard");
                animator.GuardianPose("Attack", attack, true, true);
                Check(animator.CurrentPose == "Guard", attack + " resolves its explicit existing-sprite phase");
                animator.ReleaseWavePhase();
                animator.GuardianPose("Attack", attack, true, true);
                Check(animator.CurrentPose == "Wave", attack + " release briefly overrides its pending guard");
                animator.Hurt();
                animator.PlayWavePhase("Rising");
                animator.GuardianPose("Attack", attack, true, true);
                Check(animator.CurrentPose == "Hurt", attack + " hurt takes priority over phase and release");
                Set(animator, "hurtUntil", -1f);
                animator.GuardianPose("Stunned", attack, true, true);
                Check(animator.CurrentPose == "Hurt", attack + " stun takes priority over phase and release");
                animator.GuardianPose("Recover", attack, false, false);
                Check(animator.CurrentPose == "FollowThrough", attack + " recovery clears its visual phase");
            }
            animator.PlayPose("Slash", 10);
            Set(animator, "poseUntil", Time.time - 1);
            animator.GuardianPose("Attack", EnemyAttack.SW01, false, false);
            Check(animator.CurrentPose == "Guard", "expired sword cue returns to the waiting guard pose");
            animator.PlayPose("Slash", 10);
            animator.GuardianPose("Approach", EnemyAttack.SW01, false, false);
            Check(animator.CurrentPose == "Idle", "queued attack cues do not leak into approach");
            animator.PrepareWaveAttack(0.4f, true);
            animator.GuardianPose("Telegraph", EnemyAttack.SW02, false, false);
            Check(animator.CurrentPose == "Guard", "locked aimed wave starts with its existing guard key pose");
            Set(animator, "waveSwingAt", Time.time - 0.01f);
            animator.GuardianPose("Telegraph", EnemyAttack.SW02, false, false);
            Check(animator.CurrentPose == "Slash", "aimed wave schedules its swing inside the unchanged warning");
            animator.Attack();
            animator.PrepareWaveAttack(0.54f, false);
            animator.GuardianPose("Telegraph", EnemyAttack.SW01, false, false);
            Check(animator.CurrentPose == "Wave", "next SW01 preparation preserves the preceding shot release pose");
            Set(animator, "actionUntil", Time.time - 1);
            animator.GuardianPose("Telegraph", EnemyAttack.SW01, false, false);
            Check(animator.CurrentPose == "Windup", "next SW01 preparation resumes after the short release pose");
            animator.Die(); Call(animator, "Update");
            var death = picture.sprite;
            animator.PlayPose("Slash", 10);
            animator.PlayWavePhase("Guard"); animator.ReleaseWavePhase();
            animator.GuardianPose("Attack", EnemyAttack.SW01, false, false);
            Check(animator.CurrentPose == "Death" && picture.sprite == death, "death outranks new queued sword cues");
        }
        finally { UnityEngine.Object.Destroy(root); }
        foreach (string sheet in new[] { "Rise", "Ward" })
        {
            animator = PoseProbe(sheet, out root, out picture);
            try
            {
                animator.PlayPose("Hurt", 10);
                animator.GuardianPose("Attack", sheet == "Rise" ? EnemyAttack.UP01 : EnemyAttack.GD02, false, false);
                Check(animator.CurrentPose == (sheet == "Rise" ? "Rising" : "ShieldBash"),
                    sheet + " guardian continues ignoring Wave-specific queued pose support");
            }
            finally { UnityEngine.Object.Destroy(root); }
        }
    }
    static void SameBounds(Bounds actual, Bounds expected, string label)
    {
        Check(Vector3.Distance(actual.center, expected.center) < 0.001f
            && Vector3.Distance(actual.size, expected.size) < 0.001f, label);
    }
    static Bounds NeutralBounds(SpriteRenderer picture, Vector3 home, Quaternion rotation,
        Sprite sprite = null, Vector3? scale = null)
    {
        var position = picture.transform.localPosition;
        var turn = picture.transform.localRotation;
        var originalSprite = picture.sprite;
        var originalScale = picture.transform.localScale;
        picture.transform.localPosition = home; picture.transform.localRotation = rotation;
        if (sprite != null) picture.sprite = sprite;
        if (scale.HasValue) picture.transform.localScale = scale.Value;
        var bounds = picture.bounds;
        picture.transform.localPosition = position; picture.transform.localRotation = turn;
        picture.sprite = originalSprite; picture.transform.localScale = originalScale;
        return bounds;
    }
    static void WaveMotionChecks()
    {
        Call(enemy, "InterruptPattern");
        var art = enemy.transform.Find("Artwork");
        var animator = art.GetComponent<KarmaCharacterAnimator>();
        var picture = art.Find("Guardian sprite").GetComponent<SpriteRenderer>();
        var motion = art.GetComponent<KarmaWaveMotion>();
        Check(motion != null && art.GetComponents<KarmaWaveMotion>().Length == 1,
            "the existing Guardian Wave hookup attaches exactly one motion component");
        Call(motion, "Start"); Call(motion, "ResetVisualTransform");
        Vector3 home = picture.transform.localPosition, originalScale = art.localScale;
        Quaternion homeRotation = picture.transform.localRotation;
        Vector3 pictureScale = Get<Vector3>(animator, "pictureScale");
        var oldParent = enemy.transform.parent;
        Vector3 oldPosition = enemy.transform.localPosition, oldEnemyScale = enemy.transform.localScale;
        Quaternion oldRotation = enemy.transform.localRotation;
        var oldSelected = Get<EnemyAttack>(enemy, "selected");
        var oldState = Get<object>(enemy, "state");
        float oldHealth = enemy.Health;
        var oldAppearance = Get<Sprite>(animator, "guardianAppearance");
        float oldAppearanceScale = Get<float>(animator, "appearanceScale");
        int colliders = enemy.GetComponentsInChildren<Collider2D>().Length;
        var parent = new GameObject("Wave motion transformed-parent probe").transform;
        parent.SetParent(game.World, false);
        bool sawMotion = false, sawVisualBoundsDifference = false;
        try
        {
            enemy.transform.SetParent(parent, false);
            Set(enemy, "selected", EnemyAttack.SW01);
            Set(enemy, "state", Enum.Parse(Field(enemy, "state").FieldType, "Attack"));
            Set(animator, "hurtUntil", -1f); Set(animator, "actionUntil", -1f);
            foreach (int layout in new[] { 0, 1 })
                foreach (int facing in new[] { -1, 1 })
                    foreach (string pose in new[] { "Idle", "Move", "Windup", "Slash", "Wave", "Guard", "Rising", "FollowThrough", "Hurt", "Death" })
                    {
                        parent.localScale = layout == 0 ? Vector3.one : new Vector3(1.3f, 0.7f, 1);
                        parent.localRotation = Quaternion.Euler(0, 0, layout * 17);
                        enemy.transform.localPosition = new Vector3(20 + layout * 4, KarmaSpace.GuardianGroundY + layout * 5, 0);
                        enemy.transform.localRotation = Quaternion.identity; enemy.transform.localScale = Vector3.one;
                        art.localScale = new Vector3(facing * Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
                        Call(motion, "ResetVisualTransform");
                        animator.PlayPose(pose, 10);
                        animator.GuardianPose("Attack", EnemyAttack.SW01, false, false);
                        Check(animator.CurrentPose == pose, "motion probe uses actual existing Wave/" + pose + " pose");
                        bool inheritedBounds = oldAppearance != null && (pose == "Idle" || pose == "Guard");
                        Bounds expected = NeutralBounds(picture, home, homeRotation,
                            inheritedBounds ? oldAppearance : KarmaArtwork.Sprite("Wave", pose),
                            inheritedBounds ? pictureScale * oldAppearanceScale : pictureScale);
                        Vector3 rootAt = enemy.transform.position, rootScale = enemy.transform.localScale, artAt = art.localPosition;
                        var sprite = picture.sprite;
                        Set(motion, "visualClock", 0.34f); Set(motion, "previousPose", pose); Set(motion, "poseStartedAt", 0.27f);
                        Call(motion, "LateUpdate");
                        SameBounds((Bounds)Call(motion, "GetDamageBounds", picture), expected, "Wave/" + pose + " neutral hit bounds survive flip/parent scale/root movement");
                        SameBounds(enemy.DamageBounds, expected, "enemy damage entry points use neutral Wave/" + pose + " bounds");
                        Check(picture.sprite == sprite && picture.sprite == KarmaArtwork.Sprite("Wave", pose)
                            && picture.transform.localScale == pictureScale,
                            "motion keeps the prototype " + pose + " sprite and its damage silhouette");
                        Check(enemy.transform.position == rootAt && enemy.transform.localScale == rootScale && art.localPosition == artAt,
                            "motion never moves or scales the boss gameplay root");
                        sawMotion |= Vector3.Distance(picture.transform.localPosition, home) > 0.0001f
                            || Quaternion.Angle(picture.transform.localRotation, homeRotation) > 0.01f;
                        sawVisualBoundsDifference |= Vector3.Distance(picture.bounds.size, expected.size) > 0.001f
                            || Vector3.Distance(picture.bounds.center, expected.center) > 0.001f;
                    }
            Check(sawMotion && sawVisualBoundsDifference, "existing sprites visibly move while gameplay hurt bounds remain stable");
            // New visual phases must retain the pre-integration SW03-05 pose bounds.
            foreach (var attack in new[] { EnemyAttack.SW03, EnemyAttack.SW04, EnemyAttack.SW05 })
                foreach (bool airborne in new[] { false, true })
                    foreach (string phase in new[] { "Guard", "Windup", "Rising" })
                    {
                        Call(motion, "ResetVisualTransform");
                        Set(enemy, "selected", attack); Set(animator, "actionUntil", -1f);
                        animator.PlayWavePhase(phase);
                        animator.GuardianPose("Attack", attack, airborne, true);
                        Check(animator.CurrentPose == phase, attack + " explicitly selects its " + phase + " phase");
                        var baseline = KarmaArtwork.Sprite("Wave", airborne ? "Rising" : "Slash");
                        Bounds expected = NeutralBounds(picture, home, homeRotation, baseline, pictureScale);
                        Call(motion, "LateUpdate");
                        SameBounds(enemy.DamageBounds, expected, attack + " new " + phase + " visual preserves original " + baseline.name + " hit bounds");
                    }
            Set(enemy, "selected", EnemyAttack.SW05); Set(enemy, "fullShield", true);
            animator.Hurt(); animator.GuardianPose("Attack", EnemyAttack.SW05, true, true);
            Set(motion, "visualClock", 0.34f); Set(motion, "previousPose", "Hurt"); Set(motion, "poseStartedAt", 0.27f);
            Call(motion, "LateUpdate");
            Check(!(bool)typeof(KarmaWaveMotion).GetProperty("HoverActive", Flags).GetValue(motion)
                && Get<Vector3>(motion, "motionOffset").y == 0,
                "hurt immediately suppresses the SW05 hover target");
            Set(enemy, "fullShield", false); Call(motion, "LateUpdate");
            Check(!(bool)typeof(KarmaWaveMotion).GetProperty("HoverActive", Flags).GetValue(motion)
                && Get<Vector3>(motion, "motionOffset").y == 0,
                "SW05 shield hover never adds vertical motion to the higher-priority hurt pose");
            Set(animator, "hurtUntil", -1f); animator.GuardianPose("Stunned", EnemyAttack.SW05, true, false);
            Set(enemy, "state", Enum.Parse(Field(enemy, "state").FieldType, "Stunned")); Call(motion, "LateUpdate");
            Check(picture.transform.localPosition == home && Quaternion.Angle(picture.transform.localRotation, homeRotation) < 0.001f,
                "stun resets all secondary motion");
            Set(enemy, "state", Enum.Parse(Field(enemy, "state").FieldType, "Attack"));
            animator.PlayWavePhase("Guard"); animator.GuardianPose("Attack", EnemyAttack.SW05, true, false);
            Call(motion, "LateUpdate");
            Vector3 pausedPosition = picture.transform.localPosition; Quaternion pausedRotation = picture.transform.localRotation;
            float pausedClock = Get<float>(motion, "visualClock");
            game.TogglePause(); Call(motion, "LateUpdate"); game.TogglePause();
            Check(picture.transform.localPosition == pausedPosition && picture.transform.localRotation == pausedRotation
                && Get<float>(motion, "visualClock") == pausedClock, "pause freezes secondary transforms and motion clock");
            motion.enabled = false;
            Check(picture.transform.localPosition == home && Quaternion.Angle(picture.transform.localRotation, homeRotation) < 0.001f,
                "disabling Wave motion restores its neutral transform immediately");
            SameBounds(enemy.DamageBounds, NeutralBounds(picture, home, homeRotation, KarmaArtwork.Sprite("Wave", "Rising"), pictureScale),
                "disabled secondary motion still preserves the original airborne hit silhouette");
            motion.enabled = true; Call(motion, "LateUpdate");
            typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, 0f); Call(motion, "LateUpdate");
            Check(picture.transform.localPosition == home && Quaternion.Angle(picture.transform.localRotation, homeRotation) < 0.001f,
                "boss death resets secondary motion without waiting for object destruction");
            Check(enemy.GetComponentsInChildren<Collider2D>().Length == colliders, "Wave motion adds no gameplay collider");
        }
        finally
        {
            motion.enabled = true; Call(motion, "ResetVisualTransform");
            enemy.transform.SetParent(oldParent, false); enemy.transform.localPosition = oldPosition;
            enemy.transform.localRotation = oldRotation; enemy.transform.localScale = oldEnemyScale;
            art.localScale = originalScale;
            typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, oldHealth);
            Set(enemy, "selected", oldSelected); Set(enemy, "state", oldState); Set(enemy, "fullShield", false);
            Set(animator, "guardianAppearance", oldAppearance); Set(animator, "appearanceScale", oldAppearanceScale);
            Set(animator, "hurtUntil", -1f); Set(animator, "actionUntil", -1f);
            animator.GuardianPose("Approach", oldSelected, false, false);
            UnityEngine.Object.Destroy(parent.gameObject);
        }
        var attachmentProbe = new GameObject("Guardian motion attachment probe");
        foreach (var order in new[] { new Essence[0], new[] { Essence.RisingSlash }, new[] { Essence.Guard }, new[] { Essence.SwordWave } })
        {
            var artwork = KarmaArtwork.Guardian(attachmentProbe.transform, order, false);
            Check(artwork.GetComponents<KarmaWaveMotion>().Length == (order.Length == 1 && order[0] == Essence.SwordWave ? 1 : 0),
                "motion attachment remains exclusive to the Wave guardian");
        }
        attachmentProbe.SetActive(false); UnityEngine.Object.Destroy(attachmentProbe);
    }
    static void SpatialChecks()
    {
        BossVisualChecks();
        var config = game.Config;
        var baseline = AssetDatabase.LoadAssetAtPath<KarmaConfig>("Assets/K4RMA/Settings/KarmaConfig.asset");
        Check(baseline != null, "serialized gameplay configuration exists");
        Check(baseline.playerHealth == 160 && baseline.swordDamage == 60,
            "serialized difficulty does not retain the 10000 HP / 1000 damage values");
        Check(baseline.swordWaveDamage == 72 && baseline.Techniques.risingDamage == 60
            && baseline.Techniques.counterDamage == 85 && baseline.healAfterSacrifice == 45,
            "skill damage and transition healing use the revised player balance");
        Check(KarmaCombatVFX.GlowMaterial != null, "additive combat material is included in Resources");
        foreach (Transform child in enemy.transform.Find("Artwork"))
            Check(child.name != "SwordPart" && child.name != "WardPart" && child.name != "RisePart",
                "guardian has no duplicate fixed-position equipment");
        Check(Mathf.Abs(config.jumpSpeed / config.gravityScale - 13f / 3) < 0.0001f,
            "spatial compensation preserves jump airtime");
        Check(Mathf.Abs(config.jumpSpeed * config.jumpSpeed / config.gravityScale - 169f / 3 * KarmaSpace.Scale) < 0.0001f,
            "spatial compensation scales jump height");
        Check(config.moveSpeed == baseline.moveSpeed && config.swordWaveSpeed == baseline.swordWaveSpeed,
            "serialized and default movement/projectile speeds agree");
        Check(config.Techniques.pierceDistance == baseline.Techniques.pierceDistance
            && config.BossTuning.swordSpeed == baseline.BossTuning.swordSpeed,
            "serialized and default skill/boss distances agree");
        Check(game.Player.GetComponent<BoxCollider2D>().size == KarmaSpace.PlayerSize,
            "physical body uses compensated dimensions");
        var body = game.Player.GetComponent<Rigidbody2D>();
        Set(game.Player, "move", 1f);
        Call(game.Player, "FixedUpdate");
        Check(Mathf.Abs(body.linearVelocity.x - config.moveSpeed) < 0.0001f,
            "movement applies compensated speed to the physics body");
        Set(game.Player, "jumpBufferedUntil", Time.time + 1f);
        Set(game.Player, "lastGrounded", Time.time);
        Call(game.Player, "FixedUpdate");
        Check(Mathf.Abs(body.linearVelocity.y - config.jumpSpeed) < 0.0001f,
            "buffered jump applies compensated vertical speed");
        Set(game.Player, "move", 0f);
        body.linearVelocity = Vector2.zero;
        var art = enemy.transform.Find("Artwork");
        var picture = art.Find("Guardian sprite");
        Check(Mathf.Abs(art.localScale.y - KarmaSpace.GuardianArtScale) < 0.0001f,
            "guardian keeps its larger silhouette");
        Check(Mathf.Abs(picture.position.y - (enemy.Position.y - KarmaSpace.GuardianGroundY)) < 0.0001f,
            "guardian foot pivot remains grounded after enlargement");

    }
    static void BossDamageBoundsChecks()
    {
        Call(enemy, "InterruptPattern");
        var art = enemy.transform.Find("Artwork");
        var picture = art.Find("Guardian sprite").GetComponent<SpriteRenderer>();
        var originalSprite = picture.sprite;
        var animator = art.GetComponent<KarmaCharacterAnimator>();
        var damageSprite = typeof(KarmaCharacterAnimator).GetProperty("GuardianDamageSprite", Flags);
        var damageScale = typeof(KarmaCharacterAnimator).GetProperty("GuardianDamageScale", Flags);
        var damageOverride = typeof(KarmaCharacterAnimator).GetProperty("GuardianPhaseOverridesPose", Flags);
        var originalDamageSprite = damageSprite.GetValue(animator);
        var originalDamageScale = damageScale.GetValue(animator);
        var originalDamageOverride = damageOverride.GetValue(animator);
        Vector3 originalPictureScale = picture.transform.localScale, originalPicturePosition = picture.transform.localPosition;
        Quaternion originalPictureRotation = picture.transform.localRotation;
        bool originalFlipX = picture.flipX, originalFlipY = picture.flipY;
        Vector3 authoredScale = Get<Vector3>(animator, "pictureScale");
        Vector3 originalScale = art.localScale;
        Vector2 originalPosition = enemy.Position, playerPosition = game.Player.Position;
        float health = enemy.Health;
        int facing = game.Player.Facing;
        try
        {
            foreach (string sheet in new[] { "Neutral", "Wave", "Rise", "Ward", "Inheritance" })
                foreach (var sprite in KarmaArtwork.Settings.spriteLibrary.All(sheet))
                    foreach (int side in new[] { -1, 1 })
                    {
                        // The persistent cutout and its logical hit silhouette are
                        // independent. Exercise every old source at its original
                        // scale, with the same sprite selected as the hit baseline.
                        picture.sprite = sprite; picture.transform.localScale = authoredScale;
                        picture.transform.localPosition = Get<Vector3>(animator, "pictureHome");
                        picture.transform.localRotation = Quaternion.identity;
                        picture.flipX = picture.flipY = false;
                        damageSprite.SetValue(animator, sprite); damageScale.SetValue(animator, authoredScale);
                        damageOverride.SetValue(animator, true);
                        art.localScale = new Vector3(side * Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
                        var bounds = picture.bounds;
                        SameBounds(enemy.DamageBounds, bounds, sheet + "/" + sprite.name + " original hit silhouette in facing " + side);
                        Vector2 head = new Vector2(bounds.center.x, bounds.max.y - 0.01f);
                        Vector2 foot = new Vector2(bounds.center.x, bounds.min.y + 0.01f);
                        Check(enemy.IntersectsDamagePath(head + Vector2.left * 20, head + Vector2.right * 20, 0),
                            sheet + "/" + sprite.name + " head hit in facing " + side);
                        Check(enemy.OverlapsDamageArea(foot, Vector2.one * 0.02f), "foot/hem remains hittable");
                        Check(!enemy.IntersectsDamagePath(head + Vector2.up, head + Vector2.up + Vector2.right, 0.1f),
                            "attack entirely above artwork still misses");
                    }
        }
        finally
        {
            picture.sprite = originalSprite; picture.transform.localScale = originalPictureScale;
            picture.transform.localPosition = originalPicturePosition; picture.transform.localRotation = originalPictureRotation;
            picture.flipX = originalFlipX; picture.flipY = originalFlipY; art.localScale = originalScale;
            damageSprite.SetValue(animator, originalDamageSprite); damageScale.SetValue(animator, originalDamageScale);
            damageOverride.SetValue(animator, originalDamageOverride);
        }
        // Real damage entry points at the upper edge, far above the old foot-centered circle.
        var body = game.Player.GetComponent<Rigidbody2D>();
        Action place = () =>
        {
            var bounds = enemy.DamageBounds;
            Vector2 at = new Vector2(bounds.min.x - 0.3f, bounds.max.y - 0.05f);
            game.Player.transform.position = at; body.position = at;
            typeof(KarmaPlayer).GetProperty("Facing").SetValue(game.Player, 1);
            Set(game.Player, "nextSword", 0f);
        };
        place(); float before = enemy.Health;
        Call(game.Player, "Attack");
        Check(enemy.Health < before, "normal sword damages the head");
        place(); before = enemy.Health;
        KarmaProjectile.Spawn(game, game.Player.Position, Vector2.right * 200, 1, Color.white, 0.1f);
        var shot = game.Projectiles[game.Projectiles.Count - 1];
        Call(shot, "Update");
        Check(enemy.Health < before && !shot.enabled, "swept projectile damages the head and stops");
        place(); before = enemy.Health;
        Call(game.Player, "BeginDirectionalSlash", Vector2.right, 1f, Essence.SwordWave);
        Check(enemy.Health < before, "directional slash damages upper artwork");
        place(); before = enemy.Health;
        Set(game.Player, "airSlashDirection", Vector2.right);
        Set(game.Player, "airSlashPrevious", game.Player.Position);
        Get<System.Collections.Generic.HashSet<KarmaEnemy>>(game.Player, "airSlashHits").Clear();
        Call(game.Player, "DamageAirSlash");
        Check(enemy.Health < before, "air slash damages upper artwork");
        before = enemy.Health; Call(game.Player, "DamageAirSlash");
        Check(enemy.Health == before, "air slash still damages each boss only once");
        place(); before = enemy.Health; game.Player.BeginRisingSlash();
        Check(enemy.Health < before, "rising slash damages upper artwork");
        place(); before = enemy.Health; Set(game.Player, "invulnerableUntil", 0f);
        game.Player.BeginCounter(); game.Player.TakeDamage(1);
        Check(enemy.Health < before, "counter measures range from the visible body");
        // World bounds must follow a boss moving through the air as well.
        var originalBounds = enemy.DamageBounds;
        enemy.transform.position += new Vector3(4, 5, 0);
        Check(Vector3.Distance(enemy.DamageBounds.center, originalBounds.center + new Vector3(4, 5, 0)) < 0.001f,
            "hurt bounds follow airborne motion");
        enemy.transform.position = originalPosition;
        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, health);
        game.Player.transform.position = playerPosition; body.position = playerPosition; body.linearVelocity = Vector2.zero;
        typeof(KarmaPlayer).GetProperty("Facing").SetValue(game.Player, facing);
        Set(game.Player, "nextSword", 0f); Set(game.Player, "invulnerableUntil", 0f);
        Call(game.Player, "CancelAirSlash"); Call(enemy, "InterruptPattern");
    }
    static void BossVisualChecks()
    {
        var root = new GameObject("Boss visual orientation probe");
        var renderer = root.AddComponent<SpriteRenderer>();
        var size = new Vector2(3, 2);
        foreach (var velocity in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down,
            new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
        {
            KarmaBossAttack.ConfigureVisual(renderer, velocity, size, -1, "Wave");
            Check(Vector2.Dot(renderer.transform.right, velocity.normalized) > 0.999f,
                "moving boss effect aligns with velocity " + velocity);
            Check(!renderer.flipX, "projectile orientation has no second facing flip");
            Check(Mathf.Abs(renderer.transform.localScale.x - renderer.transform.localScale.y) < 0.0001f,
                "boss artwork preserves sprite proportions");
            Check(renderer.bounds.size.x <= size.x * 1.151f && renderer.bounds.size.y <= size.y * 1.151f,
                "rotated boss effect stays within its reviewed world footprint");
        }
        foreach (string pose in new[] { "Cleave", "Pillar", "GroundRing" })
            foreach (int facing in new[] { -1, 1 })
            {
                KarmaBossAttack.ConfigureVisual(renderer, Vector2.zero, size, facing, pose);
                Check(renderer.flipX == (facing < 0), "static boss effect mirrors with facing " + pose);
                Check(Vector2.Dot(renderer.transform.up, Vector2.up) > 0.999f, "pillar/slash remains upright");
            }
        UnityEngine.Object.Destroy(root);
        var attack = KarmaBossAttack.Spawn(game, enemy, Get<Transform>(enemy, "effectsRoot"),
            enemy.Position, new Vector2(-3, 2), size, 1, 1, Color.yellow, null, 0);
        Check(attack.transform.localScale == Vector3.one && attack.transform.rotation == Quaternion.identity,
            "boss hit-box root is unaffected by art rotation or scale");
        Check(attack.Visual.transform.parent == attack.transform, "boss artwork has its own transform");
        UnityEngine.Object.Destroy(attack.gameObject);
    }
    static void CameraChecks()
    {
        var camera = game.GameCamera;
        float originalAspect = camera.aspect;
        Vector2 originalPosition = game.Player.Position;
        camera.aspect = 16f / 9;
        game.Player.ResetForRoom(new Vector2(KarmaStage.ArenaLeft + 2, KarmaSpace.Units(1)), false);
        for (int i = 0; i < 100; i++) Call(game, "LateUpdate");
        Vector3 left = camera.transform.position;
        float zoom = camera.orthographicSize;
        game.Player.ResetForRoom(new Vector2(KarmaStage.ArenaRight - 2, KarmaSpace.Units(1)), false);
        for (int i = 0; i < 100; i++) Call(game, "LateUpdate");
        Vector3 right = camera.transform.position;
        Check(right.x - left.x > 15, "camera follows horizontally through the expanded arena");
        Check(Mathf.Abs(right.y - left.y) < 0.001f && Mathf.Abs(camera.orthographicSize - zoom) < 0.001f,
            "horizontal scrolling preserves camera height and zoom");
        float halfWidth = zoom * camera.aspect;
        Check(left.x - halfWidth >= KarmaStage.ArenaLeft - 5.01f
            && right.x + halfWidth <= KarmaStage.ArenaRight + 5.01f,
            "camera stops at arena framing limits");
        var backdrop = game.World.Find("Mountain dojo artwork").GetComponent<SpriteRenderer>();
        Check(backdrop.bounds.min.x <= left.x - halfWidth + 0.001f
            && backdrop.bounds.max.x >= right.x + halfWidth - 0.001f,
            "one continuous panorama covers both camera extremes");
        Check(!backdrop.flipX && game.World.Find("Mountain dojo artwork left continuation") == null
            && game.World.Find("Mountain dojo artwork right continuation") == null,
            "scrolling uses no mirrored background copies");
        game.Player.ResetForRoom(originalPosition, false);
        camera.aspect = originalAspect;
    }
    static void ProgressionReloadChecks()
    {
        var essences = game.Player.Essences;
        var order = new System.Collections.Generic.List<Essence>(essences.SacrificeOrder);
        string serialized = JsonUtility.ToJson(essences);
        JsonUtility.FromJsonOverwrite(serialized, essences);
        Check(essences.Count == order.Count, "confirmed personalization count survives Unity serialization");
        for (int i = 0; i < order.Count; i++)
            Check(essences.SacrificeOrder[i] == order[i] && essences.HasRemnant(order[i])
                && !essences.HasActive(order[i]), "personalized skill ownership/order survives script reload");
        Call(game, "LoadRoom", false);
        enemy = game.Enemies[0];
        game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        Check(essences.Count == order.Count, "room loading preserves owned skill HUD data");
        Set(essences, "progression", new KarmaProgressionState());
        Check(enemy.RestoreMissingPersonalizations(essences, order.Count, order[order.Count - 1]),
            "already-lost personalization state can migrate from the guardian cycle");
        for (int i = 0; i < order.Count; i++)
            Check(essences.SacrificeOrder[i] == order[i], "migration restores exact acquisition order");
        Check(!enemy.RestoreMissingPersonalizations(essences, order.Count, order[order.Count - 1]),
            "migration leaves existing ownership unchanged");
    }
    static void BoundaryChecks()
    {
        // Apply the update to already-existing geometry, as on an Editor script reload.
        game.World.Find("Right wall").localPosition = new Vector3(51, KarmaSpace.Units(4), 0);
        game.World.Find("Left wall").localPosition = new Vector3(-11, KarmaSpace.Units(4), 0);
        KarmaStage.RefreshBoundaries(game.World);
        // Simulate real collisions in an isolated physics scene, not just camera transforms.
        var scene = SceneManager.CreateScene("Boundary checks", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        var physics = scene.GetPhysicsScene2D();
        foreach (var surface in game.World.GetComponentsInChildren<KarmaSurface>())
        {
            var source = surface.GetComponent<BoxCollider2D>();
            if (source == null) continue;
            var wall = new GameObject("Probe " + surface.name);
            SceneManager.MoveGameObjectToScene(wall, scene);
            wall.transform.position = surface.transform.position;
            wall.transform.localScale = surface.transform.lossyScale;
            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = source.size; collider.offset = source.offset;
        }
        var probe = new GameObject("Movement collision probe");
        SceneManager.MoveGameObjectToScene(probe, scene);
        probe.AddComponent<BoxCollider2D>().size = KarmaSpace.PlayerSize;
        var body = probe.AddComponent<Rigidbody2D>();
        body.gravityScale = 0; body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.position = new Vector2(45, KarmaSpace.Units(1));
        for (int i = 0; i < 100; i++)
        { body.linearVelocity = Vector2.right * game.Config.moveSpeed; physics.Simulate(0.02f); }
        Check(body.position.x > 52, "player passes the former right invisible wall");
        Check(body.position.x <= KarmaStage.ArenaRight + 0.1f, "right boundary matches panorama edge");
        body.position = new Vector2(-5, KarmaSpace.Units(1));
        for (int i = 0; i < 100; i++)
        { body.linearVelocity = Vector2.left * game.Config.moveSpeed; physics.Simulate(0.02f); }
        Check(body.position.x < -12, "player passes the former left invisible wall");
        Check(body.position.x >= KarmaStage.ArenaLeft - 0.1f, "left boundary matches panorama edge");
        SceneManager.UnloadSceneAsync(scene);
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
        Time.captureDeltaTime = index == 5 ? 1f/30 : index == 27 ? 1f/60 : 0.05f;
        // Load the real stage, guardian artwork and inherited cycle for every pattern.
        var pattern = Cases[index];
        Essence[] order;
        if (pattern >= EnemyAttack.SW01 && pattern <= EnemyAttack.SW05)
            order = pattern == EnemyAttack.SW05 ? new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard }
                : pattern == EnemyAttack.SW03 ? new[] { Essence.SwordWave, Essence.RisingSlash }
                : pattern == EnemyAttack.SW04 ? new[] { Essence.SwordWave, Essence.Guard } : new[] { Essence.SwordWave };
        else if (pattern >= EnemyAttack.UP01 && pattern <= EnemyAttack.UP05)
            order = pattern == EnemyAttack.UP05 ? new[] { Essence.RisingSlash, Essence.SwordWave, Essence.Guard }
                : pattern == EnemyAttack.UP03 ? new[] { Essence.RisingSlash, Essence.SwordWave }
                : pattern == EnemyAttack.UP04 ? new[] { Essence.RisingSlash, Essence.Guard } : new[] { Essence.RisingSlash };
        else if (pattern >= EnemyAttack.GD02 && pattern <= EnemyAttack.GD05)
            order = pattern == EnemyAttack.GD05 ? new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash }
                : pattern == EnemyAttack.GD03 ? new[] { Essence.Guard, Essence.SwordWave }
                : pattern == EnemyAttack.GD04 ? new[] { Essence.Guard, Essence.RisingSlash } : new[] { Essence.Guard };
        else order = new Essence[0];
        Set(game.Player.Essences, "progression", new KarmaProgressionState());
        foreach (var essence in order) game.Player.Essences.Sacrifice(essence);
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, order.Length);
        Call(game, "LoadRoom", true); enemy = game.Enemies[0];
        game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        Check(enemy.PrimaryType == KarmaPatternCatalog.Primary(order), "runtime pattern uses its actual inherited guardian type");
        Debug.Log("PLAY STAGE " + (game.StageIndex + 1) + " " + pattern);
        Call(enemy,"InterruptPattern");
        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * (index >= EnrageStart ? 0.45f : 1));
        enemy.transform.position = new Vector3(20,KarmaSpace.GuardianGroundY,0);
        game.Player.transform.position = new Vector3(13,KarmaSpace.Units(1),0);
        game.Player.GetComponent<Rigidbody2D>().position = new Vector2(13,KarmaSpace.Units(1));
        Set(enemy,"engaged",true); Set(enemy,"selected",Cases[index]); Set(enemy,"direction",-1f);
        Set(enemy,"running",true); Set(enemy,"patternStarted",enemy.FightSeconds);
        Set(enemy,"nextShield",float.MaxValue);
        if(Cases[index]==EnemyAttack.GD02) Set(enemy,"shieldUntil",enemy.FightSeconds+5);
        movingAttacks.Clear(); scarAttacks.Clear(); pathWarnings.Clear(); wavePhases.Clear(); lockedWaveAims.Clear(); waveSwings.Clear();
        scarCues.Clear(); scarSustainStartedAt = -1;
        aerialCues.Clear(); aerialPauseActive = aerialPauseDone = false;
        aerialGroupSizes = new int[0];
        aerialFightStarted = enemy.FightSeconds;
        aerialShieldOn = aerialShieldOff = aerialRecoveryAt = -1;
        aerialPeakAttacks = aerialPeakObjects = 0;
        if (allocationRecorder.Valid) allocationRecorder.Dispose();
        if (frameRecorder.Valid) frameRecorder.Dispose();
        aerialFrameMs.Clear(); aerialGcBytes=aerialMaxGc=0; aerialPeakWarnings=0;
        aerialGcCollections=GC.CollectionCount(0);
        if (pattern==EnemyAttack.SW05)
        {
            allocationRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");
            frameRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread");
        }
        game.Config.BossTuning.aerialSwordSeed = index == 5 || index == 27 ? 20261029 : 0;
        aerialFormationPauseDone = aerialProjectilePauseDone = false;
        aerialPauseShot = null; aerialPauseBlade = null;
        latestWaveShot = null;
        lastWardShot = -100; observingWardWarning = sawWardWarning = sawWardRotation = false;
        wardCues.Clear(); aimingWard = null;
        wardStartedAt = wardShieldOff = wardRecoveryAt = -1; wardOrbitMinimum = 4;
        enemy.StartCoroutine((IEnumerator)Call(enemy,"RunPattern"));
        sawRecovery=sawFullShield=responseTriggered=sawResponse=injectedFailure=sawTrialFailure=false;
        startedAt=Time.time; started=true;
    }
    static void ObserveSwordPattern(EnemyAttack attack)
    {
        if (attack != EnemyAttack.SW01 && attack != EnemyAttack.SW02 && attack != EnemyAttack.SW03 && attack != EnemyAttack.SW04 && attack != EnemyAttack.SW05) return;
        var effects = Get<Transform>(enemy, "effectsRoot");
        var warnings = effects.GetComponentsInChildren<KarmaAttackWarning>();
        foreach (var warning in warnings) pathWarnings.Add(warning);
        if (attack == EnemyAttack.SW03) MoveIntoScarGap(warnings);
        if (attack == EnemyAttack.SW04) ObserveWardAim(effects, warnings);
        if (attack == EnemyAttack.SW05)
        {
            if (allocationRecorder.Valid) { aerialGcBytes+=allocationRecorder.LastValue; aerialMaxGc=Math.Max(aerialMaxGc,allocationRecorder.LastValue); }
            if (frameRecorder.Valid && frameRecorder.LastValue>0) aerialFrameMs.Add(frameRecorder.LastValue/1000000f);
            aerialPeakWarnings=Mathf.Max(aerialPeakWarnings,warnings.Length);
            bool shield = Get<bool>(enemy, "fullShield");
            if (shield && aerialShieldOn < 0) aerialShieldOn = enemy.FightSeconds;
            if (!shield && aerialShieldOn >= 0 && aerialShieldOff < 0) aerialShieldOff = enemy.FightSeconds;
            if (Get<object>(enemy, "state").ToString() == "Recover" && aerialRecoveryAt < 0) aerialRecoveryAt = enemy.FightSeconds;
            ObserveAerialFormation(effects, warnings);
            aerialPeakAttacks = Mathf.Max(aerialPeakAttacks, effects.GetComponentsInChildren<KarmaBossAttack>().Length);
            int active = 0;
            foreach (Transform child in effects) if (child.gameObject.activeInHierarchy) active++;
            aerialPeakObjects = Mathf.Max(aerialPeakObjects, active);
        }
        foreach (var shot in effects.GetComponentsInChildren<KarmaBossAttack>())
        {
            bool moving = Get<Vector2>(shot, "velocity").sqrMagnitude > 0.01f;
            if (!(moving ? movingAttacks : scarAttacks).Add(shot)) continue;
            if (moving && (attack == EnemyAttack.SW01 || attack == EnemyAttack.SW03))
            {
                Check(pathWarnings.Count >= movingAttacks.Count, "each staggered wave has a preceding path warning");
                float expectedY = KarmaSpace.Units(movingAttacks.Count % 2 == 1 ? 1.05f : 3.3f);
                Check(Mathf.Abs(shot.transform.position.y - expectedY) < 0.001f, "staggered waves retain alternating locked lanes");
            }
            if (!moving && attack == EnemyAttack.SW03)
                Check(movingAttacks.Count == (enemy.Enraged ? 4 : 3), "SW03 scars follow the complete original volley");
            if (moving) CheckWaveProjectile(shot, attack);
            if (moving && attack == EnemyAttack.SW05) CheckAerialHandoff(shot, movingAttacks.Count - 1);
            if (!moving && attack == EnemyAttack.SW03)
            {
                Check(Vector2.Distance(Get<Vector2>(shot, "size"), new Vector2(4.5f, 13.5f)) < 0.0001f,
                    "SW03 rising scars enlarge width 1.25x and height 1.35x");
                Check(Mathf.Abs(shot.transform.position.y - 6.75f) < 0.0001f,
                    "SW03 height expansion grows upward from the original floor");
                Check(Mathf.Abs(Get<float>(shot, "damage") - game.Config.enemyDamage * game.Config.BossTuning.damageMultiplier) < 0.0001f
                    && Get<float>(shot, "remaining") >= 3.25f - Time.deltaTime * 2 && Get<float>(shot, "remaining") <= 3.25f,
                    "SW03 rising scars retain damage and add three planted seconds to the 0.25-second rise");
                var cue = new ScarCue { shot = shot, contact = Get<KarmaAttackContact>(shot, "contact"),
                    origin = shot.transform.position, launchedAt = Time.time - (3.25f - Get<float>(shot, "remaining")) };
                shot.gameObject.AddComponent<KarmaScarLifetimeProbe>().Stopped = at => cue.stoppedAt = at;
                scarCues.Add(cue);
            }
            if (moving && attack == EnemyAttack.SW04)
            {
                Check(aimingWard != null && aimingWard.lockedAt > 0, "each ward shot follows its own observed direction lock");
                aimingWard.shot = shot; aimingWard.contact = Get<KarmaAttackContact>(shot, "contact");
                aimingWard.launchedAt = enemy.FightSeconds;
                Check(Vector2.Dot(aimingWard.aim, Get<Vector2>(shot, "velocity").normalized) > 0.999f,
                    "a launched ward sword follows its frozen warning after the player moves");
                Check(Vector2.Distance(aimingWard.origin, (Vector2)shot.transform.position - Get<Vector2>(shot,"velocity")
                    * (Mathf.Clamp(game.Config.BossTuning.swordLifetime,0.5f,2)-Get<float>(shot,"remaining"))) < 0.01f,
                    "actual projectile starts at the consumed orbit sword, without teleporting to the boss center");
                wardCues.Add(aimingWard); aimingWard = null;
                Check(CountWardBlades(effects) == 4 - wardCues.Count, "orbit sword count decreases 4->3->2->1->0 at the actual release");
                lastWardShot = enemy.FightSeconds;
            }
        }
        if (attack == EnemyAttack.SW03) ObserveScarSustain();
        if (attack == EnemyAttack.SW01 || attack == EnemyAttack.SW02) ObserveWavePresentation(attack, warnings);
        else ObserveExtendedWavePresentation(attack, warnings);
        if (attack != EnemyAttack.SW04) return;
        Transform blade = null;
        foreach (Transform child in effects.GetComponentsInChildren<Transform>())
            if (child.name == "회전 검막 · 접촉 피해 없음" && child.gameObject.activeInHierarchy) blade = child;
        bool warningActive = (Get<bool>(enemy, "swordGuard") || Get<bool>(enemy, "guardResponseRunning")) && warnings.Length > 0 && blade != null;
        if (warningActive)
        {
            sawWardWarning = true;
            if (observingWardWarning && enemy.FightSeconds > bladeObservedAt && CountWardBlades(effects) > 1)
            {
                Check((blade.position - bladeDuringWarning).sqrMagnitude > 0.000001f,
                    "SW04 orbit advances on consecutive warning frames");
                sawWardRotation = true;
            }
            bladeDuringWarning = blade.position; bladeObservedAt = enemy.FightSeconds;
        }
        observingWardWarning = warningActive;
    }
    static int CountWardBlades(Transform effects)
    {
        int count=0;
        foreach (Transform child in effects.GetComponentsInChildren<Transform>())
            if (child.name == "회전 검막 · 접촉 피해 없음" && child.gameObject.activeInHierarchy) count++;
        return count;
    }
    static void ObserveWardAim(Transform effects, KarmaAttackWarning[] warnings)
    {
        bool shield=Get<bool>(enemy,"swordGuard");
        if (shield && wardStartedAt<0)
        {
            wardStartedAt=enemy.FightSeconds;
            Check(CountWardBlades(effects)==4, "ward begins with four actual counter-sword PNGs");
        }
        if (!shield && wardStartedAt>=0 && wardShieldOff<0) wardShieldOff=enemy.FightSeconds;
        if (wardStartedAt>=0) wardOrbitMinimum=Mathf.Min(wardOrbitMinimum,CountWardBlades(effects));
        if (Get<object>(enemy,"state").ToString()=="Recover" && wardRecoveryAt<0) wardRecoveryAt=enemy.FightSeconds;
        foreach (var warning in warnings)
        {
            bool tracking=warning.name=="추적 중인 경로", locked=warning.name=="고정 공격 경로";
            if (!tracking && !locked) continue;
            var sr=warning.GetComponent<SpriteRenderer>();
            if (tracking)
            {
                if (aimingWard==null) aimingWard=new WardCue { trackingAt=enemy.FightSeconds };
                aimingWard.origin=WardAimingOrigin(effects);
                aimingWard.player=game.Player.GetComponent<Collider2D>().bounds.center;
                aimingWard.aim=(aimingWard.player-aimingWard.origin).normalized;
                Check(sr.color.b>sr.color.r,"tracking warning is blue");
            }
            else if (aimingWard!=null)
            {
                if (aimingWard.lockedAt<=0)
                {
                    aimingWard.lockedAt=enemy.FightSeconds;
                    aimingWard.origin=WardAimingOrigin(effects);
                    aimingWard.aim=WarningHeading(warning);
                }
                Check(sr.color.r>sr.color.b,"locked warning is orange");
                Check(Vector2.Dot(WarningHeading(warning),aimingWard.aim)>0.999f,
                    "locked direction cannot continue tracking");
            }
        }
        // Move between shots and during the fixed phase; the next sword must
        // reacquire this new location, while the locked sword must ignore it.
        if (wardStartedAt>=0)
        {
            float x=13+wardCues.Count*3;
            if (aimingWard!=null && aimingWard.lockedAt>0) x+=2;
            var body=game.Player.GetComponent<Rigidbody2D>();
            body.position=new Vector2(x,2+(wardCues.Count%2)*3);
            game.Player.transform.position=body.position; Physics2D.SyncTransforms();
        }
    }
    static Vector2 WardAimingOrigin(Transform effects)
    {
        foreach (Transform child in effects.GetComponentsInChildren<Transform>())
            if (child.name=="회전 검막 · 접촉 피해 없음" && child.gameObject.activeInHierarchy) return child.position;
        throw new Exception("Aiming warning must have a remaining orbit sword");
    }
    static Vector2 WarningHeading(KarmaAttackWarning warning)
    {
        var arrow=Get<LineRenderer>(warning,"arrow");
        Vector3 head=arrow.GetPosition(1), tail=(arrow.GetPosition(0)+arrow.GetPosition(2))/2;
        if (!arrow.useWorldSpace) { head=arrow.transform.TransformPoint(head); tail=arrow.transform.TransformPoint(tail); }
        return (head-tail).normalized;
    }
    static void MoveIntoScarGap(KarmaAttackWarning[] warnings)
    {
        SpriteRenderer left = null, right = null;
        foreach (var warning in warnings)
        {
            if (warning.name != "검흔 상승 범위") continue;
            var picture = warning.GetComponent<SpriteRenderer>();
            if (left == null || picture.bounds.center.x < left.bounds.center.x) left = picture;
            if (right == null || picture.bounds.center.x > right.bounds.center.x) right = picture;
        }
        if (left == null) return;
        Check(left != right, "the integrated SW03 hold probe has its original paired scar warnings");
        var player = game.Player;
        var body = player.GetComponent<Rigidbody2D>();
        Physics2D.SyncTransforms();
        var bounds = player.GetComponent<Collider2D>().bounds;
        float low = left.bounds.max.x + bounds.extents.x + 0.25f;
        float high = right.bounds.min.x - bounds.extents.x - 0.25f;
        Check(low <= high, "the SW03 telegraph exposes a real player-body corridor with side clearance");
        float target = Mathf.Clamp(bounds.center.x, low, high);
        float step = Mathf.MoveTowards(bounds.center.x, target, game.Config.moveSpeed * Time.deltaTime) - bounds.center.x;
        // The integrated driver keeps its body kinematic. Move only during the
        // actual warning and at the configured speed; independent expansion
        // checks exercise the same corridor with full terrain physics.
        Vector2 next = body.position + Vector2.right * step;
        body.position = next; player.transform.position = next; Physics2D.SyncTransforms();
        Check(Mathf.Abs(step) <= game.Config.moveSpeed * Time.deltaTime + 0.0001f,
            "the SW03 hold probe reaches warning safety using a physically available movement speed");
    }
    static void ObserveScarSustain()
    {
        if (scarSustainStartedAt < 0 && enemy.Intent.StartsWith("SW03 검흔 유지"))
        {
            scarSustainStartedAt = Time.time;
            foreach (var cue in scarCues)
                Check(Mathf.Abs(scarSustainStartedAt - cue.launchedAt - 0.25f) <= Time.deltaTime * 3,
                    "SW03 planted hold follows the original 0.25-second rising phase");
        }
        if (scarSustainStartedAt < 0) return;
        float held = Time.time - scarSustainStartedAt;
        foreach (var cue in scarCues)
        {
            bool active = cue.shot != null && cue.shot.gameObject.activeInHierarchy;
            if (held < 3 - Time.deltaTime * 3)
            {
                Check(active && cue.shot.Visual.gameObject.activeInHierarchy,
                    "SW03 planted blade and damage remain alive throughout the three-second hold");
                Check((Vector2)cue.shot.transform.position == cue.origin && Get<Vector2>(cue.shot, "velocity") == Vector2.zero,
                    "SW03 planted blade remains fixed at its original ground position");
            }
            if (!active) continue;
            if (held >= 0.5f && !cue.checkedSustainedDamage)
            {
                CheckScarContact(cue, true);
                cue.checkedSustainedDamage = true;
            }
            if (held >= 2.5f && !cue.checkedLateContact)
            {
                CheckScarContact(cue, false);
                cue.checkedLateContact = true;
            }
        }
    }
    static void CheckScarContact(ScarCue cue, bool firstContact)
    {
        var player = game.Player;
        var body = player.GetComponent<Rigidbody2D>();
        Vector2 position = body.position;
        float health = player.Health, invulnerableUntil = Get<float>(player, "invulnerableUntil");
        float wardUntil = Get<float>(player, "guardUntil"), damageTaken = game.DamageTaken;
        try
        {
            body.position = cue.origin; player.transform.position = cue.origin; Physics2D.SyncTransforms();
            Set(player, "invulnerableUntil", 0f); Set(player, "guardUntil", 0f);
            Check(cue.contact.Consumed != firstContact,
                firstContact ? "uncontacted SW03 scar keeps its damage available during the hold" : "SW03 contact remains consumed near the end of its hold");
            var size = Get<Vector2>(cue.shot, "size");
            float damage = Get<float>(cue.shot, "damage");
            enemy.TryAttackHit(cue.origin, cue.origin, size, damage, cue.contact, 0);
            float afterFirst = player.Health;
            Check(firstContact ? afterFirst < health : afterFirst == health,
                firstContact ? "SW03 planted scar deals real damage after its rising animation has ended" : "late re-entry into the same SW03 scar deals no second hit");
            for (int repeat = 0; repeat < 20; repeat++)
            {
                Set(player, "invulnerableUntil", 0f);
                enemy.TryAttackHit(cue.origin, cue.origin, size, damage, cue.contact, 0);
            }
            Check(cue.contact.Consumed && player.Health == afterFirst,
                "SW03 overlapping damage cannot repeat even when player invulnerability is cleared");
        }
        finally
        {
            body.position = position; player.transform.position = position; Physics2D.SyncTransforms();
            Set(player, "<Health>k__BackingField", health);
            Set(player, "invulnerableUntil", invulnerableUntil); Set(player, "guardUntil", wardUntil);
            game.DamageTaken = damageTaken;
        }
    }
    static KarmaBossAttack SpawnCleanupScar()
    {
        return KarmaBossAttack.Spawn(game, enemy, Get<Transform>(enemy, "effectsRoot"), new Vector2(20, 6.75f),
            Vector2.zero, new Vector2(4.5f, 13.5f), 3.25f, 1, Color.white, null, 0,
            null, true, true, "SW03_RisingBlade");
    }
    static void ScarInterruptChecks()
    {
        var effects = Get<Transform>(enemy, "effectsRoot");
        var scar = SpawnCleanupScar();
        var warning = (SpriteRenderer)Call(enemy, "Mark", "SW03 cleanup warning", new Vector2(20, 6.75f), new Vector2(4.5f, 13.5f), true);
        Check(scar.Visual.gameObject.activeInHierarchy && warning.gameObject.activeInHierarchy,
            "SW03 cancellation probe begins with a planted blade and warning");
        Call(enemy, "InterruptPattern");
        Check(!scar.gameObject.activeInHierarchy && !scar.Visual.gameObject.activeInHierarchy && !warning.gameObject.activeInHierarchy,
            "SW03 interruption removes planted visual, actual damage and warning synchronously");
        Check(effects.GetComponentsInChildren<KarmaBossAttack>().Length == 0
            && effects.GetComponentsInChildren<KarmaAttackWarning>().Length == 0,
            "SW03 interruption leaves no active owned attack or telegraph");
    }
    static void ObserveExtendedWavePresentation(EnemyAttack attack, KarmaAttackWarning[] warnings)
    {
        var animator = Get<KarmaCharacterAnimator>(enemy, "animator");
        string state = Get<object>(enemy, "state").ToString();
        bool airborne = enemy.Position.y > KarmaSpace.GuardianGroundY + 0.05f;
        float hurtUntil = Get<float>(animator, "hurtUntil");
        // SW04 deliberately receives ten hits each tick. Inspect its underlying
        // phase separately, then restore Hurt; priority is verified independently.
        if (attack == EnemyAttack.SW04) Set(animator, "hurtUntil", -1f);
        animator.GuardianPose(state, attack, airborne, enemy.Armored);
        string pose = animator.CurrentPose;
        if (attack == EnemyAttack.SW04)
        {
            Set(animator, "hurtUntil", hurtUntil);
            animator.GuardianPose(state, attack, airborne, enemy.Armored);
        }
        bool warningActive = false;
        foreach (var warning in warnings) warningActive |= warning.gameObject.activeInHierarchy;
        if (state == "Recover")
        {
            Check(pose == "FollowThrough", attack + " recovery visibly overrides every attack phase");
            wavePhases.Add("Recovery");
        }
        if (attack == EnemyAttack.SW03)
        {
            int volley = enemy.Enraged ? 4 : 3;
            if (movingAttacks.Count == volley && scarAttacks.Count == 0 && warningActive && pose == "Windup") wavePhases.Add("ScarWarning");
            if (scarAttacks.Count > 0 && pose == "Rising") wavePhases.Add("ScarRise");
        }
        else if (attack == EnemyAttack.SW04 && Get<bool>(enemy, "swordGuard"))
        {
            if (pose == "Guard") wavePhases.Add("WardGuard");
            if (warningActive && (pose == "Windup" || pose == "Slash")) wavePhases.Add("WardWarning");
            if (movingAttacks.Count > 0 && pose == "Wave") wavePhases.Add("WardRelease");
        }
        else if (attack == EnemyAttack.SW05)
        {
            if (state == "Recover") Check(FindFormingAerialSword() == null,
                "SW05 recovery contains no decorative sword or promised path");
            if (airborne && !sawFullShield && !enemy.Invulnerable && pose == "Rising") wavePhases.Add("Jump");
            if (enemy.Invulnerable && pose == "Guard") wavePhases.Add("AirGuard");
            if (enemy.Invulnerable && movingAttacks.Count > 0 && pose == "Wave") wavePhases.Add("AirRelease");
            if (airborne && sawFullShield && !enemy.Invulnerable && pose == "Rising") wavePhases.Add("Descent");
        }
    }
    static void CheckWaveProjectile(KarmaBossAttack shot, EnemyAttack attack)
    {
        latestWaveShot = shot;
        var tuning = game.Config.BossTuning;
        Vector2 baseline = new Vector2(Mathf.Max(KarmaSpace.Units(0.2f), tuning.swordWidth),
            Mathf.Clamp(tuning.swordHeight, KarmaSpace.Units(0.2f), KarmaSpace.Units(0.8f)));
        bool aimed = attack == EnemyAttack.SW02;
        float expectedSpeed = aimed ? tuning.swordSpeed * 1.25f
            : attack == EnemyAttack.SW04 || attack == EnemyAttack.SW05 ? tuning.swordSpeed : Mathf.Max(KarmaSpace.Scale, tuning.swordSpeed);
        Vector2 velocity = Get<Vector2>(shot, "velocity");
        Vector2 expectedSize = aimed ? (Vector2)Call(enemy, "SwordSize", attack)
            : baseline * (attack == EnemyAttack.SW01 || attack == EnemyAttack.SW03 ? new Vector2(1.35f, 1.3f) : Vector2.one * 1.3f);
        Check(Vector2.Distance(Get<Vector2>(shot, "size"), expectedSize) < 0.0001f,
            attack + " enlarges actual collision length and thickness by the requested factors");
        Check(Mathf.Abs(velocity.magnitude - expectedSpeed) < 0.0001f, attack + " keeps the existing projectile speed");
        Check(Mathf.Abs(Get<float>(shot, "damage") - game.Config.enemyDamage * tuning.damageMultiplier * (aimed ? 1.15f : 1)) < 0.0001f,
            attack + " keeps the existing damage");
        float life = Mathf.Clamp(tuning.swordLifetime, 0.5f, 2);
        if (aimed)
        {
            Vector2 origin = (Vector2)Call(enemy, "AimedSlashOrigin", enemy.Position, Mathf.Sign(velocity.x));
            life = (float)Call(enemy, "AimedSlashLife", origin, velocity);
        }
        if (attack == EnemyAttack.SW05) life = aerialCues[movingAttacks.Count - 1].launchLife;
        Check(Get<float>(shot, "remaining") <= life + 0.0001f && Get<float>(shot, "remaining") >= life - 0.11f,
            attack + " uses its promised projectile lifetime");
        Check(shot.GetComponentsInChildren<Collider2D>().Length == 0,
            attack + " visual layers do not add physical colliders");
        Check(shot.Visual.sprite == KarmaArtwork.Sprite("ExtraWaveVFX", KarmaWaveArtwork.Pose(attack)),
            attack + " uses its supplied ExtraWaveVFX projectile sprite");
        if (attack == EnemyAttack.SW05) Check(enemy.Invulnerable, "SW05 releases each sword during its existing full shield");
        Vector2 cuttingWorld = aimed
            ? (Vector2)shot.Visual.transform.TransformDirection(Vector3.left * (shot.Visual.flipX ? -1 : 1))
            : (Vector2)shot.Visual.transform.right;
        Check(Vector2.Dot(cuttingWorld, velocity.normalized) > 0.999f,
            attack + " projectile artwork points along the actual attack velocity");
        if (aimed)
        {
            Vector2 locked;
            Check(lockedWaveAims.TryGetValue(movingAttacks.Count - 1, out locked), "SW02 launch has a previously observed locked direction");
            Check(Vector2.Dot(locked, velocity.normalized) > 0.999f, "SW02 projectile uses its fixed warning direction after the player moves");
        }
    }
    static void ObserveWavePresentation(EnemyAttack attack, KarmaAttackWarning[] warnings)
    {
        foreach (Transform child in Get<Transform>(enemy, "effectsRoot"))
        {
            if (child.name != "Wave sword swing" || !child.gameObject.activeInHierarchy || !waveSwings.Add(child)) continue;
            Check(latestWaveShot != null, attack + " swing effect begins only with an actual projectile release");
            Check(child.GetComponentsInChildren<Collider2D>().Length == 0
                && child.GetComponentsInChildren<KarmaBossAttack>().Length == 0,
                attack + " swing effect has no collider or independent damage component");
            var edge = child.Find("Ivory edge").GetComponent<SpriteRenderer>();
            Check(edge.sprite == KarmaArtwork.Sprite("GuardianVFX", attack == EnemyAttack.SW02 ? "Beam" : "Cleave"),
                attack + " swing reuses an existing GuardianVFX sprite");
            Vector2 velocity = Get<Vector2>(latestWaveShot, "velocity"), size = Get<Vector2>(latestWaveShot, "size");
            Check(Vector2.Dot(child.right, velocity.normalized) > 0.999f,
                attack + " swing direction matches the actual projectile");
            float life = Mathf.Clamp(game.Config.BossTuning.swordLifetime, 0.5f, 2);
            Vector2 origin = attack == EnemyAttack.SW02
                ? (Vector2)Call(enemy, "AimedSlashOrigin", enemy.Position, Mathf.Sign(velocity.x))
                : (Vector2)latestWaveShot.transform.position - velocity * (life - Get<float>(latestWaveShot, "remaining"));
            Check(Vector2.Distance(child.position, origin) < 0.001f, attack + " swing starts at the actual projectile origin");
            Vector2 axis = velocity.normalized;
            Vector2 extent = new Vector2(Mathf.Abs(axis.x) * size.x + Mathf.Abs(axis.y) * size.y, Mathf.Abs(axis.y) * size.x + Mathf.Abs(axis.x) * size.y);
            Check(edge.bounds.size.x <= extent.x + 0.001f && edge.bounds.size.y <= extent.y + 0.001f,
                attack + " swing core fits within the reviewed projectile footprint");
        }
        var animator = Get<KarmaCharacterAnimator>(enemy, "animator");
        string state = Get<object>(enemy, "state").ToString();
        animator.GuardianPose(state, attack, false, enemy.Armored);
        string pose = animator.CurrentPose;
        if (state == "Recover")
        {
            Check(pose == "FollowThrough", attack + " recovery visibly overrides attack cues");
            wavePhases.Add("Recovery");
            return;
        }
        if (pose == "Wave" && movingAttacks.Count > 0) wavePhases.Add((movingAttacks.Count - 1) + "/Wave");
        foreach (var warning in warnings)
        {
            if (!warning.gameObject.activeInHierarchy) continue;
            int volley = movingAttacks.Count;
            if (pose == "Windup" || pose == "Guard" || pose == "Slash") wavePhases.Add(volley + "/" + pose);
            bool locked = Get<Color>(warning, "baseColor").r > 0.8f;
            if (attack == EnemyAttack.SW02)
            {
                var dimensions = (Vector2)Call(enemy, "SwordSize", EnemyAttack.SW02);
                var warningArrow = Get<LineRenderer>(warning, "arrow");
                Vector2 heading = (warningArrow.GetPosition(1) - (warningArrow.GetPosition(0) + warningArrow.GetPosition(2)) / 2).normalized;
                dimensions = new Vector2(Mathf.Abs(heading.x) * dimensions.x + Mathf.Abs(heading.y) * dimensions.y,
                    Mathf.Abs(heading.y) * dimensions.x + Mathf.Abs(heading.x) * dimensions.y);
                var bounds = warning.GetComponent<SpriteRenderer>().bounds;
                Check(bounds.min.x >= KarmaStage.ArenaLeft - 0.001f
                    && bounds.max.x <= KarmaStage.ArenaRight + 0.001f
                    && Mathf.Abs(bounds.min.y - 0.05f) <= 0.001f
                    && Mathf.Abs(bounds.size.y - dimensions.y) <= 0.001f,
                    "SW02 tracking and locked paths stay inside the actual arena-limited projectile reach");
            }
            if (attack != EnemyAttack.SW02 || !locked) continue;
            var arrow = Get<LineRenderer>(warning, "arrow");
            Vector3 head = arrow.GetPosition(1), tail = (arrow.GetPosition(0) + arrow.GetPosition(2)) / 2;
            if (!arrow.useWorldSpace) { head = arrow.transform.TransformPoint(head); tail = arrow.transform.TransformPoint(tail); }
            Vector2 aim = (head - tail).normalized;
            Vector2 saved;
            if (lockedWaveAims.TryGetValue(volley, out saved))
                Check(Vector2.Dot(saved, aim) > 0.999f, "SW02 fixed path does not retrack the moving player");
            else
            {
                lockedWaveAims.Add(volley, aim);
                // Move after the fixed warning exists; the current shot must keep its old direction.
                Vector2 at = game.Player.Position;
                at.x = at.x < enemy.Position.x ? 27 : 13;
                game.Player.transform.position = at;
                game.Player.GetComponent<Rigidbody2D>().position = at;
            }
        }
    }
    static int ExpectedAerialCount()
    {
        int total = 0;
        foreach (int count in aerialGroupSizes) total += count;
        return total;
    }
    static int AerialGroupForShot(int shot)
    {
        for (int group = 0, first = 0; group < aerialGroupSizes.Length; group++)
        {
            first += aerialGroupSizes[group];
            if (shot < first) return group;
        }
        throw new Exception("SW05 observed a sword outside its seeded volley plan: " + shot);
    }
    static SpriteRenderer FindFormingAerialSword()
    {
        if (enemy == null) return null;
        var effects = Get<Transform>(enemy, "effectsRoot");
        foreach (Transform child in effects)
            if (child.name == FormingAerialName && child.gameObject.activeInHierarchy) return child.GetComponent<SpriteRenderer>();
        return null;
    }
    static void ObserveAerialFormation(Transform effects, KarmaAttackWarning[] warnings)
    {
        var blades = new List<SpriteRenderer>();
        foreach (Transform child in effects)
            if (child.name == FormingAerialName && child.gameObject.activeInHierarchy)
                blades.Add(child.GetComponent<SpriteRenderer>());
        if (blades.Count == 0) return;
        var planner = effects.GetComponentInChildren<KarmaAerialSwordPlanner>();
        Check(planner != null, "SW05 owns its physical escape planner during formation");
        if (aerialGroupSizes.Length == 0)
        {
            aerialGroupSizes = (int[])planner.GroupSizes.Clone();
            var expected = KarmaAerialSwordPlanner.CreateGroupSizes(game.Config.BossTuning, new System.Random(planner.Seed));
            Check(expected.Length == aerialGroupSizes.Length, "SW05 group count is reproducible from the recorded seed");
            for (int i = 0; i < expected.Length; i++) Check(expected[i] == aerialGroupSizes[i], "SW05 seeded group membership is reproducible");
            if (game.Config.BossTuning.aerialSwordSeed == 0)
                Check(randomAerialSeeds.Add(planner.Seed), "normal runtime casts use a fresh random seed");
            else Check(planner.Seed == game.Config.BossTuning.aerialSwordSeed, "runtime honors the fixed replay seed");
        }
        int group = AerialGroupForShot(movingAttacks.Count);
        Check(blades.Count == aerialGroupSizes[group] && warnings.Length == blades.Count,
            "SW05 forms fifteen independent harmless swords with one promised path each");
        for (int member = 0; member < blades.Count; member++)
            ObserveAerialSword(blades[member], warnings[member], movingAttacks.Count + member);
    }
    static void ObserveAerialSword(SpriteRenderer blade, KarmaAttackWarning warningPath, int shotIndex)
    {
        Check(enemy.Invulnerable, "SW05 decorative swords form during the original airborne shield");
        Check(blade.sprite == KarmaArtwork.Sprite("ExtraWaveVFX", "SW05_AerialSword"), "SW05 formation uses the supplied sword image");
        Check(blade.GetComponentsInChildren<KarmaBossAttack>().Length == 0
            && blade.GetComponentsInChildren<Collider2D>().Length == 0,
            "SW05 forming sword has no independent damage or collision component");
        var tuning = game.Config.BossTuning;
        var size = new Vector2(Mathf.Max(KarmaSpace.Units(0.2f), tuning.swordWidth),
            Mathf.Clamp(tuning.swordHeight, KarmaSpace.Units(0.2f), KarmaSpace.Units(0.8f))) * 1.3f;
        Vector2 origin = blade.transform.position;
        Check(Vector2.Distance(origin, enemy.Position) <= 6 && origin.x >= KarmaStage.ArenaLeft
            && origin.x <= KarmaStage.ArenaRight && origin.y >= 0 && origin.y <= KarmaSpace.Units(12),
            "SW05 forms every independent sword near the airborne guardian inside its legal arena");
        Vector2 aim = blade.transform.right;
        Check(Vector2.Distance(blade.transform.position, origin) < 0.0001f,
            "SW05 forming sword remains at its upcoming projectile origin");
        float life = float.PositiveInfinity;
        Vector2 velocity = aim * tuning.swordSpeed;
        if (velocity.x > 0) life = Mathf.Min(life, (KarmaStage.ArenaRight - origin.x) / velocity.x);
        else if (velocity.x < 0) life = Mathf.Min(life, (KarmaStage.ArenaLeft - origin.x) / velocity.x);
        if (velocity.y > 0) life = Mathf.Min(life, (KarmaSpace.Units(12) - origin.y) / velocity.y);
        else if (velocity.y < 0) life = Mathf.Min(life, -origin.y / velocity.y);
        float launchLife = Mathf.Max(0, life);
        Vector2 end = origin + velocity * Mathf.Max(0, life);
        var bounds = warningPath.GetComponent<SpriteRenderer>().bounds;
        Vector2 extent = new Vector2(Mathf.Abs(aim.x) * size.x + Mathf.Abs(aim.y) * size.y,
            Mathf.Abs(aim.y) * size.x + Mathf.Abs(aim.x) * size.y);
        Check(Vector2.Distance(bounds.center, (origin + end) / 2) < 0.001f
            && Vector2.Distance(bounds.size, new Vector2(Mathf.Abs(end.x - origin.x), Mathf.Abs(end.y - origin.y)) + extent) < 0.001f,
            "SW05 warning matches each sword's full boundary flight and swept damage width");
        AerialCue cue;
        if (!aerialCues.TryGetValue(shotIndex, out cue))
        {
            var core = KarmaWaveArtwork.CorePixels("SW05_AerialSword");
            float ppu = blade.sprite.pixelsPerUnit;
            cue = new AerialCue { formingSword = blade, origin = origin, aim = aim,
                rotation = blade.transform.rotation, readyScale = new Vector3(size.x * ppu / core.width, size.y * ppu / core.height, 1),
                launchLife = launchLife,
                firstSeen = enemy.FightSeconds, alpha = blade.color.a,
                pathLength = Vector2.Distance(origin, (Vector2)bounds.center * 2 - origin) };
            aerialCues.Add(shotIndex, cue);
        }
        Check(cue.formingSword == blade && Vector2.Dot(cue.aim, aim) > 0.9999f,
            "SW05 ornament and locked path stay fixed until their own release");
        Check(blade.color.a + 0.0001f >= cue.alpha && blade.color.a <= 0.8601f,
            "SW05 formation opacity grows without an opaque glowing flash");
        cue.grew |= blade.color.a > cue.alpha + 0.0001f;
        cue.alpha = blade.color.a;
        float formed = blade.transform.localScale.x / cue.readyScale.x;
        Check(formed >= 0.4499f && formed <= 1.0001f && formed + 0.0001f >= cue.formed,
            "SW05 decorative sword grows smoothly within the actual projectile's ready size");
        cue.formed = formed;
    }
    static void CheckAerialHandoff(KarmaBossAttack shot, int shotIndex)
    {
        AerialCue cue;
        Check(aerialCues.TryGetValue(shotIndex, out cue), "SW05 actual sword has a previously observed harmless formation");
        if (cue == null) return;
        Vector2 velocity = Get<Vector2>(shot, "velocity");
        float life = cue.launchLife;
        Vector2 origin = (Vector2)shot.transform.position - velocity * (life - Get<float>(shot, "remaining"));
        Check(Vector2.Distance(origin, cue.origin) < 0.001f && Vector2.Dot(velocity.normalized, cue.aim) > 0.9999f,
            "SW05 actual projectile continues from its decorative sword's exact origin and locked aim");
        Check(Quaternion.Angle(shot.Visual.transform.rotation, cue.rotation) < 0.001f
            && Vector3.Distance(shot.Visual.transform.localScale, cue.readyScale) < 0.0001f,
            "SW05 release uses the formed Blade orientation and ready scale");
        Check(FindFormingAerialSword() == null, "SW05 ornament disappears in the same observed frame as its actual release");
        foreach (var mark in Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<KarmaAttackWarning>(true))
            Check(!mark.gameObject.activeInHierarchy, "SW05 released group's warning is inactive in the actual launch frame");
        Check(shot.GetComponentsInChildren<LineRenderer>(true).Length == 0
            && shot.Visual.GetComponent<KarmaCombatVFX>() == null && shot.Visual.color == Color.white
            && shot.Visual.sharedMaterial == Resources.Load<Material>("KarmaAerialSword"),
            "SW05 actual launch uses only its full-opacity blue-white PNG without a boundary or orange VFX layer");
        cue.launchFrame = Time.frameCount;
        cue.launchAt = enemy.FightSeconds;
        cue.contact = Get<KarmaAttackContact>(shot, "contact");
        if (shotIndex == 0 || AerialGroupForShot(shotIndex) != AerialGroupForShot(shotIndex - 1))
            Debug.Log("SW05 RELEASE phase=" + (enemy.Enraged ? "enraged" : "normal")
                + " group=" + (AerialGroupForShot(shotIndex) + 1) + "/7 at=" + cue.launchAt
                + " relative=" + (cue.launchAt - aerialCues[0].launchAt)
                + " count=" + aerialGroupSizes[AerialGroupForShot(shotIndex)]);
        // Capture the real final center synchronously before cleanup destroys
        // the projectile. Sampling only active shots misses its final Update.
        var travel = shot.gameObject.AddComponent<KarmaAerialSwordTravelProbe>();
        travel.Stopped = (position, maxStep) =>
        {
            cue.finalPosition = position;
            cue.maxProjectileStep = maxStep;
            cue.projectileStopped = true;
            cue.stoppedAt = enemy.FightSeconds;
        };
        var tuning = game.Config.BossTuning;
        float tempo = enemy.Enraged ? Mathf.Clamp(tuning.enrageTempo, 0.65f, 1) : 1;
        float warning = Mathf.Max(0.28f, 0.5f * Mathf.Clamp(tuning.warningMultiplier, 0.65f, 1.5f) * tempo);
        Check(Mathf.Abs(enemy.FightSeconds - cue.firstSeen - warning) <= Time.deltaTime * 2 + 0.0001f,
            "SW05 formation stays inside the unchanged warning time: observed="+(enemy.FightSeconds-cue.firstSeen)+" warning="+warning+" dt="+Time.deltaTime);
    }
    static void CheckAerialFinalReach()
    {
        var tuning = game.Config.BossTuning;
        float dt = Time.deltaTime, lastStop = 0;
        var measure = new AerialMeasurement { enraged = enemy.Enraged, seed = tuning.aerialSwordSeed,
            total = aerialCues.Count, counts = (int[])aerialGroupSizes.Clone(), releaseTimes = new float[7],
            flightLifetimes = new float[aerialCues.Count], travelDistances = new float[aerialCues.Count] };
        Check(aerialGroupSizes.Length == 7 && Mathf.Abs(tuning.aerialSwordInterval - 1) < 0.0001f,
            "the current configuration executes exactly seven one-second release slots");
        for (int group = 0, first = 0; group < 7; first += aerialGroupSizes[group++])
        {
            measure.releaseTimes[group] = aerialCues[first].launchAt - aerialCues[0].launchAt;
            Check(Mathf.Abs(measure.releaseTimes[group] - group) <= dt * 2 + 0.001f,
                "actual launch starts follow 0,1,2,3,4,5,6 seconds without accumulating warning waits");
            if (group > 0) Check(Mathf.Abs(measure.releaseTimes[group] - measure.releaseTimes[group - 1] - 1) <= dt * 2 + 0.001f,
                "actual adjacent salvos preserve approximately one-second spacing in both phases");
        }
        for (int i = 0; i < aerialCues.Count; i++)
        {
            var cue = aerialCues[i];
            Check(cue.projectileStopped, "every real sword, including the last volley, records its natural endpoint");
            Vector2 promisedEnd = cue.origin + cue.aim * cue.pathLength;
            float error = Vector2.Distance(cue.finalPosition, promisedEnd);
            measure.maxEndpointError = Mathf.Max(measure.maxEndpointError, error);
            Check(error < 0.003f, "every real SW05 projectile reaches its entire warned path before cleanup");
            Check(Mathf.Abs(cue.finalPosition.x - KarmaStage.ArenaLeft) < 0.003f
                || Mathf.Abs(cue.finalPosition.x - KarmaStage.ArenaRight) < 0.003f
                || Mathf.Abs(cue.finalPosition.y) < 0.003f || Mathf.Abs(cue.finalPosition.y - KarmaSpace.Units(12)) < 0.003f,
                "every actual SW05 sword ends at an arena boundary rather than in midair");
            Check(Mathf.Abs(cue.pathLength - tuning.swordSpeed * cue.launchLife) < 0.003f,
                "full boundary lifetime, real movement speed and promised distance agree");
            measure.flightLifetimes[i] = cue.launchLife;
            measure.travelDistances[i] = Vector2.Distance(cue.origin, cue.finalPosition);
            lastStop = Mathf.Max(lastStop, cue.stoppedAt);
        }
        Check(aerialShieldOn >= 0 && aerialShieldOff >= lastStop - 0.001f
            && aerialShieldOff - lastStop <= dt * 2 + 0.001f,
            "the shield remains until all old and final-volley swords finish, then releases promptly");
        Check(aerialRecoveryAt >= aerialShieldOff && Mathf.Abs(aerialRecoveryAt - aerialShieldOff - 0.5f) <= dt * 3 + 0.001f,
            "unshielded landing retains the original half-second motion before recovery");
        float tempo = enemy.Enraged ? Mathf.Clamp(tuning.enrageTempo, 0.65f, 1) : 1;
        float recovery = Mathf.Max(0.38f, 2 * Mathf.Clamp(tuning.recoveryMultiplier, 0.4f, 2) * tempo);
        float ended = enemy.FightSeconds;
        Check(Mathf.Abs(ended - aerialRecoveryAt - recovery) <= dt * 3 + 0.001f,
            "the original normal/enraged recovery length remains unchanged after the longer aerial phase");
        measure.duration = ended - aerialFightStarted;
        measure.shieldOn = aerialShieldOn - aerialFightStarted; measure.shieldOff = aerialShieldOff - aerialFightStarted;
        measure.shieldDuration = aerialShieldOff - aerialShieldOn; measure.recoveryAt = aerialRecoveryAt - aerialFightStarted;
        measure.simulatedFps=Mathf.RoundToInt(1/Time.captureDeltaTime);
        measure.peakProjectiles=aerialPeakAttacks; measure.peakObjects=aerialPeakObjects; measure.peakWarnings=aerialPeakWarnings;
        measure.gcBytes=aerialGcBytes; measure.maxFrameGcBytes=aerialMaxGc; measure.gcCollections=GC.CollectionCount(0)-aerialGcCollections;
        aerialFrameMs.Sort();
        if (aerialFrameMs.Count>0) { measure.mainThreadP95Ms=aerialFrameMs[Mathf.Min(aerialFrameMs.Count-1,Mathf.FloorToInt(aerialFrameMs.Count*.95f))]; measure.mainThreadMaxMs=aerialFrameMs[aerialFrameMs.Count-1]; }
        Check(allocationRecorder.Valid && frameRecorder.Valid && aerialFrameMs.Count>0,"actual Unity profiler samples accompany 30/60 Hz runtime validation");
        allocationRecorder.Dispose(); frameRecorder.Dispose();
        measure.recoveryDuration = ended - aerialRecoveryAt;
        measure.tailAfterLastRelease = lastStop - aerialCues[0].launchAt - measure.releaseTimes[6];
        aerialMeasurements.Add(measure);
        Debug.Log("SW05 TIMELINE " + JsonUtility.ToJson(measure));
    }
    static bool AerialPauseCheck()
    {
        if (index != 5 || aerialPauseDone) return false;
        if (!aerialPauseActive)
        {
            var blade = aerialFormationPauseDone ? null : FindFormingAerialSword();
            var live = Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<KarmaBossAttack>();
            var shot = !aerialProjectilePauseDone && live.Length > 0 ? live[0] : null;
            if (blade == null && shot == null) return false;
            aerialPauseBlade = blade; aerialPauseShot = shot;
            aerialPauseFight = enemy.FightSeconds; aerialPauseFrame = Time.frameCount;
            if (shot != null)
            {
                aerialPauseRemaining = Get<float>(shot, "remaining");
                aerialPauseShotPosition = shot.transform.position;
            }
            if (blade != null)
            {
                aerialPauseBladePosition = blade.transform.position; aerialPauseBladeScale = blade.transform.localScale;
                aerialPauseBladeRotation = blade.transform.rotation; aerialPauseAlpha = blade.color.a;
            }
            game.TogglePause(); aerialPauseActive = true; return true;
        }
        if (Time.frameCount < aerialPauseFrame + 5) return true;
        Check(game.Paused && enemy.Invulnerable && enemy.FightSeconds == aerialPauseFight,
            "SW05 pause preserves airborne invulnerability and freezes the combat clock");
        if (aerialPauseShot != null)
        {
            Check(aerialPauseShot.transform.position == aerialPauseShotPosition
                && Get<float>(aerialPauseShot, "remaining") == aerialPauseRemaining,
                "SW05 pause freezes a live sword's position and lifetime");
            aerialProjectilePauseDone = true;
        }
        if (aerialPauseBlade != null)
        {
            Check(aerialPauseBlade.transform.position == aerialPauseBladePosition
                && aerialPauseBlade.transform.localScale == aerialPauseBladeScale
                && aerialPauseBlade.transform.rotation == aerialPauseBladeRotation && aerialPauseBlade.color.a == aerialPauseAlpha,
                "SW05 pause freezes harmless formation growth and orientation");
            aerialFormationPauseDone = true;
        }
        game.TogglePause(); aerialPauseActive = false;
        aerialPauseDone = aerialFormationPauseDone && aerialProjectilePauseDone; return false;
    }
    static bool AerialCleanupCheck()
    {
        var blade = FindFormingAerialSword();
        var effects = Get<Transform>(enemy, "effectsRoot");
        var planner = effects.GetComponentInChildren<KarmaAerialSwordPlanner>();
        bool formation = index == PlayerDeathAerialCase || index == DeathAerialCase;
        if (!enemy.Invulnerable || movingAttacks.Count < 1
            || (formation ? blade == null : effects.GetComponentsInChildren<KarmaBossAttack>().Length == 0)) return false;
        var oldEnemy = enemy;
        if (index == InterruptAerialCase) Call(enemy, "InterruptPattern");
        else if (index == PlayerDeathAerialCase) game.PlayerDied();
        else if (index == DeathAerialCase) Call(enemy, "ReceiveDamage", enemy.MaxHealth + 1);
        else game.StartRun();
        Check(!oldEnemy.Invulnerable && !oldEnemy.Armored && !Get<bool>(oldEnemy, "running"),
            "SW05 interrupt/player death/restart immediately clears shield and stops the sequence");
        Check(effects.GetComponentsInChildren<KarmaBossAttack>().Length == 0
            && effects.GetComponentsInChildren<KarmaAttackWarning>().Length == 0,
            "SW05 interrupt/player death/restart immediately removes active swords and paths");
        Check(planner != null && !planner.gameObject.activeInHierarchy
            && !Get<Scene>(planner, "simulationScene").IsValid(),
            "SW05 interrupt/player death/restart also immediately releases its copied physics world");
        foreach (Transform child in effects)
            Check(!child.gameObject.activeInHierarchy, "SW05 interrupt/player death/restart disables decorative swords and VFX");
        Check(Mathf.Abs(oldEnemy.Position.y - KarmaSpace.GuardianGroundY) < 0.001f,
            "SW05 interrupted aerial pose returns to its existing ground recovery position");
        if (index == PlayerDeathAerialCase)
        {
            Check(game.Phase == RunPhase.Defeat, "SW05 player-death case reaches defeat");
            game.StartRun();
        }
        if (index == DeathAerialCase)
        {
            Check(!oldEnemy.Alive, "SW05 forced death during the normally invulnerable phase actually reaches zero health");
            game.StartRun();
        }
        if (index != InterruptAerialCase)
        {
            enemy = game.Enemies[0];
            game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            Check(!enemy.Invulnerable && !enemy.Armored && !Get<bool>(enemy, "running"),
                "SW05 restarted guardian has no airborne shield or queued attack");
        }
        Debug.Log("PLAY PASS " + index + " SW05 cleanup");
        return true;
    }
    static bool SwordWardCleanupCheck()
    {
        if (Get<bool>(enemy, "swordGuard") && !responseTriggered)
        { enemy.TakeDamage(1); responseTriggered = true; }
        var effects = Get<Transform>(enemy, "effectsRoot");
        if (!responseTriggered || movingAttacks.Count<2 || effects.GetComponentsInChildren<KarmaAttackWarning>().Length == 0) return false;
        var oldEnemy = enemy;
        if (index == InterruptWardCase) Call(enemy, "InterruptPattern");
        else if (index==StunWardCase) { Set(enemy,"pendingParry",true);Call(enemy,"Update");Check(enemy.Stunned,"actual parry interrupts the in-flight four-shot sequence"); }
        else if (index == DeathWardCase)
            enemy.TakeDamage(enemy.MaxHealth / Mathf.Max(0.001f, game.Config.BossTuning.shieldDamageMultiplier) + 1);
        else game.StartRun();
        Check(!Get<bool>(oldEnemy, "swordGuard") && !oldEnemy.Armored && !Get<bool>(oldEnemy, "running"),
            "SW04 interrupt/death/restart releases defense and stops coroutine");
        Check(effects.GetComponentsInChildren<KarmaBossAttack>().Length == 0
            && effects.GetComponentsInChildren<KarmaAttackWarning>().Length == 0,
            "SW04 interrupt/death/restart removes attacks and warnings immediately");
        foreach (Transform child in effects)
            Check(!child.gameObject.activeInHierarchy, "SW04 interrupt/death/restart disables orbiting blades and VFX");
        if (index == DeathWardCase)
        {
            Check(!oldEnemy.Alive, "SW04 death actually reached zero health");
            game.StartRun();
        }
        if (index != InterruptWardCase && index != StunWardCase)
        {
            enemy = game.Enemies[0];
            game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            Check(!enemy.Armored && !Get<bool>(enemy, "guardResponsePending"), "restarted guardian has no ward or queued retaliation");
        }
        Debug.Log("PLAY PASS " + index + " SW04 cleanup");
        return true;
    }
    static bool LifecycleChecks()
    {
        if(lifecycleStage==0)
        {
            Call(enemy,"InterruptPattern");
            typeof(KarmaEnemy).GetProperty("PrimaryType").SetValue(enemy,(Essence?)Essence.Guard);
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
            var plantedScar = SpawnCleanupScar();
            game.PlayerDied();
            Check(!Get<bool>(game.Player,"confined"),"death releases confinement synchronously");
            Check(effects.GetComponentsInChildren<KarmaBossAttack>().Length==0,"death disables live attacks synchronously");
            Check(!plantedScar.gameObject.activeInHierarchy && !plantedScar.Visual.gameObject.activeInHierarchy,
                "player death immediately removes SW03 planted blades and their damage");
            Check(!enemy.TrialActive && !enemy.Invulnerable,"death clears trial/shield");
            game.StartRun(); enemy=game.Enemies[0];
            Check(!game.Player.Stunned && !enemy.TrialActive && !enemy.Armored,"restart has no temporary state");
            effects=Get<Transform>(enemy,"effectsRoot");
            Check(effects.GetComponentsInChildren<KarmaBossAttack>().Length == 0,
                "restart contains no planted scar inherited from the defeated run");
            var deathScar = SpawnCleanupScar();
            enemy.TakeDamage(enemy.MaxHealth*2);
            Check(effects.GetComponentsInChildren<KarmaBossAttack>().Length==0,"boss death cancels owned attack");
            Check(!deathScar.gameObject.activeInHierarchy && !deathScar.Visual.gameObject.activeInHierarchy,
                "boss death removes the SW03 planted blade and its actual damage together");
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
        File.WriteAllText("SW05measurements.json", JsonUtility.ToJson(new AerialMeasurements {
            result = error == null ? "PASS" : "FAIL", utc = DateTime.UtcNow.ToString("o"), cases = aerialMeasurements.ToArray() }, true));
        if(error==null) Debug.Log(result); else Debug.LogException(error);
        EditorApplication.Exit(error==null ? 0 : 1);
    }
}
public sealed class KarmaScarLifetimeProbe : MonoBehaviour
{
    public Action<float> Stopped;
    void OnDisable()
    {
        Stopped?.Invoke(Time.time);
        Stopped = null;
    }
}
public sealed class KarmaBossPlayCheckDriver : MonoBehaviour
{
    public Action Step;
    void Update() { Step?.Invoke(); }
}
public sealed class KarmaAerialSwordTravelProbe : MonoBehaviour
{
    public Action<Vector2, float> Stopped;
    float maxStep;
    void OnEnable() { maxStep = Time.deltaTime; }
    void Update() { maxStep = Mathf.Max(maxStep, Time.deltaTime); }
    void OnDisable()
    {
        Stopped?.Invoke(transform.position, Mathf.Max(maxStep, Time.deltaTime));
        Stopped = null;
    }
}
#endif
