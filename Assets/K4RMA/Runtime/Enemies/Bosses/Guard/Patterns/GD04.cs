using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        sealed class GD04Pattern : GuardBossPattern
        {
            readonly GD02Pattern heavy;
            public GD04Pattern(KarmaEnemy owner, GD02Pattern heavy) : base(owner) { this.heavy = heavy; }
            public override void Interrupt() { owner.animator.SetGroundSlamFrame(-1); }

            public override IEnumerator Execute()
            {
                owner.frontalGuard = true; owner.state = ActionState.Telegraph;
                owner.direction = owner.game.Player.Position.x < owner.Position.x ? -1 : 1;
                int attackDirection = owner.Facing;
                owner.art.localScale = new Vector3(attackDirection * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                bool sawPlayerInFront = (owner.game.Player.Position.x - owner.Position.x) * attackDirection >= 0;
                float room = attackDirection > 0 ? KarmaStage.ArenaRight - owner.Position.x : owner.Position.x - KarmaStage.ArenaLeft;
                float reach = Mathf.Min(Mathf.Max(0.1f, Settings.slamRange), Mathf.Max(0.1f, room));
                float near = Mathf.Min(KarmaSpace.Units(0.6f), reach * 0.25f);
                Vector2 size = new Vector2(reach - near, Mathf.Max(0.1f, Settings.slamHeight));
                Vector2 center = new Vector2(owner.Position.x + attackDirection * (reach + near) / 2, size.y / 2);
                var mark = owner.Mark("GD04 전방 강타 · 패링 불가", center, size);
                float warning = owner.Warning(0.8f);
                const float descent = 0.1f;
                owner.animator.SetGroundSlamFrame(0);
                yield return owner.Wait(Mathf.Max(0, warning - descent));
                owner.animator.SetGroundSlamFrame(1);
                yield return owner.Wait(descent);
                if (!owner.Active) yield break;
                mark.gameObject.SetActive(false); Destroy(mark.gameObject);
                owner.state = ActionState.Attack; owner.animator.Attack(Vector2.down);
                owner.animator.SetGroundSlamFrame(2);
                Vector2 impact = owner.animator.GroundSlamContact;
                impact.y = 0.08f;
                KarmaCombatVFX.Burst(owner.effectsRoot, impact, KarmaArtwork.Sprite("GuardBasaltDebris", "Burst"),
                    "GD04 ground impact", KarmaSpace.Units(new Vector2(3.2f, 2.4f)), 0.32f, 0, new Color(1, 0.54f, 0.26f), true);
                owner.game.PresentationShake(0.22f, 1.2f);
                float fanStart = near;
                var fan = KarmaVisuals.Box(owner.effectsRoot, "GD04 전방 충격파", Vector2.zero, Vector2.one, Color.white, 4);
                if (!KarmaCombatVFX.ApplySprite(fan, "GuardSlamFan", "Impact", new Vector2(Mathf.Max(0.1f, reach - fanStart), KarmaSpace.Units(2.6f))))
                    throw new System.InvalidOperationException("GD04 전방 충격파 이미지가 없습니다.");
                fan.sharedMaterial = KarmaVisuals.DefaultMaterial;
                // Keep the art on the original attack side even when the boss chains toward the rear.
                fan.transform.position = new Vector2(owner.Position.x + attackDirection * fanStart, 0.05f);
                Vector3 fanScale = fan.transform.localScale;
                fanScale.x *= attackDirection;
                var crestObject = new GameObject("GD04 moving shock front");
                crestObject.transform.SetParent(owner.effectsRoot, false);
                var crest = crestObject.AddComponent<LineRenderer>();
                crest.sharedMaterial = KarmaCombatVFX.GlowMaterial;
                crest.sortingOrder = 24; crest.positionCount = 16;
                crest.startWidth = 0.24f; crest.endWidth = 0.06f;
                float started = owner.FightSeconds;
                bool resolved = false;
                float previousFront = near, nextDebris = near;
                const float travelSeconds = 0.55f;
                while (owner.Active)
                {
                    if (owner.game.Paused) { yield return null; continue; }
                    float elapsed = owner.FightSeconds - started;
                    if (elapsed >= 0.14f) owner.animator.SetGroundSlamFrame(3);
                    float progress = Mathf.Clamp01(elapsed / travelSeconds);
                    float front = Mathf.Lerp(near, reach, progress);
                    fan.transform.localScale = new Vector3(fanScale.x * Mathf.Max(0.02f, progress), fanScale.y, fanScale.z);
                    float tail = attackDirection > 0 ? fan.bounds.min.x : fan.bounds.max.x;
                    fan.transform.position += Vector3.right * (owner.Position.x + attackDirection * near - tail);
                    fan.color = Color.white;
                    const float crestAlpha = 1;
                    crest.startColor = new Color(1, 0.78f, 0.28f, crestAlpha);
                    crest.endColor = new Color(1, 0.4f, 0.08f, crestAlpha * 0.5f);
                    for (int i = 0; i < crest.positionCount; i++)
                    {
                        float t = i / (float)(crest.positionCount - 1);
                        crest.SetPosition(i, new Vector3(owner.Position.x + attackDirection *
                            (front - (1 - Mathf.Sin(t * Mathf.PI)) * KarmaSpace.Units(0.6f)), 0.08f + t * size.y, 0));
                    }
                    // Only the moving front is dangerous; the glowing cracks behind it are aftermath.
                    if (previousFront < reach)
                    {
                        float back = Mathf.Max(near, previousFront - KarmaSpace.Units(0.7f));
                        Vector2 waveSize = new Vector2(Mathf.Max(0.05f, front - back), size.y);
                        Vector2 waveCenter = new Vector2(owner.Position.x + attackDirection * (front + back) / 2, center.y);
                        if (!resolved) resolved = TryGroundImpact(waveCenter, waveSize);
                        if (front >= nextDebris)
                        {
                            KarmaCombatVFX.Burst(owner.effectsRoot, new Vector2(owner.Position.x + attackDirection * front, 0.1f),
                                KarmaArtwork.Sprite("GuardBasaltDebris", "Burst"), "GD04 wave crest",
                                KarmaSpace.Units(new Vector2(3, 2.5f)), 0.28f, 0, new Color(1, 0.62f, 0.3f));
                            nextDebris = front + KarmaSpace.Units(1.5f);
                        }
                        previousFront = front;
                    }
                    if (!owner.Active) yield break;
                    float signedDistance = (owner.game.Player.Position.x - owner.Position.x) * attackDirection;
                    if (signedDistance >= 0) sawPlayerInFront = true;
                    if (owner.AutoShieldActive && sawPlayerInFront && signedDistance < -KarmaSpace.PlayerHalfWidth
                        && -signedDistance <= Settings.heavyStrikeReach + KarmaSpace.PlayerHalfWidth)
                    {
                        Interrupt();
                        crestObject.SetActive(false);
                        owner.selected = owner.previous = EnemyAttack.GD02;
                        owner.direction = -attackDirection;
                        owner.art.localScale = new Vector3(owner.Facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                        owner.patternStarted = owner.FightSeconds;
                        fan.gameObject.AddComponent<KarmaEffect>().Initialize(0.4f);
                        yield return heavy.Execute();
                        yield break;
                    }
                    if (progress >= 1) break;
                    yield return null;
                }
                Interrupt();
            }

            bool TryGroundImpact(Vector2 center, Vector2 size)
            {
                var player = owner.game.Player;
                var collider = player.GetComponent<Collider2D>();
                var bounds = collider != null && collider.enabled ? collider.bounds : new Bounds(player.Position, KarmaSpace.PlayerSize);
                if (Mathf.Abs(bounds.center.x - center.x) > bounds.extents.x + size.x / 2
                    || Mathf.Abs(bounds.center.y - center.y) > bounds.extents.y + size.y / 2) return false;
                if (player.Shielded)
                    KarmaCombatVFX.SpawnSprite(owner.effectsRoot, player.Position, "PlayerVFX", "Barrier", KarmaSpace.Units(new Vector2(1.6f, 2.1f)), 0.2f);
                else if (player.TakeTerrainDamage(owner.Damage * Settings.slamDamageMultiplier) && owner.Active)
                {
                    // Ground damage bypasses counter consumption; Stun cancels player attacks before the physical launch.
                    player.Stun(Mathf.Max(0.1f, Settings.slamStunSeconds));
                    player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.up * Mathf.Max(0.1f, Settings.slamKnockUpSpeed);
                    KarmaCombatVFX.SpawnSprite(owner.effectsRoot, player.Position, "HitVFX", "Burst", KarmaSpace.Units(Vector2.one), 0.18f);
                }
                return true;
            }
        }
    }
}
