using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace KarmaPrototype.EditorTools
{
    // Match the painted armour and fabric of each drawing to its own Idle.
    // Works on the imported texture so every renderer, including cut-out
    // equipment and afterimages, samples the same colours.
    sealed class KarmaSwordBodyPaletteImport : AssetPostprocessor
    {
        const string Root = "Assets/Arts/SwordWave/";
        static readonly Dictionary<string, (DateTime stamp, Swatches palette)> references = new();

        string SwordForm()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal)) return null;
            string form = Path.GetFileName(Path.GetDirectoryName(assetPath));
            return form is "Wave" or "WaveRise" or "WaveWard" or "WaveFinal" ? form : null;
        }

        static string ReferencePath(string form, string motion)
        {
            if (form == null) return null;
            if (motion is not ("Idle" or "Move" or "Backstep" or "Windup" or "Guard" or "FollowThrough"
                or "BasicDowncut" or "CastWindup" or "CastDowncut" or "ShieldRaise" or "GroundLift"
                or "SW02Downcut" or "SW02HighCharge" or "GroundReverse" or "GroundPlunge" or "SWBasic" or "SWBasicRanged" or "SW01" or "SW02" or "SW03"
                or "SW04" or "SW05")) return null;
            return "Assets/Arts/Reference/" + form + "_Idle.png";
        }

        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            string form = SwordForm();
            if (form == null) return;
            string motion = Path.GetFileNameWithoutExtension(assetPath);
            string path = ReferencePath(form, motion);
            if (path != null) context.DependsOnSourceAsset(path);
            // The thin steel highlight falls below one pixel at the gameplay camera scale.
            if (motion is "Idle" or "Move" or "Backstep" or "Guard" or "Windup" or "FollowThrough"
                or "BasicDowncut" or "SW01Cast" or "ShieldRaise" or "GroundPlunge")
            {
                var importer = (TextureImporter)assetImporter;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
            }
        }

        void OnPostprocessTexture(Texture2D texture)
        {
            string path = ReferencePath(SwordForm(), Path.GetFileNameWithoutExtension(assetPath));
            var importer = (TextureImporter)assetImporter;
            bool softenWood = importer.userData == "WaveInheritedWood";
            if (path == null && !softenWood) return;
            var pixels = texture.GetPixels32();
            var target = path == null ? null : ReferenceSwatches(path);
            if (target != null && importer.spriteImportMode == SpriteImportMode.Single)
                GradeFrame(pixels, texture.width, new RectInt(0, 0, texture.width, texture.height), target);
            else if (target != null)
            {
                // The blade has its own slice on the basic sheet. Grade only body drawings.
                var factory = new SpriteDataProviderFactories();
                factory.Init();
                var data = factory.GetSpriteEditorDataProviderFromObject(importer);
                data.InitSpriteEditorDataProvider();
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                foreach (var sprite in data.GetSpriteRects())
                {
                    if (sprite.name == "BasicBlade") continue;
                    Rect rect = sprite.rect;
                    int left = Mathf.RoundToInt(rect.xMin * texture.width / width);
                    int bottom = Mathf.RoundToInt(rect.yMin * texture.height / height);
                    int right = Mathf.RoundToInt(rect.xMax * texture.width / width);
                    int top = Mathf.RoundToInt(rect.yMax * texture.height / height);
                    GradeFrame(pixels, texture.width, new RectInt(left, bottom, right - left, top - bottom), target);
                }
            }
            if (softenWood) SoftenInheritedWood(pixels);
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: texture.mipmapCount > 1, makeNoLongerReadable: false);
        }

        static void SoftenInheritedWood(Color32[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                if (p.a == 0 || FabricOrMetal(p, out int strength) != 0 || strength <= 0) continue;
                // Preserve the grain and luminance; ease the yellow only on the warm wood.
                int luminance = (54 * p.r + 183 * p.g + 19 * p.b + 128) >> 8;
                int weight = Math.Min(4, strength);
                p.r = Blend(p.r, luminance, weight);
                p.g = Blend(p.g, luminance, weight);
                p.b = Blend(p.b, luminance, weight);
                pixels[i] = p;
            }
        }

        static Swatches ReferenceSwatches(string path)
        {
            var stamp = File.GetLastWriteTimeUtc(path);
            if (references.TryGetValue(path, out var cached) && cached.stamp == stamp) return cached.palette;
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(image, File.ReadAllBytes(path)))
                    throw new InvalidDataException("Cannot read SwordWave reference " + path);
                var palette = new Swatches(image.GetPixels32(), image.width, new RectInt(0, 0, image.width, image.height));
                references[path] = (stamp, palette);
                return palette;
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        // Gold metal, indigo cloth and teal streamers. Leave neutral steel
        // and ink alone. Feather near-neutral edges into the original colour.
        static int FabricOrMetal(Color32 p, out int strength)
        {
            int gold = Math.Min(p.r - p.g, p.g - p.b);
            int indigo = Math.Min(p.b - p.g, p.b - p.r);
            int teal = Math.Min(p.g - p.r, p.b - p.r) - Math.Max(0, p.b - p.g);
            strength = Math.Max(gold, Math.Max(indigo, teal));
            return strength == gold ? 0 : strength == indigo ? 1 : 2;
        }

        sealed class Swatches
        {
            // Three channel values at the shadow, midtone and highlight of each material.
            public readonly int[,,] levels = new int[3, 3, 3];
            public readonly int[] count = new int[3];

            public Swatches(Color32[] pixels, int width, RectInt rect)
            {
                var bins = new int[3, 3, 256];
                for (int y = rect.yMin; y < rect.yMax; y += 2)
                for (int x = rect.xMin; x < rect.xMax; x += 2)
                {
                    var p = pixels[y * width + x];
                    if (p.a < 224) continue;
                    int group = FabricOrMetal(p, out int strength);
                    if (strength < 8) continue;
                    count[group]++;
                    bins[group, 0, p.r]++;
                    bins[group, 1, p.g]++;
                    bins[group, 2, p.b]++;
                }
                for (int group = 0; group < 3; group++)
                for (int channel = 0; channel < 3; channel++)
                {
                    int sum = 0, key = 0;
                    for (int value = 0; value < 256 && key < 3; value++)
                    {
                        sum += bins[group, channel, value];
                        while (key < 3 && sum * 100 >= count[group] * (20 + 30 * key))
                            levels[group, channel, key++] = value;
                    }
                }
            }
        }

        static void GradeFrame(Color32[] pixels, int width, RectInt rect, Swatches target)
        {
            var source = new Swatches(pixels, width, rect);
            var curves = new byte[3, 3, 256];
            for (int group = 0; group < 3; group++)
            for (int channel = 0; channel < 3; channel++)
            {
                int left = 0, start = 0;
                for (int key = 0; key <= 3; key++)
                {
                    int right = key == 3 ? 255 : source.levels[group, channel, key];
                    int end = key == 3 ? 255 : target.levels[group, channel, key];
                    if (right <= left) continue;
                    for (int value = left; value <= right; value++)
                        curves[group, channel, value] = (byte)(start + (end - start) * (value - left) / (right - left));
                    left = right;
                    start = end;
                }
            }
            for (int y = rect.yMin; y < rect.yMax; y++)
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                int index = y * width + x;
                var p = pixels[index];
                if (p.a == 0) continue;
                int group = FabricOrMetal(p, out int strength);
                if (strength <= 0 || source.count[group] < 64 || target.count[group] < 64) continue;
                int weight = Math.Min(12, strength);
                p.r = Blend(p.r, curves[group, 0, p.r], weight);
                p.g = Blend(p.g, curves[group, 1, p.g], weight);
                p.b = Blend(p.b, curves[group, 2, p.b], weight);
                pixels[index] = p;
            }
        }

        static byte Blend(int original, int corrected, int weight)
            => (byte)((original * (12 - weight) + corrected * weight + 6) / 12);
    }
}
