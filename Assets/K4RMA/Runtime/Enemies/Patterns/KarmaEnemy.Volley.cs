using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void BeginVolley()
        {
            volleyCount = 1 + 2 * (CombatPhase - 1);
            volleyLeft = CombatPhase + (finalGuardian ? 1 : 0);
            state = ActionState.Volley; timer = 0;
            Intent = "추적 연사 / 발판에 머무르지 마세요";
        }
    }
}
