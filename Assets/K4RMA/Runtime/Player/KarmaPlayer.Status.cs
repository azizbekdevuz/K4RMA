using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        public void ApplyBurn(Vector2 source)
        {
            if (!game.IsCombat || Health <= 0) return;
            var tuning = game.Config.BossTuning;
            float away = Position.x < source.x ? -1 : Position.x > source.x ? 1 : -Facing;
            burn.Apply(Time.time, tuning.burnDuration, tuning.burnTickInterval, away);
            UpdateBurn();
        }
        void UpdateBurn()
        {
            float away;
            if (!burn.ConsumeTick(Time.time, out away)) return;
            var tuning = game.Config.BossTuning;
            // A status penalty, separate from contact-hit invulnerability and active shields.
            float damage = tuning.burnTickDamage;
            if (Essences.HasRemnant(Essence.Ward)) damage *= 1 - game.Config.wardDamageReduction;
            float actual = Mathf.Min(Health, Mathf.Max(0, damage));
            Health -= actual; game.DamageTaken += actual;
            dashUntil = 0;
            knockbackSpeed = tuning.burnKnockbackSpeed;
            knockbackDirection = away;
            knockbackUntil = Time.time + Mathf.Min(tuning.burnKnockbackDuration, tuning.burnTickInterval * 0.7f);
            body.velocity = new Vector2(away * tuning.burnKnockbackSpeed, Mathf.Max(1.5f, body.velocity.y));
            KarmaRemnantVFX.Impact(game, Position, Essence.Flame);
            if (Health <= 0) game.PlayerDied();
        }
        public void ClearStatus()
        {
            burn.Clear(); knockbackUntil = 0;
        }
    }
}
