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
using UnityEngine.SceneManagement;

// Sequential Play Mode checks: production pattern coroutine, real Rigidbody2D
// integration, real player movement/skill methods and the production hit ledger.
public static class KarmaAimedSlashChecks
{
    // Fast isolated entrypoint: -executeMethod KarmaAimedSlashChecks.Run.
    public static void Run() { KarmaAimedSlashStandaloneRunner.Run(); }
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const int ScenarioCount = 13;
    static KarmaEnemy enemy;
    static KarmaPlayer player;
    static KarmaAimedSlashMovementDriver movement;
    static int scenario, phase, assertions, pauseFrame;
    static float started, health, lockedAt, expectedLock, lockedFacing, recoveryAt;
    static float maxFeet, maxHead, minimumCrossingFeet, crossedAt, pauseClock;
    static Vector2 lockedOrigin, dimensions, initialPlayer;
    static Bounds lockedWarning;
    static bool sawTracking, sawLocked, swappedTracking, sawChangedTracking, crossed;
    static bool jumped, usedRise, sawWard, sawRecovery, pauseDone;
    static Vector2 pausedPosition;
    static readonly HashSet<string> poses = new HashSet<string>();
    static readonly List<Shot> shots = new List<Shot>();
    static readonly List<Measurement> measurements = new List<Measurement>();

