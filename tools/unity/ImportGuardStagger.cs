#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportGuardStagger
{
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode before importing.");
        AssetDatabase.Refresh();
        string report = "Transparent C+A stagger sheets; original standing assets unchanged.\n";
        string[] keys = { "Ward", "WardWave", "WardRise", "WardFinal" };
        // Authored body center between grounded boots, independent of sword/cloak width.
        float[] anchors = { 0.79f, 0.80f, 0.79f, 0.75f };
        // Rise's cloak hangs 12 pixels below the grounded soles.
        float[] soles = { 0, 0, 12, 0 };
        for (int k = 0; k < keys.Length; k++)
        {
            string path = "Assets/K4RMA/Resources/K4RMAArtwork/Sheets/Stagger/" + keys[k] + ".png";
            var image = new Texture2D(2, 2);
            if (!image.LoadImage(File.ReadAllBytes(path))) throw new Exception("PNG load failed: " + path);
            int w = image.width, h = image.height;
            var pixels = image.GetPixels32();
            if (pixels.Count(p => p.a == 0) < pixels.Length / 4) throw new Exception("Real transparency required.");
            int split = -1;
            for (int x = w / 2; x < w * 3 / 5 && split < 0; x++)
                if (Enumerable.Range(0, h).All(y => pixels[y * w + x].a <= 8)) split = x;
            if (split < 0) throw new Exception("No transparent gutter: " + keys[k]);
            var frames = new SpriteRect[2];
            for (int cell = 0; cell < 2; cell++)
            {
                int left = cell == 0 ? 0 : split, right = cell == 0 ? split : w;
                int x0 = right, x1 = left, y0 = h, y1 = -1;
                for (int y = 0; y < h; y++)
                for (int x = left; x < right; x++)
                    if (pixels[y * w + x].a > 8)
                    { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
                if (y1 < y0) throw new Exception("Empty pose");
                var rect = new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                frames[cell] = new SpriteRect { name = cell == 0 ? "Reference" : "Stagger", rect = rect,
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(cell == 0 ? 0.5f : (w * anchors[k] - x0) / rect.width,
                        cell == 0 ? 0 : soles[k] / rect.height),
                    spriteID = GUID.Generate() };
            }
            UnityEngine.Object.DestroyImmediate(image);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 200; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new Exception("Sprite provider unavailable");
            provider.InitSpriteEditorDataProvider();
            var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
            var required = EEditCapability.CreateAndDeleteSprite | EEditCapability.EditSpriteName
                | EEditCapability.EditSpriteRect | EEditCapability.EditPivot;
            if (capability == null || !capability.GetEditCapability().HasCapability(required))
                throw new Exception("Required slicing capabilities unavailable");
            var ids = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (ids == null) throw new Exception("Name/file ID provider unavailable");
            // Keep IDs stable when adjusting an imported pivot.
            var old = provider.GetSpriteRects();
            foreach (var frame in frames)
            { var previous = old.FirstOrDefault(f => f.name == frame.name); if (previous != null) frame.spriteID = previous.spriteID; }
            provider.SetSpriteRects(frames);
            ids.SetNameFileIdPairs(frames.Select(f => new SpriteNameFileIdPair(f.name, f.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
            if (AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() != 2) throw new Exception("Missing imported pose");
            report += keys[k] + " " + w + "x" + h + ": " + string.Join("; ", frames.Select(f => f.name + " " + f.rect + " pivot " + f.pivot)) + "\n";
        }
        Directory.CreateDirectory("output/validation");
        File.WriteAllText("output/validation/guard-stagger-import.txt", report);
        return report;
    }
}
#endif
