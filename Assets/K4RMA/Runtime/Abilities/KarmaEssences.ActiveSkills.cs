using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEssences
    {
        public void TryUse(Essence e)
        {
            if (!game.IsCombat || player.KnockedBack || player.Stunned || !HasActive(e) || Cooldown(e) > 0) return;
            var c = game.Config;
            switch (e)
            {
                case Essence.Flame:
                    readyAt[0] = Time.time + c.flameCooldown;
                    player.BeginSwordWave();
                    KarmaProjectile.Spawn(game, player.Position + Vector2.right * player.Facing * KarmaSpace.Units(0.7f),
                        Vector2.right * player.Facing * c.flameSpeed, c.flameDamage, c.flameColor, KarmaSpace.Units(0.25f));
                    break;
                case Essence.Dash:
                    readyAt[1] = Time.time + c.dashCooldown;
                    player.BeginRisingSlash(); break;
                case Essence.Ward:
                    readyAt[2] = Time.time + c.wardCooldown;
                    player.BeginWard(c.wardDuration); break;
            }
        }
        public void TryCounter()
        {
            if (!game.IsCombat || player.KnockedBack || player.Stunned || !HasRemnant(Essence.Ward) || Cooldown(Essence.Ward) > 0) return;
            readyAt[2] = Time.time + game.Config.Techniques.counterCooldown;
            player.BeginCounter();
        }
    }
}
