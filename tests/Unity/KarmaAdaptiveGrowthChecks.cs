#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

[InitializeOnLoad]
public static class KarmaAdaptiveGrowthChecks
{
    const string Key = "Karma.AdaptiveGrowthChecks";
    const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static KarmaGame game;
    static bool started;
    static string evidence;

    static KarmaAdaptiveGrowthChecks() { EditorApplication.update += CheckPlayMode; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }

    static void CheckPlayMode()
    {
        if (!SessionState.GetBool(Key, false)) return;
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if (started || !EditorApplication.isPlaying) return;
        game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        if (!game) return;
        started = true;
        Debug.Log("ADAPTIVE GROWTH START");
        Time.captureFramerate = 60;
        evidence = Path.Combine(Application.dataPath, "../../evidence");
        Directory.CreateDirectory(evidence);
        game.StartCoroutine(Drive());
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, Instance).SetValue(target, value);

    static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Instance).Invoke(target, args);

    static bool Flag(KarmaEnemy enemy, string field) =>
        (bool)typeof(KarmaEnemy).GetField(field, Instance).GetValue(enemy);

    static void CheckContacts(KarmaEnemy enemy, KarmaPlayer player)
    {
        Vector2 point = player.DamageBounds.center;
        float health = player.Health;
        var block = new KarmaAttackContact();
        enemy.TryAttackHit(point, point, Vector2.one, 5, block, 0, plainTechnique: true);
        Assert(block.Consumed && !block.DealtDamage && player.Health == health, "Plain attack bypassed original guard");

        player.ClearStatus();
        Set(player, "guardUntil", 0f);
        var parry = new KarmaAttackContact();
        player.BeginCounter();
        int parries = player.ParryCount;
        enemy.TryAttackHit(point, point, Vector2.one, 5, parry, 0, plainTechnique: true);
        Assert(parry.Consumed && !parry.DealtDamage && player.ParryCount == parries + 1
            && !Flag(enemy, "pendingParry"), "Plain projectile parry was not local");

        Set(player, "invulnerableUntil", 0f);
        var hit = new KarmaAttackContact();
        enemy.TryAttackHit(point, point, Vector2.one, 5, hit, 0, plainTechnique: true);
        enemy.TryAttackHit(point, point, Vector2.one, 5, hit, 0, plainTechnique: true);
        Assert(hit.Consumed && hit.DealtDamage && player.Health == health - 5, "Plain attack contact ledger did not apply one hit");

        Set(player, "invulnerableUntil", 0f);
        player.BeginCounter();
        enemy.TryAttackHit(point, point, Vector2.one, 5, new KarmaAttackContact(), 0, parryStun: true);
        Assert(Flag(enemy, "pendingParry"), "Native parry lost the stun route");
        Call(enemy, "FixedUpdate");
        Assert(enemy.Stunned, "Native stun did not resolve");
    }

    static void DrivePlayerTo(float x)
    {
        var body = game.Player.GetComponent<Rigidbody2D>();
        body.position = game.Player.transform.position = new Vector2(x, KarmaSpace.PlayerHalfHeight + 0.02f);
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    static void OpenCombat()
    {
        Set(game, "guardianInheriting", false);
        typeof(KarmaGame).GetProperty("Phase").SetValue(game, RunPhase.Combat);
    }

    static void Advance(Essence expected)
    {
        Assert(game.Phase == RunPhase.GuardianFallen, "Defeat did not enter the existing ceremony");
        Set(game, "ceremonySpacingStarted", true);
        Set(game, "ceremonySpacingFinished", true);
        Set(game, "transitionLeft", -1f);
        Call(game, "UpdateGuardianFall");
        Assert(game.Phase == RunPhase.Transition && game.LastPersonalizedEssence == expected,
            "Automatic selection did not choose " + expected);
    }

    static IEnumerator Drive()
    {
        yield return null;
        var run = CheckRoutes();
        while (true)
        {
            object next;
            try
            {
                if (!run.MoveNext()) { Finish(0); yield break; }
                next = run.Current;
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); yield break; }
            yield return next;
        }
    }

    static void Finish(int status)
    {
        SessionState.SetBool(Key, false);
        if (status == 0) Debug.Log("ADAPTIVE GROWTH PASS");
        EditorApplication.Exit(status);
    }

    static IEnumerator CheckRoutes()
    {
        game.StartRun();
        var player = game.Player;
        DrivePlayerTo(22);
        player.Essences.TryUse(Essence.Guard);
        Assert(player.Essences.Mastery(Essence.Guard) == 0, "Activation awarded mastery");
        player.TakeDamage(1);
        player.TakeDamage(1);
        Assert(player.Essences.Mastery(Essence.Guard) == game.Config.guardMastery, "Guard duplicate or missing block");
        var enemy = game.Enemies[0];
        Assert(enemy.Alive, "Unexpected defeat");
        CheckContacts(enemy, player);
        Call(player.Essences, "RecordSuccess", Essence.Guard);
        Call(player.Essences, "RecordSuccess", Essence.SwordWave);
        Set(enemy, "timer", 0f);
        float deadline = Time.time + 19;
        Set(player, "invulnerableUntil", deadline);
        while (string.IsNullOrEmpty((string)typeof(KarmaEnemy).GetProperty("LearningLine", Instance).GetValue(enemy))
            && Time.time < deadline) yield return null;
        Assert(Time.time < deadline, "Stage-one guardian did not imitate");
        Assert(((string)typeof(KarmaEnemy).GetProperty("LearningLine", Instance).GetValue(enemy)).StartsWith(KarmaPatternCatalog.Name(Essence.Guard)),
            "Imitation picked the recent hit instead of the mastery leader");
        yield return new WaitForSeconds(1.03f);
        Capture("01-guard-imitation");
        yield return null;
        Call(enemy, "InterruptPattern");
        Assert(!Flag(enemy, "presentingCopy") && !Flag(enemy, "running") && !enemy.Invulnerable
            && enemy.GetComponentInChildren<SpriteRenderer>() != null, "Copy interruption leaked action state");
        Call(player.Essences, "RecordSuccess", Essence.SwordWave);
        Call(player.Essences, "RecordSuccess", Essence.SwordWave);
        Assert(!(bool)Call(enemy, "TryImitate"), "Leader switch ignored the spacing gate");
        Set(enemy, "nextLearningAt", 0f);
        Assert(!(bool)Call(enemy, "TryImitate"), "A copy interrupted the requirement for a native pattern");
        Set(enemy, "completedPatternSinceCopy", true);
        Assert((bool)Call(enemy, "TryImitate"), "New dominant unlearned technique did not trigger a copy");
        var effects = (Transform)typeof(KarmaEnemy).GetField("effectsRoot", Instance).GetValue(enemy);
        yield return new WaitForSeconds(1.03f);
        var copy = effects.GetComponentInChildren<KarmaBossAttack>();
        Assert(copy != null && copy.GetType() == typeof(KarmaBossAttack), "Copy used a specialized projectile");
        Call(enemy, "InterruptPattern");
        Assert(!copy.gameObject.activeInHierarchy && string.IsNullOrEmpty(
            (string)typeof(KarmaEnemy).GetProperty("LearningLine", Instance).GetValue(enemy)),
            "Interruption left a live projectile or dialogue");
        Set(enemy, "nextLearningAt", 0f);
        Set(enemy, "completedPatternSinceCopy", true);
        Assert(!(bool)Call(enemy, "TryImitate"), "An interrupted technique could be baited again");
        game.StartRun();
        Assert(game.Player.Essences.Mastery(Essence.Guard) == 0 && game.Player.Essences.Count == 0, "Restart retained mastery");
        DrivePlayerTo(22);
        var skills = new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard };
        Assert((Essence)typeof(KarmaEssences).GetProperty("MasteryLeader", Instance).GetValue(game.Player.Essences)
            == Essence.SwordWave, "Zero-mastery fallback");
        enemy = game.Enemies[0];
        Set(enemy, "timer", 999f);
        DrivePlayerTo(24);
        game.Player.Essences.TryUse(Essence.SwordWave);
        Assert(game.Player.Essences.Mastery(Essence.SwordWave) == 0, "Wave activation awarded mastery");
        yield return new WaitForSeconds(0.3f);
        Assert(game.Player.Essences.Mastery(Essence.SwordWave) == game.Config.swordWaveMastery,
            "Original projectile hit did not award exactly one score");
        DrivePlayerTo(enemy.Position.x - 1);
        game.Player.Essences.TryUse(Essence.RisingSlash);
        Assert(game.Player.Essences.Mastery(Essence.RisingSlash) == game.Config.risingSlashMastery,
            "Original rising hit did not award exactly one score");
        Assert((Essence)typeof(KarmaEssences).GetProperty("MasteryLeader", Instance).GetValue(game.Player.Essences)
            == Essence.SwordWave, "Tie did not preserve the configured default order");

        foreach (var first in skills)
        foreach (var second in skills)
        {
            if (first == second) continue;
            game.StartRun();
            foreach (var preferred in new[] { first, second })
            {
                enemy = game.Enemies[0];
                DrivePlayerTo(17);
                if (preferred == Essence.Guard)
                {
                    game.Player.Essences.TryUse(Essence.Guard);
                    game.Player.TakeDamage(1);
                    if (game.StageIndex == 1 && first == Essence.SwordWave)
                    {
                        DrivePlayerTo(enemy.Position.x - 5);
                        Set(enemy, "timer", 0f);
                        deadline = Time.time + 20;
                        Set(game.Player, "invulnerableUntil", deadline);
                        while (!(bool)typeof(KarmaEnemy).GetField("presentingCopy", Instance).GetValue(enemy)
                            && Time.time < deadline) yield return null;
                        Assert(Time.time < deadline, "Stage-two guardian did not imitate the next technique");
                        yield return new WaitForSeconds(1.04f);
                        Capture("03-stage-two-imitation");
                        yield return null;
                    }
                }
                else
                {
                    typeof(KarmaEnemy).GetMethod("TakeDamage", Instance, null,
                        new[] { typeof(float), typeof(Vector2), typeof(bool).MakeByRefType(), typeof(bool), typeof(Essence?) }, null)
                        .Invoke(enemy, new object[] { 100000f, game.Player.Position, false, false, preferred });
                    Assert(game.Player.Essences.Mastery(preferred) > 0, "Lethal hit omitted");
                }
                if (enemy.Alive) enemy.TakeDamage(100000);
                Advance(preferred);
                if (first == Essence.SwordWave && preferred == first)
                {
                    yield return null;
                    Capture("02-automatic-personalization");
                    yield return null;
                }
                Set(game, "transitionLeft", 0f);
                Call(game, "UpdatePersonalization");
                OpenCombat();
                Assert(game.Enemies[0].PrimaryType == first && game.Player.Essences.Mastery(preferred) == 0, "Wrong specialization or round reset");
            }

            bool representative = first == Essence.SwordWave;
            enemy = game.Enemies[0];
            DrivePlayerTo(enemy.Position.x - 6);
            var last = Array.Find(skills, skill => skill != first && skill != second);
            if (representative || first == Essence.Guard)
            {
                deadline = Time.time + 15;
                while (!Flag(enemy, "finalTechniqueReleased") && Time.time < deadline) yield return null;
                Assert(Flag(enemy, "finalTechniqueReleased") && enemy.Health == enemy.MaxHealth,
                    "Stage three did not demonstrate at an early combat opportunity");
                if (last == Essence.Guard)
                {
                    Assert(enemy.Invulnerable, "Mastered guard did not raise its shield");
                    enemy.TakeDamage(100000);
                    Assert(enemy.Health == enemy.MaxHealth, "Mastered guard no longer blocks damage");
                }
                if (representative || last == Essence.SwordWave)
                {
                    yield return new WaitForSeconds(0.14f);
                    Capture("04-final-technique-" + last);
                    yield return null;
                }
                while (Flag(enemy, "presentingCopy") && Time.time < deadline) yield return null;
                Assert(!enemy.Invulnerable, "Demonstration left the shield raised");
            }
            else if (second == Essence.Guard)
            {
                typeof(KarmaEnemy).GetProperty("FightSeconds").SetValue(enemy, 3f);
                Call(enemy, "BeginAttack");
                Assert(Flag(enemy, "presentingCopy") && !Flag(enemy, "finalTechniqueReleased"),
                    "Expected a Stage-three windup before the lethal hit");
            }
            enemy.TakeDamage(100000);
            Assert(!enemy.Alive, "Stage-three lethal hit was deferred");
            Assert(!Flag(enemy, "presentingCopy"), "Lethal hit did not cancel the demonstration");
            Advance(last);
            Assert(game.LastPersonalizedEssence == last && game.Player.Essences.Count == 3, "Third technique did not resolve");
            Set(game, "transitionLeft", 0f);
            Call(game, "UpdatePersonalization");
            Assert(game.StageIndex == 3 && game.Enemies[0].PrimaryType == first, "Final specialization changed");
            if (representative && second == Essence.Guard)
            {
                yield return null;
                Capture("05-final-inheritance");
                yield return null;
                OpenCombat();
                enemy = game.Enemies[0];
                DrivePlayerTo(17);
                Set(game.Player, "invulnerableUntil", Time.time + 25);
                enemy.TakeDamage(enemy.MaxHealth * 0.62f, enemy.Position);
                Set(enemy, "timer", 0f);
                Set(enemy, "nextUltimate", 0f);
                deadline = Time.time + 22;
                while (GameObject.Find("Mastery stance recall") == null && Time.time < deadline)
                    yield return null;
                Assert(Time.time < deadline, "Stage four did not begin its inherited ultimate");
                yield return new WaitForSeconds(0.2f);
                Capture("06-combined-ultimate");
                yield return null;
            }
            Debug.Log("ADAPTIVE ORDER PASS " + first + " / " + second + " / " + last);
        }
    }

    static void Capture(string name)
    {
        ScreenCapture.CaptureScreenshot(Path.Combine(evidence, name + "-hud.png"));
        var camera = game.GameCamera;
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        var target = RenderTexture.GetTemporary(960, 540, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        File.WriteAllBytes(Path.Combine(evidence, name + ".png"), texture.EncodeToPNG());
        UnityEngine.Object.Destroy(texture);
        camera.targetTexture = previous;
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(target);
    }
}
#endif
