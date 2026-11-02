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
using UnityEngine.InputSystem.LowLevel;
using InputKey = UnityEngine.InputSystem.Key;

[InitializeOnLoad]
public static class KarmaSwordRandomParryChecks
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Key = "K4RMA.RandomParry2";
    static bool started, baseline;
    static KarmaGame game;
    static string runId, runtimeError;
    static readonly List<string> results = new List<string>();
    static string Output => "output/random-parry2/" + runId;
    static KarmaEnemy Enemy => (KarmaEnemy)typeof(KarmaSwordPlayChecks).GetField("enemy", F).GetValue(null);
    static object Behavior => Get(Enemy, "behavior");
    static object Movement => Get(Behavior, "movement");
    static object Counter => Get(Behavior, "counter");
    static Transform Art => (Transform)Get(Enemy, "art");
    static Transform Root => (Transform)Get(Enemy, "effectsRoot");
    static object Get(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, F); if (field != null) return field.GetValue(target); }
        throw new MissingFieldException(name);
    }
    static void Set(object target, string name, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, F); if (field != null) { field.SetValue(target, value); return; } }
        throw new MissingFieldException(name);
    }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, F).Invoke(target, args);
    static async Task Invoke(Type type, string name, params object[] args)
    { var result = type.GetMethod(name, F).Invoke(null, args); if (result is Task task) await task; }
    static void Reset() => typeof(KarmaSwordPlayChecks).GetMethod("Reset", F).Invoke(null, new object[] { Essence.SwordWave });
    static void Place(float x) => typeof(KarmaSwordPlayChecks).GetMethod("Place", F).Invoke(null, new object[] { x });
    static void Force(EnemyAttack pattern) => typeof(KarmaSwordPlayChecks).GetMethod("Force", F).Invoke(null, new object[] { pattern });
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Log(string message) { results.Add("PASS: " + message); Debug.Log(message); }
    static async Task Until(Func<bool> done, double seconds = 180)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (!done()) { Check(EditorApplication.timeSinceStartup < end, "Play Mode timeout"); await Task.Delay(5); }
    }
    static Task Seconds(float seconds) { float end = Enemy.FightSeconds + seconds; return Until(() => Enemy.FightSeconds >= end); }
    static void Shot(string name)
    {
        typeof(KarmaSwordPlayChecks).GetMethod("Snapshot", F).Invoke(null, new object[] { "random-parry2-" + name });
        File.Copy("output/validation/random-parry2-" + name + ".png", Output + "/" + name + ".png", true);
    }
    static KarmaSwordRandomParryChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying) EditorApplication.isPaused = false;
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2) { started = true; Run(); }
    }
    public static void RunBaseline() { Begin(true); }
    public static void RunUpdated() { Begin(false); }
    static void Begin(bool before)
    {
        if (!before) typeof(KarmaSwordPlayChecks).GetMethod("CompilePlayerScripts", F).Invoke(null, null);
        SessionState.SetBool(Key + ".Baseline", before); SessionState.SetBool(Key, true);
        SessionState.SetString(Key + ".Run", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static Keyboard keyboard;
    static string scenario;
    static readonly List<string> debugTrace = new List<string>();
    static async void Run()
    {
        int exit = 0; baseline = SessionState.GetBool(Key + ".Baseline", false);
        runId = SessionState.GetString(Key + ".Run", "missing"); Directory.CreateDirectory(Output);
        try
        {
            Application.runInBackground = true; Application.targetFrameRate = 120; Time.captureDeltaTime = 1f / 60;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            foreach (var type in new[] { typeof(KarmaSwordPlayChecks), typeof(KarmaSwordRetreatDashPlayChecks), typeof(KarmaSwordOverhaulPlayChecks), typeof(KarmaSwordPolishPlayChecks), typeof(KarmaSwordWallCounterChecks) })
                type.GetField("game", F).SetValue(null, game);
            Set(game, "config", UnityEngine.Object.Instantiate(game.Config)); game.Config.playerHealth = 1000000;
            game.Config.BossTuning.sword.sw04DebugLogging = true;
            keyboard = InputSystem.AddDevice<Keyboard>();
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (message.StartsWith("[SW04]")) debugTrace.Add(scenario + " " + message);
                if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert) && stack.Contains("Assets/K4RMA/")) runtimeError = message + "\n" + stack;
            };
            await ParryScenario("A-first-parry", 0, false, false, false);
            await ParryScenario("B-hit1-parry2", 1, false, false, false);
            await ParryScenario("B-hit1-walk-parry2", 1, false, true, false);
            await ParryScenario("B-earlyK1-hit1-K2", 1, false, false, true);
            await ParryScenario("C-jump1-parry2", 1, true, false, false);
            await ParryScenario("D-third-parry", 2, false, false, false);
            await ParryScenario("E-fourth-parry", 3, false, false, false);
            await ParryScenario("F-expired-parry", -1, false, false, true);
            CheckSamples(); await Campaign();
            if (!baseline)
            {
                await CheckGhosts(6); await CheckGhosts(32);
                scenario = "regression";
                typeof(KarmaSwordRetreatDashPlayChecks).GetField("updated", F).SetValue(null, true); Directory.CreateDirectory("output/dash-updated");
                await Invoke(typeof(KarmaSwordRetreatDashPlayChecks), "CheckDash");
                File.Copy("output/dash-updated/dash.json", Output + "/dash.json", true);
                typeof(KarmaSwordWallCounterChecks).GetField("runId", F).SetValue(null, runId); Directory.CreateDirectory("output/wall-counter/" + runId);
                await Invoke(typeof(KarmaSwordWallCounterChecks), "CheckObstacles");
                CheckWallSafety();
                foreach (int wall in new[] { -1, 1 }) await Invoke(typeof(KarmaSwordWallCounterChecks), "CheckRenderedEscape", wall);
                await Invoke(typeof(KarmaSwordWallCounterChecks), "CheckParry", 1, true);
                await Invoke(typeof(KarmaSwordWallCounterChecks), "CheckReuseAndStale");
                await Invoke(typeof(KarmaSwordPlayChecks), "CheckBasics");
                await Invoke(typeof(KarmaSwordPlayChecks), "CheckSwordDamageFeedback");
                await Invoke(typeof(KarmaSwordPlayChecks), "CheckSW04");
                await Invoke(typeof(KarmaSwordPlayChecks), "CheckForcedInterruption");
                typeof(KarmaSwordOverhaulPlayChecks).GetField("updated", F).SetValue(null, true);
                SessionState.SetString("K4RMA.OverhaulChecks.Run", runId); Directory.CreateDirectory("output/overhaul-updated/" + runId);
                foreach (int facing in new[] { -1, 1 }) await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckWaves", facing);
                await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckScars", false);
                await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckScarEscape");
                File.Copy("output/validation/sword-player-compilation.txt", Output + "/player-compilation.txt", true);
                foreach (var file in Directory.GetFiles("output/overhaul-updated/" + runId)) File.Copy(file, Output + "/" + Path.GetFileName(file), true);
                foreach (var file in Directory.GetFiles("output/wall-counter/" + runId)) File.Copy(file, Output + "/" + Path.GetFileName(file), true);
                Log("existing pattern/art/real parry ordered cancellation/reuse/ordinary-hit/stun/death regressions");
            }
            Check(runtimeError == null, "no game runtime errors: " + runtimeError);
            results.Insert(0, "PASS: " + (baseline ? "baseline" : "updated") + " random movement and real K-after-hit SW04 checks");
        }
        catch (Exception e) { results.Insert(0, "FAIL: " + e); Debug.LogException(e); exit = 1; }
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        File.WriteAllLines(Output + "/result.txt", results); File.WriteAllLines(Output + "/sw04-trace.log", debugTrace);
        Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
    [Serializable] sealed class ParryMetrics
    {
        public string name;
        public int target, parries, released, cancelled;
        public bool firstHit, delivered, counterReadyOnK, anyCounterReady;
        public string firstResult, targetResult;
        public float hitInvulnerability, counterWindow, counterCooldown, hit1At, contactAt, kAt, inputObservedAt, cooldownOnK, invulOnK;
        public List<string> trace = new List<string>();
    }
    static object Volley => Get(Counter, "activeVolley");
    static bool CounterReady => ((KarmaTechniqueState)Get(game.Player, "techniques")).CounterReady(Time.time);
    static void Keys(bool k, bool jump, InputKey? movement)
    {
        var keys = new List<InputKey>(); if (k) keys.Add(InputKey.K); if (jump) keys.Add(InputKey.Space); if (movement.HasValue) keys.Add(movement.Value);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
    }
    static bool Near(KarmaBossAttack shot, float seconds)
    {
        if (shot == null || !shot.gameObject.activeInHierarchy || ((KarmaAttackContact)Get(shot, "contact")).Consumed) return false;
        var body = game.Player.GetComponent<Collider2D>().bounds; Vector2 p = shot.transform.position;
        Vector2 velocity = (Vector2)Get(shot, "velocity"), size = (Vector2)Get(shot, "size"), axis = velocity.sqrMagnitude > 0.000001f ? velocity.normalized : Vector2.right;
        return KarmaBossGeometry.SweptOrientedBox(p.x, p.y, p.x + velocity.x * seconds, p.y + velocity.y * seconds,
            body.center.x, body.center.y, axis.x, axis.y, size.x, size.y, body.extents.x, body.extents.y);
    }
    static async Task ParryScenario(string name, int target, bool evadeFirst, bool walkAfterHit, bool earlyFirst)
    {
        scenario = name; Reset(); Keys(false, false, null); Place(35);
        game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
        // Remove only the old test harness's setup invulnerability. Never reset it after a hit.
        Set(game.Player, "invulnerableUntil", 0f);
        Check(game.Player.Essences.HasRemnant(Essence.Guard), "real K ability acquired");
        // Let the newly reset scene/player and native sprite jobs settle before
        // forcing the very first formation in the same editor update.
        await Seconds(0.1f);
        Force(EnemyAttack.SW04); await Until(() => Volley != null); Enemy.TakeDamage(1);
        var volley = Volley; int id = (int)Get(volley, "Id");
        var shots = (KarmaBossAttack[])Get(volley, "Shots"); var contacts = (KarmaAttackContact[])Get(volley, "Contacts");
        var metrics = new ParryMetrics { name = name, target = target + 1, hitInvulnerability = game.Config.hitInvulnerability,
            counterWindow = game.Config.Techniques.counterWindow, counterCooldown = game.Config.Techniques.counterCooldown };
        int traceStart = debugTrace.Count, kUntil = -1, jumpUntil = -1; string frameError = null;
        bool pressed = false, early = false, jumped = false, secondJumped = false, captured = false, parryCaptured = false; float release1 = -1;
        var probe = new GameObject("actual input/flight SW04 scenario").AddComponent<KarmaSwordDashFrameProbe>();
        probe.Before = () =>
        {
            // Keep this isolated SW04 volley intact; no extra boss attacks or root movement.
            if (!(bool)Get(Enemy, "running")) Set(Enemy, "running", true);
            if (shots[0] != null && release1 < 0) release1 = Enemy.FightSeconds;
            metrics.firstHit |= contacts[0].DealtDamage;
            if (metrics.firstHit && metrics.hit1At == 0) metrics.hit1At = Time.time;
            // Jump early enough to land before the second blade commits its aim.
            if (evadeFirst && !jumped && Near(shots[0], 0.60f)) { jumped = true; jumpUntil = Time.frameCount + 1; }
            // Shot two tracks the first jump. Meet that committed airborne path
            // with a second ordinary jump, rather than teleporting into its hitbox.
            if (evadeFirst && jumped && !secondJumped && shots[1] != null && !game.Player.Airborne)
            { secondJumped = true; jumpUntil = Time.frameCount + 1; }
            if (earlyFirst && !early && Near(shots[0], 0.24f)) { early = true; kUntil = Time.frameCount + 1; }
            bool imminent = target >= 0 && Near(shots[target], 0.09f);
            if (walkAfterHit && metrics.firstHit && target == 1 && shots[1] == null && release1 >= 0)
            {
                float next = release1 + 0.35f + Mathf.Max(0.3f, game.Config.BossTuning.sword.counterWarning)
                    + Mathf.Max(0.16f, game.Config.BossTuning.sword.swordGuardResponseCooldown - 0.35f - Mathf.Max(0.3f, game.Config.BossTuning.sword.counterWarning));
                imminent |= Enemy.FightSeconds >= next - 0.08f;
            }
            if (!pressed && imminent)
            {
                pressed = true; kUntil = Time.frameCount + 1;
                metrics.kAt = Time.time; metrics.cooldownOnK = game.Player.Essences.Cooldown(Essence.Guard);
                metrics.invulOnK = Mathf.Max(0, (float)Get(game.Player, "invulnerableUntil") - Time.time);
            }
            InputKey? movement = walkAfterHit && metrics.firstHit && game.Player.Position.x > Enemy.Position.x + 2 ? InputKey.A : (InputKey?)null;
            Keys(Time.frameCount <= kUntil, Time.frameCount <= jumpUntil, movement);
        };
        probe.After = () =>
        {
            metrics.counterReadyOnK |= pressed && CounterReady;
            metrics.anyCounterReady |= CounterReady;
            if (pressed && KarmaInput.Pressed(KarmaAction.Counter)) metrics.inputObservedAt = Time.time;
            if (!captured && metrics.firstHit && CounterReady)
            { captured = true; Shot(name + "-hit1-K"); }
            if (!parryCaptured && game.Player.ParryCount > 0)
            {
                parryCaptured = true;
                for (int later = target + 1; later < 4; later++)
                {
                    var slot = ((Transform[])Get(volley, "Slots"))[later];
                    if (!contacts[later].Consumed || slot != null && slot.gameObject.activeInHierarchy)
                        frameError = "same-frame successful K did not deactivate later collision and visuals";
                }
                Shot(name + "-parry-instant");
            }
        };
        await Until(() => Volley == null, 180);
        probe.Before = probe.After = null; UnityEngine.Object.Destroy(probe.gameObject); Keys(false, false, null);
        Check(frameError == null, frameError);
        metrics.parries = game.Player.ParryCount;
        for (int i = traceStart; i < debugTrace.Count; i++)
        {
            string line = debugTrace[i]; metrics.trace.Add(line);
            if (line.Contains("Result=Released")) metrics.released++;
            if (line.Contains("Cancelled=")) metrics.cancelled++;
            if (line.Contains("Projectile=1, Result=PlayerHit")) metrics.firstResult = "PlayerHit";
            var time = System.Text.RegularExpressions.Regex.Match(line, @"Time=([0-9.]+)");
            if (time.Success && line.Contains("Projectile=1, Result=PlayerHit"))
                metrics.hit1At = float.Parse(time.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (time.Success && line.Contains("Projectile=" + (target + 1) + ", Result=") && !line.Contains("Result=Released"))
                metrics.contactAt = float.Parse(time.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (line.Contains("Projectile=" + (target + 1) + ", Result=ParrySuccess")) metrics.targetResult = "ParrySuccess";
            if (line.Contains("Projectile=" + (target + 1) + ", Result=IgnoredInvulnerability")) metrics.targetResult = "IgnoredInvulnerability";
            if (line.Contains("Projectile=" + (target + 1) + ", Result=PlayerHit")) metrics.targetResult = "PlayerHit";
            metrics.delivered |= line.Contains("Projectile=" + (target + 1) + ", Result=ParryDelivered");
        }
        File.WriteAllText(Output + "/" + name + ".json", JsonUtility.ToJson(metrics, true));
        if (target >= 0) Check(pressed && metrics.inputObservedAt > 0, "actual K reached the game's input action");
        if (target >= 0 && metrics.targetResult == "ParrySuccess")
        { Check(metrics.parries == 1 && metrics.delivered && metrics.cancelled == 3 - target, "real successful K must cancel exact later sequence"); Shot(name + "-cancelled"); }
        if (name == "A-first-parry" || name == "B-hit1-parry2" || name == "C-jump1-parry2" || name == "D-third-parry" || name == "E-fourth-parry")
            Check(metrics.targetResult == "ParrySuccess", name + " actual K success, got " + metrics.targetResult);
        if (name.StartsWith("B-")) Check(metrics.firstHit && metrics.firstResult == "PlayerHit", "first shot really dealt damage");
        if (walkAfterHit) Check(metrics.counterReadyOnK && metrics.targetResult == "IgnoredInvulnerability", "real armed K during hit invulnerability does not fabricate a parry");
        if (name == "B-earlyK1-hit1-K2") Check(metrics.cooldownOnK > 0 && !metrics.counterReadyOnK && metrics.targetResult == "PlayerHit", "actual second K during cooldown is rejected");
        if (evadeFirst) Check(!metrics.firstHit && jumped, "real Space jump evades first shot");
        if (target < 0) Check(metrics.anyCounterReady && metrics.parries == 0 && metrics.cancelled == 0 && metrics.released == 4, "expired K never fabricates success or cancellation");
        Log(name + ": hit1=" + metrics.firstHit + ", target=" + metrics.targetResult + ", parries=" + metrics.parries + ", cancel=" + metrics.cancelled + ", K cooldown=" + metrics.cooldownOnK.ToString("F3") + ", K invul=" + metrics.invulOnK.ToString("F3"));
    }
    static string Kind()
    {
        var field = Movement.GetType().GetField("slideKind", F);
        return field != null ? field.GetValue(Movement).ToString() : (bool)Get(Movement, "swordWallEscaping") ? "WallEscape" : "LongRetreat";
    }
    [Serializable] sealed class MovementMetrics
    {
        public string scenario;
        public int left, right, shortMoves, longMoves, wallEscapes, failures, deferred, sameDirectionMax, attacks;
        public List<float> shortDistances = new List<float>(), longDistances = new List<float>(), escapeDistances = new List<float>();
    }
    static void Tick(float dt)
    {
        typeof(KarmaEnemy).GetProperty("FightSeconds").SetValue(Enemy, Enemy.FightSeconds + dt);
        Call(Behavior, "TickMovement", dt, game.Player.Position.x - Enemy.Position.x);
    }
    static void Record(MovementMetrics m, float direction, string kind, float distance)
    {
        if (direction < 0) m.left++; else m.right++;
        if (kind == "ShortReposition") { m.shortMoves++; m.shortDistances.Add(distance); }
        else if (kind == "WallEscape") { m.wallEscapes++; m.escapeDistances.Add(distance); }
        else { m.longMoves++; m.longDistances.Add(distance); }
        if (distance < 0.1f) m.failures++;
    }
    static void CheckSamples()
    {
        foreach (int side in new[] { -1, 1 }) foreach (bool wall in new[] { false, true })
        {
            Reset(); UnityEngine.Random.InitState(6104 + side);
            var metrics = new MovementMetrics { scenario = (wall ? "wall-" : "center-") + side };
            float last = 0; int repeats = 0;
            for (int sample = 0; sample < 60; sample++)
            {
                float x = wall ? side < 0 ? KarmaStage.BossLeftWall + 0.6f : KarmaStage.BossRightWall - 0.6f : 20;
                Enemy.transform.position = new Vector3(x, KarmaSpace.GuardianGroundY, 0);
                Place(x + (wall ? -side : side) * 3.5f);
                typeof(KarmaEnemy).GetProperty("FightSeconds").SetValue(Enemy, Enemy.FightSeconds + (wall ? 6 : 1.3f));
                Call(Behavior, "OnCompleted"); Tick(1f / 60);
                if ((float)Get(Movement, "swordRetreatRemaining") <= 0) { metrics.deferred++; continue; }
                string kind = Kind(); float direction = (float)Get(Movement, "swordRetreatDirection");
                int frames = 0; do { Tick(1f / 60); Check(++frames < 180, "movement completes"); } while ((float)Get(Movement, "swordRetreatRemaining") > 0);
                Record(metrics, direction, kind, Mathf.Abs(Enemy.Position.x - x));
                repeats = direction == last ? repeats + 1 : 1; last = direction; metrics.sameDirectionMax = Mathf.Max(metrics.sameDirectionMax, repeats);
            }
            if (!baseline)
            {
                if (!wall) Check(metrics.left > 0 && metrics.right > 0 && metrics.shortMoves > 0 && metrics.longMoves > 0, "central mixed movement on either player side");
                else { Check(metrics.wallEscapes == 60, "wall escape sampled sixty times"); foreach (float distance in metrics.escapeDistances) Check(distance > 31.9f && distance <= 32.01f, "only wall escapes reach 32"); }
                foreach (float distance in metrics.shortDistances) Check(distance >= 3.99f && distance <= 8.01f, "short actual travel 4-8");
                foreach (float distance in metrics.longDistances) Check(Mathf.Abs(distance - 16) < 0.01f, "ordinary long stays sixteen");
            }
            File.WriteAllText(Output + "/move-" + metrics.scenario + ".json", JsonUtility.ToJson(metrics, true));
            Log(metrics.scenario + ": L/R=" + metrics.left + "/" + metrics.right + ", short/long/wall=" + metrics.shortMoves + "/" + metrics.longMoves + "/" + metrics.wallEscapes + ", deferred=" + metrics.deferred);
        }
    }
    static async Task Campaign()
    {
        Reset(); Place(16.5f); UnityEngine.Random.InitState(6104); Set(Enemy, "running", false); Set(Enemy, "timer", 0f);
        var m = new MovementMetrics { scenario = "pursuit60" }; float start = Enemy.FightSeconds, nextHit = start + 0.6f, from = 0, lastPattern = -1, lastDir = 0;
        string kind = ""; bool wasMoving = false; int repeat = 0;
        var probe = new GameObject("random movement normal AI pursuit").AddComponent<KarmaSwordDashFrameProbe>();
        probe.Before = () =>
        {
            float target = Mathf.Clamp(Enemy.Position.x - 3.5f, -14, 54);
            float step = Mathf.MoveTowards(game.Player.Position.x, target, game.Config.moveSpeed * Time.deltaTime) - game.Player.Position.x;
            if (Mathf.Abs(step) > 0.00001f) Call(game.Player, "MoveSlash", Vector2.right * Mathf.Sign(step), Mathf.Abs(step));
            if (Enemy.FightSeconds >= nextHit) { Enemy.TakeDamage(0.1f); nextHit += 0.6f; }
        };
        probe.After = () =>
        {
            bool moving = (float)Get(Movement, "swordRetreatRemaining") > 0;
            if (moving && !wasMoving)
            { from = Enemy.Position.x; kind = Kind(); float dir = (float)Get(Movement, "swordRetreatDirection"); repeat = dir == lastDir ? repeat + 1 : 1; lastDir = dir; m.sameDirectionMax = Mathf.Max(m.sameDirectionMax, repeat); }
            if (!moving && wasMoving) Record(m, lastDir, kind, Mathf.Abs(Enemy.Position.x - from));
            wasMoving = moving;
            float pattern = (float)Get(Enemy, "patternStarted");
            if ((bool)Get(Enemy, "running") && pattern != lastPattern) { m.attacks++; lastPattern = pattern; }
        };
        await Until(() => Enemy.FightSeconds - start >= 60); probe.Before = probe.After = null; UnityEngine.Object.Destroy(probe.gameObject);
        Check(m.attacks > 0 && m.left > 0 && m.right > 0 && m.failures == 0, "live pursuit mixes directions and attacks");
        File.WriteAllText(Output + "/move-pursuit60.json", JsonUtility.ToJson(m, true));
        Log("normal pursuit60: L/R=" + m.left + "/" + m.right + ", short/long/wall=" + m.shortMoves + "/" + m.longMoves + "/" + m.wallEscapes + ", attacks=" + m.attacks);
    }
    static async Task CheckGhosts(float distance)
    {
        Reset(); float x = distance > 8 ? KarmaStage.BossRightWall - 0.6f : 20;
        Enemy.transform.position = new Vector3(x, KarmaSpace.GuardianGroundY, 0); Place(x - (distance > 8 ? 3.5f : 8.5f)); await Seconds(0.05f);
        Call(Behavior, "OnCompleted"); Set(Enemy, "running", false); Set(Enemy, "timer", 100f);
        for (int attempt = 0; (float)Get(Movement, "swordRetreatRemaining") <= 0 && attempt < 60; attempt++)
        {
            typeof(KarmaEnemy).GetProperty("FightSeconds").SetValue(Enemy, Enemy.FightSeconds + 0.61f);
            Tick(1f / 60);
        }
        Check((float)Get(Movement, "swordRetreatRemaining") > 0, "requested visual slide starts through normal choice logic");
        var images = Art.GetComponent<KarmaSwordRetreatAfterimages>(); var pool = (Array)Get(images, "pool"); int peak = 0; bool captured = false;
        string actualKind = Kind();
        var probe = new GameObject("random move trail render").AddComponent<KarmaSwordDashFrameProbe>();
        probe.After = () =>
        {
            int active = 0; foreach (var echo in pool) if ((bool)Get(echo, "active")) active++;
            if ((float)Get(Movement, "swordRetreatRemaining") > 0) actualKind = Kind();
            peak = Mathf.Max(peak, active); if (active >= 4 && !captured) { captured = true; Shot("slide-" + distance); }
        };
        await Until(() => (float)Get(Movement, "swordRetreatRemaining") <= 0); probe.After = null; UnityEngine.Object.Destroy(probe.gameObject);
        Check(captured && peak <= pool.Length, "short/32-unit continuous bounded trail");
        Check(actualKind == (distance > 8 ? "WallEscape" : "ShortReposition"), "render captures requested slide type");
        Set(Enemy, "running", true); await Seconds(0.4f); foreach (var echo in pool) Check(!(bool)Get(echo, "active"), "all ghosts expire without accumulation");
        Log("rendered slide " + distance + ": peak ghosts=" + peak + ", pool=" + pool.Length + ", expires");
    }
    static void CheckWallSafety()
    {
        foreach (int wall in new[] { -1, 1 })
        {
            Reset(); float start = wall < 0 ? KarmaStage.BossLeftWall + 0.6f : KarmaStage.BossRightWall - 0.6f;
            Enemy.transform.position = new Vector3(start, KarmaSpace.GuardianGroundY, 0); Place(start - wall * 3.5f);
            var obstacle = new GameObject("limited escape route");
            obstacle.transform.position = new Vector3(start - wall * 14, 3, 0); obstacle.AddComponent<BoxCollider2D>().size = new Vector2(1, 6);
            Physics2D.SyncTransforms(); Call(Behavior, "OnCompleted");
            float previous = start, invulnerability = (float)Get(game.Player, "invulnerableUntil"); int frames = 0;
            do
            {
                Tick(1f / 60); float step = Enemy.Position.x - previous; previous = Enemy.Position.x;
                Check(step * wall <= 0.001f && Mathf.Abs(step) <= 32.01f / 60, "continuous inward slide obeys speed limit");
                Check(++frames < 180, "limited wall escape completes");
            } while ((float)Get(Movement, "swordRetreatRemaining") > 0);
            float distance = Mathf.Abs(Enemy.Position.x - start);
            Check(distance >= 12.4f && distance <= 12.7f, "escape stops at actual obstacle clearance instead of forcing thirty-two");
            Check((float)Get(game.Player, "invulnerableUntil") == invulnerability, "escape never adds invulnerability");
            UnityEngine.Object.DestroyImmediate(obstacle);
            Place(Enemy.Position.x - wall * 3.5f); Call(Behavior, "OnCompleted");
            for (int frame = 0; frame < 120; frame++)
            { previous = Enemy.Position.x; Tick(1f / 60); Check((Enemy.Position.x - previous) * wall <= 0.001f, "post-escape suppresses immediate wallward return"); }
            Log("wall " + wall + ": obstacle-limited actual escape " + distance.ToString("F3") + ", no new invulnerability or immediate wall return");
        }
    }
}
#endif
