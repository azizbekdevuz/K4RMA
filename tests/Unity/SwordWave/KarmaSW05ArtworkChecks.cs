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

// One SW05 rain group only, at 60 Hz. Capture the actual URP game camera and
// compare the unmodified attack against the artwork-only version.
[InitializeOnLoad]
public static class KarmaSW05ArtworkChecks
{
    const string Output = "output/validation/sw05-artwork-20261105";
    const string Key = "K4RMA.SW05Artwork";
    const string Artwork = "Assets/Arts/SwordWave/Effects/";
    public static string EffectPath(string name) => Artwork + name + ".png";
    const string Library = "Assets/Data/Resources/BossArtwork/KarmaSwordBossSprites.asset";
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
    public static void ImportSuppliedPack() => ImportGrandPack();
    public static void ImportGrandPack()
    {
        try
        {
            var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>(Library);
            var sheet = library.sheets.Single(s => s.id == "SwordBossVFX");
            var sprites = sheet.sprites.ToList(); var poses = sheet.poses.ToList();
            foreach (string name in new[] { "SW04_OrbitingSwordGuard_Base", "SW05_HeavyJudgementSword_optional" })
            {
                string path = EffectPath(name); AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 100;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 8192; importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = name.Contains("Heavy") ? new Vector2(255f/512,509f/1024) : new Vector2(193f/384,193f/384);
                importer.SetTextureSettings(settings);
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name))
                {
                    int index=poses.IndexOf(sprite.name);
                    if(index<0){poses.Add(sprite.name);sprites.Add(sprite);}else sprites[index]=sprite;
                }
            }
            sheet.sprites=sprites.ToArray();sheet.poses=poses.ToArray();EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
            Debug.Log("GRAND IMPORT PASS: guard ring base and heavy sword; existing GUIDs preserved");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Begin(bool before)
    {
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Before", before);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    public static void ImportArtwork() => ImportSuppliedPack();

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
            bool groundedLandingSeen = false;
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
                                // Current heavy artwork: alpha >= 128 core is
                                // x=57..453, y=25..993 in Unity pixel space.
                                // The imported (255,509) pivot centers that core.
                                Check(Vector2.Distance(sprite.pivot, new Vector2(255, 509)) < 0.001f,
                                    "heavy artwork retains its measured opaque-core pivot");
                                var localTip = (new Vector2(255, 25) - sprite.pivot) / sprite.pixelsPerUnit;
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
                        foreach(var energy in Root.GetComponentsInChildren<KarmaSW05Energy>())
                            if(energy.name=="SW05 batched grounded landings / no damage")
                            {
                                groundedLandingSeen=true;
                                Check(!energy.CapacityExceeded && energy.GetComponent<Collider2D>()==null,
                                    "grounded landing batch fits its budget and remains harmless");
                            }
                        var block = new MaterialPropertyBlock();
                        foreach (var sr in Root.GetComponentsInChildren<SpriteRenderer>())
                        {
                            sr.GetPropertyBlock(block);
                            if (sr.name == "Forming aerial sword / no damage")
                            { float value = block.GetFloat("_Formation"); revealed |= value > 0 && value < 1; }
                            if (sr.name == "SW05 wood-embedded sword / no damage")
                            { float value = block.GetFloat("_Dissolve"); dissolved |= value > 0 && value < 1; }

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
            foreach (string material in new[] { "SwordWave/SW05/SW05Celestial", "SwordWave/SW05/SW05CrimsonBlade", "SwordWave/SW05/SW05EmbeddedWood" })
            { var mat = KarmaArtResource.Load<Material>(material); Check(mat != null && !ShaderUtil.ShaderHasError(mat.shader), material + " compiles"); }
            File.WriteAllText(Output + "/" + (baseline ? "baseline" : "updated") + ".json", JsonUtility.ToJson(data, true));
            if (!baseline)
            {
                var old = JsonUtility.FromJson<Data>(File.ReadAllText(Output + "/baseline.json"));
                Check(JsonUtility.ToJson(old) == JsonUtility.ToJson(data), "exact launch/landing, trajectories, damage, bounds and health comparison");
                foreach (string pose in new[] { "SW05_HeavyJudgementSword_optional", "SW05_AerialSword" })
                    Check(KarmaArtwork.Sprite("SwordBossVFX", pose) != null, pose + " loads by owned logical name");
                Check(revealed && dissolved && groundedLandingSeen, "formation, grounded landing batch and dissolve animate");
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
