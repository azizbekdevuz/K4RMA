using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void BeginWardPattern()
        {
            state = ActionState.Ward; timer = 2.5f;
            overlay.Begin(CombatPhase);
            Intent = selected == EnemyAttack.EmberAegis ? "화염 보호막 / 공격 금지! 공격 시 화상·반복 넉백" : "반사 보호막 / 차단 피해를 모아 사방 충격파로 반사";
            ShowWard(selected == EnemyAttack.EmberAegis ? game.Config.flameColor : game.Config.wardColor);
        }
    }
}
