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
                float life = Mathf.Max(Settings.fragmentLifetime, 0.35f);
                Transform root = ((RisingBossBehavior)owner.behavior).FragmentsRoot;
                Vector2 shard = KarmaSpace.Units(new Vector2(1.05f, 0.675f));
                KarmaRisingPolish.ShieldPlant(root, landing, KarmaSpace.Units(new Vector2(5.4f, 4.8f)));
                if (owner.Active) KarmaRisingAudio.ShieldBurst();
                const int upperCount = 6, sideCount = 8;
                for (int i = 0; i < upperCount + sideCount && owner.Active; i++)
                {
                    // No downward rays: retain the upper fan and distribute the rest to both flanks.
                    float degrees;
                    if (i < upperCount)
                    {
                        float sectorWidth = 140f / upperCount;
                        degrees = 20f + i * sectorWidth + Random.Range(sectorWidth * 0.15f, sectorWidth * 0.85f);
                    }
                    else
                    {
                        int side = i - upperCount;
                        float angle = 1f + (side / 2) * 4.5f + Random.Range(0.5f, 2.5f);
                        degrees = side % 2 == 0 ? angle : 180f - angle;
                    }
                    float rad = degrees * Mathf.Deg2Rad;
                    Vector2 launch = origin + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * KarmaSpace.Units(1.35f);
                    AimFragment(landing, launch, degrees, life, out _, out var velocity);
                    int fragment = Random.Range(0, 3);
                    // The contact suppresses the instant counter riposte; the shard itself returns.
                    var contact = new KarmaAttackContact(parryWithoutRiposte: true);
                    var shot = KarmaBossAttack.Spawn(owner.game, owner, root, launch, velocity, shard, life + 3f,
                        owner.Damage * Settings.fragmentDamageMultiplier, Color.white, contact, 0f,
                        visualPose: "Fragment" + fragment, visualSheet: "RisingBossVFX",
                        renderArtwork: false, sourcePattern: owner.selected);
                    if (shot == null) continue;
                    ((KarmaRisingFragmentAttack)shot).ConfigureFragment(owner, contact, life,
                        ((RisingBossBehavior)owner.behavior).FragmentReturned);
                    shot.name = "방패 파편";
                    // Irregular chips vary visually; their damage footprint and flight stay unchanged.
                    var sprite = KarmaRisingArt.Get("Fragment" + fragment);
                    if (sprite != null && shot.Visual != null)
                    {
                        shot.Visual.sprite = sprite;
                        // Emphasize the actual shard silhouette while preserving its damage footprint.
                        shot.Visual.transform.localScale = new Vector3(
                            shard.x * 1.5f / sprite.bounds.size.x, shard.y * 1.5f / sprite.bounds.size.y, 1);
                        shot.Visual.transform.localRotation = Quaternion.Euler(0, 0, degrees + Random.Range(-12f, 12f));
                        shot.Visual.flipX = Random.value < 0.5f;
                        shot.Visual.gameObject.SetActive(true);
                        shot.Visual.gameObject.AddComponent<KarmaRisingFragmentVisual>().Initialize(
                            Random.Range(35f, 75f) * (i % 2 == 0 ? 1 : -1), life);
                    }
                    if (shot.Visual != null)
                    {
                        shot.Visual.color = Color.white;
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
