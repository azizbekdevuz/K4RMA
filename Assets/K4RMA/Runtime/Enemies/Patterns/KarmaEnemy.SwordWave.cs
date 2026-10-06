using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void FireSwordWave()
        {
            // Facing locks at telegraph; air waves descend toward the arena floor.
            var velocity = new Vector2(direction * 11, selected == EnemyAttack.AirWave ? -4f : 0);
            KarmaProjectile.Spawn(game, Position + Vector2.right * direction * 1.2f,
                velocity, Damage, false, KarmaArtwork.BossSwordColor, 0.28f);
        }
    }
}
