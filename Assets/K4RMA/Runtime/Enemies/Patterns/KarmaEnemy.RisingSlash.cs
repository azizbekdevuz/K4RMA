using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        readonly Vector2 bodyAttackSize = KarmaSpace.Units(new Vector2(1.4f, 1.8f));
        IEnumerator RiseSlam(bool shield)
        {
            Vector2 start = Position;
            Vector2 apex = new Vector2(Mathf.Clamp(start.x + direction * KarmaSpace.Units(2), KarmaStage.ArenaLeft + 3, KarmaStage.ArenaRight - 3), KarmaSpace.GuardianGroundY + Mathf.Clamp(Tuning.riseHeight, KarmaSpace.Units(2), KarmaSpace.Units(6)));
            state = ActionState.Telegraph;
            var path = PathMark(start, apex, bodyAttackSize);
            yield return Wait(Warning(0.65f)); Destroy(path.gameObject);
            fullShield = shield;
            yield return Motion(apex, 0.4f, bodyAttackSize);
            Vector2 landing = new Vector2(Mathf.Clamp(game.Player.Position.x, KarmaStage.ArenaLeft + 3, KarmaStage.ArenaRight - 3), KarmaSpace.GuardianGroundY);
            // Limit the slam's horizontal reach; target is immutable once displayed.
            landing.x = Mathf.Clamp(landing.x, apex.x - KarmaSpace.Units(4), apex.x + KarmaSpace.Units(4));
            var drop = PathMark(apex, landing, bodyAttackSize);
            var left = PathMark(new Vector2(landing.x, KarmaSpace.Units(0.55f)), new Vector2(Mathf.Max(KarmaStage.ArenaLeft, landing.x - KarmaSpace.Units(9)), KarmaSpace.Units(0.55f)), KarmaSpace.Units(new Vector2(1.1f, 0.65f)));
            var right = PathMark(new Vector2(landing.x, KarmaSpace.Units(0.55f)), new Vector2(Mathf.Min(KarmaStage.ArenaRight, landing.x + KarmaSpace.Units(9)), KarmaSpace.Units(0.55f)), KarmaSpace.Units(new Vector2(1.1f, 0.65f)));
            Intent = shield ? "UP04 착지점 고정 · 착지 시 방어 해제/지면 파편" : "UP01 착지점 고정 · 착지 충격파는 점프로";
            yield return Wait(Warning(0.55f)); Destroy(drop.gameObject);
            yield return Motion(landing, 0.3f, bodyAttackSize);
            fullShield = false; Destroy(left.gameObject); Destroy(right.gameObject);
            // The pair shares one contact so overlapping waves cannot hit twice.
            // UP04 fragments replace the shockwave. They are not added on top of it.
            var groundContact = new KarmaAttackContact();
            foreach (float side in new[] { -1f, 1f })
            {
                var shot = Attack(new Vector2(landing.x, KarmaSpace.Units(0.55f)), Vector2.right * side * KarmaSpace.Units(10), KarmaSpace.Units(new Vector2(1.1f, 0.65f)), 0.9f, 1, groundContact);
                shot.name = shield ? "착지 방어막 파편" : "착지 지면 충격파";
                if (shield) shot.Visual.color = KarmaArtwork.BarrierColor;
            }
            if (!shield && Enraged)
            {
                yield return Wait(0.2f);
                Vector2 aftershock = new Vector2(Mathf.Clamp(game.Player.Position.x, KarmaStage.ArenaLeft + 3, KarmaStage.ArenaRight - 3), KarmaSpace.Units(2.5f));
                Vector2 afterSize = KarmaSpace.Units(new Vector2(1.7f, 5));
                var warning = Mark("착지 잔흔 · 지연 상승", aftershock, afterSize);
                Intent = "착지 잔흔 고정 · 옆으로 이탈";
                yield return Wait(Warning(0.65f)); Destroy(warning.gameObject);
                Attack(aftershock, Vector2.zero, afterSize, 0.25f, 0.8f);
                yield return Wait(0.3f);
            }
            else yield return Wait(0.9f);
            yield return Recovery(1.2f);
        }
        IEnumerator AirPursuit(bool recover)
        {
            Vector2 origin = Position, target = origin;
            SpriteRenderer path = null;
            state = ActionState.Telegraph;
            float elapsed = 0;
            while (elapsed < 0.4f && Active)
            {
                if (game.Paused) { yield return null; continue; }
                target = game.Player.Position;
                target.x = Mathf.Clamp(target.x, Mathf.Max(KarmaStage.ArenaLeft + 2, origin.x - KarmaSpace.Units(7)), Mathf.Min(KarmaStage.ArenaRight - 2, origin.x + KarmaSpace.Units(7)));
                target.y = Mathf.Clamp(target.y, KarmaSpace.Units(2.5f), KarmaSpace.Units(7));
                if (path == null) path = PathMark(origin, target, bodyAttackSize, false);
                else path.GetComponent<KarmaAttackWarning>().SetPath(path, origin, target, bodyAttackSize);
                elapsed += Time.deltaTime; yield return null;
            }
            if (path != null) Destroy(path.gameObject);
            path = PathMark(origin, target, bodyAttackSize);
            Intent = "UP02 목표 고정 · 추적 종료";
            yield return Wait(Warning(0.4f)); Destroy(path.gameObject);
            yield return Motion(target, 0.45f, bodyAttackSize);
            // Return is presentation only: no unannounced falling hit box.
            yield return Motion(new Vector2(target.x, KarmaSpace.GuardianGroundY), 0.4f, Vector2.zero);
            if (recover && Enraged)
            {
                yield return Wait(Mathf.Max(0.45f, Tuning.crossingReset * Tempo));
                yield return AirPursuit(false);
            }
            if (recover) yield return Recovery(1);
        }
        IEnumerator GroundBlade()
        {
            Vector2 start = Position;
            float width = Mathf.Clamp(Tuning.groundBladeWidth, KarmaSpace.Units(2), KarmaSpace.Units(7)), height = Mathf.Clamp(Tuning.groundBladeHeight, KarmaSpace.Units(3), KarmaSpace.Units(7));
            Vector2 center = new Vector2(start.x + direction * (width / 2 + KarmaSpace.Units(0.5f)), height / 2);
            Vector2 size = new Vector2(width, height);
            var mark = Mark("전방 지면 칼날 · 점프 회피 불가", center, size);
            state = ActionState.Telegraph; yield return Wait(Warning(0.65f));
            Destroy(mark.gameObject);
            Attack(center, Vector2.zero, size, 0.4f);
            yield return Motion(start + Vector2.up * KarmaSpace.Units(4), 0.4f, Vector2.zero);
            yield return Wait(0.3f); ClearPatternObjects();
            yield return Motion(start, 0.3f, Vector2.zero);
            yield return Recovery(1.2f);
        }
        IEnumerator Crossings()
        {
            state = ActionState.Telegraph;
            Mark("UP05 중앙 도약 · 이동 접촉 피해 없음", new Vector2(20, KarmaSpace.Units(4)), KarmaSpace.Units(new Vector2(2, 2)));
            yield return Wait(0.8f); ClearPatternObjects();
            yield return Motion(new Vector2(20, KarmaSpace.Units(5)), 0.5f, Vector2.zero);
            yield return GroundCrossing(6, 34);
            yield return Wait(Mathf.Max(0.45f, Tuning.crossingReset * Tempo));
            yield return AirPursuit(false);
            yield return Wait(Mathf.Max(0.45f, Tuning.crossingReset * Tempo));
            yield return GroundCrossing(34, 6);
            yield return Wait(Mathf.Max(0.45f, Tuning.crossingReset * Tempo));
            yield return Motion(new Vector2(Position.x, KarmaSpace.Units(6)), 0.4f, Vector2.zero);
            Vector2 landing = new Vector2(Mathf.Clamp(game.Player.Position.x, KarmaStage.ArenaLeft + 3, KarmaStage.ArenaRight - 3), KarmaSpace.GuardianGroundY);
            var mark = PathMark(Position, landing, bodyAttackSize);
            Intent = "UP05 최종 낙하 위치 고정 · 범위 밖으로";
            yield return Wait(0.8f); Destroy(mark.gameObject);
            yield return Motion(landing, 0.45f, bodyAttackSize);
            yield return Recovery(3f, false, "UP05 탈진 · 지금 공격하세요");
        }
        IEnumerator GroundCrossing(float fromX, float toX)
        {
            yield return Motion(new Vector2(fromX, KarmaSpace.GuardianGroundY), 0.45f, Vector2.zero);
            Vector2 target = new Vector2(toX, KarmaSpace.GuardianGroundY);
            Vector2 slashSize = KarmaSpace.Units(new Vector2(1.6f, 0.85f));
            var mark = PathMark(Position, target, slashSize);
            Intent = "UP05 지상 횡단 고정 · 점프/체공";
            yield return Wait(Warning(Tuning.crossingWarning)); Destroy(mark.gameObject);
            yield return Motion(target, 1.1f / KarmaSpace.Scale, slashSize);
            yield return Wait(1.1f * (1 - 1 / KarmaSpace.Scale));
        }
    }
}
