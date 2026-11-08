#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Focused checks for the SwordWave skill refresh; no unrelated regression suites.
[InitializeOnLoad]
public static class KarmaSwordSkillRefreshChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const string Key = "K4RMA.SkillRefresh";
    static KarmaGame game;
    static KarmaEnemy enemy;
    static bool started;
    static string error;
    static Transform Root => (Transform)Get(enemy, "effectsRoot");
    static object Get(object o, string n)
    { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, F); if (f != null) return f.GetValue(o); } throw new MissingFieldException(n); }
    static void Set(object o, string n, object v)
    { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, F); if (f != null) { f.SetValue(o, v); return; } } throw new MissingFieldException(n); }
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, F).Invoke(o, a);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Pass(string message) => Debug.Log("REFRESH PASS: " + message);
    static KarmaSwordSkillRefreshChecks() { EditorApplication.update += Pump; }
    public static void RunBatch()
    {
        SessionState.SetBool(Key + ".RequestedChanges", false);
        SessionState.SetBool(Key + ".RainOnly", false);
        SessionState.SetBool(Key + ".PackedRenderOnly", false);
        SessionState.SetBool(Key + ".VfxOnly", false);
        SessionState.SetBool(Key + ".Grand", false);
        SessionState.SetBool(Key + ".WardFinal", false);
        SessionState.SetBool(Key + ".WardOnly", false);
        SessionState.SetBool(Key + ".SW03Only", false);
        SessionState.SetInt(Key + ".Stage", 0);
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    public static void RunRetreatAndLater() { RunBatch(); SessionState.SetInt(Key + ".Stage", 1); }
    public static void RunScarAndLater() { RunBatch(); SessionState.SetInt(Key + ".Stage", 2); }
    public static void RunJumpAndLater() { RunBatch(); SessionState.SetInt(Key + ".Stage", 3); }
    public static void RunRainAndLater() { RunBatch(); SessionState.SetInt(Key + ".Stage", 4); }
    public static void RunExplosionAndRender() { RunBatch(); SessionState.SetInt(Key + ".Stage", 5); }
    public static void RunRenderOnly() { RunBatch(); SessionState.SetInt(Key + ".Stage", 6); }
    public static void RunSW03Only() { RunBatch(); SessionState.SetBool(Key + ".SW03Only", true); SessionState.SetInt(Key + ".SW03Step", 0); }
    public static void RunSW03MovementOnly() { RunSW03Only(); SessionState.SetInt(Key + ".SW03Step", 1); }
    public static void RunSW03VisualOnly() { RunSW03Only(); SessionState.SetInt(Key + ".SW03Step", 2); }
    public static void RunRequestedChanges() { RunBatch(); SessionState.SetBool(Key + ".RequestedChanges", true); }
    public static void RunRequestedRainOnly() { RunRequestedChanges(); SessionState.SetBool(Key + ".RainOnly", true); }
    public static void RunPackedRenderOnly() { RunBatch(); SessionState.SetBool(Key + ".PackedRenderOnly",true); }
    public static void RunRequestedVfxOnly() { RunRequestedChanges(); SessionState.SetBool(Key + ".VfxOnly",true); }
    public static void RunGrandOverhaul() { RunRequestedChanges(); SessionState.SetBool(Key+".Grand",true); }
    public static void RunWardAndFinal() { RunRequestedChanges(); SessionState.SetBool(Key+".WardFinal",true); }
    public static void RunWardOnly() { RunWardAndFinal(); SessionState.SetBool(Key+".WardOnly",true); }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true; EditorApplication.isPaused = false;
        if (!started && Time.frameCount > 2) { started = true; Run(); }
    }
    static void Reset()
    {
        typeof(KarmaSwordPlayChecks).GetField("game", F).SetValue(null, game);
        typeof(KarmaSwordPlayChecks).GetMethod("Reset", F).Invoke(null, new object[] { Essence.SwordWave });
        enemy = (KarmaEnemy)typeof(KarmaSwordPlayChecks).GetField("enemy", F).GetValue(null);
    }
    static void Force(EnemyAttack pattern) => typeof(KarmaSwordPlayChecks).GetMethod("Force", F).Invoke(null, new object[] { pattern });
    static async Task Until(Func<bool> done, Action observe = null, float timeout = 25)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!done()) { observe?.Invoke(); Check(EditorApplication.timeSinceStartup < end, "focused check timeout"); await Task.Delay(5); }
        observe?.Invoke();
    }
    static Task Seconds(float seconds) { float end = enemy.FightSeconds + seconds; return Until(() => enemy.FightSeconds >= end); }
    static async Task SW01()
    {
        Reset(); await Seconds(0.1f);
        bool low = false, high = false, repeated = false; bool? previous = null;
        for (int cast = 0; cast < 8 && !(low && high && repeated); cast++)
        {
            // The first two casts replay the same random sample on the SAME boss.
            // A forced alternation would fail even though both lanes still fire.
            UnityEngine.Random.InitState(cast < 2 ? 0 : cast - 1);
            var seen = new HashSet<KarmaBossAttack>(Root.GetComponentsInChildren<KarmaBossAttack>()); var heights = new List<float>();
            float cue = -1;
            Force(EnemyAttack.SW01);
            await Until(() => !(bool)Get(enemy, "running"), () =>
            {
                if (cue < 0)
                {
                    var mark = Root.GetComponentsInChildren<KarmaAttackWarning>().FirstOrDefault();
                    if (mark != null) cue = mark.transform.position.y;
                }
                foreach (var shot in Root.GetComponentsInChildren<KarmaBossAttack>()) if (seen.Add(shot))
                {
                    heights.Add(shot.transform.position.y);
                    Check(Mathf.Abs(((Vector2)Get(shot, "velocity")).x - game.Config.BossTuning.sword.swordSpeed * 1.3f) < 0.01f, "SW01 speed +30%");
                    Check((Vector2)Get(shot, "size") == (Vector2)Call(Get(enemy, "behavior"), "SwordSize", EnemyAttack.SW01), "SW01 shape unchanged");
                }
            });
            Check(heights.Count == 2, "SW01 exactly two shots");
            Check(Mathf.Abs(heights[0] - cue) < 0.05f, "first height matches visible lane cue");
            low |= heights[0] < heights[1]; high |= heights[0] > heights[1];
            bool highFirst = heights[0] > heights[1];
            repeated |= previous.HasValue && previous.Value == highFirst; previous = highFirst;
        }
        Check(low && high && repeated, "both SW01 orders and consecutive repeated order execute on same boss");
        Pass("SW01 independent cast order, consecutive repeats, locked cue, two shots, +30% speed, unchanged shape");
    }
    static object Pattern(EnemyAttack attack) => ((System.Collections.IDictionary)Get(Get(enemy, "behavior"), "patterns"))[attack];
    static async Task SW02()
    {
        Reset(); await Seconds(0.1f);
        var pattern = Pattern(EnemyAttack.SW02);
        var behavior = Get(enemy, "behavior");
        Vector2 size = (Vector2)Call(behavior, "SwordSize", EnemyAttack.SW02);
        Vector2 origin = (Vector2)Call(pattern, "AimedSlashOrigin", enemy.Position, 1f);
        float locked = (float)Call(pattern, "AimedSlashLockTime", origin, 1f, size);
        float safeX = origin.x - size.x / 2 - game.Player.GetComponent<Collider2D>().bounds.extents.x - 0.15f;
        float travel = Mathf.Max(0, game.Player.GetComponent<Collider2D>().bounds.center.x - safeX);
        float oldLock = Mathf.Max(0.9f, travel / (game.Config.moveSpeed * 0.9f) + 0.35f + 2 * Time.fixedDeltaTime);
        Check(locked < oldLock, "SW02 direction lock shortened with walking escape budget retained");
        float start = enemy.FightSeconds, released = -1;
        var seen = new HashSet<KarmaBossAttack>();
        Force(EnemyAttack.SW02);
        await Until(() => !(bool)Get(enemy, "running"), () =>
        {
            foreach (var shot in Root.GetComponentsInChildren<KarmaBossAttack>()) if (seen.Add(shot))
            {
                released = enemy.FightSeconds - start;
                Check(Mathf.Abs(((Vector2)Get(shot, "velocity")).x - game.Config.BossTuning.sword.swordSpeed * 1.25f * 1.3f) < 0.01f, "SW02 speed +30%");
                Check((Vector2)Get(shot, "size") == size, "SW02 original hit rectangle");
                Check(shot.Visual.sprite == KarmaWaveArtwork.Sprite("Sword_Crescent_v6"), "SW01/SW02 source sharing preserved");
            }
        });
        Check(seen.Count == 1 && Mathf.Abs(released - (0.34f + locked)) < 0.12f, "SW02 one release at shorter advertised timing");
        Pass("SW02 +30% speed, shorter tracking/lock/release, one original hit rectangle, shared source");
    }
    static void Capture(string name)
    {
        var rt = new RenderTexture(1600, 700, 24); var picture = new Texture2D(1600, 700, TextureFormat.RGBA32, false);
        var active = RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(game.GameCamera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; picture.ReadPixels(new Rect(0, 0, 1600, 700), 0, 0); picture.Apply();
            string folder = SessionState.GetBool(Key + ".RequestedChanges",false) ? "output/requested-sw03-sw05" : "output/skill-refresh";
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(folder + "/" + name + ".png", picture.EncodeToPNG());
        }
        finally { RenderTexture.active = active; UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(picture); }
    }
    static void Retreat()
    {
        Reset(); var move = Get(Get(enemy, "behavior"), "movement");
        Check(Mathf.Abs((float)Call(move, "SwordRetreatDistance") - game.Config.Techniques.pierceDistance * 1.75f) < 0.01f, "retreat 1.75 dash");
        Set(move, "swordAttackedSinceRetreat", true);
        Call(move, "RetreatSword", 1f / 60, 5f);
        float remaining = (float)Get(move, "nextSwordRetreat") - enemy.FightSeconds;
        Check(remaining > 2.1f, "live retreat cooldown exceeds previous 1.5 cap");
        Check((float)Get(move, "swordRetreatDirection") < 0, "retreat away from player");
        Call(move, "Interrupt", false);
        enemy.transform.position = new Vector2(KarmaStage.BossLeftWall + 0.5f, KarmaSpace.GuardianGroundY);
        object[] args = { 5f, 0f, 0f };
        Check((bool)move.GetType().GetMethod("ChooseRetreat", F).Invoke(move, args) && (float)args[1] > 0, "wall escape points inward");
        Pass("retreat 12 units, live cooldown 2.4 seconds, away priority and wall escape");
    }
    static async Task SW03()
    {
        Reset(); Call(enemy, "InterruptPattern"); Set(enemy, "selected", EnemyAttack.SW02); Set(enemy, "running", true);
        // Compare the real SW02 projectile path without exercising its unrelated
        // common warning-mesh generator during this SW03-only render check.
        var aimed = Pattern(EnemyAttack.SW02); var behavior = Get(enemy, "behavior");
        var waveSize = (Vector2)Call(behavior, "SwordSize", EnemyAttack.SW02);
        var origin = (Vector2)Call(aimed, "AimedSlashOrigin", enemy.Position, 1f);
        float speed = (float)aimed.GetType().GetProperty("AimedSlashSpeed", F).GetValue(aimed);
        Vector2 velocity = Vector2.right * speed;
        var wave = KarmaBossAttack.Spawn(game, enemy, Root, origin, velocity, waveSize,
            (float)Call(aimed, "AimedSlashLife", origin, velocity), 1, KarmaSwordAttackPolish.Blue,
            null, 0, separateLaunch: true, extraWavePose: "SW02_AimedSlash", sourcePattern: EnemyAttack.SW02);
        KarmaSwordAttackPolish.Projectile(game, enemy, wave, velocity, waveSize);
        float aimedHeight = RenderedBounds(wave.Visual).size.y;
        Capture("sw03-comparison-sw02");
        Reset();
        var player = game.Player; var playerBody = player.GetComponent<Rigidbody2D>();
        playerBody.bodyType = RigidbodyType2D.Dynamic; Set(player, "invulnerableUntil", 0f);
        float health = player.Health;
        var escape = player.gameObject.AddComponent<KarmaSwordRefreshJumpDriver>();
        escape.player = player; escape.WalkOnly = true; escape.TargetX = player.Position.x; escape.MoveSpeed = game.Config.moveSpeed;
        player.enabled = false;
        UnityEngine.Random.InitState(6103); Force(EnemyAttack.SW03);
        bool slam = false, wasActive = false, captured = false;
        int rounds = 0; SpriteRenderer lastWarning = null;
        float peakHeight = 0, releasedAt = 0, minCoverage = 1, maxCoverage = 0;
        var coveredRounds = new HashSet<int>();
        var marks = new List<float>(); var seen = new HashSet<KarmaBossAttack>();
        var warnings = new HashSet<string>(); var blades = new HashSet<string>(); var impacts = new HashSet<string>();
        var measured = new HashSet<int>();
        await Until(() => !(bool)Get(enemy, "running"), () =>
        {
            slam |= Root.GetComponentInChildren<KarmaSwordGroundSlam>() != null;
            var cues = Root.GetComponentsInChildren<SpriteRenderer>().Where(s => s.name.StartsWith("SW03 sword-cut warning")).ToArray();
            if (cues.Length > 0) marks = cues.Select(s => s.transform.position.x).ToList();
            if (cues.Length > 0 && cues[0] != lastWarning)
            {
                lastWarning = cues[0];
                float width = KarmaStage.ArenaWidth / 16;
                var gaps = SafeGaps(marks.Select(x => new Vector2(x - width / 2, x + width / 2)));
                Check(gaps.Count == 3 && gaps.All(g => Mathf.Abs(g.y - g.x - width) < 0.01f), "three separated narrow safe channels");
                float inset = player.GetComponent<Collider2D>().bounds.extents.x + 0.6f;
                escape.TargetX = gaps.Select(g => Mathf.Clamp(player.Position.x, g.x + inset, g.y - inset))
                    .OrderBy(x => Mathf.Abs(x - player.Position.x)).First();
                escape.ReactAt = Time.time + 0.28f;
            }
            foreach (var sr in cues) warnings.Add(sr.sprite.name);
            foreach (var sr in Root.GetComponentsInChildren<SpriteRenderer>().Where(s => s.sprite != null && s.sprite.name.StartsWith("SW03_HitCut_5f_"))) impacts.Add(sr.sprite.name);
            var active = Root.GetComponentsInChildren<KarmaBossAttack>();
            if (active.Length > 0 && !wasActive)
            { Check(slam && marks.Count == 13 && active.Length == 26, "slam then twenty-six swords in thirteen warned cells"); rounds++; releasedAt = Time.time; }
            wasActive = active.Length > 0;
            if (active.Length == 26 && active.All(s => s.Visual.GetComponent<KarmaSwordScarRise>().FrameIndex == 4) && coveredRounds.Add(rounds))
            {
                var danger = active.Select(s => { var b = (Vector2)Get(s, "size"); return new Vector2(s.transform.position.x - b.x / 2, s.transform.position.x + b.x / 2); }).ToArray();
                var safe = SafeGaps(danger);
                float coverage = 1 - safe.Sum(g => g.y - g.x) / KarmaStage.ArenaWidth;
                minCoverage = Mathf.Min(minCoverage, coverage); maxCoverage = Mathf.Max(maxCoverage, coverage);
                Check(coverage >= 0.80f && coverage <= 0.83f && safe.Count == 3, "actual damage union is about eighty percent with three real gaps");
                var body = player.GetComponent<Collider2D>().bounds;
                Check(safe.Any(g => body.min.x > g.x && body.max.x < g.y), "real movement reaches a safe channel before full eruption");
                Check(Time.time - releasedAt <= 0.12f, "full sword eruption within 0.12 seconds after warning");
            }
            foreach (var shot in active)
            {
                seen.Add(shot); blades.Add(shot.Visual.sprite.name);
                var size = (Vector2)Get(shot, "size"); var view = shot.Visual.GetComponent<KarmaSwordScarRise>();
                Check(Mathf.Abs(shot.transform.position.y - size.y / 2) < 0.002f, "damage grows upward from ground");
                Check(marks.Any(x => Mathf.Abs(shot.transform.position.x - x) <= 2.35f), "damage inside its marked cell");
                if (rounds == 1 && measured.Add(view.FrameIndex))
                {
                    var rendered = RenderedBounds(shot.Visual);
                    Check(Mathf.Abs(rendered.min.y) < 0.06f && Mathf.Abs(rendered.size.y - size.y) < 0.12f && Mathf.Abs(rendered.center.y - shot.transform.position.y) < 0.08f,
                        "GPU-rendered blade and damage height match frame " + view.FrameIndex);
                    if (view.FrameIndex == 4) peakHeight = rendered.size.y;
                }
                if (!captured && view.FrameIndex == 4) { captured = true; Capture("sw03-supplied-rise"); }
            }
        }, 40);
        UnityEngine.Object.Destroy(escape); player.enabled = true;
        Check(rounds == 5 && seen.Count == 130 && coveredRounds.Count == 5, "five rounds of twenty-six swords");
        Check(player.Health == health, "real player movement avoids all five dense rounds without invulnerability");
        Check(warnings.Count == 5 && blades.Count == 5 && impacts.Count == 5, "all five frames of all three supplied animations play");
        Check(peakHeight > game.Player.GetComponent<Collider2D>().bounds.size.y * 2.5f && peakHeight / aimedHeight > 0.85f && peakHeight / aimedHeight < 1.05f,
            "SW03 visibly comparable to SW02 and substantially taller than player");
        var sample = new GameObject("SW03 supplied frame import check");
        var renderer = sample.AddComponent<SpriteRenderer>(); KarmaVisuals.ApplyMaterial(renderer);
        try
        {
            for (int s = 0; s < 3; s++) for (int f = 0; f < 5; f++)
            {
                renderer.sprite = KarmaSwordScarRise.Frame(s, f);
                Check(renderer.sprite.texture.width == (s == 1 ? 2172 : 2560)
                    && renderer.sprite.rect.width >= (s == 1 ? 434 : 512)
                    && renderer.sprite.rect.height == (s == 1 ? 724 : 512) && renderer.sprite.pixelsPerUnit == 100,
                    "native sprite slices and PPU remain correct");
                Check(RenderedBounds(renderer).size.y > 0.2f, "each of fifteen supplied frames visibly renders");
            }
        }
        finally { UnityEngine.Object.Destroy(sample); }
        Pass("SW03 sword artwork, 15 animated frames, five rounds/130 swords, 0.10-second rise; actual danger " + (minCoverage * 100).ToString("F2") + ".." + (maxCoverage * 100).ToString("F2")
            + "% with three reachable safe channels; real movement takes zero damage; visible height=" + peakHeight.ToString("F3") + " vs SW02=" + aimedHeight.ToString("F3"));
    }
    static List<Vector2> SafeGaps(IEnumerable<Vector2> danger)
    {
        var gaps = new List<Vector2>(); float cursor = KarmaStage.ArenaLeft;
        foreach (var interval in danger.OrderBy(x => x.x))
        {
            float start = Mathf.Max(KarmaStage.ArenaLeft, interval.x), end = Mathf.Min(KarmaStage.ArenaRight, interval.y);
            if (start > cursor + 0.001f) gaps.Add(new Vector2(cursor, start));
            cursor = Mathf.Max(cursor, end);
        }
        if (cursor < KarmaStage.ArenaRight - 0.001f) gaps.Add(new Vector2(cursor, KarmaStage.ArenaRight));
        return gaps;
    }
    static Bounds RenderedBounds(SpriteRenderer source)
    {
        var node = new GameObject("SW03 focused world height render"); var camera = node.AddComponent<Camera>();
        node.AddComponent<UniversalAdditionalCameraData>(); camera.orthographic = true; camera.orthographicSize = 9;
        camera.aspect = 1; camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
        camera.transform.position = new Vector3(source.transform.position.x, 8, -10);
        var sample = new GameObject("supplied sprite render sample"); sample.layer = 31;
        var sr = sample.AddComponent<SpriteRenderer>(); sr.sprite = source.sprite; sr.sharedMaterial = source.sharedMaterial;
        sr.flipX = source.flipX; sr.flipY = source.flipY; sr.color = source.color;
        sample.transform.position = source.transform.position; sample.transform.rotation = source.transform.rotation; sample.transform.localScale = source.transform.lossyScale;
        var rt = new RenderTexture(512, 512, 24); var image = new Texture2D(512, 512, TextureFormat.RGBA32, false); var old = RenderTexture.active;
        try
        {
            Check(!UnityEditor.ShaderUtil.ShaderHasError(sr.sharedMaterial.shader), "supplied sprite material renders");
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
            var pixels = image.GetPixels32(); int x0 = 512, y0 = 512, x1 = -1, y1 = -1, blue = 0;
            for (int y = 0; y < 512; y++) for (int x = 0; x < 512; x++)
            {
                var c = pixels[y * 512 + x]; if (c.a < 128) continue;
                x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                if (c.b > c.r * 1.2f && c.b > 70) blue++;
            }
            Check(y1 >= y0 && blue > 20, "visible supplied blue/cyan frame");
            float pixel = 18f / 512;
            return new Bounds(new Vector3(camera.transform.position.x + ((x0 + x1 + 1) / 2f - 256) * pixel, -1 + (y0 + y1 + 1) / 2f * pixel, 0),
                new Vector3((x1 - x0 + 1) * pixel, (y1 - y0 + 1) * pixel, 0));
        }
        finally { RenderTexture.active = old; UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(sample); UnityEngine.Object.Destroy(node); }
    }
    static async Task JumpAcrossBlade()
    {
        Reset(); Call(enemy, "InterruptPattern"); Set(enemy, "selected", EnemyAttack.SW03); Set(enemy, "running", true);
        var player = game.Player; var body = player.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic; body.position = new Vector2(21f, KarmaSpace.PlayerHalfHeight + 0.03f);
        player.transform.position = body.position; body.linearVelocity = new Vector2(14, game.Config.jumpSpeed);
        Set(player, "invulnerableUntil", 0f); Physics2D.SyncTransforms();
        float before = player.Health;
        Vector2 scarSize = (Vector2)Pattern(EnemyAttack.SW03).GetType().GetProperty("ScarSize", F).GetValue(Pattern(EnemyAttack.SW03));
        var shot = KarmaBossAttack.Spawn(game, enemy, Root, new Vector2(35, scarSize.y / 2), Vector2.zero,
            scarSize, 2, 10, Color.white, null, 0, separateLaunch: true,
            extraWavePose: "SW03_RisingBlade_v5", sourcePattern: EnemyAttack.SW03);
        KarmaSwordAttackPolish.Core(shot); shot.Visual.gameObject.AddComponent<KarmaSwordScarRise>().Initialize(game, enemy, shot, scarSize, 2);
        var neighbor = KarmaBossAttack.Spawn(game, enemy, Root, new Vector2(43.75f, scarSize.y / 2), Vector2.zero,
            scarSize, 2, 10, Color.white, null, 0, separateLaunch: true, extraWavePose: "SW03_RisingBlade_v5", sourcePattern: EnemyAttack.SW03);
        KarmaSwordAttackPolish.Core(neighbor); neighbor.Visual.gameObject.AddComponent<KarmaSwordScarRise>().Initialize(game, enemy, neighbor, scarSize, 2);
        var drive = player.gameObject.AddComponent<KarmaSwordRefreshJumpDriver>(); drive.player = player;
        player.enabled = false;
        await Until(() => body.position.x > 39.5f || drive.age > 1.5f);
        Check(drive.slashed && body.position.x > 39.5f && player.Health == before, "jump + air slash crosses full-height blade without damage");
        Set(player, "move", 0f); UnityEngine.Object.Destroy(drive); player.enabled = true;
        await Until(() => body.position.y < KarmaSpace.PlayerHalfHeight + 0.10f, timeout: 6);
        var landed = player.GetComponent<Collider2D>().bounds;
        Check(landed.min.x > 35 + scarSize.x / 2 && landed.max.x < 43.75f - scarSize.x / 2 && player.Health == before, "safe landing between two live cells");
        Pass("real player jump attack crosses full SW03 cell into opposite safe side without damage");
    }
    static async Task DashAcrossBlade()
    {
        Reset(); Call(enemy, "InterruptPattern"); Set(enemy, "selected", EnemyAttack.SW03); Set(enemy, "running", true);
        Vector2 size = (Vector2)Pattern(EnemyAttack.SW03).GetType().GetProperty("ScarSize", F).GetValue(Pattern(EnemyAttack.SW03));
        var player = game.Player; var body = player.GetComponent<Rigidbody2D>();
        body.position = new Vector2(35 - size.x / 2 - KarmaSpace.PlayerHalfWidth - 0.03f, KarmaSpace.PlayerHalfHeight + 0.03f);
        player.transform.position = body.position; Set(player, "invulnerableUntil", 0f); Physics2D.SyncTransforms();
        var shot = KarmaBossAttack.Spawn(game, enemy, Root, new Vector2(35, size.y / 2), Vector2.zero, size, 1, 10,
            Color.white, null, 0, separateLaunch: true, extraWavePose: "SW03_RisingBlade_v5", sourcePattern: EnemyAttack.SW03);
        KarmaSwordAttackPolish.Core(shot); shot.Visual.gameObject.AddComponent<KarmaSwordScarRise>().Initialize(game, enemy, shot, size, 1);
        var neighbor = KarmaBossAttack.Spawn(game, enemy, Root, new Vector2(43.75f, size.y / 2), Vector2.zero, size, 1, 10,
            Color.white, null, 0, separateLaunch: true, extraWavePose: "SW03_RisingBlade_v5", sourcePattern: EnemyAttack.SW03);
        KarmaSwordAttackPolish.Core(neighbor); neighbor.Visual.gameObject.AddComponent<KarmaSwordScarRise>().Initialize(game, enemy, neighbor, size, 1);
        float before = player.Health;
        Call(player, "BeginDirectionalSlash", Vector2.right, game.Config.Techniques.pierceDistance, Essence.SwordWave);
        Physics2D.SyncTransforms();
        Check(player.GetComponent<Collider2D>().bounds.min.x > 35 + size.x / 2, "real dash lands whole body beyond SW03 cell");
        Check(player.GetComponent<Collider2D>().bounds.max.x < 43.75f - size.x / 2, "dash arrival fits safe gap before next cell");
        await Seconds(0.25f); Check(player.Health == before, "dash avoids SW03 without lingering damage after invulnerability");
        Pass("SW03 real ground dash crosses the active cell and lands safely");
    }
    static async Task SW04Sequence()
    {
        Reset(); game.Player.transform.position = new Vector2(35,12); Physics2D.SyncTransforms();
        Force(EnemyAttack.SW04); var counter = Get(Get(enemy,"behavior"),"counter");
        await Until(() => Get(counter,"activeVolley") != null);
        var volley=Get(counter,"activeVolley"); float absorbed=enemy.FightSeconds; enemy.TakeDamage(1);
        Check((bool)Get(counter,"Guarding"),"absorbing guard retains its defensive state");
        var released = new float[4];
        await Until(() => released[3]>0, () =>
        {
            var shots=(KarmaBossAttack[])Get(volley,"Shots");
            for(int i=0;i<4;i++) if(shots[i]!=null && released[i]==0) released[i]=enemy.FightSeconds;
        });
        float tracking=(float)counter.GetType().GetProperty("GuardTrackingTime",F).GetValue(counter);
        float locked=(float)counter.GetType().GetProperty("GuardLockTime",F).GetValue(counter);
        float gap=(float)counter.GetType().GetProperty("GuardShotGap",F).GetValue(counter);
        Check(Mathf.Abs(released[0]-absorbed-tracking-locked)<0.08f,"first counter keeps tracking and lock timing");
        for(int i=1;i<4;i++) Check(Mathf.Abs(released[i]-released[i-1]-tracking-locked-gap)<0.09f,"all four counter shots retain their cadence");
        await Until(() => Get(counter,"activeVolley")==null);
        Check(Root.GetComponentInChildren<KarmaSwordWardVfx>()==null,"ward cleans up after all four projectiles finish");
        Pass("SW04 full four-shot sequence, original tracking/lock/gap timing, defensive state and normal cleanup");
    }
    static async Task SW04()
    {
        Reset(); Force(EnemyAttack.SW04); var counter = Get(Get(enemy, "behavior"), "counter");
        await Until(() => Get(counter, "activeVolley") != null);
        var ward=Root.GetComponentInChildren<KarmaSwordWardVfx>();Check(ward!=null,"one native SW04 barrier exists");
        int initialFrame=ward.FrameIndex;await Seconds(0.24f);
        Check(ward.FrameIndex!=initialFrame && Root.GetComponentsInChildren<KarmaSwordWardVfx>().Length==1,"one baked formation advances without duplicate formations");
        foreach(var blade in (SpriteRenderer[])Get(Get(counter,"activeVolley"),"Blades"))Check(!blade.enabled,"quiet guard hides duplicate individual blades");
        Capture("sw04-grand-orbit");enemy.TakeDamage(1);
        await Seconds(0.06f);Capture("sw04-grand-absorb");
        await Seconds(0.34f);Capture("sw04-grand-locked-warning");
        await Until(() => ((KarmaBossAttack[])Get(Get(counter, "activeVolley"), "Shots"))[0] != null);
        Capture("sw04-counter");
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 2);
        var volley = Get(counter, "activeVolley");
        var shot = ((KarmaBossAttack[])Get(volley, "Shots"))[0];
        var contacts = (KarmaAttackContact[])Get(volley, "Contacts"); var slots = (Transform[])Get(volley, "Slots");
        Set(game.Player, "invulnerableUntil", 0f); game.Player.BeginCounter();
        shot.transform.position = game.Player.GetComponent<Collider2D>().bounds.center; Set(shot, "velocity", Vector2.zero);
        int parries = game.Player.ParryCount; Call(shot, "Update");
        Check(game.Player.ParryCount == parries + 1, "SW04 actual contact parried");
        for (int i = 1; i < 4; i++) Check(contacts[i].Consumed && !slots[i].gameObject.activeInHierarchy, "SW04 successors synchronously canceled");
        Check(game.World.GetComponentInChildren<KarmaSwordParryFeedback>() != null, "SW04 success feedback active"); Capture("sw04-parry");
        await Until(() => enemy.Stunned); await Seconds(0.1f);
        float left = (float)Get(enemy, "stunnedUntil") - enemy.FightSeconds;
        Check(left > 1.8f && left <= 2, "SW04 two-second stun");
        Check(game.World.GetComponentsInChildren<LineRenderer>().Any(l => l.name == "Stun orbit" && l.gameObject.activeInHierarchy), "stun orbit visible"); Capture("sw04-stun");
        await Seconds(2); Check(!enemy.Stunned, "SW04 stun ends after two seconds");
        Reset(); typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 2); Force(EnemyAttack.SW05);
        Call(Get(enemy, "behavior"), "OnAttackParried", true); await Seconds(0.1f);
        Check(!enemy.Stunned && !(bool)Get(enemy, "pendingParry") && (bool)Get(enemy, "running"), "SW05 ignores parry stun even before final stage");
        Pass("SW04 actual parry cancels successors, visible success/stun, two seconds; SW05 parry stun immunity retained");
    }
    static async Task SW05()
    {
        Reset(); game.Player.transform.position = new Vector2(35, 12); Physics2D.SyncTransforms(); Force(EnemyAttack.SW05);
        bool warned = false, exploded = false, retained = false, dimmed = false, finalWarnCaptured = false;
        bool distinctionCaptured = false, rainCaptured = false, impactCaptured = false, blastCaptured = false, tailCaptured = false;
        var played = new Dictionary<KarmaSwordPackedVfx.Clip,HashSet<int>>();
        int lastCount = 0; float lastLanding = -1, detonation = -1;
        var waves = new List<float>(); var launches = new HashSet<KarmaSW05RainBlade>();
        await Until(() => !(bool)Get(enemy, "running"), () =>
        {
            var shots = Root.GetComponentsInChildren<KarmaSW05RainBlade>();
            foreach (var shot in shots) launches.Add(shot);
            if (launches.Count > lastCount) { if (waves.Count == 0 || enemy.FightSeconds - waves.Last() > 0.5f) waves.Add(enemy.FightSeconds); lastCount = launches.Count; }
            retained |= shots.Count(s => s.Landed) >= 32;
            var sprites = Root.GetComponentsInChildren<SpriteRenderer>();
            foreach (var layer in Root.GetComponentsInChildren<KarmaSwordPackedVfx>())
            {
                if (!played.TryGetValue(layer.ClipId,out var frames)) played[layer.ClipId] = frames = new HashSet<int>();
                frames.Add(layer.FrameIndex);
                Check(layer.GetComponent<Collider2D>() == null,"pack layers add no damaging collider");
                if (layer.ClipId == KarmaSwordPackedVfx.Clip.Detonation)
                    Check(shots.Length == 0 || shots.All(s => (bool)Get(s,"detonating")),"packed detonation starts at the real shared damage event");
            }
            foreach(var energy in Root.GetComponentsInChildren<KarmaSW05Energy>())Check(!energy.CapacityExceeded,"bounded VFX meshes do not truncate");
            warned |= sprites.Any(s => s.name.Contains("planted explosion warning"));
            dimmed |= sprites.Any(s => s.name.Contains("wood-embedded sword") && s.color.a >= 0.28f && s.color.a <= 0.35f && s.sortingOrder == 15);
            if (!rainCaptured && shots.Any(s => !s.Landed && (float)Get(s,"age") > 0.025f))
            { rainCaptured = true; Capture("sw05-falling"); }
            if (!impactCaptured && waves.Count == 1 && shots.Any(s => s.Landed))
            { impactCaptured = true; Capture("sw05-impact"); }
            if (!distinctionCaptured && dimmed && sprites.Any(s => s.name == "Forming aerial sword / no damage"))
            { distinctionCaptured = true; Capture("sw05-next-strike"); }
            if (waves.Count == 3 && shots.Length > 0 && shots.All(s => s.Landed && !(bool)Get(s,"detonating")))
            {
                if (lastLanding < 0) lastLanding = enemy.FightSeconds;
                if (sprites.Any(s => s.name.Contains("planted explosion warning")))
                    Check(sprites.Count(s => s.name.Contains("planted explosion warning")) == shots.Length, "all three volleys have their own final blast cue");
                if (!finalWarnCaptured && enemy.FightSeconds-lastLanding >= 0.35f)
                { finalWarnCaptured = true; Capture("sw05-final-warning"); }
            }
            if (sprites.Any(s => s.name.Contains("final explosion / damage volume")))
            {
                if (!exploded) detonation = enemy.FightSeconds;
                exploded = true;
                if (!blastCaptured && enemy.FightSeconds-detonation >= 0.035f)
                { blastCaptured = true; Capture("sw05-final-explosion"); }
                if (!tailCaptured && enemy.FightSeconds-detonation >= 0.15f)
                { tailCaptured = true; Capture("sw05-final-explosion-tail"); }
                Check(shots.All(s => (bool)Get(s, "detonating")), "every planted sword detonates synchronously");
            }
        });
        Check(waves.Count == 3 && waves[2] - waves[1] < 2.8f, "three existing volleys with faster impact cadence");
        Check(retained && warned && exploded && dimmed && distinctionCaptured, "old swords recede below clear new strike cues");
        Check(finalWarnCaptured && detonation - lastLanding >= 0.58f && detonation-lastLanding <= 0.68f, "final warning is 0.60 seconds");
        Check(launches.Count >= 48 && launches.Count <= 54, "all three complete rain waves retained");
        foreach (var clip in new[] { KarmaSwordPackedVfx.Clip.FallingAura,KarmaSwordPackedVfx.Clip.LandingShock,
            KarmaSwordPackedVfx.Clip.Overcharge,KarmaSwordPackedVfx.Clip.Detonation })
            Check(played.ContainsKey(clip) && played[clip].Count >= (clip == KarmaSwordPackedVfx.Clip.FallingAura ? 4 : clip == KarmaSwordPackedVfx.Clip.Detonation ? 8 : 6),
                "supplied animation advances through its frames: " + clip);
        await Seconds(0.05f);
        Check(Root.GetComponentsInChildren<KarmaSW05RainBlade>(true).Length == 0
            && Root.GetComponentsInChildren<KarmaSW05VFX>(true).Length == 0
            && Root.GetComponentsInChildren<KarmaSW05Energy>(true).Length == 0
            && Root.GetComponentsInChildren<KarmaSwordPackedVfx>(true).Length == 0, "no lingering explosion, sprite animation or energy mesh after completion");
        Force(EnemyAttack.SW05); await Until(() => Root.GetComponentsInChildren<KarmaSW05RainBlade>().Length > 0);
        Call(enemy, "InterruptPattern"); await Seconds(0.05f);
        Check(!enemy.Invulnerable && Root.GetComponentsInChildren<KarmaSW05Energy>(true).Length == 0, "SW05 interruption clears energy and shield");
        Pass("SW05 three full rain waves, heavy sword silhouette, all animated layers, 0.60-second synchronized recharge/detonation, completion and interruption cleanup");
    }
    static async Task SW03Shield()
    {
        Reset(); Force(EnemyAttack.SW03);
        Check(enemy.Invulnerable, "SW03 shields immediately at cast start");
        float health = enemy.Health; int rounds = 0; bool released = false, activeLast = false;
        var chargeFrames = new HashSet<int>(); var surgeFrames = new HashSet<int>();
        bool warningCaptured = false, eruptionCaptured = false;
        await Until(() => !(bool)Get(enemy, "running"), () =>
        {
            var attacks = Root.GetComponentsInChildren<KarmaBossAttack>();
            bool active = attacks.Length > 0;
            if (active && !activeLast) rounds++;
            activeLast = active;
            foreach (var layer in Root.GetComponentsInChildren<KarmaSwordPackedVfx>())
            {
                if (layer.ClipId == KarmaSwordPackedVfx.Clip.GroundCharge)
                {
                    chargeFrames.Add(layer.FrameIndex);
                    if (!warningCaptured && layer.FrameIndex == 3) { warningCaptured = true; Capture("sw03-packed-warning"); }
                }
                if (layer.ClipId == KarmaSwordPackedVfx.Clip.BladeSurge)
                {
                    surgeFrames.Add(layer.FrameIndex);
                    Check(active,"SW03 surge coincides with existing real rising-blade attacks");
                    if (!eruptionCaptured && layer.FrameIndex == 2) { eruptionCaptured = true; Capture("sw03-packed-eruption"); }
                }
            }
            foreach(var energy in Root.GetComponentsInChildren<KarmaSW05Energy>())Check(!energy.CapacityExceeded,"SW03 fixed mesh budgets hold");
            // Final Recovery calls EndGuardianAction; all round-local Recover
            // states before it still belong to the actively executing sequence.
            if (!enemy.Invulnerable) { Check(rounds == 5, "shield spans every rising-blade round"); released = true; }
            else Check(!released, "shield never reactivates after cast ends");
            if (!released)
            {
                enemy.TakeDamage(1);
                Check(enemy.Health == health, "real damage blocked during every SW03 round and gap");
            }
        });
        Check(rounds == 5 && !enemy.Invulnerable, "all five SW03 rounds complete and shield ends");
        Check(chargeFrames.Count == 6 && surgeFrames.Count == 6 && warningCaptured && eruptionCaptured,"both six-frame SW03 layers render and advance");
        Check(Root.GetComponentsInChildren<KarmaSwordPackedVfx>(true).Length == 0,"SW03 layered effects clean up at completion");
        enemy.TakeDamage(1); Check(enemy.Health < health, "damage works after SW03");
        foreach (string transition in new[] { "interrupt", "parry", "disable", "combat", "death" })
        {
            Reset(); Force(EnemyAttack.SW03); Check(enemy.Invulnerable, "shield active before " + transition);
            if (transition == "interrupt") Call(enemy, "InterruptPattern");
            else if (transition == "parry")
            {
                typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 2);
                Call(Get(enemy,"behavior"),"OnAttackParried",true);
                Check(!enemy.Invulnerable, "parry interruption clears shield immediately");
                await Until(() => enemy.Stunned);
            }
            else if (transition == "disable") enemy.enabled = false;
            else if (transition == "death")
            {
                typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy,0f);
                await Until(() => !enemy.Invulnerable);
            }
            else
            {
                typeof(KarmaGame).GetProperty("Phase").SetValue(game,RunPhase.Gate);
                await Until(() => !enemy.Invulnerable);
            }
            Check(!enemy.Invulnerable, "shield cleared by " + transition);
        }
        Pass("SW03 real damage blocked through five rounds; finish, interruption, parry, disable, death and combat cancellation release shield");
    }
    static async Task JumpExplosion()
    {
        foreach (bool attack in new[] { false, true })
        {
            Reset(); var player = game.Player; var body = player.GetComponent<Rigidbody2D>();
            player.enabled = false;
            body.position = new Vector2(40, KarmaSpace.PlayerHalfHeight + 0.02f);
            player.transform.position = body.position; Set(player,"invulnerableUntil",0f); Physics2D.SyncTransforms();
            var spec = new KarmaAerialSwordPlanner.Shot { origin = new Vector2(35,5.05f), velocity = Vector2.down*20,
                size = new Vector2(6,1), life = 0.1f };
            var blades = new List<KarmaSW05RainBlade>();
            for (int i = 0; i < 2; i++) blades.Add(KarmaSW05RainBlade.Spawn(game,enemy,Root,spec,10,null));
            await Until(() => blades.All(b => b.Landed));
            var rain = blades[0]; Vector2 size = rain.ExplosionSize;
            Check(Mathf.Abs(size.y-KarmaSW05RainBlade.CalculateExplosionHeight(game.Config)) < 0.001f,"derived height used by damage geometry");
            foreach (var b in blades) b.ArmExplosion(0.60f);
            var energy = KarmaSW05Energy.Prepare(Root,game,blades,0.60f);
            var zones = (Vector3[])Get(energy,"zones");
            Check(zones.Length == 1 && Mathf.Abs(zones[0].z-size.y) < 0.001f
                && Mathf.Abs(zones[0].x-(35-size.x/2)) < 0.001f
                && Mathf.Abs(zones[0].y-(35+size.x/2)) < 0.001f,"visible warning union matches actual horizontal edges and ceiling");
            body.bodyType = RigidbodyType2D.Dynamic;
            body.position = new Vector2(35,KarmaSpace.PlayerHalfHeight+0.02f);
            player.transform.position = body.position; body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
            float before = player.Health, began = enemy.FightSeconds;
            var drive = player.gameObject.AddComponent<KarmaSwordRefreshJumpDriver>();
            drive.player = player; drive.VerticalTrial = true;
            if (attack)
            {
                // Allow a real 180 ms reaction before the jump, then use the
                // unchanged upward air slash 200 ms into that jump.
                drive.UpAttackAt = 0.38f;await Seconds(0.18f);
                body.linearVelocity = Vector2.up*game.Config.jumpSpeed;
            }
            else
            {
                float apexTime = game.Config.jumpSpeed/Mathf.Abs(Physics2D.gravity.y*game.Config.gravityScale);
                await Seconds(0.60f-apexTime);
                body.linearVelocity = Vector2.up*game.Config.jumpSpeed;
            }
            await Until(() => enemy.FightSeconds-began >= 0.60f);
            var bounds = player.GetComponent<Collider2D>().bounds;
            Check(bounds.min.x < 35+size.x/2 && bounds.max.x > 35-size.x/2,"player remains horizontally inside hazard");
            Check(attack ? bounds.min.y > size.y : bounds.min.y < size.y,"real collider distinguishes apex jump from upward jump attack");
            Check((float)Get(player,"invulnerableUntil") <= Time.time && !player.Shielded && !player.TestInvincible,"avoidance trial has no immunity or guard");
            energy.Ignite(); var ledger = new KarmaAttackContact();
            foreach (var b in blades) b.Explode(ledger);
            Capture(attack ? "sw05-jump-attack-clears" : "sw05-normal-jump-hit");
            float after = player.Health;
            Check(attack ? after == before : Mathf.Abs(before-after-10/game.Config.BossTuning.sword.aerialSwordDamageMultiplier) < 0.001f,
                "overlapping explosion deals one unchanged hit; upward movement alone avoids it");
            await Until(() => blades.All(b => b == null), () =>
            {
                // Remove post-hit immunity to expose any repeated-contact bug.
                Set(player,"invulnerableUntil",0f);
                Check(player.Health == after,"no repeated explosion damage throughout active frames");
                if (attack) Check(player.GetComponent<Collider2D>().bounds.min.y > size.y,"air slash stays above hazard through full 0.12-second damage interval");
            });
            Check(enemy.FightSeconds-began >= 0.60f+KarmaSW05RainBlade.ExplosionActiveSeconds-0.02f,"damage lifetime retained");
            UnityEngine.Object.Destroy(drive); player.enabled = true;
            Pass((attack ? "upward jump attack avoids" : "normal jump at apex is hit by") + " real SW05 explosion; height=" + size.y.ToString("F4") + ", collider bottom=" + bounds.min.y.ToString("F4"));
        }
    }
    static void RenderPalette()
    {
        var node = new GameObject("isolated actual skill render"); var camera = node.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 3; camera.aspect = 1; camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear; camera.transform.position = new Vector3(0, 0, -10);
        node.AddComponent<UniversalAdditionalCameraData>();
        var picture = new GameObject("palette probe"); picture.layer = 31; var sr = picture.AddComponent<SpriteRenderer>();
        var rt = new RenderTexture(256, 256, 24); var pixels = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        var old = RenderTexture.active;
        try
        {
            foreach (var pose in new[] { "Sword_Crescent_v6", "Sword_Release_v6", "Sword_Impact_v6", "Sword_GuardRing_v6", "SW04_CounterBlade_v6", "SW03_GroundScar_v7", "SW03_RisingBlade_v7", "SW05_JudgementSword_v2", "SW05_Impact_1" })
            {
                sr.sprite = KarmaArtwork.Sprite("SwordBossVFX", pose); Check(sr.sprite != null, "linked " + pose);
                sr.color = Color.white; sr.transform.position = -sr.sprite.bounds.center * (4.5f / Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y));
                sr.transform.localScale = Vector3.one * (4.5f / Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y));
                sr.sharedMaterial = Resources.Load<Material>(pose.StartsWith("SW05") ? "SW05/SW05CrimsonBlade" : pose == "SW03_RisingBlade_v7" ? "KarmaSwordRisePolish" : "KarmaSwordSkillPolish");
                Check(sr.sharedMaterial != null && !UnityEditor.ShaderUtil.ShaderHasError(sr.sharedMaterial.shader), "valid shader " + pose);
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); pixels.Apply();
                int blue = 0, red = 0, magenta = 0;
                foreach (var c in pixels.GetPixels32()) if (c.a > 40)
                { if (c.b > c.r * 1.2f && c.b > 70) blue++; if (c.r > c.b * 1.3f && c.r > 70) red++; if (c.r > 220 && c.b > 220 && c.g < 40) magenta++; }
                Check(blue > 30 && red < blue / 4 && magenta == 0, "blue/white real Unity render " + pose + " blue=" + blue + " red=" + red);
            }
        }
        finally { RenderTexture.active = old; UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(pixels); UnityEngine.Object.Destroy(picture); UnityEngine.Object.Destroy(node); }
        Pass("nine connected effect sprites rendered blue/white in Unity URP; no shader fallback");
    }
    static void RenderPacked()
    {
        Reset();
        var node = new GameObject("packed render camera"); var camera = node.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 4; camera.aspect = 1; camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
        camera.transform.position = new Vector3(0,3,-10); node.AddComponent<UniversalAdditionalCameraData>();
        var fx = KarmaSwordPackedVfx.Spawn(Root,game,KarmaSwordPackedVfx.Clip.Detonation,Vector2.zero,new Vector2(6,6),1,30,1,true);
        fx.gameObject.layer = 31; fx.SetProgress(3f/8);
        var rt = new RenderTexture(512,512,24); var pixels = new Texture2D(512,512,TextureFormat.RGBA32,false);
        var active = RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; pixels.ReadPixels(new Rect(0,0,512,512),0,0); pixels.Apply();
            int lit = pixels.GetPixels32().Count(p => p.b > 30);
            System.IO.Directory.CreateDirectory("output/requested-sw03-sw05");
            System.IO.File.WriteAllBytes("output/requested-sw03-sw05/packed-isolated.png",pixels.EncodeToPNG());
            Check(lit > 1000,"packed shader produces visible animated texture, lit pixels="+lit);
            Pass("isolated supplied detonation render, lit pixels="+lit);
        }
        finally { RenderTexture.active = active; UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(pixels); UnityEngine.Object.Destroy(node); UnityEngine.Object.Destroy(fx.gameObject); }
    }
    static async void Run()
    {
        int code = 0;
        try
        {
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            Application.targetFrameRate = 120; Time.captureDeltaTime = 1f / 60;
            Application.logMessageReceived += (m, s, t) => { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) error = m+"\n"+s; };
            int stage = SessionState.GetInt(Key + ".Stage", 0);
            if (SessionState.GetBool(Key + ".PackedRenderOnly",false))
            {
                RenderPacked(); Time.captureDeltaTime = 0; SessionState.SetBool(Key,false); EditorApplication.Exit(0); return;
            }
            if (SessionState.GetBool(Key + ".RequestedChanges", false))
            {
                bool rainOnly = SessionState.GetBool(Key + ".RainOnly",false);
                bool vfxOnly = SessionState.GetBool(Key + ".VfxOnly",false);
                bool wardFinal = SessionState.GetBool(Key + ".WardFinal",false);
                if (vfxOnly) RenderPacked();
                if (!rainOnly && !wardFinal) await SW03Shield();
                if (SessionState.GetBool(Key+".Grand",false) || wardFinal) { await SW04Sequence(); await SW04(); }
                if (SessionState.GetBool(Key+".WardOnly",false))
                {
                    Check(error==null,"runtime error: "+error);Pass("SW04 FOCUSED CHECKS COMPLETE");
                    Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;
                }
                await SW05();
                if (!rainOnly && !vfxOnly) await JumpExplosion();
                foreach (string material in new[] { "SW05/SW05Celestial", "SW05/SW05CrimsonBlade", "SW05/SW05EmbeddedWood" })
                {
                    var asset = Resources.Load<Material>(material);
                    Check(asset != null && !UnityEditor.ShaderUtil.ShaderHasError(asset.shader),"SW05 shader compiles: " + material);
                }
                Check(error == null,"runtime error: " + error); Pass("REQUESTED SW03/SW05 CHECKS COMPLETE at normal speed");
                Time.captureDeltaTime = 0; SessionState.SetBool(Key,false); EditorApplication.Exit(0); return;
            }
            if (SessionState.GetBool(Key + ".SW03Only", false))
            {
                int step = SessionState.GetInt(Key + ".SW03Step", 0);
                if (step != 1) await SW03();
                if (step != 2) { await JumpAcrossBlade(); await DashAcrossBlade(); }
                Check(error == null, "runtime error: " + error); Pass("SW03 focused checks complete; selection=" + step);
                Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(0); return;
            }
            if (stage <= 0) await SW01();
            if (stage <= 1) await SW02();
            if (stage <= 2) await SW03();
            if (stage <= 3) { await JumpAcrossBlade(); await DashAcrossBlade(); await SW04(); }
            if (stage <= 4) await SW05();
            if (stage <= 5) await JumpExplosion();
            RenderPalette();
            Check(error == null, "runtime error: " + error); Pass("ALL REQUESTED FOCUSED CHECKS");
        }
        catch (Exception e) { Debug.LogException(e); code = 1; }
        Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(code);
    }
}
public sealed class KarmaSwordRefreshJumpDriver : MonoBehaviour
{
    public KarmaPlayer player; public float age; public bool slashed;
    public bool VerticalTrial; public float UpAttackAt = -1;
    public bool WalkOnly; public float TargetX, ReactAt, MoveSpeed;
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    void FixedUpdate()
    {
        age += Time.fixedDeltaTime;
        if (VerticalTrial)
        {
            typeof(KarmaPlayer).GetField("move",F).SetValue(player,0f);
            if (!slashed && UpAttackAt > 0 && age >= UpAttackAt)
            { slashed = true; typeof(KarmaPlayer).GetMethod("BeginAirSlash",F).Invoke(player,new object[] { Vector2.up }); }
            typeof(KarmaPlayer).GetMethod("FixedUpdate",F).Invoke(player,null); return;
        }
        if (WalkOnly)
        {
            float move = Time.time < ReactAt ? 0 : Mathf.Clamp((TargetX - player.Position.x) / (MoveSpeed * Time.fixedDeltaTime), -1, 1);
            typeof(KarmaPlayer).GetField("move", F).SetValue(player, move);
            typeof(KarmaPlayer).GetMethod("FixedUpdate", F).Invoke(player, null); return;
        }
        typeof(KarmaPlayer).GetField("move", F).SetValue(player, 1f);
        if (!slashed && age >= 0.42f)
        { slashed = true; typeof(KarmaPlayer).GetMethod("BeginAirSlash", F).Invoke(player, new object[] { Vector2.right }); }
        typeof(KarmaPlayer).GetMethod("FixedUpdate", F).Invoke(player, null);
    }
}
#endif
