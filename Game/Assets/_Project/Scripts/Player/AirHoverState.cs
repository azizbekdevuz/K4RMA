namespace K4RMA
{
    /// <summary>
    /// One bounded aerial suspend per airborne period, plus a cooldown. Not a launch.
    /// </summary>
    public sealed class AirHoverState
    {
        public float Duration = 0.28f;
        public float CooldownDuration = 0.5f;

        public bool Hovering { get; private set; }
        public bool Armed { get; private set; } = true;
        public float HoverRemaining { get; private set; }
        public float CooldownRemaining { get; private set; }

        public void Reset()
        {
            Hovering = false;
            Armed = true;
            HoverRemaining = 0f;
            CooldownRemaining = 0f;
        }

        public bool TryBegin(bool alive, bool airborne)
        {
            if (!alive || !airborne || Hovering || !Armed || CooldownRemaining > 0f)
                return false;

            Hovering = true;
            Armed = false;
            HoverRemaining = Duration > 0f ? Duration : 0f;
            CooldownRemaining = CooldownDuration > 0f ? CooldownDuration : 0f;
            return true;
        }

        public void Tick(float deltaTime, bool grounded, bool knockedBack, bool alive)
        {
            if (deltaTime < 0f)
                deltaTime = 0f;

            if (!alive)
            {
                Reset();
                return;
            }

            if (CooldownRemaining > 0f)
            {
                CooldownRemaining -= deltaTime;
                if (CooldownRemaining < 0f)
                    CooldownRemaining = 0f;
            }

            if (knockedBack && Hovering)
            {
                Hovering = false;
                HoverRemaining = 0f;
            }

            if (Hovering)
            {
                HoverRemaining -= deltaTime;
                if (grounded || HoverRemaining <= 0f)
                {
                    Hovering = false;
                    HoverRemaining = 0f;
                }
            }

            if (grounded && !Hovering)
                Armed = true;
        }
    }
}
