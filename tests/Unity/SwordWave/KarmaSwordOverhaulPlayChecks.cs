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
        Reset(); Place(20); Set(game.Player, "invulnerableUntil", 0f); Force(EnemyAttack.SW03);
        float health = game.Player.Health, target = 20, warningAt = 0; int escapes = 0;
        bool warning = false; string failure = null;
        var probe = new GameObject("SW03 real movement escape observer").AddComponent<KarmaSwordDashFrameProbe>();
        probe.Before = () =>
        {
            try
            {
                var centers = new System.Collections.Generic.List<float>();
                foreach (Transform child in Root) if (child.gameObject.activeInHierarchy && child.name.StartsWith("SW03 sword-cut warning")) centers.Add(child.position.x);
                if (centers.Count == 10 && !warning)
                {
                    warning = true; warningAt = enemy.FightSeconds; float nearest = float.PositiveInfinity;
                    var candidates = new System.Collections.Generic.List<float> { game.Player.Position.x };
                    foreach (float center in centers) { candidates.Add(center - 4.75f - KarmaSpace.PlayerHalfWidth - 0.21f); candidates.Add(center + 4.75f + KarmaSpace.PlayerHalfWidth + 0.21f); }
                    foreach (float point in candidates)
                    {
                        if (point < -15 + KarmaSpace.PlayerHalfWidth + 0.2f || point > 55 - KarmaSpace.PlayerHalfWidth - 0.2f) continue;
                        bool safe = true; foreach (float center in centers) safe &= Mathf.Abs(point - center) >= 4.75f + KarmaSpace.PlayerHalfWidth + 0.199f;
                        float distance = Mathf.Abs(point - game.Player.Position.x);
                        if (safe && distance < nearest) { nearest = distance; target = point; }
                    }
                    Check(!float.IsInfinity(nearest), "independent geometry finds body-wide escape");
                    if (nearest > 0.5f) escapes++;
                }
                if (centers.Count == 0) warning = false;
                if (warning && enemy.FightSeconds - warningAt >= 0.28f)
                {
                    float step = Mathf.MoveTowards(game.Player.Position.x, target, game.Config.moveSpeed * 0.8f * Time.deltaTime) - game.Player.Position.x;
                    if (Mathf.Abs(step) > 0.00001f) Call(game.Player, "MoveSlash", Vector2.right * Mathf.Sign(step), Mathf.Abs(step));
                }
            } catch (Exception e) { failure = e.ToString(); }
        };
        await Until(() => !(bool)Get(enemy, "running") || failure != null); probe.Before = null; UnityEngine.Object.Destroy(probe.gameObject);
        Check(failure == null && game.Player.Health == health, "SW03 0.28-second reaction then real collision-checked walking evades every round: " + failure);
        Check(escapes >= 2, "SW03 forces repeated movement, rather than a standing safe pocket");
        Set(enemy, "running", true); results.Add("PASS: SW03 real 11.2-unit/s collision-checked walking after 0.28-second reaction evades all rounds; " + escapes + " forced relocations, invulnerability disabled");
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
        Reset(); Place(-13.8f);
        if (enraged) typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.1f);
        Force(EnemyAttack.SW03);
        var seen = new System.Collections.Generic.HashSet<KarmaBossAttack>(); int rounds = 0; bool rising = false, warningShot = false;
        var centers = new System.Collections.Generic.HashSet<float>();
        while ((bool)Get(enemy, "running"))
        {
            typeof(KarmaSwordPlayChecks).GetMethod("RepeatedOrdinaryDamage", F).Invoke(null, null);
            var attacks = Attacks();
            if (attacks.Length == 0)
            {
                centers.Clear();
                foreach (Transform child in Root) if (child.gameObject.activeInHierarchy && child.name.StartsWith("SW03 sword-cut warning"))
                { centers.Add(child.position.x); Check(child.GetComponent<SpriteRenderer>().sprite.name == "SW03_GroundScar_v5", "new crack warning"); }
                if (centers.Count == 10 && !warningShot) { warningShot = true; Shot("SW03-warning-" + enraged); }
                rising = false;
            }
            else if (!rising)
            {
                rising = true; rounds++; Check(attacks.Length == 20 && centers.Count == 10, "ten warned pairs / twenty simultaneous blades");
                var contacts = new System.Collections.Generic.Dictionary<KarmaAttackContact, int>();
                foreach (var attack in attacks)
                {
                    seen.Add(attack); Vector2 size = (Vector2)Get(attack, "size");
                    Check(size == new Vector2(4.75f, 30) && attack.Visual.sprite.name == "SW03_RisingBlade_v5", "larger 4.75x30 v5 blades");
                    float x = attack.transform.position.x;
                    Check(x - size.x / 2 >= -15 && x + size.x / 2 <= 55, "full blade in arena");
                    bool match = false; foreach (float center in centers) match |= Mathf.Abs(Mathf.Abs(x - center) - 2.375f) < 0.001f;
                    Check(match, "pair matches locked ground warning");
                    var contact = (KarmaAttackContact)Get(attack, "contact");
                    contacts[contact] = contacts.TryGetValue(contact, out int count) ? count + 1 : 1;
                }
                Check(contacts.Count == 10, "shared pair ledger prevents seam duplicate damage");
                foreach (int count in contacts.Values) Check(count == 2, "exactly two per contact ledger");
                Shot("SW03-rise-" + enraged + "-" + rounds);
            }
            await Task.Delay(5);
        }
        Check(rounds == 5 && seen.Count == rounds * 20, "five normal/enraged full rounds under repeated incoming damage");
        Set(enemy, "running", true);
        results.Add("PASS: SW03 " + (enraged ? "enraged" : "normal") + " 100 blades, five rounds, warnings/size/bounds/shared ledgers/cleanup");
    }
    static async Task CheckScarHitAndCancel()
    {
        Reset(); Force(EnemyAttack.SW03); await Seconds(0.1f); Call(enemy, "InterruptPattern");
        Check(Root.GetComponentsInChildren<SpriteRenderer>().Length == 0, "interrupt clears crack warnings");
        Force(EnemyAttack.SW03); await Until(() => Attacks().Length == 20);
        Place(Attacks()[0].transform.position.x); float health = game.Player.Health; await Seconds(0.08f);
        Check(game.Player.Health == health && Root.Find("SW03 confirmed hit") == null, "invulnerability has no false hit flash");
        Force(EnemyAttack.SW03); await Until(() => Attacks().Length == 20);
        Place(Attacks()[0].transform.position.x); Set(game.Player, "invulnerableUntil", 0f); health = game.Player.Health;
        await Until(() => game.Player.Health < health && Root.Find("SW03 confirmed hit") != null);
        Check(Root.Find("SW03 confirmed hit").GetComponentInChildren<SpriteRenderer>().sprite.name == "SW03_HitFlash_v5", "confirmed actual damage emits v5 hit flash");
        Shot("SW03-confirmed-hit"); var root = Root; enemy.TakeDamage(enemy.MaxHealth * 10); await Task.Delay(100);
        Check(root == null || root.GetComponentsInChildren<KarmaBossAttack>().Length == 0, "death clears contacts");
        results.Add("PASS: SW03 v5 flash only on actual damage; invulnerability/miss suppress flash; interruption/death cleanup");
    }
}
#endif
