using UnityEngine;

namespace KarmaPrototype
{
    // Seven distinct lanes, with an alternating order starting near the locked player position.
    public static class KarmaMeteorLayout
    {
        public static float LandingX(float targetX, int index)
        {
            int start = Mathf.Clamp(Mathf.RoundToInt((targetX - 2) / 6), 0, 6);
            int step = index % 7;
            int offset = step == 0 ? 0 : (step % 2 == 1 ? -(step + 1) / 2 : step / 2);
            int lane = (start + offset + 7) % 7;
            return 2 + lane * 6;
        }
    }
}
