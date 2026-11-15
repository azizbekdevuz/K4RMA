using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class SWBasicPattern : SwordBossPattern
        {
            public SWBasicPattern(KarmaEnemy owner, SwordBossBehavior sword) : base(owner, sword) { }
            public override IEnumerator Execute()
            {
                // The choice is latched in OnSelected, before any warning starts.
                // Crossing a range boundary never cancels or swaps this attack.
                if (sword.BasicMeleeSelected)
                { yield return Melee(); yield break; }
                owner.state = ActionState.Telegraph;
                Vector2 origin = KarmaSwordWaveAnimation.BasicLaunchOrigin(owner, owner.Position, owner.direction);
                Vector2 aim = SwordBossBehavior.SwordAim(origin, sword.SwordPlayerBounds.center, owner.direction);
                Vector2 velocity = aim * sword.BasicSwordSpeed;
                float flightLife = KarmaBossProjectileGeometry.SwordShotLife(origin, velocity, float.MaxValue);
                float warning = owner.Warning(0.4f);
                var mark = owner.PathMark(origin, origin + velocity * flightLife, sword.BasicSwordSize);
                KarmaSwordAttackPolish.Warning(mark);
                KarmaSwordWaveAnimation.Charge(owner.game, owner, owner.art, mark.transform, warning, false, basicRanged: true);
                owner.animator.PrepareBasicAttack(warning, false);
                yield return owner.Wait(warning);
                mark.gameObject.SetActive(false); Destroy(mark.gameObject);
                owner.state = ActionState.Attack; owner.animator.Attack(aim);
                owner.animator.BeginBasicRelease(Mathf.Min(2f / 24, sword.BasicSwordLife));
                // Recovery clears ordinary pattern objects. Let this released
                // wave finish at the real boundary under existing sword ownership.
                var shot = owner.Attack(origin, velocity, sword.BasicSwordSize, flightLife,
                    Settings.basicDamageMultiplier, parent: sword.PersistentRoot);
                KarmaSwordBasicPresentation.Ranged(owner.game, owner, shot, velocity,
                    sword.BasicSwordSize, sword.SwordSize(EnemyAttack.SW01));
                KarmaSwordWaveAnimation.AlignBasicVisual(shot);
                KarmaAudio.Play(KarmaSfxId.SwWaveLaunch, lifetimeOwner: shot);
                // Keep the previous casting/recovery/selection cadence. The old
                // ten-unit lifetime remains only the existing release wait here.
                yield return owner.Wait(sword.BasicSwordLife);
                yield return owner.Recovery(0.5f);
            }
            IEnumerator Melee()
            {
                const float contactSeconds = 3f / 24;
                // Measure from the first window's explicit retirement, so even
                // its last possible hit finishes normal player i-frames before
                // the return cut. Round up to authored frames with one-frame slack.
                float linkSeconds = Mathf.Ceil(Mathf.Max(6f / 24,
                    owner.game.Config.hitInvulnerability + 1f / 24) * 24) / 24;
                float strikeInterval = contactSeconds + linkSeconds;
                Vector2 size = sword.BasicMeleeSize;
                Vector2 center = owner.Position + Vector2.right * owner.direction * sword.BasicMeleeOffset;
                float warning = owner.Warning(0.4f);
                owner.state = ActionState.Telegraph;
                var mark = owner.Mark("SWBasic 근거리 칼질", center, size);
                KarmaSwordAttackPolish.Warning(mark);
                KarmaSwordBasicPresentation.Charge(owner.game, owner, mark.transform, warning);
                owner.animator.PrepareBasicAttack(warning, true);
                yield return owner.Wait(warning);
                mark.gameObject.SetActive(false); Destroy(mark.gameObject);
                if (!owner.Active) yield break;
                // Independent one-contact cuts each own half the original combo
                // damage. A block, parry or miss never transfers damage to the other.
                float strikeDamage = Settings.basicDamageMultiplier * 0.5f;
                for (int strike = 0; strike < 2; strike++)
                {
                    if (!owner.Active) yield break;
                    owner.state = ActionState.Attack; owner.animator.PlayPose("Slash", strike == 0 ? strikeInterval : 8f / 24);
                    owner.animator.BeginBasicRelease(contactSeconds, strike);
                    var shot = owner.Attack(center, Vector2.zero, size, contactSeconds, strikeDamage, new KarmaAttackContact());
                    KarmaSwordAttackPolish.Projectile(owner.game, owner, shot, Vector2.zero, size);
                    shot.Visual.flipX = owner.direction < 0;
                    yield return owner.Wait(contactSeconds);
                    // Lifetime expiry can fall between Update and coroutine ticks.
                    // Retire explicitly before starting the non-damaging link.
                    if (shot != null) { shot.gameObject.SetActive(false); Destroy(shot.gameObject); }
                    if (!owner.Active) yield break;
                    yield return owner.Wait(strike == 0 ? linkSeconds : 5f / 24);
                }
                yield return owner.Recovery(0.5f);
            }
            public override bool CanReach(Vector2 from, Vector2 target)
            {
                if (sword.BasicMeleeCanReach(from, target)) return true;
                float facing = target.x < from.x ? -1 : 1;
                Vector2 origin = KarmaSwordWaveAnimation.BasicLaunchOrigin(owner, from, facing);
                Vector2 aim = SwordBossBehavior.SwordAim(origin, target + (Vector2)sword.SwordPlayerBounds.center - owner.game.Player.Position, facing);
                return Vector2.Distance(from, target) <= sword.BasicWaveRange
                    && sword.SwordShotCanReach(origin, aim * sword.BasicSwordSpeed, sword.BasicSwordSize, sword.BasicSwordLife, target);
            }

        }
    }
}
