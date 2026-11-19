using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace KarmaPrototype.EditorTools
{
    public static class KarmaMotionSheetImport
    {
        internal const string Root = "Assets/Arts/Resources/Motions12/";
        public static void Import()
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string line in File.ReadLines(Root + "Catalog.txt"))
                {
                    string[] fields = line.Split('\t');
                    string path = Root + fields[0] + "/" + fields[1] + ".png";
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    int columns = int.Parse(fields[2]), rows = int.Parse(fields[3]), count = int.Parse(fields[4]);
                    int w = int.Parse(fields[5]), h = int.Parse(fields[6]);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Multiple;
                    importer.spritePixelsPerUnit = float.Parse(fields[7], CultureInfo.InvariantCulture);
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.maxTextureSize = 4096;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    var data = factory.GetSpriteEditorDataProviderFromObject(importer);
                    data.InitSpriteEditorDataProvider();
                    var capability = data.GetDataProvider<ISpriteFrameEditCapability>();
                    if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))
                        throw new InvalidOperationException("Cannot slice " + path);
                    var old = data.GetSpriteRects().ToDictionary(r => r.name);
                    var rects = new SpriteRect[count];
                    var pivot = new Vector2(float.Parse(fields[8], CultureInfo.InvariantCulture), float.Parse(fields[9], CultureInfo.InvariantCulture));
                    for (int i = 0; i < count; i++)
                    {
                        string key = fields[1] + "_" + i.ToString("00");
                        rects[i] = new SpriteRect { name = key, spriteID = old.TryGetValue(key, out var prior) ? prior.spriteID : GUID.Generate(),
                            rect = new Rect(i % columns * w, (rows - 1 - i / columns) * h, w, h), alignment = SpriteAlignment.Custom, pivot = pivot };
                    }
                    data.SetSpriteRects(rects);
                    var ids = data.GetDataProvider<ISpriteNameFileIdDataProvider>();
                    ids.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
                    data.Apply();
                    importer.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
        }
    }

    // A sheet can contain several drawing scales. Store the scale on each native
    // Sprite so clips, mesh copies and weapon sockets see the same geometry.
    sealed class KarmaMotionSpriteScaleImport : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        const string Catalog = KarmaMotionSheetImport.Root + "Catalog.txt";
        static Dictionary<string, float[]> scales;
        static DateTime catalogTime;

        bool IsMotionSheet => assetPath.StartsWith(KarmaMotionSheetImport.Root, StringComparison.Ordinal)
            && assetPath.EndsWith(".png", StringComparison.Ordinal);

        void OnPreprocessTexture()
        {
            if (IsMotionSheet) context.DependsOnSourceAsset(Catalog);
        }

        void OnPostprocessSprites(Texture2D texture, Sprite[] sprites)
        {
            if (!IsMotionSheet) return;
            var stamp = File.GetLastWriteTimeUtc(Catalog);
            if (scales == null || stamp != catalogTime)
            {
                var parsed = new Dictionary<string, float[]>(StringComparer.Ordinal);
                foreach (string row in File.ReadLines(Catalog))
                {
                    var fields = row.Split('\t');
                    string path = KarmaMotionSheetImport.Root + fields[0] + "/" + fields[1] + ".png";
                    if (fields.Length != 11) throw new InvalidDataException("Missing sprite scales for " + path);
                    var values = fields[10].Split(',').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                    if (values.Length != int.Parse(fields[4]) || values.Any(v => !(v > 0f) || float.IsInfinity(v)))
                        throw new InvalidDataException("Invalid sprite scales for " + path);
                    parsed.Add(path, values);
                }
                scales = parsed;
                catalogTime = stamp;
            }
            if (!scales.TryGetValue(assetPath, out var targets) || targets.Length != sprites.Length)
                throw new InvalidDataException("Unregistered motion sheet or frame count for " + assetPath);
            var frames = sprites.OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
            string motion = Path.GetFileNameWithoutExtension(assetPath);
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i].name != motion + "_" + i.ToString("00"))
                    throw new InvalidDataException("Unexpected frame " + frames[i].name + " in " + assetPath);
                SetScale(frames[i], targets[i]);
            }
        }

        static void SetScale(Sprite sprite, float target)
        {
            float original = sprite.pixelsPerUnit;
            if (original == target) return;
            Vector2[] positions = sprite.vertices;
            var outlines = new List<Vector2[]>();
            for (int i = 0; i < sprite.GetPhysicsShapeCount(); i++)
            {
                var points = sprite.GetPhysicsShape(i).ToArray();
                // Auto-tracing can emit a two-point fragment. Unity only accepts closed polygons.
                if (points.Length < 3) continue;
                for (int j = 0; j < points.Length; j++) points[j] = points[j] * original + sprite.pivot;
                outlines.Add(points);
            }

            using var serialized = new SerializedObject(sprite);
            var units = serialized.FindProperty("m_PixelsToUnits")
                ?? throw new InvalidOperationException("Unity Sprite has no pixels-per-unit field");
            var uvs = new NativeArray<Vector2>(sprite.uv, Allocator.Temp);
            var vertices = new NativeArray<Vector3>(positions.Length, Allocator.Temp);
            try
            {
                units.floatValue = target;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                float scale = original / target;
                for (int i = 0; i < positions.Length; i++) vertices[i] = positions[i] * scale;
                sprite.SetVertexAttribute(VertexAttribute.Position, vertices);
                sprite.SetVertexAttribute(VertexAttribute.TexCoord0, uvs);
                // OverridePhysicsShape accepts rect-local pixels, GetPhysicsShape returns world units.
                if (outlines.Count > 0) sprite.OverridePhysicsShape(outlines);
            }
            finally
            {
                vertices.Dispose();
                uvs.Dispose();
            }
        }
    }
}
