using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        [BossKind(Essence.RisingSlash)]
        [UnityEngine.Scripting.Preserve]
        sealed class RisingBossBehavior : BossBehavior
        {
#if UNITY_EDITOR
            public bool PracticeHold;
#endif
            new KarmaRisingBossSettings Settings { get { return owner.Tuning.rising; } }
            public override Sprite StunSprite { get { return KarmaArtwork.Sprite("RisingStunStars", "Frame00"); } }
            public override KarmaBossBalance Balance { get { return Settings; } }
            float nextStride, nextAirPursuit;
            KarmaRisingBarrier barrier;
            public override bool ReduceDamage(Vector2 source) => barrier != null && barrier.Formed;
            public void BeginBarrier(float seconds)
            {
                if (barrier == null) barrier = KarmaRisingBarrier.Begin(owner, seconds);
            }
            public void ClearBarrier()
            {
                if (barrier != null) barrier.End();
                barrier = null;
            }
            Transform fragmentsRoot;
            public override Transform PersistentEffects => fragmentsRoot;
            public Transform FragmentsRoot
            {
                get
                {
                    if (fragmentsRoot == null)
                    {
                        fragmentsRoot = new GameObject("Rising landing fragments").transform;
                        fragmentsRoot.SetParent(owner.effectsRoot, false);
                    }
                    return fragmentsRoot;
                }
            }
            void ClearFragments()
            {
                if (fragmentsRoot == null) return;
                foreach (Transform child in fragmentsRoot)
                { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            }
            public override void Interrupt(bool preservePressure)
            {
                base.Interrupt(preservePressure);
                ClearBarrier();
                owner.animator.EndGuardianAction();
                ClearFragments();
                motionContact = null;
                KarmaRisingAudio.StopPattern();
            }
            int consecutiveSkills;
            bool usedHalfHealthUltimate, usedQuarterHealthUltimate;
            bool UltimateDue => (!usedHalfHealthUltimate && owner.Health <= owner.MaxHealth * 0.5f)
                || (!usedQuarterHealthUltimate && owner.Health <= owner.MaxHealth * 0.25f);
            public bool GuardEmpowered { get; private set; }
            KarmaAttackContact motionContact;
            KarmaSfxId motionImpact, resolvingImpact;
            bool knockbackSoundPending;
            float knockbackLeft, knockbackDirection;

            public IEnumerator MotionWithImpact(Vector2 target, float seconds, Vector2 size, Sprite trail, KarmaSfxId impact)
            {
                var contact = new KarmaAttackContact();
                motionContact = contact;
                motionImpact = impact;
                try
                {
                    var motion = owner.Motion(target, seconds, size, contact, trail);
                    while (motion.MoveNext()) yield return motion.Current;
                }
                finally
                {
                    if (motionContact == contact) motionContact = null;
                }
            }
            [UnityEngine.Scripting.Preserve]
            public RisingBossBehavior(KarmaEnemy owner) : base(owner)
            {
                Register(EnemyAttack.GroundStrike, new UPBasicPattern(owner));
                Register(EnemyAttack.UP01, new UP01Pattern(owner));
                Register(EnemyAttack.UP02, new UP02Pattern(owner));
                Register(EnemyAttack.UP03, new UP03Pattern(owner));
                Register(EnemyAttack.UP04, new UP04Pattern(owner));
                Register(EnemyAttack.UP05, new UP05Pattern(owner));
            }
            public override void Initialize()
            {
                owner.animator.ConfigureRisingPresentation();
                // The rising boss uses its golden sphere instead of the shared white halo.
                owner.shieldHalo.gameObject.SetActive(false);
                owner.gameObject.AddComponent<KarmaRisingStunStars>().Initialize(owner.game,
                    owner.art.GetComponentInChildren<SpriteRenderer>(), () => owner.stunCue);
            }
            public override List<EnemyAttack> BuildCycle(IReadOnlyList<Essence> inherited)
            {
                var owned = new HashSet<Essence>(inherited);
                var result = new List<EnemyAttack> { EnemyAttack.GroundStrike, EnemyAttack.UP01, EnemyAttack.UP02 };
                if (owned.Contains(Essence.SwordWave)) result.Add(EnemyAttack.UP03);
                GuardEmpowered = owned.Contains(Essence.Guard);
                if (owned.Count == 3) result.Add(EnemyAttack.UP05);
                return result;
            }
            public override bool TrySelect(float distance, bool sight, bool escape, out EnemyAttack attack)
            {
                if (owner.attacks.Contains(EnemyAttack.UP05) && UltimateDue)
                { attack = EnemyAttack.UP05; return true; }
                if (owner.game.Player.Airborne && owner.FightSeconds >= nextAirPursuit
                    && owner.attacks.Contains(EnemyAttack.UP02) && owner.previous != EnemyAttack.UP02)
                { attack = EnemyAttack.UP02; return true; }
                if (consecutiveSkills >= 1 && Settings.Weights.basicWeight > 0
                    && Eligible(EnemyAttack.GroundStrike, distance, sight, escape))
                { attack = EnemyAttack.GroundStrike; return true; }
                attack = default; float total = 0;
                foreach (var candidate in owner.attacks)
                {
                    if (!Eligible(candidate, distance, sight, escape)) continue;
                    float weight = candidate == EnemyAttack.GroundStrike ? Settings.Weights.basicWeight
                        : candidate == EnemyAttack.UP01 ? Settings.Weights.up01Weight
                        : candidate == EnemyAttack.UP02 ? Settings.Weights.up02Weight * (owner.game.Player.Airborne ? Settings.Weights.airborneUp02Multiplier : 1)
                        : candidate == EnemyAttack.UP03 ? Settings.Weights.up03Weight
                        : candidate == EnemyAttack.UP04 ? Settings.Weights.up04Weight : Settings.Weights.up05Weight;
                    if (candidate == owner.previous)
                        weight *= candidate == EnemyAttack.GroundStrike ? 0.75f : Settings.Weights.repeatWeightScale;
                    weight = Mathf.Max(0, weight);
                    if (weight <= 0) continue;
                    total += weight;
                    if (total <= weight || Random.value * total < weight) attack = candidate;
                }
                return total > 0;
            }
            public override KarmaBossAttack CreateAttackComponent(GameObject target)
                => target.AddComponent<KarmaRisingFragmentAttack>();
            public void FragmentReturned(Vector2 point)
            {
                if (!owner.Active || owner.fullShield) return;
                owner.pendingParry = true;
                KarmaRisingPolish.ParryBreak(owner.effectsRoot, point);
            }
            public override void ContactStarting(EnemyAttack pattern, KarmaAttackContact contact)
            {
                // Lethal damage can cancel the pattern before ContactResolved runs.
                resolvingImpact = contact == motionContact ? motionImpact : KarmaSfxId.CombatBladeHit;
            }
            public override void ContactResolved(EnemyAttack pattern, KarmaAttackContact contact, Vector2 point, bool parried, bool damaged)
            {
                if (parried && contact.ParryWithoutRiposte && fragmentsRoot != null)
                    foreach (var shard in fragmentsRoot.GetComponentsInChildren<KarmaRisingFragmentAttack>())
                        if (shard.Matches(contact)) { shard.Reflect(); break; }
                bool knockback = GuardEmpowered && pattern == EnemyAttack.GroundStrike && damaged && !parried;
                if (knockback)
                {
                    knockbackDirection = owner.game.Player.Position.x < owner.Position.x ? -1 : 1;
                    knockbackLeft = 0.16f;
                    owner.game.Player.Stun(0.16f);
                    knockbackSoundPending = owner.Active;
                }
                if (parried || !damaged) return;
                KarmaAudio.Play(resolvingImpact);
            }
            public override void Tick()
            {
                if (knockbackLeft <= 0 || owner.game.Paused) return;
                float dt = Mathf.Min(Time.deltaTime, knockbackLeft);
                knockbackLeft -= dt;
                var player = owner.game.Player;
                var body = player.GetComponent<Rigidbody2D>();
                float travel = KarmaSpace.Units(12f) * dt;
                var axis = Vector2.right * knockbackDirection;
                KarmaPhysicsQueries.BoxCast(player.Position, KarmaSpace.PlayerSize, axis, travel, owner.terrainHits);
                foreach (var hit in owner.terrainHits)
                    if (!hit.collider.isTrigger && hit.collider.GetComponent<KarmaSurface>() != null && Vector2.Dot(hit.normal, axis) < -0.01f)
                        travel = Mathf.Min(travel, Mathf.Max(0, hit.distance - 0.02f));
                var target = player.Position + axis * travel;
                target.x = Mathf.Clamp(target.x, KarmaStage.ArenaLeft + KarmaSpace.PlayerHalfWidth, KarmaStage.ArenaRight - KarmaSpace.PlayerHalfWidth);
                if (knockbackSoundPending && !Mathf.Approximately(target.x, player.Position.x))
                {
                    knockbackSoundPending = false;
                    KarmaRisingAudio.Knockback();
                }
                body.position = target;
                player.transform.position = target;
            }
            public override void StopCombat()
            {
                knockbackLeft = 0;
                knockbackSoundPending = false;
                ClearBarrier();
                ClearFragments();
                motionContact = null;
                KarmaRisingAudio.StopPattern();
            }
            public override void OnSelected()
            {
                if (owner.selected == EnemyAttack.UP02) nextAirPursuit = owner.FightSeconds + 1.8f;
                if (owner.selected == EnemyAttack.GroundStrike) consecutiveSkills = 0;
                else consecutiveSkills++;
                if (owner.selected != EnemyAttack.UP05) return;
                // Consume one threshold per cast, including when damage crosses both at once.
                if (!usedHalfHealthUltimate) usedHalfHealthUltimate = true;
                else usedQuarterHealthUltimate = true;
            }
            public override bool IsUltimate(EnemyAttack attack) { return attack == EnemyAttack.UP05; }
            public override void OnAttackParried(bool stun)
            {
                if (stun) KarmaRisingPolish.ParryBreak(owner.effectsRoot, owner.game.Player.Position);
            }
            public override void OnParried() { KarmaRisingAudio.Stagger(); }
            public override float ParryStunSeconds { get { return Settings.fragmentParryStunSeconds; } }
            public override bool TickMovement(float dt, float dx)
            {
#if UNITY_EDITOR
                if (PracticeHold) return true;
#endif
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
                if (attack == EnemyAttack.UP05) return UltimateDue;
                if (!base.Eligible(attack, distance, sight, escape)) return false;
                if (attack == EnemyAttack.GroundStrike)
                    return sight && !owner.game.Player.Airborne;
                if (attack == EnemyAttack.UP01) return owner.Position.y > KarmaSpace.GuardianGroundY + KarmaSpace.Units(0.5f);
                if (attack == EnemyAttack.UP02) return owner.game.Player.Airborne && owner.previous != attack
                    && owner.FightSeconds >= nextAirPursuit;
                return attack != EnemyAttack.UP03 || escape;
            }
            public override float DecisionDelay { get { return Mathf.Max(0.26f, base.DecisionDelay * Settings.attackDecisionMultiplier); } }

        }
    }
}
