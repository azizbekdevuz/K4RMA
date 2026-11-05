#if UNITY_EDITOR
using UnityEngine;
using KarmaPrototype;
public sealed class KarmaGuardSelectionChecks : KarmaSelectionFixture
{
    public override Essence Kind => Essence.Guard;
    public override void Run(KarmaEnemy enemy, KarmaGame game)
    {
        var settings = game.Config.BossTuning.guard.Weights;
        float old_basicWeight = settings.basicWeight;
        float old_gd02Weight = settings.gd02Weight;
        float old_gd03Weight = settings.gd03Weight;
        float old_gd04Weight = settings.gd04Weight;
        try
        {
            enemy.transform.position = new Vector3(26, KarmaSpace.GuardianGroundY, 0);
            game.Player.ResetForRoom(new Vector2(20, 1.55f), true);
            Set(enemy, "previous", (EnemyAttack)(-1));
            Set(enemy, "shieldUntil", float.PositiveInfinity);
            settings.basicWeight = 0f;
            settings.gd02Weight = 0f;
            settings.gd03Weight = 1f;
            settings.gd04Weight = 3f;
            Distribution(enemy, 6, EnemyAttack.GD03, EnemyAttack.GD04);
        }
        finally
        {
            settings.basicWeight = old_basicWeight;
            settings.gd02Weight = old_gd02Weight;
            settings.gd03Weight = old_gd03Weight;
            settings.gd04Weight = old_gd04Weight;
        }
    }
}
#endif
