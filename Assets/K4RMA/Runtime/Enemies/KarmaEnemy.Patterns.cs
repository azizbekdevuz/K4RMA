using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void BeginAttack()
        {
            selected = attacks[turn++ % attacks.Count];
            direction = game.Player.Position.x < Position.x ? -1 : 1;
            lockedX = Position.x + direction * 1.8f;
            state = ActionState.Telegraph;
            timer = Mathf.Max(0.45f, (KarmaPatternCatalog.IsFusion(selected) ? game.Config.fusionTelegraphTime : game.Config.enemyTelegraphTime) * (CombatPhase == 3 ? 0.85f : 1));
            Intent = KarmaPatternCatalog.Label(selected);
            indicator = KarmaVisuals.Box(game.World, "검술 예고",
                selected == EnemyAttack.GroundStrike ? new Vector2(lockedX, 1.1f) : Position + Vector2.up * 2.1f,
                selected == EnemyAttack.GroundStrike ? new Vector2(3.3f, 0.18f) : new Vector2(0.5f, 0.5f), AttackColor(), 6);
            indicator.transform.localRotation = Quaternion.Euler(0, 0, selected == EnemyAttack.GroundStrike ? -20 * direction : 45);
        }
        void ExecuteAttack()
        {
            ClearIndicator();
            art.GetComponent<KarmaCharacterAnimator>().Attack();
            switch (selected)
            {
                case EnemyAttack.GroundStrike: ExecuteGroundStrike(); break;
                case EnemyAttack.SwordWave: FireSwordWave(); Recover(0.85f); break;
                case EnemyAttack.RisingSlash:
                case EnemyAttack.AirWave: BeginRisingPattern(); break;
                case EnemyAttack.Ward:
                case EnemyAttack.GuardCounter: BeginWardPattern(); break;
            }
        }
    }
}
