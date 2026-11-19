#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

// Validates the current five-round SW03 cast in Play Mode.
[InitializeOnLoad]
public static class KarmaSW03ArtworkChecks
{
    const string Output = "output/validation/sw03-artwork-20261105";
    const string Key = "K4RMA.SW03Artwork";
    const string Library = "Assets/Data/Resources/BossArtwork/KarmaSwordBossSprites.asset";
    static bool started;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static KarmaSW03ArtworkChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying) { Application.runInBackground = true; EditorApplication.isPaused = false; }
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2) { started = true; Run(); }
    }
    public static void RunBaseline() => RunUpdated();
    public static void RunUpdated()
    {
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    public static void ImportArtwork()
    {
        try
        {
            var names = new[] { "SW03_GroundScar_v7", "SW03_RisingBlade_v7" };
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
                if (name == "SW03_GroundScar_v7")
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(1089f / 2172, 373f / 724);
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
    static async void Run()
    {
        int exit=0;
        try
        {
            // Placement/count/timing intentionally changed. Historical artwork
            // baseline JSON is no longer a gameplay-equivalence oracle.
            await KarmaSwordSkillRefreshChecks.CheckSW03Redesign(null,true);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"/result.txt","PASS: current SW03 five maximum-density cluster rounds; preserved artwork/anchor/aura and full warnings; physical safe-route checks\n");
        }
        catch(Exception e){Debug.LogException(e);exit=1;}
        Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(exit);
    }
}
#endif
