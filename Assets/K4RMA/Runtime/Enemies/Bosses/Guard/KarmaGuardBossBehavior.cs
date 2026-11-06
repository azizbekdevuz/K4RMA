using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        [BossKind(Essence.Guard)]
        [UnityEngine.Scripting.Preserve]
        sealed class GuardBossBehavior : BossBehavior
        {
#if UNITY_EDITOR
            public bool PracticeHold;
#endif
            new KarmaGuardBossSettings Settings { get { return owner.Tuning.guard; } }
            public override Sprite StunSprite { get { return KarmaArtwork.Sprite("BossStun", "Ward") ?? base.StunSprite; } }
            public override KarmaBossBalance Balance { get { return Settings; } }
            readonly GD01Pattern shield;
            public readonly GD02Pattern Heavy;
            public readonly GD05Pattern Dome;
            int domeUses;
            [UnityEngine.Scripting.Preserve]
            public GuardBossBehavior(KarmaEnemy owner) : base(owner)
            {
                Register(EnemyAttack.GroundStrike, new GDBasicPattern(owner));
                shield = new GD01Pattern(owner);
                Heavy = new GD02Pattern(owner);
                Register(EnemyAttack.GD02, Heavy);
                Register(EnemyAttack.GD03, new GD03Pattern(owner));
                Register(EnemyAttack.GD04, new GD04Pattern(owner, Heavy));
                Dome = new GD05Pattern(owner, Heavy);
                Register(EnemyAttack.GD05, Dome);
            }
            public override void Initialize()
            {
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
                if (attack == EnemyAttack.GD05 || !base.Eligible(attack, distance, sight, escape)) return false;
                if (attack == EnemyAttack.GD02) return owner.FightSeconds < owner.shieldUntil && distance <= Settings.heavyStrikeReach;
                return attack != EnemyAttack.GD04 || escape;
            }
            public override bool TickMovement(float dt, float dx)
            {
#if UNITY_EDITOR
                if (PracticeHold) return true;
#endif
                Face(dx);
                if (Mathf.Abs(dx) > Settings.preferredDistance)
                {
                    owner.Move(Mathf.Sign(dx) * owner.game.Config.enemyMoveSpeed * owner.Tuning.moveMultiplier * Settings.movementScale * dt);
                    // The host updates poses before movement. Refresh once after the actual step.
                    owner.animator.GuardianPose("Approach", owner.selected, false, Armored);
                }
                return false;
            }
            public override bool Armored { get { return owner.frontalGuard || AutoShieldActive; } }
            public override bool AutoShieldActive { get { return owner.FightSeconds < owner.shieldUntil; } }
            public override bool BlocksSelection { get { return owner.shieldWarningUntil > 0; } }
            public override bool ReduceDamage(Vector2 source)
            { return owner.frontalGuard && (source.x - owner.Position.x) * owner.direction >= 0 || AutoShieldActive; }
            public override void ReducedDamageFeedback()
            { KarmaRemnantVFX.Impact(owner.game, owner.Position, Essence.Guard); }
            public override Color ShieldColor { get { return BlocksSelection
                ? new Color(1, 0.8f, 0.2f, 0.4f + 0.4f * Mathf.Abs(Mathf.Sin(owner.FightSeconds * 8))) : base.ShieldColor; } }
            public override void EndAction(bool keepAutoShield) { owner.frontalGuard = false; }
            public override void StopCombat() { owner.shieldUntil = owner.shieldWarningUntil = 0; }
            public override bool TryQueueParry() { return Heavy.TryQueueParry(); }
            public override Vector3? CameraFocus { get { return (owner.Invulnerable || Dome.Active) && owner.selected == EnemyAttack.GD05
                ? new Vector3(TrialCenter, TrialRadius * 0.625f, -10) : (Vector3?)null; } }
            public override float CameraSize(float aspect)
            { return Mathf.Max(TrialRadius * 0.8f, (TrialRadius + KarmaSpace.Units(1)) / Mathf.Max(aspect, 0.01f)); }
            public override float ParryStunSeconds { get { return Settings.parryStunSeconds; } }
            public override float PatternTimeout { get { return owner.selected == EnemyAttack.GD05 ? 40 : owner.selected == EnemyAttack.GD03 ? 12 : 5; } }
            public override bool ConsumeQueuedParry() { return Heavy.ConsumeQueuedParry(); }
            public override void OnInterrupted()
            {
                owner.frontalGuard = false; owner.shieldWarningUntil = 0;
                if (owner.game.Player != null) owner.game.Player.ReleaseArena();
            }
            public override void OnParried()
            {
                owner.shieldUntil = 0;
                owner.nextShield = owner.FightSeconds + Mathf.Max(2, Settings.autoShieldPeriod);
            }
            public override void Tick() { shield.Tick(); }
            public override bool TrySelect(float distance, bool sight, bool escape, out EnemyAttack attack)
            {
                if (domeUses < 2 && owner.attacks.Contains(EnemyAttack.GD05)
                    && owner.Health <= owner.MaxHealth * (domeUses == 0 ? 0.5f : 0.25f))
                {
                    attack = EnemyAttack.GD05;
                    return true;
                }
                attack = default; float total = 0;
                foreach (var candidate in owner.attacks)
                {
                    if (!Eligible(candidate, distance, sight, escape)) continue;
                    float weight = candidate == EnemyAttack.GroundStrike ? Settings.Weights.basicWeight
                        : candidate == EnemyAttack.GD02 ? Settings.Weights.gd02Weight
                        : candidate == EnemyAttack.GD03 ? Settings.Weights.gd03Weight : Settings.Weights.gd04Weight;
                    if (candidate == owner.previous) weight *= Settings.Weights.repeatWeightScale;
                    weight = Mathf.Max(0, weight);
                    if (weight <= 0) continue;
                    total += weight;
                    if (total <= weight || Random.value * total < weight) attack = candidate;
                }
                return total > 0;
            }
            public override void OnSelected() { if (owner.selected == EnemyAttack.GD05) domeUses++; }
            public override void OnCompleted() { Heavy.Interrupt(); }
            public override Bounds DamageBounds(SpriteRenderer renderer) { return owner.animator.GuardDamageBounds(renderer); }
            public override void ContactResolved(EnemyAttack pattern, KarmaAttackContact contact, Vector2 point, bool parried, bool damaged)
            {
                if (parried && contact.ParryWithoutRiposte && Dome.Active) Dome.StunFromParry();
            }
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
