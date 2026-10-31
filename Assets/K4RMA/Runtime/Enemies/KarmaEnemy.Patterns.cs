using System.Collections;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        IEnumerator ExecuteAttack()
        {
            switch (selected)
            {
                case EnemyAttack.SW01: return StaggeredWaves(false);
                case EnemyAttack.SW02: return AimedWave();
                case EnemyAttack.SW03: return StaggeredWaves(true);
                case EnemyAttack.SW04: return CounterGuard();
                case EnemyAttack.SW05: return AerialSwords();
                case EnemyAttack.UP01: return RiseSlam(false);
                case EnemyAttack.UP02: return AirPursuit(true);
                case EnemyAttack.UP03: return GroundBlade();
                case EnemyAttack.UP04: return RiseSlam(true);
                case EnemyAttack.UP05: return Crossings();
                case EnemyAttack.GD02: return HeavyStrike();
                case EnemyAttack.GD03: return SplitShield();
                case EnemyAttack.GD04: return RisingBarrier();
                case EnemyAttack.GD05: return SurvivalTrial();
                default: return GroundStrike();
            }
        }
    }
}
