#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;
using UnityEngine.InputSystem;

[InitializeOnLoad]
public static class KarmaSwordFinalPolishChecks
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Key = "K4RMA.FinalSwordPolish";
    static KarmaGame game; static bool started, incomingDamage = true; static string output, error, id;
    static readonly List<string> results = new List<string>();
    static KarmaEnemy Enemy => (KarmaEnemy)typeof(KarmaSwordPlayChecks).GetField("enemy", F).GetValue(null);
    static object Behavior => Get(Enemy, "behavior");
    static object Counter => Get(Behavior, "counter");
    static Transform Root => (Transform)Get(Enemy, "effectsRoot");
    static object Get(object o, string name) { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, F); if (f != null) return f.GetValue(o); } throw new MissingFieldException(name); }
    static void Set(object o, string name, object v) { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, F); if (f != null) { f.SetValue(o, v); return; } } throw new MissingFieldException(name); }
    static async Task Invoke(Type t, string n, params object[] a) { if (t.GetMethod(n, F).Invoke(null, a) is Task task) await task; }
    static void Check(bool ok, string m) { if (!ok) throw new Exception(m); }
    static void Log(string m) { results.Add("PASS: " + m); Debug.Log(m); }
    static void Reset() => typeof(KarmaSwordPlayChecks).GetMethod("Reset", F).Invoke(null, new object[] { Essence.SwordWave });
    static void Place(float x) => typeof(KarmaSwordPlayChecks).GetMethod("Place", F).Invoke(null, new object[] { x });
    static void Force(EnemyAttack p)
    {
        typeof(KarmaSwordPlayChecks).GetMethod("Force", F).Invoke(null, new object[] { p });
        // Match BeginAttack's art-facing assignment in this forced fixture.
        ((Transform)Get(Enemy, "art")).localScale = new Vector3(Enemy.Facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
    }
    static async Task Until(Func<bool> done) { double end = EditorApplication.timeSinceStartup + 180; while (!done()) { Check(EditorApplication.timeSinceStartup < end, "Play Mode timed out"); await Task.Delay(5); } }
    static Task Seconds(float s) { float until = Enemy.FightSeconds + s; return Until(() => Enemy.FightSeconds >= until); }
    static KarmaSwordFinalPolishChecks() { EditorApplication.update += Pump; }
    static void Pump() { if (!SessionState.GetBool(Key, false)) return; EditorApplication.QueuePlayerLoopUpdate(); if (EditorApplication.isPlaying) EditorApplication.isPaused = false; if (!started && EditorApplication.isPlaying && Time.frameCount > 2) { started = true; Run(); } }
    public static void RunBatch()
    {
        SessionState.SetBool(Key + ".PresentationOnly", false);
        SessionState.SetBool(Key + ".LeftOnly", false);
        SessionState.SetBool(Key + ".ScarOnly", false);
        SessionState.SetBool(Key + ".MovementOnly", false);
        SessionState.SetBool(Key + ".ParryStunOnly", false);
        SessionState.SetBool(Key + ".SkipCounterChecks", false);
        SessionState.SetBool(Key + ".GroundedArtAndParry", false);
        typeof(KarmaSwordPlayChecks).GetMethod("CompilePlayerScripts", F).Invoke(null, null);
        SessionState.SetBool(Key, true); SessionState.SetString(Key + ".Id", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity"); EditorApplication.EnterPlaymode();
    }
    public static void RunPresentationOnly() { RunBatch(); SessionState.SetBool(Key + ".PresentationOnly", true); }
    public static void RunLeftPresentation() { RunPresentationOnly(); SessionState.SetBool(Key + ".LeftOnly", true); }
    public static void RunScarPresentation() { RunPresentationOnly(); SessionState.SetBool(Key + ".ScarOnly", true); }
    public static void RunMovementOnly() { RunBatch(); SessionState.SetBool(Key + ".MovementOnly", true); }
    public static void RunParryStunOnly() { RunBatch(); SessionState.SetBool(Key + ".ParryStunOnly", true); }
    public static void RunStunStateOnly() { RunParryStunOnly(); SessionState.SetBool(Key + ".SkipCounterChecks", true); }
    public static void RunGroundedArtAndParry() { RunBatch(); SessionState.SetBool(Key + ".GroundedArtAndParry", true); }
    static void Shot(string name)
    {
        var camera = Camera.main; Check(camera != null, "real game camera");
        var rt = new RenderTexture(1600, 700, 24); var previous = camera.targetTexture; var active = RenderTexture.active; var rect = camera.rect; var aspect = camera.aspect;
        var image = new Texture2D(1600, 700, TextureFormat.RGBA32, false);
        try
        {
            camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1); camera.pixelRect = new Rect(0, 0, 1600, 700); camera.aspect = 1600f / 700;
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1600, 700), 0, 0); image.Apply();
            File.WriteAllBytes(output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; camera.rect = rect; camera.aspect = aspect; RenderTexture.active = active; UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(rt); }
    }
    [Serializable] sealed class PatternMetrics
    {
        public string pattern; public float side, seconds; public bool enraged; public int shots, rounds, peakCosmetics, peakCoreRenderers;
        public List<float> releaseTimes = new List<float>(), heights = new List<float>();
        public List<string> timeline = new List<string>();
    }
    static void VerifyArt(KarmaBossAttack shot, EnemyAttack pattern)
    {
        var size = (Vector2)Get(shot, "size"); var velocity = (Vector2)Get(shot, "velocity");
        bool rangedBasic = pattern == EnemyAttack.SWBasic && velocity.sqrMagnitude > 0.01f;
        if (rangedBasic || pattern == EnemyAttack.SW01 || pattern == EnemyAttack.SW02)
        {
            var visual = shot.Visual;
            string sequence = pattern == EnemyAttack.SW02 ? "SW02Flight" : "SW01Flight";
            bool validFrame = false;
            for (int i = 0; i < 5; i++)
                validFrame |= visual.sprite == KarmaArtwork.Sprite("SwordBossVFX", "SwordEnergy_" + sequence + "_" + i);
            Check(validFrame, "released shot uses a registered frame from its electric flight sequence");
            Vector2 footprint = rangedBasic
                ? (Vector2)Behavior.GetType().GetMethod("SwordSize", F).Invoke(Behavior, new object[] { EnemyAttack.SW01 }) * 0.5f
                : size;
            Vector2 coreFraction = pattern == EnemyAttack.SW02 ? new Vector2(0.83f, 0.86f) : new Vector2(0.65f, 0.81f);
            var actual = Vector2.Scale(visual.sprite.bounds.size, visual.transform.localScale);
            Check(Vector2.Distance(Vector2.Scale(actual, coreFraction), footprint) < 0.002f,
                "animated frame preserves the authored footprint and unchanged damage rectangle");
            Check(visual.flipX == (velocity.x < 0) && !visual.flipY, "electric flight keeps its committed facing");
            return;
        }
        string pose = KarmaWaveArtwork.Pose(pattern);
        var core = KarmaWaveArtwork.CorePixels(pose); var sr = shot.Visual;
        Check(sr.sprite == KarmaWaveArtwork.Sprite(pose), "released shot uses expected final sprite");
        Check(Mathf.Abs(sr.transform.localScale.x * core.width / sr.sprite.pixelsPerUnit - size.x) < 0.002f,
            "cutting width maps to unchanged damage rectangle");
        if (pattern != EnemyAttack.SW03)
            Check(Mathf.Abs(sr.transform.localScale.y * core.height / sr.sprite.pixelsPerUnit - size.y) < 0.002f,
                "cutting height maps to unchanged damage rectangle");
        if (Mathf.Abs(velocity.y) < 0.0001f && velocity.sqrMagnitude > 0.01f && pattern != EnemyAttack.SW04)
            Check(sr.flipX == (velocity.x < 0) && !sr.flipY, "horizontal crescent mirrors without vertical inversion");
        if (pattern == EnemyAttack.SWBasic && velocity.sqrMagnitude < 0.01f)
            Check(sr.flipX == (Enemy.Facing < 0) && !sr.flipY, "stationary melee arc faces its committed attack direction");

    }
    static async Task Pattern(EnemyAttack pattern, float side, bool enraged = false, bool melee = false)
    {
        if(pattern==EnemyAttack.SW03)
        {
            await KarmaSwordSkillRefreshChecks.CheckSW03Redesign(enraged);
            return;
        }
        Reset(); Place(20 + side * (melee ? 5 : pattern == EnemyAttack.SWBasic ? 9 : 15));
        if (enraged) typeof(KarmaEnemy).GetProperty("Health").SetValue(Enemy, Enemy.MaxHealth * 0.2f);
        await Seconds(0.1f); Force(pattern);
        var m = new PatternMetrics { pattern = pattern.ToString() + (melee ? "-melee" : ""), side = side, enraged = enraged };
        string prefix = m.pattern + "-" + side + "-" + enraged; float start = Enemy.FightSeconds, nextDamage = start + 0.08f;
        var seen = new HashSet<KarmaBossAttack>(); bool rising = false, warned = false, recovery = false, both = false; string lastState = "";
        int serial = 0; var pendingFrames = new List<int>(); var probe = new GameObject("attack continuity + real render observer").AddComponent<KarmaSwordDashFrameProbe>();
        probe.Before = () => { if (incomingDamage && Enemy.FightSeconds >= nextDamage) { Enemy.TakeDamage(0.01f); nextDamage += 0.075f; } };
        probe.After = () =>
        {
            try
            {
                var attacks = Root.GetComponentsInChildren<KarmaBossAttack>();
                var animator = (KarmaCharacterAnimator)Get(Enemy, "animator"); Check(animator.CurrentPose != "Hurt", "ordinary hit preserves committed attack pose");
                string state = Get(Enemy, "state") + "/" + animator.CurrentPose;
                if (state != lastState) { m.timeline.Add((Enemy.FightSeconds - start).ToString("F4") + " " + state); lastState = state; }
                var pool = game.World.GetComponent<KarmaSwordVfxPool>(); if (pool != null) m.peakCosmetics = Mathf.Max(m.peakCosmetics, pool.ActiveCount);
                m.peakCoreRenderers = Mathf.Max(m.peakCoreRenderers, attacks.Length);
                if (!warned && Get(Enemy, "state").ToString() == "Telegraph" && Enemy.FightSeconds - start > 0.15f)
                { warned = true; Shot(prefix + "-prepare"); }
                if (attacks.Length > 0 && !rising)
                {
                    rising = true; m.rounds++;

                }
                if (attacks.Length == 0) rising = false;
                foreach (var shot in attacks) if (seen.Add(shot))
                {
                    VerifyArt(shot, pattern); m.shots++; m.releaseTimes.Add(Enemy.FightSeconds - start); m.heights.Add(shot.transform.position.y);
                    if (m.shots == 1) { Shot(prefix + "-release"); pendingFrames.Add(Time.frameCount + 4); pendingFrames.Add(Time.frameCount + 10); }
                }
                if (pattern == EnemyAttack.SW01 && attacks.Length == 2 && !both) { both = true; Shot(prefix + "-both-lanes"); }
                for (int i = pendingFrames.Count - 1; i >= 0; i--) if (Time.frameCount >= pendingFrames[i]) { Shot(prefix + "-motion-" + serial++); pendingFrames.RemoveAt(i); }
                if (!recovery && Get(Enemy, "state").ToString() == "Recover" && seen.Count > 0) { recovery = true; Shot(prefix + "-recovery"); }
            }
            catch (Exception e) { error = e.ToString(); }
        };
        await Until(() => !(bool)Get(Enemy, "running") || error != null); probe.Before = probe.After = null; UnityEngine.Object.Destroy(probe.gameObject); Set(Enemy, "running", true);
        m.seconds = Enemy.FightSeconds - start; Check(error == null, error);
        Check(m.shots == (pattern == EnemyAttack.SW01 ? 2 : 1), "exact committed shot count " + JsonUtility.ToJson(m));

        // SW01 chooses its first lane randomly once per cast. Both original
        // heights and their stagger must survive in either order.
        if (pattern == EnemyAttack.SW01) Check(both
            && Mathf.Abs(Mathf.Min(m.heights[0], m.heights[1]) - 2.1f) < 0.01f
            && Mathf.Abs(Mathf.Max(m.heights[0], m.heights[1]) - 6.6f) < 0.01f
            && m.releaseTimes[1] - m.releaseTimes[0] >= 0.42f,
            "SW01 retains both randomized lanes and release stagger: " + JsonUtility.ToJson(m) + "; overlap=" + both);
        if (pattern == EnemyAttack.SWBasic && melee) Check(seen.Count == 1, "one melee retains latched mode");
        Check(m.peakCosmetics <= KarmaSwordVfxPool.Capacity, "bounded sprite pool");
        File.WriteAllText(output + "/" + prefix + ".json", JsonUtility.ToJson(m, true)); Log(prefix + ": " + m.shots + " shots, " + m.rounds + " rises, " + m.seconds.ToString("F3") + "s, cosmetics peak " + m.peakCosmetics);
    }
    static async Task LockTest()
    {
        Reset(); Place(35); await Seconds(0.1f); Force(EnemyAttack.SW02); await Seconds(0.10f); Place(5);
        await Seconds(0.3f);
        Check(Enemy.Facing < 0, "SW02 tracks the player crossing left during preparation");
        Shot("SW02-tracking-left"); Place(40);
        await Until(() => Root.GetComponentsInChildren<KarmaBossAttack>().Length > 0);
        var shot = Root.GetComponentsInChildren<KarmaBossAttack>()[0];
        var released = (Vector2)Get(shot, "velocity");
        Check(Enemy.Facing > 0 && released.x > 0, "SW02 keeps tracking through the presentation's Locked phase until release");
        Check(((Vector2)Get(shot, "velocity")).y == 0 && (bool)Get(shot, "aimedSlash"), "horizontal slash flag/trajectory preserved");
        Place(5); await Seconds(0.05f);
        Check(shot != null && (Vector2)Get(shot, "velocity") == released, "released SW02 never retargets after a second crossing");
        Shot("SW02-committed-right-after-release"); Log("SW02 tracks both preparation crossings; the released projectile keeps its original direction");
    }
    static async Task Impact()
    {
        Reset(); Place(29); await Seconds(0.1f); Force(EnemyAttack.SWBasic); await Seconds(0.15f); Enemy.TakeDamage(1); await Seconds(0.02f); Shot("boss-confirmed-hit");
        Check(game.World.Find("Sword boss confirmed hit") != null, "boss confirmed-hit overlay");
        await Until(() => Root.GetComponentsInChildren<KarmaBossAttack>().Length > 0);
        var shot = Root.GetComponentsInChildren<KarmaBossAttack>()[0]; Place(shot.transform.position.x); Set(game.Player, "invulnerableUntil", 0f);
        float hp = game.Player.Health; await Until(() => game.Player.Health < hp); Shot("player-confirmed-hit");
        var pool = game.World.GetComponent<KarmaSwordVfxPool>();
        var hitSprite = KarmaWaveArtwork.Sprite("Sword_Impact_v6");
        Check(pool != null && Array.Exists(pool.GetComponentsInChildren<SpriteRenderer>(),
            picture => picture.sprite == hitSprite && Vector2.Distance(picture.transform.position, game.Player.DamageBounds.center) < 2),
            "SWBasic confirmed contact displays its pooled impact at the player");
        Log("boss and player actual confirmed-hit effects; ordinary hit preserves action");
    }
    static async Task PoolCheck()
    {
        Reset(); await Seconds(0.1f); var pool = KarmaSwordVfxPool.For(game);
        var cosmeticRoot = (Transform)Get(pool, "poolRoot");
        Check(cosmeticRoot.GetComponentsInChildren<Collider2D>(true).Length == 0 && cosmeticRoot.GetComponentsInChildren<KarmaBossAttack>(true).Length == 0, "cosmetic pool cannot deal damage");
        pool.Sparks(Enemy, Enemy.Position, Vector2.up, 1, 1); pool.Flash(Enemy, "Sword_Impact_v6", Enemy.Position, Vector2.one, 0.1f);
        // Enemy is a reflection-based fixture accessor; keep its allocations
        // outside the measured runtime operation.
        var owner = Enemy;
        long allocated = KarmaAllocationChecks.Count(() =>
        {
            for (int i = 0; i < 100; i++) { pool.Sparks(owner, owner.Position, Vector2.up, 3, 5); pool.Flash(owner, "Sword_Impact_v6", owner.Position, Vector2.one, 0.1f); }
        });
        File.WriteAllText(output + "/pool-allocation.txt", "100 iterations / 400 pooled emissions after warmup; GC.Alloc events = " + allocated + "; fixed slots = " + KarmaSwordVfxPool.Capacity);
        Check(allocated == 0, "warm cosmetic emission avoids per-effect managed allocation: " + allocated);
        await Seconds(0.4f); Check(pool.ActiveCount == 0, "pooled effects expire"); Log("100 warmed pooled-emission iterations, allocated " + allocated + " GC.Alloc events; fixed 96 slots");
    }
    static async Task AccentCancellation()
    {
        Reset(); await Seconds(0.1f); Force(EnemyAttack.SW01);
        await Until(() => Root.GetComponentsInChildren<KarmaBossAttack>().Length > 0);
        var accent = ((Transform)Get(Enemy, "art")).GetComponent<KarmaSwordAttackAccent>();
        Check(accent != null, "live release has cosmetic recoil");
        Set(Enemy, "pendingParry", true); await Until(() => Enemy.Stunned);
        await Seconds(0.05f); Check((float)Get(accent, "age") >= 0.22f, "stun cancels recoil rather than resuming it later");
        Reset(); await Seconds(0.1f); var art = (Transform)Get(Enemy, "art"); accent = art.gameObject.AddComponent<KarmaSwordAttackAccent>(); accent.Fire(game, Enemy, true);
        Set(Enemy, "selected", EnemyAttack.SW05); await Seconds(0.05f);
        Check((float)Get(accent, "age") >= 0.22f, "SW05 never inherits an unfinished ordinary recoil");
        Log("live release recoil cancels on real forced stun and on SW05 transition; no deferred visual impulse");
    }
    static async Task CounterChecks()
    {
        var t = typeof(KarmaSwordWallCounterChecks);
        // Preserve real contact-ledger tests for both queued and already flying later shots.
        for (int index = 0; index < 4; index++) { await Invoke(t, "CheckParry", index, false); await Invoke(t, "CheckParry", index, true); }
        await Invoke(t, "CheckNonParry"); await Invoke(t, "CheckReuseAndStale");
        var keyboard = InputSystem.AddDevice<Keyboard>(); typeof(KarmaSwordRandomParryChecks).GetField("keyboard", F).SetValue(null, keyboard);
        try
        {
            await Invoke(typeof(KarmaSwordRandomParryChecks), "ParryScenario", "A-first-parry", 0, false, false, false);
            await Invoke(typeof(KarmaSwordRandomParryChecks), "ParryScenario", "B-hit1-parry2", 1, false, false, false);
            await Invoke(typeof(KarmaSwordRandomParryChecks), "ParryScenario", "D-third-parry", 2, false, false, false);
            await Invoke(typeof(KarmaSwordRandomParryChecks), "ParryScenario", "E-fourth-parry", 3, false, false, false);
        }
        finally { InputSystem.RemoveDevice(keyboard); }
        Log("SW04 real K parries 1/2/3/4, immediate later-contact suppression, pending cancellation, normal hit/guard survival and fresh reuse");
    }
    static async Task ParryStunChecks()
    {
        foreach (int stage in new[] { 2, 3 })
        foreach (var pattern in new[] { EnemyAttack.SWBasic, EnemyAttack.SW01, EnemyAttack.SW02,
            EnemyAttack.SW03, EnemyAttack.SW04, EnemyAttack.SW05 })
        {
            if (stage != 3 && pattern == EnemyAttack.SW05) continue;
            Reset(); typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, stage);
            Place(35); await Seconds(0.1f); Force(pattern);
            if (pattern == EnemyAttack.SW04)
            {
                await Until(() => (bool)Get(Counter, "Guarding")); Enemy.TakeDamage(1);
            }
            await Until(() => Root.GetComponentsInChildren<KarmaBossAttack>().Length > 0
                || Root.GetComponentsInChildren<KarmaSW05RainBlade>().Length > 0);
            var attacks = Root.GetComponentsInChildren<KarmaBossAttack>();
            object active = attacks.Length > 0 ? (object)attacks[0] : Root.GetComponentsInChildren<KarmaSW05RainBlade>()[0];
            var contact = (KarmaAttackContact)Get(active, "contact");
            Set(game.Player, "invulnerableUntil", 0f); Set(game.Player, "guardUntil", 0f);
            game.Player.BeginCounter(); int parries = game.Player.ParryCount;
            Vector2 at = game.Player.GetComponent<Collider2D>().bounds.center;
            bool wasRunning = (bool)Get(Enemy, "running");
            float stunBegan = float.NaN;
            var onsetProbe = stage == 3 ? null
                : new GameObject("stun onset frame observer").AddComponent<KarmaSwordDashFrameProbe>();
            if (onsetProbe != null)
                onsetProbe.After = () => { if (float.IsNaN(stunBegan) && Enemy.Stunned) stunBegan = Enemy.FightSeconds; };
            Enemy.TryAttackHit(at, at, Vector2.one, 1, contact, 0, sourcePattern: pattern);
            if (stage == 3)
            {
                Check(game.Player.ParryCount == parries + 1 && !(bool)Get(Enemy, "pendingParry")
                    && !Enemy.Stunned && (bool)Get(Enemy, "running") == wasRunning, pattern + " final inheritance accepts parry without stun or interruption");
                // Also exercise the host's explicit stun and shield-break flags.
                Set(game.Player, "invulnerableUntil", 0f); game.Player.BeginCounter();
                Enemy.TryAttackHit(at, at, Vector2.one, 1, new KarmaAttackContact(true), 0,
                    parryStun: true, shieldBreak: true, sourcePattern: pattern);
                Check(game.Player.ParryCount == parries + 2 && !(bool)Get(Enemy, "pendingParry"),
                    pattern + " final inheritance clears host-queued parry stun");
                await Seconds(0.1f);
                var finalOrbit = Enemy.transform.Find("Sword stunned orbit");
                Check(!Enemy.Stunned && (finalOrbit == null || !finalOrbit.gameObject.activeInHierarchy),
                    pattern + " final inheritance has no delayed stun or stun marker");
                await Until(() => !(bool)Get(Enemy, "running") && !(bool)Get(Counter, "guardResponseRunning"));
                Check(!Enemy.Stunned, pattern + " final inheritance completes normally after parry");
                Log(pattern + ": final inheritance parry succeeds without stun; attack completes");
                continue;
            }
            Check(game.Player.ParryCount == parries + 1 && (bool)Get(Enemy, "pendingParry"), pattern + " real successful contact queues boss stun");
            Check(Root.GetComponentsInChildren<KarmaBossAttack>().Length == 0
                && Root.GetComponentsInChildren<KarmaSW05RainBlade>().Length == 0, pattern + " suppresses every live contact synchronously");
            try { await Until(() => !float.IsNaN(stunBegan)); }
            finally { onsetProbe.After = null; UnityEngine.Object.Destroy(onsetProbe.gameObject); }
            float start = stunBegan, deadline = (float)Get(Enemy, "stunnedUntil");
            Vector2 position = Enemy.Position;
            Check(Mathf.Abs(deadline - start - 2) < 0.02f, pattern + " stun deadline is exactly two combat seconds");
            Check(!Enemy.Armored && !Enemy.Invulnerable && !(bool)Get(Enemy, "running"), pattern + " releases armor/ultimate invulnerability and interrupts attack");
            await Seconds(0.1f);
            var orbit = Enemy.transform.Find("Sword stunned orbit");
            var picture = ((Transform)Get(Enemy, "art")).Find("Guardian sprite").GetComponent<SpriteRenderer>();
            var feedback = Enemy.GetComponent<KarmaSwordStunFeedback>();
            Shot("parry-stun-" + pattern);
            var electric = Array.Find(game.World.GetComponentsInChildren<KarmaSwordElectrocution>(), fx => fx.IsBoss);
            if (pattern == EnemyAttack.SW04)
            {
                Check(feedback.Electrocuted && electric != null && electric.FrameIndex > 0
                    && electric.GetComponentsInChildren<LineRenderer>().Length == 15 && !orbit.gameObject.activeInHierarchy,
                    "SW04 uses its animated electrocution instead of the ordinary star orbit");
                Check(electric.GetComponentsInChildren<Collider2D>().Length == 0
                    && electric.GetComponentsInChildren<KarmaBossAttack>().Length == 0, "electrocution cannot deal damage");
            }
            else
            {
                Check(orbit != null && orbit.gameObject.activeInHierarchy && orbit.GetComponentsInChildren<LineRenderer>().Length == 4,
                    pattern + " has a visible orbit and three stars");
                Check(picture.color.r < 0.7f && picture.color.b > picture.color.r && ((KarmaCharacterAnimator)Get(Enemy, "animator")).CurrentPose == "Hurt",
                    pattern + " distinct stunned body tint and hurt pose");
                Check(Quaternion.Angle(picture.transform.localRotation, Quaternion.identity) > 5, "stunned body visibly leans");
            }
            Check(orbit.GetComponentsInChildren<Collider2D>().Length == 0 && orbit.GetComponentsInChildren<KarmaBossAttack>().Length == 0,
                "stun marker is cosmetic and outside incoming damage artwork");
            float health = Enemy.Health; Enemy.TakeDamage(1);
            Check(Enemy.Health < health && (float)Get(Enemy, "stunnedUntil") == deadline, "damage during stun is accepted without extending its duration");
            // A consumed contact cannot re-trigger or extend this stun.
            Enemy.TryAttackHit(at, at, Vector2.one, 1, contact, 0, sourcePattern: pattern);
            Check(!(bool)Get(Enemy, "pendingParry") && (float)Get(Enemy, "stunnedUntil") == deadline, "duplicate parry contact cannot extend stun");
            if (pattern == EnemyAttack.SW04)
            {
                Shot("parry-stun-active");
                int electricFrame = electric.FrameIndex;
                typeof(KarmaGame).GetMethod("TogglePause", F).Invoke(game, null);
                float clock = Enemy.FightSeconds;
                try
                {
                    await Task.Delay(150);
                    Check(Enemy.FightSeconds == clock && electric.FrameIndex == electricFrame
                        && Array.TrueForAll(electric.GetComponentsInChildren<LineRenderer>(), line => !line.enabled),
                        "pause freezes the stun clock and electric frame and hides its discharges");
                }
                finally { typeof(KarmaGame).GetMethod("TogglePause", F).Invoke(game, null); }
            }
            float recoveredAt = float.NaN;
            var recoveryProbe = new GameObject("stun recovery frame observer").AddComponent<KarmaSwordDashFrameProbe>();
            recoveryProbe.After = () => { if (float.IsNaN(recoveredAt) && !Enemy.Stunned) recoveredAt = Enemy.FightSeconds; };
            try
            {
                await Until(() => Enemy.FightSeconds >= deadline - 0.1f);
                Check(Enemy.Stunned && Vector2.Distance(position, Enemy.Position) < 0.001f
                    && Root.GetComponentsInChildren<KarmaBossAttack>().Length == 0, pattern + " cannot move or attack before two seconds");
                await Until(() => !float.IsNaN(recoveredAt));
                // Observe the actual transition frame, not the later async poll.
                Check(recoveredAt >= deadline && recoveredAt <= deadline + 0.04f,
                    pattern + " recovers at the two-second boundary: " + recoveredAt + " / " + deadline);
            }
            finally { recoveryProbe.After = null; UnityEngine.Object.Destroy(recoveryProbe.gameObject); }
            await Seconds(0.05f);
            Check(!orbit.gameObject.activeInHierarchy && picture.color.r > 0.9f, "recovery removes the orbit and restores body tint");
            Check(!feedback.Electrocuted && Array.Find(game.World.GetComponentsInChildren<KarmaSwordElectrocution>(), fx => fx.IsBoss) == null,
                "recovery removes electrical status and its presentation");
            Check(Enemy.GetComponentsInChildren<KarmaSwordStunFeedback>(true).Length == 1, "stun feedback is reused without accumulating components");
            if (pattern == EnemyAttack.SW04) Shot("parry-stun-recovered");
            Log(pattern + ": successful parry, two-second stun, tint/lean/orbit, damage and recovery verified");
        }
        // Cleanup while still stunned must not leave the marker alive.
        Reset(); await Seconds(0.1f); Set(Enemy, "pendingParry", true); await Until(() => Enemy.Stunned); await Seconds(0.05f);
        var canceled = Enemy.transform.Find("Sword stunned orbit"); Enemy.CancelCombat();
        await Task.Delay(80); Check(canceled == null || !canceled.gameObject.activeInHierarchy, "combat cancellation hides stun marker");
        Reset(); await Seconds(0.1f); Set(Enemy, "pendingParry", true); await Until(() => Enemy.Stunned); await Seconds(0.05f);
        var dying = Enemy.transform.Find("Sword stunned orbit"); Enemy.TakeDamage(Enemy.MaxHealth * 2);
        await Task.Delay(80); Check(dying == null || !dying.gameObject.activeInHierarchy, "death removes stun marker");
        Log("stun pause, cancellation and death cleanup verified");
    }
    static async Task CounterPresentation()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>(); typeof(KarmaSwordRandomParryChecks).GetField("keyboard", F).SetValue(null, keyboard);
        bool ward = false, release = false, parry = false; int flight = 0, releaseFrame = -1;
        var probe = new GameObject("SW04 actual flight presentation").AddComponent<KarmaSwordDashFrameProbe>();
        probe.After = () =>
        {
            // A successful first parry can drain the whole volley this frame.
            // Capture the world-owned success cue even after its root is gone.
            if (!parry && game.Player.ParryCount > 0) { parry = true; Shot("SW04-real-parry-disintegration"); }
            var volley = Get(Counter, "activeVolley"); if (volley == null) return;
            var shots = (KarmaBossAttack[])Get(volley, "Shots");
            if (!ward && shots[0] == null) { ward = true; Shot("SW04-ward-deployed"); }
            if (!release && shots[0] != null) { release = true; releaseFrame = Time.frameCount; Shot("SW04-counter-release"); }
            if (release && flight < 3 && Time.frameCount >= releaseFrame + 4 * (flight + 1)) { Shot("SW04-flight-" + flight); flight++; }
        };
        try
        {
            await Invoke(typeof(KarmaSwordRandomParryChecks), "ParryScenario", "B-hit1-parry2", 1, false, false, false);
            // The shortened volley may finish before this frame's LateUpdate.
            // Let the observer render its world-owned success cue before teardown.
            await Seconds(0.05f);
        }
        finally { probe.After = null; UnityEngine.Object.Destroy(probe.gameObject); InputSystem.RemoveDevice(keyboard); }
        Check(ward && release && parry, "actual SW04 formation, live release and real-K disintegration captures");
        Log("SW04 formation / actual flight + 4-frame sequence / real-K parry and harmless disintegration captured");
    }
    static async Task MovementChecks()
    {
        await Invoke(typeof(KarmaSwordSlideDistanceChecks), "Samples");
        await Invoke(typeof(KarmaSwordSlideDistanceChecks), "Safety");
        await Invoke(typeof(KarmaSwordSlideDistanceChecks), "Campaign");
        foreach (int direction in new[] { -1, 1 })
        { await Invoke(typeof(KarmaSwordSlideDistanceChecks), "Render", direction, false); await Invoke(typeof(KarmaSwordSlideDistanceChecks), "Render", direction, true); }
    }
    static async void Run()
    {
        int exit = 0; id = SessionState.GetString(Key + ".Id", "missing"); output = "output/final-sword-polish/" + id; Directory.CreateDirectory(output);
        try
        {
            Application.runInBackground = true; Application.targetFrameRate = 120; Time.captureDeltaTime = 1f / 60;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            // Match the exported widescreen Game view before the game's normal
            // FrameStageBackground runs; do not change the production camera.
            game.GameCamera.aspect = 1600f / 700;
            foreach (var t in new[] { typeof(KarmaSwordPlayChecks), typeof(KarmaSwordRetreatDashPlayChecks), typeof(KarmaSwordOverhaulPlayChecks), typeof(KarmaSwordWallCounterChecks), typeof(KarmaSwordRandomParryChecks), typeof(KarmaSwordSlideDistanceChecks) }) t.GetField("game", F).SetValue(null, game);
            Set(game, "config", UnityEngine.Object.Instantiate(game.Config)); game.Config.playerHealth = 1000000; game.Config.BossTuning.sword.sw04DebugLogging = true;
            typeof(KarmaSwordRandomParryChecks).GetField("runId", F).SetValue(null, id); Directory.CreateDirectory("output/random-parry2/" + id);
            typeof(KarmaSwordWallCounterChecks).GetField("runId", F).SetValue(null, id); Directory.CreateDirectory("output/wall-counter/" + id);
            typeof(KarmaSwordOverhaulPlayChecks).GetField("updated", F).SetValue(null, true); SessionState.SetString("K4RMA.OverhaulChecks.Run", id); Directory.CreateDirectory("output/overhaul-updated/" + id);
            typeof(KarmaSwordSlideDistanceChecks).GetField("output", F).SetValue(null, output); typeof(KarmaSwordSlideDistanceChecks).GetField("baseline", F).SetValue(null, false);
            Application.logMessageReceived += (m, s, type) =>
            {
                if (m.StartsWith("[SW04]")) ((List<string>)typeof(KarmaSwordRandomParryChecks).GetField("debugTrace", F).GetValue(null)).Add(m);
                if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert) && s.Contains("Assets/K4RMA/")) error = m + "\n" + s;
            };
            Check(ReferenceEquals(KarmaWaveArtwork.Sprite("SW01_CrescentWave_v4"), KarmaWaveArtwork.Sprite("SW02_AimedSlash")), "SW01 and SW02 use the same complete Sprite object");
            typeof(KarmaSwordPlayChecks).GetMethod("CheckNewArtworkImports", F).Invoke(null, null);
            await AccentCancellation();
            if (SessionState.GetBool(Key + ".GroundedArtAndParry", false))
            {
                incomingDamage = false;
                await Pattern(EnemyAttack.SW03, 1); await Pattern(EnemyAttack.SW03, 1, true);
                await ParryStunChecks();
            }
            else if (SessionState.GetBool(Key + ".ParryStunOnly", false))
            { if (!SessionState.GetBool(Key + ".SkipCounterChecks", false)) await CounterChecks(); await ParryStunChecks(); }
            else if (SessionState.GetBool(Key + ".MovementOnly", false))
                await MovementChecks();
            else if (SessionState.GetBool(Key + ".PresentationOnly", false))
            {
                // Capture attack art without the stress test's constant damage overlay.
                incomingDamage = false;
                if (SessionState.GetBool(Key + ".ScarOnly", false))
                {
                    await Pattern(EnemyAttack.SW03, 1); await Pattern(EnemyAttack.SW03, 1, true);
                }
                else
                {
                bool leftOnly = SessionState.GetBool(Key + ".LeftOnly", false);
                foreach (int side in leftOnly ? new[] { -1 } : new[] { -1, 1 })
                {
                    await Pattern(EnemyAttack.SWBasic, side, false, true); await Pattern(EnemyAttack.SWBasic, side);
                    await Pattern(EnemyAttack.SW01, side); await Pattern(EnemyAttack.SW02, side);
                }
                if (!leftOnly)
                {
                await Pattern(EnemyAttack.SW03, 1);
                await CounterPresentation();
                foreach (int direction in new[] { -1, 1 }) { await Invoke(typeof(KarmaSwordSlideDistanceChecks), "Render", direction, false); await Invoke(typeof(KarmaSwordSlideDistanceChecks), "Render", direction, true); }
                Log("cyan movement afterimages rendered for both ordinary and wall-escape directions");
                }
                }
            }
            else
            {
            await Invoke(typeof(KarmaSwordPlayChecks), "CheckBasics");
            await PoolCheck();
            foreach (int side in new[] { -1, 1 })
            {
                await Pattern(EnemyAttack.SWBasic, side, false, true); await Pattern(EnemyAttack.SWBasic, side);
                await Pattern(EnemyAttack.SW01, side); await Pattern(EnemyAttack.SW02, side);
            }
            await LockTest(); await Pattern(EnemyAttack.SW03, 1); await Pattern(EnemyAttack.SW03, 1, true);
            await Impact(); await CounterChecks(); await ParryStunChecks(); await CounterPresentation();
            await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckScarEscape");
            await Invoke(typeof(KarmaSwordPlayChecks), "CheckForcedInterruption");
            await MovementChecks();
            // Current health-gated SW05 is covered by the separate sw05 suite.
            }
            foreach (var t in new[] { typeof(KarmaSwordSlideDistanceChecks), typeof(KarmaSwordWallCounterChecks), typeof(KarmaSwordOverhaulPlayChecks) })
                results.AddRange((List<string>)t.GetField("results", F).GetValue(null));
            foreach (string folder in new[] { "output/random-parry2/" + id, "output/wall-counter/" + id }) foreach (var file in Directory.GetFiles(folder)) File.Copy(file, output + "/" + Path.GetFileName(file), true);
            File.Copy("output/validation/sword-player-compilation.txt", output + "/player-compilation.txt", true);
            Check(error == null, "no game runtime error: " + error);
            results.Insert(0, SessionState.GetBool(Key + ".MovementOnly", false)
                ? "PASS: sword avoidance, actual slide distances, pursuit and rendered afterimages"
                : "PASS: red sword final polish, real render and combat regressions");
        }
        catch (Exception e) { results.Insert(0, "FAIL: " + e); Debug.LogException(e); exit = 1; }
        File.WriteAllLines(output + "/result.txt", results); Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
}
#endif