    sealed class Shot
    {
        public KarmaBossAttack attack;
        public Vector2 last, velocity, size, final;
        public Quaternion rotation;
        public Vector3 scale;
        public float releasedAt, stoppedAt;
        public bool stopped;
    }
    [Serializable]
    sealed class Measurement
    {
        public string scenario;
        public bool enraged, walkedBehind, usedRisingSkill, damaged, wardAtRelease;
        public int projectiles;
        public float facing, lockSeconds, observedLockSeconds, width, height, floor, top;
        public float playerSpeed, pierceDistance, maxPlayerFeet, maxPlayerHead, crossingFeet;
        public float escapeSeconds, recoverySeconds, patternSeconds, flightSeconds;
        public Vector2 playerStart, playerEnd, origin, endpoint;
    }
    [Serializable]
    sealed class Report
    {
        public string result, utc;
        public int assertions;
        public Measurement[] cases;
    }
    static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Private).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Private).Invoke(o, args);
    static void Check(bool value, string label)
    {
        assertions++;
        if (!value) throw new Exception("SW02 live scenario " + scenario + " (" + Name + "): " + label);
    }
    static string Name => new[] {
        "right / elevated target", "left / elevated target / enraged", "tracking changes then locks",
        "walk behind right", "walk behind left", "left wall / outward", "right wall / outward",
        "left wall / inward", "right wall / inward", "opposite arena edges / walk",
        "ordinary jump apex", "jump plus active rising skill", "maintained SW04 / pause / interruption"
    }[scenario];
    static bool Walking => scenario >= 3 && scenario <= 9;
    static bool Jumping => scenario == 10 || scenario == 11;
    static void PlayerAt(Vector2 at)
    {
        var body = player.GetComponent<Rigidbody2D>();
        body.position = at; player.transform.position = at; body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }
    static void Launch(EnemyAttack attack)
    {
        Set(enemy, "selected", attack); Set(enemy, "running", true);
        Set(enemy, "patternStarted", enemy.FightSeconds);
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static void Prepare(KarmaGame game)
    {
        // All three internalized essences include the inheritance state without
        // an active rising skill. Walking cases never invoke any ability.
        Set(game.Player.Essences, "progression", new KarmaProgressionState());
        foreach (var essence in new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard }) game.Player.Essences.Sacrifice(essence);
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
        Call(game, "LoadRoom", true);
        enemy = game.Enemies[0]; player = game.Player;
        player.enabled = false; // Driver injects input before the real FixedUpdate.
        var body = player.GetComponent<Rigidbody2D>();
        body.bodyType = Walking || Jumping ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.None;
        body.gravityScale = game.Config.gravityScale;
        movement = new GameObject("SW02 real movement input").AddComponent<KarmaAimedSlashMovementDriver>();
        movement.Player = player;
        movement.Drive = Walking || Jumping;
        Call(enemy, "InterruptPattern");
        Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue);
        Set(enemy, "nextSwordRetreat", float.MaxValue);
        Set(enemy, "nextUltimate", float.MaxValue);
        float bossX = 20, playerX = 28, playerY = KarmaSpace.PlayerHalfHeight + 0.01f;
        if (scenario == 0) { playerX = 30; playerY = 15; }
        if (scenario == 1) { playerX = 10; playerY = 15; }
        if (scenario == 4) playerX = 12;
        if (scenario == 5) { bossX = KarmaStage.BossLeftWall; playerX = KarmaStage.ArenaLeft + KarmaSpace.PlayerHalfWidth; }
        if (scenario == 6) { bossX = KarmaStage.BossRightWall; playerX = KarmaStage.ArenaRight - KarmaSpace.PlayerHalfWidth; }
        if (scenario == 7) { bossX = KarmaStage.BossLeftWall; playerX = bossX + 8; }
        if (scenario == 8) { bossX = KarmaStage.BossRightWall; playerX = bossX - 8; }
        if (scenario == 9) { bossX = KarmaStage.BossLeftWall; playerX = KarmaStage.ArenaRight - KarmaSpace.PlayerHalfWidth; }
        if (Jumping) { bossX = 0; playerX = 27; }
        enemy.transform.position = new Vector3(bossX, KarmaSpace.GuardianGroundY, 0);
        Set(enemy, "direction", playerX < bossX ? -1f : 1f);
        PlayerAt(new Vector2(playerX, playerY));
        if (scenario == 0)
        {
            Vector2 from = player.GetComponent<Rigidbody2D>().position;
            Call(player, "BeginDirectionalSlash", Vector2.right, game.Config.Techniques.pierceDistance, Essence.SwordWave);
            Check(Mathf.Abs(Vector2.Distance(from, player.GetComponent<Rigidbody2D>().position)
                - game.Config.Techniques.pierceDistance) < 0.001f,
                "real piercing slash travels its configured eight-world-unit distance on an unobstructed route");
            PlayerAt(new Vector2(playerX, playerY));
        }
        Set(player, "invulnerableUntil", -1f); Set(player, "guardUntil", -1f);
        if (scenario == 1) typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.45f);
        if (scenario == 11)
        {
            // Flame remains the guardian's inherited type; the player has kept
            // Dash active in this different valid inheritance state.
            Set(player.Essences, "progression", new KarmaProgressionState());
            player.Essences.Sacrifice(Essence.SwordWave); player.Essences.Sacrifice(Essence.Guard);
            Check(player.Essences.HasActive(Essence.RisingSlash), "rising test owns the active rising technique");
        }
        dimensions = (Vector2)Call(enemy, "SwordSize", EnemyAttack.SW02);
        poses.Clear(); shots.Clear();
        sawTracking = sawLocked = swappedTracking = sawChangedTracking = crossed = false;
        jumped = usedRise = sawWard = sawRecovery = pauseDone = false;
        lockedAt = expectedLock = recoveryAt = crossedAt = 0;
        maxFeet = maxHead = float.NegativeInfinity; minimumCrossingFeet = float.PositiveInfinity;
        initialPlayer = player.Position; health = player.Health; started = enemy.FightSeconds;
        phase = scenario == 12 ? 1 : 2;
        if (scenario == 12) Launch(EnemyAttack.SW04); else Launch(EnemyAttack.SW02);
    }
    public static bool Tick(KarmaGame game, out int count)
    {
        count = 0;
        if (scenario >= ScenarioCount) { count = assertions; return true; }
        if (phase == 0) { Prepare(game); return false; }
        Check(enemy.FightSeconds - started < 22, "production coroutine finishes within watchdog");
        if (phase == 1)
        {
            if (Get<bool>(enemy, "running") || !Get<bool>(enemy, "swordGuard")) return false;
            Check(Get<Transform>(enemy, "swordGuardRoot") != null, "SW04 root is alive before SW02 starts");
            started = enemy.FightSeconds; Launch(EnemyAttack.SW02); phase = 2;
            return false;
        }
        if (phase == 3)
        {
            if (Time.frameCount < pauseFrame + 5) return false;
            Check(Mathf.Abs(enemy.FightSeconds - pauseClock) < 0.00001f, "pause freezes combat clock");
            Check(shots[0].attack != null && Vector2.Distance(shots[0].attack.transform.position, pausedPosition) < 0.00001f,
                "pause freezes the released crescent and its damage location");
            game.TogglePause();
            var root = Get<Transform>(enemy, "effectsRoot");
            Call(enemy, "InterruptPattern");
            Check(root.GetComponentsInChildren<KarmaBossAttack>().Length == 0
                && root.GetComponentsInChildren<KarmaAttackWarning>().Length == 0,
                "interrupt synchronously removes SW02 and SW04 warnings, damage and pictures");
            Check(!Get<bool>(enemy, "running") && !enemy.Armored, "interrupt clears concurrent ward and action state");
            Complete(game, true); return false;
        }
        ObserveWarnings(game);
        var playerBounds = player.GetComponent<BoxCollider2D>().bounds;
        maxFeet = Mathf.Max(maxFeet, playerBounds.min.y); maxHead = Mathf.Max(maxHead, playerBounds.max.y);
        if (Walking && sawLocked)
        {
            float rear = lockedOrigin.x - lockedFacing * dimensions.x / 2;
            bool safe = lockedFacing > 0 ? playerBounds.max.x < rear - 0.05f : playerBounds.min.x > rear + 0.05f;
            if (safe)
            {
                movement.Horizontal = 0;
                if (!crossed) { crossed = true; crossedAt = enemy.FightSeconds; }
            }
            // The route includes a measured 0.35-second reaction plus the
            // following real physics step, without teleporting through terrain.
            else if (!crossed && enemy.FightSeconds - lockedAt >= 0.35f) movement.Horizontal = -lockedFacing;
        }
        if (Jumping && sawLocked)
        {
            // Normal takeoff happens shortly before release. The second case
            // adds an active rising technique after 0.30 seconds of ascent.
            float elapsed = enemy.FightSeconds - lockedAt;
            if (!jumped && elapsed >= Mathf.Max(0, expectedLock - 0.30f))
            { movement.Jump = true; jumped = true; }
            if (scenario == 11 && jumped && !usedRise && elapsed >= expectedLock)
            {
                player.Essences.TryUse(Essence.RisingSlash); usedRise = true;
                Check(player.GetComponent<Rigidbody2D>().linearVelocity.y >= game.Config.Techniques.risingSpeed - 0.001f,
                    "real active technique applies its configured rising velocity");
            }
        }
        ObserveProjectiles(game);
        poses.Add(Get<KarmaCharacterAnimator>(enemy, "animator").CurrentPose ?? "");
        if (Get<object>(enemy, "state").ToString() == "Recover")
        { if (!sawRecovery) recoveryAt = enemy.FightSeconds; sawRecovery = true; }
        if (scenario == 12 && shots.Count > 0 && !pauseDone)
        {
            sawWard = Get<bool>(enemy, "swordGuard");
            Check(sawWard, "SW02 releases while SW04 is still actively maintained");
            pauseDone = true; pauseClock = enemy.FightSeconds; pauseFrame = Time.frameCount;
            pausedPosition = shots[0].attack.transform.position; game.TogglePause(); phase = 3;
            return false;
        }
        if (Get<bool>(enemy, "running")) return false;
        Set(enemy, "timer", float.MaxValue);
        Complete(game, false); return false;
    }
    static void ObserveWarnings(KarmaGame game)
    {
        var effects = Get<Transform>(enemy, "effectsRoot");
        foreach (var warning in effects.GetComponentsInChildren<KarmaAttackWarning>())
        {
            var sr = Get<SpriteRenderer>(warning, "visual");
            Color color = Get<Color>(warning, "baseColor");
            bool locked = color.r > 0.9f;
            Check(Mathf.Abs(sr.bounds.min.y - 0.05f) < 0.003f && Mathf.Abs(sr.bounds.size.y - dimensions.y) < 0.003f,
                "warning shows the real ground-adjacent damage height");
            Check(sr.bounds.min.x >= KarmaStage.ArenaLeft - 0.003f && sr.bounds.max.x <= KarmaStage.ArenaRight + 0.003f,
                "entire warning stays inside arena bounds");
            if (!locked)
            {
                sawTracking = true;
                if (scenario == 2 && !swappedTracking)
                {
                    Check(sr.bounds.center.x > enemy.Position.x, "tracking initially points right");
                    PlayerAt(new Vector2(enemy.Position.x - 8, KarmaSpace.PlayerHalfHeight));
                    swappedTracking = true;
                }
                else if (scenario == 2 && sr.bounds.center.x < enemy.Position.x) sawChangedTracking = true;
                continue;
            }
            if (!sawLocked)
            {
                sawLocked = true; lockedAt = enemy.FightSeconds; lockedFacing = enemy.Facing;
                lockedOrigin = (Vector2)Call(enemy, "AimedSlashOrigin", enemy.Position, lockedFacing);
                expectedLock = (float)Call(enemy, "AimedSlashLockTime", lockedOrigin, lockedFacing, dimensions);
                lockedWarning = sr.bounds;
                if (Walking)
                {
                    movement.StopAtTarget = true;
                    movement.TargetX = lockedOrigin.x - lockedFacing * (dimensions.x / 2
                        + player.GetComponent<BoxCollider2D>().bounds.extents.x + 0.15f);
                    movement.Speed = game.Config.moveSpeed;
                }
                Check(sawTracking, "blue tracking precedes the distinct orange direction lock");
                Check(expectedLock >= 0.9f, "direction lock supplies the unshortened minimum response time");
                if (scenario == 2)
                {
                    Check(sawChangedTracking && lockedFacing == -1, "tracking follows a side change before locking left");
                    PlayerAt(new Vector2(enemy.Position.x + 8, KarmaSpace.PlayerHalfHeight));
                }
            }
            else
            {
                Check(Vector3.Distance(sr.bounds.center, lockedWarning.center) < 0.0001f
                    && Vector3.Distance(sr.bounds.size, lockedWarning.size) < 0.0001f,
                    "locked full-height path never follows the player");
                Check(enemy.Facing == lockedFacing, "locked facing remains unchanged before release");
            }
        }
    }
    static void ObserveProjectiles(KarmaGame game)
    {
        foreach (var attack in Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<KarmaBossAttack>())
        {
            // SW04 counters have independent sprites and ownership.
            if (attack.Visual.sprite != KarmaArtwork.Sprite("ExtraWaveVFX", "SW02_AimedSlash")) continue;
            Shot shot = shots.Find(item => item.attack == attack);
            if (shot == null)
            {
                Check(sawLocked, "projectile cannot precede direction lock");
                shot = new Shot { attack = attack, last = attack.transform.position,
                    velocity = Get<Vector2>(attack, "velocity"), size = Get<Vector2>(attack, "size"),
                    rotation = attack.Visual.transform.localRotation, scale = attack.Visual.transform.localScale,
                    releasedAt = enemy.FightSeconds };
                shots.Add(shot);
                Shot captured = shot;
                attack.gameObject.AddComponent<KarmaAimedSlashTravelProbe>().Stopped = endpoint => {
                    captured.final = endpoint; captured.stoppedAt = enemy.FightSeconds; captured.stopped = true;
                };
                Check(shots.Count == 1, "normal, enraged and concurrent-ward casts release exactly one crescent");
                Check(Mathf.Abs(shot.velocity.y) < 0.00001f && Mathf.Sign(shot.velocity.x) == lockedFacing,
                    "elevated and grounded targets produce only the locked horizontal direction");
                Check(Vector2.Distance(shot.size, dimensions) < 0.0001f, "real damage footprint uses the large configured dimensions");
                Check(attack.Visual.flipX == (lockedFacing > 0) && !attack.Visual.flipY,
                    "source crescent cutting edge mirrors correctly for left and right");
                Check(Quaternion.Angle(attack.Visual.transform.localRotation, Quaternion.identity) < 0.0001f,
                    "released crescent stays upright for both directions");
                Check(attack.GetComponentsInChildren<SpriteRenderer>(true).Length == 1
                    && attack.GetComponentsInChildren<LineRenderer>(true).Length == 0
                    && attack.Visual.GetComponent<KarmaCombatVFX>() == null,
                    "one fixed crescent has no released outline, radiance or trailing copies");
                Check(Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<KarmaAttackWarning>().Length == 0,
                    "warning is removed before the real attack activates");
                Check(enemy.FightSeconds - lockedAt + 0.11f >= expectedLock, "projectile respects full escape lock duration");
                if (Walking) Check(crossed, "ordinary movement reaches the safe rear before release after a measured 0.35-second reaction");
                if (scenario == 2) Check(shot.velocity.x < 0 && player.Position.x > enemy.Position.x,
                    "crossing after lock cannot reverse or retarget the released slash");
            }
            Vector2 at = attack.transform.position;
            Check(Mathf.Abs(at.y - lockedOrigin.y) < 0.0001f && Mathf.Abs(at.y - shot.last.y) < 0.0001f,
                "flight has no vertical, diagonal or wave-like displacement");
            Check((at.x - shot.last.x) * lockedFacing >= -0.0001f, "flight never reverses horizontally");
            Check(Quaternion.Angle(attack.Visual.transform.localRotation, shot.rotation) < 0.0001f
                && Vector3.Distance(attack.Visual.transform.localScale, shot.scale) < 0.0001f,
                "picture never rotates, stretches or pulses during flight");
            Bounds core = CoreBounds(attack.Visual);
            Check(Vector3.Distance(core.center, new Vector3(at.x, at.y, 0)) < 0.0005f
                && Vector2.Distance(core.size, shot.size) < 0.0005f,
                "transformed opaque source core matches the actual damage box center, width and height");
            Check(core.min.x >= KarmaStage.ArenaLeft - 0.003f && core.max.x <= KarmaStage.ArenaRight + 0.003f,
                "full moving damage core remains within arena walls");
            Check(core.min.y <= 0.051f && core.min.y >= 0.049f, "large crescent remains adjacent to the ground");
            var target = player.GetComponent<BoxCollider2D>().bounds;
            bool overlapsX = core.min.x <= target.max.x && core.max.x >= target.min.x;
            if (overlapsX) minimumCrossingFeet = Mathf.Min(minimumCrossingFeet, target.min.y);
            shot.last = at;
        }
    }
    static Bounds CoreBounds(SpriteRenderer picture)
    {
        Rect pixels = KarmaWaveArtwork.CorePixels("SW02_AimedSlash");
        Bounds bounds = new Bounds(); bool first = true;
        foreach (float x in new[] { pixels.xMin, pixels.xMax })
            foreach (float y in new[] { pixels.yMin, pixels.yMax })
            {
                Vector2 local = (new Vector2(x, y) - picture.sprite.pivot) / picture.sprite.pixelsPerUnit;
                if (picture.flipX) local.x = -local.x;
                if (picture.flipY) local.y = -local.y;
                Vector3 point = picture.transform.TransformPoint(local);
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
            }
        return bounds;
    }
    static void Complete(KarmaGame game, bool interrupted)
    {
        Check(shots.Count == 1, "cast finishes with one large slash in every difficulty/defense state");
        if (!interrupted)
        {
            float recovery = Mathf.Max(0.38f, 1.1f * game.Config.BossTuning.recoveryMultiplier
                * (enemy.Enraged ? Mathf.Clamp(game.Config.BossTuning.enrageTempo, 0.65f, 1) : 1));
            Check(sawRecovery && enemy.FightSeconds - recoveryAt >= recovery - 0.12f,
                "existing scaled 1.1-second recovery remains after the projectile ends");
            foreach (var shot in shots)
            {
                Check(shot.stopped, "projectile reaches its boundary-limited lifetime");
                float edge = shot.final.x + lockedFacing * dimensions.x / 2;
                Check(Mathf.Abs(edge - (lockedFacing > 0 ? KarmaStage.ArenaRight : KarmaStage.ArenaLeft)) < 0.003f,
                    "leading damage edge stops exactly at the arena boundary");
            }
            Check(poses.Contains("Slash") || poses.Contains("Wave"), "boss connects the warning to a visible forceful swing");
        }
        if (Walking)
        {
            Check(crossed && player.Health == health && !usedRise && !jumped,
                "walking behind with no active rising skill avoids all real damage");
            Check(Mathf.Abs(movement.MaximumHorizontalSpeed - game.Config.moveSpeed) < 0.001f,
                "escape uses the actual configured player movement speed");
            Bounds bounds = player.GetComponent<BoxCollider2D>().bounds;
            Check(bounds.min.x >= KarmaStage.ArenaLeft - 0.02f && bounds.max.x <= KarmaStage.ArenaRight + 0.02f,
                "physical escape keeps the full player collider inside the arena, including wall cases");
        }
        float top = lockedOrigin.y + dimensions.y / 2;
        Check(top > game.Config.jumpSpeed * game.Config.jumpSpeed / (2 * Mathf.Abs(Physics2D.gravity.y * game.Config.gravityScale))
                + player.GetComponent<BoxCollider2D>().bounds.size.y + 0.5f,
            "real attack top exceeds the complete ordinary-jump player collider");
        var smaller = (Vector2)Call(enemy, "SwordSize", EnemyAttack.SW01);
        Check(dimensions.x > smaller.x * 1.5f && dimensions.y > smaller.y * 3,
            "large slash is substantially bigger than SW01 in both dimensions");
        if (scenario == 10)
        {
            Check(jumped && maxFeet > 4 && maxHead < top && player.Health < health,
                "actual ordinary jump reaches its apex but the real slash still damages the player");
        }
        if (scenario == 11)
        {
            Check(jumped && usedRise && maxFeet > top && !float.IsPositiveInfinity(minimumCrossingFeet)
                && minimumCrossingFeet > top && player.Health == health,
                "jump plus owned active rising technique physically clears the full core while it crosses without damage");
        }
        measurements.Add(new Measurement {
            scenario = Name, enraged = enemy.Enraged, walkedBehind = crossed, usedRisingSkill = usedRise,
            damaged = player.Health < health, wardAtRelease = sawWard, projectiles = shots.Count,
            facing = lockedFacing, lockSeconds = expectedLock, observedLockSeconds = shots[0].releasedAt - lockedAt,
            width = dimensions.x, height = dimensions.y, floor = lockedOrigin.y - dimensions.y / 2, top = top,
            playerSpeed = game.Config.moveSpeed, pierceDistance = game.Config.Techniques.pierceDistance,
            maxPlayerFeet = maxFeet, maxPlayerHead = maxHead,
            crossingFeet = float.IsPositiveInfinity(minimumCrossingFeet) ? -1 : minimumCrossingFeet,
            escapeSeconds = crossed ? crossedAt - lockedAt : -1,
            recoverySeconds = sawRecovery ? enemy.FightSeconds - recoveryAt : -1,
            patternSeconds = enemy.FightSeconds - started,
            flightSeconds = shots[0].stopped ? shots[0].stoppedAt - shots[0].releasedAt : -1,
            playerStart = initialPlayer, playerEnd = player.Position, origin = lockedOrigin, endpoint = shots[0].final
        });
        Debug.Log("PASS: SW02 live scenario " + scenario + " / " + Name);
        movement.Drive = false; UnityEngine.Object.Destroy(movement.gameObject);
        player.enabled = true;
        player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        Call(enemy, "InterruptPattern"); Set(enemy, "timer", float.MaxValue);
        scenario++; phase = 0;
        File.WriteAllText("SW02measurements.json", JsonUtility.ToJson(new Report {
            result = scenario == ScenarioCount ? "PASS" : "RUNNING", utc = DateTime.UtcNow.ToString("o"),
            assertions = assertions, cases = measurements.ToArray() }, true));
    }
}

