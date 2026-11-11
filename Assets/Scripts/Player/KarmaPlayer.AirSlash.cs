using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        readonly KarmaAirSlashMotion airSlashMotion = new KarmaAirSlashMotion();
        readonly HashSet<KarmaEnemy> airSlashHits = new HashSet<KarmaEnemy>();
        internal bool ContinuousSwordAttack { get { return airSlashRunning; } }
        bool airSlashRunning;
        Vector2 airSlashDirection, airSlashPrevious;
        float airSlashBufferedUntil, airRecoveryLeft, airSlashHoverLeft;
        float airSlashExitX;
        Vector2 airSlashStartVelocity;
        float airSlashLaunchX, airSlashCruiseX, airSlashLaunchY;
        bool airSlashDownward, airSlashRebounded;
        void BeginAirSlash(Vector2 direction)
        {
            var tuning = game.Config.Techniques;
            airSlashDirection = direction;
            airSlashDownward = direction.y < -0.25f;
            airSlashRebounded = false;
            airSlashStartVelocity = body.linearVelocity;
            ConfigureAirSlashVelocity(direction, tuning);
            airSlashPrevious = Position;
            airSlashMotion.Begin(tuning.airSlashDuration);
            airSlashRunning = true;
            airSlashHits.Clear(); airSlashBufferedUntil = 0; airRecoveryLeft = 0;
            airSlashHoverLeft = airSlashDownward ? tuning.airSlashDownHover : 0;
            body.gravityScale = airSlashHoverLeft > 0 ? 0 : game.Config.gravityScale;
            body.linearVelocity = new Vector2(airSlashLaunchX, airSlashLaunchY);
            jumpBufferedUntil = 0; lastGrounded = -10;
            nextSword = Time.time + Mathf.Max(game.Config.swordCooldown, tuning.airSlashDuration);
            if (direction.x != 0) Facing = direction.x > 0 ? 1 : -1;
            FaceAction(); animator.Attack(direction);
            KarmaAudio.Play(KarmaSfxId.PlSwordSwing);
            string airPose = direction.y < -0.25f ? "AirHover" : direction.y > 0.75f ? "AirUp"
                : direction.y > 0.25f ? "AirDiagonal" : "AirHorizontal";
            animator.PlayPlayerAction(airPose, "AirSlash", tuning.airSlashDuration + 0.04f);
            Essences.NotifyUnique(Essence.RisingSlash);
            KarmaRemnantVFX.SwordSlash(transform, Vector2.zero, direction, KarmaSpace.Units(1.6f), KarmaSpace.Units(1.8f), tuning.airSlashDuration + 0.04f);
        }
        void DamageAirSlash()
        {
            Vector2 from = airSlashPrevious + airSlashDirection * KarmaSpace.Units(0.35f);
            Vector2 to = Position + airSlashDirection * KarmaSpace.Units(0.85f);
            airSlashPrevious = Position;
            foreach (var enemy in game.Enemies.ToArray())
            {
                if (enemy == null || !enemy.Alive || airSlashHits.Contains(enemy)) continue;
                if (!enemy.IntersectsDamagePath(from, to, KarmaSpace.Units(0.75f))) continue;
                airSlashHits.Add(enemy);
                if (SwordHit(enemy, airSlashDirection) && airSlashDownward && !airSlashRebounded)
                {
                    airSlashRebounded = true;
                    body.linearVelocity = new Vector2(body.linearVelocity.x,
                        Mathf.Max(body.linearVelocity.y, game.Config.Techniques.airSlashDownRebound));
                }
            }
        }
        void AdvanceAirSlash()
        {
            var tuning = game.Config.Techniques;
            if (airSlashMotion.Active)
            {
                airSlashMotion.Advance(Time.fixedDeltaTime);
                float progress = airSlashMotion.Progress;
                float eased = progress * progress * (3f - 2f * progress);
                body.linearVelocity = new Vector2(Mathf.Lerp(airSlashLaunchX, airSlashCruiseX, eased),
                    body.linearVelocity.y);
                if (airSlashDownward)
                {
                    float previousHover = airSlashHoverLeft;
                    airSlashHoverLeft = Mathf.Max(0, airSlashHoverLeft - Time.fixedDeltaTime);
                    if (previousHover > 0 && airSlashHoverLeft <= 0)
                        animator.PlayPlayerAction("AirDown", "AirSlash", Mathf.Max(0.02f, tuning.airSlashDuration * (1 - progress)) + 0.04f);
                    body.gravityScale = airSlashHoverLeft > 0 ? 0 : game.Config.gravityScale;
                }
            }
            else
            {
                airSlashRunning = false; airSlashHits.Clear();
                airRecoveryLeft = Mathf.Max(0.02f, tuning.airSlashRecovery);
                airSlashExitX = body.linearVelocity.x;
                body.gravityScale = game.Config.gravityScale;
            }
        }
        void ConfigureAirSlashVelocity(Vector2 direction, KarmaTechniqueTuning tuning)
        {
            float duration = Mathf.Max(0.02f, tuning.airSlashDuration);
            float gravity = Physics2D.gravity.y * game.Config.gravityScale;
            if (direction.y > 0.75f)
            {
                airSlashLaunchX = airSlashStartVelocity.x;
                airSlashCruiseX = airSlashStartVelocity.x;
                airSlashLaunchY = tuning.airSlashUpDistance / duration - gravity * duration * 0.5f;
                return;
            }
            if (direction.y > 0.25f)
            {
                float horizontal = tuning.airSlashDiagonalDistance * 0.7071f / duration;
                airSlashLaunchX = Mathf.Sign(direction.x) * (2f * horizontal - game.Config.moveSpeed);
                airSlashCruiseX = Mathf.Sign(direction.x) * game.Config.moveSpeed;
                airSlashLaunchY = horizontal - gravity * duration * 0.5f;
                return;
            }
            if (direction.y < -0.25f)
            {
                airSlashLaunchX = airSlashStartVelocity.x * 0.65f + direction.x * game.Config.moveSpeed * 0.15f;
                airSlashCruiseX = airSlashStartVelocity.x * 0.5f + direction.x * game.Config.moveSpeed * 0.1f;
                airSlashLaunchY = Mathf.Min(0, airSlashStartVelocity.y) * 0.2f;
                return;
            }
            float horizontalSpeed = tuning.airSlashHorizontalDistance / duration;
            airSlashLaunchX = Mathf.Sign(direction.x) * (2f * horizontalSpeed - game.Config.moveSpeed);
            airSlashCruiseX = Mathf.Sign(direction.x) * game.Config.moveSpeed;
            airSlashLaunchY = tuning.airSlashHorizontalRise / duration - gravity * duration * 0.5f;
        }
        void CancelAirSlash()
        {
            airSlashRunning = false; airSlashMotion.Reset(); airSlashHits.Clear();
            airSlashBufferedUntil = airRecoveryLeft = airSlashHoverLeft = 0;
        }
    }
}
