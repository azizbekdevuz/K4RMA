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
                if ((game.Phase == RunPhase.GuardianFallen && !ReadyForPersonalization)
                    || (game.Phase == RunPhase.PlayerFallen && !ReadyForDefeat))
                {
                    // Stop attacks and horizontal travel, but let an airborne finisher land.
                    body.gravityScale = game.Config.gravityScale;
                    body.linearVelocity = new Vector2(0, Mathf.Min(0, body.linearVelocity.y));
                    return;
                }
                if (game.Phase == RunPhase.GuardianFallen && separatingForCeremony)
                { UpdateCeremonySpacing(); return; }
                body.linearVelocity = Vector2.zero;
                body.gravityScale = 0;
                return;
            }
            if (!airSlashRunning && Grounded() && body.linearVelocity.y <= 0.1f) { lastGrounded = Time.time; techniques.Land(); }
            if (Stunned)
            {
                body.gravityScale = game.Config.gravityScale;
                body.linearVelocity = new Vector2(0, body.linearVelocity.y);
                return;
            }
            float horizontal = move * game.Config.moveSpeed;
            body.gravityScale = game.Config.gravityScale;
            body.linearVelocity = new Vector2(horizontal, body.linearVelocity.y);
            if (airSlashRunning)
            {
                DamageAirSlash();
                if (!game.IsCombat) { CancelAirSlash(); return; }
                AdvanceAirSlash();
            }
            if (airHangLeft > 0)
            {
                airHangLeft = Mathf.Max(0, airHangLeft - Time.fixedDeltaTime);
                body.gravityScale = 0;
                body.linearVelocity = new Vector2(horizontal, 0);
                if (airHangLeft <= 0)
                {
                    body.gravityScale = game.Config.gravityScale;
                    body.linearVelocity = new Vector2(horizontal, game.Config.jumpSpeed * game.Config.Techniques.pogoJumpScale);
                }
            }
            if (doubleJumpDelay > 0)
            {
                doubleJumpDelay = Mathf.Max(0, doubleJumpDelay - Time.fixedDeltaTime);
                body.gravityScale = 0;
                body.linearVelocity = new Vector2(horizontal, 0);
                if (doubleJumpDelay <= 0) LaunchDoubleJump();
            }
            ConsumeJumpBuffer();
            if (jumpCutAvailable && !KarmaInput.Held(KarmaAction.Jump) && body.linearVelocity.y > 0)
            {
                body.linearVelocity = new Vector2(horizontal, body.linearVelocity.y * game.Config.Techniques.jumpReleaseMultiplier);
                jumpCutAvailable = false;
            }
            if (Position.y < KarmaSpace.Units(-8)) game.PlayerDied();
        }
        bool jumpCutAvailable;
        void ConsumeJumpBuffer()
        {
            if (jumpBufferedUntil <= Time.time) return;
            if (Time.time - lastGrounded < 0.1f)
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, game.Config.jumpSpeed);
                jumpBufferedUntil = 0; lastGrounded = -10; jumpCutAvailable = true;
            }
            else if (Airborne && doubleJumpDelay <= 0 && Essences.HasRemnant(Essence.RisingSlash) && techniques.TryAirJump())
            {
                jumpBufferedUntil = 0; airHangLeft = 0;
                doubleJumpDelay = game.Config.Techniques.doubleJumpAnticipation;
                animator.PlayPose("DoubleJumpStart", Mathf.Max(Time.fixedDeltaTime, doubleJumpDelay));
                Essences.NotifyUnique(Essence.RisingSlash);
                if (doubleJumpDelay <= 0) LaunchDoubleJump();
            }
        }
        void LaunchDoubleJump()
        {
            body.gravityScale = game.Config.gravityScale;
            body.linearVelocity = new Vector2(move * game.Config.moveSpeed, game.Config.jumpSpeed * game.Config.Techniques.doubleJumpScale);
            jumpCutAvailable = true;
            animator.PlayPose("DoubleJumpRise", 0.16f);
        }
        public bool ReadyForPersonalization => Grounded() && Mathf.Abs(body.linearVelocity.y) < 0.1f;
        public bool ReadyForDefeat => ReadyForPersonalization || Position.y < KarmaSpace.Units(-8);
        public void BeginDefeat()
        {
            ClearStatus();
            move = 0; jumpBufferedUntil = 0; airSlashBufferedUntil = 0;
            guardUntil = invulnerableUntil = 0;
            Crouching = false;
            body.linearVelocity = new Vector2(0, Mathf.Min(0, body.linearVelocity.y));
            art.gameObject.SetActive(true);
            animator.Die();
        }
        bool Grounded()
        {
            int count = Physics2D.OverlapBox(Position + Vector2.down * KarmaSpace.Units(0.81f),
                KarmaSpace.Units(new Vector2(0.58f, 0.12f)), 0, new ContactFilter2D { useTriggers = false }, groundHits);
            for (int i = 0; i < count; i++)
                if (groundHits[i].GetComponent<KarmaSurface>() != null) return true;
            return false;
        }
        public void ResetForRoom(Vector2 position, bool restoreHealth)
        {
            transform.position = position;
            body.position = position;
            body.linearVelocity = Vector2.zero;
            move = 0; jumpBufferedUntil = 0; lastGrounded = -10; nextSword = 0;
            Crouching = false; jumpCutAvailable = false;
            separatingForCeremony = false; ceremonyMove = 0;
            invulnerableUntil = guardUntil = 0;
            CancelAirSlash();
            techniques.Reset();
            ClearStatus();
            Facing = 1; ParryCount = 0;
            Essences.ResetTemporaryState();
            if (restoreHealth) Health = game.Config.playerHealth;
        }
    }
}
