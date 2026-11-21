#if UNITY_EDITOR
using System;
using System.Linq;
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
    static void Reset(params Essence[] order)
    {
        if (order.Length == 0) order = new[] { Essence.RisingSlash, Essence.SwordWave, Essence.Guard };
        game.StartRun();
        foreach (var essence in order)
        { game.Player.Essences.Preview(essence); game.Player.Essences.Confirm(out _); }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, order.Length);
        Call(game, "LoadRoom", true); enemy = game.Enemies[0];
        enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
        var body = game.Player.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
        body.position = game.Player.transform.position = new Vector2(32, KarmaSpace.PlayerHalfHeight + 0.02f);
        Set(game.Player, "invulnerableUntil", Time.time + 1000);
        Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue);
    }
    static void Force(EnemyAttack pattern, float height = 0)
    {
        Call(enemy, "InterruptPattern"); Set(enemy, "selected", pattern); Set(enemy, "direction", 1f);
        if (height > 0) enemy.transform.position += Vector3.up * height;
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
                    string form = KarmaArtwork.GuardianFormName(order);
                    string motion = state == "Attack" ? attack.ToString() : state == "Stunned" ? "Stun" : name;
                    var sprite = renderer.sprite;
                    bool connected = motion == "Idle" ? sprite == KarmaArtwork.StageIdle(form)
                        : Array.IndexOf(KarmaArtResource.Frames("Motions12/" + form + "/" + motion), sprite) >= 0;
                    Check(connected,
                        "Actual Rising Sprite connection: " + string.Join(">", order) + "/" + name + " got=" + renderer.sprite.name);
                    count++;
                };
                pose("Telegraph", EnemyAttack.UP01, false, "Windup");
                pose("Attack", EnemyAttack.UP01, false, "Rising");
                pose("Attack", EnemyAttack.UP02, false, "Slash");
                pose("Attack", EnemyAttack.UP01, true, "AirSlash");
                pose("Recover", EnemyAttack.UP01, false, "Landing");
                if (Array.IndexOf(order, Essence.SwordWave) >= 0)
                    pose("Recover", EnemyAttack.UP03, false, "Idle");
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
        var frames = Frames("Rise", "UPBasic");
        Check(frames.Length == 6, "Base strike loads six authored drawings");
        var observed = new System.Collections.Generic.List<int>();
        bool dash = false;
        var strikes = new System.Collections.Generic.HashSet<double>();
        Force(EnemyAttack.GroundStrike);
        await Until(() =>
        {
            double strikeAt = (double)Get(animator, "risingStrikeAt");
            dash |= animator.CurrentPose == "Dash" && renderer.sprite.texture.name == "Dash";
            if (strikeAt >= 0) strikes.Add(strikeAt);
            int index = Array.IndexOf(frames, renderer.sprite);
            if (index >= 0 && (observed.Count == 0 || observed[observed.Count-1] != index)) observed.Add(index);
            return !(bool)Get(enemy, "running");
        });
        Check(dash && strikes.Count == 2, "Actual dash and both strike events appear in the basic combo");
        Check(observed.SequenceEqual(Enumerable.Range(0, frames.Length)), "Ordered in-game basic combo: " + string.Join(",", observed));
        Check(((Transform)Get(enemy, "art")).localScale.x < 0, "Existing facing mirrors the new frames");
        Force(EnemyAttack.UP01, KarmaSpace.Units(4));
        await ObserveClip(renderer, "Rise", "UP01", "UP01Landing");

        Force(EnemyAttack.GroundStrike);
        await Until(() => (double)Get(animator, "risingStrikeAt") >= 0);
        Call(enemy, "InterruptPattern");
        Check((double)Get(animator, "risingStrikeAt") < 0, "Interrupt clears strike playback");
        animator.GuardianPose("Stunned", EnemyAttack.GroundStrike, false, false);
        Check(Frames("Rise", "Stun").Contains(renderer.sprite), "Stun overrides strike playback");
    }
    static Sprite[] Frames(string form, string motion) =>
        KarmaArtResource.Frames("Motions12/" + form + "/" + motion)
            .OrderBy(frame => frame.name, StringComparer.Ordinal).ToArray();

    static async Task ObserveClip(SpriteRenderer renderer, string form, string motion, string landing = null)
    {
        var frames = Frames(form, motion);
        var ground = landing == null ? Array.Empty<Sprite>() : Frames(form, landing);
        Check(frames.Length >= 2 && (landing == null || ground.Length > 0), form + "/" + motion + " resources");
        var observed = new System.Collections.Generic.List<int>();
        var landed = new System.Collections.Generic.List<int>();
        var complete = new TaskCompletionSource<bool>();
        game.StartCoroutine(AtRender());
        await complete.Task;
        IEnumerator AtRender()
        {
            var endOfFrame = new WaitForEndOfFrame();
            double deadline = EditorApplication.timeSinceStartup + 90;
            while (true)
            {
                yield return endOfFrame;
                try
                {
                    int frame = Array.IndexOf(frames, renderer.sprite);
                    if (frame >= 0 && (observed.Count == 0 || observed[observed.Count-1] != frame)) observed.Add(frame);
                    int contact = Array.IndexOf(ground, renderer.sprite);
                    if (contact >= 0 && observed.Count > 0)
                    {
                        Check(Mathf.Abs(enemy.Position.y - KarmaSpace.GuardianGroundY) < 0.05f, form + "/" + landing + " requires ground contact");
                        if (landed.Count == 0 || landed[landed.Count-1] != contact) landed.Add(contact);
                    }
                    if (!(bool)Get(enemy, "running")) { complete.SetResult(true); yield break; }
                    Check(EditorApplication.timeSinceStartup < deadline, form + "/" + motion + " timeout");
                }
                catch (Exception ex) { complete.SetException(ex); yield break; }
            }
        }
        Check(observed.Contains(0) && observed.Contains(1), form + "/" + motion + " actual starting frames: " + string.Join(",",observed));
        if (form == "RiseWave" && motion == "UP03")
            Check(observed.SequenceEqual(new[] { 0, 1, 2, 3 }), "RiseWave UP03 plays the sword transition in order");
        for (int i = 1; i < observed.Count; i++)
            Check(observed[i] == 0 || observed[i] > observed[i-1], form + "/" + motion + " forward playback");
        if (landing != null)
            Check(landed.Count > 0 && landed[0] == 0 && landed.SequenceEqual(landed.OrderBy(i => i)), form + "/" + landing + " landing playback");
    }

    static async Task CheckCrossingEffects(SpriteRenderer renderer, string form)
    {
        var frames = Frames(form, "UP05");
        int crossings = 0, seenFrames = 0;
        bool reachedStart = false, onRight = false;
        Force(EnemyAttack.UP05);
        await Until(() =>
        {
            if (!(bool)Get(enemy, "running")) return true;
            var barrier = enemy.GetComponentInChildren<KarmaRisingBarrier>();
            if (barrier == null || barrier.Opacity <= 0) return false;
            int frame = Array.IndexOf(frames, renderer.sprite);
            Check(frame >= 0 && renderer.enabled, "UP05 shield and dash effect must be visible together: " + renderer.sprite.name);
            float x = enemy.Position.x;
            reachedStart |= x <= KarmaStage.ArenaLeft + 4.1f;
            if (reachedStart)
            {
                seenFrames |= 1 << frame;
                if ((x > KarmaStage.ArenaCenter) != onRight)
                {
                    onRight = !onRight;
                    crossings++;
                    Check(seenFrames == (1 << frames.Length) - 1, "UP05 effect frames at crossing " + crossings);
                    seenFrames = 0;
                }
            }
            return false;
        });
        Check(crossings == 8 && !frames.Contains(renderer.sprite) && enemy.GetComponentInChildren<KarmaRisingBarrier>() == null,
            "UP05 completes eight crossings and clears both effects");
    }

    static async Task CheckFormClips()
    {
        foreach (var order in new[] {
            new[] { Essence.RisingSlash },
            new[] { Essence.RisingSlash, Essence.SwordWave },
            new[] { Essence.RisingSlash, Essence.Guard },
            new[] { Essence.RisingSlash, Essence.SwordWave, Essence.Guard },
            new[] { Essence.RisingSlash, Essence.Guard, Essence.SwordWave } })
        {
            Reset(order);
            string form = KarmaArtwork.GuardianFormName(order);
            var renderer = (SpriteRenderer)Get(enemy.GetComponentInChildren<KarmaCharacterAnimator>(), "picture");
            var body = game.Player.GetComponent<Rigidbody2D>();
            enemy.transform.position = new Vector3(16, KarmaSpace.GuardianGroundY, 0);
            body.position = game.Player.transform.position = new Vector2(39, KarmaSpace.Units(4));
            Force(EnemyAttack.UP02);
            await ObserveClip(renderer, form, "UP02");
            EnemyAttack drop = Array.IndexOf(order, Essence.Guard) >= 0 ? EnemyAttack.UP04 : EnemyAttack.UP01;
            Force(drop, KarmaSpace.Units(4));
            await ObserveClip(renderer, form, drop.ToString(), drop == EnemyAttack.UP01 ? "UP01Landing" : "Landing");
            if (Array.IndexOf(order, Essence.SwordWave) >= 0)
            {
                Force(EnemyAttack.UP03);
                await ObserveClip(renderer, form, "UP03");
            }
            if (order.Length == 3)
                await CheckCrossingEffects(renderer, form);
            Debug.Log("PASS: " + form + " in-game 12 FPS clip coverage (" + string.Join(">", order) + ")");
        }
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
            await CheckFormClips();
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
            result = "PASS: Rising 12 FPS all-form playback, basic/UP01-UP05 execution, landing, defense cleanup, interruption and damage";
        }
        catch (Exception e) { exit = 1; result = "FAIL: " + e; Debug.LogException(e); }
        Directory.CreateDirectory("output/validation");
        File.WriteAllText("output/validation/rising-play-validation.txt", result + "\n");
        Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
    static void Capture(string message, string stack, LogType type)
    { if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert) && stack.Contains("Assets/Runtime/")) error = message; }
}
#endif
