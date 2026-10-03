using UnityEngine;

namespace KarmaPrototype
{
    // Lingering fusion-attack fire. Removed as soon as combat ends.
    public sealed class KarmaHazard : MonoBehaviour
    {
        KarmaGame game;
        float expiresAt;
        SpriteRenderer sprite;
        public static void SpawnFire(KarmaGame game, float x)
        {
            var sprite = KarmaVisuals.Box(game.World, "Blazing charge / fire trail",
                new Vector2(x, 0.38f), new Vector2(1.4f, 0.76f), game.Config.flameColor, 5);
            var hazard = sprite.gameObject.AddComponent<KarmaHazard>();
            hazard.game = game;
            hazard.sprite = sprite;
            hazard.expiresAt = Time.time + game.Config.fireTrailLifetime;
        }
        void Update()
        {
            if (game == null) { Destroy(gameObject); return; }
            if (game.Paused) return;
            if (!game.IsCombat || Time.time >= expiresAt) { Destroy(gameObject); return; }
            var delta = game.Player.Position - (Vector2)transform.position;
            if (Mathf.Abs(delta.x) < 1 && Mathf.Abs(delta.y) < 1.05f)
                game.Player.TakeDamage(game.Config.fireTrailDamage);
            Color c = sprite.color;
            c.a = Mathf.Clamp01((expiresAt - Time.time) / 0.5f) * (0.8f + 0.2f * Mathf.Sin(Time.time * 18));
            sprite.color = c;
        }
    }
}
