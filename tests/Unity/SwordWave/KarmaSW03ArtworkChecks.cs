#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

// SW03 only: one complete five-round cast before and after presentation edits.
[InitializeOnLoad]
public static class KarmaSW03ArtworkChecks
{
    const string Output = "output/validation/sw03-artwork-20261105";
    const string Key = "K4RMA.SW03Artwork";
    const string Art = "Assets/K4RMA/Arts/Bosses/SwordWave/Effects/";
    const string Library = "Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/Resources/BossArtwork/KarmaSwordBossSprites.asset";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static KarmaGame game;
    static bool started, baseline;
    static string failure;
    static Type Fixture => typeof(KarmaSwordPlayChecks);
    static KarmaEnemy Enemy => (KarmaEnemy)Fixture.GetField("enemy", F).GetValue(null);
    static object Get(object target, string name)
    { for (Type t = target.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, F); if (f != null) return f.GetValue(target); } throw new MissingFieldException(name); }
    static void Set(object target, string name, object value)
    { for (Type t = target.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, F); if (f != null) { f.SetValue(target, value); return; } } throw new MissingFieldException(name); }
    static Transform Root => (Transform)Get(Enemy, "effectsRoot");
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    [Serializable] public sealed class Shot
    { public int launch, end, round, ledger; public Vector3 position; public Vector2 size, velocity; public float life, damage; }
    [Serializable] public sealed class Warning
    { public int tick; public string name; public float x, width; }
    [Serializable] public sealed class Frame
    { public int tick, active; public string state; public Vector3 boss, boundsCenter, boundsSize; public float health; }
    [Serializable] public sealed class Data
    { public List<Shot> shots = new List<Shot>(); public List<Warning> warnings = new List<Warning>(); public List<Frame> frames = new List<Frame>(); }
    static KarmaSW03ArtworkChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying) { Application.runInBackground = true; EditorApplication.isPaused = false; }
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2) { started = true; Run(); }
    }
    public static void RunBaseline() => Begin(true);
    public static void RunUpdated() => Begin(false);
    static void Begin(bool before)
    {
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Before", before);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    public static void ImportArtwork()
    {
        try
        {
            var names = new[] { "SW03_GroundScar_v7", "SW03_RisingBlade_v7", "SW03_HitCut_v7" };
            var sprites = new List<Sprite>();
            foreach (string name in names)
            {
                string path = Art + name + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 100;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = Vector2.one * 0.5f; importer.SetTextureSettings(settings);
                if (name == "SW03_GroundScar_v7" || name == "SW03_HitCut_v7")
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = name == "SW03_GroundScar_v7"
                        ? new Vector2(1089f / 2172, 373f / 724) : new Vector2(624f / 1254, 586f / 1254);
                    importer.SetTextureSettings(settings);
                }
                importer.spriteImportMode = SpriteImportMode.Single; importer.SaveAndReimport();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Check(sprite != null && sprite.name == name && sprite.pixelsPerUnit == 100, name + " imports"); sprites.Add(sprite);
            }
            var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>(Library);
            var sheet = library.sheets.Single(s => s.id == "SwordBossVFX");
            var old = Enumerable.Range(0, sheet.poses.Length).Where(i => !names.Contains(sheet.poses[i])).ToArray();
            sheet.sprites = old.Select(i => sheet.sprites[i]).Concat(sprites).ToArray();
            sheet.poses = old.Select(i => sheet.poses[i]).Concat(names).ToArray();
            Check(sheet.poses.Distinct().Count() == sheet.poses.Length, "unique owned logical names");
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/import-result.txt", "PASS: three new PPU100 full-rect RGBA sprites registered; existing entries preserved\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void Capture(string name)
    {
        var camera = game.GameCamera; var target = new RenderTexture(1600, 700, 24);
        var image = new Texture2D(1600, 700, TextureFormat.RGBA32, false);
        var old = camera.targetTexture; var active = RenderTexture.active; var rect = camera.rect; float aspect = camera.aspect;
        try
        {
            camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1); camera.aspect = 1600f / 700;
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1600, 700), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + (baseline ? "before" : "after") + "/" + name + ".png", image.EncodeToPNG());
        }
        finally { camera.targetTexture = old; camera.rect = rect; camera.aspect = aspect; RenderTexture.active = active; UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target); }
    }
    static async void Run()
    {
        int exit = 0; baseline = SessionState.GetBool(Key + ".Before", false);
        Directory.CreateDirectory(Output + "/" + (baseline ? "before" : "after"));
        var data = new Data(); var lines = new List<string>();
        try
        {
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); Fixture.GetField("game", F).SetValue(null, game);
            var config = UnityEngine.Object.Instantiate(game.Config); Set(game, "config", config); config.playerHealth = 100000;
            Application.targetFrameRate = 120; Time.captureDeltaTime = 1f / 60; game.GameCamera.aspect = 1600f / 700;
            Fixture.GetMethod("Reset", F).Invoke(null, new object[] { Essence.SwordWave });
            await Task.Delay(60);
            var seen = new Dictionary<KarmaBossAttack, int>(); var ended = new HashSet<int>();
            var warningIds = new HashSet<SpriteRenderer>(); var ledgers = new Dictionary<object, int>();
            int tick = 0, rounds = 0, first = -1, hit = -1;
            bool began = false, done = false, sawHit = false;
            var probe = new GameObject("SW03 artwork-only probe").AddComponent<KarmaSwordDashFrameProbe>();
            probe.Before = () =>
            {
                typeof(KarmaEnemy).GetProperty("FightSeconds").SetValue(Enemy, 0f); UnityEngine.Random.InitState(6103);
                Fixture.GetMethod("Force", F).Invoke(null, new object[] { EnemyAttack.SW03 }); began = true; probe.Before = null;
            };
            probe.After = () =>
            {
                if (!began) return;
                try
                {
                    var live = Root.GetComponentsInChildren<KarmaBossAttack>();
                    var bounds = Enemy.DamageBounds;
                    data.frames.Add(new Frame { tick = tick, active = live.Length, state = Get(Enemy, "state").ToString(), boss = Enemy.Position,
                        boundsCenter = bounds.center, boundsSize = bounds.size, health = game.Player.Health });
                    foreach (var sr in Root.GetComponentsInChildren<SpriteRenderer>())
                        if (sr.name.StartsWith("SW03 sword-cut warning / cell ") && warningIds.Add(sr))
                        {
                            var core = KarmaWaveArtwork.CorePixels("SW03_GroundScar_v5");
                            data.warnings.Add(new Warning { tick = tick, name = sr.name, x = sr.transform.position.x,
                                width = Mathf.Round(sr.transform.lossyScale.x * core.width / sr.sprite.pixelsPerUnit * 10000) / 10000 });
                            if (!baseline)
                            {
                                Check(sr.sprite.name == "SW03_GroundScar_v7" && sr.sprite.texture.width == 2172, "new warning loads without texture downscaling");
                                var center = sr.transform.TransformPoint((core.center - sr.sprite.pivot) / sr.sprite.pixelsPerUnit);
                                Check(Mathf.Abs(center.x - sr.transform.position.x) < 0.002f && Mathf.Abs(data.warnings.Last().width - 9.5f) < 0.002f,
                                    "warning limits align to the unchanged paired damage footprint");
                            }
                            if (warningIds.Count == 1) Capture("warning-start");
                        }
                    var fresh = live.Where(s => !seen.ContainsKey(s)).ToArray();
                    if (fresh.Length > 0)
                    {
                        rounds++; Check(fresh.Length == 20, "twenty blades per round");
                        foreach (var shot in fresh)
                        {
                            var contact = Get(shot, "contact"); if (!ledgers.ContainsKey(contact)) ledgers.Add(contact, ledgers.Count);
                            seen.Add(shot, data.shots.Count);
                            data.shots.Add(new Shot { launch = tick, round = rounds, ledger = ledgers[contact], position = shot.transform.position,
                                size = (Vector2)Get(shot, "size"), velocity = (Vector2)Get(shot, "velocity"), life = (float)Get(shot, "remaining"), damage = (float)Get(shot, "damage") });
                            if (!baseline)
                            {
                                var sr = shot.Visual; var core = KarmaWaveArtwork.CorePixels("SW03_RisingBlade_v5");
                                var foot = sr.transform.TransformPoint((new Vector2(core.center.x, core.yMin) - sr.sprite.pivot) / sr.sprite.pixelsPerUnit);
                                Check(Mathf.Abs(foot.y) < 0.002f && Mathf.Abs(foot.x - shot.transform.position.x) < 0.002f, "grounded rising blade");
                                Check(sr.bounds.max.y <= 19.002f && sr.bounds.max.y <= game.GameCamera.transform.position.y + game.GameCamera.orthographicSize - 0.49f,
                                    "complete sprite rectangle below hall roof and camera ceiling");
                                Check(sr.sprite.name == "SW03_RisingBlade_v7", "new sharp blade loaded");
                            }
                        }
                        if (first < 0)
                        {
                            first = tick;
                            // Preserve planning inputs; move into a committed blade only
                            // after launch to exercise its real unchanged contact path.
                            var target = fresh.OrderBy(s => Mathf.Abs(s.transform.position.x - 20)).First();
                            Fixture.GetMethod("Place", F).Invoke(null, new object[] { target.transform.position.x });
                            Set(game.Player, "invulnerableUntil", 0f); Set(game.Player, "guardUntil", 0f);
                        }
                        Capture("round-" + rounds + "-release");
                    }
                    foreach (var entry in seen)
                        if ((entry.Key == null || !entry.Key.gameObject.activeInHierarchy) && ended.Add(entry.Value)) data.shots[entry.Value].end = tick;
                    if (first < 0 && tick == 40) Capture("warning-locked");
                    if (first >= 0 && (tick == first + 3 || tick == first + 10 || tick == first + 28)) Capture("rise-" + (tick - first));
                    if (data.frames.Last().health < data.frames.First().health && hit < 0) hit = tick;
                    if (!baseline)
                    {
                        var flash = Root.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(sr => sr.name == "SW03 confirmed cut / no damage");
                        if (flash != null) { sawHit = true; Check(flash.sprite.name == "SW03_HitCut_v7" && flash.GetComponent<Collider2D>() == null, "confirmed hit sprite is harmless"); }
                    }
                    if (hit >= 0 && tick == hit + 3) Capture("confirmed-hit");
                    if (!(bool)Get(Enemy, "running") && first >= 0) { Set(Enemy, "running", true); done = true; probe.After = null; }
                    tick++;
                }
                catch (Exception error) { failure = error.ToString(); done = true; probe.After = null; }
            };
            double limit = EditorApplication.timeSinceStartup + 150;
            while (!done) { Check(EditorApplication.timeSinceStartup < limit, "SW03 cast timeout"); await Task.Delay(5); }
            UnityEngine.Object.Destroy(probe.gameObject); Check(failure == null, failure);
            Check(rounds == 5 && data.shots.Count == 100 && data.warnings.Count == 50 && ended.Count == 100, "five complete rounds, 100 blades, 50 warnings");
            Check(data.shots.GroupBy(s => s.ledger).Count() == 50 && data.shots.GroupBy(s => s.ledger).All(g => g.Count() == 2), "unchanged pair ledgers");
            Check(hit >= 0, "real SW03 damage contact");
            File.WriteAllText(Output + "/" + (baseline ? "baseline" : "updated") + ".json", JsonUtility.ToJson(data, true));
            if (!baseline)
            {
                var old = JsonUtility.FromJson<Data>(File.ReadAllText(Output + "/baseline.json"));
                Check(JsonUtility.ToJson(old) == JsonUtility.ToJson(data), "exact full-cast count/timing/geometry/contact/health comparison");
                Check(sawHit, "confirmed hit effect actually rendered");
                var material = Resources.Load<Material>("KarmaSwordRisePolish"); Check(material != null && !ShaderUtil.ShaderHasError(material.shader), "rise shader compiles and loads");
                lines.Add("PASS: exact before/after five-round launch/end ticks, warnings, roots, velocities, sizes, lifetimes, damage, pair ledgers and player health");
                lines.Add("PASS: new warning/rise/hit Sprite playback; complete blade rectangle below y19 and camera ceiling; ground y0; harmless confirmed hit");
            }
            lines.Add("PASS: SW03 only, five rounds, 100 blades, 50 locked warnings; first launch=" + first + ", first damage=" + hit + ", cast end=" + (tick - 1));
            lines.Add("Actual health loss=" + (data.frames.First().health - data.frames.Last().health));
        }
        catch (Exception error) { lines.Insert(0, "FAIL: " + error); Debug.LogException(error); exit = 1; }
        File.WriteAllLines(Output + "/" + (baseline ? "baseline-result" : "result") + ".txt", lines);
        Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
}
#endif
