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
            // Begin between attacks so an in-flight recovery cannot erase the new shield.
            if (!running && FightSeconds >= nextShield && FightSeconds >= shieldUntil)
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
            int parries = game.Player.ParryCount;
            var contact = new KarmaAttackContact();
            Attack(center, Vector2.zero, size, 0.18f, 1.5f, contact);
            TryAttackHit(center, center, size, Damage * 1.5f, contact, 0);
            if (game.Player.ParryCount != parries) yield break;
            // Include contacts made after the first frame of the active hit box.
            yield return Wait(0.2f);
            if (!Active || pendingParry) yield break;
            bool refreshed = Tuning.dodgeRefreshesShield || contact.DealtDamage;
            if (refreshed)
            {
                RefreshAutoShield();
                Intent = "GD02 강공격 적중 · 자동방어막 갱신";
            }
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
            for (int i = 0; i < count; i++)
            {
                pieces[i] = KarmaVisuals.Box(effectsRoot, "회수 조각 · 피해 없음", starts[i] + aims[i] * 0.8f,
                    Vector2.one, new Color(0.3f, 0.85f, 1, 0.65f), 8);
                if (!KarmaCombatVFX.ApplySprite(pieces[i], "GuardShard", "Ward", KarmaSpace.Units(new Vector2(0.4f, 0.4f)), true))
                    KarmaCombatVFX.ApplySprite(pieces[i], "GuardianVFX", "Ward", KarmaSpace.Units(new Vector2(0.4f, 0.4f)), true);
            }
            Intent = "GD03 방패 회수 · 복귀 판정 없음";
            float elapsed = 0;
            while (elapsed < 0.6f && Active)
            {
                if (game.Paused) { yield return null; continue; }
                elapsed += Time.deltaTime;
                for (int i = 0; i < count; i++)
                {
                    pieces[i].transform.position = Vector2.Lerp(starts[i] + aims[i] * 0.8f, Position, Mathf.Clamp01(elapsed / 0.6f));
                    Vector2 heading = Position - (Vector2)pieces[i].transform.position;
                    pieces[i].transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
                }
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
            wall.gameObject.AddComponent<KarmaSurface>(); wall.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
            GuardDecoration("GuardPillar", "Pillar", center, size, new Color(0.3f, 0.85f, 1, 0.6f));
            yield return Wait(0.5f);
            yield return Recovery(Mathf.Max(0.3f, Tuning.barrierRecovery));
        }
    }
}
