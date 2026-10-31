using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using KarmaPrototype;

// Execute with `unity command script run`; metadata is always written by Unity.
public static class ImportGuardianRevival
{
    const string Folder = "Assets/K4RMA/Art/Resources/K4RMAArtwork/Characters/Guardians/Revival";
    static List<Vector2Int> MainComponent(Color32[] pixels, int stride, int left, int bottom, int width, int height)
    {
        var seen = new bool[width * height];
        var largest = new List<Vector2Int>();
        for (int start = 0; start < seen.Length; start++)
        {
            if (seen[start]) continue;
            seen[start] = true;
            if (pixels[(bottom + start / width) * stride + left + start % width].a < 64) continue;
            var component = new List<Vector2Int>();
            var queue = new Queue<int>(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int index = queue.Dequeue(), x = index % width, y = index / width;
                component.Add(new Vector2Int(x, y));
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        int next = ny * width + nx;
                        if (seen[next]) continue;
                        seen[next] = true;
                        if (pixels[(bottom + ny) * stride + left + nx].a >= 64) queue.Enqueue(next);
                    }
            }
            if (component.Count > largest.Count) largest = component;
        }
        return largest;
    }
    static Vector2[] Outline(List<Vector2Int> component, int width, int height)
    {
        var points = component.GroupBy(p => p.y).SelectMany(row => new[] {
            new Vector2(row.Min(p => p.x) - 2, row.Key - 2),
            new Vector2(row.Max(p => p.x) + 2, row.Key + 2)
        }).Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToArray();
        var hull = new List<Vector2>();
        Func<Vector2, Vector2, Vector2, float> cross = (a, b, c) => (b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        foreach (var point in points)
        {
            while (hull.Count >= 2 && cross(hull[hull.Count-2], hull[hull.Count-1], point) <= 0) hull.RemoveAt(hull.Count-1);
            hull.Add(point);
        }
        int lower = hull.Count;
        for (int i = points.Length - 2; i >= 0; i--)
        {
            while (hull.Count > lower && cross(hull[hull.Count-2], hull[hull.Count-1], points[i]) <= 0) hull.RemoveAt(hull.Count-1);
            hull.Add(points[i]);
        }
        hull.RemoveAt(hull.Count-1);
        return hull.Select(p => new Vector2(Mathf.Clamp(p.x, 0, width) - width/2f,
            Mathf.Clamp(p.y, 0, height) - height/2f)).ToArray();
    }
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before importing revival frames.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report = new List<string>();
        foreach (string form in new[] { "Neutral", "Wave", "Rise", "Ward" })
        {
            string path = Folder + "/" + form + ".png";
            var source = new Texture2D(2, 2);
            ImageConversion.LoadImage(source, System.IO.File.ReadAllBytes(path));
            int width = source.width / 3, height = source.height / 2;
            if (source.width % 3 != 0 || source.height % 2 != 0)
                throw new InvalidOperationException("Revival sheet must have three columns and two rows: " + form);
            var pixels = source.GetPixels32();
            var opaque = new RectInt[6];
            var renderOutlines = new Vector2[6][];
            for (int frame = 0; frame < 6; frame++)
            {
                int left = frame % 3 * width, bottom = (1 - frame / 3) * height;
                var component = MainComponent(pixels, source.width, left, bottom, width, height);
                if (component.Count == 0) throw new InvalidOperationException("Empty revival frame: " + form + "/" + frame);
                int minX = component.Min(p => p.x), minY = component.Min(p => p.y);
                int maxX = component.Max(p => p.x), maxY = component.Max(p => p.y);
                opaque[frame] = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
                renderOutlines[frame] = Outline(component, width, height);
            }
            var idle = KarmaArtwork.Sprite(form, "Idle");
            if (idle == null) throw new InvalidOperationException("Missing guardian idle: " + form);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = opaque[5].height / idle.bounds.size.y;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            var textureSettings = new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.Tight; importer.SetTextureSettings(textureSettings);
            importer.SaveAndReimport();
            var factories = new SpriteDataProviderFactories(); factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new InvalidOperationException("No sprite data provider: " + form);
            provider.InitSpriteEditorDataProvider();
            var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
            var required = EEditCapability.CreateAndDeleteSprite | EEditCapability.EditSpriteName
                | EEditCapability.EditSpriteRect | EEditCapability.EditPivot;
            if (capability == null || !capability.GetEditCapability().HasCapability(required))
                throw new InvalidOperationException("Revival slicing is not supported: " + form);
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (names == null) throw new InvalidOperationException("No stable sprite IDs: " + form);
            var outlines = provider.GetDataProvider<ISpriteOutlineDataProvider>();
            if (outlines == null) throw new InvalidOperationException("No render outline capability: " + form);
            var old = provider.GetSpriteRects().ToDictionary(s => s.name, s => s.spriteID);
            var rects = Enumerable.Range(0, 6).Select(frame => {
                string name = "Revive" + frame.ToString("00");
                return new SpriteRect {
                    name = name,
                    rect = new Rect(frame % 3 * width, (1 - frame / 3) * height, width, height),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, (float)opaque[frame].yMin / height),
                    spriteID = old.TryGetValue(name, out var id) ? id : GUID.Generate()
                };
            }).ToArray();
            provider.SetSpriteRects(rects);
            names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            for (int frame = 0; frame < 6; frame++)
                outlines.SetOutlines(rects[frame].spriteID, new List<Vector2[]> { renderOutlines[frame] });
            provider.Apply(); importer.SaveAndReimport();
            int count = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count();
            if (count != 6) throw new InvalidOperationException("Expected six revival frames: " + form);
            report.Add(form + ": " + count + " frames, " + importer.spritePixelsPerUnit.ToString("0.0") + " PPU");
            UnityEngine.Object.DestroyImmediate(source);
        }
        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }
}
