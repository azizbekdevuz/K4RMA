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
        typeof(KarmaSwordPlayChecks).GetMethod("CompilePlayerScripts", F).Invoke(null, null);
        SessionState.SetBool(Key, true); SessionState.SetString(Key + ".Id", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity"); EditorApplication.EnterPlaymode();
    }
    public static void RunPresentationOnly() { RunBatch(); SessionState.SetBool(Key + ".PresentationOnly", true); }
    public static void RunLeftPresentation() { RunPresentationOnly(); SessionState.SetBool(Key + ".LeftOnly", true); }
    public static void RunScarPresentation() { RunPresentationOnly(); SessionState.SetBool(Key + ".ScarOnly", true); }
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
        string pose = pattern == EnemyAttack.SW03 ? "SW03_RisingBlade_v5" : KarmaWaveArtwork.Pose(pattern);
        var core = KarmaWaveArtwork.CorePixels(pose); var sr = shot.Visual;
        Check(sr.sprite == KarmaWaveArtwork.Sprite(pose), "released shot uses expected final sprite");
        Check(Mathf.Abs(sr.transform.localScale.x * core.width / sr.sprite.pixelsPerUnit - size.x) < 0.002f
            && Mathf.Abs(sr.transform.localScale.y * core.height / sr.sprite.pixelsPerUnit - size.y) < 0.002f, "cutting core maps to unchanged damage rectangle");
        if (Mathf.Abs(velocity.y) < 0.0001f && velocity.sqrMagnitude > 0.01f && pattern != EnemyAttack.SW04)
            Check(sr.flipX == (velocity.x < 0) && !sr.flipY, "horizontal crescent mirrors without vertical inversion");
        if (pattern == EnemyAttack.SWBasic && velocity.sqrMagnitude < 0.01f)
            Check(sr.flipX == (Enemy.Facing < 0) && !sr.flipY, "stationary melee arc faces its committed attack direction");
        if (pattern == EnemyAttack.SW03)
        {
            Check(size == new Vector2(4.75f, 30), "SW03 physical 4.75 x 30 blade retained");
            Check(sr.transform.Find("SW03 upward inner echo / no damage") == null, "no duplicate full-height echo renderer");
            var light = sr.transform.Find("Attack radiance"); Check(light == null || !light.gameObject.activeInHierarchy, "no overlapping full-height glow columns");
        }
    }
    static async Task Pattern(EnemyAttack pattern, float side, bool enraged = false, bool melee = false)
    {
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
                    if (pattern == EnemyAttack.SW03) { Check(attacks.Length == 20, "twenty simultaneous blades per round"); Shot(prefix + "-round-" + m.rounds); }
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
        Check(m.shots == (pattern == EnemyAttack.SW03 ? 100 : pattern == EnemyAttack.SW01 ? 2 : 1), "exact committed shot count " + JsonUtility.ToJson(m));
        if (pattern == EnemyAttack.SW03) Check(m.rounds == 5, "normal/enraged five rounds");
        if (pattern == EnemyAttack.SW01) Check(both && m.heights[0] < m.heights[1] && Mathf.Abs(m.heights[0] - 2.1f) < 0.01f && Mathf.Abs(m.heights[1] - 6.6f) < 0.01f, "SW01 low then high, both visible in flight");
        if (pattern == EnemyAttack.SWBasic && melee) Check(seen.Count == 1, "one melee retains latched mode");
        Check(m.peakCosmetics <= KarmaSwordVfxPool.Capacity, "bounded sprite pool");
        File.WriteAllText(output + "/" + prefix + ".json", JsonUtility.ToJson(m, true)); Log(prefix + ": " + m.shots + " shots, " + m.rounds + " rises, " + m.seconds.ToString("F3") + "s, cosmetics peak " + m.peakCosmetics);
    }
    static async Task LockTest()
    {
        Reset(); Place(35); await Seconds(0.1f); Force(EnemyAttack.SW02); await Seconds(0.10f); Place(5);
        await Until(() => Root.Find("고정 공격 경로") != null); float lockedDirection = Enemy.Facing;
        var warning = Root.Find("고정 공격 경로"); Vector2 center = warning.position; Shot("SW02-locked"); Place(40);
        await Until(() => Root.GetComponentsInChildren<KarmaBossAttack>().Length > 0);
        var shot = Root.GetComponentsInChildren<KarmaBossAttack>()[0];
        Check(lockedDirection < 0 && ((Vector2)Get(shot, "velocity")).x < 0, "SW02 tracks before lock and never retargets after lock");
        Check(((Vector2)Get(shot, "velocity")).y == 0 && (bool)Get(shot, "aimedSlash"), "horizontal slash flag/trajectory preserved");
        Shot("SW02-committed-left-after-player-crosses"); Log("SW02 tracking -> fixed left lock -> player crosses right -> fixed left release");
    }
    static async Task Impact()
    {
        Reset(); Place(29); await Seconds(0.1f); Force(EnemyAttack.SWBasic); await Seconds(0.15f); Enemy.TakeDamage(1); await Seconds(0.02f); Shot("boss-confirmed-hit");
        Check(game.World.Find("Sword boss confirmed hit") != null, "boss confirmed-hit overlay");
        await Until(() => Root.GetComponentsInChildren<KarmaBossAttack>().Length > 0);
        var shot = Root.GetComponentsInChildren<KarmaBossAttack>()[0]; Place(shot.transform.position.x); Set(game.Player, "invulnerableUntil", 0f);
        float hp = game.Player.Health; await Until(() => game.Player.Health < hp); Shot("player-confirmed-hit");
        Check(game.World.Find("Sword player confirmed hit") != null, "player confirmed-hit red flash"); Log("boss and player actual confirmed-hit effects; ordinary hit preserves action");
    }
    static async Task PoolCheck()
    {
        Reset(); await Seconds(0.1f); var pool = KarmaSwordVfxPool.For(game);
        var cosmeticRoot = (Transform)Get(pool, "poolRoot");
        Check(cosmeticRoot.GetComponentsInChildren<Collider2D>(true).Length == 0 && cosmeticRoot.GetComponentsInChildren<KarmaBossAttack>(true).Length == 0, "cosmetic pool cannot deal damage");
        pool.Sparks(Enemy, Enemy.Position, Vector2.up, 1, 1); pool.Flash(Enemy, "Sword_Impact_v6", Enemy.Position, Vector2.one, 0.1f);
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) { pool.Sparks(Enemy, Enemy.Position, Vector2.up, 3, 5); pool.Flash(Enemy, "Sword_Impact_v6", Enemy.Position, Vector2.one, 0.1f); }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        File.WriteAllText(output + "/pool-allocation.txt", "100 iterations / 400 pooled emissions after warmup; managed bytes = " + allocated + "; fixed slots = " + KarmaSwordVfxPool.Capacity);
        Check(allocated < 2048, "warm cosmetic emission avoids per-effect managed allocation: " + allocated);
        await Seconds(0.4f); Check(pool.ActiveCount == 0, "pooled effects expire"); Log("100 warmed pooled-emission iterations, allocated " + allocated + " managed bytes; fixed 96 slots");
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
    static async Task CounterPresentation()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>(); typeof(KarmaSwordRandomParryChecks).GetField("keyboard", F).SetValue(null, keyboard);
        bool ward = false, release = false, parry = false; int flight = 0, releaseFrame = -1;
        var probe = new GameObject("SW04 actual flight presentation").AddComponent<KarmaSwordDashFrameProbe>();
        probe.After = () =>
        {
            var volley = Get(Counter, "activeVolley"); if (volley == null) return;
            var shots = (KarmaBossAttack[])Get(volley, "Shots");
            if (!ward && shots[0] == null) { ward = true; Shot("SW04-ward-deployed"); }
            if (!release && shots[0] != null) { release = true; releaseFrame = Time.frameCount; Shot("SW04-counter-release"); }
            if (release && flight < 3 && Time.frameCount >= releaseFrame + 4 * (flight + 1)) { Shot("SW04-flight-" + flight); flight++; }
            if (!parry && game.Player.ParryCount > 0) { parry = true; Shot("SW04-real-parry-disintegration"); }
        };
        try { await Invoke(typeof(KarmaSwordRandomParryChecks), "ParryScenario", "B-hit1-parry2", 1, false, false, false); }
        finally { probe.After = null; UnityEngine.Object.Destroy(probe.gameObject); InputSystem.RemoveDevice(keyboard); }
        Check(ward && release && parry, "actual SW04 formation, live release and real-K disintegration captures");
        Log("SW04 formation / actual flight + 4-frame sequence / real-K parry and harmless disintegration captured");
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
            if (SessionState.GetBool(Key + ".PresentationOnly", false))
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
                Log("unchanged cyan movement afterimages rendered for both ordinary 16-unit and wall 24-unit directions");
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
            await Impact(); await CounterChecks();
            await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckScarEscape");
            await Invoke(typeof(KarmaSwordPlayChecks), "CheckForcedInterruption");
            await Invoke(typeof(KarmaSwordSlideDistanceChecks), "Samples"); await Invoke(typeof(KarmaSwordSlideDistanceChecks), "Safety"); await Invoke(typeof(KarmaSwordSlideDistanceChecks), "Campaign");
            // Current health-gated SW05 is covered by the separate sw05 suite.
            }
            foreach (var t in new[] { typeof(KarmaSwordSlideDistanceChecks), typeof(KarmaSwordWallCounterChecks), typeof(KarmaSwordOverhaulPlayChecks) })
                results.AddRange((List<string>)t.GetField("results", F).GetValue(null));
            foreach (string folder in new[] { "output/random-parry2/" + id, "output/wall-counter/" + id }) foreach (var file in Directory.GetFiles(folder)) File.Copy(file, output + "/" + Path.GetFileName(file), true);
            File.Copy("output/validation/sword-player-compilation.txt", output + "/player-compilation.txt", true);
            Check(error == null, "no game runtime error: " + error); results.Insert(0, "PASS: red sword final polish, real render and combat regressions");
        }
        catch (Exception e) { results.Insert(0, "FAIL: " + e); Debug.LogException(e); exit = 1; }
        File.WriteAllLines(output + "/result.txt", results); Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
}
#endif
