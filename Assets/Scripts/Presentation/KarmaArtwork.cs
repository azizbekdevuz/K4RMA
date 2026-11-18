using System;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Native Unity assets are linked in the shared Inspector settings asset.
    public static class KarmaArtwork
    {
        public const string SettingsResourcePath = "KarmaArtworkSettings";
        public const string StageArt = "Reference/GameScale_HD_StageStyle/";
        public static Sprite StageIdle(string form)
        {
            var sprite = Resources.Load<Sprite>(StageArt + form + "_Idle");
            if (sprite == null) throw new InvalidOperationException("Missing StageStyle Idle: " + form);
            return sprite;
        }
        // Enlarge the foot-pivoted artwork for the full-arena camera view.
        const float CharacterPictureScale = KarmaSpace.Scale;
        static KarmaArtworkSettings settings;
        const string SwordLibrary = "BossArtwork/KarmaSwordBossSprites";
        const string RisingLibrary = "BossArtwork/KarmaRisingBossSprites";
        const string GuardLibrary = "BossArtwork/KarmaGuardBossSprites";
        static readonly Dictionary<string, ResourceRequest> pendingLibraries = new Dictionary<string, ResourceRequest>();
        static readonly Dictionary<string, KarmaSpriteLibrary> loadedLibraries = new Dictionary<string, KarmaSpriteLibrary>();

        // These sheet IDs are owned by one library. Shared and extension sheets use the normal lookup below.
        static string EffectLibrary(string sheet)
        {
            if (sheet == "SwordBossVFX" || sheet == "BossStun") return SwordLibrary;
            if (sheet == "RisingBossVFX" || sheet == "RisingStunStars") return RisingLibrary;
            if (sheet != null && sheet.StartsWith("Guard", StringComparison.Ordinal) && sheet != "GuardianVFX") return GuardLibrary;
            return null;
        }
        internal static void PrepareLibrary(Essence? primary)
        {
            if (!primary.HasValue) return;
            string path = primary == Essence.Guard ? GuardLibrary : primary == Essence.RisingSlash ? RisingLibrary : SwordLibrary;
            if (!loadedLibraries.ContainsKey(path) && !pendingLibraries.ContainsKey(path))
                pendingLibraries.Add(path, Resources.LoadAsync<KarmaSpriteLibrary>(path));
        }
        static KarmaSpriteLibrary LoadLibrary(string path)
        {
            if (loadedLibraries.TryGetValue(path, out var library)) return library;
            library = pendingLibraries.TryGetValue(path, out var request) && request.isDone
                ? (KarmaSpriteLibrary)request.asset : Resources.Load<KarmaSpriteLibrary>(path);
            pendingLibraries.Remove(path);
            if (library == null) throw new InvalidOperationException("Missing boss sprite library " + path);
            return loadedLibraries[path] = library;
        }
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
            pendingLibraries.Clear(); loadedLibraries.Clear();
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
            var body = KarmaBodyPoses.Get(sheet, pose);
            if (body != null) return body;
            var common = Settings.spriteLibrary != null ? Settings.spriteLibrary.Get(sheet, pose) : null;
            if (common != null) return common;
            var path = EffectLibrary(sheet);
            if (path != null) return LoadLibrary(path).Get(sheet, pose);
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
            var body = KarmaBodyPoses.All(sheet);
            if (body.Length > 0 && sheet != "Inheritance") return body;
            var common = Settings.spriteLibrary != null ? Settings.spriteLibrary.All(sheet) : Array.Empty<Sprite>();
            // These character sheets are wholly owned by the shared library.
            if (sheet == "Player" || sheet == "Neutral") return common;
            var sprites = new List<Sprite>();
            var path = EffectLibrary(sheet);
            if (path != null) sprites.AddRange(LoadLibrary(path).All(sheet));
            else foreach (var library in BossLibraries) sprites.AddRange(library.All(sheet));
            sprites.AddRange(common);
            return sprites.ToArray();
        }
        static readonly string[] WavePoseNames = {
            "Idle", "Side", "Back", "Move", "Windup", "Slash", "FollowThrough", "Wave",
            "Guard", "Rising", "Hurt", "Death", "RevivePush", "ReviveKneel", "ReviveRise", "ReviveStand"
        };

        static readonly string[] RisePoseNames = {
            "Idle", "Side", "Back", "Dash", "Windup", "Rising", "AirSlash", "Fall",
            "Landing", "Slash", "Hurt", "Death", "RevivePush", "ReviveKneel", "ReviveRise", "ReviveStand"
        };
        static readonly string[] WardPoseNames = {
            "Idle", "Side", "Back", "Guard", "Move", "ShieldBash", "Hurt", "Thrust",
            "Crouch", "Windup", "Recover", "Death", "RevivePush", "ReviveKneel", "ReviveRise", "ReviveStand"
        };
        public static Sprite[] InheritedPoses(string form)
        {
            // Base forms share the Forms folder but are not inherited pose sets.
            if (form == "Neutral" || form == "Ward" || form == "Wave" || form == "Rise")
                return Array.Empty<Sprite>();
            var folder = GuardianFolder(form);
            if (folder == null) return Array.Empty<Sprite>();
            var drawings = KarmaBodyPoses.All("@" + folder + "/Forms/" + form);
            if (drawings.Length == 0) return drawings;
            var names = folder == "Guard" ? WardPoseNames : folder == "RisingSlash" ? RisePoseNames : WavePoseNames;
            var poses = new Sprite[names.Length];
            for (int i = 0; i < poses.Length; i++)
            {
                string name = names[i];
                poses[i] = Array.Find(drawings, sprite => sprite.name == name);
                if (poses[i] == null) throw new InvalidOperationException(form + " missing inherited pose " + name);
            }
            return poses;
        }
        internal static Sprite[] RevivalPoses(string form)
        {
            var folder = GuardianFolder(form);
            return folder == null ? Array.Empty<Sprite>() : KarmaBodyPoses.All("@" + folder + "/Revival/" + form);
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
            return Settings.PlayerFrames();
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
            var primary = KarmaPatternCatalog.Primary(order);
            var sprite = order.Count == 0 ? Sprite("Neutral", "Idle") : Settings.Guardian(primary);
            if (sprite == null) throw new InvalidOperationException("수호자 Sprite 슬롯이 비어 있습니다. KarmaArtworkSettings를 확인하세요.");
            var root = new GameObject("Artwork").transform; root.SetParent(parent, false);
            var picture = new GameObject("Guardian sprite"); picture.transform.SetParent(root, false);
            picture.transform.localPosition = new Vector3(0, -KarmaSpace.GuardianGroundY / KarmaSpace.GuardianArtScale, 0);
            picture.transform.localScale = Vector3.one * CharacterPictureScale;
            var renderer = picture.AddComponent<SpriteRenderer>(); renderer.sortingOrder = 5; KarmaVisuals.ApplyMaterial(renderer);
            var animator = root.gameObject.AddComponent<KarmaCharacterAnimator>();
            animator.ConfigureSprites(renderer, new[] { sprite });
            animator.ConfigureSheet(!primary.HasValue ? "Neutral" : primary == Essence.SwordWave ? "Wave" : primary == Essence.RisingSlash ? "Rise" : "Ward");
            if (final) renderer.color = new Color(1, 0.88f, 0.73f);
            string form = GuardianFormName(order);
            if (order.Count >= 2) animator.SetGuardianAppearance(Sprite("Inheritance", form) ?? sprite, form);
            animator.ConfigureGuardianIdle(form);
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
            return StageIdle(GuardianFormName(order));
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
                : renderer.bounds, renderer.bounds);
            return true;
        }
    }
}
