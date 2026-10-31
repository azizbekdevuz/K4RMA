#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using KarmaPrototype;
using KarmaPrototype.Editor;

// Live Update + real K input; captures unmodified Unity Game view frames.
public static class KarmaStaggerPlayChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static int checks;
    static string runtimeError;
    static readonly List<string> log = new List<string>();
    static readonly Essence[][] Orders = {
        new[] { Essence.Guard }, new[] { Essence.Guard, Essence.SwordWave }, new[] { Essence.Guard, Essence.RisingSlash },
        new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash }, new[] { Essence.Guard, Essence.RisingSlash, Essence.SwordWave }
    };
    static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, Private).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); log.Add("PASS " + label); }
    static async Task Until(Func<bool> condition, float timeout = 5)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!condition()) { if (EditorApplication.timeSinceStartup > end) throw new Exception("Timeout: " + enemy.Intent); await Task.Delay(5); }
    }
    static Task Seconds(float seconds) { float end = enemy.FightSeconds + seconds; return Until(() => enemy.FightSeconds >= end, seconds + 4); }
    static async Task Reset(int form, int facing)
    {
        game.StartRun();
        foreach (var essence in Orders[form]) game.Player.Essences.Sacrifice(essence);
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, Orders[form].Length);
        Call(game, "LoadRoom", true); enemy = game.Enemies[0];
        enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
        var body = game.Player.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
        body.position = new Vector2(20 + facing * KarmaSpace.Units(2), KarmaSpace.Units(1));
        game.Player.transform.position = body.position;
        Set(game.Player, "invulnerableUntil", 0f); Set(enemy, "engaged", true);
        Set(enemy, "timer", float.MaxValue); Set(enemy, "nextShield", float.MaxValue);
        if (facing > 0) typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.4f);
        await Task.Delay(100);
    }
    static void Force(int facing, EnemyAttack pattern = EnemyAttack.GD02)
    {
        Call(enemy, "InterruptPattern"); Set(enemy, "selected", pattern); Set(enemy, "direction", (float)facing);
        Get<Transform>(enemy, "art").localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
        Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
        if (pattern == EnemyAttack.GD02) Call(enemy, "RefreshAutoShield");
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static void Capture(string path)
    {
        Type type = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        { type = assembly.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands"); if (type != null) break; }
        if (type == null) throw new Exception("Official capture command required");
        EditorApplication.isPaused = true;
        try
        {
            var result = type.GetMethod("GameView", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { 1280, 720, null, null, false, 0, "screen" });
            File.WriteAllBytes(path, Convert.FromBase64String((string)result.GetType().GetProperty("Base64").GetValue(result)));
        }
        finally { EditorApplication.isPaused = false; }
    }
    static void Error(string message, string stack, LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            && (stack.Contains("KarmaPrototype") || message.Contains("OverrideGeometry"))) runtimeError = message + stack;
    }
    public static async Task<string> Run()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        KarmaGuardPractice.Stop(); EditorApplication.isPaused = false;
        game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        var original = game.Config; var config = UnityEngine.Object.Instantiate(original);
        config.playerHealth = 100000; Set(game, "config", config);
        var background = InputSystem.settings.backgroundBehavior;
        var inputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        var updateMode = InputSystem.settings.updateMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        checks = 0; log.Clear(); runtimeError = null; Application.logMessageReceived += Error;
        Directory.CreateDirectory("output/validation/stagger");
        try
        {
            int frame = Time.frameCount; await Task.Delay(120);
            Check(Time.frameCount > frame, "native player loop advances");
            for (int form = 0; form < Orders.Length; form++)
            foreach (int facing in new[] { -1, 1 })
            {
                string key = form == 0 ? "Ward" : form == 1 ? "WardWave" : form == 2 ? "WardRise" : "WardFinal";
                string label = key + (form == 4 ? "Reverse" : "") + (facing < 0 ? "-left" : "-right-enraged");
                await Reset(form, facing);
                var art = Get<Transform>(enemy, "art"); var picture = art.GetComponentInChildren<SpriteRenderer>();
                var animator = Get<KarmaCharacterAnimator>(enemy, "animator");
                var standing = picture.sprite; var scale = picture.transform.localScale; var home = picture.transform.localPosition;
                var restored = form == 0 ? KarmaArtwork.Sprite("Ward", "Idle")
                    : Array.Find(KarmaArtwork.InheritedPoses(key), s => s.name == "Idle");
                var color = picture.color; var bounds = picture.bounds; var position = enemy.Position;
                Capture("output/validation/stagger/" + label + "-before.png");
                Force(facing); await Until(() => enemy.HeavyParryWindowOpen);
                float hp = game.Player.Health; int parries = game.Player.ParryCount;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K)); await Task.Delay(45);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                await Until(() => enemy.Stunned); float began = enemy.FightSeconds;
                await Seconds(0.35f);
                Capture("output/validation/stagger/" + label + "-stunned.png");
                log.Add(label + " standing bounds " + bounds + " / stagger bounds " + picture.bounds
                    + " / standing pivot " + standing.pivot + " / scale " + scale + " -> " + picture.transform.localScale);
                Check(game.Player.ParryCount == parries + 1 && game.Player.Health == hp, label + " actual K parry");
                Check(!enemy.AutoShieldActive && !enemy.Armored && !Get<SpriteRenderer>(enemy, "shieldHalo").enabled, label + " GD01 and halo broken");
                Check(animator.CurrentPose == "Stagger/" + key && picture.sprite.name == "Stagger"
                    && picture.sprite.texture.name == key, label + " correct inherited stagger sprite");
                Check(Mathf.Sign(art.localScale.x) == facing && !picture.flipX && !picture.flipY
                    && picture.transform.localScale.x > 0 && picture.transform.localRotation == Quaternion.identity,
                    label + " one horizontal mirror, no upside-down or reversed local scale");
                Check(Vector2.Distance(position, enemy.Position) < 0.01f && picture.transform.localPosition == home,
                    label + " body anchor does not teleport");
                float sole = form == 2 ? 12 : 0;
                float foot = picture.transform.TransformPoint(new Vector3(0, (sole - picture.sprite.pivot.y) / picture.sprite.pixelsPerUnit, 0)).y;
                Check(Mathf.Abs(foot) < 0.03f, label + " authored boot soles stay on original floor (transparent padding excluded)");
                float ratio = picture.bounds.size.y / bounds.size.y;
                Check(ratio > 0.60f && ratio < 0.95f && picture.bounds.size.x < bounds.size.x * 1.35f,
                    label + " crouch stays shorter without enlarging body: " + ratio.ToString("F3"));
                Check(Mathf.Abs(picture.color.r - color.r * 0.82f) < 0.001f && picture.color.a == color.a,
                    label + " readable darkening preserves final tint and opacity");
                float bossHP = enemy.Health; enemy.TakeDamage(1, game.Player.Position); await Seconds(0.06f);
                Check(enemy.Health < bossHP && animator.CurrentPose == "Stagger/" + key, label + " hurt keeps inherited stagger");
                float pause = enemy.FightSeconds; var pausedSprite = picture.sprite; var pausedScale = picture.transform.localScale;
                game.TogglePause(); await Task.Delay(100);
                Check(enemy.FightSeconds == pause && picture.sprite == pausedSprite && picture.transform.localScale == pausedScale,
                    label + " pause freezes stagger duration and appearance");
                game.TogglePause(); await Until(() => !enemy.Stunned, 4); Set(enemy, "timer", float.MaxValue); await Seconds(0.05f);
                Check(enemy.FightSeconds - began >= 1.95f, label + " full two-second stun");
                Check(picture.sprite == restored && picture.transform.localScale == scale && picture.transform.localPosition == home
                    && picture.color == color && picture.transform.localRotation == Quaternion.identity,
                    label + " exact standing sprite, scale, position and tint restored");
                Check(Mathf.Abs(picture.bounds.size.y - restored.bounds.size.y * scale.y * art.localScale.y) < 0.001f,
                    label + " restored size and floor match before parry");
                Capture("output/validation/stagger/" + label + "-after.png");
                Force(facing); await Until(() => enemy.HeavyParryWindowOpen);
                Check(animator.CurrentPose == "GD02/Windup" && picture.sprite.texture.name == key, label + " next GD02 uses original attack sheet");
                foreach (var pattern in new[] { EnemyAttack.GD03, EnemyAttack.GD04, EnemyAttack.GD05 })
                {
                    Force(facing, pattern); Set(game.Player, "invulnerableUntil", Time.time + 100); await Seconds(0.08f);
                    Check((form == 0 ? picture.sprite.texture.name == "Ward" && picture.sprite.name != "Stagger" : picture.sprite.texture == standing.texture)
                        && picture.color == color && picture.transform.localScale == scale,
                        label + " " + pattern + " restores stage body without leftover stagger");
                }
            }
            await Reset(3, -1); Force(-1); await Until(() => enemy.HeavyParryWindowOpen);
            game.Player.Essences.TryCounter(); await Until(() => enemy.Stunned); await Seconds(0.04f);
            var deadAnimator = Get<KarmaCharacterAnimator>(enemy, "animator");
            var deadPicture = Get<Transform>(enemy, "art").GetComponentInChildren<SpriteRenderer>();
            enemy.TakeDamage(100000, game.Player.Position);
            Check(deadPicture.color == new Color(1, 0.88f, 0.73f), "death immediately clears stagger tint before the fall animation");
            await Task.Delay(100);
            Check(!enemy.Alive && !deadAnimator.CurrentPose.StartsWith("Stagger/"), "death exits stagger pose");
            Check(deadPicture.color.r < 1 && deadPicture.color.r >= 0.55f
                && deadPicture.color.g <= 0.88f && deadPicture.color.g >= 0.55f,
                "team guardian-fall fade runs after stagger without keeping its tint");
            await Reset(0, 1);
            Check(!enemy.Stunned && Get<KarmaCharacterAnimator>(enemy, "animator").CurrentPose != "Stagger/Ward", "new room has no stale stun");
            Check(runtimeError == null, "no game-code runtime errors: " + runtimeError);
            string result = "PASS: " + checks + " stagger runtime assertions; 5 inheritance paths x both facings; 30 real Game-view captures";
            log.Insert(0, result); File.WriteAllLines("output/validation/guard-stagger-play-validation.txt", log);
            return result;
        }
        catch (Exception e) { File.WriteAllText("output/validation/guard-stagger-play-validation.txt", "FAIL after " + checks + ": " + e + "\n" + string.Join("\n", log)); throw; }
        finally
        {
            EditorApplication.isPaused = false; Application.logMessageReceived -= Error;
            InputSystem.RemoveDevice(keyboard); game.ReturnToTitle(); Set(game, "config", original); UnityEngine.Object.Destroy(config);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = inputBehavior;
            InputSystem.settings.updateMode = updateMode;
        }
    }
}
#endif
