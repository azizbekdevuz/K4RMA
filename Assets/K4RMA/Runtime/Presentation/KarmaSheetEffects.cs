using UnityEngine;

namespace KarmaPrototype
{
    // Artwork and collision dimensions stay independent; this only changes renderers.
    public static class KarmaSheetEffects
    {
        public static bool Apply(SpriteRenderer renderer, string sheet, string pose, Vector2 size, bool preserveAspect = false)
        {
            var sprite = KarmaArtwork.Sprite(sheet, pose);
            if (sprite == null) return false;
            renderer.sprite = sprite;
            float x = size.x / sprite.bounds.size.x, y = size.y / sprite.bounds.size.y;
            if (preserveAspect) x = y = Mathf.Min(x, y);
            renderer.transform.localScale = new Vector3(x, y, 1);
            return true;
        }
        public static SpriteRenderer Spawn(Transform parent, Vector2 at, string sheet, string pose,
            Vector2 size, float seconds, float angle = 0)
        {
            var sprite = KarmaArtwork.Sprite(sheet, pose);
            if (sprite == null) return null;
            Color tint = sheet == "HitVFX" ? new Color(1, 0.75f, 0.32f)
                : sheet == "PersonalizationVFX" ? new Color(0.58f, 0.85f, 1)
                : new Color(0.35f, 0.7f, 1);
            return KarmaCombatVFX.Burst(parent, at, sprite, sheet + "/" + pose, size, seconds, angle, tint);
        }
    }
}
