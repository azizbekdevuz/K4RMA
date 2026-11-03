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
                Vector2 origin = owner.Position;
                Vector2 aim = SwordBossBehavior.SwordAim(origin, sword.SwordPlayerBounds.center, owner.direction);
                float warning = owner.Warning(0.4f);
                var mark = owner.PathMark(origin, origin + aim * sword.BasicSwordSpeed * sword.BasicSwordLife, sword.BasicSwordSize);
                KarmaSwordAttackPolish.Warning(mark);
                KarmaSwordAttackPolish.Charge(owner.game, owner, owner.art, origin, warning, false);
                owner.animator.PrepareWaveAttack(warning, false);
                yield return owner.Wait(warning);
                mark.gameObject.SetActive(false); Destroy(mark.gameObject);
                owner.state = ActionState.Attack; owner.animator.Attack(aim);
                var shot = owner.Attack(origin, aim * sword.BasicSwordSpeed, sword.BasicSwordSize, sword.BasicSwordLife, Settings.basicDamageMultiplier);
                KarmaSwordAttackPolish.Projectile(owner.game, owner, shot, aim * sword.BasicSwordSpeed, sword.BasicSwordSize);
                sword.WaveLaunchVFX(origin, aim * sword.BasicSwordSpeed, sword.BasicSwordSize, false);
                yield return owner.Wait(sword.BasicSwordLife);
                yield return owner.Recovery(0.5f);
            }
            IEnumerator Melee()
            {
                Vector2 size = sword.BasicMeleeSize;
                Vector2 center = owner.Position + Vector2.right * owner.direction * sword.BasicMeleeOffset;
                float warning = owner.Warning(0.4f);
                owner.state = ActionState.Telegraph;
                var mark = owner.Mark("SWBasic 근거리 칼질", center, size);
                KarmaSwordAttackPolish.Warning(mark);
                KarmaSwordAttackPolish.Charge(owner.game, owner, owner.art, center, warning, false);
                owner.animator.PrepareWaveAttack(warning, false);
                yield return owner.Wait(warning);
                mark.gameObject.SetActive(false); Destroy(mark.gameObject);
                owner.state = ActionState.Attack; owner.animator.PlayPose("Slash", 0.3f);
                var shot = owner.Attack(center, Vector2.zero, size, 0.26f, Settings.basicDamageMultiplier);
                KarmaSwordAttackPolish.Projectile(owner.game, owner, shot, Vector2.zero, size);
                shot.Visual.flipX = owner.direction < 0;
                KarmaSwordAttackPolish.Release(owner.game, owner, owner.art, center, Vector2.right * owner.direction, size, false);
                yield return owner.Wait(0.3f);
                yield return owner.Recovery(0.5f);
            }
            public override bool CanReach(Vector2 from, Vector2 target)
            {
                if (sword.BasicMeleeCanReach(from, target)) return true;
                float facing = target.x < from.x ? -1 : 1;
                Vector2 aim = SwordBossBehavior.SwordAim(from, target + (Vector2)sword.SwordPlayerBounds.center - owner.game.Player.Position, facing);
                return Vector2.Distance(from, target) <= sword.BasicWaveRange
                    && sword.SwordShotCanReach(from, aim * sword.BasicSwordSpeed, sword.BasicSwordSize, sword.BasicSwordLife, target);
            }

        }
    }
}
