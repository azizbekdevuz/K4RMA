using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        void FixedUpdate()
        {
            if (game == null) return;
            if (!game.CanMove)
            {
                body.velocity = Vector2.zero;
                body.gravityScale = 0;
                return;
            }
            if (Grounded() && body.velocity.y <= 0.1f) lastGrounded = Time.time;
            if (KnockedBack)
            {
                body.gravityScale = game.Config.gravityScale;
                body.velocity = new Vector2(knockbackDirection * knockbackSpeed, body.velocity.y);
            }
            else if (Dashing)
            {
                body.gravityScale = 0;
                body.velocity = new Vector2(dashFacing * game.Config.dashSpeed, 0);
            }
            else
            {
                body.gravityScale = game.Config.gravityScale;
                body.velocity = new Vector2(move * game.Config.moveSpeed, body.velocity.y);
                if (jumpBufferedUntil > Time.time && Time.time - lastGrounded < 0.1f)
                {
                    body.velocity = new Vector2(body.velocity.x, game.Config.jumpSpeed);
                    jumpBufferedUntil = 0;
                    lastGrounded = -10;
                }
            }
            if (Position.y < -8) game.PlayerDied();
        }
        bool Grounded()
        {
            int count = Physics2D.OverlapBoxNonAlloc(Position + Vector2.down * 0.81f,
                new Vector2(0.58f, 0.12f), 0, groundHits);
            for (int i = 0; i < count; i++)
                if (groundHits[i].GetComponent<KarmaSurface>() != null) return true;
            return false;
        }
        public void BeginDash(float seconds) { dashUntil = Time.time + seconds; dashFacing = Facing; }
        public void ResetForRoom(Vector2 position, bool restoreHealth)
        {
            transform.position = position;
            body.position = position;
            body.velocity = Vector2.zero;
            move = 0; jumpBufferedUntil = 0; lastGrounded = -10; nextSword = 0;
            invulnerableUntil = dashUntil = wardUntil = 0;
            ClearStatus();
            Facing = 1;
            Essences.ResetTemporaryState();
            if (restoreHealth) Health = game.Config.playerHealth;
        }
    }
}
