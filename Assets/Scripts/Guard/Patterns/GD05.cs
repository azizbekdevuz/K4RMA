using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        sealed class GD05Pattern : GuardBossPattern
        {
            public const float TrialCenter = 20;
            public const float TrialRadius = 10 * KarmaSpace.Scale;
            readonly GD02Pattern heavy;
            readonly KarmaTrialState trial = new KarmaTrialState();

            public GD05Pattern(KarmaEnemy owner, GD02Pattern heavy) : base(owner) { this.heavy = heavy; }
            public bool Active { get; private set; }
            public override void Interrupt() { Active = false; trial.Clear(); owner.animator.SetGuardTrialShield(null); }
            public void RecordHit() { if (Active) trial.Hit(); }

            public void StunFromParry()
            {
                bool enteredStun = !owner.Stunned;
                owner.state = ActionState.Stunned;
                owner.stunnedUntil = owner.FightSeconds + Mathf.Max(0.1f, Settings.trialParryStunSeconds);
                owner.ShowStunCue();
                if (enteredStun) KarmaAudio.Play(KarmaSfxId.CombatBossStagger);
            }

            public override IEnumerator Execute()
            {
                try { yield return ExecuteTrial(); }
                finally { owner.animator.SetGuardTrialShield(null); }
            }
            IEnumerator ExecuteTrial()
            {
                trial.Clear();
                owner.fullShield = true;
                BreakShield();
                owner.state = ActionState.Telegraph;
                yield return owner.Wait(0.55f);
                if (!owner.Active) yield break;

                float limit = TrialRadius - KarmaSpace.Units(0.6f);
                owner.game.Player.ConfineArena(TrialCenter - limit, TrialCenter + limit);
                yield return owner.Motion(new Vector2(TrialCenter, KarmaSpace.GuardianGroundY), 0.5f, Vector2.zero);
                if (!owner.Active) yield break;

                Vector2 center = new Vector2(TrialCenter, 0);
                var dome = KarmaGuardDome.Create(owner.game, owner.effectsRoot, center, TrialRadius);
                GuardDecoration("GuardBossVFX", "Seal", center + Vector2.up * KarmaSpace.Units(0.25f),
                    new Vector2(TrialRadius * 2, KarmaSpace.Units(2)), new Color(1, 1, 1, 0.5f), 3);
                trial.Begin(1, Mathf.Max(1, Settings.trialFailureHits) - 1);
                Active = true;
                // The survival trial stays invulnerable, including its parry stun and dome dissolution.
                KarmaAudio.Play(KarmaSfxId.GdUltimate);
                owner.state = ActionState.Approach;

                var sprite = KarmaArtwork.Sprite("GuardDomeShield", "Ricochet");
                float radius = KarmaSpace.Units(0.9f);
                float heading = owner.game.Player.Position.x < TrialCenter ? -1 : 1;
                var start = center + KarmaSpace.Units(new Vector2(heading * 1.55f, 1.65f));
                var shield = KarmaVisuals.Box(owner.effectsRoot, "GD05 튕기는 방패", start, Vector2.one, Color.white, 26);
                shield.sprite = sprite;
                owner.animator.SetGuardTrialShield(shield);
                // The transparent margin and aura are outside the round shield's physical rim.
                shield.transform.localScale = Vector3.one * (2 * radius / (sprite.rect.width / sprite.pixelsPerUnit * 0.77f));
                yield return owner.Wait(0.4f);
                if (!owner.Active) yield break;

                float endsAt = owner.FightSeconds + Mathf.Clamp(Settings.trialDuration, 2, 25);
                owner.StartCoroutine(Ricochet(shield, center, start - center, new Vector2(heading, 0.3f).normalized, radius));
                float nextStrike = owner.FightSeconds + 0.25f;
                while (owner.Active && owner.FightSeconds < endsAt)
                {
                    if (owner.game.Paused || owner.Stunned) { yield return null; continue; }
                    float dx = owner.game.Player.Position.x - owner.Position.x;
                    if (Mathf.Abs(dx) > 0.03f)
                    {
                        owner.direction = Mathf.Sign(dx);
                        owner.art.localScale = new Vector3(owner.direction * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                    }

                    float distance = Mathf.Abs(dx);
                    if (distance > KarmaSpace.Units(2.1f))
                    {
                        float step = Mathf.Min(distance - KarmaSpace.Units(2.1f), KarmaSpace.Units(Mathf.Max(1, Settings.trialChaseSpeed)) * Time.deltaTime);
                        float x = Mathf.Clamp(owner.Position.x + owner.direction * step, TrialCenter - limit, TrialCenter + limit);
                        owner.Move(x - owner.Position.x);
                    }
                    if (distance <= (Settings.heavyStrikeReach - KarmaSpace.Units(0.3f))
                        && owner.FightSeconds >= nextStrike && owner.FightSeconds + heavy.StrikeDuration < endsAt)
                    {
                        yield return heavy.Strike(true);
                        heavy.Interrupt();
                        if (!owner.Stunned) owner.state = ActionState.Approach;
                        nextStrike = owner.FightSeconds + 0.65f;
                    }
                    yield return null;
                }

                if (!owner.Active) yield break;
                Active = false;
                trial.CompleteAttack();
                shield.gameObject.SetActive(false);
                owner.animator.SetGuardTrialShield(null);
                heavy.Interrupt();
                owner.HideStunCue();
                owner.state = ActionState.Recover;
                if (!trial.Passed)
                {
                    // Stop all room attacks before the blast tell, so the last ordinary hit's
                    // invulnerability expires naturally. Do not alter player invulnerability.
                    owner.state = ActionState.Telegraph;
                    dome.WarnFailure();
                    yield return owner.Wait(Mathf.Max(0.55f, owner.game.Config.hitInvulnerability + 0.05f));
                    if (!owner.Active) yield break;
                    dome.Explode();
                    owner.game.PresentationShake(0.3f, 1.7f);
                    var player = owner.game.Player;
                    if (player.TakeTerrainDamage(owner.Damage * Settings.trialExplosionDamageMultiplier))
                        KarmaCombatVFX.SpawnSprite(owner.effectsRoot, player.Position, "HitVFX", "Burst", KarmaSpace.Units(Vector2.one * 1.8f), 0.25f);
                    if (!owner.Active) yield break;
                }
                else dome.Dissolve();
                owner.game.Player.ReleaseArena();
                yield return owner.Wait(0.4f);
                if (!owner.Active) yield break;
                if (trial.Passed)
                {
                    owner.ClearPatternObjects();
                    owner.fullShield = false;
                    owner.state = ActionState.Stunned;
                    owner.stunnedUntil = owner.FightSeconds + Mathf.Max(0.1f, Settings.trialSuccessStunSeconds);
                    owner.ShowStunCue();
                    yield return owner.Wait(Mathf.Max(0.1f, Settings.trialSuccessStunSeconds));
                    if (!owner.Active) yield break;
                    owner.HideStunCue();
                    owner.state = ActionState.Approach;
                }
                else yield return owner.Recovery(0.65f);
            }

            IEnumerator Ricochet(SpriteRenderer shield, Vector2 center, Vector2 position, Vector2 velocity, float radius)
            {
                var trail = shield.gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = KarmaCombatVFX.GlowMaterial;
                trail.sortingOrder = 25;
                trail.time = 0.11f;
                trail.startWidth = radius * 0.7f;
                trail.endWidth = 0;
                trail.startColor = new Color(1, 0.81f, 0.12f, 0.55f);
                trail.endColor = new Color(1, 0.7f, 0.08f, 0);
                trail.minVertexDistance = 0.05f;

                float speed = KarmaSpace.Units(Mathf.Max(1, Settings.trialShieldSpeed));
                float arcRadius = TrialRadius - radius;
                var contact = new KarmaAttackContact();
                while (Active && owner.Active)
                {
                    if (owner.game.Paused) { yield return null; continue; }
                    float remaining = speed * Time.deltaTime;
                    // Split the frame at each impact. Hit tests follow the actual path around corners.
                    for (int i = 0; i < 6 && remaining > 0.0001f && owner.Active; i++)
                    {
                        float projected = Vector2.Dot(position, velocity);
                        float arc = Mathf.Max(0, -projected + Mathf.Sqrt(Mathf.Max(0,
                            projected * projected + arcRadius * arcRadius - position.sqrMagnitude)));
                        float floor = velocity.y < 0 ? Mathf.Max(0, (radius - position.y) / velocity.y) : float.PositiveInfinity;
                        float boundary = Mathf.Min(arc, floor);
                        float travel = Mathf.Min(remaining, boundary);
                        Vector2 next = position + velocity * travel;
                        owner.TryGuardCircleHit(center + position, center + next, radius,
                            owner.Damage * Settings.trialDamageMultiplier, contact);
                        position = next;
                        if (boundary > remaining) break;
                        Vector2 normal = floor <= arc ? Vector2.up : -position.normalized;
                        velocity = Vector2.Reflect(velocity, normal).normalized;
                        position += normal * 0.001f;
                        contact = new KarmaAttackContact();
                        remaining -= travel;
                    }
                    if (!owner.Active) yield break;
                    shield.transform.position = center + position;
                    shield.transform.Rotate(0, 0, -330 * Time.deltaTime);
                    yield return null;
                }
            }
        }
    }
}
