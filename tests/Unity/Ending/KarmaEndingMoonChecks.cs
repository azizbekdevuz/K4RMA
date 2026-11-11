#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using KarmaPrototype;

[InitializeOnLoad]
public static class KarmaEndingMoonChecks
{
    const string Key = "K4RMA.EndingMoonChecks";
    const string Output = "output/validation/ending-moon";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static bool started, recordGD05;
    static int captureIndex, lastCaptureFrame = -1;
    static int checks, knownWarnings;
    static string runtimeError;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static RenderTexture target;
    static Texture2D image;
    static Rect renderedViewport;
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
    [MenuItem("K4RMA/Artwork/Validate moon and camera zoom")]
    public static void RunArtworkInEditor()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before validation.");
        SessionState.SetBool(Key + ".ArtworkOnly", true);
        SessionState.SetBool(Key + ".Editor", true);
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        RunBatch();
    }
    public static void RunBatch()
    {
        SessionState.SetBool(Key + ".EndingOnly", false);
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    public static void RunEndingBatch() { RunBatch(); SessionState.SetBool(Key + ".EndingOnly", true); }
    public static void RunArtworkBatch() { SessionState.SetBool(Key + ".ArtworkOnly", true); RunBatch(); }
    static async Task Until(Func<bool> done, float timeout = 90)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!done())
        {
            if (EditorApplication.timeSinceStartup > end) throw new Exception("Ending/camera timeout");
            if (SessionState.GetBool(Key + ".ArtworkOnly", false))
            {
                CheckCoverage();
                if (recordGD05 && Time.frameCount != lastCaptureFrame && Time.frameCount % 3 == 0)
                {
                    lastCaptureFrame = Time.frameCount;
                    Render("gd05-frames/" + (captureIndex++).ToString("D4"));
                }
            }
            await Task.Delay(10);
        }
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
        // Native viewport coordinates belong to the Game View's dimensions.
        // Applying a portrait Game View rect to a 16:9 target distorts the test image.
        var captureTarget = nativeViewport
            ? RenderTexture.GetTemporary(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 24) : target;
        try
        {
            // The output texture is 16:9; do not apply the Game View's letterbox a second time.
            camera.targetTexture = captureTarget;
            if (!nativeViewport) camera.rect = new Rect(0, 0, 1, 1);
            RenderTexture.active = captureTarget;
            GL.Clear(true, true, Color.clear);
            renderedViewport = camera.pixelRect;
            camera.Render(); RenderTexture.active = captureTarget;
            if (image.width != captureTarget.width || image.height != captureTarget.height)
                image.Reinitialize(captureTarget.width, captureTarget.height);
            image.ReadPixels(new Rect(0, 0, captureTarget.width, captureTarget.height), 0, 0); image.Apply();
            if (name != null) File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = prior; camera.rect = priorRect; RenderTexture.active = active;
            if (nativeViewport) RenderTexture.ReleaseTemporary(captureTarget);
        }
    }
    static float MoonRatio(bool mountain, bool nativeViewport = false)
    {
        if (mountain) return EmbeddedMountainMoonRatio(nativeViewport);
        var moon = game.World.Find("Round moon").GetComponent<SpriteRenderer>();
        Color32[] without;
        try { moon.enabled = false; Render(nativeViewport: nativeViewport); without = image.GetPixels32(); }
        finally { moon.enabled = true; }
        Render(nativeViewport: nativeViewport);
        int width = image.width, height = image.height;
        int minX = width, minY = height, maxX = -1, maxY = -1;
        var pixels = image.GetPixels32();
        // Compare the actual moon contribution; warm windows must not count as moon pixels.
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (pixels[y * width + x].Equals(without[y * width + x])) continue;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }
        Check(maxX > minX && maxY > minY, "Moon visible in rendered frame");
        float ratio = (maxX - minX + 1f) / (maxY - minY + 1f);
        Debug.Log("MOON ratio=" + ratio + " size=" + (maxX - minX + 1) + "x" + (maxY - minY + 1));
        return ratio;
    }
    static float EmbeddedMountainMoonRatio(bool nativeViewport)
    {
        Render(nativeViewport ? "restored-mountain-native" : "restored-mountain", nativeViewport: nativeViewport);
        var bounds = Background.bounds;
        var camera = game.GameCamera;
        // Measure only the original moon's known source region, excluding warm house windows.
        Vector3 World(float x, float y) => new Vector3(bounds.min.x + (x + 669) / 4682 * bounds.size.x,
            bounds.max.y - y / 1882 * bounds.size.y, bounds.center.z);
        var lo = camera.WorldToViewportPoint(World(1207, 617.5f));
        var hi = camera.WorldToViewportPoint(World(1295, 529.5f));
        var viewport = renderedViewport;
        int left = Mathf.Max(0, Mathf.FloorToInt(viewport.x + lo.x * viewport.width));
        int right = Mathf.Min(image.width - 1, Mathf.CeilToInt(viewport.x + hi.x * viewport.width));
        int bottom = Mathf.Max(0, Mathf.FloorToInt(viewport.y + lo.y * viewport.height));
        int top = Mathf.Min(image.height - 1, Mathf.CeilToInt(viewport.y + hi.y * viewport.height));
        int minX = image.width, minY = image.height, maxX = -1, maxY = -1;
        var pixels = image.GetPixels32();
        for (int y = bottom; y <= top; y++)
        for (int x = left; x <= right; x++)
        {
            var pixel = pixels[y * image.width + x];
            if (pixel.r < 150 || pixel.g < 135 || pixel.b < 105 || pixel.r <= pixel.b) continue;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }
        Check(maxX > minX && maxY > minY, "Original embedded mountain moon is visible");
        float expectedWidth = 78f / 4682 * bounds.size.x / (camera.orthographicSize * 2 * camera.aspect) * viewport.width;
        Check(Mathf.Abs(maxX - minX + 1 - expectedWidth) <= 3,
            $"Embedded moon retains its original projected diameter without a shrinking overlay (actual={maxX-minX+1}x{maxY-minY+1}, expected width={expectedWidth}, viewport={viewport})");
        return (maxX - minX + 1f) / (maxY - minY + 1f);
    }
    static void SettleCamera()
    {
        for (int i = 0; i < 100; i++) { Call(game, "LateUpdate"); CheckCoverage(); }
    }
    static void CheckMoonExterior()
    {
        var moon = game.World.Find("Round moon").GetComponent<SpriteRenderer>();
        var camera = game.GameCamera;
        var view = camera.WorldToViewportPoint(moon.transform.position);
        float cx = view.x * 1280, cy = view.y * 720;
        float diskFraction = moon.sprite.rect.width == 87 ? 79f / 87 : Mathf.Sqrt((78f / 86) * (79f / 87));
        float radius = Mathf.Sqrt(moon.bounds.size.x * moon.bounds.size.y) * diskFraction * 0.5f
            / (camera.orthographicSize * 2) * 720;
        Color32[] without;
        try { moon.enabled = false; Render(); without = image.GetPixels32(); }
        finally { moon.enabled = true; }
        Render(); var with = image.GetPixels32(); int changed = 0, outside = 0;
        for (int y = 0; y < 720; y++)
        for (int x = 0; x < 1280; x++)
        {
            int i = y * 1280 + x;
            if (without[i].Equals(with[i])) continue;
            changed++;
            if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)
                > (radius + 2) * (radius + 2)) outside++;
        }
        Check(changed > 100, "Extracted moon contributes visible disk pixels");
        Check(outside == 0, "Moon draws no halo, sky patch or residual pixels outside the disk: " + outside);
    }
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
        if (message.StartsWith("Not allowed to override geometry") && stack.Contains("Assets/Scripts/"))
        { knownWarnings++; return; }
        if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert) && stack.Contains("Assets/Scripts/")) runtimeError = message;
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
        Check(flanks == 8, "Four low-angle fragments on each flank retain six upper rays");
        Call(enemy, "InterruptPattern");
    }
    static async Task CheckMountainTraversal()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var map = (InputActionMap)typeof(KarmaInput).GetField("map", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var devices = map.devices;
        string bindings = map.SaveBindingOverridesAsJson();
        bool enemyActive = enemy.gameObject.activeSelf;
        try
        {
            map.devices = new[] { keyboard }; map.RemoveAllBindingOverrides();
            enemy.gameObject.SetActive(false);
            game.Player.ResetForRoom(new Vector2(KarmaStage.ArenaLeft + 2, KarmaSpace.PlayerHalfHeight + 0.05f), true);
            Directory.CreateDirectory(Output + "/mountain-traverse");
            int index = 0, previous = -1;
            foreach (int direction in new[] { 1, -1 })
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(direction > 0
                    ? UnityEngine.InputSystem.Key.D : UnityEngine.InputSystem.Key.A));
                double deadline = EditorApplication.timeSinceStartup + 30;
                float destination = direction > 0 ? KarmaStage.ArenaRight - 2 : KarmaStage.ArenaLeft + 2;
                while (direction * (destination - game.Player.Position.x) > 0)
                {
                    if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Mountain traversal movement stopped");
                    CheckCoverage();
                    if (Time.frameCount != previous && Time.frameCount % 3 == 0)
                    {
                        previous = Time.frameCount; Render("mountain-traverse/" + (index++).ToString("D4"));
                    }
                    await Task.Delay(10);
                }
                Check(Mathf.Abs(game.Player.Position.y - KarmaSpace.PlayerHalfHeight) < 0.25f,
                    "Player crosses the mountain arena on its original level collision floor");
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Task.Delay(50);
        }
        finally
        {
            map.devices = devices; map.LoadBindingOverridesFromJson(bindings);
            InputSystem.RemoveDevice(keyboard); enemy.gameObject.SetActive(enemyActive);
            game.Player.ResetForRoom(new Vector2(16, KarmaSpace.PlayerHalfHeight + 0.05f), true);
            SettleCamera();
        }
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
        foreach (var primary in SessionState.GetBool(Key + ".ArtworkOnly", false) ? new[] { Essence.Guard } : new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard })
        foreach (int stage in new[] { 0, 3 })
        {
            Reset(primary, stage); SettleCamera();
            var bg = Background; var before = bg.bounds; var frame = (Bounds)Get(game, "stageBackgroundBounds");
            Check(bg.sprite.rect.width == 4682 && bg.sprite.rect.height == 1882
                && bg.sprite.texture.width == 4682 && bg.sprite.texture.height == 1882,
                "Expanded background imports at native 4682x1882 without downsampling");
            Check(Mathf.Abs(bg.transform.localScale.x - bg.transform.localScale.y) < 0.0001f
                && Mathf.Abs(bg.bounds.size.x / bg.bounds.size.y - 4682f / 1882) < 0.0001f,
                "Background uses identical X/Y scale without stretching the artwork");
            Check(bg.bounds.size.x >= 140 && Mathf.Abs(bg.bounds.size.y - 50 * 941f / 1672 * 2) < 0.001f,
                "Background keeps native vertical detail and covers the original zoom range");
            Check(bg.sharedMaterial == (game.Config.spriteMaterial != null ? game.Config.spriteMaterial : KarmaVisuals.DefaultMaterial) && !bg.HasPropertyBlock(),
                "Background has no sky reconstruction material or property overrides");
            if (stage == 0)
                Check(game.World.Find("Round moon") == null, "Restored mountain plate draws its embedded moon only once");
            else
            {
                var moonSprite = game.World.Find("Round moon").GetComponent<SpriteRenderer>().sprite;
                Check(moonSprite == Resources.Load<Sprite>("Backgrounds/TrialHall/Moon"),
                    "Trial hall moon uses the approved original-pixel extraction");
                CheckMoonExterior();
            }
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
            if ((primary == Essence.SwordWave || SessionState.GetBool(Key + ".ArtworkOnly", false)) && stage == 0) Render("moon-mountain");
            if (stage == 0 && SessionState.GetBool(Key + ".ArtworkOnly", false)) await CheckMountainTraversal();
            if ((primary == Essence.SwordWave || SessionState.GetBool(Key + ".ArtworkOnly", false)) && stage == 3)
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
            foreach (float edge in new[] { KarmaStage.ArenaLeft + 1, KarmaStage.ArenaRight - 1 })
            foreach (bool inheriting in new[] { false, true })
            {
                game.Player.ResetForRoom(new Vector2(edge, KarmaSpace.PlayerHalfHeight), true);
                enemy.transform.position = new Vector3(edge, KarmaSpace.GuardianGroundY, 0);
                Set(game, "guardianInheriting", inheriting);
                SettleCamera(); CheckCoverage();
                Check(Mathf.Abs(game.GameCamera.orthographicSize - normal * (inheriting ? 0.8f : 0.75f)) < 0.1f,
                    "Original player/guardian zoom stays covered at arena edges");
                Render("zoom-in-stage-" + stage + "-" + (inheriting ? "guardian" : "player") + "-" + edge);
                typeof(KarmaGame).GetProperty("Phase").SetValue(game, RunPhase.Combat);
                SettleCamera(); CheckCoverage();
                // After real input traversal, Editor frames can be very short. Let the camera's
                // unscaled-time blend actually finish rather than assuming 100 calls equal elapsed time.
                await Until(() => Mathf.Abs(game.GameCamera.orthographicSize - normal) < 0.1f, 10);
                Check(Mathf.Abs(game.GameCamera.orthographicSize - normal) < 0.1f, "Zoom-out returns to original combat size without black margins");
                typeof(KarmaGame).GetProperty("Phase").SetValue(game, RunPhase.Transition);
            }
            Set(game, "guardianInheriting", false);
            Check(bg.bounds == before, "Moon correction never changes background geometry");
        }
        Reset(Essence.Guard); SettleCamera(); float baseline = game.GameCamera.orthographicSize;
        Set(Get(enemy, "behavior"), "PracticeHold", true);
        game.Player.ToggleTestInvincibility();
        if (SessionState.GetBool(Key + ".ArtworkOnly", false))
        {
            Directory.CreateDirectory(Output + "/gd05-frames"); recordGD05 = true;
        }
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
        try { foreach (var renderer in foreground) renderer.enabled = false; moonRatio = MoonRatio(false); CheckMoonExterior(); }
        finally { foreach (var renderer in foreground) if (renderer != null) renderer.enabled = true; }
        Check(Mathf.Abs(moonRatio - 1) < 0.06f, "Moon remains round in GD05 zoom-out");
        CheckCoverage(); Render("gd05-zoom");
        await Until(() => !enemy.TrialActive && enemy.GetType().GetProperty("CameraFocus", F).GetValue(enemy) == null);
        await Until(() => Mathf.Abs(game.GameCamera.orthographicSize - baseline) < 0.05f);
        recordGD05 = false;
        SettleCamera();
        Check(Mathf.Abs(game.GameCamera.orthographicSize - baseline) < 0.1f, "GD05 camera returns after trial ends");
        Call(enemy, "InterruptPattern"); SettleCamera(); CheckCoverage(); Render("gd05-return");
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
                if (!SessionState.GetBool(Key + ".ArtworkOnly", false)) CheckFragmentRedistribution();
                await CheckMoonsAndCameras();
            }
            if (!SessionState.GetBool(Key + ".ArtworkOnly", false)) await CheckEnding();
            Check(runtimeError == null, "No new gameplay error: " + runtimeError);
            string scope = SessionState.GetBool(Key + ".ArtworkOnly", false)
                ? " native background resolution, original moon, no-halo, camera coverage and zoom checks"
                : SessionState.GetBool(Key + ".EndingOnly", false) ? " ending, restart and tutorial checks"
                : " ending, round-moon, GD05 camera, restart and tutorial checks";
            verdict = "PASS: " + checks + scope + "; pre-existing geometry messages=" + knownWarnings;
        }
        catch (Exception ex) { verdict = "FAIL: " + ex; Debug.LogException(ex); }
        finally
        {
            Time.captureDeltaTime = 0;
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (image != null) UnityEngine.Object.Destroy(image);
        }
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/result.txt", verdict);
        Application.logMessageReceived -= Log;
        SessionState.SetBool(Key, false); Debug.Log(verdict);
        if (SessionState.GetBool(Key + ".Editor", false))
        {
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            SessionState.EraseBool(Key + ".Editor"); SessionState.EraseBool(Key + ".ArtworkOnly");
            EditorApplication.ExitPlaymode();
        }
        else EditorApplication.Exit(verdict.StartsWith("PASS:") ? 0 : 1);
    }
}
#endif
