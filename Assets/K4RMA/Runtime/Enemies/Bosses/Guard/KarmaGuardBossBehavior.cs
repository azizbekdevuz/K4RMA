using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class GuardBossBehavior : BossBehavior
        {
            new KarmaGuardBossSettings Settings { get { return owner.Tuning.guard; } }
            public override Sprite StunSprite { get { return KarmaArtwork.Sprite("BossStun", "Ward") ?? base.StunSprite; } }
            public override KarmaBossBalance Balance { get { return Settings; } }
            readonly GD01Pattern shield;
            public readonly GD02Pattern Heavy;
            public GuardBossBehavior(KarmaEnemy owner) : base(owner)
            {
                Register(EnemyAttack.GroundStrike, new GDBasicPattern(owner));
                shield = new GD01Pattern(owner);
                Heavy = new GD02Pattern(owner);
                Register(EnemyAttack.GD02, Heavy);
                Register(EnemyAttack.GD03, new GD03Pattern(owner));
                Register(EnemyAttack.GD04, new GD04Pattern(owner, Heavy));
                Register(EnemyAttack.GD05, new GD05Pattern(owner));
            }
            public override void Initialize()
            {
                if (KarmaCombatVFX.ApplySprite(owner.shieldHalo, "GuardBossVFX", "Shield",
                    KarmaSpace.Units(new Vector2(4.4f, 3.8f)) * KarmaSpace.GuardianSizeBoost))
                    owner.shieldHalo.transform.localPosition = Vector2.up * KarmaSpace.Units(1.2f);
                owner.nextShield = Settings.autoShieldPeriod;
                shield.RefreshAutoShield(); owner.shieldHalo.enabled = true;
                owner.animator.ConfigureHeavyStrike(owner.game.Player.Essences.PersonalizationOrder);
            }
            public override List<EnemyAttack> BuildCycle(IReadOnlyList<Essence> inherited)
            {
                var owned = new HashSet<Essence>(inherited);
                // GD01 runs independently of the attack cycle.
                var result = new List<EnemyAttack> { EnemyAttack.GroundStrike, EnemyAttack.GD02 };
                if (owned.Contains(Essence.SwordWave)) result.Add(EnemyAttack.GD03);
                if (owned.Contains(Essence.RisingSlash)) result.Add(EnemyAttack.GD04);
                if (owned.Count == 3) result.Add(EnemyAttack.GD05);
                return result;
            }
            public override bool IsUltimate(EnemyAttack attack) { return attack == EnemyAttack.GD05; }
            protected override bool Eligible(EnemyAttack attack, float distance, bool sight, bool escape)
            {
                if (!base.Eligible(attack, distance, sight, escape)) return false;
                if (attack == EnemyAttack.GD02) return owner.FightSeconds < owner.shieldUntil && distance <= Settings.heavyStrikeReach;
                return attack != EnemyAttack.GD04 || escape;
            }
            public override bool TickMovement(float dt, float dx)
            {
                Face(dx);
                if (Mathf.Abs(dx) > Settings.preferredDistance)
                    owner.Move(Mathf.Sign(dx) * owner.game.Config.enemyMoveSpeed * owner.Tuning.moveMultiplier * Settings.movementScale * dt);
                return false;
            }
            public override bool PreserveAutoShield { get { return true; } }
            public override float ParryStunSeconds { get { return Settings.parryStunSeconds; } }
            public override float PatternTimeout { get { return owner.selected == EnemyAttack.GD05 ? 40 : owner.selected == EnemyAttack.GD03 ? 12 : 5; } }
            public override bool ConsumeQueuedParry() { return Heavy.ConsumeQueuedParry(); }
            public override void OnPlayerDamaged() { owner.trial.Hit(); }
            public override void OnInterrupted()
            {
                owner.trial.Clear();
                if (owner.game.Player != null) owner.game.Player.ReleaseArena();
            }
            public override void OnParried()
            {
                owner.shieldUntil = 0;
                owner.nextShield = owner.FightSeconds + Mathf.Max(2, Settings.autoShieldPeriod);
            }
            public override void Tick() { shield.Tick(); }
            public override void OnCompleted() { Heavy.Interrupt(); }
            public override BossAttackVisual AttackVisual(EnemyAttack attack, Vector2 velocity)
            {
                string pose = attack == EnemyAttack.GD03 ? "Shard" : attack == EnemyAttack.GD04 ? "Pillar"
                    : velocity.sqrMagnitude > 0.01f ? "Wave" : attack == EnemyAttack.GD02 ? "Bash" : null;
                return new BossAttackVisual { Color = new Color(1, 0.7f, 0.12f),
                    Pose = pose, Sheet = pose != null ? "GuardBossVFX" : "GuardianVFX" };
            }
        }
    }
}
