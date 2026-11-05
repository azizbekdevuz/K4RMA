#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KarmaPrototype;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Explicitly invoked only in the disposable validation project. Never an
// automatic importer: original sprites, GUIDs and slices are not touched.
public static class KarmaSW02SpriteImportChecks
{
    public const string Sheet = "Assets/K4RMA/Arts/Bosses/SwordWave/Characters/SW02/Wave_SW02_Transitions.png";
    const string Library = "Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/Resources/BossArtwork/KarmaSwordBossSprites.asset";
    [Serializable] public sealed class Polygon { public Vector2[] points; }
    [Serializable] public sealed class Frame { public string name; public float x, y, pivotX, pivotY; public Polygon[] outlines; }
    [Serializable] public sealed class Frames { public Frame[] frames; }
    public static void RunBatch()
    {
        try
        {
            var frames = JsonUtility.FromJson<Frames>(File.ReadAllText("output/validation/sw02-animation-phase2-20261105/sprite-import.json")).frames;
            AssetDatabase.ImportAsset(Sheet, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Sheet);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 125; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.Tight; importer.SetTextureSettings(settings);
            var factories = new SpriteDataProviderFactories(); factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var rects = frames.Select(f => new SpriteRect { name = f.name, rect = new Rect(f.x, f.y, 512, 512),
                alignment = SpriteAlignment.Custom, pivot = new Vector2(f.pivotX, f.pivotY),
                spriteID = existing.TryGetValue(f.name, out var id) ? id : GUID.Generate() }).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            var outlineProvider = provider.GetDataProvider<ISpriteOutlineDataProvider>();
            for (int i = 0; i < rects.Length; i++) outlineProvider.SetOutlines(rects[i].spriteID, frames[i].outlines.Select(p => p.points).ToList());
            provider.Apply(); importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().ToArray();
            if (sprites.Length != 6 || sprites.Any(s => s.pixelsPerUnit != 125)) throw new Exception("Six PPU-125 sprites required");
            var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>(Library);
            var sheets = library.sheets.Where(s => s.id != "WaveSW02").ToList();
            sheets.Add(new KarmaSpriteLibrary.Sheet { id = "WaveSW02", source = Sheet,
                poses = frames.Select(f => f.name).ToArray(), sprites = frames.Select(f => sprites.Single(s => s.name == f.name)).ToArray() });
            library.sheets = sheets.ToArray(); EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
            File.WriteAllText("output/validation/sw02-animation-phase2-20261105/import-result.txt", "PASS: six tightly outlined, foot-pivoted PPU125 sprites registered only in owned library.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
#endif
