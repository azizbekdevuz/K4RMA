using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void BeginWardPattern()
        {
            defenseHit = false; state = ActionState.Ward; timer = 0.8f;
            ShowWard(game.Config.wardColor); Intent = "방어 중 · 보호가 끝나면 빈틈";
        }
        void EndDefense()
        {
            if (!defenseHit && selected != EnemyAttack.GuardCounter) { Recover(1.1f); return; }
            ClearIndicator();
            selected = selected != EnemyAttack.GuardCounter ? EnemyAttack.GroundStrike
                : PrimaryType == Essence.Dash ? EnemyAttack.RisingSlash
                : PrimaryType == Essence.Flame ? EnemyAttack.SwordWave
                : game.Player.Essences.HasRemnant(Essence.Dash) ? EnemyAttack.RisingSlash : EnemyAttack.SwordWave;
            direction = game.Player.Position.x < Position.x ? -1 : 1; lockedX = Position.x + direction * 1.8f;
            state = ActionState.Telegraph; timer = Mathf.Max(0.55f, game.Config.enemyTelegraphTime);
            Intent = KarmaPatternCatalog.Label(selected);
            indicator = KarmaVisuals.Box(game.World, "연계 예고", Position + Vector2.up * 2,
                new Vector2(1.2f, 0.2f), AttackColor(), 6);
        }
    }
}
