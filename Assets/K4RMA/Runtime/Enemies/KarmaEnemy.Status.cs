using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        public void ApplyBurn()
        {
            if (!Alive || !game.IsCombat || Invulnerable) return;
            burn.Apply(Time.time, Tuning.enemyBurnDuration, Tuning.enemyBurnInterval, 0);
        }
        void UpdateBurn()
        {
            float unused;
            if (burn.ConsumeTick(Time.time, out unused) && !Invulnerable)
            {
                // Damage over time is not a new attack: never reflect it or retaliate against it.
                KarmaRemnantVFX.Impact(game, Position, Essence.Flame);
                ReceiveDamage(Tuning.enemyBurnDamage * (Armored ? game.Config.enemyWardDamageMultiplier : 1));
            }
        }
    }
}
