#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

// Executed in the disposable project by run-sword-slide-distance-checks.ps1.
[InitializeOnLoad]
public static class KarmaSwordSlideDistanceChecks
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Key = "K4RMA.SlideDistance";
    static KarmaGame game;
    static bool started, baseline, reviewHold;
    static string output, runtimeError;
    static readonly List<string> results = new List<string>();
    static KarmaEnemy Enemy => (KarmaEnemy)typeof(KarmaSwordPlayChecks).GetField("enemy", F).GetValue(null);
    static object Behavior => Get(Enemy, "behavior");
    static object Movement => Get(Behavior, "movement");
    static object Get(object o, string n) { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, F); if (f != null) return f.GetValue(o); } throw new MissingFieldException(n); }
    static void Set(object o, string n, object v) { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, F); if (f != null) { f.SetValue(o, v); return; } } throw new MissingFieldException(n); }
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, F).Invoke(o, a);
    static async Task Invoke(Type t, string n, params object[] a) { if (t.GetMethod(n, F).Invoke(null, a) is Task task) await task; }
    static void Reset() => typeof(KarmaSwordPlayChecks).GetMethod("Reset", F).Invoke(null, new object[] { Essence.SwordWave });
    static void Place(float x) => typeof(KarmaSwordPlayChecks).GetMethod("Place", F).Invoke(null, new object[] { x });
    static void Check(bool ok, string m) { if (!ok) throw new Exception(m); }
    static void Log(string m) { results.Add("PASS: " + m); Debug.Log(m); }
    static float Remaining => (float)Get(Movement, "swordRetreatRemaining");
    static string Kind => Get(Movement, "slideKind").ToString();
    static void Tick() { typeof(KarmaEnemy).GetProperty("FightSeconds").SetValue(Enemy, Enemy.FightSeconds + 1f / 60); Call(Behavior, "TickMovement", 1f / 60, game.Player.Position.x - Enemy.Position.x); Physics2D.SyncTransforms(); }
    static async Task Until(Func<bool> done) { double end = EditorApplication.timeSinceStartup + 180; while (!done()) { Check(EditorApplication.timeSinceStartup < end, "Play Mode timeout"); await Task.Delay(5); } }
    static Task Seconds(float s) { float end = Enemy.FightSeconds + s; return Until(() => Enemy.FightSeconds >= end); }
    static void Shot(string n) { typeof(KarmaSwordPlayChecks).GetMethod("Snapshot", F).Invoke(null, new object[] { "slide-distance-" + n }); File.Copy("output/validation/slide-distance-" + n + ".png", output + "/" + n + ".png", true); }
    static KarmaSwordSlideDistanceChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (reviewHold && File.Exists(output + "/ui-continue")) { File.Delete(output + "/ui-continue"); File.Delete(output + "/ui-ready.txt"); reviewHold = false; }
        if (EditorApplication.isPlaying) EditorApplication.isPaused = reviewHold;
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2) { started = true; Run(); }
    }
    public static void RunBaseline() => Begin(true);
    public static void RunUpdated() => Begin(false);
    public static void RunSW05() { Begin(false); SessionState.SetBool(Key + ".SW05Only", true); }
    public static void RunPresentation() { Begin(false); SessionState.SetBool(Key + ".PresentationOnly", true); }
    static void Begin(bool before)
    {
        SessionState.SetBool(Key + ".SW05Only", false);
        SessionState.SetBool(Key + ".PresentationOnly", false);
        if (!before) typeof(KarmaSwordPlayChecks).GetMethod("CompilePlayerScripts", F).Invoke(null, null);
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Baseline", before);
        SessionState.SetString(Key + ".Run", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity"); EditorApplication.EnterPlaymode();
    }
    [Serializable] sealed class Row { public string scenario, kind, stop; public float direction, requested, planned, actual, seconds, peakSpeed; public int frames; }
    [Serializable] sealed class Metrics { public int left, right, attacks, failures, consecutiveEvades, reached, targetStops, collisionStops, timeoutStops, interruptedStops, inFlight; public List<Row> moves = new List<Row>(); }
    static readonly List<Row> samples = new List<Row>();
    static Row StartRow(string scenario, float from) => new Row { scenario = scenario, kind = Kind, direction = (float)Get(Movement, "swordRetreatDirection"), planned = Mathf.Abs((float)Get(Movement, "swordRetreatTarget") - from), requested = Kind == "WallEscape" ? (baseline ? 32 : 48) : baseline ? 16 : 32 };
    static string Stop(Row r) => Mathf.Abs(r.actual - r.planned) < 0.01f ? "TargetReached" : Enemy.FightSeconds >= (float)Get(Movement, "swordRetreatUntil") ? "Timeout" : (bool)Get(Enemy, "running") || Enemy.Stunned ? "Interrupted" : "CollisionOrGround";
    static Row Sample(string scenario, float from)
    {
        var row = StartRow(scenario, from); float previous = Enemy.Position.x;
        do { Tick(); row.frames++; row.peakSpeed = Mathf.Max(row.peakSpeed, Mathf.Abs(Enemy.Position.x - previous) * 60); previous = Enemy.Position.x; Check(row.frames < 180, "slide stuck"); } while (Remaining > 0);
        row.actual = Mathf.Abs(Enemy.Position.x - from); row.seconds = (row.frames + 1) / 60f;
        row.stop = Mathf.Abs(row.actual - row.planned) < 0.01f ? "TargetReached" : Enemy.FightSeconds >= (float)Get(Movement, "swordRetreatUntil") ? "Timeout" : "CollisionOrGround";
        samples.Add(row); return row;
    }
    static void Arrange(float x, float playerX)
    {
        Enemy.transform.position = new Vector3(x, KarmaSpace.GuardianGroundY, 0); Place(playerX);
        typeof(KarmaEnemy).GetProperty("FightSeconds").SetValue(Enemy, Enemy.FightSeconds + 6);
        Call(Behavior, "OnCompleted"); Physics2D.SyncTransforms();
    }
    static void Samples()
    {
        int left = 0, right = 0;
        foreach (int playerSide in new[] { -1, 1 })
        {
            Reset(); UnityEngine.Random.InitState(6104 + playerSide);
            for (int i = 0; i < 80; i++)
            {
                Arrange(20, 20 + playerSide * 3.5f);
                UnityEngine.Random.InitState(6104 + playerSide * 1000 + i);
                var randomState = UnityEngine.Random.state;
                Tick(); Check(Remaining > 0, "open-space choice must start");
                if (!baseline) Check(UnityEngine.Random.state.Equals(randomState), "pressured direction selection consumes no random choice");
                var r = Sample("open-player-" + playerSide, 20); if (r.direction < 0) left++; else right++;
                if (!baseline) Check(r.direction == -playerSide && Kind == "LongRetreat",
                    "open retreat always moves away from the player, independently of random state");
                if (!baseline) Check(Mathf.Abs(r.actual - 32) < 0.01f && r.stop == "TargetReached", "normal slide must actually reach thirty-two: " + JsonUtility.ToJson(r));
            }
        }
        Check(left >= 30 && right >= 30, "at least thirty measured open slides each direction");
        Log("open-space 160 player-relative retreats: left " + left + ", right " + right);
        foreach (int wall in new[] { -1, 1 })
        {
            Reset(); float x = wall < 0 ? KarmaStage.BossLeftWall + 0.6f : KarmaStage.BossRightWall - 0.6f;
            for (int i = 0; i < 30; i++)
            {
                Arrange(x, x - wall * 3.5f); float invul = (float)Get(game.Player, "invulnerableUntil"); Tick();
                Check(Remaining > 0 && Kind == "WallEscape", "wall escape starts"); var r = Sample("wall-" + wall, x);
                Check(r.direction == -wall && Mathf.Abs(r.actual - (baseline ? 32 : 48)) < 0.01f, "wall actual distance");
                Check((float)Get(game.Player, "invulnerableUntil") == invul, "no new invulnerability");
                Place(Enemy.Position.x - wall * 3.5f); Call(Behavior, "OnCompleted");
                for (int j = 0; j < 120; j++) { float before = Enemy.Position.x; Tick(); Check((Enemy.Position.x - before) * wall <= 0.001f, "no immediate return toward same wall: wall=" + wall + ", frame=" + j + ", step=" + (Enemy.Position.x - before) + ", kind=" + Kind + ", remaining=" + Remaining); }
            }
            Log("wall " + wall + ": thirty continuous " + (baseline ? 32 : 48) + "-unit escapes, no return loop");
        }
        var csv = new List<string> { "scenario,kind,direction,requested,planned,actual,seconds,peakSpeed,frames,stop" };
        foreach (var r in samples) csv.Add(string.Join(",", r.scenario, r.kind, r.direction, r.requested, r.planned, r.actual, r.seconds, r.peakSpeed, r.frames, r.stop));
        File.WriteAllLines(output + "/samples.csv", csv);
    }
    static async Task Campaign()
    {
        Reset(); Place(16.5f); UnityEngine.Random.InitState(6104); Set(Enemy, "running", false); Set(Enemy, "timer", 0f);
        var m = new Metrics(); float start = Enemy.FightSeconds, nextHit = start + 0.6f, from = 0, lastPattern = -1, began = 0, previous = 0; Row row = null;
        int attacksAtPreviousEvade = -1;
        var probe = new GameObject("slide distance pursuit 60Hz").AddComponent<KarmaSwordDashFrameProbe>();
        probe.Before = () =>
        {
            float target = Mathf.Clamp(Enemy.Position.x - 3.5f, -14, 54);
            float step = Mathf.MoveTowards(game.Player.Position.x, target, game.Config.moveSpeed * Time.deltaTime) - game.Player.Position.x;
            if (Mathf.Abs(step) > 0.00001f) Call(game.Player, "MoveSlash", Vector2.right * Mathf.Sign(step), Mathf.Abs(step));
            if (Enemy.FightSeconds >= nextHit) { Enemy.TakeDamage(0.1f); nextHit += 0.6f; }
            if (row == null) from = Enemy.Position.x;
            previous = Enemy.Position.x;
        };
        probe.After = () =>
        {
            if (Remaining > 0 && row == null)
            {
                if (m.attacks <= attacksAtPreviousEvade) m.consecutiveEvades++;
                attacksAtPreviousEvade = m.attacks;
                row = StartRow("pursuit60", from); began = Enemy.FightSeconds;
            }
            if (row != null)
            {
                row.frames++; row.peakSpeed = Mathf.Max(row.peakSpeed, Mathf.Abs(Enemy.Position.x - previous) * 60);
                if (Remaining <= 0)
                {
                    row.actual = Mathf.Abs(Enemy.Position.x - from); row.seconds = Enemy.FightSeconds - began + 1f / 60; row.stop = Stop(row);
                    if (row.direction < 0) m.left++; else m.right++;
                    if (row.actual < 0.1f) m.failures++;
                    if (Mathf.Abs(row.actual - row.requested) < 0.01f) m.reached++;
                    if (row.stop == "TargetReached") m.targetStops++; else if (row.stop == "Timeout") m.timeoutStops++; else if (row.stop == "Interrupted") m.interruptedStops++; else m.collisionStops++;
                    m.moves.Add(row); row = null;
                }
            }
            float p = (float)Get(Enemy, "patternStarted"); if ((bool)Get(Enemy, "running") && p != lastPattern) { m.attacks++; lastPattern = p; }
        };
        await Until(() => Enemy.FightSeconds - start >= 60); m.inFlight = row != null ? 1 : 0; probe.Before = probe.After = null; UnityEngine.Object.Destroy(probe.gameObject);
        File.WriteAllText(output + "/pursuit60.json", JsonUtility.ToJson(m, true));
        Check(m.attacks >= 25 && m.left > 0 && m.right > 0 && m.failures == 0 && m.consecutiveEvades == 0, "attacks and both directions continue without failed moves or attack-free consecutive evades");
        Log("60s seed6104: moves " + m.moves.Count + ", left/right " + m.left + "/" + m.right + ", attacks " + m.attacks + ", failures " + m.failures + ", consecutive evades " + m.consecutiveEvades + ", nominal reached " + m.reached);
    }
    static void Safety()
    {
        Reset(); Arrange(20, 16.5f);
        Set(Movement, "wallReturnUntil", Enemy.FightSeconds + 3); Set(Movement, "wallReturnDirection", 1f);
        Tick();
        Check(Remaining <= 0 && Mathf.Abs(Enemy.Position.x - 20) < 0.001f && (bool)Get(Behavior, "basicAfterRetreat"),
            "wall-return protection yields an attack instead of an unnecessary slide toward the player");
        foreach (int wall in new[] { -1, 1 })
        {
            Reset(); float x = wall < 0 ? KarmaStage.BossLeftWall + 0.6f : KarmaStage.BossRightWall - 0.6f;
            Arrange(x, x - wall * 3.5f); var blocker = new GameObject("limited slide obstruction"); blocker.transform.position = new Vector3(x - wall * 14, 3, 0); blocker.AddComponent<BoxCollider2D>().size = new Vector2(1, 6); Physics2D.SyncTransforms();
            Tick(); Check(Remaining > 0, "limited escape starts"); var r = Sample("limited-wall-" + wall, x); Check(r.actual > 12.4f && r.actual < 12.7f, "limited escape respects body clearance"); UnityEngine.Object.DestroyImmediate(blocker);
            Reset(); Arrange(x, x - wall * 3.5f); blocker = new GameObject("fully blocked escape"); blocker.transform.position = new Vector3(x - wall * 2, 3, 0); blocker.AddComponent<BoxCollider2D>().size = new Vector2(1, 6); Physics2D.SyncTransforms();
            Tick(); Check(Remaining <= 0 && Mathf.Abs(Enemy.Position.x - x) < 0.001f && (bool)Get(Behavior, "basicAfterRetreat"), "both blocked routes yield attack"); UnityEngine.Object.DestroyImmediate(blocker);
            Reset(); Arrange(x, x - wall * 3.5f); var body = Enemy.gameObject.AddComponent<BoxCollider2D>(); body.size = new Vector2(1.6f, 3.2f); Physics2D.SyncTransforms(); Tick(); Check(Remaining <= 0, "existing solid player collision rejects crossing"); UnityEngine.Object.DestroyImmediate(body);
            Reset(); float near = wall < 0 ? KarmaStage.BossLeftWall + 4 : KarmaStage.BossRightWall - 4; Arrange(near, near - wall * 3.5f); Tick(); Check(Kind == "WallEscape" && (float)Get(Movement, "swordRetreatDirection") == -wall, "boundary approach detected before contact"); Sample("early-wall-" + wall, near);
        }
        // Put an obstacle into an already selected route, rather than only testing planning.
        Reset(); Arrange(20, 16.5f); Tick(); float direction = (float)Get(Movement, "swordRetreatDirection");
        var dynamicBlock = new GameObject("obstacle entering active slide"); dynamicBlock.transform.position = new Vector3(20 + direction * 7, 3, 0); dynamicBlock.AddComponent<BoxCollider2D>().size = new Vector2(1, 6); Physics2D.SyncTransforms();
        var stopped = Sample("dynamic-obstacle", 20); Check(stopped.actual < 6 && stopped.stop == "CollisionOrGround", "active slide stops safely at new obstacle"); UnityEngine.Object.DestroyImmediate(dynamicBlock);
        var csv = new List<string> { "scenario,kind,direction,requested,planned,actual,seconds,peakSpeed,frames,stop" };
        foreach (var r in samples) csv.Add(string.Join(",", r.scenario, r.kind, r.direction, r.requested, r.planned, r.actual, r.seconds, r.peakSpeed, r.frames, r.stop));
        File.WriteAllLines(output + "/samples.csv", csv);
        Log("short routes, both routes blocked, solid player, early wall detection and obstacle entering active slide pass");
    }
    static async Task Render(int direction, bool wall)
    {
        Reset(); float x = wall ? direction > 0 ? KarmaStage.BossLeftWall + 0.6f : KarmaStage.BossRightWall - 0.6f : 20;
        Arrange(x, x - direction * 3.5f); await Seconds(0.1f);
        // Player position determines the requested direction on the first choice.
        Arrange(x, x - direction * 3.5f); Tick();
        Check(Remaining > 0 && (float)Get(Movement, "swordRetreatDirection") == direction, "requested render direction selected");
        Set(Enemy, "running", false); Set(Enemy, "timer", 100f);
        var images = ((Transform)Get(Enemy, "art")).GetComponent<KarmaSwordRetreatAfterimages>(); var pool = (Array)Get(images, "pool"); int peak = 0; bool captured = false; float previousEcho = float.NaN, maxSpacing = 0;
        var probe = new GameObject("render trail observer").AddComponent<KarmaSwordDashFrameProbe>();
        probe.After = () =>
        {
            int active = 0; foreach (var echo in pool) if ((bool)Get(echo, "active")) active++;
            peak = Mathf.Max(peak, active);
            float emitted = ((Vector2)Get(images, "lastEmissionPosition")).x;
            if (!float.IsNaN(previousEcho) && Mathf.Abs(emitted - previousEcho) > 0.001f) maxSpacing = Mathf.Max(maxSpacing, Mathf.Abs(emitted - previousEcho)); previousEcho = emitted;
            // Wall starts lie at the edge of the real camera. Capture later in
            // the same slide so the body and trailing silhouettes are in view.
            if (active >= 4 && Mathf.Abs(Enemy.Position.x - x) >= (wall ? 18 : 6) && !captured)
            {
                captured = true; string name = (wall ? "wall48-" : "normal32-") + direction; Shot(name);
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-slideUiReview") >= 0)
                { reviewHold = true; EditorApplication.isPaused = true; File.WriteAllText(output + "/ui-ready.txt", name); }
            }
        };
        await Until(() => Remaining <= 0); probe.After = null; UnityEngine.Object.Destroy(probe.gameObject);
        Check(captured && peak <= pool.Length && Mathf.Abs(Enemy.Position.x - x - direction * (wall ? 48 : 32)) < 0.01f, "rendered slide reaches actual endpoint with bounded echoes");
        Set(Enemy, "running", true); await Seconds(0.4f); foreach (var echo in pool) Check(!(bool)Get(echo, "active"), "echoes naturally expire after movement");
        Set(Enemy, "running", false); Set(Enemy, "timer", 0f); await Until(() => (bool)Get(Enemy, "running"));
        Log("render " + (wall ? "wall48" : "normal32") + " direction " + direction + ": peak echoes " + peak + "/" + pool.Length + ", max spacing " + maxSpacing.ToString("F3") + ", naturally expires; attack resumes");
    }
    static async Task CheckSW05()
    {
        Reset(); Place(35); await Seconds(0.1f);
        typeof(KarmaSwordPlayChecks).GetMethod("Force", F).Invoke(null, new object[] { EnemyAttack.SW05 });
        var seen = new HashSet<KarmaBossAttack>(); int expected = 0; float start = Enemy.FightSeconds;
        var probe = new GameObject("unchanged SW05 observer").AddComponent<KarmaSwordDashFrameProbe>();
        probe.After = () =>
        {
            var root = (Transform)Get(Enemy, "effectsRoot");
            var planner = root.GetComponentInChildren<KarmaAerialSwordPlanner>();
            if (planner != null && expected == 0) { Check(planner.GroupSizes.Length == 7, "existing seven SW05 volleys"); foreach (int size in planner.GroupSizes) expected += size; }
            foreach (var shot in root.GetComponentsInChildren<KarmaBossAttack>()) seen.Add(shot);
        };
        await Until(() => !(bool)Get(Enemy, "running")); probe.After = null; UnityEngine.Object.Destroy(probe.gameObject); Set(Enemy, "running", true);
        Check(expected == 105 && seen.Count == expected, "unchanged SW05 completes all seven 15-blade volleys: " + seen.Count + "/" + expected);
        Check(((Transform)Get(Enemy, "effectsRoot")).GetComponentsInChildren<KarmaBossAttack>().Length == 0 && !Enemy.Invulnerable, "SW05 landing cleans up existing shield and projectiles");
        Log("unchanged SW05: seven volleys / " + seen.Count + " blades, completes and lands in " + (Enemy.FightSeconds - start).ToString("F3") + "s");
    }
    static async void Run()
    {
        int exit = 0; baseline = SessionState.GetBool(Key + ".Baseline", false); string id = SessionState.GetString(Key + ".Run", "missing"); output = "output/slide-distance/" + id; Directory.CreateDirectory(output);
        try
        {
            Application.runInBackground = true; Application.targetFrameRate = 120; Time.captureDeltaTime = 1f / 60;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            foreach (var t in new[] { typeof(KarmaSwordPlayChecks), typeof(KarmaSwordRetreatDashPlayChecks), typeof(KarmaSwordOverhaulPlayChecks), typeof(KarmaSwordWallCounterChecks) }) t.GetField("game", F).SetValue(null, game);
            Set(game, "config", UnityEngine.Object.Instantiate(game.Config)); game.Config.playerHealth = 1000000;
            Application.logMessageReceived += (m, s, t) => { if ((t == LogType.Error || t == LogType.Exception || t == LogType.Assert) && s.Contains("Assets/K4RMA/")) runtimeError = m + "\n" + s; };
            bool sw05Only = SessionState.GetBool(Key + ".SW05Only", false);
            bool presentationOnly = SessionState.GetBool(Key + ".PresentationOnly", false);
            if (sw05Only) await CheckSW05();
            else if (presentationOnly) { foreach (int dir in new[] { -1, 1 }) { await Render(dir, false); await Render(dir, true); } await CheckSW05(); }
            else { Samples(); await Campaign(); }
            if (!baseline && !sw05Only && !presentationOnly)
            {
                Safety(); foreach (int dir in new[] { -1, 1 }) { await Render(dir, false); await Render(dir, true); }
                await Invoke(typeof(KarmaSwordPlayChecks), "CheckBasics"); await Invoke(typeof(KarmaSwordPlayChecks), "CheckSwordDamageFeedback"); await Invoke(typeof(KarmaSwordPlayChecks), "CheckSW04"); await Invoke(typeof(KarmaSwordPlayChecks), "CheckForcedInterruption");
                typeof(KarmaSwordOverhaulPlayChecks).GetField("updated", F).SetValue(null, true); SessionState.SetString("K4RMA.OverhaulChecks.Run", id); Directory.CreateDirectory("output/overhaul-updated/" + id);
                foreach (int dir in new[] { -1, 1 }) await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckWaves", dir);
                await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckScars", false); await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckScarEscape");
                File.Copy("output/validation/sword-player-compilation.txt", output + "/player-compilation.txt", true);
                Log("melee/wave basic, SW01/SW02 both directions, SW03 five rounds/escape, SW04, damage feedback, forced interruption regressions");
            }
            Check(runtimeError == null, "no game runtime error: " + runtimeError); results.Insert(0, "PASS: " + (baseline ? "baseline" : "updated") + " actual slide distance Play Mode");
        }
        catch (Exception e) { results.Insert(0, "FAIL: " + e); Debug.LogException(e); exit = 1; }
        File.WriteAllLines(output + "/result.txt", results); Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
}
#endif
