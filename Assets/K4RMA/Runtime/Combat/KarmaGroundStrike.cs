using UnityEngine;

namespace KarmaPrototype
{
    // An individually telegraphed follow-up strike, canceled at the end of combat.
    public sealed class KarmaGroundStrike : MonoBehaviour
    {
        KarmaGame game;
        float delay, damage;
        public static void Spawn(KarmaGame game, float x, float delay, float damage)
        {
            var sr = KarmaVisuals.Box(game.World, "연속 강타 예고", new Vector2(Mathf.Clamp(x, 1, 39), 0.12f),
                new Vector2(3.4f, 0.2f), new Color(1, 0.7f, 0.2f), 6);
            var strike = sr.gameObject.AddComponent<KarmaGroundStrike>();
            strike.game = game; strike.delay = delay; strike.damage = damage;
        }
        void Update()
        {
            if (game == null) { Destroy(gameObject); return; }
            if (game.Paused) return;
            if (!game.IsCombat) { Destroy(gameObject); return; }
            delay -= Time.deltaTime;
            if (delay > 0) return;
            float x = transform.position.x;
            KarmaVisuals.Flash(game.World, new Vector2(x, 0.65f), new Vector2(3.4f, 1.3f), new Color(1, 0.5f, 0.1f), 0.3f);
            if (Mathf.Abs(game.Player.Position.x - x) < 1.9f && game.Player.Position.y < 1.95f)
                game.Player.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
