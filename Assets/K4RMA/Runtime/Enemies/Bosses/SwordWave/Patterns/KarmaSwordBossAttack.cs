using UnityEngine;
namespace KarmaPrototype
{
    // Sword-only artwork and flight limits; common damage/contact handling stays in KarmaBossAttack.
    public sealed class KarmaSwordBossAttack : KarmaBossAttack
    {
        bool oriented, aimedSlash, aerialSword, scar;
        float aimedTravel;
        protected override bool Oriented { get { return oriented; } }
        protected override bool AttachTrail { get { return !aerialSword && !aimedSlash && !scar; } }
        protected override bool AttachLaunch { get { return !aerialSword && !aimedSlash; } }
        protected override bool ApplyPatternVisual(SpriteRenderer renderer, string extraPose, Vector2 size, Vector2 velocity)
        {
            aerialSword = extraPose == "SW05_AerialSword";
            aimedSlash = extraPose == "SW02_AimedSlash";
            scar = extraPose == "SW03_RisingBlade_v5";
            oriented = extraPose != null && velocity.sqrMagnitude > 0.01f;
            if (extraPose == null) return false;
            KarmaWaveArtwork.Apply(renderer, extraPose, size, velocity);
            return true;
        }
        public void SetSW03Frame(Vector2 center, Vector2 footprint)
        {
            if (!scar) return;
            transform.position = center; size = footprint;
        }
        protected override Color AttackColor(Color color, bool restrained)
        { return aerialSword ? Color.white : base.AttackColor(color, restrained); }
        protected override float FlightStep(Vector2 from, float dt)
        {
            if (!oriented) return dt;
            dt = KarmaBossProjectileGeometry.SwordShotLife(from, velocity, dt);
            if (aimedSlash) dt = KarmaBossProjectileGeometry.AimedSlashBoundaryLife(from, velocity, size, dt);
            if (aimedSlash && aimedTravel < 0.18f)
            {
                aimedTravel += dt;
                if (aimedTravel >= 0.18f)
                    KarmaAudio.Play(KarmaSfxId.SwWaveTravel, 0.4f, this);
            }
            if (aerialSword && velocity.y < 0)
                dt = Mathf.Min(dt, Mathf.Max(0, (from.y - (0.05f + size.x * 0.5f)) / -velocity.y));
            return dt;
        }
        protected override bool FinishFlight(Vector2 to)
        {
            if (!aerialSword || velocity.y >= 0 || to.y > 0.05f + size.x * 0.5f + 0.0001f) return false;
            var impact = KarmaArtwork.Sprite("SwordBossVFX", "SW03_GroundImpact");
            if (impact != null)
                KarmaCombatVFX.Burst(transform.parent, new Vector2(to.x, 0.9f), impact,
                    "SW05 Ground Impact / no damage", new Vector2(Mathf.Max(2.2f, size.y * 2), 1.7f),
                    0.24f, 0, new Color(0.55f, 0.85f, 1), true);
            var ring = KarmaArtwork.Sprite("GuardianVFX", "GroundRing");
            if (ring != null)
                KarmaCombatVFX.Burst(transform.parent, new Vector2(to.x, 0.12f), ring,
                    "SW05 Ground shock / no damage", new Vector2(Mathf.Max(2.8f, size.y * 2.6f), 0.8f),
                    0.18f, 0, new Color(0.7f, 0.92f, 1));
            return true;
        }
    }
}
