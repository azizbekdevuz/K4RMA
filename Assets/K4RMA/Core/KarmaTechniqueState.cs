using System;
namespace KarmaPrototype
{
    public sealed class KarmaHitFeedbackState
    {
        float slowUntil, shakeUntil;
        public void Trigger(float now, float slowSeconds, float shakeSeconds)
        {
            slowUntil = Math.Max(slowUntil, now + Math.Max(0, slowSeconds));
            shakeUntil = Math.Max(shakeUntil, now + Math.Max(0, shakeSeconds));
        }
        public float TimeScale(float now, bool combat, bool paused)
        {
            return paused ? 0 : combat && now < slowUntil ? 0.08f : 1;
        }
        public float Shake(float now, float seconds)
        {
            return seconds <= 0 ? 0 : Math.Max(0, Math.Min(1, (shakeUntil - now) / seconds));
        }
        public void Reset() { slowUntil = shakeUntil = 0; }
    }
    // Frame-rate independent travel curve: quick slash, then a soft deceleration.
    public sealed class KarmaAirSlashMotion
    {
        float elapsed, duration;
        public bool Active { get { return duration > 0 && elapsed < duration; } }
        public void Begin(float seconds) { elapsed = 0; duration = Math.Max(0.02f, seconds); }
        static float Distance(float t) { return (t - 0.25f * t * t) / 0.75f; }
        public float Advance(float seconds)
        {
            if (!Active || seconds <= 0) return 0;
            float before = elapsed / duration;
            elapsed = Math.Min(duration, elapsed + seconds);
            return Distance(elapsed / duration) - Distance(before);
        }
        public void Reset() { elapsed = duration = 0; }
    }
    // Runtime supplies scaled time; no physics, input or visuals here.
    public sealed class KarmaTechniqueState
    {
        int lastDirection;
        float lastTap = -100, counterUntil;
        bool counterArmed, airSlashUsed, hovering;
        float hoverUntil, hoverReleaseAt;
        public bool DirectionTap(int direction, float now, float window)
        {
            if (direction < 1 || direction > 4) { ClearDirectionTap(); return false; }
            bool ready = lastDirection == direction && now >= lastTap && now - lastTap <= Math.Max(0, window);
            if (ready) ClearDirectionTap();
            else { lastDirection = direction; lastTap = now; }
            return ready;
        }
        public void ClearDirectionTap() { lastDirection = 0; lastTap = -100; }
        public bool HoverReady { get { return !airSlashUsed && !hovering; } }
        public bool Hovering { get { return hovering; } }
        public bool TryAirSlash()
        {
            if (airSlashUsed) return false;
            airSlashUsed = true; return true;
        }
        // One hang per airborne period. Holding continues it; release after the minimum drops.
        public bool TryBeginHover(float now, float duration, float minimumHold)
        {
            if (airSlashUsed || hovering) return false;
            airSlashUsed = true;
            hovering = true;
            hoverUntil = now + Math.Max(0.2f, duration);
            hoverReleaseAt = now + Math.Max(0.05f, minimumHold);
            return true;
        }
        public bool ShouldEndHover(float now, bool held)
        {
            return hovering && (now >= hoverUntil || (!held && now >= hoverReleaseAt));
        }
        public void EndHover() { hovering = false; }
        public void Land() { airSlashUsed = false; EndHover(); }
        public void ArmCounter(float now, float window) { counterArmed = true; counterUntil = now + Math.Max(0, window); }
        public bool CounterReady(float now) { return counterArmed && now < counterUntil; }
        public bool ConsumeCounter(float now)
        {
            bool ready = CounterReady(now); counterArmed = false; return ready;
        }
        public void Reset() { ClearDirectionTap(); Land(); counterArmed = false; counterUntil = 0; }
    }
}
