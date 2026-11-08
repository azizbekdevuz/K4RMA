using System;

namespace KarmaPrototype
{
    // SW03 only. World-space intervals include the whole player body, not just its pivot.
    internal static class KarmaSwordScarPlanner
    {
        public const int CellCount = 16, ScarCount = 13;
        public const float Clearance = 0.55f, ReactionSeconds = 0.28f;

        public static float Center(float left, float right, int cell)
        { return left + (cell + 0.5f) * (right - left) / CellCount; }
        public static float DangerCenter(float left, float right, int cell, float width)
        { return Math.Max(left + width / 2, Math.Min(right - width / 2, Center(left, right, cell))); }

        public static bool TryPlan(Random random, float left, float right, float scarWidth,
            float playerX, float playerHalfWidth, float speed, float warning,
            Func<float, bool> clearPath, out int[] cells, out float safeX, out float seconds)
        {
            cells = null; safeX = playerX; seconds = warning;
            if (right <= left || scarWidth <= 0 || scarWidth > (right - left) / CellCount + 0.001f) return false;
            var order = new int[CellCount];
            for (int i = 0; i < CellCount; i++) order[i] = i;
            for (int i = 0; i < CellCount; i++)
            {
                int pick = random.Next(i, CellCount), temp = order[i];
                order[i] = order[pick]; order[pick] = temp;
            }
            // Three single-cell channels spaced five/five/six cells apart.
            // Rotate and reflect that readable formation, rather than scattering
            // random holes or accidentally joining them into a large safe zone.
            float best = float.PositiveInfinity;
            for (int attempt = 0; attempt < CellCount * 2; attempt++)
            {
                int anchor = order[attempt / 2];
                int gap2 = (anchor + 5) % CellCount, gap3 = (anchor + (attempt % 2 == 0 ? 10 : 11)) % CellCount;
                var candidate = new int[ScarCount];
                int count = 0;
                for (int cell = 0; cell < CellCount; cell++)
                    if (cell != anchor && cell != gap2 && cell != gap3) candidate[count++] = cell;
                if (!TrySafePoint(candidate, left, right, scarWidth, playerX, playerHalfWidth, clearPath, out float target)) continue;
                float needed = RequiredWarning(playerX, target, speed);
                if (needed <= warning && (speed <= 0.001f || Math.Abs(target - playerX) > 0.5f))
                { cells = candidate; safeX = target; return true; }
                if (needed >= best) continue;
                best = needed; cells = candidate; safeX = target;
            }
            if (cells == null) return false;
            seconds = Math.Max(warning, best);
            return true;
        }

        static float RequiredWarning(float from, float to, float speed)
        {
            float distance = Math.Abs(to - from);
            return distance < 0.001f ? ReactionSeconds : speed > 0.001f
                ? ReactionSeconds + distance / speed : float.PositiveInfinity;
        }

        static bool TrySafePoint(int[] cells, float left, float right, float width,
            float playerX, float half, Func<float, bool> clearPath, out float safeX)
        {
            float inset = Math.Max(0, half) + Clearance;
            float cursor = left + inset, limit = right - inset, best = float.PositiveInfinity;
            safeX = playerX;
            for (int i = 0; i <= cells.Length; i++)
            {
                float end = i == cells.Length ? limit : Math.Min(limit, DangerCenter(left, right, cells[i], width) - width / 2 - inset);
                if (end >= cursor)
                {
                    float target = Math.Max(cursor, Math.Min(end, playerX));
                    float travel = Math.Abs(target - playerX);
                    if (travel < best && (clearPath == null || clearPath(target)))
                    { best = travel; safeX = target; }
                }
                if (i < cells.Length) cursor = Math.Max(cursor, DangerCenter(left, right, cells[i], width) + width / 2 + inset);
            }
            return !float.IsPositiveInfinity(best);
        }
    }
}
