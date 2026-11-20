using UnityEngine;

namespace KarmaPrototype
{
    public enum KarmaRisingCue
    {
        Tracking,
        Charging,
        Locked
    }

    // Presentation only. These objects never apply damage or change attack timing.
    public static class KarmaRisingPolish
    {
        public static void Stride(Transform parent, Vector2 at, float direction)
        {
            var sprite = KarmaRisingArt.Get("SlashEcho");
            if (parent == null || sprite == null) return;
            var sweep = KarmaCombatVFX.Burst(parent, at + Vector2.up * KarmaSpace.Units(0.12f), sprite,
                "상승 접근", KarmaSpace.Units(new Vector2(1.75f, 0.36f)), 0.14f, 0f, new Color(0.88f, 0.73f, 0.54f, 0.28f),
                restrained: true, emitSparks: false);
            if (sweep != null) sweep.transform.parent.localScale = new Vector3(direction < 0 ? -1 : 1, 1, 1);
        }
        public static SpriteRenderer ShowContact(KarmaBossAttack attack, Sprite sprite, float opacity = 1)
        {
            var visual = attack != null ? attack.Visual : null;
            if (visual == null || sprite == null) return null;
            attack.Repaint(sprite, true);
            visual.color = new Color(1, 1, 1, opacity);
            visual.gameObject.SetActive(true);
            return visual;
        }
        internal static SpriteRenderer ShieldBreakBeat(Transform parent, Vector2 center, float diameter)
        {
            var sprite = KarmaRisingArt.Get("Shield");
            if (sprite == null) return null;
            var shell = new GameObject("UP04 방패 붕괴").AddComponent<SpriteRenderer>();
            shell.transform.SetParent(parent, false);
            shell.transform.position = center;
            shell.transform.localScale = Vector3.one * (diameter / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
            shell.sprite = sprite;
            shell.color = new Color(1, 0.90f, 0.64f, 0.52f);
            shell.sortingOrder = 18;
            KarmaVisuals.ApplyMaterial(shell);
            return shell;
        }
        public static void ParryBreak(Transform parent, Vector2 at)
        {
            var sprite = KarmaRisingArt.Get("Parry") ?? KarmaArtwork.Sprite("HitVFX", "Cross");
            if (parent == null || sprite == null) return;
            KarmaCombatVFX.Burst(parent, at, sprite, "파편 패링", KarmaSpace.Units(new Vector2(3.4f, 3.4f)), 0.36f, 0f, new Color(1f, 0.9f, 0.55f));
        }

    }

}
