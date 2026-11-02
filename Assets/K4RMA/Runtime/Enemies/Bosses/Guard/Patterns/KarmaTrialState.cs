using System;
namespace KarmaPrototype
{
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
}
