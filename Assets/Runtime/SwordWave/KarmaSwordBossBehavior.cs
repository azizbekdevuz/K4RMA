using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
#if UNITY_EDITOR
        public bool SwordGroundPracticeActive => behavior is SwordBossBehavior sword
            && (sword.GroundPatternRunning || pendingParry || Stunned);
        public bool SwordCounterPracticeActive => behavior is SwordBossBehavior sword
            && (sword.CounterWardFormed || pendingParry || Stunned);
#endif

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
            public Bounds SwordPlayerBounds => owner.game.Player.DamageBounds;
            public float WaveLife { get { return Mathf.Clamp(Settings.swordLifetime, 0.5f, 2); } }
            public Vector2 BasicSwordSize { get { return SwordSize(EnemyAttack.SW01) * 0.90f; } }
            public float BasicSwordSpeed { get { return Mathf.Max(KarmaSpace.Scale, Settings.swordSpeed * 1.3f); } }
            // Casting cadence is independent of the faster projectile travel.
            public float BasicReleaseWait { get { return KarmaSpace.Units(5) / Mathf.Max(KarmaSpace.Units(5), Settings.swordSpeed * 0.8f); } }
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
                var shot = KarmaBossAttack.Spawn(owner.game, owner, persistentRoot, origin, velocity, size, life,
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
                if (ground.Running) return false;
                // SW05 is a once-per-health-gate set piece. Its two triggers
                // bypass ordinary cooldown/repeat rules after the current action.
                if (!ground.Running && owner.attacks.Contains(EnemyAttack.SW05) && ultimate.ThresholdPending && !counter.Formed)
                { next = EnemyAttack.SW05; return true; }
                // Prevent ordinary basics/wards from starving the inherited ground skill.
                // Keep health gates, active wards, reach and repeat protection authoritative.
                if (attacksSinceGround >= 3 && owner.previous != EnemyAttack.SW03
                    && owner.attacks.Contains(EnemyAttack.SW03)
                    && SwordCandidateAvailable(EnemyAttack.SW03, owner.Position, distance, sight, escape))
                { next = EnemyAttack.SW03; return true; }
                bool basic = Settings.Weights.swBasicWeight > 0 && owner.FightSeconds >= nextSwordBasic && sight && SwordAttackCanReach(EnemyAttack.SWBasic, owner.game.Player.Position);
                // Due basics and retreat follow-ups remain readable guaranteed responses.
                // Other ready basics join the same weighted pool as the eligible skills.
                if (!skillAfterRetreat && basic && consecutiveBasics < 2 && (swordBasicDue || basicAfterRetreat
                    || !counter.Formed && BasicMeleeCanReach(owner.Position, owner.game.Player.Position)))
                { next = EnemyAttack.SWBasic; return true; }
                bool repeatAvailable = false;
                var repeated = owner.previous;
                float total = !skillAfterRetreat && basic && consecutiveBasics < 2
                    ? Settings.Weights.swBasicWeight * (distance < KarmaSpace.Units(2.5f) ? Settings.Weights.closeBasicMultiplier : 1) : 0;
                if (total > 0) next = EnemyAttack.SWBasic;
                foreach (var candidate in owner.attacks)
                {
                    if (!SwordCandidateAvailable(candidate, owner.Position, distance, sight, escape)) continue;
                    // Keep immediate-repeat protection; use a repeat only if no other skill is available.
                    if (candidate == owner.previous) { repeated = candidate; repeatAvailable = true; continue; }
                    float weight = candidate == EnemyAttack.SW01 ? Settings.Weights.sw01Weight
                        : candidate == EnemyAttack.SW02 ? Settings.Weights.sw02Weight
                        : candidate == EnemyAttack.SW03 ? Settings.Weights.sw03Weight : Settings.Weights.sw04Weight;
                    weight = Mathf.Max(0, weight);
                    if (weight <= 0) continue;
                    total += weight;
                    if (total <= weight || Random.value * total < weight) next = candidate;
                }
                if (total > 0) return true;
                if (basic) { next = EnemyAttack.SWBasic; return true; }
                if (repeatAvailable && Settings.Weights.allowRepeatFallback) { next = repeated; return true; }
                return false;
            }
            bool SwordCandidateAvailable(EnemyAttack candidate, Vector2 from, float distance, bool sight, bool escape)
            {
                if (ground.Running) return false;
                if (candidate == EnemyAttack.SW05) return ultimate.ThresholdPending && !counter.Formed;
                if (candidate == EnemyAttack.SW01 && Settings.Weights.sw01Weight <= 0
                    || candidate == EnemyAttack.SW02 && Settings.Weights.sw02Weight <= 0
                    || candidate == EnemyAttack.SW03 && Settings.Weights.sw03Weight <= 0
                    || candidate == EnemyAttack.SW04 && Settings.Weights.sw04Weight <= 0) return false;
                if (candidate == EnemyAttack.SW04 && (counter.Formed || owner.FightSeconds < counter.NextReadyAt)) return false;
                // Keep the defensive overlay readable: its four committed counters
                // can accompany ordinary waves, while larger set pieces wait for it.
                if (counter.Formed && candidate != EnemyAttack.SW01 && candidate != EnemyAttack.SW02) return false;
                return Eligible(candidate, distance, sight, escape)
                && SwordAttackCanReachFrom(candidate, from, owner.game.Player.Position);
            }
            public bool EvasionOnly { get { return ground.Running; } }
            public bool HasSwordCandidate(Vector2 from)
            {
                if (ground.Running) return false;
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
            public void ChargeDash(float remaining) { movement.ChargeDash(remaining); }
            public void EndChargeDash() { movement.EndChargeDash(); }
            readonly SWBasicPattern basic;
            readonly SW04Pattern counter;
            readonly SW05Pattern ultimate;
            readonly SW03Pattern ground;
            // Frame the overhead command and the player's landing lanes as the caster ascends.
            public override Vector3? CameraFocus => ultimate.Framing
                ? new Vector3((owner.Position.x + owner.game.Player.Position.x) * 0.5f, KarmaSpace.Units(5.2f), -10) : null;
            public override float CameraSize(float aspect) => ultimate.Framing
                ? Mathf.Max(KarmaSpace.Units(8.2f),
                    (Mathf.Abs(owner.Position.x - owner.game.Player.Position.x) * 0.5f + KarmaSpace.Units(4)) / Mathf.Max(aspect, 0.01f))
                : 0;
            int swordSkillsSinceBasic;
            int attacksSinceGround;
            int consecutiveBasics;
            float nextSwordBasic;
            bool swordBasicDue = true, basicAfterRetreat, skillAfterRetreat;
            Transform persistentRoot;
            public Transform PersistentRoot { get { return persistentRoot; } }
#if UNITY_EDITOR
            public bool PracticeHold;
            public bool GroundPatternRunning => ground.Running;
            public bool CounterWardFormed => counter.Formed;
#endif
            KarmaSwordBossSettings Settings { get { return owner.Tuning.sword; } }
            public override bool PlaysOwnStunSound => stunFeedback != null && stunFeedback.Electrocuted;
            public override Sprite StunSprite { get { return PlaysOwnStunSound ? null
                : KarmaArtwork.Sprite("BossStun", "Wave") ?? base.StunSprite; } }
            public override KarmaBossBalance Balance { get { return Settings; } }
            [UnityEngine.Scripting.Preserve]
            public SwordBossBehavior(KarmaEnemy owner) : base(owner)
            {
                movement = new SwordBossMovement(owner, this);
                counter = new SW04Pattern(owner, this);
                basic = new SWBasicPattern(owner, this);
                Register(EnemyAttack.SWBasic, basic);
                Register(EnemyAttack.SW01, new SW01Pattern(owner, this));
                Register(EnemyAttack.SW02, new SW02Pattern(owner, this));
                ground = new SW03Pattern(owner, this);
                Register(EnemyAttack.SW03, ground);
                Register(EnemyAttack.SW04, counter);
                ultimate = new SW05Pattern(owner, this);
                Register(EnemyAttack.SW05, ultimate);
            }
            KarmaWaveMotion damageMotion;
            KarmaSwordStunFeedback stunFeedback;
            bool parryPendingBeforeContact;
            public override void Initialize()
            {
                persistentRoot = new GameObject("Sword persistent attacks").transform;
                persistentRoot.SetParent(owner.effectsRoot, false);
                owner.animator.ConfigureSwordPresentation();
                damageMotion = owner.art.gameObject.AddComponent<KarmaWaveMotion>();
                stunFeedback = owner.gameObject.AddComponent<KarmaSwordStunFeedback>();
                stunFeedback.Initialize(owner, owner.game, owner.art);
                movement.Initialize();
            }
            public override float ParryStunSeconds { get { return 2; } }
            public override void OnAttackParried(bool stun)
            {
                // A reflected SW04 blade owns the delayed hit. Undo only the
                // host flag from this contact, retaining an earlier queued stun.
                owner.pendingParry = owner.game.StageIndex == 3 || owner.running && owner.selected == EnemyAttack.SW05
                    ? false : parryPendingBeforeContact;
            }
            public void OnReturnedCounterHit()
            {
                if (owner.game.StageIndex == 3 || owner.running && owner.selected == EnemyAttack.SW05) return;
                if (!owner.Active || owner.Stunned || owner.pendingParry) return;
                // Use the host's stun transition next Update. Stop emissions and
                // live contacts now, including a separately owned SW04 volley.
                owner.pendingParry = true;
                owner.animator.CancelSwordBodyPresentation();
                stunFeedback.ArmElectrocution();
                owner.StopAllCoroutines();
                foreach (Transform effect in owner.effectsRoot)
                    effect.gameObject.SetActive(false);
            }
            public override Bounds DamageBounds(SpriteRenderer renderer)
            { return damageMotion != null ? damageMotion.GetDamageBounds(renderer) : renderer.bounds; }
            KarmaAttackContact finalDetonationContact;
            float finalDetonationUntil;
            public void RegisterFinalDetonation(KarmaAttackContact contact)
            {
                finalDetonationContact = contact;
                finalDetonationUntil = Time.time + KarmaSW05RainBlade.ExplosionActiveSeconds;
            }
            void ClearFinalDetonation() { finalDetonationContact = null; finalDetonationUntil = 0; }
            bool IsFinalDetonation(KarmaAttackContact contact)
                => contact != null && ReferenceEquals(contact, finalDetonationContact) && Time.time < finalDetonationUntil;
            public override bool TryPlayerCrowdControl(EnemyAttack pattern, KarmaAttackContact contact,
                out KarmaPlayer.CrowdControlKind kind, out float seconds)
            {
                kind = KarmaPlayer.CrowdControlKind.Shock; seconds = 0.75f;
                return pattern == EnemyAttack.SW04 || pattern == EnemyAttack.SW05 && IsFinalDetonation(contact);
            }
            public override void ContactStarting(EnemyAttack pattern, KarmaAttackContact contact)
            {
                parryPendingBeforeContact = owner.pendingParry;
                if (pattern == EnemyAttack.SW04) counter.TraceStarting(contact);
            }
            public override void ContactResolved(EnemyAttack pattern, KarmaAttackContact contact, Vector2 point, bool parried, bool damaged)
            {
                if (pattern == EnemyAttack.SW04)
                {
                    counter.TraceResolved(contact);
                    if (damaged)
                    {
                        KarmaSW05Energy.WardBurst(owner.effectsRoot,owner.game,point,Vector2.up,2.5f,1);
                    }
                    if (parried) { counter.OnParried(contact); }
                }
                bool sw04Shock = false;
                if (damaged && !parried)
                {
                    if (pattern == EnemyAttack.SW04)
                        sw04Shock = KarmaSwordElectrocution.ShowPlayer(owner.game, playShockSound: true);
                    else if (pattern == EnemyAttack.SW05 && IsFinalDetonation(contact))
                        KarmaSwordElectrocution.ShowPlayer(owner.game);
                }
                if (damaged && pattern != EnemyAttack.SW05 && pattern != EnemyAttack.SW03)
                {
                    if (pattern == EnemyAttack.SW01 || pattern == EnemyAttack.SW02)
                        KarmaSwordWaveAnimation.Impact(owner.game, owner, point, pattern == EnemyAttack.SW02);
                    else if (pattern == EnemyAttack.SWBasic) KarmaSwordBasicPresentation.Impact(owner.game, owner, point);
                    else KarmaSwordHitFeedback.ShowPlayerImpact(owner.game.World, point);
                }
                if (damaged && !parried && (pattern == EnemyAttack.SW01 || pattern == EnemyAttack.SW02
                    || pattern == EnemyAttack.SW04 && !sw04Shock || pattern == EnemyAttack.SWBasic && !BasicMeleeSelected))
                    KarmaAudio.Play(KarmaSfxId.SwWaveImpact);
            }
            public override bool Armored { get { return counter.Guarding; } }
            public override bool ReduceDamage(Vector2 source) { return counter.Guarding; }
            public override void ReducedDamageFeedback(bool basicMelee)
            {
                counter.GuardImpact();
                KarmaAudio.Play(KarmaSfxId.CombatGuardBlock);
            }
            // SW05 invulnerability is gameplay-only. SW04 owns its own dome.
            public override Color ShieldColor { get { return owner.fullShield || counter.Guarding
                ? Color.clear : base.ShieldColor; } }
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
            public override KarmaGuardianGrowth.Technique GrowthTechnique(Essence essence)
            {
                if (essence == Essence.Guard)
                    return new KarmaGuardianGrowth.Technique("Guard", "Guard", "Windup",
                        KarmaArtwork.Sprite("SwordBossVFX", "SW04_OrbitingSwordGuard_Base"),
                        KarmaGuardianGrowth.Motion.Surround, new Vector2(7, 7), realization: "검으로도 막을 수 있겠군.");
                if (essence == Essence.RisingSlash)
                    return new KarmaGuardianGrowth.Technique("Windup", "Rising", "Windup",
                        KarmaWaveArtwork.Sprite("SW03_RisingBlade_v5"),
                        KarmaGuardianGrowth.Motion.Rise, new Vector2(3.5f, 6), copy: "Rising", realization: "내 검을 솟구치게 한다면…");
                return new KarmaGuardianGrowth.Technique("Windup", "Wave", "Windup",
                    owner.game.Player.Essences.Count == 1 ? KarmaSwordWaveAnimation.BasicFlightDrawing
                        : KarmaWaveArtwork.Sprite("SW01_CrescentWave_v4"),
                    KarmaGuardianGrowth.Motion.Sweep, new Vector2(3.5f, 4.5f), copy: "Wave")
                    { FaceInIdle = new Vector2(0.623f, 0.886f) };
            }
            public override KarmaGuardianGrowth.Technique UltimateForeshadow => new KarmaGuardianGrowth.Technique(
                "Windup", "Guard", "Windup", KarmaArtwork.Sprite("SwordBossVFX", "SW05_HeavyJudgementSword_optional"),
                KarmaGuardianGrowth.Motion.Rise, new Vector2(2.8f, 6));
            public override string VictoryPose => "Recovery";
            public override string VictoryLine(int stage) => stage == 1 ? "그 움직임… 이제는 보인다."
                : stage == 2 ? "네가 준 힘, 내 검에 익었다." : "내 검이… 너를 넘어섰구나.";
            public override Sprite GrowthReflection => KarmaArtResource.First(owner.game.LastPersonalizedEssence == Essence.Guard
                ? "Motions12/Wave/Guard" : "SwordWave/Growth/Reflection");
            // The guard drawing holds the blade diagonally across the chest.
            public override Vector2 GrowthInspectionAnchor => owner.game.LastPersonalizedEssence == Essence.Guard
                ? new Vector2(0.85f, 4.95f) : new Vector2(1.4f, 2.8f);
            protected override bool Eligible(EnemyAttack attack, float distance, bool sight, bool escape)
            {
                if (ground.Running) return false;
                if (!base.Eligible(attack, distance, sight, escape)) return false;
                // Only travelling volleys need separation. SW03 now summons
                // ground blades across the arena and can also answer close pressure.
                if (attack == EnemyAttack.SW01) return HasWaveRoom(distance);
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
                        ExtraPose = "SWBasic_CrescentWave_v3", SeparateLaunch = true, Color = KarmaSwordAttackPolish.Blue };
                bool revised = attack == EnemyAttack.SWBasic || attack == EnemyAttack.SW01 || attack == EnemyAttack.SW03 || attack == EnemyAttack.SW04;
                return new BossAttackVisual { Sheet = "GuardianVFX", Color = KarmaSwordAttackPolish.Blue,
                    Restrained = true, SeparateLaunch = revised || attack == EnemyAttack.SWBasic || attack == EnemyAttack.SW02,
                    ExtraPose = KarmaWaveArtwork.Pose(attack, velocity.sqrMagnitude <= 0.01f) };
            }
            public void RequestBasic()
            {
                swordBasicDue = true;
                basicAfterRetreat = true;
                nextSwordBasic = Mathf.Min(nextSwordBasic, owner.FightSeconds + 0.12f);
            }
            public void RequestSkillAfterRetreat()
            { skillAfterRetreat = true; basicAfterRetreat = false; }
            public override bool TrySelect(float distance, bool sight, bool escape, out EnemyAttack attack)
            { return TrySelectSwordAttack(distance, sight, escape, out attack); }
            // Reserve one close-range follow-up before an unforced spacing
            // step. Real pressure, active retreats and wall escape still win.
            public bool CloseBasicReady => !ground.Running && !counter.Formed && !skillAfterRetreat
                && consecutiveBasics < 2 && Settings.Weights.swBasicWeight > 0
                && owner.FightSeconds >= nextSwordBasic
                && BasicMeleeCanReach(owner.Position, owner.game.Player.Position);
            public override bool TickMovement(float dt, float dx)
            {
#if UNITY_EDITOR
                if (PracticeHold) return true;
#endif
                if (!ground.Running && owner.attacks.Contains(EnemyAttack.SW05) && ultimate.ThresholdPending && !counter.Formed) return false;
                if (ground.Running)
                {
                    // Keep spacing live without handing the action slot to any
                    // attack. The next ordinary decision is due as soon as it ends.
                    movement.Tick(dt, dx); owner.timer = 0; return true;
                }
                return movement.Tick(dt, dx);
            }
            public override void OnSelected()
            {
                skillAfterRetreat = false;
                attacksSinceGround = owner.selected == EnemyAttack.SW03 ? 0 : Mathf.Min(3, attacksSinceGround + 1);
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
                ClearFinalDetonation();
                movement.OnCompleted();
                consecutiveBasics = owner.selected == EnemyAttack.SWBasic ? consecutiveBasics + 1 : 0;
                if (owner.selected != EnemyAttack.SWBasic)
                { swordSkillsSinceBasic++; swordBasicDue = swordSkillsSinceBasic >= Mathf.Clamp(Settings.swordSkillsBetweenBasics, 1, 6); }
            }
            public override float DecisionDelay { get { return IsUltimate(owner.selected) ? base.DecisionDelay : base.DecisionDelay * Mathf.Clamp(Settings.swordDecisionMultiplier, 0.25f, 1.5f); } }
            public override float RetryDelay { get { return Mathf.Max(0.05f, Settings.swordSelectionRetry); } }
            public override void Tick()
            {
                if (finalDetonationContact != null && Time.time >= finalDetonationUntil) ClearFinalDetonation();
                counter.Tick(); movement.ObservePressure();
            }
            public override void OnDamaged()
            {
                movement.RememberSwordPressure(); counter.OnDamaged();
            }
            public override Transform PersistentEffects { get { return persistentRoot; } }
            public override void Interrupt(bool preservePressure)
            {
                owner.animator.CancelSwordBodyPresentation();
                ClearFinalDetonation();
                base.Interrupt(preservePressure); movement.Interrupt(preservePressure);
                KarmaAudio.StopSlot("sw-bed");
                KarmaAudio.StopSlot("sw-pattern");
                KarmaAudio.StopSlot("sw-rain");
                KarmaAudio.StopSlot("sw-rain-impact");
                KarmaAudio.StopSlot("sw-summon");
                KarmaAudio.StopSlot("sw-ascension");
                KarmaAudio.StopSlot("sw-detonation");
                skillAfterRetreat = false;
                if (persistentRoot == null) return;
                foreach (Transform child in persistentRoot)
                { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                persistentRoot.gameObject.SetActive(true);
            }

        }
    }
}
