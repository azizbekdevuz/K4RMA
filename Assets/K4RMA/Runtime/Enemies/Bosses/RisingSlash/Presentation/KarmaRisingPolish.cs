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
        public static void PursuitStreak(Transform parent, Vector2 at, float direction)
        {
            KarmaRisingClip.Play(parent, at + Vector2.up * KarmaSpace.Units(1.05f), KarmaSpace.Units(new Vector2(4.8f, 1.7f)), direction, KarmaRisingClip.Rush, 0.48f);
        }
        public static void MeleeTell(Transform parent, Vector2 at, float direction)
        {
            KarmaRisingClip.Play(parent, at, KarmaSpace.Units(new Vector2(2.8f, 2.1f)), direction, KarmaRisingClip.Warn, 0.36f, 47);
        }
        public static void SkyImpact(Transform parent, Vector2 at)
        {
            KarmaRisingClip.Play(parent, at, KarmaSpace.Units(new Vector2(6.4f, 1.8f)), 1f, KarmaRisingClip.Sky, 0.5f, 44);
        }
        public static KarmaRisingClip SkyBeacon(Transform parent, Vector2 at)
        {
            return KarmaRisingClip.Play(parent, at, KarmaSpace.Units(new Vector2(3.4f, 1.1f)), 1f, new[] { "Sky0", "Sky1", "Sky2" }, 0.7f, 43);
        }
        public static void AegisBreak(Transform parent, Vector2 at, float seconds)
        {
            KarmaRisingClip.Play(parent, at + Vector2.up * KarmaSpace.Units(1.15f), KarmaSpace.Units(new Vector2(2.8f, 2.8f)), 1f, KarmaRisingClip.Aegis, seconds, 45);
        }
        public static void BasicArc(Transform parent, Vector2 at, Vector2 size, float direction, string pose)
        {
            if (parent == null) return;
            string[] frames = pose == "SlashLow" ? KarmaRisingClip.Low : pose == "SlashFinish" ? KarmaRisingClip.Fin : KarmaRisingClip.High;
            float life = pose == "SlashFinish" ? 0.42f : 0.32f;
            KarmaRisingClip.Play(parent, at, size, direction, frames, life, 48);
            if (pose == "SlashFinish")
                KarmaRisingClip.Play(parent, at, KarmaSpace.Units(new Vector2(2.4f, 2.4f)), direction, KarmaRisingClip.Hit, 0.4f, 49);
        }
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

    }

}
