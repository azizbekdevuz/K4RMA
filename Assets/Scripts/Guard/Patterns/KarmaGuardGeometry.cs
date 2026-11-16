using System;
namespace KarmaPrototype
{
    public static class KarmaGuardGeometry
    {
        public static bool SweptCircle(float ax, float ay, float bx, float by, float radius,
            float px, float py, float halfWidth, float halfHeight)
        {
            // The rounded rectangle is two strips plus the four corner circles.
            if (KarmaBossGeometry.SweptBox(ax, ay, bx, by, px, py, halfWidth + radius, halfHeight)
                || KarmaBossGeometry.SweptBox(ax, ay, bx, by, px, py, halfWidth, halfHeight + radius)) return true;
            float vx = bx - ax, vy = by - ay, length = vx * vx + vy * vy;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                float dx = px + x * halfWidth - ax, dy = py + y * halfHeight - ay;
                float t = length > 0 ? Math.Max(0, Math.Min(1, (dx * vx + dy * vy) / length)) : 0;
                dx -= t * vx; dy -= t * vy;
                if (dx * dx + dy * dy <= radius * radius) return true;
            }
            return false;
        }
    }
}
