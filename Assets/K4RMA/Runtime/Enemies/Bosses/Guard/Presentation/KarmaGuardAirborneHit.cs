using UnityEngine;

namespace KarmaPrototype
{
    // GD04-only presentation. Does not change player state, collision or motion.
    [DefaultExecutionOrder(100)]
    public sealed class KarmaGuardAirborneHit : MonoBehaviour
    {
        KarmaGame game;
        KarmaPlayer player;
        Rigidbody2D body;
        SpriteRenderer picture;
        Sprite[] frames;
        Sprite previous, displayed;
        float age;
        bool leftGround;

        public static void Play(KarmaGame game, KarmaPlayer player)
        {
            var fx = player.GetComponent<KarmaGuardAirborneHit>();
            if (fx == null) fx = player.gameObject.AddComponent<KarmaGuardAirborneHit>();
            fx.game = game; fx.player = player;
            fx.body = player.GetComponent<Rigidbody2D>();
            fx.picture = player.GetComponentInChildren<KarmaCharacterAnimator>(true).GetComponentInChildren<SpriteRenderer>(true);
            if (fx.frames == null) fx.frames = KarmaArtwork.Sprites("GuardPlayerAirborneHit");
            fx.age = 0; fx.leftGround = false; fx.previous = fx.picture.sprite;
            fx.enabled = true;
        }

        void LateUpdate()
        {
            if (game == null || player == null || picture == null || frames == null || frames.Length != 4)
            { enabled = false; return; }
            if (game.Paused) return;
            bool airborne = player.Airborne;
            if (airborne) leftGround = true;
            if (!game.IsCombat || player.Health <= 0 || age > 3 || leftGround && !airborne)
            { enabled = false; return; }
            age += Time.deltaTime;
            // Pose changes are quantized to 24 FPS; physical ascent/descent is not retimed.
            int tick = Mathf.FloorToInt(age * 24);
            int index = tick < 2 ? 0 : body.linearVelocity.y > 3 ? 1 : body.linearVelocity.y > -3 ? 2 : 3;
            displayed = frames[index]; picture.sprite = displayed;
        }

        void OnDisable()
        {
            if (picture != null && picture.sprite == displayed && previous != null) picture.sprite = previous;
        }
    }
}
