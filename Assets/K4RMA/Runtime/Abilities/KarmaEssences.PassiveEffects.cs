using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEssences
    {
        public void OnSwordConnected() { swordHits++; }
        public void NotifyUnique(Essence e) { ShowProc(e); }
        void ShowProc(Essence e)
        {
            procCounts[(int)e]++; lastProc[(int)e] = Time.time;
            KarmaRemnantVFX.Proc(game, player.Position, player.Facing, e);
        }
    }
}
