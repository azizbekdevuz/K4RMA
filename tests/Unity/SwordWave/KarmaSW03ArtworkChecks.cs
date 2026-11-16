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
    const string Library = "Assets/Data/Resources/BossArtwork/KarmaSwordBossSprites.asset";
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
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
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
                string path = KarmaSW05ArtworkChecks.EffectPath(name);
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
        int exit=0;
        try
        {
            // Placement/count/timing intentionally changed. Historical artwork
            // baseline JSON is no longer a gameplay-equivalence oracle.
            await KarmaSwordSkillRefreshChecks.CheckSW03Redesign(null,true);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"/result.txt","PASS: current SW03 5 x (8+8); preserved artwork/anchor/aura and full warnings; physical safe-route checks\n");
        }
        catch(Exception e){Debug.LogException(e);exit=1;}
        Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(exit);
    }
}
#endif
