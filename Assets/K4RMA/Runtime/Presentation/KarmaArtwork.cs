using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Original sheets are preserved. Rectangles use top-left source coordinates;
    // runtime sprites/UVs do not modify Unity importer-owned sprite metadata.
    public static class KarmaArtwork
    {
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static byte[] bundle;
        static readonly Dictionary<string, byte[]> images = new Dictionary<string, byte[]>();
        static readonly HashSet<string> failures = new HashSet<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            foreach (var sprite in sprites.Values) if (sprite != null) UnityEngine.Object.Destroy(sprite);
            foreach (var texture in textures.Values) if (texture != null) UnityEngine.Object.Destroy(texture);
            sprites.Clear(); textures.Clear(); images.Clear(); failures.Clear(); bundle = null;
        }
        static bool LoadBundle()
        {
            if (bundle != null) return true;
            var asset = Resources.Load<TextAsset>("K4RMAArtwork/KarmaArtwork");
            if (asset == null) return false;
            byte[] data = asset.bytes;
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "K4RA" || reader.ReadInt32() != 1)
                    throw new InvalidDataException("K4RMA 이미지 묶음 형식이 올바르지 않습니다.");
                int count = reader.ReadInt32();
                if (count < 1 || count > 100) throw new InvalidDataException("K4RMA 이미지 수가 올바르지 않습니다.");
                var loaded = new Dictionary<string, byte[]>();
                for (int i = 0; i < count; i++)
                {
                    string name = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadUInt16()));
                    int length = reader.ReadInt32();
                    if (length < 1 || length > reader.BaseStream.Length - reader.BaseStream.Position)
                        throw new InvalidDataException("K4RMA 이미지 데이터가 잘렸습니다: " + name);
                    loaded.Add(name, reader.ReadBytes(length));
                }
                foreach (var pair in loaded) images.Add(pair.Key, pair.Value);
            }
            // Keep only individual PNG bytes, rather than retaining two copies of the bundle.
            bundle = new byte[0];
            return true;
        }
        public static Texture2D Texture(string name)
        {
            Texture2D value;
            if (textures.TryGetValue(name, out value) && value != null) return value;
            byte[] data;
            if (LoadBundle() && images.TryGetValue(name, out data))
            {
                value = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                value.name = "K4RMA_" + name;
                if (!ImageConversion.LoadImage(value, data, false))
                {
                    UnityEngine.Object.Destroy(value);
                    throw new InvalidDataException("K4RMA PNG 디코딩 실패: " + name);
                }
                value.filterMode = FilterMode.Bilinear; value.wrapMode = TextureWrapMode.Clamp;
                textures[name] = value;
                return value;
            }
            if (failures.Add(name)) Debug.LogError("K4RMA 이미지 누락: " + name +
                " / Assets/K4RMA/Resources/K4RMAArtwork/KarmaArtwork.bytes를 포함해 Assets/K4RMA 전체를 적용하세요.");
            return null;
        }
        public static Rect UV(Texture2D texture, Rect topLeft)
        {
            return new Rect(topLeft.x / texture.width, 1 - (topLeft.y + topLeft.height) / texture.height,
                topLeft.width / texture.width, topLeft.height / texture.height);
        }
        public static Sprite Slice(string textureName, string name, Rect topLeft, float pixelsPerUnit, Vector2? pivot = null)
        {
            Sprite result;
            string key = textureName + "/" + name;
            if (sprites.TryGetValue(key, out result)) return result;
            var texture = Texture(textureName);
            if (texture == null) return null;
            Rect rect = new Rect(topLeft.x, texture.height - topLeft.y - topLeft.height, topLeft.width, topLeft.height);
            result = Sprite.Create(texture, rect, pivot ?? new Vector2(0.5f, 0), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            result.name = name; sprites[key] = result; return result;
        }
        public static Sprite[] PlayerFrames()
        {
            var texture = Texture("PlayerAnimation");
            if (texture == null) return new[] { Slice("PlayerSheet", "Idle", new Rect(18, 16, 184, 352), 205) };
            float cell = texture.width / 3f;
            float[] feet = { 397f/418, 395f/418, 395f/418, 368f/418, 369f/418, 380f/418, 341f/418, 342f/418, 340f/418 };
            var result = new Sprite[9];
            for (int i = 0; i < result.Length; i++)
                result[i] = Slice("PlayerAnimation", "Pose" + i,
                    new Rect((i % 3) * cell, (i / 3) * cell, cell, cell), cell / 2.5f,
                    new Vector2(0.5f, 1 - feet[i]));
            return result;
        }
        public static Transform Player(Transform parent)
        {
            var frames = PlayerFrames(); if (frames[0] == null) throw new InvalidOperationException("플레이어 이미지가 없습니다. K4RMA 이미지 묶음을 확인하세요.");
            var root = new GameObject("Artwork").transform; root.SetParent(parent, false);
            var picture = new GameObject("Player sprite"); picture.transform.SetParent(root, false);
            picture.transform.localPosition = new Vector3(0, -0.775f, 0);
            var renderer = picture.AddComponent<SpriteRenderer>(); renderer.sortingOrder = 5; KarmaVisuals.ApplyMaterial(renderer);
            var animator = root.gameObject.AddComponent<KarmaCharacterAnimator>();
            animator.Initialize(); animator.ConfigureSprites(renderer, frames);
            return root;
        }
        public static Transform Guardian(Transform parent, IReadOnlyList<Essence> order, bool final)
        {
            // Keep the clean body cutouts; inherited effects are independent layers.
            var texture = Texture("GuardianSprites"); if (texture == null) throw new InvalidOperationException("수호자 이미지가 없습니다. K4RMA 이미지 묶음을 확인하세요.");
            var primary = KarmaPatternCatalog.Primary(order);
            int cell = primary.HasValue ? (int)primary.Value : 2;
            float width = texture.width / 3f;
            var sprite = Slice("GuardianSprites", "Guardian" + cell,
                new Rect(cell * width, 0, width, texture.height), texture.height / 2.05f);
            var root = new GameObject("Artwork").transform; root.SetParent(parent, false);
            var picture = new GameObject("Guardian sprite"); picture.transform.SetParent(root, false);
            picture.transform.localPosition = new Vector3(0, -0.89f, 0);
            var renderer = picture.AddComponent<SpriteRenderer>(); renderer.sortingOrder = 5; KarmaVisuals.ApplyMaterial(renderer);
            var animator = root.gameObject.AddComponent<KarmaCharacterAnimator>();
            animator.Initialize(); animator.ConfigureSprites(renderer, new[] { sprite });
            if (final) renderer.color = new Color(1, 0.88f, 0.73f);
            root.gameObject.AddComponent<KarmaGuardianAdornment>().Initialize(order);
            return root;
        }
        public static readonly Color BossSwordColor = new Color(0.42f, 0.68f, 1);
        public static readonly Color BarrierColor = new Color(1, 0.83f, 0.48f);
        public static string GuardianIllustration(Essence? type)
        {
            return type == Essence.Flame ? "SwordGuardian" : type == Essence.Dash ? "RisingGuardian" : "WardGuardian";
        }
        public static bool Background(Transform parent)
        {
            var texture = Texture("DojoBackground"); if (texture == null) return false;
            var sprite = Slice("DojoBackground", "Dojo", new Rect(0, 0, texture.width, texture.height), 100);
            // Source floor lies around 75% image height; align it to world ground y=0.
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Dojo artwork " + i); go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(-7 + i * 27, -3.8f, 0);
                go.transform.localScale = new Vector3(27 / sprite.bounds.size.x, 15.2f / sprite.bounds.size.y, 1);
                var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = -20; KarmaVisuals.ApplyMaterial(renderer);
            }
            return true;
        }
    }
}
