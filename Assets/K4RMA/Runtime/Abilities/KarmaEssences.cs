using System;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Unity facade: persistent progression lives in Core/KarmaProgressionState.
    // The director only asks this component to sacrifice an essence.
    public sealed partial class KarmaEssences : MonoBehaviour
    {
        readonly KarmaProgressionState progression = new KarmaProgressionState();
        readonly float[] readyAt = new float[3];
        KarmaPlayer player;
        KarmaGame game;
        int swordHits;
        readonly int[] procCounts = new int[3];
        readonly float[] lastProc = { -100, -100, -100 };
        public int SwordHits { get { return swordHits; } }
        public int ProcCount(Essence e) { return procCounts[(int)e]; }
        public bool JustTriggered(Essence e) { return Time.time - lastProc[(int)e] < 0.7f; }
        public event Action<Essence> Sacrificed;
        public IReadOnlyList<Essence> SacrificeOrder { get { return progression.Order; } }
        public int Count { get { return progression.Count; } }
        public Essence? Pending { get { return progression.Pending; } }
        public bool Preview(Essence e) { return progression.Preview(e); }
        public void Cancel() { progression.Cancel(); }
        public bool Confirm(out Essence e)
        {
            if (!progression.Confirm(out e)) return false;
            Sacrificed?.Invoke(e); return true;
        }
        public bool HasRemnant(Essence e) { return progression.HasRemnant(e); }
        public bool HasActive(Essence e) { return !HasRemnant(e); }
        public float Cooldown(Essence e) { return Mathf.Max(0, readyAt[(int)e] - Time.time); }
        public void Initialize(KarmaPlayer owner, KarmaGame director) { player = owner; game = director; }
        public bool Sacrifice(Essence e)
        {
            if (!progression.TryInternalize(e)) return false;
            Sacrificed?.Invoke(e);
            return true;
        }
        public void ResetTemporaryState()
        {
            for (int i = 0; i < readyAt.Length; i++) readyAt[i] = 0;
            swordHits = 0;
            for (int i = 0; i < 3; i++) { procCounts[i] = 0; lastProc[i] = -100; }
        }

        // Triggered once per connected sword swing, not once per enemy.

    }
}
