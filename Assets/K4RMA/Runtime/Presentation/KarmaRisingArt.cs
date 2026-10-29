using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Illustrated Rising Slash effects. Fragments are shards of Shield.
    public static class KarmaRisingArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, string> fallback = new Dictionary<string, string>
        {
            { "Slash", "GuardianVFX/AirSlash" },
            { "SlashEcho", "GuardianVFX/AirSlash" },
            { "Impact", "GuardianVFX/GroundRing" },
            { "Wave", "GuardianVFX/Wave" },
            { "WaveEcho", "GuardianVFX/Wave" },
            { "Shield", "GuardianVFX/Ward" },
            { "Charge", "GuardianVFX/Rising" },
            { "Parry", "HitVFX/Burst" }
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { cache.Clear(); }

        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (cache.TryGetValue(name, out var cached) && cached != null) return cached;
            var sprite = Load(name);
            if (sprite == null && name.StartsWith("Fragment")) sprite = Load("Shield");
            if (sprite == null && fallback.TryGetValue(name, out var path))
            {
                int split = path.IndexOf('/');
                sprite = KarmaArtwork.Sprite(path.Substring(0, split), path.Substring(split + 1));
            }
            if (sprite == null && name.StartsWith("Fragment")) sprite = Get("Slash");
            if (sprite != null) cache[name] = sprite;
            return sprite;
        }

        static Sprite Load(string name)
        {
            string path = "K4RMAArtwork/RisingSlash/" + name;
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null || !texture.isReadable) return null;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
