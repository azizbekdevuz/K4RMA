using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void ExecuteGroundStrike()
        {
            KarmaVisuals.Flash(game.World, new Vector2(lockedX, Position.y), new Vector2(3.3f, 0.35f), Color.white, 0.2f);
            if (Mathf.Abs(game.Player.Position.x - lockedX) < 1.8f && Mathf.Abs(game.Player.Position.y - Position.y) < 1.5f)
                game.Player.TakeImpact(Damage, Position, 5);
            Recover(0.9f);
        }
    }
}
