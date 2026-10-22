using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        IEnumerator GroundStrike()
        {
            Vector2 center = Position + Vector2.right * direction * 1.8f;
            Vector2 size = new Vector2(3.3f, 2);
            state = ActionState.Telegraph;
            var mark = Mark("근접 베기 범위", center, size);
            yield return Wait(Mathf.Max(0.65f, game.Config.enemyTelegraphTime));
            Destroy(mark.gameObject); animator.Attack(); state = ActionState.Attack;
            Attack(center, Vector2.zero, size, 0.2f);
            yield return Wait(0.2f);
            yield return Recovery(0.9f);
        }
    }
}
