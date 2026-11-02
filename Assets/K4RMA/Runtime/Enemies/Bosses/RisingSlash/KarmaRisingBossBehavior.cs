using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class RisingBossBehavior : BossBehavior
        {
            new KarmaRisingBossSettings Settings { get { return owner.Tuning.rising; } }
            public override Sprite StunSprite { get { return KarmaArtwork.Sprite("BossStun", "Rise") ?? base.StunSprite; } }
            public override KarmaBossBalance Balance { get { return Settings; } }
            float nextStride;
            public RisingBossBehavior(KarmaEnemy owner) : base(owner)
            {
                Register(EnemyAttack.GroundStrike, new UPBasicPattern(owner));
                Register(EnemyAttack.UP01, new UP01Pattern(owner));
                Register(EnemyAttack.UP02, new UP02Pattern(owner));
                Register(EnemyAttack.UP03, new UP03Pattern(owner));
                Register(EnemyAttack.UP04, new UP04Pattern(owner));
                Register(EnemyAttack.UP05, new UP05Pattern(owner));
            }
            public override List<EnemyAttack> BuildCycle(IReadOnlyList<Essence> inherited)
            {
                var owned = new HashSet<Essence>(inherited);
                var result = new List<EnemyAttack> { EnemyAttack.GroundStrike, EnemyAttack.UP01, EnemyAttack.UP02 };
                if (owned.Contains(Essence.SwordWave)) result.Add(EnemyAttack.UP03);
                if (owned.Contains(Essence.Guard)) result.Add(EnemyAttack.UP04);
                if (owned.Count == 3) result.Add(EnemyAttack.UP05);
                return result;
            }
            public override bool IsUltimate(EnemyAttack attack) { return attack == EnemyAttack.UP05; }
            public override void OnAttackParried(bool stun)
            { if (stun) KarmaRisingPolish.ParryBreak(owner.effectsRoot, owner.game.Player.Position); }
            public override float ParryStunSeconds { get { return Settings.fragmentParryStunSeconds; } }
            public override bool TickMovement(float dt, float dx)
            {
                Face(dx);
                if (Mathf.Abs(dx) > Settings.preferredDistance)
                {
                    owner.Move(Mathf.Sign(dx) * owner.game.Config.enemyMoveSpeed * owner.Tuning.moveMultiplier * Settings.movementScale * Settings.approachSpeedMultiplier * dt);
                    if (owner.FightSeconds >= nextStride)
                    { nextStride = owner.FightSeconds + 0.12f; KarmaRisingPolish.Stride(owner.effectsRoot, owner.Position, Mathf.Sign(dx)); }
                }
                return false;
            }
            protected override bool Eligible(EnemyAttack attack, float distance, bool sight, bool escape)
            {
                if (!base.Eligible(attack, distance, sight, escape)) return false;
                if (attack == EnemyAttack.GroundStrike) return distance <= Settings.meleeReach;
                if (attack == EnemyAttack.UP02) return owner.previous != attack && (owner.game.Player.Airborne || distance <= KarmaSpace.Units(8));
                return attack != EnemyAttack.UP03 || escape;
            }
            public override float DecisionDelay { get { return Mathf.Max(0.26f, base.DecisionDelay * Settings.attackDecisionMultiplier); } }

        }
    }
}
