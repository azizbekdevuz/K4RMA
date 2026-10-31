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
        public static void LockPop(Transform parent, Vector2 at)
        {
            if (parent == null) return;
            var ring = KarmaArtwork.Sprite("GuardianVFX", "GroundRing");
            if (ring != null)
                KarmaCombatVFX.Burst(parent, at, ring, "상승베기 고정", KarmaSpace.Units(new Vector2(3.6f, 1.2f)), 0.28f, 0f, new Color(1f, 0.38f, 0.05f));
            var flash = KarmaVisuals.Box(parent, "고정 섬광", at + Vector2.up * KarmaSpace.Units(1.1f),
                KarmaSpace.Units(new Vector2(1.7f, 3.1f)), new Color(1f, 0.78f, 0.28f, 0.55f), 28);
            flash.gameObject.AddComponent<KarmaEffect>().Initialize(0.1f);
        }
        public static void Landing(KarmaGame game, Transform parent, Vector2 at, bool shield)
        {
            if (parent == null) return;
            var flash = KarmaVisuals.Box(parent, "착지 섬광", at + Vector2.up * KarmaSpace.Units(0.35f),
                KarmaSpace.Units(new Vector2(7.2f, 2.2f)), new Color(1f, 0.95f, 0.82f, 0.62f), 32);
            flash.gameObject.AddComponent<KarmaEffect>().Initialize(0.08f);
            Color tint = shield ? KarmaArtwork.BarrierColor : new Color(1f, 0.58f, 0.14f);
            KarmaCombatVFX.SpawnSprite(parent, at, "HitVFX", "Burst", KarmaSpace.Units(new Vector2(3.2f, 1.45f)), 0.3f);
            KarmaCombatVFX.SpawnSprite(parent, at + Vector2.left * KarmaSpace.Units(1.2f), "HitVFX", "Spark", KarmaSpace.Units(new Vector2(1.5f, 1f)), 0.26f);
            KarmaCombatVFX.SpawnSprite(parent, at + Vector2.right * KarmaSpace.Units(1.2f), "HitVFX", "Spark", KarmaSpace.Units(new Vector2(1.5f, 1f)), 0.26f);
            var dust = KarmaArtwork.Sprite("GuardianVFX", "Landing");
            if (dust != null)
                KarmaCombatVFX.Burst(parent, at, dust, "Landing Impact", KarmaSpace.Units(new Vector2(4.2f, 1.5f)), 0.34f, 0f, tint);
            if (game != null) game.PresentationShake(0.12f, 0.7f);
        }
        public static void Stride(Transform parent, Vector2 at, float direction)
        {
            var sprite = KarmaRisingArt.Get("SlashEcho");
            if (parent == null || sprite == null) return;
            var sweep = KarmaCombatVFX.Burst(parent, at + Vector2.up * KarmaSpace.Units(0.85f), sprite,
                "상승 접근", KarmaSpace.Units(new Vector2(2.4f, 0.85f)), 0.16f, 0f, new Color(1f, 0.72f, 0.28f));
            if (sweep != null) sweep.transform.parent.localScale = new Vector3(direction < 0 ? -1 : 1, 1, 1);
        }
        public static void AreaImpact(KarmaGame game, Transform parent, Vector2 at, Vector2 size)
        {
            if (parent == null) return;
            var sprite = KarmaRisingArt.Get("Impact");
            if (sprite != null)
                KarmaCombatVFX.Burst(parent, at, sprite, "착지 범위 충격", size, 0.42f, 0f, new Color(1f, 0.62f, 0.16f));
            if (game != null) game.PresentationShake(0.14f, 0.75f);
        }
        public static void ShieldPlant(Transform parent, Vector2 at, Vector2 size)
        {
            var sprite = KarmaRisingArt.Get("Shield");
            if (parent == null || sprite == null) return;
            KarmaCombatVFX.Burst(parent, at + Vector2.up * KarmaSpace.Units(1.15f), sprite,
                "계승 방패", size, 0.34f, 0f, KarmaArtwork.BarrierColor);
        }
        public static void ParryBreak(Transform parent, Vector2 at)
        {
            var sprite = KarmaRisingArt.Get("Parry");
            if (parent == null || sprite == null) return;
            KarmaCombatVFX.Burst(parent, at, sprite, "파편 패링", KarmaSpace.Units(new Vector2(3.4f, 3.4f)), 0.36f, 0f, new Color(1f, 0.9f, 0.55f));
        }
        public static void DressShot(SpriteRenderer visual, bool shield)
        {
            if (visual == null || visual.sprite == null) return;
            var echo = new GameObject(shield ? "파편 잔광" : "충격파 잔광");
            echo.transform.SetParent(visual.transform, false);
            var glow = echo.AddComponent<SpriteRenderer>();
            glow.sprite = visual.sprite;
            glow.flipX = visual.flipX;
            glow.flipY = visual.flipY;
            glow.sortingOrder = visual.sortingOrder - 1;
            var tint = shield ? KarmaArtwork.BarrierColor : new Color(1f, 0.48f, 0.08f, 1f);
            tint.a = 0.85f;
            glow.color = tint;
            if (KarmaCombatVFX.GlowMaterial != null) glow.sharedMaterial = KarmaCombatVFX.GlowMaterial;
            else KarmaVisuals.ApplyMaterial(glow);
            echo.transform.localScale = new Vector3(1.16f, 1.28f, 1f);
        }
    }

    public sealed class KarmaRisingCharge : MonoBehaviour
    {
        Transform follow;
        SpriteRenderer core, glow, ring;
        KarmaRisingCue cue;
        float punch;

        public static KarmaRisingCharge Begin(Transform parent, Transform follow, KarmaRisingCue cue)
        {
            if (parent == null || follow == null) return null;
            var go = new GameObject(cue == KarmaRisingCue.Tracking ? "상승베기 추적 준비" : cue == KarmaRisingCue.Locked ? "상승베기 고정 준비" : "상승베기 준비");
            go.transform.SetParent(parent, false);
            var charge = go.AddComponent<KarmaRisingCharge>();
            charge.follow = follow;
            charge.cue = cue;
            charge.punch = 0.14f;
            charge.core = charge.Make("기둥", "GuardianVFX", "Rising", KarmaSpace.Units(new Vector2(2.4f, 4.1f)), 16);
            charge.glow = charge.Make("빛", "GuardianVFX", "Rising", KarmaSpace.Units(new Vector2(2.9f, 4.6f)), 15);
            charge.ring = charge.Make("발밑", "GuardianVFX", "GroundRing", KarmaSpace.Units(new Vector2(2.8f, 0.95f)), 14);
            if (KarmaCombatVFX.GlowMaterial != null && charge.glow != null) charge.glow.sharedMaterial = KarmaCombatVFX.GlowMaterial;
            return charge;
        }
        public void Lock()
        {
            cue = KarmaRisingCue.Locked;
            punch = 0.16f;
        }
        public void End()
        {
            if (this != null) Destroy(gameObject);
        }
        SpriteRenderer Make(string name, string sheet, string pose, Vector2 size, int order)
        {
            var sprite = KarmaVisuals.Box(transform, name, Vector2.zero, Vector2.one, Color.white, order);
            if (!KarmaCombatVFX.ApplySprite(sprite, sheet, pose, size, true))
                sprite.transform.localScale = new Vector3(size.x, size.y, 1f);
            return sprite;
        }
        void LateUpdate()
        {
            if (follow == null) { Destroy(gameObject); return; }
            float lift = KarmaSpace.Units(1.05f);
            transform.position = follow.position + Vector3.up * lift;
            if (ring != null) ring.transform.localPosition = Vector3.down * (lift + KarmaSpace.Units(0.2f));
            float speed = cue == KarmaRisingCue.Locked ? 28f : cue == KarmaRisingCue.Charging ? 14f : 7f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * speed);
            Color tint = cue == KarmaRisingCue.Locked
                ? new Color(1f, 0.36f, 0.05f, 1f)
                : cue == KarmaRisingCue.Charging
                    ? new Color(1f, 0.72f, 0.22f, 1f)
                    : new Color(0.25f, 0.9f, 1f, 1f);
            float alpha = cue == KarmaRisingCue.Tracking ? 0.42f + pulse * 0.28f : 0.72f + pulse * 0.28f;
            if (core != null) core.color = new Color(1f, 1f, 1f, alpha);
            if (glow != null)
            {
                var light = tint;
                light.a = alpha;
                glow.color = light;
            }
            if (ring != null)
            {
                var mark = tint;
                mark.a = alpha * 0.9f;
                ring.color = mark;
            }
            float pop = punch > 0f ? 1f + Mathf.Clamp01(punch / 0.16f) * 0.28f : 1f;
            if (punch > 0f) punch -= Time.deltaTime;
            transform.localScale = Vector3.one * (pop * (cue == KarmaRisingCue.Locked ? 1.08f : 1f));
        }
    }
}
