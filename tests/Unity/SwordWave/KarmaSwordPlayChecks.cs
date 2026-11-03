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

// Run only in a disposable project via tools/run-boss-playchecks.py --suite sword.
[InitializeOnLoad]
public static class KarmaSwordPlayChecks
{
    const string BatchKey = "K4RMA.SwordPlayChecks.Batch";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static int checks;
    static bool started;
    static string runtimeError;
    static readonly List<string> results = new List<string>();
    static KarmaSwordPlayChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(BatchKey, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        // Unity Search can enable Error Pause during a fresh cache's startup.
        // Runtime errors are captured only once the test itself has started.
        if (!started && EditorApplication.isPlaying) EditorApplication.isPaused = false;
        if (!started && EditorApplication.isPlaying && Time.frameCount > 1)
        { started = true; RunChecks(); }
    }
    public static void RunBatch()
    {
        Debug.Log("Starting SwordWave Play Mode validation");
        SessionState.SetBool(BatchKey, true);
        if (!SessionState.GetBool(BatchKey + ".SpacingOnly", false)) CompilePlayerScripts();
        var gameView = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        gameView.Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static void CompilePlayerScripts()
    {
        Directory.CreateDirectory("output/validation/player-scripts");
        var settings = new UnityEditor.Build.Player.ScriptCompilationSettings
        {
            target = EditorUserBuildSettings.activeBuildTarget,
            group = BuildTargetGroup.Standalone,
            subtarget = (int)StandaloneBuildSubtarget.Player,
            options = UnityEditor.Build.Player.ScriptCompilationOptions.None
        };
        var result = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(settings, "output/validation/player-scripts");
        bool runtime = false;
        if (result.assemblies != null)
            foreach (string path in result.assemblies) runtime |= Path.GetFileName(path) == "Assembly-CSharp.dll";
        if (!runtime)
            throw new Exception("Player script compilation produced no runtime assembly");
        File.WriteAllText("output/validation/sword-player-compilation.txt", "PASS: Unity Player script compilation; "
            + result.assemblies.Count + " assemblies. Editor compilation completed before executeMethod.\n");
    }
    public static void RunSpacingBaselineBatch()
    {
        SessionState.SetBool(BatchKey + ".SpacingOnly", true);
        RunBatch();
    }
    static async void RunChecks()
    {
        int exit = 0;
        try
        {
            Application.runInBackground = true;
            Application.logMessageReceived += CaptureError;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            Check(game != null, "game scene starts");
            var config = UnityEngine.Object.Instantiate(game.Config);
            Set(game, "config", config); config.playerHealth = 100000;
            bool spacingOnly = SessionState.GetBool(BatchKey + ".SpacingOnly", false);
            if (!spacingOnly)
            {
                await CheckSW01();
                await CheckSW03(false); await CheckSW03(true);
                await CheckScarCancellation();
                await CheckSW04();
                await CheckBasics();
                await CheckSwordDamageFeedback();
                await CheckForcedInterruption();
                CheckNewArtworkImports();
                CheckOtherBossDamagePresentation();
                await CheckMovement();
            }
            await CheckLiveSpacingAndBasics(!spacingOnly);
            Check(runtimeError == null, "no runtime exceptions: " + runtimeError);
            results.Insert(0, "PASS: " + checks + " SwordWave Play Mode assertions");
        }
        catch (Exception error) { results.Insert(0, "FAIL: " + error); Debug.LogException(error); exit = 1; }
        Directory.CreateDirectory("output/validation");
        File.WriteAllLines("output/validation/sword-play-validation.txt", results);
        SessionState.SetBool(BatchKey, false); SessionState.SetBool(BatchKey + ".SpacingOnly", false);
        EditorApplication.Exit(exit);
    }
    static void CaptureError(string message, string stack, LogType type)
    { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) runtimeError = message + "\n" + stack; }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static FieldInfo Field(object target, string name)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, Flags); if (field != null) return field; }
        throw new MissingFieldException(target.GetType().Name, name);
    }
    static T Get<T>(object target, string name) { return (T)Field(target, name).GetValue(target); }
    static void Set(object target, string name, object value) { Field(target, name).SetValue(target, value); }
    static object Call(object target, string name, params object[] args)
    { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
    static object Behavior { get { return Get<object>(enemy, "behavior"); } }
    static object Movement { get { return Get<object>(Behavior, "movement"); } }
    static Transform Root { get { return Get<Transform>(enemy, "effectsRoot"); } }
    static KarmaBossAttack[] Attacks() { return Root.GetComponentsInChildren<KarmaBossAttack>(); }
    static void Place(float x)
    {
        var position = new Vector2(x, KarmaSpace.PlayerHalfHeight + 0.02f);
        game.Player.transform.position = position; game.Player.GetComponent<Rigidbody2D>().position = position;
        Physics2D.SyncTransforms();
    }
    static void Reset(Essence primary = Essence.SwordWave)
    {
        game.StartRun();
        var order = new List<Essence> { primary };
        foreach (var essence in new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard })
            if (essence != primary) order.Add(essence);
        foreach (var essence in order)
        { game.Player.Essences.Preview(essence); game.Player.Essences.Confirm(out _); }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
        Call(game, "LoadRoom", true);
        enemy = game.Enemies[0]; enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
        game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        Place(35); Set(game.Player, "invulnerableUntil", Time.time + 200);
        Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue); Set(enemy, "running", true);
    }
    static void Force(EnemyAttack pattern)
    {
        nextDamageAt = 0;
        Call(enemy, "InterruptPattern");
        Set(enemy, "selected", pattern); Set(enemy, "direction", game.Player.Position.x < enemy.Position.x ? -1f : 1f);
        Call(Behavior, "OnSelected");
        Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static async Task Until(Func<bool> predicate, float timeout = 10)
    {
        double deadline = EditorApplication.timeSinceStartup + timeout;
        while (!predicate())
        {
            if (EditorApplication.timeSinceStartup >= deadline) throw new Exception("Timed out / pattern " + Get<EnemyAttack>(enemy, "selected"));
            await Task.Delay(10);
        }
    }
    static Task Seconds(float duration)
    { float target = enemy.FightSeconds + duration; return Until(() => enemy.FightSeconds >= target, duration + 10); }
    static async Task Finished(Action observe)
    {
        double deadline = EditorApplication.timeSinceStartup + 30;
        while (Get<bool>(enemy, "running"))
        {
            observe();
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Pattern did not complete");
            await Task.Delay(10);
        }
        // Keep the test arrangement still after the coroutine completes.
        Set(enemy, "running", true);
    }
    static void Snapshot(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var camera = Camera.main; if (camera == null) return;
        var texture = new RenderTexture(1400, 600, 24); var previous = camera.targetTexture;
        camera.targetTexture = texture;
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = texture });
        else camera.Render();
        var active = RenderTexture.active; RenderTexture.active = texture;
        var picture = new Texture2D(1400, 600, TextureFormat.RGBA32, false);
        picture.ReadPixels(new Rect(0, 0, 1400, 600), 0, 0); picture.Apply();
        Directory.CreateDirectory("output/validation");
        File.WriteAllBytes("output/validation/" + name + ".png", picture.EncodeToPNG());
        camera.targetTexture = previous; RenderTexture.active = active;
        UnityEngine.Object.Destroy(picture); UnityEngine.Object.Destroy(texture);
    }
    static async Task CheckSW01()
    {
        Reset();
        // Match the game's normal startup: its runtime pixel sprite is prepared
        // before the first decision/warning, rather than forced in its birth frame.
        await Seconds(0.05f); Force(EnemyAttack.SW01);
        var ids = new HashSet<KarmaBossAttack>(); var heights = new List<float>();
        await Finished(() =>
        {
            RepeatedOrdinaryDamage();
            foreach (var attack in Attacks()) if (ids.Add(attack))
            {
                heights.Add(attack.transform.position.y);
                Check(attack.Visual.sprite.name == "SW01_StaggeredWave_v3", "SW01 new PNG applied");
                Check(Get<Vector2>(attack, "size") == new Vector2(3.24f, 1.3f), "SW01 hit size preserved");
                Check(Mathf.Abs(Get<Vector2>(attack, "velocity").x) == game.Config.BossTuning.sword.swordSpeed, "SW01 shot speed preserved");
                Snapshot("sw01-release");
            }
        });
        Check(ids.Count == 2, "SW01 exactly two shots");
        Check(Mathf.Abs(heights[0] - 2.1f) < 0.001f && Mathf.Abs(heights[1] - 6.6f) < 0.001f, "SW01 low/high lanes preserved");
        results.Add("SW01: two original low/high projectiles, unchanged speed/hit shape, new PNG.");
    }
    static async Task CheckSW03(bool enraged)
    {
        Reset(); Place(-13.8f);
        if (enraged) typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.1f);
        Force(EnemyAttack.SW03);
        var ids = new HashSet<KarmaBossAttack>(); int rounds = 0; bool rising = false, paused = false, warningCaptured = false;
        var warnedCenters = new HashSet<float>();
        double deadline = EditorApplication.timeSinceStartup + 30;
        while (Get<bool>(enemy, "running"))
        {
            RepeatedOrdinaryDamage();
            var attacks = Attacks();
            if (attacks.Length == 0)
            {
                var current = new HashSet<float>();
                foreach (Transform child in Root)
                    if (child.gameObject.activeInHierarchy && child.name.StartsWith("SW03 sword-cut warning / cell "))
                        current.Add(child.position.x);
                if (current.Count == 5)
                {
                    warnedCenters = current;
                    if (!warningCaptured && !enraged) { warningCaptured = true; Snapshot("sw03-warning"); }
                }
            }
            if (attacks.Length > 0 && !rising)
            {
                rising = true; rounds++;
                Check(attacks.Length == 10, "SW03 five pairs / ten simultaneous blades");
                var cells = new HashSet<int>();
                var ledgers = new Dictionary<KarmaAttackContact, int>();
                foreach (var attack in attacks)
                {
                    ids.Add(attack);
                    float x = attack.transform.position.x;
                    int cell = int.Parse(System.Text.RegularExpressions.Regex.Match(attack.Visual.name, @"cell (\d+)").Groups[1].Value);
                    cells.Add(cell);
                    float center = Mathf.Clamp(-15 + (cell + 0.5f) * 7, -10.5f, 50.5f);
                    Check(warnedCenters.Contains(center), "SW03 pair shares its locked crack warning's center");
                    Check(cell >= 0 && cell < 10 && Mathf.Abs(Mathf.Abs(x - center) - 2.25f) < 0.001f, "SW03 two distinct blade centers per selected cell");
                    var size = Get<Vector2>(attack, "size");
                    Check(size == new Vector2(4.5f, 27), "SW03 paired footprint 9 x 27, double original width/height");
                    Check(x - size.x / 2 >= -15 && x + size.x / 2 <= 55, "SW03 entire contact inside arena");
                    Check(attack.Visual.sprite.name == "SW03_RisingBlade_v4", "SW03 v4 rising PNG");
                    Check(Mathf.Abs(attack.Visual.transform.localScale.y * 1751 / 100 - size.y) < 0.001f,
                        "SW03 full hit height visible from the first active frame");
                    var ledger = Get<KarmaAttackContact>(attack, "contact");
                    ledgers[ledger] = ledgers.TryGetValue(ledger, out int count) ? count + 1 : 1;
                }
                Check(cells.Count == 5, "SW03 no duplicate cells");
                Check(ledgers.Count == 5, "SW03 each pair shares exactly one damage ledger");
                foreach (int count in ledgers.Values) Check(count == 2, "SW03 pair cannot deal duplicate seam damage");
                Snapshot(enraged ? "sw03-enraged-rise" : "sw03-rise");
                if (!paused)
                {
                    paused = true; Call(game, "TogglePause"); float clock = enemy.FightSeconds;
                    float life = Get<float>(attacks[0], "remaining");
                    await Task.Delay(250);
                    Check(enemy.FightSeconds == clock && Get<float>(attacks[0], "remaining") == life, "pause freezes scar clock and contacts");
                    Call(game, "TogglePause");
                }
            }
            if (attacks.Length == 0) rising = false;
            Check(EditorApplication.timeSinceStartup < deadline, "SW03 completes within timeout");
            await Task.Delay(15);
        }
        Set(enemy, "running", true);
        Check(rounds == (enraged ? 3 : 2) && ids.Count == rounds * 10, "SW03 normal/enraged round count and doubled total blades");
        Check(Attacks().Length == 0, "SW03 recovery clears every contact");
        results.Add("SW03 " + (enraged ? "enraged" : "normal") + ": " + rounds + " x ten blades in five cells; doubled footprint, shared pair ledgers, pause, bounds and cleanup pass.");
    }
    static async Task CheckScarCancellation()
    {
        Reset(); Force(EnemyAttack.SW03); await Seconds(0.1f);
        Check(Root.GetComponentsInChildren<SpriteRenderer>().Length >= 5, "SW03 five visible crack warnings");
        Call(enemy, "InterruptPattern");
        Check(Root.GetComponentsInChildren<SpriteRenderer>().Length == 0, "interruption clears warnings immediately");
        Force(EnemyAttack.SW03); await Until(() => Attacks().Length == 10);
        Place(Attacks()[0].transform.position.x);
        float invulnerableHealth = game.Player.Health;
        await Seconds(0.08f);
        Check(game.Player.Health == invulnerableHealth && Root.Find("SW03 confirmed hit") == null,
            "SW03 miss/invulnerability produces no false damage feedback");
        Force(EnemyAttack.SW03); await Until(() => Attacks().Length == 10);
        Place(Attacks()[0].transform.position.x); Set(game.Player, "invulnerableUntil", 0f);
        float health = game.Player.Health; await Until(() => game.Player.Health < health);
        await Until(() => Root.Find("SW03 confirmed hit") != null);
        var hit = Root.Find("SW03 confirmed hit");
        Check(hit.GetComponentInChildren<SpriteRenderer>().sprite.name == "SW03_HitFlash_v4", "SW03 confirmed damage uses dedicated v4 hit PNG");
        Snapshot("sw03-confirmed-hit");
        var retainedRoot = Root;
        enemy.TakeDamage(enemy.MaxHealth * 10); await Task.Delay(100);
        Check(retainedRoot == null || retainedRoot.GetComponentsInChildren<KarmaBossAttack>().Length == 0, "death clears active scar contacts");
        results.Add("SW03 real collision emits dedicated hit flash; invulnerability suppresses false feedback; interruption/death clears all owned effects.");
    }
    static async Task CheckSW04()
    {
        Reset(); Force(EnemyAttack.SW04);
        var counter = Get<object>(Behavior, "counter");
        await Until(() => { RepeatedOrdinaryDamage(); return (Transform)counter.GetType().GetProperty("EffectsRoot").GetValue(counter) != null; });
        var root = (Transform)counter.GetType().GetProperty("EffectsRoot").GetValue(counter);
        var blades = new List<SpriteRenderer>();
        foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>())
            if (renderer.gameObject.name.StartsWith("회전 검막")) blades.Add(renderer);
        Check(blades.Count == 4, "SW04 four orbiting swords");
        var start = blades[0].transform.position; await Seconds(0.15f);
        Check(Vector3.Distance(start, blades[0].transform.position) > 0.1f, "SW04 Sin/Cos orbit advances");
        Check(blades[0].sprite.name == "SW04_CounterBlade_v2", "SW04 new blade PNG");
        Snapshot("sw04-orbit");
        enemy.TakeDamage(1); enemy.TakeDamage(1);
        var ids = new HashSet<KarmaBossAttack>(); bool aimCaptured = false;
        await Until(() => Get<bool>(counter, "guardResponseRunning"));
        Call(enemy, "ClearPatternObjects");
        Check(root != null && root.gameObject.activeInHierarchy, "ordinary cleanup preserves independent ward");
        double deadline = EditorApplication.timeSinceStartup + 15;
        while (root != null && root.gameObject.activeInHierarchy)
        {
            RepeatedOrdinaryDamage();
            foreach (var attack in root.GetComponentsInChildren<KarmaBossAttack>())
            {
                ids.Add(attack);
                Check(Get<EnemyAttack>(attack, "sourcePattern") == EnemyAttack.SW04, "independent counter retains its damage-feedback source");
            }
            if (!aimCaptured && root.GetComponentsInChildren<KarmaAttackWarning>().Length > 0)
            { aimCaptured = true; Snapshot("sw04-aim"); }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("SW04 did not clean up");
            await Task.Delay(15);
        }
        Check(ids.Count == 4, "SW04 repeated damage commits one four-shot sequential response");
        results.Add("SW04: four orbiting blades; real damage triggers one independent sequential four-shot response and cleanup.");
    }
    static async Task CheckBasics()
    {
        Reset(); Place(25); Set(enemy, "running", false); Call(enemy, "BeginAttack");
        Check(Get<EnemyAttack>(enemy, "selected") == EnemyAttack.SWBasic, "close range selects basic");
        Check((bool)Behavior.GetType().GetProperty("BasicMeleeSelected").GetValue(Behavior), "close basic is melee");
        Place(29); await Until(() => Attacks().Length > 0);
        Check(Get<Vector2>(Attacks()[0], "velocity") == Vector2.zero, "moving out during windup does not swap melee to wave");
        Check(Attacks()[0].Visual.sprite == KarmaWaveArtwork.Sprite("SWBasic_CrescentWave_v3"), "melee uses shared sword crescent");
        Snapshot("swbasic-melee");
        Reset(); Place(29); Set(enemy, "running", false); Call(enemy, "BeginAttack");
        Check(!(bool)Behavior.GetType().GetProperty("BasicMeleeSelected").GetValue(Behavior), "medium basic is a wave");
        await Until(() => Attacks().Length > 0);
        Check(Get<Vector2>(Attacks()[0], "velocity").x > 0, "medium wave aims toward player");
        Check(Attacks()[0].Visual.sprite == KarmaWaveArtwork.Sprite("SWBasic_CrescentWave_v3"), "medium wave uses shared sword crescent");
        Snapshot("swbasic-wave");
        Reset(); typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.49f);
        object[] args = { 15f, true, true, EnemyAttack.SWBasic };
        Check((bool)Call(Behavior, "TrySelect", args) && (EnemyAttack)args[3] == EnemyAttack.SW05,
            "health-gated SW05 retains priority over a due basic");
        results.Add("SWBasic: close melee, medium wave, committed windup mode; current SW05 health priority retained.");
    }
    static float nextDamageAt;
    static void RepeatedOrdinaryDamage()
    {
        var animator = Get<KarmaCharacterAnimator>(enemy, "animator");
        Check(animator.CurrentPose != "Hurt", "ordinary damage never masks a sword attack with Hurt");
        if (game.Paused || enemy.FightSeconds < nextDamageAt) return;
        nextDamageAt = enemy.FightSeconds + 0.075f;
        float health = enemy.Health, startedAt = Get<float>(enemy, "patternStarted");
        enemy.TakeDamage(0.1f);
        Check(enemy.Health < health, "repeated incoming hits still deal damage");
        Check(!enemy.Stunned && Get<float>(enemy, "patternStarted") == startedAt,
            "ordinary damage does not restart or stun the committed pattern");
        Check(Get<float>(animator, "hurtUntil") <= Time.time, "ordinary sword damage uses overlay instead of Hurt reservation");
    }
    static async Task CheckSwordDamageFeedback()
    {
        foreach (float distance in new[] { 5f, 9f, -9f })
        {
            Reset(); Place(20 + distance); Force(EnemyAttack.SWBasic);
            var seen = new HashSet<KarmaBossAttack>();
            bool captured = false;
            await Finished(() =>
            {
                RepeatedOrdinaryDamage();
                foreach (var attack in Attacks())
                {
                    seen.Add(attack);
                    if (Mathf.Abs(distance) == 9)
                    {
                        Check(attack.Visual.sprite.name == "SWBasic_CrescentWave_v3", "both directions use new basic wave");
                        Check(Mathf.Sign(Get<Vector2>(attack, "velocity").x) == Mathf.Sign(distance), "wave follows latched aim");
                        if (!captured) { captured = true; Snapshot(distance < 0 ? "swbasic-left" : "swbasic-right"); }
                    }
                }
            });
            Check(seen.Count == 1, "SWBasic survives repeated hits through one full melee/wave release");
        }
        foreach (var pattern in new[] { EnemyAttack.SWBasic, EnemyAttack.SW01 })
        {
            Reset(); Place(29); Force(pattern);
            await Until(() => Attacks().Length > 0);
            Place(Attacks()[0].transform.position.x);
            Set(game.Player, "invulnerableUntil", 0f);
            float health = game.Player.Health;
            await Until(() => game.Player.Health < health);
            var flash = game.World.Find("Sword player confirmed hit");
            Check(flash != null && flash.GetComponentInChildren<SpriteRenderer>().sprite.name == "Sword_HitBurst_v2",
                pattern + " actual damage emits new player impact PNG");
            Check(game.World.Find("Effect") == null, "custom sword impact replaces the old red rectangle");
            Snapshot(pattern == EnemyAttack.SWBasic ? "swbasic-player-hit" : "sw01-player-hit");
        }
        Reset(); Force(EnemyAttack.SW01); await Seconds(0.06f);
        enemy.TakeDamage(1);
        var bossFlash = game.World.Find("Sword boss confirmed hit");
        Check(bossFlash != null && bossFlash.GetComponentInChildren<SpriteRenderer>().sprite.name == "Sword_HitBurst_v2",
            "real boss hit emits overlay PNG without touching animation");
        Check(game.World.Find("Effect") == null, "sword hit no longer stacks the old white rectangle");
        Snapshot("sword-boss-hit");
        await Seconds(0.3f);
        Check(game.World.Find("Sword boss confirmed hit") == null, "boss flash expires promptly");
        Reset(); Set(enemy, "selected", EnemyAttack.SW03); Set(game.Player, "invulnerableUntil", 0f);
        var overlap = new KarmaAttackContact();
        enemy.TryAttackHit(game.Player.Position, game.Player.Position, Vector2.one * 4, 1, overlap, 0, sourcePattern: EnemyAttack.SW04);
        Check(overlap.DealtDamage && game.World.Find("Sword player confirmed hit") != null && game.World.Find("Effect") == null,
            "SW04 hit during another pattern uses its own impact instead of SW03 feedback");
        results.Add("Repeated 75 ms ordinary damage: SWBasic melee/waves, SW01 two shots, SW03 full rounds, SW04 four counters survive; new boss/player confirmed-hit overlays verified.");
    }
    static async Task CheckForcedInterruption()
    {
        foreach (var pattern in new[] { EnemyAttack.SWBasic, EnemyAttack.SW01, EnemyAttack.SW03, EnemyAttack.SW04 })
        {
            Reset(); Place(25); Force(pattern); await Seconds(0.1f);
            Set(enemy, "pendingParry", true);
            await Until(() => enemy.Stunned);
            Check(!Get<bool>(enemy, "running") && Attacks().Length == 0, pattern + " forced parry/stun still cancels");
            Check(Get<KarmaCharacterAnimator>(enemy, "animator").CurrentPose == "Hurt", "stun keeps its explicit pose");
        }
        results.Add("Forced parry/stun still cancels SWBasic/SW01/SW03/SW04; death and restart cleanup retained.");
    }
    static void CheckNewArtworkImports()
    {
        foreach (string pose in new[] { "Sword_Crescent_v6", "SW03_GroundScar_v6", "SW03_RisingBlade_v6",
            "SW04_CounterBlade_v6", "Sword_GuardRing_v6", "Sword_Impact_v6", "Sword_Release_v6", "Sword_Shard_v6" })
        {
            string path = "Assets/K4RMA/Arts/Bosses/SwordWave/Effects/" + pose + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var sprite = KarmaWaveArtwork.Sprite(pose);
            Check(importer != null && sprite != null && sprite.name == pose, "library sprite imported: " + pose);
            Check(importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single,
                "single sprite import: " + pose);
            Check(importer.alphaIsTransparency && !importer.mipmapEnabled && importer.spritePixelsPerUnit == 100,
                "alpha/PPU/no-mipmap settings: " + pose);
            Check(importer.filterMode == FilterMode.Bilinear && importer.textureCompression == TextureImporterCompression.Uncompressed,
                "bilinear uncompressed import: " + pose);
            Check(Vector2.Distance(sprite.pivot, KarmaWaveArtwork.CorePixels(pose).center) < 0.02f,
                "pivot centers the cutting core: " + pose);
        }
        results.Add("All eight sword sprites load from the boss library with preserved import settings and pivots.");
    }
    static void CheckOtherBossDamagePresentation()
    {
        foreach (var primary in new[] { Essence.Guard, Essence.RisingSlash })
        {
            Reset(primary);
            Check(enemy.PrimaryType == primary, "correct non-sword boss regression setup");
            Set(enemy, "fullShield", false);
            enemy.TakeDamage(1);
            var animator = Get<KarmaCharacterAnimator>(enemy, "animator");
            Check(Get<float>(animator, "hurtUntil") > Time.time, primary + " retains ordinary Hurt animation");
            Check(game.World.Find("Sword boss confirmed hit") == null, primary + " does not receive sword-only VFX");
        }
        Reset(); Set(game.Player, "invulnerableUntil", 0f);
        game.Player.TakeDamage(1);
        Check(game.World.Find("Effect") != null, "ordinary player damage API retains original feedback");
        results.Add("Guard/RisingSlash damage retains original Hurt; sword-only overlay and white-flash suppression do not leak to other boss types.");
    }
    static async Task CheckMovement()
    {
        Reset(); Place(25);
        float start = enemy.Position.x;
        Check(!(bool)Call(Behavior, "TickMovement", 0.02f, 5f), "no pre-attack retreat at close range");
        Check(enemy.Position.x == start, "close player can reach first melee");
        Call(Behavior, "OnCompleted"); float previousSpeed = 0, peak = 0;
        for (int i = 0; i < 55; i++)
        {
            float before = enemy.Position.x;
            Call(Behavior, "TickMovement", 0.02f, game.Player.Position.x - before);
            float speed = Mathf.Abs(enemy.Position.x - before) / 0.02f;
            Check(speed <= 16.01f, "retreat speed capped");
            Check(speed - previousSpeed <= 56 * 0.02f + 0.01f, "retreat accelerates within configured limit");
            previousSpeed = speed; peak = Mathf.Max(peak, speed);
            await Seconds(0.02f);
        }
        Check(peak > 1 && enemy.Position.x < start, "retreat makes a bounded spacing step");
        float stopped = enemy.Position.x;
        for (int i = 0; i < 10; i++) Call(Behavior, "TickMovement", 0.02f, game.Player.Position.x - enemy.Position.x);
        Check(Mathf.Abs(stopped - enemy.Position.x) < 0.001f, "retreat does not repeat without another attack");
        Reset(); Place(48); start = enemy.Position.x;
        Check((bool)Call(Behavior, "TickMovement", 0.1f, 28f) && enemy.Position.x > start, "far range approaches");
        Reset(); Place(29); start = enemy.Position.x;
        Check(!(bool)Call(Behavior, "TickMovement", 0.1f, 9f) && enemy.Position.x == start, "medium range attacks without unnecessary approach");
        foreach (float dt in new[] { 1f / 30, 1f / 60, 1f / 120 })
        foreach (float initialGap in new[] { 5f, 8f })
        {
            Reset(); Place(20 + initialGap); Call(Behavior, "OnCompleted");
            float clock = enemy.FightSeconds, lastSpeed = 0;
            // Invoke the actual movement against real stage colliders with a controlled
            // fight clock. No render frame can advance this synchronous sample loop.
            for (int frame = 0; frame < Mathf.CeilToInt(1.35f / dt); frame++)
            {
                clock += dt; typeof(KarmaEnemy).GetProperty("FightSeconds").SetValue(enemy, clock);
                float before = enemy.Position.x;
                Call(Behavior, "TickMovement", dt, game.Player.Position.x - before);
                float speed = Mathf.Abs(enemy.Position.x - before) / dt;
                Check(speed <= 16.02f, "30/60/120 FPS actual movement speed limit");
                Check(speed - lastSpeed <= 56 * dt + 0.02f, "30/60/120 FPS actual acceleration limit");
                Check(lastSpeed - speed <= 72 * dt + 0.04f, "30/60/120 FPS actual braking limit");
                lastSpeed = speed;
            }
            Check(lastSpeed < 0.01f, "retreat settles at zero speed");
        }
        results.Add("Movement: no initial retreat; measured acceleration/speed cap; bounded retreat then attack token; far approach and medium hold.");
        results.Add("Movement: actual stage collision path obeys acceleration AND braking limits at 30/60/120 FPS.");
    }
    [Serializable]
    sealed class SpacingMetrics
    {
        public int closeStarts, closeMelee, closeRetreats, mediumStarts, mediumWaves;
        public float closeSeconds = 12, mediumSeconds = 7, minimumRetreatInterval = 999;
    }
    static async Task CheckLiveSpacingAndBasics(bool validate)
    {
        var metrics = new SpacingMetrics();
        Reset(); await Seconds(0.05f); Place(25.5f);
        Set(enemy, "running", false); Set(enemy, "timer", 0f);
        float end = enemy.FightSeconds + metrics.closeSeconds, nextPressure = enemy.FightSeconds + 0.6f;
        float lastStart = -1, lastRetreat = -100;
        bool moving = false, captured = false;
        var trace = new List<string>();
        var probe = new GameObject("Sword spacing frame observer").AddComponent<KarmaSwordFrameProbe>();
        probe.BeforeFrame = () =>
        {
            // A repeatable pursuer keeps pressure on the boss, while ordinary
            // Update/BeginAttack/RunPattern and all decision/recovery timers run.
            Place(Mathf.Min(KarmaStage.ArenaRight - 1, enemy.Position.x + 5.5f));
            if (enemy.FightSeconds >= nextPressure)
            { enemy.TakeDamage(0.1f); nextPressure += 0.6f; }
        };
        probe.AfterFrame = () =>
        {
            float start = Get<float>(enemy, "patternStarted");
            if (Get<bool>(enemy, "running") && start != lastStart)
            {
                lastStart = start; metrics.closeStarts++;
                trace.Add(enemy.FightSeconds.ToString("F3") + " start " + Get<EnemyAttack>(enemy, "selected") + " x=" + enemy.Position.x);
                if (Get<EnemyAttack>(enemy, "selected") == EnemyAttack.SWBasic
                    && (bool)Behavior.GetType().GetProperty("BasicMeleeSelected").GetValue(Behavior)) metrics.closeMelee++;
            }
            bool retreating = Get<float>(Movement, "swordRetreatRemaining") > 0;
            if (retreating && !moving)
            {
                metrics.closeRetreats++;
                trace.Add(enemy.FightSeconds.ToString("F3") + " retreat x=" + enemy.Position.x);
                if (lastRetreat > 0) metrics.minimumRetreatInterval = Mathf.Min(metrics.minimumRetreatInterval, enemy.FightSeconds - lastRetreat);
                lastRetreat = enemy.FightSeconds;
            }
            moving = retreating;
        };
        while (enemy.FightSeconds < end)
        {
            if (!captured && Get<EnemyAttack>(enemy, "selected") == EnemyAttack.SWBasic && Attacks().Length > 0)
            { captured = true; Snapshot("live-close-basic"); }
            await Task.Delay(15);
        }
        probe.BeforeFrame = probe.AfterFrame = null;
        Call(enemy, "InterruptPattern"); Set(enemy, "running", true);
        Reset(); await Seconds(0.05f); Place(28.7f);
        Set(enemy, "running", false); Set(enemy, "timer", 0f);
        end = enemy.FightSeconds + metrics.mediumSeconds; lastStart = -1; captured = false;
        probe.AfterFrame = () =>
        {
            float start = Get<float>(enemy, "patternStarted");
            if (Get<bool>(enemy, "running") && start != lastStart)
            {
                lastStart = start; metrics.mediumStarts++;
                if (Get<EnemyAttack>(enemy, "selected") == EnemyAttack.SWBasic)
                {
                    Check(!(bool)Behavior.GetType().GetProperty("BasicMeleeSelected").GetValue(Behavior), "live medium basic never selects melee");
                    metrics.mediumWaves++;
                }
            }
        };
        while (enemy.FightSeconds < end)
        {
            if (!captured && Get<EnemyAttack>(enemy, "selected") == EnemyAttack.SWBasic && Attacks().Length > 0)
            { captured = true; Snapshot("live-medium-wave"); }
            await Task.Delay(15);
        }
        probe.BeforeFrame = probe.AfterFrame = null;
        UnityEngine.Object.Destroy(probe.gameObject);
        Call(enemy, "InterruptPattern"); Set(enemy, "running", true);
        // Save observations before assertions so a failed run never leaves stale metrics.
        Directory.CreateDirectory("output/validation");
        File.WriteAllText("output/validation/sword-spacing-metrics.json", JsonUtility.ToJson(metrics, true));
        File.WriteAllLines("output/validation/sword-spacing-trace.txt", trace);
        if (validate)
        {
            Check(metrics.closeMelee >= 3, "live AI visibly chooses at least three close basics in 12 seconds");
            Check(metrics.closeRetreats >= 3, "live pressured AI repositions at least three times in 12 seconds");
            Check(metrics.closeRetreats <= metrics.closeStarts, "live retreat cannot outrun attack opportunities");
            Check(metrics.minimumRetreatInterval >= 2.38f, "live retreat respects 2.4-second cooldown");
            Check(metrics.mediumWaves >= 2, "live medium AI visibly chooses at least two basic waves in seven seconds");
        }
        Directory.CreateDirectory("output/validation");
        File.WriteAllText("output/validation/sword-spacing-metrics.json", JsonUtility.ToJson(metrics, true));
        results.Add("Live AI: close 12s = " + metrics.closeMelee + "/" + metrics.closeStarts + " melee basics, " + metrics.closeRetreats
            + " retreats; medium 7s = " + metrics.mediumWaves + "/" + metrics.mediumStarts + " basic waves; minimum retreat interval "
            + metrics.minimumRetreatInterval.ToString("F2") + "s.");
    }
}
[DefaultExecutionOrder(-1000)]
public sealed class KarmaSwordFrameProbe : MonoBehaviour
{
    public Action BeforeFrame, AfterFrame;
    void Update() { BeforeFrame?.Invoke(); }
    void LateUpdate() { AfterFrame?.Invoke(); }
}
#endif
