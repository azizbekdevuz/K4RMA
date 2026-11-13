#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

[InitializeOnLoad]
public static class KarmaRisingPlayChecks
{
    const string Key = "K4RMA.RisingOwnershipChecks";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static bool started;
    static string error;
    static int assertions;
    static KarmaRisingPlayChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true; EditorApplication.isPaused = false;
        if (!started && Time.frameCount > 2) { started = true; Run(); }
    }
    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static object Get(object target, string name) { return target.GetType().GetField(name, F).GetValue(target); }
    static void Set(object target, string name, object value) { target.GetType().GetField(name, F).SetValue(target, value); }
    static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, F).Invoke(target, args); }
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static async Task Until(Func<bool> done)
    {
        double end = EditorApplication.timeSinceStartup + 90;
        while (!done()) { Check(EditorApplication.timeSinceStartup < end, "Rising pattern timeout"); await Task.Delay(10); }
    }
    static void Reset()
    {
        game.StartRun();
        foreach (var essence in new[] { Essence.RisingSlash, Essence.SwordWave, Essence.Guard })
        { game.Player.Essences.Preview(essence); game.Player.Essences.Confirm(out _); }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
        Call(game, "LoadRoom", true); enemy = game.Enemies[0];
        enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
        var body = game.Player.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
        body.position = game.Player.transform.position = new Vector2(32, KarmaSpace.PlayerHalfHeight + 0.02f);
        Set(game.Player, "invulnerableUntil", Time.time + 1000);
        Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue);
    }
    static void Force(EnemyAttack pattern)
    {
        Call(enemy, "InterruptPattern"); Set(enemy, "selected", pattern); Set(enemy, "direction", 1f);
        Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    public static string CheckPoseConnections()
    {
        var director = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        int count = 0;
        try
        {
            foreach (var order in new[] {
                new[] { Essence.RisingSlash },
                new[] { Essence.RisingSlash, Essence.SwordWave },
                new[] { Essence.RisingSlash, Essence.Guard },
                new[] { Essence.RisingSlash, Essence.SwordWave, Essence.Guard },
                new[] { Essence.RisingSlash, Essence.Guard, Essence.SwordWave } })
            {
                director.StartRun();
                foreach (var kind in order) { director.Player.Essences.Preview(kind); director.Player.Essences.Confirm(out _); }
                typeof(KarmaGame).GetProperty("StageIndex").SetValue(director, order.Length);
                Call(director, "LoadRoom", true);
                var actor = director.Enemies[0]; var animator = actor.GetComponentInChildren<KarmaCharacterAnimator>();
                var renderer = (SpriteRenderer)Get(animator, "picture");
                var expected = order.Length == 1 ? KarmaArtwork.Sprites("Rise")
                    : KarmaArtwork.InheritedPoses(KarmaArtwork.GuardianFormName(order));
                Check(expected.Length >= 12, "Every Rising body has its authored pose library");
                Action<string, EnemyAttack, bool, string> pose = (state, attack, air, name) =>
                {
                    animator.GuardianPose(state, attack, air, false);
                    var sprite = Array.Find(expected, entry => entry.name == name);
                    Check(sprite != null && renderer.sprite == sprite,
                        "Actual Rising Sprite connection: " + string.Join(">", order) + "/" + name + " got=" + renderer.sprite.name);
                    count++;
                };
                pose("Telegraph", EnemyAttack.UP01, false, "Windup");
                pose("Attack", EnemyAttack.UP01, false, "Rising");
                pose("Attack", EnemyAttack.UP02, false, "Slash");
                pose("Attack", EnemyAttack.UP01, true, "AirSlash");
                pose("Recover", EnemyAttack.UP01, false, "Landing");
                pose("Stunned", EnemyAttack.UP01, false, "Hurt");
                animator.transform.position += Vector3.right * 0.2f;
                pose("Approach", EnemyAttack.UP01, false, "Dash");
                pose("Approach", EnemyAttack.UP01, false, "Idle");
            }
            return "PASS: " + count + " actual Rising base/inherited Sprite connections";
        }
        finally { director.StartRun(); }
    }
    static async Task CheckBaseStrike()
    {
        game.StartRun();
        game.Player.Essences.Preview(Essence.RisingSlash);
        game.Player.Essences.Confirm(out _);
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 1);
        Call(game, "LoadRoom", true);
        enemy = game.Enemies[0];
        enemy.transform.position = new Vector3(24, KarmaSpace.GuardianGroundY, 0);
        var body = game.Player.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.position = game.Player.transform.position = new Vector2(18, KarmaSpace.PlayerHalfHeight);
        Set(game.Player, "invulnerableUntil", Time.time + 1000);
        Set(enemy, "engaged", true);
        Set(enemy, "timer", float.MaxValue);
        var animator = enemy.GetComponentInChildren<KarmaCharacterAnimator>();
        var renderer = (SpriteRenderer)Get(animator, "picture");
        var frames = (Sprite[])Get(animator, "risingBasicStrike");
        Check(frames != null && frames.Length == 6, "Base strike loads six authored sprites");
        for (int i = 0; i < frames.Length; i++)
        {
            Check(frames[i].name == $"Strike{i:00}", "Strike frames import in order");
            Check(frames[i].pixelsPerUnit == 125 && frames[i].pivot == new Vector2(270, 20),
                "Strike preserves canonical PPU and a fixed pivot");
        }
        bool first = false, second = false, dash = false;
        var observed = new System.Collections.Generic.HashSet<Sprite>();
        double lastStrike = -1;
        Force(EnemyAttack.GroundStrike);
        await Until(() =>
        {
            double strikeAt = (double)Get(animator, "risingStrikeAt");
            dash |= animator.CurrentPose == "Dash" && strikeAt < 0;
            if (strikeAt >= 0 && animator.CurrentPose == "Slash" && renderer.sprite == frames[0])
            {
                if (!first) { first = true; lastStrike = strikeAt; }
                else second |= strikeAt > lastStrike;
            }
            if (Array.IndexOf(frames, renderer.sprite) >= 0) observed.Add(renderer.sprite);
            return !(bool)Get(enemy, "running");
        });
        Check(dash && first && second, "A visible dash precedes the two actual strike starts");
        Check(observed.Count == 6, "All six strike frames appear during the basic combo");
        Check(((Transform)Get(enemy, "art")).localScale.x < 0, "The existing facing mirrors the new frames");
        Force(EnemyAttack.GroundStrike);
        await Until(() => (double)Get(animator, "risingStrikeAt") >= 0);
        Call(enemy, "InterruptPattern");
        Check((double)Get(animator, "risingStrikeAt") < 0, "Interrupt clears strike playback");
        animator.GuardianPose("Stunned", EnemyAttack.GroundStrike, false, false);
        Check(renderer.sprite.name == "Hurt", "Stun overrides strike playback");
    }
    static EnemyAttack SelectAttack(float distance)
    {
        var behavior = Get(enemy, "behavior");
        object[] args = { distance, true, true, default(EnemyAttack) };
        Check((bool)behavior.GetType().GetMethod("TrySelect", F).Invoke(behavior, args), "Rising selects an attack");
        return (EnemyAttack)args[3];
    }
    static void CheckAttackMix()
    {
        Reset();
        var behavior = Get(enemy, "behavior");
        for (int i = 0; i < 1; i++)
        { Set(enemy, "selected", EnemyAttack.UP01); Call(behavior, "OnSelected"); }
        Check(SelectAttack(18f) == EnemyAttack.GroundStrike, "A skill links into a basic dash at long approach range");
        Set(enemy, "selected", EnemyAttack.GroundStrike); Call(behavior, "OnSelected");
        for (int i = 0; i < 20; i++)
            Check(SelectAttack(6f) != EnemyAttack.UP05, "UP05 is unavailable above half health");
        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.5f);
        Check(SelectAttack(6f) == EnemyAttack.UP05, "Half health prioritizes UP05 despite initial cooldown");
        Set(enemy, "selected", EnemyAttack.UP05); Call(behavior, "OnSelected");
        Check(SelectAttack(6f) != EnemyAttack.UP05, "Half health UP05 is consumed once");
        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.25f);
        Check(SelectAttack(6f) == EnemyAttack.UP05, "Quarter health prioritizes the second UP05");
        Call(behavior, "OnSelected");
        for (int i = 0; i < 20; i++)
            Check(SelectAttack(6f) != EnemyAttack.UP05, "UP05 stays unavailable after both thresholds");
        Reset();
        var body = game.Player.GetComponent<Rigidbody2D>();
        body.position = game.Player.transform.position = new Vector2(24, KarmaSpace.Units(4));
        Check(SelectAttack(4f) == EnemyAttack.UP02, "Airborne player prioritizes UP02 pursuit");
        Debug.Log("PASS: Rising basic skill links, airborne pursuit and once-per-threshold UP05 selection");
    }
    static async void Run()
    {
        int exit = 0; string result;
        try
        {
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 60;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            var config = UnityEngine.Object.Instantiate(game.Config); config.playerHealth = 1000000; Set(game, "config", config);
            Application.logMessageReceived += Capture;
            Debug.Log(CheckPoseConnections());
            await CheckBaseStrike();
            CheckAttackMix();
            foreach (var pattern in new[] { EnemyAttack.GroundStrike, EnemyAttack.UP01, EnemyAttack.UP02, EnemyAttack.UP03, EnemyAttack.UP04, EnemyAttack.UP05 })
            {
                Reset();
                if (pattern == EnemyAttack.UP01 || pattern == EnemyAttack.UP04)
                    enemy.transform.position += Vector3.up * KarmaSpace.Units(3);
                Force(pattern);
                bool pursued = false, shards = false;
                await Until(() =>
                {
                    pursued |= enemy.Position.y > KarmaSpace.GuardianGroundY + KarmaSpace.Units(0.5f);
                    shards |= Array.Exists(((Transform)Get(enemy, "effectsRoot")).GetComponentsInChildren<KarmaBossAttack>(), shot => shot.name == "방패 파편");
                    return !(bool)Get(enemy, "running");
                });
                if (pattern == EnemyAttack.UP02)
                {
                    Check(pursued, "UP02 pursues into the air");
                    Check(shards, "UP02 chains the falling UP01 with shards");
                    var fragmentRoot = (Transform)Get(enemy, "behavior").GetType().GetProperty("PersistentEffects", F).GetValue(Get(enemy, "behavior"));
                    Check(fragmentRoot != null && fragmentRoot.GetComponentsInChildren<KarmaBossAttack>().Length > 0,
                        "Landing fragments survive recovery cleanup");
                    Call(enemy, "InterruptPattern");
                    Check(fragmentRoot.GetComponentsInChildren<KarmaBossAttack>().Length == 0,
                        "Interruption disables remaining landing fragments");
                }
                Set(enemy, "timer", float.MaxValue);
                Check(!enemy.Invulnerable && !enemy.Armored, pattern + " clears defense");
                Check(Mathf.Abs(enemy.Position.y - KarmaSpace.GuardianGroundY) < 0.05f, pattern + " lands");
                Check(enemy.DamageBounds.size.sqrMagnitude > 0, pattern + " preserves damage bounds");
                var animator = enemy.GetComponentInChildren<KarmaCharacterAnimator>();
                animator.GuardianPose("Attack", pattern, true, false);
                Check(animator.CurrentPose == "AirSlash", pattern + " uses local airborne pose");
                animator.GuardianPose("Recover", pattern, false, false);
                Check(animator.CurrentPose == "Landing", pattern + " uses local recovery pose");
                Debug.Log("PASS: Rising " + pattern + " executes and cleans up");
            }
            Reset(); Force(EnemyAttack.UP04); enemy.CancelCombat();
            Check(!enemy.Invulnerable && !enemy.Armored, "interruption clears rising defense");
            Reset(); float health = enemy.Health; enemy.TakeDamage(1);
            Check(enemy.Health < health, "Rising retains normal damage");
            Check(error == null, "no runtime error: " + error);
            result = "PASS: Rising basic/UP01-UP05 execution, local poses, landing, defense cleanup, interruption and damage";
        }
        catch (Exception e) { exit = 1; result = "FAIL: " + e; Debug.LogException(e); }
        Directory.CreateDirectory("output/validation");
        File.WriteAllText("output/validation/rising-play-validation.txt", result + "\n");
        Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
    static void Capture(string message, string stack, LogType type)
    { if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert) && stack.Contains("Assets/Scripts/")) error = message; }
}
#endif
