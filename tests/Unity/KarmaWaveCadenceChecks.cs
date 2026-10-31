#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KarmaPrototype;

// Observe the same isolated, current local runtime before/after changes. These
// are actual selectors, coroutines and travelled hit boxes, not a schedule model.
[InitializeOnLoad]
public static class KarmaWaveCadenceChecks
{
    const string Key = "K4RMA.WaveCadenceChecks";
    const float Window = 120;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] sealed class Row
    {
        public string stage, layout, phase, appearance, visibleSprite;
        public int stageIndex;
        public float maxHealth, startingHealth;
        public bool enraged, finalAppearance;
        public Color appearanceTint, combatTint;
        public string[] inheritance, unlockedPatterns;
        public float seconds = Window;
        public int swBasic, sw01, sw02, sw03, sw04, sw05, groundStrike, consecutive;
        public int basicProjectiles, basicSelectionsDuringWard, basicProjectilesDuringWard, skillBasicTransitions;
        public int retreats;
        public float retreatDistance, minimumRetreatDistance = -1, maximumRetreatDistance, minimumRetreatInterval = -1;
        public int patterns, completedPatterns, effectivePatterns, ineffectivePatterns;
        public int projectiles, resolvedProjectiles, pendingProjectiles, effectiveProjectiles, unreachableProjectiles, intentionalLaneMisses;
        public int stationaryAttacks, effectiveStationaryAttacks, selectionFailures;
        public float firstAttack = -1, firstEffectiveAttack = -1;
        public float meanRegularIdle, shortestUltimateInterval = -1;
        public float effectivePatternRate, effectiveProjectileRate, unreachableProjectileRate;
        public float approachSeconds, approachDistance, selectionFailureSeconds;
        public int regularIdleSamples;
    }
    [Serializable] sealed class Report
    {
        public string result;
        public string reachDefinition = "Actual travelled unrotated swept hit boxes expanded by player size; invulnerability does not affect geometry. firstEffectiveAttack is the launch time of the earliest shot that actually reaches the target. Intentional high/low lane misses are separate from pattern effectiveness; pending shots at 120 seconds are excluded from projectile rates.";
        public int assertions;
        public List<Row> stages = new List<Row>();
    }
    sealed class PatternSample
    {
        public EnemyAttack attack;
        public bool effective;
    }
    sealed class ShotSample
    {
        public KarmaBossAttack attack;
        public PatternSample pattern;
        public Row row;
        public Vector2 previous, size;
        public bool moving, effective, finished;
        public float launched;
    }
    static readonly Essence[][] Orders = {
        new[] { Essence.SwordWave }, new[] { Essence.SwordWave, Essence.RisingSlash },
        new[] { Essence.SwordWave, Essence.Guard }, new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard }
    };
    static readonly string[] Labels = { "Stage 2", "Stage 3 / SW03", "Stage 3 / SW04", "Stage 4" };
    static readonly string[] Layouts = { "fixed-14world", "far", "close", "wall", "air" };
    static readonly string[] AppearanceKeys = { "Wave/Prototype", "Wave/Prototype", "Wave/Prototype", "Wave/Prototype" };
    static readonly Dictionary<KarmaBossAttack, ShotSample> shots = new Dictionary<KarmaBossAttack, ShotSample>();
    static readonly List<KarmaBossAttack> expiredShots = new List<KarmaBossAttack>();
    static KarmaGame game;
    static KarmaEnemy enemy;
    static Transform effects;
    static Rigidbody2D playerBody;
    static Report report;
    static Row row;
    static PatternSample pattern;
    static int scenario, stageVariant, layoutIndex, phaseIndex;
    static bool wasRunning, selectionDue, selectionFailed, sampling;
    static float lastStart, finishedAt, recoveredAt, lastUltimate, idleTotal;
    static float retreatStart, retreatDistance, lastRetreatStart;
    static bool wasRetreating;
    static Vector2 beforePosition;
    static EnemyAttack previous;
    static double deadline;
    static FieldInfo Field(object target, string name) => target.GetType().GetField(name, Flags)
        ?? throw new MissingFieldException(target.GetType().Name, name);
    static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
    static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    static void Check(bool condition, string label)
    { report.assertions++; if (!condition) throw new Exception((row == null ? "Cadence" : row.stage + "/" + row.layout + "/" + row.phase) + ": " + label); }
    static KarmaWaveCadenceChecks()
    {
        EditorApplication.playModeStateChanged += OnState;
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Pump;
    }
    public static void Run()
    {
        SessionState.SetBool(Key, true);
        Application.runInBackground = true;
        EditorApplication.update += Pump;
        // Let Editor startup delay calls create the default Search database
        // before Play Mode makes Unity skip that first-use initialization.
        EditorApplication.delayCall += DeferPlayEntry;
    }
    static void DeferPlayEntry()
    {
        EditorApplication.delayCall += BeginPlay;
    }
    static void BeginPlay()
    {
        if (!SessionState.GetBool(Key, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
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
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying && game == null && Time.frameCount > 0)
            OnState(PlayModeStateChange.EnteredPlayMode);
    }
    static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false) || game != null) return;
        try
        {
            Time.captureDeltaTime = 0.05f;
            report = new Report();
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            if (game == null) game = new GameObject("Cadence check game").AddComponent<KarmaGame>();
            game.Config.playerHealth = 1000000;
            // Forty independent 120-second rows, instead of the former four.
            deadline = EditorApplication.timeSinceStartup + 900;
            Setup();
            var driver = new GameObject("Cadence check driver").AddComponent<KarmaWaveCadenceDriver>();
            driver.Before = Control; driver.After = Observe;
        }
        catch (Exception error) { Finish(error); }
    }
    static void Setup()
    {
        stageVariant = scenario % Orders.Length;
        layoutIndex = scenario / Orders.Length % Layouts.Length;
        phaseIndex = scenario / (Orders.Length * Layouts.Length);
        var order = Orders[stageVariant];
        game.StartRun();
        foreach (var essence in order) game.Player.Essences.Sacrifice(essence);
        // Loading just inheritance would leave HP, final tint and room at Stage 1.
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, order.Length);
        typeof(KarmaGame).GetProperty("LastSacrifice").SetValue(game, order[order.Length - 1]);
        Call(game, "LoadRoom", true); enemy = game.Enemies[0];
        float bossX = layoutIndex == 1 ? -13 : layoutIndex == 3 ? 53.8f : 20;
        enemy.transform.position = new Vector3(bossX, KarmaSpace.GuardianGroundY, 0);
        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * (phaseIndex == 0 ? 1 : 0.45f));
        effects = Get<Transform>(enemy, "effectsRoot");
        playerBody = game.Player.GetComponent<Rigidbody2D>(); playerBody.bodyType = RigidbodyType2D.Kinematic;
        PositionTarget();
        row = new Row {
            stage = Labels[stageVariant], layout = Layouts[layoutIndex], phase = phaseIndex == 0 ? "full-health" : "enraged",
            stageIndex = game.StageIndex, maxHealth = enemy.MaxHealth, startingHealth = enemy.Health,
            enraged = enemy.Enraged, appearance = AppearanceKeys[stageVariant], finalAppearance = game.StageIndex == 3
        };
        report.stages.Add(row);
        var inherited = game.Player.Essences.SacrificeOrder;
        row.inheritance = new string[inherited.Count];
        for (int i = 0; i < inherited.Count; i++) row.inheritance[i] = inherited[i].ToString();
        var unlocked = Get<List<EnemyAttack>>(enemy, "attacks");
        row.unlockedPatterns = new string[unlocked.Count];
        for (int i = 0; i < unlocked.Count; i++) row.unlockedPatterns[i] = unlocked[i].ToString();
        Check(game.StageIndex == order.Length && game.World.name == "Stage " + (order.Length + 1), "actual StageIndex and generated room agree");
        Check(Mathf.Abs(enemy.MaxHealth - game.Config.BossTuning.HealthFor(order.Length)) < 0.001f, "stage-specific maximum health");
        Check(Mathf.Abs(enemy.Health - enemy.MaxHealth * (phaseIndex == 0 ? 1 : 0.45f)) < 0.001f
            && enemy.Enraged == (phaseIndex != 0), "health and enhancement state are independent scenarios");
        Check(inherited.Count == order.Length && game.LastSacrifice == order[order.Length - 1], "inheritance count and last sacrifice match progression");
        for (int i = 0; i < order.Length; i++) Check(inherited[i] == order[i], "inheritance order " + i);
        var expected = KarmaPatternCatalog.BuildCycle(order);
        Check(unlocked.Count == expected.Count, "exact unlocked pattern count");
        for (int i = 0; i < expected.Count; i++) Check(unlocked[i] == expected[i], "unlocked pattern " + i);
        var animator = Get<KarmaCharacterAnimator>(enemy, "animator");
        var picture = animator.transform.Find("Guardian sprite").GetComponent<SpriteRenderer>();
        Sprite expectedAppearance = KarmaArtwork.Sprite("Wave", "Idle");
        Check(KarmaArtwork.GuardianPreview(order) == KarmaArtwork.Settings.Guardian(Essence.SwordWave), "all Wave previews use the prototype");
        Check(Get<Sprite>(animator, "waveAppearance") == null && Get<Sprite>(animator, "guardianAppearance") == null,
            "no inherited/final body override replaces the prototype poses");
        Color expectedTint = game.StageIndex == 3 ? new Color(1, 0.88f, 0.73f) : Color.white;
        Check(picture.color == expectedTint, "final-only initial appearance tint");
        row.appearanceTint = picture.color;
        Set(animator, "guardianPreviousPosition", animator.transform.position);
        animator.GuardianPose("Approach", EnemyAttack.SW01, false, false);
        Check(picture.sprite == expectedAppearance, "actual idle drawing uses the stage appearance");
        row.visibleSprite = picture.sprite.name; row.combatTint = picture.color;
        if (layoutIndex == 4) Check(game.Player.Airborne, "air layout has a genuinely airborne player");
        wasRunning = selectionDue = selectionFailed = false;
        lastStart = finishedAt = recoveredAt = lastUltimate = -1;
        idleTotal = 0; previous = (EnemyAttack)(-1); pattern = null;
        wasRetreating = false; retreatDistance = 0; retreatStart = lastRetreatStart = -1;
        shots.Clear(); expiredShots.Clear();
        sampling = true;
    }
    static void PositionTarget()
    {
        Vector2 at = layoutIndex == 0 ? new Vector2(20 + KarmaSpace.Units(7), KarmaSpace.PlayerHalfHeight)
            : layoutIndex == 1 ? new Vector2(53.5f, KarmaSpace.PlayerHalfHeight)
            : layoutIndex == 2 ? new Vector2(20.5f, KarmaSpace.PlayerHalfHeight)
            : layoutIndex == 3 ? new Vector2(54.2f, KarmaSpace.PlayerHalfHeight)
            : new Vector2(34, KarmaSpace.Units(5));
        playerBody.position = at; playerBody.linearVelocity = Vector2.zero;
        game.Player.transform.position = at;
    }
    static void Control()
    {
        if (enemy == null || row == null) return;
        try
        {
            PositionTarget();
            Set(game.Player, "invulnerableUntil", Time.time + 2);
            beforePosition = enemy.Position;
            selectionDue = !Get<bool>(enemy, "running") && !enemy.Stunned
                && Get<float>(enemy, "timer") <= Time.deltaTime + 0.00001f;
            if (Get<bool>(enemy, "swordGuard") && !Get<bool>(enemy, "guardResponsePending")
                && enemy.FightSeconds >= Get<float>(enemy, "nextGuardResponse")) enemy.TakeDamage(1);
        }
        catch (Exception error) { Finish(error); }
    }
    static void Observe()
    {
        if (row == null) return;
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "cadence wall-clock deadline");
            float now = enemy.FightSeconds;
            bool running = Get<bool>(enemy, "running");
            var selected = Get<EnemyAttack>(enemy, "selected");
            float started = Get<float>(enemy, "patternStarted");
            string state = Get<object>(enemy, "state").ToString();
            if (running && started != lastStart)
            {
                Check(!wasRunning, "a new pattern cannot start before the preceding recovery finishes");
                var ward = Get<Transform>(enemy, "swordGuardRoot");
                foreach (var shot in effects.GetComponentsInChildren<KarmaBossAttack>())
                    Check(ward != null && shot.transform.IsChildOf(ward), "only independent SW04 counters may overlap a new action");
                if (row.firstAttack < 0) row.firstAttack = now;
                if (selected == previous) row.consecutive++;
                if (finishedAt >= 0 && !KarmaPatternCatalog.IsUltimate(previous))
                { idleTotal += now - finishedAt; row.regularIdleSamples++; }
                switch (selected)
                {
                    case EnemyAttack.SWBasic:
                        row.swBasic++;
                        if (previous != EnemyAttack.SWBasic && (int)previous >= 0) row.skillBasicTransitions++;
                        if (Get<Transform>(enemy, "swordGuardRoot") != null) row.basicSelectionsDuringWard++;
                        break;
                    case EnemyAttack.SW01: row.sw01++; break;
                    case EnemyAttack.SW02: row.sw02++; break;
                    case EnemyAttack.SW03: row.sw03++; break;
                    case EnemyAttack.SW04: row.sw04++; break;
                    case EnemyAttack.SW05:
                        row.sw05++;
                        if (lastUltimate >= 0)
                        {
                            float gap = now - lastUltimate;
                            Check(gap + 0.001f >= Mathf.Max(10, game.Config.BossTuning.ultimateCooldown), "SW05 retains its ultimate gate");
                            row.shortestUltimateInterval = row.shortestUltimateInterval < 0 ? gap : Mathf.Min(row.shortestUltimateInterval, gap);
                        }
                        lastUltimate = now; break;
                    default: row.groundStrike++; break;
                }
                row.patterns++;
                pattern = new PatternSample { attack = selected };
                previous = selected; lastStart = started; recoveredAt = -1; selectionFailed = false;
            }
            SampleProjectiles();
            if (state == "Recover" && recoveredAt < 0) recoveredAt = now;
            if (wasRunning && !running)
            {
                Check(recoveredAt >= 0, "pattern ends through recovery");
                float recovery = selected == EnemyAttack.SWBasic ? 0.65f : selected == EnemyAttack.SW02 ? 1.1f
                    : selected == EnemyAttack.SW04 ? 0.45f : selected == EnemyAttack.SW05 ? 2 : 1;
                float tempo = enemy.Enraged ? Mathf.Clamp(game.Config.BossTuning.enrageTempo, 0.65f, 1) : 1;
                float expected = Mathf.Max(0.38f, recovery * Mathf.Clamp(game.Config.BossTuning.recoveryMultiplier, 0.4f, 2) * tempo);
                Check(now - recoveredAt + Time.captureDeltaTime + 0.001f >= expected, "the complete original recovery is retained");
                Check(!enemy.Invulnerable && !Get<bool>(enemy, "frontalGuard"), "action-owned defenses are released on recovery");
                if (Get<bool>(enemy, "swordGuard"))
                    Check(now < Get<float>(enemy, "swordGuardUntil"), "independent ward never outlives its original deadline");
                Check(Mathf.Abs(enemy.Position.y - KarmaSpace.GuardianGroundY) < 0.001f, "attack returns to grounded approach");
                row.completedPatterns++;
                if (pattern != null && pattern.effective) row.effectivePatterns++;
                else row.ineffectivePatterns++;
                pattern = null; finishedAt = now;
            }
            if (!running)
            {
                float travelled = Mathf.Abs(enemy.Position.x - beforePosition.x);
                if (travelled > 0.00001f) { row.approachSeconds += Time.deltaTime; row.approachDistance += travelled; }
                bool retreating = travelled > 0.00001f && enemy.Intent == "거리 유지 · 짧은 후퇴 후 공격";
                if (retreating)
                {
                    if (!wasRetreating)
                    {
                        row.retreats++; retreatStart = now; retreatDistance = 0;
                        if (lastRetreatStart >= 0)
                        {
                            float interval = now - lastRetreatStart;
                            row.minimumRetreatInterval = row.minimumRetreatInterval < 0 ? interval : Mathf.Min(row.minimumRetreatInterval, interval);
                            Check(interval + 0.001f >= game.Config.BossTuning.swordRetreatCooldown, "actual retreat starts respect cooldown");
                        }
                        lastRetreatStart = now;
                    }
                    Check(!enemy.Invulnerable, "retreat adds no invulnerability");
                    Check(now - retreatStart <= 0.4f + Time.captureDeltaTime + 0.001f, "actual retreat duration is bounded");
                    retreatDistance += travelled; row.retreatDistance += travelled;
                    Check(retreatDistance <= 3.001f, "actual Transform retreat remains within three world units");
                    row.maximumRetreatDistance = Mathf.Max(row.maximumRetreatDistance, retreatDistance);
                }
                if (wasRetreating && !retreating)
                    row.minimumRetreatDistance = row.minimumRetreatDistance < 0 ? retreatDistance : Mathf.Min(row.minimumRetreatDistance, retreatDistance);
                wasRetreating = retreating;
                if (selectionDue) { row.selectionFailures++; selectionFailed = true; }
                if (selectionFailed) row.selectionFailureSeconds += Time.deltaTime;
            }
            wasRunning = running;
            if (now < Window) return;
            row.seconds = now;
            row.meanRegularIdle = row.regularIdleSamples > 0 ? idleTotal / row.regularIdleSamples : 0;
            row.effectivePatternRate = row.completedPatterns > 0 ? (float)row.effectivePatterns / row.completedPatterns : 0;
            // Live shots at the exact 120-second cutoff are censored, not misses.
            row.pendingProjectiles = row.projectiles - row.resolvedProjectiles;
            row.effectiveProjectileRate = row.resolvedProjectiles > 0 ? (float)row.effectiveProjectiles / row.resolvedProjectiles : 0;
            row.unreachableProjectileRate = row.resolvedProjectiles > 0 ? (float)row.unreachableProjectiles / row.resolvedProjectiles : 0;
            Check(enemy.Enraged == (phaseIndex != 0), "measurement retains its requested enhancement state");
            Check(row.groundStrike == 0, "Wave selection never falls back to another boss type");
            if (stageVariant == 3) Check(row.sw05 > 0, "independent wards cannot starve the unlocked ultimate throughout a two-minute fight");
            // The same observer also measures the frozen original runtime. Its
            // missing spacing field marks the deliberate before-change run.
            if (typeof(KarmaBossTuning).GetField("swordPreferredDistance") != null && layoutIndex == 0)
            {
                Check(row.swBasic > 0 && row.basicProjectiles > 0, "ordinary stationary combat selects and actually launches basic waves in 120 seconds");
                Check(row.basicProjectiles >= row.swBasic - 1, "only a basic windup at the observation cutoff can remain unlaunched");
                Check(row.skillBasicTransitions > 0 && row.sw01 + row.sw02 + row.sw03 + row.sw04 + row.sw05 > 0,
                    "basic attacks accompany completed inherited skills");
                if (stageVariant >= 2)
                    Check(row.basicSelectionsDuringWard > 0 && row.basicProjectilesDuringWard > 0,
                        "actual AI selects and fires basic waves while SW04 remains active");
            }
            Debug.Log("CADENCE " + JsonUtility.ToJson(row));
            sampling = false;
            enemy.CancelCombat(); scenario++;
            if (scenario == Orders.Length * Layouts.Length * 2) { Finish(null); return; }
            Setup();
        }
        catch (Exception error) { Finish(error); }
    }
    static void SampleProjectiles()
    {
        // Include inactive objects: cleanup can deactivate a sword before this
        // frame's deferred destruction. Measure its actual last segment, never
        // the extra distance suggested by its original nominal lifetime.
        foreach (var attack in effects.GetComponentsInChildren<KarmaBossAttack>(true))
        {
            if (shots.ContainsKey(attack)) continue;
            Vector2 velocity = Get<Vector2>(attack, "velocity");
            Vector2 size = Get<Vector2>(attack, "size");
            bool moving = velocity.sqrMagnitude > 0.01f;
            bool laneMiss = moving && pattern != null && (pattern.attack == EnemyAttack.SW01 || pattern.attack == EnemyAttack.SW03)
                && Mathf.Abs(attack.transform.position.y - game.Player.Position.y) > size.y / 2 + KarmaSpace.PlayerHalfHeight;
            var sample = new ShotSample {
                attack = attack, pattern = pattern, row = row, previous = attack.transform.position, size = size,
                moving = moving, launched = enemy.FightSeconds
            };
            shots.Add(attack, sample);
            var ward = Get<Transform>(enemy, "swordGuardRoot");
            bool counter = ward != null && attack.transform.IsChildOf(ward);
            if (moving && pattern != null && pattern.attack == EnemyAttack.SWBasic && !counter)
            {
                row.basicProjectiles++;
                if (ward != null) row.basicProjectilesDuringWard++;
            }
            // OnDisable fires at the actual endpoint even when deferred Destroy
            // removes an attack before the driver's LateUpdate can inspect it.
            attack.gameObject.AddComponent<KarmaWaveCadenceShotObserver>().OnDisabled = () => SampleShot(sample, true);
            if (moving) { row.projectiles++; if (laneMiss) row.intentionalLaneMisses++; }
            else row.stationaryAttacks++;
        }
        expiredShots.Clear();
        foreach (var pair in shots)
        {
            var sample = pair.Value;
            SampleShot(sample, sample.attack == null || !sample.attack.gameObject.activeInHierarchy);
            if (sample.finished) expiredShots.Add(pair.Key);
        }
        foreach (var attack in expiredShots) shots.Remove(attack);
    }
    static void SampleShot(ShotSample sample, bool finished)
    {
        if (!sampling || sample.finished || row != sample.row) return;
        if (sample.attack != null)
        {
            Vector2 at = sample.attack.transform.position, target = game.Player.Position;
            Vector2 velocity = Get<Vector2>(sample.attack, "velocity");
            Vector2 axis = sample.moving ? velocity.normalized : Vector2.right;
            bool overlaps = KarmaBossGeometry.SweptOrientedBox(sample.previous.x, sample.previous.y, at.x, at.y,
                target.x, target.y, axis.x, axis.y, sample.size.x, sample.size.y, KarmaSpace.PlayerHalfWidth, KarmaSpace.PlayerHalfHeight);
            if (!sample.effective && overlaps)
            {
                sample.effective = true;
                if (!sample.moving) row.effectiveStationaryAttacks++;
                if (row.firstEffectiveAttack < 0 || sample.launched < row.firstEffectiveAttack) row.firstEffectiveAttack = sample.launched;
                if (sample.pattern != null) sample.pattern.effective = true;
            }
            sample.previous = at;
        }
        if (!finished) return;
        sample.finished = true;
        if (sample.moving)
        {
            row.resolvedProjectiles++;
            if (sample.effective) row.effectiveProjectiles++; else row.unreachableProjectiles++;
        }
    }
    static void Finish(Exception error)
    {
        sampling = false;
        EditorApplication.update -= Pump; SessionState.SetBool(Key, false);
        if (report == null) report = new Report();
        report.result = error == null ? "PASS" : "FAIL: " + error;
        File.WriteAllText("wave-cadence-validation.json", JsonUtility.ToJson(report, true));
        row = null;
        if (error == null) Debug.Log("PASS: Wave cadence comparison, " + report.assertions + " assertions");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
[DefaultExecutionOrder(-500)]
public sealed class KarmaWaveCadenceDriver : MonoBehaviour
{
    public Action Before, After;
    void Update() { Before?.Invoke(); }
    void LateUpdate() { After?.Invoke(); }
}
public sealed class KarmaWaveCadenceShotObserver : MonoBehaviour
{
    public Action OnDisabled;
    void OnDisable() { OnDisabled?.Invoke(); }
}
#endif
