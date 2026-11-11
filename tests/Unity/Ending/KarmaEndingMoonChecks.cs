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
public static class KarmaEndingMoonChecks
{
    const string Key = "K4RMA.EndingMoonChecks";
    const string Output = "output/validation/ending-moon";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static bool started;
    static int checks, knownWarnings;
    static string runtimeError;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static RenderTexture target;
    static Texture2D image;
    static KarmaEndingMoonChecks() { EditorApplication.update += Pump; }
    static object Get(object obj, string name) => obj.GetType().GetField(name, F).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, F).SetValue(obj, value);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, F).Invoke(obj, args);
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.isPlaying) { Application.runInBackground = true; EditorApplication.isPaused = false; }
        EditorApplication.QueuePlayerLoopUpdate();
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2) { started = true; Run(); }
    }
    public static void RunBatch()
    {
        SessionState.SetBool(Key + ".EndingOnly", false);
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    public static void RunEndingBatch() { RunBatch(); SessionState.SetBool(Key + ".EndingOnly", true); }
    static async Task Until(Func<bool> done, float timeout = 90)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!done()) { if (EditorApplication.timeSinceStartup > end) throw new Exception("Ending/camera timeout"); await Task.Delay(10); }
    }
    static void Reset(Essence primary, int stage = 3)
    {
        game.StartRun();
        foreach (var essence in new[] { primary, (Essence)(((int)primary + 1) % 3), (Essence)(((int)primary + 2) % 3) })
        { game.Player.Essences.Preview(essence); game.Player.Essences.Confirm(out _); }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, stage); Call(game, "LoadRoom", true);
        enemy = game.Enemies[0]; Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue);
        game.Player.ResetForRoom(new Vector2(16, KarmaSpace.PlayerHalfHeight + 0.05f), true);
        Set(game.Player, "invulnerableUntil", Time.time + 10000);
    }
    static SpriteRenderer Background => (game.World.Find("Trial hall artwork") ?? game.World.Find("Mountain dojo artwork")).GetComponent<SpriteRenderer>();
    static void Render(string name = null, bool nativeViewport = false)
    {
        var camera = game.GameCamera; var prior = camera.targetTexture; var active = RenderTexture.active;
        var priorRect = camera.rect;
        try
        {
            // The output texture is 16:9; do not apply the Game View's letterbox a second time.
            camera.targetTexture = target;
            if (!nativeViewport) camera.rect = new Rect(0, 0, 1, 1);
            camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            if (name != null) File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally { camera.targetTexture = prior; camera.rect = priorRect; RenderTexture.active = active; }
    }
    static float MoonRatio(bool mountain, bool nativeViewport = false)
    {
        Render(nativeViewport: nativeViewport);
        var renderer = Background; var sprite = renderer.sprite;
        Vector2 uv = mountain ? new Vector2(0.374f, 0.695f) : new Vector2(0.548f, 0.66f);
        var local = new Vector3((uv.x * sprite.rect.width - sprite.pivot.x) / sprite.pixelsPerUnit,
            (uv.y * sprite.rect.height - sprite.pivot.y) / sprite.pixelsPerUnit, 0);
        var moon = game.World.Find("Round moon");
        var view = game.GameCamera.WorldToViewportPoint(moon != null ? moon.position : renderer.transform.TransformPoint(local));
        var rect = nativeViewport ? game.GameCamera.rect : new Rect(0, 0, 1, 1);
        int cx = Mathf.RoundToInt((rect.x + view.x * rect.width) * 1280);
        int cy = Mathf.RoundToInt((rect.y + view.y * rect.height) * 720);
        int radius = Mathf.CeilToInt(0.035f * renderer.bounds.size.y / game.GameCamera.orthographicSize * 720);
        int minX = 1280, minY = 720, maxX = -1, maxY = -1;
        var pixels = image.GetPixels32();
        for (int y = Mathf.Max(0, cy - radius); y < Mathf.Min(720, cy + radius); y++)
        for (int x = Mathf.Max(0, cx - radius); x < Mathf.Min(1280, cx + radius); x++)
        {
            var c = pixels[y * 1280 + x];
            if (c.r < 175 || c.g < 170 || c.b < 95) continue;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }
        Check(maxX > minX && maxY > minY, "Moon visible in rendered frame");
        float ratio = (maxX - minX + 1f) / (maxY - minY + 1f);
        Debug.Log("MOON ratio=" + ratio + " size=" + (maxX - minX + 1) + "x" + (maxY - minY + 1));
        return ratio;
    }
    static void SettleCamera() { for (int i = 0; i < 100; i++) Call(game, "LateUpdate"); }
    static void CheckCoverage()
    {
        var bounds = Background.bounds; var camera = game.GameCamera;
        float h = camera.orthographicSize, w = h * camera.aspect; var p = camera.transform.position;
        Check(p.x - w >= bounds.min.x - 0.1f && p.x + w <= bounds.max.x + 0.1f
            && p.y - h >= bounds.min.y - 0.1f && p.y + h <= bounds.max.y + 0.1f, "Camera remains inside background artwork");
    }
    static void Force(EnemyAttack attack)
    {
        Call(enemy, "InterruptPattern"); Set(enemy, "selected", attack); Set(enemy, "running", true);
        Set(enemy, "patternStarted", enemy.FightSeconds);
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static void Log(string message, string stack, LogType type)
    {
        if (message.StartsWith("Not allowed to override geometry") && stack.Contains("Assets/K4RMA/"))
        { knownWarnings++; return; }
        if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert) && stack.Contains("Assets/K4RMA/")) runtimeError = message;
    }
    static void CheckFragmentRedistribution()
    {
        Reset(Essence.RisingSlash);
        var type = typeof(KarmaEnemy).GetNestedType("UP04Pattern", BindingFlags.NonPublic);
        var pattern = Activator.CreateInstance(type, F, null, new object[] { enemy }, null);
        Call(pattern, "Burst", enemy.Position);
        var root = (Transform)Get(enemy, "effectsRoot");
        var shots = root.GetComponentsInChildren<KarmaRisingFragmentAttack>();
        int down = 0, up = 0, flanks = 0;
        foreach (var shot in shots)
        {
            if (shot.transform.position.y < enemy.Position.y + KarmaSpace.Units(0.35f)) down++; else up++;
            var velocity = (Vector2)Get(shot, "velocity");
            Check(velocity.y >= -0.001f, "No downward fragment velocity");
            if (Mathf.Abs(velocity.x) > 0.001f && velocity.y / Mathf.Abs(velocity.x) < Mathf.Tan(20 * Mathf.Deg2Rad)) flanks++;
            var sprite = shot.Visual.sprite;
            Check(Mathf.Abs(shot.Visual.transform.localScale.x * sprite.bounds.size.x - KarmaSpace.Units(1.05f) * 1.5f) < 0.01f,
                "Fragment artwork is fifty percent larger for a substantial silhouette");
        }
        Check(shots.Length == 14 && down == 0 && up == 14, "No downward rays; fourteen upper and flank fragments");
        Check(flanks == 6, "Three additional low-angle fragments on each flank");
        Call(enemy, "InterruptPattern");
    }
    static async Task CheckMoonsAndCameras()
    {
        foreach (var display in new[] { new Vector2(1024, 768), new Vector2(1280, 720), new Vector2(1680, 720) })
        {
            var rect = (Rect)typeof(KarmaGame).GetMethod("ScreenViewport", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { display.x, display.y });
            Check(Mathf.Abs(rect.width / rect.height - 16f / 9) < 0.001f,
                "4:3, 16:9 and ultrawide displays retain undistorted gameplay viewport");
        }
        foreach (var primary in new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard })
        foreach (int stage in new[] { 0, 3 })
        {
            Reset(primary, stage); SettleCamera();
            var bg = Background; var before = bg.bounds; var frame = (Bounds)Get(game, "stageBackgroundBounds");
            float normal = Mathf.Max(frame.extents.y, 25 / game.GameCamera.aspect);
            Check(Mathf.Abs(game.GameCamera.orthographicSize - normal) < 0.05f, "Original normal zoom preserved");
            var viewport = game.GameCamera.pixelRect;
            Debug.Log("VIEWPORT Screen=" + Screen.width + "x" + Screen.height + " pixelRect=" + viewport
                + " rect=" + game.GameCamera.rect + " aspect=" + game.GameCamera.aspect);
            float ratio = MoonRatio(stage == 0);
            Check(Mathf.Abs(ratio - 1) < 0.04f, "Moon is round in normal combat");
            Check(Mathf.Abs(MoonRatio(stage == 0, nativeViewport: true) - 1) < 0.04f,
                "Moon remains round in the Editor's actual letterboxed viewport");
            CheckCoverage();
            if (primary == Essence.SwordWave && stage == 0) Render("moon-mountain");
            if (primary == Essence.SwordWave && stage == 3)
            {
                Check(game.World.Find("Round moon") != null, "Moon is isolated while approved background is preserved");
                Check(bg.sprite == KarmaArtwork.Settings.dojoPanorama,
                    "Approved trial hall background remains assigned");
                Render("moon-normal");
            }
            typeof(KarmaGame).GetProperty("Phase").SetValue(game, RunPhase.Transition);
            SettleCamera(); CheckCoverage();
            Check(Mathf.Abs(game.GameCamera.orthographicSize - normal * 0.75f) < 0.1f,
                "Personalization preserves its original zoom-in even when moon is outside the frame");
            Check(bg.bounds == before, "Moon correction never changes background geometry");
        }
        Reset(Essence.Guard); SettleCamera(); float baseline = game.GameCamera.orthographicSize;
        Set(Get(enemy, "behavior"), "PracticeHold", true);
        game.Player.ToggleTestInvincibility();
        Force(EnemyAttack.GD05);
        await Until(() => enemy.TrialActive);
        SettleCamera();
        Check(game.GameCamera.orthographicSize > baseline + 0.5f, "GD05 zooms out normally");
        var frameBounds = (Bounds)Get(game, "stageBackgroundBounds");
        float expected = Mathf.Max(frameBounds.extents.y, 25 / game.GameCamera.aspect, 16, 22 / game.GameCamera.aspect);
        Check(Mathf.Abs(game.GameCamera.orthographicSize - expected) < 0.1f, "GD05 retains exact original zoom target");
        Render("gd05-zoom");
        // GD05 dome crosses the moon. Exclude foreground renderers only for the pixel measurement.
        var foreground = Array.FindAll(game.World.GetComponentsInChildren<Renderer>(), r => r != Background && r.gameObject.name != "Round moon" && r.enabled);
        float moonRatio;
        try { foreach (var renderer in foreground) renderer.enabled = false; moonRatio = MoonRatio(false); }
        finally { foreach (var renderer in foreground) if (renderer != null) renderer.enabled = true; }
        Check(Mathf.Abs(moonRatio - 1) < 0.06f, "Moon remains round in GD05 zoom-out");
        CheckCoverage(); Render("gd05-zoom");
        await Until(() => !enemy.TrialActive && enemy.GetType().GetProperty("CameraFocus", F).GetValue(enemy) == null);
        SettleCamera();
        Check(Mathf.Abs(game.GameCamera.orthographicSize - baseline) < 0.1f, "GD05 camera returns after trial ends");
        Call(enemy, "InterruptPattern"); SettleCamera(); CheckCoverage();
    }
    static async Task CheckEnding()
    {
        Reset(Essence.SwordWave);
        game.Player.ResetForRoom(new Vector2(12, KarmaSpace.PlayerHalfHeight + 0.05f), true);
        await Task.Yield();
        KarmaProjectile.Spawn(game, game.Player.Position, Vector2.right, 1, Color.white);
        enemy.TakeDamage(enemy.MaxHealth * 100);
        Check(game.Phase == RunPhase.Ending && !game.EndingCompleted, "Final kill starts ending, not ordinary victory");
        Check(!game.AllowsPlayerActions && !game.CanMove && game.Projectiles.Count == 0, "Ending blocks combat and clears projectiles");
        var clips = Resources.Load<KarmaSfxLibrary>("KarmaSfxLibrary").Clips;
        foreach (var cue in new[] { KarmaSfxId.CombatBossDefeat, KarmaSfxId.PlDash, KarmaSfxId.PlRisingSlash,
            KarmaSfxId.CombatGuardBlock, KarmaSfxId.GameAbilityInherit, KarmaSfxId.PlSwordSwing })
            Check(clips[(int)cue] != null, "Ending audio asset exists: " + cue);
        float musicPreference = KarmaPreferences.MusicVolume;
        float time = game.EndingTime; game.EnemyDefeated(enemy);
        Check(game.EndingTime == time, "Duplicate defeat notification cannot restart ending");
        await Until(() => game.EndingTime >= 2.5f); Render("ending-01-dissolve"); CheckCoverage();
        await Until(() => game.EndingTime >= 7);
        Check((float)typeof(KarmaAudio).GetField("endingAmbience", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) == 0,
            "Battle ambience fades to silence before the quiet interval");
        Check(game.World.Find("Ending presentation") != null, "Ending presentation exists");
        Check(!Array.Exists(game.World.GetComponentsInChildren<KarmaCharacterAnimator>(), a => a.CurrentPose == "Death"), "Boss vanishes before techniques appear");
        await Until(() => game.EndingTime >= 14.5f); Render("ending-02-techniques"); CheckCoverage();
        float health = game.Player.Health; game.Player.TakeDamage(10000);
        Check(game.Player.Health == health && !game.Player.ApplyAirborne(Vector2.up * 12), "Combat damage and CC cannot interrupt ending");
        time = game.EndingTime; game.TogglePause(); await Task.Delay(150);
        Check(game.Paused && Mathf.Abs(game.EndingTime - time) < 0.05f, "Pause freezes ending timeline");
        game.TogglePause();
        var completedTechniques = game.World.Find("Ending presentation");
        for (int i = 0; i < 3; i++)
            Check(completedTechniques.Find("Completed technique " + (Essence)i).GetComponent<SpriteRenderer>().sprite != null,
                "Each completed technique reuses a connected sprite");
        await Until(() => game.EndingTime >= 19.4f); Render("ending-02-union");
        Check(completedTechniques.Find("Completed aura").GetComponent<SpriteRenderer>().color.a > 0.2f,
            "United techniques leave a visible aura on the player");
        await Until(() => game.EndingTime >= 21.7f);
        Check(game.EndingLine == "내가 가르친 것은 기술의 형태일 뿐이다.", "First master line appears verbatim");
        Check(game.EndingMemoryDim > 0.3f, "Memory dims the scene");
        Render("ending-03-memory"); ScreenCapture.CaptureScreenshot(Output + "/ending-03-memory-ui.png"); CheckCoverage();
        await Until(() => game.EndingTime >= 25.8f);
        Check(game.EndingLine == "그 기술을 어떻게 완성할지는 너에게 달려 있다.", "Second master line appears verbatim");
        await Until(() => game.EndingTime >= 31.8f);
        Check(game.EndingLine == "이제, 나만의 기술이 완성되었다.", "Final player line appears verbatim");
        float beforeWalk = game.Player.Position.x;
        await Until(() => game.EndingTime >= 37); Render("ending-04-departure"); CheckCoverage();
        Check(game.Player.Position.x > beforeWalk + 1, "Player walks toward the exit");
        await Until(() => game.EndingCompleted);
        Check((int)Get(game, "endingSoundStage") == 5, "All five ending sound cues fire once in order");
        Check(KarmaPreferences.MusicVolume == musicPreference, "Ending never changes user's music preference");
        Check(game.Phase == RunPhase.Victory && game.EndingFade == 1 && game.EndingTime == 40,
            "Forty-second ending finishes in a quiet fade");
        await Task.Yield();
        Check(game.World.Find("Ending presentation") == null, "Ending effects clean up after completion");
        game.RestartSession();
        Check(game.Phase == RunPhase.Tutorial && game.Tutorial != null && !game.EndingCompleted && game.EndingTime == 0, "Restart begins tutorial and clears ending state");
        foreach (var primary in new[] { Essence.RisingSlash, Essence.Guard })
        {
            Reset(primary); enemy.TakeDamage(enemy.MaxHealth * 100);
            await Until(() => game.EndingTime >= 6.3f);
            Check(game.Phase == RunPhase.Ending, primary + " final form dissolves without breaking ending");
            game.RestartSession(); await Task.Yield();
            Check(!game.EndingCompleted && game.World.Find("Ending presentation") == null, "Mid-ending restart removes all presentation objects");
        }
        Reset(Essence.SwordWave, 1); enemy.TakeDamage(enemy.MaxHealth * 100);
        Check(game.Phase == RunPhase.GuardianFallen, "Ordinary boss defeat keeps existing progression");
        game.ReturnToTitle(); game.StartOpening();
        Check(game.Phase == RunPhase.Tutorial && game.Tutorial != null, "Tutorial still starts after ending restart");
        game.ReturnToTitle();
    }
    static async void Run()
    {
        string verdict;
        try
        {
            Directory.CreateDirectory(Output);
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 24;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            target = new RenderTexture(1280, 720, 24); image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            Application.logMessageReceived += Log;
            if (!SessionState.GetBool(Key + ".EndingOnly", false))
            {
                CheckFragmentRedistribution();
                await CheckMoonsAndCameras();
            }
            await CheckEnding();
            Check(runtimeError == null, "No new gameplay error: " + runtimeError);
            verdict = "PASS: " + checks + (SessionState.GetBool(Key + ".EndingOnly", false) ? " ending, restart and tutorial checks" : " ending, round-moon, GD05 camera, restart and tutorial checks") + "; pre-existing geometry messages=" + knownWarnings;
        }
        catch (Exception ex) { verdict = "FAIL: " + ex; Debug.LogException(ex); }
        finally
        {
            Time.captureDeltaTime = 0;
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (image != null) UnityEngine.Object.Destroy(image);
        }
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/result.txt", verdict);
        SessionState.SetBool(Key, false); Debug.Log(verdict); EditorApplication.Exit(verdict.StartsWith("PASS:") ? 0 : 1);
    }
}
#endif
