using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Owns ground spacing and pressure memory; never chooses or interrupts an attack.
        sealed class SwordBossMovement
        {
            readonly KarmaEnemy owner;
            readonly SwordBossBehavior behavior;
            KarmaSwordBossSettings Settings { get { return owner.Tuning.sword; } }
            float nextSwordRetreat, swordRetreatRemaining, swordRetreatUntil, swordRetreatDirection;
            float nextSwordApproach, swordApproachUntil, swordRetreatTarget, swordPressureTarget;
            float retreatSpeed, approachSpeed;
            float swordRetreatMoveAt;
            bool swordWallEscaping;
            enum SlideKind { LongRetreat, Reposition, WallEscape }
            SlideKind slideKind;
            float nextWallEscape, wallReturnUntil, wallReturnDirection;
            KarmaSwordRetreatAfterimages afterimages;
            bool swordAttackedSinceRetreat, swordApproaching, swordPressurePending;
            public SwordBossMovement(KarmaEnemy owner, SwordBossBehavior behavior)
            {
                this.owner = owner; this.behavior = behavior;
            }
            public void Initialize()
            {
                afterimages = owner.art.gameObject.AddComponent<KarmaSwordRetreatAfterimages>();
                afterimages.Initialize(owner, owner.game, Settings);
            }
            float SwordSpacingDistance()
            {
                // Pressure memory uses the basic wave's reach. Retreat travel
                // is a separate displacement budget based on the player's dash.
                return Mathf.Clamp(Settings.swordRetreatStopDistance,
                    Mathf.Min(behavior.BasicMeleeRange + Settings.swordMeleeHysteresis, behavior.BasicWaveRange * 0.95f),
                    behavior.BasicWaveRange * 0.95f);
            }
            float SwordRetreatDistance()
            {
                return Mathf.Max(0, Settings.swordRetreatDistance,
                    owner.game.Config.Techniques.pierceDistance * Settings.swordRetreatDashMultiplier);
            }
            float SwordRetreatClearance(float direction, float requested)
            {
                float before = owner.Position.x;
                float left = Mathf.Min(before, KarmaStage.BossLeftWall + 0.5f);
                float right = Mathf.Max(before, KarmaStage.BossRightWall - 0.5f);
                float available = Mathf.Min(requested, direction < 0 ? before - left : right - before);
                var body = SwordMoveBounds();
                KarmaPhysicsQueries.BoxCast(body.center, body.size,
                    Vector2.right * direction, available + 0.1f, owner.terrainHits);
                foreach (var hit in owner.terrainHits)
                {
                    if (!BlocksSwordMove(hit.collider)) continue;
                    if (Vector2.Dot(hit.normal, Vector2.right * direction) >= -0.001f) continue;
                    available = Mathf.Min(available, Mathf.Max(0, hit.distance - 0.1f));
                }
                // Validate the route, not only the destination, before choosing
                // a side. The per-frame ground check remains the final guard.
                for (float travel = 0.5f; travel < available; travel += 0.5f)
                    if (!SwordFloorAt(before + direction * travel))
                    { available = Mathf.Max(0, travel - 0.5f); break; }
                if (!SwordFloorAt(before + direction * available)) return 0;
                return Mathf.Max(0, available);
            }
            Bounds SwordMoveBounds()
            {
                var collider = owner.GetComponent<Collider2D>();
                return collider != null && collider.enabled && !collider.isTrigger ? collider.bounds
                    : new Bounds(owner.Position, KarmaSpace.Units(new Vector2(0.8f, 1.6f)));
            }
            bool BlocksSwordMove(Collider2D collider)
            {
                // Boss roots have no solid player-contact collider. Preserve
                // that rule; add no invulnerability or collision suppression.
                if (collider.transform.IsChildOf(owner.game.Player.transform))
                {
                    var body = owner.GetComponent<Collider2D>();
                    return body != null && body.enabled && !body.isTrigger && !collider.isTrigger
                        && !Physics2D.GetIgnoreLayerCollision(body.gameObject.layer, collider.gameObject.layer)
                        && !Physics2D.GetIgnoreCollision(body, collider);
                }
                return !collider.isTrigger && collider.GetComponent<PlatformEffector2D>() == null
                    && !collider.transform.IsChildOf(owner.transform)
                    && !collider.transform.IsChildOf(owner.game.Player.transform)
                    && collider.GetComponentInParent<KarmaEnemy>() == null;
            }
            bool SwordFloorAt(float x)
            {
                KarmaPhysicsQueries.Raycast(new Vector2(x, owner.Position.y), Vector2.down,
                    KarmaSpace.GuardianGroundY + 0.5f, owner.terrainHits);
                foreach (var hit in owner.terrainHits)
                    if (hit.collider.GetComponent<KarmaSurface>() != null) return true;
                return false;
            }
            bool ChooseRetreat(float dx, out float direction, out float travel)
            {
                float requested = SwordRetreatDistance();
                float away = Mathf.Abs(dx) > 0.001f ? (dx < 0 ? 1 : -1) : -owner.Facing;
                float leftRoom = SwordRetreatClearance(-1, requested);
                float rightRoom = SwordRetreatClearance(1, requested);
                slideKind = SlideKind.LongRetreat;
                // A wall escape is relocation, distinct from a player-facing
                // backstep. It may cross the player under existing non-solid
                // boss rules, but must pass the same terrain/floor sweep.
                // Near a boundary, choose the inward route with actual room.
                bool leftBlocked = leftRoom < Mathf.Min(6, requested * 0.45f);
                bool rightBlocked = rightRoom < Mathf.Min(6, requested * 0.45f);
                if (leftBlocked || rightBlocked)
                {
                    float escapeDirection = leftBlocked && rightBlocked
                        ? (rightRoom > leftRoom ? 1 : rightRoom < leftRoom ? -1 : away)
                        : leftBlocked ? 1 : -1;
                    float blockedRoom = escapeDirection > 0 ? leftRoom : rightRoom;
                    float escapeBudget = requested * 1.5f;
                    float escapeRoom = SwordRetreatClearance(escapeDirection, escapeBudget);
                    if (owner.FightSeconds >= nextWallEscape && escapeRoom >= Mathf.Max(4, blockedRoom + 2)
                        && !(owner.FightSeconds < wallReturnUntil && escapeDirection == wallReturnDirection))
                    {
                        direction = escapeDirection; travel = escapeRoom;
                        swordWallEscaping = true;
                        slideKind = SlideKind.WallEscape;
                        wallReturnDirection = -direction;
                        nextWallEscape = owner.FightSeconds + 5;
                        return true;
                    }
                    direction = travel = 0;
                    nextSwordRetreat = owner.FightSeconds + 0.65f;
                    swordAttackedSinceRetreat = false;
                    behavior.RequestBasic();
                    return false;
                }
                direction = 0; travel = 0;
                // Retreat directly away first. Only an unsafe/blocked route may
                // select the other side; random rolls and direction streaks no
                // longer override player-relative spacing.
                for (int candidate = 0; candidate < 2; candidate++)
                {
                    float side = candidate == 0 ? away : -away;
                    // If retreat would undo a recent wall escape, give combat
                    // its turn rather than substitute a player-facing slide.
                    if (owner.FightSeconds < wallReturnUntil && side == wallReturnDirection) break;
                    float room = side < 0 ? leftRoom : rightRoom;
                    if (room < 4) continue;
                    float distance = Mathf.Min(room, requested);
                    if (Mathf.Abs(owner.Position.x + side * distance - owner.game.Player.Position.x) < 3)
                        continue;
                    direction = side; travel = distance;
                    slideKind = candidate == 0 ? SlideKind.LongRetreat : SlideKind.Reposition;
                    return true;
                }
                nextSwordRetreat = owner.FightSeconds + 0.65f;
                swordAttackedSinceRetreat = false; behavior.RequestBasic(); return false;
            }
            public void RememberSwordPressure()
            {
                if (owner.PrimaryType != Essence.SwordWave || !owner.Active) return;
                swordPressurePending = true;
                // Capture before Hurt changes the pose. A smaller next pose must
                // not erase a real hit or shrink its requested separation.
                swordPressureTarget = Mathf.Max(swordPressureTarget, SwordSpacingDistance());
            }
            bool RetreatSword(float dt, float dx)
            {
                if (swordRetreatRemaining <= 0)
                {
                    float safeGap = Mathf.Max(swordPressureTarget, SwordSpacingDistance());
                    bool pressure = swordPressurePending && Mathf.Abs(dx) < safeGap;
                    float distance = Mathf.Abs(dx);
                    bool tooClose = distance < behavior.BasicMeleeRange * 0.55f;
                    bool nearMelee = tooClose || distance < Settings.pressureRetreatStartDistance
                        && !behavior.BasicMeleeCanReach(owner.Position, owner.game.Player.Position);
                    bool repositionOpportunity = !pressure && !nearMelee && distance <= behavior.BasicWaveRange + 3;
                    // Ordinary combat still requires a completed action. The live
                    // SW03 timeline permits evasion-only steps on the same cooldown.
                    if ((!pressure && !nearMelee && !repositionOpportunity)
                    || owner.FightSeconds < nextSwordRetreat || !swordAttackedSinceRetreat && !behavior.EvasionOnly)
                    return false;
                    // Slightly favor another spacing step after an attack. This roll
                    // only offers an action; ChooseRetreat always picks away.
                    if (repositionOpportunity && Random.value > 0.45f)
                    { nextSwordRetreat = owner.FightSeconds + 0.6f; return false; }
                    if (!ChooseRetreat(dx, out var chosenDirection, out var chosenTravel)) return false;
                    swordApproaching = false; approachSpeed = 0; retreatSpeed = 0;
                    swordPressurePending = false;
                    swordPressureTarget = 0;
                    swordRetreatDirection = chosenDirection;
                    // Even inside the ordinary facing dead zone, a backstep
                    // must face its target rather than slide forward.
                    bool relocating = slideKind != SlideKind.LongRetreat;
                    owner.direction = relocating ? (dx < 0 ? -1 : 1) : -swordRetreatDirection;
                    owner.art.localScale = new Vector3(owner.direction * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                    swordRetreatRemaining = chosenTravel;
                    // The endpoint measures this boss's displacement, not its
                    // changing gap to the pursuer. A 9.5-unit gap used to stop a
                    // nominal nine-unit retreat after only a few world units.
                    swordRetreatTarget = owner.Position.x + swordRetreatDirection * swordRetreatRemaining;
                    float topSpeed = Mathf.Min(Mathf.Max(1, Settings.swordRetreatSpeed), Mathf.Max(1, Settings.swordRetreatSpeedLimit));
                    float requiredTime = Mathf.Clamp(Settings.swordBackstepPreparation, 0, 0.08f)
                        + swordRetreatRemaining / topSpeed
                        + topSpeed / (2 * Mathf.Max(1, Settings.swordBackstepAcceleration))
                        + topSpeed / (2 * Mathf.Max(1, Settings.swordBackstepDeceleration)) + 0.12f;
                    swordRetreatUntil = owner.FightSeconds + Mathf.Max(Settings.pressureRetreatDuration, requiredTime);
                    swordRetreatMoveAt = owner.FightSeconds + Mathf.Clamp(Settings.swordBackstepPreparation, 0, 0.08f);
                    nextSwordRetreat = owner.FightSeconds + Mathf.Clamp(
                        Mathf.Min(Settings.swordRetreatCooldown, Settings.pressureRetreatCooldown), 2.1f, 3.5f);
                    // Even a blocked attempt yields to an attack before retrying.
                    swordAttackedSinceRetreat = false;
                    if (swordRetreatRemaining < 0.12f)
                    { FinishSwordRetreat(); return false; }
                    // Facing can change if the player crosses behind a long
                    // slide; sample real travel in either direction throughout.
                    afterimages.BeginRetreat(true);
                }
                float toEndpoint = (swordRetreatTarget - owner.Position.x) * swordRetreatDirection;
                // A change in player side is not a physical obstruction. The
                // swept collision/floor checks still guard every movement step.
                if (owner.FightSeconds >= swordRetreatUntil || toEndpoint <= 0.001f)
                { FinishSwordRetreat(); return false; }
                if (owner.FightSeconds < swordRetreatMoveAt) return true;
                float remaining = Mathf.Min(swordRetreatRemaining, toEndpoint);
                float braking = Mathf.Max(1, Settings.swordBackstepDeceleration);
                float speed = Mathf.Min(Mathf.Max(1, Settings.swordRetreatSpeed),
                    Mathf.Max(1, Settings.swordRetreatSpeedLimit));
                // Brake for both the remaining distance and the duration budget.
                float brakingStep = braking * dt;
                speed = Mathf.Min(speed, Mathf.Sqrt(brakingStep * brakingStep + 2 * braking * remaining) - brakingStep,
                    braking * Mathf.Max(0, swordRetreatUntil - owner.FightSeconds));
                retreatSpeed = Mathf.MoveTowards(retreatSpeed, speed,
                    (speed < retreatSpeed ? braking : Mathf.Max(1, Settings.swordBackstepAcceleration)) * dt);
                float step = swordRetreatDirection * Mathf.Min(remaining, retreatSpeed * dt);
                if (Mathf.Abs(step) < 0.0001f) { FinishSwordRetreat(); return false; }
                float travelled = MoveSwordOnGround(step);
                if (travelled <= 0.0001f) { FinishSwordRetreat(); return false; }
                swordRetreatRemaining = Mathf.Max(0, swordRetreatRemaining - travelled);
                if (swordRetreatRemaining <= 0.001f)
                { FinishSwordRetreat(); return false; }

                return true;
            }
            void FinishSwordRetreat()
            {
                swordRetreatRemaining = 0;
                swordRetreatTarget = 0;
                retreatSpeed = 0;
                afterimages.EndRetreat();
                if (swordWallEscaping)
                {
                    swordWallEscaping = false;
                    // Keep the wall-return guard, with a slightly earlier pursuit response.
                    wallReturnUntil = owner.FightSeconds + Mathf.Clamp(Settings.swordWallReturnCooldown, 2, 3);
                    nextWallEscape = Mathf.Max(nextWallEscape, owner.FightSeconds + 3);
                }
                // A new hit during this move remains queued behind the cooldown;
                // starting the move already consumed the previous request.
                behavior.RequestSkillAfterRetreat();
                owner.timer = 0;
                float attackWindow = Mathf.Clamp(Settings.swordRetreatAttackWindow, 0.5f, 0.8f);
                nextSwordRetreat = Mathf.Max(nextSwordRetreat, owner.FightSeconds + attackWindow);
                nextSwordApproach = owner.FightSeconds + Mathf.Max(attackWindow, owner.timer + 0.05f);
            }
            bool ApproachSword(float dt, float dx)
            {
                // Relocation can put the pursuer behind the boss. Apply the
                // existing three-second wall-return guard to walking as well,
                // so approach cannot undo the escape before attacks get a turn.
                if (owner.FightSeconds < wallReturnUntil && Mathf.Sign(dx) == wallReturnDirection)
                {
                    if (swordApproaching) FinishSwordApproach();
                    return false;
                }
                float distance = Mathf.Abs(dx), preferred = Mathf.Min(Settings.swordPreferredDistance, behavior.BasicWaveRange * 0.9f);
                // An available medium-range basic should not be postponed by walking.
                if (!swordApproaching && distance <= behavior.BasicWaveRange && behavior.HasSwordCandidate(owner.Position)) return false;
                if (distance <= preferred)
                {
                    if (swordApproaching) FinishSwordApproach();
                    return false;
                }
                if (!swordApproaching)
                {
                    if (owner.FightSeconds < nextSwordApproach) return false;
                    if (distance <= Mathf.Max(Settings.swordApproachStartDistance, preferred + 1) && behavior.HasSwordCandidate(owner.Position)) return false;
                    swordApproaching = true;
                    swordApproachUntil = owner.FightSeconds + 1.2f;
                }
                // A faster fleeing player must not postpone attacks indefinitely.
                // A reachable ranged attack gets a turn after this short approach.
                if (owner.FightSeconds >= swordApproachUntil && behavior.HasSwordCandidate(owner.Position))
                { FinishSwordApproach(); return false; }
                float speed = Mathf.Min(owner.game.Config.enemyMoveSpeed * owner.Tuning.moveMultiplier * Settings.movementScale, owner.game.Config.moveSpeed * 0.8f);
                speed = Mathf.Min(speed, Mathf.Sqrt(2 * Mathf.Max(1, Settings.swordRetreatDeceleration) * (distance - preferred)));
                approachSpeed = Mathf.MoveTowards(approachSpeed, speed, Mathf.Max(1, Settings.swordRetreatAcceleration) * dt);
                float step = Mathf.Sign(dx) * Mathf.Min(distance - preferred, approachSpeed * dt);
                if (MoveSwordOnGround(step) <= 0.0001f)
                {
                    FinishSwordApproach();
                    return false;
                }

                return true;
            }
            void FinishSwordApproach()
            {
                swordApproaching = false;
                approachSpeed = 0;
                nextSwordApproach = owner.FightSeconds + Mathf.Max(0.25f, owner.timer + 0.05f);
            }
            float MoveSwordOnGround(float step)
            {
                float before = owner.Position.x;
                // A boss already inside the wall margin can walk back out of it,
                // but clamping must never reverse or enlarge the requested step.
                float left = Mathf.Min(before, KarmaStage.BossLeftWall + 0.5f);
                float right = Mathf.Max(before, KarmaStage.BossRightWall - 0.5f);
                float after = Mathf.Clamp(before + step, left, right);
                step = after - before;
                if (Mathf.Abs(step) <= 0.0001f) return 0;
                var body = SwordMoveBounds();
                KarmaPhysicsQueries.BoxCast(body.center, body.size,
                    Vector2.right * Mathf.Sign(step), Mathf.Abs(step) + 0.1f, owner.terrainHits);
                foreach (var hit in owner.terrainHits)
                if (BlocksSwordMove(hit.collider)
                    && Vector2.Dot(hit.normal, Vector2.right * Mathf.Sign(step)) < -0.001f)
                return 0;
                bool floor = false;
                Physics2D.Raycast(new Vector2(after, owner.Position.y), Vector2.down, TerrainFilter,
                    owner.terrainHits, KarmaSpace.GuardianGroundY + 0.5f);
                foreach (var hit in owner.terrainHits)
                if (hit.collider.GetComponent<KarmaSurface>() != null) { floor = true; break; }
                if (!floor) return 0;
                owner.Move(step);
                return Mathf.Abs(owner.Position.x - before);
            }

            public bool Tick(float dt, float dx)
            {
                if (dt <= 0) return true;
                if (Mathf.Abs(dx) > 0.5f) owner.direction = Mathf.Sign(dx);
                if (owner.direction == 0) owner.direction = owner.art.localScale.x < 0 ? -1 : 1;
                owner.art.localScale = new Vector3(owner.direction * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                bool wasRetreating = swordRetreatRemaining > 0;
                if (RetreatSword(dt, dx))
                { owner.animator.ShowSwordRetreat(); return true; }
                // Hand the completed slide straight to BeginAttack this frame.
                if (wasRetreating) return false;
                if (!ApproachSword(dt, dx)) return false;
                owner.animator.ShowGuardianSpacing(); return true;
            }
            public void ObservePressure()
            {
                if (owner.game.Player.SwordPressureActive && owner.game.Player.SwordAreaOverlaps(owner)) RememberSwordPressure();
                else if (Mathf.Abs(owner.game.Player.Position.x - owner.Position.x) > Mathf.Max(swordPressureTarget, SwordSpacingDistance()) + 0.25f)
                { swordPressurePending = false; swordPressureTarget = 0; }
            }
            public void OnSelected() { swordRetreatRemaining = 0; swordWallEscaping = false; swordApproaching = false; retreatSpeed = approachSpeed = 0; afterimages.EndRetreat(); }
            public void OnCompleted() { swordAttackedSinceRetreat = true; }
            public void Interrupt(bool preservePressure)
            {
                swordRetreatRemaining = 0; swordApproaching = false; nextSwordApproach = 0;
                swordWallEscaping = false;
                retreatSpeed = approachSpeed = 0;
                swordRetreatTarget = 0;
                afterimages.Clear();
                if (!preservePressure || !owner.Active) { swordPressurePending = false; swordPressureTarget = 0; }
            }
        }
    }
}
