using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        float stunUntil;
        bool confined;
        float arenaLeft, arenaRight;
        public bool Stunned { get { return Time.time < stunUntil; } }
        public enum CrowdControlKind { Shock, Airborne, Knockback, Knockdown }
        float shockUntil, airborneUntil, knockbackUntil, knockdownUntil, getUpUntil;
        bool airborneControl, leftGroundUnderControl;
        public bool Shocked => Time.time < shockUntil;
        public bool KnockedAirborne => airborneControl && Time.time < airborneUntil;
        public bool KnockedBack => Time.time < knockbackUntil;
        public bool KnockedDown => Time.time < knockdownUntil;
        public bool GettingUp => Time.time < getUpUntil;
        public bool ControlLocked => Stunned || Shocked || KnockedAirborne || KnockedBack || KnockedDown || GettingUp;

        // Standalone CC respects defenses. For attack contacts prefer TakeCrowdControlHit.
        // Durations use scaled seconds; launchVelocity uses Unity world units / second.
        public bool ApplyShock(float seconds) => ApplyCrowdControl(CrowdControlKind.Shock, seconds, Vector2.zero, false);
        public bool ApplyAirborne(Vector2 launchVelocity, float maxSeconds = 3f)
            => ApplyCrowdControl(CrowdControlKind.Airborne, maxSeconds, launchVelocity, false);

        // Opt-in CC contracts; existing boss contacts do not acquire these effects automatically.
        public bool ApplyKnockback(Vector2 velocity, float seconds = 0.3f)
            => ApplyCrowdControl(CrowdControlKind.Knockback, seconds, velocity, false);
        public bool ApplyKnockdown(float seconds = 0.6f)
            => ApplyCrowdControl(CrowdControlKind.Knockdown, seconds, Vector2.zero, false);

        // Return true only when damage landed and the requested CC was accepted.
        // Reuse the normal damage path, including counter, guard, tutorial and test immunity.
        public bool TakeCrowdControlHit(float damage, CrowdControlKind kind, float seconds,
            Vector2 launchVelocity = default)
            => TakeCrowdControlHit(damage, kind, seconds, launchVelocity, false, true);

        // Attack dispatch keeps its existing visual and parry/riposte policy.
        internal bool TakeCrowdControlHit(float damage, CrowdControlKind kind, float seconds,
            Vector2 launchVelocity, bool customHitVisual, bool counterDamage)
        {
            if (!ValidCrowdControl(kind, seconds, launchVelocity) || game == null || body == null
                || game.Paused || !game.AllowsPlayerActions || Health <= 0 || !Finite(damage) || damage <= 0) return false;
            float before = Health;
            TakeDamage(damage, customHitVisual, counterDamage);
            return Health < before && Health > 0 && ApplyCrowdControl(kind, seconds, launchVelocity, true);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool ValidCrowdControl(CrowdControlKind kind, float seconds, Vector2 velocity)
            => Finite(seconds) && seconds > 0 && (kind == CrowdControlKind.Shock
                || kind == CrowdControlKind.Airborne && Finite(velocity.x) && Finite(velocity.y) && velocity.y > 0
                || kind == CrowdControlKind.Knockback && Finite(velocity.x) && Finite(velocity.y)
                || kind == CrowdControlKind.Knockdown);

        bool ApplyCrowdControl(CrowdControlKind kind, float seconds, Vector2 velocity, bool confirmedHit)
        {
            if (!ValidCrowdControl(kind, seconds, velocity) || game == null || body == null
                || !game.AllowsPlayerActions || game.Paused || Health <= 0) return false;
            if (!confirmedHit && (Shielded || TestInvincible || game.Tutorial != null
                || Time.time < invulnerableUntil || techniques.CounterReady(Time.time))) return false;
            if (kind == CrowdControlKind.Knockdown && !Grounded()) return false;
            CancelAirSlash();
            move = 0; jumpBufferedUntil = airSlashBufferedUntil = 0; lastGrounded = -10;
            Crouching = false;
            body.gravityScale = game.Config.gravityScale;
            BeginControlPresentation();
            if (kind != CrowdControlKind.Knockdown) knockdownUntil = getUpUntil = 0;
            if (kind == CrowdControlKind.Shock)
            {
                shockUntil = Mathf.Max(shockUntil, Time.time + seconds);
                if (!KnockedAirborne) body.linearVelocity = new Vector2(0, body.linearVelocity.y);
            }
            else if (kind == CrowdControlKind.Knockback)
            {
                knockbackUntil = Mathf.Max(knockbackUntil, Time.time + seconds);
                body.linearVelocity = velocity;
            }
            else if (kind == CrowdControlKind.Knockdown)
            {
                knockdownUntil = Mathf.Max(knockdownUntil, Time.time + seconds);
                getUpUntil = knockdownUntil + 0.5f;
                body.linearVelocity = new Vector2(0, body.linearVelocity.y);
            }
            else
            {
                // A repeated launch refreshes the cap and replaces launch velocity.
                airborneControl = true; leftGroundUnderControl = false;
                airborneUntil = Time.time + seconds;
                body.linearVelocity = velocity;
            }
            if (!animator.UsesNativePlayer24) animator.Hurt();
            return true;
        }

        void UpdateCrowdControl(bool grounded)
        {
            if (!airborneControl) return;
            if (!grounded) leftGroundUnderControl = true;
            if (Time.time >= airborneUntil || leftGroundUnderControl && grounded && body.linearVelocity.y <= 0.1f)
            {
                airborneControl = leftGroundUnderControl = false;
                airborneUntil = 0;
            }
        }
        public int ParryCount { get; private set; }
        public void Stun(float seconds)
        {
            if (!game.IsCombat || Health <= 0 || Stunned) return;
            CancelAirSlash(); move = 0; jumpBufferedUntil = 0;
            BeginControlPresentation();
            knockdownUntil = getUpUntil = 0;
            stunUntil = Time.time + Mathf.Max(0, seconds);
            // A complete recovery interval before another hit is allowed.
            invulnerableUntil = Mathf.Max(invulnerableUntil, stunUntil + 0.25f);
            body.linearVelocity = Vector2.zero;

        }
        public void ConfineArena(float left, float right)
        {
            CancelAirSlash(); confined = true; arenaLeft = left; arenaRight = right;
            Vector2 p = new Vector2(Mathf.Clamp(Position.x, left + KarmaSpace.Units(1), right - KarmaSpace.Units(1)), Mathf.Clamp(Position.y, KarmaSpace.Units(1), KarmaSpace.Units(6)));
            body.position = p; transform.position = p; body.linearVelocity = Vector2.zero;
            ResetLandingAudio();
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + 0.6f);
        }
        public void ReleaseArena() { confined = false; }
        void LateUpdate()
        {
            if (!confined || game == null || game.Paused) return;
            float x = Mathf.Clamp(body.position.x, arenaLeft, arenaRight);
            if (Mathf.Abs(x - body.position.x) < 0.0001f) return;
            Vector2 p = new Vector2(x, body.position.y);
            body.position = p; transform.position = p;
            body.linearVelocity = new Vector2(0, body.linearVelocity.y);
            SuppressLandingAudioIfGrounded();
        }
        public void ResolveBarrier(float centerX, float width, float top = float.PositiveInfinity)
        {
            if (Position.y - KarmaSpace.PlayerHalfHeight >= top - 0.02f) return;
            float separation = width / 2 + KarmaSpace.PlayerHalfWidth + KarmaSpace.Units(0.05f);
            if (Mathf.Abs(Position.x - centerX) >= separation) return;
            CancelAirSlash();
            if (!float.IsPositiveInfinity(top))
            {
                // Adjacent rising steps must lift, rather than shove into an already solid neighbor.
                Vector2 above = new Vector2(Position.x, top + KarmaSpace.PlayerHalfHeight + KarmaSpace.Units(0.03f));
                body.position = above; transform.position = above; body.linearVelocity = Vector2.zero;
                ResetLandingAudio();
                return;
            }
            Vector2 p = new Vector2(centerX + (Position.x < centerX ? -separation : separation), Position.y);
            body.position = p; transform.position = p;
            body.linearVelocity = new Vector2(0, body.linearVelocity.y);
            SuppressLandingAudioIfGrounded();
        }
        public void ClearStatus()
        {
            CancelAirSlash(); stunUntil = shockUntil = airborneUntil = knockbackUntil = knockdownUntil = getUpUntil = 0;
            ResetControlPresentation();
            airborneControl = leftGroundUnderControl = false; ReleaseArena();
        }
    }
}
