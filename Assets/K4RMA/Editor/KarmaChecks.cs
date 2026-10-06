#if UNITY_EDITOR
using System;
using UnityEngine;
namespace KarmaPrototype.Editor
{
    public static class KarmaChecks
    {
        public static void Run()
        {
            int count = 0;
            Action<bool, string> check = (ok, msg) => { count++; if (!ok) throw new Exception(msg); };
            int[][] orders = { new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0} };
            foreach (var order in orders)
            {
                var go = new GameObject("temporary rule test");
                try
                {
                    var state = go.AddComponent<KarmaEssences>(); int events = 0;
                    state.Sacrificed += e => events++;
                    for (int i = 0; i < 3; i++)
                    {
                        Essence e; check(state.Preview((Essence)order[i]), "preview");
                        check(state.Count == i, "preview is reversible");
                        check(state.Confirm(out e) && (int)e == order[i], "confirm");
                        check(events == i + 1 && state.Count == i + 1, "one event per confirm");
                        check(KarmaPatternCatalog.Primary(state.SacrificeOrder) == (Essence)order[0], "stable primary");
                        state.ResetTemporaryState(); check(state.Count == i + 1, "room transition keeps inheritance");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            check(KarmaProjectile.Intersects(Vector2.zero, new Vector2(20,0), new Vector2(10,0),.5f), "swept hit");
            check(!KarmaProjectile.Intersects(Vector2.zero, new Vector2(20,0),new Vector2(10,2),.5f), "swept miss");
            Debug.Log("K4RMA: " + count + " Unity logic assertions passed");
        }
    }
}
#endif
