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
        int controlRevision;

        public static void Play(KarmaGame game, KarmaPlayer player)
        {
            if (game == null || player == null || !game.IsCombat || player.Health <= 0) return;
            var animator = player.GetComponentInChildren<KarmaCharacterAnimator>(true);
            var body = player.GetComponent<Rigidbody2D>();
            var picture = animator != null ? animator.GetComponentInChildren<SpriteRenderer>(true) : null;
            if (body == null || picture == null)
                throw new System.InvalidOperationException("GD04 feedback requires the player's body and artwork.");
            var fx = player.GetComponent<KarmaGuardAirborneHit>();
            var frames = fx != null && fx.frames != null ? fx.frames : KarmaArtwork.Sprites("GuardPlayerAirborneHit");
            if (frames.Length != 4 || System.Array.Exists(frames, frame => frame == null))
                throw new System.InvalidOperationException("GD04 feedback requires four airborne frames.");
            if (fx == null) fx = player.gameObject.AddComponent<KarmaGuardAirborneHit>();
            else fx.enabled = false;
            fx.game = game; fx.player = player;
            fx.body = body; fx.picture = picture; fx.frames = frames;
            fx.controlRevision = player.ControlRevision;
            fx.age = 0; fx.leftGround = false; fx.previous = fx.picture.sprite;
            fx.enabled = true;
        }

        void LateUpdate()
        {
            if (game == null || player == null || body == null || picture == null || frames == null || frames.Length != 4)
            { enabled = false; return; }
            bool airborne = player.Airborne;
            if (airborne) leftGround = true;
            if (!game.IsCombat || player.Health <= 0 || player.ControlRevision != controlRevision
                || age > 3 || leftGround && !airborne)
            { enabled = false; return; }
            if (game.Paused) return;
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
