using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        Color AttackColor()
        {
            if (selected == EnemyAttack.FlameFan || selected == EnemyAttack.BlazingCharge || selected == EnemyAttack.EmberAegis || selected == EnemyAttack.Overdrive)
                return game.Config.flameColor;
            if (KarmaPatternCatalog.IsCharge(selected)) return game.Config.dashColor;
            if (selected == EnemyAttack.Ward) return game.Config.wardColor;
            return new Color(1, 0.75f, 0.3f);
        }
        void ShowWard(Color color)
        {
            color.a = 0.4f;
            indicator = KarmaVisuals.Box(transform, "Enemy Ward", Vector2.zero, new Vector2(1.8f, 2.7f), color, 1);
        }
        void Recover(float seconds)
        {
            overlay.Cancel();
            ClearIndicator(); state = ActionState.Recover; timer = seconds * Tuning.recoveryMultiplier; Intent = "빈틈 발생 / 지금 공격하세요";
        }
        void ClearIndicator() { if (indicator != null) Destroy(indicator.gameObject); indicator = null; }
        void OnDestroy() { ClearIndicator(); }
    }
}
