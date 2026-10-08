namespace K4RMA
{
    /// <summary>
    /// Short press window. One open window accepts one hit, then closes.
    /// A miss starts the whiff cooldown. This is not a held guard.
    /// </summary>
    public sealed class CounterTiming
    {
        public float WindowDuration = 0.24f;
        public float WhiffCooldown = 0.48f;
        public float SuccessCooldown = 0.62f;

        public bool WindowOpen { get; private set; }
        public bool LastCloseWasSuccess { get; private set; }
        public float WindowRemaining { get; private set; }
        public float CooldownRemaining { get; private set; }

        public void Reset()
        {
            WindowOpen = false;
            LastCloseWasSuccess = false;
            WindowRemaining = 0f;
            CooldownRemaining = 0f;
        }

        public bool TryOpen()
        {
            if (WindowOpen || CooldownRemaining > 0f)
                return false;

            WindowRemaining = WindowDuration > 0f ? WindowDuration : 0f;
            LastCloseWasSuccess = false;
            if (WindowRemaining <= 0f)
            {
                CloseWhiff();
                return false;
            }

            WindowOpen = true;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                deltaTime = 0f;

            if (CooldownRemaining > 0f)
            {
                CooldownRemaining -= deltaTime;
                if (CooldownRemaining < 0f)
                    CooldownRemaining = 0f;
            }

            if (!WindowOpen)
                return;

            WindowRemaining -= deltaTime;
            if (WindowRemaining <= 0f)
                CloseWhiff();
        }

        public bool TryReceiveHit(out bool shouldRetaliate)
        {
            shouldRetaliate = false;
            if (!WindowOpen)
                return false;

            WindowOpen = false;
            WindowRemaining = 0f;
            LastCloseWasSuccess = true;
            CooldownRemaining = SuccessCooldown > 0f ? SuccessCooldown : 0f;
            shouldRetaliate = true;
            return true;
        }

        void CloseWhiff()
        {
            WindowOpen = false;
            WindowRemaining = 0f;
            LastCloseWasSuccess = false;
            CooldownRemaining = WhiffCooldown > 0f ? WhiffCooldown : 0f;
        }
    }
}