// Unity performs the following physics integration itself. This driver calls
// the unchanged production movement code with deterministic input only.
public sealed class KarmaAimedSlashMovementDriver : MonoBehaviour
{
    public KarmaPlayer Player;
    public bool Drive, Jump;
    public float Horizontal;
    public bool StopAtTarget;
    public float TargetX, Speed;
    public float MaximumHorizontalSpeed;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    void FixedUpdate()
    {
        if (!Drive || Player == null) return;
        float input = Horizontal;
        if (StopAtTarget && input != 0)
        {
            float remaining = TargetX - Player.GetComponent<Rigidbody2D>().position.x;
            input = remaining * input <= 0 ? 0 : Mathf.Clamp(remaining / (Speed * Time.fixedDeltaTime), -1, 1);
        }
        typeof(KarmaPlayer).GetField("move", Private).SetValue(Player, input);
        if (Jump)
        {
            typeof(KarmaPlayer).GetField("jumpBufferedUntil", Private).SetValue(Player, Time.time + 0.12f);
            Jump = false;
        }
        typeof(KarmaPlayer).GetMethod("FixedUpdate", Private).Invoke(Player, null);
        MaximumHorizontalSpeed = Mathf.Max(MaximumHorizontalSpeed,
            Mathf.Abs(Player.GetComponent<Rigidbody2D>().linearVelocity.x));
    }
}
public sealed class KarmaAimedSlashTravelProbe : MonoBehaviour
{
    public Action<Vector2> Stopped;
    void OnDisable() { Stopped?.Invoke(transform.position); Stopped = null; }
}

