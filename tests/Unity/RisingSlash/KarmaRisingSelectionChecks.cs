#if UNITY_EDITOR
using System;
using UnityEngine;
using KarmaPrototype;
public sealed class KarmaRisingSelectionChecks : KarmaSelectionFixture
{
    public override Essence Kind => Essence.RisingSlash;
    public override void Run(KarmaEnemy enemy, KarmaGame game)
    {
        var behavior = Get(enemy, "behavior");
        var field = behavior.GetType().GetField("nextAirPursuit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        float oldCooldown = (float)field.GetValue(behavior);
        try
        {
            enemy.transform.position = new Vector3(26, KarmaSpace.GuardianGroundY, 0);
            game.Player.ResetForRoom(new Vector2(20, 8f), true);
            Set(enemy, "previous", (EnemyAttack)(-1));
            field.SetValue(behavior, 0f);
            // This fixture follows the practice sequence, which has already consumed both UP05 thresholds.
            if (!(bool)Select(enemy, 6, out var attack) || attack != EnemyAttack.UP02)
                throw new Exception("Airborne player must prioritize Rising pursuit");
        }
        finally { field.SetValue(behavior, oldCooldown); }
    }
}
#endif
