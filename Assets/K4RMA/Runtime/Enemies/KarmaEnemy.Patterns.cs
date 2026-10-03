using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void BeginAttack()
        {
            // Deterministic rotation makes sacrifice-order comparisons reproducible.
            // Insert pressure without replacing inherited attacks (including cycles of length 3).
            if (CombatPhase == 3 && extraPressure)
            {
                selected = EnemyAttack.Bolt; extraPressure = false;
            }
            else
            {
                selected = attacks[turn++ % attacks.Count];
                extraPressure = CombatPhase == 3 && turn % 3 == 0;
            }
            direction = game.Player.Position.x < Position.x ? -1 : 1;
            lockedX = game.Player.Position.x;
            state = ActionState.Telegraph;
            timer = (KarmaPatternCatalog.IsFusion(selected) ? game.Config.fusionTelegraphTime
                : game.Config.enemyTelegraphTime) * (CombatPhase == 3 ? 0.8f : 1);
            Intent = KarmaPatternCatalog.Label(selected);
            Color color = AttackColor();
            Vector2 markerPos = selected == EnemyAttack.GroundStrike ? new Vector2(lockedX, 0.15f) : Position + Vector2.up * 2.1f;
            Vector2 markerSize = selected == EnemyAttack.GroundStrike ? new Vector2(3.4f, 0.22f) : new Vector2(0.5f, 0.5f);
            if (KarmaPatternCatalog.IsCharge(selected)) { markerPos = new Vector2(20, 0.2f); markerSize = new Vector2(37, 0.12f); }
            indicator = KarmaVisuals.Box(game.World, "Attack telegraph", markerPos, markerSize, color, 6);
            if (KarmaPatternCatalog.IsFusion(selected))
                KarmaVisuals.Flash(game.World, Position + Vector2.up * 2.5f,
                    new Vector2(2.5f, 0.2f), game.Config.ColorOf(selected == EnemyAttack.BlazingCharge ? Essence.Dash : Essence.Ward), timer);
        }
        void ExecuteAttack()
        {
            ClearIndicator();
            switch (selected)
            {
                case EnemyAttack.GroundStrike:
                    ExecuteGroundStrike();
                    break;
                case EnemyAttack.FlameFan:
                    BeginMeteorRain();
                    break;
                case EnemyAttack.Overdrive:
                    BeginOverdrive();
                    break;
                case EnemyAttack.Bolt:
                    BeginVolley();
                    break;
                case EnemyAttack.Charge:
                case EnemyAttack.BlazingCharge:
                case EnemyAttack.GuardedCharge:
                    BeginChargePattern();
                    break;
                case EnemyAttack.Ward:
                case EnemyAttack.EmberAegis:
                    BeginWardPattern();
                    break;
            }
        }
        void StartCharge()
        {
            ClearIndicator();
            state = ActionState.Charge; timer = 0.85f; nextTrail = 0;
            Intent = KarmaPatternCatalog.Label(selected);
            if (selected == EnemyAttack.GuardedCharge) ShowWard(game.Config.wardColor);
        }
    }
}
