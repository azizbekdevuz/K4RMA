using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void Update()
        {
            if (game == null || !game.IsCombat || game.Paused || !Alive) return;
            UpdateBurn();
            if (!Alive || !game.IsCombat) return;
            if (reflectedDamage > 0 && Time.time >= reflectAt) ReleaseReflection();
            if (!engaged)
            {
                if (game.Player.Position.x < 12) { Intent = "앞으로 나아가 시험을 시작하세요"; return; }
                engaged = true;
            }
            float dt = Time.deltaTime;
            FightSeconds += dt;
            if (visiblePhase != CombatPhase)
            {
                visiblePhase = CombatPhase;
                KarmaRemnantVFX.Impact(game, Position, Essence.Ward);
            }
            timer -= dt;
            float dx = game.Player.Position.x - Position.x;
            if (state != ActionState.Charge && state != ActionState.OverdriveCharge && !Stunned) art.localScale = new Vector3(dx < 0 ? -1.4f : 1.4f, 1.4f, 1);
            switch (state)
            {
                case ActionState.OverdriveWindup:
                    TickFireAura();
                    if (timer <= 0)
                    {
                        // Track only during windup. Direction locks before the charge begins.
                        direction = game.Player.Position.x < Position.x ? -1 : 1;
                        art.localScale = new Vector3(direction * 1.4f, 1.4f, 1);
                        state = ActionState.OverdriveCharge;
                        Intent = "불타는 방어막 돌진 / 벽 앞에서 점프로 피하세요!";
                    }
                    break;
                case ActionState.OverdriveCharge:
                    Vector2 beforeCharge = Position;
                    transform.position = new Vector3(Mathf.Clamp(Position.x + direction * Tuning.overdriveSpeed * dt,
                        KarmaStage.BossLeftWall, KarmaStage.BossRightWall), Position.y, 0);
                    if (KarmaProjectile.Intersects(beforeCharge, Position, game.Player.Position, 1.55f))
                        game.Player.TakeImpact(Damage * 2, Position, 12);
                    TickFireAura();
                    if ((direction < 0 && Position.x <= KarmaStage.BossLeftWall) ||
                        (direction > 0 && Position.x >= KarmaStage.BossRightWall)) CrashIntoWall();
                    break;
                case ActionState.Stunned:
                    if (indicator != null) indicator.transform.Rotate(0, 0, 180 * dt);
                    float recoil = Mathf.Min(recoilRemaining, 12 * dt);
                    Move(-direction * recoil);
                    recoilRemaining -= recoil;
                    if (timer <= 0) Recover(0.6f);
                    break;
                case ActionState.Approach:
                    if (Mathf.Abs(dx) > 3.1f) Move(Mathf.Sign(dx) * game.Config.enemyMoveSpeed * Tuning.moveMultiplier * dt);
                    if (timer <= 0) BeginAttack();
                    break;
                case ActionState.Telegraph:
                    if (timer <= 0) ExecuteAttack();
                    break;
                case ActionState.Charge:
                    Move(direction * (finalGuardian ? 17 : 13) * dt);
                    if (selected == EnemyAttack.BlazingCharge && Time.time >= nextTrail)
                    {
                        KarmaHazard.SpawnFire(game, Position.x);
                        nextTrail = Time.time + game.Config.fireTrailInterval;
                    }
                    if (timer <= 0 || Position.x <= 1.6f || Position.x >= 38.4f)
                    {
                        ClearIndicator();
                        if (selected == EnemyAttack.GuardedCharge)
                            FireRing(game.Config.dashColor, game.Config.wardColor);
                        if (remainingCharges > 0)
                        {
                            remainingCharges--;
                            state = ActionState.ChargeTurn; timer = Tuning.returnChargeWarning;
                            direction = game.Player.Position.x < Position.x ? -1 : 1;
                            Intent = "연속 돌진 예고 / 반대 방향으로 다시 돌진합니다";
                            indicator = KarmaVisuals.Box(game.World, "재돌진 예고", new Vector2(20, 0.2f),
                                new Vector2(37, 0.12f), AttackColor(), 6);
                        }
                        else
                        {
                            Recover(1.3f);
                        }
                    }
                    break;
                case ActionState.ChargeTurn:
                    if (timer <= 0) StartCharge();
                    break;
                case ActionState.Volley:
                    if (timer <= 0)
                    {
                        FireFan(volleyCount, new Color(0.95f, 0.68f, 0.85f), 9);
                        volleyLeft--;
                        if (volleyLeft <= 0) Recover(0.9f);
                        else { timer = Tuning.volleyInterval; Intent = "추적 연사 / 계속 이동하세요 · 남은 " + volleyLeft + "회"; }
                    }
                    break;
                case ActionState.MeteorRain:
                    if (activeMeteors <= 0)
                    {
                        activeMeteors = 0; overlay.Cancel(); Recover(0.9f);
                    }
                    break;
                case ActionState.Ward:
                    if (timer <= 0)
                    {
                        if (selected == EnemyAttack.EmberAegis)
                            FireRing(game.Config.flameColor, game.Config.wardColor);
                        ReleaseReflection();
                        Recover(1.1f);
                    }
                    break;
                case ActionState.Recover:
                    if (timer <= 0)
                    {
                        state = ActionState.Approach;
                        timer = game.Config.enemyDecisionDelay * Tuning.decisionMultiplier * (Enraged ? 0.75f : 1);
                        Intent = "접근 중";
                    }
                    break;
            }
            overlay.Tick(state == ActionState.MeteorRain || state == ActionState.Ward);
            if (!Stunned && !Invulnerable && Mathf.Abs(game.Player.Position.x - Position.x) < 1 && Mathf.Abs(game.Player.Position.y - Position.y) < 1.3f)
                game.Player.TakeDamage(Damage * (state == ActionState.Charge ? 1.5f : 0.6f));
        }
        void Move(float distance)
        {
            transform.position = new Vector3(Mathf.Clamp(Position.x + distance, 1.5f, 38.5f), Position.y, 0);
        }
    }
}
