using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Swept segment collision prevents fast projectiles skipping a target.
    // Projectiles intentionally pass through the small arena platforms.
    public sealed class KarmaProjectile : MonoBehaviour
    {
        KarmaGame game;
        Vector2 velocity;
        float damage, radius, expiresAt;
        bool friendly, burns;
        Essence? remnant;
        float nextTrail;
        readonly HashSet<KarmaEnemy> hitEnemies = new HashSet<KarmaEnemy>();
        public static void Spawn(KarmaGame game, Vector2 position, Vector2 velocity, float damage,
            bool friendly, Color color, float radius = 0.2f, Essence? remnant = null, bool burns = false)
        {
            var sr = KarmaVisuals.Box(game.World, friendly ? "Player projectile" : "Enemy projectile",
                position, Vector2.one * radius * 2, color, 7);
            var shot = sr.gameObject.AddComponent<KarmaProjectile>();
            shot.game = game; shot.velocity = velocity; shot.damage = damage;
            shot.friendly = friendly; shot.radius = radius; shot.expiresAt = Time.time + 3;
            shot.remnant = remnant; shot.burns = burns || remnant == Essence.Flame;
            KarmaRemnantVFX.ProjectileLook(sr, Essence.Dash, velocity);
            game.Projectiles.Add(shot);
        }
        void Update()
        {
            if (game == null) { Destroy(gameObject); return; }
            if (game.Paused) return;
            if (!game.IsCombat || Time.time > expiresAt) { Destroy(gameObject); return; }
            Vector2 from = transform.position;
            Vector2 to = from + velocity * Time.deltaTime;
            transform.position = to;
            if (to.x < 0 || to.x > 40 || to.y < 0 || to.y > 12) { Destroy(gameObject); return; }
            if (remnant.HasValue && Time.time >= nextTrail)
            {
                KarmaRemnantVFX.Trail(game, from, remnant.Value, velocity);
                nextTrail = Time.time + 0.06f;
            }
            if (friendly)
            {
                foreach (var enemy in game.Enemies.ToArray())
                {
                    if (enemy != null && enemy.Alive && !hitEnemies.Contains(enemy)
                        && Intersects(from, to, enemy.Position, radius + 0.68f))
                    {
                        hitEnemies.Add(enemy);
                        if (remnant.HasValue) KarmaRemnantVFX.Impact(game, enemy.Position, remnant.Value);
                        enemy.TakeDamage(damage);
                        if (burns && enemy != null && enemy.Alive) enemy.ApplyBurn();
                        // Internalized attacks pierce; each target is damaged once per projectile.
                        if (!remnant.HasValue) { Destroy(gameObject); return; }
                    }
                }
            }
            else if (game.Player != null && Intersects(from, to, game.Player.Position, radius + 0.5f))
            {
                game.Player.TakeDamage(damage);
                Destroy(gameObject);
            }
        }
        public static void ClearHostile(KarmaGame game)
        {
            foreach (var shot in game.Projectiles.ToArray())
            {
                if (shot == null || shot.friendly) continue;
                shot.enabled = false;
                Destroy(shot.gameObject);
            }
        }
        public static bool Intersects(Vector2 from, Vector2 to, Vector2 center, float radius)
        {
            var segment = to - from;
            float t = segment.sqrMagnitude < 0.000001f ? 0 :
                Mathf.Clamp01(Vector2.Dot(center - from, segment) / segment.sqrMagnitude);
            return (center - (from + segment * t)).sqrMagnitude <= radius * radius;
        }
        void OnDestroy() { if (game != null) game.Projectiles.Remove(this); }
    }
}
