#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

[InitializeOnLoad]
public static class KarmaSwordPolishPlayChecks
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Key = "K4RMA.SwordPolish";
    static bool started;
    static KarmaGame game;
    static string runId;
    static string runtimeError;
    static int checks;
    static readonly List<string> results = new List<string>();
    static string Output => "output/polish/" + runId;
    static KarmaEnemy Enemy => (KarmaEnemy)typeof(KarmaSwordPlayChecks).GetField("enemy", F).GetValue(null);
    static object Behavior => Get(Enemy, "behavior");
    static object Movement => Get(Behavior, "movement");
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
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Log(string message) { results.Add("PASS: " + message); Debug.Log(message); }
    static async Task Until(Func<bool> done, double seconds = 180)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (!done()) { Check(EditorApplication.timeSinceStartup < end, "Play Mode timeout"); await Task.Delay(5); }
    }
    static Task Seconds(float seconds) { float end = Enemy.FightSeconds + seconds; return Until(() => Enemy.FightSeconds >= end); }
    static void Shot(string name)
    {
        typeof(KarmaSwordPlayChecks).GetMethod("Snapshot", F).Invoke(null, new object[] { "polish-" + name });
        File.Copy("output/validation/polish-" + name + ".png", Output + "/" + name + ".png", true);
    }
    static KarmaSwordPolishPlayChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying) EditorApplication.isPaused = false;
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2) { started = true; Run(); }
    }
    public static void RunBatch()
    {
        typeof(KarmaSwordPlayChecks).GetMethod("CompilePlayerScripts", F).Invoke(null, null);
        SessionState.SetString(Key + ".Run", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static async void Run()
    {
        int exit = 0;
        runId = SessionState.GetString(Key + ".Run", "missing"); Directory.CreateDirectory(Output);
        try
        {
            Application.runInBackground = true; Application.targetFrameRate = 120; Time.captureDeltaTime = 1f / 60;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            foreach (var type in new[] { typeof(KarmaSwordPlayChecks), typeof(KarmaSwordRetreatDashPlayChecks), typeof(KarmaSwordOverhaulPlayChecks) })
                type.GetField("game", F).SetValue(null, game);
            Set(game, "config", UnityEngine.Object.Instantiate(game.Config)); game.Config.playerHealth = 1000000;
            typeof(KarmaSwordOverhaulPlayChecks).GetField("updated", F).SetValue(null, true);
            SessionState.SetString("K4RMA.OverhaulChecks.Run", runId);
            Directory.CreateDirectory("output/overhaul-updated/" + runId);
            Application.logMessageReceived += (message, stack, type) =>
            { if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert) && stack.Contains("Assets/Scripts/")) runtimeError = message + "\n" + stack; };
            CheckDirections(); CheckLedge(); await Campaign();
            await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckCurves");
            await Invoke(typeof(KarmaSwordRetreatDashPlayChecks), "CheckBlocked");
            await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckGhosts", 1);
            await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckGhosts", -1);
            await Invoke(typeof(KarmaSwordPlayChecks), "CheckBasics");
            await Invoke(typeof(KarmaSwordPlayChecks), "CheckSwordDamageFeedback");
            await Invoke(typeof(KarmaSwordPlayChecks), "CheckSW04");
            await CheckBasicArt(1); await CheckBasicArt(-1);
            await CheckDamageFlash();
            foreach (int facing in new[] { -1, 1 })
            {
                await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckWaves", facing);
                await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckSW01Jump", facing);
            }
            await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckScars", false);
            await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckScars", true);
            await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckScarEscape");
            await Invoke(typeof(KarmaSwordOverhaulPlayChecks), "CheckScarHitAndCancel");
            await Invoke(typeof(KarmaSwordPlayChecks), "CheckForcedInterruption");
            Reset(); Force(EnemyAttack.SW02); await Seconds(0.1f); Set(Enemy, "pendingParry", true);
            await Until(() => Enemy.Stunned); Check(!(bool)Get(Enemy, "running"), "SW02 parry cancels");
            Check(runtimeError == null, "no game runtime exception: " + runtimeError);
            var reused = "output/overhaul-updated/" + runId;
            if (Directory.Exists(reused)) foreach (var file in Directory.GetFiles(reused)) File.Copy(file, Output + "/" + Path.GetFileName(file), true);
            foreach (var type in new[] { typeof(KarmaSwordPlayChecks), typeof(KarmaSwordOverhaulPlayChecks) })
                foreach (string message in (List<string>)type.GetField("results", F).GetValue(null)) results.Add(message);
            File.Copy("output/validation/sword-player-compilation.txt", Output + "/player-compilation.txt", true);
            Log("regressions: SWBasic melee/waves; SW01 two complete crescents and physics jump; SW02 original logic; SW03 normal/enraged/escape/hit; SW04; forced parry/stun/death; both-side ghosts");
            results.Insert(0, "PASS: Sword polish Play Mode, " + checks + " assertions plus reused regression assertions");
        }
        catch (Exception failure) { results.Insert(0, "FAIL: " + failure); Debug.LogException(failure); exit = 1; }
        File.WriteAllLines(Output + "/result.txt", results); Time.captureDeltaTime = 0;
        SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
    static bool Choose(float dx, out float direction, out float distance)
    {
        object[] args = { dx, 0f, 0f }; bool result = (bool)Call(Movement, "ChooseRetreat", args);
        direction = (float)args[1]; distance = (float)args[2]; return result;
    }
    static void History(float direction, int repeats)
    { Set(Movement, "lastRetreatDirection", direction); Set(Movement, "sameDirectionRetreats", repeats); Set(Movement, "lastRetreatAt", Enemy.FightSeconds); }
    static void CheckDirections()
    {
        Reset(); UnityEngine.Random.InitState(6102);
        foreach (int side in new[] { -1, 1 })
        {
            Place(20 + side * 3.5f); History(0, 0);
            Check(Choose(side * 3.5f, out float direction, out float distance) && direction == -side && distance > 15.9f, "retreat opens spacing without crossing player");
            History(direction, 2); Set(Movement, "swordAttackedSinceRetreat", true);
            Check(!Choose(side * 3.5f, out _, out _) && !(bool)Get(Movement, "swordAttackedSinceRetreat"), "two recent same-side retreats yield an extra attack");
            Place(20 - side * 3.5f);
            Check(Choose(-side * 3.5f, out float opposite, out _) && opposite == side, "player flank change allows immediate opposite retreat");
        }
        Place(20); int left = 0, right = 0;
        for (int sample = 0; sample < 160; sample++)
        {
            History(-1, 0); Check(Choose(0, out float direction, out _), "aligned safe dual choice");
            if (direction < 0) left++; else right++;
        }
        Check(left > 0 && right > left * 2, "both viable directions randomize with anti-repeat preference");
        foreach (int side in new[] { -1, 1 })
        {
            float x = side < 0 ? KarmaStage.BossLeftWall + 0.6f : KarmaStage.BossRightWall - 0.6f;
            Enemy.transform.position = new Vector3(x, KarmaSpace.GuardianGroundY, 0); Place(x - side * 3.5f); History(0, 0);
            Check(!Choose(-side * 3.5f, out _, out _), "wall never forces a retreat through the player");
        }
        Log("direction choice: both attacker sides, safe flanks, same-side anti-repeat attack, wall rejection; aligned random draw left/right " + left + "/" + right);
    }
    static void CheckLedge()
    {
        Reset(); Place(16.5f);
        var enabled = new List<Collider2D>(); GameObject platform = null;
        try
        {
            foreach (var surface in UnityEngine.Object.FindObjectsByType<KarmaSurface>())
                foreach (var collider in surface.GetComponents<Collider2D>()) if (collider.enabled) { enabled.Add(collider); collider.enabled = false; }
            platform = new GameObject("retreat ledge fixture"); platform.transform.position = new Vector3(20, -0.25f, 0);
            platform.AddComponent<KarmaSurface>(); platform.AddComponent<BoxCollider2D>().size = new Vector2(8, 0.5f); Physics2D.SyncTransforms(); History(0, 0);
            Check(Choose(-3.5f, out float direction, out float distance) && direction > 0 && distance <= 4, "route clearance stops at ledge before a long retreat");
        }
        finally { if (platform != null) UnityEngine.Object.DestroyImmediate(platform); foreach (var collider in enabled) if (collider != null) collider.enabled = true; Physics2D.SyncTransforms(); }
        Log("ledge route preflight limits travel; existing per-step wall/floor/boundary checks retained");
    }
    [Serializable] sealed class Metrics { public float seconds = 40; public int leftRetreats, rightRetreats, attacks, maxRetreatChain; }
    static async Task Campaign()
    {
        Reset(); await Seconds(0.05f); UnityEngine.Random.InitState(6102);
        float start = Enemy.FightSeconds, nextHit = start + 0.6f, lastPattern = -1;
        bool wasRetreating = false; int chain = 0; string error = null;
        var metrics = new Metrics(); var trace = new List<string> { "seconds,bossX,playerX,retreating,pattern" };
        var probe = new GameObject("sword polish pursuit").AddComponent<KarmaSwordDashFrameProbe>();
        probe.Before = () =>
        {
            float elapsed = Enemy.FightSeconds - start;
            int side = Mathf.FloorToInt(elapsed / 8) % 2 == 0 ? -1 : 1;
            if (Enemy.Position.x < KarmaStage.BossLeftWall + 3) side = -1;
            if (Enemy.Position.x > KarmaStage.BossRightWall - 3) side = 1;
            float target = Mathf.Clamp(Enemy.Position.x + side * 3.5f, -14, 54);
            float step = Mathf.MoveTowards(game.Player.Position.x, target, game.Config.moveSpeed * Time.deltaTime) - game.Player.Position.x;
            if (Mathf.Abs(step) > 0.00001f) Call(game.Player, "MoveSlash", Vector2.right * Mathf.Sign(step), Mathf.Abs(step));
            if (Enemy.FightSeconds >= nextHit) { Enemy.TakeDamage(0.1f); nextHit += 0.6f; }
        };
        probe.After = () =>
        {
            try
            {
                float pattern = (float)Get(Enemy, "patternStarted");
                if ((bool)Get(Enemy, "running") && pattern != lastPattern) { metrics.attacks++; lastPattern = pattern; chain = 0; }
                bool retreating = (float)Get(Movement, "swordRetreatRemaining") > 0;
                if (retreating && !wasRetreating)
                {
                    if ((float)Get(Movement, "swordRetreatDirection") < 0) metrics.leftRetreats++; else metrics.rightRetreats++;
                    metrics.maxRetreatChain = Mathf.Max(metrics.maxRetreatChain, ++chain);
                }
                Check(Enemy.Position.x >= KarmaStage.BossLeftWall && Enemy.Position.x <= KarmaStage.BossRightWall, "live pursuit arena bounds");
                wasRetreating = retreating;
                trace.Add((Enemy.FightSeconds - start) + "," + Enemy.Position.x + "," + game.Player.Position.x + "," + retreating + "," + Get(Enemy, "selected"));
            } catch (Exception e) { error = e.ToString(); }
        };
        Set(Enemy, "running", false); Set(Enemy, "timer", 0f);
        await Until(() => Enemy.FightSeconds - start >= 40 || error != null);
        probe.Before = probe.After = null; UnityEngine.Object.Destroy(probe.gameObject);
        Check(error == null && metrics.leftRetreats > 0 && metrics.rightRetreats > 0 && metrics.attacks > 0 && metrics.maxRetreatChain <= 1, "live pursuit mixes both safe directions with attacks: " + error);
        File.WriteAllText(Output + "/campaign.json", JsonUtility.ToJson(metrics, true)); File.WriteAllLines(Output + "/trace.csv", trace);
        Call(Enemy, "InterruptPattern"); Set(Enemy, "running", true);
        Log("40-second real AI pursuit: left/right retreats " + metrics.leftRetreats + "/" + metrics.rightRetreats + ", attacks " + metrics.attacks + ", max retreat chain " + metrics.maxRetreatChain);
    }
    static async Task CheckBasicArt(int facing)
    {
        Reset(); Place(20 + facing * 9); Force(EnemyAttack.SWBasic);
        await Until(() => Root.GetComponentsInChildren<KarmaBossAttack>().Length > 0);
        var attack = Root.GetComponentsInChildren<KarmaBossAttack>()[0];
        Check(attack.Visual.sprite.name == "SWBasic_CrescentWave_v3", "new basic PNG really used");
        Check((Vector2)Get(attack, "size") == new Vector2(2.16f, 1), "basic damage size unchanged");
        Shot("basic-" + facing); Log("basic art facing " + facing + ": new compact crescent, unchanged reach/speed/hit size");
    }
    static async Task CheckDamageFlash()
    {
        Reset(); Force(EnemyAttack.SW01); await Seconds(0.08f);
        float pattern = (float)Get(Enemy, "patternStarted"); Vector2 root = Enemy.Position;
        SpriteRenderer overlay = null; bool captured = false; string failure = null;
        var probe = new GameObject("confirmed hit rendered frame observer").AddComponent<KarmaSwordDashFrameProbe>();
        probe.After = () =>
        {
            try
            {
                var node = Art.Find("Sword confirmed damage silhouette");
                if (node == null || captured) return;
                overlay = node.GetComponent<SpriteRenderer>();
                var source = Art.Find("Guardian sprite").GetComponent<SpriteRenderer>();
                Check(overlay.enabled && overlay.color.a > 0.5f && overlay.sprite == source.sprite && overlay.sharedMaterial.name == "SwordWave/Shared/KarmaSwordRetreatEcho", "whole-body visible cyan confirmed-hit pulse");
                Check(overlay.transform.localPosition == source.transform.localPosition && overlay.transform.localRotation == source.transform.localRotation, "flash follows real rendered pose after LateUpdate");
                Check(Enemy.Position == root && (float)Get(Enemy, "patternStarted") == pattern && (bool)Get(Enemy, "running") && !Enemy.Stunned, "visible hit never restarts/interrupts/moves attack");
                captured = true; Shot("boss-confirmed-hit");
            } catch (Exception error) { failure = error.ToString(); }
        };
        Enemy.TakeDamage(1);
        await Until(() => captured || failure != null); probe.After = null; UnityEngine.Object.Destroy(probe.gameObject);
        Check(failure == null && captured, "rendered hit feedback: " + failure);
        await Seconds(0.24f); Check(!overlay.enabled, "flash expires without restoring over animation");
        Reset(); Force(EnemyAttack.SW01); await Seconds(0.05f);
        float shieldMultiplier = game.Config.BossTuning.sword.shieldDamageMultiplier;
        game.Config.BossTuning.sword.shieldDamageMultiplier = 0; Set(Get(Behavior, "counter"), "Guarding", true); Enemy.TakeDamage(1); await Seconds(0.04f);
        Check(Art.Find("Sword confirmed damage silhouette") == null, "zero actual damage never flashes");
        game.Config.BossTuning.sword.shieldDamageMultiplier = shieldMultiplier;
        Reset(); Force(EnemyAttack.SW05); await Seconds(0.05f); Enemy.TakeDamage(1); await Seconds(0.04f);
        Check(Art.GetComponent<KarmaSwordDamageFlash>() == null, "SW05 receives no new overlay behavior");
        Log("confirmed damage silhouette: visible body pulse, rendered-pose follow, finite lifetime, no animation/root/coroutine changes; zero damage and SW05 excluded");
    }
}
#endif
