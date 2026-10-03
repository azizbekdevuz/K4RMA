#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KarmaPrototype.Editor
{
    public static class KarmaChecks
    {
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("플레이 모드 밖에서 로직 검사를 실행하세요."); return; }
            int assertions = 0;
            Action<bool, string> check = (ok, message) =>
            {
                assertions++;
                if (!ok) throw new Exception("K4RMA CHECK FAILED: " + message);
            };
            // Exercise production sacrifice logic for every legal order.
            int[][] orders = {
                new[] {0,1,2}, new[] {0,2,1}, new[] {1,0,2},
                new[] {1,2,0}, new[] {2,0,1}, new[] {2,1,0}
            };
            foreach (var order in orders)
            {
                var go = new GameObject("K4RMA temporary logic test");
                try
                {
                    var state = go.AddComponent<KarmaEssences>();
                    int events = 0; state.Sacrificed += e => events++;
                    for (int step = 0; step < 3; step++)
                    {
                        var e = (Essence)order[step];
                        check(state.HasActive(e), "essence starts active");
                        check(state.Sacrifice(e), "first sacrifice succeeds");
                        check(!state.HasActive(e) && state.HasRemnant(e), "active becomes remnant");
                        check(!state.Sacrifice(e), "duplicate sacrifice rejected");
                        check(state.Count == step + 1, "no duplicate count");
                        check(state.SacrificeOrder[step] == e, "order preserved");
                        check(events == step + 1, "one event per sacrifice");
                        state.ResetTemporaryState();
                        check(state.Count == step + 1 && state.HasRemnant(e), "retry keeps permanent state");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            // All eight possession subsets: no fusion before both ingredients return.
            for (int mask = 0; mask < 8; mask++)
            {
                var returned = new List<Essence>();
                for (int bit = 0; bit < 3; bit++)
                    if ((mask & (1 << bit)) != 0) returned.Add((Essence)bit);
                var cycle = KarmaPatternCatalog.BuildCycle(returned);
                check(cycle.Contains(EnemyAttack.BlazingCharge) == ((mask & 3) == 3), "flame + dash recipe");
                check(cycle.Contains(EnemyAttack.GuardedCharge) == ((mask & 6) == 6), "dash + ward recipe");
                check(cycle.Contains(EnemyAttack.EmberAegis) == ((mask & 5) == 5), "flame + ward recipe");
                check(cycle.Contains(EnemyAttack.Overdrive) == (mask == 7), "triple fusion requires every essence");
                int n = returned.Count;
                check(cycle.Count == 2 + n + n * (n - 1) / 2 + (n == 3 ? 1 : 0), "one pattern per owned ability and fusion");
            }
            var duplicateCycle = KarmaPatternCatalog.BuildCycle(new[] { Essence.Flame, Essence.Flame, Essence.Dash });
            check(duplicateCycle.Count == 5, "duplicate ingredients do not duplicate attacks");
            check(KarmaProjectile.Intersects(Vector2.zero, new Vector2(20, 0), new Vector2(10, 0), .5f), "fast shot hits");
            check(!KarmaProjectile.Intersects(Vector2.zero, new Vector2(20, 0), new Vector2(10, 2), .5f), "near miss excluded");
            check(!KarmaProjectile.Intersects(Vector2.zero, new Vector2(20, 0), new Vector2(25, 0), .5f), "beyond endpoint excluded");
            check(KarmaProjectile.Intersects(Vector2.zero, Vector2.zero, new Vector2(.1f, 0), .5f), "stationary overlap");
            check(!KarmaProjectile.Intersects(Vector2.zero, Vector2.zero, Vector2.one, .5f), "stationary miss");
            var tuning = new KarmaBossTuning();
            for (int stage = 0; stage < 4; stage++)
                check(tuning.HealthFor(stage) > 1000, "long-run profile used for every trial");
            check(tuning.HealthFor(3) > tuning.HealthFor(2), "master has highest base health");
            check(tuning.MeteorCountFor(1) == 3 && tuning.MeteorCountFor(2) == 5
                && tuning.MeteorCountFor(3) == 7, "meteor count increases across phases");
            foreach (float target in new[] { -1f, 0.5f, 12f, 20f, 38f, 41f })
            {
                var lanes = new HashSet<float>();
                for (int i = 0; i < 7; i++)
                {
                    float x = KarmaMeteorLayout.LandingX(target, i);
                    check(x >= 2 && x <= 38, "meteor stays within arena lanes");
                    check(lanes.Add(x), "seven meteors have distinct positions even near walls");
                }
                var sorted = new List<float>(lanes); sorted.Sort();
                for (int i = 1; i < sorted.Count; i++)
                    check(sorted[i] - sorted[i - 1] > 2 * (tuning.meteorRadius + 0.5f),
                        "default blast radii leave a player-center gap");
            }
            var burn = new KarmaBurnState();
            float direction;
            check(burn.Remaining(0) == 0, "fresh player has no burn");
            burn.Apply(0, 3.6f, 0.6f, -1);
            check(burn.ConsumeTick(0, out direction) && direction == -1, "first hit burns immediately away from source");
            burn.Apply(0.1f, 3.6f, 0.6f, 1);
            check(!burn.ConsumeTick(0.1f, out direction), "refresh does not create another immediate tick");
            check(!burn.ConsumeTick(0.59f, out direction), "tick cooldown respected");
            check(burn.ConsumeTick(0.6f, out direction) && direction == 1, "periodic tick uses latest source direction");
            check(!burn.ConsumeTick(3.71f, out direction), "expired burn stops damage and knockback");
            burn.Apply(4, 3.6f, 0.6f, 1); burn.Clear();
            check(!burn.ConsumeTick(4, out direction) && burn.Remaining(4) == 0, "death or room reset clears burn");
            burn.Apply(10, 3.6f, 0.6f, 1);
            foreach (float offset in new[] { 0f, 0.61f, 1.22f, 1.83f, 2.44f, 3.05f })
                check(burn.ConsumeTick(10 + offset, out direction), "six periodic ticks without reapplication");
            check(!burn.ConsumeTick(13.7f, out direction), "standalone burn is finite");
            Debug.Log("K4RMA: " + assertions + "개 로직 검사 통과. 이동·화면 표시·밸런스는 실제 플레이로 확인하세요.");
        
        }
    }
}
#endif
