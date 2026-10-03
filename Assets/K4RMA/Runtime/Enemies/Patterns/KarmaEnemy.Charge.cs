using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void BeginChargePattern()
        {
            remainingCharges = CombatPhase >= 2 ? 1 : 0;
            StartCharge();
        }
    }
}
