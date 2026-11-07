#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class KarmaRisingMeleeChecks
{
    const string Key = "K4RMA.RisingMeleeChecks";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static bool started;
    static string error;
    static readonly List<string> results = new List<string>();
    static object Get(object o, string n) => o.GetType().GetField(n, F).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, F).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, F).Invoke(o, a);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static KarmaRisingMeleeChecks()
    {
        // Play Mode reloads the domain and runs IndexationOnStartup. A disposable
        // project has no search database, so that call throws and batch mode exits 1.
        if (RequestsPlayMode()) SetSearchStartup(false);
        EditorApplication.update += Pump;
    }
    static bool RequestsPlayMode()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != "-executeMethod") continue;
            string method = args[i + 1];
            return method.EndsWith("RunBatch") || method.EndsWith("RunCandidateBatch") || method.EndsWith("RunStreakBatch");
        }
        return false;
    }
    public static void RestoreEditorSearch()
    {
        SetSearchStartup(true);
        EditorApplication.Exit(0);
    }
    static void SetSearchStartup(bool enabled)
    {
        try
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var settings = assembly.GetType("UnityEditor.Search.SearchSettings");
                var prop = settings?.GetProperty("indexOnEditorStartup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool)) prop.SetValue(null, enabled);
            }
        }
        catch (Exception) { }
    }
    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying && !started && Time.frameCount > 2) { started = true; Run(); }
    }
    static async Task Frame()
    {
        int frame = Time.frameCount;
        double until = EditorApplication.timeSinceStartup + 45;
        while (frame == Time.frameCount)
        { Check(EditorApplication.timeSinceStartup < until, "player loop stalled"); await Task.Delay(1); }
    }
    static void PlayerAt(float x, float y = 1.57f)
    {
        var body = game.Player.GetComponent<Rigidbody2D>();
        body.position = game.Player.transform.position = new Vector2(x, y);
        body.linearVelocity = Vector2.zero;
    }
    static async Task Reset(RisingBasicVariant variant, bool inherited = false, float x = 20, float facing = 1)
    {
        game.StartRun();
        game.Player.Essences.Preview(Essence.RisingSlash); game.Player.Essences.Confirm(out _);
        if (inherited) { game.Player.Essences.Preview(Essence.Guard); game.Player.Essences.Confirm(out _); }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
        Call(game, "LoadRoom", true); enemy = game.Enemies[0];
        Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue);
        game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        game.Config.BossTuning.rising.basicVariant = variant;
        game.Config.BossTuning.rising.guardBasicAccent = true;
        PlayerAt(x + facing * 7);
        await Frame();
        enemy.transform.position = new Vector2(x, KarmaSpace.GuardianGroundY);
        Set(game.Player, "invulnerableUntil", 0f);
        Set(game.Player, "guardUntil", 0f);
        Call(enemy, "InterruptPattern");
        Set(enemy, "selected", EnemyAttack.GroundStrike); Set(enemy, "direction", facing);
        Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static async Task<float> Combo(RisingBasicVariant variant, int fps, bool inherited = false, float face = 1, bool evade = false, bool parry = false, bool wall = false)
    {
        Time.captureDeltaTime = 1f / fps;
        float x = wall ? (face > 0 ? KarmaStage.BossRightWall - 0.1f : KarmaStage.BossLeftWall + 0.1f) : 20;
        await Reset(variant, inherited, x, face);
        var seen = new HashSet<KarmaBossAttack>();
        var contacts = new HashSet<object>();
        int hits = 0, previousParries = game.Player.ParryCount;
        float health = game.Player.Health, lastHealth = health, recoveryAt = -1;
        float start = Time.time;
        Transform effects = (Transform)Get(enemy, "effectsRoot");
        Vector3 scale = effects.localScale;
        var poses = new HashSet<string>();
        while ((bool)Get(enemy, "running"))
        {
            Check(Time.time - start < 5, "combo timeout");
            Check(enemy.Facing == face, "facing changed after commitment");
            Check(enemy.Position.x >= KarmaStage.BossLeftWall - .001f && enemy.Position.x <= KarmaStage.BossRightWall + .001f, "wall penetration");
            Check(effects.localScale == scale, "effects root scale changed");
            poses.Add(enemy.GetComponentInChildren<KarmaCharacterAnimator>().CurrentPose);
            var attacks = effects.GetComponentsInChildren<KarmaBossAttack>();
            foreach (var attack in attacks)
            {
                seen.Add(attack); contacts.Add(Get(attack, "contact"));
                // Each actual hitbox is tested with a positioned target. This isolates ledgers
                // from the shared player's longer hit invulnerability and from player skill.
                var center = attack.transform.position;
                if (!evade) PlayerAt(center.x, center.y);
                Check(!attack.Visual.gameObject.activeSelf, "duplicate static attack art enabled");
            }
            if (evade) PlayerAt(enemy.Position.x - face * 8);
            if (parry) game.Player.BeginCounter();
            else Set(game.Player, "invulnerableUntil", 0f);
            if (Get(enemy, "state").ToString() == "Recover")
            {
                if (recoveryAt < 0) recoveryAt = Time.time;
                Check(attacks.Length == 0, "damaging object remains in recovery");
            }
            await Frame();
            if (game.Player.Health < lastHealth) hits++;
            lastHealth = game.Player.Health;
        }
        int expected = variant == RisingBasicVariant.DashTwoCut ? 2 : 3;
        Check(seen.Count == expected && contacts.Count == expected, "hitbox/contact count " + seen.Count + "/" + contacts.Count);
        Check(evade || parry ? hits == 0 : hits == expected, "damage event count " + hits + " expected " + expected);
        if (parry) Check(game.Player.ParryCount > previousParries, "K parry did not resolve");
        Check(recoveryAt > 0 && Time.time - recoveryAt >= .35f, "counterattack window too short");
        Check(poses.Contains("Windup") && poses.Contains("Slash") && poses.Contains("Landing"), "missing body pose progression: " + string.Join(",", poses));
        float enemyHealth = enemy.Health; enemy.TakeDamage(1);
        Check(enemy.Health < enemyHealth, "counterattack damage blocked");
        await Frame();
        Check(effects.GetComponentsInChildren<KarmaBossAttack>().Length == 0, "attack leak");
        results.Add("PASS: " + variant + " " + fps + "fps face=" + face + " guard=" + inherited + " evade=" + evade + " parry=" + parry + " wall=" + wall + " hits=" + hits);
        return health - game.Player.Health;
    }
    static async void Run()
    {
        int exit = 0;
        try
        {
            Application.runInBackground = true;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            var config = UnityEngine.Object.Instantiate(game.Config); config.playerHealth = 100000;
            Set(game, "config", config);
            Application.logMessageReceived += (m,s,t) => { if (t == LogType.Error || t == LogType.Exception) error = m; };
            foreach (int fps in new[] { 30, 60, 120 })
            {
                await Combo(RisingBasicVariant.AdvancingThreeHit, fps);
                await Combo(RisingBasicVariant.DashTwoCut, fps);
            }
            float plain = await Combo(RisingBasicVariant.DashTwoCut, 60);
            float guard = await Combo(RisingBasicVariant.DashTwoCut, 60, true);
            Check(guard > plain && guard < plain * 1.1f, "Guard accent not bounded to second cut");
            await Combo(RisingBasicVariant.DashTwoCut, 60, face: -1);
            await Combo(RisingBasicVariant.DashTwoCut, 60, evade: true);
            await Combo(RisingBasicVariant.DashTwoCut, 60, parry: true);
            await Combo(RisingBasicVariant.DashTwoCut, 60, wall: true);
            await Combo(RisingBasicVariant.DashTwoCut, 60, face: -1, wall: true);
            foreach (float cancelAt in new[] { .1f, .4f, .65f })
            {
                await Reset(RisingBasicVariant.DashTwoCut);
                float start = Time.time;
                while (Time.time - start < cancelAt) await Frame();
                enemy.CancelCombat(); await Frame();
                var effects = (Transform)Get(enemy, "effectsRoot");
                Check(effects.childCount == 0, "interruption leaked effects");
                Check(!enemy.Invulnerable && !enemy.Armored, "interruption leaked defense");
                results.Add("PASS: cancel at " + cancelAt);
            }
            await Reset(RisingBasicVariant.DashTwoCut);
            var root = (Transform)Get(enemy, "effectsRoot");
            enemy.TakeDamage(enemy.MaxHealth * 2); await Frame(); await Frame();
            Check(root == null, "death leaked effects root");
            results.Add("PASS: death cleanup");
            Check(error == null, "runtime error: " + error);
        }
        catch (Exception e) { exit = 1; results.Add("FAIL: " + e); }
        Directory.CreateDirectory("output/validation");
        File.WriteAllText("output/validation/rising-melee-validation.txt", (exit == 0 ? "PASS" : "FAIL") + ": Rising melee\n" + string.Join("\n", results));
        Time.captureDeltaTime = 0; SetSearchStartup(true); SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
}
#endif
