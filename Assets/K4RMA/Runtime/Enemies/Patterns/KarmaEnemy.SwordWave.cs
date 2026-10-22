using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        Vector2 WaveSize { get { return new Vector2(Mathf.Max(0.2f, Tuning.swordWidth), Mathf.Clamp(Tuning.swordHeight, 0.2f, 0.8f)); } }
        float WaveLife { get { return Mathf.Clamp(Tuning.swordLifetime, 0.5f, 2); } }
        IEnumerator StaggeredWaves(bool scars)
        {
            state = ActionState.Telegraph;
            // Two fixed, vertically disjoint lanes. Ground is safe from the second shot.
            Vector2 first = new Vector2(Position.x + direction, 1.05f);
            Vector2 second = new Vector2(Position.x + direction, 3.3f);
            Vector2 velocity = Vector2.right * direction * Mathf.Max(1, Tuning.swordSpeed);
            var low = PathMark(first, first + velocity * WaveLife, WaveSize);
            var high = PathMark(second, second + velocity * WaveLife, WaveSize);
            yield return Wait(0.7f);
            state = ActionState.Attack; animator.Attack(); Destroy(low.gameObject);
            Attack(first, velocity, WaveSize, WaveLife);
            yield return Wait(0.65f);
            Destroy(high.gameObject); Attack(second, velocity, WaveSize, WaveLife);
            yield return Wait(WaveLife);
            if (scars)
            {
                // Two separated locked scars, with at least a 3.6-unit escape gap.
                float endX = first.x + velocity.x * WaveLife;
                float lowX = Mathf.Clamp(Mathf.Min(first.x, endX), 1, 39);
                float highX = Mathf.Clamp(Mathf.Max(first.x, endX), 1, 39);
                bool pair = highX - lowX >= 8;
                float x = pair ? Mathf.Clamp(game.Player.Position.x, lowX + 3.6f, highX - 3.6f) : (lowX + highX) / 2;
                var size = new Vector2(1.8f, 5);
                Vector2 a = new Vector2(x - (pair ? 2.7f : 0), 2.5f), b = new Vector2(x + 2.7f, 2.5f);
                Mark("검흔 상승 범위", a, size);
                if (pair) Mark("검흔 상승 범위", b, size);
                Intent = "SW03 검흔 고정 · 0.8초 후 상승 / 검흔 사이로";
                yield return Wait(0.8f); ClearPatternObjects();
                Attack(a, Vector2.zero, size, 0.25f);
                if (pair) Attack(b, Vector2.zero, size, 0.25f);
                yield return Wait(0.25f);
            }
            yield return Recovery(1);
        }
        IEnumerator AimedWave()
        {
            state = ActionState.Telegraph;
            Vector2 origin = Position + Vector2.right * direction;
            Vector2 aim = Vector2.right * direction;
            Vector2 size = WaveSize * 1.5f;
            SpriteRenderer marker = null;
            float elapsed = 0;
            while (elapsed < 0.6f && Active)
            {
                if (game.Paused) { yield return null; continue; }
                aim = (game.Player.Position - origin).normalized;
                if (aim.sqrMagnitude < 0.01f) aim = Vector2.right * direction;
                if (marker == null) marker = PathMark(origin, origin + aim * Tuning.swordSpeed * WaveLife, size, false);
                else marker.GetComponent<KarmaAttackWarning>().SetPath(marker, origin, origin + aim * Tuning.swordSpeed * WaveLife, size);
                elapsed += Time.deltaTime; yield return null;
            }
            if (marker != null) Destroy(marker.gameObject);
            marker = PathMark(origin, origin + aim * Tuning.swordSpeed * WaveLife, size);
            Intent = "SW02 조준 고정 · 지금 사선 밖으로";
            yield return Wait(0.3f); Destroy(marker.gameObject);
            state = ActionState.Attack; animator.Attack(aim);
            Attack(origin, aim * Tuning.swordSpeed, size, WaveLife, 1.3f);
            yield return Wait(WaveLife);
            yield return Recovery(1.3f);
        }
        bool wardResponsePending;
        IEnumerator CounterWard()
        {
            state = ActionState.Telegraph;
            var formation = Mark("검막 형성", Position, new Vector2(3, 3));
            yield return Wait(0.7f); Destroy(formation.gameObject);
            state = ActionState.Attack; swordWard = true; wardResponsePending = false;
            swordWardUntil = FightSeconds + Mathf.Max(0.6f, Tuning.swordWardDuration);
            nextWardResponse = FightSeconds;
            var blades = new SpriteRenderer[4];
            for (int i = 0; i < blades.Length; i++) blades[i] = Mark("회전 검막 · 접촉 피해 없음", Position, new Vector2(0.2f, 0.8f));
            while (FightSeconds < swordWardUntil && Active)
            {
                for (int i = 0; i < blades.Length; i++)
                {
                    float angle = FightSeconds * 3 + i * Mathf.PI / 2;
                    blades[i].transform.position = Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.6f;
                    blades[i].transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
                }
                if (wardResponsePending)
                {
                    wardResponsePending = false;
                    Vector2 origin = Position, aim = (game.Player.Position - origin).normalized;
                    if (aim.sqrMagnitude < 0.01f) aim = Vector2.right * direction;
                    var mark = PathMark(origin, origin + aim * Tuning.swordSpeed * WaveLife, WaveSize);
                    Intent = "SW04 반격 검기 방향 고정 · 회피";
                    yield return Wait(Mathf.Max(0.3f, Tuning.counterWarning));
                    Destroy(mark.gameObject);
                    Attack(origin, aim * Tuning.swordSpeed, WaveSize, WaveLife);
                    nextWardResponse = FightSeconds + Mathf.Max(0.5f, Tuning.swordWardResponseCooldown);
                }
                yield return null;
            }
            swordWard = false; wardResponsePending = false;
            // Recovery cancels all remaining ward shots before exposing the boss.
            yield return Recovery(1.4f);
        }
        IEnumerator AerialSwords()
        {
            state = ActionState.Telegraph;
            Mark("도약 준비 · 접촉 피해 없음", Position, new Vector2(2, 2));
            yield return Wait(0.8f); ClearPatternObjects();
            Vector2 ground = Position;
            yield return Motion(new Vector2(Mathf.Clamp(Position.x, 6, 34), 6), 0.5f, Vector2.zero);
            fullShield = true;
            for (int i = 0; i < Mathf.Clamp(Tuning.aerialSwordCount, 1, 8); i++)
            {
                Vector2 origin = Position + new Vector2(i % 2 == 0 ? -1 : 1, -0.8f);
                Vector2 aim = (game.Player.Position - origin).normalized;
                var mark = PathMark(origin, origin + aim * Tuning.swordSpeed * WaveLife, WaveSize);
                Intent = "SW05 구형 무적 · 사출 " + (i + 1) + "/" + Tuning.aerialSwordCount + " 방향 고정";
                yield return Wait(0.45f); Destroy(mark.gameObject);
                Attack(origin, aim * Tuning.swordSpeed, WaveSize, WaveLife);
                yield return Wait(Mathf.Max(0.5f, Tuning.aerialSwordGap));
            }
            ClearPatternObjects(); fullShield = false;
            yield return Motion(ground, 0.5f, Vector2.zero);
            yield return Recovery(2);
        }
    }
}
