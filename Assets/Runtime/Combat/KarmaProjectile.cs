using UnityEngine;

namespace KarmaPrototype
{
    // Swept segment collision prevents fast projectiles skipping a target.
    // Projectiles intentionally pass through the small arena platforms.
    public sealed class KarmaProjectile : MonoBehaviour
    {
        static int nextTrainingId;
        int trainingId;
        bool originalWave;
        KarmaGame game;
        Vector2 velocity;
        float damage, radius, remaining;
        public static void Spawn(KarmaGame game, Vector2 position, Vector2 velocity, float damage,
            Color color, float radius = 0.2f * KarmaSpace.Scale, bool originalWave = false)
        {
            var sr = KarmaVisuals.Box(game.World, "Player projectile",
                position, Vector2.one * radius * 2, color, 7);
            var shot = sr.gameObject.AddComponent<KarmaProjectile>();
            shot.trainingId = ++nextTrainingId;
            shot.originalWave = originalWave;
            shot.game = game; shot.velocity = velocity; shot.damage = damage;
            shot.radius = radius; shot.remaining = 3;
            KarmaRemnantVFX.ProjectileLook(sr, velocity);
            KarmaCombatVFX.AttachAttack(sr, velocity, new Color(0.35f, 0.75f, 1));
            game.Projectiles.Add(shot);
            KarmaAudio.Play(KarmaSfxId.SwWaveLaunch, lifetimeOwner: shot);
        }
        void FixedUpdate()
        {
            if (game == null) { enabled = false; Destroy(gameObject); return; }
            if (game.GameplayFrozen) return;
            if (!game.AllowsPlayerActions) { enabled = false; Destroy(gameObject); return; }
            Vector2 from = transform.position;
            float travelTime = Mathf.Min(remaining, Time.fixedDeltaTime);
            remaining -= travelTime;
            Vector2 to = from + velocity * travelTime;
            transform.position = to;
            if (game.Tutorial != null && game.Tutorial.HitProjectile(trainingId, from, to, radius))
            { enabled = false; Destroy(gameObject); return; }
            // Damage can remove the hit enemy; this path returns immediately afterward.
            for (int i = 0; i < game.Enemies.Count; i++)
            {
                var enemy = game.Enemies[i];
                if (enemy == null || !enemy.Alive || !enemy.IntersectsDamagePath(from, to, radius)) continue;
                float before = enemy.Health;
                enemy.TakeDamage(damage, from, out bool reduced, mastery: originalWave ? Essence.SwordWave : (Essence?)null);
                if (enemy.Health < before && !reduced) KarmaAudio.Play(KarmaSfxId.SwWaveImpact);
                // Current player projectiles stop at their first target; piercing is a melee move.
                enabled = false; Destroy(gameObject); return;
            }
            if (remaining <= 0 || to.x < KarmaStage.ArenaLeft || to.x > (game.Tutorial != null ? KarmaTutorialStage.Right : KarmaStage.ArenaRight) || to.y < 0 || to.y > KarmaSpace.Units(12)) { enabled = false; Destroy(gameObject); return; }
        }
        void OnDestroy() { if (game != null) game.Projectiles.Remove(this); }
    }
}
