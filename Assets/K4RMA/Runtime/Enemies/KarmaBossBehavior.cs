using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Private collaborators can use the combat host without exposing mutable fields to game code.
        // Selection and movement are overridable; hit resolution and lifecycle remain in KarmaEnemy.
        struct BossAttackVisual
        {
            public string Pose, Sheet, ExtraPose;
            public bool Restrained, SeparateLaunch, ShieldBreak;
            public Color Color;
        }
        [System.AttributeUsage(System.AttributeTargets.Class)]
        sealed class BossKindAttribute : System.Attribute
        {
            public readonly Essence Kind;
            public BossKindAttribute(Essence kind) { Kind = kind; }
        }
        class BossBehavior
        {
            protected readonly KarmaEnemy owner;
            protected int cycleIndex;
            readonly Dictionary<EnemyAttack, BossPattern> patterns = new Dictionary<EnemyAttack, BossPattern>();
            public BossBehavior(KarmaEnemy owner)
            {
                this.owner = owner;
                Register(EnemyAttack.GroundStrike, new GroundStrikePattern(owner));
            }
            protected void Register(EnemyAttack attack, BossPattern pattern) { patterns[attack] = pattern; }
            protected bool PatternCanReach(EnemyAttack attack, Vector2 from, Vector2 target)
            { return patterns.TryGetValue(attack, out var pattern) && pattern.CanReach(from, target); }
            public virtual bool CanEngage { get { return owner.game.Player.Position.x >= 12; } }
            public virtual Sprite StunSprite { get { return KarmaArtwork.Sprite("HitVFX", "Burst"); } }
            public virtual float ParryStunSeconds { get { return 2; } }
            public virtual void Initialize() { }
            public virtual KarmaBossAttack CreateAttackComponent(GameObject target)
            { return target.AddComponent<KarmaBossAttack>(); }
            public virtual List<EnemyAttack> BuildCycle(IReadOnlyList<Essence> inherited)
            { return new List<EnemyAttack> { EnemyAttack.GroundStrike }; }
            public virtual bool IsUltimate(EnemyAttack attack) { return false; }
            public virtual float PatternTimeout { get { return 5; } }
            public virtual void OnParried() { }
            public virtual bool ConsumeQueuedParry() { return false; }
            public virtual void OnAttackParried(bool stun) { }
            public virtual void OnPlayerDamaged() { }
            public virtual void OnInterrupted() { }
            public virtual Vector2 WarningEndpoint(Vector2 from, Vector2 to) { return to; }
            public virtual bool OrientedWarnings { get { return false; } }
            public virtual bool GroundWarning { get { return false; } }
            public virtual BossAttackVisual AttackVisual(EnemyAttack attack, Vector2 velocity)
            { return new BossAttackVisual { Sheet = "GuardianVFX", Color = new Color(1, 0.38f, 0.12f) }; }
            public IEnumerator Execute(EnemyAttack attack)
            {
                if (!patterns.TryGetValue(attack, out var pattern)) throw new System.ArgumentOutOfRangeException(nameof(attack));
                return pattern.Execute();
            }
            public virtual bool TrySelect(float distance, bool sight, bool escape, out EnemyAttack attack)
            {
                for (int i = 0; i < owner.attacks.Count; i++)
                {
                    var candidate = owner.attacks[cycleIndex++ % owner.attacks.Count];
                    if (!Eligible(candidate, distance, sight, escape)) continue;
                    attack = candidate; return true;
                }
                attack = default; return false;
            }
            protected virtual bool Eligible(EnemyAttack attack, float distance, bool sight, bool escape)
            {
                return !IsUltimate(attack) || owner.FightSeconds >= owner.nextUltimate && !IsUltimate(owner.previous);
            }
            protected void Face(float dx)
            {
                owner.direction = dx < 0 ? -1 : 1;
                owner.art.localScale = new Vector3(owner.direction * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            }
            public virtual bool TickMovement(float dt, float dx)
            {
                Face(dx);
                if (Mathf.Abs(dx) > KarmaSpace.Units(3))
                owner.Move(Mathf.Sign(dx) * owner.game.Config.enemyMoveSpeed * owner.Tuning.moveMultiplier * Balance.movementScale * dt);
                return false;
            }
            public virtual void Tick() { }
            public virtual void OnDamaged() { }
            public virtual void ReducedDamageFeedback() { }
            public virtual Bounds DamageBounds(SpriteRenderer renderer) { return renderer.bounds; }
            public virtual void DamageFeedback(Vector2 point)
            {
                owner.animator.Hurt();
                KarmaVisuals.Flash(owner.game.World, owner.Position, Vector2.one * KarmaSpace.Units(0.7f), Color.white, 0.1f);
            }
            public virtual bool CustomHitVisual(EnemyAttack pattern) { return false; }
            public virtual void ContactStarting(EnemyAttack pattern, KarmaAttackContact contact) { }
            public virtual void ContactResolved(EnemyAttack pattern, KarmaAttackContact contact, Vector2 point, bool parried, bool damaged) { }
            public virtual bool Armored { get { return false; } }
            public virtual bool AutoShieldActive { get { return false; } }
            public virtual bool BlocksSelection { get { return false; } }
            public virtual bool ReduceDamage(Vector2 source) { return false; }
            public virtual Color ShieldColor { get { return owner.fullShield ? Color.white : KarmaArtwork.BarrierColor; } }
            public virtual void EndAction(bool keepAutoShield) { }
            public virtual void StopCombat() { }
            public virtual bool TryQueueParry() { return false; }
            public virtual Vector3? CameraFocus { get { return null; } }
            public virtual float CameraSize(float aspect) { return 0; }
            public virtual void OnSelected() { }
            public virtual void OnCompleted() { }
            public virtual void Interrupt(bool preservePressure)
            { foreach (var pattern in patterns.Values) pattern.Interrupt(); }
            public virtual Transform PersistentEffects { get { return null; } }
            protected KarmaBossBalance Settings { get { return Balance; } }
            static readonly KarmaBossBalance neutralBalance = new KarmaBossBalance();
            public virtual KarmaBossBalance Balance { get { return neutralBalance; } }
            public virtual float DecisionDelay { get { return owner.game.Config.enemyDecisionDelay * owner.Tuning.decisionMultiplier * owner.Tempo * Balance.decisionScale; } }
            public virtual float RetryDelay { get { return 0.2f; } }
        }
        static Dictionary<Essence, System.Type> behaviorTypes;
        BossBehavior CreateBehavior()
        {
            if (behaviorTypes == null)
            {
                behaviorTypes = new Dictionary<Essence, System.Type>();
                foreach (var type in typeof(KarmaEnemy).GetNestedTypes(System.Reflection.BindingFlags.NonPublic))
                {
                    var kind = (BossKindAttribute)System.Attribute.GetCustomAttribute(type, typeof(BossKindAttribute));
                    if (kind == null) continue;
                    if (!typeof(BossBehavior).IsAssignableFrom(type) || behaviorTypes.ContainsKey(kind.Kind))
                        throw new System.InvalidOperationException("Invalid or duplicate boss registration: " + type.Name);
                    behaviorTypes.Add(kind.Kind, type);
                }
            }
            return PrimaryType.HasValue && behaviorTypes.TryGetValue(PrimaryType.Value, out var registered)
                ? (BossBehavior)System.Activator.CreateInstance(registered, new object[] { this }) : new BossBehavior(this);
        }
    }
}
