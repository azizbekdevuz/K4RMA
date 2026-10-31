#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using KarmaPrototype;
using KarmaPrototype.Editor;

// Diagnostic probes, not production fixes. Movement probes use real keyboard + dynamic Rigidbody.
public static class KarmaGuardRelationshipAudit
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static Keyboard keyboard;
    static string report;
    static int checks;
    static readonly List<string> notes = new List<string>();
    static readonly Essence[] Final = { Essence.Ward, Essence.Flame, Essence.Dash };
    static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, Private).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
    static void Note(string line) { notes.Add(line); File.WriteAllLines(report, notes); }
    static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    static void Place(Vector2 p) { game.Player.transform.position = p; game.Player.GetComponent<Rigidbody2D>().position = p; }
    static Task Seconds(float value) { float until = enemy.FightSeconds + value; return Until(() => enemy.FightSeconds >= until, value + 5); }
    static async Task Until(Func<bool> condition, float timeout = 5)
    {
        double until = EditorApplication.timeSinceStartup + timeout;
        while (!condition()) { if (EditorApplication.timeSinceStartup > until) throw new Exception("Timeout: " + enemy.Intent); await Task.Delay(5); }
    }
    static async Task Reset(Essence[] order, float playerX = 26, float bossX = 20, bool dynamic = true)
    {
        Keys(); game.StartRun();
        foreach (var e in order) game.Player.Essences.Sacrifice(e);
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, order.Length);
        Call(game, "LoadRoom", true); enemy = game.Enemies[0];
        enemy.transform.position = new Vector3(bossX, KarmaSpace.GuardianGroundY, 0);
        var body = game.Player.GetComponent<Rigidbody2D>();
        body.bodyType = dynamic ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
        Place(new Vector2(playerX, KarmaSpace.PlayerHalfHeight + 0.04f)); body.linearVelocity = Vector2.zero;
        Set(game.Player, "invulnerableUntil", 0f);
        Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue); Set(enemy, "nextShield", float.MaxValue);
        await Task.Delay(100);
    }
    static void Force(EnemyAttack pattern, int facing)
    {
        Call(enemy, "InterruptPattern"); Set(enemy, "selected", pattern); Set(enemy, "direction", (float)facing);
        Get<Transform>(enemy, "art").localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
        Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static string State() => Get<object>(enemy, "state").ToString();
    static KarmaBossAttack[] Attacks() => Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<KarmaBossAttack>();
    static void Capture(string file)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands")).First(t => t != null);
        EditorApplication.isPaused = true;
        try
        {
            var result = type.GetMethod("GameView", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { 1280, 720, null, null, false, 0, "screen" });
            File.WriteAllBytes("output/validation/relationship-" + file + ".png",
                Convert.FromBase64String((string)result.GetType().GetProperty("Base64").GetValue(result)));
        }
        finally { EditorApplication.isPaused = false; }
    }
    public static Task<string> Run() => RunProbes(0);
    public static Task<string> RunFocused() => RunProbes(1);
    public static Task<string> VerifyFixes() => RunProbes(2);
    public static Task<string> VerifyShardRange() => RunProbes(3);
    public static Task<string> VerifyTrialShieldReward() => RunProbes(4);
    static void Check(bool ok, string label)
    { checks++; if (!ok) throw new Exception(label); Note("PASS " + label); }
    static async Task<string> RunProbes(int mode)
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        KarmaGuardPractice.Stop(); EditorApplication.isPaused = false;
        game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        var original = game.Config; var config = UnityEngine.Object.Instantiate(original);
        config.playerHealth = 100000; Set(game, "config", config);
        keyboard = InputSystem.AddDevice<Keyboard>(); notes.Clear(); Directory.CreateDirectory("output/validation");
        report = mode == 4 ? "output/validation/gd05-success-shield-validation.txt"
            : mode == 3 ? "output/validation/gd03-range-speed-validation.txt"
            : mode == 2 ? "output/validation/guard-priority-fix-validation.txt"
            : "output/validation/guard-relationship-audit" + (mode == 1 ? "-focused" : "") + ".txt";
        checks = 0;
        try
        {
            int frame = Time.frameCount; await Task.Delay(120);
            if (Time.frameCount <= frame) throw new Exception("Player loop not advancing");
            Note("Live audit; player health increased only for observation, production speeds/timings/physics unchanged.");
            if (mode == 4) { await VerifyTrialShieldRewardRules(); Note("PASS: " + checks + " GD05 success / GD01 reward runtime assertions"); }
            else if (mode == 3) { await VerifyShardMotion(); Note("PASS: " + checks + " GD03 distance / speed / lifecycle runtime assertions"); }
            else if (mode == 2) { await VerifyHeavyReach(); await VerifyWallLayout(); Note("PASS: " + checks + " GD02 reach / GD04 wall-layout runtime assertions"); }
            else if (mode == 1) { await HeavyRange(); await DomeJumpFrames(); }
            else { await HeavyRange(); await ShieldMovement(); await BarrierEdges(); await DomeEntry(); await SharedTrialContact(); await TrialReward(); }
            Note("COMPLETE: all diagnostic probes finished; observations are not blanket correctness assertions.");
            return mode >= 2 ? "PASS: " + checks + " runtime assertions; " + report : string.Join("\n", notes);
        }
        catch (Exception e) { Note("AUDIT INTERRUPTED: " + e); throw; }
        finally
        {
            Keys(); EditorApplication.isPaused = false; InputSystem.RemoveDevice(keyboard);
            if (game.Paused) game.TogglePause(); game.ReturnToTitle(); Set(game, "config", original); UnityEngine.Object.Destroy(config);
        }
    }
    static async Task HeavyRange()
    {
        foreach (int side in new[] { -1, 1 })
        foreach (float distance in new[] { 6f, 9.5f, 10.4f })
        {
            await Reset(new[] { Essence.Ward }, 20 + side * distance, 20, false);
            Set(enemy, "cycleIndex", 1); Set(enemy, "timer", 0f);
            await Until(() => enemy.HeavyParryWindowOpen);
            float gap = Mathf.Abs(game.Player.Position.x - enemy.Position.x);
            float hp = game.Player.Health; int parries = game.Player.ParryCount;
            Keys(Key.K); await Task.Delay(40); Keys();
            await Until(() => enemy.Stunned || State() == "Recover");
            Note("GD02 natural selection side=" + side + " startGap=" + distance + " cueGap=" + gap.ToString("F3")
                + " parries=" + (game.Player.ParryCount - parries) + " playerDamage=" + (hp - game.Player.Health) + " result=" + State());
        }
    }
    static async Task ShieldMovement()
    {
        foreach (int side in new[] { -1, 1 })
        foreach (string policy in new[] { "stand", "early-cross", "late-retreat" })
        {
            await Reset(new[] { Essence.Ward, Essence.Flame }, 20 + side * 6);
            float hp = game.Player.Health; Force(EnemyAttack.GD03, side);
            if (policy == "early-cross") Keys(side > 0 ? Key.A : Key.D);
            if (policy == "late-retreat")
            {
                await Until(() => Get<Transform>(enemy, "effectsRoot").Cast<Transform>().Any(t => t.name.EndsWith("추적")));
                Keys(side > 0 ? Key.D : Key.A);
            }
            await Until(() => State() == "Recover", 8); Keys();
            Note("GD03 dynamic side=" + side + " policy=" + policy + " damage=" + (hp - game.Player.Health).ToString("F2")
                + " finalPlayer=" + game.Player.Position.ToString("F2") + " shieldRetained=" + enemy.AutoShieldActive);
            if (policy == "stand" && side == 1) Capture("gd03-recovery");
        }
        await Reset(new[] { Essence.Ward, Essence.Flame }); Force(EnemyAttack.GD03, 1);
        await Until(() => Get<Transform>(enemy, "effectsRoot").Find("GD03 큰 방패 투척") != null);
        await Seconds(0.16f); Capture("gd03-duplicate-held-shield");
        Note("GD03 launch body sprite=" + Get<Transform>(enemy, "art").GetComponentInChildren<SpriteRenderer>().sprite.name
            + "; thrown shield exists while inherited standing body remains.");
    }
    static async Task BarrierEdges()
    {
        foreach (int side in new[] { -1, 1 })
        {
            float bossX = side > 0 ? 48 : -8;
            await Reset(new[] { Essence.Ward, Essence.Dash }, bossX + side * 6, bossX);
            Force(EnemyAttack.GD04, side); await Seconds(0.12f);
            Note("GD04 near-wall side=" + side + " requestedFacing=" + side + " actualFacing=" + enemy.Facing
                + " playerSignedToActual=" + ((game.Player.Position.x - enemy.Position.x) * enemy.Facing).ToString("F2"));
            await Until(() => State() == "Attack"); await Seconds(0.7f);
            Capture(side > 0 ? "gd04-wall-turn-away-right" : "gd04-wall-turn-away-left");
            await Until(() => State() == "Recover", 6);
            Note("GD04 near-wall ending selected=" + Get<EnemyAttack>(enemy, "selected") + " no rear GD02 triggered=" + (Get<EnemyAttack>(enemy, "selected") == EnemyAttack.GD04));
        }
        await Reset(new[] { Essence.Ward, Essence.Dash }); Force(EnemyAttack.GD04, 1);
        await Until(() => State() == "Attack"); Keys(Key.A);
        await Until(() => enemy.HeavyParryWindowOpen, 5); Keys();
        float hp = game.Player.Health; Keys(Key.K); await Task.Delay(45); Keys();
        await Until(() => enemy.Stunned || State() == "Recover", 4);
        Note("GD04 real A crossing -> rear GD02: bossStunned=" + enemy.Stunned + " shieldBroken=" + !enemy.AutoShieldActive
            + " additionalDamageAfterCue=" + (hp - game.Player.Health));
    }
    static async Task DomeEntry()
    {
        foreach (int side in new[] { -1, 1 })
        {
            await Reset(Final, side > 0 ? 52 : -12);
            Vector2 before = game.Player.Position; Force(EnemyAttack.GD05, side);
            await Until(() => Get<bool>(game.Player, "confined"));
            Note("GD05 entry side=" + side + " before=" + before.ToString("F2") + " after=" + game.Player.Position.ToString("F2")
                + " instantaneousShift=" + Vector2.Distance(before, game.Player.Position).ToString("F2"));
        }
        foreach (bool beforeRoof in new[] { true, false })
        foreach (int side in new[] { -1, 1 })
        {
            await Reset(Final, side > 0 ? 36.8f : 3.2f);
            Force(EnemyAttack.GD05, side);
            await Until(() => Get<bool>(game.Player, "confined"));
            if (!beforeRoof) await Until(() => UnityEngine.Object.FindAnyObjectByType<KarmaGuardDome>() != null);
            Keys(side > 0 ? Key.D : Key.A, Key.Space); await Task.Delay(45); Keys(side > 0 ? Key.D : Key.A);
            await Until(() => UnityEngine.Object.FindAnyObjectByType<KarmaGuardDome>() != null);
            await Seconds(1);
            var dome = UnityEngine.Object.FindAnyObjectByType<KarmaGuardDome>();
            Vector2 p = game.Player.Position;
            float dx = p.x - 20; float roof = Mathf.Sqrt(Mathf.Max(0, dome.Radius * dome.Radius - dx * dx));
            bool outside = p.y - KarmaSpace.PlayerHalfHeight > roof - 0.1f;
            bool grounded = (bool)Call(game.Player, "Grounded");
            Note("GD05 dynamic roof side=" + side + " jumpBeforeRoof=" + beforeRoof + " position=" + p.ToString("F2")
                + " localRoof=" + roof.ToString("F2") + " feet=" + (p.y - KarmaSpace.PlayerHalfHeight).ToString("F2")
                + " aboveRoof=" + outside + " grounded=" + grounded);
            Capture("gd05-roof-" + side + "-" + beforeRoof);
            if (outside)
            {
                float hp = game.Player.Health;
                Keys(); await Task.Delay(40); Keys(Key.Space); await Task.Delay(45); Keys(); await Seconds(0.3f);
                Note("GD05 outside re-jump position=" + game.Player.Position.ToString("F2") + " explosionOverlap="
                    + dome.Overlaps(game.Player.Position, new Vector2(KarmaSpace.PlayerHalfWidth, KarmaSpace.PlayerHalfHeight)));
                Capture("gd05-outside-rejump-" + side);
                await Until(() => State() == "Recover", 35);
                Note("GD05 roof route ending=" + enemy.Intent + " damageSinceRoof=" + (hp - game.Player.Health)
                    + " player=" + game.Player.Position.ToString("F2"));
            }
        }
    }
    static async Task SharedTrialContact()
    {
        await Reset(Final, 20, 20, false); Call(enemy, "InterruptPattern");
        Set(enemy, "selected", EnemyAttack.GD05); Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
        var contact = new KarmaAttackContact();
        enemy.StartCoroutine((IEnumerator)Call(enemy, "TrialPincer", contact));
        await Until(() => Attacks().Length == 4);
        await Until(() => Attacks().Any(a => Mathf.Abs(a.transform.position.x - 20) < 4));
        int parries = game.Player.ParryCount; float hp = game.Player.Health;
        Keys(Key.K); await Task.Delay(35); Keys();
        await Until(() => contact.Consumed);
        float first = enemy.FightSeconds; int alive = Attacks().Length;
        await Seconds(0.5f);
        Note("GD05 pincer single counter: parries=" + (game.Player.ParryCount - parries) + " damage=" + (hp - game.Player.Health)
            + " liveAttacksAfterFirst=" + alive + " invulnerabilityRemaining=" + (Get<float>(game.Player, "invulnerableUntil") - Time.time).ToString("F3")
            + " sharedConsumed=" + contact.Consumed + " secondsAfterFirst=" + (enemy.FightSeconds - first).ToString("F3"));
    }
    static async Task TrialReward()
    {
        await Reset(Final, 23.4f);
        Set(game.Player, "invulnerableUntil", Time.time + 100); // Isolate reward behavior; not a dodge claim.
        Force(EnemyAttack.GD05, -1);
        await Until(() => State() == "Recover" && enemy.Intent.Contains("시험 성공"), 35);
        float hp = enemy.Health; enemy.TakeDamage(game.Config.swordDamage, game.Player.Position);
        Note("GD05 successful reward: GD01=" + enemy.AutoShieldActive + " bossInvulnerable=" + enemy.Invulnerable
            + " nominalSword=" + game.Config.swordDamage + " actualDamage=" + (hp - enemy.Health).ToString("F2")
            + " rewardSeconds=" + game.Config.BossTuning.trialStunSeconds);
    }
    static async Task DomeJumpFrames()
    {
        foreach (int side in new[] { -1, 1 })
        foreach (float delay in new[] { 0f, 0.12f, 0.25f })
        {
            await Reset(Final, side > 0 ? 36.8f : 3.2f); Force(EnemyAttack.GD05, side);
            await Until(() => Get<bool>(game.Player, "confined"));
            if (delay > 0) await Seconds(delay);
            Keys(side > 0 ? Key.D : Key.A, Key.Space); await Task.Delay(40); Keys(side > 0 ? Key.D : Key.A);
            float began = enemy.FightSeconds, maxY = game.Player.Position.y, maxOutside = -100;
            bool rose = false, captured = false; int samples = 0;
            while (enemy.FightSeconds < began + 1.2f)
            {
                var p = game.Player.Position; var body = game.Player.GetComponent<Rigidbody2D>();
                maxY = Mathf.Max(maxY, p.y); rose |= body.linearVelocity.y > 5;
                var dome = UnityEngine.Object.FindAnyObjectByType<KarmaGuardDome>();
                if (dome != null)
                {
                    float dx = p.x - 20, roof = Mathf.Sqrt(Mathf.Max(0, dome.Radius * dome.Radius - dx * dx));
                    float outside = p.y - KarmaSpace.PlayerHalfHeight - roof;
                    maxOutside = Mathf.Max(maxOutside, outside);
                    if (outside > 0.1f && !captured) { Capture("roof-transient-" + side + "-" + delay); captured = true; }
                }
                samples++; await Task.Delay(5);
            }
            Keys();
            Note("GD05 frame probe side=" + side + " entryJumpDelay=" + delay + " samples=" + samples
                + " jumpRegistered=" + rose + " maxY=" + maxY.ToString("F2") + " maxFeetAboveRoof=" + maxOutside.ToString("F2"));
        }
    }
    static async Task VerifyHeavyReach()
    {
        Essence[][] orders = { new[] { Essence.Ward }, new[] { Essence.Ward, Essence.Flame },
            new[] { Essence.Ward, Essence.Dash }, Final, new[] { Essence.Ward, Essence.Dash, Essence.Flame } };
        for (int form = 0; form < orders.Length; form++)
        foreach (int side in new[] { -1, 1 })
        foreach (float distance in new[] { 8f, 8.1f, 9.54f })
        {
            await Reset(orders[form], 20 + side * distance, 20, false);
            enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
            Set(enemy, "cycleIndex", 1);
            if (side > 0) typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.4f);
            Call(enemy, "BeginAttack");
            string label = "form=" + form + " side=" + side + " gap=" + distance;
            bool selected = Get<EnemyAttack>(enemy, "selected") == EnemyAttack.GD02;
            Check(selected == (distance <= 8), label + " selects GD02 only inside its actual reach");
            if (!selected) continue;
            await Until(() => enemy.HeavyParryWindowOpen);
            float hp = game.Player.Health; int parries = game.Player.ParryCount;
            Keys(Key.K); await Task.Delay(80); Keys();
            Note(label + " input: queued=" + Get<bool>(enemy, "heavyParryQueued") + " actualGap="
                + Mathf.Abs(game.Player.Position.x - enemy.Position.x).ToString("F3") + " facing=" + enemy.Facing
                + " player=" + game.Player.Position + " boss=" + enemy.Position + " invuln=" + (Get<float>(game.Player, "invulnerableUntil") - Time.time));
            await Until(() => enemy.Stunned || State() == "Recover");
            Check(game.Player.ParryCount == parries + 1 && game.Player.Health == hp && !enemy.AutoShieldActive,
                label + " boundary-distance real K parry breaks shield without damage");
        }
        // Reproduce the original natural-selection case; capture the gap at selection, not at the later tell.
        foreach (int side in new[] { -1, 1 })
        {
            await Reset(new[] { Essence.Ward }, 20 + side * 10.4f, 20, false);
            Set(enemy, "cycleIndex", 1); Set(enemy, "timer", 0f);
            await Until(() => Get<bool>(enemy, "running"));
            float gap = Mathf.Abs(game.Player.Position.x - enemy.Position.x);
            Check(Get<EnemyAttack>(enemy, "selected") != EnemyAttack.GD02 || gap <= 8.001f,
                "natural selector no longer starts a whiffing GD02 at the old gap; side=" + side + " actual=" + gap.ToString("F3"));
        }
    }
    static async Task VerifyWallLayout()
    {
        Essence[][] orders = { new[] { Essence.Ward, Essence.Dash }, Final, new[] { Essence.Ward, Essence.Dash, Essence.Flame } };
        for (int form = 0; form < orders.Length; form++)
        foreach (int side in new[] { -1, 1 })
        foreach (float room in new[] { 35f, 7f, 5.2f })
        {
            float bossX = side > 0 ? KarmaStage.ArenaRight - room : KarmaStage.ArenaLeft + room;
            float playerX = bossX + side * Mathf.Min(6, room - 0.9f);
            string label = "form=" + form + " side=" + side + " wallRoom=" + room;
            await Reset(orders[form], playerX, bossX);
            Force(EnemyAttack.GD04, side);
            var root = Get<Transform>(enemy, "effectsRoot");
            int count = root.Cast<Transform>().Count(t => t.name.StartsWith("GD04 계단 상승 예고"));
            Check(enemy.Facing == side && count >= 2 && count <= game.Config.BossTuning.barrierSteps,
                label + " faces the player and creates several steps");
            if (room == 35) Check(count == 6, label + " central layout retains all six normal steps");
            await Until(() => root.Cast<Transform>().Count(t => t.name.StartsWith("완성 방벽")) == count, 5);
            var bounds = root.Cast<Transform>().Where(t => t.name.StartsWith("완성 방벽"))
                .Select(t => t.GetComponent<BoxCollider2D>().bounds).OrderBy(b => (b.center.x - bossX) * side).ToArray();
            Check(bounds.All(b => b.min.x >= KarmaStage.ArenaLeft + 0.29f && b.max.x <= KarmaStage.ArenaRight - 0.29f
                && (b.center.x - bossX) * side > 0 && b.size.x >= KarmaSpace.PlayerSize.x + 0.09f),
                label + " every step stays inside the wall, ahead of the boss and wide enough for the player");
            Check(bounds.Any(b => Mathf.Abs(playerX - b.center.x) <= b.extents.x + KarmaSpace.PlayerHalfWidth),
                label + " the rising ground actually reaches the player's original lane");
            for (int i = 1; i < bounds.Length; i++)
                Check(bounds[i].max.y > bounds[i - 1].max.y && Mathf.Abs(bounds[i].center.x - bounds[i - 1].center.x)
                    < bounds[i].extents.x + bounds[i - 1].extents.x, label + " steps rise progressively without gaps");
            Check(Get<EnemyAttack>(enemy, "selected") == EnemyAttack.GD04 && enemy.AutoShieldActive,
                label + " standing in front does not spuriously trigger the rear counter");
            if (form == 1 && room == 7 && side > 0) Capture("fixed-gd04-wall-layout");

            await Reset(orders[form], playerX, bossX);
            root = Get<Transform>(enemy, "effectsRoot");
            if (side > 0) typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.4f);
            Force(EnemyAttack.GD04, side); await Until(() => State() == "Attack");
            Keys(side > 0 ? Key.A : Key.D);
            await Until(() => enemy.HeavyParryWindowOpen); Keys();
            Check(enemy.Facing == -side && Get<EnemyAttack>(enemy, "selected") == EnemyAttack.GD02,
                label + " actual retreat across the boss triggers the opposite-facing GD02");
            if (form == 1 && room == 7 && side > 0)
            { await Seconds(0.05f); Capture("fixed-gd04-wall-rear-cue"); }
            float hp = game.Player.Health; int parries = game.Player.ParryCount;
            Keys(Key.K); await Task.Delay(40); Keys(); await Until(() => enemy.Stunned);
            Check(game.Player.ParryCount == parries + 1 && game.Player.Health == hp && !enemy.AutoShieldActive,
                label + " real K parry breaks GD01 without additional damage");
            await Seconds(0.05f);
            Check(root == null || !root.Cast<Transform>().Any(t => t.gameObject.activeInHierarchy
                && (t.name.StartsWith("GD04") || t.name.StartsWith("완성 방벽"))),
                label + " parry clears all rising and completed terrain");
        }
    }
    static SpriteRenderer[] Shards() => Get<Transform>(enemy, "effectsRoot").GetComponentsInChildren<SpriteRenderer>()
        .Where(p => p.name.StartsWith("GD03 방패 ")).ToArray();
    static async Task VerifyShardMotion()
    {
        Check(Mathf.Approximately(game.Config.BossTuning.fragmentChaseSpeed, 30), "serialized chase speed is 30 (previously 20)");
        Check(Mathf.Approximately(game.Config.BossTuning.fragmentReturnSeconds, 0.4f), "serialized return duration is 0.4 (previously 0.6)");
        Essence[][] orders = { new[] { Essence.Ward, Essence.Flame }, Final, new[] { Essence.Ward, Essence.Dash, Essence.Flame } };
        for (int form = 0; form < orders.Length; form++)
        foreach (int side in new[] { -1, 1 })
        foreach (bool moving in new[] { false, true })
        {
            await Reset(orders[form], 20 + side * 20, 20, moving);
            Call(enemy, "InterruptPattern");
            Set(enemy, "selected", EnemyAttack.GD03); Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
            Set(enemy, "state", Enum.Parse(Get<object>(enemy, "state").GetType(), "Attack"));
            var piece = KarmaVisuals.Box(Get<Transform>(enemy, "effectsRoot"), "GD03 방패 1 준비",
                new Vector2(20 - side * 20, 3), Vector2.one, Color.white, 27);
            enemy.StartCoroutine((IEnumerator)Call(enemy, "ShieldFragment", piece, 1));
            if (moving) Keys(side > 0 ? Key.D : Key.A);
            float hp = game.Player.Health;
            await Until(() => piece.name.EndsWith("추적")); float began = enemy.FightSeconds;
            Vector2 start = piece.transform.position;
            await Seconds(0.1f);
            float actualSpeed = Vector2.Distance(start, piece.transform.position) / (enemy.FightSeconds - began);
            string label = "form=" + form + " side=" + side + " moving=" + moving;
            Check(actualSpeed > 28 && actualSpeed < 32, label + " observed chase speed=" + actualSpeed.ToString("F2"));
            if (form == 0 && side == -1 && !moving)
            {
                Vector3 frozen = piece.transform.position; float clock = enemy.FightSeconds;
                game.TogglePause(); await Task.Delay(150);
                Check(piece.transform.position == frozen && enemy.FightSeconds == clock, "long-distance chase pauses with the game");
                game.TogglePause();
            }
            await Until(() => game.Player.Health < hp, 5);
            Check(enemy.FightSeconds - began > 0.65f && game.Player.Stunned,
                label + " reaches the distant player after the old chase timeout and retains hot stun");
            Keys(); await Until(() => piece.name.EndsWith("정지"), 2);
            Vector2 stopped = piece.transform.position, home = enemy.Position;
            Check(piece.sprite.name == "Cooled" && !piece.GetComponent<TrailRenderer>().emitting,
                label + " reached shard cools and stops before returning");
            await Until(() => piece == null || piece.name.EndsWith("복귀"));
            Check(piece != null, label + " return phase exists"); float returnAt = enemy.FightSeconds;
            bool fixedLine = true;
            while (piece != null)
            {
                Vector2 line = home - stopped, offset = (Vector2)piece.transform.position - stopped;
                fixedLine &= Mathf.Abs(line.x * offset.y - line.y * offset.x) / Mathf.Max(0.01f, line.magnitude) < 0.05f;
                await Task.Delay(5);
            }
            Check(fixedLine && enemy.FightSeconds - returnAt > 0.32f && enemy.FightSeconds - returnAt < 0.46f,
                label + " fixed return completes faster in " + (enemy.FightSeconds - returnAt).ToString("F3") + " seconds");
            Check(enemy.AutoShieldActive, label + " GD01 survives chase and return");
        }
        // An early shard can still be chasing after the final shard has returned.
        await Reset(Final, 50, -8, false); game.Config.BossTuning.patternTimeout = 5;
        Set(game.Player, "invulnerableUntil", Time.time + 100); Force(EnemyAttack.GD03, 1);
        await Until(() => Shards().Length == 4 && Shards().All(p => p.name.EndsWith("준비")), 4);
        Place(new Vector2(-12, 2));
        await Until(() => Shards().Any(p => p.name == "GD03 방패 1 추적"));
        var first = Shards().First(p => p.name == "GD03 방패 1 추적");
        await Until(() => Shards().Any(p => p.name == "GD03 방패 4 추적"), 3);
        var last = Shards().First(p => p.name == "GD03 방패 4 추적"); Place(new Vector2(50, 2));
        await Until(() => last == null, 4);
        Check(first != null && first.gameObject.activeInHierarchy && State() != "Recover",
            "last shard finishing does not delete an earlier long-distance chase");
        await Until(() => State() == "Recover", 5);
        Check(Shards().Length == 0 && enemy.AutoShieldActive, "all independently timed shards finish before recovery and preserve GD01");

        // Every ownership exit must cancel an extended chase.
        foreach (bool death in new[] { false, true })
        {
            await Reset(Final, 50, -8); Force(EnemyAttack.GD03, 1);
            Keys(Key.A); await Until(() => Shards().Any(p => p.name.EndsWith("추적")), 4); Keys();
            if (death) game.PlayerDied(); else Call(enemy, "InterruptPattern");
            await Task.Delay(100);
            Check(Shards().Length == 0, death ? "death removes every extended chase" : "interruption removes every extended chase");
        }
    }
    static async Task VerifyTrialShieldRewardRules()
    {
        Essence[][] orders = { Final, new[] { Essence.Ward, Essence.Dash, Essence.Flame } };
        await Reset(new[] { Essence.Ward });
        Check(!enemy.DefenseStatus.Contains("GD05"), "earlier Ward stage keeps its parry-only shield hint");
        for (int form = 0; form < orders.Length; form++)
        foreach (bool success in new[] { true, false })
        {
            game.Config.BossTuning.trialAttackCount = form == 0 && success ? 7 : 3;
            await Reset(orders[form], 23.4f);
            if (form == 1) typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.4f);
            string label = "finalOrder=" + form + " success=" + success;
            Check(enemy.DefenseStatus.Contains("GD05"), label + " final-stage hint lists the GD05 shield-break route");
            Set(game.Player, "invulnerableUntil", Time.time + 100); // Isolate the end-of-trial rule, not player dodge skill.
            Force(EnemyAttack.GD05, form == 0 ? -1 : 1); await Until(() => enemy.TrialActive, 4);
            if (!success)
            {
                var trial = Get<KarmaTrialState>(enemy, "trial");
                for (int i = 0; i <= trial.Allowed; i++) trial.Hit();
            }
            bool protectedDuringTrial = true;
            await Until(() =>
            {
                if (enemy.TrialActive) protectedDuringTrial &= enemy.AutoShieldActive && enemy.Invulnerable;
                return State() == "Recover";
            }, 35);
            Check(protectedDuringTrial && !enemy.Invulnerable, label + " trial keeps protection and releases full immunity for recovery");
            Check(enemy.AutoShieldActive == !success, label + " only success removes GD01");
            float hp = enemy.Health; enemy.TakeDamage(60, game.Player.Position);
            Check(Mathf.Abs(hp - enemy.Health - (success ? 60 : 18)) < 0.02f,
                label + " reward damage=" + (hp - enemy.Health).ToString("F2"));
            if (!success) continue;
            Check(enemy.Intent.Contains("GD01 방어막 해제") && !Get<SpriteRenderer>(enemy, "shieldHalo").enabled,
                label + " success message and actual shield halo agree");
            Check(Get<float>(enemy, "nextShield") > enemy.FightSeconds + 7.4f,
                label + " success starts a fresh eight-second regeneration delay");
            float rewardAt = enemy.FightSeconds; bool open = true;
            await Until(() =>
            {
                if (State() == "Recover") open &= !enemy.AutoShieldActive && !enemy.Invulnerable
                    && !Get<SpriteRenderer>(enemy, "shieldHalo").enabled;
                return !Get<bool>(enemy, "running");
            }, 7);
            Check(open && enemy.FightSeconds - rewardAt >= game.Config.BossTuning.trialStunSeconds - 0.08f,
                label + " all four reward seconds remain unshielded, including enrage");
            Set(enemy, "timer", float.MaxValue);
            await Until(() => Get<float>(enemy, "shieldWarningUntil") > 0, 6);
            Check(!enemy.AutoShieldActive, label + " regeneration gives its warning before shielding");
            await Until(() => enemy.AutoShieldActive, 2);
            Check(enemy.AutoShieldActive, label + " GD01 resumes its ordinary delayed regeneration");
        }
        game.Config.BossTuning.trialAttackCount = 3;
        await Reset(Final); Call(enemy, "BreakAutoShield");
        Set(game.Player, "invulnerableUntil", Time.time + 100); Force(EnemyAttack.GD05, -1);
        await Until(() => State() == "Recover" && enemy.Intent.Contains("시험 성공"), 25);
        Check(!enemy.AutoShieldActive && Get<float>(enemy, "nextShield") > enemy.FightSeconds + 7.4f,
            "success with an already-broken shield refreshes the full reward delay");
    }
}
#endif
