#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;
using KarmaPrototype.Editor;

// Pipeline script run, in Play Mode. Uses the same TryCounter entry point as keyboard K.
public static class KarmaHeavyStrikePlayChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static int checks;
    static readonly List<string> results = new List<string>();
    static readonly Essence[][] Orders = {
        new[] { Essence.Ward }, new[] { Essence.Ward, Essence.Flame },
        new[] { Essence.Ward, Essence.Dash }, new[] { Essence.Ward, Essence.Flame, Essence.Dash },
        new[] { Essence.Ward, Essence.Dash, Essence.Flame }
    };
    static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, Private).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
    static void Check(bool condition, string label)
    { checks++; if (!condition) throw new Exception(label); results.Add("PASS " + label); }
    static void Init()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        EditorApplication.isPaused = false;
        KarmaGuardPractice.Stop();
        game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        if (game == null) throw new Exception("K4RMA scene required");
    }
    static async Task Reset(int form = 0, int facing = -1, bool enraged = false)
    {
        game.StartRun();
        foreach (var essence in Orders[form]) game.Player.Essences.Sacrifice(essence);
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, Orders[form].Length);
        Call(game, "LoadRoom", true);
        enemy = game.Enemies[0];
        enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
        var body = game.Player.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        Place(new Vector2(20 + facing * KarmaSpace.Units(2), KarmaSpace.Units(1)));
        Set(game.Player, "invulnerableUntil", 0f);
        Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue); Set(enemy, "nextShield", float.MaxValue);
        if (enraged) typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.4f);
        await Task.Delay(35); // Old room objects are destroyed before searching the new room.
        Force(facing);
    }
    static void Place(Vector2 value)
    { game.Player.transform.position = value; game.Player.GetComponent<Rigidbody2D>().position = value; }
    static void Force(int facing, EnemyAttack pattern = EnemyAttack.GD02)
    {
        Call(enemy, "InterruptPattern");
        Set(enemy, "selected", pattern); Set(enemy, "direction", (float)facing);
        Get<Transform>(enemy, "art").localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
        Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
        if (pattern == EnemyAttack.GD02) Call(enemy, "RefreshAutoShield");
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static Transform Cue() => Get<Transform>(enemy, "effectsRoot").Find("GD02 parry cue");
    static async Task Until(Func<bool> predicate, float timeout = 4)
    {
        double deadline = EditorApplication.timeSinceStartup + timeout;
        while (!predicate())
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout: " + enemy.Intent);
            await Task.Delay(5);
        }
    }
    static Task Seconds(float duration)
    { float until = enemy.FightSeconds + duration; return Until(() => enemy.FightSeconds >= until, duration + 4); }

    public static async Task<string> Run()
    {
        Init(); checks = 0; results.Clear();
        var original = game.Config;
        var config = UnityEngine.Object.Instantiate(original);
        config.playerHealth = 100000;
        Set(game, "config", config);
        try
        {
            int frame = Time.frameCount; await Task.Delay(100);
            Check(Time.frameCount > frame, "real player loop advances while editor is in background");
            Check(Mathf.Approximately(config.BossTuning.heavyParryCueSeconds, 0.5f), "cue setting is 0.5 seconds");
            Check(Mathf.Approximately(config.BossTuning.heavyHitStunSeconds, 1), "hit stun setting is 1 second");
            float[] inputs = { 0.02f, 0.25f, 0.45f };
            for (int form = 0; form < Orders.Length; form++)
            for (int timing = 0; timing < inputs.Length; timing++)
            {
                await Reset(form, timing == 1 ? 1 : -1, timing == 2);
                // A normal premature press must not lock the visible cue behind its cooldown.
                if (timing == 0) game.Player.Essences.TryCounter();
                await Until(() => enemy.HeavyParryWindowOpen);
                float opened = enemy.FightSeconds;
                var cue = Cue(); var renderer = cue.GetComponent<SpriteRenderer>();
                Check(renderer.sprite != null && renderer.bounds.size.x < KarmaSpace.Units(0.8f), "form " + form + " cue stays compact around the grip");
                Check(renderer.color.a == 1 && renderer.sharedMaterial == KarmaVisuals.DefaultMaterial,
                    "form " + form + " cue has an opaque core throughout its input window");
                var glow = cue.Find("GD02 parry glow").GetComponent<SpriteRenderer>();
                Check(glow.enabled && glow.sharedMaterial == KarmaCombatVFX.GlowMaterial
                    && renderer.sortingOrder > 26 && glow.sortingOrder < renderer.sortingOrder,
                    "form " + form + " grip light has a separate red glow above attack artwork");
                Check(Vector2.Distance(cue.position, Get<KarmaCharacterAnimator>(enemy, "animator").HeavyHandPosition) < 0.01f,
                    "form " + form + " cue follows raised weapon hand");
                var sprite = Get<Transform>(enemy, "art").GetComponentInChildren<SpriteRenderer>();
                string expected = form == 0 ? "Ward" : form == 1 ? "WardWave" : form == 2 ? "WardRise" : "WardFinal";
                Check(sprite.sprite.texture.name == expected, "form " + form + " retains inherited body");
                await Seconds(inputs[timing]);
                Check(enemy.HeavyParryWindowOpen, "input " + timing + " occurs while cue is visible");
                int parries = game.Player.ParryCount; float hp = game.Player.Health;
                game.Player.Essences.TryCounter();
                await Until(() => enemy.Stunned);
                Check(game.Player.ParryCount == parries + 1 && game.Player.Health == hp && !game.Player.Stunned,
                    "form " + form + " timing " + timing + " parries without damage or player stun");
                Check(!enemy.Armored && !enemy.HeavyParryWindowOpen && Cue() == null,
                    "parry removes shield, cue and pending window");
                float duration = enemy.FightSeconds - opened;
                Check(duration >= 0.48f && duration < 0.62f, "cue duration normal/enraged: " + duration.ToString("0.000"));
            }

            await Reset();
            game.Player.Essences.TryCounter(); // Press before the red cue, no follow-up input.
            float health = game.Player.Health;
            await Until(() => game.Player.Stunned);
            Check(game.Player.Health < health, "premature input is not buffered before cue");
            float until = Get<float>(game.Player, "stunUntil");
            Check(until - Time.time > 0.9f && until - Time.time <= 1.01f, "actual hit starts one-second stun");
            await Seconds(0.2f);
            Check(Mathf.Abs(health - game.Player.Health - config.enemyDamage * config.BossTuning.damageMultiplier * 1.5f) < 0.02f,
                "heavy strike deals exactly one configured hit");
            await Until(() => !game.Player.Stunned, 2);
            Check(Time.time >= until, "player recovers after stun deadline");

            await Reset();
            await Until(() => enemy.HeavyParryWindowOpen);
            float beforePause = enemy.FightSeconds;
            game.TogglePause(); await Task.Delay(250);
            Check(Mathf.Abs(enemy.FightSeconds - beforePause) < 0.001f && Cue().gameObject.activeInHierarchy,
                "pause preserves cue and its remaining duration");
            game.TogglePause(); game.Player.Essences.TryCounter();
            await Until(() => enemy.Stunned);
            Check(!game.Player.Stunned, "resumed visible cue can still parry");

            await Reset();
            await Until(() => enemy.HeavyParryWindowOpen);
            game.Player.Essences.TryCounter();
            Force(-1);
            Check(!Get<bool>(enemy, "heavyParryQueued") && !enemy.HeavyParryWindowOpen,
                "interrupt discards buffered parry before a new strike");
            await Until(() => game.Player.Stunned);

            foreach (bool guard in new[] { true, false })
            {
                await Reset();
                if (guard) game.Player.BeginWard(3);
                else Set(game.Player, "invulnerableUntil", Time.time + 3);
                health = game.Player.Health;
                await Until(() => Get<object>(enemy, "state").ToString() == "Recover");
                Check(game.Player.Health == health && !game.Player.Stunned, guard ? "guard causes no stun" : "invulnerable dodge causes no stun");
            }
            await Reset();
            Place(new Vector2(3, KarmaSpace.Units(1)));
            await Until(() => enemy.HeavyParryWindowOpen);
            game.Player.Essences.TryCounter();
            await Until(() => Get<object>(enemy, "state").ToString() == "Recover");
            Check(!Get<bool>(enemy, "heavyParryQueued") && !enemy.Stunned, "missed strike cannot leave a stored or remote parry");
            await CheckShockwaveRules();
            results.Insert(0, "PASS: " + checks + " GD02 runtime assertions");
            Directory.CreateDirectory("output/validation");
            File.WriteAllLines("output/validation/gd02-play-validation.txt", results);
            return results[0];
        }
        catch (Exception error)
        {
            File.WriteAllText("output/validation/gd02-play-validation.txt", string.Join("\n", results) + "\nFAIL: " + error);
            throw;
        }
        finally
        {
            if (game.Paused) game.TogglePause();
            game.ReturnToTitle(); Set(game, "config", original); UnityEngine.Object.Destroy(config);
        }
    }

    static async Task CheckShockwaveRules()
    {
        float[] healthRatios = { 0.5001f, 0.5f, 0.4999f };
        for (int form = 0; form < Orders.Length; form++)
        for (int hp = 0; hp < healthRatios.Length; hp++)
        {
            int facing = hp == 1 ? 1 : -1;
            await Reset(form, facing);
            typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * healthRatios[hp]);
            Place(new Vector2(3, KarmaSpace.Units(9))); // Miss the downstroke; inspect its real follow-up.
            bool inheritedWave = Array.IndexOf(Orders[form], Essence.Flame) >= 0;
            bool expected = inheritedWave && healthRatios[hp] <= 0.5f;
            string label = "form " + form + " HP " + (healthRatios[hp] * 100).ToString("F2") + "%";
            await Until(() => enemy.Intent.Contains("강공격 후 지면 충격파")
                || Get<object>(enemy, "state").ToString() == "Recover");
            bool warned = enemy.Intent.Contains("강공격 후 지면 충격파");
            Check(warned == expected, label + " follow-up requires sword inheritance and HP <= 50%");
            if (!expected)
            {
                Check(enemy.AutoShieldActive && !enemy.Stunned, label + " no wave, GD01 remains active");
                continue;
            }
            Check(!enemy.HeavyParryWindowOpen && !Get<bool>(enemy, "heavyParryQueued"),
                label + " downstroke input is cleared before the wave");
            await Until(() => UnityEngine.Object.FindAnyObjectByType<KarmaBossAttack>() != null);
            var wave = UnityEngine.Object.FindAnyObjectByType<KarmaBossAttack>();
            Check(Get<Vector2>(wave, "velocity").x * facing > 0,
                label + " actual wave travels in the facing direction");
            Check(!Get<KarmaAttackContact>(wave, "contact").BreaksShieldOnParry,
                label + " wave contact is distinct from the shield-breaking downstroke");
            Place((Vector2)wave.transform.position + Vector2.right * facing * KarmaSpace.Units(0.2f));
            Set(game.Player, "invulnerableUntil", 0f);
            int parries = game.Player.ParryCount;
            float health = game.Player.Health;
            game.Player.Essences.TryCounter();
            await Until(() => game.Player.ParryCount > parries, 1);
            Check(game.Player.ParryCount == parries + 1 && game.Player.Health == health,
                label + " real wave is counterable without player damage");
            await Seconds(0.05f);
            Check(enemy.AutoShieldActive && !enemy.Stunned && !Get<bool>(enemy, "pendingParry"),
                label + " wave counter neither breaks GD01 nor stuns or interrupts the boss");
            await Until(() => Get<object>(enemy, "state").ToString() == "Recover");
            Check(enemy.AutoShieldActive, label + " GD01 persists through recovery after a wave counter");
        }
    }

    // Freeze an actual live GD02 frame for Pipeline capture game_view (no mockup).
    public static async Task<string> Preview(int form, bool stroke = false)
    {
        Init(); await Reset(form);
        Place(new Vector2(13, KarmaSpace.Units(1)));
        Set(game.Player, "invulnerableUntil", Time.time + 100);
        await Until(() => enemy.HeavyParryWindowOpen);
        if (stroke)
            await Until(() => Get<KarmaCharacterAnimator>(enemy, "animator").CurrentPose == "GD02/Downstroke");
        else await Seconds(0.06f);
        EditorApplication.isPaused = true;
        return "Frozen live GD02 " + (stroke ? "downstroke" : "red cue") + ", form " + form;
    }

    public static async Task<string> RunAndCapture()
    {
        string result = await Run();
        string appearance = await CheckWardVisuals();
        return result + "; " + appearance + "; " + await CaptureGallery();
    }

    static void CaptureFrame(string path, int width = 1920, int height = 1080, string source = "screen")
    {
        Type captureType = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            captureType = assembly.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands");
            if (captureType != null) break;
        }
        if (captureType == null) throw new Exception("Unity Pipeline capture command required");
        var capture = captureType.GetMethod("GameView", BindingFlags.Public | BindingFlags.Static);
        var result = capture.Invoke(null, new object[] { width, height, null, null, false, 0, source });
        string png = (string)result.GetType().GetProperty("Base64").GetValue(result);
        File.WriteAllBytes(path, Convert.FromBase64String(png));
    }

    public static async Task<string> CheckWardVisuals()
    {
        Init();
        var original = game.Config;
        var config = UnityEngine.Object.Instantiate(original);
        Set(game, "config", config);
        config.BossTuning.trialAttackCount = 5;
        int samples = 0;
        try
        {
            for (int form = 3; form <= 4; form++)
            {
                await Reset(form, form == 3 ? -1 : 1, form == 4);
                Set(game.Player, "invulnerableUntil", Time.time + 100);
                Force(form == 3 ? -1 : 1, EnemyAttack.GD05);
                var body = Get<Transform>(enemy, "art").GetComponentInChildren<SpriteRenderer>();
                var expected = KarmaArtwork.GuardianPreview(Orders[form]);
                var states = new HashSet<string>();
                bool hurt = false, captured = false;
                double deadline = EditorApplication.timeSinceStartup + 28;
                // Check every sampled live frame, including movement into the room and recovery.
                while (Get<bool>(enemy, "running"))
                {
                    await Task.Delay(5);
                    if (EditorApplication.timeSinceStartup > deadline) throw new Exception("GD05 visual timeout");
                    if (body.sprite != expected) throw new Exception("GD05 changed final body to " + body.sprite.name);
                    samples++;
                    string state = Get<object>(enemy, "state").ToString();
                    states.Add(state);
                    if (enemy.TrialActive && state == "Attack" && !hurt)
                    {
                        float hp = enemy.Health;
                        enemy.TakeDamage(1, game.Player.Position);
                        if (enemy.Health != hp || !enemy.Invulnerable) throw new Exception("GD05 must reject damage during its trial");
                        hurt = true;
                    }
                    if (enemy.TrialActive && state == "Attack" && !captured)
                    {
                        EditorApplication.isPaused = true;
                        CaptureFrame("output/validation/GD05-final-form-" + form + ".png");
                        EditorApplication.isPaused = false; captured = true;
                    }
                }
                if (!states.Contains("Telegraph") || !states.Contains("Attack") || !states.Contains("Recover") || !hurt)
                    throw new Exception("GD05 visual coverage incomplete");
                if (body.color != new Color(1, 0.88f, 0.73f)) throw new Exception("Final body tint was lost");
            }
            string report = "PASS: GD05 final body retained across " + samples
                + " sampled frames; both inheritance orders, both facings, normal/enraged, blocked damage and recovery";
            File.WriteAllText("output/validation/ward-visual-validation.txt", report + "\n");
            return report;
        }
        finally
        {
            EditorApplication.isPaused = false; game.ReturnToTitle();
            Set(game, "config", original); UnityEngine.Object.Destroy(config);
        }
    }

    public static async Task<string> CaptureGallery()
    {
        // Capture each short-lived frame in-process: avoids CLI round-trip latency.
        string[] forms = { "Ward", "WardWave", "WardRise", "WardFinal", "WardFinalReverse" };
        try
        {
            for (int form = 0; form < forms.Length; form++)
            foreach (bool stroke in new[] { false, true })
            {
                await Preview(form, stroke);
                Get<Transform>(game.Player, "art").gameObject.SetActive(true);
                CaptureFrame("output/validation/GD02-" + forms[form] + (stroke ? "-downstroke.png" : "-cue.png"));
                if (form == 3 && !stroke)
                {
                    CaptureFrame("output/validation/GD02-grip-light-on-960.png", 960, 540, "camera");
                    Cue().gameObject.SetActive(false);
                    CaptureFrame("output/validation/GD02-grip-light-off-960.png", 960, 540, "camera");
                    Cue().gameObject.SetActive(true);
                }
            }
            return "Captured 10 real game-view frames: 5 stage forms x cue/downstroke";
        }
        finally { EditorApplication.isPaused = false; if (game != null) game.ReturnToTitle(); }
    }

    public static async Task<string> CaptureGripComparison()
    {
        await Preview(3);
        try
        {
            // Re-render the camera synchronously; the paused Game view caches its last frame.
            CaptureFrame("output/validation/GD02-grip-light-on-960.png", 960, 540, "camera");
            Cue().gameObject.SetActive(false);
            CaptureFrame("output/validation/GD02-grip-light-off-960.png", 960, 540, "camera");
            return "Captured fresh camera renders with/without grip light at 960 x 540";
        }
        finally { EditorApplication.isPaused = false; game.ReturnToTitle(); }
    }
}
#endif
