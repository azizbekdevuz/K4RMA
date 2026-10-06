using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        void FixedUpdate()
        {
            if (game == null || game.Paused) return;
            if (!game.CanMove)
            {
                CancelAirSlash();
                body.linearVelocity = Vector2.zero;
                body.gravityScale = 0;
                return;
            }
            if (!airSlashRunning && Grounded() && body.linearVelocity.y <= 0.1f) { lastGrounded = Time.time; techniques.Land(); }
            if (KnockedBack)
            {
                CancelAirSlash();
                body.gravityScale = game.Config.gravityScale;
                body.linearVelocity = new Vector2(knockbackDirection * knockbackSpeed, body.linearVelocity.y);
            }
            else if (airSlashRunning)
            {
                DamageAirSlash();
                if (!game.IsCombat) { CancelAirSlash(); return; }
                var tuning = game.Config.Techniques;
                if (airSlashMotion.Active)
                {
                    // Rigidbody movement keeps interpolation and terrain collision active.
                    body.gravityScale = 0;
                    float travel = airSlashMotion.Advance(Time.fixedDeltaTime) * tuning.airSlashDistance;
                    body.linearVelocity = airSlashDirection * (travel / Time.fixedDeltaTime);
                }
                else
                {
                    airSlashRunning = false; airSlashHits.Clear();
                    body.gravityScale = game.Config.gravityScale;
                    airRecoveryLeft = Mathf.Max(0.02f, tuning.airSlashRecovery);
                    airLaunchX = airSlashDirection.x * game.Config.moveSpeed * tuning.airSlashHorizontalBoost;
                    body.linearVelocity = new Vector2(airLaunchX, game.Config.jumpSpeed * tuning.airSlashJumpScale);
                }
            }
            else if (Dashing)
            {
                body.gravityScale = 0;
                body.linearVelocity = new Vector2(dashFacing * game.Config.dashSpeed, 0);
            }
            else
            {
                body.gravityScale = game.Config.gravityScale;
                float horizontal = move * game.Config.moveSpeed;
                if (airRecoveryLeft > 0)
                {
                    float weight = Mathf.Clamp01(airRecoveryLeft / Mathf.Max(0.02f, game.Config.Techniques.airSlashRecovery));
                    horizontal = Mathf.Lerp(horizontal, airLaunchX, weight * weight);
                    airRecoveryLeft = Mathf.Max(0, airRecoveryLeft - Time.fixedDeltaTime);
                }
                body.linearVelocity = new Vector2(horizontal, body.linearVelocity.y);
                if (jumpBufferedUntil > Time.time && Time.time - lastGrounded < 0.1f)
                {
                    body.linearVelocity = new Vector2(body.linearVelocity.x, game.Config.jumpSpeed);
                    jumpBufferedUntil = 0;
                    lastGrounded = -10;
                }
            }
            if (Position.y < -8) game.PlayerDied();
        }
        bool Grounded()
        {
            int count = Physics2D.OverlapBox(Position + Vector2.down * 0.81f,
                new Vector2(0.58f, 0.12f), 0, new ContactFilter2D { useTriggers = false }, groundHits);
            for (int i = 0; i < count; i++)
                if (groundHits[i].GetComponent<KarmaSurface>() != null) return true;
            return false;
        }
        public void BeginDash(float seconds) { dashUntil = Time.time + seconds; dashFacing = Facing; }
        public void ResetForRoom(Vector2 position, bool restoreHealth)
        {
            transform.position = position;
            body.position = position;
            body.linearVelocity = Vector2.zero;
            move = 0; jumpBufferedUntil = 0; lastGrounded = -10; nextSword = 0;
            invulnerableUntil = dashUntil = wardUntil = 0;
            CancelAirSlash();
            techniques.Reset();
            ClearStatus();
            Facing = 1;
            Essences.ResetTemporaryState();
            if (restoreHealth) Health = game.Config.playerHealth;
        }
    }
}
