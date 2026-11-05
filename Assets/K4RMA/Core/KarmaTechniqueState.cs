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
    // Runtime supplies scaled time; no physics, input or visuals here.
    public sealed class KarmaTechniqueState
    {
        float counterUntil;
        bool counterArmed, airJumpUsed;
        public bool TryAirJump()
        {
            if (airJumpUsed) return false;
            airJumpUsed = true; return true;
        }
        public void RefreshAirJump() { airJumpUsed = false; }
        public void Land() { RefreshAirJump(); }
        public void ArmCounter(float now, float window) { counterArmed = true; counterUntil = now + Math.Max(0, window); }
        public bool CounterReady(float now) { return counterArmed && now < counterUntil; }
        public bool ConsumeCounter(float now)
        {
            bool ready = CounterReady(now); counterArmed = false; return ready;
        }
        public void Reset() { Land(); counterArmed = false; counterUntil = 0; }
    }
}
