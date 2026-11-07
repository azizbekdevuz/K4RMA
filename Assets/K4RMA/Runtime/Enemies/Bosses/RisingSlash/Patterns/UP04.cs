using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class UP04Pattern : RisingBossPattern
        {
            public UP04Pattern(KarmaEnemy owner) : base(owner) { }
            public override IEnumerator Execute()
            {
                // Legacy practice ID previews the passive through the empowered landing attack.
                yield return new UP01Pattern(owner).Execute();
            }
            public void Burst(Vector2 landing)
            {
                Vector2 origin = landing + Vector2.up * KarmaSpace.Units(0.35f);
                float life = Mathf.Max(Settings.fragmentLifetime, 2f);
                Transform root = ((RisingBossBehavior)owner.behavior).FragmentsRoot;
                Vector2 shard = KarmaSpace.Units(new Vector2(1.05f, 0.675f));
                KarmaRisingPolish.ShieldPlant(root, landing, KarmaSpace.Units(new Vector2(5.4f, 4.8f)));
                if (owner.Active) KarmaRisingAudio.ShieldBurst();
                const int lowerCount = 9, upperCount = 5;
                for (int i = 0; i < lowerCount + upperCount && owner.Active; i++)
                {
                    // More lower-half sectors, with a fresh direction inside each sector per burst.
                    bool lower = i < lowerCount;
                    int sector = lower ? i : i - lowerCount;
                    float sectorWidth = 180f / (lower ? lowerCount : upperCount);
                    float sectorStart = (lower ? 180f : 0f) + sector * sectorWidth;
                    float degrees = sectorStart + Random.Range(sectorWidth * 0.15f, sectorWidth * 0.85f);
                    float rad = degrees * Mathf.Deg2Rad;
                    Vector2 launch = origin + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * KarmaSpace.Units(1.35f);
                    AimFragment(landing, launch, degrees, life, out _, out var velocity);
                    var shot = owner.Attack(launch, velocity, shard, life, Settings.fragmentDamageMultiplier,
                        new KarmaAttackContact(), 0f, visualPose: "Blade", parryStun: true, parent: root);
                    if (shot == null) continue;
                    shot.name = "방패 파편";
                    shot.Repaint(KarmaRisingArt.Get("Fragment" + (i % 8)), false);
                    if (shot.Visual != null)
                    {
                        shot.Visual.color = new Color(0.65f, 1f, 1f, 1f);
                        shot.Visual.sortingOrder = 35;
                    }
                }
            }
            void AimFragment(Vector2 center, Vector2 origin, float degrees, float life, out Vector2 end, out Vector2 velocity)
            {
                var dir = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
                dir.Normalize();
                // Cap every ray at the same 35-world-unit radius from the impact center.
                Vector2 offset = origin - center;
                float projection = Vector2.Dot(offset, dir);
                float radialTravel = Mathf.Max(0f, -projection + Mathf.Sqrt(Mathf.Max(0f,
                    projection * projection + 35f * 35f - offset.sqrMagnitude)));
                float limit = radialTravel;
                if (Mathf.Abs(dir.x) > 0.02f)
                {
                    float wall = dir.x > 0 ? KarmaStage.ArenaRight - 0.8f : KarmaStage.ArenaLeft + 0.8f;
                    limit = Mathf.Min(limit, (wall - origin.x) / dir.x);
                }
                if (dir.y > 0.02f) limit = Mathf.Min(limit, (KarmaSpace.Units(7.4f) - origin.y) / dir.y);
                else if (dir.y < -0.02f) limit = Mathf.Min(limit, (KarmaSpace.Units(0.45f) - origin.y) / dir.y);
                limit = Mathf.Clamp(limit, 0f, radialTravel);
                end = origin + dir * limit;
                velocity = dir * (limit / Mathf.Max(0.35f, life));
            }

        }
    }
}
