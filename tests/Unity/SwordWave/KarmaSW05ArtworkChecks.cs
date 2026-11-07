#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KarmaPrototype;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// One SW05 rain group only, at 60 Hz. Capture the actual URP game camera and
// compare the unmodified attack against the artwork-only version.
[InitializeOnLoad]
public static class KarmaSW05ArtworkChecks
{
    const string Output = "output/validation/sw05-artwork-20261105";
    const string Key = "K4RMA.SW05Artwork";
    const string Artwork = "Assets/K4RMA/Arts/Bosses/SwordWave/Effects/";
    const string Library = "Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/Resources/BossArtwork/KarmaSwordBossSprites.asset";
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static KarmaGame game;
    static bool started, baseline;
    static string failure;
    static Type Fixture => typeof(KarmaSwordPlayChecks);
    static KarmaEnemy Enemy => (KarmaEnemy)Fixture.GetField("enemy", Flags).GetValue(null);
    static object Get(object target, string name)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, Flags); if (field != null) return field.GetValue(target); }
        throw new MissingFieldException(name);
    }
    static void Set(object target, string name, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, Flags); if (field != null) { field.SetValue(target, value); return; } }
        throw new MissingFieldException(name);
    }
    static Transform Root => (Transform)Get(Enemy, "effectsRoot");
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    [Serializable] public sealed class Shot
    {
        public int launch, landing;
        public Vector2 origin, orbit, velocity, acceleration, size;
        public float delay, life, depth, damage;
    }
    [Serializable] public sealed class Sample
    {
        public int tick, shot;
        public Vector3 position;
    }
    [Serializable] public sealed class Frame
    {
        public int tick;
        public string state;
        public Vector3 boss, boundsCenter, boundsSize;
        public float playerHealth;
    }
    [Serializable] public sealed class Data
    {
        public List<Shot> shots = new List<Shot>();
        public List<Sample> positions = new List<Sample>();
        public List<Frame> frames = new List<Frame>();
    }
    static KarmaSW05ArtworkChecks() { EditorApplication.update += Pump; }
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
    // Explicit editor operation for these two new assets only. Existing PNGs,
    // GUIDs, slices and logical library entries are retained.
    public static void ImportArtwork()
    {
        try
        {
            string swordPath = Artwork + "SW05JudgementSword_v2.png";
            string impactPath = Artwork + "SW05GroundImpact_v2.png";
            foreach (string path in new[] { swordPath, impactPath })
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 100; importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true; importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                if (path == swordPath)
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(0.5f, 771.5f / 1536);
                    importer.SetTextureSettings(settings); importer.spriteImportMode = SpriteImportMode.Single;
                    importer.SaveAndReimport();
                }
                else
                {
                    importer.SetTextureSettings(settings);
                    importer.spriteImportMode = SpriteImportMode.Multiple;
                    var factories = new SpriteDataProviderFactories(); factories.Init();
                    var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
                    var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
                    var pivots = new[] { new Vector2(313, 132), new Vector2(308, 131), new Vector2(313, 137), new Vector2(308.5f, 137) };
                    var rects = Enumerable.Range(0, 4).Select(i => new SpriteRect
                    {
                        name = "SW05_Impact_" + i, rect = new Rect((i % 2) * 627, (1 - i / 2) * 627, 627, 627),
                        alignment = SpriteAlignment.Custom, pivot = pivots[i] / 627,
                        spriteID = existing.TryGetValue("SW05_Impact_" + i, out var id) ? id : GUID.Generate()
                    }).ToArray();
                    provider.SetSpriteRects(rects);
                    provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
                    provider.Apply(); importer.SaveAndReimport();
                }
            }
            var sword = AssetDatabase.LoadAssetAtPath<Sprite>(swordPath);
            var frames = AssetDatabase.LoadAllAssetsAtPath(impactPath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            Check(sword != null && frames.Length == 4, "one sword and four impact sprites imported");
            Check(sword.name == "SW05JudgementSword_v2" && Vector2.Distance(sword.pivot, new Vector2(512, 771.5f)) < 0.001f,
                "single sword Sprite retains its authored opaque-core pivot");
            var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>(Library);
            var sheet = library.sheets.Single(s => s.id == "SwordBossVFX");
            var names = new[] { "SW05_JudgementSword_v2", "SW05_Impact_0", "SW05_Impact_1", "SW05_Impact_2", "SW05_Impact_3" };
            var old = Enumerable.Range(0, sheet.poses.Length).Where(i => !names.Contains(sheet.poses[i])).ToArray();
            sheet.sprites = old.Select(i => sheet.sprites[i]).Concat(new[] { sword }).Concat(frames).ToArray();
            sheet.poses = old.Select(i => sheet.poses[i]).Concat(names).ToArray();
            Check(sheet.poses.Distinct().Count() == sheet.poses.Length, "unique owned logical names");
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/import-result.txt", "PASS: two RGBA textures, one PPU100 sword, four grounded PPU100 impact frames; original logical entries retained\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void Capture(string name)
    {
        var camera = game.GameCamera;
        var target = new RenderTexture(1600, 700, 24);
        var image = new Texture2D(1600, 700, TextureFormat.RGBA32, false);
        var old = camera.targetTexture; var active = RenderTexture.active;
        var rect = camera.rect; float aspect = camera.aspect;
        try
        {
            camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1); camera.aspect = 1600f / 700;
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1600, 700), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + (baseline ? "before" : "after") + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = old; camera.rect = rect; camera.aspect = aspect; RenderTexture.active = active;
            UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target);
        }
    }
    static async void Run()
    {
        int exit = 0; baseline = SessionState.GetBool(Key + ".Before", false);
        Directory.CreateDirectory(Output + "/" + (baseline ? "before" : "after"));
        var data = new Data(); var lines = new List<string>();
        try
        {
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); Fixture.GetField("game", Flags).SetValue(null, game);
            var config = UnityEngine.Object.Instantiate(game.Config); Set(game, "config", config);
            config.playerHealth = 100000; config.BossTuning.sword.aerialSwordSeed = 5005;
            Application.targetFrameRate = 120; Time.captureDeltaTime = 1f / 60;
            game.GameCamera.aspect = 1600f / 700;
            Fixture.GetMethod("Reset", Flags).Invoke(null, new object[] { Essence.SwordWave });
            await Task.Delay(60);
            game.Player.enabled = false;
            if (game.Player.TestInvincible) game.Player.ToggleTestInvincibility();
            var seen = new Dictionary<KarmaSW05RainBlade, int>();
            var ended = new HashSet<int>();
            var playedImpactFrames = new HashSet<string>();
            bool revealed = false, dissolved = false;
            int tick = 0, release = -1, impact = -1;
            bool began = false, done = false;
            var probe = new GameObject("SW05 artwork-only probe").AddComponent<KarmaSwordDashFrameProbe>();
            probe.Before = () =>
            {
                typeof(KarmaEnemy).GetProperty("FightSeconds").SetValue(Enemy, 0f);
                UnityEngine.Random.InitState(73110);
                Fixture.GetMethod("Force", Flags).Invoke(null, new object[] { EnemyAttack.SW05 });
                began = true; probe.Before = null;
            };
            probe.After = () =>
            {
                if (!began) return;
                try
                {
                    var bounds = Enemy.DamageBounds;
                    data.frames.Add(new Frame { tick = tick, state = Get(Enemy, "state").ToString(), boss = Enemy.Position,
                        boundsCenter = bounds.center, boundsSize = bounds.size, playerHealth = game.Player.Health });
                    var live = Root.GetComponentsInChildren<KarmaSW05RainBlade>();
                    foreach (var blade in live)
                    {
                        if (!seen.TryGetValue(blade, out int index))
                        {
                            var shot = (KarmaAerialSwordPlanner.Shot)Get(blade, "shot"); index = data.shots.Count; seen.Add(blade, index);
                            data.shots.Add(new Shot { launch = tick, origin = shot.origin, orbit = shot.orbit, velocity = shot.velocity,
                                acceleration = shot.acceleration, size = shot.size, delay = shot.delay, life = shot.life, depth = shot.depth,
                                damage = (float)Get(blade, "damage") });
                            Vector2 tip = shot.PositionAt(shot.life) + shot.velocity.normalized * shot.size.x / 2;
                            Check(Mathf.Abs(tip.y - 0.05f) < 0.002f, "unchanged ground plane");
                            Check(blade.Visual.sprite != null && blade.Visual.sharedMaterial != null, "sword Sprite and material load");
                            Check(Vector2.Dot(-blade.Visual.transform.up, shot.velocity.normalized) > 0.999f, "tip points along fall");
                            if (!baseline)
                            {
                                var sprite = blade.Visual.sprite;
                                var localTip = (new Vector2(512, 33.5f) - sprite.pivot) / sprite.pixelsPerUnit;
                                var actualTip = blade.Visual.transform.TransformPoint(localTip);
                                var expectedTip = (Vector2)blade.transform.position + shot.velocity.normalized * shot.size.x / 2;
                                Check(Vector2.Distance(actualTip, expectedTip) < 0.002f, "new image tip maps to unchanged physical tip");
                            }
                        }
                        data.positions.Add(new Sample { tick = tick, shot = index, position = blade.transform.position });
                    }
                    if (release < 0 && live.Length > 0)
                    {
                        release = tick; Capture("release");
                        var selected = live.OrderBy(b => Mathf.Abs(((KarmaAerialSwordPlanner.Shot)Get(b, "shot")).origin.x - 20)).First();
                        var shot = (KarmaAerialSwordPlanner.Shot)Get(selected, "shot");
                        var tip = shot.PositionAt(shot.life) + shot.velocity.normalized * shot.size.x / 2;
                        Fixture.GetMethod("Place", Flags).Invoke(null, new object[] { tip.x });
                        Set(game.Player, "invulnerableUntil", 0f); Set(game.Player, "guardUntil", 0f);
                    }
                    foreach (var entry in seen)
                        if ((entry.Key == null || !entry.Key.gameObject.activeInHierarchy) && ended.Add(entry.Value))
                        { data.shots[entry.Value].landing = tick; if (impact < 0) impact = tick; }
                    if (!baseline)
                    {
                        var block = new MaterialPropertyBlock();
                        foreach (var sr in Root.GetComponentsInChildren<SpriteRenderer>())
                        {
                            sr.GetPropertyBlock(block);
                            if (sr.name == "Forming aerial sword / no damage")
                            { float value = block.GetFloat("_Formation"); revealed |= value > 0 && value < 1; }
                            if (sr.name == "SW05 wood-embedded sword / no damage")
                            { float value = block.GetFloat("_Dissolve"); dissolved |= value > 0 && value < 1; }
                            if (sr.name == "SW05 animated ground impact / no damage")
                            {
                                playedImpactFrames.Add(sr.sprite.name);
                                Check(Mathf.Abs(sr.transform.position.y - 0.05f) < 0.0001f, "impact pivot remains on ground");
                                Check(sr.GetComponent<Collider2D>() == null, "impact animation is harmless");
                            }
                        }
                    }
                    if (tick == 103 || tick == 120 || tick == 180) Capture("formation-" + tick);
                    if (tick == 180 && !baseline)
                    {
                        var camera = game.GameCamera; var position = camera.transform.position; float height = camera.orthographicSize;
                        try
                        {
                            // Supplement the unchanged gameplay camera with a
                            // wider native render of the complete sky formation.
                            camera.transform.position = new Vector3(position.x, 18, position.z); camera.orthographicSize = 23;
                            Capture("formation-overview");
                        }
                        finally { camera.transform.position = position; camera.orthographicSize = height; }
                    }
                    if (release >= 0 && (tick == release + 3 || tick == release + 6)) Capture("fall-" + (tick - release));
                    if (impact >= 0 && new[] { 0, 3, 7, 13, 22, 34 }.Contains(tick - impact)) Capture("impact-" + (tick - impact));
                    if (release >= 0 && tick >= release + 40) { done = true; probe.After = null; }
                    tick++;
                }
                catch (Exception error) { failure = error.ToString(); done = true; probe.After = null; }
            };
            double limit = EditorApplication.timeSinceStartup + 90;
            while (!done) { Check(EditorApplication.timeSinceStartup < limit, "first-group timeout"); await Task.Delay(5); }
            UnityEngine.Object.Destroy(probe.gameObject); Check(failure == null, failure);
            Check(data.shots.Count >= 16 && data.shots.Count <= 18 && ended.Count == data.shots.Count, "one complete dense group");
            Check(data.frames.Last().playerHealth < data.frames.First().playerHealth, "real descending blade damages player");
            foreach (string material in new[] { "SW05/SW05Celestial", "SW05/SW05CrimsonBlade", "SW05/SW05EmbeddedWood" })
            { var mat = Resources.Load<Material>(material); Check(mat != null && !ShaderUtil.ShaderHasError(mat.shader), material + " compiles"); }
            File.WriteAllText(Output + "/" + (baseline ? "baseline" : "updated") + ".json", JsonUtility.ToJson(data, true));
            if (!baseline)
            {
                var old = JsonUtility.FromJson<Data>(File.ReadAllText(Output + "/baseline.json"));
                Check(JsonUtility.ToJson(old) == JsonUtility.ToJson(data), "exact launch/landing, trajectories, damage, bounds and health comparison");
                foreach (string pose in new[] { "SW05_JudgementSword_v2", "SW05_Impact_0", "SW05_Impact_1", "SW05_Impact_2", "SW05_Impact_3" })
                    Check(KarmaArtwork.Sprite("SwordBossVFX", pose) != null, pose + " loads by owned logical name");
                Check(revealed && dissolved && playedImpactFrames.Count == 4, "formation, four actual impact frames and dissolve animate");
                lines.Add("PASS: exact baseline comparison of all shot recipes, per-frame trajectories, landing ticks, boss bounds and actual player damage");
                lines.Add("PASS: formation reveal, all four grounded impact frames and planted dissolve played; visible tip aligned to physical tip");
            }
            lines.Add("PASS: SW05 first group only; " + data.shots.Count + " swords; first launch tick " + release + "; first impact tick " + impact);
            lines.Add("PASS: downward-facing sword; ground contact y=0.05; SW05 materials compile/load");
            lines.Add("Actual damage: " + (data.frames.First().playerHealth - data.frames.Last().playerHealth));
            Enemy.GetType().GetMethod("InterruptPattern", Flags).Invoke(Enemy, null); Set(Enemy, "running", true);
        }
        catch (Exception error) { lines.Insert(0, "FAIL: " + error); Debug.LogException(error); exit = 1; }
        File.WriteAllLines(Output + "/" + (baseline ? "baseline-result" : "result") + ".txt", lines);
        Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
}
#endif
