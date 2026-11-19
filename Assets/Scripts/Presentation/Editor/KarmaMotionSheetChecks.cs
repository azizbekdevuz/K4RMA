using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KarmaPrototype.EditorTools
{
    public static class KarmaMotionSheetChecks
    {
        [MenuItem("K4RMA/Animation/Reimport and verify motion sheets")]
        public static void ReimportAndVerify()
        {
            KarmaMotionSheetImport.Import();
            KarmaMotionBindings.Apply();
            Run();
        }

        public static void Run()
        {
            KarmaPlayer12AnimationAssets.Rebuild();
            var rows = new List<string> { "form,motion,frames,source" };
            var root = new GameObject("Motion binding check");
            try
            {
                var picture = root.AddComponent<SpriteRenderer>();
                foreach (string line in Resources.Load<TextAsset>("Motions12/Catalog").text.Split(new[]{'\n'}, StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = line.Split('\t');
                    string path = "Motions12/" + f[0] + "/" + f[1];
                    CheckCellEdges(KarmaMotionSheetImport.SheetPath(f[0], f[1]), f);
                    var sprites = KarmaArtResource.Frames(path).OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
                    if (sprites.Length != int.Parse(f[4])) throw new Exception(path + ": count");
                    var scales = f[10].Split(',').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                    var pivot = new Vector2(float.Parse(f[8], CultureInfo.InvariantCulture), float.Parse(f[9], CultureInfo.InvariantCulture));
                    for (int i = 0; i < sprites.Length; i++)
                    {
                        picture.sprite = sprites[i];
                        if (picture.sprite.name != f[1] + "_" + i.ToString("00") ||
                            AssetDatabase.GetAssetPath(picture.sprite) != KarmaMotionSheetImport.SheetPath(f[0], f[1]))
                            throw new Exception(path + ": incorrect frame " + i);
                        CheckGeometry(sprites[i], scales[i], pivot);
                    }
                    rows.Add(string.Join(",",f[0],f[1],sprites.Length,path));
                }
                foreach(var form in rows.Skip(1).Select(s => s.Split(',')[0]).Distinct()) KarmaArtwork.StageIdle(form);
                KarmaPlayerAnimationAssets.VerifyParrySuccess();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Directory.CreateDirectory("output/validation/motions12");
            File.WriteAllLines("output/validation/motions12/sheets.csv", rows);
            KarmaMotionRouteChecks.Run();
            Debug.Log("Verified " + (rows.Count-1) + " Resources sheets and both player clip libraries.");
        }

        // Read source PNG alpha before platform texture compression. Neighboring
        // cells must have a clear gutter, including around sword tips and ribbons.
        static void CheckCellEdges(string path, string[] fields)
        {
            int columns = int.Parse(fields[2]), rows = int.Parse(fields[3]), used = int.Parse(fields[4]);
            int width = int.Parse(fields[5]), height = int.Parse(fields[6]);
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!image.LoadImage(File.ReadAllBytes(path)) || image.width != columns * width
                    || image.height != rows * height)
                    throw new InvalidDataException(path + ": source PNG does not match its catalog grid");
                var pixels = image.GetPixels32();
                for (int cell = 0; cell < rows * columns; cell++)
                {
                    int left = cell % columns * width;
                    int bottom = (rows - 1 - cell / columns) * height;
                    for (int y = 0; y < height; y++)
                    {
                        bool fullRow = cell >= used || y == 0 || y == height - 1;
                        int start = (bottom + y) * image.width + left;
                        for (int x = 0; x < width; x += fullRow ? 1 : Mathf.Max(1, width - 1))
                            if (pixels[start + x].a >= 16)
                                throw new InvalidDataException($"{path} cell {cell + 1} contains visible artwork at ({x},{y}) in the gutter or an unused cell");
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        static void CheckGeometry(Sprite sprite, float expectedScale, Vector2 pivot)
        {
            if (Mathf.Abs(sprite.pixelsPerUnit - expectedScale) > 0.001f ||
                Vector2.Distance(sprite.pivot, Vector2.Scale(sprite.rect.size, pivot)) > 0.1f)
                throw new Exception(sprite.name + ": incorrect imported scale or pivot");
            Vector2[] vertices = sprite.vertices, uv = sprite.uv;
            Vector2 textureSize = new Vector2(sprite.texture.width, sprite.texture.height);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 texel = vertices[i] * expectedScale + sprite.pivot + sprite.rect.position;
                if (Vector2.Distance(texel, Vector2.Scale(uv[i], textureSize)) > 0.1f)
                    throw new Exception(sprite.name + ": vertex no longer samples its original texel");
            }
        }
    }
}
