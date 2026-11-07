#if UNITY_EDITOR
using UnityEngine;
using KarmaPrototype;
public sealed class KarmaSwordSelectionChecks : KarmaSelectionFixture
{
    public override Essence Kind => Essence.SwordWave;
    public override void Run(KarmaEnemy enemy, KarmaGame game)
    {
        var settings = game.Config.BossTuning.sword.Weights;
        float old_sw01Weight = settings.sw01Weight;
        float old_sw02Weight = settings.sw02Weight;
        float old_sw03Weight = settings.sw03Weight;
        float old_sw04Weight = settings.sw04Weight;
        try
        {
            enemy.transform.position = new Vector3(26, KarmaSpace.GuardianGroundY, 0);
            game.Player.ResetForRoom(new Vector2(14, 1.55f), true);
            Set(enemy, "previous", (EnemyAttack)(-1));
            var behavior = Get(enemy, "behavior");
            Set(behavior, "nextSwordBasic", float.PositiveInfinity);
            Set(behavior, "swordBasicDue", false);
            Set(behavior, "skillAfterRetreat", true);
            settings.sw01Weight = 1f;
            settings.sw02Weight = 3f;
            settings.sw03Weight = 0f;
            settings.sw04Weight = 0f;
            Distribution(enemy, 12, EnemyAttack.SW01, EnemyAttack.SW02);
        }
        finally
        {
            settings.sw01Weight = old_sw01Weight;
            settings.sw02Weight = old_sw02Weight;
            settings.sw03Weight = old_sw03Weight;
            settings.sw04Weight = old_sw04Weight;
        }
    }
}
#endif
