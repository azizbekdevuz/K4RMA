using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        readonly Vector2 bodyAttackSize = new Vector2(1.4f, 1.8f);
        IEnumerator RiseSlam(bool shield)
        {
            Vector2 start = Position;
            Vector2 apex = new Vector2(Mathf.Clamp(start.x + direction * 2, 3, 37), 1.15f + Mathf.Clamp(Tuning.riseHeight, 2, 6));
            state = ActionState.Telegraph;
            var path = PathMark(start, apex, bodyAttackSize);
            yield return Wait(0.65f); Destroy(path.gameObject);
            fullShield = shield;
            yield return Motion(apex, 0.4f, bodyAttackSize);
            Vector2 landing = new Vector2(Mathf.Clamp(game.Player.Position.x, 3, 37), 1.15f);
            // Limit the slam's horizontal reach; target is immutable once displayed.
            landing.x = Mathf.Clamp(landing.x, apex.x - 4, apex.x + 4);
            var drop = PathMark(apex, landing, bodyAttackSize);
            var left = PathMark(new Vector2(landing.x, 0.55f), new Vector2(Mathf.Max(0, landing.x - 9), 0.55f), new Vector2(1.1f, 0.65f));
            var right = PathMark(new Vector2(landing.x, 0.55f), new Vector2(Mathf.Min(40, landing.x + 9), 0.55f), new Vector2(1.1f, 0.65f));
            Intent = shield ? "UP04 착지점 고정 · 착지 시 방어 해제/지면 파편" : "UP01 착지점 고정 · 착지 충격파는 점프로";
            yield return Wait(0.5f); Destroy(drop.gameObject);
            yield return Motion(landing, 0.3f, bodyAttackSize);
            fullShield = false; Destroy(left.gameObject); Destroy(right.gameObject);
            // UP04 replaces the shockwave; it never stacks another attack on top.
            foreach (float side in new[] { -1f, 1f })
            {
                var shot = Attack(new Vector2(landing.x, 0.55f), Vector2.right * side * 10, new Vector2(1.1f, 0.65f), 0.9f);
                shot.name = shield ? "착지 방어막 파편" : "착지 지면 충격파";
                if (shield) shot.GetComponent<SpriteRenderer>().color = KarmaArtwork.BarrierColor;
            }
            yield return Wait(0.9f);
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
                target.x = Mathf.Clamp(target.x, Mathf.Max(2, origin.x - 7), Mathf.Min(38, origin.x + 7));
                target.y = Mathf.Clamp(target.y, 2.5f, 7);
                if (path == null) path = PathMark(origin, target, bodyAttackSize, false);
                else path.GetComponent<KarmaAttackWarning>().SetPath(path, origin, target, bodyAttackSize);
                elapsed += Time.deltaTime; yield return null;
            }
            if (path != null) Destroy(path.gameObject);
            path = PathMark(origin, target, bodyAttackSize);
            Intent = "UP02 목표 고정 · 추적 종료";
            yield return Wait(0.35f); Destroy(path.gameObject);
            yield return Motion(target, 0.45f, bodyAttackSize);
            // Return is presentation only: no unannounced falling hit box.
            yield return Motion(new Vector2(target.x, 1.15f), 0.4f, Vector2.zero);
            if (recover) yield return Recovery(1);
        }
        IEnumerator GroundBlade()
        {
            Vector2 start = Position;
            float width = Mathf.Clamp(Tuning.groundBladeWidth, 2, 7), height = Mathf.Clamp(Tuning.groundBladeHeight, 3, 7);
            Vector2 center = new Vector2(start.x + direction * (width / 2 + 0.5f), height / 2);
            Vector2 size = new Vector2(width, height);
            var mark = Mark("전방 지면 칼날 · 점프 회피 불가", center, size);
            state = ActionState.Telegraph; yield return Wait(0.65f);
            Destroy(mark.gameObject);
            Attack(center, Vector2.zero, size, 0.4f);
            yield return Motion(start + Vector2.up * 4, 0.4f, Vector2.zero);
            yield return Wait(0.3f); ClearPatternObjects();
            yield return Motion(start, 0.3f, Vector2.zero);
            yield return Recovery(1.2f);
        }
        IEnumerator Crossings()
        {
            state = ActionState.Telegraph;
            Mark("UP05 중앙 도약 · 이동 접촉 피해 없음", new Vector2(20, 4), new Vector2(2, 2));
            yield return Wait(0.8f); ClearPatternObjects();
            yield return Motion(new Vector2(20, 5), 0.5f, Vector2.zero);
            yield return GroundCrossing(6, 34);
            yield return Wait(Mathf.Max(1.1f, Tuning.crossingReset));
            yield return AirPursuit(false);
            yield return Wait(Mathf.Max(1.1f, Tuning.crossingReset));
            yield return GroundCrossing(34, 6);
            yield return Wait(Mathf.Max(1.1f, Tuning.crossingReset));
            yield return Motion(new Vector2(Position.x, 6), 0.4f, Vector2.zero);
            Vector2 landing = new Vector2(Mathf.Clamp(game.Player.Position.x, 3, 37), 1.15f);
            var mark = PathMark(Position, landing, bodyAttackSize);
            Intent = "UP05 최종 낙하 위치 고정 · 범위 밖으로";
            yield return Wait(0.8f); Destroy(mark.gameObject);
            yield return Motion(landing, 0.45f, bodyAttackSize);
            yield return Recovery(3);
        }
        IEnumerator GroundCrossing(float fromX, float toX)
        {
            yield return Motion(new Vector2(fromX, 1.15f), 0.45f, Vector2.zero);
            Vector2 target = new Vector2(toX, 1.15f);
            var mark = PathMark(Position, target, new Vector2(1.4f, 1.4f));
            Intent = "UP05 지상 횡단 고정 · 점프/체공";
            yield return Wait(Mathf.Max(0.5f, Tuning.crossingWarning)); Destroy(mark.gameObject);
            yield return Motion(target, 1.1f, new Vector2(1.4f, 1.4f));
        }
    }
}
