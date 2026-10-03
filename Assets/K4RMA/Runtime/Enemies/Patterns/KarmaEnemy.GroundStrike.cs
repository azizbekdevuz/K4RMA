using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void ExecuteGroundStrike()
        {
            KarmaVisuals.Flash(game.World, new Vector2(lockedX, 0.65f), new Vector2(3.4f, 1.3f), new Color(1, 0.6f, 0.2f), 0.3f);
            if (Mathf.Abs(game.Player.Position.x - lockedX) < 1.9f && game.Player.Position.y < 1.95f)
                game.Player.TakeDamage(Damage * 1.4f);
            if (CombatPhase >= 2)
                KarmaGroundStrike.Spawn(game, lockedX + direction * 3.8f, 0.5f, Damage * 1.2f);
            if (CombatPhase == 3)
                KarmaGroundStrike.Spawn(game, lockedX - direction * 3.8f, 0.9f, Damage * 1.2f);
            Recover(1.2f);
        }
    }
}
