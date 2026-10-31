using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class GD04Pattern : GuardBossPattern
        {
            public GD04Pattern(KarmaEnemy owner) : base(owner) { }
            public override IEnumerator Execute()
            {
                // Front-only defense deliberately supersedes auto-shield to reward a back attack.
                owner.shieldUntil = owner.shieldWarningUntil = 0; owner.frontalGuard = true;
                owner.state = ActionState.Telegraph;
                float x = Mathf.Clamp(owner.Position.x + owner.direction * KarmaSpace.Units(2.5f), KarmaStage.ArenaLeft + 5, KarmaStage.ArenaRight - 5);
                Vector2 center = new Vector2(x, KarmaSpace.Units(3)), size = KarmaSpace.Units(new Vector2(1.4f, 6));
                var mark = owner.Mark("GD04 방벽 상승 · 높이 전체 위험", center, size);
                yield return owner.Wait(owner.Warning(0.8f));
                owner.animator.Attack(Vector2.down); yield return owner.Wait(0.3f);
                Destroy(mark.gameObject); owner.state = ActionState.Attack;
                owner.Attack(center, Vector2.zero, size, 0.4f);
                yield return owner.Wait(0.4f); owner.ClearPatternObjects();
                var wall = owner.Mark("완성 방벽 · 이동 차단/피해 없음", center, size, false);
                owner.game.Player.ResolveBarrier(x, size.x);
                wall.gameObject.AddComponent<KarmaSurface>(); wall.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
                GuardDecoration("GuardBossVFX", "Pillar", center, size, new Color(0.3f, 0.85f, 1, 0.6f));
                yield return owner.Wait(0.5f);
                yield return owner.Recovery(Mathf.Max(0.3f, Settings.barrierRecovery));
            }

        }
    }
}
