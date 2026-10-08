namespace K4RMA
{
    /// <summary>
    /// Hold-to-guard window followed by recovery. No scene or Unity types.
    /// </summary>
    public sealed class GuardTiming
    {
        public float ActiveDuration = 0.8f;
        public float CooldownDuration = 0.55f;

        public bool IsGuarding { get; private set; }
        public float ActiveRemaining { get; private set; }
        public float CooldownRemaining { get; private set; }

        public void Reset()
        {
            IsGuarding = false;
            ActiveRemaining = 0f;
            CooldownRemaining = 0f;
        }

        public void Tick(float deltaTime, bool held)
        {
            if (deltaTime < 0f)
                deltaTime = 0f;

            if (CooldownRemaining > 0f)
            {
                CooldownRemaining -= deltaTime;
                if (CooldownRemaining < 0f)
                    CooldownRemaining = 0f;
                IsGuarding = false;
                ActiveRemaining = 0f;
                return;
            }

            if (IsGuarding)
            {
                ActiveRemaining -= deltaTime;
                if (!held || ActiveRemaining <= 0f)
                    EndGuard();
                return;
            }

            if (!held)
                return;

            IsGuarding = true;
            ActiveRemaining = ActiveDuration;
            if (ActiveRemaining <= 0f)
                EndGuard();
        }

        void EndGuard()
        {
            IsGuarding = false;
            ActiveRemaining = 0f;
            CooldownRemaining = CooldownDuration;
        }
    }
}
