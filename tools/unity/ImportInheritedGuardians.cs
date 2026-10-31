using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using KarmaPrototype;

// Standalone native import: preserves authored equipment and stable subasset IDs.
public static class ImportInheritedGuardians
{
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
        var mask = new HashSet<Vector2Int>();
        foreach (var p in component)
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                var q = p + new Vector2Int(dx, dy);
                if (q.x >= 0 && q.x < width && q.y >= 0 && q.y < height) mask.Add(q);
            }
        var edges = new Dictionary<Vector2Int, List<Vector2Int>>();
        Action<Vector2Int, Vector2Int> edge = (a, b) => {
            if (!edges.TryGetValue(a, out var next)) edges[a] = next = new List<Vector2Int>();
            next.Add(b);
        };
        foreach (var p in mask)
        {
            if (!mask.Contains(p + Vector2Int.down)) edge(p, p + Vector2Int.right);
            if (!mask.Contains(p + Vector2Int.right)) edge(p + Vector2Int.right, p + Vector2Int.one);
            if (!mask.Contains(p + Vector2Int.up)) edge(p + Vector2Int.one, p + Vector2Int.up);
            if (!mask.Contains(p + Vector2Int.left)) edge(p + Vector2Int.up, p);
        }
        var largest = new List<Vector2>(); float largestArea = 0;
        while (edges.Count > 0)
        {
            var start = edges.First().Key; var current = start; var loop = new List<Vector2>();
            int limit = mask.Count * 8;
            do
            {
                loop.Add(current);
                if (!edges.TryGetValue(current, out var next) || next.Count == 0) break;
                var destination = next[next.Count-1]; next.RemoveAt(next.Count-1);
                if (next.Count == 0) edges.Remove(current);
                current = destination;
            } while (current != start && --limit > 0);
            if (loop.Count < 3) continue;
            float area = 0;
            for (int i = 0; i < loop.Count; i++) { var a = loop[i]; var b = loop[(i+1)%loop.Count]; area += a.x*b.y-b.x*a.y; }
            if (Mathf.Abs(area) > largestArea) { largestArea = Mathf.Abs(area); largest = loop; }
        }
        int half = largest.Count/2;
        var first = Simplify(largest.Take(half+1).ToList());
        var second = Simplify(largest.Skip(half).Concat(new[] { largest[0] }).ToList());
        return first.Take(first.Count-1).Concat(second.Take(second.Count-1))
            .Select(p => p-new Vector2(width/2f, height/2f)).ToArray();
    }
    static List<Vector2> Simplify(List<Vector2> points)
    {
        if (points.Count <= 2) return points;
        var a = points[0]; var b = points[points.Count-1]; var ab = b-a;
        float farthest = 0; int split = 0;
        for (int i = 1; i < points.Count-1; i++)
        {
            float t = ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(points[i]-a, ab)/ab.sqrMagnitude) : 0;
            float distance = (points[i]-(a+ab*t)).sqrMagnitude;
            if (distance > farthest) { farthest = distance; split = i; }
        }
        if (farthest <= 1) return new List<Vector2> { a, b };
        var left = Simplify(points.Take(split+1).ToList());
        var right = Simplify(points.Skip(split).ToList());
        return left.Take(left.Count-1).Concat(right).ToList();
    }

    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before importing inherited poses.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report = new List<string>();
        foreach (string form in new[] { "WaveRise", "WaveWard", "WaveFinal", "RiseWave", "RiseWard", "RiseFinal", "WardWave", "WardRise", "WardFinal" })
        {
            string type = form.StartsWith("Wave", StringComparison.Ordinal) ? "Wave" : form.StartsWith("Rise", StringComparison.Ordinal) ? "Rise" : "Ward";
            string[] poseNames = KarmaArtwork.Settings.spriteLibrary.All(type).Select(s => s.name)
                .Concat(new[] { "RevivePush", "ReviveKneel", "ReviveRise", "ReviveStand" }).ToArray();
            if (poseNames.Length != 16) throw new InvalidOperationException("Expected 12 base poses plus four get-up poses.");
            string path = "Assets/K4RMA/Art/Resources/K4RMAArtwork/Characters/Guardians/Inherited/" + form + ".png";
            var source = new Texture2D(2, 2);
            ImageConversion.LoadImage(source, System.IO.File.ReadAllBytes(path));
            if (source.width < 64 || source.height < 64 || source.width != source.height)
                throw new InvalidOperationException("Expected a square 4 by 4 sheet: " + form);
            int cellWidth = source.width / 4, cellHeight = source.height / 4;
            int padX = cellWidth / 3, padY = cellHeight / 6;
            var pixels = source.GetPixels32();
            var rectangles = new Rect[16]; var pivots = new Vector2[16]; var outlinesByFrame = new Vector2[16][];
            int idleHeight = 0;
            for (int frame = 0; frame < 16; frame++)
            {
                int column = frame % 4, row = 3 - frame / 4;
                int left = Math.Max(0, column * source.width / 4 - padX), bottom = Math.Max(0, row * source.height / 4 - padY);
                int right = Math.Min(source.width, (column + 1) * source.width / 4 + padX);
                int top = Math.Min(source.height, (row + 1) * source.height / 4 + padY);
                int width = right-left, height = top-bottom;
                var component = MainComponent(pixels, source.width, left, bottom, width, height);
                if (component.Count == 0) throw new InvalidOperationException("Empty pose: " + form + "/" + poseNames[frame]);
                int minX = component.Min(p => p.x), minY = component.Min(p => p.y);
                int maxX = component.Max(p => p.x), maxY = component.Max(p => p.y);
                int cropX = Math.Max(0, minX-2), cropY = Math.Max(0, minY-2);
                int cropWidth = Math.Min(width, maxX+3)-cropX, cropHeight = Math.Min(height, maxY+3)-cropY;
                rectangles[frame] = new Rect(left+cropX, bottom+cropY, cropWidth, cropHeight);
                pivots[frame] = new Vector2(((column + 0.5f) * source.width/4f-left-cropX)/cropWidth,
                    (float)(minY-cropY) / cropHeight);
                outlinesByFrame[frame] = Outline(component.Select(p => new Vector2Int(p.x-cropX, p.y-cropY)).ToList(), cropWidth, cropHeight);
                if (frame == 0) idleHeight = component.Max(p => p.y)-component.Min(p => p.y)+1;
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = idleHeight / KarmaArtwork.Sprite(type, "Idle").bounds.size.y;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 4096;
            var textureSettings = new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.Tight; importer.SetTextureSettings(textureSettings);
            importer.SaveAndReimport();
            var factories = new SpriteDataProviderFactories(); factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new InvalidOperationException("No sprite provider: " + form);
            provider.InitSpriteEditorDataProvider();
            var edit = provider.GetDataProvider<ISpriteFrameEditCapability>();
            var required = EEditCapability.CreateAndDeleteSprite | EEditCapability.EditSpriteName
                | EEditCapability.EditSpriteRect | EEditCapability.EditPivot;
            if (edit == null || !edit.GetEditCapability().HasCapability(required)) throw new InvalidOperationException("Unsupported sprite edits: " + form);
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            var outlines = provider.GetDataProvider<ISpriteOutlineDataProvider>();
            if (names == null || outlines == null) throw new InvalidOperationException("Missing sprite ID/outline provider: " + form);
            var old = provider.GetSpriteRects().ToDictionary(s => s.name, s => s.spriteID);
            var rects = Enumerable.Range(0, 16).Select(frame => new SpriteRect {
                name = poseNames[frame], rect = rectangles[frame], alignment = SpriteAlignment.Custom, pivot = pivots[frame],
                spriteID = old.TryGetValue(poseNames[frame], out var id) ? id : GUID.Generate()
            }).ToArray();
            provider.SetSpriteRects(rects);
            names.SetNameFileIdPairs(rects.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
            for (int frame = 0; frame < 16; frame++)
                outlines.SetOutlines(rects[frame].spriteID, new List<Vector2[]> { outlinesByFrame[frame] });
            provider.Apply(); importer.SaveAndReimport();
            var imported = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            if (imported.Length != 16 || imported.Any(s => s.vertices.Length < 3)) throw new InvalidOperationException("Incomplete inherited sheet: " + form);
            report.Add(form + ": 16 poses, stable sprite IDs, ground pivots, clipped render meshes");
            UnityEngine.Object.DestroyImmediate(source);
        }
        AssetDatabase.SaveAssets();
        System.IO.Directory.CreateDirectory("output/validation/inheritance");
        System.IO.File.WriteAllText("output/validation/inheritance/import.txt", string.Join("\n", report));
        return string.Join("\n", report);
    }
}

