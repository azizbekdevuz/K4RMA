using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        sealed class GD03Pattern : GuardBossPattern
        {
            public GD03Pattern(KarmaEnemy owner) : base(owner) { }
            public override void Interrupt()
            { KarmaAudio.StopSlot("gd-flight"); owner.animator.SetGuardThrownShield(null); }

            public override IEnumerator Execute()
            {
                owner.state = ActionState.Telegraph;
                Vector2 size = KarmaSpace.Units(Vector2.one * 1.8f);
                float heading = owner.direction;
                Vector2 origin = owner.Position + KarmaSpace.Units(new Vector2(heading * 1.05f, 0.9f));
                origin.x = Mathf.Clamp(origin.x, KarmaStage.ArenaLeft + size.x / 2, KarmaStage.ArenaRight - size.x / 2);
                Vector2 edge = new Vector2(heading < 0 ? KarmaStage.ArenaLeft + size.x / 2
                    : KarmaStage.ArenaRight - size.x / 2, origin.y);
                var mark = owner.PathMark(origin, edge, size);
                yield return owner.Wait(owner.Warning(0.8f));
                if (!owner.Active) yield break;
                mark.gameObject.SetActive(false); Destroy(mark.gameObject);
                owner.state = ActionState.Attack; owner.animator.Attack();

                var shield = KarmaVisuals.Box(owner.effectsRoot, "GD03 큰 방패 투척", origin, Vector2.one, Color.white, 26);
                KarmaCombatVFX.ApplySprite(shield, "GuardBossVFX", "WholeHot", size, true);
                shield.sharedMaterial = KarmaVisuals.DefaultMaterial;
                var trail = shield.gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = KarmaVisuals.DefaultMaterial;
                trail.sortingOrder = 25;
                trail.minVertexDistance = KarmaSpace.Units(0.04f);
                trail.time = 0.1f;
                trail.startWidth = KarmaSpace.Units(0.1f); trail.endWidth = 0;
                trail.startColor = new Color(1, 0.12f, 0.025f, 0.7f);
                trail.endColor = new Color(1, 0.3f, 0.04f, 0);
                trail.numCapVertices = trail.numCornerVertices = 2;

                owner.animator.SetGuardThrownShield(shield);
                KarmaAudio.Play(KarmaSfxId.GdShieldLaunch, lifetimeOwner: shield);
                KarmaAudio.Play(KarmaSfxId.GdShieldFlight, lifetimeOwner: shield);
                try
                {
                    float speed = Mathf.Max(KarmaSpace.Units(1), Settings.shieldThrowSpeed);
                    bool returning = false, reflected = false;
                    var contact = new KarmaAttackContact(parryWithoutRiposte: true);
                    while (owner.Active)
                    {
                        if (owner.game.Paused) { yield return null; continue; }
                        Vector2 before = shield.transform.position;
                        Vector2 home = owner.Position + Vector2.up * KarmaSpace.Units(0.9f);
                        Vector2 target = returning ? home : edge;
                        Vector2 to = Vector2.MoveTowards(before, target, speed * Time.deltaTime);
                        shield.transform.position = to;
                        shield.transform.Rotate(0, 0, -heading * 720 * Time.deltaTime);
                        if (reflected)
                        {
                            // Only the returned shield deals the counter damage; no generic riposte is spawned.
                            if (owner.IntersectsDamagePath(before, to, size.x / 2))
                            {
                                KarmaCombatVFX.SpawnSprite(owner.effectsRoot, to, "HitVFX", "Burst", KarmaSpace.Units(Vector2.one * 2.2f), 0.25f);
                                owner.game.SwordHitFeedback();
                                owner.game.PresentationShake(0.2f, 0.22f);
                                shield.gameObject.SetActive(false); Destroy(shield.gameObject);
                                owner.animator.SetGuardThrownShield(null);
                                owner.TakeDamage(owner.game.Config.Techniques.counterDamage, before);
                                if (!owner.Active) yield break;
                                owner.state = ActionState.Stunned;
                                owner.stunnedUntil = owner.FightSeconds + Mathf.Max(0.1f, Settings.parryStunSeconds);
                                owner.ShowStunCue();
                                while (owner.Active && owner.Stunned) yield return null;
                                yield break;
                            }
                        }
                        else
                        {
                            int parries = owner.game.Player.ParryCount;
                            owner.TryAttackHit(before, to, size, owner.Damage * Settings.shieldThrowDamageMultiplier,
                                contact, 0, sourcePattern: EnemyAttack.GD03);
                            if (!owner.Active) yield break;
                            if (owner.game.Player.ParryCount != parries)
                            {
                                if (owner.AutoShieldActive) KarmaAudio.Play(KarmaSfxId.GdShieldBreak);
                                BreakShield();
                                owner.shieldHalo.enabled = false;
                                returning = reflected = true;
                                speed *= 1.5f;
                                KarmaCombatVFX.SpawnSprite(owner.effectsRoot, to, "HitVFX", "Burst", KarmaSpace.Units(Vector2.one * 1.5f), 0.18f);
                                owner.game.SwordHitFeedback();
                                owner.game.PresentationShake(0.1f, 0.1f);
                                trail.startWidth = KarmaSpace.Units(0.25f);
                                trail.time = 0.16f;
                                shield.name = "GD03 큰 방패 반사";
                                trail.startColor = new Color(1, 0.85f, 0.2f, 0.8f);
                                trail.endColor = new Color(1, 0.85f, 0.2f, 0);
                            }
                        }
                        if (!reflected && (to - target).sqrMagnitude < 0.0001f)
                        {
                            if (returning) break;
                            returning = true;
                            shield.name = "GD03 큰 방패 복귀";
                            // Outward and return passes each have one independent player contact.
                            contact = new KarmaAttackContact(parryWithoutRiposte: true);
                        }
                        yield return null;
                    }
                }
                finally { KarmaAudio.StopOwned(shield, 0.08f); owner.animator.SetGuardThrownShield(null); }
                // A normal catch immediately ends the attack; only a reflected hit earns a stun.
            }
        }
    }
}
