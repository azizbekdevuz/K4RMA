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
        public static void Spawn(KarmaGame game, Vector2 position, Vector2 velocity, float damage,
            Color color, float radius = 0.2f)
        {
            var sr = KarmaVisuals.Box(game.World, "Player projectile",
                position, Vector2.one * radius * 2, color, 7);
            var shot = sr.gameObject.AddComponent<KarmaProjectile>();
            shot.game = game; shot.velocity = velocity; shot.damage = damage;
            shot.radius = radius; shot.expiresAt = Time.time + 3;
            KarmaRemnantVFX.ProjectileLook(sr, velocity);
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
            foreach (var enemy in game.Enemies.ToArray())
            {
                if (enemy == null || !enemy.Alive || !Intersects(from, to, enemy.Position, radius + 0.68f)) continue;
                enemy.TakeDamage(damage, from);
                // Current player projectiles stop at their first target; piercing is a melee move.
                enabled = false; Destroy(gameObject); return;
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