// Separate from the full regression runner so SW02 failures can be reproduced
// without executing the long aerial-sword suites first.
[InitializeOnLoad]
public static class KarmaAimedSlashStandaloneRunner
{
    const string ActiveKey = "K4RMA.AimedSlashStandalone.Active";
    const string ExitPendingKey = "K4RMA.AimedSlashStandalone.ExitPending";
    const string ExitCodeKey = "K4RMA.AimedSlashStandalone.ExitCode";
    static KarmaGame game;
    static bool finishing;
    static double deadline;
    static string runtimeError;
    static KarmaAimedSlashStandaloneRunner()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += KeepEditorRunning;
        if (SessionState.GetBool(ExitPendingKey, false)) EditorApplication.delayCall += ExitEditor;
    }
    public static void Run()
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(ExitPendingKey, false);
        Application.runInBackground = true;
        PlayerSettings.runInBackground = true;
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Debug.Log("K4RMA SW02 STANDALONE START");
        EditorApplication.update -= KeepEditorRunning;
        EditorApplication.update += KeepEditorRunning;
        EditorApplication.delayCall += BeginPlay;
    }
    static void BeginPlay()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += BeginPlay; return; }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void KeepEditorRunning()
    {
        EditorApplication.isPaused = false;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying && game == null && Time.frameCount > 0)
            OnPlayModeChanged(PlayModeStateChange.EnteredPlayMode);
    }
    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (SessionState.GetBool(ExitPendingKey, false)) EditorApplication.delayCall += ExitEditor;
            else if (SessionState.GetBool(ActiveKey, false)) Finish(new Exception("SW02 Play Mode ended unexpectedly"), 0);
            return;
        }
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(ActiveKey, false) || game != null) return;
        try
        {
            Application.logMessageReceived -= CaptureRuntimeError;
            Application.logMessageReceived += CaptureRuntimeError;
            Time.captureDeltaTime = 0.05f;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            if (game == null) game = new GameObject("SW02 standalone game").AddComponent<KarmaGame>();
            game.Config.playerHealth = 1000000;
            game.StartRun();
            deadline = EditorApplication.timeSinceStartup + 300;
            new GameObject("SW02 standalone driver").AddComponent<KarmaAimedSlashStandaloneDriver>().Step = Step;
        }
        catch (Exception error) { Finish(error, 0); }
    }
    static void CaptureRuntimeError(string message, string stack, LogType type)
    {
        if (!finishing && (type == LogType.Error || type == LogType.Assert || type == LogType.Exception))
            runtimeError = message + "\n" + stack;
    }
    static void Step()
    {
        try
        {
            if (runtimeError != null) throw new Exception(runtimeError);
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("SW02 standalone exceeded 300-second deadline");
            if (KarmaAimedSlashChecks.Tick(game, out int count)) Finish(null, count);
        }
        catch (Exception error) { Finish(error, 0); }
    }
    static void Finish(Exception error, int count)
    {
        if (finishing) return;
        finishing = true;
        EditorApplication.update -= KeepEditorRunning;
        Application.logMessageReceived -= CaptureRuntimeError;
        SessionState.SetBool(ActiveKey, false);
        SessionState.SetBool(ExitPendingKey, true);
        SessionState.SetInt(ExitCodeKey, error == null ? 0 : 1);
        string result = error == null ? "PASS: 13 SW02 live scenarios, " + count + " Play Mode assertions" : "FAIL: " + error;
        File.WriteAllText("SW02-validation.txt", result + "\n");
        if (error == null) Debug.Log(result); else Debug.LogException(error);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        else EditorApplication.delayCall += ExitEditor;
    }
    static void ExitEditor()
    {
        if (!SessionState.GetBool(ExitPendingKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += ExitEditor; return; }
        int code = SessionState.GetInt(ExitCodeKey, 1);
        SessionState.SetBool(ExitPendingKey, false);
        EditorApplication.Exit(code);
    }
}
public sealed class KarmaAimedSlashStandaloneDriver : MonoBehaviour
{
    public Action Step;
    void Update() { Step?.Invoke(); }
}
#endif
