using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Rising-only frame player. It owns one object and never scales the shared effects root.
    public sealed class KarmaRisingClip : MonoBehaviour
    {
        public static readonly string[] Rush = { "Rush0", "Rush1", "Rush2", "Rush3", "Rush4", "Rush5" };
        public static readonly string[] Warn = { "Warn0", "Warn1", "Warn2", "Warn3" };
        public static readonly string[] High = { "High0", "High1", "High2", "High3" };
        public static readonly string[] Low = { "Low0", "Low1", "Low2", "Low3" };
        public static readonly string[] Fin = { "Fin0", "Fin1", "Fin2", "Fin3", "Fin4" };
        public static readonly string[] Hit = { "Hit0", "Hit1", "Hit2", "Hit3", "Hit4" };
        public static readonly string[] Sky = { "Sky0", "Sky1", "Sky2", "Sky3", "Sky4", "Sky5" };
        public static readonly string[] Aegis = { "Aegis0", "Aegis1", "Aegis2", "Aegis3", "Aegis4", "Aegis5" };
        static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

        SpriteRenderer picture;
        Sprite[] frames = System.Array.Empty<Sprite>();
        float duration = 0.4f;
        float age;
        int shown = -1;
        public int Frame { get { return shown < 0 ? 0 : shown; } }
        public int FrameCount { get { return frames.Length; } }

        public static KarmaRisingClip Play(Transform parent, Vector2 at, Vector2 worldSize, float direction, string[] poses, float seconds, int sorting = 46)
        {
            if (parent == null || poses == null || poses.Length == 0) return null;
            Sprite[] frames = Frames(poses);
            if (frames.Length == 0 || frames[0] == null) return null;
            var root = new GameObject("상승 연출");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = at;
            float face = direction < 0f ? -1f : 1f;
            var clip = root.AddComponent<KarmaRisingClip>();
            clip.picture = root.AddComponent<SpriteRenderer>();
            clip.picture.sortingOrder = sorting;
            KarmaVisuals.ApplyMaterial(clip.picture);
            clip.frames = frames;
            clip.duration = Mathf.Max(0.12f, seconds);
            clip.picture.sprite = frames[0];
            Vector2 bounds = frames[0].bounds.size;
            float sx = worldSize.x / Mathf.Max(0.01f, bounds.x);
            float sy = worldSize.y / Mathf.Max(0.01f, bounds.y);
            root.transform.localScale = new Vector3(face * sx, sy, 1f);
            clip.shown = 0;
            return clip;
        }

        public void Place(Vector2 at) { transform.localPosition = at; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || frames.Length == 0) return;
            age += dt;
            if (age >= duration) { Destroy(gameObject); return; }
            int frame = Mathf.Min(frames.Length - 1, (int)(age / duration * frames.Length));
            if (frame != shown && frames[frame] != null)
            {
                picture.sprite = frames[frame];
                shown = frame;
            }
            float fade = 1f - Mathf.Clamp01((age - duration * 0.78f) / (duration * 0.22f));
            picture.color = new Color(1f, 1f, 1f, fade);
        }

        static Sprite[] Frames(string[] poses)
        {
            string key = string.Join("|", poses);
            Sprite[] found;
            if (cache.TryGetValue(key, out found)) return found;
            var list = new List<Sprite>(poses.Length);
            for (int i = 0; i < poses.Length; i++)
            {
                Sprite sprite = KarmaRisingArt.Get(poses[i]);
                if (sprite != null) list.Add(sprite);
            }
            found = list.ToArray();
            cache[key] = found;
            return found;
        }
    }
}
