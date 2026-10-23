using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        IEnumerator GroundStrike()
        {
            int count = Enraged ? 3 : 2;
            for (int i = 0; i < count && Active; i++)
            {
                // Sample again BETWEEN swings; each orange warning locks its target.
                float dx = game.Player.Position.x - Position.x;
                direction = dx < 0 ? -1 : 1;
                art.localScale = new Vector3(direction * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                bool finish = i == count - 1;
                float step = Mathf.Clamp(Mathf.Abs(dx) - KarmaSpace.Units(1.8f), 0, KarmaSpace.Units(finish ? 3 : 2));
                Vector2 target = new Vector2(Mathf.Clamp(Position.x + direction * step, KarmaStage.BossLeftWall, KarmaStage.BossRightWall), Position.y);
                Vector2 size = KarmaSpace.Units(finish ? new Vector2(4.2f, 2.1f) : new Vector2(3.3f, 1.8f));
                Vector2 center = target + Vector2.right * direction * KarmaSpace.Units(1.6f);
                state = ActionState.Telegraph;
                var mark = Mark(finish ? "연속 베기 · 마무리" : "연속 베기 · 첫 타", center, size);
                Intent = "연속 베기 " + (i + 1) + "/" + count + " · 범위 고정";
                yield return Wait(Warning(i == 0 ? game.Config.enemyTelegraphTime : 0.55f));
                yield return Motion(target, 0.16f, Vector2.zero);
                Destroy(mark.gameObject); animator.Attack(); state = ActionState.Attack;
                Attack(center, Vector2.zero, size, 0.18f, finish ? 1.25f : 0.85f);
                yield return Wait(0.2f);
            }
            yield return Recovery(0.85f);
        }
    }
}
