#if UNITY_EDITOR
using UnityEngine;
using KarmaPrototype;
public sealed class KarmaRisingSelectionChecks : KarmaSelectionFixture
{
    public override Essence Kind => Essence.RisingSlash;
    public override void Run(KarmaEnemy enemy, KarmaGame game)
    {
        var settings = game.Config.BossTuning.rising.Weights;
        float old_basicWeight = settings.basicWeight;
        float old_up01Weight = settings.up01Weight;
        float old_up02Weight = settings.up02Weight;
        float old_up03Weight = settings.up03Weight;
        float old_up04Weight = settings.up04Weight;
        float old_up05Weight = settings.up05Weight;
        try
        {
            enemy.transform.position = new Vector3(26, KarmaSpace.GuardianGroundY, 0);
            game.Player.ResetForRoom(new Vector2(20, 8f), true);
            Set(enemy, "previous", (EnemyAttack)(-1));
            settings.basicWeight = 0f;
            settings.up01Weight = 1f;
            settings.up02Weight = 1.5f; // Airborne pressure doubles this to an effective weight of 3.
            settings.up03Weight = 0f;
            settings.up04Weight = 0f;
            settings.up05Weight = 0f;
            Distribution(enemy, 6, EnemyAttack.UP01, EnemyAttack.UP02);
        }
        finally
        {
            settings.basicWeight = old_basicWeight;
            settings.up01Weight = old_up01Weight;
            settings.up02Weight = old_up02Weight;
            settings.up03Weight = old_up03Weight;
            settings.up04Weight = old_up04Weight;
            settings.up05Weight = old_up05Weight;
        }
    }
}
#endif
