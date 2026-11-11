using System;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Native Unity assets are linked in the shared Inspector settings asset.
    public static class KarmaArtwork
    {
        public const string SettingsResourcePath = "KarmaArtworkSettings";
        // Enlarge the foot-pivoted artwork for the full-arena camera view.
        const float CharacterPictureScale = KarmaSpace.Scale;
        static KarmaArtworkSettings settings;
        static KarmaSpriteLibrary[] bossLibraries;
        static KarmaSpriteLibrary[] BossLibraries
        {
            get
            {
                if (bossLibraries == null)
                {
                    bossLibraries = Resources.LoadAll<KarmaSpriteLibrary>("BossArtwork");
                    Array.Sort(bossLibraries, (a, b) => string.CompareOrdinal(a.name, b.name));
                }
                return bossLibraries;
            }
        }
        static readonly HashSet<string> failures = new HashSet<string>();
        public static KarmaArtworkSettings Settings
        {
            get
            {
                if (settings == null) settings = Resources.Load<KarmaArtworkSettings>(SettingsResourcePath);
                if (settings == null) throw new InvalidOperationException(
                    "이미지 설정이 없습니다: Assets/Data/Resources/KarmaArtworkSettings.asset");
                return settings;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            // Imported textures and sprites belong to Unity, not this runtime cache.
            settings = null; bossLibraries = null;
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
        public static Sprite Sprite(string sheet, string pose)
        {
            var common = Settings.spriteLibrary != null ? Settings.spriteLibrary.Get(sheet, pose) : null;
            if (common != null) return common;
            foreach (var library in BossLibraries)
            {
                var sprite = library.Get(sheet, pose);
                if (sprite != null) return sprite;
            }
            return null;
        }
        public static Sprite SkillIcon(Essence essence)
        {
            var icons = Settings.skillIcons;
            int index = (int)essence;
            return icons != null && index >= 0 && index < icons.Length ? icons[index] : null;
        }
        public static Sprite[] Sprites(string sheet)
        {
            var common = Settings.spriteLibrary != null ? Settings.spriteLibrary.All(sheet) : Array.Empty<Sprite>();
            // These character sheets are wholly owned by the shared library.
            if (sheet == "Player" || sheet == "Neutral") return common;
            var sprites = new List<Sprite>();
            foreach (var library in BossLibraries) sprites.AddRange(library.All(sheet));
            sprites.AddRange(common);
            return sprites.ToArray();
        }
        public static Sprite[] InheritedPoses(string form)
        {
            // Base forms share the Forms folder but are not inherited pose sets.
            if (form == "Neutral" || form == "Ward" || form == "Wave" || form == "Rise")
                return Array.Empty<Sprite>();
            var folder = GuardianFolder(form);
            return folder == null ? Array.Empty<Sprite>() : Resources.LoadAll<Sprite>(folder + "/Forms/" + form);
        }
        internal static Sprite[] RevivalPoses(string form)
        {
            var folder = GuardianFolder(form);
            return folder == null ? Array.Empty<Sprite>() : Resources.LoadAll<Sprite>(folder + "/Revival/" + form);
        }
        static string GuardianFolder(string form)
        {
            if (string.IsNullOrEmpty(form)) return null;
            if (form == "Neutral") return "Neutral";
            if (form.StartsWith("Ward", StringComparison.Ordinal)) return "Guard";
            if (form.StartsWith("Wave", StringComparison.Ordinal)) return "SwordWave";
            if (form.StartsWith("Rise", StringComparison.Ordinal)) return "RisingSlash";
            return null;
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
            picture.transform.localPosition = new Vector3(0, -KarmaSpace.PlayerHalfHeight, 0);
            picture.transform.localScale = Vector3.one * CharacterPictureScale;
            var renderer = picture.AddComponent<SpriteRenderer>(); renderer.sortingOrder = 5; KarmaVisuals.ApplyMaterial(renderer);
            var animator = root.gameObject.AddComponent<KarmaCharacterAnimator>();
            animator.ConfigureSprites(renderer, frames);
            animator.ConfigureSheet("Player");
            return root;
        }
        public static Transform Guardian(Transform parent, IReadOnlyList<Essence> order, bool final)
        {
            var sprite = Settings.Guardian(KarmaPatternCatalog.Primary(order));
            if (sprite == null) throw new InvalidOperationException("수호자 Sprite 슬롯이 비어 있습니다. KarmaArtworkSettings를 확인하세요.");
            var root = new GameObject("Artwork").transform; root.SetParent(parent, false);
            var picture = new GameObject("Guardian sprite"); picture.transform.SetParent(root, false);
            picture.transform.localPosition = new Vector3(0, -KarmaSpace.GuardianGroundY / KarmaSpace.GuardianArtScale, 0);
            picture.transform.localScale = Vector3.one * CharacterPictureScale;
            var renderer = picture.AddComponent<SpriteRenderer>(); renderer.sortingOrder = 5; KarmaVisuals.ApplyMaterial(renderer);
            var animator = root.gameObject.AddComponent<KarmaCharacterAnimator>();
            animator.ConfigureSprites(renderer, new[] { sprite });
            var primary = KarmaPatternCatalog.Primary(order);
            animator.ConfigureSheet(!primary.HasValue ? "Neutral" : primary == Essence.SwordWave ? "Wave" : primary == Essence.RisingSlash ? "Rise" : "Ward");
            if (final) renderer.color = new Color(1, 0.88f, 0.73f);
            if (order.Count >= 2) animator.SetGuardianAppearance(GuardianPreview(order), GuardianFormName(order));
            return root;
        }
        public static readonly Color BarrierColor = new Color(1, 0.83f, 0.48f);
        public static string GuardianFormName(IReadOnlyList<Essence> order)
        {
            var primary = KarmaPatternCatalog.Primary(order);
            if (!primary.HasValue) return "Neutral";
            string name = primary == Essence.SwordWave ? "Wave" : primary == Essence.RisingSlash ? "Rise" : "Ward";
            if (order.Count == 3) name += "Final";
            else if (order.Count == 2)
                name += order[1] == Essence.SwordWave ? "Wave" : order[1] == Essence.RisingSlash ? "Rise" : "Ward";
            return name;
        }
        public static Sprite GuardianPreview(IReadOnlyList<Essence> order)
        {
            var primary = KarmaPatternCatalog.Primary(order);
            string name = GuardianFormName(order);
            return Sprite("Inheritance", name) ?? Settings.Guardian(primary);
        }
        public static bool Background(Transform parent, int stage = 1, KarmaGame game = null)
        {
            // The first stage keeps its mountain dojo. Later stages keep the existing trial hall.
            bool mountain = stage == 0 && Settings.mountainBackground != null;
            var original = mountain ? Settings.mountainBackground : Settings.dojoBackground;
            var sprite = mountain
                ? (Settings.mountainPanorama != null ? Settings.mountainPanorama : original)
                : (Settings.dojoPanorama != null ? Settings.dojoPanorama : original);
            int coverage = sprite != original ? Mathf.Max(1, Settings.panoramaCoverage) : 1;
            if (sprite == null || original == null) throw new InvalidOperationException("전투장 배경 슬롯이 비어 있습니다.");
            float frameWidth = sprite != original ? KarmaStage.BackgroundWidth : 50;
            float frameHeight = 50 * original.rect.height / original.rect.width;
            // The extended mountain image keeps Background.png's original center pixels,
            // so its ground row stays at the original 0.74 vertical position.
            float floorFromTop = mountain ? 0.74f : 0.8f;
            float width = frameWidth * coverage, height = frameHeight * coverage;
            // Fill the camera's existing coverage using one scale, preserving the painted proportions.
            float scale = Mathf.Max(width / sprite.bounds.size.x, height / sprite.bounds.size.y);
            width = sprite.bounds.size.x * scale;
            height = sprite.bounds.size.y * scale;
            floorFromTop = (coverage - 1) / (2f * coverage) + floorFromTop / coverage;
            var center = new Vector3(20, 0, 0);
            var go = new GameObject(mountain ? "Mountain dojo artwork" : "Trial hall artwork");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center + new Vector3(
                (sprite.pivot.x / sprite.rect.width - 0.5f) * width,
                (sprite.pivot.y / sprite.rect.height - (1 - floorFromTop)) * height, 0);
            go.transform.localScale = new Vector3(scale, scale, 1);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sortingOrder = -20; KarmaVisuals.ApplyMaterial(renderer);
            KarmaMoonArtwork.Apply(renderer, mountain);
            if (game != null) game.FrameStageBackground(coverage > 1
                ? new Bounds(renderer.bounds.center, new Vector3(frameWidth, frameHeight, renderer.bounds.size.z))
                : renderer.bounds);
            return true;
        }
    }
}
