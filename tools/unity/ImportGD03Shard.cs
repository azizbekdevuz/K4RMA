#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using KarmaPrototype;

// Standalone Pipeline script; imports only the selected GD03 art, leaving GuardShard/GD05 intact.
public static class ImportGD03Shard
{
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before importing.");
        const string path = "Assets/K4RMA/Resources/K4RMAArtwork/Sheets/GD03/ShardDB.png";
        AssetDatabase.Refresh();
        var image = new Texture2D(2, 2);
        if (!image.LoadImage(File.ReadAllBytes(path))) throw new Exception("PNG load failed.");
        int width = image.width, height = image.height;
        var pixels = image.GetPixels32();
        int split = -1, bestDistance = int.MaxValue, transparent = 0;
        foreach (var p in pixels) if (p.a == 0) transparent++;
        if (transparent < pixels.Length / 4) throw new Exception("A genuinely transparent sprite sheet is required.");
        // The generated sheet's central transparent gutter need not be precisely half the canvas.
        for (int x = width * 2 / 5; x < width * 3 / 5; x++)
        {
            bool clear = true;
            for (int y = 0; y < height && clear; y++) clear = pixels[y * width + x].a <= 8;
            int distance = Math.Abs(x - width / 2);
            if (clear && distance < bestDistance) { split = x; bestDistance = distance; }
        }
        if (split < 0) throw new Exception("No transparent gutter between the two states.");
        var frames = new SpriteRect[2];
        for (int cell = 0; cell < 2; cell++)
        {
            int left = cell == 0 ? 0 : split, right = cell == 0 ? split : width;
            int x0 = right, y0 = height, x1 = left, y1 = -1;
            for (int y = 0; y < height; y++)
            for (int x = left; x < right; x++)
                if (pixels[y * width + x].a > 8)
                { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
            if (y1 < y0) throw new Exception("Empty sprite state.");
            x0 = Math.Max(left, x0 - 2); y0 = Math.Max(0, y0 - 2);
            x1 = Math.Min(right - 1, x1 + 2); y1 = Math.Min(height - 1, y1 + 2);
            frames[cell] = new SpriteRect { name = cell == 0 ? "Hot" : "Cooled",
                rect = new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1),
                pivot = Vector2.one * 0.5f, alignment = SpriteAlignment.Center, spriteID = GUID.Generate() };
        }
        UnityEngine.Object.DestroyImmediate(image);
        return ImportFrames(path, "GuardShardDB", frames, "Imported GD03 D+B: " + width + "x" + height
            + ", Hot/Cooled with centered pivots; alpha coverage " + (100f * transparent / pixels.Length).ToString("F1")
            + "% transparent", "gd03-import-validation.txt");
    }

    public static string RunWholeShield()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before importing.");
        const string path = "Assets/K4RMA/Resources/K4RMAArtwork/Sheets/GD03/HeatedWholeShield.png";
        AssetDatabase.Refresh();
        var image = new Texture2D(2, 2);
        if (!image.LoadImage(File.ReadAllBytes(path))) throw new Exception("Whole-shield PNG load failed.");
        int width = image.width, height = image.height;
        var pixels = image.GetPixels32();
        int x0 = width, y0 = height, x1 = -1, y1 = -1, transparent = 0;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            byte alpha = pixels[y * width + x].a;
            if (alpha == 0) transparent++;
            if (alpha > 8) { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
        }
        if (transparent < pixels.Length / 10 || x1 < x0) throw new Exception("Transparent whole-shield sprite required.");
        var frame = new SpriteRect { name = "Hot", rect = new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1),
            pivot = Vector2.one * 0.5f, alignment = SpriteAlignment.Center, spriteID = GUID.Generate() };
        UnityEngine.Object.DestroyImmediate(image);
        return ImportFrames(path, "GuardWholeShield", new[] { frame },
            "Imported whole heated shield: " + width + "x" + height + ", centered Hot sprite", "gd03-whole-shield-import-validation.txt");
    }

    static string ImportFrames(string path, string sheetId, SpriteRect[] frames, string summary, string report)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 200;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new Exception("Sprite provider unavailable; abort.");
        provider.InitSpriteEditorDataProvider();
        var edit = provider.GetDataProvider<ISpriteFrameEditCapability>();
        var required = EEditCapability.CreateAndDeleteSprite | EEditCapability.EditSpriteName
            | EEditCapability.EditSpriteRect | EEditCapability.EditPivot;
        if (edit == null || !edit.GetEditCapability().HasCapability(required))
            throw new Exception("Sprite slicing capability unavailable; abort.");
        var ids = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (ids == null) throw new Exception("Sprite name/file ID provider unavailable; abort.");
        provider.SetSpriteRects(frames);
        ids.SetNameFileIdPairs(frames.Select(f => new SpriteNameFileIdPair(f.name, f.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        if (sprites.Length != frames.Length || frames.Any(f => !sprites.Any(s => s.name == f.name)))
            throw new Exception("All imported states must exist.");
        var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>("Assets/K4RMA/Resources/KarmaSpriteLibrary.asset");
        var sheets = new List<KarmaSpriteLibrary.Sheet>(library.sheets);
        sheets.RemoveAll(s => s.id == sheetId);
        sheets.Add(new KarmaSpriteLibrary.Sheet { id = sheetId, source = path, sprites = sprites });
        library.sheets = sheets.ToArray(); EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
        string result = summary + "; rects "
            + string.Join("; ", frames.Select(f => f.name + " " + f.rect));
        Directory.CreateDirectory("output/validation");
        File.WriteAllText("output/validation/" + report, result);
        return result;
    }
}
#endif
