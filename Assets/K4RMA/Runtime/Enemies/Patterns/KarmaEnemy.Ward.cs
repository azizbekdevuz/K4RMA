using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void UpdateAutoShield()
        {
            if (PrimaryType != Essence.Ward || trial.Active || Stunned || state == ActionState.Recover || frontalGuard) return;
            if (shieldWarningUntil > 0)
            {
                if (FightSeconds >= shieldWarningUntil) RefreshAutoShield();
                return;
            }
            if (FightSeconds >= nextShield && FightSeconds >= shieldUntil)
            { shieldWarningUntil = FightSeconds + Mathf.Max(0.3f, Tuning.autoShieldWarning); }
            if (shieldWarningUntil > 0)
                KarmaVisuals.Flash(game.World, Position, KarmaSpace.Units(new Vector2(1.8f, 2.6f)), new Color(1, 0.85f, 0.2f, 0.3f), 0.25f);
        }
        void RefreshAutoShield()
        {
            shieldUntil = FightSeconds + Mathf.Max(1, Tuning.autoShieldDuration);
            nextShield = FightSeconds + Mathf.Max(2, Tuning.autoShieldPeriod);
            shieldWarningUntil = 0;
        }
        IEnumerator HeavyStrike()
        {
            Vector2 center = Position + Vector2.right * direction * KarmaSpace.Units(2);
            Vector2 size = KarmaSpace.Units(new Vector2(4, 2.8f));
            var mark = Mark("GD02 강공격 · 패링 가능", center, size);
            state = ActionState.Telegraph; yield return Wait(Warning(0.9f));
            Destroy(mark.gameObject); animator.Attack(); state = ActionState.Attack;
            if(KarmaArtwork.Settings.useCompleteArtwork) KarmaArtworkVFX.Spawn(effectsRoot,center,KarmaArtAtlas.Effects(1)[5],size,0.2f);
            int parries = game.Player.ParryCount; float health = game.Player.Health;
            var contact = new KarmaAttackContact();
            Attack(center, Vector2.zero, size, 0.18f, 1.5f, contact);
            TryAttackHit(center, center, size, Damage * 1.5f, contact, 0);
            if (game.Player.ParryCount != parries) yield break;
            bool refreshed = Tuning.dodgeRefreshesShield || game.Player.Health < health;
            if (refreshed)
            {
                RefreshAutoShield();
                Intent = "GD02 강공격 적중 · 자동방어막 갱신";
            }
            yield return Wait(0.2f);
            if (Enraged)
            {
                Vector2 waveAt = new Vector2(Position.x, KarmaSpace.Units(0.65f));
                Vector2 waveSize = KarmaSpace.Units(new Vector2(1.2f, 0.65f));
                var warning = PathMark(waveAt, waveAt + Vector2.right * direction * KarmaSpace.Units(10), waveSize);
                Intent = "강공격 후 지면 충격파 · 점프로";
                yield return Wait(Warning(0.6f)); Destroy(warning.gameObject);
                Attack(waveAt, Vector2.right * direction * KarmaSpace.Units(14), waveSize, 0.7f, 0.75f);
                yield return Wait(0.75f);
            }
            // The update refresh is useful during the visible result. Recovery then exposes the boss.
            yield return Recovery(1.4f, refreshed);
        }
        IEnumerator SplitShield()
        {
            state = ActionState.Telegraph;
            var contact = new KarmaAttackContact();
            Vector2 origin = Position + Vector2.right * direction * KarmaSpace.Scale;
            Vector2 impact = new Vector2(Mathf.Clamp(game.Player.Position.x, KarmaStage.ArenaLeft + 4, KarmaStage.ArenaRight - 4), KarmaSpace.Units(0.65f));
            Vector2 size = KarmaSpace.Units(new Vector2(1.2f, 1.2f));
            var mark = PathMark(origin, impact, size);
            yield return Wait(Warning(0.8f)); Destroy(mark.gameObject);
            state = ActionState.Attack; animator.Attack();
            Attack(origin, (impact - origin) / 0.5f, size, 0.5f, 0.8f, contact, Tuning.fragmentStunSeconds);
            yield return Wait(0.55f);
            int count = Mathf.Clamp(Tuning.shieldFragments, 2, 5);
            var starts = new Vector2[count]; var aims = new Vector2[count];
            var paths = new SpriteRenderer[count];
            Vector2 target = game.Player.Position; // One sample for all fragments; no late tracking.
            for (int i = 0; i < count; i++)
            {
                starts[i] = impact + KarmaSpace.Units(new Vector2((i - (count - 1) / 2f) * 1.1f, 1.3f + i * 0.35f));
                aims[i] = (target - starts[i]).normalized * KarmaSpace.Units(10);
                paths[i] = PathMark(starts[i], starts[i] + aims[i] * 0.8f, KarmaSpace.Units(new Vector2(0.5f, 0.5f)));
            }
            Intent = "GD03 조각 조준 고정 · 한 번만 피격/기절";
            yield return Wait(Warning(Tuning.fragmentAimWarning));
            for (int i = 0; i < count; i++)
            {
                Destroy(paths[i].gameObject);
                Attack(starts[i], aims[i], KarmaSpace.Units(new Vector2(0.5f, 0.5f)), 0.8f, 0.8f, contact, Tuning.fragmentStunSeconds);
            }
            yield return Wait(0.85f);
            // Return uses separate non-damaging visuals and a fixed deadline.
            var pieces = new SpriteRenderer[count];
            for (int i = 0; i < count; i++) pieces[i] = Mark("회수 조각 · 피해 없음", starts[i] + aims[i] * 0.8f, KarmaSpace.Units(new Vector2(0.4f, 0.4f)), false);
            Intent = "GD03 방패 회수 · 복귀 판정 없음";
            float elapsed = 0;
            while (elapsed < 0.6f && Active)
            {
                if (game.Paused) { yield return null; continue; }
                elapsed += Time.deltaTime;
                for (int i = 0; i < count; i++) pieces[i].transform.position = Vector2.Lerp(starts[i] + aims[i] * 0.8f, Position, Mathf.Clamp01(elapsed / 0.6f));
                yield return null;
            }
            yield return Recovery(1.2f);
        }
        IEnumerator RisingBarrier()
        {
            // Front-only defense deliberately supersedes auto-shield to reward a back attack.
            shieldUntil = shieldWarningUntil = 0; frontalGuard = true;
            state = ActionState.Telegraph;
            float x = Mathf.Clamp(Position.x + direction * KarmaSpace.Units(2.5f), KarmaStage.ArenaLeft + 5, KarmaStage.ArenaRight - 5);
            Vector2 center = new Vector2(x, KarmaSpace.Units(3)), size = KarmaSpace.Units(new Vector2(1.4f, 6));
            var mark = Mark("GD04 방벽 상승 · 높이 전체 위험", center, size);
            yield return Wait(Warning(0.8f));
            animator.Attack(Vector2.down); yield return Wait(0.3f);
            Destroy(mark.gameObject); state = ActionState.Attack;
            Attack(center, Vector2.zero, size, 0.4f);
            yield return Wait(0.4f); ClearPatternObjects();
            var wall = Mark("완성 방벽 · 이동 차단/피해 없음", center, size, false);
            game.Player.ResolveBarrier(x, size.x);
            wall.gameObject.AddComponent<KarmaSurface>(); wall.gameObject.AddComponent<BoxCollider2D>();
            yield return Wait(0.5f);
            yield return Recovery(Mathf.Max(0.3f, Tuning.barrierRecovery));
        }
        IEnumerator SurvivalTrial()
        {
            state = ActionState.Telegraph;
            Mark("GD05 결계 범위 예고", new Vector2(20, KarmaSpace.Units(4)), KarmaSpace.Units(new Vector2(16, 8)), false);
            yield return Wait(1); ClearPatternObjects();
            shieldUntil = shieldWarningUntil = 0;
            // Placement is protected, then the actual trial begins with a full warning.
            game.Player.ConfineArena(20 - KarmaSpace.Units(7.4f), 20 + KarmaSpace.Units(7.4f));
            yield return Motion(new Vector2(20, KarmaSpace.GuardianGroundY), 0.5f, Vector2.zero);
            Mark("결계 좌측", new Vector2(20 - KarmaSpace.Units(8), KarmaSpace.Units(5)), KarmaSpace.Units(new Vector2(0.25f, 10)), false);
            Mark("결계 우측", new Vector2(20 + KarmaSpace.Units(8), KarmaSpace.Units(5)), KarmaSpace.Units(new Vector2(0.25f, 10)), false);
            trial.Begin(Mathf.Clamp(Tuning.trialAttackCount, 3, 8), Tuning.trialAllowedHits);
            state = ActionState.Attack;
            for (int i = 0; i < trial.Total; i++)
            {
                bool left = i % 2 == 0;
                float y = KarmaSpace.Units(i % 3 == 1 ? 3.3f : 1.05f);
                Vector2 start = new Vector2(20 + KarmaSpace.Units(left ? -7.2f : 7.2f), y);
                Vector2 velocity = KarmaSpace.Units(new Vector2(left ? 10 : -10, 0));
                var mark = PathMark(start, start + velocity * 1.4f, WaveSize);
                Intent = "GD05 " + TrialStatus + (trial.Hits > trial.Allowed ? " · 종료 후 폭발 예정" : " · 회피/반격");
                yield return Wait(Warning(0.7f)); Destroy(mark.gameObject);
                Attack(start, velocity, WaveSize, 1.4f, 0.65f);
                yield return Wait(1.45f);
                trial.CompleteAttack();
                yield return Wait(Mathf.Max(0.4f, Tuning.trialAttackGap * Tempo));
            }
            // Snapshot only after the final attack expires. Threshold crossing never ends early.
            bool passed = trial.Passed;
            ClearPatternObjects(); trial.Clear();
            if (!passed)
            {
                Intent = "GD05 시험 실패 · 결계 폭발 예고 / 반격 가능";
                var mark = Mark("결계 폭발", new Vector2(20, KarmaSpace.Units(4)), KarmaSpace.Units(new Vector2(16, 8)));
                yield return Wait(0.9f); Destroy(mark.gameObject);
                Attack(new Vector2(20, KarmaSpace.Units(4)), Vector2.zero, KarmaSpace.Units(new Vector2(16, 8)), 0.15f, Tuning.trialExplosionMultiplier);
                yield return Wait(0.2f); ClearPatternObjects();
                game.Player.ReleaseArena();
                yield return Recovery(1.5f);
            }
            else
            {
                game.Player.ReleaseArena();
                yield return Recovery(Mathf.Max(1, Tuning.trialStunSeconds), false, "GD05 시험 성공 · 무력화 / 지금 공격하세요");
            }
        }
    }
}
