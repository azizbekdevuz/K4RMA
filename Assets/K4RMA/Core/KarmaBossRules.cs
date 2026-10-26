using System;
namespace KarmaPrototype
{
    // Shared by shield fragments: one contact attempt per cast, never a stun chain.
    public sealed class KarmaAttackContact
    {
        public bool Consumed { get; private set; }
        public bool DealtDamage { get; private set; }
        public bool TryConsume() { if (Consumed) return false; Consumed = true; return true; }
        public void RecordDamage() { DealtDamage = true; }
    }
    public sealed class KarmaTrialState
    {
        public bool Active { get; private set; }
        public int Hits { get; private set; }
        public int Completed { get; private set; }
        public int Total { get; private set; }
        public int Allowed { get; private set; }
        public bool Finished { get { return Active && Completed >= Total; } }
        public bool Passed { get { return Finished && Hits <= Allowed; } }
        public void Begin(int total, int allowed)
        { Total = Math.Max(1, total); Allowed = Math.Max(0, allowed); Hits = Completed = 0; Active = true; }
        public void Hit() { if (Active && !Finished) Hits++; }
        public void CompleteAttack() { if (Active && !Finished) Completed++; }
        public void Clear() { Active = false; Hits = Completed = Total = Allowed = 0; }
    }
    public static class KarmaBossGeometry
    {
        // Segment versus expanded AABB: exact swept rectangular attack, including target size.
        public static bool SweptBox(float ax, float ay, float bx, float by,
            float px, float py, float halfWidth, float halfHeight)
        {
            float first = 0, last = 1;
            return Axis(ax - px, bx - ax, halfWidth, ref first, ref last)
                && Axis(ay - py, by - ay, halfHeight, ref first, ref last);
        }
        static bool Axis(float origin, float delta, float extent, ref float first, ref float last)
        {
            if (Math.Abs(delta) < 0.000001f) return Math.Abs(origin) <= extent;
            float a = (-extent - origin) / delta, b = (extent - origin) / delta;
            first = Math.Max(first, Math.Min(a, b)); last = Math.Min(last, Math.Max(a, b));
            return first <= last;
        }
    }
}
