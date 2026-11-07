using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void TryGuardCircleHit(Vector2 from, Vector2 to, float radius, float amount, KarmaAttackContact contact)
        {
            if (!Active || game.Paused || contact.Consumed) return;
            var player = game.Player;
            var collider = player.GetComponent<Collider2D>();
            var body = collider != null && collider.enabled ? collider.bounds : new Bounds(player.Position, KarmaSpace.PlayerSize);
            var p = body.center;
            if (KarmaGuardGeometry.SweptCircle(from.x, from.y, to.x, to.y, radius,
                p.x, p.y, body.extents.x, body.extents.y)) ResolveAttackHit(p, amount, contact, 0);
        }
        float shieldUntil, nextShield, shieldWarningUntil;
        bool frontalGuard;
        internal static float TrialRadius { get { return GD05Pattern.TrialRadius; } }
        internal static float TrialCenter { get { return GD05Pattern.TrialCenter; } }
        public bool TrialActive { get { return behavior is GuardBossBehavior guard && guard.Dome.Active; } }
        public bool HeavyParryWindowOpen { get { return behavior is GuardBossBehavior guard && guard.Heavy.ParryWindowOpen; } }
    }
}
