namespace K4RMA
{
    public readonly struct PiercingExit
    {
        public readonly float X;
        public readonly bool Found;
        public readonly bool Crossed;

        public PiercingExit(float x, bool found, bool crossed)
        {
            X = x;
            Found = found;
            Crossed = crossed;
        }
    }

    /// <summary>
    /// Close-range cut that crosses one contacted body. Not a projectile and not a free dash.
    /// </summary>
    public static class PiercingSlashRules
    {
        public static bool CanActivate(bool alive, bool slashActive, float cooldownRemaining)
        {
            return alive && !slashActive && cooldownRemaining <= 0f;
        }

        public static bool TryClaimTarget(bool alreadyClaimed)
        {
            return !alreadyClaimed;
        }

        public static float MissEndX(
            float startX,
            int facing,
            float distance,
            float playerHalfWidth,
            float arenaLeft,
            float arenaRight)
        {
            int travel = facing >= 0 ? 1 : -1;
            float pad = playerHalfWidth > 0f ? playerHalfWidth : 0f;
            float left = arenaLeft + pad;
            float right = arenaRight - pad;
            if (left > right)
                return startX;

            float desired = startX + travel * (distance > 0f ? distance : 0f);
            float clamped = Clamp(desired, left, right);
            if (travel > 0 && clamped < startX)
                clamped = Clamp(startX, left, right);
            if (travel < 0 && clamped > startX)
                clamped = Clamp(startX, left, right);
            return clamped;
        }

        public static PiercingExit Resolve(
            float approachX,
            int facing,
            float targetCenterX,
            float targetHalfWidth,
            float playerHalfWidth,
            float clearance,
            float arenaLeft,
            float arenaRight)
        {
            Sides(
                approachX,
                facing,
                targetCenterX,
                targetHalfWidth,
                playerHalfWidth,
                clearance,
                arenaLeft,
                arenaRight,
                out float farX,
                out bool farFits,
                out float nearX,
                out bool nearFits);
            if (farFits)
                return new PiercingExit(farX, true, true);
            if (nearFits)
                return new PiercingExit(nearX, true, false);
            return new PiercingExit(approachX, false, false);
        }

        public static void Sides(
            float approachX,
            int facing,
            float targetCenterX,
            float targetHalfWidth,
            float playerHalfWidth,
            float clearance,
            float arenaLeft,
            float arenaRight,
            out float farX,
            out bool farFits,
            out float nearX,
            out bool nearFits)
        {
            int travel = facing >= 0 ? 1 : -1;
            float half = targetHalfWidth > 0f ? targetHalfWidth : 0f;
            float pad = playerHalfWidth > 0f ? playerHalfWidth : 0f;
            float gap = clearance > 0f ? clearance : 0f;
            float left = arenaLeft + pad;
            float right = arenaRight - pad;
            farX = approachX;
            nearX = approachX;
            if (left > right)
            {
                farFits = false;
                nearFits = false;
                return;
            }

            farFits = TrySide(targetCenterX, travel, half, pad, gap, left, right, out farX);
            nearFits = TrySide(targetCenterX, -travel, half, pad, gap, left, right, out nearX);
        }

        static bool TrySide(
            float center,
            int side,
            float half,
            float pad,
            float gap,
            float left,
            float right,
            out float exitX)
        {
            float desired = center + side * (half + pad + gap);
            exitX = Clamp(desired, left, right);
            bool onSide = side > 0 ? exitX > center : exitX < center;
            float separation = exitX > center ? exitX - center : center - exitX;
            bool clear = separation + 0.0001f >= half + pad + gap;
            return onSide && clear;
        }

        static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
