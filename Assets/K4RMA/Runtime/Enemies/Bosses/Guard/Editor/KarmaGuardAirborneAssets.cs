using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
namespace KarmaPrototype.EditorTools
{
    public static class KarmaGuardAirborneAssets
    {
        const string Path = "Assets/K4RMA/Arts/Bosses/Guard/Characters/PlayerAirborneHit.png";
        public static void Import()
        {
            ImportSheet(Path);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>("Assets/K4RMA/Runtime/Enemies/Bosses/Guard/Presentation/Resources/BossArtwork/KarmaGuardBossSprites.asset");
            var sheets = library.sheets.Where(s => s.id != "GuardPlayerAirborneHit").ToList();
            sheets.Add(new KarmaSpriteLibrary.Sheet { id = "GuardPlayerAirborneHit", source = Path, sprites = sprites,
                poses = sprites.Select(s => s.name).ToArray() });
            library.sheets = sheets.ToArray(); EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
            if (sprites.Any(s => s.rect.width != 627 || s.pixelsPerUnit != 216)) throw new Exception("GD04 import mismatch");
            Debug.Log("GD04 airborne import PASS: four frames, 627px cells, 216 PPU.");
        }
        static void ImportSheet(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing parry texture: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 216;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
            if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))
                throw new InvalidOperationException("GD04 airborne slicing unsupported: " + path);
            var old = provider.GetSpriteRects();
            var rects = Enumerable.Range(0, 4).Select(i => new SpriteRect {
                name = "Frame" + i.ToString("00"),
                rect = new Rect(i % 2 * 627, (1 - i / 2) * 627, 627, 627),
                alignment = SpriteAlignment.Custom, pivot = new Vector2(0.5f, 70f / 627),
                spriteID = old.FirstOrDefault(s => s.name == "Frame" + i.ToString("00"))?.spriteID ?? GUID.Generate()
            }).ToArray();
            provider.SetSpriteRects(rects);
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (names == null) throw new InvalidOperationException("Sprite name mapping unsupported");
            names.SetNameFileIdPairs(rects.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            if (sprites.Length != 4) throw new InvalidOperationException("GD04 airborne frame count mismatch");
        }
    }
}
