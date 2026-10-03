using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        void Attack()
        {
            if (Time.time < nextSword || Dashing || KnockedBack) return;
            nextSword = Time.time + game.Config.swordCooldown;
            var center = Position + Vector2.right * Facing * 1.15f;
            KarmaVisuals.Flash(game.World, center, new Vector2(1.9f, 1.3f), new Color(0.94f, 0.92f, 0.75f), 0.13f);
            bool hit = false;
            foreach (var enemy in game.Enemies.ToArray())
            {
                if (enemy == null || !enemy.Alive) continue;
                var delta = enemy.Position - Position;
                if (Mathf.Abs(delta.y) <= 1.55f && delta.x * Facing >= -0.35f
                    && Mathf.Abs(delta.x) <= game.Config.swordReach)
                {
                    enemy.TakeDamage(game.Config.swordDamage);
                    hit = true;
                }
            }
            if (hit && game.Phase != RunPhase.Defeat) Essences.OnSwordConnected();
        }
        public void BeginWard(float seconds) { wardUntil = Time.time + seconds; }
        public void TakeDamage(float damage)
        {
            if (!game.IsCombat || Health <= 0 || Dashing || Time.time < invulnerableUntil) return;
            if (Shielded)
            {
                KarmaVisuals.Flash(game.World, Position, new Vector2(1.6f, 2.1f), game.Config.wardColor);
                return;
            }
            if (Essences.HasRemnant(Essence.Ward)) damage *= 1 - game.Config.wardDamageReduction;
            Health = Mathf.Max(0, Health - damage);
            game.DamageTaken += damage;
            invulnerableUntil = Time.time + game.Config.hitInvulnerability;
            KarmaVisuals.Flash(game.World, Position, Vector2.one, new Color(1, 0.2f, 0.3f));
            if (Health <= 0) game.PlayerDied();
        }
        public void TakeImpact(float damage, Vector2 source, float speed, float duration = 0.3f)
        {
            float before = Health;
            TakeDamage(damage);
            if (Health >= before || Health <= 0 || !game.IsCombat) return;
            knockbackDirection = Position.x < source.x ? -1 : Position.x > source.x ? 1 : -Facing;
            knockbackSpeed = speed;
            knockbackUntil = Time.time + duration;
            dashUntil = 0;
            body.velocity = new Vector2(knockbackDirection * speed, 4);
        }
        public void Heal(float amount) { Health = Mathf.Min(game.Config.playerHealth, Health + amount); }
    }
}
