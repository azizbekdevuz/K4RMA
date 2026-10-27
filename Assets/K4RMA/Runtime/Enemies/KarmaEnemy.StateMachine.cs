using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        int cycleIndex;
        void Update()
        {
            if (game == null || stopped) return;
            if (game.Paused) return;
            if (!Active) { CancelCombat(); return; }
            if (!engaged)
            {
                if (game.Player.Position.x < 12) { Intent = "앞으로 나아가 시험을 시작하세요"; return; }
                engaged = true;
            }
            float dt = Time.deltaTime; FightSeconds += dt;
            if (Enraged && !announcedEnrage)
            {
                announcedEnrage = true;
                KarmaCombatVFX.SpawnSprite(effectsRoot, Position, "PersonalizationVFX", "Fusion", KarmaSpace.Units(new Vector2(4, 4)), 0.7f);
            }
            if (pendingParry)
            {
                pendingParry = false; InterruptPattern();
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
            if (running)
            {
                if (FightSeconds - patternStarted > Mathf.Max(5, Tuning.patternTimeout) || Position.y < KarmaSpace.Units(-2) || Position.y > KarmaSpace.Units(12))
                { InterruptPattern(); state = ActionState.Approach; timer = 1.2f; Intent = "자세 복구 · 빈틈"; }
                return;
            }
            float dx = game.Player.Position.x - Position.x;
            art.localScale = new Vector3(Mathf.Sign(dx) * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            float preferred = KarmaSpace.Units(PrimaryType == Essence.SwordWave ? 7 : PrimaryType == Essence.Guard ? 3 : 3.5f);
            if (Mathf.Abs(dx) > preferred) Move(Mathf.Sign(dx) * game.Config.enemyMoveSpeed * Tuning.moveMultiplier * dt);
            else if (PrimaryType == Essence.SwordWave && Mathf.Abs(dx) < KarmaSpace.Units(4.2f)) Move(-Mathf.Sign(dx) * game.Config.enemyMoveSpeed * dt);
            timer -= dt;
            if (timer <= 0) BeginAttack();
        }
        void BeginAttack()
        {
            float distance = Mathf.Abs(game.Player.Position.x - Position.x);
            bool escape = Position.x > KarmaStage.ArenaLeft + 5 && Position.x < KarmaStage.ArenaRight - 5;
            bool sight = ClearShot(Position, game.Player.Position);
            selected = EnemyAttack.GroundStrike;
            for (int i = 0; i < attacks.Count; i++)
            {
                var candidate = attacks[cycleIndex++ % attacks.Count];
                if (!KarmaPatternCatalog.CanSelect(candidate, previous, distance / KarmaSpace.Scale, game.Player.Airborne,
                    sight, FightSeconds < shieldUntil, escape, FightSeconds >= nextUltimate)) continue;
                selected = candidate; break;
            }
            previous = selected;
            if (KarmaPatternCatalog.IsUltimate(selected)) nextUltimate = FightSeconds + Mathf.Max(10, Tuning.ultimateCooldown);
            direction = game.Player.Position.x < Position.x ? -1 : 1;
            art.localScale = new Vector3(direction * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            running = true; patternStarted = FightSeconds;
            Intent = KarmaPatternCatalog.Label(selected);
            StartCoroutine(RunPattern());
        }
        IEnumerator RunPattern()
        {
            yield return ExecuteAttack();
            if (!Active) yield break;
            ClearPatternObjects();
            fullShield = frontalGuard = swordGuard = false;
            state = ActionState.Approach; running = false;
            timer = Mathf.Max(0.22f, game.Config.enemyDecisionDelay * Tuning.decisionMultiplier * Tempo);
        }
        IEnumerator Wait(float seconds)
        {
            float elapsed = 0;
            while (elapsed < seconds && Active)
            { yield return null; if (!game.Paused) elapsed += Time.deltaTime; }
        }
        IEnumerator Recovery(float seconds, bool keepAutoShield = false, string message = null)
        {
            ClearPatternObjects(); fullShield = frontalGuard = swordGuard = false;
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
        {
            StopAllCoroutines(); ClearPatternObjects();
            trial.Clear(); if (game.Player != null) game.Player.ReleaseArena();
            fullShield = frontalGuard = swordGuard = running = pendingParry = false;
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
