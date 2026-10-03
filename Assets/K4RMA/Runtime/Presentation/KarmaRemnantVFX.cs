using UnityEngine;

namespace KarmaPrototype
{
    // Visuals only: never owns damage, proc timing, or collision radius.
    // Textures are generated once; no external sprite/material setup required.
    public sealed class KarmaRemnantVFX : MonoBehaviour
    {
        static Sprite ring, crescent, ember;
        SpriteRenderer visual;
        Vector3 originalSize;
        Vector2 drift;
        float age, duration, expand, alpha;

        static Sprite Shape(int kind)
        {
            Sprite cached = kind == 0 ? ring : kind == 1 ? crescent : ember;
            if (cached != null) return cached;
            const int n = 96;
            var texture = new Texture2D(n, n);
            texture.name = "RemnantShape_" + kind;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n * 2 - 1;
                float r = Mathf.Sqrt(u * u + v * v);
                float a;
                if (kind == 0) a = Mathf.Clamp01((0.10f - Mathf.Abs(r - 0.82f)) * 35);
                else if (kind == 1)
                {
                    // A bright curved blade, opening toward the direction of travel.
                    a = Mathf.Clamp01((0.16f - Mathf.Abs(r - 0.78f)) * 25)
                        * Mathf.Clamp01((u + 0.1f) * 4);
                }
                else
                {
                    float width = 0.12f + (u + 1) * 0.3f;
                    a = Mathf.Clamp01((1 - Mathf.Abs(v) / width) * 3)
                        * Mathf.Clamp01((1 - r) * 10);
                }
                pixels[y * n + x] = new Color(1, 1, 1, a);
            }
            texture.SetPixels(pixels); texture.Apply();
            cached = Sprite.Create(texture, new Rect(0, 0, n, n), Vector2.one * 0.5f, n);
            if (kind == 0) ring = cached; else if (kind == 1) crescent = cached; else ember = cached;
            return cached;
        }
        static SpriteRenderer Draw(Transform parent, Vector2 p, Vector2 size, Color color, int kind)
        {
            var sr = KarmaVisuals.Box(parent, "Remnant VFX", p, size, color, 14);
            sr.sprite = Shape(kind);
            return sr;
        }
        static void Animate(SpriteRenderer sr, float seconds, float growth, Vector2 drift)
        {
            var fx = sr.gameObject.AddComponent<KarmaRemnantVFX>();
            fx.visual = sr; fx.duration = seconds; fx.expand = growth; fx.drift = drift;
            fx.originalSize = sr.transform.localScale; fx.alpha = sr.color.a;
        }
        public static SpriteRenderer CreateWardHalo(Transform player, Color color)
        {
            color.a = 0.35f;
            var sr = Draw(player, Vector2.zero, new Vector2(1.5f, 2.15f), color, 0);
            sr.sortingOrder = 2;
            return sr;
        }
        public static void ProjectileLook(SpriteRenderer sr, Essence essence, Vector2 velocity)
        {
            sr.sprite = Shape(essence == Essence.Dash ? 1 : 2);
            sr.sortingOrder = 14;
            sr.transform.localScale = essence == Essence.Dash ? new Vector3(1.25f, 2.3f, 1) : new Vector3(0.85f, 0.48f, 1);
            sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
        }
        public static void Trail(KarmaGame game, Vector2 p, Essence essence, Vector2 velocity)
        {
            Color color = game.Config.ColorOf(essence); color.a = 0.5f;
            var sr = Draw(game.World, p, essence == Essence.Dash ? new Vector2(1, 1.9f) : new Vector2(0.65f, 0.32f),
                color, essence == Essence.Dash ? 1 : 2);
            sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            Animate(sr, 0.22f, 0.3f, Vector2.zero);
        }
        public static void Proc(KarmaGame game, Vector2 p, int facing, Essence essence)
        {
            Color color = game.Config.ColorOf(essence);
            if (essence == Essence.Ward)
            {
                // Outline expands to the same radius as the existing instantaneous damage query.
                float diameter = game.Config.wardBurstRadius * 2 / 0.82f;
                var sr = Draw(game.World, p, Vector2.one * diameter, color, 0);
                Animate(sr, 0.65f, 1, Vector2.zero);
            }
            else
            {
                var sr = Draw(game.World, p + Vector2.right * facing * 0.8f,
                    essence == Essence.Flame ? new Vector2(2.2f, 2.2f) : new Vector2(1.8f, 2.8f), color, 1);
                sr.transform.localRotation = Quaternion.Euler(0, 0, facing > 0 ? 0 : 180);
                Animate(sr, 0.35f, 0.35f, Vector2.right * facing * 0.5f);
            }
        }
        public static void Impact(KarmaGame game, Vector2 p, Essence essence)
        {
            Color color = game.Config.ColorOf(essence);
            var sr = Draw(game.World, p, Vector2.one * (essence == Essence.Dash ? 2.2f : 1.35f), color, 0);
            Animate(sr, 0.4f, 1, Vector2.zero);
        }
        void Update()
        {
            age += Time.deltaTime;
            if (age >= duration) { Destroy(gameObject); return; }
            float t = Mathf.Clamp01(age / duration);
            // Hold opacity briefly before fading, rather than disappearing in one frame.
            float a = 1 - Mathf.Clamp01((t - 0.3f) / 0.7f);
            var c = visual.color; c.a = alpha * a; visual.color = c;
            transform.localScale = originalSize * Mathf.Lerp(1 - expand * 0.75f, 1, Mathf.Sqrt(t));
            transform.position += (Vector3)(drift * Time.deltaTime);
        }
    }
}
