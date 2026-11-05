using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        readonly HashSet<KarmaEnemy> airSlashHits = new HashSet<KarmaEnemy>();
        internal bool ContinuousSwordAttack { get { return airSlashRunning; } }
        bool airSlashRunning, airSlashBounced;
        Vector2 airSlashDirection, airSlashPrevious;
        float airSlashBufferedUntil, airSlashLeft, airHangLeft, doubleJumpDelay;
        void BeginAirSlash(Vector2 direction)
        {
            var tuning = game.Config.Techniques;
            airSlashDirection = direction;
            airSlashPrevious = Position;
            airSlashLeft = tuning.airSlashDuration;
            airSlashRunning = true; airSlashBounced = false;
            airSlashHits.Clear(); airSlashBufferedUntil = 0;
            nextSword = Time.time + Mathf.Max(game.Config.swordCooldown, tuning.airSlashDuration);
            if (direction.x != 0) Facing = direction.x > 0 ? 1 : -1;
            animator.Attack(direction);
            animator.PlayPose(direction.y < -0.5f ? "DownSlashWindup" : direction.y > 0.5f ? "Rising" : "Slash", tuning.airSlashDuration);
            if (Essences.HasRemnant(Essence.RisingSlash)) Essences.NotifyUnique(Essence.RisingSlash);
            KarmaRemnantVFX.SwordSlash(transform, Vector2.zero, direction, KarmaSpace.Units(1.6f), KarmaSpace.Units(1.8f), tuning.airSlashDuration);
        }
        void DamageAirSlash()
        {
            if (airSlashDirection.y < -0.5f && airSlashLeft > game.Config.Techniques.airSlashDuration * 0.65f)
            { airSlashPrevious = Position; return; }
            Vector2 from = airSlashPrevious + airSlashDirection * KarmaSpace.Units(0.35f);
            Vector2 to = Position + airSlashDirection * KarmaSpace.Units(0.85f);
            airSlashPrevious = Position;
            foreach (var enemy in game.Enemies.ToArray())
            {
                if (enemy == null || !enemy.Alive || airSlashHits.Contains(enemy)) continue;
                if (!enemy.IntersectsDamagePath(from, to, KarmaSpace.Units(0.75f))) continue;
                airSlashHits.Add(enemy);
                if (SwordHit(enemy, airSlashDirection) && airSlashDirection.y < -0.5f && !airSlashBounced)
                {
                    airSlashBounced = true;
                    airHangLeft = game.Config.Techniques.airHitHangSeconds;
                    body.linearVelocity = new Vector2(body.linearVelocity.x, 0);
                    techniques.RefreshAirJump();
                    animator.PlayPose("AirHang", airHangLeft);
                }
            }
        }
        void AdvanceAirSlash()
        {
            airSlashLeft = Mathf.Max(0, airSlashLeft - Time.fixedDeltaTime);
            if (!airSlashBounced && airSlashDirection.y < -0.5f)
                animator.PlayPose(airSlashLeft > game.Config.Techniques.airSlashDuration * 0.65f
                    ? "DownSlashWindup" : airSlashLeft > game.Config.Techniques.airSlashDuration * 0.2f
                    ? "DownSlash" : "DownSlashRecover", Time.fixedDeltaTime * 2);
            if (airSlashLeft > 0 && Airborne) return;
            airSlashRunning = false; airSlashHits.Clear();
        }
        void CancelAirSlash()
        {
            airSlashRunning = false; airSlashHits.Clear();
            airSlashBufferedUntil = airSlashLeft = airHangLeft = doubleJumpDelay = 0;
            if (body != null && game != null) body.gravityScale = game.Config.gravityScale;
        }
    }
}
