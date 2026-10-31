using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        int cycleIndex, swordSkillsSinceBasic;
        float nextSwordBasic, nextSwordRetreat, swordRetreatRemaining, swordRetreatUntil, swordRetreatDirection;
        bool swordBasicDue, swordAttackedSinceRetreat = true, swordUltimateDeferred;
        bool swordApproaching;
        float nextSwordApproach, swordApproachUntil;
        bool swordPressurePending;
        float swordRetreatTarget, swordPressureTarget;
        float SwordSpacingDistance()
        {
            // Use the incoming-hit silhouette, including its asymmetric pivot.
            // Movement/idle safety and basic-wave reach are different bands.
            var body = DamageBounds;
            float front = game.Player.Position.x < Position.x ? Position.x - body.min.x : body.max.x - Position.x;
            return Mathf.Max(Mathf.Clamp(Tuning.swordRetreatStopDistance, 9, 14),
                game.Config.Techniques.longSlashReach + Mathf.Max(0, front)
                + Mathf.Clamp(Tuning.swordMeleeSpacingMargin, 0.25f, 1.25f));
        }
        internal void RememberSwordPressure()
        {
            if (PrimaryType != Essence.SwordWave || !Active) return;
            swordPressurePending = true;
            // Capture before Hurt changes the pose. A smaller next pose must
            // not erase a real hit or shrink its requested separation.
            swordPressureTarget = Mathf.Max(swordPressureTarget, SwordSpacingDistance());
        }
        float SwordDecisionScale
        { get { return PrimaryType == Essence.SwordWave ? Mathf.Clamp(Tuning.swordDecisionMultiplier, 0.25f, 1.5f) : 1; } }
        float DecisionDelay(EnemyAttack completed)
        {
            float delay = game.Config.enemyDecisionDelay * Tuning.decisionMultiplier * Tempo;
            if (!KarmaPatternCatalog.IsUltimate(completed)) delay *= SwordDecisionScale;
            return Mathf.Max(0.22f, delay);
        }
        void Update()
        {
            if (game == null || stopped) return;
            if (game.Paused) return;
            if (!Active) { CancelCombat(); return; }
            if (!engaged)
            {
                bool swordInRange = PrimaryType == Essence.SwordWave
                    && Vector2.Distance(Position, game.Player.Position) <= KarmaSpace.Units(7);
                if (game.Player.Position.x < 12 && !swordInRange) { Intent = "앞으로 나아가 시험을 시작하세요"; return; }
                engaged = true;
            }
            float dt = Time.deltaTime; FightSeconds += dt;
            if (swordGuard && FightSeconds >= swordGuardUntil) swordGuard = false;
            if (Enraged && !announcedEnrage)
            {
                announcedEnrage = true;
                KarmaCombatVFX.SpawnSprite(effectsRoot, Position, "PersonalizationVFX", "Fusion", KarmaSpace.Units(new Vector2(4, 4)), 0.7f);
            }
            if (pendingParry)
            {
                pendingParry = false; InterruptPatternCore(true);
                state = ActionState.Stunned; stunnedUntil = FightSeconds + Mathf.Max(0.1f, Tuning.parryStunSeconds);
                Intent = "패링 성공 · 방어막 파괴 / 무력화";
            }
            UpdateAutoShield();
            shieldHalo.enabled = Armored || shieldWarningUntil > 0;
            shieldHalo.color = shieldWarningUntil > 0
                ? new Color(1, 0.8f, 0.2f, 0.4f + 0.4f * Mathf.Abs(Mathf.Sin(FightSeconds * 8)))
                : fullShield ? Color.white : swordGuard ? KarmaArtwork.BossSwordColor : KarmaArtwork.BarrierColor;
            animator.GuardianPose(state.ToString(), selected, Position.y > KarmaSpace.Units(1.5f), Armored);
            if (Stunned)
            {
                if (FightSeconds < stunnedUntil) return;
                state = ActionState.Approach; timer = 0.8f;
            }
            if (PrimaryType == Essence.SwordWave)
            {
                // Remember an actual melee threat while an action owns the root.
                // This only requests a later idle decision: never cancel a
                // warning, attack or recovery in response to a player swing.
                if (game.Player.SwordPressureActive && game.Player.SwordAreaOverlaps(this)) RememberSwordPressure();
                else if (Mathf.Abs(game.Player.Position.x - Position.x)
                    > Mathf.Max(swordPressureTarget, SwordSpacingDistance()) + 0.25f)
                { swordPressurePending = false; swordPressureTarget = 0; }
            }
            if (running)
            {
                if (FightSeconds - patternStarted > Mathf.Max(5, Tuning.patternTimeout) || Position.y < KarmaSpace.Units(-2) || Position.y > KarmaSpace.Units(12))
                { InterruptPatternCore(true); state = ActionState.Approach; timer = 1.2f; Intent = "자세 복구 · 빈틈"; }
                return;
            }
            float dx = game.Player.Position.x - Position.x;
            if (PrimaryType == Essence.SwordWave)
            {
                // Hit-stop pauses movement; a zero step is not a blocked path.
                if (dt <= 0) return;
                // Keep facing the player while backing away. A small crossing
                // dead zone avoids image flips at overlapping player centers.
                if (Mathf.Abs(dx) > 0.5f) direction = Mathf.Sign(dx);
                if (direction == 0) direction = art.localScale.x < 0 ? -1 : 1;
                art.localScale = new Vector3(direction * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                // Only an actually needed spacing action owns this idle slot.
                // It completes before selection; a long SW02 reach must not
                // preempt the first approach frame. Warnings/recovery above
                // remain stationary, including SW05's own central movement.
                if (RetreatSword(dt, dx) || ApproachSword(dt, dx))
                {
                    animator.ShowGuardianSpacing();
                    return;
                }
            }
            else
            {
                art.localScale = new Vector3((dx < 0 ? -1 : 1) * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                float preferred = KarmaSpace.Units(PrimaryType == Essence.Guard ? 3 : 3.5f);
                if (Mathf.Abs(dx) > preferred)
                    Move(Mathf.Sign(dx) * game.Config.enemyMoveSpeed * Tuning.moveMultiplier * dt);
            }
            timer -= dt;
            if (timer <= 0) BeginAttack();
        }
        bool RetreatSword(float dt, float dx)
        {
            if (swordRetreatRemaining <= 0)
            {
                // SW01's existing minimum range is wider than a close SW02.
                // After using SW02, make room once if that rule still excludes
                // SW01; keep its attack range and the inherited cycle intact.
                bool waveRoom = previous == EnemyAttack.SW02 && attacks.Contains(EnemyAttack.SW01)
                    && !KarmaPatternCatalog.CanSelect(EnemyAttack.SW01, previous, Mathf.Abs(dx) / KarmaSpace.Scale,
                        game.Player.Airborne, true, false, true, true);
                float safeGap = Mathf.Max(swordPressureTarget, SwordSpacingDistance());
                bool pressure = swordPressurePending && Mathf.Abs(dx) < safeGap;
                // React before the player has reached the wide melee body.
                // The completed-attack token and cooldown still limit escapes.
                bool nearMelee = Mathf.Abs(dx) < Mathf.Max(Tuning.swordRetreatStartDistance,
                    safeGap - Mathf.Clamp(Tuning.swordMeleeSpacingMargin, 0.25f, 1.25f));
                if ((!waveRoom && !pressure && !nearMelee)
                    || FightSeconds < nextSwordRetreat || !swordAttackedSinceRetreat || game.Player.ContinuousSwordAttack)
                    return false;
                swordApproaching = false;
                swordPressurePending = false;
                swordPressureTarget = 0;
                swordRetreatTarget = safeGap;
                swordRetreatDirection = dx < 0 ? 1 : -1;
                swordRetreatRemaining = Mathf.Clamp(Tuning.swordRetreatDistance, 2, 18);
                swordRetreatUntil = FightSeconds + Mathf.Clamp(Tuning.swordRetreatDuration, 0.35f, 0.75f);
                nextSwordRetreat = FightSeconds + Mathf.Max(4.5f, Tuning.swordRetreatCooldown);
                // Even a blocked attempt yields to an attack before retrying.
                swordAttackedSinceRetreat = false;
            }
            float stop = swordRetreatTarget;
            if (FightSeconds >= swordRetreatUntil || dx * swordRetreatDirection >= 0 || Mathf.Abs(dx) >= stop)
            { FinishSwordRetreat(); return false; }
            // A short burst can gain ground on pursuit; ordinary walking was
            // slower than the player and reduced the gap despite moving away.
            float speed = Mathf.Min(Mathf.Max(1, Tuning.swordRetreatSpeed), game.Config.moveSpeed * 2.5f);
            float step = swordRetreatDirection * Mathf.Min(Mathf.Min(swordRetreatRemaining, stop - Mathf.Abs(dx)), speed * dt);
            float travelled = MoveSwordOnGround(step);
            if (travelled <= 0.0001f) { FinishSwordRetreat(); return false; }
            swordRetreatRemaining = Mathf.Max(0, swordRetreatRemaining - travelled);
            if (swordRetreatRemaining <= 0.001f) FinishSwordRetreat();
            Intent = "거리 유지 · 짧은 후퇴 후 공격";
            return true;
        }
        void FinishSwordRetreat()
        {
            swordRetreatRemaining = 0;
            swordRetreatTarget = 0;
            // A new hit during this move remains queued behind the cooldown;
            // starting the move already consumed the previous request.
            swordBasicDue = true;
            timer = Mathf.Max(timer, 0.18f);
            nextSwordApproach = FightSeconds + Mathf.Max(0.25f, timer + 0.05f);
        }
        bool ApproachSword(float dt, float dx)
        {
            float distance = Mathf.Abs(dx), preferred = Mathf.Max(Mathf.Clamp(Tuning.swordPreferredDistance, 9, 14), SwordSpacingDistance());
            if (distance <= preferred)
            {
                if (swordApproaching) FinishSwordApproach();
                return false;
            }
            if (!swordApproaching)
            {
                if (FightSeconds < nextSwordApproach) return false;
                if (distance <= Mathf.Max(Tuning.swordApproachStartDistance, preferred + 1) && HasSwordCandidate(Position)) return false;
                swordApproaching = true;
                swordApproachUntil = FightSeconds + 1.2f;
            }
            // A faster fleeing player must not postpone attacks indefinitely.
            // A reachable ranged attack gets a turn after this short approach.
            if (FightSeconds >= swordApproachUntil && HasSwordCandidate(Position))
            { FinishSwordApproach(); return false; }
            float speed = Mathf.Min(game.Config.enemyMoveSpeed * Tuning.moveMultiplier, game.Config.moveSpeed * 0.8f);
            float step = Mathf.Sign(dx) * Mathf.Min(distance - preferred, speed * dt);
            if (MoveSwordOnGround(step) <= 0.0001f)
            {
                FinishSwordApproach();
                return false;
            }
            Intent = "거리 유지 · 유효 사거리로 접근";
            return true;
        }
        void FinishSwordApproach()
        {
            swordApproaching = false;
            nextSwordApproach = FightSeconds + Mathf.Max(0.25f, timer + 0.05f);
        }
        float MoveSwordOnGround(float step)
        {
            float before = Position.x;
            // A boss already inside the wall margin can walk back out of it,
            // but clamping must never reverse or enlarge the requested step.
            float left = Mathf.Min(before, KarmaStage.BossLeftWall + 0.5f);
            float right = Mathf.Max(before, KarmaStage.BossRightWall - 0.5f);
            float after = Mathf.Clamp(before + step, left, right);
            step = after - before;
            if (Mathf.Abs(step) <= 0.0001f) return 0;
            foreach (var hit in Physics2D.BoxCastAll(Position, KarmaSpace.Units(new Vector2(0.8f, 1.6f)), 0,
                Vector2.right * Mathf.Sign(step), Mathf.Abs(step) + 0.1f))
                if (hit.collider.GetComponent<KarmaSurface>() != null && hit.collider.GetComponent<PlatformEffector2D>() == null)
                    return 0;
            bool floor = false;
            foreach (var hit in Physics2D.RaycastAll(new Vector2(after, Position.y), Vector2.down, KarmaSpace.GuardianGroundY + 0.5f))
                if (hit.collider.GetComponent<KarmaSurface>() != null) { floor = true; break; }
            if (!floor) return 0;
            Move(step);
            return Mathf.Abs(Position.x - before);
        }
        void BeginAttack()
        {
            float distance = Mathf.Abs(game.Player.Position.x - Position.x);
            bool escape = Position.x > KarmaStage.ArenaLeft + 5 && Position.x < KarmaStage.ArenaRight - 5;
            bool sight = ClearShot(Position, game.Player.Position);
            if (PrimaryType == Essence.SwordWave)
            {
                EnemyAttack next;
                if (!TrySelectSwordAttack(distance, sight, escape, out next))
                {
                    // Approach continues while range or sight prevents every unlocked skill.
                    // Do not substitute a different type's fallback attack or poll every rendered frame.
                    timer = Mathf.Max(0.05f, Tuning.swordSelectionRetry);
                    return;
                }
                selected = next;
            }
            else
            {
                selected = EnemyAttack.GroundStrike;
                for (int i = 0; i < attacks.Count; i++)
                {
                    var candidate = attacks[cycleIndex++ % attacks.Count];
                    if (!KarmaPatternCatalog.CanSelect(candidate, previous, distance / KarmaSpace.Scale, game.Player.Airborne,
                        sight, FightSeconds < shieldUntil, escape, FightSeconds >= nextUltimate)) continue;
                    selected = candidate; break;
                }
            }
            previous = selected;
            if (selected == EnemyAttack.SW05) swordUltimateDeferred = false;
            swordRetreatRemaining = 0;
            swordApproaching = false;
            if (selected == EnemyAttack.SWBasic)
            {
                nextSwordBasic = FightSeconds + Mathf.Max(0.5f, Tuning.swordBasicCooldown);
                swordBasicDue = false;
                swordSkillsSinceBasic = 0;
            }
            if (KarmaPatternCatalog.IsUltimate(selected)) nextUltimate = FightSeconds + Mathf.Max(10, Tuning.ultimateCooldown);
            direction = game.Player.Position.x < Position.x ? -1 : 1;
            art.localScale = new Vector3(direction * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            running = true; patternStarted = FightSeconds;
            Intent = KarmaPatternCatalog.Label(selected);
            StartCoroutine(RunPattern());
        }
        bool TrySelectSwordAttack(float distance, bool sight, bool escape, out EnemyAttack next)
        {
            next = previous;
            // A ward can overlap several short actions. Remember a ready
            // ultimate skipped by that overlay so the cycle cannot re-arm
            // another ward before the deferred ultimate gets its turn.
            if (swordUltimateDeferred && attacks.Contains(EnemyAttack.SW05)
                && SwordCandidateAvailable(EnemyAttack.SW05, Position, distance, sight, escape))
            { next = EnemyAttack.SW05; return true; }
            bool basic = FightSeconds >= nextSwordBasic && sight && SwordAttackCanReach(EnemyAttack.SWBasic, game.Player.Position);
            if (basic && (swordBasicDue || distance < KarmaSpace.Units(2.5f)))
            { next = EnemyAttack.SWBasic; return true; }
            bool repeatAvailable = false;
            for (int i = 0; i < attacks.Count; i++)
            {
                var candidate = attacks[cycleIndex++ % attacks.Count];
                if (candidate == EnemyAttack.SW05 && swordGuardRoot != null && FightSeconds >= nextUltimate)
                    swordUltimateDeferred = true;
                if (!SwordCandidateAvailable(candidate, Position, distance, sight, escape)) continue;
                // Keep the inherited cycle; repeat only when all other unlocked skills are ineligible.
                if (candidate == previous) { next = candidate; repeatAvailable = true; continue; }
                next = candidate;
                return true;
            }
            if (!repeatAvailable && basic) { next = EnemyAttack.SWBasic; return true; }
            return repeatAvailable;
        }
        bool SwordCandidateAvailable(EnemyAttack candidate, Vector2 from, float distance, bool sight, bool escape)
        {
            if (candidate == EnemyAttack.SW04 && (swordGuardRoot != null || FightSeconds < nextSwordGuard)) return false;
            // Keep the defensive overlay readable: its four committed counters
            // can accompany ordinary waves, while larger set pieces wait for it.
            if (swordGuardRoot != null && candidate != EnemyAttack.SW01 && candidate != EnemyAttack.SW02) return false;
            return KarmaPatternCatalog.CanSelect(candidate, previous, distance / KarmaSpace.Scale, game.Player.Airborne,
                sight, FightSeconds < shieldUntil, escape, FightSeconds >= nextUltimate)
                && SwordAttackCanReachFrom(candidate, from, game.Player.Position);
        }
        // Movement queries eligibility without advancing the inherited deterministic cycle.
        bool HasSwordCandidate(Vector2 from)
        {
            float distance = Mathf.Abs(game.Player.Position.x - from.x);
            bool escape = from.x > KarmaStage.ArenaLeft + 5 && from.x < KarmaStage.ArenaRight - 5;
            bool sight = ClearShot(from, game.Player.Position);
            if (FightSeconds >= nextSwordBasic && sight && SwordAttackCanReachFrom(EnemyAttack.SWBasic, from, game.Player.Position)) return true;
            foreach (var candidate in attacks)
                if (SwordCandidateAvailable(candidate, from, distance, sight, escape)) return true;
            return false;
        }
        bool SwordAttackCanReach(EnemyAttack candidate, Vector2 target)
        { return SwordAttackCanReachFrom(candidate, Position, target); }
        bool SwordAttackCanReachFrom(EnemyAttack candidate, Vector2 from, Vector2 target)
        {
            float facing = target.x < from.x ? -1 : 1;
            Vector2 origin = from + Vector2.right * facing * KarmaSpace.Scale;
            if (candidate == EnemyAttack.SWBasic)
            {
                origin = from;
                Vector2 aim = SwordAim(origin, target + (Vector2)SwordPlayerBounds.center - game.Player.Position, facing);
                return Vector2.Distance(from, target) <= KarmaSpace.Units(5)
                    && SwordShotCanReach(origin, aim * BasicSwordSpeed, BasicSwordSize, BasicSwordLife, target);
            }
            if (candidate == EnemyAttack.SW01 || candidate == EnemyAttack.SW03)
            {
                Vector2 velocity = Vector2.right * facing * Mathf.Max(KarmaSpace.Scale, Tuning.swordSpeed);
                return SwordShotCanReach(new Vector2(origin.x, KarmaSpace.Units(1.05f)), velocity, SwordSize(candidate), WaveLife, target)
                    || SwordShotCanReach(new Vector2(origin.x, KarmaSpace.Units(3.3f)), velocity, SwordSize(candidate), WaveLife, target);
            }
            if (candidate == EnemyAttack.SW02)
            {
                origin = AimedSlashOrigin(from, facing);
                Vector2 velocity = Vector2.right * facing * AimedSlashSpeed;
                float life = AimedSlashLife(origin, velocity);
                float duration = 0.4f * Tempo + AimedSlashLockTime(origin, facing, SwordSize(candidate)) + life
                    + Mathf.Max(0.38f, 1.1f * Mathf.Clamp(Tuning.recoveryMultiplier, 0.4f, 2) * Tempo);
                return duration < Mathf.Max(5, Tuning.patternTimeout)
                    && SwordShotCanReach(origin, velocity, SwordSize(candidate), life, target);
            }
            if (candidate == EnemyAttack.SW04)
                return SwordShotCanReach(from, SwordAim(from, target, facing) * Tuning.swordSpeed, SwordSize(candidate), WaveLife, target);
            if (candidate == EnemyAttack.SW05)
            {
                Vector2 elevated = AerialSwordCastPosition;
                // The first group contains a direct aim with the full reachable lifetime.
                // AI probing must not create physics scenes or consume the cast's random seed.
                Vector2 aim = SwordAim(elevated, target + (Vector2)SwordPlayerBounds.center - game.Player.Position, facing);
                origin = elevated + aim * KarmaAerialSwordPlanner.FormationRadius;
                float life = AerialSwordReachLife();
                float duration = 0.8f + 0.5f + Warning(0.5f) + (KarmaAerialSwordPlanner.VolleyCount - 1) * AerialSwordInterval()
                    + life + 0.5f + Mathf.Max(0.38f, 2 * Mathf.Clamp(Tuning.recoveryMultiplier, 0.4f, 2) * Tempo);
                return duration < Mathf.Max(5, Tuning.patternTimeout)
                    && SwordShotCanReach(origin, aim * Tuning.swordSpeed, SwordSize(candidate), life, target);
            }
            return false;
        }
        static Vector2 SwordAim(Vector2 origin, Vector2 target, float facing)
        {
            Vector2 aim = (target - origin).normalized;
            return aim.sqrMagnitude < 0.01f ? Vector2.right * facing : aim;
        }
        bool SwordShotCanReach(Vector2 origin, Vector2 velocity, Vector2 size, float life, Vector2 target)
        {
            Vector2 end = origin + velocity * SwordShotLife(origin, velocity, life);
            var axis = velocity.sqrMagnitude > 0.000001f ? velocity.normalized : Vector2.right;
            var player = SwordPlayerBounds;
            target += (Vector2)player.center - game.Player.Position;
            return KarmaBossGeometry.SweptOrientedBox(origin.x, origin.y, end.x, end.y, target.x, target.y,
                axis.x, axis.y, size.x, size.y, player.extents.x, player.extents.y);
        }
        // The runtime discards a projectile when its center leaves these bounds.
        // Keep initial box overlap valid even if a wall-adjacent origin starts outside.
        internal static float SwordShotLife(Vector2 origin, Vector2 velocity, float life)
        {
            life = Mathf.Max(0, life);
            if (origin.x < KarmaStage.ArenaLeft || origin.x > KarmaStage.ArenaRight || origin.y < 0 || origin.y > KarmaSpace.Units(12)) return 0;
            if (velocity.x > 0) life = Mathf.Min(life, (KarmaStage.ArenaRight - origin.x) / velocity.x);
            else if (velocity.x < 0) life = Mathf.Min(life, (KarmaStage.ArenaLeft - origin.x) / velocity.x);
            if (velocity.y > 0) life = Mathf.Min(life, (KarmaSpace.Units(12) - origin.y) / velocity.y);
            else if (velocity.y < 0) life = Mathf.Min(life, -origin.y / velocity.y);
            return Mathf.Max(0, life);
        }
        IEnumerator RunPattern()
        {
            yield return ExecuteAttack();
            if (!Active) yield break;
            ClearPatternObjects();
            fullShield = frontalGuard = false;
            if (PrimaryType == Essence.SwordWave)
            {
                swordAttackedSinceRetreat = true;
                if (selected != EnemyAttack.SWBasic)
                {
                    swordSkillsSinceBasic++;
                    swordBasicDue = swordSkillsSinceBasic >= Mathf.Clamp(Tuning.swordSkillsBetweenBasics, 1, 6);
                }
            }
            state = ActionState.Approach; running = false;
            timer = DecisionDelay(selected);
        }
        IEnumerator Wait(float seconds)
        {
            float elapsed = 0;
            while (elapsed < seconds && Active)
            { yield return null; if (!game.Paused) elapsed += Time.deltaTime; }
        }
        IEnumerator Recovery(float seconds, bool keepAutoShield = false, string message = null)
        {
            ClearPatternObjects(); fullShield = frontalGuard = false;
            // A real, stationary attack opportunity: auto-shield cannot conceal it.
            if (!keepAutoShield) shieldUntil = shieldWarningUntil = 0;
            state = ActionState.Recover; Intent = message ?? "빈틈 발생 · 지금 공격하세요";
            yield return Wait(Mathf.Max(0.38f, seconds * Mathf.Clamp(Tuning.recoveryMultiplier, 0.4f, 2) * Tempo));
        }
        void Move(float distance)
        { transform.position = new Vector3(Mathf.Clamp(Position.x + distance, KarmaStage.BossLeftWall, KarmaStage.BossRightWall), Position.y, 0); }
        bool ClearShot(Vector2 from, Vector2 to)
        {
            foreach (var hit in Physics2D.LinecastAll(from, to))
                if (hit.collider.GetComponent<KarmaSurface>() != null && hit.collider.GetComponent<PlatformEffector2D>() == null) return false;
            return true;
        }
        void InterruptPattern()
        { InterruptPatternCore(false); }
        void InterruptPatternCore(bool preserveSwordPressure)
        {
            StopAllCoroutines(); ClearSwordGuard(); ClearPatternObjects();
            trial.Clear(); if (game.Player != null) game.Player.ReleaseArena();
            fullShield = frontalGuard = swordGuard = running = pendingParry = false;
            guardResponsePending = guardResponseRunning = false;
            swordRetreatRemaining = 0;
            swordApproaching = false;
            nextSwordApproach = 0;
            if (!preserveSwordPressure || !Active)
            { swordPressurePending = false; swordPressureTarget = 0; }
            swordRetreatTarget = 0;
            swordUltimateDeferred = false;
            shieldUntil = shieldWarningUntil = 0;
            transform.position = new Vector3(Mathf.Clamp(Position.x, KarmaStage.BossLeftWall, KarmaStage.BossRightWall), KarmaSpace.GuardianGroundY, 0);
        }
        public void CancelCombat()
        {
            if (stopped) return;
            stopped = true; InterruptPattern();
            if (shieldHalo != null) shieldHalo.enabled = false;
        }
        void OnDisable() { if (game != null) CancelCombat(); }
        void OnDestroy() { if (effectsRoot != null) Destroy(effectsRoot.gameObject); }
    }
}
