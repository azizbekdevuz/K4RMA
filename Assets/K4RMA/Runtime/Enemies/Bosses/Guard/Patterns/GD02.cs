using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class GD02Pattern : GuardBossPattern
        {
            public GD02Pattern(KarmaEnemy owner, GD01Pattern shield) : base(owner) { this.shield = shield; }
            readonly GD01Pattern shield;
            public override IEnumerator Execute()
            {
                Vector2 center = owner.Position + Vector2.right * owner.direction * KarmaSpace.Units(2);
                Vector2 size = KarmaSpace.Units(new Vector2(4, 2.8f));
                var mark = owner.Mark("GD02 강공격 · 패링 가능", center, size);
                owner.state = ActionState.Telegraph; yield return owner.Wait(owner.Warning(0.9f));
                Destroy(mark.gameObject); owner.animator.Attack(); owner.state = ActionState.Attack;

                // Show the guardian Counter accent from the shared sprite library.
                KarmaCombatVFX.SpawnSprite(owner.effectsRoot, center, "GuardianVFX", "Counter", size, 0.2f);
                int parries = owner.game.Player.ParryCount;
                var contact = new KarmaAttackContact();
                owner.Attack(center, Vector2.zero, size, 0.18f, Settings.heavyStrikeDamageMultiplier, contact);
                owner.TryAttackHit(center, center, size, owner.Damage * Settings.heavyStrikeDamageMultiplier, contact, 0, shieldBreak: true);
                if (owner.game.Player.ParryCount != parries) yield break;
                // Include contacts made after the first frame of the active hit box.
                yield return owner.Wait(0.2f);
                if (!owner.Active || owner.pendingParry) yield break;
                bool refreshed = Settings.dodgeRefreshesShield || contact.DealtDamage;
                if (refreshed)
                {
                    shield.RefreshAutoShield();

                }
                if (owner.Enraged)
                {
                    Vector2 waveAt = new Vector2(owner.Position.x, KarmaSpace.Units(0.65f));
                    Vector2 waveSize = KarmaSpace.Units(new Vector2(1.2f, 0.65f));
                    var warning = owner.PathMark(waveAt, waveAt + Vector2.right * owner.direction * KarmaSpace.Units(10), waveSize);

                    yield return owner.Wait(owner.Warning(0.6f)); Destroy(warning.gameObject);
                    owner.Attack(waveAt, Vector2.right * owner.direction * KarmaSpace.Units(14), waveSize, 0.7f, 0.75f);
                    yield return owner.Wait(0.75f);
                }
                // The update refresh is useful during the visible result. Recovery then exposes the boss.
                yield return owner.Recovery(1.4f, refreshed);
            }

        }
    }
}
