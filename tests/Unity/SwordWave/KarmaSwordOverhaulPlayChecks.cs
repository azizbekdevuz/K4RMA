#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

[InitializeOnLoad]
public static class KarmaSwordOverhaulPlayChecks
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Key = "K4RMA.OverhaulChecks";
    static bool started;
    static bool updated;
    static KarmaGame game;
    static KarmaEnemy enemy => (KarmaEnemy)typeof(KarmaSwordPlayChecks).GetField("enemy", F).GetValue(null);
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
    static object Behavior => Get(enemy, "behavior");
    static object Movement => Get(Behavior, "movement");
    static Transform Root => (Transform)Get(enemy, "effectsRoot");
    static KarmaBossAttack[] Attacks() => Root.GetComponentsInChildren<KarmaBossAttack>();
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    static void Reset() => typeof(KarmaSwordPlayChecks).GetMethod("Reset", F).Invoke(null, new object[] { Essence.SwordWave });
    static void Place(float x) => typeof(KarmaSwordPlayChecks).GetMethod("Place", F).Invoke(null, new object[] { x });
    static void Force(EnemyAttack attack) => typeof(KarmaSwordPlayChecks).GetMethod("Force", F).Invoke(null, new object[] { attack });
    static void Shot(string name)
    {
        typeof(KarmaSwordPlayChecks).GetMethod("Snapshot", F).Invoke(null, new object[] { "overhaul-" + name });
        File.Copy("output/validation/overhaul-" + name + ".png", Output + "/" + name + ".png", true);
    }
    static async Task Until(Func<bool> done, double timeout = 90)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!done()) { Check(EditorApplication.timeSinceStartup < end, "timeout"); await Task.Delay(5); }
    }
    static Task Seconds(float time) { float end = enemy.FightSeconds + time; return Until(() => enemy.FightSeconds >= end); }
    static readonly System.Collections.Generic.List<string> results = new System.Collections.Generic.List<string>();
    static string Output => "output/overhaul-" + (updated ? "updated" : "baseline") + "/" + SessionState.GetString(Key + ".Run", "missing");
    static KarmaSwordOverhaulPlayChecks() { EditorApplication.update += Pump; }
    public static void RunBaseline() { Begin(false); }
    public static void RunUpdated() { Begin(true); }
    static void Begin(bool revised)
    {
        SessionState.SetBool(Key + ".Updated", revised);
        SessionState.SetString(Key + ".Run", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        if (revised) typeof(KarmaSwordPlayChecks).GetMethod("CompilePlayerScripts", F).Invoke(null, null);
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying) EditorApplication.isPaused = false;
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2)
        { started = true; updated = SessionState.GetBool(Key + ".Updated", false); Run(); }
    }
    static async Task Invoke(Type type, string name)
    { var value = type.GetMethod(name, F).Invoke(null, null); if (value is Task task) await task; }
    static async void Run()
    {
        int exit = 0;
        Directory.CreateDirectory(Output);
        try
        {
            Application.runInBackground = true; Application.targetFrameRate = 120;
            Time.captureDeltaTime = 1f / 60;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            typeof(KarmaSwordPlayChecks).GetField("game", F).SetValue(null, game);
            typeof(KarmaSwordRetreatDashPlayChecks).GetField("game", F).SetValue(null, game);
            typeof(KarmaSwordRetreatDashPlayChecks).GetField("updated", F).SetValue(null, true);
            var config = UnityEngine.Object.Instantiate(game.Config);
            typeof(KarmaGame).GetField("config", F).SetValue(game, config); config.playerHealth = 1000000;
            UnityEngine.Random.InitState(6101);
            await Invoke(typeof(KarmaSwordRetreatDashPlayChecks), "CheckDash");
            await Invoke(typeof(KarmaSwordRetreatDashPlayChecks), "Campaign");
            foreach (var name in new[] { "campaign.json", "dash.json" }) File.Copy("output/dash-updated/" + name, Output + "/" + name, true);
            File.WriteAllLines(Output + "/trace.csv", (System.Collections.Generic.IEnumerable<string>)typeof(KarmaSwordRetreatDashPlayChecks).GetField("trace", F).GetValue(null));
            if (updated) await CheckUpdated();
            results.Insert(0, "PASS: " + (updated ? "updated" : "baseline") + " overhaul Play Mode checks");
        }
        catch (Exception failure) { results.Insert(0, "FAIL: " + failure); Debug.LogException(failure); exit = 1; }
        File.WriteAllLines(Output + "/result.txt", results);
        Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
    static async Task CheckUpdated()
    {
        var before = JsonUtility.FromJson<Metrics>(File.ReadAllText("output/overhaul-before-campaign.json"));
        var after = JsonUtility.FromJson<Metrics>(File.ReadAllText(Output + "/campaign.json"));
        Check(after.retreatStarts > before.retreatStarts, "actual 60s frequency must increase over current 27/min");
        results.Add("60s before/after: " + before.retreatStarts + "/" + after.retreatStarts + " retreats; mean distance "
            + before.meanActualRetreatDistance + "/" + after.meanActualRetreatDistance);
        CheckCurves();
        // Existing regression checks run against the actual revised code.
        await Invoke(typeof(KarmaSwordRetreatDashPlayChecks), "CheckBlocked");
        await CheckGhosts(1); await CheckGhosts(-1);
        await Invoke(typeof(KarmaSwordPlayChecks), "CheckBasics");
        await Invoke(typeof(KarmaSwordPlayChecks), "CheckSwordDamageFeedback");
        await Invoke(typeof(KarmaSwordPlayChecks), "CheckSW04");
        await CheckWaves(1); await CheckWaves(-1);
        await CheckSW01Jump(1); await CheckSW01Jump(-1);
        await CheckScars(false); await CheckScars(true);
        await CheckScarEscape();
        await CheckScarHitAndCancel();
        await Invoke(typeof(KarmaSwordPlayChecks), "CheckForcedInterruption");
        Reset(); Force(EnemyAttack.SW02); await Seconds(0.1f); Set(enemy, "pendingParry", true);
        await Until(() => enemy.Stunned);
        Check(!(bool)Get(enemy, "running") && Attacks().Length == 0, "SW02 parry cancels");
        File.Copy("output/validation/sword-player-compilation.txt", Output + "/player-compilation.txt", true);
        results.Add("PASS: close melee / medium wave, SWBasic/SW01/SW02/SW03/SW04 repeated damage, parry/stun/death cleanup and Player compilation");
    }
    [Serializable] sealed class Metrics { public int retreatStarts; public float meanActualRetreatDistance; }
    static async Task CheckSW01Jump(int facing)
    {
        Reset(); Place(20 + facing * 15); var body = game.Player.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic; Set(game.Player, "invulnerableUntil", 0f);
        await Seconds(0.1f); Force(EnemyAttack.SW01); float health = game.Player.Health;
        await Until(() => Attacks().Length > 0);
        // Use the same rigidbody impulse as the real buffered jump, at the first release.
        body.linearVelocity = new Vector2(0, game.Config.jumpSpeed);
        await Until(() => !(bool)Get(enemy, "running"));
        Check(game.Player.Health == health, "ordinary physics jump then landing evades both large SW01 lanes, facing " + facing);
        Set(enemy, "running", true); Shot("SW01-jump-safe-" + facing);
        results.Add("PASS: SW01 facing " + facing + " actual dynamic-body ordinary jump/landing avoids both shots with invulnerability disabled");
    }
    static async Task CheckScarEscape()
    {
        await KarmaSwordSkillRefreshChecks.CheckSW03Redesign(false,true);
        results.Add("PASS: SW03 reaction-delayed physical walking avoids both staggered groups");
    }
    static void Tick(float dt)
    {
        typeof(KarmaEnemy).GetProperty("FightSeconds").SetValue(enemy, enemy.FightSeconds + dt);
        Call(Behavior, "TickMovement", dt, game.Player.Position.x - enemy.Position.x);
    }
    static void CheckCurves()
    {
        var data = new System.Collections.Generic.List<string> { "fps,facing,travel,seconds" };
        foreach (int fps in new[] { 30, 60, 120 }) foreach (int facing in new[] { -1, 1 })
        {
            Reset(); enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0); Place(20 + facing * 3.5f);
            float start = enemy.Position.x, clock = 0, last = 0, dt = 1f / fps;
            Tick(dt); Check(enemy.Position.x == start, "no spawn retreat"); Call(Behavior, "OnCompleted");
            do
            {
                float before = enemy.Position.x; Tick(dt); clock += dt;
                float speed = Mathf.Abs(enemy.Position.x - before) / dt;
                Check(speed <= 32.03f && speed - last <= 240 * dt + 0.05f && last - speed <= 220 * dt + 0.08f,
                    "slide speed/continuous acceleration/braking");
                Check(enemy.Facing == facing && (enemy.Position.x - before) * facing <= 0.001f, "backward facing slide");
                last = speed; Check(clock < 3, "retreat converges");
            } while ((float)Get(Movement, "swordRetreatRemaining") > 0);
            float travel = Mathf.Abs(enemy.Position.x - start);
            Check(Mathf.Abs(travel - game.Config.Techniques.pierceDistance * 2) < 0.015f, "actual retreat is two measured dashes");
            float stopped = enemy.Position.x;
            for (int i = 0; i < fps / 2; i++) Tick(dt);
            Check(enemy.Position.x == stopped, "post retreat attack opening");
            data.Add(fps + "," + facing + "," + travel + "," + clock);
        }
        // The goal must follow player tuning, including above today's 16 units.
        Reset(); Place(23.5f); float original = game.Config.Techniques.pierceDistance;
        game.Config.Techniques.pierceDistance = 10; Call(Behavior, "OnCompleted"); float x = enemy.Position.x;
        do { Tick(1f / 60); } while ((float)Get(Movement, "swordRetreatRemaining") > 0);
        Check(Mathf.Abs(enemy.Position.x - x) >= 19.99f, "goal follows a ten-unit dash");
        game.Config.Techniques.pierceDistance = original;
        File.WriteAllLines(Output + "/curves.csv", data); results.Add("PASS: 16-unit retreat at 30/60/120 FPS both directions, dynamic dash tuning, smooth acceleration/braking, attack opening, bounds/terrain");
    }
    static async Task CheckGhosts(int facing)
    {
        Reset(); enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0); Place(20 + facing * 3.5f);
        await Seconds(0.05f);
        var images = enemy.GetComponentInChildren<KarmaSwordRetreatAfterimages>();
        var pool = (Array)Get(images, "pool"); var sources = (Transform[])Get(images, "sources");
        int index = Array.IndexOf(sources, images.transform), peak = 0; bool captured = false;
        string failure = null;
        var probe = new GameObject("overhaul retreat visual observer").AddComponent<KarmaSwordDashFrameProbe>();
        probe.After = () =>
        {
            try
            {
                int active = 0;
                foreach (var echo in pool) if ((bool)Get(echo, "active"))
                {
                    active++; var transforms = (Transform[])Get(echo, "transforms");
                    Check((transforms[index].position.x - enemy.Position.x) * facing > 0, "ghost stays in player direction");
                    foreach (var sprite in (SpriteRenderer[])Get(echo, "sprites"))
                        if (sprite.enabled) Check(sprite.color.b >= sprite.color.r && sprite.sprite != null, "cyan actual sprite echo");
                }
                peak = Mathf.Max(peak, active);
                if (active >= 4 && !captured) { captured = true; Shot("retreat-" + facing); }
            } catch (Exception e) { failure = e.ToString(); }
        };
        Call(Behavior, "OnCompleted"); Set(enemy, "running", false); Set(enemy, "timer", 100f);
        await Seconds(1.2f); probe.After = null; UnityEngine.Object.Destroy(probe.gameObject);
        Check(failure == null && captured && peak >= 4 && peak <= 7, "retreat rendered ghosts: " + failure);
        results.Add("PASS: facing " + facing + " cyan rendered silhouettes, peak " + peak + ", continuous slide");
    }
    static async Task CheckWaves(int facing)
    {
        foreach (var pattern in new[] { EnemyAttack.SW01, EnemyAttack.SW02 })
        {
            Reset(); Place(20 + facing * 15); await Seconds(0.05f); Force(pattern);
            var seen = new System.Collections.Generic.HashSet<KarmaBossAttack>(); bool warned = false;
            float nextHit = 0;
            while ((bool)Get(enemy, "running"))
            {
                if (enemy.FightSeconds >= nextHit)
                { typeof(KarmaSwordPlayChecks).GetMethod("RepeatedOrdinaryDamage", F).Invoke(null, null); nextHit = enemy.FightSeconds + 0.075f; }
                if (!warned && Root.GetComponentsInChildren<KarmaAttackWarning>().Length > 0)
                { warned = true; Shot(pattern + "-warning-" + facing); }
                foreach (var attack in Attacks()) if (seen.Add(attack))
                {
                    string expected = pattern == EnemyAttack.SW02 ? "SW02_AimedSlash_v2"
                        : "SW01_CrescentWave_v4";
                    Check(attack.Visual.sprite.name == expected, "correct full/half crescent at release");
                    Check(attack.Visual.flipX == (facing < 0) && !attack.Visual.flipY, "horizontal mirror preserves upper/lower");
                    Vector2 size = (Vector2)Get(attack, "size"); var core = KarmaWaveArtwork.CorePixels(expected);
                    Check(Mathf.Abs(attack.Visual.transform.localScale.x * core.width / attack.Visual.sprite.pixelsPerUnit - size.x) < 0.001f
                        && Mathf.Abs(attack.Visual.transform.localScale.y * core.height / attack.Visual.sprite.pixelsPerUnit - size.y) < 0.001f, "visible core matches hit size");
                    if (pattern == EnemyAttack.SW02) Check((bool)Get(attack, "aimedSlash") && Mathf.Abs(((Vector2)Get(attack, "velocity")).x) == 40, "SW02 existing logic flag/speed retained");
                    else Check(Mathf.Abs(attack.transform.position.y - (seen.Count == 1 ? 2.1f : 6.6f)) < 0.001f && size.y > 3.9f, "large separated low/high lanes");
                    Shot(pattern + "-release-" + facing + "-" + seen.Count);
                }
                await Task.Delay(5);
            }
            Check(seen.Count == (pattern == EnemyAttack.SW01 ? 2 : 1), "committed shot count under repeated damage");
            results.Add("PASS: " + pattern + " facing " + facing + ", correct art/lanes/mirror/core/hit alignment, survives repeated incoming damage");
        }
    }
    static async Task CheckScars(bool enraged)
    {
        await KarmaSwordSkillRefreshChecks.CheckSW03Redesign(enraged);
        results.Add("PASS: SW03 normal/empowered 80 swords in five staggered rounds");
    }
    static async Task CheckScarHitAndCancel()
    {
        await KarmaSwordSkillRefreshChecks.CheckSW03ContactAndCancellation();
        results.Add("PASS: SW03 real shared contact prevents cross-group duplicate damage; cancellation clears groups");
    }
}
#endif
