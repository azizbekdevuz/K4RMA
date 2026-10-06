using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        Color AttackColor()
        {
            if (selected == EnemyAttack.SwordWave || selected == EnemyAttack.FlameFan || selected == EnemyAttack.BlazingCharge || selected == EnemyAttack.EmberAegis || selected == EnemyAttack.Overdrive)
                return KarmaArtwork.BossSwordColor;
            if (selected == EnemyAttack.RisingSlash || selected == EnemyAttack.AirWave || KarmaPatternCatalog.IsCharge(selected)) return game.Config.dashColor;
            if (selected == EnemyAttack.Ward || selected == EnemyAttack.GuardCounter) return game.Config.wardColor;
            return new Color(1, 0.75f, 0.3f);
        }
        void ShowWard(Color color)
        {
            color = KarmaArtwork.BarrierColor; color.a = 0.7f;
            indicator = KarmaRemnantVFX.CreateWardHalo(transform, color);
            indicator.name = "Guardian barrier";
            indicator.color = color;
            indicator.transform.localPosition = new Vector3(direction * 0.8f, 0, 0);
            indicator.transform.localScale = new Vector3(1.15f, 2.8f, 1);
            indicator.sortingOrder = 9;
        }
        void Recover(float seconds)
        {
            
            ClearIndicator(); state = ActionState.Recover; timer = seconds * Tuning.recoveryMultiplier; Intent = "빈틈 발생 / 지금 공격하세요";
        }
        void ClearIndicator() { if (indicator != null) Destroy(indicator.gameObject); indicator = null; }
        void OnDestroy() { ClearIndicator(); }
    }
}
