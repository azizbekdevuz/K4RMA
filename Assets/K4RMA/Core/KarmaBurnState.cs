using System;

namespace KarmaPrototype
{
    // Pure status clock; the player supplies scaled game time so pause is respected.
    public sealed class KarmaBurnState
    {
        float endsAt, nextTick, interval, direction;
        bool applied;
        public float Remaining(float now) { return applied ? Math.Max(0, endsAt - now) : 0; }
        public void Apply(float now, float duration, float tickInterval, float awayFromSource)
        {
            bool alreadyBurning = Remaining(now) > 0;
            endsAt = Math.Max(alreadyBurning ? endsAt : now, now + Math.Max(0.1f, duration));
            interval = Math.Max(0.2f, tickInterval);
            direction = awayFromSource < 0 ? -1 : 1;
            if (!alreadyBurning) nextTick = now;
            applied = true;
        }
        public bool ConsumeTick(float now, out float awayFromSource)
        {
            awayFromSource = direction;
            if (Remaining(now) <= 0 || now < nextTick) return false;
            // Refreshes never add damage stacks or reset this deadline.
            nextTick = now + interval;
            return true;
        }
        public void Clear() { applied = false; endsAt = nextTick = 0; }
    }
}
