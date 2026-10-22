using System;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Native Unity assets are linked in the shared Inspector settings asset.
    public static class KarmaArtwork
    {
        public const string SettingsResourcePath = "KarmaArtworkSettings";
        static KarmaArtworkSettings settings;
        static readonly HashSet<string> failures = new HashSet<string>();
        public static KarmaArtworkSettings Settings
        {
            get
            {
                if (settings == null) settings = Resources.Load<KarmaArtworkSettings>(SettingsResourcePath);
                if (settings == null) throw new InvalidOperationException(
                    "이미지 설정이 없습니다: Assets/K4RMA/Resources/KarmaArtworkSettings.asset");
                return settings;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            // Imported textures and sprites belong to Unity, not this runtime cache.
            settings = null;
            failures.Clear();
        }
        public static Texture2D Texture(string name)
        {
            var texture = Settings.Texture(name);
            if (texture == null && failures.Add(name))
                Debug.LogError("이미지 슬롯이 비어 있습니다: " + name + " / KarmaArtworkSettings를 확인하세요.");
            return texture;
        }
        public static Rect UV(Texture2D texture, Rect topLeft)
        {
            return new Rect(topLeft.x / texture.width, 1 - (topLeft.y + topLeft.height) / texture.height,
                topLeft.width / texture.width, topLeft.height / texture.height);
        }
        public static Sprite[] PlayerFrames()
        {
            var frames = Settings.PlayerFrames();
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] == null) throw new InvalidOperationException(
                    "플레이어 동작 슬롯이 비어 있습니다: " + i + " / KarmaArtworkSettings를 확인하세요.");
            return frames;
        }
        public static Transform Player(Transform parent)
        {
            var frames = PlayerFrames();
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
            var sprite = Settings.Guardian(KarmaPatternCatalog.Primary(order));
            if (sprite == null) throw new InvalidOperationException("수호자 Sprite 슬롯이 비어 있습니다. KarmaArtworkSettings를 확인하세요.");
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
            var sprite = Settings.dojoBackground;
            if (sprite == null) throw new InvalidOperationException("도장 배경 슬롯이 비어 있습니다. KarmaArtworkSettings를 확인하세요.");
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
