using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        Vector2 WaveSize { get { return new Vector2(Mathf.Max(KarmaSpace.Units(0.2f), Tuning.swordWidth), Mathf.Clamp(Tuning.swordHeight, KarmaSpace.Units(0.2f), KarmaSpace.Units(0.8f))); } }
        float WaveLife { get { return Mathf.Clamp(Tuning.swordLifetime, 0.5f, 2); } }
        IEnumerator StaggeredWaves(bool scars)
        {
            state = ActionState.Telegraph;
            // Alternating lanes force repeated jump/landing decisions.
            Vector2 first = new Vector2(Position.x + direction * KarmaSpace.Scale, KarmaSpace.Units(1.05f));
            Vector2 second = new Vector2(Position.x + direction * KarmaSpace.Scale, KarmaSpace.Units(3.3f));
            Vector2 velocity = Vector2.right * direction * Mathf.Max(KarmaSpace.Scale, Tuning.swordSpeed);
            int shots = Enraged ? 4 : 3;
            for (int i = 0; i < shots; i++)
            {
                Vector2 origin = i % 2 == 0 ? first : second;
                var mark = PathMark(origin, origin + velocity * WaveLife, WaveSize);
                Intent = "교차 검기 " + (i + 1) + "/" + shots + (i % 2 == 0 ? " · 낮은 검기 / 점프" : " · 높은 검기 / 착지");
                yield return Wait(i == 0 ? Warning(0.65f) : Mathf.Max(0.42f, 0.54f * Tempo));
                state = ActionState.Attack; animator.Attack(); Destroy(mark.gameObject);
                Attack(origin, velocity, WaveSize, WaveLife);
            }
            yield return Wait(WaveLife);
            if (scars)
            {
                // Two separated locked scars, with at least a 3.6-unit escape gap.
                float endX = first.x + velocity.x * WaveLife;
                float lowX = Mathf.Clamp(Mathf.Min(first.x, endX), KarmaStage.ArenaLeft + 1, KarmaStage.ArenaRight - 1);
                float highX = Mathf.Clamp(Mathf.Max(first.x, endX), KarmaStage.ArenaLeft + 1, KarmaStage.ArenaRight - 1);
                bool pair = highX - lowX >= KarmaSpace.Units(8);
                float x = pair ? Mathf.Clamp(game.Player.Position.x, lowX + KarmaSpace.Units(3.6f), highX - KarmaSpace.Units(3.6f)) : (lowX + highX) / 2;
                var size = KarmaSpace.Units(new Vector2(1.8f, 5));
                Vector2 a = new Vector2(x - (pair ? KarmaSpace.Units(2.7f) : 0), KarmaSpace.Units(2.5f)), b = new Vector2(x + KarmaSpace.Units(2.7f), KarmaSpace.Units(2.5f));
                Mark("검흔 상승 범위", a, size);
                if (pair) Mark("검흔 상승 범위", b, size);
                Intent = "SW03 검흔 고정 · 상승 예고 / 검흔 사이로";
                yield return Wait(Warning(0.75f)); ClearPatternObjects();
                Attack(a, Vector2.zero, size, 0.25f);
                if (pair) Attack(b, Vector2.zero, size, 0.25f);
                yield return Wait(0.25f);
            }
            yield return Recovery(1);
        }
        IEnumerator AimedWave()
        {
            int shots = Enraged ? 3 : 2;
            for (int i = 0; i < shots && Active; i++)
            {
                state = ActionState.Telegraph;
                Vector2 origin = Position + Vector2.right * direction * KarmaSpace.Scale;
                Vector2 aim = Vector2.right * direction;
                Vector2 size = WaveSize * 1.35f;
                float speed = Tuning.swordSpeed * 1.25f;
                SpriteRenderer marker = null;
                float elapsed = 0;
                while (elapsed < 0.4f * Tempo && Active)
                {
                    if (game.Paused) { yield return null; continue; }
                    aim = (game.Player.Position - origin).normalized;
                    if (aim.sqrMagnitude < 0.01f) aim = Vector2.right * direction;
                    if (marker == null) marker = PathMark(origin, origin + aim * speed * WaveLife, size, false);
                    else marker.GetComponent<KarmaAttackWarning>().SetPath(marker, origin, origin + aim * speed * WaveLife, size);
                    elapsed += Time.deltaTime; yield return null;
                }
                if (marker != null) Destroy(marker.gameObject);
                marker = PathMark(origin, origin + aim * speed * WaveLife, size);
                Intent = "SW02 조준 " + (i + 1) + "/" + shots + " 고정 · 사선 밖으로";
                yield return Wait(Warning(0.4f)); Destroy(marker.gameObject);
                state = ActionState.Attack; animator.Attack(aim);
                Attack(origin, aim * speed, size, WaveLife, 1.15f);
                yield return Wait(0.16f);
            }
            yield return Wait(WaveLife);
            yield return Recovery(1.1f);
        }
        bool guardResponsePending;
        IEnumerator CounterGuard()
        {
            state = ActionState.Telegraph;
            var formation = Mark("검막 형성", Position, KarmaSpace.Units(new Vector2(3, 3)));
            yield return Wait(Warning(0.7f)); Destroy(formation.gameObject);
            state = ActionState.Attack; swordGuard = true; guardResponsePending = false;
            swordGuardUntil = FightSeconds + Mathf.Max(0.6f, Tuning.swordGuardDuration);
            nextGuardResponse = FightSeconds;
            var blades = new SpriteRenderer[4];
            for (int i = 0; i < blades.Length; i++)
            {
                blades[i] = KarmaVisuals.Box(effectsRoot, "회전 검막 · 접촉 피해 없음", Position, Vector2.one, Color.white, 13);
                KarmaCombatVFX.ApplySprite(blades[i], "GuardianVFX", "Blade", KarmaSpace.Units(new Vector2(1.2f, 0.5f)));
            }
            while (FightSeconds < swordGuardUntil && Active)
            {
                for (int i = 0; i < blades.Length; i++)
                {
                    float angle = FightSeconds * 3 + i * Mathf.PI / 2;
                    blades[i].transform.position = Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * KarmaSpace.Units(1.6f);
                    blades[i].transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
                }
                if (guardResponsePending)
                {
                    guardResponsePending = false;
                    Vector2 origin = Position, aim = (game.Player.Position - origin).normalized;
                    if (aim.sqrMagnitude < 0.01f) aim = Vector2.right * direction;
                    var mark = PathMark(origin, origin + aim * Tuning.swordSpeed * WaveLife, WaveSize);
                    Intent = "SW04 반격 검기 방향 고정 · 회피";
                    yield return Wait(Mathf.Max(0.3f, Tuning.counterWarning));
                    Destroy(mark.gameObject);
                    Attack(origin, aim * Tuning.swordSpeed, WaveSize, WaveLife);
                    nextGuardResponse = FightSeconds + Mathf.Max(0.5f, Tuning.swordGuardResponseCooldown);
                }
                yield return null;
            }
            swordGuard = false; guardResponsePending = false;
            // Recovery cancels all remaining guard shots before exposing the boss.
            yield return Recovery(1.4f);
        }
        IEnumerator AerialSwords()
        {
            state = ActionState.Telegraph;
            Mark("도약 준비 · 접촉 피해 없음", Position, KarmaSpace.Units(new Vector2(2, 2)));
            yield return Wait(0.8f); ClearPatternObjects();
            Vector2 ground = Position;
            yield return Motion(new Vector2(Mathf.Clamp(Position.x, KarmaStage.ArenaLeft + 6, KarmaStage.ArenaRight - 6), KarmaSpace.Units(6)), 0.5f, Vector2.zero);
            fullShield = true;
            for (int i = 0; i < Mathf.Clamp(Tuning.aerialSwordCount, 1, 8); i++)
            {
                Vector2 origin = Position + KarmaSpace.Units(new Vector2(i % 2 == 0 ? -1 : 1, -0.8f));
                Vector2 aim = (game.Player.Position - origin).normalized;
                var mark = PathMark(origin, origin + aim * Tuning.swordSpeed * WaveLife, WaveSize);
                Intent = "SW05 구형 무적 · 사출 " + (i + 1) + "/" + Tuning.aerialSwordCount + " 방향 고정";
                yield return Wait(Warning(0.5f)); Destroy(mark.gameObject);
                Attack(origin, aim * Tuning.swordSpeed, WaveSize, WaveLife);
                yield return Wait(Mathf.Max(0.28f, Tuning.aerialSwordGap * Tempo));
            }
            ClearPatternObjects(); fullShield = false;
            yield return Motion(ground, 0.5f, Vector2.zero);
            yield return Recovery(2);
        }
    }
}
