using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class GuardBossBehavior : BossBehavior
        {
            new KarmaGuardBossSettings Settings { get { return owner.Tuning.guard; } }
            public override KarmaBossBalance Balance { get { return Settings; } }
            readonly GD01Pattern shield;
            public GuardBossBehavior(KarmaEnemy owner) : base(owner)
            {
                shield = new GD01Pattern(owner);
                Register(EnemyAttack.GD02, new GD02Pattern(owner, shield));
                Register(EnemyAttack.GD03, new GD03Pattern(owner));
                Register(EnemyAttack.GD04, new GD04Pattern(owner));
                Register(EnemyAttack.GD05, new GD05Pattern(owner));
            }
            public override void Tick() { shield.Tick(); }
            public override BossAttackVisual AttackVisual(EnemyAttack attack, Vector2 velocity)
            {
                string pose = attack == EnemyAttack.GD03 ? "Shard" : attack == EnemyAttack.GD04 ? "Pillar"
                    : velocity.sqrMagnitude > 0.01f ? "Wave" : attack == EnemyAttack.GD02 ? "Bash" : null;
                return new BossAttackVisual { Color = new Color(1, 0.7f, 0.12f), ShieldBreak = attack == EnemyAttack.GD02,
                    Pose = pose, Sheet = pose != null ? "GuardBossVFX" : "GuardianVFX" };
            }
            public override bool TrySelect(float distance, bool sight, bool escape, out EnemyAttack attack)
            {
                if (owner.FightSeconds < owner.shieldUntil && distance <= KarmaSpace.Units(5))
                { attack = EnemyAttack.GD02; return true; }
                return base.TrySelect(distance, sight, escape, out attack);
            }

        }
    }
}
