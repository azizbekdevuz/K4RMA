using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        [BossKind(Essence.SwordWave)]
        [UnityEngine.Scripting.Preserve]
        sealed class SwordBossBehavior : BossBehavior
        {
            public Vector2 WaveSize { get { return new Vector2(Mathf.Max(KarmaSpace.Units(0.2f), Settings.swordWidth), Mathf.Clamp(Settings.swordHeight, KarmaSpace.Units(0.2f), KarmaSpace.Units(0.8f))); } }
            public Vector2 SwordSize(EnemyAttack attack)
            {
                if (attack == EnemyAttack.SW02)
                {
                    // Bound the whole ordinary jump, including the player's real body.
                    // The head at its apex stays below the top of this single slash.
                    float gravity = Mathf.Max(0.01f, Mathf.Abs(Physics2D.gravity.y * owner.game.Config.gravityScale));
                    float jump = owner.game.Config.jumpSpeed * owner.game.Config.jumpSpeed / (2 * gravity);
                    float height = jump + SwordPlayerBounds.size.y + 0.6f - SW02Pattern.Floor;
                    return new Vector2(Mathf.Max(WaveSize.x * 1.35f * 1.7f, height * 0.6f), height);
                }
                if (attack == EnemyAttack.SW01) return SwordSize(EnemyAttack.SW02) * 0.425f;
                return WaveSize * (attack == EnemyAttack.SW03 ? new Vector2(1.35f, 1.30f) : Vector2.one * 1.30f);
            }
            public Bounds SwordPlayerBounds
            {
                get
                {
                    var collider = owner.game.Player.GetComponent<Collider2D>();
                    return collider != null && collider.enabled ? collider.bounds
                    : new Bounds(owner.game.Player.Position, KarmaSpace.PlayerSize);
                }
            }
            public float WaveLife { get { return Mathf.Clamp(Settings.swordLifetime, 0.5f, 2); } }
            public Vector2 BasicSwordSize { get { return WaveSize * new Vector2(0.9f, 1); } }
            public float BasicSwordSpeed { get { return Mathf.Max(KarmaSpace.Units(5), Settings.swordSpeed * 0.8f); } }
            public float BasicSwordLife { get { return KarmaSpace.Units(5) / BasicSwordSpeed; } }
            public float BasicWaveRange { get { return BasicSwordSpeed * BasicSwordLife; } }
            // Reuse the common opening ground slash's size and forward offset.
            public Vector2 BasicMeleeSize { get { return KarmaSpace.Units(new Vector2(3.3f, 1.8f)); } }
            public float BasicMeleeOffset { get { return KarmaSpace.Units(1.6f); } }
            public float BasicMeleeRange { get { return BasicMeleeOffset + BasicMeleeSize.x / 2 + SwordPlayerBounds.extents.x; } }
            public bool BasicMeleeSelected { get; private set; }
            public bool BasicMeleeCanReach(Vector2 from, Vector2 target)
            {
                var body = SwordPlayerBounds;
                Vector2 center = from + Vector2.right * (target.x < from.x ? -1 : 1) * BasicMeleeOffset;
                Vector2 point = target + (Vector2)body.center - owner.game.Player.Position;
                return KarmaBossGeometry.SweptBox(center.x, center.y, center.x, center.y, point.x, point.y,
                    BasicMeleeSize.x / 2 + body.extents.x, BasicMeleeSize.y / 2 + body.extents.y);
            }
            void SelectBasicMode()
            {
                float distance = Mathf.Abs(SwordPlayerBounds.center.x - owner.Position.x);
                BasicMeleeSelected = BasicMeleeCanReach(owner.Position, owner.game.Player.Position)
                    && (BasicMeleeSelected || distance <= BasicMeleeRange - Settings.swordMeleeHysteresis);
            }
            public KarmaBossAttack ReleaseSW01(Vector2 origin, Vector2 velocity, Vector2 size, float life, bool upper)
            {
                var visual = AttackVisual(EnemyAttack.SW01, velocity);
                var shot = KarmaBossAttack.Spawn(owner.game, owner, owner.effectsRoot, origin, velocity, size, life,
                    owner.Damage, visual.Color, null, 0, visual.Pose, visual.Restrained, true,
                    "SW01_CrescentWave_v4",
                    visual.Sheet, sourcePattern: EnemyAttack.SW01);
                KarmaSwordAttackPolish.Projectile(owner.game, owner, shot, velocity, size);
                return shot;
            }
            public void WaveLaunchVFX(Vector2 origin, Vector2 velocity, Vector2 size, bool aimed, bool upperHalf = false)
            {
                if (owner.selected == EnemyAttack.SWBasic || owner.selected == EnemyAttack.SW01 || owner.selected == EnemyAttack.SW02)
                {
                    KarmaSwordAttackPolish.Release(owner.game, owner, owner.art, origin, velocity, size, aimed);
                    return;
                }
                var sprite = KarmaArtwork.Sprite("GuardianVFX", aimed ? "Beam" : "Cleave");
                if (sprite == null) return;
                Vector2 axis = velocity.normalized;
                var bounds = sprite.bounds.size;
                float width = Mathf.Abs(axis.x) * bounds.x + Mathf.Abs(axis.y) * bounds.y;
                float height = Mathf.Abs(axis.y) * bounds.x + Mathf.Abs(axis.x) * bounds.y;
                float fit = Mathf.Min(size.x / width, size.y / height) * 0.7f;
                // Fit the rotated core/echo to this shot's footprint. No collider or damage;
                // ownership keeps the brief swing and existing afterimages cancellable.
                KarmaCombatVFX.Burst(owner.effectsRoot, origin, sprite, "Wave sword swing",
                new Vector2(bounds.x, bounds.y) * fit, 0.18f,
                Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg, new Color(1, 0.38f, 0.12f), owner.WaveVisuals);
            }
            public static Vector2 SwordAim(Vector2 origin, Vector2 target, float facing)
            {
                Vector2 aim = (target - origin).normalized;
                return aim.sqrMagnitude < 0.01f ? Vector2.right * facing : aim;
            }
            public bool SwordShotCanReach(Vector2 origin, Vector2 velocity, Vector2 size, float life, Vector2 target)
            {
                Vector2 end = origin + velocity * KarmaBossProjectileGeometry.SwordShotLife(origin, velocity, life);
                var axis = velocity.sqrMagnitude > 0.000001f ? velocity.normalized : Vector2.right;
                var player = SwordPlayerBounds;
                target += (Vector2)player.center - owner.game.Player.Position;
                return KarmaBossGeometry.SweptOrientedBox(origin.x, origin.y, end.x, end.y, target.x, target.y,
                axis.x, axis.y, size.x, size.y, player.extents.x, player.extents.y);
            }
            bool SwordAttackCanReachFrom(EnemyAttack candidate, Vector2 from, Vector2 target)
            { return PatternCanReach(candidate, from, target); }
            bool TrySelectSwordAttack(float distance, bool sight, bool escape, out EnemyAttack next)
            {
                next = owner.previous;
                // SW05 is a once-per-health-gate set piece. Its two triggers
                // bypass ordinary cooldown/repeat rules after the current action.
                if (owner.attacks.Contains(EnemyAttack.SW05) && ultimate.ThresholdPending && !counter.Formed)
                { next = EnemyAttack.SW05; return true; }
                bool basic = owner.FightSeconds >= nextSwordBasic && sight && SwordAttackCanReach(EnemyAttack.SWBasic, owner.game.Player.Position);
                // Broaden close-range basics without taking a ready SW05's inherited
                // cycle turn. The original due-basic / <2.5-unit priority stays intact.
                bool ultimateReady = owner.attacks.Contains(EnemyAttack.SW05)
                    && SwordCandidateAvailable(EnemyAttack.SW05, owner.Position, distance, sight, escape);
                if (basic && (consecutiveBasics < 2 || ultimateReady || basicAfterRetreat) && (swordBasicDue || distance < KarmaSpace.Units(2.5f)
                    || !ultimateReady && (BasicMeleeCanReach(owner.Position, owner.game.Player.Position)
                        || distance <= BasicWaveRange && consecutiveBasics == 0)))
                { next = EnemyAttack.SWBasic; return true; }
                bool repeatAvailable = false;
                for (int i = 0; i < owner.attacks.Count; i++)
                {
                    var candidate = owner.attacks[cycleIndex++ % owner.attacks.Count];
                    if (!SwordCandidateAvailable(candidate, owner.Position, distance, sight, escape)) continue;
                    // Keep the inherited cycle; repeat only when all other unlocked skills are ineligible.
                    if (candidate == owner.previous) { next = candidate; repeatAvailable = true; continue; }
                    next = candidate;
                    return true;
                }
                if (!repeatAvailable && basic) { next = EnemyAttack.SWBasic; return true; }
                return repeatAvailable;
            }
            bool SwordCandidateAvailable(EnemyAttack candidate, Vector2 from, float distance, bool sight, bool escape)
            {
                if (candidate == EnemyAttack.SW05) return ultimate.ThresholdPending && !counter.Formed;
                if (candidate == EnemyAttack.SW04 && (counter.Formed || owner.FightSeconds < counter.NextReadyAt)) return false;
                // Keep the defensive overlay readable: its four committed counters
                // can accompany ordinary waves, while larger set pieces wait for it.
                if (counter.Formed && candidate != EnemyAttack.SW01 && candidate != EnemyAttack.SW02) return false;
                return Eligible(candidate, distance, sight, escape)
                && SwordAttackCanReachFrom(candidate, from, owner.game.Player.Position);
            }
            public bool HasSwordCandidate(Vector2 from)
            {
                float distance = Mathf.Abs(owner.game.Player.Position.x - from.x);
                bool escape = from.x > KarmaStage.ArenaLeft + 5 && from.x < KarmaStage.ArenaRight - 5;
                bool sight = owner.ClearShot(from, owner.game.Player.Position);
                if (owner.FightSeconds >= nextSwordBasic && sight && SwordAttackCanReachFrom(EnemyAttack.SWBasic, from, owner.game.Player.Position)) return true;
                foreach (var candidate in owner.attacks)
                if (SwordCandidateAvailable(candidate, from, distance, sight, escape)) return true;
                return false;
            }
            bool SwordAttackCanReach(EnemyAttack candidate, Vector2 target)
            { return SwordAttackCanReachFrom(candidate, owner.Position, target); }
            readonly SwordBossMovement movement;
            readonly SW04Pattern counter;
            readonly SW05Pattern ultimate;
            int swordSkillsSinceBasic;
            int consecutiveBasics;
            float nextSwordBasic;
            bool swordBasicDue = true, basicAfterRetreat;
            new KarmaSwordBossSettings Settings { get { return owner.Tuning.sword; } }
            public override Sprite StunSprite { get { return KarmaArtwork.Sprite("BossStun", "Wave") ?? base.StunSprite; } }
            public override KarmaBossBalance Balance { get { return Settings; } }
            [UnityEngine.Scripting.Preserve]
            public SwordBossBehavior(KarmaEnemy owner) : base(owner)
            {
                movement = new SwordBossMovement(owner, this);
                counter = new SW04Pattern(owner, this);
                Register(EnemyAttack.SWBasic, new SWBasicPattern(owner, this));
                Register(EnemyAttack.SW01, new SW01Pattern(owner, this));
                Register(EnemyAttack.SW02, new SW02Pattern(owner, this));
                Register(EnemyAttack.SW03, new SW03Pattern(owner, this));
                Register(EnemyAttack.SW04, counter);
                ultimate = new SW05Pattern(owner, this);
                Register(EnemyAttack.SW05, ultimate);
            }
            KarmaWaveMotion damageMotion;
            public override void Initialize()
            {
                owner.animator.ConfigureSwordPresentation();
                damageMotion = owner.art.gameObject.AddComponent<KarmaWaveMotion>();
                movement.Initialize();
            }
            public override Bounds DamageBounds(SpriteRenderer renderer)
            { return damageMotion != null ? damageMotion.GetDamageBounds(renderer) : renderer.bounds; }
            public override void DamageFeedback(Vector2 point) { KarmaSwordHitFeedback.Show(owner.game.World, point, true); }
            public override bool CustomHitVisual(EnemyAttack pattern) { return pattern != EnemyAttack.SW05; }
            public override void ContactStarting(EnemyAttack pattern, KarmaAttackContact contact)
            { if (pattern == EnemyAttack.SW04) counter.TraceStarting(contact); }
            public override void ContactResolved(EnemyAttack pattern, KarmaAttackContact contact, Vector2 point, bool parried, bool damaged)
            {
                if (pattern == EnemyAttack.SW04)
                {
                    counter.TraceResolved(contact);
                    if (parried) counter.OnParried(contact);
                }
                if (damaged && CustomHitVisual(pattern) && pattern != EnemyAttack.SW03)
                    KarmaSwordHitFeedback.Show(owner.game.World, point, false);
            }
            public override bool Armored { get { return counter.Guarding; } }
            public override bool ReduceDamage(Vector2 source) { return counter.Guarding; }
            public override Color ShieldColor { get { return owner.fullShield ? Color.white
                : counter.Guarding ? new Color(0.42f, 0.68f, 1) : base.ShieldColor; } }
            public override List<EnemyAttack> BuildCycle(IReadOnlyList<Essence> inherited)
            {
                var owned = new HashSet<Essence>(inherited);
                var result = new List<EnemyAttack> { EnemyAttack.SW01, EnemyAttack.SW02 };
                if (owned.Contains(Essence.RisingSlash)) result.Add(EnemyAttack.SW03);
                if (owned.Contains(Essence.Guard)) result.Add(EnemyAttack.SW04);
                if (owned.Count == 3) result.Add(EnemyAttack.SW05);
                return result;
            }
            public override bool IsUltimate(EnemyAttack attack) { return attack == EnemyAttack.SW05; }
            protected override bool Eligible(EnemyAttack attack, float distance, bool sight, bool escape)
            {
                if (!base.Eligible(attack, distance, sight, escape)) return false;
                if (attack == EnemyAttack.SW01 || attack == EnemyAttack.SW03) return HasWaveRoom(distance);
                return attack != EnemyAttack.SW02 || sight;
            }
            public bool HasWaveRoom(float distance) { return distance >= KarmaSpace.Units(4); }
            public override Vector2 WarningEndpoint(Vector2 from, Vector2 to)
            { return from + (to - from) * KarmaBossProjectileGeometry.SwordShotLife(from, to - from, 1); }
            public override bool CanEngage { get { return base.CanEngage || Vector2.Distance(owner.Position, owner.game.Player.Position) <= KarmaSpace.Units(7); } }
            public override KarmaBossAttack CreateAttackComponent(GameObject target)
            { return target.AddComponent<KarmaSwordBossAttack>(); }
            public override bool OrientedWarnings { get { return true; } }
            public override bool GroundWarning { get { return owner.selected == EnemyAttack.SW05; } }
            public override BossAttackVisual AttackVisual(EnemyAttack attack, Vector2 velocity)
            {
                if (attack == EnemyAttack.SWBasic && BasicMeleeSelected && velocity.sqrMagnitude < 0.01f)
                    return new BossAttackVisual { Sheet = "GuardianVFX", Pose = "Cleave", Restrained = true,
                        ExtraPose = "SWBasic_CrescentWave_v3", SeparateLaunch = true, Color = KarmaSwordAttackPolish.Red };
                bool revised = attack == EnemyAttack.SWBasic || attack == EnemyAttack.SW01 || attack == EnemyAttack.SW03 || attack == EnemyAttack.SW04;
                return new BossAttackVisual { Sheet = "GuardianVFX", Color = attack == EnemyAttack.SW05 ? new Color(1, 0.38f, 0.12f) : KarmaSwordAttackPolish.Red,
                    Restrained = true, SeparateLaunch = revised || attack == EnemyAttack.SWBasic || attack == EnemyAttack.SW02,
                    ExtraPose = KarmaWaveArtwork.Pose(attack, velocity.sqrMagnitude <= 0.01f) };
            }
            public void RequestBasic()
            {
                swordBasicDue = true;
                basicAfterRetreat = true;
                nextSwordBasic = Mathf.Min(nextSwordBasic, owner.FightSeconds + 0.12f);
            }
            public override bool TrySelect(float distance, bool sight, bool escape, out EnemyAttack attack)
            { return TrySelectSwordAttack(distance, sight, escape, out attack); }
            public override bool TickMovement(float dt, float dx)
            {
                if (owner.attacks.Contains(EnemyAttack.SW05) && ultimate.ThresholdPending && !counter.Formed) return false;
                return movement.Tick(dt, dx);
            }
            public override void OnSelected()
            {
                movement.OnSelected();
                if (owner.selected == EnemyAttack.SWBasic)
                {
                    SelectBasicMode();
                    float cooldown = BasicMeleeSelected ? Settings.meleeBasicCooldown : Settings.waveBasicCooldown;
                    nextSwordBasic = owner.FightSeconds + Mathf.Max(0.5f, Mathf.Min(Settings.swordBasicCooldown, cooldown));
                    swordBasicDue = false; swordSkillsSinceBasic = 0;
                    basicAfterRetreat = false;
                }
            }
            public override void OnCompleted()
            {
                movement.OnCompleted();
                consecutiveBasics = owner.selected == EnemyAttack.SWBasic ? consecutiveBasics + 1 : 0;
                if (owner.selected != EnemyAttack.SWBasic)
                { swordSkillsSinceBasic++; swordBasicDue = swordSkillsSinceBasic >= Mathf.Clamp(Settings.swordSkillsBetweenBasics, 1, 6); }
            }
            public override float DecisionDelay { get { return IsUltimate(owner.selected) ? base.DecisionDelay : base.DecisionDelay * Mathf.Clamp(Settings.swordDecisionMultiplier, 0.25f, 1.5f); } }
            public override float RetryDelay { get { return Mathf.Max(0.05f, Settings.swordSelectionRetry); } }
            public override void Tick() { counter.Tick(); movement.ObservePressure(); }
            public override void OnDamaged()
            {
                movement.RememberSwordPressure(); counter.OnDamaged();
                // Confirm the health change in LateUpdate without reserving Hurt
                // or touching the committed attack coroutine. SW05 stays intact.
                if (owner.selected != EnemyAttack.SW05)
                {
                    var flash = owner.art.GetComponent<KarmaSwordDamageFlash>();
                    if (flash == null) flash = owner.art.gameObject.AddComponent<KarmaSwordDamageFlash>();
                    flash.Queue(owner);
                }
            }
            public override Transform PersistentEffects { get { return counter.EffectsRoot; } }
            public void CounterParried(KarmaAttackContact contact) { counter.OnParried(contact); }
            public void CounterContactStarting(KarmaAttackContact contact) { counter.TraceStarting(contact); }
            public void CounterContactResolved(KarmaAttackContact contact) { counter.TraceResolved(contact); }
            public override void Interrupt(bool preservePressure)
            { base.Interrupt(preservePressure); movement.Interrupt(preservePressure); }

        }
    }
}
